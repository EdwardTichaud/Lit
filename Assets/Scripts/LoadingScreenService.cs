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

    private struct SuspendedCameraState
    {
        public Camera camera;
        public bool wasEnabled;
    }

    private static LoadingScreenService instance;

    [Header("Fade")]
    [SerializeField, Min(0f), Tooltip("Duree du fondu d'apparition (s).")]
    private float showFadeDuration = 0.5f;
    [SerializeField, Min(0f), Tooltip("Temps minimum avant d'autoriser la disparition du loading screen (s).")]
    private float minimumVisibleDuration = 1f;
    [SerializeField, Min(0f), Tooltip("Temps d'attente apres chargement complet avant disparition (s).")]
    private float hideDelayAfterSceneLoad = 0.25f;
    [SerializeField, Min(0f), Tooltip("Marge de stabilisation apres toutes les operations de scene avant le fondu de sortie.")]
    private float readyHoldDuration = 1.25f;
    [SerializeField, Min(0f), Tooltip("Duree du fondu de disparition (s).")]
    private float hideFadeDuration = 0.5f;
    [SerializeField, Min(0f), Tooltip("Temps minimal entre la disparition de l'orbe et le fondu de sortie de l'ecran noir.")]
    private float loadingOrbHideLeadDuration = 0.5f;

    [Header("Look")]
    [SerializeField, Tooltip("Couleur du fond plein ecran.")]
    private Color backgroundColor = new Color(0f, 0f, 0f, 0.9f);
    [SerializeField, Tooltip("Police par defaut des messages de chargement.")]
    private TMP_FontAsset defaultMessageFont;
    [SerializeField, Min(1f), Tooltip("Taille par defaut des messages de chargement.")]
    private float defaultMessageFontSize = 42f;

    [Header("Loading Orb")]
    [SerializeField, Tooltip("Prefab de particules rendu dans l'interface de chargement.")]
    private GameObject loadingOrbPrefab;
    [SerializeField, Min(128), Tooltip("Resolution du rendu de l'orbe dans l'UI.")]
    private int loadingOrbTextureSize = 512;
    [SerializeField, Min(0.01f)] private float loadingOrbScale = 1f;

    [Header("Layout References")]
    [SerializeField, Tooltip("Texte TMP existant qui affiche les messages de chargement.")]
    private TMP_Text loadingTextReference;
    [SerializeField, Tooltip("Point de reference qui definit la position de l'orbe dans l'interface.")]
    private RectTransform orbPoint;

    private CanvasGroup canvasGroup;
    private TMP_Text messageText;
    private RawImage loadingOrbImage;
    private GameObject loadingOrbInstance;
    private Camera loadingOrbCamera;
    private Camera loadingPresentationCamera;
    private RenderTexture loadingOrbTexture;
    private Coroutine sceneLoadRoutine;
    private Coroutine visibilityRoutine;
    private bool isRuntimeGenerated;
    private float overlayShownAtUnscaledTime = float.NegativeInfinity;
    private float currentFadeTarget;
    private readonly List<ParticleSystem> childParticleSystems = new List<ParticleSystem>();
    private readonly Dictionary<ParticleSystem, ParticleEmissionSnapshot> particleEmissionSnapshots = new Dictionary<ParticleSystem, ParticleEmissionSnapshot>();
    private readonly List<SuspendedCameraState> suspendedCameras = new List<SuspendedCameraState>();

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

    /// <summary>
    /// Affiche l'overlay et attend qu'il soit reellement rendu avant de lancer
    /// une operation couteuse, telle qu'un chargement de scene.
    /// </summary>
    public static IEnumerator ShowAndWaitForPresentation(string message = null)
    {
        LoadingScreenService service = EnsureInstance();
        // Affiche immediatement le texte et une barre a 0 %. Sans cela, les
        // flux GameFlow qui gerent eux-memes LoadSceneAsync ne montraient
        // qu'un fond noir vide avant la fin du chargement.
        service.ShowOverlay(message, progress: 0f);

        // Laisse Unity construire le Canvas, puis presenter au moins une frame.
        yield return null;
        yield return new WaitForEndOfFrame();

        // La duree est courte (0.5 s par defaut) et permet un fondu fluide
        // avant que le chargement asynchrone ne commence.
        while (service.canvasGroup != null && service.canvasGroup.alpha < 0.99f && service.visibilityRoutine != null)
        {
            yield return null;
        }

        // Le monde n'a plus besoin d'etre rendu derriere un overlay opaque.
        // Cela libere du CPU/GPU pour le chargement et laisse l'orbe visible.
        service.SuspendGameplayCameras();
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

    /// <summary>
    /// A appeler uniquement lorsque chargements, dechargements et placement
    /// du joueur sont termines. L'overlay reste opaque pendant la marge de
    /// stabilisation, puis disparait progressivement.
    /// </summary>
    public static void HideWhenSceneIsReady()
    {
        if (instance == null || instance.sceneLoadRoutine != null)
        {
            return;
        }

        instance.BeginHide(instance.readyHoldDuration);
    }

    /// <summary>
    /// Attend que l'overlay ait termine son delai de stabilisation et son
    /// fondu de sortie. Utilise par les contenus non indispensables qui ne
    /// doivent pas concurrencer le chargement initial de la zone.
    /// </summary>
    public static IEnumerator WaitUntilHidden()
    {
        if (instance == null)
        {
            yield break;
        }

        while (instance != null &&
               (instance.visibilityRoutine != null ||
                (instance.canvasGroup != null && instance.canvasGroup.alpha > 0.001f)))
        {
            yield return null;
        }
    }

    private static LoadingScreenService EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

#if UNITY_2023_1_OR_NEWER
        instance = FindAnyObjectByType<LoadingScreenService>();
#else
        instance = FindAnyObjectByType<LoadingScreenService>();
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
        RuntimePersistenceUtility.DontDestroyOnLoadRoot(gameObject);
        BuildUiIfNeeded();
        RefreshChildParticleCache();
        HideOverlayImmediate();
    }

    private void OnTransformChildrenChanged()
    {
        RefreshChildParticleCache();
        ResolveLayoutReferences();
        ApplyParticleEmissionFromAlpha(canvasGroup != null ? canvasGroup.alpha : 0f);
    }

    private void OnDestroy()
    {
        ResumeGameplayCameras();
        if (loadingOrbTexture != null)
        {
            loadingOrbTexture.Release();
            Destroy(loadingOrbTexture);
            loadingOrbTexture = null;
        }
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
        yield return ShowAndWaitForPresentation(message);

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, mode);
        if (operation == null)
        {
            Debug.LogWarning($"LoadingScreenService: echec LoadSceneAsync('{sceneName}').");
            sceneLoadRoutine = null;
            BeginHide(delaySeconds: 0f);
            yield break;
        }

        operation.allowSceneActivation = false;
        while (operation.progress < 0.9f)
        {
            SetOverlayContent(message, Mathf.Clamp01(operation.progress / 0.9f));
            yield return null;
        }

        SetOverlayContent(message, 0.9f);
        yield return null;
        operation.allowSceneActivation = true;

        while (!operation.isDone)
        {
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
            CreateLoadingPresentationCameraIfNeeded();
            ResolveLayoutReferences();
            return;
        }

        Canvas canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Doit rester au-dessus de ScreenFade (32767) afin que le joueur voie
        // le message et la progression, pas uniquement le fondu noir.
        canvas.sortingOrder = 40000;
        CreateLoadingPresentationCameraIfNeeded();

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
        ResolveLayoutReferences();

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

        CreateLoadingOrbUiIfNeeded(root);
    }

    private void ShowOverlay(string message, float? progress)
    {
        SetOverlayContent(message, progress);
        BeginShow();
    }

    private void SetOverlayContent(string message, float? progress)
    {
        BuildUiIfNeeded();
        if (messageText != null)
        {
            messageText.text = message ?? string.Empty;
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
        PlayLoadingOrb();
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

        // La scene est deja prete et l'overlay reste opaque durant le delai.
        // On autorise donc quelques frames de chauffe (GPU, shaders, camera)
        // avant que le joueur puisse revoir le monde.
        if (targetAlpha <= 0f)
        {
            ResumeGameplayCameras();
            HideLoadingOrbBeforeScreenFade();
            delaySeconds = Mathf.Max(delaySeconds, loadingOrbHideLeadDuration);
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
            StopLoadingOrb();
            SetLoadingPresentationCameraActive(false);
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
        ApplyOverlayAlpha(0f);
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        overlayShownAtUnscaledTime = float.NegativeInfinity;
        currentFadeTarget = 0f;
        StopChildParticleSystems();
        StopLoadingOrb();
        SetLoadingOrbVisible(false);
        ResumeGameplayCameras();
        SetLoadingPresentationCameraActive(false);
    }

    private void SuspendGameplayCameras()
    {
        if (suspendedCameras.Count > 0)
        {
            return;
        }

        // L'overlay est deja opaque lorsque cette methode est appelee. La
        // camera de secours peut donc prendre le relais sans casser le fondu
        // d'apparition de l'ecran noir.
        SetLoadingPresentationCameraActive(true);
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || camera == loadingOrbCamera || camera == loadingPresentationCamera)
            {
                continue;
            }

            suspendedCameras.Add(new SuspendedCameraState
            {
                camera = camera,
                wasEnabled = camera.enabled
            });
            camera.enabled = false;
        }
    }

    private void ResumeGameplayCameras()
    {
        for (int i = 0; i < suspendedCameras.Count; i++)
        {
            SuspendedCameraState state = suspendedCameras[i];
            if (state.camera != null)
            {
                state.camera.enabled = state.wasEnabled;
            }
        }

        suspendedCameras.Clear();
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

    /// <summary>
    /// Camera de secours active uniquement pendant l'overlay. La camera de
    /// l'orbe rend dans une RenderTexture et ne compte donc pas comme camera
    /// de Game View : sans cette camera, Unity affiche "No cameras rendering"
    /// lorsque les cameras de jeu sont suspendues entre deux scenes.
    /// </summary>
    private void CreateLoadingPresentationCameraIfNeeded()
    {
        if (loadingPresentationCamera != null)
        {
            return;
        }

        GameObject cameraObject = new GameObject("LoadingPresentationCamera");
        cameraObject.transform.SetParent(transform, false);
        loadingPresentationCamera = cameraObject.AddComponent<Camera>();
        loadingPresentationCamera.clearFlags = CameraClearFlags.SolidColor;
        loadingPresentationCamera.backgroundColor = Color.black;
        loadingPresentationCamera.cullingMask = 0;
        loadingPresentationCamera.depth = -1000f;
        loadingPresentationCamera.allowHDR = false;
        loadingPresentationCamera.allowMSAA = false;
        loadingPresentationCamera.enabled = false;
    }

    private void SetLoadingPresentationCameraActive(bool isActive)
    {
        if (loadingPresentationCamera != null)
        {
            loadingPresentationCamera.enabled = isActive;
        }
    }

    private void CreateLoadingOrbUiIfNeeded(RectTransform root)
    {
        if (loadingOrbPrefab == null || loadingOrbImage != null)
        {
            return;
        }

        RectTransform parent = orbPoint != null && orbPoint.parent is RectTransform pointParent
            ? pointParent
            : root;
        loadingOrbImage = CreateUiObject<RawImage>("LoadingOrb", parent);
        loadingOrbImage.color = Color.white;
        loadingOrbImage.raycastTarget = false;

        RectTransform rect = loadingOrbImage.rectTransform;
        if (orbPoint != null)
        {
            rect.anchorMin = orbPoint.anchorMin;
            rect.anchorMax = orbPoint.anchorMax;
            rect.pivot = orbPoint.pivot;
            rect.anchoredPosition = orbPoint.anchoredPosition;
            rect.localRotation = orbPoint.localRotation;
        }
        else
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }
        rect.sizeDelta = new Vector2(290f, 290f);

        int textureSize = Mathf.Max(128, loadingOrbTextureSize);
        loadingOrbTexture = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32)
        {
            name = "LoadingOrbRenderTexture",
            antiAliasing = 1
        };
        loadingOrbTexture.Create();
        loadingOrbImage.texture = loadingOrbTexture;

        GameObject cameraObject = new GameObject("LoadingOrbCamera");
        cameraObject.transform.SetParent(transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, -10000f, -8f);
        cameraObject.transform.localRotation = Quaternion.identity;
        loadingOrbCamera = cameraObject.AddComponent<Camera>();
        loadingOrbCamera.clearFlags = CameraClearFlags.SolidColor;
        loadingOrbCamera.backgroundColor = Color.clear;
        loadingOrbCamera.orthographic = false;
        loadingOrbCamera.fieldOfView = 28f;
        loadingOrbCamera.nearClipPlane = 0.01f;
        loadingOrbCamera.farClipPlane = 100f;
        loadingOrbCamera.allowHDR = true;
        loadingOrbCamera.targetTexture = loadingOrbTexture;

        loadingOrbInstance = Instantiate(loadingOrbPrefab, transform);
        loadingOrbInstance.name = "Loading_Orbe_Preview";
        loadingOrbInstance.transform.localPosition = new Vector3(0f, -10000f, 0f);
        loadingOrbInstance.transform.localRotation = Quaternion.identity;
        loadingOrbInstance.transform.localScale *= loadingOrbScale;
        StopLoadingOrb();
    }

    private void ResolveLayoutReferences()
    {
        if (loadingTextReference == null)
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].name == "Chargement_Text")
                {
                    loadingTextReference = texts[i];
                    break;
                }
            }
        }

        if (orbPoint == null)
        {
            RectTransform[] rects = GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < rects.Length; i++)
            {
                if (rects[i] != null && rects[i].name == "OrbePoint")
                {
                    orbPoint = rects[i];
                    break;
                }
            }
        }

        messageText = loadingTextReference;
        if (messageText != null)
        {
            if (defaultMessageFont != null)
            {
                messageText.font = defaultMessageFont;
            }

            messageText.fontSize = defaultMessageFontSize;
        }
    }

    private void PlayLoadingOrb()
    {
        if (loadingOrbInstance == null)
        {
            return;
        }

        SetLoadingOrbVisible(true);
        foreach (ParticleSystem system in loadingOrbInstance.GetComponentsInChildren<ParticleSystem>(true))
        {
            system.Stop(withChildren: false, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.Play(withChildren: false);
        }
    }

    private void StopLoadingOrb()
    {
        if (loadingOrbInstance == null)
        {
            return;
        }

        foreach (ParticleSystem system in loadingOrbInstance.GetComponentsInChildren<ParticleSystem>(true))
        {
            system.Stop(withChildren: false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void HideLoadingOrbBeforeScreenFade()
    {
        StopLoadingOrb();
        SetLoadingOrbVisible(false);
    }

    private void SetLoadingOrbVisible(bool isVisible)
    {
        if (loadingOrbImage != null)
        {
            loadingOrbImage.enabled = isVisible;
        }
    }

    private static T CreateUiObject<T>(string objectName, Transform parent) where T : Component
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child.AddComponent<T>();
    }
}
