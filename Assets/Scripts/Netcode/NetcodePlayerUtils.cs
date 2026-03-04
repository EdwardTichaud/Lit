using Unity.Netcode;
using UnityEngine;

// Utilitaires pour resoudre les objets joueurs Netcode.
public static class NetcodePlayerUtils
{
    public static Transform GetPlayerTransform(ulong clientId)
    {
        if (NetworkManager.Singleton == null)
        {
            return null;
        }

        WorldInteractionService service = WorldInteractionService.Instance;
        if (service != null && service.TryGetAssignedCharacterId(clientId, out string assignedId))
        {
            GameObject assigned = ResolveCharacterInstanceById(assignedId);
            if (assigned != null)
            {
                return assigned.transform;
            }
        }

        if (NetworkManager.Singleton.SpawnManager != null)
        {
            NetworkObject[] owned = NetworkManager.Singleton.SpawnManager.GetClientOwnedObjects(clientId);
            if (owned != null)
            {
                for (int i = 0; i < owned.Length; i++)
                {
                    NetworkObject obj = owned[i];
                    if (obj == null)
                    {
                        continue;
                    }

                    if (obj.GetComponent<SquadCharacterController>() != null)
                    {
                        return obj.transform;
                    }
                }
            }
        }

        if (NetworkManager.Singleton.ConnectedClients != null
            && NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client)
            && client != null
            && client.PlayerObject != null)
        {
            return client.PlayerObject.transform;
        }

        return null;
    }

    private static GameObject ResolveCharacterInstanceById(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return null;
        }

        SquadManager manager = SquadManager.Instance;
        if (manager != null && manager.currentSquad != null)
        {
            for (int i = 0; i < manager.currentSquad.Count; i++)
            {
                CharacterData character = manager.currentSquad[i];
                if (character == null)
                {
                    continue;
                }

                if (GetCharacterId(character) == characterId)
                {
                    return manager.GetCharacterInstance(character);
                }
            }
        }

#if UNITY_2023_1_OR_NEWER
        SquadCharacterController[] controllers = UnityEngine.Object.FindObjectsByType<SquadCharacterController>(UnityEngine.FindObjectsSortMode.None);
#else
        SquadCharacterController[] controllers = UnityEngine.Object.FindObjectsOfType<SquadCharacterController>();
#endif
        for (int i = 0; i < controllers.Length; i++)
        {
            SquadCharacterController controller = controllers[i];
            if (controller == null || controller.CharacterData == null)
            {
                continue;
            }

            if (GetCharacterId(controller.CharacterData) == characterId)
            {
                return controller.gameObject;
            }
        }

        return null;
    }

    private static string GetCharacterId(CharacterData character)
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
