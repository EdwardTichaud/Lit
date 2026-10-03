using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LitGameplayCameraModeController))]
public sealed class LitTacticalCameraEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var controller = (LitGameplayCameraModeController)target;
        if (!Application.isPlaying) return;
        EditorGUILayout.LabelField("Requested", controller.RequestedMode.ToString());
        EditorGUILayout.LabelField("Authority", controller.ExternalControl ? "Cinematic (pending gameplay mode)" : controller.EffectiveMode.ToString());
        EditorGUILayout.LabelField("Obstacles — requested", controller.RequestedObstacleMode.ToString());
        EditorGUILayout.LabelField("Obstacles — effective", controller.EffectiveObstacleMode.ToString());
        EditorGUILayout.HelpBox("VisibilityMask only in follow. Free camera and cinematics restore the mask. Incompatible groups retain collisions/sliding.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Third-person")) controller.SetMode(GameplayCameraMode.ThirdPerson);
            if (GUILayout.Button("Tactical")) controller.SetMode(GameplayCameraMode.Tactical);
        }
        if (GUILayout.Button("Recenter / exit inspection")) controller.Recenter();
        EditorGUILayout.HelpBox("Test room: Assets/CombatRealTime/Camera/TacticalCameraTestRoom.prefab. Place its floor at the character's feet in an empty area.", MessageType.Info);
    }
}

[CustomEditor(typeof(LitCameraOcclusionGroup))]
public sealed class LitCameraOcclusionGroupEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var group = (LitCameraOcclusionGroup)target;
        bool compatible = group.IsMaskCompatible(out string diagnostic);
        EditorGUILayout.HelpBox(diagnostic, compatible ? MessageType.Info : MessageType.Warning);
    }
}
