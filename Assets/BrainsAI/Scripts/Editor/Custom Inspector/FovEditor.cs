using UnityEditor;

namespace Ultrabolt.BrainsAI
{
    [CustomEditor(typeof(FOV))]
    public class FovEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            FOV fov = (FOV)target;

            DrawSimpleBoxGroup("FOV Settings", "scenevis_visible_hover@2x",
                new string[] { "drawGizmos", "viewRadius", "viewAngle", "awarenessScale", "fovOffset" });

            DrawSimpleBoxGroup("Layers Settings", "d_FlareLayer Icon",
                new string[] { "targetMask", "obstacleMask" });

            DrawSimpleFoldoutGroup(fov.canInvestigate ? "Eyes Can Investigate" : "Eyes Can't Investigate", "d_Search Icon",
                new string[] { "investigateDuration" }, ref fov.canInvestigate);

            DrawSimpleFoldoutGroup("Unity Events", "AnimationWindowEvent Icon",
                new string[] { "onDetectTarget", "onLostTarget", "onEnterNone", "onEnterAlerted", "onEnterInvestigate" }, ref fov.showEvents);

            EditorGUI.BeginDisabledGroup(true);
            DrawField("status");
            EditorGUI.EndDisabledGroup();

            serializedObject.ApplyModifiedProperties();
        }

        void DrawField(string propertyName) =>
            StaticMethods.DrawField(serializedObject, propertyName);

        void DrawSimpleBoxGroup(string groupName, string iconName, string[] fields) =>
            StaticMethods.CreateSimpleBoxGroup(serializedObject, groupName, iconName, fields);

        void DrawSimpleFoldoutGroup(string groupName, string iconName, string[] fields, ref bool value) =>
            StaticMethods.CreateSimpleFoldoutGroup(serializedObject, groupName, iconName, fields, ref value);
    }
}
