using UnityEngine;



public sealed partial class EnemyController
{
    [SerializeField] private Transform AnimationAnimationRoot;
    [SerializeField] private Animator AnimationAnimator;
    [SerializeField] private Transform AnimationLockPoint;

    private bool animationRelayEnabled = true;
    private EnemyController AnimationEnemyPhysicsMotor;
    private CombatTimeDomain AnimationTimeDomain;
    private int AnimationCinematicSessionToken = -1;

    public override Transform ActorRoot => transform;
    public override Transform AnimationRoot => AnimationAnimationRoot;
    public override CombatTimeDomain TimeDomain => AnimationTimeDomain;
    public override CombatActorAnimatorContractMode AnimatorContractMode => AnimationAnimator != null && AnimationAnimator.transform == transform
        ? CombatActorAnimatorContractMode.RootAnimator
        : CombatActorAnimatorContractMode.LegacyChildAnimator;
    public override bool UsesRootAnimator => AnimationAnimator != null && AnimationAnimator.transform == transform;
    public override bool IsCinematicMotionActive => AnimationCinematicSessionToken >= 0;
    public override bool ShouldConsumeAnimatorRootMotion => IsCinematicMotionActive ||
                                                   (AnimationEnemyPhysicsMotor != null && AnimationEnemyPhysicsMotor.IsDrivingActionRootMotion);

    private void AnimationOnDisable()
    {
        AnimationCinematicSessionToken = -1;
        GetComponent<LitOpsiveLocomotionBridge>()?.EnforceGameplayMotionAuthority();
    }



    private void AnimationAwake()
    {
        AnimationResolveReferences();
        if (AnimationTimeDomain == null)
        {
            AnimationTimeDomain = gameObject.AddComponent<CombatTimeDomain>();
        }
        AnimationLogDevelopmentContractDiagnostic();
    }

    private void AnimationLateUpdate()
    {
        // AnimationRoot is purely visual. Some imported clips animate this
        // transform even while applyRootMotion is disabled (notably Hit), which
        // moves the whole mesh away from ActorRoot. World movement must always
        // be handled by the relay and its explicit receiver instead.
        if (AnimationAnimationRoot != null && AnimationAnimationRoot != transform &&
            (AnimationAnimationRoot.localPosition.sqrMagnitude > 0.000001f ||
             Quaternion.Angle(AnimationAnimationRoot.localRotation, Quaternion.identity) > 0.01f))
        {
            ResetAnimationRootPose();
        }
    }

#if UNITY_EDITOR

#endif

    public override void Configure(Transform configuredAnimationRoot, Animator configuredAnimator, Transform configuredLockPoint)
    {
        AnimationAnimationRoot = configuredAnimationRoot;
        AnimationAnimator = configuredAnimator;
        ActorAnimator = configuredAnimator;
        ActorEnemyLockPoint = configuredLockPoint;
        AnimationLockPoint = configuredLockPoint;
        AnimationResolveReferences();
    }

    private bool ValidateAnimationContract(out string error)
    {
        AnimationResolveReferences();
        if (AnimationAnimationRoot == null)
        {
            error = name + ": AnimationRoot manquant.";
            return false;
        }

        if (AnimationAnimationRoot != transform && AnimationAnimationRoot.parent != transform)
        {
            error = name + ": AnimationRoot doit etre le root acteur ou son enfant direct.";
            return false;
        }

        if (AnimationAnimationRoot != transform &&
            (AnimationAnimationRoot.localPosition.sqrMagnitude > 0.000001f ||
             Quaternion.Angle(AnimationAnimationRoot.localRotation, Quaternion.identity) > 0.01f ||
             (AnimationAnimationRoot.localScale - Vector3.one).sqrMagnitude > 0.000001f))
        {
            error = name + ": AnimationRoot doit conserver une pose locale identite.";
            return false;
        }

        if (AnimationAnimator == null || AnimationAnimator.runtimeAnimatorController == null)
        {
            error = name + ": Animator de gameplay valide manquant.";
            return false;
        }

        if (AnimationAnimator.transform != AnimationAnimationRoot && !AnimationAnimator.transform.IsChildOf(AnimationAnimationRoot))
        {
            error = name + ": Animator doit etre porte par AnimationRoot ou sa hierarchie.";
            return false;
        }

        if (AnimationAnimator.transform != transform) { error = "Animator ennemi requis sur la racine"; return false; }
        error = null;
        return true;
    }

    public override bool SetActorPose(Vector3 position, Quaternion rotation)
    {
        if (TryGetComponent(out LitOpsiveLocomotionBridge bridge))
        {
            return bridge.SetCinematicPositionAndRotation(position, rotation, true, false);
        }

        if (TryGetComponent(out EnemyController enemyBehaviour))
        {
            return enemyBehaviour.PlaceForCinematic(position, rotation);
        }

        transform.SetPositionAndRotation(position, rotation);
        Physics.SyncTransforms();
        return true;
    }

    public override void ResetAnimationRootPose()
    {
        if (AnimationAnimationRoot == null || AnimationAnimationRoot == transform)
        {
            return;
        }

        AnimationAnimationRoot.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        AnimationAnimationRoot.localScale = Vector3.one;
    }

    public override void BeginCinematicMotion(int sessionToken)
    {
        AnimationCinematicSessionToken = sessionToken;
        AnimationResolveReferences();
        GetComponent<PlayerStateMotionController>()?.Cancel();
        GetComponent<LitOpsiveLocomotionBridge>()?.EnforceGameplayMotionAuthority();
        if (true)
        {
            animationRelayEnabled = true;
        }
    }

