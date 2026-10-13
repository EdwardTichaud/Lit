using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SkillControlProfile))]
public sealed class SkillControlProfileDrawer : PropertyDrawer
{
    private static string[] Fields(SkillControlKind kind) => kind == SkillControlKind.Revelation ? new[] { "range", "durationSeconds", "strength" } :
        kind == SkillControlKind.Entrave ? new[] { "range", "durationSeconds", "radius", "strength" } :
        kind == SkillControlKind.PasDeRecul ? new[] { "distance", "durationSeconds" } : new string[0];
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        (1 + Fields((SkillControlKind)property.FindPropertyRelative("kind").enumValueIndex).Length) * (EditorGUIUtility.singleLineHeight + 2);
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property); position.height = EditorGUIUtility.singleLineHeight;
        EditorGUI.PropertyField(position, property.FindPropertyRelative("kind"), new GUIContent("Control"));
        foreach (var field in Fields((SkillControlKind)property.FindPropertyRelative("kind").enumValueIndex))
        { position.y += EditorGUIUtility.singleLineHeight + 2; EditorGUI.PropertyField(position, property.FindPropertyRelative(field)); }
        EditorGUI.EndProperty();
    }
}
