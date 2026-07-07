using System;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Role: drives a single HDRP full-screen screen wave custom pass.
// Usage: place on BattleManager, assign a scene CustomPassVolume and a ScreenWave material.
// Runtime API: call PlayScreenWave(), PlayScreenWave(Vector2), or TryPlayScreenWave(Vector3).
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class ScreenWaveController : MonoBehaviour
{
    [Serializable]
    public struct ScreenWaveSettings
    {
        public Vector2 origin;
        public Vector2 direction;
        public bool reverse;
        [Min(0.1f)] public float frequency;
        [Min(0.01f)] public float propagationSpeed;
        [Range(0f, 0.25f)] public float amplitude;
        [Min(0.05f)] public float duration;
        [Min(0.01f)] public float falloff;
        [Min(0f)] public float fadeOutDuration;

        public static ScreenWaveSettings Default => new ScreenWaveSettings
        {
            origin = new Vector2(0.5f, 0.5f),
            direction = Vector2.zero,
            reverse = false,
            frequency = 14f,
            propagationSpeed = 1.45f,
            amplitude = 0.15f,
            duration = 0.9f,
            falloff = 6f,
            fadeOutDuration = 0.75f
        };

        public ScreenWaveSettings Sanitized()
        {
            origin = new Vector2(Mathf.Clamp01(origin.x), Mathf.Clamp01(origin.y));
            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            frequency = Mathf.Max(0.1f, frequency);
            propagationSpeed = Mathf.Max(0.01f, propagationSpeed);
            amplitude = Mathf.Clamp(amplitude, 0f, 0.25f);
            duration = Mathf.Max(0.05f, duration);
            falloff = Mathf.Max(0.01f, falloff);
            fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
            return this;
        }
    }

    private static readonly int OriginId = Shader.PropertyToID("_Origin");
    private static readonly int DirectionId = Shader.PropertyToID("_Direction");
    private static readonly int ElapsedId = Shader.PropertyToID("_Elapsed");
    private static readonly int DurationId = Shader.PropertyToID("_Duration");
    private static readonly int ReverseId = Shader.PropertyToID("_Reverse");
    private static readonly int FrequencyId = Shader.PropertyToID("_Frequency");
    private static readonly int PropagationSpeedId = Shader.PropertyToID("_PropagationSpeed");
    private static readonly int AmplitudeId = Shader.PropertyToID("_Amplitude");
    private static readonly int FalloffId = Shader.PropertyToID("_Falloff");
    private static readonly int WaveFadeId = Shader.PropertyToID("_WaveFade");

    public static ScreenWaveController Instance { get; private set; }

    [Header("HDRP Custom Pass")]
    [SerializeField] private CustomPassVolume customPassVolume;
    [SerializeField] private Material screenWaveMaterial;
    [SerializeField] private string customPassName = "Screen Wave";

    [Header("Wave")]
    [SerializeField] private ScreenWaveSettings defaultSettings = ScreenWaveSettings.Default;

    public float TotalDuration
    {
        get
        {
            ScreenWaveSettings settings = defaultSettings.Sanitized();
            return settings.duration * 2f + settings.fadeOutDuration;
        }
    }

    public float MainDuration => defaultSettings.Sanitized().duration;
    public float SinglePhaseDuration
    {
        get
        {
            ScreenWaveSettings settings = defaultSettings.Sanitized();
            return settings.duration + settings.fadeOutDuration;
        }
    }

    public bool IsPlaying => playing || passDisablePending;

    private FullScreenCustomPass cachedPass;
    private ScreenWaveSettings activeSettings;
    private float elapsed;
    private float releaseElapsed;
    private bool playing;
    private bool releasing;
    private bool playReverseAfterForward;
    private bool passDisablePending;
#if UNITY_EDITOR
    private double lastEditorUpdateTime;
#endif

    public static ScreenWaveController EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

#if UNITY_2023_1_OR_NEWER
        Instance = FindAnyObjectByType<ScreenWaveController>();
#else
        Instance = FindObjectOfType<ScreenWaveController>();
#endif
        return Instance;
    }

    public void PlayScreenWave()
    {
        PlayScreenWaveCycle(defaultSettings);
    }

    public void PlayScreenWave(Vector2 origin)
    {
        PlayScreenWaveCycle(origin);
    }

    public void PlayScreenWaveCycle(Vector2 origin)
    {
        ScreenWaveSettings settings = defaultSettings;
        settings.origin = origin;
        PlayScreenWaveCycle(settings);
    }

    public void PlayScreenWaveCycle(ScreenWaveSettings settings)
    {
        settings.reverse = false;
        PlayScreenWavePhase(settings, true);
    }

    public void PlayScreenWave(Vector2 origin, bool reverse)
    {
        ScreenWaveSettings settings = defaultSettings;
        settings.origin = origin;
        settings.reverse = reverse;
        PlayScreenWavePhase(settings, false);
    }

    public void PlayScreenWave(ScreenWaveSettings settings)
    {
        PlayScreenWaveCycle(settings);
    }

    public void PlayScreenWavePhase(ScreenWaveSettings settings)
    {
        PlayScreenWavePhase(settings, false);
    }

    public void PlayScreenWavePhase(Vector2 origin, bool reverse)
    {
        ScreenWaveSettings settings = defaultSettings;
        settings.origin = origin;
        settings.reverse = reverse;
        PlayScreenWavePhase(settings, false);
    }

    public void PlayInverseScreenWave()
    {
        ScreenWaveSettings settings = defaultSettings;
        settings.reverse = true;
        PlayScreenWavePhase(settings, false);
    }

    public void PlayInverseScreenWave(Vector2 origin)
    {
        PlayScreenWavePhase(origin, true);
    }

    private void PlayScreenWavePhase(ScreenWaveSettings settings, bool chainReverse)
    {
        activeSettings = settings.Sanitized();
        elapsed = 0f;
        releaseElapsed = 0f;
        playing = true;
        releasing = false;
        playReverseAfterForward = chainReverse && !activeSettings.reverse;
        passDisablePending = false;

        if (!EnsurePass(true))
        {
            playing = false;
            playReverseAfterForward = false;
            return;
        }

        ApplyWave(activeSettings, 0f, 1f);
        SetPassActive(true);
        BeginEditorPreviewUpdate();
    }

    public bool TryPlayScreenWave(Vector3 worldOrigin, Camera camera = null)
    {
        Vector2 origin;
        if (!TryResolveViewportOrigin(worldOrigin, camera, out origin))
        {
            origin = new Vector2(0.5f, 0.5f);
        }

        PlayScreenWave(origin);
        return playing;
    }

    public void StopScreenWave()
    {
        if (!playing)
        {
            return;
        }

        BeginRelease();
    }

    public void WarmUp()
    {
        if (playing || releasing || passDisablePending)
        {
            return;
        }

        if (!EnsurePass(false))
        {
            return;
        }

        ApplyWave(defaultSettings.Sanitized(), 0f, 0f);
        screenWaveMaterial.SetPass(0);
        SetPassActive(false);
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

    private void OnEnable()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        if (!Application.isPlaying)
        {
            WarmUp();
        }
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            Tick(Time.unscaledDeltaTime);
        }
    }

    private void OnValidate()
    {
        defaultSettings = defaultSettings.Sanitized();
        cachedPass = null;

        if (!Application.isPlaying && !playing)
        {
            WarmUp();
        }
    }

    private void OnDisable()
    {
        StopScreenWaveImmediate();
    }

    private void OnDestroy()
    {
        StopScreenWaveImmediate();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Tick(float deltaTime)
    {
        if (passDisablePending)
        {
            passDisablePending = false;
            SetPassActive(false);
            EndEditorPreviewUpdate();
            return;
        }

        if (!playing)
        {
            return;
        }

        if (releasing)
        {
            TickRelease(deltaTime);
            return;
        }

        elapsed += Mathf.Max(0f, deltaTime);
        float mainDuration = Mathf.Max(0.05f, activeSettings.duration);
        if (elapsed >= mainDuration)
        {
            elapsed = mainDuration;
            if (playReverseAfterForward)
            {
                StartReversePhase();
                return;
            }

            BeginRelease();
            return;
        }

        ApplyWave(activeSettings, elapsed, 1f);
        SetPassActive(true);
    }

    private void BeginRelease()
    {
        if (!playing || releasing)
        {
            return;
        }

        float fadeDuration = Mathf.Max(0f, activeSettings.fadeOutDuration);
        if (fadeDuration <= 0f)
        {
            FinishRelease();
            return;
        }

        releasing = true;
        playReverseAfterForward = false;
        releaseElapsed = 0f;
        ApplyWave(activeSettings, elapsed, 1f);
        SetPassActive(true);
    }

    private void StartReversePhase()
    {
        playReverseAfterForward = false;
        releasing = false;
        releaseElapsed = 0f;
        elapsed = 0f;
        activeSettings.reverse = true;
        ApplyWave(activeSettings, 0f, 1f);
        SetPassActive(true);
    }

    private void TickRelease(float deltaTime)
    {
        float fadeDuration = Mathf.Max(0f, activeSettings.fadeOutDuration);
        releaseElapsed += Mathf.Max(0f, deltaTime);

        if (releaseElapsed >= fadeDuration)
        {
            FinishRelease();
            return;
        }

        float fade = 1f - Ease(releaseElapsed / fadeDuration);
        ApplyWave(activeSettings, elapsed, fade);
        SetPassActive(true);
    }

    private void FinishRelease()
    {
        ApplyWave(activeSettings, elapsed, 0f);
        SetPassActive(true);
        playing = false;
        releasing = false;
        playReverseAfterForward = false;
        passDisablePending = true;
    }

    private void StopScreenWaveImmediate()
    {
        playing = false;
        releasing = false;
        playReverseAfterForward = false;
        passDisablePending = false;
        EndEditorPreviewUpdate();
        if (EnsurePass(false))
        {
            ApplyWave(defaultSettings.Sanitized(), 0f, 0f);
            SetPassActive(false);
        }
    }

    private bool EnsurePass(bool logWarnings)
    {
        if (cachedPass != null && screenWaveMaterial != null)
        {
            return true;
        }

        if (customPassVolume == null)
        {
            customPassVolume = GetComponentInChildren<CustomPassVolume>(true);
        }

        if (customPassVolume == null)
        {
            if (logWarnings)
            {
                Debug.LogWarning("ScreenWaveController: assign a scene CustomPassVolume. Runtime creation is intentionally unsupported.", this);
            }

            return false;
        }

        cachedPass = FindConfiguredPass(customPassVolume);
        if (cachedPass == null)
        {
            if (logWarnings)
            {
                Debug.LogWarning($"ScreenWaveController: no FullScreenCustomPass named '{customPassName}' was found on the assigned CustomPassVolume.", customPassVolume);
            }

            return false;
        }

        if (screenWaveMaterial == null)
        {
            screenWaveMaterial = cachedPass.fullscreenPassMaterial;
        }

        if (screenWaveMaterial == null)
        {
            if (logWarnings)
            {
                Debug.LogWarning("ScreenWaveController: assign a material using Hidden/Lit/ScreenWave.", this);
            }

            return false;
        }

        if (screenWaveMaterial.shader == null || !string.Equals(screenWaveMaterial.shader.name, "Hidden/Lit/ScreenWave", StringComparison.Ordinal))
        {
            if (logWarnings)
            {
                Debug.LogWarning("ScreenWaveController: the assigned material must use the Hidden/Lit/ScreenWave shader.", screenWaveMaterial);
            }

            return false;
        }

        customPassVolume.enabled = true;
        cachedPass.fullscreenPassMaterial = screenWaveMaterial;
        cachedPass.fetchColorBuffer = true;
        cachedPass.materialPassName = "Custom Pass 0";
        return true;
    }

    private FullScreenCustomPass FindConfiguredPass(CustomPassVolume volume)
    {
        if (volume.customPasses == null)
        {
            return null;
        }

        FullScreenCustomPass materialMatch = null;
        FullScreenCustomPass onlyFullScreenPass = null;
        int fullScreenPassCount = 0;
        for (int i = 0; i < volume.customPasses.Count; i++)
        {
            FullScreenCustomPass pass = volume.customPasses[i] as FullScreenCustomPass;
            if (pass == null)
            {
                continue;
            }

            fullScreenPassCount++;
            onlyFullScreenPass = pass;
            if (string.Equals(pass.name, customPassName, StringComparison.Ordinal))
            {
                return pass;
            }

            if (screenWaveMaterial != null && pass.fullscreenPassMaterial == screenWaveMaterial)
            {
                materialMatch = pass;
            }
        }

        if (materialMatch != null)
        {
            return materialMatch;
        }

        return fullScreenPassCount == 1 ? onlyFullScreenPass : null;
    }

    private void ApplyWave(ScreenWaveSettings settings, float time, float fade)
    {
        if (screenWaveMaterial == null)
        {
            return;
        }

        ScreenWaveSettings sanitized = settings.Sanitized();
        screenWaveMaterial.SetVector(OriginId, new Vector4(sanitized.origin.x, sanitized.origin.y, 0f, 0f));
        screenWaveMaterial.SetVector(DirectionId, new Vector4(sanitized.direction.x, sanitized.direction.y, 0f, 0f));
        screenWaveMaterial.SetFloat(ElapsedId, Mathf.Max(0f, time));
        screenWaveMaterial.SetFloat(DurationId, sanitized.duration);
        screenWaveMaterial.SetFloat(ReverseId, sanitized.reverse ? 1f : 0f);
        screenWaveMaterial.SetFloat(FrequencyId, sanitized.frequency);
        screenWaveMaterial.SetFloat(PropagationSpeedId, sanitized.propagationSpeed);
        screenWaveMaterial.SetFloat(AmplitudeId, sanitized.amplitude);
        screenWaveMaterial.SetFloat(FalloffId, sanitized.falloff);
        screenWaveMaterial.SetFloat(WaveFadeId, Mathf.Clamp01(fade));
    }

    private void SetPassActive(bool active)
    {
        if (cachedPass != null)
        {
            cachedPass.enabled = active;
        }
    }

    private static bool TryResolveViewportOrigin(Vector3 worldOrigin, Camera camera, out Vector2 origin)
    {
        Camera resolvedCamera = camera != null ? camera : Camera.main;
        if (resolvedCamera == null)
        {
            origin = new Vector2(0.5f, 0.5f);
            return false;
        }

        Vector3 viewport = resolvedCamera.WorldToViewportPoint(worldOrigin);
        if (viewport.z <= 0f ||
            float.IsNaN(viewport.x) ||
            float.IsNaN(viewport.y) ||
            float.IsInfinity(viewport.x) ||
            float.IsInfinity(viewport.y))
        {
            origin = new Vector2(0.5f, 0.5f);
            return false;
        }

        origin = new Vector2(Mathf.Clamp01(viewport.x), Mathf.Clamp01(viewport.y));
        return true;
    }

    private static float Ease(float value)
    {
        float t = Mathf.Clamp01(value);
        return t * t * (3f - 2f * t);
    }

    private void BeginEditorPreviewUpdate()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            return;
        }

        lastEditorUpdateTime = EditorApplication.timeSinceStartup;
        EditorApplication.update -= UpdateEditorPreview;
        EditorApplication.update += UpdateEditorPreview;
        RepaintEditorViews();
#endif
    }

    private void EndEditorPreviewUpdate()
    {
#if UNITY_EDITOR
        EditorApplication.update -= UpdateEditorPreview;
        if (!Application.isPlaying)
        {
            RepaintEditorViews();
        }
#endif
    }

#if UNITY_EDITOR
    private void UpdateEditorPreview()
    {
        if (Application.isPlaying)
        {
            EndEditorPreviewUpdate();
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        float deltaTime = Mathf.Max(0f, (float)(now - lastEditorUpdateTime));
        lastEditorUpdateTime = now;
        Tick(deltaTime);
        RepaintEditorViews();
    }

    private static void RepaintEditorViews()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }
#endif
}
