#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Fast editor audit for the fixed gamepad map and the Player_Model animation contract.</summary>
public static class PlayerModelInputAnimationAudit
{
    private const string ControllerPath = "Assets/Characters/4_Animations/Player_Model.controller";
    private const string ProfilePath = "Assets/CombatRealTime/Actors/PlayerModelAnimationProfile.asset";
    private const string LucianPrefabPath = "Assets/Characters/1_Squad/Lucian/Player_Model_Lucian.prefab";
    private const string InputActionsPath = "Assets/PlayerInputs.inputactions";

    [MenuItem("Lit/Combat/Audit Player Model Inputs & Animation")]
    public static void Audit()
    {
        int errors = 0;
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        PlayerModelAnimationProfile profile = AssetDatabase.LoadAssetAtPath<PlayerModelAnimationProfile>(ProfilePath);
        if (controller == null) errors += Report("AnimatorController introuvable : " + ControllerPath);
        if (profile == null) errors += Report("Profil Player_Model introuvable : " + ProfilePath);

        HashSet<string> states = controller != null ? CollectStatePaths(controller) : new HashSet<string>();
        if (profile != null)
        {
            foreach (string statePath in profile.GetRequiredStatePaths())
            {
                if (!states.Contains(statePath)) errors += Report("Etat requis absent de Player_Model : " + statePath);
            }
        }

        errors += AuditSkills(profile, states);
        errors += AuditPlayerPrefab(profile);
        errors += AuditInputActions();
        if (errors == 0)
        {
            Debug.Log("[PlayerModel Audit] OK : inputs gamepad et contrat d'animation valides.");
        }
        else
        {
            Debug.LogError("[PlayerModel Audit] " + errors + " erreur(s) detectee(s). Voir les logs precedents.");
        }
    }

    private static int AuditSkills(PlayerModelAnimationProfile profile, HashSet<string> states)
    {
        int errors = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/CombatRealTime/Skills" }))
        {
            SkillSO skill = AssetDatabase.LoadAssetAtPath<SkillSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (skill == null || string.IsNullOrWhiteSpace(skill.AnimatorState)) continue;
            string statePath = profile != null ? profile.NormalizeStatePath(skill.AnimatorState) : skill.AnimatorState.Trim();
            if (!states.Contains(statePath)) errors += Report("SkillSO '" + skill.name + "' pointe vers un etat absent : " + statePath);
        }

        return errors;
    }

    private static int AuditPlayerPrefab(PlayerModelAnimationProfile profile)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(LucianPrefabPath);
        try
        {
            CombatActorAnimationRoot contract = root.GetComponent<CombatActorAnimationRoot>();
            if (contract == null) return Report("CombatActorAnimationRoot absent du prefab Lucian.");
            if (!contract.ValidateContract(out string error)) return Report("Contrat Lucian invalide : " + error);
            return contract.AnimationProfile == profile ? 0 : Report("Le prefab Lucian ne reference pas PlayerModelAnimationProfile.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static int AuditInputActions()
    {
        InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        if (actions == null) return Report("Asset d'inputs introuvable : " + InputActionsPath);
        InputActionMap player = actions.FindActionMap("Player", false);
        InputActionMap combat = actions.FindActionMap("RealTimeCombat", false);
        int errors = 0;
        if (player == null) errors += Report("Action map Player absente.");
        if (combat == null) errors += Report("Action map RealTimeCombat absente.");
        if (player != null)
        {
            foreach (string action in new[] { "Move", "Interact", "Return", "Inventory", "SwitchTarget", "LightSkill" })
            {
                if (player.FindAction(action, false) == null) errors += Report("Action Player absente : " + action);
            }
        }
        if (combat != null)
        {
            foreach (string action in new[] { "BasicAttack", "Counter", "Dodge", "Jump", "OpenPalette", "NavigatePalette", "ConfirmPalette", "SwitchEnemyLock", "LightSkill" })
            {
                if (combat.FindAction(action, false) == null) errors += Report("Action combat absente : " + action);
            }
        }

        return errors;
    }

    private static HashSet<string> CollectStatePaths(AnimatorController controller)
    {
        HashSet<string> result = new HashSet<string>();
        foreach (AnimatorControllerLayer layer in controller.layers) CollectStates(layer.stateMachine, layer.name, result);
        return result;
    }

    private static void CollectStates(AnimatorStateMachine machine, string path, ISet<string> destination)
    {
        foreach (ChildAnimatorState child in machine.states) destination.Add(path + "." + child.state.name);
        foreach (ChildAnimatorStateMachine child in machine.stateMachines)
            CollectStates(child.stateMachine, path + "." + child.stateMachine.name, destination);
    }

    private static int Report(string message)
    {
        Debug.LogError("[PlayerModel Audit] " + message);
        return 1;
    }
}
#endif
