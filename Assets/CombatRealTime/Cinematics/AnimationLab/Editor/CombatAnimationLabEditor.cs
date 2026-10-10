using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

[CustomEditor(typeof(CombatAnimationLab))]
public sealed class CombatAnimationLabEditor : Editor
{
    private bool ownsPreview, playing;
    private float time;
    private double lastTick;

    private void OnEnable() => EditorApplication.update += Tick;
    private void OnDisable() { EditorApplication.update -= Tick; StopPreview(); }

    public override void OnInspectorGUI()
    {
        var lab = (CombatAnimationLab)target;
        EditorGUILayout.HelpBox("Atelier hors Play. Clips : aperçu ci-dessous. Timeline : préparer puis utiliser la lecture et le curseur de la fenêtre Timeline. Les acteurs restent sans IA ni contrôle de combat.", MessageType.Info);
        DrawDefaultInspector();
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Préparer et ouvrir la Timeline"))
            {
                StopPreview();
                if (AnimationMode.InAnimationMode())
                    Debug.LogWarning("Arrêter l'aperçu Animation/Timeline en cours avant de changer les bindings.", lab);
                else
                {
                    BindTimeline(lab);
                    Selection.activeGameObject = lab.director.gameObject;
                    EditorApplication.ExecuteMenuItem("Window/Sequencing/Timeline");
                }
            }
            EditorGUILayout.Space();
            float duration = Mathf.Max(lab.playerClip != null ? lab.playerClip.length : 0, lab.enemyClip != null ? lab.enemyClip.length : 0);
            EditorGUI.BeginChangeCheck();
            time = EditorGUILayout.Slider("Temps du clip (s)", time, 0, Mathf.Max(.01f, duration));
            if (EditorGUI.EndChangeCheck()) Sample(lab);
            if (GUILayout.Button(playing ? "Pause des clips" : "Lire les clips en boucle"))
            {
                playing = !playing;
                lastTick = EditorApplication.timeSinceStartup;
                if (playing) Sample(lab);
            }
            if (GUILayout.Button("Arrêter l'aperçu des clips")) StopPreview();
            if (GUILayout.Button("Recadrer les deux acteurs dans la Scene"))
                SceneView.lastActiveSceneView?.LookAt((lab.player.transform.position + lab.enemy.transform.position) * .5f + Vector3.up, Quaternion.Euler(12, 25, 0), 5);
        }
    }

    public static void BindTimeline(CombatAnimationLab lab)
    {
        if (lab == null || lab.director == null || lab.timeline == null || lab.player == null || lab.enemy == null)
            throw new System.InvalidOperationException("AnimationLab : acteurs, Director et Timeline requis.");
        Undo.RecordObject(lab.director, "Bind AnimationLab Timeline");
        lab.director.Stop();
        lab.director.playableAsset = lab.timeline;
        lab.director.playOnAwake = false;
        // Native Timeline preview playback cannot advance a Manual director.
        lab.director.timeUpdateMode = UnityEngine.Playables.DirectorUpdateMode.UnscaledGameTime;
        lab.director.time = 0;
        foreach (var output in lab.timeline.outputs)
        {
            if (output.sourceObject is AnimationTrack track)
            {
                if (track.name == "Player.Animator") lab.director.SetGenericBinding(track, lab.player);
                else if (track.name == "Enemy.Animator") lab.director.SetGenericBinding(track, lab.enemy);
            }
            else if (output.sourceObject is CinemachineTrack cameraTrack)
            {
                lab.director.SetGenericBinding(cameraTrack, lab.cameraBrain);
                foreach (var clip in cameraTrack.GetClips())
                {
                    if (clip.asset is not CinemachineShot shot) continue;
                    var camera = lab.director.GetReferenceValue(shot.VirtualCamera.exposedName, out bool valid) as CinemachineCamera;
                    if (!valid || camera == null) lab.director.SetReferenceValue(shot.VirtualCamera.exposedName, lab.previewCamera);
                }
            }
            else if (output.sourceObject is SignalTrack) lab.director.SetGenericBinding(output.sourceObject, lab.previewSignals);
        }
        EditorUtility.SetDirty(lab.director);
    }

    private void Tick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { StopPreview(); return; }
        if (!playing || target == null) return;
        var lab = (CombatAnimationLab)target;
        double now = EditorApplication.timeSinceStartup;
        float duration = Mathf.Max(lab.playerClip != null ? lab.playerClip.length : 0, lab.enemyClip != null ? lab.enemyClip.length : 0);
        time = duration > 0 ? (time + (float)(now - lastTick)) % duration : 0;
        lastTick = now;
        Sample(lab);
        Repaint();
    }

    private void Sample(CombatAnimationLab lab)
    {
        if (AnimationMode.InAnimationMode() && !ownsPreview) { playing = false; return; }
        if (!ownsPreview) { AnimationMode.StartAnimationMode(); ownsPreview = true; }
        AnimationMode.BeginSampling();
        try
        {
            if (lab.player != null && lab.playerClip != null) AnimationMode.SampleAnimationClip(lab.player.gameObject, lab.playerClip, Mathf.Min(time, lab.playerClip.length));
            if (lab.enemy != null && lab.enemyClip != null) AnimationMode.SampleAnimationClip(lab.enemy.gameObject, lab.enemyClip, Mathf.Min(time, lab.enemyClip.length));
        }
        finally { AnimationMode.EndSampling(); }
        SceneView.RepaintAll();
    }

    private void StopPreview()
    {
        playing = false;
        if (!ownsPreview) return;
        AnimationMode.StopAnimationMode();
        ownsPreview = false;
        SceneView.RepaintAll();
    }
}
