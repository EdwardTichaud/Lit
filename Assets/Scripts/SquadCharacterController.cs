using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Controle le mouvement et l'inventaire runtime d'un personnage de la squad.
[RequireComponent(typeof(Animator))]
public partial class SquadCharacterController : MonoBehaviour
{
    private enum TorchVisualTransition
    {
        None,
        Equip,
        Unequip
    }

    private const string TorchAnimationLayerName = "Upper Body Torch";
    private const float TorchAnimationStateFallbackDelay = 0.2f;
    private const float TorchAnimationVisualDelay = 0.5f;
    private const int StepAssistLookAheadSampleCount = 3;
    private const float StepAssistSurfaceDeadZone = 0.01f;
    private static readonly int TorchEquipStateHash = Animator.StringToHash("Torch_Equip");
    private static readonly int TorchLocomotionStateHash = Animator.StringToHash("Torch_Locomotion");
    private static readonly int TorchOffStateHash = Animator.StringToHash("Torch_Off");
    private static readonly int TorchUnequipStateHash = Animator.StringToHash("Torch_Unequip");

    [Header("Inventory")]
    [SerializeField, HideInInspector] private List<Item> items = new List<Item>();
    [SerializeField, Tooltip("Duree initiale de la torche (secondes).")]
    private int startingTorchSeconds = 300;
    [SerializeField, Tooltip("Duree restante de la torche (secondes).")]
    private int torchSecondsRemaining = 300;

    [Header("Character Data")]
    [SerializeField, Tooltip("CharacterData lie a ce controller.")]
    private CharacterData characterData;

    [Header("Health")]
    [SerializeField, Tooltip("PV max runtime.")]
    private int maxHp = 10;
    [SerializeField, Tooltip("PV actuels runtime.")]
    private int currentHp = 10;
    [SerializeField, Tooltip("Reinitialise les PV au bind.")]
    private bool resetHpOnBind = true;
    [SerializeField, Tooltip("Clamp les PV actuels au max.")]
    private bool clampHpToMax = true;

    [Header("References")]
    [SerializeField, Tooltip("Animator pilote par le controller.")]
    private Animator animator;
    [SerializeField, Tooltip("Rigidbody optionnel pour le mouvement.")]
    private Rigidbody rigidbodyTarget;
    [SerializeField, Tooltip("CharacterController optionnel.")]
    private CharacterController characterController;
    [SerializeField, Tooltip("Transform racine utilise pour le mouvement.")]
    private Transform motionRoot;

    [Header("Animator Params")]
    [SerializeField, Tooltip("Nom du parametre Speed dans l'Animator.")]
    private string speedParam = "Speed";
    [SerializeField, Tooltip("Damping du parametre Speed.")]
    private float speedDampTime = 0.1f;
    [SerializeField, Tooltip("Utilise un damping sur Speed.")]
    private bool useSpeedDamping = false;

    [Header("Animation")]
    [SerializeField, Tooltip("Utilise des valeurs discretes (idle/walk/run).")]
    private bool useDiscreteLocomotion = true;
    [SerializeField, Tooltip("Seuil de vitesse pour la marche.")]
    private float walkSpeedThreshold = 0.1f;
    [SerializeField, Tooltip("Seuil de vitesse pour la course.")]
    private float runSpeedThreshold = 1f;
    [SerializeField, Tooltip("Valeur Speed pour idle.")]
    private float idleAnimValue = 0f;
    [SerializeField, Tooltip("Valeur Speed pour marche.")]
    private float walkAnimValue = 0.5f;
    [SerializeField, Tooltip("Valeur Speed pour course.")]
    private float runAnimValue = 2f;

    [Header("Movement")]
    [SerializeField, Tooltip("Vitesse de deplacement.")]
    private float moveSpeed = 2.5f;
    [SerializeField, Tooltip("Acceleration horizontale.")]
    private float acceleration = 15f;
    [SerializeField, Tooltip("Deceleration horizontale.")]
    private float deceleration = 10f;
    [SerializeField, Tooltip("Lissage de l'input.")]
    private float inputResponsiveness = 12f;
    [SerializeField, Tooltip("Utilise la velocite pour l'animation.")]
    private bool useVelocityForAnimation = true;
    [SerializeField, Tooltip("Tourne vers la direction d'input.")]
    private bool rotateToInput = true;
    [SerializeField, Tooltip("Vitesse de rotation.")]
    private float rotationSpeed = 10f;
    [SerializeField, Tooltip("Deplacement relatif a la camera.")]
    private bool useCameraRelative = true;
    [SerializeField, Tooltip("Camera de reference (fallback Main).")]
    private Camera referenceCamera;
    [SerializeField, Tooltip("Prefere un Rigidbody si present.")]
    private bool preferRigidbody = true;
    [SerializeField, Tooltip("Anime les RB en physics.")]
    private bool animatePhysics = true;

    [Header("Grounding")]
    [SerializeField, Tooltip("Distance du controle sol pour autoriser les etats relies au sol (m).")]
    private float jumpGroundCheckDistance = 0.18f;
    [SerializeField, Tooltip("Multiplicateur du rayon capsule pour le controle sol.")]
    private float jumpGroundCheckRadiusScale = 0.9f;
    [SerializeField, Tooltip("Petite vitesse verticale negative pour rester plaque au sol.")]
    private float groundedStickVelocity = 2f;

    [Header("Void Detection")]
    [SerializeField, Tooltip("Active la detection du vide pour eviter les chutes hors du monde.")]
    private bool enableVoidDetection = true;
    [SerializeField, Tooltip("Distance horizontale de controle devant le personnage (m).")]
    private float voidCheckDistance = 0.35f;
    [SerializeField, Tooltip("Profondeur du raycast vers le sol (m).")]
    private float voidCheckDepth = 4f;
    [SerializeField, Tooltip("LayerMask utilise pour detecter le sol.")]
    private LayerMask voidGroundMask = ~0;
    [SerializeField, Tooltip("Utilise la matrice de collision du layer pour la detection du sol.")]
    private bool voidUseCollisionMatrixMask = true;

    [Header("Step Assist")]
    [SerializeField, Tooltip("Permet de monter/descendre les marches et reliefs avec un Rigidbody.")]
    private bool enableStepAssist = true;
    [SerializeField, Tooltip("Hauteur max des marches (m).")]
    private float stepHeight = 0.55f;
    [SerializeField, Tooltip("Distance de detection des marches (m).")]
    private float stepCheckDistance = 0.35f;
    [SerializeField, Tooltip("Vitesse verticale appliquee pour monter une marche.")]
    private float stepUpSpeed = 7f;
    [SerializeField, Tooltip("Hauteur max pour descendre une marche (0 = utilise stepHeight).")]
    private float stepDownHeight = 0f;
    [SerializeField, Tooltip("Vitesse verticale appliquee pour descendre une marche (0 = utilise stepUpSpeed).")]
    private float stepDownSpeed = 0f;
    [SerializeField, Tooltip("Temps de lissage vertical en montee sur les escaliers (s).")]
    private float stepUpSmoothTime = 0.08f;
    [SerializeField, Tooltip("Temps de lissage vertical en descente sur les escaliers (s).")]
    private float stepDownSmoothTime = 0.1f;
    [SerializeField, Tooltip("Marge ajoutee a la hauteur max pour etre plus permissif (m).")]
    private float stepHeightTolerance = 0.05f;
    [SerializeField, Tooltip("Seuil minimal de relief pour declencher un step (m).")]
    private float stepMinHeight = 0.05f;
    [SerializeField, Tooltip("Vitesse verticale max autorisee pour declencher un step (0 = ignore).")]
    private float stepMaxUpVelocity = 1.5f;
    [SerializeField, Tooltip("Necessite d'etre au sol pour declencher un step.")]
    private bool requireGroundForStep = true;
    [SerializeField, Tooltip("Rayon du test haut (0 = meme que bas).")]
    private float stepUpperRadius = 0.0f;
    [SerializeField, Tooltip("Hauteur ajoutee au test haut (m).")]
    private float stepUpperHeightOffset = 0.02f;
    [SerializeField, Tooltip("Petit boost avant applique lors d'un step (m).")]
    private float stepForwardBoost = 0f;
    [SerializeField, Tooltip("Marge retiree au rayon pour les tests.")]
    private float stepRadiusPadding = 0.02f;
    [SerializeField, Tooltip("Distance de verification du sol (m).")]
    private float stepGroundCheckDistance = 0.15f;
    [SerializeField, Tooltip("LayerMask utilise pour les escaliers.")]
    private LayerMask stepLayerMask = 1 << 7;
    [SerializeField, Tooltip("Combine stepLayerMask avec la matrice de collision du layer.")]
    private bool stepUseCollisionMatrixMask = false;
    [SerializeField, Tooltip("Active les logs de debug pour le step assist.")]
    private bool stepDebugLogs = false;
    [SerializeField, Tooltip("Cooldown des logs (secondes).")]
    private float stepDebugCooldown = 0.5f;

    [Header("Foot IK")]
    [SerializeField, Tooltip("Active l'IK des pieds pour stabiliser l'ancrage au sol.")]
    private bool enableFootIk = true;
    [SerializeField, Tooltip("Poids max de l'IK (0-1).")]
    private float footIkWeight = 1f;
    [SerializeField, Tooltip("Poids position IK (0-1).")]
    private float footIkPositionWeight = 1f;
    [SerializeField, Tooltip("Poids rotation IK (0-1).")]
    private float footIkRotationWeight = 1f;
    [SerializeField, Tooltip("Vitesse max pour activer l'IK (m/s).")]
    private float footIkSpeedThreshold = 0.15f;
    [SerializeField, Tooltip("Vitesse de blend du poids IK.")]
    private float footIkBlendSpeed = 10f;
    [SerializeField, Tooltip("Offset vertical des pieds (m).")]
    private float footIkHeightOffset = 0.02f;
    [SerializeField, Tooltip("Raycast vers le haut pour trouver le sol (m).")]
    private float footIkRaycastUp = 0.25f;
    [SerializeField, Tooltip("Raycast vers le bas pour trouver le sol (m).")]
    private float footIkRaycastDown = 0.6f;
    [SerializeField, Tooltip("LayerMask utilise pour l'IK des pieds.")]
    private LayerMask footIkLayerMask = ~0;
    [SerializeField, Tooltip("Utilise la matrice de collision du layer pour l'IK.")]
    private bool footIkUseCollisionMatrixMask = true;

    [Header("Torch")]
    [SerializeField, Tooltip("Autorise ToggleTorch via input.")]
    private bool allowTorchToggle = true;
    [SerializeField, Tooltip("Nom du parent de la torche.")]
    private string torchParentName = "Stuff";
    [SerializeField, Tooltip("Nom du child de la torche.")]
    private string torchChildName = "Torch";
    [SerializeField, Tooltip("Parametre bool de torche.")]
    private string torchBoolParam = "Torch";
    [SerializeField, Tooltip("Torche active au demarrage.")]
    private bool torchStartsActive = true;
    [SerializeField, Tooltip("Lit l'etat depuis la hierarchie.")]
    private bool initializeTorchFromHierarchy = true;
    [SerializeField, Range(0f, 1f), Tooltip("Poids du layer torche a l'arret.")]
    private float torchUpperBodyIdleLayerWeight = 0.92f;
    [SerializeField, Range(0f, 1f), Tooltip("Poids du layer torche en locomotion rapide.")]
    private float torchUpperBodyMovingLayerWeight = 0.76f;
    [SerializeField, Tooltip("Vitesse de lissage du poids du layer torche.")]
    private float torchUpperBodyLayerWeightResponsiveness = 10f;

    [Header("External Forces")]
    [SerializeField, Tooltip("Temps de blocage input apres une force externe.")]
    private float inputLockTime = 0.2f;
    [SerializeField, Tooltip("ForceMode utilise pour les knockbacks.")]
    private ForceMode knockbackForceMode = ForceMode.VelocityChange;

