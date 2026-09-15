using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Presentation state for the authored SavingPanel in the persistent overlay.
/// The panel is never created at runtime; this component only drives its scene objects.
/// </summary>
[DisallowMultipleComponent]
public sealed class SavingPanelController : MonoBehaviour
{
    public static SavingPanelController Instance { get; private set; }

    [Header("Panel")]
    [SerializeField] private CanvasGroup panelCanvasGroup;
    [SerializeField] private CanvasGroup progressCanvasGroup;
    [SerializeField] private CanvasGroup successCanvasGroup;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private TMP_Text successText;
    [SerializeField] private float successDisplayDuration = 1f;

    [Header("Particle Systems")]
    [SerializeField] private List<ParticleSystem> particleSystems = new List<ParticleSystem>();
    [SerializeField, Range(0f, 1f)] private float particleColorAlpha = 1f;

    [Header("Audio")]
    [SerializeField] private AudioClipSO saveSuccessAudioClip;

    private Coroutine hideRoutine;
    private readonly List<ParticleColorState> originalParticleColors = new List<ParticleColorState>();
    private float lastPanelAlpha = -1f;

    private struct ParticleColorState
    {
        public ParticleSystem system;
        public ParticleSystem.MinMaxGradient startColor;
    }

    public bool IsVisible => panelCanvasGroup != null && panelCanvasGroup.alpha > 0.001f;

    public float ParticleColorAlpha
    {
        get => particleColorAlpha;
        set
        {
            particleColorAlpha = Mathf.Clamp01(value);
            CaptureParticleColors();
            ApplyParticleColorAlpha();
        }
    }

    public void SetParticleColorAlpha(float alpha)
    {
        ParticleColorAlpha = alpha;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }

        Instance = this;
        ResolveReferences();
        CaptureParticleColors();
        ApplyParticleColorAlpha();
        HideImmediate();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void LateUpdate()
    {
        if (panelCanvasGroup == null) return;

        float panelAlpha = Mathf.Clamp01(panelCanvasGroup.alpha);
        if (!Mathf.Approximately(panelAlpha, lastPanelAlpha))
        {
            ApplyParticleColorAlpha();
        }
    }

    public void BeginSave()
    {
        ResolveReferences();
        StopHideRoutine();
        SetPanelState(true, true);
        SetChildState(progressCanvasGroup, true);
        SetChildState(successCanvasGroup, false);
        if (progressText != null) progressText.text = "Sauvegarde en cours";
    }

    public void CompleteSave(bool succeeded)
    {
        ResolveReferences();
        StopHideRoutine();

        if (!succeeded)
        {
            HideImmediate();
            return;
        }

        SetPanelState(true, true);
        SetChildState(progressCanvasGroup, false);
        SetChildState(successCanvasGroup, true);
        if (successText != null) successText.text = "Sauvegarde réussie!";
        if (saveSuccessAudioClip != null)
        {
            AudioManager.Instance?.PlayUiOneShotClip(saveSuccessAudioClip);
        }

        hideRoutine = StartCoroutine(HideAfterSuccess());
    }

    public void HideImmediate()
    {
        StopHideRoutine();
        ResolveReferences();
        SetChildState(progressCanvasGroup, false);
        SetChildState(successCanvasGroup, false);
        SetPanelState(false, false);
    }

