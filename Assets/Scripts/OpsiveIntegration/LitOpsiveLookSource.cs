using Opsive.Shared.Events;
using Opsive.UltimateCharacterController.Character;
using UnityEngine;

// Look source used while the project camera remains outside Opsive.
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

    private bool attached;
    private bool started;
    private int attachRetryFramesRemaining = InitialAttachRetryFrames;

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

        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
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
        return transform.forward;
    }

    public Vector3 LookDirection(Vector3 lookPosition, bool characterLookDirection, int layerMask, bool includeRecoil, bool includeMovementSpread)
    {
        return transform.forward;
    }

    private void Awake()
    {
        ResolveDefaults();
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
        if (attached && eventTarget != null)
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
}
