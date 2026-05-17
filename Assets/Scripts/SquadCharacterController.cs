using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Controle le mouvement et l'inventaire runtime d'un personnage de la squad.
[RequireComponent(typeof(Animator), typeof(Rigidbody))]
public partial class SquadCharacterController : MonoBehaviour
{
    private const float WalkLocomotionTier = 1f;
    private const float JogtrotLocomotionTier = 2f;
    private const float RunLocomotionTier = 3f;
    private const string WalkStartStateName = "Walk_Start";
    private const string JogtrotStartStateName = "Jogtrot_Start";
    private const string RunStartStateName = "Run_Start";
    private static readonly int[] LocomotionEndStateHashes =
    {
        Animator.StringToHash("Walk_Stop"),
        Animator.StringToHash("Jogtrot_Stop"),
        Animator.StringToHash("Run_Stop"),
        Animator.StringToHash("Walk_End"),
        Animator.StringToHash("Wal_End"),
        Animator.StringToHash("Jogtrot_End"),
        Animator.StringToHash("Jojtrot_End"),
        Animator.StringToHash("Run_End")
    };

    private enum TorchVisualTransition
    {
        None,
        Equip,
        Unequip
    }

    private const string TorchAnimationLayerName = "Upper Body Torch";
    private const float TorchAnimationStateFallbackDelay = 0.2f;
    private const float TorchAnimationVisualDelay = 0.5f;
    private static readonly int TorchEquipStateHash = Animator.StringToHash("Torch_Equip");
    private static readonly int TorchLocomotionStateHash = Animator.StringToHash("Torch_Locomotion");
    private static readonly int TorchOffStateHash = Animator.StringToHash("Torch_Off");
    private static readonly int TorchUnequipStateHash = Animator.StringToHash("Torch_Unequip");

    [Header("Inventory")]
    [SerializeField, HideInInspector] private List<Item> items = new List<Item>();
    [SerializeField, HideInInspector] private List<Item> equippedInteractionItems = new List<Item>();
    [SerializeField, Tooltip("Duree initiale de la torche (secondes).")]
    private int startingTorchSeconds = 300;
    [SerializeField, Tooltip("Duree restante de la torche (secondes).")]
    private int torchSecondsRemaining = 300;
    [SerializeField, Tooltip("Active les logs du flux d'initialisation d'inventaire.")]
    private bool logInventoryInitialization = true;

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
    [SerializeField, Tooltip("Transform racine utilise pour le mouvement.")]
    private Transform motionRoot;

    [Header("Animator Params")]
    [SerializeField, Tooltip("Nom du parametre Speed dans l'Animator.")]
    private string speedParam = "Speed";
    [SerializeField, Tooltip("Damping du parametre Speed.")]
    private float speedDampTime = 0.06f;
    [SerializeField, Tooltip("Utilise un damping sur Speed.")]
    private bool useSpeedDamping = false;

    [Header("Animation")]
    [SerializeField, Tooltip("Vitesse gameplay a laquelle la phase de marche atteint son plein poids dans le blend tree.")]
    private float walkSpeedThreshold = 1.35f;
    [SerializeField, Tooltip("Vitesse gameplay a laquelle la phase de course atteint son plein poids dans le blend tree.")]
    private float runSpeedThreshold = 3.25f;
    [SerializeField, Tooltip("Valeur Speed pour idle.")]
    private float idleAnimValue = 0f;
    [SerializeField, Tooltip("Valeur Speed du blend tree correspondant au palier marche.")]
    private float walkAnimValue = 1.35f;
    [SerializeField, Tooltip("Valeur Speed du blend tree correspondant au palier course.")]
    private float runAnimValue = 3.25f;
    [SerializeField, Tooltip("Vitesse nominale envoyee a l'Animator quand le personnage atteint son trot max sans sprint. Permet d'augmenter la vitesse gameplay reelle sans pousser le blend tree sur le clip de sprint.")]
    private float trotPresentationSpeed = 2.5f;

    [Header("Animation Feel")]
    [SerializeField, Tooltip("Seuil bas utilise pour couper les etats de locomotion optionnels de l'Animator.")]
    private float animationMovingExitSpeed = 0.12f;
    [SerializeField, Tooltip("Seuil haut utilise pour declencher les etats de locomotion optionnels de l'Animator.")]
    private float animationMovingEnterSpeed = 0.32f;
    [SerializeField, Tooltip("Responsivite du float de turn optionnel expose a l'Animator.")]
    private float animationTurnResponsiveness = 10f;
    [SerializeField, Tooltip("Nom du bool optionnel pour distinguer idle et locomotion.")]
    private string isMovingParam = "IsMoving";
    [SerializeField, Tooltip("Nom du trigger optionnel emis au depart du mouvement.")]
    private string moveStartTriggerParam = "MoveStartTrigger";
    [SerializeField, Tooltip("Nom du trigger optionnel emis a l'arret du mouvement.")]
    private string moveStopTriggerParam = "MoveStopTrigger";
    [SerializeField, Tooltip("Nom du float optionnel qui selectionne le Start/Stop locomotion: 1=Walk, 2=Jogtrot, 3=Run.")]
    private string locomotionTierParam = "LocomotionTier";
    [SerializeField, Tooltip("Nom du float optionnel signe (-1..1) mesurant le besoin de rotation.")]
    private string turnParam = "Turn";
    [SerializeField, Tooltip("Nom du bool optionnel actif quand un pivot sur place est pertinent.")]
    private string turnInPlaceParam = "TurnInPlace";
    [SerializeField, Tooltip("Angle minimal avant d'annoncer un pivot sur place (deg).")]
    private float turnInPlaceAngleThreshold = 60f;
    [SerializeField, Tooltip("Layer Animator contenant la locomotion base.")]
    private int locomotionAnimationLayer;
    [SerializeField, Tooltip("Seuil d'input qui autorise l'interruption directe d'une animation Walk/Jogtrot/Run_End.")]
    private float locomotionEndRestartInputThreshold = 0.12f;
    [SerializeField, Tooltip("Duree du crossfade quand un input relance la locomotion depuis une animation End.")]
    private float locomotionEndRestartTransitionDuration = 0.04f;

    [Header("Movement")]
    [SerializeField, Tooltip("Vitesse max de marche quand le modificateur de course n'est pas maintenu.")]
    private float walkMoveSpeed = 5f;
    [SerializeField, Tooltip("Vitesse de deplacement.")]
    private float moveSpeed = 6.5f;
    [SerializeField, Tooltip("Lissage de l'input.")]
    private float inputResponsiveness = 14f;
    [SerializeField, Tooltip("Lissage utilise quand le stick revient dans la zone morte.")]
    private float inputReleaseResponsiveness = 36f;
    [SerializeField, Range(0f, 1f), Tooltip("Zone morte gameplay de la locomotion InPlace.")]
    private float movementInputDeadZone = 0.08f;
    [SerializeField, Tooltip("Tourne vers la direction d'input.")]
    private bool rotateToInput = true;
    [SerializeField, Tooltip("Vitesse de rotation.")]
    private float rotationSpeed = 24f;
    [SerializeField, Tooltip("Deplacement relatif a la camera.")]
    private bool useCameraRelative = true;
    [SerializeField, Tooltip("Camera de reference (fallback Main).")]
    private Camera referenceCamera;
    [SerializeField, Tooltip("Conserve la reference de mouvement tant que l'input est maintenu, notamment pendant les changements de camera fixe.")]
    private bool preserveFixedCameraMovementContinuity = true;
    [SerializeField, Range(0f, 180f), Tooltip("Angle d'input qui force la reference de mouvement a se recaler sur la camera active.")]
    private float fixedCameraMovementInputRefreshAngle = 65f;
    [SerializeField, Min(0f), Tooltip("Blend optionnel de la reference de mouvement vers la camera active. 0 = reference verrouillee jusqu'au relachement/changement d'input.")]
    private float fixedCameraMovementReferenceBlendSharpness = 0f;
    [SerializeField, Tooltip("Anime les RB en physics.")]
    private bool animatePhysics = true;
    [SerializeField, Tooltip("Responsivite de rotation appliquee quand le personnage est deja en mouvement.")]
    private float movingRotationSpeed = 16f;
    [SerializeField, Tooltip("Seuil de vitesse a partir duquel la rotation passe en mode mouvement.")]
    private float movingRotationSpeedThreshold = 0.75f;
    private bool storedMovementReferenceActive;
    private Vector3 storedForward;
    private Vector3 storedRight;
    private Vector2 storedInput;

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

