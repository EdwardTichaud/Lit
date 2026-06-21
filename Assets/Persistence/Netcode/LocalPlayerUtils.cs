using UnityEngine;

// Utilitaires pour resoudre le personnage local controle.
public static class LocalPlayerUtils
{
    public static GameObject GetControlledCharacter()
    {
        Transform localRoot = LocalPlayerContext.LocalCharacterRoot;
        if (localRoot != null)
        {
            return localRoot.gameObject;
        }

        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            return null;
        }

        return SquadManager.Instance != null ? SquadManager.Instance.currentCharacter : null;
    }
}