    [Header("Collision")]
    [SerializeField, Tooltip("Ignore les collisions entre personnages.")]
    private bool ignoreCharacterCollisions = true;
    [SerializeField, Tooltip("Ignore les trigger colliders entre personnages.")]
    private bool ignoreCharacterTriggerColliders = true;
    [SerializeField, Tooltip("Intervalle de refresh des collisions.")]
    private float collisionRefreshInterval = 0.5f;

    private Vector2 moveInput;
    private float inputLockTimer;
    private Vector2 smoothedInput;
    private Vector2 animationPreviewInput;
    private Vector2 smoothedAnimationPreviewInput;
    private bool moveInputIsWorldSpace;
    private Vector3 currentHorizontalVelocity;
    private bool isGrounded;
    private float lastGroundedTime = float.NegativeInfinity;
    private float groundIgnoreUntilTime;
    private Transform torchTransform;
    private bool torchInitialized;
    private bool torchEquipped;
    private bool torchVisualEquipped;
    private TorchVisualTransition pendingTorchVisualTransition;
    private bool torchVisualTransitionStateObserved;
    private float torchVisualTransitionTimer;
    private float torchDrainTimer;
    private float nextCollisionRefreshTime;
    private bool collidersDirty = true;
    private readonly List<Collider> cachedColliders = new List<Collider>();
    private CapsuleCollider stepCapsule;
    [Header("Audio")]
    [SerializeField] private AudioListener audioListener;
    [SerializeField] private bool searchAudioListenerInChildren = true;
    private bool audioListenerActive;
    private NetworkObject cachedNetworkObject;
    private readonly RaycastHit[] stepCastHits = new RaycastHit[8];
    private readonly Collider[] stepOverlapHits = new Collider[8];
    private float nextStepDebugTime;
    private float stepVerticalSmoothVelocity;
    private float footIkWeightCurrent;
    private string lastAnimationDriverMode = string.Empty;
    private string lastAnimationMovementMode = string.Empty;
    private int lastAnimationSpeedBucket = int.MinValue;
    private bool lastAnimationAnimatorEnabled;

    private static readonly List<SquadCharacterController> activeCharacters = new List<SquadCharacterController>();
    private static readonly List<SquadCharacterController> registeredCharacters = new List<SquadCharacterController>();

    public CharacterData CharacterData => characterData;

    public IReadOnlyList<Item> Items => items;

    public IReadOnlyList<Skill> Skills => characterData != null ? characterData.skills : null;

    public int CurrentHp => currentHp;

    public int MaxHp => maxHp;

    public bool IsGrounded => isGrounded;

    private void Reset()
    {
        animator = GetComponent<Animator>();
        characterController = GetComponent<CharacterController>();
        rigidbodyTarget = GetComponent<Rigidbody>();
        stepCapsule = GetComponent<CapsuleCollider>();
        motionRoot = transform;
        ApplyAnimatorSettings();
        InitializeTorchState();
        ResetCommittedJumpRuntime();
    }

    private void Update()
    {
        // Torche + collisions en runtime.
        UpdateTorchLifetime(Time.deltaTime);
        RefreshCharacterCollisionsIfNeeded();
        UpdateAudioListenerState(false);
    }

    private void LateUpdate()
    {
        UpdateTorchVisualTransition();
        UpdateTorchAnimationLayerWeight();
    }

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (rigidbodyTarget == null)
        {
            rigidbodyTarget = GetComponent<Rigidbody>();
        }

        if (stepCapsule == null)
        {
            stepCapsule = GetComponent<CapsuleCollider>();
        }

        if (motionRoot == null)
        {
            motionRoot = transform;
        }

        CacheAudioListener();
        CacheNetworkObject();
        EnsureDynamicMeshCollidersSafe();

        EnsureInventoryList();
        if (characterData != null)
        {
            if (SquadManager.Instance != null)
            {
                characterData = SquadManager.Instance.GetRuntimeCharacter(characterData);
            }

            BindCharacterData(characterData);
        }

