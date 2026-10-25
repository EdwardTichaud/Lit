using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Timeline;

public static class CounterAudioSetup
{
    private const string Folder = "Assets/Characters/3_Enemy/Iluvilirae/";
    private const string AudioFolder = "Assets/Audio/AudioClips/ActionBindings/";

    [MenuItem("Lit/Combat Lab/Configure Counter Audio And Sword")]
    public static void Configure()
    {
        var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(CounterKnockoutSignalSetup.TimelinePath);
        if (timeline == null) throw new InvalidOperationException("CounterSkill_2 Timeline missing.");
        var track = timeline.GetRootTracks().OfType<SignalTrack>().First(t => t.name == "Signals");
        var slash = AudioAsset("SFX_Counter_SwordSlash", "Assets/FreeGameSoundsVol1/SFX/Sword_Swing_Fast_Light/Sword_Swing_Fast_Light_01.wav", .7f);
        var voice = AudioAsset("CounterSkill_1", "Assets/Audio/Voices/[Voice][ToS][Lloyd] That was way too easy.mp3", .8f);
        var slashSignal = Signal("CombatLab_CounterSlash");
        var voiceSignal = Signal("CombatLab_CounterVoice");
        var showSwordSignal = Signal("CombatLab_CounterShowSword");
        var hideSwordSignal = Signal("CombatLab_CounterHideSword");
        var hitSignal = AssetDatabase.LoadAssetAtPath<SignalAsset>(Folder + "CombatLab_CounterHitVfx.signal");
        var hitPrefab = HitPrefab();
        var contactSignal = AssetDatabase.LoadAssetAtPath<SignalAsset>(CounterKnockoutSignalSetup.CameraShakeSignalPath);
        var contacts = track.GetMarkers().OfType<SignalEmitter>().Where(m => m.asset == contactSignal).Select(m => m.time).OrderBy(t => t).ToArray();
        var previousHitTimes = track.GetMarkers().OfType<SignalEmitter>().Where(m => hitSignal != null && m.asset == hitSignal).Select(m => m.time).OrderBy(t => t).ToArray();
        if (contacts.Length != 4) throw new InvalidOperationException("Expected four authored counter contacts.");
        // Migrate only the old contact effects. Preserve authored voice/sword markers and times.
        foreach (var marker in track.GetMarkers().OfType<SignalEmitter>().Where(m => m.asset == slashSignal || hitSignal != null && m.asset == hitSignal).ToArray())
            track.DeleteMarker(marker);
        CounterHitSignalSetup.ConfigureMarkers(timeline, track, previousHitTimes.Length > 0 ? previousHitTimes : contacts, hitPrefab, slash);
        CounterHitSignalSetup.ConfigureNativeHitStates();
        AddMarker(track, voiceSignal, .05);
        AddMarker(track, showSwordSignal, 0);
        AddMarker(track, hideSwordSignal, Math.Max(0, timeline.duration - 1.0 / timeline.editorSettings.frameRate));

        var root = PrefabUtility.LoadPrefabContents(CounterKnockoutSignalSetup.RigPath);
        try
        {
            var receiver = root.GetComponent<SignalReceiver>();
            var events = root.GetComponent<AnimationEvents>();
            if (receiver == null || events == null) throw new InvalidOperationException("Runtime signal receiver missing.");
            Bind(receiver, slashSignal, events.PlaySfx, slash);
            Bind(receiver, voiceSignal, events.PlayVoice, voice);
            Bind(receiver, showSwordSignal, events.ShowTimelineSword);
            Bind(receiver, hideSwordSignal, events.HideTimelineSword);
            if (hitSignal != null && receiver.GetReaction(hitSignal) != null) receiver.Remove(hitSignal);
            EditorUtility.SetDirty(receiver);
            PrefabUtility.SaveAsPrefabAsset(root, CounterKnockoutSignalSetup.RigPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        EditorUtility.SetDirty(track);
        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssetIfDirty(timeline);
        Debug.Log("[CounterAudio] Unified damage/Hit_n/VFX/SFX contacts configured; voice, sword and camera timings preserved.");
    }

    private static GameObject HitPrefab()
    {
        string path = Folder + "CombatLab_CounterHitVfx.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab != null) return prefab;
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/0 - UnityPackages/UnityAssets/FullOpaqueSpell/Materials/M_Particles.mat");
        if (material == null) throw new InvalidOperationException("Existing sparks material missing.");
        var root = new GameObject("CombatLab_CounterHitVfx", typeof(ParticleSystem));
        try
        {
            var particles = root.GetComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.duration = .15f; main.loop = false; main.playOnAwake = true;
            main.useUnscaledTime = true; main.stopAction = ParticleSystemStopAction.Destroy;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.12f, .3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(.02f, .055f);
            main.startColor = new Color(1f, .75f, .3f, 1f);
            main.gravityModifier = .3f; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 32;
            var emission = particles.emission;
            emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 65; shape.radius = .06f;
            var size = particles.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material; renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = .1f; renderer.lengthScale = 2;
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static AudioClipSO AudioAsset(string name, string clipPath, float volume)
    {
        string path = AudioFolder + name + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<AudioClipSO>(path);
        if (asset != null) return asset; // Preserve subsequent author choices.
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
        if (clip == null) throw new InvalidOperationException("Audio clip missing: " + clipPath);
        asset = ScriptableObject.CreateInstance<AudioClipSO>();
        asset.title = name; asset.audioClip = clip; asset.volume = volume;
        asset.loop = false; asset.affectedByTimeScale = false;
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static SignalAsset Signal(string name)
    {
        string path = Folder + name + ".signal";
        var signal = AssetDatabase.LoadAssetAtPath<SignalAsset>(path);
        if (signal != null) return signal;
        signal = ScriptableObject.CreateInstance<SignalAsset>();
        AssetDatabase.CreateAsset(signal, path);
        return signal;
    }

    private static void AddMarker(SignalTrack track, SignalAsset signal, double time)
    {
        if (track.GetMarkers().OfType<SignalEmitter>().Any(m => m.asset == signal)) return;
        var marker = track.CreateMarker<SignalEmitter>(time);
        marker.asset = signal; marker.emitOnce = true; marker.retroactive = false;
        EditorUtility.SetDirty(marker);
    }

    private static void Bind(SignalReceiver receiver, SignalAsset signal, UnityAction<AudioClipSO> action, AudioClipSO clip)
    {
        var reaction = receiver.GetReaction(signal);
        if (reaction == null) { reaction = new UnityEvent(); receiver.AddReaction(signal, reaction); }
        for (int i = reaction.GetPersistentEventCount() - 1; i >= 0; i--)
            if (reaction.GetPersistentMethodName(i) == action.Method.Name) UnityEventTools.RemovePersistentListener(reaction, i);
        UnityEventTools.AddObjectPersistentListener(reaction, action, clip);
        EditorUtility.SetDirty(receiver);
    }

    private static void Bind(SignalReceiver receiver, SignalAsset signal, UnityAction action)
    {
        var reaction = receiver.GetReaction(signal);
        if (reaction == null) { reaction = new UnityEvent(); receiver.AddReaction(signal, reaction); }
        for (int i = reaction.GetPersistentEventCount() - 1; i >= 0; i--)
            if (reaction.GetPersistentMethodName(i) == action.Method.Name) UnityEventTools.RemovePersistentListener(reaction, i);
        UnityEventTools.AddPersistentListener(reaction, action);
        EditorUtility.SetDirty(receiver);
    }

    public static void RunBatch()
    {
        try { Configure(); Configure(); Validate(); EditorApplication.Exit(0); }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    private static void Validate()
    {
        var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(CounterKnockoutSignalSetup.TimelinePath);
        var rig = AssetDatabase.LoadAssetAtPath<GameObject>(CounterKnockoutSignalSetup.RigPath);
        var receiver = rig.GetComponent<SignalReceiver>();
        foreach (var pair in new[] { ("CombatLab_CounterSlash", "PlaySfx", 4), ("CombatLab_CounterVoice", "PlayVoice", 1),
            ("CombatLab_CounterShowSword", "ShowTimelineSword", 1), ("CombatLab_CounterHideSword", "HideTimelineSword", 1) })
        {
            var signal = AssetDatabase.LoadAssetAtPath<SignalAsset>(Folder + pair.Item1 + ".signal");
            int count = timeline.GetRootTracks().OfType<SignalTrack>().SelectMany(t => t.GetMarkers()).OfType<SignalEmitter>().Count(m => m.asset == signal);
            var reaction = receiver.GetReaction(signal);
            if (pair.Item1 == "CombatLab_CounterSlash") continue; // Swing SFX now belongs to each unified contact.
            if (count != pair.Item3 || reaction == null || reaction.GetPersistentEventCount() != 1 || reaction.GetPersistentMethodName(0) != pair.Item2)
                throw new InvalidOperationException("Counter audio validation failed: " + pair.Item1);
        }
        if (CombatCinematicRig.CounterHits(timeline).Count() != 4) throw new InvalidOperationException("Expected four unified CounterHit markers");
        var hitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "CombatLab_CounterHitVfx.prefab");
        var particles = hitPrefab.GetComponent<ParticleSystem>();
        if (particles.main.loop || !particles.main.useUnscaledTime || particles.main.stopAction != ParticleSystemStopAction.Destroy ||
            particles.emission.burstCount != 1 || hitPrefab.GetComponent<ParticleSystemRenderer>().sharedMaterial == null)
            throw new InvalidOperationException("Counter hit VFX must be a finite unscaled burst with a valid material.");
        Debug.Log("[CounterAudio] PASS: idempotent setup, four slash/hit markers, voice and sword markers, runtime persistent bindings and finite hit particles.");
    }
}
