using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SkillProtectionProfile))]
public sealed class SkillProtectionProfileDrawer : PropertyDrawer
{
    private static string[] Fields(SkillProtectionKind kind)
    {
        switch (kind)
        {
            case SkillProtectionKind.Intercession: return new[] { "radius", "durationSeconds", "maximumLinkDistance", "reduction", "transferFraction" };
            case SkillProtectionKind.Rempart: return new[] { "radius", "reduction" };
            case SkillProtectionKind.Sommation: return new[] { "radius", "durationSeconds" };
            case SkillProtectionKind.JusteRetour: return new[] { "animationSlot", "reserveMaxHealthFraction" };
            default: return new string[0];
        }
    }
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        (1 + Fields((SkillProtectionKind)property.FindPropertyRelative("kind").enumValueIndex).Length) * (EditorGUIUtility.singleLineHeight + 2);
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        position.height = EditorGUIUtility.singleLineHeight;
        EditorGUI.PropertyField(position, property.FindPropertyRelative("kind"), new GUIContent("Protection"));
        foreach (string field in Fields((SkillProtectionKind)property.FindPropertyRelative("kind").enumValueIndex))
        {
            position.y += EditorGUIUtility.singleLineHeight + 2;
            EditorGUI.PropertyField(position, property.FindPropertyRelative(field));
        }
        EditorGUI.EndProperty();
    }
}
