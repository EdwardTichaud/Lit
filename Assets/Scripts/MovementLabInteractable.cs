using UnityEngine;

[DisallowMultipleComponent]
public sealed class MovementLabInteractable : MonoBehaviour, ICharacterDetectedInteractable, ILocalInteractHandler
{
    public enum ResponseMode
    {
        ToggleColor,
        Lift,
        PulseScale
    }

    [Header("Interaction")]
    [SerializeField] private Collider interactionCollider;
    [SerializeField] private Transform interactionAnchor;
    [SerializeField, Min(0.1f)] private float interactionDistance = 2.2f;
    [SerializeField] private int interactionPriority = 5;

    [Header("Response")]
    [SerializeField] private ResponseMode responseMode = ResponseMode.ToggleColor;
    [SerializeField] private Transform animatedPart;
    [SerializeField] private Vector3 activeLocalOffset = new Vector3(0f, 1f, 0f);
    [SerializeField, Min(0.01f)] private float animationDuration = 0.25f;
    [SerializeField] private Color idleColor = new Color(0.25f, 0.55f, 0.85f, 1f);
    [SerializeField] private Color detectedColor = new Color(1f, 0.8f, 0.25f, 1f);
    [SerializeField] private Color activeColor = new Color(0.25f, 0.95f, 0.55f, 1f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private MaterialPropertyBlock propertyBlock;
    private Renderer cachedRenderer;
    private GameObject detectedCharacter;
    private Vector3 closedLocalPosition;
    private Vector3 closedLocalScale = Vector3.one;
    private float blend;
    private bool active;
    private bool initialized;

    public bool IsActive => active;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
        CaptureBasePose();
        RefreshVisual();
    }

    private void OnValidate()
    {
        animationDuration = Mathf.Max(0.01f, animationDuration);
        interactionDistance = Mathf.Max(0.1f, interactionDistance);
        CacheReferences();
        RefreshVisual();
    }

    private void Update()
    {
        float target = active ? 1f : 0f;
        if (!Mathf.Approximately(blend, target))
        {
            float step = Time.deltaTime / Mathf.Max(0.01f, animationDuration);
            blend = Mathf.MoveTowards(blend, target, step);
            ApplyAnimatedPose();
            RefreshVisual();
        }
    }

    public void Configure(
        ResponseMode mode,
        Transform part,
        Collider detectionCollider,
        Transform anchor,
        float maxDistance,
        int priority)
    {
        responseMode = mode;
        animatedPart = part;
        interactionCollider = detectionCollider;
        interactionAnchor = anchor;
        interactionDistance = Mathf.Max(0.1f, maxDistance);
        interactionPriority = priority;
        CacheReferences();
        CaptureBasePose();
        RefreshVisual();
    }

    public bool TryHandleLocalInteract()
    {
        if (!isActiveAndEnabled)
        {
            return false;
        }

        active = !active;
        Debug.Log($"[MovementLab] {name} toggled {(active ? "on" : "off")}.", this);
        return true;
    }

    public bool CanBeDetectedBy(SquadCharacterController controller)
    {
        return isActiveAndEnabled && controller != null;
    }

    public Collider GetInteractionDetectionCollider()
    {
        if (interactionCollider == null)
        {
            interactionCollider = CharacterInteractionDetection.ResolveInteractionCollider(this, null);
        }

        return interactionCollider;
    }

    public Transform GetInteractionAnchor()
    {
        return interactionAnchor != null ? interactionAnchor : transform;
    }

    public float GetInteractionMaxDistance(SquadCharacterController controller)
    {
        return interactionDistance;
    }

    public int GetInteractionPriority(SquadCharacterController controller)
    {
        return interactionPriority;
    }

    public void SetDetectedCharacter(GameObject character)
    {
        detectedCharacter = character;
        RefreshVisual();
    }

    private void CacheReferences()
    {
        if (interactionCollider == null)
        {
            interactionCollider = GetComponent<Collider>();
        }

        if (interactionAnchor == null)
        {
            interactionAnchor = transform;
        }

        if (animatedPart == null)
        {
            animatedPart = transform;
        }

        cachedRenderer = animatedPart != null
            ? animatedPart.GetComponentInChildren<Renderer>(true)
            : GetComponentInChildren<Renderer>(true);
    }

    private void CaptureBasePose()
    {
        if (initialized || animatedPart == null)
        {
            return;
        }

        closedLocalPosition = animatedPart.localPosition;
        closedLocalScale = animatedPart.localScale;
        initialized = true;
    }

    private void ApplyAnimatedPose()
    {
        if (animatedPart == null)
        {
            return;
        }

        float eased = Mathf.SmoothStep(0f, 1f, blend);
        switch (responseMode)
        {
            case ResponseMode.Lift:
                animatedPart.localPosition = closedLocalPosition + activeLocalOffset * eased;
                break;
            case ResponseMode.PulseScale:
                animatedPart.localScale = closedLocalScale * (1f + 0.25f * Mathf.Sin(eased * Mathf.PI));
                break;
            default:
                animatedPart.localPosition = closedLocalPosition;
                animatedPart.localScale = closedLocalScale;
                break;
        }
    }

    private void RefreshVisual()
    {
        if (cachedRenderer == null)
        {
            return;
        }

        Color targetColor = active ? activeColor : detectedCharacter != null ? detectedColor : idleColor;
        propertyBlock ??= new MaterialPropertyBlock();
        cachedRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(BaseColorId, targetColor);
        propertyBlock.SetColor(ColorId, targetColor);
        cachedRenderer.SetPropertyBlock(propertyBlock);
    }
}
