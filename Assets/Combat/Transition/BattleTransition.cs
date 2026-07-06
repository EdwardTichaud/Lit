using System;
using System.Collections;
using INab.VFXAssets;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.VFX;

// Role: pilote localement la transition d'entree combat depuis BattleManager.
// Usage: appele par CombatSessionManager avant le placement en arene.
// Responsibilities: jouer la vague HDRP, lancer l'audio combat local et prechauffer BattleSphere/VFX.
// Dependencies: HDRP Custom Pass, CombatTransitionController, CharacterEffect.
// Precautions: l'action couverte doit toujours etre executee, meme si l'effet est interrompu.
[ExecuteAlways]
public sealed class BattleTransition : MonoBehaviour
{
    private const string ShaderName = "Hidden/Lit/BattleScreenWave";
    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int WaveCenterId = Shader.PropertyToID("_WaveCenter");
    private static readonly int RingWidthId = Shader.PropertyToID("_RingWidth");
    private static readonly int FrequencyId = Shader.PropertyToID("_Frequency");
    private static readonly int ChromaticAberrationId = Shader.PropertyToID("_ChromaticAberration");
    private static readonly int VignetteId = Shader.PropertyToID("_Vignette");
    private static readonly int FadeId = Shader.PropertyToID("_Fade");

    public static BattleTransition Instance { get; private set; }

    [Header("Screen Wave")]
    [SerializeField] private Shader screenWaveShader;
    [SerializeField] private Material screenWaveMaterial;
    [SerializeField] private CustomPassVolume screenWaveVolume;
    [SerializeField] private string screenWavePassName = "Battle Screen Wave";
    [SerializeField, Min(0.2f)] private float enterDuration = 0.9f;
    [SerializeField, Range(0.05f, 0.95f)] private float enterPeakNormalizedTime = 0.38f;
    [SerializeField, Range(0f, 0.25f)] private float maxIntensity = 0.12f;
    [SerializeField, Range(0.02f, 0.5f)] private float ringWidth = 0.16f;
    [SerializeField, Range(1f, 48f)] private float frequency = 18f;
    [SerializeField, Range(0f, 0.08f)] private float chromaticAberration = 0.02f;
    [SerializeField, Range(0f, 0.6f)] private float vignette = 0.22f;
    [SerializeField, Range(0f, 0.6f)] private float fade = 0.2f;

    [Header("Editor Preview")]
    [SerializeField] private bool previewInEditMode;
    [SerializeField, Range(0f, 1f)] private float previewProgress = 0.38f;
    [SerializeField, Range(0f, 0.25f)] private float previewIntensity = 0.18f;
    [SerializeField, Range(0f, 0.08f)] private float previewChromaticAberration = 0.02f;
    [SerializeField, Range(0f, 0.6f)] private float previewVignette = 0.22f;
    [SerializeField, Range(0f, 0.6f)] private float previewFade = 0.35f;
    [SerializeField] private Vector2 previewWaveCenter = new Vector2(0.5f, 0.5f);

    [Header("Preload")]
    [SerializeField] private GameObject battleSpherePrefab;
    [SerializeField] private ShaderVariantCollection shaderVariants;
    [SerializeField] private Material[] preloadMaterials;
    [SerializeField] private GameObject[] preloadPrefabs;
    [SerializeField] private bool warmBattleSphereOnStart = true;
    [SerializeField] private Vector3 warmupPosition = new Vector3(0f, -10000f, 0f);

    public float EnterPeakDelaySeconds => Mathf.Max(0.2f, enterDuration) * Mathf.Clamp01(enterPeakNormalizedTime);

    private Material waveMaterial;
    private CustomPassVolume waveVolume;
    private FullScreenCustomPass wavePass;
    private Coroutine transitionRoutine;
    private Action pendingCoveredAction;

    public static BattleTransition EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

#if UNITY_2023_1_OR_NEWER
        Instance = FindAnyObjectByType<BattleTransition>();
#else
        Instance = FindObjectOfType<BattleTransition>();
#endif
        if (Instance != null)
        {
            return Instance;
        }

