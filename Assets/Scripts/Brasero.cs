using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using Unity.Netcode;

// Brasero: source de lumiere qui peut etre allumée ou éteinte.
[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(NetworkObject))]
[DisallowMultipleComponent]
public class Brasero : NetworkBehaviour, ICharacterDetectedInteractable
{
    [Header("State")]
    [SerializeField, Tooltip("Etat du brasero au demarrage.")]
    private bool isLit = false;
    [SerializeField, Tooltip("Identifiant unique utilise pour la sauvegarde.")]
    private string braseroId;

    public bool IsLit => isLit;
    public string BraseroId => braseroId;
    public bool IsAncientBrasero => ancientBrasero;
    public IReadOnlyList<GameObject> CommonLightActivationOrder => commonLightActivationOrder;
    public Color FlameColor => LitFlameColorUtility.ResolveFlameColor(flameLight, flameObject, Color.white);

    public event Action<Brasero, bool> StateChanged;

    [Header("Visuals")]
    [SerializeField, Tooltip("GameObject enfant Flame active quand le brasero est allumé.")]
    private GameObject flameObject;
    [Tooltip("Lumiere de flamme optionnelle.")]
    public Light flameLight;
    [Tooltip("Objets actives quand le brasero est allumé.")]
    public GameObject[] activateWhenLitTargets = Array.Empty<GameObject>();

    [Header("Interaction")]
    [Tooltip("Legacy: les braseros ne doivent plus ecouter Interact. L'allumage passe par TriggerMunin.")]
    public bool useInteractInput = false;
    [SerializeField, FormerlySerializedAs("useToggleTorchInput"), Tooltip("Ecoute TriggerMunin quand le brasero est cible par le personnage local.")]
    private bool useTriggerMuninInput = true;
    [SerializeField, Tooltip("Affiche un dialogue d'etat avec Interact sans allumér/éteindre.")]
    private bool showStateDialogueOnInteract = true;
    [SerializeField, Tooltip("Message affiche avec Interact quand le brasero est allumé.")]
    private string litStateMessage = "Le brasero est allumé.";
    [SerializeField, Tooltip("Message affiche avec Interact quand le brasero est éteint.")]
    private string unlitStateMessage = "Le brasero est éteint.";
    [SerializeField, Min(0.05f), Tooltip("Distance maximale propre a Interact. TriggerMunin continue d'utiliser le collider trigger.")]
    private float interactMaxDistance = 1f;
    [SerializeField, Tooltip("Priorite de selection si plusieurs interactions sont proches.")]
    private int interactionPriority = 80;
    [Tooltip("Rayon du trigger d'interaction.")]
    public float interactionRadius = 2f;
    [Tooltip("Centre local du trigger d'interaction.")]
    public Vector3 interactionCenter = Vector3.zero;

    [SerializeField, Tooltip("Collider d'interaction (auto).")]
    private SphereCollider interactionTrigger;

    [Header("Influence")]
    [SerializeField, Tooltip("Zone d'information active seulement quand le brasero est allumé.")]
    private LitInfluenceSource litInfluence = new LitInfluenceSource(6f);
    [SerializeField, Tooltip("Ordre d allumage des lights communes taggees Light quand ce brasero les influence.")]
    private List<GameObject> commonLightActivationOrder = new List<GameObject>();

    [Header("Age")]
    [SerializeField, FormerlySerializedAs("drivesAgeManager"), Tooltip("Si actif, ce brasero allumé compte dans l'AgeManager comme brasero ancien.")]
    private bool ancientBrasero;

    [Header("Flame Emission")]
    [Tooltip("Duree du fondu d'emission (allumage/extinction).")]
    public float emissionFadeDuration = 1f;

    [Header("Munin")]
    [SerializeField, FormerlySerializedAs("muninTransform"), Tooltip("Controleur de Munin. Laisse vide pour utiliser celui du personnage detecte.")]
    private MuninController muninController;
    [SerializeField, Tooltip("Offset local depuis l'ancre du brasero pour la destination de Munin.")]
    private Vector3 muninTargetOffset = Vector3.zero;

