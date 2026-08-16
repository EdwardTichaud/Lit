#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class CombatCinematicClearanceSetup
{
    private static readonly string[] ActorPrefabPaths =
    {
        "Assets/Characters/1_Squad/Lucian/Player_Model_Lucian.prefab",
        "Assets/Characters/3_Enemy/Juggernaut/Juggernaut_Combat.prefab",
        "Assets/Characters/3_Enemy/GiantJuggernaut/GiantJuggernaut.prefab"
    };

    [MenuItem("Lit/Combat/Configure LightSkill Clearance")]
    private static void Configure()
    {
        if (!EditorUtility.DisplayDialog(
                "LightSkill Clearance",
                "Ajouter ou mettre a jour les proxies de degagement sur Lucian, Juggernaut et GiantJuggernaut ?",
                "Configurer",
                "Annuler")) return;

        int configured = 0;
        for (int i = 0; i < ActorPrefabPaths.Length; i++)
        {
            string path = ActorPrefabPaths[i];
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                CombatCinematicClearanceProxy proxy = root.GetComponent<CombatCinematicClearanceProxy>();
                if (proxy == null) proxy = root.AddComponent<CombatCinematicClearanceProxy>();
                CharacterController characterController = root.GetComponentInChildren<CharacterController>(true);
                Collider source = characterController != null
                    ? characterController
                    : root.GetComponentInChildren<CapsuleCollider>(true);
                if (source != null) proxy.ConfigureFrom(source);
                EditorUtility.SetDirty(proxy);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                configured++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[LightSkill Clearance] " + configured + " actor prefab(s) configured. Rebakez ensuite chaque LightSkill.");
    }
}
#endif
