using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class TorchVisionSensitive : MonoBehaviour
{
    public enum VisibilityMode
    {
        AlwaysVisible,
        VisibleOnlyWhenVisionMatches,
        HiddenWhenVisionMatches
    }

    [Header("Vision")]
    [SerializeField] private VisibilityMode visibilityMode = VisibilityMode.VisibleOnlyWhenVisionMatches;
    [SerializeField] private TorchVisionDefinition vision;

    [Header("Torch")]
    [SerializeField] private bool requireTorchEquipped = true;

    [Header("Fade")]
    [SerializeField] private float fadeDuration = 1f;

    [Header("Targets")]
    [SerializeField] private bool includeChildren = true;
    [SerializeField] private bool affectRenderers = true;
    [SerializeField] private bool affectColliders = true;
    [SerializeField] private bool enableColliderWhenVisible = true;
    [SerializeField] private bool enableColliderWhenHidden = false;
    [FormerlySerializedAs("disableColliderWhenVisible")]
    [FormerlySerializedAs("enableColliderWhenVisible")]
    [SerializeField, HideInInspector] private bool legacyColliderWhenVisible = false;
    [SerializeField, HideInInspector] private bool colliderVisibilityMigrated = false;
    [SerializeField] private bool affectBehaviours = false;
    [SerializeField] private Renderer[] renderers;
    [SerializeField] private Collider[] colliders;
    [SerializeField] private Behaviour[] behaviours;

    private struct RendererFadeData
    {
        public Renderer Renderer;
        public int ColorPropertyId;
        public Color BaseColor;
        public bool CanFade;
    }

    private RendererFadeData[] fadeRenderers;
    private MaterialPropertyBlock propertyBlock;
    private Coroutine fadeRoutine;
    private float currentFade = 1f;
    private bool currentVisible = true;

    private void Awake()
    {
        MigrateColliderVisibilityIfNeeded();
        CacheTargets();
    }

    private void OnEnable()
    {
        CacheTargets();
        TorchVisionSystem.GetOrCreate();
        TorchVisionSystem.VisionChanged += OnVisionChanged;
        TorchVisionSystem.TorchStateChanged += OnTorchStateChanged;
        ApplyVision(TorchVisionSystem.CurrentVision);
    }

    private void OnDisable()
    {
        TorchVisionSystem.VisionChanged -= OnVisionChanged;
        TorchVisionSystem.TorchStateChanged -= OnTorchStateChanged;
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            MigrateColliderVisibilityIfNeeded();
            CacheTargets();
        }
    }

    private void CacheTargets()
    {
        if (affectRenderers && (renderers == null || renderers.Length == 0))
        {
            renderers = includeChildren ? GetComponentsInChildren<Renderer>(true) : GetComponents<Renderer>();
        }

        if (affectColliders && (colliders == null || colliders.Length == 0))
        {
            colliders = includeChildren ? GetComponentsInChildren<Collider>(true) : GetComponents<Collider>();
        }

        if (affectBehaviours && (behaviours == null || behaviours.Length == 0))
        {
            behaviours = includeChildren ? GetComponentsInChildren<Behaviour>(true) : GetComponents<Behaviour>();
        }

        CacheFadeRenderers();
    }

    private void MigrateColliderVisibilityIfNeeded()
    {
        if (colliderVisibilityMigrated)
        {
            return;
        }

        enableColliderWhenVisible = !legacyColliderWhenVisible;
        enableColliderWhenHidden = false;
        colliderVisibilityMigrated = true;
    }

    private void CacheFadeRenderers()
    {
        if (!affectRenderers || renderers == null || renderers.Length == 0)
        {
            fadeRenderers = null;
            return;
        }

        if (fadeRenderers == null || fadeRenderers.Length != renderers.Length)
        {
            fadeRenderers = new RendererFadeData[renderers.Length];
        }

        int baseColorId = Shader.PropertyToID("_BaseColor");
        int colorId = Shader.PropertyToID("_Color");

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            RendererFadeData data = new RendererFadeData
            {
                Renderer = renderer,
                CanFade = false,
                ColorPropertyId = 0,
                BaseColor = Color.white
            };

            if (renderer == null)
            {
                fadeRenderers[i] = data;
                continue;
            }

            Material[] materials = renderer.sharedMaterials;
            if (materials != null)
            {
                for (int m = 0; m < materials.Length; m++)
                {
                    Material material = materials[m];
                    if (material == null)
                    {
                        continue;
                    }

                    if (material.HasProperty(baseColorId))
                    {
                        data.ColorPropertyId = baseColorId;
                        data.BaseColor = material.GetColor(baseColorId);
                        data.CanFade = true;
                        break;
                    }

                    if (!data.CanFade && material.HasProperty(colorId))
                    {
                        data.ColorPropertyId = colorId;
                        data.BaseColor = material.GetColor(colorId);
                        data.CanFade = true;
                    }
                }
            }

            fadeRenderers[i] = data;
        }
    }

    private void OnVisionChanged(TorchVisionDefinition previous, TorchVisionDefinition current)
    {
        ApplyVision(current);
    }

    private void OnTorchStateChanged(bool equipped)
    {
        ApplyVision(TorchVisionSystem.CurrentVision);
    }

    private void ApplyVision(TorchVisionDefinition current)
    {
        TorchVisionDefinition effectiveVision = current;
        if (requireTorchEquipped && !TorchVisionSystem.IsTorchEquipped())
        {
            effectiveVision = null;
        }

        bool visible = IsVisibleFor(effectiveVision);
        ApplyRenderers(visible);

        if (affectColliders && colliders != null)
        {
            bool colliderEnabled = visible ? enableColliderWhenVisible : enableColliderWhenHidden;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider != null)
                {
                    collider.enabled = colliderEnabled;
                }
            }
        }

        if (affectBehaviours && behaviours != null)
        {
            for (int i = 0; i < behaviours.Length; i++)
            {
                Behaviour behaviour = behaviours[i];
                if (behaviour == null || behaviour == this)
                {
                    continue;
                }

                behaviour.enabled = visible;
            }
        }
    }

    private void ApplyRenderers(bool visible)
    {
        if (!affectRenderers || renderers == null || renderers.Length == 0)
        {
            return;
        }

        currentVisible = visible;

        if (fadeDuration <= 0f || !HasFadeableRenderers())
        {
            currentFade = visible ? 1f : 0f;
            SetRenderersEnabled(visible);
            ApplyFadeToRenderers(currentFade);
            return;
        }

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        float target = visible ? 1f : 0f;
        fadeRoutine = StartCoroutine(FadeRoutine(target));
    }

    private bool HasFadeableRenderers()
    {
        if (fadeRenderers == null || fadeRenderers.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < fadeRenderers.Length; i++)
        {
            if (fadeRenderers[i].Renderer != null && fadeRenderers[i].CanFade)
            {
                return true;
            }
        }

        return false;
    }

    private System.Collections.IEnumerator FadeRoutine(float target)
    {
        float start = currentFade;
        if (Mathf.Approximately(start, target))
        {
            currentFade = target;
            ApplyFadeToRenderers(currentFade);
            SetRenderersEnabled(target > 0f);
            fadeRoutine = null;
            yield break;
        }

        if (target > 0f)
        {
            SetRenderersEnabled(true);
        }

        float duration = Mathf.Max(0.001f, fadeDuration);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / duration);
            currentFade = Mathf.Lerp(start, target, lerp);
            ApplyFadeToRenderers(currentFade);
            yield return null;
        }

        currentFade = target;
        ApplyFadeToRenderers(currentFade);
        if (target <= 0f)
        {
            SetRenderersEnabled(false);
        }

        fadeRoutine = null;
    }

    private void ApplyFadeToRenderers(float fade)
    {
        if (fadeRenderers == null || fadeRenderers.Length == 0)
        {
            return;
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        for (int i = 0; i < fadeRenderers.Length; i++)
        {
            RendererFadeData data = fadeRenderers[i];
            if (data.Renderer == null)
            {
                continue;
            }

            if (!data.CanFade)
            {
                continue;
            }

            data.Renderer.GetPropertyBlock(propertyBlock);
            Color color = data.BaseColor;
            color.a *= Mathf.Clamp01(fade);
            propertyBlock.SetColor(data.ColorPropertyId, color);
            data.Renderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void SetRenderersEnabled(bool enabled)
    {
        if (renderers == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer != null)
            {
                renderer.enabled = enabled;
            }
        }
    }

    private bool IsVisibleFor(TorchVisionDefinition current)
    {
        switch (visibilityMode)
        {
            case VisibilityMode.AlwaysVisible:
                return true;
            case VisibilityMode.HiddenWhenVisionMatches:
                if (vision == null)
                {
                    return current != null;
                }
                return current != vision;
            default:
                if (vision == null)
                {
                    return current == null;
                }
                return current == vision;
        }
    }
}
