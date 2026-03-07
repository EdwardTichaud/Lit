using Unity.Netcode;
using UnityEngine;

// Associe le joueur local au personnage possede par ce client.
public class NetcodeLocalPlayer : NetworkBehaviour
{
    [SerializeField] private Transform localCharacterRoot;
    private WorldInteractionService subscribedService;

    private void Awake()
    {
        if (localCharacterRoot == null)
        {
            localCharacterRoot = transform;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            EvaluateLocalAssignment();
        }

        TrySubscribeAssignments();
    }

    public override void OnNetworkDespawn()
    {
        UnsubscribeAssignments();
        if (IsOwner)
        {
            LocalPlayerContext.ClearIfMatch(localCharacterRoot);
        }
    }

    public override void OnGainedOwnership()
    {
        EvaluateLocalAssignment();
    }

    public override void OnLostOwnership()
    {
        LocalPlayerContext.ClearIfMatch(localCharacterRoot);
    }

    private void OnEnable()
    {
        TrySubscribeAssignments();
    }

    private void OnDisable()
    {
        UnsubscribeAssignments();
    }

    private void Update()
    {
        if (subscribedService == null)
        {
            TrySubscribeAssignments();
        }
    }

    private void TrySubscribeAssignments()
    {
        WorldInteractionService service = WorldInteractionService.Instance;
        if (ReferenceEquals(subscribedService, service))
        {
            return;
        }

        UnsubscribeAssignments();
        subscribedService = service;
        if (subscribedService != null)
        {
            subscribedService.AssignmentsChanged += OnAssignmentsChanged;
        }
    }

    private void UnsubscribeAssignments()
    {
        if (subscribedService != null)
        {
            subscribedService.AssignmentsChanged -= OnAssignmentsChanged;
        }

        subscribedService = null;
    }

    private void OnAssignmentsChanged()
    {
        if (!IsOwner)
        {
            return;
        }

        EvaluateLocalAssignment();
    }

    private void EvaluateLocalAssignment()
    {
        if (!IsOwner)
        {
            return;
        }

        if (IsAssignedToLocalClient())
        {
            LocalPlayerContext.SetLocalCharacter(localCharacterRoot);
        }
        else
        {
            LocalPlayerContext.ClearIfMatch(localCharacterRoot);
        }
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
