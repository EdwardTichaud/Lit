using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using Unity.Netcode;

// Source de flamme unifiee. La variante AncientFlame participe aussi a l'AgeManager.
[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(NetworkObject))]
[DisallowMultipleComponent]
public class Flame : NetworkBehaviour, ICharacterDetectedInteractable
{
    [Header("State")]
    [SerializeField, Tooltip("Etat de la flamme au demarrage.")]
    private bool isLit = false;
    [SerializeField, Tooltip("Identifiant unique utilise pour la sauvegarde.")]
    private string flameId;

    public bool IsLit => isLit;
    public bool IsEffectivelyLit => isLit && !externalSuppression;
    public bool IsExternallySuppressed => externalSuppression;
    public string FlameId => flameId;
    public bool IsAncientFlame => ancientFlame;
    public int ChargeCostToLight => ancientFlame
        ? Mathf.Max(2, chargeCostToLight)
        : Mathf.Max(0, chargeCostToLight);
    public IReadOnlyList<GameObject> CommonLightActivationOrder => commonLightActivationOrder;
    public Color FlameColor => LitFlameColorUtility.ResolveFlameColor(flameLight, flameObject, Color.white);

    public event Action<Flame, bool> StateChanged;

    public int GetChargeCostForTargetState(bool targetLit)
    {
        return targetLit && !isLit ? ChargeCostToLight : 0;
    }

    [Header("Visuals")]
    [SerializeField, Tooltip("GameObject visuel actif quand la flamme est allumee.")]
    private GameObject flameObject;
    [Tooltip("Lumiere de flamme optionnelle.")]
    public Light flameLight;
    [Tooltip("Objets actives quand la flamme est allumee.")]
    public GameObject[] activateWhenLitTargets = Array.Empty<GameObject>();
    [SerializeField, Tooltip("Configure cette flamme comme source de reveal/dissolve de monde.")]
    private bool configureRevealSource = true;
    [SerializeField, Tooltip("Receiver de lumiere optionnel. Si vide, cherche dans les enfants.")]
    private FlameLightReceiver flameLightReceiver;

    [Header("Interaction")]
    [Tooltip("Ecoute Interact en plus de TriggerMunin.")]
    public bool useInteractInput = false;
    [SerializeField, Tooltip("Autorise l'appel de Munin depuis le choix d'interaction de la torche.")]
    private bool useTriggerMuninInput = true;
    [SerializeField, Tooltip("Affiche un dialogue d'etat avec Interact sans changer la flamme.")]
    private bool showStateDialogueOnInteract = true;
    [SerializeField, Tooltip("Message affiche avec Interact quand la flamme est allumee.")]
    private string litStateMessage = "La flamme est allumee.";
    [SerializeField, Tooltip("Message affiche avec Interact quand la flamme est eteinte.")]
    private string unlitStateMessage = "La flamme est eteinte.";
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
    [SerializeField, Tooltip("Zone d'information active seulement quand la flamme est allumee.")]
    private LitInfluenceSource litInfluence = new LitInfluenceSource(6f);
    [SerializeField, Tooltip("Affiche en runtime une sphere transparente qui represente la zone d'influence lumineuse.")]
    private bool showInfluenceSphere = true;
    [SerializeField, Tooltip("Masque la sphere quand la zone d'influence n'est pas active.")]
    private bool influenceSphereOnlyWhenLit = true;
    [SerializeField, Range(0f, 1f), Tooltip("Opacite de la sphere d'influence runtime.")]
    private float influenceSphereAlpha = 0.14f;
    [SerializeField, Tooltip("Couleur de la sphere d'influence pour les flames communes.")]
    private Color influenceSphereColor = new Color(1f, 0.72f, 0.18f, 1f);
    [SerializeField, Tooltip("Couleur de la sphere d'influence pour les ancient flames.")]
    private Color ancientInfluenceSphereColor = new Color(0.35f, 0.72f, 1f, 1f);
    [SerializeField, Tooltip("Materiau transparent optionnel pour la sphere d'influence. Laisse vide pour utiliser le materiau runtime.")]
    private Material influenceSphereMaterialOverride;
    [SerializeField, Tooltip("Ordre d'allumage des lights communes influencees par cette flamme.")]
    private List<GameObject> commonLightActivationOrder = new List<GameObject>();

