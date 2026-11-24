using System.Collections.Generic;
using UnityEngine;

public partial class AudioManager
{
    private sealed class AmbienceLayer
    {
        public AudioClipSO clip;
        public AudioSource source;
        public float weight, target, duration;
    }
    private readonly List<AmbienceLayer> ambienceLayers = new();
    private readonly HashSet<AudioClipSO> desiredAmbiences = new();

    private void SetAmbienceLayers(IReadOnlyList<AudioClipSO> clips, float seconds)
    {
        desiredAmbiences.Clear();
        if (clips != null && !HasAmbienceSuppressingMusicOverride())
            foreach (var clip in clips)
                if (clip != null && clip.audioClip != null) desiredAmbiences.Add(clip);
        foreach (var layer in ambienceLayers)
        {
            layer.target = desiredAmbiences.Contains(layer.clip) ? 1 : 0;
            layer.duration = Mathf.Max(.01f, seconds);
        }
        foreach (var clip in desiredAmbiences)
        {
            if (ambienceLayers.Exists(layer => layer.clip == clip)) continue;
            var source = CreateSource("Ambience_" + clip.name);
            ConfigureAmbienceSource(source);
            source.clip = clip.audioClip;
            source.loop = clip.loop;
            source.volume = 0;
            ApplyClipPitch(source, clip);
            source.Play();
            ambienceLayers.Add(new AmbienceLayer { clip = clip, source = source, target = 1, duration = Mathf.Max(.01f, seconds) });
        }
    }

    private void TickAmbienceLayers()
    {
        for (int i = ambienceLayers.Count - 1; i >= 0; i--)
        {
            var layer = ambienceLayers[i];
            if (layer.source == null) { ambienceLayers.RemoveAt(i); continue; }
            layer.weight = Mathf.MoveTowards(layer.weight, layer.target, Time.unscaledDeltaTime / layer.duration);
            layer.source.volume = GetAmbienceSourceVolume(layer.clip) * layer.weight;
            ApplyClipPitch(layer.source, layer.clip);
            if (layer.target == 0 && layer.weight == 0)
            {
                layer.source.Stop();
                Destroy(layer.source.gameObject);
                ambienceLayers.RemoveAt(i);
            }
        }
    }
}
