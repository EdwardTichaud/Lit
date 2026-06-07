using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TriggerPairTeleporter : MonoBehaviour
{
    [System.Serializable]
    private sealed class TeleportEndpoint
    {
        [Tooltip("Collider Trigger utilise comme entree.")]
        public Collider trigger;

        [Tooltip("Point d'arrivee optionnel. Si vide, le script utilise l'offset local ci-dessous.")]
        public Transform arrivalPoint;

        [Tooltip("Offset local applique depuis le trigger si aucun point d'arrivee n'est renseigne.")]
        public Vector3 fallbackArrivalOffset = new Vector3(0f, 0f, 1.25f);
    }

    private sealed class EndpointRuntimeState
    {
        public readonly Dictionary<Transform, HashSet<int>> overlapColliderIdsByTraveler = new Dictionary<Transform, HashSet<int>>();
    }

    [Header("Triggers")]
    [SerializeField] private TeleportEndpoint trigger1 = new TeleportEndpoint();
    [SerializeField] private TeleportEndpoint trigger2 = new TeleportEndpoint();
    [SerializeField] private bool bidirectional = true;

    [Header("Teleportation")]
    [SerializeField, Min(0f)] private float cooldownSeconds = 0.35f;
    [SerializeField, Min(0.0001f)] private float pointInsideTolerance = 0.01f;
    [SerializeField] private bool stopSquadCharacterAfterTeleport = true;

    [Header("Debug")]
    [SerializeField] private bool logConfigurationWarnings = true;

    private readonly EndpointRuntimeState[] endpointStates =
    {
        new EndpointRuntimeState(),
        new EndpointRuntimeState()
    };

    private readonly Dictionary<Transform, float> teleportCooldownUntilByTraveler = new Dictionary<Transform, float>();
    private readonly Dictionary<Transform, int> blockedEndpointByTraveler = new Dictionary<Transform, int>();

    private void Awake()
    {
        BindEndpointRelay(0);
        BindEndpointRelay(1);
        ValidateConfiguration();
    }

    private void OnDisable()
    {
        endpointStates[0].overlapColliderIdsByTraveler.Clear();
        endpointStates[1].overlapColliderIdsByTraveler.Clear();
        teleportCooldownUntilByTraveler.Clear();
        blockedEndpointByTraveler.Clear();
    }

    private void OnValidate()
    {
        cooldownSeconds = Mathf.Max(0f, cooldownSeconds);
        pointInsideTolerance = Mathf.Max(0.0001f, pointInsideTolerance);
        EnsureTriggerMode(trigger1.trigger);
        EnsureTriggerMode(trigger2.trigger);
    }

    internal void HandleEndpointTriggerEnter(int endpointIndex, Collider other)
    {
        Transform travelerRoot = ResolveTravelerRoot(other);
        if (travelerRoot == null)
        {
            return;
        }

        RegisterOverlap(endpointIndex, travelerRoot, other);
        TryTeleport(endpointIndex, travelerRoot);
    }

    internal void HandleEndpointTriggerStay(int endpointIndex, Collider other)
    {
        Transform travelerRoot = ResolveTravelerRoot(other);
        if (travelerRoot == null)
        {
            return;
        }

        RegisterOverlap(endpointIndex, travelerRoot, other);
        TryTeleport(endpointIndex, travelerRoot);
    }

    internal void HandleEndpointTriggerExit(int endpointIndex, Collider other)
    {
        Transform travelerRoot = ResolveTravelerRoot(other);
        if (travelerRoot == null)
        {
            return;
        }

        UnregisterOverlap(endpointIndex, travelerRoot, other);
    }

    private void TryTeleport(int sourceEndpointIndex, Transform travelerRoot)
    {
        if (!isActiveAndEnabled || travelerRoot == null)
        {
            return;
        }

        if (sourceEndpointIndex == 1 && !bidirectional)
        {
            return;
        }

        TeleportEndpoint sourceEndpoint = GetEndpoint(sourceEndpointIndex);
        int destinationEndpointIndex = GetDestinationEndpointIndex(sourceEndpointIndex);
        TeleportEndpoint destinationEndpoint = GetEndpoint(destinationEndpointIndex);
        if (sourceEndpoint.trigger == null || destinationEndpoint.trigger == null)
        {
            return;
        }

        if (teleportCooldownUntilByTraveler.TryGetValue(travelerRoot, out float cooldownUntil)
            && Time.unscaledTime < cooldownUntil)
        {
            return;
        }

        if (blockedEndpointByTraveler.TryGetValue(travelerRoot, out int blockedEndpoint)
            && blockedEndpoint == sourceEndpointIndex)
        {
            return;
        }

        ResolveDestinationPose(destinationEndpoint, out Vector3 destinationPosition, out Quaternion destinationRotation);
        if (!TeleportTraveler(travelerRoot, destinationPosition, destinationRotation))
        {
            return;
        }

        teleportCooldownUntilByTraveler[travelerRoot] = Time.unscaledTime + cooldownSeconds;

        if (DoesTravelerTouchTrigger(destinationEndpoint.trigger, travelerRoot))
        {
            blockedEndpointByTraveler[travelerRoot] = destinationEndpointIndex;
        }
        else
        {
            blockedEndpointByTraveler.Remove(travelerRoot);
        }
    }

    private bool TeleportTraveler(Transform travelerRoot, Vector3 destinationPosition, Quaternion destinationRotation)
    {
        SquadCharacterController squadController = travelerRoot.GetComponent<SquadCharacterController>();
        if (squadController == null)
        {
            squadController = travelerRoot.GetComponentInChildren<SquadCharacterController>(true);
        }

        if (squadController == null ||
            !squadController.TrySetUccExternalPositionAndRotation(destinationPosition, destinationRotation, stopActiveAbilities: true))
        {
            return false;
        }

        AudioManager.EnsureInstance()?.PlayActionCue(ActionAudioCue.Teleport, destinationPosition);
        if (stopSquadCharacterAfterTeleport)
        {
            squadController.Stop();
        }

        return true;
    }

    private bool IsPointInsideTrigger(Collider trigger, Vector3 point)
    {
        Vector3 closestPoint = trigger.ClosestPoint(point);
        return (closestPoint - point).sqrMagnitude <= pointInsideTolerance * pointInsideTolerance;
    }

    private bool DoesTravelerTouchTrigger(Collider trigger, Transform travelerRoot)
    {
        if (trigger == null || travelerRoot == null)
        {
            return false;
        }

        Collider[] colliders = travelerRoot.GetComponentsInChildren<Collider>(true);
        Bounds triggerBounds = trigger.bounds;
        bool foundCollider = false;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider travelerCollider = colliders[i];
            if (travelerCollider == null || travelerCollider.isTrigger || !travelerCollider.enabled)
            {
                continue;
            }

            foundCollider = true;
            Bounds travelerBounds = travelerCollider.bounds;
            if (triggerBounds.Intersects(travelerBounds) || triggerBounds.Contains(travelerBounds.center))
            {
                return true;
            }
        }

        return !foundCollider && triggerBounds.Contains(travelerRoot.position);
    }

    private void ResolveDestinationPose(TeleportEndpoint endpoint, out Vector3 position, out Quaternion rotation)
    {
        if (endpoint.arrivalPoint != null)
        {
            position = endpoint.arrivalPoint.position;
            rotation = ResolveArrivalRotation(endpoint.arrivalPoint);
            return;
        }

        Transform triggerTransform = endpoint.trigger != null ? endpoint.trigger.transform : transform;
        position = triggerTransform.TransformPoint(endpoint.fallbackArrivalOffset);
        rotation = ResolveArrivalRotation(triggerTransform);
    }

    private static Quaternion ResolveArrivalRotation(Transform reference)
    {
        if (reference == null)
        {
            return Quaternion.identity;
        }

        Vector3 worldForward = reference.TransformDirection(Vector3.forward);
        Vector3 worldUp = reference.TransformDirection(Vector3.up);
        if (worldForward.sqrMagnitude <= 0.0001f || worldUp.sqrMagnitude <= 0.0001f)
        {
            return reference.rotation;
        }

        return Quaternion.LookRotation(worldForward.normalized, worldUp.normalized);
    }

    private void RegisterOverlap(int endpointIndex, Transform travelerRoot, Collider other)
    {
        EndpointRuntimeState state = endpointStates[endpointIndex];
        if (!state.overlapColliderIdsByTraveler.TryGetValue(travelerRoot, out HashSet<int> overlapColliderIds))
        {
            overlapColliderIds = new HashSet<int>();
            state.overlapColliderIdsByTraveler.Add(travelerRoot, overlapColliderIds);
        }

        overlapColliderIds.Add(other.GetInstanceID());
    }

    private void UnregisterOverlap(int endpointIndex, Transform travelerRoot, Collider other)
    {
        EndpointRuntimeState state = endpointStates[endpointIndex];
        if (!state.overlapColliderIdsByTraveler.TryGetValue(travelerRoot, out HashSet<int> overlapColliderIds))
        {
            return;
        }

        overlapColliderIds.Remove(other.GetInstanceID());
        if (overlapColliderIds.Count > 0)
        {
            return;
        }

        state.overlapColliderIdsByTraveler.Remove(travelerRoot);
        if (blockedEndpointByTraveler.TryGetValue(travelerRoot, out int blockedEndpoint)
            && blockedEndpoint == endpointIndex)
        {
            blockedEndpointByTraveler.Remove(travelerRoot);
        }
    }

    private void BindEndpointRelay(int endpointIndex)
    {
        TeleportEndpoint endpoint = GetEndpoint(endpointIndex);
        if (endpoint.trigger == null)
        {
            return;
        }

        EnsureTriggerMode(endpoint.trigger);

        TriggerPairTeleporterEndpoint relay = endpoint.trigger.GetComponent<TriggerPairTeleporterEndpoint>();
        if (relay == null)
        {
            relay = endpoint.trigger.gameObject.AddComponent<TriggerPairTeleporterEndpoint>();
        }

        relay.Configure(this, endpointIndex);
    }

    private void EnsureTriggerMode(Collider colliderTarget)
    {
        if (colliderTarget != null)
        {
            colliderTarget.isTrigger = true;
        }
    }

    private void ValidateConfiguration()
    {
        if (!logConfigurationWarnings)
        {
            return;
        }

        if (trigger1.trigger == null || trigger2.trigger == null)
        {
            Debug.LogWarning("TriggerPairTeleporter: il faut renseigner les deux colliders Trigger.", this);
        }

        if (trigger1.trigger != null && trigger1.trigger == trigger2.trigger)
        {
            Debug.LogWarning("TriggerPairTeleporter: Trigger 1 et Trigger 2 ne doivent pas pointer vers le meme collider.", this);
        }
    }

    private static Transform ResolveTravelerRoot(Collider other)
    {
        if (other == null || other.isTrigger)
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

        Animator animator = other.GetComponentInParent<Animator>();
        if (animator != null)
        {
            return animator.transform;
        }

        if (other.attachedRigidbody != null)
        {
            return other.attachedRigidbody.transform;
        }

        return other.transform.root;
    }

    private TeleportEndpoint GetEndpoint(int endpointIndex)
    {
        return endpointIndex == 0 ? trigger1 : trigger2;
    }

    private static int GetDestinationEndpointIndex(int sourceEndpointIndex)
    {
        return sourceEndpointIndex == 0 ? 1 : 0;
    }
}

[AddComponentMenu("")]
[DisallowMultipleComponent]
public sealed class TriggerPairTeleporterEndpoint : MonoBehaviour
{
    [SerializeField, HideInInspector] private TriggerPairTeleporter owner;
    [SerializeField, HideInInspector] private int endpointIndex;

    public void Configure(TriggerPairTeleporter owner, int endpointIndex)
    {
        this.owner = owner;
        this.endpointIndex = endpointIndex;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (owner != null)
        {
            owner.HandleEndpointTriggerEnter(endpointIndex, other);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (owner != null)
        {
            owner.HandleEndpointTriggerStay(endpointIndex, other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (owner != null)
        {
            owner.HandleEndpointTriggerExit(endpointIndex, other);
        }
    }
}
