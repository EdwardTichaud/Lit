#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace Ultrabolt.BrainsAI
{
    /// <summary>
    /// A static helper class that provides reusable methods for creating clean and consistent
    /// custom editor GUI layouts in the Unity Editor (e.g., box groups, foldout groups, sliders, and enum buttons).
    /// </summary>
    public static class StaticMethods
    {
        #region Create Simple Groups

        /// <summary>
        /// Creates a simple boxed group with a title and icon, and automatically draws a list of serialized fields inside.
        /// </summary>
        /// <param name="s">SerializedObject containing the fields.</param>
        /// <param name="groupName">The displayed group title.</param>
        /// <param name="iconName">The name of the built-in editor icon to use.</param>
        /// <param name="fields">The list of field property names to draw inside the group.</param>
        public static void CreateSimpleBoxGroup(SerializedObject s, string groupName, string iconName, string[] fields)
        {
            GUIContent contentGUI = EditorGUIUtility.IconContent(iconName);
            contentGUI.text = $" {groupName}";

            CreateBoxGroup(contentGUI, () => { DrawFields(s, fields); });
        }

        /// <summary>
        /// Creates a simple boxed group with a title, and automatically draws a list of serialized fields inside.
        /// </summary>
        /// <param name="s">SerializedObject containing the fields.</param>
        /// <param name="groupName">The displayed group title.</param>
        /// <param name="fields">The list of field property names to draw inside the group.</param>
        public static void CreateSimpleBoxGroup(SerializedObject s, string groupName, string[] fields)
        {
            GUIContent contentGUI = new($" {groupName}");
            CreateBoxGroup(contentGUI, () => { DrawFields(s, fields); });
        }

        /// <summary>
        /// Creates a simple foldout group with a title and icon, and automatically draws a list of serialized fields when expanded.
        /// </summary>
        /// <param name="s">SerializedObject containing the fields.</param>
        /// <param name="groupName">The displayed group title.</param>
        /// <param name="iconName">The name of the built-in editor icon to use.</param>
        /// <param name="fields">The list of field property names to draw inside the foldout.</param>
        /// <param name="value">Reference to a bool controlling whether the foldout is expanded.</param>
        public static void CreateSimpleFoldoutGroup(SerializedObject s, string groupName, string iconName, string[] fields, ref bool value)
        {
            GUIContent contentGUI = EditorGUIUtility.IconContent(iconName);
            contentGUI.text = $" {groupName}";

            CreateFoldoutGroup(contentGUI, ref value, () => { DrawFields(s, fields); });
        }

        /// <summary>
        /// Creates a simple foldout group with a title, and automatically draws a list of serialized fields when expanded.
        /// </summary>
        /// <param name="s">SerializedObject containing the fields.</param>
        /// <param name="groupName">The displayed group title.</param>
        /// <param name="fields">The list of field property names to draw inside the foldout.</param>
        /// <param name="value">Reference to a bool controlling whether the foldout is expanded.</param>
        public static void CreateSimpleFoldoutGroup(SerializedObject s, string groupName, string[] fields, ref bool value)
        {
            GUIContent contentGUI = new($" {groupName}");
            CreateFoldoutGroup(contentGUI, ref value, () => { DrawFields(s, fields); });
        }

        private static void DrawFields(SerializedObject s, string[] fields)
        {
            for (int i = 0; i < fields.Length; i++)
                DrawField(s, fields[i]);
        }
        #endregion

        #region Create Groups

        /// <summary>
        /// Creates a stylized vertical "box" group with a bold label header and custom content inside.
        /// </summary>
        /// <param name="label">The label with optional icon to display at the top of the group.</param>
        /// <param name="drawContent">A callback to draw the group's inner content.</param>
        public static void CreateBoxGroup(GUIContent label, System.Action drawContent)
        {
            EditorGUILayout.BeginVertical("box");

            // Header
            EditorGUILayout.BeginVertical("helpbox");
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();

            // Content
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(20f); // Indent content
            EditorGUILayout.BeginVertical();

            drawContent?.Invoke();

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Creates a foldout group with a header and collapsible content section.
        /// </summary>
        /// <param name="label">The label with optional icon to display at the top of the foldout.</param>
        /// <param name="foldout">Reference to a bool controlling whether the foldout is expanded.</param>
        /// <param name="drawContent">A callback to draw the group's inner content when expanded.</param>
        public static void CreateFoldoutGroup(GUIContent label, ref bool foldout, System.Action drawContent)
        {
            EditorGUILayout.BeginVertical("box");

            // Foldout header
            EditorGUILayout.BeginVertical("box");
            foldout = DrawCustomFoldout(label, foldout);
            EditorGUILayout.EndVertical();

            // Content when expanded
            if (foldout)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20f);
                EditorGUILayout.BeginVertical();

                drawContent?.Invoke();

                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }
        #endregion

        #region Helpers

        /// <summary>
        /// Draws a custom foldout header with an icon and label.
        /// </summary>
        /// <param name="iconLabel">The label with icon.</param>
        /// <param name="foldout">Current foldout state.</param>
        /// <returns>Updated foldout state after drawing.</returns>
        private static bool DrawCustomFoldout(GUIContent iconLabel, bool foldout)
        {
            EditorGUILayout.BeginVertical("helpbox");

            // Reserve space for the foldout
            Rect rect = EditorGUILayout.GetControlRect();
            rect = EditorGUI.IndentedRect(rect);

            // Foldout arrow rect
            Rect toggleRect = new Rect(rect.x + 15f, rect.y, 18f, rect.height);
            // Label rect next to arrow
            Rect labelRect = new Rect(toggleRect.xMax - 15f, rect.y, rect.width - toggleRect.width, rect.height);

            // Foldout arrow
            foldout = EditorGUI.Foldout(toggleRect, foldout, GUIContent.none, true);
            // Label with icon
            EditorGUI.LabelField(labelRect, iconLabel, EditorStyles.boldLabel);

            EditorGUILayout.EndVertical();
            return foldout;
        }
        #endregion

        #region Field Drawers

        /// <summary>
        /// Draws a serialized property field by name.
        /// </summary>
        public static void DrawField(SerializedObject s, string propertyName) =>
            EditorGUILayout.PropertyField(s.FindProperty(propertyName));

        /// <summary>
        /// Draws an enum as a horizontal toolbar with buttons for each value.
        /// </summary>
        /// <typeparam name="T">Enum type.</typeparam>
        /// <param name="current">Current selected enum value.</param>
        /// <param name="label">Optional label above the toolbar.</param>
        /// <returns>New selected enum value.</returns>
        public static T DrawEnumAsButtons<T>(T current, string label = "") where T : System.Enum
        {
            // Optional centered label
            if (!string.IsNullOrEmpty(label))
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(label, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            var values = System.Enum.GetValues(typeof(T));
            int count = values.Length;
            int index = 0;

            foreach (T value in values)
            {
                bool isSelected = value.Equals(current);

                // Pick the right style for button position (left/mid/right)
                GUIStyle style;
                if (index == 0)
                    style = isSelected ? EditorStyles.miniButtonLeft : EditorStyles.miniButtonLeft;
                else if (index == count - 1)
                    style = isSelected ? EditorStyles.miniButtonRight : EditorStyles.miniButtonRight;
                else
                    style = isSelected ? EditorStyles.miniButtonMid : EditorStyles.miniButtonMid;

                // Toggle button
                if (GUILayout.Toggle(isSelected, value.ToString(), style, GUILayout.MinWidth(50)))
                    current = value;

                index++;
            }

            GUILayout.EndHorizontal();
            return current;
        }

        /// <summary>
        /// Draws a labeled MinMaxSlider for a Vector2 value.
        /// </summary>
        public static void DrawMinMaxSlider(string label, ref Vector2 value, float minValue = 0, float maxValue = 100)
        {
            EditorGUILayout.BeginHorizontal();

            if (!string.IsNullOrEmpty(label))
                EditorGUILayout.LabelField(label, GUILayout.Width(EditorGUIUtility.labelWidth));

            // Min value field
            value.x = EditorGUILayout.FloatField(value.x, GUILayout.Width(60));
            // Slider
            EditorGUILayout.MinMaxSlider(ref value.x, ref value.y, minValue, maxValue);
            // Max value field
            value.y = EditorGUILayout.FloatField(value.y, GUILayout.Width(60));

            EditorGUILayout.EndHorizontal();

            // Clamp values
            value.x = Mathf.Clamp(value.x, minValue, value.y);
            value.y = Mathf.Clamp(value.y, minValue, maxValue);
        }

        /// <summary>
        /// Draws a labeled MinMaxSlider for a Vector2Int value.
        /// </summary>
        public static void DrawMinMaxSlider(string label, ref Vector2Int value, float minValue = 0, float maxValue = 100)
        {
            EditorGUILayout.BeginHorizontal();

            if (!string.IsNullOrEmpty(label))
                EditorGUILayout.LabelField(label, GUILayout.Width(EditorGUIUtility.labelWidth));

            // Min value field
            value.x = (int)EditorGUILayout.FloatField(value.x, GUILayout.Width(60));

            // Slider using temporary Vector2
            Vector2 amount = new Vector2(value.x, value.y);
            EditorGUILayout.MinMaxSlider(ref amount.x, ref amount.y, minValue, maxValue);
            value.x = Mathf.RoundToInt(amount.x);
            value.y = Mathf.RoundToInt(amount.y);

            // Max value field
            value.y = (int)EditorGUILayout.FloatField(value.y, GUILayout.Width(60));

            EditorGUILayout.EndHorizontal();

            // Clamp values
            value.x = Mathf.Clamp(value.x, (int)minValue, value.y);
            value.y = Mathf.Clamp(value.y, (int)minValue, (int)maxValue);
        }
        #endregion
    }
}
#endif
