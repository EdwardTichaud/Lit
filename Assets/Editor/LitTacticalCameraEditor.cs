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
        EditorGUILayout.LabelField("Maison visibility mask", controller.MaisonMaskActive ? "Active (camera distance preserved)" : "Inactive");
        EditorGUILayout.LabelField("Near clip — initial (m)", controller.InitialNearClip.ToString("F3"));
        EditorGUILayout.LabelField("Near clip — effective (m)", controller.EffectiveNearClip.ToString("F3"));
        EditorGUILayout.LabelField("Adaptive near clip", controller.AdaptiveNearClipActive ? "Follow only" : "Inactive");
        EditorGUILayout.HelpBox("IgnoreObstacles keeps camera distance, including L3. Adaptive near clip is follow-only and cuts ALL foreground geometry; walls can still hide the player. VisibilityMask is follow-only; incompatible groups retain collisions.", MessageType.Info);
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

[CustomEditor(typeof(LitMaisonVisibilityZone))]
public sealed class LitMaisonVisibilityZoneEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var zone = (LitMaisonVisibilityZone)target;
        EditorGUILayout.LabelField("Prepared architecture renderers", zone.PreparedRendererCount.ToString());
        EditorGUILayout.LabelField("Incompatible renderers", zone.UnsupportedRendererCount.ToString());
        EditorGUILayout.HelpBox("Maison Near 1–4 only. Local material copies enable HDRP depth/normal cutout; shared assets and colliders are unchanged. Floors, walkable stairs, animated objects and other scenes are excluded. Follow only with IgnoreObstacles; L3 and Timelines restore the effect.", MessageType.Info);
    }
}
