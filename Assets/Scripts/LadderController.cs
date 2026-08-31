using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Passage instantane bidirectionnel entre les extremites d'un VFX_Ladder.</summary>
[DisallowMultipleComponent]
public sealed class LadderController : MonoBehaviour, ICharacterDetectedInteractable, ILocalInteractHandler
{
    [Header("Extremites")]
    [Tooltip("Resolue automatiquement parmi les enfants nommes Leafs_1.")]
    public Transform leafs1;
    [Tooltip("Resolue automatiquement parmi les enfants nommes Leafs_2.")]
    public Transform leafs2;
    [Min(0.1f)] public float activationRadius = 1.2f;
    public Vector3 destinationLocalOffset = Vector3.zero;
    public bool useDestinationRotation = true;

    [Header("Interaction")]
    [Tooltip("Collider de detection. Un BoxCollider Trigger temporaire est cree autour des deux Leafs si vide.")]
    public Collider interactionCollider;
    [Min(0.1f), Tooltip("Distance maximale prise en compte par la detection d'interaction.")]
    public float interactionMaxDistance = 1.5f;
    public int interactionPriority = 115;
    public string climbInteractionText = "Monter";
    public string descendInteractionText = "Descendre";

    [Header("UI monde")]
    public bool showInteractionUi = true;
    public GameObject interactionBox;
    public Transform interactionBoxParent;
    public Vector3 interactionUiOffset = new Vector3(0f, 1.6f, 0f);

    [Header("Presentation")]
    [Tooltip("Trigger de Player_Model. Le clip associe doit etre in-place.")]
    public string animationTrigger = "LadderPassageTrigger";
    [Min(0f)] public float teleportDelay = 0.35f;
    [Min(0f)] public float reuseCooldown = 0.35f;
    public bool playTeleportAudio = true;

    [Header("VFX de teleportation")]
    [Tooltip("Prefab instancie a la position du joueur au debut de la disparition.")]
    public GameObject disappearVfxPrefab;
    [Min(0f), Tooltip("Delai avant l'instanciation du VFX de disparition.")]
    public float disappearVfxDelay;
    [Tooltip("Prefab instancie a la position d'arrivee apres le repositionnement UCC.")]
    public GameObject appearVfxPrefab;
    [Min(0f), Tooltip("Delai avant l'instanciation du VFX de reapparition.")]
    public float appearVfxDelay;
    [Min(0f)] public float teleportVfxLifetime = 2.5f;

    [Header("Validation")]
    public bool logConfigurationWarnings = true;
    public bool drawDebugGizmos = true;

    private readonly Dictionary<Transform, PassageRuntime> activePassages = new Dictionary<Transform, PassageRuntime>();
    private readonly Dictionary<Transform, float> cooldownUntil = new Dictionary<Transform, float>();
    private readonly Dictionary<Transform, int> blockedEndpointUntilExit = new Dictionary<Transform, int>();
    private uint netcodeId;
    private bool awaitingServerResponse;
    private GameObject detectedCharacter;
    private Collider resolvedInteractionCollider;
    private GameObject interactionBoxInstance;
    private Canvas interactionCanvas;

    private sealed class PassageRuntime
    {
        public Coroutine routine;
        public LitOpsiveLocomotionBridge bridge;
        public bool ownsExternalLock;
        public bool ownsRootMotionMode;
    }

    private void Reset() => ResolveEndpoints();

    private void Awake()
    {
        ResolveEndpoints();
        EnsureInteractionCollider(createFallback: Application.isPlaying);
        netcodeId = NetcodeSceneIdUtility.GetStableId(transform);
        ValidateConfiguration();
    }

    private void OnEnable() => NetcodeTriggerRegistry.Register(this, netcodeId);

    private void OnDisable()
    {
        NetcodeTriggerRegistry.Unregister(this, netcodeId);
        awaitingServerResponse = false;
        detectedCharacter = null;
        DestroyInteractionInstance();
        StopAllPassages();
        cooldownUntil.Clear();
        blockedEndpointUntilExit.Clear();
    }

    private void Update()
    {
        GameObject character = LocalPlayerUtils.GetControlledCharacter();
        if (TryResolveController(character, out SquadCharacterController controller))
            UpdateEndpointExitBlock(controller.transform);
    }

    private void LateUpdate() => UpdateInteractionUiPosition();

