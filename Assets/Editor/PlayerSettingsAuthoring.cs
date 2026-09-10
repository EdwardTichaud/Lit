using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal static class PlayerSettingsAuthoring
{
    internal static CharacterData ResolveData(Component component)
    {
        var info = component.GetComponentInParent<CharacterInfo>();
        if (info != null && info.SourceData != null) return info.SourceData;
        var squad = component.GetComponentInParent<SquadCharacterController>();
        if (squad != null && squad.CharacterData != null) return squad.CharacterData;
        string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(component.gameObject);
        var prefab = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        CharacterData found = null;
        foreach (string guid in AssetDatabase.FindAssets("t:CharacterData"))
        {
            var data = AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(guid));
            if (data == null || data.worldPrefab == null) continue;
            bool match = prefab != null ? data.worldPrefab == prefab : data.worldPrefab.name == component.transform.root.name;
            if (!match) continue;
            if (found != null && found != data) return null;
            found = data;
        }
        return found;
    }
    internal static SerializedProperty FindProperty(SerializedObject owner, string name)
    {
        var direct = owner.FindProperty(name);
        if (direct != null || !(owner.targetObject is LitOpsiveLocomotionBridge bridge)) return direct;
        var data = ResolveData(bridge);
        return data != null ? new SerializedObject(data).FindProperty("playerSettings.locomotion." + name) : null;
    }
    private static readonly string[] Titles = { "Controle et locomotion", "Saut et atterrissage", "Esquive et mouvements d action", "Combat et equipement", "Animation et presentation", "Interactions et suivi", "Voix, visage et pas", "Diagnostics" };
    private static readonly string[] Help = {
        "Deplacement, orientation, vol et franchissement via UCC.", "Impulsion, gravite, contact et reprise du mouvement.",
        "Direction, impulsion et freinage des actions de mobilite.", "Actions equipees et ressources de combat du personnage.",
        "Contrat de parametres Animator et bibliotheques de trajectoires.", "Conditions autorisant les interactions du personnage.",
        "Configuration independante des expressions et contacts sonores. Les voix restent dans Voice Lines.", "Diagnostics de developpement; desactives par defaut."
    };
    private static int Category(SerializedProperty block, SerializedProperty field)
    {
        if (field.name.StartsWith("log", StringComparison.OrdinalIgnoreCase) || field.name.StartsWith("debug", StringComparison.OrdinalIgnoreCase) || field.name.Contains("Diagnostic") || field.name.StartsWith("record")) return 7;
        if (block.name == "jump") return 1;
        if (block.name == "dodge" || block.name == "dash") return 2;
        if (block.name == "equipment") return 3;
        if (block.name == "trajectories" || field.name.EndsWith("Param", StringComparison.Ordinal)) return 4;
        if (block.name == "interactions") return 5;
        if (block.name == "face" || block.name == "footsteps") return 6;
        return 0;
    }
    internal static void Draw(SerializedProperty settings)
    {
        for (int category = 0; category < Titles.Length; category++)
        {
            var fields = new List<SerializedProperty>();
            var block = settings.Copy(); var end = settings.GetEndProperty();
            bool first = true;
            while (block.NextVisible(first) && !SerializedProperty.EqualContents(block, end))
            {
                first = false;
                var field = block.Copy(); var blockEnd = block.GetEndProperty(); bool child = true;
                while (field.NextVisible(child) && !SerializedProperty.EqualContents(field, blockEnd))
                {
                    child = false;
                    if (Category(block, field) == category) fields.Add(field.Copy());
                }
            }
            if (fields.Count == 0) continue;
            string key = "Lit.PlayerSettings." + category;
            bool open = EditorGUILayout.Foldout(SessionState.GetBool(key, false), new GUIContent(Titles[category], Help[category]), true);
            SessionState.SetBool(key, open);
            if (!open) continue;
            EditorGUI.indentLevel++;
            foreach (var field in fields) EditorGUILayout.PropertyField(field, new GUIContent(field.displayName, field.tooltip), true);
            EditorGUI.indentLevel--;
        }
    }
}
