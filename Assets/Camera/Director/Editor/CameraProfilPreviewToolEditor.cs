#if UNITY_EDITOR
using System;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

[CustomEditor(typeof(CameraProfilPreviewTool))]
public sealed class CameraProfilPreviewToolEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Camera Profil Preview", EditorStyles.boldLabel);

        CameraProfilPreviewTool tool = (CameraProfilPreviewTool)target;
        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Cet outil est reserve au mode Edition. Utilisez le runtime pour tester en Play Mode.", MessageType.Info);
            return;
        }

        bool resolved = tool.TryResolvePreview(out _, out _, out _, out string error);
        if (!resolved)
        {
            EditorGUILayout.HelpBox(error, MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox("La lecture utilise Preview_MainCamera, son CinemachineBrain et Lucian_Anchor. Elle ne sauvegarde aucune pose dans AnimationLab.", MessageType.None);
        }

        CameraProfilPreviewPlayback.State state = CameraProfilPreviewPlayback.GetState(tool);
        if (state != null)
        {
            EditorGUILayout.LabelField(state.Label);
            Rect rect = GUILayoutUtility.GetRect(18f, 18f, "TextField");
            EditorGUI.ProgressBar(rect, state.Progress, state.Label);
        }

        using (new EditorGUI.DisabledScope(!resolved))
        {
            if (GUILayout.Button(state == null ? "Play" : "Rejouer"))
            {
                CameraProfilPreviewPlayback.Play(tool);
            }
        }

        using (new EditorGUI.DisabledScope(state == null))
        {
            if (GUILayout.Button("Stop"))
            {
                CameraProfilPreviewPlayback.Stop(tool);
            }
        }
    }
}

[InitializeOnLoad]
internal static class CameraProfilPreviewPlayback
{
    internal sealed class State
    {
        public float Progress;
        public string Label;
    }

    private static CameraProfilPreviewTool activeTool;
    private static Camera activeCamera;
    private static CinemachineBrain activeBrain;
    private static Transform activeAnchor;
    private static CinemachineCamera temporaryCamera;
    private static Vector3 sourcePosition;
    private static Quaternion sourceRotation;
    private static float sourceFieldOfView;
    private static double startedAt;
    private static CameraProfilSO activeProfile;
    private static readonly State ActiveState = new State();

    static CameraProfilPreviewPlayback()
    {
        EditorApplication.update += Tick;
        AssemblyReloadEvents.beforeAssemblyReload += Restore;
        EditorApplication.quitting += Restore;
    }

    internal static State GetState(CameraProfilPreviewTool tool)
    {
        return activeTool == tool ? ActiveState : null;
    }

    internal static void Play(CameraProfilPreviewTool tool)
    {
        Restore();
        if (tool == null)
        {
            Debug.LogWarning("[Camera Profil Preview] CameraProfilPreviewTool manquant.");
            return;
        }

        if (!tool.TryResolvePreview(out Transform anchor, out Camera previewCamera, out CinemachineBrain previewBrain, out string error))
        {
            Debug.LogWarning("[Camera Profil Preview] " + error, tool);
            return;
        }

        activeTool = tool;
        activeProfile = tool.CameraProfil;
        activeAnchor = anchor;
        activeCamera = previewCamera;
        activeBrain = previewBrain;
        sourcePosition = previewCamera.transform.position;
        sourceRotation = previewCamera.transform.rotation;
        sourceFieldOfView = previewCamera.fieldOfView;
        startedAt = EditorApplication.timeSinceStartup;

        GameObject temporary = new GameObject("Camera Profil Preview (Temporary)")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        temporaryCamera = temporary.AddComponent<CinemachineCamera>();
        temporaryCamera.Lens.FieldOfView = sourceFieldOfView;
        PrioritySettings priority = temporaryCamera.Priority;
        priority.Value = int.MaxValue;
        temporaryCamera.Priority = priority;
        temporaryCamera.Prioritize();
        Apply(0f);
    }

    internal static void Stop(CameraProfilPreviewTool tool)
    {
        if (activeTool == tool)
        {
            Restore();
        }
    }