    private void OnValidate()
    {
        activationRadius = Mathf.Max(0.1f, activationRadius);
        teleportDelay = Mathf.Max(0f, teleportDelay);
        reuseCooldown = Mathf.Max(0f, reuseCooldown);
        teleportVfxLifetime = Mathf.Max(0f, teleportVfxLifetime);
        disappearVfxDelay = Mathf.Max(0f, disappearVfxDelay);
        appearVfxDelay = Mathf.Max(0f, appearVfxDelay);
        interactionMaxDistance = Mathf.Max(0.1f, interactionMaxDistance);
        ResolveEndpoints();
    }

    public bool CanBeDetectedBy(SquadCharacterController controller)
    {
        return controller != null && !awaitingServerResponse && !activePassages.ContainsKey(controller.transform) &&
               CanStartPassage(controller.gameObject, controller, out _);
    }

    public Collider GetInteractionDetectionCollider()
    {
        EnsureInteractionCollider(createFallback: Application.isPlaying);
        return resolvedInteractionCollider;
    }

    public Transform GetInteractionAnchor()
    {
        if (TryResolveController(detectedCharacter, out SquadCharacterController controller) &&
            CanStartPassage(detectedCharacter, controller, out int sourceEndpoint))
            return GetEndpoint(sourceEndpoint);
        return leafs1 != null ? leafs1 : transform;
    }

    public float GetInteractionMaxDistance(SquadCharacterController controller) => Mathf.Max(0.1f, interactionMaxDistance);
    public int GetInteractionPriority(SquadCharacterController controller) => interactionPriority;

    public void SetDetectedCharacter(GameObject character)
    {
        if (character != null && (!TryResolveController(character, out SquadCharacterController controller) || !CanBeDetectedBy(controller)))
            character = null;
        if (detectedCharacter == character) return;
        detectedCharacter = character;
        ShowInteraction(detectedCharacter != null && showInteractionUi);
    }

    public bool TryHandleLocalInteract()
    {
        if (!isActiveAndEnabled || detectedCharacter == null || awaitingServerResponse) return false;
        if (InputFocusStack.HasAnyFocus() || (SquadManager.Instance != null && SquadManager.Instance.IsInputLocked())) return true;
        if (!TryResolveController(detectedCharacter, out SquadCharacterController controller) ||
            !CanStartPassage(detectedCharacter, controller, out int sourceEndpoint))
        {
            SetDetectedCharacter(null);
            return true;
        }

        RequestPassage(detectedCharacter, sourceEndpoint);
        return true;
    }

    private void RequestPassage(GameObject character, int sourceEndpoint)
    {
        if (IsNetworked() && !NetworkManager.Singleton.IsServer)
        {
            WorldInteractionService service = WorldInteractionService.Instance;
            if (service == null) return;
            awaitingServerResponse = true;
            service.RequestLadderPassageServerRpc(netcodeId, sourceEndpoint);
            return;
        }

        if (ServerTryBeginPassage(character, sourceEndpoint, out Vector3 destination, out Quaternion rotation))
            BeginLocalPassage(character, sourceEndpoint, destination, rotation);
    }

    public bool ServerTryBeginPassage(GameObject character, int sourceEndpoint, out Vector3 destination, out Quaternion rotation)
    {
        destination = Vector3.zero;
        rotation = Quaternion.identity;
        if (!TryResolveController(character, out SquadCharacterController controller) ||
            !CanStartPassage(character, controller, out int resolvedEndpoint) || resolvedEndpoint != sourceEndpoint) return false;

        Transform destinationEndpoint = GetEndpoint(1 - sourceEndpoint);
        if (destinationEndpoint == null) return false;
        destination = destinationEndpoint.TransformPoint(destinationLocalOffset);
        rotation = useDestinationRotation ? destinationEndpoint.rotation : controller.transform.rotation;
        RegisterCooldown(controller.transform);
        return true;
    }

    public void HandlePassageResult(bool success, int sourceEndpoint, Vector3 destination, Quaternion rotation)
    {
        awaitingServerResponse = false;
        if (!success) return;
        GameObject character = LocalPlayerUtils.GetControlledCharacter();
        if (character != null) BeginLocalPassage(character, sourceEndpoint, destination, rotation);
    }

