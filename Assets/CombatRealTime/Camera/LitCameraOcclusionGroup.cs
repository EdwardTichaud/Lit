using System.Collections.Generic;
using UnityEngine;

/// <summary>Explicit local presentation only: colliders/materials remain untouched.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class LitCameraOcclusionGroup : MonoBehaviour
{
    [SerializeField] private Renderer[] renderers = new Renderer[0];
    private readonly HashSet<object> owners = new HashSet<object>();
    private bool[] previous;
    private sealed class MaskSlot
    {
        public Renderer renderer;
        public int index;
        public float enabled;
        public readonly Vector4[] values = new Vector4[5];
        public readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        public bool hadIndexedBlock;
    }
    private readonly List<Material> materials = new List<Material>(8);
    private readonly List<MaskSlot> slots = new List<MaskSlot>();
    private object maskOwner;
    private bool warnedIncompatible;

    public bool IsMaskCompatible(out string diagnostic)
    {
        if (renderers == null || renderers.Length == 0)
        { diagnostic = "Aucun renderer configuré."; return false; }
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) { diagnostic = "Référence renderer manquante."; return false; }
            materials.Clear(); renderer.GetSharedMaterials(materials);
            if (materials.Count == 0) { diagnostic = "Renderer sans matériau : " + renderer.name; return false; }
            foreach (Material material in materials)
                if (!LitTacticalMaskProperties.Supports(material))
                { diagnostic = "Matériau sans découpe tactique HDRP : " + renderer.name; return false; }
        }
        diagnostic = "Découpe HDRP compatible pour tous les renderers et slots.";
        return true;
    }

    public bool CanBypassCameraCollision()
    {
        if (!isActiveAndEnabled) return false;
        if (IsMaskCompatible(out string diagnostic)) return true;
        if (!warnedIncompatible)
        {
            warnedIncompatible = true;
            Debug.LogWarning("[TacticalMask] Glissement conservé pour " + name + ": " + diagnostic, this);
        }
        return false;
    }

    public void ApplyMask(object owner, LitTacticalMaskProperties.Data data)
    {
        if (!isActiveAndEnabled || maskOwner != null || !IsMaskCompatible(out _)) return;
        int count = 0;
        foreach (Renderer renderer in renderers)
        {
            materials.Clear(); renderer.GetSharedMaterials(materials);
            for (int index = 0; index < materials.Count; index++)
            {
                if (count == slots.Count) slots.Add(new MaskSlot());
                MaskSlot slot = slots[count++];
                slot.renderer = renderer; slot.index = index;
                slot.block.Clear(); renderer.GetPropertyBlock(slot.block, index);
                slot.hadIndexedBlock = !slot.block.isEmpty;
                // An indexed block takes precedence over a renderer-wide block.
                // Seed it from the latter to preserve existing ice/VFX properties.
                if (!slot.hadIndexedBlock) renderer.GetPropertyBlock(slot.block);
                slot.enabled = slot.block.GetFloat(LitTacticalMaskProperties.Enabled);
                for (int i = 0; i < slot.values.Length; i++) slot.values[i] = slot.block.GetVector(LitTacticalMaskProperties.Vectors[i]);
                data.Write(slot.block); renderer.SetPropertyBlock(slot.block, index);
            }
        }
        // Previously cached slots may refer to removed material slots.
        for (int i = count; i < slots.Count; i++) slots[i].renderer = null;
        maskOwner = owner;
    }

    public void RestoreMask(object owner)
    {
        if (maskOwner == null || !ReferenceEquals(maskOwner, owner)) return;
        foreach (MaskSlot slot in slots)
        {
            if (slot.renderer == null) continue;
            if (!slot.hadIndexedBlock) { slot.renderer.SetPropertyBlock(null, slot.index); continue; }
            slot.renderer.GetPropertyBlock(slot.block, slot.index);
            slot.block.SetFloat(LitTacticalMaskProperties.Enabled, slot.enabled);
            for (int i = 0; i < slot.values.Length; i++) slot.block.SetVector(LitTacticalMaskProperties.Vectors[i], slot.values[i]);
            slot.renderer.SetPropertyBlock(slot.block, slot.index);
        }
        maskOwner = null;
    }
    public void SetHidden(object owner, bool hidden)
    {
        if (hidden)
        {
            if (!owners.Add(owner) || owners.Count != 1) return;
            previous = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                previous[i] = renderers[i].forceRenderingOff;
                renderers[i].forceRenderingOff = true;
            }
        }
        else if (owners.Remove(owner) && owners.Count == 0) Restore();
    }
    public void Configure(Renderer[] explicitRenderers) { RestoreMask(maskOwner); Restore(); owners.Clear(); renderers = explicitRenderers; slots.Clear(); warnedIncompatible = false; }
    private void Restore()
    {
        for (int i = 0; previous != null && i < previous.Length && i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].forceRenderingOff = previous[i];
        previous = null;
    }
    private void OnDisable() { RestoreMask(maskOwner); Restore(); owners.Clear(); }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, .6f, 0);
        foreach (Renderer item in renderers) if (item != null) Gizmos.DrawWireCube(item.bounds.center, item.bounds.size);
    }
}
