using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// Identite reseau autoritaire d'un personnage pour resoudre le bon client local.
[RequireComponent(typeof(NetworkObject))]
public class NetcodeCharacterIdentity : NetworkBehaviour
{
    [SerializeField] private SquadCharacterController controller;

    private readonly NetworkVariable<FixedString64Bytes> characterId = new NetworkVariable<FixedString64Bytes>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public string CharacterId => characterId.Value.ToString();

    private void Awake()
    {
        if (controller == null)
        {
            controller = GetComponent<SquadCharacterController>();
        }
    }

    public override void OnNetworkSpawn()
    {
        characterId.OnValueChanged += OnCharacterIdChanged;
        ApplyCharacterData(CharacterId);
    }

    public override void OnNetworkDespawn()
    {
        characterId.OnValueChanged -= OnCharacterIdChanged;
    }

    public void SetCharacter(CharacterData character)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager != null && manager.IsListening && !manager.IsServer)
        {
            return;
        }

        string resolvedId = GetCharacterId(character);
        if (string.IsNullOrWhiteSpace(resolvedId))
        {
            return;
        }

        characterId.Value = new FixedString64Bytes(resolvedId);
        ApplyCharacterData(resolvedId);
    }

    private void OnCharacterIdChanged(FixedString64Bytes previous, FixedString64Bytes current)
    {
        ApplyCharacterData(current.ToString());
    }

    private void ApplyCharacterData(string resolvedId)
    {
        if (string.IsNullOrWhiteSpace(resolvedId))
        {
            return;
        }

        if (controller == null)
        {
            controller = GetComponent<SquadCharacterController>();
        }

        if (controller == null)
        {
            return;
        }

        CharacterData current = controller.CharacterData;
        if (current != null && string.Equals(GetCharacterId(current), resolvedId, StringComparison.Ordinal))
        {
            return;
        }

        if (!TryResolveCharacterData(resolvedId, out CharacterData resolvedCharacter))
        {
            return;
        }

        bool initializeInventory = NetworkManager.Singleton == null
            || !NetworkManager.Singleton.IsListening
            || NetworkManager.Singleton.IsServer;

        controller.BindCharacterData(resolvedCharacter, initializeInventory);

        NetworkInventory inventory = GetComponent<NetworkInventory>();
        if (inventory != null && !initializeInventory)
        {
            inventory.RefreshControllerFromNetworkState();
        }
    }

    public static bool MatchesCharacterId(GameObject instance, string resolvedId)
    {
        if (instance == null || string.IsNullOrWhiteSpace(resolvedId))
        {
            return false;
        }

        NetcodeCharacterIdentity identity = instance.GetComponent<NetcodeCharacterIdentity>();
        if (identity != null && !string.IsNullOrWhiteSpace(identity.CharacterId))
        {
            return string.Equals(identity.CharacterId, resolvedId, StringComparison.Ordinal);
        }

        SquadCharacterController controller = instance.GetComponent<SquadCharacterController>();
        return controller != null && string.Equals(GetCharacterId(controller.CharacterData), resolvedId, StringComparison.Ordinal);
    }

    public static bool TryResolveCharacterData(string resolvedId, out CharacterData character)
    {
        character = null;
        if (string.IsNullOrWhiteSpace(resolvedId))
        {
            return false;
        }

        SquadManager manager = SquadManager.Instance;
        if (manager != null && manager.currentSquad != null)
        {
            for (int i = 0; i < manager.currentSquad.Count; i++)
            {
                CharacterData candidate = manager.currentSquad[i];
                if (candidate != null && string.Equals(GetCharacterId(candidate), resolvedId, StringComparison.Ordinal))
                {
                    character = candidate;
                    return true;
                }
            }
        }

#if UNITY_2023_1_OR_NEWER
        CharacterData[] characters = Resources.FindObjectsOfTypeAll<CharacterData>();
#else
        CharacterData[] characters = Resources.FindObjectsOfTypeAll<CharacterData>();
#endif
        if (characters == null)
        {
            return false;
        }

        for (int i = 0; i < characters.Length; i++)
        {
            CharacterData candidate = characters[i];
            if (candidate != null && string.Equals(GetCharacterId(candidate), resolvedId, StringComparison.Ordinal))
            {
                character = candidate;
                return true;
            }
        }

        return false;
    }

    public static string GetCharacterId(CharacterData character)
    {
        if (character == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(character.UniqueId))
        {
            return character.UniqueId;
        }

        if (!string.IsNullOrWhiteSpace(character.characterId))
        {
            return character.characterId;
        }

        if (!string.IsNullOrWhiteSpace(character.characterName))
        {
            return character.characterName;
        }

        return character.name;
    }
}