    /// <summary>
    /// A Timeline using scene offsets already owns the actor transform. In that
    /// case its Animator deltas must not be applied a second time by the relay.
    /// </summary>
    public override void SetCinematicRootMotionRelayEnabled(bool enabled)
    {
        if (true)
        {
            animationRelayEnabled = enabled;
        }
    }

    public override void EnableRootMotionRelay()
    {
        if (true)
        {
            animationRelayEnabled = true;
        }
    }

    public override void EndCinematicMotion(int sessionToken)
    {
        if (AnimationCinematicSessionToken != sessionToken)
        {
            return;
        }

        AnimationCinematicSessionToken = -1;
        GetComponent<LitOpsiveLocomotionBridge>()?.EnforceGameplayMotionAuthority();
        if (true)
        {
            animationRelayEnabled = false;
        }
        ResetAnimationRootPose();
    }

    public override void ApplyAnimationDelta(Vector3 worldDeltaPosition, Quaternion deltaRotation)
    {
        if (!IsCinematicMotionActive && AnimationEnemyPhysicsMotor != null && AnimationEnemyPhysicsMotor.ScriptedOnly) return;
        if (AnimationEnemyPhysicsMotor != null && AnimationEnemyPhysicsMotor.IsDrivingActionRootMotion)
        {
            AnimationEnemyPhysicsMotor.ApplyActionRootMotion(worldDeltaPosition, deltaRotation);
            return;
        }

        if (!IsCinematicMotionActive)
        {
            return;
        }

        if (TryGetComponent(out LitOpsiveLocomotionBridge bridge))
        {
            bridge.ApplyCinematicRootMotion(worldDeltaPosition, deltaRotation);
            return;
        }

        if (TryGetComponent(out EnemyController enemyBehaviour))
        {
            enemyBehaviour.ApplyCinematicRootMotion(worldDeltaPosition, deltaRotation);
            return;
        }

        transform.SetPositionAndRotation(transform.position + worldDeltaPosition, deltaRotation * transform.rotation);
        Physics.SyncTransforms();
    }

    private void AnimationResolveReferences()
    {
        // Root Animator is the current authoring convention. It owns the same
        // transform as physics, navigation and combat, preventing a visual
        // hierarchy from silently becoming a second movement authority.
        Animator rootAnimator = GetComponent<Animator>();
        if (rootAnimator != null)
        {
            AnimationAnimationRoot = transform;
            AnimationAnimator = rootAnimator;
        }
        else
        {
            // Legacy prefabs keep their child Animator contract unchanged.
            if (AnimationAnimationRoot == null)
            {
                AnimationAnimationRoot = transform;
            }

            if (AnimationAnimator == null)
            {
                AnimationAnimator = AnimationAnimationRoot.GetComponent<Animator>();
                if (AnimationAnimator == null)
                {
                    AnimationAnimator = AnimationAnimationRoot.GetComponentInChildren<Animator>(true);
                }
            }
        }

        if (AnimationLockPoint == null)
        {
            AnimationLockPoint = transform.Find("EnemyLockPoint");
        }



        AnimationTimeDomain ??= GetComponent<CombatTimeDomain>();

        AnimationEnemyPhysicsMotor ??= GetComponent<EnemyController>();
    }

    private void AnimationLogDevelopmentContractDiagnostic()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!ValidateAnimationContract(out string contractError))
        {
            Debug.LogError("[CombatAnimatorContract] " + contractError, this);
            return;
        }

        string controllerName = AnimationAnimator.runtimeAnimatorController != null
            ? AnimationAnimator.runtimeAnimatorController.name
            : "<aucun>";
        Debug.Log("[CombatAnimatorContract] actor='" + name + "' mode='" + AnimatorContractMode + "' AnimationAnimator='" + AnimationAnimator.name + "' controller='" + controllerName + "'.", this);

        if (GetComponent<EnemyController>() != null)
        {
            AnimationValidateRequiredEnemyStates();
        }
#endif
    }

    private void AnimationValidateRequiredEnemyStates()
    {
        // The unified combat controller uses CombatIdle. Keep Idle as a
        // fallback for older enemy controllers, but do not report a false
        // contract failure when the new explicit combat state is present.
        string idleState = AnimationAnimator.HasState(0, Animator.StringToHash("Base Layer.CombatIdle"))
            ? "CombatIdle"
            : "Idle";
        AnimationValidateAnimatorState(idleState);
        AnimationValidateAnimatorState("Hit");
        AnimationValidateAnimatorState("Death");

        // A state is required only when this enemy can actually play the
        // corresponding SkillSO. GiantJuggernaut has its own skill set and
        // controller, so requiring Juggernaut's "Assomoir" state on every
        // enemy was a false contract failure.
        EnemyController enemySkills = GetComponent<EnemyController>();
        if (enemySkills == null)
        {
            return;
        }

        foreach (SkillSO skill in enemySkills.Skills)
        {
            if (skill == null)
            {
                continue;
            }

            string stateName = string.IsNullOrWhiteSpace(skill.AnimatorState)
                ? (skill.AnimationClip != null ? skill.AnimationClip.name : null)
                : skill.AnimatorState;
            if (!string.IsNullOrWhiteSpace(stateName))
            {
                AnimationValidateAnimatorState(stateName);
            }
        }
    }

    private void AnimationValidateAnimatorState(string stateName)
    {
        int shortHash = Animator.StringToHash(stateName);
        int fullPathHash = Animator.StringToHash("Base Layer." + stateName);
        if (AnimationAnimator.HasState(0, shortHash) || AnimationAnimator.HasState(0, fullPathHash))
        {
            return;
        }

        Debug.LogError("[CombatAnimatorContract] actor='" + name + "' controller='" + AnimationAnimator.runtimeAnimatorController.name + "' missing required state='" + stateName + "'.", this);
    }
}
