using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Overlay global de chargement pour eviter les transitions brutales et centraliser les operations longues.
[DisallowMultipleComponent]
public sealed class LoadingScreenService : MonoBehaviour
{
    private const string LoadingOrbLayerName = "LoadingOrb";

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
    private static Sprite opaqueBackgroundSprite;

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
    [SerializeField, Min(0f), Tooltip("Delai apres que l'ecran de chargement soit devenu completement opaque, avant l'apparition de l'orbe.")]
    private float loadingOrbShowDelayDuration = 1f;

    [Header("Look")]
    [SerializeField, Tooltip("Couleur du fond plein ecran.")]
    private Color backgroundColor = Color.black;
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
    [SerializeField, Min(1f), Tooltip("Taille de l'orbe affichee dans l'interface, en pixels de reference UI.")]
    private float loadingOrbDisplaySize = 290f;

    [Header("Layout References")]
    [SerializeField, Tooltip("Texte TMP existant qui affiche les messages de chargement.")]
    private TMP_Text loadingTextReference;
    [SerializeField, Tooltip("Point de reference qui definit la position de l'orbe dans l'interface.")]
    private RectTransform orbPoint;
    [SerializeField, Tooltip("Camera deja placee dans Bootstrap qui rend l'orbe dans l'interface.")]
    private Camera loadingOrbCamera;

    private CanvasGroup canvasGroup;
    private TMP_Text messageText;
    private RawImage loadingOrbImage;
    private Canvas loadingOrbCanvas;
    private CanvasGroup loadingOrbCanvasGroup;
    private RectTransform loadingOrbOverlayRoot;
    private GameObject loadingOrbOverlayObject;
    private GameObject loadingOrbRenderRoot;
    private GameObject loadingOrbInstance;
    private Camera loadingPresentationCamera;
    private RenderTexture loadingOrbTexture;
    private Coroutine sceneLoadRoutine;
    private Coroutine visibilityRoutine;
    private Coroutine loadingOrbVisibilityRoutine;
    private Coroutine loadingOrbRenderPrimeRoutine;
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
    /// Autorise le rendu de la scene chargee tout en gardant l'overlay noir
    /// opaque. Utile avant de reveler une scene dont le Volume, les shaders
    /// ou l'exposition ont besoin d'une premiere image de chauffe.
    /// </summary>
    public static void PrepareSceneReveal()
    {
        if (instance == null)
        {
            return;
        }

        instance.ResumeGameplayCameras();
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
        if (loadingOrbOverlayObject != null)
        {
            Destroy(loadingOrbOverlayObject);
            loadingOrbOverlayObject = null;
        }

        if (loadingOrbRenderRoot != null)
        {
            Destroy(loadingOrbRenderRoot);
            loadingOrbRenderRoot = null;
        }

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
            EnsureTopmostCanvasPriority();
            EnsureOpaqueBackground();
            EnsureLoadingOrbCanvasPriority();
            CreateLoadingPresentationCameraIfNeeded();
            ResolveLayoutReferences();
            return;
        }

