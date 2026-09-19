using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RuntimeOutlineInspector))]
public sealed class RuntimeOutlineInspectorEditor : Editor
{
    public override bool RequiresConstantRepaint() => Application.isPlaying;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("Contours des objets interactifs. En jeu : DontDestroyOnLoad > GameplaySessionRoot > OutlineManager.\nPour conserver vos reglages, modifiez le prefab GameplaySessionRoot hors Play.", MessageType.Info);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("showOutlines"), new GUIContent("Afficher les contours"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("outlineColor"), new GUIContent("Couleur et opacite"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("thickness"), new GUIContent("Epaisseur (pixels)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("alphaThreshold"), new GUIContent("Seuil de transparence"));
        serializedObject.ApplyModifiedProperties();

        var manager = (RuntimeOutlineInspector)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Etat en direct", EditorStyles.boldLabel);
        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Lancez le jeu pour voir la selection et les suspensions.", MessageType.None);
        else
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle("Rendu configure", manager.IsConfigured);
                EditorGUILayout.Toggle("Suspendu", RuntimeOutlineSelectionManager.IsSuspended);
                EditorGUILayout.Toggle("Combat actif", RealTimeCombatManager.Instance != null && RealTimeCombatManager.Instance.IsCombatActive);
                EditorGUILayout.ObjectField("Personnage / proprietaire", RuntimeOutlineSelectionManager.ActiveOwner, typeof(Object), true);
                EditorGUILayout.ObjectField("Interactif selectionne", RuntimeOutlineSelectionManager.ActiveInteractable as Object, typeof(Object), true);
                EditorGUILayout.LabelField("Cibles du contour", RuntimeOutlineSelectionManager.SelectedTargets.Count.ToString());
                foreach (var item in RuntimeOutlineSelectionManager.SelectedTargets)
                    EditorGUILayout.ObjectField(item, typeof(RuntimeOutlineTarget), true);
                EditorGUILayout.LabelField("Systemes suspendant le contour", RuntimeOutlineSelectionManager.CurrentSuspensionOwners.Count.ToString());
                foreach (var owner in RuntimeOutlineSelectionManager.CurrentSuspensionOwners)
                    EditorGUILayout.ObjectField(owner, typeof(Object), true);
            }
        }
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.ObjectField("Passes de rendu", manager.Volume, typeof(UnityEngine.Rendering.HighDefinition.CustomPassVolume), true);
        EditorGUILayout.HelpBox("Le seuil exclut les pixels transparents du masque. Augmentez-le pour resserrer les contours diffus. Chaque RuntimeOutlineTarget permet de choisir le canal d'opacite et la propriete texture pour un shader personnalise.", MessageType.Info);
    }
}
