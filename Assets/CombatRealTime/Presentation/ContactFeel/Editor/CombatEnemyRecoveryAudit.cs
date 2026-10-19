using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Measures authored follow-through against the actual lab avatar; never retimes impacts.</summary>
public static class CombatEnemyRecoveryAudit
{
    public const string ProfilePath = "Assets/Characters/3_Enemy/Iluvilirae/Clash/CombatLab_ClashEnemyProfile.asset";
    [Serializable] public sealed class Exit
    {
        public int index;
        public string state, clip;
        public float clipSeconds, recoveryStarts, currentExit, proposedExit, currentMismatch, proposedMismatch;
        public bool candidate;
    }
    [Serializable] public sealed class Report { public Exit[] exits; }
    public static void RunBatch() { Measure(); }
    [MenuItem("Lit/Combat Lab/Configure Measured Enemy Follow Through")]
    public static void Install()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring.");
        var report = Measure();
        var profile = AssetDatabase.LoadAssetAtPath<LitBrainsCombatProfile>(ProfilePath);
        bool changed = false;
        foreach (var exit in report.exits.Where(e => e.candidate))
        {
            profile.attacks[exit.index].recoveryDurationSeconds = exit.proposedExit - exit.recoveryStarts;
            changed = true;
        }
        if (changed) { EditorUtility.SetDirty(profile); AssetDatabase.SaveAssets(); }
        Debug.Log("[EnemyRecovery] Laboratory profile " + (changed ? "updated from measured exits." : "unchanged; no better exit candidate."));
    }
    public static Report Measure()
    {
        var profile = AssetDatabase.LoadAssetAtPath<LitBrainsCombatProfile>(ProfilePath);
        var actor = AssetDatabase.LoadAssetAtPath<GameObject>(ChargedSkillSetup.EnemyPath).GetComponent<Animator>();
        var controller = actor.runtimeAnimatorController as AnimatorController;
        if (profile == null || controller == null) throw new InvalidOperationException("Lab recovery sources missing.");
        var states = controller.layers[0].stateMachine.states.Select(s => s.state).ToArray();
        var locomotion = states.Single(s => s.name == "Locomotion");
        var tree = locomotion.motion as BlendTree;
        var idle = tree?.children.OrderBy(c => c.threshold).First().motion as AnimationClip;
        if (idle == null) throw new InvalidOperationException("Authored neutral Idle clip missing.");
        var report = new Report { exits = new Exit[profile.attacks.Length] };
        using (var sampler = new ActorPoseSampler(actor))
        {
            var reference = sampler.Sample(idle, sampleCount: 30, applyFootIK: locomotion.iKOnFeet)[0];
            for (int i = 0; i < profile.attacks.Length; i++)
            {
                var attack = profile.attacks[i];
                float current = attack.recoverySeconds + attack.recoveryDurationSeconds;
                var state = states.Single(s => s.name == attack.stateName);
                var frames = sampler.Sample(attack.clip, applyFootIK: state.iKOnFeet);
                float automaticExit = state.transitions.Where(t => t.hasExitTime && t.conditions.Length == 0)
                    .Select(t => t.exitTime * attack.clip.length).DefaultIfEmpty(attack.clip.length).Min();
                float latest = Mathf.Min(attack.clip.length - .05f, automaticExit - .05f, attack.recoverySeconds + 1.2f);
                var currentFrame = frames.OrderBy(f => Mathf.Abs(f.time - current)).First();
                var candidates = frames.Where(f => f.time >= Mathf.Max(current, attack.clip.length * .7f) && f.time <= latest).ToArray();
                var best = candidates.Length > 0 ? candidates.OrderBy(f => Mismatch(f, reference)).First() : currentFrame;
                float oldScore = Mismatch(currentFrame, reference), newScore = Mismatch(best, reference);
                report.exits[i] = new Exit { index = i, state = attack.stateName, clip = attack.clip.name,
                    clipSeconds = attack.clip.length, recoveryStarts = attack.recoverySeconds, currentExit = current,
                    proposedExit = best.time, currentMismatch = oldScore, proposedMismatch = newScore,
                    candidate = best.time >= current + .15f && newScore < oldScore * .75f };
            }
        }
        string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library/CombatFeel");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "enemy-recovery-measurements.json"), JsonUtility.ToJson(report, true));
        foreach (var exit in report.exits)
            Debug.Log($"[EnemyRecovery] {exit.state} exit={exit.currentExit:F3}->{exit.proposedExit:F3} mismatch={exit.currentMismatch:F3}->{exit.proposedMismatch:F3} candidate={exit.candidate}");
        return report;
    }

    private static float Mismatch(ActorPoseSampler.Frame frame, ActorPoseSampler.Frame idle) =>
        (Vector3.Distance(frame.leftFoot, idle.leftFoot) + Vector3.Distance(frame.rightFoot, idle.rightFoot)) * .5f +
        Mathf.Abs(frame.body.y - idle.body.y) * .5f;
}
