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
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Third-person")) controller.SetMode(GameplayCameraMode.ThirdPerson);
            if (GUILayout.Button("Tactical")) controller.SetMode(GameplayCameraMode.Tactical);
        }
        if (GUILayout.Button("Recenter / exit inspection")) controller.Recenter();
        EditorGUILayout.HelpBox("Test room: Assets/CombatRealTime/Camera/TacticalCameraTestRoom.prefab. Place its floor at the character's feet in an empty area.", MessageType.Info);
    }
}