    private void BeginLocalPassage(GameObject character, int sourceEndpoint, Vector3 destination, Quaternion rotation)
    {
        if (!TryResolveController(character, out SquadCharacterController controller) || activePassages.ContainsKey(controller.transform)) return;
        LitOpsiveLocomotionBridge bridge = controller.GetComponent<LitOpsiveLocomotionBridge>();
        if (bridge == null || !bridge.BeginExternalLock(disableGameplayInput: true, stopActiveAbilities: true)) return;

        PassageRuntime runtime = new PassageRuntime { bridge = bridge, ownsExternalLock = true };
        activePassages.Add(controller.transform, runtime);
        bridge.SetPlayerActionRootMotionMode(PlayerActionRootMotionMode.InPlace, suppressRootRotation: true);
        runtime.ownsRootMotionMode = true;

        Animator animator = controller.GetComponent<Animator>() ?? controller.GetComponentInChildren<Animator>(true);
        ScheduleTeleportVfx(disappearVfxPrefab, controller.transform.position, controller.transform.rotation, disappearVfxDelay);
        if (animator != null && !string.IsNullOrWhiteSpace(animationTrigger))
        {
            animator.ResetTrigger(animationTrigger);
            animator.SetTrigger(animationTrigger);
        }

        runtime.routine = StartCoroutine(CompletePassageRoutine(controller, sourceEndpoint, destination, rotation, runtime));
    }

    private IEnumerator CompletePassageRoutine(SquadCharacterController controller, int sourceEndpoint, Vector3 destination, Quaternion rotation, PassageRuntime runtime)
    {
        if (teleportDelay > 0f) yield return new WaitForSeconds(teleportDelay);
        // This UCC route remains available while the external input lock owns
        // the character, unlike the ordinary movement-driver guarded route.
        bool teleported = controller != null && runtime.bridge != null &&
                          runtime.bridge.SetCinematicPositionAndRotation(
                              destination,
                              rotation,
                              stopActiveAbilities: true,
                              logDiagnostics: false);
        if (teleported)
        {
            controller.Stop();
            Physics.SyncTransforms();
            ScheduleTeleportVfx(appearVfxPrefab, destination, rotation, appearVfxDelay);
            if (playTeleportAudio) AudioManager.EnsureInstance()?.PlayActionCue(ActionAudioCue.Teleport, destination);
            RegisterCooldown(controller.transform);
            blockedEndpointUntilExit[controller.transform] = 1 - sourceEndpoint;
        }
        else if (logConfigurationWarnings)
        {
            Debug.LogWarning($"[LadderController] Teleport refused for '{name}': UCC locomotion unavailable.", this);
        }

        EndPassage(controller != null ? controller.transform : null, runtime);
    }

    private bool CanStartPassage(GameObject character, SquadCharacterController controller, out int sourceEndpoint)
    {
        sourceEndpoint = -1;
        if (character == null || controller == null || !isActiveAndEnabled || !HasValidEndpoints() ||
            controller.CurrentHp <= 0 || controller.IsMovementInputSuppressed || controller.IsFlightActive) return false;

        LitOpsiveLocomotionBridge bridge = controller.GetComponent<LitOpsiveLocomotionBridge>();
        if (bridge == null || !bridge.IsDriving || bridge.IsInputSuppressedByUcc || !bridge.Grounded) return false;
        PlayerScriptedJumpController jump = controller.GetComponent<PlayerScriptedJumpController>();
        if (jump != null && jump.IsActive) return false;
        RealTimeCombatManager combat = RealTimeCombatManager.Instance;
        if (combat != null && (combat.IsCombatActive || combat.IsPlayerActionActive || combat.IsCinematicSequenceActive)) return false;

        Transform traveler = controller.transform;
        if (IsOnCooldown(traveler)) return false;
        float radiusSquared = activationRadius * activationRadius;
        float distance1 = (traveler.position - leafs1.position).sqrMagnitude;
        float distance2 = (traveler.position - leafs2.position).sqrMagnitude;
        if (distance1 > radiusSquared && distance2 > radiusSquared) return false;
        sourceEndpoint = distance1 <= distance2 ? 0 : 1;
        return !blockedEndpointUntilExit.TryGetValue(traveler, out int blockedEndpoint) || blockedEndpoint != sourceEndpoint;
    }

    private void UpdateEndpointExitBlock(Transform traveler)
    {
        if (traveler == null || !blockedEndpointUntilExit.TryGetValue(traveler, out int endpoint)) return;
        Transform point = GetEndpoint(endpoint);
        if (point == null || (traveler.position - point.position).sqrMagnitude > activationRadius * activationRadius)
            blockedEndpointUntilExit.Remove(traveler);
    }