    [Header("Age")]
    [SerializeField, Tooltip("Si actif, cette AncientFlame compte dans l'AgeManager.")]
    private bool ancientFlame;

    [Header("Flame Emission")]
    [Tooltip("Duree du fondu d'emission (allumage/extinction).")]
    public float emissionFadeDuration = 1f;

    [Header("Munin")]
    [SerializeField, FormerlySerializedAs("muninTransform"), Tooltip("Controleur de Munin. Laisse vide pour utiliser celui du personnage detecte.")]
    private MuninController muninController;
    [SerializeField, Tooltip("Offset local depuis l'ancre de la flamme pour la destination de Munin.")]
    private Vector3 muninTargetOffset = Vector3.zero;
    [SerializeField, Min(0), Tooltip("Charges Munin consommees uniquement pour allumer cette flamme. L'extinction ne rend jamais de charge.")]
    private int chargeCostToLight = 1;

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
    private bool externalSuppression;
    private MuninController activeMuninController;
    private GameObject influenceSphereVisual;
    private MeshRenderer influenceSphereRenderer;
    private Material runtimeInfluenceSphereMaterial;
    private MaterialPropertyBlock influenceSphereProperties;
    private NetworkVariable<bool> netIsLit = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private const string InfluenceSphereVisualName = "LitInfluenceSphereVisual";
    private static readonly int baseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int colorPropertyId = Shader.PropertyToID("_Color");
    private static Mesh influenceSphereMesh;

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
        EnsureRevealSource();
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
        float distance = Mathf.Max(0.1f, interactionRadius);
        MuninController munin = controller != null ? MuninController.FindForCharacter(controller.gameObject) : null;
        if (munin != null && munin.TryGetLightSourceDetectionDistance(this, out float muninDistance))
        {
            distance = Mathf.Max(distance, muninDistance);
        }

