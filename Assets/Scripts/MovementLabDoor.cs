using UnityEngine;

[DisallowMultipleComponent]
public sealed class MovementLabDoor : MonoBehaviour, ICharacterDetectedInteractable, ILocalInteractHandler
{
    public enum DoorMode
    {
        Hinge,
        Slide
    }

    [Header("Interaction")]
    [SerializeField] private Collider interactionCollider;
    [SerializeField] private Transform interactionAnchor;
    [SerializeField, Min(0.1f)] private float interactionDistance = 2.5f;
    [SerializeField] private int interactionPriority = 20;

    [Header("Door")]
    [SerializeField] private Transform movingPart;
    [SerializeField] private DoorMode mode = DoorMode.Hinge;
    [SerializeField] private float openAngle = 95f;
    [SerializeField] private Vector3 openLocalOffset = new Vector3(0f, 0f, 1.8f);
    [SerializeField, Min(0.01f)] private float animationDuration = 0.45f;
    [SerializeField] private Color closedColor = new Color(0.5f, 0.26f, 0.12f, 1f);
    [SerializeField] private Color detectedColor = new Color(1f, 0.8f, 0.25f, 1f);
    [SerializeField] private Color openColor = new Color(0.32f, 0.75f, 0.5f, 1f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private MaterialPropertyBlock propertyBlock;
    private Renderer cachedRenderer;
    private GameObject detectedCharacter;
    private Vector3 closedLocalPosition;
    private Quaternion closedLocalRotation = Quaternion.identity;
    private float blend;
    private bool open;
    private bool initialized;

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
        float target = open ? 1f : 0f;
        if (!Mathf.Approximately(blend, target))
        {
            float step = Time.deltaTime / Mathf.Max(0.01f, animationDuration);
            blend = Mathf.MoveTowards(blend, target, step);
            ApplyDoorPose();
            RefreshVisual();
        }
    }

    public void Configure(
        DoorMode doorMode,
        Transform part,
        Collider detectionCollider,
        Transform anchor,
        float angle,
        Vector3 slideOffset,
        float maxDistance)
    {
        mode = doorMode;
        movingPart = part;
        interactionCollider = detectionCollider;
        interactionAnchor = anchor;
        openAngle = angle;
        openLocalOffset = slideOffset;
        interactionDistance = Mathf.Max(0.1f, maxDistance);
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

        open = !open;
        Debug.Log($"[MovementLab] {name} {(open ? "opened" : "closed")}.", this);
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
        if (movingPart == null)
        {
            movingPart = transform;
        }

        if (interactionCollider == null)
        {
            interactionCollider = movingPart != null ? movingPart.GetComponentInChildren<Collider>(true) : GetComponent<Collider>();
        }

        if (interactionAnchor == null)
        {
            interactionAnchor = movingPart != null ? movingPart : transform;
        }

        cachedRenderer = movingPart != null
            ? movingPart.GetComponentInChildren<Renderer>(true)
            : GetComponentInChildren<Renderer>(true);
    }

    private void CaptureBasePose()
    {
        if (initialized || movingPart == null)
        {
            return;
        }

        closedLocalPosition = movingPart.localPosition;
        closedLocalRotation = movingPart.localRotation;
        initialized = true;
    }

    private void ApplyDoorPose()
    {
        if (movingPart == null)
        {
            return;
        }

        float eased = Mathf.SmoothStep(0f, 1f, blend);
        if (mode == DoorMode.Slide)
        {
            movingPart.localPosition = closedLocalPosition + openLocalOffset * eased;
            movingPart.localRotation = closedLocalRotation;
            return;
        }

        movingPart.localPosition = closedLocalPosition;
        movingPart.localRotation = closedLocalRotation * Quaternion.Euler(0f, openAngle * eased, 0f);
    }

    private void RefreshVisual()
    {
        if (cachedRenderer == null)
        {
            return;
        }

        Color targetColor = open ? openColor : detectedCharacter != null ? detectedColor : closedColor;
        propertyBlock ??= new MaterialPropertyBlock();
        cachedRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(BaseColorId, targetColor);
        propertyBlock.SetColor(ColorId, targetColor);
        cachedRenderer.SetPropertyBlock(propertyBlock);
    }
}
