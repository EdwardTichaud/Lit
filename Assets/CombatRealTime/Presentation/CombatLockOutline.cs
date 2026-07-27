using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatLockOutline : MonoBehaviour
{
    private const string CombatOutlineLayerName = "CombatOutline";

    private readonly Dictionary<GameObject, int> originalRendererLayers = new Dictionary<GameObject, int>();
    private bool locked;

    private void Awake()
    {
        CacheRendererLayers();
        Apply();
    }

    private void OnEnable()
    {
        CacheRendererLayers();
        Apply();
    }

    private void OnDisable()
    {
        RestoreRendererLayers();
    }

    public void SetLocked(bool value)
    {
        if (locked == value)
        {
            return;
        }

        locked = value;
        Apply();
    }

    private void CacheRendererLayers()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer != null && !originalRendererLayers.ContainsKey(renderer.gameObject))
            {
                originalRendererLayers.Add(renderer.gameObject, renderer.gameObject.layer);
            }
        }
    }

    private void Apply()
    {
        CacheRendererLayers();
        if (!locked)
        {
            RestoreRendererLayers();
            return;
        }

        int outlineLayer = LayerMask.NameToLayer(CombatOutlineLayerName);
        if (outlineLayer < 0)
        {
            Debug.LogError("Layer CombatOutline introuvable.", this);
            return;
        }

        foreach (KeyValuePair<GameObject, int> entry in originalRendererLayers)
        {
            if (entry.Key != null)
            {
                entry.Key.layer = outlineLayer;
            }
        }
    }

    private void RestoreRendererLayers()
    {
        foreach (KeyValuePair<GameObject, int> entry in originalRendererLayers)
        {
            if (entry.Key != null)
            {
                entry.Key.layer = entry.Value;
            }
        }
    }
}
