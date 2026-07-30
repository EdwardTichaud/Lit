using UnityEngine;

public partial class SquadCharacterController
{
    private LitOpsiveLocomotionBridge uccLocomotionBridge;
    private LitUccInteractionBridge uccInteractionBridge;
    private Vector2 lastUccWorldMoveInput;
    private Vector2 queuedUccJumpWorldInput;
    private bool hasQueuedUccJumpWorldInput;

    public bool HasUccLocomotionBridge => GetUccLocomotionBridge() != null;
    public bool IsUccLocomotionActive => TryGetActiveUccLocomotionBridge(out _);
    public bool IsFlightActive => TryGetActiveUccLocomotionBridge(out LitOpsiveLocomotionBridge bridge) && bridge.IsFlightActive;
    public bool IsUccFlightActive => IsFlightActive;
    public bool IsStandaloneFlightFallbackActive =>
        TryGetActiveUccLocomotionBridge(out LitOpsiveLocomotionBridge bridge) &&
        bridge.IsUsingStandaloneFlightFallback;

    public void QueueUccJumpInput(Vector2 input, bool isWorldSpace)
    {
        Vector2 worldInput = isWorldSpace ? input : GetWorldSpaceInput(input);
        queuedUccJumpWorldInput = Vector2.ClampMagnitude(worldInput, 1f);
        hasQueuedUccJumpWorldInput = queuedUccJumpWorldInput.sqrMagnitude > 0.0001f;
    }

    private bool TryForwardMoveToUcc(Vector2 input, bool isWorldSpace)
    {
        if (!TryGetActiveUccLocomotionBridge(out LitOpsiveLocomotionBridge bridge))
        {
            return false;
        }

        Vector2 worldInput = isWorldSpace ? input : GetWorldSpaceInput(input);
        bridge.SetMoveInput(worldInput, isWorldSpace: true);
        ApplyMovementFacialNeutral(ResolveMovementInputMagnitude(input));
        lastUccWorldMoveInput = worldInput.sqrMagnitude <= movementInputDeadZone * movementInputDeadZone
            ? Vector2.zero
            : Vector2.ClampMagnitude(worldInput, 1f);
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

        Vector2 jumpWorldInput = hasQueuedUccJumpWorldInput ? queuedUccJumpWorldInput : lastUccWorldMoveInput;
        bool accepted = bridge.Jump(jumpWorldInput, jumpWorldInput.sqrMagnitude > 0.0001f);
        if (accepted)
        {
            ClearQueuedUccJumpInput();
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

    public bool TryToggleUccFlightMode(float verticalInput)
    {
        if (!TryGetActiveUccLocomotionBridge(out LitOpsiveLocomotionBridge bridge))
        {
            return false;
        }

        return bridge.ToggleFlightMode(verticalInput);
    }

    public bool TrySetUccFlightInput(Vector2 input, bool isWorldSpace, bool boost, float verticalInput)
    {
        if (!TryGetActiveUccLocomotionBridge(out LitOpsiveLocomotionBridge bridge))
        {
            return false;
        }

        Vector2 worldInput = isWorldSpace ? input : GetWorldSpaceInput(input);
        return bridge.SetFlightInput(worldInput, boost, verticalInput);
    }

    public bool TryBeginUccExternalLock(bool disableGameplayInput = true, bool stopActiveAbilities = false)
    {
        LitOpsiveLocomotionBridge bridge = GetUccLocomotionBridge();
        return bridge != null && bridge.BeginExternalLock(disableGameplayInput, stopActiveAbilities);
    }

    public bool TryBeginUccProgressiveStop(bool disableGameplayInput = true, bool stopActiveAbilities = false)
    {
        LitOpsiveLocomotionBridge bridge = GetUccLocomotionBridge();
        return bridge != null && bridge.BeginExternalLockWithProgressiveStop(disableGameplayInput, stopActiveAbilities);
    }

    public bool IsUccProgressiveStopComplete(float velocityThreshold)
    {
        LitOpsiveLocomotionBridge bridge = GetUccLocomotionBridge();
        return bridge == null || bridge.IsProgressiveStopComplete(velocityThreshold);
    }

    public void CompleteUccProgressiveStop()
    {
        GetUccLocomotionBridge()?.CompleteProgressiveStop();
    }

    public void EndUccExternalLock()
    {
        LitOpsiveLocomotionBridge bridge = GetUccLocomotionBridge();
        if (bridge != null)
        {
            bridge.EndExternalLock();
        }
    }

    public bool TrySetUccExternalPositionAndRotation(Vector3 position, Quaternion rotation, bool stopActiveAbilities = true)
    {
        LitOpsiveLocomotionBridge bridge = GetUccLocomotionBridge();
        return bridge != null && bridge.SetExternalPositionAndRotation(position, rotation, stopActiveAbilities);
    }

    /// <summary>
    /// Repositionnement atomique reserve aux changements de scene. UCC doit
    /// recevoir un snap Animator dans ce cas, contrairement aux petites
    /// corrections de position utilisees pendant le jeu.
    /// </summary>
    public bool TryTeleportForSceneTransition(Vector3 position, Quaternion rotation)
    {
        CancelSittingState();
        scriptedMovementSuppressionCount = 0;
        ResetUccLocomotionIntent();

        LitOpsiveLocomotionBridge bridge = GetUccLocomotionBridge();
        if (bridge != null && bridge.TeleportForSceneTransition(position, rotation))
        {
            return true;
        }

        // Les personnages non-UCC restent compatibles avec les points de
        // spawn. Ce chemin ne s'applique pas au joueur UCC normal.
        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.position = position;
            body.rotation = rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        else
        {
            transform.SetPositionAndRotation(position, rotation);
        }

        Physics.SyncTransforms();
        return true;
    }

    private void ResetUccLocomotionIntent()
    {
        lastUccWorldMoveInput = Vector2.zero;
        ClearQueuedUccJumpInput();
    }

    private void ClearQueuedUccJumpInput()
    {
        queuedUccJumpWorldInput = Vector2.zero;
        hasQueuedUccJumpWorldInput = false;
    }

    private bool TryAddImpulseToUcc(Vector3 worldImpulse, ForceMode forceMode, float lockInputForSeconds)
    {
        if (!TryGetActiveUccLocomotionBridge(out LitOpsiveLocomotionBridge bridge))
        {
            return false;
        }

        return bridge.AddExternalImpulse(worldImpulse, forceMode, lockInputForSeconds);
    }

    private bool TryGetUccGrounded(out bool grounded)
    {
        grounded = false;
        if (!TryGetActiveUccLocomotionBridge(out LitOpsiveLocomotionBridge bridge))
        {
            return false;
        }

        grounded = bridge.Grounded;
        return true;
    }

    private bool TryGetUccPlanarVelocity(out Vector3 planarVelocity)
    {
        planarVelocity = Vector3.zero;
        if (!TryGetActiveUccLocomotionBridge(out LitOpsiveLocomotionBridge bridge))
        {
            return false;
        }

        planarVelocity = bridge.PlanarVelocity;
        return true;
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