    private readonly List<ParticleSystem> flameParticleSystems = new List<ParticleSystem>();
    private readonly Dictionary<ParticleSystem, EmissionBase> emissionBases = new Dictionary<ParticleSystem, EmissionBase>();
    private Coroutine emissionRoutine;
    private float currentEmissionFactor = -1f;
    private readonly List<GameObject> charactersInRange = new List<GameObject>();
    private readonly Dictionary<GameObject, int> characterColliderCounts = new Dictionary<GameObject, int>();
    private GameObject currentCharacter;
    private GameObject detectedCharacter;
    private Coroutine interactionRoutine;
    private bool interactionInProgress;
    private MuninController activeMuninController;
    private NetworkVariable<bool> netIsLit = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private void Reset()
    {
        interactionRadius = 2f;
        interactionCenter = Vector3.zero;
        EnsureInteractionTrigger();
        EnsureId();
    }

    private void Awake()
    {
        RuntimeOutlineUtility.EnsureOutlineTargets(gameObject);
        EnsureInteractionTrigger();
        EnsureId();
    }

    public bool CanBeDetectedBy(SquadCharacterController controller)
    {
        return controller != null
            && isActiveAndEnabled
            && (useTriggerMuninInput || useInteractInput || showStateDialogueOnInteract)
            && interactionTrigger != null
            && interactionTrigger.enabled
            && interactionTrigger.isTrigger;
    }

    public Collider GetInteractionDetectionCollider()
    {
        return interactionTrigger != null ? interactionTrigger : GetComponent<Collider>();
    }

    public Transform GetInteractionAnchor()
    {
        if (flameLight != null)
        {
            return flameLight.transform;
        }

        if (flameObject != null)
        {
            return flameObject.transform;
        }

        return transform;
    }

    public float GetInteractionMaxDistance(SquadCharacterController controller)
    {
        return Mathf.Max(0.1f, interactionRadius);
    }

    public int GetInteractionPriority(SquadCharacterController controller)
    {
        return interactionPriority;
    }

    public void SetDetectedCharacter(GameObject character)
    {
        detectedCharacter = character;
        if (character != null)
        {
            currentCharacter = character;
        }
    }

    public bool ProvidesLitInfluenceTo(Collider targetCollider, Vector3 fallbackPoint)
    {
        EnsureLitInfluence();
        return isLit
            && litInfluence != null
            && litInfluence.TouchesCollider(transform, targetCollider, fallbackPoint);
    }

    private void EnsureId()
    {
        if (TryResolvePersistentObjectId(out string resolvedId))
        {
            braseroId = resolvedId;
            return;
        }
    }

    public bool TryResolvePersistentObjectId(out string id)
    {
        PersistentNetworkObject persistentObject = GetComponent<PersistentNetworkObject>();
        if (persistentObject != null && !string.IsNullOrWhiteSpace(persistentObject.PersistentId))
        {
            id = persistentObject.PersistentId;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(braseroId))
        {
            id = braseroId;
            return true;
        }

        if (gameObject.scene.IsValid())
        {
            id = $"scene-brasero:{gameObject.scene.name}:{NetcodeSceneIdUtility.GetStableId(transform):X8}";
            return true;
        }

        id = $"runtime-brasero:{name}:{GetInstanceID()}";
        return true;
    }

    private void OnEnable()
    {
        ApplyVisuals(true);

        if (useInteractInput || useTriggerMuninInput || showStateDialogueOnInteract)
        {
            LocalInputRouter.EnsureInitialized();
        }

        if (useInteractInput || showStateDialogueOnInteract)
        {
            LocalInputRouter.Interact += OnInteractPerformed;
        }

        if (useTriggerMuninInput)
        {
            LocalInputRouter.TriggerMunin += OnTriggerMuninPerformed;
        }

        UpdateLitInfluence(true);
        NotifyAgeManagerDriverAvailabilityChanged();
    }

    private void OnDisable()
    {
        LocalInputRouter.Interact -= OnInteractPerformed;
        LocalInputRouter.TriggerMunin -= OnTriggerMuninPerformed;

        ClearLitInfluence();
        NotifyAgeManagerDriverAvailabilityChanged();
        StopEmissionRoutine();
        StopInteractionRoutine();
        ResetInteractionState();
    }

    private void Update()
    {
        UpdateLitInfluence(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!useInteractInput && !useTriggerMuninInput && !showStateDialogueOnInteract)
        {
            return;
        }

        HandleCharacterEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!useInteractInput && !useTriggerMuninInput && !showStateDialogueOnInteract)
        {
            return;
        }

