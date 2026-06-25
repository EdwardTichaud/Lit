using TMPro;
using UnityEngine;
using UnityEngine.UI;

// UI locale: pointe vers l'Ancient Flame active la plus proche du personnage controle.
[DefaultExecutionOrder(-20)]
[DisallowMultipleComponent]
public class AncientFlameCompassUI : MonoBehaviour, IAncientFlameDisplayTarget
{
    private const string RuntimeHostName = "AncientFlameCompassUI";

    private static AncientFlameCompassUI runtimeInstance;

    [Header("Behaviour")]
    [SerializeField, Tooltip("Masque la boussole si aucun personnage local ou aucune AncientFlame active n'est trouve.")]
    private bool hideWhenNoTarget = true;
    [SerializeField, InspectorName("Ancient Flames eteintes uniquement"), Tooltip("Si actif, la boussole ignore les AncientFlames deja allumees.")]
    private bool targetOnlyUnlitAncientFlames = true;
    [SerializeField, Min(0.05f), Tooltip("Intervalle de recherche de l'AncientFlame la plus proche.")]
    private float targetRefreshInterval = 0.35f;

    [Header("Layout")]
    [SerializeField, Tooltip("Position de la boussole depuis le coin haut droit.")]
    private Vector2 topRightOffset = new Vector2(-28f, -28f);
    [SerializeField, Min(56f), Tooltip("Taille du cadran de boussole.")]
    private float compassSize = 82f;

    [Header("Display")]
    [SerializeField, Tooltip("Format de distance sous le cadran.")]
    private string distanceFormat = "Flame ancienne\n{0:0} m";
    [SerializeField] private Color panelColor = new Color(0.035f, 0.04f, 0.055f, 0.76f);
    [SerializeField] private Color dialColor = new Color(0.11f, 0.12f, 0.14f, 0.86f);
    [SerializeField] private Color tickColor = new Color(0.74f, 0.68f, 0.5f, 0.72f);
    [SerializeField] private Color needleColor = new Color(1f, 0.74f, 0.25f, 1f);
    [SerializeField] private Color textColor = new Color(0.94f, 0.91f, 0.8f, 1f);

    private CanvasGroup rootGroup;
    private RectTransform needlePivot;
    private TMP_Text distanceText;
    private Transform localCharacter;
    private Flame targetFlame;
    private float nextTargetRefreshTime;
    private Camera cachedCamera;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeInstance()
    {
        runtimeInstance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (runtimeInstance != null)
        {
            return;
        }

        AncientFlameCompassUI existing = FindAnyObjectByType<AncientFlameCompassUI>();
        if (existing != null)
        {
            runtimeInstance = existing;
            return;
        }

        GameObject host = new GameObject(RuntimeHostName);
        DontDestroyOnLoad(host);
        runtimeInstance = host.AddComponent<AncientFlameCompassUI>();
    }

    private void Awake()
    {
        if (runtimeInstance != null && runtimeInstance != this)
        {
            Destroy(gameObject);
            return;
        }

        runtimeInstance = this;
        BuildRuntimeUI();
    }

    private void OnEnable()
    {
        LocalPlayerContext.LocalCharacterChanged += OnLocalCharacterChanged;
        AncientFlameDisplayManager.Register(this);
        RefreshTarget();
    }

    private void OnDisable()
    {
        LocalPlayerContext.LocalCharacterChanged -= OnLocalCharacterChanged;
        AncientFlameDisplayManager.Unregister(this);
    }

    private void OnDestroy()
    {
        if (runtimeInstance == this)
        {
            runtimeInstance = null;
        }
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextTargetRefreshTime)
        {
            RefreshTarget();
        }

