using TMPro;
using UnityEngine;

// UI locale: affiche les points cardinaux et pointe vers l'Ancient Flame la plus proche du personnage controle.
[DefaultExecutionOrder(-20)]
[DisallowMultipleComponent]
public class AncientFlameCompassUI : MonoBehaviour, IAncientFlameDisplayTarget
{
    private static AncientFlameCompassUI runtimeInstance;

    [Header("Behaviour")]
    [SerializeField, Tooltip("Masque la boussole si aucun personnage local ou aucune AncientFlame active n'est trouve.")]
    private bool hideWhenNoTarget = true;
    [SerializeField, Min(0.05f), Tooltip("Intervalle de recherche de l'AncientFlame la plus proche.")]
    private float targetRefreshInterval = 0.35f;

    [Header("Display")]
    [SerializeField, Tooltip("Format de distance sous le cadran.")]
    private string distanceFormat = "Flame ancienne\n{0:0} m";

    private CanvasGroup rootGroup;
    private Canvas rootCanvas;
    private RectTransform compassRender;
    private RectTransform arrowPivot;
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

    private void Awake()
    {
        if (runtimeInstance != null && runtimeInstance != this)
        {
            Destroy(gameObject);
            return;
        }

        runtimeInstance = this;
        if (!TryBindExistingUI())
        {
            Debug.LogWarning("AncientFlameCompassUI requiert une UI de scene contenant Compass_Render et Arrow.", this);
            enabled = false;
        }
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
        Vector3 forward = ResolveReferenceForward();
        float cameraYaw = Vector3.SignedAngle(Vector3.forward, forward, Vector3.up);
        SetScreenRotationZ(compassRender, cameraYaw);

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

        float angle = Vector3.SignedAngle(forward, delta.normalized, Vector3.up);
        SetScreenRotationZ(ResolveArrowPivot(), -angle);
    }

    private Vector3 ResolveReferenceForward()
    {
        if (cachedCamera == null || !cachedCamera.isActiveAndEnabled)
        {
            cachedCamera = Camera.main;
        }

        Vector3 forward = cachedCamera != null
            ? cachedCamera.transform.forward
            : localCharacter != null
                ? localCharacter.forward
                : Vector3.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f && localCharacter != null)
        {
            forward = localCharacter.forward;
            forward.y = 0f;
        }

        return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
    }

    private void UpdateVisibility()
    {
        if (rootGroup == null && rootCanvas == null)
        {
            return;
        }

        bool visible = localCharacter != null && IsUsableTarget(targetFlame);
        if (!visible && !hideWhenNoTarget)
        {
            visible = true;
        }

        if (rootGroup != null)
        {
            rootGroup.alpha = visible ? 1f : 0f;
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;
        }

        if (rootCanvas != null)
        {
            rootCanvas.enabled = visible;
        }
    }

    private bool TryBindExistingUI()
    {
        RectTransform existingCompassRender = FindChildRectTransform("Compass_Render", "CompassRender");
        RectTransform existingArrow = FindChildRectTransform("Arrow");
        if (existingCompassRender == null || existingArrow == null)
        {
            return false;
        }

        compassRender = existingCompassRender;
        arrowPivot = existingArrow;
        needlePivot = existingArrow;

        rootGroup = GetComponent<CanvasGroup>();
        rootCanvas = GetComponent<Canvas>();

        if (distanceText == null)
        {
            RectTransform distanceRect = FindChildRectTransform("DistanceText");
            distanceText = distanceRect != null ? distanceRect.GetComponent<TMP_Text>() : null;
        }

        UpdateVisibility();
        return true;
    }

    private RectTransform FindChildRectTransform(params string[] names)
    {
        if (names == null || names.Length == 0)
        {
            return null;
        }

        RectTransform[] rects = GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform rect = rects[i];
            if (rect == null)
            {
                continue;
            }

            for (int nameIndex = 0; nameIndex < names.Length; nameIndex++)
            {
                if (string.Equals(rect.name, names[nameIndex], System.StringComparison.OrdinalIgnoreCase))
                {
                    return rect;
                }
            }
        }

        return null;
    }

    private RectTransform ResolveArrowPivot()
    {
        return arrowPivot != null ? arrowPivot : needlePivot;
    }

    private static void SetScreenRotationZ(RectTransform target, float screenRotationZ)
    {
        if (target == null)
        {
            return;
        }

        float inheritedRotationZ = ResolveInheritedRotationZ(target);
        float localRotationZ = Mathf.DeltaAngle(0f, screenRotationZ - inheritedRotationZ);
        target.localEulerAngles = new Vector3(0f, 0f, localRotationZ);
    }

    private static float ResolveInheritedRotationZ(RectTransform target)
    {
        float rotationZ = 0f;
        Transform current = target != null ? target.parent : null;
        while (current != null)
        {
            rotationZ += NormalizeAngle(current.localEulerAngles.z);
            current = current.parent;
        }

        return NormalizeAngle(rotationZ);
    }

    private static float NormalizeAngle(float angle)
    {
        return Mathf.DeltaAngle(0f, angle);
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