    private void EndPassage(Transform traveler, PassageRuntime runtime)
    {
        if (runtime == null) return;
        if (runtime.ownsRootMotionMode && runtime.bridge != null) runtime.bridge.ClearPlayerActionRootMotionMode();
        if (runtime.ownsExternalLock && runtime.bridge != null) runtime.bridge.EndExternalLock();
        if (traveler != null && activePassages.TryGetValue(traveler, out PassageRuntime active) && active == runtime) activePassages.Remove(traveler);
    }

    private void StopAllPassages()
    {
        List<KeyValuePair<Transform, PassageRuntime>> passages = new List<KeyValuePair<Transform, PassageRuntime>>(activePassages);
        activePassages.Clear();
        foreach (KeyValuePair<Transform, PassageRuntime> passage in passages)
        {
            PassageRuntime runtime = passage.Value;
            if (runtime != null && runtime.routine != null) StopCoroutine(runtime.routine);
            if (runtime != null && runtime.ownsRootMotionMode && runtime.bridge != null) runtime.bridge.ClearPlayerActionRootMotionMode();
            if (runtime != null && runtime.ownsExternalLock && runtime.bridge != null) runtime.bridge.EndExternalLock();
        }
    }

    private void EnsureInteractionCollider(bool createFallback)
    {
        if (resolvedInteractionCollider != null)
        {
            return;
        }

        resolvedInteractionCollider = interactionCollider;
        if (resolvedInteractionCollider != null || !createFallback || !HasValidEndpoints())
        {
            return;
        }

        BoxCollider fallback = GetComponent<BoxCollider>();
        if (fallback == null)
        {
            fallback = gameObject.AddComponent<BoxCollider>();
            fallback.hideFlags = HideFlags.DontSave;
        }

        Vector3 leafs1Local = transform.InverseTransformPoint(leafs1.position);
        Vector3 leafs2Local = transform.InverseTransformPoint(leafs2.position);
        Vector3 padding = Vector3.one * Mathf.Max(activationRadius, interactionMaxDistance) * 2f;
        fallback.center = (leafs1Local + leafs2Local) * 0.5f;
        fallback.size = Vector3.Max(Vector3.one * 0.2f, new Vector3(
            Mathf.Abs(leafs2Local.x - leafs1Local.x),
            Mathf.Abs(leafs2Local.y - leafs1Local.y),
            Mathf.Abs(leafs2Local.z - leafs1Local.z)) + padding);
        fallback.isTrigger = true;
        interactionCollider = fallback;
        resolvedInteractionCollider = fallback;
    }

    private void ShowInteraction(bool show)
    {
        if (!show)
        {
            DestroyInteractionInstance();
            return;
        }

        if (interactionBoxInstance == null)
        {
            interactionBoxInstance = interactionBox != null
                ? (interactionBoxParent != null ? Instantiate(interactionBox, interactionBoxParent) : Instantiate(interactionBox))
                : CreateFallbackInteractionBox();
            if (interactionBoxInstance != null)
            {
                interactionCanvas = interactionBoxInstance.GetComponentInParent<Canvas>();
            }
        }

        ApplyInteractionText();
        if (interactionBoxInstance != null)
        {
            interactionBoxInstance.SetActive(true);
        }
    }

    private void ApplyInteractionText()
    {
        if (interactionBoxInstance == null)
        {
            return;
        }

        string label = ResolveInteractionText();
        TMP_Text tmp = interactionBoxInstance.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
        {
            tmp.text = label;
            return;
        }

        Text fallback = interactionBoxInstance.GetComponentInChildren<Text>(true);
        if (fallback != null)
        {
            fallback.text = label;
        }
    }

    private string ResolveInteractionText()
    {
        if (TryResolveController(detectedCharacter, out SquadCharacterController controller) &&
            CanStartPassage(detectedCharacter, controller, out int sourceEndpoint))
        {
            return sourceEndpoint == 0 ? climbInteractionText : descendInteractionText;
        }

        return climbInteractionText;
    }

    private void UpdateInteractionUiPosition()
    {
        if (interactionBoxInstance == null || !interactionBoxInstance.activeSelf)
        {
            return;
        }

        Camera camera = Camera.main;
        Transform anchor = GetInteractionAnchor();
        if (camera == null || anchor == null)
        {
            return;
        }

        Vector3 worldPosition = anchor.position + interactionUiOffset;
        Canvas canvas = interactionCanvas != null ? interactionCanvas : interactionBoxInstance.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
        {
            RectTransform rect = interactionBoxInstance.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            Vector3 screenPosition = camera.WorldToScreenPoint(worldPosition);
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                rect.position = screenPosition;
                return;
            }

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            Camera uiCamera = canvas.worldCamera != null ? canvas.worldCamera : camera;
            if (canvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, uiCamera, out Vector2 localPoint))
            {
                rect.localPosition = localPoint;
            }

