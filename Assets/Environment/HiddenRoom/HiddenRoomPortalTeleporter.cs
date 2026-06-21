using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class HiddenRoomPortalTeleporter : MonoBehaviour
{
    [SerializeField] private HiddenRoomBootstrap bootstrap;
    [SerializeField] private Transform destinationAnchor;
    [SerializeField] private bool autoResolveBootstrap = true;
    [SerializeField] private bool requireControlledCharacter = true;
    [SerializeField] private string portalLabel = "Portal";
    [SerializeField, Min(0f)] private float cooldownSeconds = 0.35f;

    [Header("Trigger")]
    [SerializeField] private bool teleportOnTriggerEnter = true;
    [SerializeField] private bool teleportOnTriggerStay = true;
    [SerializeField] private bool pollTriggerOverlap = true;
    [SerializeField, Min(0.02f)] private float overlapPollInterval = 0.1f;
    [SerializeField] private LayerMask overlapLayers = ~0;
    [SerializeField] private QueryTriggerInteraction overlapTriggerInteraction = QueryTriggerInteraction.Collide;

    private Collider triggerCollider;
    private float lastLocalTeleportTime = float.NegativeInfinity;
    private float nextOverlapPollTime;
    private readonly Collider[] overlapBuffer = new Collider[32];

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
        ResolveBootstrapIfNeeded();
    }

    private void OnEnable()
    {
        EnsureTriggerCollider();
        ResolveBootstrapIfNeeded();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!teleportOnTriggerEnter)
        {
            return;
        }

        TryTeleport(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!teleportOnTriggerStay)
        {
            return;
        }

        TryTeleport(other);
    }

    private void Update()
    {
        if (!pollTriggerOverlap || triggerCollider == null || Time.unscaledTime < nextOverlapPollTime)
        {
            return;
        }

        nextOverlapPollTime = Time.unscaledTime + overlapPollInterval;
        PollTriggerOverlap();
    }

    private void EnsureTriggerCollider()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void ResolveBootstrapIfNeeded()
    {
        if (!autoResolveBootstrap || bootstrap != null)
        {
            return;
        }

        bootstrap = GetComponentInParent<HiddenRoomBootstrap>(true);
        if (bootstrap == null)
        {
#if UNITY_2023_1_OR_NEWER
            bootstrap = FindAnyObjectByType<HiddenRoomBootstrap>(FindObjectsInactive.Include);
#else
            bootstrap = FindSceneBootstrap();
#endif
        }

        if (bootstrap != null)
        {
            bootstrap.EnsureSceneSetup();
        }
    }

    private static HiddenRoomBootstrap FindSceneBootstrap()
    {
        HiddenRoomBootstrap[] candidates = Resources.FindObjectsOfTypeAll<HiddenRoomBootstrap>();
        for (int i = 0; i < candidates.Length; i++)
        {
            HiddenRoomBootstrap candidate = candidates[i];
            if (candidate == null)
            {
                continue;
            }

            Scene scene = candidate.gameObject.scene;
            if (scene.IsValid() && scene.isLoaded)
            {
                return candidate;
            }
        }

        return null;
    }

    private void PollTriggerOverlap()
    {
        if (triggerCollider is BoxCollider boxCollider)
        {
            PollBoxTriggerOverlap(boxCollider);
            return;
        }

        Bounds bounds = triggerCollider.bounds;
        int count = Physics.OverlapBoxNonAlloc(
            bounds.center,
            bounds.extents,
            overlapBuffer,
            Quaternion.identity,
            overlapLayers,
            overlapTriggerInteraction);

        TryTeleportOverlaps(count);
    }

    private void PollBoxTriggerOverlap(BoxCollider boxCollider)
    {
        Vector3 lossyScale = boxCollider.transform.lossyScale;
        Vector3 halfExtents = Vector3.Scale(boxCollider.size, Abs(lossyScale)) * 0.5f;
        Vector3 center = boxCollider.transform.TransformPoint(boxCollider.center);

        int count = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            overlapBuffer,
            boxCollider.transform.rotation,
            overlapLayers,
            overlapTriggerInteraction);

        TryTeleportOverlaps(count);
    }

    private void TryTeleportOverlaps(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Collider candidate = overlapBuffer[i];
            overlapBuffer[i] = null;

            if (candidate == null || candidate == triggerCollider)
            {
                continue;
            }

            TryTeleport(candidate);
        }
    }

    private void TryTeleport(Collider other)
    {
        if (!isActiveAndEnabled || destinationAnchor == null || other == null)
        {
            return;
        }

        ResolveBootstrapIfNeeded();

        if (Time.unscaledTime - lastLocalTeleportTime < cooldownSeconds)
        {
            return;
        }

        Transform travelerRoot = ResolveTravelerRoot(other);
        if (travelerRoot == null)
        {
            return;
        }

        if (bootstrap != null && bootstrap.IsTravelerOnCooldown(travelerRoot))
        {
            return;
        }

        if (requireControlledCharacter && !IsControlledTraveler(travelerRoot))
        {
            return;
        }

        bool teleported = bootstrap != null
            ? bootstrap.TryTeleport(travelerRoot, destinationAnchor, this)
            : TeleportTravelerDirectly(travelerRoot);

        if (!teleported)
        {
            return;
        }

        AudioManager.EnsureInstance()?.PlayActionCue(ActionAudioCue.Teleport, destinationAnchor.position);
        lastLocalTeleportTime = Time.unscaledTime;
    }

    private Transform ResolveTravelerRoot(Collider other)
    {
        if (other == null)
        {
            return null;
        }

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

    private bool IsControlledTraveler(Transform travelerRoot)
    {
        if (travelerRoot == null)
        {
            return false;
        }

        if (bootstrap != null)
        {
            return bootstrap.IsControlledTraveler(travelerRoot);
        }

        GameObject controlled = LocalPlayerUtils.GetControlledCharacter();
        if (controlled == null && SquadManager.Instance != null)
        {
            controlled = SquadManager.Instance.currentCharacter;
        }

        if (controlled == null)
        {
            return travelerRoot.GetComponentInParent<SquadCharacterController>() != null
                || travelerRoot.GetComponentInChildren<SquadCharacterController>(true) != null;
        }

        Transform controlledTransform = controlled.transform;
        return SharesHierarchy(travelerRoot, controlledTransform);
    }

    private bool TeleportTravelerDirectly(Transform travelerRoot)
    {
        if (travelerRoot == null || destinationAnchor == null)
        {
            return false;
        }

        SquadCharacterController squadController = travelerRoot.GetComponent<SquadCharacterController>();
        if (squadController == null)
        {
            squadController = travelerRoot.GetComponentInChildren<SquadCharacterController>(true);
        }

        if (squadController == null ||
            !squadController.TrySetUccExternalPositionAndRotation(destinationAnchor.position, destinationAnchor.rotation, stopActiveAbilities: true))
        {
            return false;
        }

        squadController.Stop();
        return true;
    }

    private static Vector3 Abs(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    private static bool SharesHierarchy(Transform a, Transform b)
    {
        if (a == null || b == null)
        {
            return false;
        }

        return a == b || a.IsChildOf(b) || b.IsChildOf(a);
    }

    private void OnValidate()
    {
        cooldownSeconds = Mathf.Max(0f, cooldownSeconds);
        overlapPollInterval = Mathf.Max(0.02f, overlapPollInterval);
        EnsureTriggerCollider();
    }
}
