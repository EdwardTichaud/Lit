using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>Dialogue requests complete poses from the existing action presenter. Never edits bones or layers.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(150)]
public sealed class CharacterDialoguePresentationController : NetworkBehaviour
{
    // Historical names remain discoverable by migration tools; no runtime layer uses them.
    [SerializeField] private DialoguePresentationProfile fallbackProfile;
    private readonly NetworkVariable<DialoguePresentationState> replicated = new(default,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private SquadCharacterController squad;
    private CharacterAnimationController animationController;
    private PlayerActionPresentationController action;
    private LitOpsiveLocomotionBridge bridge;
    private FacialExpressionController face;
    private Animator animator;
    private LitOpsiveLocomotionBridge.ExternalLockHandle motionLock;
    private Object sessionOwner;
    private CharacterData sessionCharacter;
    private Transform controlledAtBegin, listener;
    private DialoguePresentationState state, lastPublished;
    private DialogueDirection direction;
    private DialogueRole role;
    private int generation, selected = -1, faceToken, shownRevision = -1, presentedRevision = -1;
    private uint variation;
    private bool explicitGesturePlayed, presenting;
    private float gestureElapsed, releaseRemaining;

    public CharacterData CharacterData => squad != null ? squad.CharacterData : GetComponent<CharacterInfo>()?.CharacterData;
    public DialoguePresentationProfile Profile => CharacterData != null ? CharacterData.playerSettings?.dialoguePresentation : fallbackProfile;
    public bool IsEngaged => state.active;
    public bool HasPresentationWeight => presenting;
    public bool OwnsFullBody => presenting;
    public bool HasFullBodyWeight => presenting;
    public int CurrentGesture => state.active ? state.gesture : -1;
    public int Generation => state.generation;
    public string Diagnostic => $"{name} dialogue generation={state.generation} owner={sessionOwner?.name} gesture={state.gesture} active={state.active} presenting={presenting}";

    private void Awake() => Resolve();
    private void Resolve()
    {
        if (squad == null) squad = GetComponent<SquadCharacterController>();
        if (animationController == null) animationController = GetComponent<CharacterAnimationController>();
        if (action == null) action = GetComponent<PlayerActionPresentationController>();
        if (bridge == null) bridge = GetComponent<LitOpsiveLocomotionBridge>();
        if (face == null) face = GetComponentInChildren<FacialExpressionController>(true);
        if (animator == null) animator = animationController != null ? animationController.Animator : GetComponent<Animator>();
    }
    public override void OnNetworkSpawn()
    {
        replicated.OnValueChanged += OnReplication;
        if (!IsOwner) OnReplication(default, replicated.Value);
    }
    public override void OnNetworkDespawn() { replicated.OnValueChanged -= OnReplication; ResetSession(true); }
    public override void OnLostOwnership() { ResetSession(true); OnReplication(default, replicated.Value); }
    public override void OnGainedOwnership() => ResetSession(true);
    private void OnReplication(DialoguePresentationState previous, DialoguePresentationState current)
    {
        if (IsOwner) return;
        if (previous.generation != current.generation) { shownRevision = -1; presentedRevision = -1; }
        state = current;
        listener = null;
        if (current.listenerId != 0 && NetworkManager != null &&
            NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(current.listenerId, out var target)) listener = target.transform;
    }
    public int Begin(Object owner, Transform target, bool stationary, DialogueDirection requestedDirection, DialogueRole requestedRole)
    {
        Resolve();
        if (owner == null || !isActiveAndEnabled || Profile == null || IsSpawned && !IsOwner) return 0;
        generation++;
        if (!stationary) ReleaseMotion();
        sessionOwner = owner;
        sessionCharacter = CharacterData;
        controlledAtBegin = LocalPlayerContext.LocalCharacterRoot;
        listener = target;
        direction = requestedDirection ?? new DialogueDirection();
        role = requestedRole;
        explicitGesturePlayed = false; presentedRevision = -1;
        releaseRemaining = 0;
        state = new DialoguePresentationState { active = true, stationary = stationary, bodySuppressed = true,
            seated = squad != null && squad.IsSittingRequested, generation = generation,
            intent = requestedRole == DialogueRole.Speaking ? direction.intent : DialogueIntent.Neutral,
            intensity = Mathf.Clamp(direction.intensity, .25f, 1) };
        var net = target != null ? target.GetComponentInParent<NetworkObject>() : null;
        if (net != null && net.IsSpawned) state.listenerId = net.NetworkObjectId;
        var actor = target != null ? target.GetComponentInParent<Lit.Story.StorySequenceActor>() : null;
        if (actor != null && !string.IsNullOrEmpty(actor.ActorId) && actor.ActorId.Length <= 60)
            state.listenerActorId = new FixedString64Bytes(actor.ActorId);
        SelectGesture(); Publish(); return generation;
    }
    public void FinishSpeaking(int token)
    {
        if (token == 0 || !state.active || token != state.generation || role != DialogueRole.Speaking) return;
        role = DialogueRole.Waiting; state.intent = DialogueIntent.Neutral; SelectGesture(); Publish();
    }
    public void End(int token) { if (token != 0 && token == state.generation) ResetSession(); }
    private void SelectGesture()
    {
        selected = Profile.Choose(role, direction.intent, state.seated,
            role == DialogueRole.Speaking && !explicitGesturePlayed ? direction.gestureId : null, selected,
            unchecked((uint)generation * 2654435761u + ++variation * 2246822519u));
        if (role == DialogueRole.Speaking) explicitGesturePlayed = true;
        state.gesture = selected; state.revision++; gestureElapsed = 0;
        state.startedAt = IsSpawned ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
        shownRevision = -1;
    }
    private static readonly string[] AllowedStates = { "Locomotion", "CombatLocomotion", "CombatIdle", "Idle",
        "Sitting_Idle", "Mixamo_Sitting_Idle", "Walk_Start", "Run_Start", "Walk_Stop", "Run_Stop", "WalkStartToRun" };
    private static bool IsBaseState(AnimatorStateInfo info)
    {
        foreach (string name in AllowedStates) if (info.shortNameHash == Animator.StringToHash(name)) return true;
        return false;
    }
    private bool IsBlocked()
    {
        if (Profile == null || animator == null || !animator.isActiveAndEnabled || !animator.isInitialized ||
            IsSpawned && !IsOwner && state.suspended || squad != null && squad.CurrentHp <= 0 ||
            action != null && (!action.isActiveAndEnabled || action.IsActionActive || action.IsDeathAnimationLocked) ||
            animationController != null && animationController.IsCinematicMotionActive ||
            bridge != null && ((!IsSpawned || IsOwner) && !bridge.Grounded || bridge.IsCinematicMotionSessionActive || bridge.IsScriptedTraversalActive)) return true;
        if (presenting && action != null && action.HasDialoguePresentation) return false;
        return !IsBaseState(animator.GetCurrentAnimatorStateInfo(0)) ||
            animator.IsInTransition(0) && !IsBaseState(animator.GetNextAnimatorStateInfo(0));
    }
    private void Update() => Refresh(animationController?.TimeDomain != null ? animationController.TimeDomain.DeltaTime : Time.deltaTime);
    public void Refresh(float delta)
    {
        Resolve(); delta = Mathf.Max(0, delta);
        bool local = !IsSpawned || IsOwner;
        if (releaseRemaining > 0) { releaseRemaining -= delta; if (releaseRemaining <= 0) ReleaseMotion(); }
        if (local && state.active && (sessionOwner == null || sessionCharacter != CharacterData ||
            sessionOwner is Behaviour b && !b.isActiveAndEnabled || sessionOwner is GameObject g && !g.activeInHierarchy ||
            controlledAtBegin == transform && LocalPlayerContext.LocalCharacterRoot != transform ||
            squad != null && squad.CurrentHp <= 0)) ResetSession(true);
        if (!state.active) { ReleasePose(false); ReleaseFace(false); return; }
        if (local && squad != null && state.seated != squad.IsSittingRequested)
        { state.seated = squad.IsSittingRequested; SelectGesture(); Publish(); }
        bool blocked = IsBlocked();
        if (local && state.suspended != blocked) { state.suspended = blocked; Publish(); }
        if (blocked) { ReleasePose(true); ReleaseMotion(); ReleaseFace(true); return; }
        if (local && state.stationary && !state.seated && motionLock == null && squad != null)
            squad.TryBeginUccProgressiveStop(this, out motionLock, true, false);
        if (local)
        {
            gestureElapsed += delta;
            if (role == DialogueRole.Speaking && gestureElapsed >= NextGestureSeconds()) { SelectGesture(); Publish(); }
        }
        if (listener != null && motionLock != null && bridge != null)
        {
            Vector3 towards = Vector3.ProjectOnPlane(listener.position - transform.position, transform.up);
            if (towards.sqrMagnitude > .01f) bridge.SetActionFacingDirection(Vector3.RotateTowards(transform.forward,
                towards.normalized, Profile.facingDegreesPerSecond * Mathf.Deg2Rad * delta, 0));
        }
        var gesture = state.gesture >= 0 && state.gesture < Profile.gestures.Count ? Profile.gestures[state.gesture] : null;
        if (shownRevision != state.revision)
        {
            ReleaseFace(false);
            var expression = Profile.ExpressionFor(state.intent, gesture);
            if (face != null && expression != null) faceToken = face.BeginDialogueExpression(this, expression);
            shownRevision = state.revision;
        }
        bool moving = bridge != null && (bridge.PlanarVelocity.sqrMagnitude > .0025f || !state.stationary && bridge.HasLocomotionIntent);
        bool bodySuppressed = moving || gesture?.body == null || action == null;
        if (local && state.bodySuppressed != bodySuppressed) { state.bodySuppressed = bodySuppressed; Publish(); }
        if (local ? bodySuppressed : state.bodySuppressed) { ReleasePose(false); return; }
        int hash = Animator.StringToHash("Base Layer.Dialogue_" + gesture.id);
        bool same = presenting && (animator.GetCurrentAnimatorStateInfo(0).fullPathHash == hash ||
            animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).fullPathHash == hash);
        if (same && presentedRevision == state.revision) return;
        float elapsed = IsSpawned && !IsOwner ? Mathf.Max(0, (float)(NetworkManager.ServerTime.Time - state.startedAt)) : 0;
        float offset = gesture.startNormalizedTime * gesture.body.length + elapsed;
        offset = gesture.body.isLooping ? Mathf.Repeat(offset, gesture.body.length) : Mathf.Min(offset, gesture.body.length);
        presenting = action.PresentDialogue(this, hash, Profile.blendSeconds, offset);
        if (presenting) presentedRevision = state.revision;
    }
    private float NextGestureSeconds()
    {
        var clip = state.gesture >= 0 && state.gesture < Profile.gestures.Count ? Profile.gestures[state.gesture].body : null;
        return clip == null || clip.isLooping ? Profile.speechVariationSeconds : Mathf.Min(Profile.speechVariationSeconds,
            Mathf.Max(.5f, clip.length * (1 - Profile.gestures[state.gesture].startNormalizedTime) - Profile.blendSeconds));
    }
    private void Publish() { if (IsSpawned && IsOwner && !state.Equals(lastPublished)) { replicated.Value = state; lastPublished = state; } }
    private void ReleaseMotion() { motionLock?.Dispose(); motionLock = null; releaseRemaining = 0; }
    private void ReleaseFace(bool immediate) { if (face != null) face.EndDialogueExpression(faceToken, immediate); faceToken = 0; shownRevision = -1; }
    private void ReleasePose(bool priority)
    {
        if (!presenting) return;
        action?.ReleaseDialogue(this, Profile != null ? Profile.blendSeconds : .2f, !priority);
        presenting = false;
    }
    public void SuspendForPriority() { ReleasePose(true); ReleaseMotion(); ReleaseFace(true); if (state.active) { state.suspended = true; Publish(); } }
    private void ResetSession(bool immediate = false)
    {
        bool wasPresenting = presenting;
        ReleasePose(immediate); state.active = false; sessionOwner = null; shownRevision = -1;
        if (immediate || !wasPresenting) ReleaseMotion();
        else releaseRemaining = Profile != null ? Profile.blendSeconds : .2f;
        ReleaseFace(immediate); Publish();
    }
    private void OnEnable() => UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneChanged;
    private void OnSceneChanged(UnityEngine.SceneManagement.Scene previous, UnityEngine.SceneManagement.Scene next) => ResetSession(true);
    private void OnDisable() { UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnSceneChanged; ResetSession(true); }
}
