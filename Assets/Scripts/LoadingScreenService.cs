using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Overlay global de chargement pour eviter les transitions brutales et centraliser les operations longues.
[DisallowMultipleComponent]
public sealed class LoadingScreenService : MonoBehaviour
{
    private struct ParticleEmissionSnapshot
    {
        public float rateOverTimeMultiplier;
        public float rateOverDistanceMultiplier;
    }

    private static LoadingScreenService instance;

    [Header("Fade")]
    [SerializeField, Min(0f), Tooltip("Duree du fondu d'apparition (s).")]
    private float showFadeDuration = 0.5f;
    [SerializeField, Min(0f), Tooltip("Temps minimum avant d'autoriser la disparition du loading screen (s).")]
    private float minimumVisibleDuration = 3f;
    [SerializeField, Min(0f), Tooltip("Temps d'attente apres chargement complet avant disparition (s).")]
    private float hideDelayAfterSceneLoad = 2f;
    [SerializeField, Min(0f), Tooltip("Duree du fondu de disparition (s).")]
    private float hideFadeDuration = 0.5f;

    [Header("Look")]
    [SerializeField, Tooltip("Couleur du fond plein ecran.")]
    private Color backgroundColor = new Color(0f, 0f, 0f, 0.9f);
    [SerializeField, Tooltip("Couleur du fond de barre de progression.")]
    private Color progressTrackColor = new Color(1f, 1f, 1f, 0.15f);
    [SerializeField, Tooltip("Couleur de la progression.")]
    private Color progressFillColor = new Color(0.95f, 0.85f, 0.35f, 1f);

    private CanvasGroup canvasGroup;
    private TMP_Text messageText;
    private RectTransform progressRoot;
    private Image progressFill;
    private Coroutine sceneLoadRoutine;
    private Coroutine visibilityRoutine;
    private bool isRuntimeGenerated;
    private float overlayShownAtUnscaledTime = float.NegativeInfinity;
    private float currentFadeTarget;
    private readonly List<ParticleSystem> childParticleSystems = new List<ParticleSystem>();
    private readonly Dictionary<ParticleSystem, ParticleEmissionSnapshot> particleEmissionSnapshots = new Dictionary<ParticleSystem, ParticleEmissionSnapshot>();

    public static bool IsLoading => instance != null && instance.sceneLoadRoutine != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureRuntime()
    {
        EnsureInstance();
    }

    public static bool LoadScene(string sceneName, string message = null, LoadSceneMode mode = LoadSceneMode.Single)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("LoadingScreenService: sceneName vide.");
            return false;
        }

        return EnsureInstance().BeginSceneLoad(sceneName, mode, message);
    }

    public static void Show(string message = null)
    {
        EnsureInstance().ShowOverlay(message, progress: null);
    }

    public static void SetProgress(float progress01, string message = null)
    {
        EnsureInstance().ShowOverlay(message, Mathf.Clamp01(progress01));
    }

    public static void Hide()
    {
        if (instance == null || instance.sceneLoadRoutine != null)
        {
            return;
        }

        instance.BeginHide(delaySeconds: 0f);
    }

    private static LoadingScreenService EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

#if UNITY_2023_1_OR_NEWER
        instance = FindFirstObjectByType<LoadingScreenService>();
#else
        instance = FindObjectOfType<LoadingScreenService>();
