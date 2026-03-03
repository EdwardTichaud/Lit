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
        if (IsOwner)
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
        LocalPlayerContext.SetLocalCharacter(localCharacterRoot);
    }

    public override void OnLostOwnership()
    {
        LocalPlayerContext.ClearIfMatch(localCharacterRoot);
    }
}
