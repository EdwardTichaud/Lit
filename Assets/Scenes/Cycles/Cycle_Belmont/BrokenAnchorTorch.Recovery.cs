using System;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public sealed partial class BrokenAnchorTorch
{
    private const string ReleaseSequenceId = "anchor_release";
    private CycleController recoveryCycle;
    private bool Online => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    private void OnEnable() => BindRecoveryCycle();
    private void OnDisable() => UnbindRecoveryCycle();

    // Resolve the existing authored binding, not a global Belmont singleton or
    // a new scene reference. This also works when a torch is a sibling of the controller.
    private CycleController BindRecoveryCycle()
    {
        if (recoveryCycle != null && OwnsTorch(recoveryCycle)) return recoveryCycle;
        UnbindRecoveryCycle();
        if (!gameObject.scene.IsValid()) return null;
        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var candidate in root.GetComponentsInChildren<CycleController>(true))
                if (OwnsTorch(candidate))
                {
                    recoveryCycle = candidate;
                    recoveryCycle.SequencePresentationFinished += OnReleasePresentationFinished;
                    return recoveryCycle;
                }
        return null;
    }

    private bool OwnsTorch(CycleController cycle) => cycle != null && cycle.gameObject.scene == gameObject.scene &&
        (cycle.flames ?? Array.Empty<CycleFlameBinding>()).Any(binding => binding != null && binding.bossTorch == this);

    private void UnbindRecoveryCycle()
    {
        if (recoveryCycle != null) recoveryCycle.SequencePresentationFinished -= OnReleasePresentationFinished;
        recoveryCycle = null;
    }

    private void OnReleasePresentationFinished(string sequenceId, bool success)
    {
        if (!Application.isPlaying || success || sequenceId != ReleaseSequenceId || recoveryCycle == null || !IsLit ||
            !recoveryCycle.IsSceneEventActive(CycleStepKind.SequenceCompleted, ReleaseSequenceId)) return;
        // The outcome is broadcast to all three torches. Present one local hint,
        // without replacing a dialogue or taking control of the character.
        var firstTorch = (recoveryCycle.flames ?? Array.Empty<CycleFlameBinding>())
            .FirstOrDefault(binding => binding != null && binding.bossTorch != null)?.bossTorch;
        if (firstTorch == this)
            InfoBoxUI.TryShowToast("Le souvenir s'est interrompu. Interagissez avec une torche allumée pour le relancer.", 6f);
    }

    private bool ReleaseIsAwaiting()
    {
        var cycle = BindRecoveryCycle();
        return IsLit && cycle != null && cycle.IsSceneEventActive(CycleStepKind.SequenceCompleted, ReleaseSequenceId);
    }

    private bool TryHandleReleaseRecovery(Transform player)
    {
        if (!ReleaseIsAwaiting()) return false;
        if (Online)
        {
            if (!IsSpawned) return false;
            RetryReleaseRpc();
            return true;
        }
        return TryRetryRelease(player);
    }

    // Client detection is only a request. The actual sender and all gameplay
    // checks are resolved again on the host; no client reports a completed step.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RetryReleaseRpc(RpcParams rpc = default) =>
        TryRetryRelease(NetcodePlayerUtils.GetPlayerTransform(rpc.Receive.SenderClientId));

    private bool TryRetryRelease(Transform player)
    {
        if (Online && !IsServer || !isActiveAndEnabled || !ReleaseIsAwaiting() || player == null ||
            float.IsNaN(interactionDistance) || float.IsInfinity(interactionDistance) || interactionDistance <= 0 ||
            !CharacterInteractionDetection.IsCharacterWithinRange(player, GetInteractionDetectionCollider(), GetInteractionAnchor(), interactionDistance)) return false;
        var character = player.GetComponentInChildren<SquadCharacterController>();
        if (character == null) character = player.GetComponentInParent<SquadCharacterController>();
        if (character == null || !character.isActiveAndEnabled || character.CurrentHp <= 0) return false;
        var bridge = character.GetComponent<LitUccInteractionBridge>();
        if (bridge != null && !bridge.CanUseLitInteractable(this)) return false;
        var collider = GetInteractionDetectionCollider();
        if (collider == null || !collider.enabled) return false;
        return recoveryCycle.TryRetrySceneSequence(ReleaseSequenceId);
    }
}
