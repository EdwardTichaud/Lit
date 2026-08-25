using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

// Portail interactif sans UI : la detection standard affiche uniquement l'outline,
// puis l'action Interagir teleporte le personnage vers le point configure.
[DisallowMultipleComponent]
public sealed class PortalController : MonoBehaviour, ICharacterDetectedInteractable, ILocalInteractHandler
{
    public enum DestinationMode
    {
        LocalTeleport,
        SceneTransition
    }

    [Header("Destination")]
    [SerializeField, Tooltip("Choisit une teleportation dans la scene ou un changement de zone.")]
    private DestinationMode destinationMode = DestinationMode.LocalTeleport;
    [SerializeField, Tooltip("Points d'arrivee locaux. L'index 0 est toujours reserve au joueur principal, puis les autres joueurs suivent leur ordre d'apparition.")]
    private List<Transform> destinationPoints = new List<Transform>();
    [FormerlySerializedAs("destinationPoint")]
    [SerializeField, HideInInspector]
    private Transform legacyDestinationPoint;
    [SerializeField, Tooltip("Offset local applique par rapport au point d'arrivee.")]
    private Vector3 destinationLocalOffset;
    [SerializeField, Tooltip("Applique la rotation du point d'arrivee au personnage.")]
    private bool useDestinationRotation = true;

    [Header("Scene Transition")]
    [SerializeField, Tooltip("Zone chargee quand ce portail est configure en Scene Transition.")]
    private ZoneManifest destinationZone;

    [Header("Interaction")]
    [SerializeField, Tooltip("Collider utilise pour detecter et mesurer l'interaction. Un SphereCollider Trigger est cree au runtime s'il manque.")]
    private Collider interactionCollider;
    [SerializeField, Tooltip("Point utilise pour classer le portail parmi les interactions proches.")]
    private Transform interactionAnchor;
    [SerializeField, Min(0.1f), Tooltip("Distance maximale d'utilisation du portail.")]
    private float interactionMaxDistance = 2.25f;
    [SerializeField, Tooltip("Priorite de selection si plusieurs objets interactifs sont proches.")]
    private int interactionPriority = 110;

    [Header("Fallback Collider")]
    [SerializeField, Tooltip("Cree une zone de detection non bloquante si le portail ne possede aucun Collider.")]
    private bool createTriggerColliderIfMissing = true;
    [SerializeField, Min(0.1f)] private float fallbackColliderRadius = 1.25f;
    [SerializeField] private Vector3 fallbackColliderCenter = new Vector3(0f, 1f, 0f);

    [Header("Teleportation")]
    [SerializeField, Min(0f), Tooltip("Evite les activations repetees immediates.")]
    private float reuseCooldown = 0.35f;
    [SerializeField, Tooltip("Arrete le mouvement du personnage apres la teleportation.")]
    private bool stopCharacterAfterTeleport = true;
    [SerializeField, Tooltip("Joue le son d'action Teleport apres une utilisation reussie.")]
    private bool playTeleportAudio = true;
    [SerializeField, Tooltip("Autorise un deplacement direct du Transform uniquement hors reseau si UCC est indisponible.")]
    private bool allowStandaloneTransformFallback;

    [Header("Validation")]
    [SerializeField, Tooltip("Affiche un avertissement si la destination ou le collider est manquant.")]
    private bool logConfigurationWarnings = true;

    private readonly Dictionary<Transform, float> cooldownUntilByCharacter = new Dictionary<Transform, float>();

    private Collider resolvedInteractionCollider;
    private GameObject detectedCharacter;
    private uint netcodeId;
    private bool awaitingServerResponse;

    public IReadOnlyList<Transform> DestinationPoints => destinationPoints;
    public bool IsSceneTransition => destinationMode == DestinationMode.SceneTransition;

    private void Reset()
    {
        EnsureDestinationPoints();
        ResolveReferences(createFallback: false);
        RuntimeOutlineUtility.EnsureOutlineTargets(gameObject);
    }

    private void Awake()
    {
        EnsureDestinationPoints();
        ResolveReferences(createFallback: true);
        RuntimeOutlineUtility.EnsureOutlineTargets(gameObject);
        netcodeId = NetcodeSceneIdUtility.GetStableId(transform);
        ValidateConfiguration();
    }

