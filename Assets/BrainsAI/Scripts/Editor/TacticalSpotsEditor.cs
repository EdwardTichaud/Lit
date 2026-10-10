using UnityEditor;

namespace Ultrabolt.BrainsAI
{
    /// <summary>
    /// Custom Inspector for <see cref="TacticalSpots"/>.
    /// Provides a structured interface to configure tactical grid generation,
    /// visualization, and AI spot parameters directly in the Unity Inspector.
    /// </summary>
    [CustomEditor(typeof(TacticalSpots))]
    public class TacticalSpotsEditor : Editor
    {
        private void DrawSimpleBoxGroup(string groupName, string iconName, string[] fields) =>
            StaticMethods.CreateSimpleBoxGroup(serializedObject, groupName, iconName, fields);

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // --- Inspector Section ---
            DrawSimpleBoxGroup("Draw AI Gizmos", "ReflectionProbeSelector@2x",
                new string[] { "drawGizmos", "useDrawingRange" });

            DrawSimpleBoxGroup("Main Settings", "AudioMixerController On Icon",
                new string[] { "target", "obstacleMask" });

            DrawSimpleBoxGroup("Grid Settings", "d_Mesh Icon",
                new string[] { "gridSize", "horizontalResolution", "verticalRange", "verticalResolution" });

            DrawSimpleBoxGroup("Spot Settings", "winbtn_mac_max@2x",
                new string[] { "agentHeight", "shadowBuffer" });

            // --- Information Box ---
            EditorGUILayout.HelpBox(
                "Setting the 'Target' is used only to preview the tactical grid in the Scene view.\n" +
                "This target has no effect on gameplay.\n\n" +
                "Spot point colors:\n" +
                "- Red = Dangerous / Visible spot\n" +
                "- Green = Safe / Hidden spot",
                MessageType.Info
            );

            serializedObject.ApplyModifiedProperties();
        }
    }
}
