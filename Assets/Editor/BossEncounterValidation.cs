#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Manual authoring audit for boss prefabs; normal enemies are deliberately ignored.</summary>
public static class BossEncounterValidation
{
    [MenuItem("Lit/Validation/Validate Boss Encounters")]
    public static void ValidateBossEncounters()
    {
        string[] prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        int errors = 0;
        foreach (string guid in prefabs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            BossEncounterBehaviour boss = prefab != null ? prefab.GetComponent<BossEncounterBehaviour>() : null;
            if (boss == null) continue;
            EnemyController enemy = prefab.GetComponent<EnemyController>();
            CharacterInfo health = prefab.GetComponent<CharacterInfo>();
            Unity.Netcode.NetworkObject network = prefab.GetComponent<Unity.Netcode.NetworkObject>();
            bool valid = enemy != null && health != null && network != null && boss.Definition != null && boss.MaximumSegments > 0 && EnemyController.HasRequiredComponents(prefab);
            if (valid) continue;
            errors++;
            Debug.LogError("[BossValidation] '" + path + "' needs EnemyController, CharacterInfo, NetworkObject, valid animator contract, BossDefinitionSO and positive segments.", prefab);
        }
        if (errors == 0) Debug.Log("[BossValidation] Boss prefabs are valid.");
    }
}
#endif
