using System;
using UnityEditor;
using UnityEngine;

internal static class EnemyAuthoringLayout
{
    private static readonly string[] Titles = { "Identité et présentation", "Statistiques et santé", "Compétences et attaques", "Détection et engagement", "Déplacement et physique", "Récupération et interruptions", "Animation et cinématiques", "Récompenses et données narratives" };
    private static readonly string[] Help = {
        "Identifie le personnage et choisit son portrait et son prefab.",
        "Définit ses caractéristiques et ses points de vie initiaux.",
        "Choisit les compétences disponibles et leurs enchaînements dans le profil de combat.",
        "Définit quand poursuivre, engager ou abandonner une cible.",
        "Règle le positionnement, les collisions et les mouvements des actions.",
        "Règle la garde, les interruptions et la reprise après une attaque.",
        "Associe les états d'animation et les séquences de paliers de santé.",
        "Configure les objets initiaux, les voix et les données narratives existantes." };
    internal static void DrawData(SerializedObject data)
    {
        data.Update();
        for (int category = 0; category < Titles.Length; category++)
        {
            string key = "Lit.EnemyAuthoring." + category;
            bool expanded = SessionState.GetBool(key, true);
            expanded = EditorGUILayout.Foldout(expanded, new GUIContent(Titles[category], Help[category]), true);
            SessionState.SetBool(key, expanded);
            if (!expanded) continue;
            EditorGUI.indentLevel++;
            var property = data.GetIterator();
            bool enter = true;
            while (property.NextVisible(enter))
            {
                enter = false;
                if (property.name == "m_Script") continue;
                if (property.name == "enemySettings")
                {
                    var child = property.Copy();
                    var end = property.GetEndProperty();
                    bool first = true;
                    while (child.NextVisible(first) && !SerializedProperty.EqualContents(child, end))
                    {
                        first = false;
                        if (Category(child.name) == category) Draw(child, Help[category]);
                    }
                }
                else if (Category(property.name) == category) Draw(property, Help[category]);
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(3);
        }
        data.ApplyModifiedProperties();
    }
    internal static void Draw(SerializedProperty property, string help)
    {
        string label = property.displayName;
        foreach (string prefix in new[] { "Actor ", "Skills ", "Physics ", "Locomotion ", "Navigation ", "Recovery ", "Brain ", "Contract " })
            if (label.StartsWith(prefix, StringComparison.Ordinal)) { label = label.Substring(prefix.Length); break; }
        EditorGUILayout.PropertyField(property, new GUIContent(label, string.IsNullOrEmpty(property.tooltip) ? help : property.tooltip), true);
    }
    private static int Category(string name)
    {
        if (name == "stats" || name == "hp") return 1;
        if (name == "skills" || name.Contains("BasicSkills") || name == "combatSkills" || name == "enemyCombatProfile" || name.StartsWith("Skills")) return 2;
        if (name.StartsWith("Navigation") || name.StartsWith("Brain")) return 3;
        if (name.StartsWith("Physics") || name.StartsWith("Locomotion")) return 4;
        if (name.StartsWith("Recovery") || name.Contains("Recovery") || name.Contains("Retaliation")) return 5;
        if (name.Contains("HealthThreshold") || name.StartsWith("Actor") || name.StartsWith("Contract")) return 6;
        if (name == "starterItemsWithQuantity" || name == "voiceLines" || name == "maisonWaitingPoint") return 7;
        return 0;
    }
}

[CustomEditor(typeof(CharacterInfo))]
public sealed class CharacterInfoEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("characterData"), new GUIContent("Fiche CharacterData", "Source des données copiées pour cette instance au lancement."));
        serializedObject.ApplyModifiedProperties();
        var info = (CharacterInfo)target;
        if (!Application.isPlaying) return;
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.IntField(new GUIContent("PV actuels", "État vivant de cette instance."), info.CurrentHp);
            EditorGUILayout.IntField("PV maximum", info.MaxHp);
            if (info.CharacterData != null) EnemyAuthoringLayout.DrawData(new SerializedObject(info.CharacterData));
        }
    }
}

[CustomEditor(typeof(EnemyController))]
public sealed class EnemyControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("Les réglages du comportement sont dans la fiche CharacterData. Ce composant coordonne l'ennemi et ses références de scène.", MessageType.Info);
        foreach (string field in new[] { "combatEnabled", "ActorAnimator", "ActorVisionField", "ActorEnemyLockPoint", "inputPromptAnchor" })
        {
            var property = serializedObject.FindProperty(field);
            if (property != null) EnemyAuthoringLayout.Draw(property, "Référence utilisée par le contrôleur ennemi.");
        }
        serializedObject.ApplyModifiedProperties();
        var enemy = (EnemyController)target;
        var info = enemy.GetComponent<CharacterInfo>();
        if (info != null && info.SourceData != null && GUILayout.Button("Ouvrir la fiche du personnage")) Selection.activeObject = info.SourceData;
        if (Application.isPlaying)
            EditorGUILayout.LabelField("État", enemy.Phase + " / " + enemy.State + " / " + enemy.Status);
    }
}

[CustomEditor(typeof(EnemyCombatProfileSO))]
public sealed class EnemyCombatProfileEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        foreach (var group in new[] {
            new[] { "Attaques et enchaînements", "Choisit les patterns autorisés, leur poids et leurs délais.", "patterns", "preferMeleeApproach", "airborneAlternativeChance" },
            new[] { "Engagement et poursuite", "Définit les distances de combat et les conditions de retour.", "preferredCombatDistance", "pursuitRadius", "disengagePauseSeconds", "returnReengageDistance", "trackingDegreesPerSecond" },
            new[] { "Observation et garde", "Règle les pauses de décision et la protection entre les attaques.", "observationSeconds", "guardChance", "guardCooldownSeconds", "guardDurationSeconds", "guardedDamageMultiplier" } })
        {
            string key = "Lit.EnemyProfile." + group[0];
            bool open = EditorGUILayout.Foldout(SessionState.GetBool(key, true), new GUIContent(group[0], group[1]), true);
            SessionState.SetBool(key, open);
            if (!open) continue;
            for (int i = 2; i < group.Length; i++) EnemyAuthoringLayout.Draw(serializedObject.FindProperty(group[i]), group[1]);
        }
        serializedObject.ApplyModifiedProperties();
    }
}
