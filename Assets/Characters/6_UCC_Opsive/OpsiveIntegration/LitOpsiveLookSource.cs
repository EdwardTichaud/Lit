using Opsive.Shared.Events;
using Opsive.UltimateCharacterController.Camera;
using Opsive.UltimateCharacterController.Character;
using UnityEngine;

// Fallback look source used until the UCC gameplay camera attaches to the character.
[DefaultExecutionOrder(-1000)]
public class LitOpsiveLookSource : MonoBehaviour, ILookSource
{
    [SerializeField, Tooltip("Character GameObject that receives Opsive look-source events.")]
    private GameObject eventTarget;
    [SerializeField, Tooltip("Optional transform used as the look origin.")]
    private Transform lookTransform;
    [SerializeField, Tooltip("Distance reported to Opsive look consumers.")]
    private float lookDirectionDistance = 100f;
    [SerializeField, Tooltip("Fallback height above this transform when no head/look transform is assigned.")]
    private float fallbackLookHeight = 1.6f;
    [SerializeField, Tooltip("Reports and preserves a world-space planar direction instead of inheriting the character parent's rotation.")]
    private bool useStableWorldPlanarDirection = true;
    [SerializeField, Tooltip("Optional yaw correction for characters whose authored visual forward differs from the gameplay root.")]
    private float planarDirectionYawOffset = 0f;

    private bool attached;
    private bool started;
    private int attachRetryFramesRemaining = InitialAttachRetryFrames;
    private Vector3 stableWorldPlanarDirection;
    private bool hasStableWorldPlanarDirection;

    private const int InitialAttachRetryFrames = 8;

    public GameObject GameObject => gameObject;
    public Transform Transform => transform;
    public float LookDirectionDistance => lookDirectionDistance;
    public float Pitch => 0f;

    public GameObject EventTarget
    {
        get => eventTarget;
        set => eventTarget = value;
    }

    public void AttachToCharacter()
    {
        ResolveDefaults();
        if (eventTarget == null)
        {
            return;
        }

        if (HasGameplayCameraLookSource())
        {
            return;
        }

        EventHandler.ExecuteEvent<ILookSource>(eventTarget, "OnCharacterAttachLookSource", this);
        attached = true;
    }

    public bool IsAttachedToCharacter()
    {
        ResolveDefaults();
        if (eventTarget == null)
        {
            return false;
        }

        UltimateCharacterLocomotion locomotion = eventTarget.GetComponent<UltimateCharacterLocomotion>();
        return locomotion != null && ReferenceEquals(locomotion.LookSource, this);
    }

    public void QueueAttachRetries()
    {
        attachRetryFramesRemaining = Mathf.Max(attachRetryFramesRemaining, InitialAttachRetryFrames);
    }

    public void SetPlanarLookDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        stableWorldPlanarDirection = ApplyYawOffset(direction.normalized);
        hasStableWorldPlanarDirection = true;
        RefreshStableWorldRotation();
    }

    public Vector3 LookPosition(bool characterLookPosition)
    {
        if (lookTransform != null)
        {
            return lookTransform.position;
        }

        return transform.position + Vector3.up * fallbackLookHeight;
    }

    public Vector3 LookDirection(bool characterLookDirection)
    {
        return ResolveReportedLookDirection();
    }

    public Vector3 LookDirection(Vector3 lookPosition, bool characterLookDirection, int layerMask, bool includeRecoil, bool includeMovementSpread)
    {
        return ResolveReportedLookDirection();
    }

    private void Awake()
    {
        ResolveDefaults();
        EnsureStableWorldPlanarDirection();
    }

    private void OnEnable()
    {
        if (started)
        {
            QueueAttachRetries();
            AttachToCharacter();
        }
    }

    private void Start()
    {
        started = true;
        QueueAttachRetries();
        AttachToCharacter();
    }

    private void FixedUpdate()
    {
        RefreshStableWorldRotation();

        if (attachRetryFramesRemaining > 0)
        {
            attachRetryFramesRemaining--;
            AttachToCharacter();
            return;
        }

        if (!IsAttachedToCharacter())
        {
            AttachToCharacter();
        }
    }

    private void Update()
    {
        RefreshStableWorldRotation();
    }

    private void LateUpdate()
    {
        RefreshStableWorldRotation();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            QueueAttachRetries();
            AttachToCharacter();
        }
    }

    private void OnTransformParentChanged()
    {
        eventTarget = null;
        QueueAttachRetries();
        AttachToCharacter();
    }

    private void OnDisable()
    {
        if (attached && eventTarget != null && IsAttachedToCharacter())
        {
            EventHandler.ExecuteEvent<ILookSource>(eventTarget, "OnCharacterAttachLookSource", null);
        }

        attached = false;
    }

    private void ResolveDefaults()
    {
        if (eventTarget == null)
        {
            UltimateCharacterLocomotion locomotion = GetComponentInParent<UltimateCharacterLocomotion>();
            eventTarget = locomotion != null ? locomotion.gameObject : gameObject;
        }

        if (lookTransform == null)
        {
            Animator animator = GetComponentInParent<Animator>();
            if (animator != null && animator.isHuman)
            {
                lookTransform = animator.GetBoneTransform(HumanBodyBones.Head);
            }
        }
    }

    private Vector3 ResolveReportedLookDirection()
    {
        if (!useStableWorldPlanarDirection)
        {
            Vector3 transformDirection = transform.forward;
            transformDirection.y = 0f;
            return transformDirection.sqrMagnitude > 0.0001f ? transformDirection.normalized : Vector3.forward;
        }

        EnsureStableWorldPlanarDirection();
        return stableWorldPlanarDirection.sqrMagnitude > 0.0001f
            ? stableWorldPlanarDirection
            : Vector3.forward;
    }

    private void EnsureStableWorldPlanarDirection()
    {
        if (hasStableWorldPlanarDirection && stableWorldPlanarDirection.sqrMagnitude > 0.0001f)
        {
            return;
        }

        Vector3 direction = eventTarget != null ? eventTarget.transform.forward : transform.forward;
        direction.y = 0f;
        stableWorldPlanarDirection = direction.sqrMagnitude > 0.0001f
            ? ApplyYawOffset(direction.normalized)
            : Vector3.forward;
        hasStableWorldPlanarDirection = true;
    }

    private Vector3 ApplyYawOffset(Vector3 direction)
    {
        if (Mathf.Abs(planarDirectionYawOffset) <= 0.001f)
        {
            return direction;
        }

        Vector3 rotated = Quaternion.AngleAxis(planarDirectionYawOffset, Vector3.up) * direction;
        rotated.y = 0f;
        return rotated.sqrMagnitude > 0.0001f ? rotated.normalized : direction;
    }

    private void RefreshStableWorldRotation()
    {
        if (!useStableWorldPlanarDirection)
        {
            return;
        }

        Vector3 direction = ResolveReportedLookDirection();
        if (direction.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }
    }

    private bool HasGameplayCameraLookSource()
    {
        if (eventTarget == null)
        {
            return false;
        }

        UltimateCharacterLocomotion locomotion = eventTarget.GetComponent<UltimateCharacterLocomotion>();
        return locomotion != null && locomotion.LookSource is CameraController;
    }
}
