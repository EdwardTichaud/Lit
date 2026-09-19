using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Character.Abilities;
using UnityEngine;

// Keeps Lit interaction detection authoritative while respecting UCC locomotion state.
[DisallowMultipleComponent]
public class LitUccInteractionBridge : MonoBehaviour
{
    private readonly PlayerModuleConfiguration<PlayerInteractionSettings> moduleConfiguration = new PlayerModuleConfiguration<PlayerInteractionSettings>();
    private PlayerInteractionSettings ModuleSettings => moduleConfiguration.Resolve(this, data => data.interactions);

    private LitOpsiveLocomotionBridge locomotionBridge;
    private UltimateCharacterLocomotion locomotion;
    private bool requireGroundedForLitInteractions { get => ModuleSettings.requireGroundedForLitInteractions; set => ModuleSettings.requireGroundedForLitInteractions = value; }
    private bool allowWhileHeightChange { get => ModuleSettings.allowWhileHeightChange; set => ModuleSettings.allowWhileHeightChange = value; }
    private bool allowWhileSpeedChange { get => ModuleSettings.allowWhileSpeedChange; set => ModuleSettings.allowWhileSpeedChange = value; }
    private bool allowWhileFall { get => ModuleSettings.allowWhileFall; set => ModuleSettings.allowWhileFall = value; }
    private bool allowWhileJump { get => ModuleSettings.allowWhileJump; set => ModuleSettings.allowWhileJump = value; }
    private bool blockWhileItemAbilityActive { get => ModuleSettings.blockWhileItemAbilityActive; set => ModuleSettings.blockWhileItemAbilityActive = value; }

    public bool CanEvaluateLitInteractions
    {
        get
        {
            if (!isActiveAndEnabled)
            {
                return true;
            }

            ResolveReferences();
            if (locomotionBridge != null && locomotionBridge.IsInputSuppressedByUcc)
            {
                return false;
            }

            if (locomotion == null)
            {
                return true;
            }

            if (requireGroundedForLitInteractions && !locomotion.Grounded)
            {
                return false;
            }

            return !HasBlockingActiveAbility() && !HasBlockingActiveItemAbility();
        }
    }

    public bool CanUseLitInteractable(ICharacterDetectedInteractable target)
    {
        return target != null && CanEvaluateLitInteractions;
    }

    public string GetInteractionBlockReason()
    {
        if (!isActiveAndEnabled) return "none";
        ResolveReferences();
        if (locomotionBridge != null && locomotionBridge.IsInputSuppressedByUcc)
            return "UCC lock: external=" + locomotionBridge.IsExternalLockActive +
                   ", traversal=" + locomotionBridge.IsScriptedTraversalActive;
        if (locomotion == null) return "none";
        if (requireGroundedForLitInteractions && !locomotion.Grounded) return "not grounded";
        if (locomotion.ActiveAbilities != null)
            for (int i = 0; i < Mathf.Min(locomotion.ActiveAbilityCount, locomotion.ActiveAbilities.Length); i++)
            {
                Ability ability = locomotion.ActiveAbilities[i];
                if (ability != null && !IsAllowedConcurrentAbility(ability)) return ability.GetType().Name;
            }
        return HasBlockingActiveItemAbility() ? "UCC item ability" : "none";
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (locomotionBridge == null)
        {
            locomotionBridge = GetComponent<LitOpsiveLocomotionBridge>();
        }

        if (locomotion == null)
        {
            locomotion = GetComponent<UltimateCharacterLocomotion>();
        }
    }

    private bool HasBlockingActiveAbility()
    {
        if (locomotion.ActiveAbilityCount <= 0 || locomotion.ActiveAbilities == null)
        {
            return false;
        }

        int count = Mathf.Min(locomotion.ActiveAbilityCount, locomotion.ActiveAbilities.Length);
        for (int i = 0; i < count; i++)
        {
            Ability ability = locomotion.ActiveAbilities[i];
            if (ability == null)
            {
                continue;
            }

            if (IsAllowedConcurrentAbility(ability))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool HasBlockingActiveItemAbility()
    {
        return blockWhileItemAbilityActive &&
               locomotion.ActiveItemAbilityCount > 0 &&
               locomotion.ActiveItemAbilities != null;
    }

    private bool IsAllowedConcurrentAbility(Ability ability)
    {
        if (ability is Idle ||
            ability is AlignToGround ||
            ability is AlignUpDirection ||
            ability is QuickStart ||
            ability is QuickStop ||
            ability is QuickTurn ||
            // This UCC integration hook is deliberately automatic and
            // concurrent. It only supplies motion while an authored action
            // owns PlayerStateMotionController, so its idle presence must not
            // disable every Lit interaction and runtime outline.
            ability is LitUccStateMotionAbility)
        {
            return true;
        }

        if (ability is HeightChange)
        {
            return allowWhileHeightChange;
        }

        if (ability is SpeedChange)
        {
            return allowWhileSpeedChange;
        }

        if (ability is Fall)
        {
            return allowWhileFall;
        }

        if (ability is Jump)
        {
            return allowWhileJump;
        }

        return false;
    }
}