    private void OnEnable()
    {
        NetcodeTriggerRegistry.Register(this, netcodeId);
    }

    private void OnDisable()
    {
        NetcodeTriggerRegistry.Unregister(this, netcodeId);
        detectedCharacter = null;
        awaitingServerResponse = false;
        cooldownUntilByCharacter.Clear();

        if (RuntimeOutlineSelectionManager.IsActiveInteractable(this))
        {
            RuntimeOutlineSelectionManager.Clear();
        }
    }

    private void OnValidate()
    {
        interactionMaxDistance = Mathf.Max(0.1f, interactionMaxDistance);
        fallbackColliderRadius = Mathf.Max(0.1f, fallbackColliderRadius);
        reuseCooldown = Mathf.Max(0f, reuseCooldown);
        EnsureDestinationPoints();
        ResolveReferences(createFallback: false);
    }

    public bool CanBeDetectedBy(SquadCharacterController controller)
    {
        return controller != null
            && isActiveAndEnabled
            && HasValidDestination()
            && GetInteractionDetectionCollider() != null;
    }

    public Collider GetInteractionDetectionCollider()
    {
        if (resolvedInteractionCollider == null)
        {
            ResolveReferences(createFallback: Application.isPlaying);
        }

        return resolvedInteractionCollider;
    }

    public Transform GetInteractionAnchor()
    {
        return interactionAnchor != null ? interactionAnchor : transform;
    }

    public float GetInteractionMaxDistance(SquadCharacterController controller)
    {
        return Mathf.Max(0.1f, interactionMaxDistance);
    }

    public int GetInteractionPriority(SquadCharacterController controller)
    {
        return interactionPriority;
    }

    public void SetDetectedCharacter(GameObject character)
    {
        detectedCharacter = character;
    }

    public bool TryHandleLocalInteract()
    {
        if (!isActiveAndEnabled || !HasValidDestination() || detectedCharacter == null)
        {
            return false;
        }

        if (InputFocusStack.HasAnyFocus() ||
            (SquadManager.Instance != null && SquadManager.Instance.IsInputLocked()))
        {
            return true;
        }

        GameObject character = detectedCharacter;
        if (!CanUse(character, requireLocalControl: true, rangePadding: 0f))
        {
            detectedCharacter = null;
            return true;
        }

        if (IsOnCooldown(ResolveCooldownKey(character)))
        {
            return true;
        }

        if (IsNetworked() && !NetworkManager.Singleton.IsServer)
        {
            if (awaitingServerResponse)
            {
                return true;
            }

            WorldInteractionService service = WorldInteractionService.Instance;
            if (service == null)
            {
                return true;
            }

            awaitingServerResponse = true;
            service.RequestPortalUseServerRpc(netcodeId);
            return true;
        }

        if (IsSceneTransition)
        {
            if (GameFlowService.TravelToZone(destinationZone, CaptureDestinationPoses()))
            {
                RegisterCooldown(ResolveCooldownKey(character));
                PlayTeleportAudio(character.transform.position);
            }

            return true;
        }

        if (TryTeleportAuthoritative(character, out Vector3 position, out _))
        {
            PlayTeleportAudio(position);
        }

        return true;
    }

    public bool ServerTryUse(GameObject character, out Vector3 destinationPosition, out Quaternion destinationRotation)
    {
        destinationPosition = Vector3.zero;
        destinationRotation = Quaternion.identity;

        if (!CanUse(character, requireLocalControl: false, rangePadding: 0.35f))
        {
            return false;
        }

        if (IsSceneTransition)
        {
            destinationPosition = character.transform.position;
            destinationRotation = character.transform.rotation;
            return GameFlowService.TravelToZone(destinationZone, CaptureDestinationPoses());
        }

        return TryTeleportAuthoritative(character, out destinationPosition, out destinationRotation);
    }

    public void HandlePortalUseResult(bool success, Vector3 destinationPosition, Quaternion destinationRotation, bool sceneTransition)
    {
        awaitingServerResponse = false;
        if (!success)
        {
            return;
        }

        if (sceneTransition)
        {
            PlayTeleportAudio(transform.position);
            return;
        }

        GameObject character = LocalPlayerUtils.GetControlledCharacter();
        if (character == null)
        {
            return;
        }

        if (ApplyTeleportPose(character, destinationPosition, destinationRotation))
        {
            RegisterCooldown(ResolveCooldownKey(character));
            PlayTeleportAudio(destinationPosition);
        }
    }