        UpdateCompassDirection();
    }

    public void ApplyAncientFlameDisplay(AncientFlameDisplaySnapshot snapshot)
    {
        RefreshTarget();
    }

    private void OnLocalCharacterChanged(Transform characterRoot)
    {
        localCharacter = characterRoot;
        RefreshTarget();
    }

    private void RefreshTarget()
    {
        nextTargetRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, targetRefreshInterval);

        localCharacter = ResolveLocalCharacter();
        targetFlame = FindClosestAncientFlame(localCharacter);
        UpdateVisibility();
    }

    private Transform ResolveLocalCharacter()
    {
        if (LocalPlayerContext.LocalCharacterRoot != null)
        {
            return LocalPlayerContext.LocalCharacterRoot;
        }

        GameObject controlled = LocalPlayerUtils.GetControlledCharacter();
        return controlled != null ? controlled.transform : null;
    }

    private Flame FindClosestAncientFlame(Transform characterRoot)
    {
        if (characterRoot == null)
        {
            return null;
        }

        Flame closest = null;
        float closestSqr = float.PositiveInfinity;

        AgeManager ageManager = AgeManager.ActiveInstance;
        if (ageManager != null)
        {
            EvaluateFlames(ageManager.AncientFlames, characterRoot.position, ref closest, ref closestSqr);
        }

        if (closest == null)
        {
            Flame[] sceneFlames = FindObjectsByType<Flame>(FindObjectsInactive.Exclude);
            EvaluateFlames(sceneFlames, characterRoot.position, ref closest, ref closestSqr);
        }

        return closest;
    }

    private void EvaluateFlames(
        System.Collections.Generic.IReadOnlyList<Flame> flames,
        Vector3 characterPosition,
        ref Flame closest,
        ref float closestSqr)
    {
        if (flames == null)
        {
            return;
        }

        for (int i = 0; i < flames.Count; i++)
        {
            Flame flame = flames[i];
            if (!IsUsableTarget(flame))
            {
                continue;
            }

            if (targetOnlyUnlitAncientFlames && flame.IsLit)
            {
                continue;
            }

            float distanceSqr = GetFlatSqrDistance(characterPosition, GetFlamePosition(flame));
            if (distanceSqr < closestSqr)
            {
                closestSqr = distanceSqr;
                closest = flame;
            }
        }
    }

    private void UpdateCompassDirection()
    {
        if (localCharacter == null || !IsUsableTarget(targetFlame))
        {
            UpdateVisibility();
            return;
        }

        Vector3 delta = GetFlamePosition(targetFlame) - localCharacter.position;
        delta.y = 0f;

        float distance = delta.magnitude;
        if (distanceText != null)
        {
            distanceText.text = string.Format(distanceFormat, distance);
        }

        if (needlePivot == null || delta.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 forward = ResolveReferenceForward();
        float angle = Vector3.SignedAngle(forward, delta.normalized, Vector3.up);
        needlePivot.localEulerAngles = new Vector3(0f, 0f, -angle);
    }

    private Vector3 ResolveReferenceForward()
    {
        if (cachedCamera == null || !cachedCamera.isActiveAndEnabled)
        {
            cachedCamera = Camera.main;
        }

        Vector3 forward = cachedCamera != null ? cachedCamera.transform.forward : localCharacter.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = localCharacter.forward;
            forward.y = 0f;
        }

        return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
    }

    private void UpdateVisibility()
    {
        if (rootGroup == null)
        {
            return;
        }

        bool visible = localCharacter != null && IsUsableTarget(targetFlame);
        if (!visible && !hideWhenNoTarget)
        {
            visible = true;
        }

        rootGroup.alpha = visible ? 1f : 0f;
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;
    }

    private void BuildRuntimeUI()
    {
        if (rootGroup != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("AncientFlameCompassCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = CreateImage(canvasObject.transform, "Root", panelColor).rectTransform;
        root.anchorMin = new Vector2(1f, 1f);
        root.anchorMax = new Vector2(1f, 1f);
        root.pivot = new Vector2(1f, 1f);
        root.anchoredPosition = topRightOffset;
        root.sizeDelta = new Vector2(compassSize + 34f, compassSize + 52f);

        rootGroup = root.gameObject.AddComponent<CanvasGroup>();
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;

        RectTransform dial = CreateImage(root, "Dial", dialColor).rectTransform;
        dial.anchorMin = new Vector2(0.5f, 1f);
        dial.anchorMax = new Vector2(0.5f, 1f);
        dial.pivot = new Vector2(0.5f, 1f);
        dial.anchoredPosition = new Vector2(0f, -12f);
        dial.sizeDelta = new Vector2(compassSize, compassSize);

        CreateTick(dial, "TickN", new Vector2(0.5f, 1f), new Vector2(0f, -7f), new Vector2(4f, 14f));
        CreateTick(dial, "TickS", new Vector2(0.5f, 0f), new Vector2(0f, 7f), new Vector2(4f, 14f));
        CreateTick(dial, "TickE", new Vector2(1f, 0.5f), new Vector2(-7f, 0f), new Vector2(14f, 4f));
        CreateTick(dial, "TickW", new Vector2(0f, 0.5f), new Vector2(7f, 0f), new Vector2(14f, 4f));

        GameObject pivotObject = new GameObject("NeedlePivot", typeof(RectTransform));
        pivotObject.transform.SetParent(dial, false);
        needlePivot = pivotObject.GetComponent<RectTransform>();
        needlePivot.anchorMin = new Vector2(0.5f, 0.5f);
        needlePivot.anchorMax = new Vector2(0.5f, 0.5f);
        needlePivot.pivot = new Vector2(0.5f, 0.5f);
        needlePivot.anchoredPosition = Vector2.zero;
        needlePivot.sizeDelta = Vector2.zero;

        RectTransform needle = CreateImage(needlePivot, "Needle", needleColor).rectTransform;
        needle.anchorMin = new Vector2(0.5f, 0.5f);
        needle.anchorMax = new Vector2(0.5f, 0.5f);
        needle.pivot = new Vector2(0.5f, 0f);
        needle.anchoredPosition = Vector2.zero;
        needle.sizeDelta = new Vector2(5f, compassSize * 0.38f);

        RectTransform cap = CreateImage(needlePivot, "Cap", needleColor).rectTransform;
        cap.anchorMin = new Vector2(0.5f, 0.5f);
        cap.anchorMax = new Vector2(0.5f, 0.5f);
        cap.pivot = new Vector2(0.5f, 0.5f);
        cap.anchoredPosition = Vector2.zero;
        cap.sizeDelta = new Vector2(10f, 10f);

        GameObject textObject = new GameObject("DistanceText", typeof(RectTransform));
        textObject.transform.SetParent(root, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 0f);
        textRect.pivot = new Vector2(0.5f, 0f);
        textRect.anchoredPosition = new Vector2(0f, 9f);
        textRect.sizeDelta = new Vector2(-12f, 34f);

        distanceText = textObject.AddComponent<TextMeshProUGUI>();
        distanceText.alignment = TextAlignmentOptions.Center;
        distanceText.color = textColor;
        distanceText.fontSize = 15f;
        distanceText.textWrappingMode = TextWrappingModes.NoWrap;
        distanceText.raycastTarget = false;

        UpdateVisibility();
    }

    private void CreateTick(RectTransform parent, string name, Vector2 anchor, Vector2 offset, Vector2 size)
    {
        RectTransform tick = CreateImage(parent, name, tickColor).rectTransform;
        tick.anchorMin = anchor;
        tick.anchorMax = anchor;
        tick.pivot = new Vector2(0.5f, 0.5f);
        tick.anchoredPosition = offset;
        tick.sizeDelta = size;
    }

    private Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static bool IsUsableTarget(Flame flame)
    {
        return flame != null
            && flame.IsAncientFlame
            && flame.isActiveAndEnabled
            && flame.gameObject.activeInHierarchy;
    }

    private static Vector3 GetFlamePosition(Flame flame)
    {
        if (flame == null)
        {
            return Vector3.zero;
        }

        Transform anchor = flame.GetInteractionAnchor();
        return anchor != null ? anchor.position : flame.transform.position;
    }

    private static float GetFlatSqrDistance(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        delta.y = 0f;
        return delta.sqrMagnitude;
    }
}
