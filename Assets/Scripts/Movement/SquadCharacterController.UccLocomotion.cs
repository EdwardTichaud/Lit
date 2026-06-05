using UnityEngine;

public partial class SquadCharacterController
{
    private LitOpsiveLocomotionBridge uccLocomotionBridge;
    private LitUccInteractionBridge uccInteractionBridge;

    public bool HasUccLocomotionBridge => GetUccLocomotionBridge() != null;
    public bool IsUccLocomotionActive => TryGetActiveUccLocomotionBridge(out _);

    private bool TryForwardMoveToUcc(Vector2 input, bool isWorldSpace)
    {
        if (!TryGetActiveUccLocomotionBridge(out LitOpsiveLocomotionBridge bridge))
        {
            return false;
        }

        Vector2 worldInput = isWorldSpace ? input : GetWorldSpaceInput(input);
        bridge.SetMoveInput(worldInput, isWorldSpace: true);
        ApplyMovementFacialNeutral(ResolveMovementInputMagnitude(input));
        if (input.sqrMagnitude <= movementInputDeadZone * movementInputDeadZone || isWorldSpace)
        {
            ClearStoredMovementReference();
        }

        return true;
    }

    private bool TryForwardSprintToUcc(bool pressed)
    {
        if (!TryGetActiveUccLocomotionBridge(out LitOpsiveLocomotionBridge bridge))
        {
            return false;
        }

        bridge.SetSprintModifier(pressed);
        return true;
    }

    private bool TryForwardJumpToUcc()
    {
        if (!TryGetActiveUccLocomotionBridge(out LitOpsiveLocomotionBridge bridge))
        {
            return false;
        }

        Vector2 jumpWorldInput = hasQueuedCommittedJumpInput
            ? queuedCommittedJumpInputIsWorldSpace ? queuedCommittedJumpInput : GetWorldSpaceInput(queuedCommittedJumpInput)
            : moveInputIsWorldSpace ? moveInput : GetWorldSpaceInput(moveInput);
        bool accepted = bridge.Jump(jumpWorldInput, jumpWorldInput.sqrMagnitude > 0.0001f);
        if (accepted)
        {
            ClearQueuedCommittedJumpInput();
        }

        return accepted;
    }

    private bool TryForwardStopToUcc()
    {
        if (!TryGetActiveUccLocomotionBridge(out LitOpsiveLocomotionBridge bridge))
        {
            return false;
        }

        bridge.StopBridgeInput();
        return true;
    }

    public bool TryToggleUccHeightChange()
    {
        if (!TryGetActiveUccLocomotionBridge(out LitOpsiveLocomotionBridge bridge))
        {
            return false;
        }

        return bridge.ToggleHeightChange();
    }

    public bool TryBeginUccExternalLock(bool disableGameplayInput = true, bool stopActiveAbilities = false)
    {
        LitOpsiveLocomotionBridge bridge = GetUccLocomotionBridge();
        return bridge != null && bridge.BeginExternalLock(disableGameplayInput, stopActiveAbilities);
    }

    public void EndUccExternalLock()
    {
        LitOpsiveLocomotionBridge bridge = GetUccLocomotionBridge();
        if (bridge != null)
        {
            bridge.EndExternalLock();
        }
    }

    private bool CanUseLitInteractionsWithUcc()
    {
        LitUccInteractionBridge bridge = GetUccInteractionBridge();
        return bridge == null || bridge.CanEvaluateLitInteractions;
    }

    private bool CanUseLitInteractableWithUcc(ICharacterDetectedInteractable target)
    {
        LitUccInteractionBridge bridge = GetUccInteractionBridge();
        return bridge == null || bridge.CanUseLitInteractable(target);
    }

    private bool TryGetActiveUccLocomotionBridge(out LitOpsiveLocomotionBridge bridge)
    {
        bridge = GetUccLocomotionBridge();
        return bridge != null && bridge.IsDriving;
    }

    private LitOpsiveLocomotionBridge GetUccLocomotionBridge()
    {
        if (uccLocomotionBridge == null)
        {
            uccLocomotionBridge = GetComponent<LitOpsiveLocomotionBridge>();
        }

        return uccLocomotionBridge;
    }

    private LitUccInteractionBridge GetUccInteractionBridge()
    {
        if (uccInteractionBridge == null)
        {
            uccInteractionBridge = GetComponent<LitUccInteractionBridge>();
        }

        return uccInteractionBridge;
    }
}