    private bool TryTeleportAuthoritative(
        GameObject character,
        out Vector3 destinationPosition,
        out Quaternion destinationRotation)
    {
        ResolveDestinationPose(character, out destinationPosition, out destinationRotation);
        if (!ApplyTeleportPose(character, destinationPosition, destinationRotation))
        {
            return false;
        }

        RegisterCooldown(ResolveCooldownKey(character));
        return true;
    }

    private bool CanUse(GameObject character, bool requireLocalControl, float rangePadding)
    {
        if (character == null || !HasValidDestination() || GetInteractionDetectionCollider() == null)
        {
            return false;
        }

        if (requireLocalControl && !IsControlledCharacter(character))
        {
            return false;
        }

        SquadCharacterController controller = ResolveSquadController(character);
        if (controller == null || IsOnCooldown(controller.transform))
        {
            return false;
        }

        return CharacterInteractionDetection.IsCharacterWithinRange(
            controller.transform,
            GetInteractionDetectionCollider(),
            GetInteractionAnchor(),
            interactionMaxDistance + Mathf.Max(0f, rangePadding));
    }

    private bool HasValidDestination()
    {
        return IsSceneTransition
            ? destinationZone != null && destinationZone.IsValid
            : GetDestinationPoint(null) != null;
    }

    private void ResolveDestinationPose(
        GameObject character,
        out Vector3 destinationPosition,
        out Quaternion destinationRotation)
    {
        Transform destinationPoint = GetDestinationPoint(character);
        destinationPosition = destinationPoint.TransformPoint(destinationLocalOffset);
        destinationRotation = useDestinationRotation
            ? destinationPoint.rotation
            : character.transform.rotation;
    }

    private Transform GetDestinationPoint(GameObject character)
    {
        EnsureDestinationPoints();
        if (destinationPoints == null || destinationPoints.Count == 0)
        {
            return null;
        }

        int requestedIndex = 0;
        if (character != null && SquadManager.Instance != null)
        {
            requestedIndex = SquadManager.Instance.GetPlayerSpawnIndex(character);
        }

        requestedIndex = Mathf.Clamp(requestedIndex, 0, destinationPoints.Count - 1);
        Transform point = destinationPoints[requestedIndex];
        if (point != null)
        {
            return point;
        }

        for (int i = 0; i < destinationPoints.Count; i++)
        {
            if (destinationPoints[i] != null)
            {
                return destinationPoints[i];
            }
        }

        return null;
    }

    private List<Pose> CaptureDestinationPoses()
    {
        EnsureDestinationPoints();
        List<Pose> poses = new List<Pose>(destinationPoints.Count);
        for (int i = 0; i < destinationPoints.Count; i++)
        {
            Transform point = destinationPoints[i];
            if (point == null)
            {
                continue;
            }

            poses.Add(new Pose(
                point.TransformPoint(destinationLocalOffset),
                useDestinationRotation ? point.rotation : Quaternion.identity));
        }

        return poses;
    }

    private void EnsureDestinationPoints()
    {
        if (destinationPoints == null)
        {
            destinationPoints = new List<Transform>();
        }

        if (destinationPoints.Count == 0 && legacyDestinationPoint != null)
        {
            destinationPoints.Add(legacyDestinationPoint);
        }
    }

    private bool ApplyTeleportPose(GameObject character, Vector3 position, Quaternion rotation)
    {
        SquadCharacterController controller = ResolveSquadController(character);
        if (controller == null)
        {
            return false;
        }

        bool teleported = controller.TrySetUccExternalPositionAndRotation(
            position,
            rotation,
            stopActiveAbilities: true);

        if (!teleported && allowStandaloneTransformFallback && !IsNetworked())
        {
            teleported = ApplyFallbackPose(controller.transform, position, rotation);
        }

        if (teleported && stopCharacterAfterTeleport)
        {
            controller.Stop();
        }

        if (teleported)
        {
            Physics.SyncTransforms();
        }

        return teleported;
    }

