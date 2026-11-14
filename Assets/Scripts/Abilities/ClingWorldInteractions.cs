using Lit.Traversal;
using Unity.Netcode;

public partial class WorldInteractionService
{
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestClingObservationServerRpc(string pointId, RpcParams rpcParams = default)
    {
        if (string.IsNullOrWhiteSpace(pointId) || pointId.Length > 96 || JoinSyncSystem.IsGameplayBlocked) return;
        var point = ClingPoint.Find(pointId);
        var actor = ResolvePlayerCharacter(rpcParams)?.GetComponent<SquadCharacterController>();
        var abilities = AbilityService.Instance;
        if (point == null || point.Revealed || actor == null || abilities == null ||
            !abilities.HasEquipped(actor.gameObject, abilities.Resolve("observateur")) || !point.CanObserve(actor)) return;
        point.Reveal();
        ClingDiscoveredClientRpc(pointId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestClingDiscoveriesServerRpc(RpcParams rpcParams = default)
    {
        var targets = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { rpcParams.Receive.SenderClientId } }
        };
        foreach (ClingPoint point in ClingPoint.Active)
            if (point.Revealed) ClingDiscoveredClientRpc(point.PointId, targets);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestClingGrabServerRpc(string pointId, RpcParams rpcParams = default)
    {
        if (string.IsNullOrWhiteSpace(pointId) || pointId.Length > 96 || JoinSyncSystem.IsGameplayBlocked ||
            GameFlowService.Instance == null || GameFlowService.Instance.IsTransitioning) return;
        var point = ClingPoint.Find(pointId);
        var actor = ResolvePlayerCharacter(rpcParams)?.GetComponent<SquadCharacterController>();
        if (point == null || !point.CanGrab(actor, false, out _)) return;
        ClingGrabClientRpc(pointId, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { rpcParams.Receive.SenderClientId } }
        });
    }

    [ClientRpc]
    private void ClingDiscoveredClientRpc(string pointId, ClientRpcParams targets = default)
    {
        if (!IsServer) ClingPoint.Find(pointId)?.Reveal();
    }

    [ClientRpc]
    private void ClingGrabClientRpc(string pointId, ClientRpcParams targets = default)
    {
        ClingController.BeginLocalGrab(ClingPoint.Find(pointId));
    }
}
