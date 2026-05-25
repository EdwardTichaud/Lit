using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Lit/Rendering/Master Shader Dissolve Controller")]
public class MasterShaderDissolveController : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Transform rendererRoot;
    [SerializeField] private bool includeChildren = true;
    [SerializeField] private Renderer[] targetRenderers = Array.Empty<Renderer>();

    [Header("Shader Properties")]
    [SerializeField] private string dissolveAmountPropertyName = "_DissolveAmount";
    [SerializeField] private string dissolveEdgeColorPropertyName = "_DissolveEdgeColor";
    [SerializeField] private string dissolveEdgeWidthPropertyName = "_DissolveEdgeWidth";
    [SerializeField] private string dissolveEdgeIntensityPropertyName = "_DissolveEdgeIntensity";

    [Header("Defaults")]
    [SerializeField, Range(0f, 1f)] private float visibleDissolveAmount = 0f;
    [SerializeField, Range(0f, 1f)] private float hiddenDissolveAmount = 1f;
    [SerializeField] private Color dissolveEdgeColor = new Color(0.35f, 0.95f, 1f, 1f);
    [SerializeField, Range(0f, 0.2f)] private float dissolveEdgeWidth = 0.03f;
    [SerializeField, Min(0f)] private float dissolveEdgeIntensity = 0f;
    [SerializeField] private bool applyOnEnable = true;

    private readonly List<Renderer> rendererBuffer = new List<Renderer>();
    private MaterialPropertyBlock propertyBlock;
    private Coroutine dissolveRoutine;
    private int dissolveAmountPropertyId;
    private int dissolveEdgeColorPropertyId;
    private int dissolveEdgeWidthPropertyId;
    private int dissolveEdgeIntensityPropertyId;

    public float CurrentDissolveAmount { get; private set; }

    private void Awake()
    {
        CachePropertyIds();
        CacheRenderers();
    }

    private void OnEnable()
    {
        CachePropertyIds();
        CacheRenderers();

        if (applyOnEnable)
        {
            SetDissolveAmount(CurrentDissolveAmount);
        }
    }

    private void OnDisable()
    {
        if (dissolveRoutine != null)
        {
            StopCoroutine(dissolveRoutine);
            dissolveRoutine = null;
        }
    }

    private void OnValidate()
    {
        CachePropertyIds();
        if (!Application.isPlaying)
        {
            CacheRenderers();
        }
    }

    [ContextMenu("Reset Dissolve")]
    public void ResetDissolve()
    {
        SetDissolveAmount(visibleDissolveAmount);
    }

    [ContextMenu("Play Dissolve")]
    public void PlayDissolve()
    {
        PlayDissolve(0.5f);
    }

    public void PlayDissolve(float duration)
    {
        PlayDissolveTo(hiddenDissolveAmount, duration);
    }

    public void PlayAppear(float duration)
    {
        PlayDissolveTo(visibleDissolveAmount, duration);
    }

    public void PlayDissolveTo(float targetAmount, float duration)
    {
        if (!isActiveAndEnabled)
        {
            SetDissolveAmount(targetAmount);
            return;
        }

        if (dissolveRoutine != null)
        {
            StopCoroutine(dissolveRoutine);
        }

        dissolveRoutine = StartCoroutine(DissolveRoutine(CurrentDissolveAmount, targetAmount, Mathf.Max(0.01f, duration)));
    }

    public void SetDissolveAmount(float value)
    {
        CurrentDissolveAmount = Mathf.Clamp01(value);
        ApplyProperties();
    }

    private IEnumerator DissolveRoutine(float startAmount, float targetAmount, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetDissolveAmount(Mathf.Lerp(startAmount, targetAmount, t));
            yield return null;
        }

        SetDissolveAmount(targetAmount);
        dissolveRoutine = null;
    }

    private void CacheRenderers()
    {
        if (targetRenderers != null && targetRenderers.Length > 0)
        {
            return;
        }

        Transform root = rendererRoot != null ? rendererRoot : transform;
        rendererBuffer.Clear();
        if (includeChildren)
        {
            root.GetComponentsInChildren(true, rendererBuffer);
        }
        else
        {
            Renderer renderer = root.GetComponent<Renderer>();
            if (renderer != null)
            {
                rendererBuffer.Add(renderer);
            }
        }

        targetRenderers = rendererBuffer.ToArray();
    }

    private void ApplyProperties()
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            return;
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer targetRenderer = targetRenderers[i];
            if (targetRenderer == null)
            {
                continue;
            }

            // Preserve other MPB writers, especially AgeManager's independent _AgeAmount driver.
            targetRenderer.GetPropertyBlock(propertyBlock);
            SetFloatIfPresent(targetRenderer, dissolveAmountPropertyId, CurrentDissolveAmount);
            SetColorIfPresent(targetRenderer, dissolveEdgeColorPropertyId, dissolveEdgeColor);
            SetFloatIfPresent(targetRenderer, dissolveEdgeWidthPropertyId, dissolveEdgeWidth);
            SetFloatIfPresent(targetRenderer, dissolveEdgeIntensityPropertyId, dissolveEdgeIntensity);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void SetFloatIfPresent(Renderer targetRenderer, int propertyId, float value)
    {
        if (propertyId != 0 && RendererHasProperty(targetRenderer, propertyId))
        {
            propertyBlock.SetFloat(propertyId, value);
        }
    }

    private void SetColorIfPresent(Renderer targetRenderer, int propertyId, Color value)
    {
        if (propertyId != 0 && RendererHasProperty(targetRenderer, propertyId))
        {
            propertyBlock.SetColor(propertyId, value);
        }
    }

    private void CachePropertyIds()
    {
        dissolveAmountPropertyId = GetPropertyId(dissolveAmountPropertyName);
        dissolveEdgeColorPropertyId = GetPropertyId(dissolveEdgeColorPropertyName);
        dissolveEdgeWidthPropertyId = GetPropertyId(dissolveEdgeWidthPropertyName);
        dissolveEdgeIntensityPropertyId = GetPropertyId(dissolveEdgeIntensityPropertyName);
    }

    private static int GetPropertyId(string propertyName)
    {
        return string.IsNullOrWhiteSpace(propertyName) ? 0 : Shader.PropertyToID(propertyName);
    }

    private static bool RendererHasProperty(Renderer targetRenderer, int propertyId)
    {
        Material[] materials = targetRenderer != null ? targetRenderer.sharedMaterials : null;
        if (materials == null)
        {
            return false;
        }

        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material != null && material.HasProperty(propertyId))
            {
                return true;
            }
        }

        return false;
    }
}
