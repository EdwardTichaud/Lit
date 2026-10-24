using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace Lit.Timeline
{
    /// <summary>Animation-track adapter, not a clock or gameplay controller.
    /// Uses local materials only, and retains invisibility after success until
    /// the quest commits its shared state. Interrupted playback restores all.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class TimelineDissolveTarget : MonoBehaviour, ITimelinePlaybackResultParticipant
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Material fallbackMaterial;
        [SerializeField] private CycleController cycle;
        [SerializeField] private string sequenceId;
        [Range(0, 1)] public float strength;
        private sealed class Snapshot
        {
            public Renderer renderer;
            public bool enabled;
            public Material[] materials;
            public MaterialPropertyBlock[] blocks;
        }
        private readonly List<Snapshot> originals = new();
        private readonly List<Material> localMaterials = new();
        private MaterialPropertyBlock block;
        private bool playback, completed;
        private void OnEnable()
        {
            if (cycle != null) cycle.SequencePresentationFinished += OnSequenceFinished;
        }
        private void OnSequenceFinished(string id, bool success)
        {
            if (id == sequenceId && !success) Restore();
        }

        public void OnTimelinePlaybackStarted(PlayableDirector director)
        {
            Restore();
            playback = true;
            strength = 0;
            Capture();
            Apply(0);
        }
        public void OnTimelinePlaybackFinished(PlayableDirector director) =>
            OnTimelinePlaybackFinished(director, TimelinePlaybackState.Stopped);
        public void OnTimelinePlaybackFinished(PlayableDirector director, TimelinePlaybackState result)
        {
            playback = false;
            if (result == TimelinePlaybackState.Completed) { completed = true; Capture(); Apply(1); }
            else Restore();
        }
        private void OnDidApplyAnimationProperties() => Present();
        private void LateUpdate() => Present();
        private void Present()
        {
            if (cycle != null && !cycle.isActiveAndEnabled) { Restore(); return; }
            if (completed) { Apply(1); return; }
            if (!playback && strength <= 0) { if (originals.Count > 0) Restore(); return; }
            Capture(); Apply(Mathf.Clamp01(strength));
        }
        private void Capture()
        {
            if (originals.Count > 0 || visualRoot == null) return;
            foreach (var renderer in visualRoot.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                var snapshot = new Snapshot { renderer = renderer, enabled = renderer.enabled,
                    materials = materials, blocks = new MaterialPropertyBlock[materials.Length] };
                var replacements = (Material[])materials.Clone();
                for (int i = 0; i < materials.Length; i++)
                {
                    snapshot.blocks[i] = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(snapshot.blocks[i], i);
                    var source = materials[i];
                    if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer) || source == null ||
                        source.HasProperty("_DissolveStrength") || source.HasProperty("_DissolveAmount") || fallbackMaterial == null) continue;
                    var copy = new Material(fallbackMaterial) { name = source.name + " (Timeline local dissolve)", hideFlags = HideFlags.HideAndDontSave };
                    var color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") :
                        source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
                    if (copy.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", color);
                    if (copy.HasProperty("_GhostTint")) copy.SetColor("_GhostTint", color);
                    if (copy.HasProperty("_GhostAlpha")) copy.SetFloat("_GhostAlpha", 1);
                    replacements[i] = copy; localMaterials.Add(copy);
                }
                renderer.sharedMaterials = replacements;
                originals.Add(snapshot);
            }
        }
        private void Apply(float value)
        {
            block ??= new MaterialPropertyBlock();
            foreach (var snapshot in originals)
            {
                if (snapshot.renderer == null) continue;
                snapshot.renderer.enabled = snapshot.enabled && value < 1;
                var materials = snapshot.renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    if (material == null) continue;
                    snapshot.renderer.GetPropertyBlock(block, i);
                    if (material.HasProperty("_DissolveStrength")) block.SetFloat("_DissolveStrength", value);
                    if (material.HasProperty("_DissolveAmount")) block.SetFloat("_DissolveAmount", 1 - value);
                    snapshot.renderer.SetPropertyBlock(block, i);
                }
            }
        }
        private void OnDisable()
        {
            if (cycle != null) cycle.SequencePresentationFinished -= OnSequenceFinished;
            Restore();
        }
        private void OnDestroy() => Restore();
        private void Restore()
        {
            playback = completed = false;
            strength = 0;
            foreach (var snapshot in originals)
            {
                if (snapshot.renderer == null) continue;
                snapshot.renderer.sharedMaterials = snapshot.materials;
                snapshot.renderer.enabled = snapshot.enabled;
                for (int i = 0; i < snapshot.blocks.Length; i++)
                    snapshot.renderer.SetPropertyBlock(snapshot.blocks[i], i);
            }
            originals.Clear();
            foreach (var material in localMaterials)
                if (material != null) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
            localMaterials.Clear();
        }
    }
}
