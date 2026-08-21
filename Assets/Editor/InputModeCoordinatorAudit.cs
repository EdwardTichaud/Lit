#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public static class InputModeCoordinatorAudit
{
    private const string InputAssetPath = "Assets/PlayerInputs.inputactions";

    [MenuItem("Lit/Input/Audit ActionMap Profiles")]
    public static void Audit()
    {
        InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);
        if (asset == null)
        {
            Debug.LogError("[Input Audit] Asset introuvable : " + InputAssetPath);
            return;
        }

        int errors = 0;
        foreach (string map in new[] { "Player", "Camera", "RealTimeCombat", "Dialogue", "UI", "Placement", "CombatWheel", "System" })
        {
            if (asset.FindActionMap(map, false) == null)
            {
                errors++;
                Debug.LogError("[Input Audit] ActionMap absente : " + map);
            }
        }

        errors += CheckDuplicateBindings(asset, new[] { "Dialogue", "Camera" }, "Dialogue + Camera");
        errors += CheckDuplicateBindings(asset, new[] { "Placement", "Camera" }, "Placement + Camera");
        if (errors == 0) Debug.Log("[Input Audit] OK : profils exclusifs et maps requises valides.");
        else Debug.LogError("[Input Audit] " + errors + " erreur(s) detectee(s).");
    }

    private static int CheckDuplicateBindings(InputActionAsset asset, string[] maps, string profile)
    {
        Dictionary<string, string> controls = new Dictionary<string, string>();
        int errors = 0;
        foreach (string mapName in maps)
        {
            InputActionMap map = asset.FindActionMap(mapName, false);
            if (map == null) continue;
            foreach (InputBinding binding in map.bindings)
            {
                if (binding.isComposite || binding.isPartOfComposite || string.IsNullOrWhiteSpace(binding.path)) continue;
                if (controls.TryGetValue(binding.path, out string other))
                {
                    errors++;
                    Debug.LogError("[Input Audit] Binding duplique dans " + profile + " : " + binding.path + " (" + other + " / " + mapName + ").");
                }
                else controls.Add(binding.path, mapName);
            }
        }
        return errors;
    }
}
#endif
