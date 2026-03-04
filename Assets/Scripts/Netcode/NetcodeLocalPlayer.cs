using Unity.Netcode;
using UnityEngine;

// Associe le joueur local au personnage possede par ce client.
public class NetcodeLocalPlayer : NetworkBehaviour
{
    [SerializeField] private Transform localCharacterRoot;

    private void Awake()
    {
        if (localCharacterRoot == null)
        {
            localCharacterRoot = transform;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner && IsAssignedToLocalClient())
        {
            LocalPlayerContext.SetLocalCharacter(localCharacterRoot);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            LocalPlayerContext.ClearIfMatch(localCharacterRoot);
        }
    }

    public override void OnGainedOwnership()
    {
        if (IsAssignedToLocalClient())
        {
            LocalPlayerContext.SetLocalCharacter(localCharacterRoot);
        }
    }

    public override void OnLostOwnership()
    {
        LocalPlayerContext.ClearIfMatch(localCharacterRoot);
    }

    private bool IsAssignedToLocalClient()
    {
        if (NetworkManager.Singleton == null)
        {
            return true;
        }

        WorldInteractionService service = WorldInteractionService.Instance;
        if (service == null)
        {
            return !NetworkManager.Singleton.IsHost;
        }

        if (!service.TryGetAssignedCharacterId(NetworkManager.Singleton.LocalClientId, out string characterId))
        {
            return !NetworkManager.Singleton.IsHost;
        }

        string localId = ResolveCharacterId();
        if (string.IsNullOrWhiteSpace(localId))
        {
            return false;
        }

        return string.Equals(characterId, localId, System.StringComparison.Ordinal);
    }

    private string ResolveCharacterId()
    {
        SquadCharacterController controller = localCharacterRoot != null
            ? localCharacterRoot.GetComponent<SquadCharacterController>()
            : null;

        CharacterData data = controller != null ? controller.CharacterData : null;
        if (data == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(data.UniqueId))
        {
            return data.UniqueId;
        }

        if (!string.IsNullOrWhiteSpace(data.characterId))
        {
            return data.characterId;
        }

        if (!string.IsNullOrWhiteSpace(data.characterName))
        {
            return data.characterName;
        }

        return data.name;
    }
}