#endif
        if (instance != null)
        {
            return instance;
        }

        GameObject host = new GameObject("LoadingScreenService");
        host.SetActive(false);
        LoadingScreenService service = host.AddComponent<LoadingScreenService>();
        service.isRuntimeGenerated = true;
        host.SetActive(true);
        instance = service;
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            if (instance.isRuntimeGenerated && !isRuntimeGenerated)
            {
                Destroy(instance.gameObject);
                instance = null;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUiIfNeeded();
        RefreshChildParticleCache();
        ResolveMessageTextIfNeeded();
        HideOverlayImmediate();
    }

    private void OnTransformChildrenChanged()
    {
        RefreshChildParticleCache();
        ResolveMessageTextIfNeeded();
        ApplyParticleEmissionFromAlpha(canvasGroup != null ? canvasGroup.alpha : 0f);
    }

    private bool BeginSceneLoad(string sceneName, LoadSceneMode mode, string message)
    {
        if (sceneLoadRoutine != null)
        {
            return false;
        }

        sceneLoadRoutine = StartCoroutine(LoadSceneRoutine(sceneName, mode, message));
        return true;
    }

    private IEnumerator LoadSceneRoutine(string sceneName, LoadSceneMode mode, string message)
    {
        ShowOverlay(message, 0f);

        // Laisse le Canvas se rendre avant de lancer la charge lourde.
        yield return null;

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, mode);
        if (operation == null)
        {
            Debug.LogWarning($"LoadingScreenService: echec LoadSceneAsync('{sceneName}').");
            sceneLoadRoutine = null;
            BeginHide(delaySeconds: 0f);
            yield break;
        }

        while (!operation.isDone)
        {
            float progress = operation.progress < 0.9f
                ? operation.progress / 0.9f
                : 1f;

            SetOverlayContent(message, progress);
            yield return null;
        }

        SetOverlayContent(message, 1f);

        // Garde l'overlay visible apres l'activation complete de la scene.
        yield return BeginHideRoutine(hideDelayAfterSceneLoad);
        sceneLoadRoutine = null;
    }

    private IEnumerator BeginHideRoutine(float delaySeconds)
    {
        BeginHide(delaySeconds);
        while (visibilityRoutine != null)
        {
            yield return null;
        }
    }

    private void BuildUiIfNeeded()
    {
        if (canvasGroup != null)
        {
            ResolveMessageTextIfNeeded();
            return;
        }

        Canvas canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        canvasGroup = gameObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        RectTransform root = canvas.transform as RectTransform;
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        Image background = CreateUiObject<Image>("Background", root);
        RectTransform backgroundRect = background.rectTransform;
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        background.color = backgroundColor;

        GameObject contentObject = new GameObject("Content", typeof(RectTransform));
        contentObject.transform.SetParent(root, false);
        RectTransform content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0.5f, 0.5f);
        content.anchorMax = new Vector2(0.5f, 0.5f);
        content.pivot = new Vector2(0.5f, 0.5f);
        content.sizeDelta = new Vector2(700f, 180f);
        content.anchoredPosition = Vector2.zero;

        Image track = CreateUiObject<Image>("ProgressTrack", content);
        progressRoot = track.rectTransform;
        progressRoot.anchorMin = new Vector2(0.15f, 0.15f);
        progressRoot.anchorMax = new Vector2(0.85f, 0.15f);
        progressRoot.pivot = new Vector2(0.5f, 0f);
        progressRoot.sizeDelta = new Vector2(0f, 18f);
        track.color = progressTrackColor;

        Image fill = CreateUiObject<Image>("ProgressFill", progressRoot);
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fill.color = progressFillColor;
        progressFill = fill;

        ResolveMessageTextIfNeeded();
    }

    private void ResolveMessageTextIfNeeded()
    {
        if (messageText != null)
        {
            return;
        }

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text current = texts[i];
            if (current != null)
            {
                messageText = current;
                return;
            }
        }
    }

    private void ShowOverlay(string message, float? progress)
    {
        SetOverlayContent(message, progress);
        BeginShow();
    }

    private void SetOverlayContent(string message, float? progress)
    {
        BuildUiIfNeeded();

        if (messageText != null && !string.IsNullOrWhiteSpace(message))
        {
            messageText.text = message;
        }

        if (progressRoot != null)
        {
            bool shouldShowProgress = progress.HasValue;
            progressRoot.gameObject.SetActive(shouldShowProgress);
            if (shouldShowProgress && progressFill != null)
            {
                float clampedProgress = Mathf.Clamp01(progress.Value);
                progressFill.rectTransform.anchorMax = new Vector2(clampedProgress, 1f);
            }
        }
    }

    private void BeginShow()
    {
        BuildUiIfNeeded();
        RefreshChildParticleCache();
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = false;

        if (currentFadeTarget >= 0.999f)
        {
            return;
        }

        overlayShownAtUnscaledTime = Time.unscaledTime;
        PlayChildParticleSystems();
        StartVisibilityFade(1f, showFadeDuration, 0f);
    }

    private void BeginHide(float delaySeconds)
    {
        if (canvasGroup == null)
        {
            return;
        }

        float minimumDelay = GetRemainingMinimumVisibleDelay();
        StartVisibilityFade(0f, hideFadeDuration, Mathf.Max(minimumDelay, Mathf.Max(0f, delaySeconds)));
    }

    private void StartVisibilityFade(float targetAlpha, float duration, float delaySeconds)
    {
        if (visibilityRoutine != null)
        {
            StopCoroutine(visibilityRoutine);
        }

        currentFadeTarget = Mathf.Clamp01(targetAlpha);
        visibilityRoutine = StartCoroutine(FadeCanvasRoutine(targetAlpha, duration, delaySeconds));
    }

    private IEnumerator FadeCanvasRoutine(float targetAlpha, float duration, float delaySeconds)
    {
        if (targetAlpha > canvasGroup.alpha)
        {
            PlayChildParticleSystems();
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = false;
        }

        if (delaySeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(delaySeconds);
        }

        float startAlpha = canvasGroup.alpha;
        if (duration <= 0f)
        {
            ApplyOverlayAlpha(targetAlpha);
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / duration));
                ApplyOverlayAlpha(alpha);
                yield return null;
            }
        }

        ApplyOverlayAlpha(targetAlpha);

        if (targetAlpha <= 0f)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            overlayShownAtUnscaledTime = float.NegativeInfinity;
            StopChildParticleSystems();
        }

        visibilityRoutine = null;
    }

    private void ApplyOverlayAlpha(float alpha)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.Clamp01(alpha);
        }

        ApplyParticleEmissionFromAlpha(alpha);
    }

    private void HideOverlayImmediate()
    {
        BuildUiIfNeeded();
        if (progressRoot != null)
        {
            progressRoot.gameObject.SetActive(false);
        }

        ApplyOverlayAlpha(0f);
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        overlayShownAtUnscaledTime = float.NegativeInfinity;
        currentFadeTarget = 0f;
        StopChildParticleSystems();
    }

    private float GetRemainingMinimumVisibleDelay()
    {
        if (minimumVisibleDuration <= 0f || float.IsNegativeInfinity(overlayShownAtUnscaledTime))
        {
            return 0f;
        }

        float elapsed = Time.unscaledTime - overlayShownAtUnscaledTime;
        return Mathf.Max(0f, minimumVisibleDuration - elapsed);
    }

    private void RefreshChildParticleCache()
    {
        ParticleSystem[] systems = GetComponentsInChildren<ParticleSystem>(true);
        childParticleSystems.Clear();

        HashSet<ParticleSystem> presentSystems = new HashSet<ParticleSystem>();
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem current = systems[i];
            if (current == null)
            {
                continue;
            }

            childParticleSystems.Add(current);
            presentSystems.Add(current);
            if (!particleEmissionSnapshots.ContainsKey(current))
            {
                ParticleSystem.EmissionModule emission = current.emission;
                particleEmissionSnapshots[current] = new ParticleEmissionSnapshot
                {
                    rateOverTimeMultiplier = emission.rateOverTimeMultiplier,
                    rateOverDistanceMultiplier = emission.rateOverDistanceMultiplier,
                };
            }
        }

        List<ParticleSystem> removedSystems = new List<ParticleSystem>();
        foreach (KeyValuePair<ParticleSystem, ParticleEmissionSnapshot> pair in particleEmissionSnapshots)
        {
            if (pair.Key == null || !presentSystems.Contains(pair.Key))
            {
                removedSystems.Add(pair.Key);
            }
        }

        for (int i = 0; i < removedSystems.Count; i++)
        {
            particleEmissionSnapshots.Remove(removedSystems[i]);
        }
    }

    private void ApplyParticleEmissionFromAlpha(float alpha)
    {
        float clampedAlpha = Mathf.Clamp01(alpha);
        for (int i = 0; i < childParticleSystems.Count; i++)
        {
            ParticleSystem current = childParticleSystems[i];
            if (current == null || !particleEmissionSnapshots.TryGetValue(current, out ParticleEmissionSnapshot snapshot))
            {
                continue;
            }

            ParticleSystem.EmissionModule emission = current.emission;
            emission.rateOverTimeMultiplier = snapshot.rateOverTimeMultiplier * clampedAlpha;
            emission.rateOverDistanceMultiplier = snapshot.rateOverDistanceMultiplier * clampedAlpha;
        }
    }

    private void PlayChildParticleSystems()
    {
        for (int i = 0; i < childParticleSystems.Count; i++)
        {
            ParticleSystem current = childParticleSystems[i];
            if (current == null)
            {
                continue;
            }

            current.Play(withChildren: true);
        }
    }

    private void StopChildParticleSystems()
    {
        for (int i = 0; i < childParticleSystems.Count; i++)
        {
            ParticleSystem current = childParticleSystems[i];
            if (current == null)
            {
                continue;
            }

            current.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private static T CreateUiObject<T>(string objectName, Transform parent) where T : Component
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child.AddComponent<T>();
    }
}
