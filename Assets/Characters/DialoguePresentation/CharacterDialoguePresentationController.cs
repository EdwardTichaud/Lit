using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Opsive.UltimateCharacterController.Character;

/// <summary>Owns only dialogue presentation. The character motor remains the physical authority.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(150)]
public sealed class CharacterDialoguePresentationController : NetworkBehaviour
{
    public static readonly string[] Layers = { "Dialogue Body", "Dialogue Upper Body", "Dialogue Free Arm" };
    [SerializeField] private DialoguePresentationProfile fallbackProfile;
    private readonly NetworkVariable<DialoguePresentationState> replicated = new(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private SquadCharacterController squad;
    private CharacterAnimationController animationController;
    private PlayerActionPresentationController action;
    private LitOpsiveLocomotionBridge bridge;
    private FacialExpressionController face;
    private CharacterIK ik;
    private Animator animator;
    private RuntimeAnimatorController boundController;
    private readonly int[] layers = { -1, -1, -1 };
    private readonly float[] weights = new float[3];
    private LitOpsiveLocomotionBridge.ExternalLockHandle motionLock;
    private UnityEngine.Object sessionOwner;
    private CharacterData sessionCharacter;
    private Transform controlledAtBegin;
    private Transform listener;
    private DialoguePresentationState state;
    private DialogueDirection direction;
    private DialogueRole role;
    private int generation, selected = -1, faceToken;
    private uint variation;
    private bool explicitGesturePlayed;
    private float gestureElapsed;
    private bool ownsLook;
    private float previousHeadWeight, previousBodyWeight;
    private int lastVisualGesture = -1, lastVisualRevision = -1;
    private bool ownsGroundSuppression;
    private DialoguePresentationState lastPublished;

    public CharacterData CharacterData => squad != null ? squad.CharacterData :
        GetComponent<CharacterInfo>()?.CharacterData;
    public DialoguePresentationProfile Profile => CharacterData != null ?
        CharacterData.playerSettings?.dialoguePresentation : fallbackProfile;
    public bool IsEngaged => state.active;
    public bool HasPresentationWeight => weights[0] > .001f || weights[1] > .001f || weights[2] > .001f;
    public bool OwnsFullBody => state.active && !IsBlocked() && (motionLock != null || IsSpawned && !IsOwner) && state.stationary && !state.seated &&
        squad != null && !squad.IsFlameEquipped && state.gesture >= 0 && state.gesture < Profile.gestures.Count && Profile.gestures[state.gesture].body != null && bridge != null && bridge.PlanarVelocity.sqrMagnitude < .0025f;
    public bool HasFullBodyWeight => weights[0] > .001f;
    public int CurrentGesture => state.active ? state.gesture : -1;
    public int Generation => state.generation;
    public string Diagnostic => $"{name} dialogue generation={state.generation} owner={sessionOwner?.name} gesture={state.gesture} active={state.active}";

    private void Awake()
    {
        squad = GetComponent<SquadCharacterController>();
        animationController = GetComponent<CharacterAnimationController>();
        action = GetComponent<PlayerActionPresentationController>();
        bridge = GetComponent<LitOpsiveLocomotionBridge>();
        face = GetComponentInChildren<FacialExpressionController>(true);
        ik = GetComponentInChildren<CharacterIK>(true);
        ResolveAnimator();
    }

    public override void OnNetworkSpawn()
    {
        replicated.OnValueChanged += OnReplication;
        if (!IsOwner) OnReplication(default, replicated.Value);
    }

    public override void OnNetworkDespawn()
    {
        replicated.OnValueChanged -= OnReplication;
        ResetSession(true);
        SetWeights(-1, 0, true);
    }

    public override void OnLostOwnership()
    {
        ResetSession(true);
        SetWeights(-1, 0, true);
        OnReplication(default, replicated.Value);
    }

    public override void OnGainedOwnership() => ResetSession(true);

    private void OnReplication(DialoguePresentationState previous, DialoguePresentationState current)
    {
        if (IsOwner) return;
        state = current;
        listener = null;
        if (current.listenerId != 0 && NetworkManager != null &&
            NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(current.listenerId, out NetworkObject target))
            listener = target.transform;
        if (listener == null && current.listenerActorId.Length > 0)
        {
            foreach (var actor in FindObjectsByType<Lit.Story.StorySequenceActor>(FindObjectsInactive.Exclude))
                if (actor.Matches(current.listenerActorId.ToString())) { listener = actor.transform; break; }
        }
    }

    public int Begin(UnityEngine.Object owner, Transform target, bool stationary,
        DialogueDirection requestedDirection, DialogueRole requestedRole)
    {
        if (owner == null || !isActiveAndEnabled || Profile == null || (IsSpawned && !IsOwner))
            return 0;
        // Invalidate former callbacks before releasing the former owner's commands.
        generation++;
        if (!state.stationary || !stationary || squad != null && squad.IsSittingRequested) ReleaseMotion();
        sessionOwner = owner;
        sessionCharacter = CharacterData;
        controlledAtBegin = LocalPlayerContext.LocalCharacterRoot;
        listener = target;
        direction = requestedDirection ?? new DialogueDirection();
        role = requestedRole;
        explicitGesturePlayed = false;
        variation++;
        state = new DialoguePresentationState
        {
            active = true, stationary = stationary,
            seated = squad != null && squad.IsSittingRequested,
            generation = generation, intent = requestedRole == DialogueRole.Speaking ? direction.intent : DialogueIntent.Neutral, intensity = Mathf.Clamp(direction.intensity, .25f, 1f)
        };
        var targetNetwork = target != null ? target.GetComponentInParent<NetworkObject>() : null;
        if (targetNetwork != null && targetNetwork.IsSpawned) state.listenerId = targetNetwork.NetworkObjectId;
        var targetActor = target != null ? target.GetComponentInParent<Lit.Story.StorySequenceActor>() : null;
        if (targetActor != null && !string.IsNullOrEmpty(targetActor.ActorId) && targetActor.ActorId.Length <= 60)
            state.listenerActorId = new FixedString64Bytes(targetActor.ActorId);
        SelectGesture();
        Publish();
        return generation;
    }

    public void FinishSpeaking(int token)
    {
        if (token == 0 || !state.active || token != state.generation || role != DialogueRole.Speaking) return;
        role = DialogueRole.Waiting;
        state.intent = DialogueIntent.Neutral;
        SelectGesture();
        Publish();
    }

    public void End(int token)
    {
        if (token == 0 || token != state.generation) return;
        ResetSession();
    }

    private void SelectGesture()
    {
        int next = Profile.Choose(role, direction.intent, state.seated,
            role == DialogueRole.Speaking && !explicitGesturePlayed ? direction.gestureId : null, selected,
            unchecked((uint)generation * 2654435761u + ++variation * 2246822519u));
        if (role == DialogueRole.Speaking) explicitGesturePlayed = true;
        state.gesture = selected = next;
        gestureElapsed = 0;
        state.revision++;
        state.startedAt = IsSpawned ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
    }

    private static readonly string[] AllowedStates =
    {
        "Locomotion", "CombatLocomotion", "CombatIdle", "Idle", "Sitting_Idle",
        "Mixamo_Sitting_Idle", "Walk_Start", "Run_Start", "Walk_Stop", "Run_Stop",
        "WalkStartToRun", "Turn_L90", "Turn_R90", "Turn_L180", "Turn_R180"
    };

    private static bool AllowsDialogue(AnimatorStateInfo state)
    {
        foreach (string name in AllowedStates)
            if (state.shortNameHash == Animator.StringToHash(name)) return true;
        return false;
    }

    private bool IsBlocked()
    {
        return IsSpawned && !IsOwner && state.suspended || Profile == null || animator == null || !animator.isActiveAndEnabled || !animator.isInitialized ||
            !AllowsDialogue(animator.GetCurrentAnimatorStateInfo(0)) ||
            animator.IsInTransition(0) && !AllowsDialogue(animator.GetNextAnimatorStateInfo(0)) ||
            squad != null && squad.CurrentHp <= 0 ||
            action != null && (action.IsActionActive || action.IsDeathAnimationLocked) ||
            animationController != null && animationController.IsCinematicMotionActive ||
            bridge != null && (bridge.IsCinematicMotionSessionActive || bridge.IsScriptedTraversalActive || !bridge.Grounded);
    }

    private void Update() => Refresh(animationController?.TimeDomain != null ? animationController.TimeDomain.DeltaTime : Time.deltaTime);

    /// <summary>Single actor-domain presentation step, also used by deterministic native-rig validation.</summary>
    public void Refresh(float delta)
    {
        delta = Mathf.Max(0, delta);
        ResolveAnimator();
        bool local = !IsSpawned || IsOwner;
        if (local && state.active && (squad != null && squad.CurrentHp <= 0 ||
            action != null && action.IsDeathAnimationLocked))
        {
            ResetSession(true);
            SetWeights(-1, 0, true);
        }
        if (local && state.active && (sessionOwner == null ||
            sessionOwner is Behaviour behaviour && !behaviour.isActiveAndEnabled ||
            sessionOwner is GameObject go && !go.activeInHierarchy))
            ResetSession();
        if (state.active && (Profile == null || local && (sessionCharacter != CharacterData ||
            controlledAtBegin == transform && LocalPlayerContext.LocalCharacterRoot != transform))) ResetSession();
        if (state.active && local && squad != null && state.seated != squad.IsSittingRequested)
        {
            state.seated = squad.IsSittingRequested;
            SelectGesture();
            Publish();
        }

        bool blocked = IsBlocked();
        if (local && state.active && state.suspended != blocked)
        {
            state.suspended = blocked;
            Publish();
        }
        if (blocked)
        {
            ReleaseMotion();
            ReleaseLook();
            ReleaseFace(true);
            SetWeights(-1, 0, true);
            lastVisualGesture = -1;
            return;
        }
        if (state.active && state.stationary && !state.seated && local && motionLock == null && squad != null)
            squad.TryBeginUccProgressiveStop(this, out motionLock, true, false);
        if (!state.active)
        {
            SetWeights(-1, delta, false);
            if (!HasFullBodyWeight) ReleaseMotion();
            ReleaseLook();
            ReleaseFace();
            return;
        }
        if (local)
        {
            gestureElapsed += delta;
            if (role == DialogueRole.Speaking && gestureElapsed >= NextGestureSeconds())
            {
                SelectGesture();
                Publish();
            }
        }
        FaceListener(delta);
        int mode = squad != null && squad.IsFlameEquipped ? 2 : OwnsFullBody ? 0 : 1;
        var gestures = Profile.gestures;
        if (state.gesture < 0 || state.gesture >= gestures.Count)
        {
            SetWeights(-1, delta, false);
            return;
        }
        var gesture = gestures[state.gesture];
        if (lastVisualGesture != state.gesture || lastVisualRevision != state.revision)
        {
            for (int i = 0; i < layers.Length; i++)
            {
                int hash = Animator.StringToHash(Layers[i] + "." + gesture.id);
                if (layers[i] >= 0 && animator.HasState(layers[i], hash))
                {
                    var clip = i == 0 ? gesture.body : gesture.upperBody;
                    float elapsed = IsSpawned && !IsOwner ? Mathf.Max(0, (float)(NetworkManager.ServerTime.Time - state.startedAt)) : 0;
                    float requestedOffset = elapsed + (clip != null ? gesture.startNormalizedTime * clip.length : 0);
                    float offset = clip != null && clip.isLooping ? Mathf.Repeat(requestedOffset, clip.length) :
                        Mathf.Min(requestedOffset, clip != null ? clip.length : 0);
                    animator.CrossFadeInFixedTime(hash, Profile.blendSeconds, layers[i], offset);
                }
            }
            lastVisualGesture = state.gesture;
            lastVisualRevision = state.revision;
            ReleaseFace();
            var expression = Profile.ExpressionFor(state.intent, gesture);
            if (face != null && expression != null)
                faceToken = face.BeginDialogueExpression(this, expression);
        }
        int activeHash = Animator.StringToHash(Layers[mode] + "." + gesture.id);
        SetWeights(layers[mode] >= 0 && animator.HasState(layers[mode], activeHash) ? mode : -1, delta, false);
    }

    private float NextGestureSeconds()
    {
        if (state.gesture < 0 || state.gesture >= Profile.gestures.Count) return Profile.speechVariationSeconds;
        var clip = Profile.gestures[state.gesture].upperBody;
        return clip == null || clip.isLooping ? Profile.speechVariationSeconds :
            Mathf.Min(Profile.speechVariationSeconds, Mathf.Max(.5f, clip.length * (1 - Profile.gestures[state.gesture].startNormalizedTime) - Profile.blendSeconds));
    }

    private void FaceListener(float delta)
    {
        if (listener == null) { ReleaseLook(); return; }
        Vector3 towards = Vector3.ProjectOnPlane(listener.position - transform.position, Vector3.up);
        if ((!IsSpawned || IsOwner) && state.stationary && !state.seated && motionLock != null && towards.sqrMagnitude > .01f)
        {
            Vector3 forward = Vector3.RotateTowards(transform.forward, towards.normalized,
                Profile.facingDegreesPerSecond * Mathf.Deg2Rad * delta, 0);
            bridge.SetActionFacingDirection(forward);
        }
        if (ik == null || towards.sqrMagnitude < .01f) return;
        var head = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        if (head == null) return;
        var targetAnimator = listener.GetComponentInParent<CharacterAnimationController>()?.Animator;
        var targetHead = targetAnimator != null && targetAnimator.isHuman ? targetAnimator.GetBoneTransform(HumanBodyBones.Head) : null;
        Vector3 aim = targetHead != null ? targetHead.position : listener.position + Vector3.up * 1.5f;
        if (Vector3.Angle(transform.forward, towards) > 70) { ReleaseLook(); return; }
        if (!ownsLook)
        {
            previousHeadWeight = ik.LookAtHeadWeight;
            previousBodyWeight = ik.LookAtBodyWeight;
            ownsLook = true;
        }
        ik.LookAtHeadWeight = Profile.headLookWeight;
        ik.LookAtBodyWeight = 0;
        ik.SetLookAtPosition(true, aim);
    }

    private void ResolveAnimator()
    {
        if (animator == null) animator = animationController != null ? animationController.Animator : GetComponent<Animator>();
        if (animator == null || boundController == animator.runtimeAnimatorController) return;
        boundController = animator.runtimeAnimatorController;
        for (int i = 0; i < layers.Length; i++) { layers[i] = animator.GetLayerIndex(Layers[i]); weights[i] = 0; }
        lastVisualGesture = -1;
    }

    private void SetWeights(int desired, float delta, bool immediate)
    {
        if (animator == null) return;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] < 0 || layers[i] >= animator.layerCount) continue;
            float target = desired == i ? state.intensity : 0;
            // Full-body poses always completely replace the base pose.
            if (desired == 0 && i == 0) target = 1;
            weights[i] = immediate ? target : Mathf.MoveTowards(weights[i], target,
                delta / Mathf.Max(.05f, Profile != null ? Profile.blendSeconds : .25f));
            animator.SetLayerWeight(layers[i], weights[i]);
        }
        if (ik != null)
        {
            if (HasFullBodyWeight && !ik.LowerBodyGroundingSuppressed)
            {
                ik.LowerBodyGroundingSuppressed = true;
                ownsGroundSuppression = true;
            }
            else if (!HasFullBodyWeight && ownsGroundSuppression)
            {
                ik.LowerBodyGroundingSuppressed = false;
                ownsGroundSuppression = false;
            }
        }
    }

    private void Publish()
    {
        if (IsSpawned && IsOwner && !state.Equals(lastPublished)) { replicated.Value = state; lastPublished = state; }
    }

    private void ReleaseMotion() { motionLock?.Dispose(); motionLock = null; }
    private void ReleaseFace(bool immediate = false) { if (face != null) face.EndDialogueExpression(faceToken, immediate); faceToken = 0; }
    private void ReleaseLook()
    {
        if (!ownsLook || ik == null) return;
        if (Profile != null && Mathf.Approximately(ik.LookAtHeadWeight, Profile.headLookWeight))
        {
            ik.LookAtHeadWeight = previousHeadWeight;
            ik.LookAtBodyWeight = previousBodyWeight;
            ik.SetLookAtPosition(false, Vector3.zero);
        }
        ownsLook = false;
    }

    public void SuspendForPriority()
    {
        ReleaseMotion();
        ReleaseLook();
        ReleaseFace(true);
        SetWeights(-1, 0, true);
        lastVisualGesture = -1;
        if (state.active) { state.suspended = true; Publish(); }
    }

    private void ResetSession(bool immediate = false)
    {
        state.active = false;
        sessionOwner = null;
        lastVisualGesture = -1;
        if (immediate || !HasFullBodyWeight) ReleaseMotion();
        ReleaseLook();
        ReleaseFace(immediate);
        Publish();
    }

    private void OnEnable() => UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneChanged;
    private void OnSceneChanged(UnityEngine.SceneManagement.Scene previous, UnityEngine.SceneManagement.Scene next)
    {
        ResetSession(true);
        SetWeights(-1, 0, true);
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnSceneChanged;
        ResetSession(true);
        SetWeights(-1, 0, true);
    }
}