    private static void Tick()
    {
        if (activeTool == null)
        {
            return;
        }

        if (activeTool == null || activeProfile == null || activeAnchor == null || activeCamera == null || activeBrain == null || temporaryCamera == null)
        {
            Restore();
            return;
        }

        float elapsed = (float)(EditorApplication.timeSinceStartup - startedAt);
        float total = activeProfile.startLerp + activeProfile.duration + activeProfile.endLerp;
        Apply(elapsed);
        if (elapsed >= total)
        {
            Restore();
        }

        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
        if (activeTool != null)
        {
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
    }

    private static void Apply(float elapsed)
    {
        float start = Mathf.Max(0f, activeProfile.startLerp);
        float hold = Mathf.Max(0f, activeProfile.duration);
        float end = Mathf.Max(0f, activeProfile.endLerp);
        float fovProgress = Progress(elapsed, activeProfile.fieldOfViewLerp);

        Vector3 profilePosition;
        Quaternion profileRotation;
        if (elapsed < start)
        {
            GetProfilePose(0f, out profilePosition, out profileRotation);
            float t = Progress(elapsed, start);
            temporaryCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(sourcePosition, profilePosition, t),
                Quaternion.Slerp(sourceRotation, profileRotation, t));
            ActiveState.Label = "Entrée";
            ActiveState.Progress = TotalProgress(elapsed, start + hold + end);
        }
        else if (elapsed < start + hold)
        {
            GetProfilePose(elapsed - start, out profilePosition, out profileRotation);
            temporaryCamera.transform.SetPositionAndRotation(profilePosition, profileRotation);
            ActiveState.Label = "Maintien";
            ActiveState.Progress = TotalProgress(elapsed, start + hold + end);
        }
        else
        {
            GetProfilePose(hold, out profilePosition, out profileRotation);
            float t = Progress(elapsed - start - hold, end);
            temporaryCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(profilePosition, sourcePosition, t),
                Quaternion.Slerp(profileRotation, sourceRotation, t));
            ActiveState.Label = "Retour";
            ActiveState.Progress = TotalProgress(elapsed, start + hold + end);
        }

        temporaryCamera.Lens.FieldOfView = Mathf.Lerp(sourceFieldOfView, activeProfile.fieldOfView, fovProgress);
        if (elapsed >= start + hold)
        {
            float fovAtReturnStart = Mathf.Lerp(
                sourceFieldOfView,
                activeProfile.fieldOfView,
                Progress(start + hold, activeProfile.fieldOfViewLerp));
            temporaryCamera.Lens.FieldOfView = Mathf.Lerp(
                fovAtReturnStart,
                sourceFieldOfView,
                Progress(elapsed - start - hold, end));
        }

        activeBrain.ManualUpdate();
    }

    private static void GetProfilePose(float holdElapsed, out Vector3 position, out Quaternion rotation)
    {
        Vector3 localPositionOffset = activeProfile.positionOffset + activeProfile.movementSpeed * holdElapsed;
        position = activeAnchor.TransformPoint(localPositionOffset);
        Vector3 focusPoint = activeAnchor.TransformPoint(activeProfile.offset);
        Vector3 toFocus = focusPoint - position;
        Quaternion lookAtAnchor = toFocus.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(toFocus.normalized, activeAnchor.up)
            : activeAnchor.rotation;
        rotation = lookAtAnchor;
    }

    private static void Restore()
    {
        if (temporaryCamera != null)
        {
            UnityEngine.Object.DestroyImmediate(temporaryCamera.gameObject);
        }

        if (activeCamera != null)
        {
            activeCamera.transform.SetPositionAndRotation(sourcePosition, sourceRotation);
            activeCamera.fieldOfView = sourceFieldOfView;
        }

        activeTool = null;
        activeProfile = null;
        activeAnchor = null;
        activeCamera = null;
        activeBrain = null;
        temporaryCamera = null;
        ActiveState.Progress = 0f;
        ActiveState.Label = null;
        SceneView.RepaintAll();
    }

    [DidReloadScripts]
    private static void RestoreAfterReload()
    {
        Restore();
    }

    private static float Progress(float elapsed, float duration)
    {
        return duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
    }

    private static float TotalProgress(float elapsed, float duration)
    {
        return duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
    }
}
#endif
