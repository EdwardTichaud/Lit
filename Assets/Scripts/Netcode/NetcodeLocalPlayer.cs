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
            LocalPlayerContext.ClearIfMatch(
                localCharacterRoot,
                "netcode_local_player_despawn",
                LocalPlayerContext.Authority.MultiplayerAssignment);
        }
    }

    public override void OnGainedOwnership()
    {
        EvaluateLocalAssignment();
    }

    public override void OnLostOwnership()
    {
        LocalPlayerContext.ClearIfMatch(
            localCharacterRoot,
            "netcode_local_player_lost_ownership",
            LocalPlayerContext.Authority.MultiplayerAssignment);
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

        if (IsOwner)
        {
            EvaluateLocalAssignment();
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

        bool assignmentResolved = TryResolveAssignmentToLocalClient(out bool assignedToLocalClient);
        bool preserveExistingLocalControl = !assignmentResolved && IsCurrentLocalCharacter();
        if (assignedToLocalClient || preserveExistingLocalControl)
        {
            LocalPlayerContext.SetLocalCharacter(
                localCharacterRoot,
                "netcode_local_player_assignment",
                LocalPlayerContext.Authority.MultiplayerAssignment);
        }
        else
        {
            LocalPlayerContext.ClearIfMatch(
                localCharacterRoot,
                "netcode_local_player_unassigned",
                LocalPlayerContext.Authority.MultiplayerAssignment);
        }

        Transform logTarget = localCharacterRoot != null ? localCharacterRoot : transform;
        NetcodePlayerUtils.LogControlDecision(
            "local_assignment",
            logTarget != null ? logTarget.gameObject : gameObject,
            followerAiEnabled: false,
            waitingPointEnabled: false,
            movementMode: null,
            reason: assignedToLocalClient
                ? "this character is locally controlled"
                : preserveExistingLocalControl
                    ? "assignment registry temporarily unresolved; preserving existing local control"
                    : "owner character is not the locally assigned character");
    }

    private bool TryResolveAssignmentToLocalClient(out bool assignedToLocalClient)
    {
        assignedToLocalClient = false;
        if (NetworkManager.Singleton == null)
        {
            assignedToLocalClient = true;
            return true;
        }

        WorldInteractionService service = WorldInteractionService.Instance;
        if (service == null)
        {
            assignedToLocalClient = true;
            return false;
        }

        if (!service.TryGetAssignedCharacterId(NetworkManager.Singleton.LocalClientId, out string characterId))
        {
            return false;
        }

        string localId = ResolveCharacterId();
        if (string.IsNullOrWhiteSpace(localId))
        {
            return false;
        }

        assignedToLocalClient = string.Equals(characterId, localId, System.StringComparison.Ordinal);
        return true;
    }

    private bool IsCurrentLocalCharacter()
    {
        if (localCharacterRoot == null)
        {
            return false;
        }

        Transform current = LocalPlayerContext.LocalCharacterRoot;
        if (current == null)
        {
            return false;
        }

        return current == localCharacterRoot
            || current.IsChildOf(localCharacterRoot)
            || localCharacterRoot.IsChildOf(current);
    }

    private string ResolveCharacterId()
    {
        NetcodeCharacterIdentity identity = localCharacterRoot != null
            ? localCharacterRoot.GetComponent<NetcodeCharacterIdentity>()
            : null;
        if (identity != null && !string.IsNullOrWhiteSpace(identity.CharacterId))
        {
            return identity.CharacterId;
        }

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
