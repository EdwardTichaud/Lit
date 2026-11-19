using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(DialogueDirection))]
public sealed class DialogueDirectionDrawer : PropertyDrawer
{
    private static string[] gestures;
    static DialogueDirectionDrawer() => EditorApplication.projectChanged += () => gestures = null;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        3 * (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (gestures == null)
            gestures = AssetDatabase.FindAssets("t:DialoguePresentationProfile")
                .Select(guid => AssetDatabase.LoadAssetAtPath<DialoguePresentationProfile>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(profile => profile != null).SelectMany(profile => profile.gestures)
                .Where(g => g.validated && g.role == DialogueRole.Speaking)
                .Select(g => g.id).Distinct().OrderBy(id => id, StringComparer.Ordinal).ToArray();
        EditorGUI.BeginProperty(position, label, property);
        position.height = EditorGUIUtility.singleLineHeight;
        EditorGUI.PropertyField(position, property.FindPropertyRelative("intent"), new GUIContent("Intention"));
        position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
        EditorGUI.PropertyField(position, property.FindPropertyRelative("intensity"), new GUIContent("Intensité"));
        position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
        var selected = property.FindPropertyRelative("gestureId");
        var available = new[] { "" }.Concat(gestures).ToList();
        if (!string.IsNullOrEmpty(selected.stringValue) && !available.Contains(selected.stringValue))
            available.Add(selected.stringValue);
        string[] labels = available.Select(id => string.IsNullOrEmpty(id) ? "Automatique" : id).ToArray();
        int index = Mathf.Max(0, available.IndexOf(selected.stringValue ?? ""));
        EditorGUI.BeginChangeCheck();
        int next = EditorGUI.Popup(position, "Premier geste", index, labels);
        if (EditorGUI.EndChangeCheck()) selected.stringValue = available[next];
        EditorGUI.EndProperty();
    }
}
