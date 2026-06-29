using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterUnit))]
public class BattleUnitHighlight : MonoBehaviour
{
    private sealed class RendererSlot
    {
        public Renderer Renderer;
        public int MaterialIndex;
        public MaterialPropertyBlock OriginalBlock;
    }

    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlinesColorId = Shader.PropertyToID("_OutlinesColor");
    private const float MinHighlightIntensity = 1f;

    private readonly List<RendererSlot> rendererSlots = new();
    private MaterialPropertyBlock sharedBlock;
    private bool highlightActive;
    private Color currentColor;

    private void Awake()
    {
        EnsureSharedBlock();
    }

    private void OnEnable()
    {
        EnsureSharedBlock();
    }

    public void SetHighlight(Color color)
    {
        CacheRendererSlots();
        if (rendererSlots.Count == 0)
            return;

        if (!highlightActive)
            CaptureOriginalBlocks();

        if (highlightActive && color == currentColor)
            return;

        highlightActive = true;
        currentColor = color;
        ApplyHighlight(color);
    }

    public void ClearHighlight()
    {
        if (!highlightActive)
            return;

        RestoreOriginalBlocks();
        highlightActive = false;
    }

    private void CacheRendererSlots()
    {
        if (rendererSlots.Count > 0)
            return;

        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer == null || !ShouldUseRenderer(renderer))
                continue;

            var materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
                continue;

            for (int i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (material == null || !MaterialSupportsOutline(material))
                    continue;

                rendererSlots.Add(new RendererSlot
                {
                    Renderer = renderer,
                    MaterialIndex = i
                });
            }
        }
    }

    private static bool ShouldUseRenderer(Renderer renderer)
    {
        return renderer is MeshRenderer || renderer is SkinnedMeshRenderer || renderer is SpriteRenderer;
    }

    private static bool MaterialSupportsOutline(Material material)
    {
        return material.HasProperty(OutlinesColorId)
               || material.HasProperty(OutlineColorId);
    }

    private void CaptureOriginalBlocks()
    {
        EnsureSharedBlock();
        foreach (var slot in rendererSlots)
        {
            if (slot.Renderer == null)
                continue;

            slot.OriginalBlock ??= new MaterialPropertyBlock();
            slot.Renderer.GetPropertyBlock(slot.OriginalBlock, slot.MaterialIndex);
        }
    }

    private void ApplyHighlight(Color color)
    {
        EnsureSharedBlock();
        foreach (var slot in rendererSlots)
        {
            var renderer = slot.Renderer;
            if (renderer == null)
                continue;

            var material = GetMaterial(renderer, slot.MaterialIndex);
            if (material == null)
                continue;

            sharedBlock.Clear();
            renderer.GetPropertyBlock(sharedBlock, slot.MaterialIndex);
            bool hasOverrides = false;

            if (material.HasProperty(OutlinesColorId))
            {
                sharedBlock.SetColor(OutlinesColorId, ResolveHighlightColor(material, OutlinesColorId, color));
                hasOverrides = true;
            }

            if (material.HasProperty(OutlineColorId))
            {
                sharedBlock.SetColor(OutlineColorId, ResolveHighlightColor(material, OutlineColorId, color));
                hasOverrides = true;
            }

            if (hasOverrides)
                renderer.SetPropertyBlock(sharedBlock, slot.MaterialIndex);
        }
    }

    private void RestoreOriginalBlocks()
    {
        foreach (var slot in rendererSlots)
        {
            if (slot.Renderer == null)
                continue;

            if (slot.OriginalBlock == null)
            {
                slot.Renderer.SetPropertyBlock(null, slot.MaterialIndex);
                continue;
            }

            slot.Renderer.SetPropertyBlock(slot.OriginalBlock, slot.MaterialIndex);
        }
    }

    private static Material GetMaterial(Renderer renderer, int index)
    {
        var materials = renderer.sharedMaterials;
        if (materials == null || index < 0 || index >= materials.Length)
            return null;

        return materials[index];
    }

    private void EnsureSharedBlock()
    {
        if (sharedBlock == null)
            sharedBlock = new MaterialPropertyBlock();
    }

    private static Color ResolveHighlightColor(Material material, int propertyId, Color targetColor)
    {
        Color baseColor = material.GetColor(propertyId);
        float baseIntensity = Mathf.Max(baseColor.r, Mathf.Max(baseColor.g, baseColor.b));
        float intensity = baseIntensity > 0f ? baseIntensity : MinHighlightIntensity;

        float targetMax = Mathf.Max(targetColor.r, Mathf.Max(targetColor.g, targetColor.b));
        if (targetMax <= 0f)
            return new Color(intensity, intensity, intensity, targetColor.a);

        Color normalized = new Color(
            targetColor.r / targetMax,
            targetColor.g / targetMax,
            targetColor.b / targetMax,
            targetColor.a);

        return new Color(
            normalized.r * intensity,
            normalized.g * intensity,
            normalized.b * intensity,
            normalized.a);
    }
}