        HandleCharacterExit(other);
    }

    public void SetLit(bool lit)
    {
        if (IsNetworked())
        {
            if (!IsServer)
            {
                return;
            }

            SetLitServer(lit);
            return;
        }

        SetLitInternal(lit);
    }

    public void Toggle()
    {
        if (IsNetworked())
        {
            if (!IsServer)
            {
                return;
            }

            SetLitServer(!isLit);
            return;
        }

        SetLitInternal(!isLit);
    }

    private void ApplyVisuals(bool immediate)
    {
        if (flameLight != null)
        {
            flameLight.enabled = isLit;
        }

        ApplyLitActivationTargets();
        UpdateFlameVisuals(immediate);
    }

    public void SetGameObjectActiveWhenLit(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        target.SetActive(isLit);
    }

    public void ApplyLitActivationTargets()
    {
        if (activateWhenLitTargets == null || activateWhenLitTargets.Length == 0)
        {
            return;
        }

        for (int i = 0; i < activateWhenLitTargets.Length; i++)
        {
            SetGameObjectActiveWhenLit(activateWhenLitTargets[i]);
        }
    }

    private void UpdateFlameVisuals(bool immediate)
    {
        if (!Application.isPlaying)
        {
            // Ne pas modifier les modules d'emission depuis OnValidate: Unity serialise ces valeurs dans la scene.
            SetFlameVisualRootsActive(isLit);
            return;
        }

        if (isLit)
        {
            SetFlameVisualRootsActive(true);
        }

        CollectFlameParticleSystems();

        if (isLit)
        {
            EnsureFlameParticlesPlaying();
        }

        if (immediate)
        {
            StopEmissionRoutine();
            SetEmissionFactor(isLit ? 1f : 0f);
            if (!isLit)
            {
                SetFlameVisualRootsActive(false);
            }

            return;
        }

        StartEmissionTransition(isLit ? 1f : 0f, emissionFadeDuration, !isLit);
    }

    private void CollectFlameParticleSystems()
    {
        flameParticleSystems.Clear();

        if (flameObject != null)
        {
            AddFlameParticleSystems(flameObject);
        }

        for (int i = flameParticleSystems.Count - 1; i >= 0; i--)
        {
            ParticleSystem system = flameParticleSystems[i];
            if (system == null)
            {
                flameParticleSystems.RemoveAt(i);
                continue;
            }

            if (!emissionBases.ContainsKey(system))
            {
                CacheEmissionBase(system);
            }
        }
    }

    private void EnsureFlameParticlesPlaying()
    {
        for (int i = 0; i < flameParticleSystems.Count; i++)
        {
            ParticleSystem system = flameParticleSystems[i];
            if (system == null)
            {
                continue;
            }

            if (!system.isPlaying)
            {
                system.Play();
            }
        }
    }

    private void AddFlameParticleSystems(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem system = systems[i];
            if (system == null || flameParticleSystems.Contains(system))
            {
                continue;
            }

            flameParticleSystems.Add(system);
        }
    }

    private void SetFlameVisualRootsActive(bool active)
    {
        if (flameObject != null && flameObject.activeSelf != active)
        {
            flameObject.SetActive(active);
        }
    }

    private void StartEmissionTransition(float target, float duration, bool deactivateAfter)
    {
        StopEmissionRoutine();

        if (flameParticleSystems.Count == 0)
        {
            if (deactivateAfter && target <= 0f)
            {
                SetFlameVisualRootsActive(false);
            }

            return;
        }

        float start = currentEmissionFactor;
        if (start < 0f)
        {
            start = target > 0f ? 0f : 1f;
            currentEmissionFactor = start;
        }

        if (duration <= 0f || Mathf.Approximately(start, target))
        {
            SetEmissionFactor(target);
            if (deactivateAfter && target <= 0f)
            {
                SetFlameVisualRootsActive(false);
            }

            return;
        }

        emissionRoutine = StartCoroutine(AnimateEmission(start, target, duration, deactivateAfter));
    }

    private IEnumerator AnimateEmission(float start, float target, float duration, bool deactivateAfter)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            SetEmissionFactor(Mathf.Lerp(start, target, t));
            elapsed += Time.deltaTime;
            yield return null;
        }

        SetEmissionFactor(target);

        if (deactivateAfter && target <= 0f)
        {
            SetFlameVisualRootsActive(false);
        }

        emissionRoutine = null;
    }

    private void StopEmissionRoutine()
    {
        if (emissionRoutine != null)
        {
            StopCoroutine(emissionRoutine);
            emissionRoutine = null;
        }
    }

    private void SetEmissionFactor(float factor)
    {
        currentEmissionFactor = factor;

        for (int i = 0; i < flameParticleSystems.Count; i++)
        {
            ParticleSystem system = flameParticleSystems[i];
            if (system == null)
            {
                continue;
            }

            if (!emissionBases.TryGetValue(system, out EmissionBase baseRates))
            {
                baseRates = CacheEmissionBase(system);
            }

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTimeMultiplier = baseRates.rateOverTime * factor;
            emission.rateOverDistanceMultiplier = baseRates.rateOverDistance * factor;
        }
    }

    private EmissionBase CacheEmissionBase(ParticleSystem system)
    {
        ParticleSystem.EmissionModule emission = system.emission;
        EmissionBase baseRates = new EmissionBase
        {
            rateOverTime = emission.rateOverTimeMultiplier,
            rateOverDistance = emission.rateOverDistanceMultiplier
        };
        emissionBases[system] = baseRates;
        return baseRates;
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (!useInteractInput && !showStateDialogueOnInteract)
        {
            return;
        }

        if (LocalInputRouter.IsInteractConsumed || InputFocusStack.HasAnyFocus())
        {
            return;
        }

        if (SquadManager.Instance != null && SquadManager.Instance.IsInputLocked())
        {
            return;
        }

        GameObject character = ResolveDetectedInteractionCharacter();
        if (character == null)
        {
            return;
        }

        if (!IsCharacterWithinInteractDistance(character.transform))
        {
            return;
        }

        currentCharacter = character;
        if (!LocalInputRouter.TryConsumeInteract())
        {
            return;
        }

        if (showStateDialogueOnInteract)
        {
            ShowStateDialogue();
            return;
        }

        if (interactionInProgress)
        {
            return;
        }

        StartInteraction();
    }

    private void ShowStateDialogue()
    {
        InfoBoxUI.TryShow(isLit ? litStateMessage : unlitStateMessage);
    }

    private void OnTriggerMuninPerformed(InputAction.CallbackContext context)
    {
        if (!useTriggerMuninInput)
        {
            return;
        }

        if (InputFocusStack.HasAnyFocus())
        {
            return;
        }

        if (SquadManager.Instance != null && SquadManager.Instance.IsInputLocked())
        {
            return;
        }

        GameObject character = ResolveDetectedInteractionCharacter();
        if (character == null)
        {
            return;
        }

        currentCharacter = character;
        if (!LocalInputRouter.TryConsumeTriggerMunin())
        {
            return;
        }

        if (interactionInProgress)
        {
            return;
        }

        StartInteraction();
    }

    public override void OnNetworkSpawn()
    {
        netIsLit.OnValueChanged += OnNetLitChanged;
        if (IsServer)
        {
            netIsLit.Value = isLit;
        }
        else
        {
            ApplyNetState(netIsLit.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        netIsLit.OnValueChanged -= OnNetLitChanged;
    }

    private void HandleCharacterEnter(Collider other)
    {
        if (other == null || other.isTrigger)
        {
            return;
        }

        GameObject character = GetSquadCharacter(other);
        if (character == null)
        {
            return;
        }

        bool firstCollider = RegisterCharacterCollider(character);
        if (firstCollider && !charactersInRange.Contains(character))
        {
            charactersInRange.Add(character);
        }

        RefreshCurrentCharacter();
    }

    private void HandleCharacterExit(Collider other)
    {
        if (other == null || other.isTrigger)
        {
            return;
        }

        GameObject character = GetSquadCharacter(other);
        if (character == null)
        {
            return;
        }

        if (!UnregisterCharacterCollider(character))
        {
            return;
        }

        charactersInRange.Remove(character);
        RefreshCurrentCharacter();
    }

    private void RefreshCurrentCharacter()
    {
        if (charactersInRange.Count == 0)
        {
            currentCharacter = null;
            return;
        }

        GameObject controlled = LocalPlayerUtils.GetControlledCharacter();
        if (controlled != null)
        {
            currentCharacter = charactersInRange.Contains(controlled) ? controlled : null;
            return;
        }

        currentCharacter = charactersInRange[0];
    }

    private GameObject GetSquadCharacter(Collider other)
    {
        if (other == null)
        {
            return null;
        }

        Transform current = other.transform;
        bool hasPlayerTag = false;
        GameObject taggedPlayerRoot = null;
        GameObject squadRoot = null;
        bool hasSquadList = SquadManager.Instance != null && SquadManager.Instance.squadCharacters != null;
        while (current != null)
        {
            if (current.CompareTag("Player"))
            {
                hasPlayerTag = true;
                taggedPlayerRoot = current.gameObject;
            }

            if (hasSquadList && SquadManager.Instance.squadCharacters.Contains(current.gameObject))
            {
                squadRoot = current.gameObject;
            }

            current = current.parent;
        }

        if (squadRoot == null && hasSquadList)
        {
            Transform root = other.transform.root;
            if (root != null)
            {
                if (root.CompareTag("Player"))
                {
                    hasPlayerTag = true;
                    taggedPlayerRoot = root.gameObject;
                }

                for (int i = 0; i < SquadManager.Instance.squadCharacters.Count; i++)
                {
                    GameObject candidate = SquadManager.Instance.squadCharacters[i];
                    if (candidate != null && candidate.transform.IsChildOf(root))
                    {
                        squadRoot = candidate;
                        break;
                    }
                }
            }
        }

        if (squadRoot != null)
        {
            return squadRoot;
        }

        if (hasPlayerTag && taggedPlayerRoot != null)
        {
            return taggedPlayerRoot;
        }

        return null;
    }

    private bool RegisterCharacterCollider(GameObject character)
    {
        if (character == null)
        {
            return false;
        }

        if (!characterColliderCounts.TryGetValue(character, out int count))
        {
            characterColliderCounts[character] = 1;
            return true;
        }

        characterColliderCounts[character] = count + 1;
        return false;
    }

    private bool UnregisterCharacterCollider(GameObject character)
    {
        if (character == null)
        {
            return false;
        }

        if (!characterColliderCounts.TryGetValue(character, out int count))
        {
            return false;
        }

        count -= 1;
        if (count > 0)
        {
            characterColliderCounts[character] = count;
            return false;
        }

        characterColliderCounts.Remove(character);
        return true;
    }

    private void ResetInteractionState()
    {
        charactersInRange.Clear();
        characterColliderCounts.Clear();
        currentCharacter = null;
        detectedCharacter = null;
    }

    private void StartInteraction()
    {
        if (interactionRoutine != null)
        {
            return;
        }

        interactionRoutine = StartCoroutine(HandleInteractionRoutine());
    }

    private IEnumerator HandleInteractionRoutine()
    {
        interactionInProgress = true;
        MuninController munin = ResolveMuninController();
        if (munin != null)
        {
            if (munin.IsMoving || !munin.TryConsumeCharge())
            {
                interactionInProgress = false;
                interactionRoutine = null;
                yield break;
            }

            activeMuninController = munin;
            Vector3 targetPosition = ResolveMuninTargetPosition();
            yield return munin.MoveToWorldAndBack(targetPosition, ToggleFromInteraction);
            activeMuninController = null;
        }
        else
        {
            ToggleFromInteraction();
        }

        interactionInProgress = false;
        interactionRoutine = null;
    }

    private GameObject ResolveCurrentInteractionCharacter()
    {
        if (currentCharacter != null && IsCharacterInRange(currentCharacter.transform))
        {
            return currentCharacter;
        }

        RefreshCurrentCharacter();
        if (currentCharacter != null && IsCharacterInRange(currentCharacter.transform))
        {
            return currentCharacter;
        }

        GameObject controlled = LocalPlayerUtils.GetControlledCharacter();
        if (controlled != null && IsCharacterInRange(controlled.transform))
        {
            return controlled;
        }

        Transform localRoot = LocalPlayerContext.LocalCharacterRoot;
        if (localRoot != null && IsCharacterInRange(localRoot))
        {
            return localRoot.gameObject;
        }

        return null;
    }

    private GameObject ResolveDetectedInteractionCharacter()
    {
        if (detectedCharacter != null && IsCharacterInRange(detectedCharacter.transform))
        {
            return detectedCharacter;
        }

        return null;
    }

    private bool IsCharacterInRange(Transform characterRoot)
    {
        if (characterRoot == null)
        {
            return false;
        }

        if (interactionTrigger != null && interactionTrigger.enabled && interactionTrigger.isTrigger)
        {
            return CharacterInteractionDetection.IsCharacterInsideInteractionCollider(characterRoot, interactionTrigger);
        }

        Vector3 center = transform.TransformPoint(interactionCenter);
        float radius = Mathf.Max(0f, interactionRadius);
        float distanceSqr = (characterRoot.position - center).sqrMagnitude;
        return distanceSqr <= radius * radius;
    }

    private bool IsCharacterWithinInteractDistance(Transform characterRoot)
    {
        if (characterRoot == null)
        {
            return false;
        }

        Transform anchor = GetInteractionAnchor();
        Vector3 targetPosition = anchor != null ? anchor.position : transform.TransformPoint(interactionCenter);
        Vector3 delta = characterRoot.position - targetPosition;
        delta.y = 0f;

        float distance = Mathf.Max(0.05f, interactMaxDistance);
        return delta.sqrMagnitude <= distance * distance;
    }

    private void OnNetLitChanged(bool previous, bool current)
    {
        ApplyNetState(current);
    }

    private void ApplyNetState(bool lit)
    {
        isLit = lit;
        ApplyVisuals(!Application.isPlaying);
        UpdateLitInfluence(true);
        StateChanged?.Invoke(this, isLit);
    }

    private void SetLitServer(bool lit)
    {
        SetLitInternal(lit);
        netIsLit.Value = lit;
    }

    private void SetLitInternal(bool lit)
    {
        if (isLit == lit)
        {
            return;
        }

        isLit = lit;
        ApplyVisuals(!Application.isPlaying);
        UpdateLitInfluence(true);
        StateChanged?.Invoke(this, isLit);
    }

    private void UpdateLitInfluence(bool force)
    {
        EnsureLitInfluence();
        litInfluence.Tick(this, LitInfluenceSourceKind.Brasero, isLit, force);
    }

    private void ClearLitInfluence()
    {
        if (litInfluence != null)
        {
            litInfluence.Clear(this, LitInfluenceSourceKind.Brasero);
        }
    }

    private void NotifyAgeManagerDriverAvailabilityChanged()
    {
        if (!Application.isPlaying || !ancientBrasero)
        {
            return;
        }

        AgeManager manager = AgeManager.ActiveInstance;
        if (manager != null)
        {
            manager.RefreshAndResubscribe();
        }
    }

    private bool IsNetworked()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestInteractServerRpc(ServerRpcParams rpcParams = default)
    {
        Transform playerRoot = NetcodePlayerUtils.GetPlayerTransform(rpcParams.Receive.SenderClientId);
        if (!IsCharacterInRange(playerRoot))
        {
            return;
        }

        SetLitServer(!isLit);
    }

    private void ToggleFromInteraction()
    {
        if (IsNetworked())
        {
            if (IsServer)
            {
                SetLitServer(!isLit);
            }
            else
            {
                RequestInteractServerRpc();
            }

            return;
        }

        SetLitInternal(!isLit);
    }

    private MuninController ResolveMuninController()
    {
        if (muninController != null)
        {
            return muninController;
        }

        if (currentCharacter == null)
        {
            return null;
        }

        return currentCharacter.GetComponentInChildren<MuninController>(true);
    }

    private Vector3 ResolveMuninTargetPosition()
    {
        Transform anchor = GetInteractionAnchor();
        if (anchor == null)
        {
            return transform.position + transform.rotation * muninTargetOffset;
        }

        return anchor.position + anchor.rotation * muninTargetOffset;
    }

    private void StopInteractionRoutine()
    {
        if (interactionRoutine != null)
        {
            StopCoroutine(interactionRoutine);
            interactionRoutine = null;
        }

        interactionInProgress = false;
        if (activeMuninController != null)
        {
            activeMuninController.CancelManualMotion();
            activeMuninController = null;
        }
    }

    private void EnsureInteractionTrigger()
    {
        if (interactionTrigger == null)
        {
            SphereCollider[] colliders = GetComponents<SphereCollider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i].isTrigger)
                {
                    interactionTrigger = colliders[i];
                    break;
                }
            }
        }

        if (interactionTrigger == null)
        {
            interactionTrigger = gameObject.AddComponent<SphereCollider>();
        }

        if (interactionTrigger == null)
        {
            return;
        }

        interactionTrigger.isTrigger = true;
        interactionTrigger.radius = Mathf.Max(0f, interactionRadius);
        interactionTrigger.center = interactionCenter;
    }

    private void EnsureLitInfluence()
    {
        if (litInfluence == null)
        {
            litInfluence = new LitInfluenceSource(6f);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureId();
        EnsureInteractionTrigger();
        EnsureLitInfluence();
        if (!Application.isPlaying)
        {
            ApplyVisuals(true);
        }
    }

    private void OnDrawGizmosSelected()
    {
        EnsureLitInfluence();
        litInfluence.DrawGizmos(transform, isLit);
    }
#endif

    private struct EmissionBase
    {
        public float rateOverTime;
        public float rateOverDistance;
    }
}
