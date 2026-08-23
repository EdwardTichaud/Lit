using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// One-shot migration from the retired turn-based scene wiring to the real-time combat UI.
/// It intentionally uses component names for retired systems so this utility remains valid
/// after their source files have been deleted.
/// </summary>
public static class RealTimeCombatMigrationUtility
{
    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/Bootstrap.unity",
        "Assets/Scenes/Arena.unity"
    };

    private static readonly string[] RetiredComponentNames =
    {
        "CombatSessionManager",
        "CombatHudController",
        "CombatTransitionController",
        "BattleTransition",
        "CombatCameraPresentationController",
        "CombatDefensePanelController",
        "CombatAggroEnemy",
        "CombatAnimationEvents",
        "TimeManager",
        "RealTimeCombatHud"
    };

    private static readonly string[] RetiredAssetPaths =
    {
        "Assets/Combat/AnimationEvents/CombatAnimationEvents.cs",
        "Assets/Combat/Presentation/CombatCounterItemPresentation.cs",
        "Assets/Combat/Presentation/CombatReactionClipPlayer.cs",
        "Assets/Combat/Scripts/CombatAggroEnemy.cs",
        "Assets/Combat/Scripts/CombatCameraPresentationController.cs",
        "Assets/Combat/Scripts/CombatEnemyDefinition.cs",
        "Assets/Combat/Scripts/CombatHudController.cs",
        "Assets/Combat/Scripts/CombatNetworkMessages.cs",
        "Assets/Combat/Scripts/CombatRuntimeEnemy.cs",
        "Assets/Combat/Scripts/CombatSessionManager.cs",
        "Assets/Combat/Scripts/CombatSessionState.cs",
        "Assets/Combat/Scripts/CombatTransitionController.cs",
        "Assets/Combat/Scripts/CombatTurn.cs",
        "Assets/Combat/Scripts/IustiaIdolPrayer.cs",
        "Assets/Combat/Scripts/RestoreHealthEffect.cs",
        "Assets/Combat/Scripts/TimeManager.cs",
        "Assets/Combat/Transition/BattleTransition.cs",
        "Assets/Combat/UI/CombatDefensePanelController.cs",
        "Assets/Editor/CombatSceneUiInstaller.cs"
    };

    [MenuItem("Lit/Combat/Migrate Active Turn Combat To Realtime")]
    public static void Migrate()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Migrate Active Combat",
                "This removes the active turn-based combat wiring from Bootstrap, Arena, the Juggernaut prefabs and PlayerInputs. " +
                "Assets/Legacy/BattleManager_SymphonieImport are not touched. Continue?",
                "Migrate",
                "Cancel"))
        {
            return;
        }

        string activeScenePath = SceneManager.GetActiveScene().path;
        try
        {
            for (int i = 0; i < ScenePaths.Length; i++)
            {
                MigrateScene(ScenePaths[i]);
            }

            MigrateGameplaySessionRoot();
            MigratePlayerPrefab("Assets/Characters/1_Squad/Lucian/Player_Model_Lucian.prefab");
            MigrateEnemyPrefab("Assets/Characters/3_Enemy/Juggernaut/Juggernaut_Combat.prefab");
            MigrateEnemyPrefab("Assets/Characters/3_Enemy/GiantJuggernaut/GiantJuggernaut.prefab");
            RemoveRetiredActionMap();
            MoveSharedAssets();
            DeleteRetiredAssets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[RealTimeCombatMigration] Migration complete. Reopen Bootstrap and Arena, then verify the scene-authored UI bindings.");
        }
        catch (Exception exception)
        {
            Debug.LogError("[RealTimeCombatMigration] Migration stopped: " + exception.Message);
            throw;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(activeScenePath))
            {
                EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);
            }
        }
    }

    private static void MigrateScene(string path)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        GameObject uiOverlay = FindSceneObject(scene, "UI_Overlay");
        if (uiOverlay == null)
        {
            throw new InvalidOperationException("UI_Overlay introuvable dans " + path + ".");
        }

        RealTimeCombatSceneUiController controller = uiOverlay.GetComponent<RealTimeCombatSceneUiController>();
        if (controller == null)
        {
            controller = Undo.AddComponent<RealTimeCombatSceneUiController>(uiOverlay);
        }

        CanvasGroup engaged = RequireCanvasGroup(scene, "CombatEngagedPanel");
        CanvasGroup infos = RequireCanvasGroup(scene, "CombatScreenInfosPanel");
        CanvasGroup victory = RequireCanvasGroup(scene, "VictoryPanel");
        CanvasGroup defeat = RequireCanvasGroup(scene, "DefeatPanel");
        Animator engagedAnimator = engaged.GetComponent<Animator>();
        if (engagedAnimator == null)
        {
            engagedAnimator = engaged.GetComponentInChildren<Animator>(true);
        }

        SerializedObject serialized = new SerializedObject(controller);
        Assign(serialized, "combatEngagedPanel", engaged);
        Assign(serialized, "combatEngagedAnimator", engagedAnimator);
        Assign(serialized, "combatScreenInfosPanel", infos);
        Assign(serialized, "victoryPanel", victory);
        Assign(serialized, "defeatPanel", defeat);
        Assign(serialized, "titleText", FindText(scene, "CombatTitleText"));
        Assign(serialized, "stateText", FindText(scene, "CombatTurnText"));
        Assign(serialized, "playerHpText", FindText(scene, "CombatPlayerHpText"));
        Assign(serialized, "enemyHpText", FindText(scene, "CombatEnemyHpText"));
        Assign(serialized, "clarityText", FindText(scene, "CombatPrayerText"));
        Assign(serialized, "combatLogText", FindText(scene, "CombatLog"));
        Assign(serialized, "playerHpFill", FindImage(scene, "CombatPlayerHpFill"));
        Assign(serialized, "enemyHpFill", FindImage(scene, "CombatEnemyHpFill"));
        Assign(serialized, "victoryContinueButton", FindButton(victory.transform, "continue", "continuer", "resume", "close", "fermer"));
        Assign(serialized, "defeatReviveButton", FindButton(defeat.transform, "revive", "revivre", "retry", "reessayer", "recommencer"));
        Assign(serialized, "defeatQuitButton", FindButton(defeat.transform, "quit", "quitter", "menu", "exit"));
        serialized.ApplyModifiedPropertiesWithoutUndo();

        RemoveComponents(scene, RetiredComponentNames);
        DestroySceneObject(scene, "CombatDefensePanel");
        DestroySceneObject(scene, "CombatTransitionController");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void MigrateGameplaySessionRoot()
    {
        const string path = "Assets/Core/System/GameplaySessionRoot.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            RemoveComponents(root, RetiredComponentNames);
            RemoveMissingScripts(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void MigratePlayerPrefab(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            RemoveComponents(root, RetiredComponentNames);
            RemoveMissingScripts(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void MigrateEnemyPrefab(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (FindComponentByName(root, "RealTimeCombatEnemy") == null ||
                FindComponentByName(root, "RealTimeCombatEnemyBehaviour") == null ||
                FindComponentByName(root, "EnemySkills") == null ||
                FindComponentByName(root, "VisionField") == null)
            {
                throw new InvalidOperationException("Prefab temps reel incomplet, migration annulee : " + path);
            }

            RemoveComponents(root, new[] { "CombatAggroEnemy", "CombatAnimationEvents" });
            RemoveMissingScripts(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void RemoveRetiredActionMap()
    {
        const string path = "Assets/PlayerInputs.inputactions";
        InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        if (actions == null)
        {
            throw new InvalidOperationException("PlayerInputs.inputactions introuvable.");
        }

        InputActionMap combat = actions.FindActionMap("Combat", false);
        if (combat != null)
        {
            actions.RemoveActionMap(combat);
            EditorUtility.SetDirty(actions);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
    }

    private static void MoveSharedAssets()
    {
        MoveAssetIfNeeded("Assets/Combat/Scripts/CombatHealth.cs", "Assets/CombatRealTime/Core/CombatHealth.cs");
        MoveAssetIfNeeded("Assets/Combat/Prefabs/AttackLightAlert.prefab", "Assets/CombatRealTime/Presentation/AttackLightAlert.prefab");
    }

    private static void MoveAssetIfNeeded(string source, string destination)
    {
        if (!AssetDatabase.IsValidFolder(System.IO.Path.GetDirectoryName(destination)))
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination));
        }

        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(destination) != null)
        {
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(source) != null)
        {
            string error = AssetDatabase.MoveAsset(source, destination);
            if (!string.IsNullOrEmpty(error))
            {
                throw new InvalidOperationException(error);
            }
        }
    }

    private static void DeleteRetiredAssets()
    {
        for (int i = 0; i < RetiredAssetPaths.Length; i++)
        {
            string path = RetiredAssetPaths[i];
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null && !AssetDatabase.DeleteAsset(path))
            {
                throw new InvalidOperationException("Suppression impossible : " + path);
            }
        }

        ArchiveBattleSphere();
    }

    private static void ArchiveBattleSphere()
    {
        const string source = "Assets/Combat/Prefabs/BattleSphere.prefab";
        const string archive = "Assets/Legacy/TurnBasedCombatRetired/BattleSphere.prefab";
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(source) == null ||
            AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(archive) != null)
        {
            return;
        }

        string directory = System.IO.Path.GetDirectoryName(archive);
        if (!AssetDatabase.IsValidFolder(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
            AssetDatabase.Refresh();
        }

        string error = AssetDatabase.MoveAsset(source, archive);
        if (!string.IsNullOrEmpty(error))
        {
            Debug.LogWarning("[RealTimeCombatMigration] BattleSphere reste inactive dans Assets/Combat : " + error);
        }
    }

    private static void RemoveComponents(Scene scene, IReadOnlyList<string> typeNames)
    {
        for (int rootIndex = 0; rootIndex < scene.rootCount; rootIndex++)
        {
            RemoveComponents(scene.GetRootGameObjects()[rootIndex], typeNames);
        }
    }

    private static void RemoveComponents(GameObject root, IReadOnlyList<string> typeNames)
    {
        MonoBehaviour[] components = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = components.Length - 1; i >= 0; i--)
        {
            MonoBehaviour component = components[i];
            if (component != null && Contains(typeNames, component.GetType().Name))
            {
                UnityEngine.Object.DestroyImmediate(component, true);
            }
        }
    }

    private static void RemoveMissingScripts(GameObject root)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transforms[i].gameObject);
        }
    }

    private static MonoBehaviour FindComponentByName(GameObject root, string typeName)
    {
        MonoBehaviour[] components = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] != null && components[i].GetType().Name == typeName)
            {
                return components[i];
            }
        }

        return null;
    }

    private static CanvasGroup RequireCanvasGroup(Scene scene, string objectName)
    {
        GameObject panel = FindSceneObject(scene, objectName);
        if (panel == null)
        {
            throw new InvalidOperationException(objectName + " introuvable dans " + scene.path + ".");
        }

        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        return group != null ? group : Undo.AddComponent<CanvasGroup>(panel);
    }

    private static TextMeshProUGUI FindText(Scene scene, string objectName)
    {
        GameObject target = FindSceneObject(scene, objectName);
        return target != null ? target.GetComponent<TextMeshProUGUI>() ?? target.GetComponentInChildren<TextMeshProUGUI>(true) : null;
    }

    private static Image FindImage(Scene scene, string objectName)
    {
        GameObject target = FindSceneObject(scene, objectName);
        return target != null ? target.GetComponent<Image>() ?? target.GetComponentInChildren<Image>(true) : null;
    }

    private static Button FindButton(Transform root, params string[] names)
    {
        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            string candidate = buttons[i].name.ToLowerInvariant();
            for (int nameIndex = 0; nameIndex < names.Length; nameIndex++)
            {
                if (candidate.Contains(names[nameIndex]))
                {
                    return buttons[i];
                }
            }
        }

        return buttons.Length > 0 ? buttons[0] : null;
    }

    private static void Assign(SerializedObject target, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException("Propriete UI introuvable : " + propertyName);
        }

        property.objectReferenceValue = value;
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        for (int i = 0; i < scene.rootCount; i++)
        {
            Transform result = FindRecursive(scene.GetRootGameObjects()[i].transform, objectName);
            if (result != null)
            {
                return result.gameObject;
            }
        }

        return null;
    }

    private static Transform FindRecursive(Transform root, string objectName)
    {
        if (root.name == objectName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindRecursive(root.GetChild(i), objectName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static void DestroySceneObject(Scene scene, string objectName)
    {
        GameObject target = FindSceneObject(scene, objectName);
        if (target != null)
        {
            UnityEngine.Object.DestroyImmediate(target, true);
        }
    }

    private static bool Contains(IReadOnlyList<string> values, string value)
    {
        for (int i = 0; i < values.Count; i++)
        {
            if (values[i] == value)
            {
                return true;
            }
        }

        return false;
    }
}