    [Header("Foot IK")]
    [SerializeField, Tooltip("Active l'IK des pieds pour stabiliser l'ancrage au sol.")]
    private bool enableFootIk = true;
    [SerializeField, Tooltip("Poids max de l'IK (0-1).")]
    private float footIkWeight = 1f;
    [SerializeField, Tooltip("Poids position IK (0-1).")]
    private float footIkPositionWeight = 1f;
    [SerializeField, Tooltip("Poids rotation IK (0-1).")]
    private float footIkRotationWeight = 1f;
    [SerializeField, Tooltip("Poids rotation IK applique uniquement a l'Idle pour eviter de vriller les jambes tout en gardant les appuis stables.")]
    private float footIkIdleRotationWeight = 0.2f;
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
    [SerializeField, Tooltip("Empeche les deplacements pilotes par le controller de traverser les obstacles.")]
    private bool preventWallPenetration = true;
    [SerializeField, Tooltip("Marge conservee avant un obstacle lors du sweep de mouvement (m).")]
    private float movementCollisionSkin = 0.03f;
    [SerializeField, Range(0f, 1f), Tooltip("Normale minimale consideree comme walkable et ignoree pour le blocage horizontal.")]
    private float movementCollisionWalkableNormalDot = 0.35f;
    [SerializeField, Tooltip("Applique un materiau sans friction a la capsule de locomotion pour eviter que les contremarches ralentissent le Rigidbody.")]
    private bool useLowFrictionLocomotionMaterial = true;
    [SerializeField, Tooltip("Remplace aussi un PhysicMaterial deja assigne sur la capsule de locomotion.")]
    private bool overrideExistingLocomotionMaterial;

    private Vector2 moveInput;
    private float inputLockTimer;
    private int scriptedMovementSuppressionCount;
    private int externalLocomotionDriverLockCount;
    private Vector2 smoothedInput;
    private bool moveInputIsWorldSpace;
    private bool sprintModifierPressed;
    private Vector3 currentHorizontalVelocity;
    private Vector3 lastObservedWorldPosition;
    private bool hasObservedWorldPosition;
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
    private CapsuleCollider locomotionCapsule;
    private bool wasMovingForAnimator;
    private float smoothedTurnAmount;
    private float lastMovingLocomotionTier = WalkLocomotionTier;
    [Header("Audio")]
    [SerializeField] private AudioListener audioListener;
    [SerializeField] private bool searchAudioListenerInChildren = true;
    private bool audioListenerActive;
    private NetworkObject cachedNetworkObject;
    private readonly RaycastHit[] movementCastHits = new RaycastHit[8];
    private readonly Collider[] movementOverlapHits = new Collider[8];
    private float footIkWeightCurrent;
    private float heightProbeVerticalOffsetThisStep;
    private static PhysicsMaterial lowFrictionLocomotionMaterial;

    private static readonly List<SquadCharacterController> activeCharacters = new List<SquadCharacterController>();
    private static readonly List<SquadCharacterController> registeredCharacters = new List<SquadCharacterController>();

    public CharacterData CharacterData => characterData;

    public IReadOnlyList<Item> Items => items;

    public IReadOnlyList<Item> EquippedInteractionItems => equippedInteractionItems;

    public IReadOnlyList<Skill> Skills => characterData != null ? characterData.skills : null;

    public int CurrentHp => currentHp;

    public int MaxHp => maxHp;

    public bool IsGrounded => isGrounded;
    public bool IsExternalLocomotionDriverActive => externalLocomotionDriverLockCount > 0;
    public float WalkMoveSpeed => walkMoveSpeed;
    public float MoveSpeed => moveSpeed;

    public bool TryGetHeadWorldY(out float headWorldY)
    {
        if (TryGetLocomotionCapsule(out Vector3 center, out _, out float height))
        {
            headWorldY = center.y + height * 0.5f;
            return true;
        }

        headWorldY = transform.position.y;
        return false;
    }

    private void Reset()
    {
        animator = GetComponent<Animator>();
        rigidbodyTarget = GetComponent<Rigidbody>();
        locomotionCapsule = GetComponent<CapsuleCollider>();
        motionRoot = transform;
        ApplyAnimatorSettings();
        EnsureRigidbodyCollisionSafety();
        InitializeTorchState();
        ResetCommittedJumpRuntime();
    }

    private void Update()
    {
        // Torche + collisions en runtime.
        if (!IsExternalLocomotionDriverActive)
        {
            UpdateTorchLifetime(Time.deltaTime);
        }

        RefreshCharacterCollisionsIfNeeded();
        if (!IsExternalLocomotionDriverActive)
        {
            UpdateAudioListenerState(false);
        }

        if (!IsExternalLocomotionDriverActive)
        {
            UpdateLocalInteractionDetection();
        }

        TickFlightExternalLocomotion(Time.deltaTime);
    }

    private void LateUpdate()
    {
        UpdateTorchVisualTransition();
        UpdateTorchAnimationLayerWeight();

        if (ShouldRunAnimationReconciliationInLateUpdate())
        {
            ReconcileAnimationState(Time.deltaTime);
        }

        RefreshFlightExternalLocomotionInteractions();
        TickFlightFeedback();
    }

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (rigidbodyTarget == null)
        {
            rigidbodyTarget = GetComponent<Rigidbody>();
        }

        if (rigidbodyTarget == null)
        {
            Debug.LogError("SquadCharacterController requires a Rigidbody for in-place scripted locomotion.", this);
        }

        if (locomotionCapsule == null)
        {
            locomotionCapsule = GetComponent<CapsuleCollider>();
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
        EnsureRigidbodyCollisionSafety();
        InitializeTorchState();
        ResetCommittedJumpRuntime();
        RefreshAnimationReferences();
        InitializeFlightFeedback();
    }

    private void OnEnable()
    {
        RegisterCharacter();
        CacheAudioListener();
        CacheNetworkObject();
        RefreshAnimationReferences();
        UpdateAudioListenerState(true);
        ResolveFlightMotorReferences();
    }

    private void OnDisable()
    {
        SetFlightMotorActive(false);
        ShutdownFlightFeedback();
        ClearLocalInteractionTarget();
        SetAudioListenerActive(false);
        UnregisterCharacter();
    }

    private void OnDestroy()
    {
        DisposeFlightFeedbackRuntimeObjects();
    }

    private void OnTransformChildrenChanged()
    {
        MarkCollidersDirty();
    }