        ApplyAnimatorSettings();
        InitializeTorchState();
        ResetCommittedJumpRuntime();
        RefreshAnimationBindings("awake");
    }

    private void OnEnable()
    {
        RegisterCharacter();
        CacheAudioListener();
        CacheNetworkObject();
        LocalPlayerContext.LocalCharacterChanged += OnLocalCharacterChanged;
        RefreshAnimationBindings("on_enable");
        LogAnimationStatus(
            "animation_initialized",
            force: true,
            reason: "character initialized for animation");
        UpdateAudioListenerState(true);
    }

    private void OnDisable()
    {
        LocalPlayerContext.LocalCharacterChanged -= OnLocalCharacterChanged;
        SetAudioListenerActive(false);
        UnregisterCharacter();
    }

    private void OnTransformChildrenChanged()
    {
        MarkCollidersDirty();
    }

    private void OnTransformParentChanged()
    {
        MarkCollidersDirty();
        RefreshAnimationBindings("transform_parent_changed");
        LogAnimationStatus(
            "animation_references_rebound",
            force: true,
            reason: "Animator references rebound after DDOL migration or parent change");
    }

    private void CacheAudioListener()
    {
        if (audioListener != null)
        {
            return;
        }

        if (searchAudioListenerInChildren)
        {
            audioListener = GetComponentInChildren<AudioListener>(true);
        }
        else
        {
            audioListener = GetComponent<AudioListener>();
        }
    }

    private void CacheNetworkObject()
    {
        if (cachedNetworkObject != null
            && (cachedNetworkObject.transform == transform || transform.IsChildOf(cachedNetworkObject.transform)))
        {
            return;
        }

        cachedNetworkObject = GetComponentInParent<NetworkObject>();
    }

    private void RefreshAnimationBindings(string reason)
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (motionRoot == null)
        {
            motionRoot = transform;
        }

        cachedNetworkObject = null;
        CacheNetworkObject();
        ApplyAnimatorSettings();

        LogAnimationStatus(
            "animation_references_rebound",
            force: true,
            reason: $"animation bindings refreshed reason='{reason}'");
    }

    private void OnLocalCharacterChanged(Transform localCharacterRoot)
    {
        if (!IsSameOrRelatedTransform(transform, localCharacterRoot)
            && !string.Equals(lastAnimationDriverMode, "local", System.StringComparison.Ordinal))
        {
            return;
        }

        RefreshAnimationBindings("local_character_changed");
        LogAnimationStatus(
            "animation_authority_refresh",
            force: true,
            reason: "animation authority refreshed after local assignment change");
    }

    private void UpdateAudioListenerState(bool force)
    {
        if (audioListener == null)
        {
            return;
        }

        if (cachedNetworkObject == null)
        {
            CacheNetworkObject();
        }

        bool shouldBeActive = false;
        if (cachedNetworkObject != null)
        {
            shouldBeActive = cachedNetworkObject.IsSpawned && cachedNetworkObject.IsOwner;
        }
        else
        {
            SquadManager manager = SquadManager.Instance;
            if (manager != null && manager.currentCharacter != null)
            {
                shouldBeActive = transform.IsChildOf(manager.currentCharacter.transform);
            }
        }

        if (!force && shouldBeActive == audioListenerActive)
        {
            return;
        }

        SetAudioListenerActive(shouldBeActive);
    }

    private void SetAudioListenerActive(bool active)
    {
        if (audioListener == null)
        {
            return;
        }

        audioListener.enabled = active;
        audioListenerActive = active;
    }

    public void BindCharacterData(CharacterData data, bool initializeInventory = true)
    {
        if (data != null && SquadManager.Instance != null)
        {
            data = SquadManager.Instance.GetRuntimeCharacter(data);
        }

        characterData = data;
        SyncCharacterInfo(characterData);
        EnsureInventoryList();
        InitializeHealthFromCharacterData(resetHpOnBind);

        if (characterData == null)
        {
            return;
        }

        if (initializeInventory)
        {
            bool forceStarterItems = ShouldForceStarterItems(characterData);
            if (!characterData.inventoryInitialized || forceStarterItems)
            {
                ApplyStarterItems(characterData, true);
                characterData.inventoryInitialized = true;
                SyncTorchStateToCharacterData();
            }
            else
            {
                LoadInventoryFromCharacterData();
            }
        }
        else
        {
            LoadInventoryFromCharacterData();
        }
    }

    private bool ShouldForceStarterItems(CharacterData data)
    {
        if (data == null || data.starterItemsWithQuantity == null || data.starterItemsWithQuantity.Count == 0)
        {
            return false;
        }

        if (data.inventoryItems != null && data.inventoryItems.Count > 0)
        {
            return false;
        }

        if (data.torchSecondsRemaining > 0 || data.torchEquipped)
        {
            return false;
        }

        CharacterStateStore store = CharacterStateStore.Instance;
        if (store != null && store.HasSaveFile)
        {
            return false;
        }

        return true;
    }

    private void SyncCharacterInfo(CharacterData data)
    {
        CharacterInfo[] infos = GetComponentsInChildren<CharacterInfo>(true);
        if (infos == null || infos.Length == 0)
        {
            return;
        }

        for (int i = 0; i < infos.Length; i++)
        {
            infos[i].SetCharacterData(data);
        }
    }

    public void InitializeHealthFromCharacterData(bool resetCurrent)
    {
        int dataHp = characterData != null ? characterData.hp : maxHp;
        maxHp = Mathf.Max(1, dataHp);
        if (resetCurrent)
        {
            currentHp = maxHp;
        }

        if (clampHpToMax)
        {
            currentHp = Mathf.Clamp(currentHp, 0, maxHp);
        }

        NotifyHealthChanged();
    }

    public void SetHealth(int current, int max)
    {
        maxHp = Mathf.Max(1, max);
        currentHp = Mathf.Clamp(current, 0, maxHp);
        NotifyHealthChanged();
    }

    public void SetCurrentHp(int value)
    {
        int clamped = clampHpToMax ? Mathf.Clamp(value, 0, maxHp) : Mathf.Max(0, value);
        if (clamped == currentHp)
        {
            return;
        }

        currentHp = clamped;
        NotifyHealthChanged();
    }

    public void SetMaxHp(int value, bool keepCurrent = true)
    {
        int clamped = Mathf.Max(1, value);
        if (clamped == maxHp && keepCurrent)
        {
            return;
        }

        maxHp = clamped;
        if (!keepCurrent)
        {
            currentHp = maxHp;
        }
        else if (clampHpToMax)
        {
            currentHp = Mathf.Clamp(currentHp, 0, maxHp);
        }

        NotifyHealthChanged();
    }

    public void RestoreHealthToMax()
    {
        SetCurrentHp(maxHp);
    }

    private void NotifyHealthChanged()
    {
        SquadManager manager = SquadManager.Instance;
        if (manager != null)
        {
            manager.NotifyCharacterHealthChanged(this);
        }
    }

    public void AddItem(Item item, int quantity)
    {
        if (item == null)
        {
            return;
        }

        if (quantity <= 0)
        {
            return;
        }

        EnsureInventoryList();
        MarkInventoryInitialized();

        if (IsTorchItem(item))
        {
            if (!items.Contains(item))
            {
                items.Add(item);
            }

            AddTorchSeconds(quantity);
            SyncTorchStateToCharacterData();
            return;
        }

        int clampedQuantity = Mathf.Max(0, quantity);
        for (int i = 0; i < clampedQuantity; i++)
        {
            items.Add(item);
        }
    }

    private void EnsureDynamicMeshCollidersSafe()
    {
        Rigidbody rb = rigidbodyTarget != null ? rigidbodyTarget : GetComponent<Rigidbody>();
        if (rb == null || rb.isKinematic)
        {
            return;
        }

        MeshCollider[] meshColliders = GetComponentsInChildren<MeshCollider>(true);
        for (int i = 0; i < meshColliders.Length; i++)
        {
            MeshCollider meshCollider = meshColliders[i];
            if (meshCollider == null || meshCollider.convex)
            {
                continue;
            }

            meshCollider.convex = true;
        }
    }

    public int TorchSecondsRemaining => Mathf.Max(0, torchSecondsRemaining);

    public Item TorchItem => GetTorchItem();

    public bool HasTorchItem => TorchItem != null;

    public bool IsTorchEquipped => torchEquipped;

    public static IReadOnlyList<SquadCharacterController> ActiveCharacters => registeredCharacters;

    public void ResetTorchToMax(int maxSeconds, bool ensureTorchItem = true)
    {
        int target = maxSeconds > 0 ? maxSeconds : startingTorchSeconds;
        if (target <= 0)
        {
            return;
        }

        EnsureInventoryList();
        MarkInventoryInitialized();

        if (ensureTorchItem && !HasTorchItem)
        {
            Item torchItem = FindTorchItemInCharacterData();
            if (torchItem != null && !items.Contains(torchItem))
            {
                items.Add(torchItem);
            }
        }

        torchSecondsRemaining = Mathf.Max(0, target);
        if (HasTorchItem && torchSecondsRemaining > 0 && !torchEquipped && torchStartsActive)
        {
            SetTorchEquipped(true);
        }

        SyncTorchStateToCharacterData();
    }

    private Item FindTorchItemInCharacterData()
    {
        if (characterData == null)
        {
            return null;
        }

        if (characterData.starterItemsWithQuantity == null)
        {
            return null;
        }

        for (int i = 0; i < characterData.starterItemsWithQuantity.Count; i++)
        {
            CharacterData.StarterItemStack entry = characterData.starterItemsWithQuantity[i];
            Item item = entry != null ? entry.item : null;
            if (IsTorchItem(item))
            {
                return item;
            }
        }

        return null;
    }

    public void ApplyInventoryState(List<Item> newItems, int torchSeconds, bool equipTorch)
    {
        EnsureInventoryList();
        MarkInventoryInitialized();

        if (newItems == null)
        {
            newItems = new List<Item>();
        }

        if (ReferenceEquals(newItems, items))
        {
            newItems = new List<Item>(newItems);
        }

        items.Clear();
        items.AddRange(newItems);
        torchSecondsRemaining = Mathf.Max(0, torchSeconds);
        InitializeTorchState();
        if (HasTorchItem && torchSecondsRemaining > 0)
        {
            SetTorchEquipped(equipTorch);
        }
        else
        {
            SetTorchEquipped(false);
        }

        SyncTorchStateToCharacterData();
    }

    public bool TryUseItem(Item item)
    {
        if (item == null)
        {
            return false;
        }
        return item.TryUse(this);
    }

    public bool TryUseItem(Item item, out string reason)
    {
        if (item == null)
        {
            reason = "Impossible d'utiliser cet objet.";
            return false;
        }

        return item.TryUse(this, out reason);
    }

    public bool TryBreakItem(Item item)
    {
        if (item == null)
        {
            return false;
        }

        if (!item.HasBreakResults())
        {
            return false;
        }

        if (!TryRemoveItemQuantity(item, 1))
        {
            return false;
        }

        List<Item.BreakResult> results = item.breakResults;
        if (results == null)
        {
            return true;
        }

        for (int i = 0; i < results.Count; i++)
        {
            Item.BreakResult result = results[i];
            if (result == null || result.item == null || result.quantity <= 0)
            {
                continue;
            }

            AddItem(result.item, result.quantity);
        }

        return true;
    }

    public bool LearnSkill(Skill skill)
    {
        if (characterData == null || skill == null)
        {
            return false;
        }

        if (characterData.HasSkill(skill))
        {
            return false;
        }

        characterData.AddSkill(skill);
        return true;
    }

    public bool ForgetSkill(Skill skill)
    {
        if (characterData == null || skill == null)
        {
            return false;
        }

        if (!characterData.HasSkill(skill))
        {
            return false;
        }

        characterData.RemoveSkill(skill);
        return true;
    }

    public bool TryRemoveItem(Item item, int count)
    {
        EnsureInventoryList();
        MarkInventoryInitialized();

        if (IsTorchItem(item))
        {
            if (count <= 0)
            {
                return false;
            }

            if (!RemoveTorchItem())
            {
                return false;
            }

            torchSecondsRemaining = 0;
            SetTorchEquipped(false);
            SyncTorchStateToCharacterData();
            return true;
        }

        return ConsumeItem(item, count);
    }

    public bool TryRemoveItemQuantity(Item item, int quantity)
    {
        if (item == null || quantity <= 0)
        {
            return false;
        }

        EnsureInventoryList();
        MarkInventoryInitialized();

        if (IsTorchItem(item))
        {
            if (!HasTorchItem)
            {
                return false;
            }

            int available = Mathf.Max(0, torchSecondsRemaining);
            if (quantity > available)
            {
                return false;
            }

            torchSecondsRemaining = available - quantity;
            if (torchSecondsRemaining <= 0)
            {
                torchSecondsRemaining = 0;
                RemoveTorchItem();
                SetTorchEquipped(false);
            }

            SyncTorchStateToCharacterData();
            return true;
        }

        return ConsumeItem(item, quantity);
    }

    public void ApplyStarterItems(CharacterData data, bool clearExisting = true)
    {
        EnsureInventoryList();
        MarkInventoryInitialized();

        if (clearExisting)
        {
            items.Clear();
            torchSecondsRemaining = 0;
        }

        if (data == null)
        {
            torchSecondsRemaining = 0;
            SetTorchEquipped(false);
            SyncTorchStateToCharacterData();
            return;
        }

        bool hasTorch = false;
        int torchSecondsTarget = 0;
        if (data.starterItemsWithQuantity != null)
        {
            for (int i = 0; i < data.starterItemsWithQuantity.Count; i++)
            {
                CharacterData.StarterItemStack entry = data.starterItemsWithQuantity[i];
                Item item = entry != null ? entry.item : null;
                int quantity = entry != null ? Mathf.Max(0, entry.quantity) : 0;
                if (item == null || quantity <= 0)
                {
                    continue;
                }

                if (IsTorchItem(item))
                {
                    hasTorch = true;
                    if (items == null)
                    {
                        items = new List<Item>();
                    }

                    if (!items.Contains(item))
                    {
                        items.Add(item);
                    }

                    torchSecondsTarget += quantity;
                    continue;
                }

                AddItem(item, quantity);
            }
        }

        if (hasTorch)
        {
            int target = torchSecondsTarget > 0 ? torchSecondsTarget : startingTorchSeconds;
            torchSecondsRemaining = Mathf.Max(torchSecondsRemaining, target);
            InitializeTorchState();
        }
        else
        {
            torchSecondsRemaining = 0;
            SetTorchEquipped(false);
        }

        SyncTorchStateToCharacterData();
    }

    private void EnsureInventoryList()
    {
        if (characterData != null)
        {
            if (characterData.inventoryItems == null)
            {
                characterData.inventoryItems = new List<Item>();
            }

            if (!ReferenceEquals(items, characterData.inventoryItems))
            {
                items = characterData.inventoryItems;
            }

            return;
        }

        if (items == null)
        {
            items = new List<Item>();
        }
    }

    private void MarkInventoryInitialized()
    {
        if (characterData != null)
        {
            characterData.inventoryInitialized = true;
        }
    }

    private void LoadInventoryFromCharacterData()
    {
        if (characterData == null)
        {
            return;
        }

        EnsureInventoryList();
        torchSecondsRemaining = Mathf.Max(0, characterData.torchSecondsRemaining);
        InitializeTorchState();
        if (HasTorchItem && torchSecondsRemaining > 0)
        {
            SetTorchEquipped(characterData.torchEquipped);
        }
        else
        {
            SetTorchEquipped(false);
        }
    }

    private void SyncTorchStateToCharacterData()
    {
        if (characterData == null)
        {
            return;
        }

        characterData.torchSecondsRemaining = Mathf.Max(0, torchSecondsRemaining);
        characterData.torchEquipped = torchEquipped;
        characterData.inventoryInitialized = true;
    }

    private void SyncTorchStateToCharacterDataIfChanged(int prevSeconds, bool prevEquipped)
    {
        if (prevSeconds == torchSecondsRemaining && prevEquipped == torchEquipped)
        {
            return;
        }

        SyncTorchStateToCharacterData();
    }

    private void OnValidate()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (rigidbodyTarget == null)
        {
            rigidbodyTarget = GetComponent<Rigidbody>();
        }

        if (stepCapsule == null)
        {
            stepCapsule = GetComponent<CapsuleCollider>();
        }

        if (motionRoot == null)
        {
            motionRoot = transform;
        }

        acceleration = Mathf.Max(0f, acceleration);
        deceleration = Mathf.Max(0f, deceleration);
        inputResponsiveness = Mathf.Max(0f, inputResponsiveness);
        walkSpeedThreshold = Mathf.Max(0f, walkSpeedThreshold);
        runSpeedThreshold = Mathf.Max(walkSpeedThreshold, runSpeedThreshold);
        speedDampTime = Mathf.Max(0f, speedDampTime);
        stepHeight = Mathf.Max(0f, stepHeight);
        stepCheckDistance = Mathf.Max(0f, stepCheckDistance);
        stepUpSpeed = Mathf.Max(0f, stepUpSpeed);
        stepDownHeight = Mathf.Max(0f, stepDownHeight);
        stepDownSpeed = Mathf.Max(0f, stepDownSpeed);
        stepUpSmoothTime = Mathf.Max(0f, stepUpSmoothTime);
        stepDownSmoothTime = Mathf.Max(0f, stepDownSmoothTime);
        stepHeightTolerance = Mathf.Max(0f, stepHeightTolerance);
        stepMinHeight = Mathf.Max(0f, stepMinHeight);
        stepMaxUpVelocity = Mathf.Max(0f, stepMaxUpVelocity);
        stepUpperRadius = Mathf.Max(0f, stepUpperRadius);
        stepUpperHeightOffset = Mathf.Max(0f, stepUpperHeightOffset);
        stepForwardBoost = Mathf.Max(0f, stepForwardBoost);
        stepRadiusPadding = Mathf.Max(0f, stepRadiusPadding);
        stepGroundCheckDistance = Mathf.Max(0f, stepGroundCheckDistance);
        jumpGroundCheckDistance = Mathf.Max(0.02f, jumpGroundCheckDistance);
        jumpGroundCheckRadiusScale = Mathf.Clamp(jumpGroundCheckRadiusScale, 0.1f, 1.5f);
        groundedStickVelocity = Mathf.Max(0f, groundedStickVelocity);
        footIkWeight = Mathf.Clamp01(footIkWeight);
        footIkPositionWeight = Mathf.Clamp01(footIkPositionWeight);
        footIkRotationWeight = Mathf.Clamp01(footIkRotationWeight);
        footIkSpeedThreshold = Mathf.Max(0f, footIkSpeedThreshold);
        footIkBlendSpeed = Mathf.Max(0f, footIkBlendSpeed);
        footIkHeightOffset = Mathf.Max(0f, footIkHeightOffset);
        footIkRaycastUp = Mathf.Max(0.02f, footIkRaycastUp);
        footIkRaycastDown = Mathf.Max(0.02f, footIkRaycastDown);
        voidCheckDistance = Mathf.Max(0f, voidCheckDistance);
        voidCheckDepth = Mathf.Max(0.02f, voidCheckDepth);

        ValidateCommittedJumpSettings();
        ApplyAnimatorSettings();
    }

    public void ToggleTorch()
    {
        if (!allowTorchToggle)
        {
            return;
        }

        if (!HasTorchItem)
        {
            return;
        }

        if (!torchEquipped && torchSecondsRemaining <= 0)
        {
            return;
        }

        EnsureTorchCached();
        if (torchTransform == null)
        {
            return;
        }

        SetTorchEquipped(!torchEquipped);
    }

    public void ApplyTorchState(int torchSeconds, bool equipTorch)
    {
        torchSecondsRemaining = Mathf.Max(0, torchSeconds);

        if (HasTorchItem && torchSecondsRemaining > 0)
        {
            SetTorchEquipped(equipTorch);
        }
        else
        {
            SetTorchEquipped(false);
        }
    }

    public void Move(Vector2 input)
    {
        moveInputIsWorldSpace = false;
        moveInput = input;
    }

    public void MoveWorld(Vector2 worldInput)
    {
        moveInputIsWorldSpace = true;
        moveInput = worldInput;
    }

    public void SetLocalAnimationPreview(Vector2 worldInput)
    {
        animationPreviewInput = Vector2.ClampMagnitude(worldInput, 1f);
    }

    public void Jump()
    {
        RequestCommittedJump();
    }

    public void ClearLocalAnimationPreview()
    {
        animationPreviewInput = Vector2.zero;
        smoothedAnimationPreviewInput = Vector2.zero;
    }

    public void Stop()
    {
        moveInputIsWorldSpace = false;
        moveInput = Vector2.zero;
        smoothedInput = Vector2.zero;
        currentHorizontalVelocity = Vector3.zero;
        ClearLocalAnimationPreview();
        SetSpeed(0f);
    }

    private void FixedUpdate()
    {
        UpdateInputLock(Time.fixedDeltaTime);
        SmoothInput(Time.fixedDeltaTime);
        SmoothAnimationPreview(Time.fixedDeltaTime);
        UpdateGroundedState();
        UpdateCommittedJump(Time.fixedDeltaTime);
        ApplyMovement(Time.fixedDeltaTime);
        UpdateAnimationSpeed();
        UpdateCommittedJumpAnimation();
    }

    private bool IsGroundAhead(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.01f)
        {
            return true;
        }

        Vector3 up = transform.up;
        Vector3 origin = GetWorldPosition();
        float radius = 0.2f;

        if (characterController != null)
        {
            Bounds bounds = characterController.bounds;
            radius = Mathf.Max(0.01f, characterController.radius);
            origin = new Vector3(bounds.center.x, bounds.min.y + radius, bounds.center.z);
        }
        else if (TryGetStepCapsule(out Vector3 center, out float capRadius, out float height))
        {
            radius = Mathf.Max(0.01f, capRadius);
            float halfHeight = height * 0.5f;
            float bottomOffset = Mathf.Max(0f, halfHeight - radius);
            origin = center - up * bottomOffset;
        }

        float checkDistance = radius + Mathf.Max(0f, voidCheckDistance);
        Vector3 checkPos = origin + up * 0.05f + direction.normalized * checkDistance;

        int mask = GetVoidGroundMask();
        float depth = Mathf.Max(0.02f, voidCheckDepth);
        return Physics.Raycast(checkPos, -up, depth, mask, QueryTriggerInteraction.Ignore);
    }

    private int GetVoidGroundMask()
    {
        if (!voidUseCollisionMatrixMask)
        {
            return voidGroundMask;
        }

        return GetCollisionMatrixMask();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (animator == null)
        {
            return;
        }

        if (!enableFootIk)
        {
            SetFootIkWeights(0f, 0f);
            return;
        }

        if (ShouldSuppressFootIkForCommittedJump())
        {
            SetFootIkWeights(0f, 0f);
            return;
        }

        if (!isGrounded)
        {
            SetFootIkWeights(0f, 0f);
            return;
        }

        float speed = GetCurrentHorizontalVelocity().magnitude;
        float targetWeight = speed <= footIkSpeedThreshold ? footIkWeight : 0f;
        if (footIkBlendSpeed > 0f)
        {
            footIkWeightCurrent = Mathf.MoveTowards(footIkWeightCurrent, targetWeight, footIkBlendSpeed * Time.deltaTime);
        }
        else
        {
            footIkWeightCurrent = targetWeight;
        }

        if (footIkWeightCurrent <= 0.0001f)
        {
            SetFootIkWeights(0f, 0f);
            return;
        }

        int mask = GetFootIkMask();
        ApplyFootIk(AvatarIKGoal.LeftFoot, HumanBodyBones.LeftFoot, mask, footIkWeightCurrent);
        ApplyFootIk(AvatarIKGoal.RightFoot, HumanBodyBones.RightFoot, mask, footIkWeightCurrent);
    }

    private void UpdateInputLock(float deltaTime)
    {
        if (inputLockTimer <= 0f)
        {
            return;
        }

        inputLockTimer = Mathf.Max(0f, inputLockTimer - deltaTime);
    }

    private void ApplyMovement(float deltaTime)
    {
        Vector2 effectiveInput = smoothedInput;
        bool hasInput = effectiveInput.sqrMagnitude > 0.0001f;
        Vector3 desiredVelocity = Vector3.zero;
        Vector3 desiredDirection = Vector3.zero;
        if (hasInput)
        {
            desiredDirection = GetMoveDirection(effectiveInput);
            if (desiredDirection.sqrMagnitude > 0.0001f)
            {
                desiredDirection = desiredDirection.normalized;
            }

            desiredVelocity = desiredDirection * (Mathf.Clamp01(effectiveInput.magnitude) * moveSpeed);
        }

        if (enableVoidDetection && hasInput && !IsGroundAhead(desiredDirection))
        {
            hasInput = false;
            desiredVelocity = Vector3.zero;
            StopHorizontalVelocity();
        }

        if (TryApplyCommittedJumpMovement(deltaTime))
        {
            ResetStepAssistSmoothing();
            return;
        }

        if (inputLockTimer > 0f)
        {
            ResetStepAssistSmoothing();
            return;
        }

        if (ShouldUseRigidbody())
        {
            Vector3 currentVelocity = rigidbodyTarget.linearVelocity;
            Vector3 currentHorizontal = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            Vector3 targetHorizontal = new Vector3(desiredVelocity.x, 0f, desiredVelocity.z);
            float newVertical = currentVelocity.y;
            Vector3 newHorizontal = hasInput
                ? Vector3.MoveTowards(currentHorizontal, targetHorizontal, acceleration * deltaTime)
                : Vector3.Lerp(currentHorizontal, Vector3.zero, 1f - Mathf.Exp(-deceleration * deltaTime));

            rigidbodyTarget.linearVelocity = new Vector3(newHorizontal.x, newVertical, newHorizontal.z);
            currentHorizontalVelocity = newHorizontal;

            if (hasInput)
            {
                TryStepAssistRigidbody(newHorizontal, deltaTime);
            }
            else
            {
                ResetStepAssistSmoothing();
            }

            if (rotateToInput && desiredVelocity.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(desiredVelocity);
                rigidbodyTarget.MoveRotation(
                    Quaternion.Slerp(rigidbodyTarget.rotation, targetRotation, rotationSpeed * deltaTime));
            }
            return;
        }

        ResetStepAssistSmoothing();

        Vector3 newKinematicHorizontal = hasInput
            ? Vector3.MoveTowards(currentHorizontalVelocity, new Vector3(desiredVelocity.x, 0f, desiredVelocity.z), acceleration * deltaTime)
            : Vector3.Lerp(currentHorizontalVelocity, Vector3.zero, 1f - Mathf.Exp(-deceleration * deltaTime));
        currentHorizontalVelocity = newKinematicHorizontal;

        if (characterController != null)
        {
            characterController.Move(newKinematicHorizontal * deltaTime);

            if (rotateToInput && desiredVelocity.sqrMagnitude > 0.0001f)
            {
                Transform target = motionRoot != null ? motionRoot : transform;
                Quaternion targetRotation = Quaternion.LookRotation(desiredVelocity);
                target.rotation = Quaternion.Slerp(target.rotation, targetRotation, rotationSpeed * deltaTime);
            }
            return;
        }

        Transform root = motionRoot != null ? motionRoot : transform;
        root.position += newKinematicHorizontal * deltaTime;
        if (rotateToInput && desiredVelocity.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(desiredVelocity);
            root.rotation = Quaternion.Slerp(root.rotation, targetRotation, rotationSpeed * deltaTime);
        }
    }

    private void StopHorizontalVelocity()
    {
        currentHorizontalVelocity = Vector3.zero;

        if (ShouldUseRigidbody() && rigidbodyTarget != null)
        {
            Vector3 velocity = rigidbodyTarget.linearVelocity;
            rigidbodyTarget.linearVelocity = new Vector3(0f, velocity.y, 0f);
        }
    }

    private struct StepGroundSample
    {
        public Vector3 point;
        public Vector3 normal;
        public Collider collider;
    }

    private void TryStepAssistRigidbody(Vector3 horizontalVelocity, float deltaTime)
    {
        if (!enableStepAssist || rigidbodyTarget == null)
        {
            ResetStepAssistSmoothing();
            LogStepDebug("StepAssist: desactive ou Rigidbody manquant.");
            return;
        }

        if (stepHeight <= 0f || stepCheckDistance <= 0f)
        {
            ResetStepAssistSmoothing();
            LogStepDebug("StepAssist: parametres invalides (stepHeight/stepCheckDistance).");
            return;
        }

        if (stepUpSpeed <= 0f)
        {
            ResetStepAssistSmoothing();
            LogStepDebug("StepAssist: stepUpSpeed <= 0.");
            return;
        }

        if (stepMaxUpVelocity > 0f && rigidbodyTarget.linearVelocity.y > stepMaxUpVelocity)
        {
            ResetStepAssistSmoothing();
            LogStepDebug($"StepAssist: vitesse verticale > {stepMaxUpVelocity:F2} (en saut/chute).");
            return;
        }

        if (!TryGetStepCapsule(out Vector3 center, out float radius, out float height))
        {
            ResetStepAssistSmoothing();
            LogStepDebug("StepAssist: CapsuleCollider manquant ou direction != Y.");
            return;
        }

        Vector3 moveDir = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z);
        float speed = moveDir.magnitude;
        if (speed < 0.0001f)
        {
            ResetStepAssistSmoothing();
            LogStepDebug("StepAssist: pas de mouvement horizontal.");
            return;
        }

        moveDir /= speed;
        Vector3 up = transform.up;
        float halfHeight = height * 0.5f;
        float bottomOffset = Mathf.Max(0f, halfHeight - radius);
        Vector3 bottomCenter = center - up * bottomOffset;
        Vector3 foot = bottomCenter - up * radius;
        int walkableMask = GetVoidGroundMask();
        int stairMask = GetStepMask();
        int blockingMask = GetStepBlockingMask();

        if (stairMask == 0)
        {
            ResetStepAssistSmoothing();
            LogStepDebug("StepAssist: aucun layer d'escalier valide.");
            return;
        }

        if (requireGroundForStep && !IsGroundedForStep(bottomCenter, radius, up, walkableMask))
        {
            ResetStepAssistSmoothing();
            LogStepDebug("StepAssist: pas au sol (ground check).");
            return;
        }

        float maxUp = stepHeight + stepHeightTolerance;
        float maxDown = (stepDownHeight > 0f ? stepDownHeight : stepHeight) + stepHeightTolerance;
        if (!TryEvaluateStairAssistTarget(
                foot,
                moveDir,
                up,
                radius,
                maxUp,
                maxDown,
                walkableMask,
                stairMask,
                out float targetHeightDelta,
                out bool currentOnStairs))
        {
            ResetStepAssistSmoothing();
            LogStepDebug("StepAssist: aucune trajectoire d'escalier valide.");
            return;
        }

        float deadZone = currentOnStairs ? StepAssistSurfaceDeadZone : Mathf.Max(0.001f, stepMinHeight);
        if (Mathf.Abs(targetHeightDelta) <= deadZone)
        {
            ResetStepAssistSmoothing();
            LogStepDebug("StepAssist: correction verticale trop faible.");
            return;
        }

        float currentFootHeight = Vector3.Dot(foot, up);

        if (targetHeightDelta > 0f)
        {
            if (!HasStepClearance(bottomCenter, radius, height, up, moveDir, targetHeightDelta, Mathf.Max(0.02f, stepCheckDistance), blockingMask))
            {
                ResetStepAssistSmoothing();
                LogStepDebug("StepAssist: obstacle detecte au-dessus de l'escalier.");
                return;
            }

            float stepAmount = ComputeSmoothedStepOffset(
                currentFootHeight,
                currentFootHeight + targetHeightDelta,
                stepUpSmoothTime,
                stepUpSpeed,
                deltaTime);
            if (stepAmount <= 0.0001f)
            {
                LogStepDebug("StepAssist: lissage up nul.");
                return;
            }

            ApplyStepOffset(up, moveDir, stepAmount, true);
            LogStepDebug($"StepAssist: suivi escalier up ({stepAmount:F3}m, cible {targetHeightDelta:F3}m).");
            return;
        }

        float dropAmount = -targetHeightDelta;
        float downSpeed = stepDownSpeed > 0f ? stepDownSpeed : stepUpSpeed;
        float appliedDrop = ComputeSmoothedStepOffset(
            currentFootHeight,
            currentFootHeight - dropAmount,
            stepDownSmoothTime,
            downSpeed,
            deltaTime);
        if (appliedDrop <= 0.0001f)
        {
            LogStepDebug("StepAssist: lissage down nul.");
            return;
        }

        ApplyStepOffset(up, moveDir, appliedDrop, false);
        LogStepDebug($"StepAssist: suivi escalier down ({appliedDrop:F3}m, cible {dropAmount:F3}m).");
    }

    private float ComputeSmoothedStepOffset(float currentHeight, float targetHeight, float smoothTime, float maxSpeed, float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return 0f;
        }

        float delta = targetHeight - currentHeight;
        if (Mathf.Abs(delta) <= 0.0001f)
        {
            stepVerticalSmoothVelocity = 0f;
            return 0f;
        }

        float direction = Mathf.Sign(delta);
        if (stepVerticalSmoothVelocity != 0f && Mathf.Sign(stepVerticalSmoothVelocity) != direction)
        {
            stepVerticalSmoothVelocity = 0f;
        }

        float clampedSpeed = Mathf.Max(0.01f, maxSpeed);
        float nextHeight = smoothTime > 0f
            ? Mathf.SmoothDamp(currentHeight, targetHeight, ref stepVerticalSmoothVelocity, smoothTime, clampedSpeed, deltaTime)
            : Mathf.MoveTowards(currentHeight, targetHeight, clampedSpeed * deltaTime);

        float applied = direction > 0f
            ? Mathf.Max(0f, nextHeight - currentHeight)
            : Mathf.Max(0f, currentHeight - nextHeight);

        return Mathf.Min(applied, Mathf.Abs(delta));
    }

    private bool TryEvaluateStairAssistTarget(
        Vector3 foot,
        Vector3 moveDir,
        Vector3 up,
        float radius,
        float maxUp,
        float maxDown,
        int walkableMask,
        int stairMask,
        out float targetHeightDelta,
        out bool currentOnStairs)
    {
        targetHeightDelta = 0f;
        currentOnStairs = false;

        float probeUp = maxUp + stepGroundCheckDistance;
        float probeDown = maxDown + stepGroundCheckDistance;
        if (!TrySampleGround(foot, up, probeUp, probeDown, walkableMask, false, out StepGroundSample currentGround))
        {
            return false;
        }

        currentOnStairs = IsStepSurfaceCollider(currentGround.collider);
        if (TryComputeAheadStairDelta(foot, moveDir, up, radius, probeUp, probeDown, maxUp, maxDown, stairMask, currentGround, currentOnStairs, out targetHeightDelta))
        {
            return true;
        }

        if (!currentOnStairs)
        {
            return false;
        }

        return TryComputeAheadLandingDelta(foot, moveDir, up, radius, probeUp, probeDown, maxUp, maxDown, walkableMask, currentGround, out targetHeightDelta);
    }

    private bool TryComputeAheadStairDelta(
        Vector3 foot,
        Vector3 moveDir,
        Vector3 up,
        float radius,
        float probeUp,
        float probeDown,
        float maxUp,
        float maxDown,
        int stairMask,
        StepGroundSample currentGround,
        bool currentOnStairs,
        out float targetHeightDelta)
    {
        targetHeightDelta = 0f;
        float startDistance = radius + Mathf.Max(0.02f, stepCheckDistance * 0.35f);
        float endDistance = radius + Mathf.Max(0.02f, stepCheckDistance);
        float weightedDelta = 0f;
        float totalWeight = 0f;

        for (int i = 0; i < StepAssistLookAheadSampleCount; i++)
        {
            float t = StepAssistLookAheadSampleCount == 1 ? 1f : (float)i / (StepAssistLookAheadSampleCount - 1);
            float distance = Mathf.Lerp(startDistance, endDistance, t);
            Vector3 sampleOrigin = foot + moveDir * distance;
            if (!TrySampleGround(sampleOrigin, up, probeUp, probeDown, stairMask, true, out StepGroundSample stairGround))
            {
                continue;
            }

            float delta = Vector3.Dot(stairGround.point - currentGround.point, up);
            if (delta > maxUp + 0.01f || delta < -maxDown - 0.01f)
            {
                continue;
            }

            if (!currentOnStairs && delta < Mathf.Max(0.001f, stepMinHeight))
            {
                continue;
            }

            float weight = 1f - (t * 0.7f);
            weightedDelta += delta * weight;
            totalWeight += weight;
        }

        if (totalWeight <= 0f)
        {
            return false;
        }

        targetHeightDelta = weightedDelta / totalWeight;
        return true;
    }

    private bool TryComputeAheadLandingDelta(
        Vector3 foot,
        Vector3 moveDir,
        Vector3 up,
        float radius,
        float probeUp,
        float probeDown,
        float maxUp,
        float maxDown,
        int walkableMask,
        StepGroundSample currentGround,
        out float targetHeightDelta)
    {
        targetHeightDelta = 0f;
        float startDistance = radius + Mathf.Max(0.02f, stepCheckDistance * 0.35f);
        float endDistance = radius + Mathf.Max(0.02f, stepCheckDistance);

        for (int i = 0; i < StepAssistLookAheadSampleCount; i++)
        {
            float t = StepAssistLookAheadSampleCount == 1 ? 1f : (float)i / (StepAssistLookAheadSampleCount - 1);
            float distance = Mathf.Lerp(startDistance, endDistance, t);
            Vector3 sampleOrigin = foot + moveDir * distance;
            if (!TrySampleGround(sampleOrigin, up, probeUp, probeDown, walkableMask, false, out StepGroundSample aheadGround))
            {
                continue;
            }

            if (IsStepSurfaceCollider(aheadGround.collider))
            {
                continue;
            }

            float delta = Vector3.Dot(aheadGround.point - currentGround.point, up);
            if (delta > maxUp + 0.01f || delta < -maxDown - 0.01f)
            {
                continue;
            }

            targetHeightDelta = delta;
            return true;
        }

        return false;
    }

    private bool TrySampleGround(Vector3 origin, Vector3 up, float maxUp, float maxDown, int mask, bool requireStepSurface, out StepGroundSample sample)
    {
        sample = default;
        float upRange = Mathf.Max(0.02f, maxUp);
        float downRange = Mathf.Max(0.02f, maxDown);
        float rayStart = upRange + 0.05f;
        float rayDistance = upRange + downRange + 0.1f;
        Vector3 rayOrigin = origin + up * rayStart;

        int hitCount = Physics.RaycastNonAlloc(rayOrigin, -up, stepCastHits, rayDistance, mask, QueryTriggerInteraction.Ignore);
        float bestDistance = float.PositiveInfinity;
        int bestIndex = -1;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = stepCastHits[i].collider;
            if (col == null || IsSelfCollider(col))
            {
                continue;
            }

            if (requireStepSurface && !IsStepSurfaceCollider(col))
            {
                continue;
            }

            float heightOffset = Vector3.Dot(stepCastHits[i].point - origin, up);
            if (heightOffset > upRange || heightOffset < -downRange)
            {
                continue;
            }

            if (Vector3.Dot(stepCastHits[i].normal, up) <= 0.1f)
            {
                continue;
            }

            float d = stepCastHits[i].distance;
            if (d < bestDistance)
            {
                bestDistance = d;
                bestIndex = i;
            }
        }

        if (bestIndex >= 0)
        {
            RaycastHit bestHit = stepCastHits[bestIndex];
            sample.point = bestHit.point;
            sample.normal = bestHit.normal;
            sample.collider = bestHit.collider;
            return true;
        }

        return false;
    }

    private bool IsStepSurfaceCollider(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        int stairMask = GetStepMask();
        if (stairMask == 0)
        {
            return false;
        }

        int colliderLayerBit = 1 << collider.gameObject.layer;
        return (stairMask & colliderLayerBit) != 0;
    }

    private void UpdateGroundedState()
    {
        if (Time.time < groundIgnoreUntilTime)
        {
            isGrounded = false;
            return;
        }

        if (characterController != null && !ShouldUseRigidbody())
        {
            isGrounded = characterController.isGrounded;
            if (isGrounded)
            {
                lastGroundedTime = Time.time;
            }

            return;
        }

        if (!ShouldUseRigidbody())
        {
            isGrounded = false;
            return;
        }

        isGrounded = CheckRigidbodyGrounded();
        if (isGrounded)
        {
            lastGroundedTime = Time.time;
        }
    }

    private bool CheckRigidbodyGrounded()
    {
        Vector3 up = transform.up;
        if (TryGetStepCapsule(out Vector3 center, out float radius, out float height))
        {
            float probeRadius = Mathf.Max(0.05f, radius * jumpGroundCheckRadiusScale);
            float halfHeight = height * 0.5f;
            float bottomOffset = Mathf.Max(0f, halfHeight - radius);
            Vector3 bottom = center - up * bottomOffset;
            Vector3 origin = bottom + up * 0.02f;
            float distance = Mathf.Max(0.02f, jumpGroundCheckDistance);
            int hitCount = Physics.SphereCastNonAlloc(origin, probeRadius, -up, stepCastHits, distance, GetVoidGroundMask(), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                Collider col = stepCastHits[i].collider;
                if (col == null || IsSelfCollider(col))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        Vector3 fallbackOrigin = GetWorldPosition() + up * 0.1f;
        return Physics.Raycast(fallbackOrigin, -up, Mathf.Max(0.02f, jumpGroundCheckDistance + 0.1f), GetVoidGroundMask(), QueryTriggerInteraction.Ignore);
    }

    private bool HasStepClearance(Vector3 bottomCenter, float radius, float height, Vector3 up, Vector3 moveDir, float stepUp, float castDistance, int mask)
    {
        if (mask == 0)
        {
            return true;
        }

        float castRadius = Mathf.Max(0.01f, radius - stepRadiusPadding);
        float upperRadius = stepUpperRadius > 0f ? stepUpperRadius : castRadius;
        float capsuleSegment = Mathf.Max(0f, height - (radius * 2f));
        Vector3 upperBottom = bottomCenter + up * (stepUp + upperRadius + stepUpperHeightOffset);
        Vector3 upperTop = upperBottom + up * capsuleSegment;

        bool upperHit = CapsuleCastForStep(upperBottom, upperTop, upperRadius, moveDir, castDistance, mask, out _);
        if (!upperHit && OverlapCapsuleForStep(upperBottom, upperTop, upperRadius, mask))
        {
            upperHit = true;
        }

        return !upperHit;
    }

    private void ApplyStepOffset(Vector3 up, Vector3 moveDir, float amount, bool stepUp)
    {
        if (amount <= 0f || rigidbodyTarget == null)
        {
            return;
        }

        Vector3 targetPosition = rigidbodyTarget.position + (stepUp ? up : -up) * amount;
        if (stepForwardBoost > 0f)
        {
            targetPosition += moveDir * stepForwardBoost;
        }

        rigidbodyTarget.MovePosition(targetPosition);

        Vector3 velocity = rigidbodyTarget.linearVelocity;
        if (stepUp)
        {
            if (velocity.y < 0f)
            {
                velocity.y = 0f;
                rigidbodyTarget.linearVelocity = velocity;
            }
        }
        else
        {
            if (velocity.y > 0f)
            {
                velocity.y = 0f;
                rigidbodyTarget.linearVelocity = velocity;
            }
        }
    }

    private void ResetStepAssistSmoothing()
    {
        stepVerticalSmoothVelocity = 0f;
    }

    private void LogStepDebug(string message)
    {
        if (!stepDebugLogs)
        {
            return;
        }

        float now = Time.time;
        if (now < nextStepDebugTime)
        {
            return;
        }

        nextStepDebugTime = now + Mathf.Max(0.05f, stepDebugCooldown);
        Debug.Log($"{name} | {message}", this);
    }

    private void SetFootIkWeights(float positionWeight, float rotationWeight)
    {
        animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, positionWeight);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, rotationWeight);
        animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, positionWeight);
        animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, rotationWeight);
    }

    private void ApplyFootIk(AvatarIKGoal goal, HumanBodyBones bone, int mask, float baseWeight)
    {
        Transform boneTransform = animator.GetBoneTransform(bone);
        if (boneTransform == null)
        {
            animator.SetIKPositionWeight(goal, 0f);
            animator.SetIKRotationWeight(goal, 0f);
            return;
        }

        Vector3 up = transform.up;
        Vector3 origin = boneTransform.position + up * footIkRaycastUp;
        float maxDistance = footIkRaycastUp + footIkRaycastDown;

        if (!Physics.Raycast(origin, -up, out RaycastHit hit, maxDistance, mask, QueryTriggerInteraction.Ignore))
        {
            animator.SetIKPositionWeight(goal, 0f);
            animator.SetIKRotationWeight(goal, 0f);
            return;
        }

        if (Vector3.Dot(hit.normal, up) <= 0.1f)
        {
            animator.SetIKPositionWeight(goal, 0f);
            animator.SetIKRotationWeight(goal, 0f);
            return;
        }

        float positionWeight = baseWeight * footIkPositionWeight;
        float rotationWeight = baseWeight * footIkRotationWeight;
        Vector3 footPosition = hit.point + up * footIkHeightOffset;

        Vector3 forward = Vector3.ProjectOnPlane(boneTransform.forward, hit.normal);
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.ProjectOnPlane(transform.forward, hit.normal);
        }
        forward.Normalize();

        Quaternion footRotation = Quaternion.LookRotation(forward, hit.normal);

        animator.SetIKPositionWeight(goal, positionWeight);
        animator.SetIKRotationWeight(goal, rotationWeight);
        animator.SetIKPosition(goal, footPosition);
        animator.SetIKRotation(goal, footRotation);
    }

    private int GetFootIkMask()
    {
        if (!footIkUseCollisionMatrixMask)
        {
            return footIkLayerMask;
        }

        return GetCollisionMatrixMask();
    }

    private int GetCollisionMatrixMask()
    {
        int mask = 0;
        int layer = gameObject.layer;
        for (int i = 0; i < 32; i++)
        {
            if (!Physics.GetIgnoreLayerCollision(layer, i))
            {
                mask |= 1 << i;
            }
        }

        return mask;
    }

    private bool TryGetStepCapsule(out Vector3 center, out float radius, out float height)
    {
        CapsuleCollider capsule = stepCapsule;
        if (capsule == null)
        {
            capsule = GetComponent<CapsuleCollider>();
            stepCapsule = capsule;
        }

        if (capsule == null || capsule.direction != 1)
        {
            center = Vector3.zero;
            radius = 0f;
            height = 0f;
            return false;
        }

        Vector3 scale = transform.lossyScale;
        float maxXZ = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float absY = Mathf.Abs(scale.y);
        radius = capsule.radius * maxXZ;
        height = Mathf.Max(capsule.height * absY, radius * 2f);
        center = transform.TransformPoint(capsule.center);
        return true;
    }

    private int GetStepMask()
    {
        int mask = stepLayerMask.value;
        if (mask == 0)
        {
            return 0;
        }

        if (!stepUseCollisionMatrixMask)
        {
            return mask;
        }

        return mask & GetCollisionMatrixMask();
    }

    private int GetStepBlockingMask()
    {
        return GetCollisionMatrixMask() & ~GetStepMask();
    }

    private bool IsGroundedForStep(Vector3 bottom, float radius, Vector3 up, int mask)
    {
        float checkDistance = Mathf.Max(0.02f, stepGroundCheckDistance);
        Vector3 origin = bottom + up * 0.02f;

        if (OverlapForStep(origin, radius * 0.9f, mask))
        {
            return true;
        }

        int hitCount = Physics.SphereCastNonAlloc(origin, radius * 0.9f, -up, stepCastHits, checkDistance, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = stepCastHits[i].collider;
            if (col == null || IsSelfCollider(col))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool OverlapForStep(Vector3 origin, float radius, int mask)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(origin, radius, stepOverlapHits, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = stepOverlapHits[i];
            if (col == null || IsSelfCollider(col))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool OverlapCapsuleForStep(Vector3 point1, Vector3 point2, float radius, int mask)
    {
        int hitCount = Physics.OverlapCapsuleNonAlloc(point1, point2, radius, stepOverlapHits, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = stepOverlapHits[i];
            if (col == null || IsSelfCollider(col))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool CapsuleCastForStep(Vector3 point1, Vector3 point2, float radius, Vector3 direction, float distance, int mask, out RaycastHit hit)
    {
        int hitCount = Physics.CapsuleCastNonAlloc(point1, point2, radius, direction, stepCastHits, distance, mask, QueryTriggerInteraction.Ignore);
        float bestDistance = float.PositiveInfinity;
        int bestIndex = -1;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = stepCastHits[i].collider;
            if (col == null || IsSelfCollider(col))
            {
                continue;
            }

            float d = stepCastHits[i].distance;
            if (d < bestDistance)
            {
                bestDistance = d;
                bestIndex = i;
            }
        }

        if (bestIndex >= 0)
        {
            hit = stepCastHits[bestIndex];
            return true;
        }

        hit = default;
        return false;
    }

    private bool IsSelfCollider(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        Transform t = collider.transform;
        return t == transform || t.IsChildOf(transform);
    }

    private void SetSpeed(float speed)
    {
        if (animator == null || string.IsNullOrWhiteSpace(speedParam))
        {
            return;
        }

        if (useSpeedDamping)
        {
            float deltaTime = Time.inFixedTimeStep ? Time.fixedDeltaTime : Time.deltaTime;
            animator.SetFloat(speedParam, speed, speedDampTime, deltaTime);
        }
        else
        {
            animator.SetFloat(speedParam, speed);
        }
    }

    private void InitializeTorchState()
    {
        torchInitialized = false;
        EnsureTorchCached();
        ClearPendingTorchVisualTransition();

        if (torchTransform == null)
        {
            return;
        }

        if (!HasTorchItem)
        {
            torchEquipped = false;
            ApplyTorchVisualState(false);
            if (animator != null && !string.IsNullOrWhiteSpace(torchBoolParam))
            {
                animator.SetBool(torchBoolParam, false);
                SyncTorchAnimationStateImmediate();
            }
            UpdateTorchAnimationLayerWeight(immediate: true);
            return;
        }

        if (initializeTorchFromHierarchy)
        {
            torchEquipped = torchTransform.gameObject.activeSelf;
        }
        else
        {
            torchEquipped = torchStartsActive;
        }

        ApplyTorchVisualState(torchEquipped);

        if (animator != null && !string.IsNullOrWhiteSpace(torchBoolParam))
        {
            animator.SetBool(torchBoolParam, torchEquipped);
            SyncTorchAnimationStateImmediate();
        }

        UpdateTorchAnimationLayerWeight(immediate: true);

        if (torchSecondsRemaining <= 0 && torchEquipped)
        {
            torchEquipped = false;
            ApplyTorchVisualState(false);
            if (animator != null && !string.IsNullOrWhiteSpace(torchBoolParam))
            {
                animator.SetBool(torchBoolParam, false);
                SyncTorchAnimationStateImmediate();
            }
            UpdateTorchAnimationLayerWeight(immediate: true);
        }
    }

    private void UpdateTorchLifetime(float deltaTime)
    {
        int prevSeconds = torchSecondsRemaining;
        bool prevEquipped = torchEquipped;

        if (!Zone.ShouldConsumeTorch(gameObject))
        {
            torchDrainTimer = 0f;
            SyncTorchStateToCharacterDataIfChanged(prevSeconds, prevEquipped);
            return;
        }

        if (!torchEquipped)
        {
            torchDrainTimer = 0f;
            SyncTorchStateToCharacterDataIfChanged(prevSeconds, prevEquipped);
            return;
        }

        if (!HasTorchItem)
        {
            torchDrainTimer = 0f;
            SetTorchEquipped(false);
            SyncTorchStateToCharacterDataIfChanged(prevSeconds, prevEquipped);
            return;
        }

        if (torchSecondsRemaining <= 0)
        {
            SetTorchEquipped(false);
            SyncTorchStateToCharacterDataIfChanged(prevSeconds, prevEquipped);
            return;
        }

        torchDrainTimer += deltaTime;
        while (torchDrainTimer >= 1f && torchSecondsRemaining > 0)
        {
            torchSecondsRemaining -= 1;
            torchDrainTimer -= 1f;
        }

        if (torchSecondsRemaining <= 0)
        {
            torchSecondsRemaining = 0;
            SetTorchEquipped(false);
        }

        SyncTorchStateToCharacterDataIfChanged(prevSeconds, prevEquipped);
    }

    public void AddTorchSeconds(int seconds)
    {
        if (seconds <= 0)
        {
            return;
        }

        MarkInventoryInitialized();
        torchSecondsRemaining = Mathf.Max(0, torchSecondsRemaining + seconds);
        SyncTorchStateToCharacterData();
    }

    private bool ConsumeItem(Item item, int count)
    {
        if (item == null || count <= 0)
        {
            return false;
        }

        if (items == null || items.Count == 0)
        {
            return false;
        }

        int removed = 0;
        for (int i = items.Count - 1; i >= 0 && removed < count; i--)
        {
            if (items[i] == item)
            {
                items.RemoveAt(i);
                removed++;
            }
        }

        return removed == count;
    }

    private bool IsTorchItem(Item item)
    {
        return item != null && item.isTorch;
    }

    private void SetTorchEquipped(bool equipped)
    {
        EnsureTorchCached();
        if (torchTransform == null)
        {
            return;
        }

        torchEquipped = equipped;

        if (animator != null && !string.IsNullOrWhiteSpace(torchBoolParam))
        {
            animator.SetBool(torchBoolParam, torchEquipped);
        }

        QueueTorchVisualTransition(equipped);
        UpdateTorchAnimationLayerWeight(immediate: true);

        SyncTorchStateToCharacterData();
    }

    private void ApplyTorchVisualState(bool equipped)
    {
        if (torchTransform == null)
        {
            return;
        }

        torchVisualEquipped = equipped;
        if (torchTransform.gameObject.activeSelf != equipped)
        {
            torchTransform.gameObject.SetActive(equipped);
        }
    }

    private void QueueTorchVisualTransition(bool equipped)
    {
        if (torchVisualEquipped == equipped)
        {
            ClearPendingTorchVisualTransition();
            UpdateTorchAnimationLayerWeight(immediate: true);
            return;
        }

        if (!CanDelayTorchVisualTransition())
        {
            ApplyTorchVisualState(equipped);
            ClearPendingTorchVisualTransition();
            UpdateTorchAnimationLayerWeight(immediate: true);
            return;
        }

        pendingTorchVisualTransition = equipped ? TorchVisualTransition.Equip : TorchVisualTransition.Unequip;
        torchVisualTransitionStateObserved = false;
        torchVisualTransitionTimer = 0f;
        UpdateTorchAnimationLayerWeight(immediate: true);
    }

    private void UpdateTorchVisualTransition()
    {
        if (pendingTorchVisualTransition == TorchVisualTransition.None)
        {
            return;
        }

        EnsureTorchCached();
        if (torchTransform == null)
        {
            ClearPendingTorchVisualTransition();
            return;
        }

        if (!CanDelayTorchVisualTransition())
        {
            ApplyTorchVisualState(torchEquipped);
            ClearPendingTorchVisualTransition();
            return;
        }

        torchVisualTransitionTimer += Time.deltaTime;
        if (!torchVisualTransitionStateObserved && IsTorchAnimationStateActive(pendingTorchVisualTransition))
        {
            torchVisualTransitionStateObserved = true;
            torchVisualTransitionTimer = 0f;
            return;
        }

        if (!torchVisualTransitionStateObserved && torchVisualTransitionTimer < TorchAnimationStateFallbackDelay)
        {
            return;
        }

        torchVisualTransitionStateObserved = true;
        if (torchVisualTransitionTimer < TorchAnimationVisualDelay)
        {
            return;
        }

        ApplyTorchVisualState(torchEquipped);
        ClearPendingTorchVisualTransition();
    }

    private bool CanDelayTorchVisualTransition()
    {
        return GetTorchAnimationLayerIndex() >= 0
            && !string.IsNullOrWhiteSpace(torchBoolParam);
    }

    private bool IsTorchAnimationStateActive(TorchVisualTransition transition)
    {
        int layerIndex = GetTorchAnimationLayerIndex();
        if (layerIndex < 0)
        {
            return false;
        }

        int stateHash = transition == TorchVisualTransition.Equip
            ? TorchEquipStateHash
            : TorchUnequipStateHash;

        if (MatchesTorchAnimationState(animator.GetCurrentAnimatorStateInfo(layerIndex), stateHash))
        {
            return true;
        }

        return animator.IsInTransition(layerIndex)
            && MatchesTorchAnimationState(animator.GetNextAnimatorStateInfo(layerIndex), stateHash);
    }

    private int GetTorchAnimationLayerIndex()
    {
        if (animator == null || !animator.isActiveAndEnabled)
        {
            return -1;
        }

        return animator.GetLayerIndex(TorchAnimationLayerName);
    }

    private void SyncTorchAnimationStateImmediate()
    {
        int layerIndex = GetTorchAnimationLayerIndex();
        if (layerIndex < 0)
        {
            return;
        }

        int stateHash = torchEquipped ? TorchLocomotionStateHash : TorchOffStateHash;
        animator.Play(stateHash, layerIndex, 0f);
    }

    private void UpdateTorchAnimationLayerWeight(bool immediate = false)
    {
        int layerIndex = GetTorchAnimationLayerIndex();
        if (layerIndex < 0)
        {
            return;
        }

        float targetWeight = ResolveTorchAnimationLayerWeightTarget();
        float nextWeight = targetWeight;

        if (!immediate && Application.isPlaying)
        {
            float currentWeight = animator.GetLayerWeight(layerIndex);
            if (torchUpperBodyLayerWeightResponsiveness > 0f)
            {
                float t = 1f - Mathf.Exp(-torchUpperBodyLayerWeightResponsiveness * Time.deltaTime);
                nextWeight = Mathf.Lerp(currentWeight, targetWeight, t);
            }
        }

        if (!Mathf.Approximately(animator.GetLayerWeight(layerIndex), nextWeight))
        {
            animator.SetLayerWeight(layerIndex, nextWeight);
        }
    }

    private float ResolveTorchAnimationLayerWeightTarget()
    {
        if (pendingTorchVisualTransition != TorchVisualTransition.None)
        {
            return 1f;
        }

        if (!torchEquipped)
        {
            return 0f;
        }

        float maxMoveSpeed = Mathf.Max(0.01f, moveSpeed);
        float normalizedSpeed = Mathf.Clamp01(GetCurrentHorizontalVelocity().magnitude / maxMoveSpeed);
        return Mathf.Lerp(torchUpperBodyIdleLayerWeight, torchUpperBodyMovingLayerWeight, normalizedSpeed);
    }

    private static bool MatchesTorchAnimationState(AnimatorStateInfo stateInfo, int stateHash)
    {
        return stateInfo.shortNameHash == stateHash;
    }

    private void ClearPendingTorchVisualTransition()
    {
        pendingTorchVisualTransition = TorchVisualTransition.None;
        torchVisualTransitionStateObserved = false;
        torchVisualTransitionTimer = 0f;
        UpdateTorchAnimationLayerWeight(immediate: true);
    }

    private Item GetTorchItem()
    {
        if (items == null || items.Count == 0)
        {
            return null;
        }

        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];
            if (IsTorchItem(item))
            {
                return item;
            }
        }

        return null;
    }

    private bool RemoveTorchItem()
    {
        if (items == null || items.Count == 0)
        {
            return false;
        }

        for (int i = items.Count - 1; i >= 0; i--)
        {
            Item item = items[i];
            if (IsTorchItem(item))
            {
                items.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    private void EnsureTorchCached()
    {
        if (torchInitialized)
        {
            return;
        }

        torchInitialized = true;
        torchTransform = FindTorchTransform();
        if (torchTransform != null)
        {
            ConfigureTorchPhysics(torchTransform);
        }
    }

    private void ConfigureTorchPhysics(Transform root)
    {
        if (root == null)
        {
            return;
        }

        MeshCollider[] meshColliders = root.GetComponentsInChildren<MeshCollider>(true);
        for (int i = 0; i < meshColliders.Length; i++)
        {
            MeshCollider collider = meshColliders[i];
            if (collider == null)
            {
                continue;
            }

            if (!collider.convex)
            {
                collider.convex = true;
            }

            Rigidbody rb = collider.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }
    }

    private Transform FindTorchTransform()
    {
        Transform root = motionRoot != null ? motionRoot : transform;
        Transform parent = FindChildByName(root, torchParentName);
        if (parent == null)
        {
            parent = root;
        }

        Transform torch = FindChildByName(parent, torchChildName);
        if (torch == null)
        {
            torch = root.Find($"{torchParentName}/{torchChildName}");
        }

        return torch;
    }

    private static Transform FindChildByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == targetName)
            {
                return child;
            }

            Transform found = FindChildByName(child, targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private void SmoothInput(float deltaTime)
    {
        if (inputResponsiveness <= 0f)
        {
            smoothedInput = moveInput;
            return;
        }

        float t = 1f - Mathf.Exp(-inputResponsiveness * deltaTime);
        smoothedInput = Vector2.Lerp(smoothedInput, moveInput, t);
    }

    private void SmoothAnimationPreview(float deltaTime)
    {
        if (inputResponsiveness <= 0f)
        {
            smoothedAnimationPreviewInput = animationPreviewInput;
            return;
        }

        float t = 1f - Mathf.Exp(-inputResponsiveness * deltaTime);
        smoothedAnimationPreviewInput = Vector2.Lerp(smoothedAnimationPreviewInput, animationPreviewInput, t);
    }

    private void UpdateAnimationSpeed()
    {
        if (animator == null || string.IsNullOrWhiteSpace(speedParam))
        {
            return;
        }

        NetcodePlayerUtils.CharacterControlState controlState = NetcodePlayerUtils.ResolveCharacterControlState(gameObject);
        string movementMode = NetcodePlayerUtils.ResolveMovementMode(controlState, followerAiEnabled: false, waitingPointEnabled: false);
        string animationDriverMode = NetcodePlayerUtils.ResolveAnimationDriverMode(controlState);

        Vector3 velocity = GetCurrentHorizontalVelocity();
        float velocitySpeed = velocity.magnitude;
        float targetMoveSpeed = Mathf.Max(0f, moveSpeed);
        float inputSpeed = Mathf.Clamp01(smoothedInput.magnitude) * targetMoveSpeed;
        float previewSpeed = Mathf.Clamp01(smoothedAnimationPreviewInput.magnitude) * targetMoveSpeed;

        float rawSpeed = animationDriverMode == "local"
            ? Mathf.Max(previewSpeed, velocitySpeed)
            : (useVelocityForAnimation ? velocitySpeed : inputSpeed);

        float animSpeed = ResolveAnimatorSpeedValue(rawSpeed);

        SetSpeed(animSpeed);
        TrackAnimationState(controlState, movementMode, animationDriverMode, rawSpeed, animSpeed, previewSpeed, velocity);
    }

    private float ResolveAnimatorSpeedValue(float rawSpeed)
    {
        float animSpeed = rawSpeed;
        if (!useDiscreteLocomotion)
        {
            return animSpeed;
        }

        if (rawSpeed <= walkSpeedThreshold)
        {
            return idleAnimValue;
        }

        if (rawSpeed <= runSpeedThreshold)
        {
            return walkAnimValue;
        }

        return runAnimValue;
    }

    private void TrackAnimationState(
        NetcodePlayerUtils.CharacterControlState controlState,
        string movementMode,
        string animationDriverMode,
        float rawSpeed,
        float animSpeed,
        float previewSpeed,
        Vector3 velocity)
    {
        bool animatorEnabled = animator != null && animator.enabled;
        int speedBucket = ResolveAnimationSpeedBucket(animSpeed);

        if (!string.Equals(lastAnimationDriverMode, animationDriverMode, System.StringComparison.Ordinal))
        {
            string previousMode = string.IsNullOrWhiteSpace(lastAnimationDriverMode) ? "<none>" : lastAnimationDriverMode;
            LogAnimationStatus(
                "animation_driver_mode_changed",
                controlState,
                movementMode,
                animationDriverMode,
                rawSpeed,
                animSpeed,
                previewSpeed,
                velocity,
                reason: $"animation authority switched from {previousMode} to {animationDriverMode}");

            if (string.Equals(animationDriverMode, "local", System.StringComparison.Ordinal))
            {
                LogAnimationStatus(
                    "local_player_animation_mode_activated",
                    controlState,
                    movementMode,
                    animationDriverMode,
                    rawSpeed,
                    animSpeed,
                    previewSpeed,
                    velocity,
                    reason: "late-join owned character animation now uses local input preview");
            }
        }
        else if (!string.Equals(lastAnimationMovementMode, movementMode, System.StringComparison.Ordinal))
        {
            LogAnimationStatus(
                "animation_movement_mode_changed",
                controlState,
                movementMode,
                animationDriverMode,
                rawSpeed,
                animSpeed,
                previewSpeed,
                velocity,
                reason: $"movement mode changed to {movementMode}");
        }
        else if (lastAnimationAnimatorEnabled != animatorEnabled)
        {
            LogAnimationStatus(
                "animation_animator_enabled_changed",
                controlState,
                movementMode,
                animationDriverMode,
                rawSpeed,
                animSpeed,
                previewSpeed,
                velocity,
                reason: animatorEnabled
                    ? "Animator enabled"
                    : "Animator disabled");
        }
        else if (lastAnimationSpeedBucket != speedBucket)
        {
            LogAnimationStatus(
                "animation_speed_bucket_changed",
                controlState,
                movementMode,
                animationDriverMode,
                rawSpeed,
                animSpeed,
                previewSpeed,
                velocity,
                reason: $"Animator speed bucket changed to {speedBucket}");
        }

        lastAnimationDriverMode = animationDriverMode ?? string.Empty;
        lastAnimationMovementMode = movementMode ?? string.Empty;
        lastAnimationSpeedBucket = speedBucket;
        lastAnimationAnimatorEnabled = animatorEnabled;
    }

    private int ResolveAnimationSpeedBucket(float animSpeed)
    {
        if (useDiscreteLocomotion)
        {
            if (Mathf.Approximately(animSpeed, idleAnimValue))
            {
                return 0;
            }

            if (Mathf.Approximately(animSpeed, walkAnimValue))
            {
                return 1;
            }

            if (Mathf.Approximately(animSpeed, runAnimValue))
            {
                return 2;
            }
        }

        return Mathf.RoundToInt(animSpeed * 10f);
    }

    private void LogAnimationStatus(string eventName, bool force, string reason)
    {
        if (!force)
        {
            return;
        }

        NetcodePlayerUtils.CharacterControlState controlState = NetcodePlayerUtils.ResolveCharacterControlState(gameObject);
        string movementMode = NetcodePlayerUtils.ResolveMovementMode(controlState, followerAiEnabled: false, waitingPointEnabled: false);
        string animationDriverMode = NetcodePlayerUtils.ResolveAnimationDriverMode(controlState);
        Vector3 velocity = GetCurrentHorizontalVelocity();
        float velocitySpeed = velocity.magnitude;
        float targetMoveSpeed = Mathf.Max(0f, moveSpeed);
        float previewSpeed = Mathf.Clamp01(smoothedAnimationPreviewInput.magnitude) * targetMoveSpeed;
        float rawSpeed = animationDriverMode == "local"
            ? Mathf.Max(previewSpeed, velocitySpeed)
            : (useVelocityForAnimation ? velocitySpeed : Mathf.Clamp01(smoothedInput.magnitude) * targetMoveSpeed);
        float animSpeed = ResolveAnimatorSpeedValue(rawSpeed);

        LogAnimationStatus(
            eventName,
            controlState,
            movementMode,
            animationDriverMode,
            rawSpeed,
            animSpeed,
            previewSpeed,
            velocity,
            reason);
    }

    private void LogAnimationStatus(
        string eventName,
        NetcodePlayerUtils.CharacterControlState controlState,
        string movementMode,
        string animationDriverMode,
        float rawSpeed,
        float animSpeed,
        float previewSpeed,
        Vector3 velocity,
        string reason)
    {
        bool animatorEnabled = animator != null && animator.enabled;
        Debug.Log(
            $"[NetcodeAnimation] event='{eventName}' path='{PersistentWorldDebug.DescribeTransform(transform)}' characterId='{controlState.CharacterId}' ownerClientId={FormatClientId(controlState.HasNetworkObject, controlState.OwnerClientId)} localClientId={FormatClientId(controlState.HasLocalClientId, controlState.LocalClientId)} isOwner={controlState.IsOwner} isControlledLocally={controlState.IsControlledLocally} movementMode='{movementMode}' animatorEnabled={animatorEnabled} networkAnimationSyncEnabled=False animationDriverMode='{animationDriverMode}' speedValue={animSpeed:0.###} rawSpeed={rawSpeed:0.###} previewSpeed={previewSpeed:0.###} directionValue='n/a' turnValue='n/a' previewWorldInput='{FormatVector2(smoothedAnimationPreviewInput)}' velocityWorld='{FormatVector3(velocity)}' reason='{reason}'",
            this);
    }

    private static bool IsSameOrRelatedTransform(Transform current, Transform candidate)
    {
        if (current == null || candidate == null)
        {
            return false;
        }

        return current == candidate || current.IsChildOf(candidate) || candidate.IsChildOf(current);
    }

    private static string FormatVector2(Vector2 value)
    {
        return $"({value.x:0.###},{value.y:0.###})";
    }

    private static string FormatVector3(Vector3 value)
    {
        return $"({value.x:0.###},{value.y:0.###},{value.z:0.###})";
    }

    private static string FormatClientId(bool hasValue, ulong value)
    {
        return hasValue ? value.ToString() : "n/a";
    }

    private Vector3 GetCurrentHorizontalVelocity()
    {
        if (ShouldUseRigidbody() && rigidbodyTarget != null)
        {
            Vector3 velocity = rigidbodyTarget.linearVelocity;
            return new Vector3(velocity.x, 0f, velocity.z);
        }

        return currentHorizontalVelocity;
    }

    private Vector3 GetWorldPosition()
    {
        if (ShouldUseRigidbody() && rigidbodyTarget != null)
        {
            return rigidbodyTarget.position;
        }

        Transform root = motionRoot != null ? motionRoot : transform;
        return root.position;
    }

    private Vector3 GetMoveDirection(Vector2 input)
    {
        if (moveInputIsWorldSpace)
        {
            return new Vector3(input.x, 0f, input.y);
        }

        Vector3 move = new Vector3(input.x, 0f, input.y);
        if (!ShouldUseCameraRelativeInput())
        {
            return move;
        }

        Camera cam = ResolveMovementCamera();
        if (cam == null)
        {
            return move;
        }

        Vector3 camForward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
        return camRight * input.x + camForward * input.y;
    }

    public Vector2 GetWorldSpaceInput(Vector2 input)
    {
        Vector3 direction = GetMoveDirection(input);
        return new Vector2(direction.x, direction.z);
    }

    public Vector2 GetInputFromWorldDirection(Vector3 worldDirection)
    {
        Vector3 planar = new Vector3(worldDirection.x, 0f, worldDirection.z);
        if (planar.sqrMagnitude < 0.0001f)
        {
            return Vector2.zero;
        }

        planar.Normalize();

        if (!ShouldUseCameraRelativeInput())
        {
            return new Vector2(planar.x, planar.z);
        }

        Camera cam = ResolveMovementCamera();
        if (cam == null)
        {
            return new Vector2(planar.x, planar.z);
        }

        Vector3 camForward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
        float x = Vector3.Dot(planar, camRight);
        float y = Vector3.Dot(planar, camForward);
        return new Vector2(x, y);
    }

    private bool ShouldUseCameraRelativeInput()
    {
        return useCameraRelative;
    }

    private Camera ResolveMovementCamera()
    {
        CameraController controller = ResolveMovementCameraController();
        if (controller != null && controller.mainCam != null && controller.mainCam.isActiveAndEnabled)
        {
            referenceCamera = controller.mainCam;
            return referenceCamera;
        }

        if (referenceCamera != null && referenceCamera.isActiveAndEnabled)
        {
            return referenceCamera;
        }

        Camera main = Camera.main;
        if (main != null && main.isActiveAndEnabled)
        {
            referenceCamera = main;
            return main;
        }

#if UNITY_2023_1_OR_NEWER
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
#else
        Camera[] cameras = FindObjectsOfType<Camera>();
#endif
        if (cameras != null)
        {
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera candidate = cameras[i];
                if (candidate == null || !candidate.isActiveAndEnabled)
                {
                    continue;
                }

                referenceCamera = candidate;
                return referenceCamera;
            }
        }

        return null;
    }

    private CameraController ResolveMovementCameraController()
    {
        Transform preferredTarget = LocalPlayerContext.LocalCharacterRoot != null
            ? LocalPlayerContext.LocalCharacterRoot
            : transform;

#if UNITY_2023_1_OR_NEWER
        CameraController[] controllers = FindObjectsByType<CameraController>(FindObjectsSortMode.None);
#else
        CameraController[] controllers = FindObjectsOfType<CameraController>();
#endif
        if (controllers == null || controllers.Length == 0)
        {
            return null;
        }

        CameraController fallback = null;
        for (int i = 0; i < controllers.Length; i++)
        {
            CameraController controller = controllers[i];
            if (controller == null || !controller.isActiveAndEnabled)
            {
                continue;
            }

            if (fallback == null && controller.mainCam != null && controller.mainCam.isActiveAndEnabled)
            {
                fallback = controller;
            }

            if (controller.followOverrideTarget != null)
            {
                return controller;
            }

            Transform currentTarget = controller.mainCamCurrentTarget;
            if (currentTarget == null || preferredTarget == null)
            {
                continue;
            }

            if (currentTarget == preferredTarget ||
                currentTarget.IsChildOf(preferredTarget) ||
                preferredTarget.IsChildOf(currentTarget))
            {
                return controller;
            }
        }

        return fallback;
    }

    private void ApplyAnimatorSettings()
    {
        if (animator == null)
        {
            return;
        }

        animator.applyRootMotion = false;
        animator.updateMode = animatePhysics ? AnimatorUpdateMode.Fixed : AnimatorUpdateMode.Normal;
    }

    private bool ShouldUseRigidbody()
    {
        if (rigidbodyTarget == null)
        {
            return false;
        }

        if (characterController == null)
        {
            return true;
        }

        return preferRigidbody;
    }

    public void AddImpulse(Vector3 worldImpulse, float lockInputForSeconds = -1f)
    {
        if (rigidbodyTarget == null)
        {
            return;
        }

        rigidbodyTarget.AddForce(worldImpulse, knockbackForceMode);

        float duration = lockInputForSeconds < 0f ? inputLockTime : lockInputForSeconds;
        if (duration > 0f)
        {
            inputLockTimer = Mathf.Max(inputLockTimer, duration);
        }
    }

    private void RegisterCharacter()
    {
        if (!registeredCharacters.Contains(this))
        {
            registeredCharacters.Add(this);
        }

        if (!ignoreCharacterCollisions)
        {
            return;
        }

        if (!activeCharacters.Contains(this))
        {
            activeCharacters.Add(this);
        }

        MarkCollidersDirty();

        for (int i = 0; i < activeCharacters.Count; i++)
        {
            SquadCharacterController other = activeCharacters[i];
            if (other == null || other == this)
            {
                continue;
            }

            other.MarkCollidersDirty();
            SetIgnoreCollisionsWith(other, true);
        }
    }

    private void UnregisterCharacter()
    {
        registeredCharacters.Remove(this);

        if (!ignoreCharacterCollisions)
        {
            activeCharacters.Remove(this);
            return;
        }

        if (activeCharacters.Remove(this))
        {
            for (int i = 0; i < activeCharacters.Count; i++)
            {
                SquadCharacterController other = activeCharacters[i];
                if (other == null)
                {
                    continue;
                }

                SetIgnoreCollisionsWith(other, false);
                other.MarkCollidersDirty();
            }
        }
    }

    private void RefreshCharacterCollisionsIfNeeded()
    {
        if (!ignoreCharacterCollisions)
        {
            return;
        }

        float interval = Mathf.Max(0.05f, collisionRefreshInterval);
        if (!collidersDirty && Time.time < nextCollisionRefreshTime)
        {
            return;
        }

        nextCollisionRefreshTime = Time.time + interval;
        collidersDirty = false;
        CacheColliders();

        for (int i = 0; i < activeCharacters.Count; i++)
        {
            SquadCharacterController other = activeCharacters[i];
            if (other == null || other == this)
            {
                continue;
            }

            SetIgnoreCollisionsWith(other, true);
        }
    }

    private void MarkCollidersDirty()
    {
        collidersDirty = true;
        nextCollisionRefreshTime = 0f;
    }

    private void CacheColliders()
    {
        cachedColliders.Clear();
        GetComponentsInChildren(true, cachedColliders);
        if (!ignoreCharacterTriggerColliders)
        {
            for (int i = cachedColliders.Count - 1; i >= 0; i--)
            {
                Collider col = cachedColliders[i];
                if (col != null && col.isTrigger)
                {
                    cachedColliders.RemoveAt(i);
                }
            }
        }
    }

    private void SetIgnoreCollisionsWith(SquadCharacterController other, bool ignore)
    {
        if (other == null || other == this)
        {
            return;
        }

        if (collidersDirty)
        {
            CacheColliders();
            collidersDirty = false;
        }

        if (other.collidersDirty)
        {
            other.CacheColliders();
            other.collidersDirty = false;
        }

        List<Collider> mine = cachedColliders;
        List<Collider> theirs = other.cachedColliders;
        if (mine == null || theirs == null)
        {
            return;
        }

        for (int i = 0; i < mine.Count; i++)
        {
            Collider a = mine[i];
            if (!IsSceneCollider(a))
            {
                continue;
            }

            for (int j = 0; j < theirs.Count; j++)
            {
                Collider b = theirs[j];
                if (!IsSceneCollider(b))
                {
                    continue;
                }

                Physics.IgnoreCollision(a, b, ignore);
            }
        }
    }

    private static bool IsSceneCollider(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        return collider.gameObject.scene.IsValid() && collider.gameObject.scene.isLoaded;
    }
}
