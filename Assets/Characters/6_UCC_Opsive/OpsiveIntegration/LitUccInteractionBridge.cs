using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Character.Abilities;
using UnityEngine;

// Keeps Lit interaction detection authoritative while respecting UCC locomotion state.
[DisallowMultipleComponent]
public class LitUccInteractionBridge : MonoBehaviour
{
    [SerializeField] private LitOpsiveLocomotionBridge locomotionBridge;
    [SerializeField] private UltimateCharacterLocomotion locomotion;
    [SerializeField, Tooltip("Prevent Lit interactions while UCC reports the character as airborne.")]
    private bool requireGroundedForLitInteractions = true;
    [SerializeField, Tooltip("Allow Lit interactions while UCC crouch/HeightChange is active.")]
    private bool allowWhileHeightChange = true;
    [SerializeField, Tooltip("Allow Lit interactions while UCC SpeedChange is active.")]
    private bool allowWhileSpeedChange = true;
    [SerializeField, Tooltip("Allow Lit interactions while UCC Fall is active.")]
    private bool allowWhileFall;
    [SerializeField, Tooltip("Allow Lit interactions while UCC Jump is active.")]
    private bool allowWhileJump;
    [SerializeField, Tooltip("Block Lit interactions while any UCC item ability is active.")]
    private bool blockWhileItemAbilityActive = true;

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
            ability is QuickTurn)
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
