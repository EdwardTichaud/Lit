using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AbilityService))]
internal sealed class AbilityServiceInspector : Editor
{
    public override void OnInspectorGUI()
    {
        var service = (AbilityService)target;
        EditorGUILayout.LabelField("Capacités · Test solo", EditorStyles.boldLabel);
        if (!Application.isPlaying)
        {
            DrawTestDefaults();
            return;
        }
        EditorGUILayout.HelpBox(service.CanEditTestAbilities
            ? "Les changements ci-dessous ne valent que pour cette session. Ils ne modifient ni les valeurs du prefab ni la sauvegarde."
            : "Disponible dans une session solo en mode Play.", MessageType.Info);
        if (!service.CanEditTestAbilities) return;

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Tout activer"))
                foreach (var ability in service.Catalog) service.SetEditorTestAbility(ability, true);
            if (GUILayout.Button("Effacer les tests"))
                foreach (var ability in service.Catalog) service.SetEditorTestAbility(ability, false);
        }

        foreach (var ability in service.Catalog)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                bool active = service.HasEditorTestAbility(ability);
                string state = active ? "Test actif" : service.IsUnlocked(ability) ? "Débloquée" : "Verrouillée";
                EditorGUILayout.LabelField(ability.DisplayName, state);
                if (GUILayout.Button(active ? "Arrêter le test" : "Activer pour le test", GUILayout.Width(145)))
                    service.SetEditorTestAbility(ability, !active);
            }
        }
    }

    private void DrawTestDefaults()
    {
        EditorGUILayout.LabelField("Activer par défaut en test solo", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Coche les capacités à débloquer et équiper dès le démarrage d’une session solo dans Unity. Les choix sont conservés dans le prefab, pas dans la sauvegarde de jeu.", MessageType.Info);
        serializedObject.Update();
        var defaults = serializedObject.FindProperty("editorTestDefaults");
        var abilities = Resources.LoadAll<AbilitySO>("Abilities");
        Array.Sort(abilities, (a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.DisplayName, b.DisplayName));
        foreach (var ability in abilities)
        {
            int index = -1;
            for (int i = 0; i < defaults.arraySize; i++)
                if (defaults.GetArrayElementAtIndex(i).objectReferenceValue == ability) { index = i; break; }
            bool wasSelected = index >= 0;
            bool selected = EditorGUILayout.ToggleLeft(ability.DisplayName, wasSelected);
            if (selected == wasSelected) continue;
            if (selected)
            {
                int nextIndex = defaults.arraySize;
                defaults.InsertArrayElementAtIndex(nextIndex);
                defaults.GetArrayElementAtIndex(nextIndex).objectReferenceValue = ability;
            }
            else
            {
                defaults.GetArrayElementAtIndex(index).objectReferenceValue = null;
                defaults.DeleteArrayElementAtIndex(index);
            }
        }
        serializedObject.ApplyModifiedProperties();
    }
}
