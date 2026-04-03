using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class HiddenRoomPortalTeleporter : MonoBehaviour
{
    [SerializeField] private HiddenRoomBootstrap bootstrap;
    [SerializeField] private Transform destinationAnchor;
    [SerializeField] private bool requireControlledCharacter = true;
    [SerializeField] private string portalLabel = "Portal";
    [SerializeField, Min(0f)] private float cooldownSeconds = 0.35f;

    private Collider triggerCollider;
    private float lastLocalTeleportTime = float.NegativeInfinity;

    public void Configure(
        HiddenRoomBootstrap bootstrap,
        Transform destinationAnchor,
        bool requireControlledCharacter,
        string portalLabel,
        float cooldownSeconds)
    {
        this.bootstrap = bootstrap;
        this.destinationAnchor = destinationAnchor;
        this.requireControlledCharacter = requireControlledCharacter;
        this.portalLabel = portalLabel;
        this.cooldownSeconds = Mathf.Max(0f, cooldownSeconds);
        EnsureTriggerCollider();
    }

    public void SetDestinationAnchor(Transform destinationAnchor)
    {
        this.destinationAnchor = destinationAnchor;
    }

    private void Awake()
    {
        EnsureTriggerCollider();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryTeleport(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryTeleport(other);
    }

    private void EnsureTriggerCollider()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void TryTeleport(Collider other)
    {
        if (!isActiveAndEnabled || bootstrap == null || destinationAnchor == null || other == null)
        {
            return;
        }

        if (Time.unscaledTime - lastLocalTeleportTime < cooldownSeconds)
        {
            return;
        }

        Transform travelerRoot = ResolveTravelerRoot(other);
        if (travelerRoot == null)
        {
            return;
        }

        if (bootstrap.IsTravelerOnCooldown(travelerRoot))
        {
            return;
        }

        if (requireControlledCharacter && !bootstrap.IsControlledTraveler(travelerRoot))
        {
            return;
        }

        if (!bootstrap.TryTeleport(travelerRoot, destinationAnchor, this))
        {
            return;
        }

        lastLocalTeleportTime = Time.unscaledTime;
    }

    private Transform ResolveTravelerRoot(Collider other)
    {
        SquadCharacterController squadController = other.GetComponentInParent<SquadCharacterController>();
        if (squadController != null)
        {
            return squadController.transform;
        }

        CharacterController characterController = other.GetComponentInParent<CharacterController>();
        if (characterController != null)
        {
            return characterController.transform;
        }

        if (other.attachedRigidbody != null)
        {
            return other.attachedRigidbody.transform;
        }

        return other.transform.root;
    }

    private void OnValidate()
    {
        cooldownSeconds = Mathf.Max(0f, cooldownSeconds);
        EnsureTriggerCollider();
    }
}
