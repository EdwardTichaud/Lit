using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Role: stable scene-side identifier used by ScriptableObject-authored gameplay links.
// Usage: add to a scene GameObject, copy GameObjectID into data assets that must resolve it.
// Responsibilities: generate an ID once, keep it serialized, and expose runtime lookup by ID.
// Precautions: do not regenerate an ID after it has been referenced by a ScriptableObject.
[DisallowMultipleComponent]
[AddComponentMenu("Lit/Utility/GameObject ID")]
public class GameObjectID : MonoBehaviour
{
    private static readonly Dictionary<string, GameObjectID> Registry = new Dictionary<string, GameObjectID>(StringComparer.Ordinal);

    [SerializeField, Tooltip("ID stable a copier dans les ScriptableObjects. Genere automatiquement si vide.")]
    private string gameObjectID;

    public string ID => gameObjectID;
    public string GameObjectId => gameObjectID;

    private void Reset()
    {
        EnsureID();
    }

    private void Awake()
    {
        EnsureID();
    }

    private void OnEnable()
    {
        EnsureID();
        Register(this);
    }

    private void OnDisable()
    {
        Unregister(this);
    }

    private void OnValidate()
    {
        EnsureID();
    }

    [ContextMenu("Regenerate GameObject ID")]
    public void RegenerateGameObjectID()
    {
        Unregister(this);
        gameObjectID = GenerateID();
        Register(this);
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

#if UNITY_EDITOR
    [ContextMenu("Copy GameObject ID")]
    private void CopyGameObjectID()
    {
        EnsureID();
        EditorGUIUtility.systemCopyBuffer = gameObjectID;
        Debug.Log($"GameObjectID copied: {gameObjectID}", this);
    }
#endif

    public bool Matches(string candidateID)
    {
        return !string.IsNullOrWhiteSpace(candidateID) &&
               string.Equals(gameObjectID, candidateID.Trim(), StringComparison.Ordinal);
    }

    public static bool TryFind(string gameObjectID, out GameObjectID result)
    {
        result = null;
        string normalizedID = Normalize(gameObjectID);
        if (string.IsNullOrEmpty(normalizedID))
        {
            return false;
        }

        if (Registry.TryGetValue(normalizedID, out result) && result != null && result.Matches(normalizedID))
        {
            return true;
        }

        GameObjectID[] allIDs = Resources.FindObjectsOfTypeAll<GameObjectID>();
        for (int i = 0; i < allIDs.Length; i++)
        {
            GameObjectID candidate = allIDs[i];
            if (candidate == null || !candidate.gameObject.scene.IsValid() || !candidate.Matches(normalizedID))
            {
                continue;
            }

            Register(candidate);
            result = candidate;
            return true;
        }

        return false;
    }

    public static bool TryFindGameObject(string gameObjectID, out GameObject target)
    {
        target = null;
        if (!TryFind(gameObjectID, out GameObjectID idComponent) || idComponent == null)
        {
            return false;
        }

        target = idComponent.gameObject;
        return target != null;
    }

    private void EnsureID()
    {
        if (!string.IsNullOrWhiteSpace(gameObjectID))
        {
            gameObjectID = gameObjectID.Trim();
            return;
        }

        gameObjectID = GenerateID();
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
        }
#endif
    }

    private static void Register(GameObjectID idComponent)
    {
        if (idComponent == null || string.IsNullOrWhiteSpace(idComponent.gameObjectID))
        {
            return;
        }

        string normalizedID = Normalize(idComponent.gameObjectID);
        if (Registry.TryGetValue(normalizedID, out GameObjectID existing) && existing != null && existing != idComponent)
        {
            Debug.LogWarning(
                $"Duplicate GameObjectID '{normalizedID}' found on '{existing.name}' and '{idComponent.name}'. Regenerate one of them.",
                idComponent);
        }

        Registry[normalizedID] = idComponent;
    }

    private static void Unregister(GameObjectID idComponent)
    {
        if (idComponent == null || string.IsNullOrWhiteSpace(idComponent.gameObjectID))
        {
            return;
        }

        string normalizedID = Normalize(idComponent.gameObjectID);
        if (Registry.TryGetValue(normalizedID, out GameObjectID existing) && existing == idComponent)
        {
            Registry.Remove(normalizedID);
        }
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string GenerateID()
    {
        return Guid.NewGuid().ToString("N");
    }
}
