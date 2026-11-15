using UnityEngine;
using Unity.Netcode;
using Unity.Collections;



[DisallowMultipleComponent]
public sealed class PlayerAnimationController : CharacterAnimationController
{
    [SerializeField] private Transform animationRoot;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform lockPoint;

    private PlayerRootMotionRelay rootMotionRelay;
    private CombatTimeDomain timeDomain;
    private int cinematicSessionToken = -1;
    private static readonly int ClingHoldState = UnityEngine.Animator.StringToHash("Base Layer.Cling_Hold");
    private static readonly int LocomotionState = UnityEngine.Animator.StringToHash("Base Layer.Locomotion");
    private static readonly int IdleState = UnityEngine.Animator.StringToHash("Base Layer.Idle");
    private static readonly int CombatIdleState = UnityEngine.Animator.StringToHash("Base Layer.CombatIdle");
    private readonly NetworkVariable<FixedString128Bytes> clingSupport = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private bool showingClingPose;
    private Lit.Traversal.ClingPoint localClingPoint;
    private readonly Lit.Traversal.ClingPoint.AnimatedContacts clingContacts = new();
    private Opsive.UltimateCharacterController.Character.CharacterIK suspendedClingIk;
    internal bool CanPresentCling => animator != null && animator.isActiveAndEnabled &&
        animator.isHuman && animator.HasState(0, ClingHoldState);

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        clingSupport.OnValueChanged += OnClingSupportChanged;
        if (clingSupport.Value.Length > 0) ApplyClingPose(true);
    }

    public override void OnNetworkDespawn()
    {
        clingSupport.OnValueChanged -= OnClingSupportChanged;
        localClingPoint = null;
        ApplyClingPose(false);
        base.OnNetworkDespawn();
    }

    internal void SetClingPose(bool value, Lit.Traversal.ClingPoint support = null)
    {
        localClingPoint = value ? support : null;
        ApplyClingPose(value);
        if (IsSpawned && IsOwner)
            clingSupport.Value = localClingPoint != null ? new FixedString128Bytes(localClingPoint.PointId) : default;
    }

    private void OnClingSupportChanged(FixedString128Bytes previous, FixedString128Bytes current)
    {
        if (!IsOwner)
        {
            localClingPoint = null;
            clingContacts.point = null;
        }
        ApplyClingPose(current.Length > 0);
    }

    internal void ApplyClingIK(int layerIndex)
    {
        if (layerIndex != 0 || !showingClingPose || animator == null ||
            !animator.isHuman || animator.IsInTransition(0) ||
            animator.GetCurrentAnimatorStateInfo(0).fullPathHash != ClingHoldState) return;
        if (localClingPoint == null && IsSpawned && !IsOwner && clingSupport.Value.Length > 0)
            localClingPoint = Lit.Traversal.ClingPoint.Find(clingSupport.Value.ToString());
        if (localClingPoint != null && TryGetComponent(out SquadCharacterController actor))
            localClingPoint.FitAnimatedContacts(actor, animator, clingContacts);
    }

    internal static bool IsClingAnimation(Animator target)
    {
        return target != null && (target.GetCurrentAnimatorStateInfo(0).fullPathHash == ClingHoldState ||
            target.IsInTransition(0) && target.GetNextAnimatorStateInfo(0).fullPathHash == ClingHoldState);
    }

    private void ApplyClingPose(bool value)
    {
        if (showingClingPose == value || animator == null || !animator.HasState(0, ClingHoldState)) return;
        showingClingPose = value;
        clingContacts.point = null;
        if (value)
        {
            // The Animator lives on the model; callback messages do not reach the player root.
            if (!animator.TryGetComponent(out Lit.Traversal.ClingIkRelay clingRelay))
                clingRelay = animator.gameObject.AddComponent<Lit.Traversal.ClingIkRelay>();
            clingRelay.Owner = this;
            // UCC's ground solver competes for the same base-layer feet and pelvis.
            if (animator.TryGetComponent(out Opsive.UltimateCharacterController.Character.CharacterIK groundIk) && groundIk.enabled)
            {
                suspendedClingIk = groundIk;
                groundIk.enabled = false;
            }
            if (animator.isActiveAndEnabled) animator.CrossFadeInFixedTime(ClingHoldState, .1f, 0);
            return;
        }
        RestoreClingIk();
        var actor = GetComponent<SquadCharacterController>();
        if (IsCinematicMotionActive || (actor != null && actor.CurrentHp <= 0)) return;
        if (animator.isActiveAndEnabled && animator.HasState(0, LocomotionState) && IsClingAnimation(animator))
            animator.CrossFadeInFixedTime(LocomotionState, .12f, 0);
    }

    public override Transform ActorRoot => transform;
    public override Transform AnimationRoot => animationRoot;
    public override Animator Animator => animator;
    public override Transform LockPoint => lockPoint;
    public override CombatTimeDomain TimeDomain => timeDomain;
    public override CombatActorAnimatorContractMode AnimatorContractMode => animator != null && animator.transform == transform
        ? CombatActorAnimatorContractMode.RootAnimator
        : CombatActorAnimatorContractMode.LegacyChildAnimator;
    public override bool UsesRootAnimator => animator != null && animator.transform == transform;
    public override bool IsCinematicMotionActive => cinematicSessionToken >= 0;
    public override bool ShouldConsumeAnimatorRootMotion => IsCinematicMotionActive;

    private void RestoreClingIk()
    {
        if (suspendedClingIk != null) suspendedClingIk.enabled = true;
        suspendedClingIk = null;
    }

    private void OnDisable()
    {
        clingContacts.point = null;
        RestoreClingIk();
        showingClingPose = false;
        localClingPoint = null;
        cinematicSessionToken = -1;
        GetComponent<LitOpsiveLocomotionBridge>()?.EnforceGameplayMotionAuthority();
    }

    private void Reset()
    {
        animationRoot = transform;
        animator = GetComponent<Animator>();
        lockPoint = transform.Find("EnemyLockPoint");
    }

    protected override void Awake()
    {
        ResolveReferences();
        base.Awake();
        if (timeDomain == null)
        {
            timeDomain = gameObject.AddComponent<CombatTimeDomain>();
        }
        LogDevelopmentContractDiagnostic();
    }

    private void LateUpdate()
    {
        // AnimationRoot is purely visual. Some imported clips animate this
        // transform even while applyRootMotion is disabled (notably Hit), which
        // moves the whole mesh away from ActorRoot. World movement must always
        // be handled by the relay and its explicit receiver instead.
        if (animationRoot != null && animationRoot != transform &&
            (animationRoot.localPosition.sqrMagnitude > 0.000001f ||
             Quaternion.Angle(animationRoot.localRotation, Quaternion.identity) > 0.01f))
        {
            ResetAnimationRootPose();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveReferences();
    }
#endif

    public override void Configure(Transform configuredAnimationRoot, Animator configuredAnimator, Transform configuredLockPoint)
    {
        animationRoot = configuredAnimationRoot;
        animator = configuredAnimator;
        lockPoint = configuredLockPoint;
        ResolveReferences();
        EnsureAnimationEventReceiver();
    }

    public override bool ValidateContract(out string error)
    {
        ResolveReferences();
        if (animationRoot == null)
        {
            error = name + ": AnimationRoot manquant.";
            return false;
        }

        if (animationRoot != transform && animationRoot.parent != transform)
        {
            error = name + ": AnimationRoot doit etre le root acteur ou son enfant direct.";
            return false;
        }

        if (animationRoot != transform &&
            (animationRoot.localPosition.sqrMagnitude > 0.000001f ||
             Quaternion.Angle(animationRoot.localRotation, Quaternion.identity) > 0.01f ||
             (animationRoot.localScale - Vector3.one).sqrMagnitude > 0.000001f))
        {
            error = name + ": AnimationRoot doit conserver une pose locale identite.";
            return false;
        }

        if (animator == null || animator.runtimeAnimatorController == null)
        {
            error = name + ": Animator de gameplay valide manquant.";
            return false;
        }

        if (animator.transform != animationRoot && !animator.transform.IsChildOf(animationRoot))
        {
            error = name + ": Animator doit etre porte par AnimationRoot ou sa hierarchie.";
            return false;
        }

        if (rootMotionRelay == null)
        {
            error = name + ": PlayerRootMotionRelay manquant sur l'Animator de gameplay.";
            return false;
        }

        if (rootMotionRelay.transform != animator.transform)
        {
            error = name + ": PlayerRootMotionRelay doit etre porte par le meme GameObject que l'Animator.";
            return false;
        }

        error = null;
        return true;
    }

    public override bool SetActorPose(Vector3 position, Quaternion rotation)
    {
        if (TryGetComponent(out LitOpsiveLocomotionBridge bridge))
        {
            return bridge.SetCinematicPositionAndRotation(position, rotation, true, false);
        }

        transform.SetPositionAndRotation(position, rotation);
        Physics.SyncTransforms();
        return true;
    }

    public override void ResetAnimationRootPose()
    {
        if (animationRoot == null || animationRoot == transform)
        {
            return;
        }

        animationRoot.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        animationRoot.localScale = Vector3.one;
    }

    public override void BeginCinematicMotion(int sessionToken)
    {
        cinematicSessionToken = sessionToken;
        ResolveReferences();
        GetComponent<PlayerStateMotionController>()?.Cancel();
        GetComponent<LitOpsiveLocomotionBridge>()?.EnforceGameplayMotionAuthority();
        if (rootMotionRelay != null)
        {
            rootMotionRelay.enabled = true;
        }
    }

    internal void PrepareCinematicIdle()
    {
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null ||
            GetComponent<PlayerActionPresentationController>()?.IsDeathAnimationLocked == true ||
            (TryGetComponent(out SquadCharacterController actor) && actor.CurrentHp <= 0))
        {
            return;
        }

        bool combatLocked = GetComponent<LitOpsiveLocomotionBridge>()?.IsCombatLockActive == true;
        int state = combatLocked && animator.HasState(0, CombatIdleState) ? CombatIdleState :
            animator.HasState(0, IdleState) ? IdleState : LocomotionState;
        if (!animator.HasState(0, state))
        {
            return;
        }

        foreach (var parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Float &&
                parameter.name is "Speed" or "LitSpeed" or "LocomotionTier" or "HorizontalMovement" or "ForwardMovement" or
                    "CombatMoveMagnitude" or "Turn" or "MovingLegsX" or "MovingLegsZ" or "MovingLegsSpeed" or
                    "MovingLegsMagnitude" or "MovingLegsTier")
            {
                animator.SetFloat(parameter.nameHash, 0f);
            }
            else if (parameter.type == AnimatorControllerParameterType.Bool && parameter.name is "Moving" or "IsMoving")
            {
                animator.SetBool(parameter.nameHash, false);
            }
        }

        int movingLegsLayer = animator.GetLayerIndex(PlayerMovingLegsOverlay.LayerName);
        if (movingLegsLayer > 0)
        {
            animator.SetLayerWeight(movingLegsLayer, 0f);
        }

        // Sample the idle pose before the Timeline creates or evaluates its animation graph.
        animator.Play(state, 0, 0f);
        animator.Update(0f);
    }

    /// <summary>
    /// A Timeline using scene offsets already owns the actor transform. In that
    /// case its Animator deltas must not be applied a second time by the relay.
    /// </summary>
    public override void SetCinematicRootMotionRelayEnabled(bool enabled)
    {
        if (rootMotionRelay != null)
        {
            rootMotionRelay.enabled = enabled;
        }
    }

    public override void EnableRootMotionRelay()
    {
        if (rootMotionRelay != null)
        {
            rootMotionRelay.enabled = true;
        }
    }

    public override void EndCinematicMotion(int sessionToken)
    {
        if (cinematicSessionToken != sessionToken)
        {
            return;
        }

        cinematicSessionToken = -1;
        GetComponent<LitOpsiveLocomotionBridge>()?.EnforceGameplayMotionAuthority();
        if (rootMotionRelay != null)
        {
            rootMotionRelay.enabled = false;
        }
        ResetAnimationRootPose();
    }

    public override void ApplyAnimationDelta(Vector3 worldDeltaPosition, Quaternion deltaRotation)
    {
        if (!IsCinematicMotionActive)
        {
            return;
        }

        if (TryGetComponent(out LitOpsiveLocomotionBridge bridge))
        {
            bridge.ApplyCinematicRootMotion(worldDeltaPosition, deltaRotation);
            return;
        }

        transform.SetPositionAndRotation(transform.position + worldDeltaPosition, deltaRotation * transform.rotation);
        Physics.SyncTransforms();
    }

    private void ResolveReferences()
    {
        // Root Animator is the current authoring convention. It owns the same
        // transform as physics, navigation and combat, preventing a visual
        // hierarchy from silently becoming a second movement authority.
        Animator rootAnimator = GetComponent<Animator>();
        if (rootAnimator != null)
        {
            animationRoot = transform;
            animator = rootAnimator;
        }
        else
        {
            // Legacy prefabs keep their child Animator contract unchanged.
            if (animationRoot == null)
            {
                animationRoot = transform;
            }

            if (animator == null)
            {
                animator = animationRoot.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = animationRoot.GetComponentInChildren<Animator>(true);
                }
            }
        }

        if (lockPoint == null)
        {
            lockPoint = transform.Find("EnemyLockPoint");
        }

        if (rootMotionRelay == null && animator != null)
        {
            rootMotionRelay = animator.GetComponent<PlayerRootMotionRelay>();
        }

        timeDomain ??= GetComponent<CombatTimeDomain>();


    }

    private void LogDevelopmentContractDiagnostic()
    {
#if UNITY_EDITOR || DEBUG
        if (!ValidateContract(out string contractError))
        {
            Debug.LogError("[CombatAnimatorContract] " + contractError, this);
            return;
        }

        string controllerName = animator.runtimeAnimatorController != null
            ? animator.runtimeAnimatorController.name
            : "<aucun>";
        Debug.Log("[CombatAnimatorContract] actor='" + name + "' mode='" + AnimatorContractMode + "' animator='" + animator.name + "' controller='" + controllerName + "'.", this);

#endif
    }

}
