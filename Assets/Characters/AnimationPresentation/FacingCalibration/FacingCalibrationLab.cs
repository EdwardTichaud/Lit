using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>Visual comparison only. Never attached to gameplay actors or used as a movement authority.</summary>
public sealed class FacingCalibrationLab : MonoBehaviour
{
    [Serializable] public sealed class Actor
    {
        public string label;
        public Animator animator;
        public AnimationClip idle, walk, run;
        [NonSerialized] public PlayableGraph graph;
        [NonSerialized] public AnimationMixerPlayable mixer;
        [NonSerialized] public float[] weights;
        [Tooltip("Measured in the Editor from left/right support phases; preview only.")]
        public float walkToRunPhaseOffset;
    }

    public Actor[] actors = Array.Empty<Actor>();
    [Range(0, 2)] public int cycle;
    [Min(.01f)] public float transitionSeconds = .2f;
    public bool paused;
    public bool repeat = true;
    private int previousCycle = -1;

    private void OnEnable()
    {
        previousCycle = -1;
        foreach (var actor in actors)
        {
            if (actor.animator == null || actor.idle == null) continue;
            actor.animator.applyRootMotion = false;
            actor.animator.fireEvents = false; // A visual preview must never invoke combat callbacks.
            actor.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            actor.graph = PlayableGraph.Create("Facing comparison: " + actor.label);
            actor.graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            actor.mixer = AnimationMixerPlayable.Create(actor.graph, 3);
            var clips = new[] { actor.idle, actor.walk, actor.run };
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null) continue;
                var playable = AnimationClipPlayable.Create(actor.graph, clips[i]);
                playable.SetApplyFootIK(false);
                playable.SetApplyPlayableIK(false);
                actor.graph.Connect(playable, 0, actor.mixer, i);
            }
            actor.weights = new[] { 1f, 0f, 0f };
            actor.mixer.SetInputWeight(0, 1);
            AnimationPlayableOutput.Create(actor.graph, "Pose", actor.animator).SetSourcePlayable(actor.mixer);
            actor.graph.Play();
            actor.graph.Evaluate(0);
        }
    }

    private void Update() => AdvancePreview(Time.deltaTime);

    public void AdvancePreview(float deltaTime)
    {
        if (paused) return;
        if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0)
            throw new ArgumentOutOfRangeException(nameof(deltaTime));
        foreach (var actor in actors)
        {
            if (!actor.graph.IsValid()) continue;
            int selected = Mathf.Clamp(cycle, 0, 2);
            if (selected == 1 && actor.walk == null || selected == 2 && actor.run == null) selected = 0;
            if (cycle != previousCycle)
            {
                int outgoing = 0;
                for (int i = 1; i < 3; i++) if (actor.weights[i] > actor.weights[outgoing]) outgoing = i;
                if (outgoing != selected)
                {
                    double phase = 0;
                    if (outgoing > 0 && selected > 0)
                    {
                        var oldClip = (AnimationClipPlayable)actor.mixer.GetInput(outgoing);
                        phase = oldClip.GetTime() / oldClip.GetAnimationClip().length;
                        phase += selected == 2 ? actor.walkToRunPhaseOffset : -actor.walkToRunPhaseOffset;
                        phase -= Math.Floor(phase);
                    }
                    var incoming = (AnimationClipPlayable)actor.mixer.GetInput(selected);
                    incoming.SetTime(phase * incoming.GetAnimationClip().length);
                }
            }
            for (int i = 0; i < 3; i++)
            {
                if (actor.mixer.GetInput(i).IsNull()) continue;
                var clip = (AnimationClipPlayable)actor.mixer.GetInput(i);
                if (repeat && clip.GetAnimationClip().length > 0 && clip.GetTime() >= clip.GetAnimationClip().length)
                    clip.SetTime(clip.GetTime() % clip.GetAnimationClip().length);
            }
            float sum = 0;
            for (int i = 0; i < 3; i++)
            {
                actor.weights[i] = Mathf.MoveTowards(actor.weights[i], i == selected ? 1 : 0,
                    deltaTime / Mathf.Max(.01f, transitionSeconds));
                sum += actor.weights[i];
            }
            for (int i = 0; i < 3; i++) actor.mixer.SetInputWeight(i, actor.weights[i] / Mathf.Max(sum, .001f));
            actor.graph.Evaluate(deltaTime);
        }
        previousCycle = cycle;
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(12, 12, 640, 170), GUI.skin.box);
        GUILayout.Label("Comparaison des poses — première rangée : sources ; seconde : copies calibrées");
        GUILayout.Label("Lucian / Juggernaut / Werewolf Generic / StoneGolem Generic");
        cycle = GUILayout.Toolbar(cycle, new[] { "Idle", "Marche", "Course" });
        paused = GUILayout.Toggle(paused, "Pause");
        repeat = GUILayout.Toggle(repeat, "Répéter les clips dans cet aperçu");
        GUILayout.Label("Werewolf n'a pas de course native : reste Idle sur cette sélection.");
        GUILayout.Label("Aperçu sur place : ne valide ni la vitesse physique, ni le glissement au sol.");
        GUILayout.EndArea();
    }

    private void OnDisable()
    {
        foreach (var actor in actors) if (actor.graph.IsValid()) actor.graph.Destroy();
    }
}