            return;
        }

        interactionBoxInstance.transform.position = worldPosition;
        Vector3 toCamera = interactionBoxInstance.transform.position - camera.transform.position;
        if (toCamera.sqrMagnitude > 0.0001f)
        {
            interactionBoxInstance.transform.rotation = Quaternion.LookRotation(toCamera);
        }
    }

    private GameObject CreateFallbackInteractionBox()
    {
        GameObject instance = new GameObject("LadderInteractionBox", typeof(RectTransform), typeof(Canvas), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(GraphicRaycaster));
        if (interactionBoxParent != null)
        {
            instance.transform.SetParent(interactionBoxParent, false);
        }

        RectTransform rect = instance.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(220f, 50f);
        rect.localScale = Vector3.one * 0.03f;
        Canvas canvas = instance.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;

        TextMeshProUGUI label = instance.GetComponent<TextMeshProUGUI>();
        label.fontSize = 18f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        return instance;
    }

    private void DestroyInteractionInstance()
    {
        if (interactionBoxInstance != null)
        {
            Destroy(interactionBoxInstance);
            interactionBoxInstance = null;
            interactionCanvas = null;
        }
    }

    private void SpawnTeleportVfx(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            return;
        }

        GameObject instance = Instantiate(prefab, position, rotation);
        if (teleportVfxLifetime > 0f)
        {
            Destroy(instance, teleportVfxLifetime);
        }
    }

    private void ScheduleTeleportVfx(GameObject prefab, Vector3 position, Quaternion rotation, float delay)
    {
        if (prefab == null)
        {
            return;
        }

        if (delay <= 0f)
        {
            SpawnTeleportVfx(prefab, position, rotation);
            return;
        }

        StartCoroutine(SpawnTeleportVfxAfterDelay(prefab, position, rotation, delay));
    }

    private IEnumerator SpawnTeleportVfxAfterDelay(GameObject prefab, Vector3 position, Quaternion rotation, float delay)
    {
        yield return new WaitForSeconds(delay);
        SpawnTeleportVfx(prefab, position, rotation);
    }

    private void ResolveEndpoints()
    {
        if (leafs1 == null) leafs1 = FindChildRecursively(transform, "Leafs_1");
        if (leafs2 == null) leafs2 = FindChildRecursively(transform, "Leafs_2");
    }

    private static Transform FindChildRecursively(Transform root, string childName)
    {
        if (root == null) return null;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child != root && string.Equals(child.name, childName, StringComparison.Ordinal)) return child;
        return null;
    }

    private bool HasValidEndpoints() => leafs1 != null && leafs2 != null && leafs1 != leafs2;
    private Transform GetEndpoint(int endpointIndex) => endpointIndex == 0 ? leafs1 : leafs2;
    private bool IsOnCooldown(Transform traveler) => traveler != null && cooldownUntil.TryGetValue(traveler, out float until) && Time.unscaledTime < until;
    private void RegisterCooldown(Transform traveler) { if (traveler != null) cooldownUntil[traveler] = Time.unscaledTime + reuseCooldown; }

    private static bool TryResolveController(GameObject character, out SquadCharacterController controller)
    {
        controller = character != null ? character.GetComponent<SquadCharacterController>() : null;
        if (controller == null && character != null) controller = character.GetComponentInParent<SquadCharacterController>();
        if (controller == null && character != null) controller = character.GetComponentInChildren<SquadCharacterController>(true);
        return controller != null;
    }

    private static bool IsNetworked() => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    private void ValidateConfiguration()
    {
        if (logConfigurationWarnings && !HasValidEndpoints())
            Debug.LogWarning($"[LadderController] '{name}' needs distinct Leafs_1 and Leafs_2 children or explicit references.", this);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos) return;
        DrawEndpointGizmo(leafs1, leafs2, new Color(0.2f, 0.8f, 1f, 0.8f));
        DrawEndpointGizmo(leafs2, leafs1, new Color(1f, 0.65f, 0.2f, 0.8f));
    }

    private void DrawEndpointGizmo(Transform source, Transform destination, Color color)
    {
        if (source == null) return;
        Gizmos.color = color;
        Gizmos.DrawWireSphere(source.position, activationRadius);
        if (destination != null) Gizmos.DrawLine(source.position, destination.TransformPoint(destinationLocalOffset));
    }
}