    private IEnumerator HideAfterSuccess()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, successDisplayDuration));
        hideRoutine = null;
        HideImmediate();
    }

    private void ResolveReferences()
    {
        if (panelCanvasGroup == null) panelCanvasGroup = GetComponent<CanvasGroup>();
        if (progressCanvasGroup == null) progressCanvasGroup = FindCanvasGroup("Sauvegarde en cours");
        if (successCanvasGroup == null) successCanvasGroup = FindCanvasGroup("Sauvegarde réussie!");
        if (progressText == null) progressText = FindText("Sauvegarde en cours");
        if (successText == null) successText = FindText("Sauvegarde réussie!");
    }

    private CanvasGroup FindCanvasGroup(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<CanvasGroup>() : null;
    }

    private TMP_Text FindText(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private void CaptureParticleColors()
    {
        if (particleSystems == null) return;
        for (int i = 0; i < particleSystems.Count; i++)
        {
            ParticleSystem system = particleSystems[i];
            if (system == null || ContainsParticleColor(system)) continue;
            originalParticleColors.Add(new ParticleColorState
            {
                system = system,
                startColor = system.main.startColor
            });
        }
    }

    private bool ContainsParticleColor(ParticleSystem system)
    {
        for (int i = 0; i < originalParticleColors.Count; i++)
        {
            if (originalParticleColors[i].system == system) return true;
        }
        return false;
    }

    private void ApplyParticleColorAlpha()
    {
        float panelAlpha = panelCanvasGroup != null ? Mathf.Clamp01(panelCanvasGroup.alpha) : 1f;
        float effectiveAlpha = particleColorAlpha * panelAlpha;
        lastPanelAlpha = panelAlpha;

        for (int i = originalParticleColors.Count - 1; i >= 0; i--)
        {
            ParticleColorState state = originalParticleColors[i];
            if (state.system == null)
            {
                originalParticleColors.RemoveAt(i);
                continue;
            }
            ParticleSystem.MainModule main = state.system.main;
            main.startColor = ScaleGradient(state.startColor, effectiveAlpha);
        }
    }

    private static ParticleSystem.MinMaxGradient ScaleGradient(
        ParticleSystem.MinMaxGradient source, float alpha)
    {
        switch (source.mode)
        {
            case ParticleSystemGradientMode.Color:
                return new ParticleSystem.MinMaxGradient(ScaleColor(source.color, alpha));
            case ParticleSystemGradientMode.TwoColors:
                return new ParticleSystem.MinMaxGradient(
                    ScaleColor(source.colorMin, alpha), ScaleColor(source.colorMax, alpha));
            case ParticleSystemGradientMode.Gradient:
            case ParticleSystemGradientMode.RandomColor:
                return new ParticleSystem.MinMaxGradient(ScaleGradientAsset(source.gradient, alpha));
            case ParticleSystemGradientMode.TwoGradients:
                return new ParticleSystem.MinMaxGradient(
                    ScaleGradientAsset(source.gradientMin, alpha),
                    ScaleGradientAsset(source.gradientMax, alpha));
            default:
                return source;
        }
    }

    private static Color ScaleColor(Color color, float alpha)
    {
        color.a *= alpha;
        return color;
    }

    private static Gradient ScaleGradientAsset(Gradient source, float alpha)
    {
        if (source == null) return null;
        Gradient scaled = new Gradient();
        GradientColorKey[] colorKeys = source.colorKeys;
        GradientAlphaKey[] alphaKeys = source.alphaKeys;
        for (int i = 0; i < colorKeys.Length; i++) colorKeys[i].color = ScaleColor(colorKeys[i].color, alpha);
        for (int i = 0; i < alphaKeys.Length; i++) alphaKeys[i].alpha *= alpha;
        scaled.SetKeys(colorKeys, alphaKeys);
        return scaled;
    }

    private void SetPanelState(bool visible, bool blocksInput)
    {
        if (panelCanvasGroup == null) return;
        panelCanvasGroup.alpha = visible ? 1f : 0f;
        panelCanvasGroup.interactable = visible && blocksInput;
        panelCanvasGroup.blocksRaycasts = visible && blocksInput;
        if (visible && !panelCanvasGroup.gameObject.activeSelf) panelCanvasGroup.gameObject.SetActive(true);
    }

    private static void SetChildState(CanvasGroup group, bool visible)
    {
        if (group == null) return;
        group.alpha = visible ? 1f : 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private void StopHideRoutine()
    {
        if (hideRoutine == null) return;
        StopCoroutine(hideRoutine);
        hideRoutine = null;
    }
}