        return null;
    }

    public void PlayEnterTransition(Vector3 worldCenter, Action coveredAction, bool playVisual = true)
    {
        InvokePendingCoveredAction();
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        pendingCoveredAction = coveredAction;
        transitionRoutine = StartCoroutine(EnterRoutine(worldCenter, playVisual));
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }

        Instance = this;
    }

    private IEnumerator Start()
    {
        if (!Application.isPlaying)
        {
            ApplyEditModePreview();
            yield break;
        }

        yield return PreloadRoutine();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            ApplyEditModePreview();
        }
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            SetWaveActive(false);
        }
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            waveMaterial = null;
            waveVolume = null;
            wavePass = null;
            ApplyEditModePreview();
        }
    }

    private void OnDestroy()
    {
        InvokePendingCoveredAction();
        SetWaveActive(false);
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private IEnumerator EnterRoutine(Vector3 worldCenter, bool playVisual)
    {
        float duration = Mathf.Max(0.2f, enterDuration);
        float coverAt = duration * Mathf.Clamp01(enterPeakNormalizedTime);
        bool covered = false;
        bool visualReady = playVisual && EnsureWavePass();
        Vector2 screenCenter = visualReady ? ResolveWaveCenter(worldCenter) : new Vector2(0.5f, 0.5f);

        if (playVisual)
        {
            CombatTransitionController.EnsureInstance().BeginCombatEntryAudioAndMusic();
        }

        if (visualReady)
        {
            ApplyWaveValues(0f, screenCenter);
            SetWaveActive(true);
        }

        float time = 0f;
        while (time < duration)
        {
            if (visualReady)
            {
                ApplyWaveValues(time / duration, screenCenter);
            }

            if (!covered && time >= coverAt)
            {
                covered = true;
                if (visualReady)
                {
                    ApplyWaveValues(enterPeakNormalizedTime, screenCenter);
                }

                InvokePendingCoveredAction();
            }

            time += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!covered)
        {
            InvokePendingCoveredAction();
        }

        if (visualReady)
        {
            ApplyWaveValues(1f, screenCenter);
            SetWaveActive(false);
        }

        transitionRoutine = null;
    }

    private IEnumerator PreloadRoutine()
    {
        if (!Application.isPlaying)
        {
            yield break;
        }

        if (shaderVariants != null)
        {
            shaderVariants.WarmUp();
        }

        if (EnsureWavePass())
        {
            ApplyWaveValues(0f, new Vector2(0.5f, 0.5f));
            SetWaveActive(false);
        }

        WarmMaterials(preloadMaterials);

        if (warmBattleSphereOnStart)
        {
            yield return WarmPrefabRoutine(ResolveBattleSpherePrefab());
        }

        if (preloadPrefabs == null)
        {
            yield break;
        }

        GameObject defaultBattleSphere = ResolveBattleSpherePrefab();
        for (int i = 0; i < preloadPrefabs.Length; i++)
        {
            GameObject prefab = preloadPrefabs[i];
            if (prefab == null || prefab == defaultBattleSphere)
            {
                continue;
            }

            yield return WarmPrefabRoutine(prefab);
        }
    }

    private bool EnsureWavePass()
    {
        if (wavePass != null && waveMaterial != null)
        {
            return true;
        }

        waveVolume = screenWaveVolume != null ? screenWaveVolume : FindConfiguredWaveVolume();
        if (waveVolume == null)
        {
            Debug.LogWarning("BattleTransition: aucun CustomPassVolume de vague n'est assigne dans la scene.", this);
            return false;
        }

        wavePass = FindConfiguredWavePass(waveVolume);
        if (wavePass == null)
        {
            Debug.LogWarning("BattleTransition: aucun FullScreenCustomPass 'Battle Screen Wave' n'est configure dans le CustomPassVolume assigne.", waveVolume);
            return false;
        }

        waveMaterial = screenWaveMaterial != null ? screenWaveMaterial : wavePass.fullscreenPassMaterial;
        if (waveMaterial == null)
        {
            Debug.LogWarning("BattleTransition: le material de vague n'est pas assigne.", this);
            return false;
        }

        Shader expectedShader = screenWaveShader != null ? screenWaveShader : Shader.Find(ShaderName);
        if (expectedShader != null && waveMaterial.shader != expectedShader)
        {
            Debug.LogWarning("BattleTransition: le material assigne n'utilise pas le shader BattleScreenWave.", waveMaterial);
            return false;
        }

        if (wavePass.fullscreenPassMaterial == null)
        {
            wavePass.fullscreenPassMaterial = waveMaterial;
        }

        wavePass.fetchColorBuffer = true;
        wavePass.materialPassName = "Custom Pass 0";
        wavePass.enabled = false;
        return true;
    }

    private void ApplyEditModePreview()
    {
        if (Application.isPlaying || !EnsureWavePass())
        {
            return;
        }

        if (!previewInEditMode)
        {
            ApplyWaveMaterialValues(0f, 0f, previewWaveCenter, 0f, 0f, 0f);
            SetWaveActive(false);
            return;
        }

        float progress = Mathf.Lerp(-0.2f, 1.35f, Mathf.Clamp01(previewProgress));
        Vector2 center = new Vector2(Mathf.Clamp01(previewWaveCenter.x), Mathf.Clamp01(previewWaveCenter.y));
        ApplyWaveMaterialValues(
            progress,
            previewIntensity,
            center,
            previewChromaticAberration,
            previewVignette,
            previewFade);
        SetWaveActive(true);
    }

    private CustomPassVolume FindConfiguredWaveVolume()
    {
        CustomPassVolume[] volumes = GetComponentsInChildren<CustomPassVolume>(true);
        for (int i = 0; i < volumes.Length; i++)
        {
            if (volumes[i] != null && string.Equals(volumes[i].name, "BattleScreenWavePass", StringComparison.Ordinal))
            {
                return volumes[i];
            }
        }

        return null;
    }

    private FullScreenCustomPass FindConfiguredWavePass(CustomPassVolume volume)
    {
        if (volume == null || volume.customPasses == null)
        {
            return null;
        }

        for (int i = 0; i < volume.customPasses.Count; i++)
        {
            FullScreenCustomPass pass = volume.customPasses[i] as FullScreenCustomPass;
            if (pass == null)
            {
                continue;
            }

            if (string.Equals(pass.name, screenWavePassName, StringComparison.Ordinal) ||
                pass.fullscreenPassMaterial == screenWaveMaterial)
            {
                return pass;
            }
        }

        return null;
    }

    private void SetWaveActive(bool active)
    {
        if (wavePass != null)
        {
            wavePass.enabled = active;
        }
    }

    private void ApplyWaveValues(float normalizedTime, Vector2 center)
    {
        if (waveMaterial == null)
        {
            return;
        }

        float n = Mathf.Clamp01(normalizedTime);
        float peak = Mathf.Clamp(enterPeakNormalizedTime, 0.05f, 0.95f);
        float beforePeak = peak > 0f ? Mathf.Clamp01(n / peak) : 1f;
        float afterPeak = peak < 1f ? Mathf.Clamp01((n - peak) / (1f - peak)) : 1f;
        float envelope = n <= peak
            ? Ease(beforePeak)
            : 1f - Ease(afterPeak);

        ApplyWaveMaterialValues(
            Mathf.Lerp(-0.2f, 1.35f, n),
            maxIntensity * envelope,
            center,
            chromaticAberration * envelope,
            vignette * envelope,
            fade * envelope);
    }

    private void ApplyWaveMaterialValues(
        float progress,
        float intensity,
        Vector2 center,
        float chromaticAberrationAmount,
        float vignetteAmount,
        float fadeAmount)
    {
        if (waveMaterial == null)
        {
            return;
        }

        waveMaterial.SetFloat(ProgressId, progress);
        waveMaterial.SetFloat(IntensityId, intensity);
        waveMaterial.SetVector(WaveCenterId, new Vector4(center.x, center.y, 0f, 0f));
        waveMaterial.SetFloat(RingWidthId, ringWidth);
        waveMaterial.SetFloat(FrequencyId, frequency);
        waveMaterial.SetFloat(ChromaticAberrationId, chromaticAberrationAmount);
        waveMaterial.SetFloat(VignetteId, vignetteAmount);
        waveMaterial.SetFloat(FadeId, fadeAmount);
    }

    private Vector2 ResolveWaveCenter(Vector3 worldCenter)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return new Vector2(0.5f, 0.5f);
        }

        Vector3 viewport = camera.WorldToViewportPoint(worldCenter);
        if (viewport.z <= 0f ||
            float.IsNaN(viewport.x) ||
            float.IsNaN(viewport.y) ||
            float.IsInfinity(viewport.x) ||
            float.IsInfinity(viewport.y))
        {
            return new Vector2(0.5f, 0.5f);
        }

        return new Vector2(Mathf.Clamp01(viewport.x), Mathf.Clamp01(viewport.y));
    }

    private GameObject ResolveBattleSpherePrefab()
    {
        if (battleSpherePrefab != null)
        {
            return battleSpherePrefab;
        }

        CombatSessionManager manager = GetComponent<CombatSessionManager>();
        return manager != null ? manager.combatEntryMidpointPrefab : null;
    }

    private IEnumerator WarmPrefabRoutine(GameObject prefab)
    {
        if (prefab == null)
        {
            yield break;
        }

        GameObject instance = Instantiate(prefab, warmupPosition, Quaternion.identity);
        instance.name = $"{prefab.name}_Warmup";
        DisableColliders(instance);

        WarmRenderers(instance);
        VisualEffect[] visualEffects = instance.GetComponentsInChildren<VisualEffect>(true);
        CharacterEffect[] characterEffects = instance.GetComponentsInChildren<CharacterEffect>(true);

        for (int i = 0; i < visualEffects.Length; i++)
        {
            if (visualEffects[i] == null)
            {
                continue;
            }

            visualEffects[i].Reinit();
            visualEffects[i].Play();
        }

        for (int i = 0; i < characterEffects.Length; i++)
        {
            if (characterEffects[i] != null)
            {
                characterEffects[i].StartEffect();
            }
        }

        yield return null;

        for (int i = 0; i < characterEffects.Length; i++)
        {
            if (characterEffects[i] != null)
            {
                characterEffects[i].StopEffect();
            }
        }

        for (int i = 0; i < visualEffects.Length; i++)
        {
            if (visualEffects[i] != null)
            {
                visualEffects[i].Stop();
            }
        }

        Destroy(instance);
    }

    private static void DisableColliders(GameObject instance)
    {
        Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }

    private static void WarmRenderers(GameObject instance)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            _ = renderers[i].sharedMaterials;
        }
    }

    private static void WarmMaterials(Material[] materials)
    {
        if (materials == null)
        {
            return;
        }

        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null || material.shader == null)
            {
                continue;
            }

            material.SetPass(0);
        }
    }

    private void InvokePendingCoveredAction()
    {
        Action action = pendingCoveredAction;
        pendingCoveredAction = null;
        action?.Invoke();
    }

    private static float Ease(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