        Canvas canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }

        EnsureTopmostCanvasPriority();
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
        // Un ecran de chargement doit masquer integralement le monde. Le
        // CanvasGroup pilote le fondu ; le fond lui-meme reste toujours
        // opaque afin qu'aucune scene ne transparaisse derriere l'orbe.
        background.sprite = GetOpaqueBackgroundSprite();
        background.color = GetOpaqueBackgroundColor();

        CreateLoadingOrbUiIfNeeded(root);
    }

    private Color GetOpaqueBackgroundColor()
    {
        Color opaqueBackgroundColor = backgroundColor;
        opaqueBackgroundColor.a = 1f;
        return opaqueBackgroundColor;
    }

    private void EnsureOpaqueBackground()
    {
        Transform backgroundTransform = transform.Find("Background");
        if (backgroundTransform == null)
        {
            return;
        }

        Image background = backgroundTransform.GetComponent<Image>();
        if (background == null)
        {
            return;
        }

        background.enabled = true;
        background.sprite = GetOpaqueBackgroundSprite();
        background.type = Image.Type.Simple;
        background.color = GetOpaqueBackgroundColor();
        background.raycastTarget = true;
    }

    private static Sprite GetOpaqueBackgroundSprite()
    {
        if (opaqueBackgroundSprite == null)
        {
            opaqueBackgroundSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            opaqueBackgroundSprite.name = "LoadingScreenOpaqueBackground";
        }

        return opaqueBackgroundSprite;
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
            // Un appel supplementaire a Show pendant un chargement ne doit
            // pas empecher l'orbe de reapparaitre apres sa marge initiale.
            EnsureLoadingOrbPresentation();
            return;
        }

        overlayShownAtUnscaledTime = Time.unscaledTime;
        SetLoadingMessageVisible(false);
        PlayChildParticleSystems();
        EnsureLoadingOrbPresentation();
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
            // La marge de l'orbe est incluse dans le delai de stabilisation :
            // un ecran minimal de 15 s garde donc une orbe visible 13 s
            // (apparition +1 s, disparition -1 s), sans ajouter une seconde.
            float fadeDelay = Mathf.Max(delaySeconds, loadingOrbHideLeadDuration);
            float orbHideDelay = Mathf.Max(0f, fadeDelay - loadingOrbHideLeadDuration);
            if (orbHideDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(orbHideDelay);
            }

            HideLoadingOrbBeforeScreenFade();
            delaySeconds = fadeDelay - orbHideDelay;
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
        SetLoadingMessageVisible(false);
        StopLoadingOrbPresentationRoutine();
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
            if (current == null ||
                IsLoadingOrbParticleSystem(current) ||
                !particleEmissionSnapshots.TryGetValue(current, out ParticleEmissionSnapshot snapshot))
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
            if (current == null || IsLoadingOrbParticleSystem(current))
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
            if (current == null || IsLoadingOrbParticleSystem(current))
            {
                continue;
            }

            current.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private bool IsLoadingOrbParticleSystem(ParticleSystem system)
    {
        return system != null &&
               loadingOrbInstance != null &&
               system.transform.IsChildOf(loadingOrbInstance.transform);
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

    private void EnsureTopmostCanvasPriority()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            return;
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        // L'orbe utilise le dernier ordre disponible afin de toujours rester
        // visible au-dessus de ce fond noir.
        canvas.sortingOrder = short.MaxValue - 1;
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
        if (loadingOrbImage != null)
        {
            EnsureLoadingOrbCanvasPriority();
            return;
        }

        if (loadingOrbPrefab == null)
        {
            return;
        }

        RectTransform parent = CreateLoadingOrbOverlayRoot(root);
        loadingOrbImage = CreateUiObject<RawImage>("LoadingOrb", parent);
        loadingOrbImage.color = Color.white;
        loadingOrbImage.raycastTarget = false;
        EnsureLoadingOrbCanvasPriority();

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
        rect.sizeDelta = Vector2.one * Mathf.Max(1f, loadingOrbDisplaySize);

        int textureSize = Mathf.Max(128, loadingOrbTextureSize);
        loadingOrbTexture = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32)
        {
            name = "LoadingOrbRenderTexture",
            antiAliasing = 1
        };
        loadingOrbTexture.Create();
        loadingOrbImage.texture = loadingOrbTexture;

        ResolveLoadingOrbCameraIfNeeded();
        CreateLoadingOrbCameraIfNeeded();
        if (loadingOrbCamera != null)
        {
            loadingOrbCamera.transform.localPosition = new Vector3(0f, -10000f, -8f);
            loadingOrbCamera.transform.localRotation = Quaternion.identity;
            loadingOrbCamera.clearFlags = CameraClearFlags.SolidColor;
            // Cette texture est affichee dans l'UI : un fond opaque noir
            // empeche tout decor de transparaitre autour de l'orbe.
            loadingOrbCamera.backgroundColor = Color.black;
            loadingOrbCamera.orthographic = false;
            loadingOrbCamera.fieldOfView = 28f;
            loadingOrbCamera.nearClipPlane = 0.01f;
            loadingOrbCamera.farClipPlane = 100f;
            loadingOrbCamera.allowHDR = true;
            loadingOrbCamera.cullingMask = GetLoadingOrbLayerMask();
            loadingOrbCamera.targetTexture = loadingOrbTexture;
            ConfigureLoadingOrbHdrpCamera(loadingOrbCamera);
            loadingOrbCamera.enabled = true;
        }

        loadingOrbInstance = Instantiate(loadingOrbPrefab, GetLoadingOrbRenderRoot());
        loadingOrbInstance.name = "Loading_Orbe_Preview";
        loadingOrbInstance.transform.localPosition = new Vector3(0f, -10000f, 0f);
        loadingOrbInstance.transform.localRotation = Quaternion.identity;
        loadingOrbInstance.transform.localScale *= loadingOrbScale;
        SetLayerRecursively(loadingOrbInstance, GetLoadingOrbLayer());
        StopLoadingOrb();
    }

    private void CreateLoadingOrbCameraIfNeeded()
    {
        if (loadingOrbCamera != null)
        {
            return;
        }

        GameObject cameraObject = new GameObject("LoadingOrbCamera");
        cameraObject.transform.SetParent(GetLoadingOrbRenderRoot(), false);
        cameraObject.transform.localPosition = new Vector3(0f, -10000f, -8f);
        cameraObject.transform.localRotation = Quaternion.identity;
        loadingOrbCamera = cameraObject.AddComponent<Camera>();
    }

    private Transform GetLoadingOrbRenderRoot()
    {
        if (loadingOrbRenderRoot != null)
        {
            return loadingOrbRenderRoot.transform;
        }

        loadingOrbRenderRoot = new GameObject("LoadingOrbRenderRoot");
        loadingOrbRenderRoot.transform.position = Vector3.zero;
        loadingOrbRenderRoot.transform.rotation = Quaternion.identity;
        loadingOrbRenderRoot.transform.localScale = Vector3.one;
        DontDestroyOnLoad(loadingOrbRenderRoot);
        return loadingOrbRenderRoot.transform;
    }

    private static void ConfigureLoadingOrbHdrpCamera(Camera camera)
    {
        HDAdditionalCameraData hdrpCamera = camera.GetComponent<HDAdditionalCameraData>();
        if (hdrpCamera == null)
        {
            hdrpCamera = camera.gameObject.AddComponent<HDAdditionalCameraData>();
        }

        // La luminosite de l'orbe ne doit pas dependre des Volumes du menu,
        // de Maison ou de toute autre zone chargee en arriere-plan.
        hdrpCamera.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
        hdrpCamera.backgroundColorHDR = Color.black;
        hdrpCamera.volumeLayerMask = 0;
        hdrpCamera.volumeAnchorOverride = camera.transform;
        hdrpCamera.dithering = false;
    }

    private static int GetLoadingOrbLayer()
    {
        int layer = LayerMask.NameToLayer(LoadingOrbLayerName);
        if (layer < 0)
        {
            Debug.LogError($"LoadingScreenService : le layer '{LoadingOrbLayerName}' est manquant.");
        }

        return layer;
    }

    private static int GetLoadingOrbLayerMask()
    {
        int layer = GetLoadingOrbLayer();
        return layer >= 0 ? 1 << layer : 0;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null || layer < 0)
        {
            return;
        }

        root.layer = layer;
        foreach (Transform child in root.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private void ResolveLoadingOrbCameraIfNeeded()
    {
        if (loadingOrbCamera != null)
        {
            return;
        }

        Camera[] cameras = GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].name == "LoadingOrbCamera")
            {
                loadingOrbCamera = cameras[i];
                return;
            }
        }

        // La camera peut etre placee directement a la racine de Bootstrap
        // plutot que sous LoadingScreenService.
        cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].name == "LoadingOrbCamera")
            {
                loadingOrbCamera = cameras[i];
                return;
            }
        }
    }

    private void EnsureLoadingOrbCanvasPriority()
    {
        if (loadingOrbOverlayRoot == null)
        {
            return;
        }

        if (loadingOrbCanvas == null)
        {
            loadingOrbCanvas = loadingOrbOverlayRoot.GetComponent<Canvas>();
        }

        if (loadingOrbCanvas == null)
        {
            return;
        }

        loadingOrbCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        loadingOrbCanvas.overrideSorting = true;
        loadingOrbCanvas.sortingOrder = short.MaxValue;

        // L'orbe est dans un Canvas distinct : elle ne peut donc pas heriter
        // de l'opacite du CanvasGroup qui pilote le fond noir.
        if (loadingOrbCanvasGroup == null)
        {
            loadingOrbCanvasGroup = loadingOrbOverlayRoot.GetComponent<CanvasGroup>();
        }

        if (loadingOrbCanvasGroup == null)
        {
            return;
        }

        loadingOrbCanvasGroup.alpha = 1f;
        loadingOrbCanvasGroup.ignoreParentGroups = true;
        loadingOrbCanvasGroup.interactable = false;
        loadingOrbCanvasGroup.blocksRaycasts = false;
    }

    private RectTransform CreateLoadingOrbOverlayRoot(RectTransform referenceRoot)
    {
        if (loadingOrbOverlayRoot != null)
        {
            return loadingOrbOverlayRoot;
        }

        loadingOrbOverlayObject = new GameObject(
            "LoadingOrbOverlay",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(CanvasGroup));

        Transform parent = transform.parent != null ? transform.parent : transform;
        loadingOrbOverlayObject.transform.SetParent(parent, false);
        loadingOrbOverlayRoot = loadingOrbOverlayObject.GetComponent<RectTransform>();
        loadingOrbOverlayRoot.anchorMin = Vector2.zero;
        loadingOrbOverlayRoot.anchorMax = Vector2.one;
        loadingOrbOverlayRoot.offsetMin = Vector2.zero;
        loadingOrbOverlayRoot.offsetMax = Vector2.zero;

        loadingOrbCanvas = loadingOrbOverlayObject.GetComponent<Canvas>();
        CanvasScaler scaler = loadingOrbOverlayObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        loadingOrbCanvasGroup = loadingOrbOverlayObject.GetComponent<CanvasGroup>();
        EnsureLoadingOrbCanvasPriority();
        return loadingOrbOverlayRoot;
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

    private void EnsureLoadingOrbPresentation()
    {
        if (loadingOrbInstance == null ||
            (loadingOrbImage != null && loadingOrbImage.enabled) ||
            loadingOrbVisibilityRoutine != null)
        {
            return;
        }

        loadingOrbVisibilityRoutine = StartCoroutine(ShowLoadingOrbAfterDelay());
    }

    private IEnumerator ShowLoadingOrbAfterDelay()
    {
        // Le decompte ne commence qu'une fois le fondu d'entree termine.
        // Sinon, l'orbe peut apparaitre alors que l'ecran noir est encore
        // transparent, ce qui donne une transition visuellement incoherente.
        while (canvasGroup != null && canvasGroup.alpha < 0.999f)
        {
            if (currentFadeTarget < 0.999f)
            {
                loadingOrbVisibilityRoutine = null;
                yield break;
            }

            yield return null;
        }

        if (currentFadeTarget < 0.999f)
        {
            loadingOrbVisibilityRoutine = null;
            yield break;
        }

        if (loadingOrbShowDelayDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(loadingOrbShowDelayDuration);
        }

        loadingOrbVisibilityRoutine = null;
        if (currentFadeTarget < 0.999f)
        {
            yield break;
        }

        SetLoadingMessageVisible(true);
        PlayLoadingOrb();
    }

    private void StopLoadingOrbPresentationRoutine()
    {
        if (loadingOrbVisibilityRoutine == null)
        {
            return;
        }

        StopCoroutine(loadingOrbVisibilityRoutine);
        loadingOrbVisibilityRoutine = null;
    }

    private void PlayLoadingOrb()
    {
        if (loadingOrbInstance == null)
        {
            return;
        }

        EnsureLoadingOrbCameraReady();
        SetLoadingOrbVisible(true);
        foreach (ParticleSystem system in loadingOrbInstance.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.useUnscaledTime = true;
            system.Stop(withChildren: false, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.Play(withChildren: false);
        }

        if (loadingOrbRenderPrimeRoutine != null)
        {
            StopCoroutine(loadingOrbRenderPrimeRoutine);
        }

        loadingOrbRenderPrimeRoutine = StartCoroutine(PrimeLoadingOrbRender());
    }

    private void EnsureLoadingOrbCameraReady()
    {
        if (loadingOrbCamera == null)
        {
            CreateLoadingOrbCameraIfNeeded();
        }

        if (loadingOrbCamera == null || loadingOrbTexture == null)
        {
            return;
        }

        loadingOrbCamera.targetTexture = loadingOrbTexture;
        loadingOrbCamera.cullingMask = GetLoadingOrbLayerMask();
        ConfigureLoadingOrbHdrpCamera(loadingOrbCamera);
        loadingOrbCamera.enabled = true;
    }

    private IEnumerator PrimeLoadingOrbRender()
    {
        // Le premier chargement (Bootstrap -> MainMenu) peut arriver avant
        // le prochain rendu automatique de cette camera secondaire. On force
        // quelques images de la petite RenderTexture de l'orbe.
        for (int i = 0; i < 3; i++)
        {
            yield return new WaitForEndOfFrame();
            if (loadingOrbCamera != null && loadingOrbCamera.targetTexture != null)
            {
                loadingOrbCamera.Render();
            }
        }

        loadingOrbRenderPrimeRoutine = null;
    }

    private void StopLoadingOrb()
    {
        if (loadingOrbRenderPrimeRoutine != null)
        {
            StopCoroutine(loadingOrbRenderPrimeRoutine);
            loadingOrbRenderPrimeRoutine = null;
        }

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
        StopLoadingOrbPresentationRoutine();
        StopLoadingOrb();
        SetLoadingOrbVisible(false);
        SetLoadingMessageVisible(false);
    }

    private void SetLoadingMessageVisible(bool isVisible)
    {
        if (messageText != null)
        {
            messageText.enabled = isVisible;
        }
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
