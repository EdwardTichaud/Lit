using Unity.Netcode;
using UnityEngine;

// Service reseau pour router les interactions serveur (triggers non-networked).
public class WorldInteractionService : NetworkBehaviour
{
    public static WorldInteractionService Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestReturnHomeServerRpc(uint triggerId, ServerRpcParams rpcParams = default)
    {
        if (!NetcodeTriggerRegistry.TryGetReturnHome(triggerId, out ReturnHomeTrigger trigger))
        {
            SendReturnHomeResultClientRpc(triggerId, (int)SquadManager.SendHomeResult.InvalidCharacter, BuildClientRpcParams(rpcParams));
            return;
        }

        GameObject character = ResolvePlayerCharacter(rpcParams);
        if (character == null || !trigger.IsServerCharacterAllowed(character))
        {
            SendReturnHomeResultClientRpc(triggerId, (int)SquadManager.SendHomeResult.InvalidCharacter, BuildClientRpcParams(rpcParams));
            return;
        }

        SquadManager.SendHomeResult result = trigger.ServerTrySendHome(character);
        SendReturnHomeResultClientRpc(triggerId, (int)result, BuildClientRpcParams(rpcParams));
    }

    [ClientRpc]
    private void SendReturnHomeResultClientRpc(uint triggerId, int resultValue, ClientRpcParams rpcParams = default)
    {
        if (!NetcodeTriggerRegistry.TryGetReturnHome(triggerId, out ReturnHomeTrigger trigger))
        {
            return;
        }

        trigger.HandleReturnHomeResult((SquadManager.SendHomeResult)resultValue);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestHubSwapServerRpc(uint triggerId, ServerRpcParams rpcParams = default)
    {
        if (!NetcodeTriggerRegistry.TryGetHubSwap(triggerId, out HubCompanionSwapTrigger trigger))
        {
            SendHubSwapResultClientRpc(triggerId, false, BuildClientRpcParams(rpcParams));
            return;
        }

        GameObject character = ResolvePlayerCharacter(rpcParams);
        bool success = trigger.ServerTrySwap(character);
        SendHubSwapResultClientRpc(triggerId, success, BuildClientRpcParams(rpcParams));
    }

    [ClientRpc]
    private void SendHubSwapResultClientRpc(uint triggerId, bool success, ClientRpcParams rpcParams = default)
    {
        if (!NetcodeTriggerRegistry.TryGetHubSwap(triggerId, out HubCompanionSwapTrigger trigger))
        {
            return;
        }

        trigger.HandleSwapResult(success);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestLabyrinthStartServerRpc(uint triggerId, ServerRpcParams rpcParams = default)
    {
        if (!NetcodeTriggerRegistry.TryGetLabyrinth(triggerId, out LabyrinthStartTrigger trigger))
        {
            return;
        }

        GameObject character = ResolvePlayerCharacter(rpcParams);
        if (!trigger.IsServerCharacterAllowed(character))
        {
            return;
        }

        trigger.ServerStartLabyrinth();
        LabyrinthStartedClientRpc(triggerId);
    }

    [ClientRpc]
    private void LabyrinthStartedClientRpc(uint triggerId)
    {
        if (!NetcodeTriggerRegistry.TryGetLabyrinth(triggerId, out LabyrinthStartTrigger trigger))
        {
            return;
        }

        trigger.ClientHandleLabyrinthStarted();
    }

    private static GameObject ResolvePlayerCharacter(ServerRpcParams rpcParams)
    {
        Transform playerRoot = NetcodePlayerUtils.GetPlayerTransform(rpcParams.Receive.SenderClientId);
        return playerRoot != null ? playerRoot.gameObject : null;
    }

    private static ClientRpcParams BuildClientRpcParams(ServerRpcParams rpcParams)
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { rpcParams.Receive.SenderClientId }
            }
        };
    }
}
