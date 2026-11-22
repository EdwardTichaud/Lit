using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AurellReunionAnchor))]
public sealed class AurellReunionAnchorInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (!((AurellReunionAnchor)target).placementConfirmed)
            EditorGUILayout.HelpBox("Placez cette racine au milieu de Moonrise, au sol et hors combat, puis cochez Placement Confirmed. L’introduction fonctionne déjà ; Aurell reste masqué en jeu tant que son placement n’est pas confirmé.", MessageType.Warning);
    }
}