        return distance;
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
        return IsEffectivelyLit
            && litInfluence != null
            && litInfluence.TouchesCollider(transform, targetCollider, fallbackPoint);
    }

    private void EnsureId()
    {
        if (TryResolvePersistentObjectId(out string resolvedId))
        {
            flameId = resolvedId;
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

        if (!string.IsNullOrWhiteSpace(flameId))
        {
            id = flameId;
            return true;
        }

        if (gameObject.scene.IsValid())
        {
            id = $"scene-flame:{gameObject.scene.name}:{NetcodeSceneIdUtility.GetStableId(transform):X8}";
            return true;
        }

        id = $"runtime-flame:{name}:{GetInstanceID()}";
        return true;
    }

    private void EnsureRevealSource()
    {
        if (!configureRevealSource)
        {
            return;
        }

        if (flameLightReceiver == null)
        {
            flameLightReceiver = GetComponentInChildren<FlameLightReceiver>(true);
        }

        if (flameLightReceiver != null)
        {
            flameLightReceiver.ConfigureWorldRevealSource(true);
        }
    }

    private void OnEnable()
    {
        EnsureRevealSource();
        ApplyVisuals(true);

        if (useInteractInput || useTriggerMuninInput || showStateDialogueOnInteract)
        {
            LocalInputRouter.EnsureInitialized();
        }

        if (useInteractInput || showStateDialogueOnInteract)
        {
            LocalInputRouter.Interact += OnInteractPerformed;
        }

        UpdateLitInfluence(true);
        NotifyAgeManagerDriverAvailabilityChanged();
    }

    private void OnDisable()
    {
        LocalInputRouter.Interact -= OnInteractPerformed;
        ClearLitInfluence();
        SetInfluenceSphereVisualActive(false);
        NotifyAgeManagerDriverAvailabilityChanged();
        StopEmissionRoutine();
        StopInteractionRoutine();
        ResetInteractionState();
    }

    public override void OnDestroy()
    {
        DestroyRuntimeInfluenceSphereMaterial();
        base.OnDestroy();
    }

    private void Update()
    {
        UpdateLitInfluence(false);
        UpdateInfluenceSphereVisual();
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

    public void SetExternalSuppression(bool suppressed)
    {
        if (externalSuppression == suppressed)
        {
            return;
        }

        externalSuppression = suppressed;
        EnsureRevealSource();
        flameLightReceiver?.SetWorldRevealSuppressed(suppressed);
        ApplyVisuals(true);
        UpdateLitInfluence(true);
        StateChanged?.Invoke(this, IsEffectivelyLit);
    }

    private void ApplyVisuals(bool immediate)
    {
        if (flameLight != null)
        {
            flameLight.enabled = isLit;
        }

        ApplyLitActivationTargets();
        UpdateFlameVisuals(immediate);
        UpdateInfluenceSphereVisual();
    }

    public void SetGameObjectActiveWhenLit(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        target.SetActive(IsEffectivelyLit);
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

    public bool TryStartMuninInteraction(GameObject character)
    {
        if (!useTriggerMuninInput || character == null || interactionInProgress)
        {
            return false;
        }

        SquadCharacterController controller = character.GetComponent<SquadCharacterController>();
        if (controller == null || !CanBeDetectedBy(controller) || !IsCharacterWithinInteractDistance(character.transform))
        {
            return false;
        }

        currentCharacter = character;
        StartInteraction();
        return true;
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
            if (munin.IsMoving)
            {
                interactionInProgress = false;
                interactionRoutine = null;
                yield break;
            }

            // Eteindre reste utile pour le noir, les Ombres et la narration, mais ne
            // rembourse jamais Munin. Seul l'allumage consomme la valeur configuree.
            int requiredCharges = GetChargeCostForTargetState(!isLit);
            if (requiredCharges > 0 && !munin.TryConsumeCharge(requiredCharges))
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
            if (CharacterInteractionDetection.IsCharacterInsideInteractionCollider(characterRoot, interactionTrigger))
            {
                return true;
            }
        }

        return CharacterInteractionDetection.IsCharacterWithinRange(
            characterRoot,
            interactionTrigger != null ? interactionTrigger : GetComponent<Collider>(),
            GetInteractionAnchor(),
            ResolveInteractionDistanceForCharacter(characterRoot));
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
        StateChanged?.Invoke(this, IsEffectivelyLit);
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
        StateChanged?.Invoke(this, IsEffectivelyLit);
    }

    private void UpdateLitInfluence(bool force)
    {
        EnsureLitInfluence();
        LitInfluenceSourceKind sourceKind = ancientFlame
            ? LitInfluenceSourceKind.AncientFlame
            : LitInfluenceSourceKind.Flame;
        litInfluence.Tick(this, sourceKind, IsEffectivelyLit, force);
    }

    private void ClearLitInfluence()
    {
        if (litInfluence != null)
        {
            LitInfluenceSourceKind sourceKind = ancientFlame
                ? LitInfluenceSourceKind.AncientFlame
                : LitInfluenceSourceKind.Flame;
            litInfluence.Clear(this, sourceKind);
        }
    }

    private void NotifyAgeManagerDriverAvailabilityChanged()
    {
        if (!Application.isPlaying || !ancientFlame)
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

    private float ResolveInteractionDistanceForCharacter(Transform characterRoot)
    {
        float distance = Mathf.Max(0.1f, interactionRadius);
        MuninController munin = characterRoot != null ? MuninController.FindForCharacter(characterRoot.gameObject) : null;
        if (munin == null)
        {
            munin = ResolveMuninController();
        }

        if (munin != null && munin.TryGetLightSourceDetectionDistance(this, out float muninDistance))
        {
            distance = Mathf.Max(distance, muninDistance);
        }

        return distance;
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

    private void UpdateInfluenceSphereVisual()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        // Une flamme allumee ne doit jamais creer ni afficher de sphere runtime.
        if (isLit)
        {
            SetInfluenceSphereVisualActive(false);
            return;
        }

        EnsureLitInfluence();
        bool shouldShow = showInfluenceSphere
            && litInfluence != null
            && litInfluence.Enabled
            && litInfluence.Radius > 0f
            && (!influenceSphereOnlyWhenLit || IsEffectivelyLit);

        if (!shouldShow)
        {
            SetInfluenceSphereVisualActive(false);
            return;
        }

        EnsureInfluenceSphereVisual();
        if (influenceSphereVisual == null || influenceSphereRenderer == null)
        {
            return;
        }

        Transform visualTransform = influenceSphereVisual.transform;
        visualTransform.localPosition = litInfluence.Center;
        visualTransform.localRotation = Quaternion.identity;

        float diameter = litInfluence.Radius * 2f;
        Vector3 parentScale = transform.lossyScale;
        visualTransform.localScale = new Vector3(
            ResolveLocalScaleForWorldDiameter(diameter, parentScale.x),
            ResolveLocalScaleForWorldDiameter(diameter, parentScale.y),
            ResolveLocalScaleForWorldDiameter(diameter, parentScale.z));

        ApplyInfluenceSphereColor();
        SetInfluenceSphereVisualActive(true);
    }

    private void EnsureInfluenceSphereVisual()
    {
        if (influenceSphereVisual != null && influenceSphereRenderer != null)
        {
            return;
        }

        if (influenceSphereVisual == null)
        {
            Transform existing = transform.Find(InfluenceSphereVisualName);
            influenceSphereVisual = existing != null ? existing.gameObject : null;
        }

        if (influenceSphereVisual == null)
        {
            influenceSphereVisual = new GameObject(InfluenceSphereVisualName);
            influenceSphereVisual.hideFlags = HideFlags.DontSave;
            influenceSphereVisual.transform.SetParent(transform, false);
        }

        MeshFilter meshFilter = influenceSphereVisual.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = influenceSphereVisual.AddComponent<MeshFilter>();
        }

        meshFilter.sharedMesh = GetInfluenceSphereMesh();

        influenceSphereRenderer = influenceSphereVisual.GetComponent<MeshRenderer>();
        if (influenceSphereRenderer == null)
        {
            influenceSphereRenderer = influenceSphereVisual.AddComponent<MeshRenderer>();
        }

        influenceSphereRenderer.sharedMaterial = ResolveInfluenceSphereMaterial();
        influenceSphereRenderer.shadowCastingMode = ShadowCastingMode.Off;
        influenceSphereRenderer.receiveShadows = false;
        RemoveInfluenceSphereColliders();
    }

    private Material ResolveInfluenceSphereMaterial()
    {
        if (influenceSphereMaterialOverride != null)
        {
            return influenceSphereMaterialOverride;
        }

        if (runtimeInfluenceSphereMaterial == null)
        {
            runtimeInfluenceSphereMaterial = CreateRuntimeInfluenceSphereMaterial();
        }

        return runtimeInfluenceSphereMaterial;
    }

    private Material CreateRuntimeInfluenceSphereMaterial()
    {
        Shader shader = Shader.Find("HDRP/Unlit")
            ?? Shader.Find("HDRenderPipeline/Unlit")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Unlit/Color")
            ?? Shader.Find("Standard");

        if (shader == null)
        {
            return null;
        }

        Material material = new Material(shader)
        {
            name = "Runtime_FlameInfluenceSphere",
            hideFlags = HideFlags.DontSave
        };

        ConfigureTransparentRuntimeMaterial(material);
        return material;
    }

    private void ApplyInfluenceSphereColor()
    {
        if (influenceSphereRenderer == null)
        {
            return;
        }

        Material material = ResolveInfluenceSphereMaterial();
        if (material == null)
        {
            influenceSphereRenderer.enabled = false;
            return;
        }

        if (influenceSphereRenderer.sharedMaterial != material)
        {
            influenceSphereRenderer.sharedMaterial = material;
        }

        influenceSphereRenderer.enabled = true;
        Color color = ancientFlame ? ancientInfluenceSphereColor : influenceSphereColor;
        color.a = Mathf.Clamp01(influenceSphereAlpha);

        if (influenceSphereProperties == null)
        {
            influenceSphereProperties = new MaterialPropertyBlock();
        }

        influenceSphereRenderer.GetPropertyBlock(influenceSphereProperties);
        influenceSphereProperties.SetColor(baseColorPropertyId, color);
        influenceSphereProperties.SetColor(colorPropertyId, color);
        influenceSphereRenderer.SetPropertyBlock(influenceSphereProperties);
    }

    private void SetInfluenceSphereVisualActive(bool active)
    {
        if (influenceSphereVisual != null && influenceSphereVisual.activeSelf != active)
        {
            influenceSphereVisual.SetActive(active);
        }
    }

    private void RemoveInfluenceSphereColliders()
    {
        if (influenceSphereVisual == null)
        {
            return;
        }

        Collider[] colliders = influenceSphereVisual.GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider visualCollider = colliders[i];
            if (visualCollider == null)
            {
                continue;
            }

            visualCollider.enabled = false;
            Destroy(visualCollider);
        }
    }

    private void DestroyRuntimeInfluenceSphereMaterial()
    {
        if (runtimeInfluenceSphereMaterial == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(runtimeInfluenceSphereMaterial);
        }
        else
        {
            DestroyImmediate(runtimeInfluenceSphereMaterial);
        }

        runtimeInfluenceSphereMaterial = null;
    }

    private static void ConfigureTransparentRuntimeMaterial(Material material)
    {
        if (material == null)
        {
            return;
        }

        SetMaterialFloatIfPresent(material, "_SurfaceType", 1f);
        SetMaterialFloatIfPresent(material, "_BlendMode", 0f);
        SetMaterialFloatIfPresent(material, "_AlphaCutoffEnable", 0f);
        SetMaterialFloatIfPresent(material, "_ZWrite", 0f);
        SetMaterialFloatIfPresent(material, "_CullMode", (float)CullMode.Off);
        SetMaterialFloatIfPresent(material, "_DoubleSidedEnable", 1f);
        SetMaterialIntIfPresent(material, "_SrcBlend", (int)BlendMode.SrcAlpha);
        SetMaterialIntIfPresent(material, "_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        SetMaterialColorIfPresent(material, "_BaseColor", new Color(1f, 0.72f, 0.18f, 0.14f));
        SetMaterialColorIfPresent(material, "_Color", new Color(1f, 0.72f, 0.18f, 0.14f));
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    private static void SetMaterialFloatIfPresent(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static void SetMaterialIntIfPresent(Material material, string propertyName, int value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetInt(propertyName, value);
        }
    }

    private static void SetMaterialColorIfPresent(Material material, string propertyName, Color value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, value);
        }
    }

    private static float ResolveLocalScaleForWorldDiameter(float diameter, float parentAxisScale)
    {
        float safeScale = Mathf.Abs(parentAxisScale);
        return safeScale > 0.0001f ? diameter / safeScale : diameter;
    }

    private static Mesh GetInfluenceSphereMesh()
    {
        if (influenceSphereMesh != null)
        {
            return influenceSphereMesh;
        }

        const int latitudeSegments = 16;
        const int longitudeSegments = 32;
        int vertexCount = (latitudeSegments + 1) * (longitudeSegments + 1);
        Vector3[] vertices = new Vector3[vertexCount];
        Vector3[] normals = new Vector3[vertexCount];
        int vertexIndex = 0;

        for (int lat = 0; lat <= latitudeSegments; lat++)
        {
            float theta = Mathf.PI * lat / latitudeSegments;
            float y = Mathf.Cos(theta) * 0.5f;
            float ringRadius = Mathf.Sin(theta) * 0.5f;

            for (int lon = 0; lon <= longitudeSegments; lon++)
            {
                float phi = Mathf.PI * 2f * lon / longitudeSegments;
                Vector3 vertex = new Vector3(
                    Mathf.Cos(phi) * ringRadius,
                    y,
                    Mathf.Sin(phi) * ringRadius);

                vertices[vertexIndex] = vertex;
                normals[vertexIndex] = vertex.normalized;
                vertexIndex++;
            }
        }

        int[] triangles = new int[latitudeSegments * longitudeSegments * 6];
        int triangleIndex = 0;

        for (int lat = 0; lat < latitudeSegments; lat++)
        {
            for (int lon = 0; lon < longitudeSegments; lon++)
            {
                int current = lat * (longitudeSegments + 1) + lon;
                int next = current + longitudeSegments + 1;

                triangles[triangleIndex++] = current;
                triangles[triangleIndex++] = next;
                triangles[triangleIndex++] = current + 1;
                triangles[triangleIndex++] = current + 1;
                triangles[triangleIndex++] = next;
                triangles[triangleIndex++] = next + 1;
            }
        }

        influenceSphereMesh = new Mesh
        {
            name = "Runtime_FlameInfluenceSphereMesh",
            hideFlags = HideFlags.DontSave
        };
        influenceSphereMesh.vertices = vertices;
        influenceSphereMesh.normals = normals;
        influenceSphereMesh.triangles = triangles;
        influenceSphereMesh.RecalculateBounds();
        return influenceSphereMesh;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        chargeCostToLight = Mathf.Max(0, chargeCostToLight);
        influenceSphereAlpha = Mathf.Clamp01(influenceSphereAlpha);
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
        litInfluence.DrawGizmos(transform, IsEffectivelyLit);
    }
#endif

    private struct EmissionBase
    {
        public float rateOverTime;
        public float rateOverDistance;
    }
}
