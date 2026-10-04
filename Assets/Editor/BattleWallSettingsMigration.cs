#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-time project migration from the former per-prefab/boss BattleWall
/// switches to CharacterData.enemySettings.ArenaCreateBattleWall.
/// </summary>
[InitializeOnLoad]
internal static class BattleWallSettingsMigration
{
    private const string SessionKey = "Lit.BattleWallSettingsMigration.v1";

    static BattleWallSettingsMigration()
    {
        EditorApplication.delayCall += MigrateOnceAfterReload;
    }

    [MenuItem("Lit/Combat/Migrate BattleWall Settings")]
    private static void MigrateFromMenu()
    {
        Migrate(force: true);
    }

    private static void MigrateOnceAfterReload()
    {
        if (SessionState.GetBool(SessionKey, false))
        {
            return;
        }

        Migrate(force: false);
        SessionState.SetBool(SessionKey, true);
    }

    private static void Migrate(bool force)
    {
        bool changed = false;
        changed |= SetArenaWall("Assets/Scenes/Cycles/Cycle_Etienne/Data/CharacterData_DeadWeight.asset", false);
        changed |= SetArenaWall("Assets/Scenes/Cycles/Cycle_Belmont/Data/CharacterData_VeilleurDOmbre.asset", true);
        changed |= SetArenaWall("Assets/Scenes/Cycles/Cycle_Belmont/Data/CharacterData_BrokenAnchor.asset", true);
        changed |= SetArenaWall("Assets/Scenes/Cycles/Cycle_Ardent/Data/CharacterData_FalseChoir.asset", true);

        if (!changed && !force)
        {
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.ForceReserializeAssets(new List<string>
        {
            "Assets/Scenes/Cycles/Cycle_Etienne/Prefabs/Prefab_DeadWeight.prefab",
            "Assets/Scenes/Cycles/Cycle_Belmont/Prefabs/ShadowGuardian.prefab",
            "Assets/Scenes/Cycles/Cycle_Belmont/Prefabs/BrokenAnchor.prefab",
            "Assets/Scenes/Cycles/Cycle_Ardent/Prefabs/Prefab_FalseChoir.prefab",
            "Assets/Characters/9_Ghosts/Luc/Enemy_Model_MadScientist.prefab",
            "Assets/Characters/3_Enemy/GiantJuggernaut/GiantJuggernaut.prefab",
            "Assets/Scenes/Cycles/Cycle_Etienne/District_1_Cycle_Etienne.unity",
            "Assets/Scenes/Cycles/Cycle_Etienne/Data/BossDefinition_DeadWeight.asset",
            "Assets/Scenes/Cycles/Cycle_Belmont/Data/BossDefinition_BrokenAnchor.asset",
            "Assets/Scenes/Cycles/Cycle_Ardent/Data/BossDefinition_FalseChoir.asset"
        });

        Debug.Log("[BattleWallMigration] BattleWall centralise dans CharacterData.enemySettings.");
    }

    private static bool SetArenaWall(string assetPath, bool enabled)
    {
        CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(assetPath);
        if (data == null)
        {
            Debug.LogWarning("[BattleWallMigration] CharacterData introuvable : " + assetPath);
            return false;
        }

        data.enemySettings ??= new EnemySettings();
        if (data.enemySettings.ArenaCreateBattleWall == enabled)
        {
            return false;
        }

        data.enemySettings.ArenaCreateBattleWall = enabled;
        EditorUtility.SetDirty(data);
        return true;
    }
}
#endif
