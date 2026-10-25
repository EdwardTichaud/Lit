using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Timeline;

[InitializeOnLoad]
public static class CounterHitSignalSetup
{
    const string Request = "Library/CounterHitSignals.request";
    static CounterHitSignalSetup() => EditorApplication.update += Poll;
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string command = File.ReadAllText(Request).Trim(); File.Delete(Request);
        try
        {
            if (command == "configure") Configure();
            else if (command == "validate") CounterHitSignalValidation.Run();
            else if (command == "validate-flow") CounterHitSignalValidation.RunSignalFlow();
            else throw new InvalidOperationException("Unknown CounterHit request");
            File.WriteAllText("Library/CounterHitSignals.result", "PASS: " + command);
        }
        catch (Exception e) { File.WriteAllText("Library/CounterHitSignals.result", e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Lit/Combat Lab/Configure Unified Counter Hits")]
    public static void Configure()
    {
        CounterAudioSetup.Configure();
    }
    public static void ConfigureNativeHitStates()
    {
        foreach (var specification in new[] {
            ("Assets/Characters/3_Enemy/Juggernaut/BrainsProduction/Production.controller", "Hurt"),
            ("Assets/Characters/3_Enemy/Juggernaut/BrainsProduction/Production.controller", "Hurt"),
            ("Assets/Characters/3_Enemy/Juggernaut/BrainsProduction/Production.controller", "Hit"),
            ("Assets/Characters/3_Enemy/Juggernaut_v2/StoneGolem_Humanoid.controller", "gethit1"),
            ("Assets/Werewolf/Werewolf Animator Controller.controller", "gethit1") })
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(specification.Item1);
            if (controller == null) throw new InvalidOperationException("Missing target controller: " + specification.Item1);
            var machine = controller.layers[0].stateMachine;
            foreach (var original in machine.states.Select(s => s.state).Where(s => s.name == specification.Item2 ||
                s.name.StartsWith("gethit") && int.TryParse(s.name.Substring(6), out _)).ToArray())
            {
                string index = original.name.StartsWith("gethit") ? original.name.Substring(6) : "1";
                if (machine.states.Any(s => s.state.name == "Hit_" + index)) continue;
                var state = machine.AddState("Hit_" + index);
                state.motion = original.motion; state.speed = original.speed; state.iKOnFeet = original.iKOnFeet;
                state.writeDefaultValues = original.writeDefaultValues; state.tag = original.tag;
                state.speedParameter = original.speedParameter; state.speedParameterActive = original.speedParameterActive;
                state.cycleOffset = original.cycleOffset; state.cycleOffsetParameter = original.cycleOffsetParameter;
                state.cycleOffsetParameterActive = original.cycleOffsetParameterActive;
                state.mirror = original.mirror; state.mirrorParameter = original.mirrorParameter; state.mirrorParameterActive = original.mirrorParameterActive;
                // No automatic exit: the next cue, KnockedOut or session release owns the destination.
                EditorUtility.SetDirty(controller);
            }
            if (!machine.states.Any(s => s.state.name == "Hit_1" && s.state.motion != null))
                throw new InvalidOperationException("No native reaction available for " + controller.name);
            AssetDatabase.SaveAssetIfDirty(controller);
        }
    }
    public static void ConfigureMarkers(TimelineAsset timeline, SignalTrack track, double[] contactTimes, GameObject vfx, AudioClipSO sfx)
    {
        var existing = track.GetMarkers().OfType<CounterHitSignal>().OrderBy(m => m.time).ToArray();
        if (existing.Length == 0)
        {
            var skill = AssetDatabase.LoadAssetAtPath<CounterSkillSO>("Assets/Characters/3_Enemy/Iluvilirae/CombatLab_CounterSkill.asset");
            int total = skill != null ? skill.Damage : 25;
            for (int i = 0; i < contactTimes.Length; i++)
            {
                var marker = track.CreateMarker<CounterHitSignal>(contactTimes[i]);
                marker.name = "CombatLab_CounterHit_Damage_Animation_VFX_SFX";
                marker.damage = Mathf.RoundToInt(total * (i + 1f) / contactTimes.Length) - Mathf.RoundToInt(total * i / (float)contactTimes.Length);
                marker.hitIndex = 1; marker.vfx = vfx; marker.sfx = sfx;
                EditorUtility.SetDirty(marker);
            }
        }
        // Preserve author-edited times, damage floats, indices, VFX and SFX on rerun.
        foreach (var enemy in timeline.GetRootTracks().OfType<AnimationTrack>().Where(t => t.name == "Enemy.Animator"))
            if (!enemy.muted) { enemy.muted = true; EditorUtility.SetDirty(enemy); }
        EditorUtility.SetDirty(timeline);
    }
}