    private static bool ApplyFallbackPose(Transform characterRoot, Vector3 position, Quaternion rotation)
    {
        if (characterRoot == null)
        {
            return false;
        }

        CharacterController characterController = characterRoot.GetComponent<CharacterController>();
        bool characterControllerWasEnabled = characterController != null && characterController.enabled;
        if (characterControllerWasEnabled)
        {
            characterController.enabled = false;
        }

        characterRoot.SetPositionAndRotation(position, rotation);

        Rigidbody rigidbody = characterRoot.GetComponent<Rigidbody>();
        if (rigidbody != null && !rigidbody.isKinematic)
        {
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            rigidbody.position = position;
            rigidbody.rotation = rotation;
        }

        if (characterControllerWasEnabled)
        {
            characterController.enabled = true;
        }

        return true;
    }

    private void ResolveReferences(bool createFallback)
    {
        if (interactionAnchor == null)
        {
            interactionAnchor = transform;
        }

        if (interactionCollider != null)
        {
            resolvedInteractionCollider = interactionCollider;
            return;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider candidate = colliders[i];
            if (candidate != null && candidate.enabled && !candidate.isTrigger)
            {
                resolvedInteractionCollider = candidate;
                interactionCollider = candidate;
                return;
            }
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider candidate = colliders[i];
            if (candidate != null && candidate.enabled)
            {
                resolvedInteractionCollider = candidate;
                interactionCollider = candidate;
                return;
            }
        }

        if (resolvedInteractionCollider != null || !createFallback || !createTriggerColliderIfMissing)
        {
            return;
        }

        SphereCollider fallback = gameObject.AddComponent<SphereCollider>();
        fallback.isTrigger = true;
        fallback.center = fallbackColliderCenter;
        fallback.radius = fallbackColliderRadius;
        interactionCollider = fallback;
        resolvedInteractionCollider = fallback;
    }

    private void ValidateConfiguration()
    {
        if (!logConfigurationWarnings)
        {
            return;
        }

        if (!IsSceneTransition && GetDestinationPoint(null) == null)
        {
            Debug.LogWarning("PortalController: aucun Destination Point n'est renseigne.", this);
        }

        if (resolvedInteractionCollider == null)
        {
            Debug.LogWarning("PortalController: aucun Collider de detection n'est disponible.", this);
        }
    }

    private bool IsOnCooldown(Transform character)
    {
        if (character == null || reuseCooldown <= 0f)
        {
            return false;
        }

        return cooldownUntilByCharacter.TryGetValue(character, out float cooldownUntil)
            && Time.unscaledTime < cooldownUntil;
    }

    private void RegisterCooldown(Transform character)
    {
        if (character == null || reuseCooldown <= 0f)
        {
            return;
        }

        cooldownUntilByCharacter[character] = Time.unscaledTime + reuseCooldown;
    }

    private void PlayTeleportAudio(Vector3 position)
    {
        if (!playTeleportAudio)
        {
            return;
        }

        AudioManager.EnsureInstance()?.PlayActionCue(ActionAudioCue.Teleport, position);
    }

    private static SquadCharacterController ResolveSquadController(GameObject character)
    {
        if (character == null)
        {
            return null;
        }

        SquadCharacterController controller = character.GetComponent<SquadCharacterController>();
        if (controller == null)
        {
            controller = character.GetComponentInParent<SquadCharacterController>();
        }

        if (controller == null)
        {
            controller = character.GetComponentInChildren<SquadCharacterController>(true);
        }

        return controller;
    }

    private static Transform ResolveCooldownKey(GameObject character)
    {
        SquadCharacterController controller = ResolveSquadController(character);
        return controller != null ? controller.transform : character != null ? character.transform : null;
    }

    private static bool IsControlledCharacter(GameObject character)
    {
        GameObject controlled = LocalPlayerUtils.GetControlledCharacter();
        if (controlled == null || character == null)
        {
            return false;
        }

        Transform controlledTransform = controlled.transform;
        Transform characterTransform = character.transform;
        return controlledTransform == characterTransform
            || controlledTransform.IsChildOf(characterTransform)
            || characterTransform.IsChildOf(controlledTransform);
    }

    private static bool IsNetworked()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    }
}
