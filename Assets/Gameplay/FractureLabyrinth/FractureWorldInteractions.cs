using System.Linq;
using Lit.Fractures;
using Unity.Netcode;
using UnityEngine;

public partial class WorldInteractionService
{
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestFractureInteractionServerRpc(uint stationId, RpcParams rpcParams = default)
    {
        var lab = FractureGraybox.District;
        if (lab == null) return;
        var station = lab.GetComponentsInChildren<FractureStation>().FirstOrDefault(value => NetcodeSceneIdUtility.GetStableId(value.transform) == stationId);
        var actor = ResolvePlayerCharacter(rpcParams)?.GetComponent<SquadCharacterController>();
        lab.ServerDistrictInteraction(station, actor);
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestFractureStateServerRpc(RpcParams rpcParams = default)
    {
        var lab = FractureGraybox.District;
        if (lab != null) FractureStateClientRpc(lab.CaptureDistrictState(), string.Empty, string.Empty,
            new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { rpcParams.Receive.SenderClientId } } });
    }
    [ClientRpc]
    public void FractureStateClientRpc(int state, string feedback, string reveal, ClientRpcParams rpcParams = default)
    {
        if (!IsServer) FractureGraybox.District?.ApplyDistrictState(state, feedback, reveal);
    }
}
