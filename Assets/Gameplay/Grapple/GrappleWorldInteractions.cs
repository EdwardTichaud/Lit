using Lit.Traversal;
using Unity.Netcode;

public partial class WorldInteractionService
{
    private readonly System.Collections.Generic.Dictionary<ulong, double> grappleNextRequest = new();
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestGrappleServerRpc(string anchorId, bool use, RpcParams rpcParams = default)
    {
        if (string.IsNullOrWhiteSpace(anchorId) || anchorId.Length > 96) return;
        ulong sender = rpcParams.Receive.SenderClientId;
        double now = UnityEngine.Time.unscaledTimeAsDouble;
        if (grappleNextRequest.TryGetValue(sender, out double next) && now < next) return;
        // Discovery is already throttled locally; only traversal reserves the actor.
        var anchor = GrappleAnchor.Find(anchorId);
        var actor = ResolvePlayerCharacter(rpcParams)?.GetComponent<SquadCharacterController>();
        var abilities = AbilityService.Instance;
        if (anchor == null || abilities == null || actor == null || !anchor.CanObserve(actor) || JoinSyncSystem.IsGameplayBlocked) return;
        if (!use)
        {
            if (anchor.Revealed) return;
            if (!abilities.HasEquipped(actor.gameObject, abilities.Resolve("observateur"))) return;
            anchor.RevealRoute();
            foreach (var other in UnityEngine.Object.FindObjectsByType<GrappleAnchor>())
                if (other == anchor || !string.IsNullOrWhiteSpace(anchor.routeId) && other.routeId == anchor.routeId)
                    GrappleDiscoveryClientRpc(other.anchorId, other.Revealed, other.Prepared);
        }
        else if (anchor.HasTraversalCapability(actor) && anchor.CanUse(actor))
        {
            anchor.Prepare();
            grappleNextRequest[sender] = now + anchor.duration + .3;
            GrappleDiscoveryClientRpc(anchorId, true, true);
            GrappleTravelClientRpc(anchorId, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { sender } } });
        }
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestGrappleSnapshotServerRpc(RpcParams rpcParams = default)
    {
        var targets = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { rpcParams.Receive.SenderClientId } } };
        foreach (var anchor in UnityEngine.Object.FindObjectsByType<GrappleAnchor>()) GrappleDiscoveryClientRpc(anchor.anchorId, anchor.Revealed, anchor.Prepared, targets);
    }
    [ClientRpc]
    private void GrappleDiscoveryClientRpc(string anchorId, bool revealed, bool prepared, ClientRpcParams targets = default)
    {
        if (IsServer) return;
        var anchor = GrappleAnchor.Find(anchorId);
        if (anchor == null) return;
        anchor.ApplyDiscovery(revealed);
        anchor.ApplyPreparation(prepared);
    }
    [ClientRpc]
    private void GrappleTravelClientRpc(string anchorId, ClientRpcParams targets = default)
    { GrappleController.StartTravel(GrappleAnchor.Find(anchorId)); }
}