    private void OnTransformParentChanged()
    {
        MarkCollidersDirty();
        RefreshAnimationReferences();
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

    private void RefreshAnimationReferences()
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
        ValidateAnimationReconciliationMappings();
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

    public void RefreshAudioListenerStateForExternalLocomotion()
    {
        UpdateAudioListenerState(false);
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
        EnsureEquippedInteractionList();
        InitializeHealthFromCharacterData(resetHpOnBind);

        if (characterData == null)
        {
            return;
        }

        if (initializeInventory)
        {
            bool forceStarterItems = ShouldForceStarterItems(characterData, out string starterReason);
            if (!characterData.inventoryInitialized || forceStarterItems)
            {
                if (logInventoryInitialization)
                {
                    Debug.Log(
                        $"[InventoryInit] bind='{name}' character='{characterData.name}' initializeInventory={initializeInventory} path='apply_starter_items' inventoryInitialized={characterData.inventoryInitialized} forceStarterItems={forceStarterItems} reason='{starterReason}' runtimeInventoryCount={characterData.inventoryItems?.Count ?? -1} starterStackCount={characterData.starterItemsWithQuantity?.Count ?? -1} torchSeconds={characterData.torchSecondsRemaining}",
                        this);
                }

                ApplyStarterItems(characterData, true);
                characterData.inventoryInitialized = true;
                SyncTorchStateToCharacterData();
            }
            else
            {
                if (logInventoryInitialization)
                {
                    Debug.Log(
                        $"[InventoryInit] bind='{name}' character='{characterData.name}' initializeInventory={initializeInventory} path='load_runtime_inventory' inventoryInitialized={characterData.inventoryInitialized} runtimeInventoryCount={characterData.inventoryItems?.Count ?? -1} equippedCount={characterData.equippedInteractionItems?.Count ?? -1} torchSeconds={characterData.torchSecondsRemaining} torchEquipped={characterData.torchEquipped}",
                        this);
                }

                LoadInventoryFromCharacterData();
            }
        }
        else
        {
            if (logInventoryInitialization)
            {
                Debug.Log(
                    $"[InventoryInit] bind='{name}' character='{characterData.name}' initializeInventory={initializeInventory} path='load_runtime_inventory_without_init' inventoryInitialized={characterData.inventoryInitialized} runtimeInventoryCount={characterData.inventoryItems?.Count ?? -1} equippedCount={characterData.equippedInteractionItems?.Count ?? -1} torchSeconds={characterData.torchSecondsRemaining} torchEquipped={characterData.torchEquipped}",
                    this);
            }

            LoadInventoryFromCharacterData();
        }
    }

    private bool ShouldForceStarterItems(CharacterData data, out string reason)
    {
        if (data == null || data.starterItemsWithQuantity == null || data.starterItemsWithQuantity.Count == 0)
        {
            reason = "no_starter_items";
            return false;
        }

        if (data.inventoryItems != null && data.inventoryItems.Count > 0)
        {
            reason = "runtime_inventory_already_present";
            return false;
        }

        if (data.torchSecondsRemaining > 0 || data.torchEquipped)
        {
            reason = "runtime_torch_state_already_present";
            return false;
        }

        CharacterStateStore store = CharacterStateStore.Instance;
        if (store != null && store.HasSaveFile)
        {
            if (store.TryGetLoadedCharacterEntry(data, out _))
            {
                reason = "loaded_save_entry_exists";
                return false;
            }

            reason = "save_file_exists_but_character_has_no_loaded_entry";
            return true;
        }

        reason = "no_save_file";
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

        NormalizeReactiveInventoryItems();
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

    public bool IsTorchAgingEffectActive => torchEquipped ||
                                            torchVisualEquipped ||
                                            pendingTorchVisualTransition != TorchVisualTransition.None;

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

    public void ApplyInventoryState(List<Item> newItems, int torchSeconds, bool equipTorch, List<Item> newEquippedInteractionItems = null)
    {
        EnsureInventoryList();
        EnsureEquippedInteractionList();
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
        NormalizeReactiveInventoryItems();
        ApplyEquippedInteractionItems(newEquippedInteractionItems);
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
        SyncInteractionEquipmentToCharacterData();
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

    public bool IsInteractionItemEquipped(Item item)
    {
        EnsureEquippedInteractionList();
        return item != null && equippedInteractionItems.Contains(item);
    }

    public bool HasEquippedInteractionCapability(InteractionCapability capability)
    {
        if (capability == InteractionCapability.None)
        {
            return true;
        }

        return (GetEquippedInteractionCapabilities() & capability) == capability;
    }

    public InteractionCapability GetEquippedInteractionCapabilities()
    {
        EnsureEquippedInteractionList();
        InteractionCapability capabilities = InteractionCapability.None;
        for (int i = 0; i < equippedInteractionItems.Count; i++)
        {
            Item item = equippedInteractionItems[i];
            if (item == null)
            {
                continue;
            }

            capabilities |= item.interactionCapabilities;
        }

        if (IsTorchEquipped && TorchItem != null)
        {
            capabilities |= TorchItem.interactionCapabilities;
        }

        return capabilities;
    }

    public bool TryToggleEquippedInteractionItem(Item item, out string reason)
    {
        if (IsInteractionItemEquipped(item))
        {
            return TryUnequipInteractionItem(item, out reason);
        }

        return TryEquipInteractionItem(item, out reason);
    }

    public bool TryEquipInteractionItem(Item item, out string reason)
    {
        reason = string.Empty;
        if (item == null)
        {
            reason = "Impossible d'equiper cet objet.";
            return false;
        }

        if (!item.HasInteractionCapabilities())
        {
            reason = "Cet objet ne fournit aucune capacite d'interaction.";
            return false;
        }

        EnsureInventoryList();
        EnsureEquippedInteractionList();
        MarkInventoryInitialized();

        if (!items.Contains(item))
        {
            reason = "L'objet doit etre dans l'inventaire pour etre equipe.";
            return false;
        }

        if (equippedInteractionItems.Contains(item))
        {
            return true;
        }

        equippedInteractionItems.Add(item);
        SyncInteractionEquipmentToCharacterData();
        return true;
    }

    public bool TryUnequipInteractionItem(Item item, out string reason)
    {
        reason = string.Empty;
        if (item == null)
        {
            reason = "Impossible de desequiper cet objet.";
            return false;
        }

        EnsureEquippedInteractionList();
        if (!equippedInteractionItems.Remove(item))
        {
            reason = "Cet objet n'est pas equipe.";
            return false;
        }

        SyncInteractionEquipmentToCharacterData();
        return true;
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

    public bool HasMatchingKey(string lockId)
    {
        return TryFindMatchingKey(lockId, out _);
    }

    public bool TryUseMatchingKey(string lockId, bool consumeKeyOnUse, out Item keyItem)
    {
        keyItem = null;
        if (!TryFindMatchingKey(lockId, out Item matchingKey))
        {
            return false;
        }

        if (consumeKeyOnUse && !TryRemoveItem(matchingKey, 1))
        {
            return false;
        }

        keyItem = matchingKey;
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
        EnsureEquippedInteractionList();
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

        bool removed = ConsumeItem(item, count);
        if (removed)
        {
            NormalizeReactiveInventoryItems();
            SanitizeEquippedInteractionItems();
        }

        return removed;
    }

    public bool TryRemoveItemQuantity(Item item, int quantity)
    {
        if (item == null || quantity <= 0)
        {
            return false;
        }

        EnsureInventoryList();
        EnsureEquippedInteractionList();
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

        bool removed = ConsumeItem(item, quantity);
        if (removed)
        {
            NormalizeReactiveInventoryItems();
            SanitizeEquippedInteractionItems();
        }

        return removed;
    }

    public void ApplyStarterItems(CharacterData data, bool clearExisting = true)
    {
        EnsureInventoryList();
        EnsureEquippedInteractionList();
        MarkInventoryInitialized();

        if (clearExisting)
        {
            items.Clear();
            equippedInteractionItems.Clear();
            torchSecondsRemaining = 0;
        }

        if (data == null)
        {
            torchSecondsRemaining = 0;
            SetTorchEquipped(false);
            SyncTorchStateToCharacterData();
            SyncInteractionEquipmentToCharacterData();
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
        SyncInteractionEquipmentToCharacterData();

        if (logInventoryInitialization && data != null)
        {
            Debug.Log(
                $"[InventoryInit] apply_starter_items character='{data.name}' clearExisting={clearExisting} starterStackCount={data.starterItemsWithQuantity?.Count ?? -1} resultInventoryCount={items?.Count ?? -1} torchSeconds={torchSecondsRemaining} torchEquipped={torchEquipped}",
                this);
        }
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

    private void EnsureEquippedInteractionList()
    {
        if (characterData != null)
        {
            if (characterData.equippedInteractionItems == null)
            {
                characterData.equippedInteractionItems = new List<Item>();
            }

            if (!ReferenceEquals(equippedInteractionItems, characterData.equippedInteractionItems))
            {
                equippedInteractionItems = characterData.equippedInteractionItems;
            }

            return;
        }

        if (equippedInteractionItems == null)
        {
            equippedInteractionItems = new List<Item>();
        }
    }

    private void ApplyEquippedInteractionItems(List<Item> source)
    {
        EnsureEquippedInteractionList();

        if (ReferenceEquals(source, equippedInteractionItems))
        {
            source = source != null ? new List<Item>(source) : null;
        }

        equippedInteractionItems.Clear();
        if (source != null)
        {
            for (int i = 0; i < source.Count; i++)
            {
                Item item = source[i];
                if (item == null || equippedInteractionItems.Contains(item))
                {
                    continue;
                }

                equippedInteractionItems.Add(item);
            }
        }

        SanitizeEquippedInteractionItems();
    }

    private void SanitizeEquippedInteractionItems()
    {
        EnsureInventoryList();
        EnsureEquippedInteractionList();

        for (int i = equippedInteractionItems.Count - 1; i >= 0; i--)
        {
            Item item = equippedInteractionItems[i];
            if (item == null || !items.Contains(item) || !item.HasInteractionCapabilities())
            {
                equippedInteractionItems.RemoveAt(i);
            }
        }

        SyncInteractionEquipmentToCharacterData();
    }

    private void NormalizeReactiveInventoryItems()
    {
        EnsureInventoryList();
        if (items == null || items.Count == 0)
        {
            return;
        }

        bool inventoryChanged = NormalizeWetClayPreservation();
        if (inventoryChanged)
        {
            SanitizeEquippedInteractionItems();
        }
    }

    private bool NormalizeWetClayPreservation()
    {
        int preservedCapacity = 0;
        List<int> wetClayIndices = null;

        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];
            if (item == null)
            {
                continue;
            }

            if (item.preservesWetClay)
            {
                preservedCapacity += Mathf.Max(0, item.preservedWetClayCapacity);
            }

            if (!item.isWetClay)
            {
                continue;
            }

            wetClayIndices ??= new List<int>();
            wetClayIndices.Add(i);
        }

        if (wetClayIndices == null || wetClayIndices.Count == 0 || wetClayIndices.Count <= preservedCapacity)
        {
            return false;
        }

        bool changed = false;
        int preservedRemaining = Mathf.Max(0, preservedCapacity);
        for (int i = 0; i < wetClayIndices.Count; i++)
        {
            int index = wetClayIndices[i];
            if (preservedRemaining > 0)
            {
                preservedRemaining--;
                continue;
            }

            Item wetClay = items[index];
            if (wetClay == null || !wetClay.isWetClay || wetClay.driedReplacementItem == null)
            {
                continue;
            }

            items[index] = wetClay.driedReplacementItem;
            changed = true;
        }

        return changed;
    }

    private bool TryFindMatchingKey(string lockId, out Item keyItem)
    {
        keyItem = null;
        if (string.IsNullOrWhiteSpace(lockId))
        {
            return false;
        }

        EnsureInventoryList();
        MarkInventoryInitialized();

        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];
            if (item == null || !item.IsMatchingKey(lockId))
            {
                continue;
            }

            keyItem = item;
            return true;
        }

        return false;
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
        EnsureEquippedInteractionList();
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

        ApplyEquippedInteractionItems(characterData.equippedInteractionItems);

        if (logInventoryInitialization)
        {
            Debug.Log(
                $"[InventoryInit] load_runtime_inventory character='{characterData.name}' resultInventoryCount={items?.Count ?? -1} equippedCount={equippedInteractionItems?.Count ?? -1} torchSeconds={torchSecondsRemaining} torchEquipped={torchEquipped}",
                this);
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

    private void SyncInteractionEquipmentToCharacterData()
    {
        if (characterData == null)
        {
            return;
        }

        EnsureEquippedInteractionList();
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

        if (rigidbodyTarget == null)
        {
            rigidbodyTarget = GetComponent<Rigidbody>();
        }

        if (locomotionCapsule == null)
        {
            locomotionCapsule = GetComponent<CapsuleCollider>();
        }

        if (motionRoot == null)
        {
            motionRoot = transform;
        }

        walkMoveSpeed = Mathf.Max(0f, walkMoveSpeed);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        walkMoveSpeed = Mathf.Min(walkMoveSpeed, moveSpeed);
        inputResponsiveness = Mathf.Max(0f, inputResponsiveness);
        inputReleaseResponsiveness = Mathf.Max(0f, inputReleaseResponsiveness);
        movementInputDeadZone = Mathf.Clamp01(movementInputDeadZone);
        fixedCameraMovementInputRefreshAngle = Mathf.Clamp(fixedCameraMovementInputRefreshAngle, 0f, 180f);
        fixedCameraMovementReferenceBlendSharpness = Mathf.Max(0f, fixedCameraMovementReferenceBlendSharpness);
        walkSpeedThreshold = Mathf.Max(0f, walkSpeedThreshold);
        runSpeedThreshold = Mathf.Max(walkSpeedThreshold, runSpeedThreshold);
        trotPresentationSpeed = Mathf.Clamp(trotPresentationSpeed, walkSpeedThreshold, runSpeedThreshold);
        speedDampTime = Mathf.Max(0f, speedDampTime);
        animationMovingExitSpeed = Mathf.Max(0f, animationMovingExitSpeed);
        animationMovingEnterSpeed = Mathf.Max(animationMovingExitSpeed, animationMovingEnterSpeed);
        animationTurnResponsiveness = Mathf.Max(0f, animationTurnResponsiveness);
        turnInPlaceAngleThreshold = Mathf.Clamp(turnInPlaceAngleThreshold, 0f, 180f);
        locomotionAnimationLayer = Mathf.Max(0, locomotionAnimationLayer);
        locomotionEndRestartInputThreshold = Mathf.Clamp01(locomotionEndRestartInputThreshold);
        locomotionEndRestartTransitionDuration = Mathf.Max(0f, locomotionEndRestartTransitionDuration);
        locomotionAnimationCorrectionDelay = Mathf.Max(0f, locomotionAnimationCorrectionDelay);
        airborneAnimationCorrectionDelay = Mathf.Max(0f, airborneAnimationCorrectionDelay);
        landingAnimationCorrectionDelay = Mathf.Max(0f, landingAnimationCorrectionDelay);
        transientAnimationCorrectionDelay = Mathf.Max(0f, transientAnimationCorrectionDelay);
        repeatedAnimationCorrectionCooldown = Mathf.Max(0f, repeatedAnimationCorrectionCooldown);
        animationReconciliationCrossFadeDuration = Mathf.Max(0f, animationReconciliationCrossFadeDuration);
        animationPhaseTimeoutPadding = Mathf.Max(0f, animationPhaseTimeoutPadding);
        movingRotationSpeed = Mathf.Max(0f, movingRotationSpeed);
        movingRotationSpeedThreshold = Mathf.Max(0f, movingRotationSpeedThreshold);
        jumpGroundCheckDistance = Mathf.Max(0.02f, jumpGroundCheckDistance);
        jumpGroundCheckRadiusScale = Mathf.Clamp(jumpGroundCheckRadiusScale, 0.1f, 1.5f);
        groundedStickVelocity = Mathf.Max(0f, groundedStickVelocity);
        footIkWeight = Mathf.Clamp01(footIkWeight);
        footIkPositionWeight = Mathf.Clamp01(footIkPositionWeight);
        footIkRotationWeight = Mathf.Clamp01(footIkRotationWeight);
        footIkIdleRotationWeight = Mathf.Clamp01(footIkIdleRotationWeight);
        footIkSpeedThreshold = Mathf.Max(0f, footIkSpeedThreshold);
        footIkBlendSpeed = Mathf.Max(0f, footIkBlendSpeed);
        footIkHeightOffset = Mathf.Max(0f, footIkHeightOffset);
        footIkRaycastUp = Mathf.Max(0.02f, footIkRaycastUp);
        footIkRaycastDown = Mathf.Max(0.02f, footIkRaycastDown);
        voidCheckDistance = Mathf.Max(0f, voidCheckDistance);
        voidCheckDepth = Mathf.Max(0.02f, voidCheckDepth);
        maxWalkableSlopeAngle = Mathf.Clamp(maxWalkableSlopeAngle, 0f, 89f);
        movementCollisionSkin = Mathf.Max(0.001f, movementCollisionSkin);
        movementCollisionWalkableNormalDot = Mathf.Clamp01(movementCollisionWalkableNormalDot);
        ValidateHeightProbeTraversalSettings();

        ValidateCommittedJumpSettings();
        ValidateFlightSettings();
        ApplyAnimatorSettings();
        EnsureRigidbodyCollisionSafety();
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
        PlayActionAudio(ActionAudioCue.TorchToggle);
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
        if (input.sqrMagnitude <= movementInputDeadZone * movementInputDeadZone)
        {
            ClearStoredMovementReference();
        }
    }

    public void MoveWorld(Vector2 worldInput)
    {
        moveInputIsWorldSpace = true;
        moveInput = worldInput;
        ClearStoredMovementReference();
    }

    public void SetSprintModifier(bool pressed)
    {
        sprintModifierPressed = pressed;
    }

    public void Jump()
    {
        RequestCommittedJump();
    }

    public void Stop()
    {
        moveInputIsWorldSpace = false;
        moveInput = Vector2.zero;
        smoothedInput = Vector2.zero;
        sprintModifierPressed = false;
        ClearStoredMovementReference();
        StopHorizontalVelocity();
        wasMovingForAnimator = false;
        smoothedTurnAmount = 0f;
        lastMovingLocomotionTier = WalkLocomotionTier;
        SetAnimatorBoolIfValid(isMovingParam, false);
        SetAnimatorBoolIfValid(turnInPlaceParam, false);
        SetAnimatorFloatIfValid(locomotionTierParam, lastMovingLocomotionTier);
        SetAnimatorFloatIfValid(turnParam, 0f);
        SetSpeed(0f);
    }

    public void PushScriptedMovementSuppression()
    {
        scriptedMovementSuppressionCount = Mathf.Max(0, scriptedMovementSuppressionCount + 1);
        Stop();
    }

    public void PopScriptedMovementSuppression()
    {
        if (scriptedMovementSuppressionCount <= 0)
        {
            scriptedMovementSuppressionCount = 0;
            return;
        }

        scriptedMovementSuppressionCount--;
    }

    public void PushExternalLocomotionDriver()
    {
        externalLocomotionDriverLockCount = Mathf.Max(0, externalLocomotionDriverLockCount + 1);
        Stop();
        ResetCommittedJumpRuntime();
        ClearScriptDrivenHorizontalVelocity();
    }

    public void PopExternalLocomotionDriver()
    {
        if (externalLocomotionDriverLockCount <= 0)
        {
            externalLocomotionDriverLockCount = 0;
            return;
        }

        externalLocomotionDriverLockCount--;
        if (externalLocomotionDriverLockCount <= 0)
        {
            Stop();
        }
    }

    private void FixedUpdate()
    {
        UpdateInputLock(Time.fixedDeltaTime);
        if (IsExternalLocomotionDriverActive)
        {
            UpdateGroundedState();
            UpdateObservedHorizontalVelocity(Time.fixedDeltaTime);
            ClearScriptDrivenHorizontalVelocity();
            return;
        }

        SmoothInput(Time.fixedDeltaTime);
        UpdateGroundedState();
        UpdateObservedHorizontalVelocity(Time.fixedDeltaTime);
        UpdateCommittedJump(Time.fixedDeltaTime);
        UpdateNaturalFallAnimation(Time.fixedDeltaTime);
        ApplyMovement(Time.fixedDeltaTime);
        UpdateAnimationSpeed();
        UpdateCommittedJumpAnimation();
        if (ShouldRunAnimationReconciliationInFixedUpdate())
        {
            ReconcileAnimationState(Time.fixedDeltaTime);
        }
    }

    private bool IsGroundAhead(Vector3 direction, float lookAheadDistance = -1f)
    {
        if (direction.sqrMagnitude < 0.01f || !isGrounded)
        {
            return true;
        }

        return HasGroundSupportAhead(direction, lookAheadDistance);
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

        if (IsExternalLocomotionDriverActive)
        {
            SetFootIkWeights(0f, 0f);
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
        float rotationWeight = ResolveFootIkRotationWeight(speed);
        ApplyFootIk(AvatarIKGoal.LeftFoot, HumanBodyBones.LeftFoot, mask, footIkWeightCurrent, rotationWeight);
        ApplyFootIk(AvatarIKGoal.RightFoot, HumanBodyBones.RightFoot, mask, footIkWeightCurrent, rotationWeight);
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
        float inputMagnitude = ResolveMovementInputMagnitude(effectiveInput);
        bool hasInput = inputMagnitude > 0f;
        Vector3 desiredDirection = Vector3.zero;
        if (!hasInput && moveInput.sqrMagnitude <= movementInputDeadZone * movementInputDeadZone)
        {
            ClearStoredMovementReference();
        }

        if (hasInput)
        {
            desiredDirection = GetMoveDirection(effectiveInput);
            if (desiredDirection.sqrMagnitude > 0.0001f)
            {
                desiredDirection = desiredDirection.normalized;
            }
        }

        float lookAheadDistance = ResolveCurrentTargetMoveSpeed() * inputMagnitude * Mathf.Max(0f, deltaTime);
        if (enableVoidDetection && hasInput && !IsGroundAhead(desiredDirection, lookAheadDistance))
        {
            hasInput = false;
            inputMagnitude = 0f;
            StopHorizontalVelocity();
        }

        if (!CanSimulateMovementLocally())
        {
            return;
        }

        if (TryApplyCommittedJumpMovement(deltaTime))
        {
            return;
        }

        if (TryApplyNaturalFallLandingMovement(deltaTime))
        {
            return;
        }

        if (IsLocomotionEndAnimationActive())
        {
            if (TryRestartLocomotionFromEndAnimation(
                    deltaTime,
                    out Vector3 restartDirection,
                    out float restartInputMagnitude))
            {
                desiredDirection = restartDirection;
                inputMagnitude = restartInputMagnitude;
                hasInput = true;
            }
            else
            {
                StopHorizontalVelocity();
                return;
            }
        }

        if (inputLockTimer > 0f || scriptedMovementSuppressionCount > 0 || currentHp <= 0)
        {
            CaptureCurrentRigidbodyHorizontalVelocity();
            return;
        }

        // Les clips restent in-place: le gameplay pilote le deplacement, l'Animator ne fait que presenter la pose.
        HandleGroundedLocomotionIntent(desiredDirection, inputMagnitude, deltaTime);
    }

    private bool CanSimulateMovementLocally()
    {
        NetworkManager manager = NetworkManager.Singleton;
        return manager == null || !manager.IsListening || manager.IsServer;
    }

    private bool IsLocomotionEndAnimationActive()
    {
        if (animator == null ||
            !animator.isActiveAndEnabled ||
            locomotionAnimationLayer < 0 ||
            locomotionAnimationLayer >= animator.layerCount)
        {
            return false;
        }

        if (MatchesLocomotionEndAnimationState(animator.GetCurrentAnimatorStateInfo(locomotionAnimationLayer)))
        {
            return true;
        }

        return animator.IsInTransition(locomotionAnimationLayer) &&
               MatchesLocomotionEndAnimationState(animator.GetNextAnimatorStateInfo(locomotionAnimationLayer));
    }

    private static bool MatchesLocomotionEndAnimationState(AnimatorStateInfo stateInfo)
    {
        int shortNameHash = stateInfo.shortNameHash;
        for (int i = 0; i < LocomotionEndStateHashes.Length; i++)
        {
            if (shortNameHash == LocomotionEndStateHashes[i])
            {
                return true;
            }
        }

        return false;
    }

    private bool TryRestartLocomotionFromEndAnimation(
        float deltaTime,
        out Vector3 desiredDirection,
        out float inputMagnitude)
    {
        desiredDirection = Vector3.zero;
        inputMagnitude = 0f;

        if (inputLockTimer > 0f || scriptedMovementSuppressionCount > 0 || currentHp <= 0)
        {
            return false;
        }

        if (!TryResolveLocomotionRestartIntent(out desiredDirection, out inputMagnitude))
        {
            return false;
        }

        float lookAheadDistance = ResolveCurrentTargetMoveSpeed() * inputMagnitude * Mathf.Max(0f, deltaTime);
        if (enableVoidDetection && !IsGroundAhead(desiredDirection, lookAheadDistance))
        {
            return false;
        }

        TriggerLocomotionRestartFromEnd(inputMagnitude, desiredDirection);
        return true;
    }

    private bool TryResolveLocomotionRestartIntent(out Vector3 desiredDirection, out float inputMagnitude)
    {
        Vector2 restartInput = smoothedInput;
        if (moveInput.sqrMagnitude > restartInput.sqrMagnitude)
        {
            restartInput = moveInput;
        }

        inputMagnitude = ResolveMovementInputMagnitude(restartInput);
        if (inputMagnitude <= locomotionEndRestartInputThreshold)
        {
            desiredDirection = Vector3.zero;
            return false;
        }

        desiredDirection = GetMoveDirection(restartInput);
        if (desiredDirection.sqrMagnitude <= 0.0001f)
        {
            desiredDirection = Vector3.zero;
            return false;
        }

        desiredDirection.Normalize();
        return true;
    }

    private void TriggerLocomotionRestartFromEnd(float inputMagnitude, Vector3 desiredDirection)
    {
        float presentationSpeed = inputMagnitude * ResolveCurrentTargetPresentationSpeed();
        float locomotionTier = ResolveLocomotionTier(presentationSpeed);

        lastMovingLocomotionTier = locomotionTier;
        wasMovingForAnimator = true;

        SetAnimatorFloatIfValid(locomotionTierParam, locomotionTier);
        SetAnimatorBoolIfValid(isMovingParam, true);
        SetAnimatorBoolIfValid(turnInPlaceParam, false);

        Vector3 facingDirection = GetFacingPlanarForward();
        smoothedTurnAmount = desiredDirection.sqrMagnitude > 0.0001f
            ? Mathf.Clamp(Vector3.SignedAngle(facingDirection, desiredDirection, transform.up) / 90f, -1f, 1f)
            : 0f;
        SetAnimatorFloatIfValid(turnParam, smoothedTurnAmount);
        SetSpeed(ResolveAnimatorSpeedValue(presentationSpeed));

        ResetAnimatorTriggerIfValid(moveStopTriggerParam);
        SetAnimatorTriggerIfValid(moveStartTriggerParam);
        TryCrossFadeLocomotionStartState(locomotionTier);
    }

    private bool TryCrossFadeLocomotionStartState(float locomotionTier)
    {
        return TryCrossFadeLocomotionState(
            ResolveLocomotionStartStateName(locomotionTier),
            locomotionEndRestartTransitionDuration);
    }

    private bool TryCrossFadeLocomotionState(string stateName, float transitionDuration)
    {
        return TryCrossFadeAnimatorState(
            locomotionAnimationLayer,
            stateName,
            transitionDuration);
    }

    private static string ResolveLocomotionStartStateName(float locomotionTier)
    {
        if (locomotionTier >= (JogtrotLocomotionTier + RunLocomotionTier) * 0.5f)
        {
            return RunStartStateName;
        }

        if (locomotionTier >= (WalkLocomotionTier + JogtrotLocomotionTier) * 0.5f)
        {
            return JogtrotStartStateName;
        }

        return WalkStartStateName;
    }

    private void HandleGroundedLocomotionIntent(Vector3 desiredDirection, float inputMagnitude, float deltaTime)
    {
        if (inputMagnitude <= 0f || desiredDirection.sqrMagnitude <= 0.0001f)
        {
            ClearScriptDrivenHorizontalVelocity();
            return;
        }

        float targetSpeed = ResolveCurrentTargetMoveSpeed() * inputMagnitude;
        Vector3 desiredHorizontalVelocity = desiredDirection * targetSpeed;
        Vector3 resolvedVelocity = ConstrainHorizontalVelocityAgainstWalls(desiredHorizontalVelocity, deltaTime);
        currentHorizontalVelocity = Vector3.ProjectOnPlane(resolvedVelocity, transform.up);

        RotateTowardsDesiredDirection(desiredDirection, deltaTime, currentHorizontalVelocity.magnitude);

        if (ShouldUseRigidbody() && rigidbodyTarget != null)
        {
            Vector3 velocity = rigidbodyTarget.linearVelocity;
            float verticalVelocity = isGrounded
                ? ResolveGroundedLocomotionVerticalVelocity(velocity.y)
                : velocity.y;

            rigidbodyTarget.WakeUp();
            rigidbodyTarget.linearVelocity = new Vector3(
                currentHorizontalVelocity.x,
                verticalVelocity,
                currentHorizontalVelocity.z);
            return;
        }

        Transform target = motionRoot != null ? motionRoot : transform;
        target.position += currentHorizontalVelocity * Mathf.Max(0f, deltaTime);
    }

    private void ClearScriptDrivenHorizontalVelocity()
    {
        currentHorizontalVelocity = Vector3.zero;

        if (ShouldUseRigidbody() && rigidbodyTarget != null && !rigidbodyTarget.isKinematic)
        {
            Vector3 velocity = rigidbodyTarget.linearVelocity;
            rigidbodyTarget.linearVelocity = new Vector3(0f, velocity.y, 0f);
        }
    }

    private void CaptureCurrentRigidbodyHorizontalVelocity()
    {
        if (ShouldUseRigidbody() && rigidbodyTarget != null)
        {
            Vector3 velocity = rigidbodyTarget.linearVelocity;
            currentHorizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
            return;
        }

        currentHorizontalVelocity = Vector3.zero;
    }

    private void UpdateObservedHorizontalVelocity(float deltaTime)
    {
        Vector3 worldPosition = GetWorldPosition();
        if (!hasObservedWorldPosition || deltaTime <= 0f)
        {
            lastObservedWorldPosition = worldPosition;
            hasObservedWorldPosition = true;
            return;
        }

        Vector3 delta = worldPosition - lastObservedWorldPosition;
        lastObservedWorldPosition = worldPosition;

        if (CanSimulateMovementLocally())
        {
            return;
        }

        if (delta.sqrMagnitude > 4f)
        {
            currentHorizontalVelocity = Vector3.zero;
            return;
        }

        currentHorizontalVelocity = Vector3.ProjectOnPlane(delta / deltaTime, transform.up);
    }

    private void RotateTowardsDesiredDirection(Vector3 desiredDirection, float deltaTime, float horizontalSpeed)
    {
        if (!rotateToInput || desiredDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float rotationResponsiveness = rotationSpeed;
        if (horizontalSpeed >= movingRotationSpeedThreshold && movingRotationSpeed > 0f)
        {
            rotationResponsiveness = movingRotationSpeed;
        }

        Quaternion targetRotation = Quaternion.LookRotation(desiredDirection, transform.up);
        if (ShouldUseRigidbody() && rigidbodyTarget != null)
        {
            rigidbodyTarget.MoveRotation(
                Quaternion.Slerp(rigidbodyTarget.rotation, targetRotation, rotationResponsiveness * deltaTime));
            return;
        }

        Transform target = motionRoot != null ? motionRoot : transform;
        target.rotation = Quaternion.Slerp(target.rotation, targetRotation, rotationResponsiveness * deltaTime);
    }

    private void StopHorizontalVelocity()
    {
        currentHorizontalVelocity = Vector3.zero;

        if (ShouldUseRigidbody() && rigidbodyTarget != null && !rigidbodyTarget.isKinematic)
        {
            Vector3 velocity = rigidbodyTarget.linearVelocity;
            rigidbodyTarget.linearVelocity = new Vector3(0f, velocity.y, 0f);
        }
    }

    private float ResolveCurrentTargetMoveSpeed()
    {
        float sprintSpeed = Mathf.Max(0f, moveSpeed);
        float walkingSpeed = Mathf.Clamp(walkMoveSpeed, 0f, sprintSpeed);
        return sprintModifierPressed ? sprintSpeed : walkingSpeed;
    }

    private float ResolveCurrentTargetPresentationSpeed()
    {
        float sprintPresentationSpeed = Mathf.Max(runSpeedThreshold, 0.0001f);
        float trotSpeed = Mathf.Clamp(trotPresentationSpeed, 0f, sprintPresentationSpeed);
        return sprintModifierPressed ? sprintPresentationSpeed : trotSpeed;
    }

    private float ResolveCurrentMoveSpeedScale()
    {
        float sprintSpeed = Mathf.Max(0.0001f, moveSpeed);
        return Mathf.Clamp01(ResolveCurrentTargetMoveSpeed() / sprintSpeed);
    }

    private float ScaleConfiguredLocomotionSpeed(float speed)
    {
        return Mathf.Max(0f, speed) * ResolveCurrentMoveSpeedScale();
    }

    private Vector3 ConstrainHorizontalVelocityAgainstWalls(Vector3 desiredHorizontalVelocity, float deltaTime)
    {
        heightProbeVerticalOffsetThisStep = 0f;

        if (!preventWallPenetration || deltaTime <= 0f)
        {
            return desiredHorizontalVelocity;
        }

        Vector3 desiredDisplacement = desiredHorizontalVelocity * deltaTime;
        Vector3 safeDisplacement = ResolveSafeHorizontalDisplacement(desiredDisplacement);
        ApplyHeightProbeTraversalOffsetToRigidbody(safeDisplacement);
        return Vector3.ProjectOnPlane(safeDisplacement, transform.up) / deltaTime;
    }

    private float ResolveGroundedLocomotionVerticalVelocity(float currentVerticalVelocity)
    {
        if (Mathf.Abs(heightProbeVerticalOffsetThisStep) > 0.0001f)
        {
            // La correction de marche place deja la capsule sur son support.
            // Une vitesse negative ajoute de la friction sur les contremarches; une vitesse positive ressemble a un saut.
            return Mathf.Min(currentVerticalVelocity, 0f);
        }

        return Mathf.Min(currentVerticalVelocity, -groundedStickVelocity);
    }

    private Vector3 ResolveSafeHorizontalDisplacement(Vector3 desiredDisplacement)
    {
        Vector3 up = transform.up;
        Vector3 flattenedDisplacement = Vector3.ProjectOnPlane(desiredDisplacement, up);
        if (!preventWallPenetration || flattenedDisplacement.sqrMagnitude <= 0.00000001f)
        {
            return flattenedDisplacement;
        }

        if (!TryGetMovementCapsule(out Vector3 point1, out Vector3 point2, out float radius))
        {
            return flattenedDisplacement;
        }

        int mask = GetMovementBlockingMask();
        if (mask == 0)
        {
            return flattenedDisplacement;
        }

        float castRadius = Mathf.Max(0.01f, radius - movementCollisionSkin);
        Vector3 accumulated = Vector3.zero;
        Vector3 remaining = flattenedDisplacement;

        for (int i = 0; i < 2; i++)
        {
            float distance = remaining.magnitude;
            if (distance <= 0.0001f)
            {
                break;
            }

            Vector3 direction = remaining / distance;
            Vector3 castPoint1 = point1 + accumulated;
            Vector3 castPoint2 = point2 + accumulated;
            if (!TryGetHorizontalBlockingHit(castPoint1, castPoint2, castRadius, direction, distance + movementCollisionSkin, mask, out RaycastHit hit))
            {
                accumulated += remaining;
                break;
            }

            if (TryResolveHeightProbeTraversal(castPoint1, castPoint2, radius, remaining, mask, hit, out Vector3 stepDisplacement))
            {
                accumulated += stepDisplacement;
                break;
            }

            float allowedDistance = Mathf.Max(0f, hit.distance - movementCollisionSkin);
            if (allowedDistance > 0f)
            {
                accumulated += direction * Mathf.Min(allowedDistance, distance);
            }

            Vector3 consumed = direction * Mathf.Min(distance, Mathf.Max(0f, hit.distance));
            Vector3 leftover = remaining - consumed;
            Vector3 slide = Vector3.ProjectOnPlane(leftover, hit.normal);
            slide = Vector3.ProjectOnPlane(slide, up);
            if (slide.sqrMagnitude <= 0.00000001f || Vector3.Dot(slide, remaining) <= 0f)
            {
                break;
            }

            remaining = slide;
        }

        if (TryResolveHeightProbeGroundSnap(point1, point2, radius, accumulated, mask, out Vector3 snappedDisplacement))
        {
            return snappedDisplacement;
        }

        return accumulated;
    }

    private bool TryGetMovementCapsule(out Vector3 point1, out Vector3 point2, out float radius)
    {
        Vector3 up = transform.up;
        if (TryGetLocomotionCapsule(out Vector3 center, out float capsuleRadius, out float height))
        {
            float segmentHalf = Mathf.Max(0f, (height * 0.5f) - capsuleRadius);
            point1 = center + up * segmentHalf;
            point2 = center - up * segmentHalf;
            radius = capsuleRadius;
            return true;
        }

        point1 = Vector3.zero;
        point2 = Vector3.zero;
        radius = 0f;
        return false;
    }

    private int GetMovementBlockingMask()
    {
        return GetCollisionMatrixMask();
    }

    private bool TryGetHorizontalBlockingHit(
        Vector3 point1,
        Vector3 point2,
        float radius,
        Vector3 direction,
        float distance,
        int mask,
        out RaycastHit hit)
    {
        hit = default;
        if (mask == 0 || distance <= 0.0001f)
        {
            return false;
        }

        int hitCount = Physics.CapsuleCastNonAlloc(
            point1,
            point2,
            radius,
            direction,
            movementCastHits,
            distance,
            mask,
            QueryTriggerInteraction.Ignore);
        float bestDistance = float.PositiveInfinity;
        int bestIndex = -1;
        Vector3 up = transform.up;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = movementCastHits[i].collider;
            if (col == null || IsSelfCollider(col))
            {
                continue;
            }

            if (Vector3.Dot(movementCastHits[i].normal, up) >= movementCollisionWalkableNormalDot)
            {
                continue;
            }

            float hitDistance = movementCastHits[i].distance;
            if (hitDistance < bestDistance)
            {
                bestDistance = hitDistance;
                bestIndex = i;
            }
        }

        if (bestIndex < 0)
        {
            return false;
        }

        hit = movementCastHits[bestIndex];
        return true;
    }

    private struct GroundProbeSample
    {
        public Vector3 point;
        public Vector3 normal;
        public Collider collider;
    }

    private void UpdateGroundedState()
    {
        if (Time.time < groundIgnoreUntilTime)
        {
            isGrounded = false;
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
        float probeDistance = Mathf.Max(0.02f, jumpGroundCheckDistance);
        float probeRadius = 0.05f;
        if (TryBuildGroundProbeContext(out GroundProbeContext probeContext))
        {
            probeRadius = Mathf.Max(0.05f, probeContext.radius * jumpGroundCheckRadiusScale);
        }

        return TryProbeGroundedSupport(probeDistance, probeRadius, out _, out _);
    }

    private void SetFootIkWeights(float positionWeight, float rotationWeight)
    {
        animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, positionWeight);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, rotationWeight);
        animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, positionWeight);
        animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, rotationWeight);
    }

    private float ResolveFootIkRotationWeight(float speed)
    {
        float movingRotationWeight = Mathf.Clamp01(footIkRotationWeight);
        float idleRotationWeight = Mathf.Clamp01(Mathf.Min(footIkIdleRotationWeight, movingRotationWeight));
        if (walkSpeedThreshold <= 0f)
        {
            return movingRotationWeight;
        }

        float t = Mathf.Clamp01(speed / walkSpeedThreshold);
        return Mathf.Lerp(idleRotationWeight, movingRotationWeight, t);
    }

    private void ApplyFootIk(AvatarIKGoal goal, HumanBodyBones bone, int mask, float baseWeight, float rotationWeightScale)
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
        float rotationWeight = baseWeight * rotationWeightScale;
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

    private bool TryGetLocomotionCapsule(out Vector3 center, out float radius, out float height)
    {
        if (TryGetActiveFlightMotorCapsule(out center, out radius, out height))
        {
            return true;
        }

        CapsuleCollider capsule = locomotionCapsule;
        if (capsule == null)
        {
            capsule = GetComponent<CapsuleCollider>();
            locomotionCapsule = capsule;
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

    private bool TryGetActiveFlightMotorCapsule(out Vector3 center, out float radius, out float height)
    {
        center = Vector3.zero;
        radius = 0f;
        height = 0f;

        if (!IsExternalLocomotionDriverActive)
        {
            return false;
        }

        if (!TryGetActiveFlightCharacterController(out CharacterController controller) ||
            controller == null)
        {
            return false;
        }

        Transform controllerTransform = controller.transform;
        Vector3 scale = controllerTransform.lossyScale;
        float maxXZ = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float absY = Mathf.Abs(scale.y);
        radius = Mathf.Max(0.01f, controller.radius * maxXZ);
        height = Mathf.Max(controller.height * absY, radius * 2f);
        center = controllerTransform.TransformPoint(controller.center);
        return true;
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

    public void TickTorchLifetimeForExternalLocomotion(float deltaTime)
    {
        UpdateTorchLifetime(deltaTime);
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

        if (pendingTorchVisualTransition == TorchVisualTransition.Unequip &&
            !IsTorchVisualTransitionAnimationComplete(TorchVisualTransition.Unequip))
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

    private bool IsTorchVisualTransitionAnimationComplete(TorchVisualTransition transition)
    {
        int layerIndex = GetTorchAnimationLayerIndex();
        if (layerIndex < 0)
        {
            return true;
        }

        int stateHash = transition == TorchVisualTransition.Equip
            ? TorchEquipStateHash
            : TorchUnequipStateHash;

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(layerIndex);
        if (MatchesTorchAnimationState(currentState, stateHash))
        {
            return currentState.normalizedTime >= 1f;
        }

        if (animator.IsInTransition(layerIndex) &&
            MatchesTorchAnimationState(animator.GetNextAnimatorStateInfo(layerIndex), stateHash))
        {
            return false;
        }

        return torchVisualTransitionStateObserved;
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
        float responsiveness = ShouldUseInputReleaseSmoothing()
            ? inputReleaseResponsiveness
            : inputResponsiveness;

        if (responsiveness <= 0f)
        {
            smoothedInput = moveInput;
            return;
        }

        float t = 1f - Mathf.Exp(-responsiveness * deltaTime);
        smoothedInput = Vector2.Lerp(smoothedInput, moveInput, t);
        if (moveInput.sqrMagnitude <= movementInputDeadZone * movementInputDeadZone &&
            smoothedInput.sqrMagnitude <= movementInputDeadZone * movementInputDeadZone)
        {
            smoothedInput = Vector2.zero;
        }
    }

    private bool ShouldUseInputReleaseSmoothing()
    {
        float deadZoneSqr = movementInputDeadZone * movementInputDeadZone;
        return moveInput.sqrMagnitude <= deadZoneSqr && smoothedInput.sqrMagnitude > deadZoneSqr;
    }

    private float ResolveMovementInputMagnitude(Vector2 input)
    {
        float magnitude = Mathf.Clamp01(input.magnitude);
        if (magnitude <= movementInputDeadZone)
        {
            return 0f;
        }

        if (movementInputDeadZone >= 0.999f)
        {
            return 1f;
        }

        return Mathf.InverseLerp(movementInputDeadZone, 1f, magnitude);
    }

    private void UpdateAnimationSpeed()
    {
        if (animator == null)
        {
            return;
        }

        AnimationGameplaySnapshot snapshot = CreateAnimationGameplaySnapshot();
        if (!string.IsNullOrWhiteSpace(speedParam))
        {
            SetSpeed(snapshot.AnimatorSpeed);
        }

        UpdateLocomotionAnimatorSignals(
            snapshot,
            deltaTime: Time.inFixedTimeStep ? Time.fixedDeltaTime : Time.deltaTime);
    }

    private float ResolveAnimationPresentationSpeed(Vector3 velocity)
    {
        float inputMagnitude = ResolveMovementInputMagnitude(smoothedInput);
        if (inputMagnitude > 0f)
        {
            return inputMagnitude * ResolveCurrentTargetPresentationSpeed();
        }

        return velocity.magnitude;
    }

    private float ResolveLocomotionTier(float presentationSpeed)
    {
        float speed = Mathf.Max(0f, presentationSpeed);
        float jogThreshold = Mathf.Lerp(walkSpeedThreshold, trotPresentationSpeed, 0.5f);
        float runThreshold = Mathf.Lerp(trotPresentationSpeed, runSpeedThreshold, 0.5f);

        if (speed >= runThreshold)
        {
            return RunLocomotionTier;
        }

        if (speed >= jogThreshold)
        {
            return JogtrotLocomotionTier;
        }

        return WalkLocomotionTier;
    }

    private Vector3 ResolveAnimatorDesiredDirection(Vector3 velocity)
    {
        Vector3 desiredDirection = Vector3.zero;

        if (ResolveMovementInputMagnitude(smoothedInput) > 0f)
        {
            desiredDirection = GetMoveDirection(smoothedInput);
        }
        else if (velocity.sqrMagnitude > 0.0001f)
        {
            desiredDirection = velocity;
        }

        desiredDirection.y = 0f;
        if (desiredDirection.sqrMagnitude > 0.0001f)
        {
            desiredDirection.Normalize();
        }

        return desiredDirection;
    }

    private Vector3 GetFacingPlanarForward()
    {
        Transform target = motionRoot != null ? motionRoot : transform;
        Vector3 forward = target.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            return Vector3.forward;
        }

        return forward.normalized;
    }

    private float ResolveAnimatorSpeedValue(float rawSpeed)
    {
        float animSpeed = Mathf.Max(0f, rawSpeed);
        return ResolveContinuousAnimatorSpeed(animSpeed);
    }

    private float ResolveContinuousAnimatorSpeed(float rawSpeed)
    {
        // En locomotion continue, on remappe la vitesse gameplay vers l'echelle
        // attendue par le blend tree pour garder Walk/Run coherents.
        if (walkSpeedThreshold <= 0f)
        {
            return Mathf.Min(rawSpeed, runAnimValue);
        }

        if (rawSpeed <= walkSpeedThreshold)
        {
            float walkT = Mathf.InverseLerp(0f, walkSpeedThreshold, rawSpeed);
            return Mathf.Lerp(idleAnimValue, walkAnimValue, walkT);
        }

        if (runSpeedThreshold <= walkSpeedThreshold)
        {
            return runAnimValue;
        }

        if (rawSpeed <= runSpeedThreshold)
        {
            float runT = Mathf.InverseLerp(walkSpeedThreshold, runSpeedThreshold, rawSpeed);
            return Mathf.Lerp(walkAnimValue, runAnimValue, runT);
        }

        return runAnimValue;
    }

    private Vector3 GetCurrentHorizontalVelocity()
    {
        if (!CanSimulateMovementLocally())
        {
            return currentHorizontalVelocity;
        }

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

    private Vector3 GetMoveDirection(Vector2 input, bool inputRepresentsRawMovement = false)
    {
        if (moveInputIsWorldSpace)
        {
            ClearStoredMovementReference();
            return new Vector3(input.x, 0f, input.y);
        }

        Vector3 move = new Vector3(input.x, 0f, input.y);
        if (!ShouldUseCameraRelativeInput())
        {
            ClearStoredMovementReference();
            return move;
        }

        if (!TryResolveMovementBasis(out Vector3 camForward, out Vector3 camRight, out bool fixedCameraBasis))
        {
            ClearStoredMovementReference();
            return move;
        }

        if (!fixedCameraBasis)
        {
            ClearStoredMovementReference();
            return camRight * input.x + camForward * input.y;
        }

        if (TryResolveStoredMovementBasis(
            input,
            inputRepresentsRawMovement,
            camForward,
            camRight,
            out Vector3 storedMoveForward,
            out Vector3 storedMoveRight))
        {
            return storedMoveRight * input.x + storedMoveForward * input.y;
        }

        return camRight * input.x + camForward * input.y;
    }

    public Vector2 GetWorldSpaceInput(Vector2 input)
    {
        Vector3 direction = GetMoveDirection(input, inputRepresentsRawMovement: true);
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

        if (!TryResolveMovementBasis(out Vector3 camForward, out Vector3 camRight))
        {
            return new Vector2(planar.x, planar.z);
        }

        float x = Vector3.Dot(planar, camRight);
        float y = Vector3.Dot(planar, camForward);
        return new Vector2(x, y);
    }

    private bool ShouldUseCameraRelativeInput()
    {
        return useCameraRelative;
    }

    private bool TryResolveStoredMovementBasis(
        Vector2 input,
        bool inputRepresentsRawMovement,
        Vector3 currentForward,
        Vector3 currentRight,
        out Vector3 moveForward,
        out Vector3 moveRight)
    {
        moveForward = currentForward;
        moveRight = currentRight;

        if (!preserveFixedCameraMovementContinuity)
        {
            ClearStoredMovementReference();
            return false;
        }

        Vector2 referenceInput = ResolveMovementReferenceInput(input, inputRepresentsRawMovement);
        float inputMagnitude = referenceInput.magnitude;
        if (inputMagnitude <= movementInputDeadZone)
        {
            ClearStoredMovementReference();
            return false;
        }

        Vector2 inputDirection = referenceInput / inputMagnitude;
        if (!storedMovementReferenceActive)
        {
            if (!TryStoreMovementReference(currentForward, currentRight, inputDirection))
            {
                return false;
            }
        }
        else
        {
            float inputAngle = Vector2.Angle(storedInput, inputDirection);
            if (inputAngle > fixedCameraMovementInputRefreshAngle)
            {
                if (!TryStoreMovementReference(currentForward, currentRight, inputDirection))
                {
                    return false;
                }
            }
            else
            {
                BlendStoredMovementReference(currentForward, currentRight);
            }
        }

        moveForward = storedForward;
        moveRight = storedRight;
        return storedMovementReferenceActive;
    }

    private Vector2 ResolveMovementReferenceInput(Vector2 input, bool inputRepresentsRawMovement)
    {
        if (inputRepresentsRawMovement)
        {
            return input;
        }

        if (moveInputIsWorldSpace)
        {
            return Vector2.zero;
        }

        return moveInput;
    }

    private bool TryStoreMovementReference(Vector3 forward, Vector3 right, Vector2 inputDirection)
    {
        if (!TryNormalizeMovementBasis(forward, right, out Vector3 normalizedForward, out Vector3 normalizedRight))
        {
            ClearStoredMovementReference();
            return false;
        }

        storedForward = normalizedForward;
        storedRight = normalizedRight;
        storedInput = inputDirection;
        storedMovementReferenceActive = true;
        return true;
    }

    private void BlendStoredMovementReference(Vector3 targetForward, Vector3 targetRight)
    {
        if (fixedCameraMovementReferenceBlendSharpness <= 0f)
        {
            return;
        }

        if (!storedMovementReferenceActive)
        {
            return;
        }

        float deltaTime = Time.inFixedTimeStep ? Time.fixedDeltaTime : Time.deltaTime;
        if (deltaTime <= 0f)
        {
            return;
        }

        float t = 1f - Mathf.Exp(-fixedCameraMovementReferenceBlendSharpness * deltaTime);
        Vector3 blendedForward = Vector3.Slerp(storedForward, targetForward, t);
        Vector3 blendedRight = Vector3.Slerp(storedRight, targetRight, t);
        if (!TryNormalizeMovementBasis(blendedForward, blendedRight, out Vector3 normalizedForward, out Vector3 normalizedRight))
        {
            return;
        }

        storedForward = normalizedForward;
        storedRight = normalizedRight;
    }

    private static bool TryNormalizeMovementBasis(
        Vector3 forward,
        Vector3 right,
        out Vector3 normalizedForward,
        out Vector3 normalizedRight)
    {
        normalizedForward = Vector3.ProjectOnPlane(forward, Vector3.up);
        normalizedRight = Vector3.zero;
        if (normalizedForward.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        normalizedForward.Normalize();

        Vector3 projectedRight = Vector3.ProjectOnPlane(right, Vector3.up);
        normalizedRight = Vector3.Cross(Vector3.up, normalizedForward);
        if (normalizedRight.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        normalizedRight.Normalize();
        if (projectedRight.sqrMagnitude > 0.0001f && Vector3.Dot(normalizedRight, projectedRight) < 0f)
        {
            normalizedRight = -normalizedRight;
        }

        return true;
    }

    private void ClearStoredMovementReference()
    {
        storedMovementReferenceActive = false;
        storedForward = Vector3.zero;
        storedRight = Vector3.zero;
        storedInput = Vector2.zero;
    }

    private bool TryResolveMovementBasis(out Vector3 camForward, out Vector3 camRight)
    {
        return TryResolveMovementBasis(out camForward, out camRight, out _);
    }

    private bool TryResolveMovementBasis(out Vector3 camForward, out Vector3 camRight, out bool fixedCameraBasis)
    {
        camForward = Vector3.zero;
        camRight = Vector3.zero;
        fixedCameraBasis = false;

        CameraController controller = ResolveMovementCameraController();
        if (controller != null && controller.TryGetFixedCameraMovementBasis(out camForward, out camRight))
        {
            if (controller.mainCam != null && controller.mainCam.isActiveAndEnabled)
            {
                referenceCamera = controller.mainCam;
            }

            fixedCameraBasis = true;
            return true;
        }

        Camera cam = ResolveMovementCamera();
        if (cam == null)
        {
            return false;
        }

        camForward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        camRight = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up);
        if (camForward.sqrMagnitude <= 0.0001f || camRight.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        camForward.Normalize();
        camRight.Normalize();
        return true;
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

    private void EnsureRigidbodyCollisionSafety()
    {
        if (rigidbodyTarget == null)
        {
            return;
        }

        rigidbodyTarget.collisionDetectionMode = rigidbodyTarget.isKinematic
            ? CollisionDetectionMode.ContinuousSpeculative
            : CollisionDetectionMode.ContinuousDynamic;

        if (Application.isPlaying)
        {
            ApplyLowFrictionLocomotionMaterial();
        }
    }

    private void ApplyLowFrictionLocomotionMaterial()
    {
        if (!useLowFrictionLocomotionMaterial)
        {
            return;
        }

        CapsuleCollider capsule = locomotionCapsule;
        if (capsule == null)
        {
            capsule = GetComponent<CapsuleCollider>();
            locomotionCapsule = capsule;
        }

        if (capsule == null)
        {
            return;
        }

        if (!overrideExistingLocomotionMaterial && capsule.sharedMaterial != null)
        {
            return;
        }

        capsule.sharedMaterial = GetLowFrictionLocomotionMaterial();
    }

    private static PhysicsMaterial GetLowFrictionLocomotionMaterial()
    {
        if (lowFrictionLocomotionMaterial != null)
        {
            return lowFrictionLocomotionMaterial;
        }

        lowFrictionLocomotionMaterial = new PhysicsMaterial("SquadCharacter_LowFriction")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum,
            hideFlags = HideFlags.HideAndDontSave
        };

        return lowFrictionLocomotionMaterial;
    }

    private bool ShouldUseRigidbody()
    {
        return rigidbodyTarget != null;
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
