using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Controle le mouvement et l'inventaire runtime d'un personnage de la squad.
[RequireComponent(typeof(Animator), typeof(Rigidbody))]
[RequireComponent(typeof(LitOpsiveLocomotionBridge))]
public partial class SquadCharacterController : MonoBehaviour
{
    private const float WalkLocomotionTier = 1f;
    private const int MaxEnabledCombatItems = 3;

    private enum FlameVisualTransition
    {
        None,
        Equip,
        Unequip
    }

    private const string FlameAnimationLayerName = "Upper Body Flame";
    private const float FlameAnimationStateFallbackDelay = 0.2f;
    private const float FlameAnimationVisualDelay = 0.5f;
    private static readonly int FlameEquipStateHash = Animator.StringToHash("Flame_Equip");
    private static readonly int FlameLocomotionStateHash = Animator.StringToHash("Flame_Locomotion");
    private static readonly int FlameOffStateHash = Animator.StringToHash("Flame_Off");
    private static readonly int FlameUnequipStateHash = Animator.StringToHash("Flame_Unequip");
    private const bool CharacterFlameSystemEnabled = false;

    [Header("Inventory")]
    [SerializeField, HideInInspector] private List<Item> items = new List<Item>();
    [SerializeField, HideInInspector] private List<Item> equippedInteractionItems = new List<Item>();
    [SerializeField, HideInInspector] private List<Item> enabledCombatItems = new List<Item>();
    [SerializeField, HideInInspector] private List<CombatDefenseItemHitPointData> combatDefenseItemHitPoints = new List<CombatDefenseItemHitPointData>();
    [SerializeField, Tooltip("Duree initiale de la flamme (secondes).")]
    private int startingFlameSeconds = 300;
    [SerializeField, Tooltip("Duree restante de la flamme (secondes).")]
    private int flameSecondsRemaining = 300;
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

    [Header("Facial")]
    [SerializeField, Tooltip("Controleur facial a neutraliser pendant la locomotion.")]
    private FacialExpressionController facialExpressionController;
    [SerializeField, Tooltip("Force l'expression Idle/Neutre quand le personnage se deplace.")]
    private bool forceIdleFacialExpressionWhileMoving = true;
    [SerializeField, Range(0f, 1f), Tooltip("Seuil d'input a partir duquel le mouvement force une expression neutre.")]
    private float facialMovementInputThreshold = 0.08f;
    [SerializeField, Min(0f), Tooltip("Duree de fade vers Idle quand le mouvement reprend le controle du visage.")]
    private float facialMovementIdleFadeDuration = 0.08f;

    [Header("Animator Params")]
    [SerializeField, Tooltip("Nom du parametre Speed dans l'Animator.")]
    private string speedParam = "Speed";
    [SerializeField, Tooltip("Damping du parametre Speed.")]
    private float speedDampTime = 0.06f;
    [SerializeField, Tooltip("Utilise un damping sur Speed.")]
    private bool useSpeedDamping = false;

    [Header("Animation Feel")]
    [SerializeField, Tooltip("Nom du bool optionnel pour distinguer idle et locomotion.")]
    private string isMovingParam = "IsMoving";
    [SerializeField, Tooltip("Nom du float optionnel qui selectionne le Start/Stop locomotion: 1=Walk, 2=Jogtrot, 3=Run.")]
    private string locomotionTierParam = "LocomotionTier";
    [SerializeField, Tooltip("Nom du float optionnel signe (-1..1) mesurant le besoin de rotation.")]
    private string turnParam = "Turn";
    [SerializeField, Tooltip("Nom du bool optionnel actif quand un pivot sur place est pertinent.")]
    private string turnInPlaceParam = "TurnInPlace";

    [Header("Movement")]
    [SerializeField, Tooltip("Vitesse max de marche quand le modificateur de course n'est pas maintenu.")]
    private float walkMoveSpeed = 5f;
    [SerializeField, Tooltip("Vitesse de deplacement.")]
    private float moveSpeed = 6.5f;
    [SerializeField, Range(0f, 1f), Tooltip("Zone morte gameplay de la locomotion InPlace.")]
    private float movementInputDeadZone = 0.08f;
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
    [SerializeField, Tooltip("Utilise le FixedUpdate de l'Animator. Laisser désactivé avec UCC, qui lisse déjà la simulation physique.")]
    private bool animatePhysics;
    private bool storedMovementReferenceActive;
    private Vector3 storedForward;
    private Vector3 storedRight;
    private Vector2 storedInput;

    [Header("Flame")]
    [SerializeField, Tooltip("Autorise TriggerMunin a allumer/eteindre la flamme.")]
    private bool allowFlameToggle = true;
    [SerializeField, Tooltip("Nom du parent de la flamme.")]
    private string flameParentName = "Stuff";
    [SerializeField, Tooltip("Nom du child de la flamme.")]
    private string flameChildName = "Flame";
    [SerializeField, Tooltip("Parametre bool de flamme.")]
    private string flameBoolParam = "Flame";
    [SerializeField, Tooltip("Flamme active au demarrage.")]
    private bool flameStartsActive = true;
    [SerializeField, Tooltip("Lit l'etat depuis la hierarchie.")]
    private bool initializeFlameFromHierarchy = true;
    [SerializeField, Range(0f, 1f), Tooltip("Poids du layer flamme a l'arret.")]
    private float flameUpperBodyIdleLayerWeight = 0.92f;
    [SerializeField, Range(0f, 1f), Tooltip("Poids du layer flamme en locomotion rapide.")]
    private float flameUpperBodyMovingLayerWeight = 0.76f;
    [SerializeField, Tooltip("Vitesse de lissage du poids du layer flamme.")]
    private float flameUpperBodyLayerWeightResponsiveness = 10f;

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
    [SerializeField, Tooltip("Applique un materiau sans friction a la capsule de locomotion pour eviter que les contremarches ralentissent le Rigidbody.")]
    private bool useLowFrictionLocomotionMaterial = true;
    [SerializeField, Tooltip("Remplace aussi un PhysicMaterial deja assigne sur la capsule de locomotion.")]
    private bool overrideExistingLocomotionMaterial;

    private int scriptedMovementSuppressionCount;
    private int externalLocomotionDriverLockCount;
    private bool sprintModifierPressed;
    private bool isGrounded;
    private Transform flameTransform;
    private MuninController syncedMuninChargeController;
    private bool flameInitialized;
    private bool flameEquipped;
    private bool flameVisualEquipped;
    private FlameVisualTransition pendingFlameVisualTransition;
    private bool flameVisualTransitionStateObserved;
    private float flameVisualTransitionTimer;
    private float flameDrainTimer;
    private float nextCollisionRefreshTime;
    private bool collidersDirty = true;
    private readonly List<Collider> cachedColliders = new List<Collider>();
    private CapsuleCollider locomotionCapsule;
    [Header("Audio")]
    [SerializeField] private AudioListener audioListener;
    [SerializeField] private bool searchAudioListenerInChildren = true;
    private bool audioListenerActive;
    private NetworkObject cachedNetworkObject;
    private static PhysicsMaterial lowFrictionLocomotionMaterial;

    private static readonly List<SquadCharacterController> activeCharacters = new List<SquadCharacterController>();
    private static readonly List<SquadCharacterController> registeredCharacters = new List<SquadCharacterController>();

    public CharacterData CharacterData => characterData;

    public IReadOnlyList<Item> Items => items;

    public IReadOnlyList<Item> EquippedInteractionItems => equippedInteractionItems;

    public IReadOnlyList<Item> EnabledCombatItems => enabledCombatItems;

    public IReadOnlyList<CombatDefenseItemHitPointData> CombatDefenseItemHitPoints => combatDefenseItemHitPoints;

    public IReadOnlyList<Skill> Skills => characterData != null ? characterData.skills : null;

    public int CurrentHp => currentHp;

    public int MaxHp => maxHp;

    public bool IsMovementInputSuppressed => scriptedMovementSuppressionCount > 0 || currentHp <= 0;

    public event System.Action<SquadCharacterController> HealthChanged;

    public bool IsGrounded => TryGetUccGrounded(out bool uccGrounded) && uccGrounded;
    public bool IsExternalLocomotionDriverActive => externalLocomotionDriverLockCount > 0;
    public bool IsCharacterFlameSystemEnabled => CharacterFlameSystemEnabled;
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
        ResolveFacialExpressionController();
        ApplyAnimatorSettings();
        EnsureRigidbodyCollisionSafety();
        InitializeFlameState();
    }

    private void Update()
    {
        // Flamme + collisions en runtime.
        if (CharacterFlameSystemEnabled && !IsExternalLocomotionDriverActive)
        {
            UpdateFlameLifetime(Time.deltaTime);
        }

        RefreshCharacterCollisionsIfNeeded();
        if (!IsExternalLocomotionDriverActive)
        {
            UpdateAudioListenerState(false);
        }

        UpdateSittingState();

        if (!IsExternalLocomotionDriverActive)
        {
            UpdateLocalInteractionDetection();
        }

    }

    private void LateUpdate()
    {
        UpdateFlameVisualTransition();
        UpdateFlameAnimationLayerWeight();
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
            Debug.LogError("SquadCharacterController requires a Rigidbody for the UCC character stack.", this);
        }

        if (locomotionCapsule == null)
        {
            locomotionCapsule = GetComponent<CapsuleCollider>();
        }

        if (motionRoot == null)
        {
            motionRoot = transform;
        }

        ResolveFacialExpressionController();
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
        InitializeFlameState();
        RefreshAnimationReferences();
        InitializeSittingState();
    }

    private void OnEnable()
    {
        RegisterCharacter();
        CacheAudioListener();
        CacheNetworkObject();
        RefreshAnimationReferences();
        UpdateAudioListenerState(true);
        ResetSittingIdleTimer();
    }

    private void OnDisable()
    {
        CancelSittingState();
        ClearLocalInteractionTarget();
        SetAudioListenerActive(false);
        UnregisterCharacter();
    }

    private void OnDestroy()
    {
        BindMuninChargeController(null);
    }

    private void OnTransformChildrenChanged()
    {
        MarkCollidersDirty();
        BindMuninChargeController();
        ApplyMuninChargeStateFromCharacterData();
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
                Transform currentRoot = manager.currentCharacter.transform;
                shouldBeActive = transform == currentRoot || transform.IsChildOf(currentRoot);
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
        EnsureEnabledCombatList();
        EnsureCombatDefenseItemHitPointsList();
        InitializeHealthFromCharacterData(resetHpOnBind);

        if (characterData == null)
        {
            BindMuninChargeController(null);
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
                        $"[InventoryInit] bind='{name}' character='{characterData.name}' initializeInventory={initializeInventory} path='apply_starter_items' inventoryInitialized={characterData.inventoryInitialized} forceStarterItems={forceStarterItems} reason='{starterReason}' runtimeInventoryCount={characterData.inventoryItems?.Count ?? -1} starterStackCount={characterData.starterItemsWithQuantity?.Count ?? -1} flameSeconds={characterData.flameSecondsRemaining}",
                        this);
                }

                ApplyStarterItems(characterData, true);
                characterData.inventoryInitialized = true;
                SyncFlameStateToCharacterData();
            }
            else
            {
                if (logInventoryInitialization)
                {
                    Debug.Log(
                        $"[InventoryInit] bind='{name}' character='{characterData.name}' initializeInventory={initializeInventory} path='load_runtime_inventory' inventoryInitialized={characterData.inventoryInitialized} runtimeInventoryCount={characterData.inventoryItems?.Count ?? -1} equippedCount={characterData.equippedInteractionItems?.Count ?? -1} flameSeconds={characterData.flameSecondsRemaining} flameEquipped={characterData.flameEquipped}",
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
                    $"[InventoryInit] bind='{name}' character='{characterData.name}' initializeInventory={initializeInventory} path='load_runtime_inventory_without_init' inventoryInitialized={characterData.inventoryInitialized} runtimeInventoryCount={characterData.inventoryItems?.Count ?? -1} equippedCount={characterData.equippedInteractionItems?.Count ?? -1} flameSeconds={characterData.flameSecondsRemaining} flameEquipped={characterData.flameEquipped}",
                    this);
            }

            LoadInventoryFromCharacterData();
        }

        BindMuninChargeController();
        ApplyMuninChargeStateFromCharacterData();
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

        if (data.flameSecondsRemaining > 0 || data.flameEquipped)
        {
            reason = "runtime_flame_state_already_present";
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
        int resolvedMaxHp = Mathf.Max(1, dataHp);
        int resolvedCurrentHp = resetCurrent ? resolvedMaxHp : currentHp;
        if (clampHpToMax)
        {
            resolvedCurrentHp = Mathf.Clamp(resolvedCurrentHp, 0, resolvedMaxHp);
        }

        SetHealth(resolvedCurrentHp, resolvedMaxHp);
    }

    public void SetHealth(int current, int max)
    {
        LitUccDamageBridge uccDamageBridge = GetComponent<LitUccDamageBridge>();
        if (uccDamageBridge != null && uccDamageBridge.TrySetAuthorityHealth(current, max))
        {
            return;
        }

        SetHealthLocal(current, max);
    }

    public void SetCurrentHp(int value)
    {
        LitUccDamageBridge uccDamageBridge = GetComponent<LitUccDamageBridge>();
        if (uccDamageBridge != null && uccDamageBridge.TrySetAuthorityCurrentHealth(value))
        {
            return;
        }

        SetCurrentHpLocal(value);
    }

    public void SetMaxHp(int value, bool keepCurrent = true)
    {
        LitUccDamageBridge uccDamageBridge = GetComponent<LitUccDamageBridge>();
        if (uccDamageBridge != null && uccDamageBridge.TrySetAuthorityMaxHealth(value, keepCurrent))
        {
            return;
        }

        SetMaxHpLocal(value, keepCurrent);
    }

    internal void SetHealthFromAuthority(int current, int max)
    {
        SetHealthLocal(current, max);
    }

    private void SetHealthLocal(int current, int max)
    {
        int resolvedMaxHp = Mathf.Max(1, max);
        int resolvedCurrentHp = Mathf.Clamp(current, 0, resolvedMaxHp);
        if (resolvedMaxHp == maxHp && resolvedCurrentHp == currentHp)
        {
            return;
        }

        maxHp = resolvedMaxHp;
        currentHp = resolvedCurrentHp;
        NotifyHealthChanged();
    }

    private void SetCurrentHpLocal(int value)
    {
        int clamped = clampHpToMax ? Mathf.Clamp(value, 0, maxHp) : Mathf.Max(0, value);
        if (clamped == currentHp)
        {
            return;
        }

        currentHp = clamped;
        NotifyHealthChanged();
    }

    private void SetMaxHpLocal(int value, bool keepCurrent)
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
        HealthChanged?.Invoke(this);

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

        if (IsFlameItem(item))
        {
            if (!items.Contains(item))
            {
                items.Add(item);
            }

            AddFlameSeconds(quantity);
            SyncFlameStateToCharacterData();
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

    public int FlameSecondsRemaining => CharacterFlameSystemEnabled ? Mathf.Max(0, flameSecondsRemaining) : 0;

    public Item FlameItem => CharacterFlameSystemEnabled ? GetFlameItem() : null;

    public bool HasFlameItem => CharacterFlameSystemEnabled && FlameItem != null;

    public bool IsFlameEquipped => CharacterFlameSystemEnabled && flameEquipped;

    public static IReadOnlyList<SquadCharacterController> ActiveCharacters => registeredCharacters;

    public void ResetFlameToMax(int maxSeconds, bool ensureFlameItem = true)
    {
        if (!CharacterFlameSystemEnabled)
        {
            DisableCharacterFlameState();
            return;
        }

        int target = maxSeconds > 0 ? maxSeconds : startingFlameSeconds;
        if (target <= 0)
        {
            return;
        }

        EnsureInventoryList();
        MarkInventoryInitialized();

        if (ensureFlameItem && !HasFlameItem)
        {
            Item flameItem = FindFlameItemInCharacterData();
            if (flameItem != null && !items.Contains(flameItem))
            {
                items.Add(flameItem);
            }
        }

        flameSecondsRemaining = Mathf.Max(0, target);
        if (HasFlameItem && flameSecondsRemaining > 0 && !flameEquipped && flameStartsActive)
        {
            SetFlameEquipped(true);
        }

        SyncFlameStateToCharacterData();
    }

    private Item FindFlameItemInCharacterData()
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
            if (IsFlameItem(item))
            {
                return item;
            }
        }

        return null;
    }

    public void ApplyInventoryState(
        List<Item> newItems,
        int flameSeconds,
        bool equipFlame,
        List<Item> newEquippedInteractionItems = null,
        List<Item> newEnabledCombatItems = null,
        List<CombatDefenseItemHitPointData> newCombatDefenseItemHitPoints = null)
    {
        EnsureInventoryList();
        EnsureEquippedInteractionList();
        EnsureEnabledCombatList();
        EnsureCombatDefenseItemHitPointsList();
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
        ApplyEnabledCombatItems(newEnabledCombatItems);
        if (newCombatDefenseItemHitPoints != null)
        {
            ApplyCombatDefenseItemHitPoints(newCombatDefenseItemHitPoints);
        }
        else
        {
            SanitizeCombatDefenseItemHitPoints();
        }

        if (!CharacterFlameSystemEnabled)
        {
            DisableCharacterFlameState();
            SyncInteractionEquipmentToCharacterData();
            SyncCombatEquipmentToCharacterData();
            SyncCombatDefenseItemHitPointsToCharacterData();
            return;
        }

        flameSecondsRemaining = Mathf.Max(0, flameSeconds);
        InitializeFlameState();
        if (HasFlameItem && flameSecondsRemaining > 0)
        {
            SetFlameEquipped(equipFlame);
        }
        else
        {
            SetFlameEquipped(false);
        }

        SyncFlameStateToCharacterData();
        SyncInteractionEquipmentToCharacterData();
        SyncCombatEquipmentToCharacterData();
        SyncCombatDefenseItemHitPointsToCharacterData();
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

        if (IsFlameEquipped && FlameItem != null)
        {
            capabilities |= FlameItem.interactionCapabilities;
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

    public bool IsCombatItemEnabled(Item item)
    {
        EnsureEnabledCombatList();
        return item != null && enabledCombatItems.Contains(item);
    }

    public bool TryToggleEnabledCombatItem(Item item, out string reason)
    {
        if (IsCombatItemEnabled(item))
        {
            return TryDisableCombatItem(item, out reason);
        }

        return TryEnableCombatItem(item, out reason);
    }

    public bool TryEnableCombatItem(Item item, out string reason)
    {
        reason = string.Empty;
        if (item == null)
        {
            reason = "Impossible d'assigner cet objet au combat.";
            return false;
        }

        if (!item.CanUseInCombatReaction())
        {
            reason = "Seuls les items de reaction peuvent etre assignes au combat.";
            return false;
        }

        EnsureInventoryList();
        EnsureEnabledCombatList();
        MarkInventoryInitialized();

        if (!items.Contains(item))
        {
            reason = "L'objet doit etre dans l'inventaire pour etre assigne au combat.";
            return false;
        }

        if (enabledCombatItems.Contains(item))
        {
            return true;
        }

        SanitizeEnabledCombatItems();
        if (enabledCombatItems.Count >= MaxEnabledCombatItems)
        {
            reason = $"Seulement {MaxEnabledCombatItems} items peuvent etre gardes a portee de main en combat.";
            return false;
        }

        enabledCombatItems.Add(item);
        SyncCombatEquipmentToCharacterData();
        return true;
    }

    public bool TryDisableCombatItem(Item item, out string reason)
    {
        reason = string.Empty;
        if (item == null)
        {
            reason = "Impossible de retirer cet objet du combat.";
            return false;
        }

        EnsureEnabledCombatList();
        if (!enabledCombatItems.Remove(item))
        {
            reason = "Cet objet n'est pas assigne au combat.";
            return false;
        }

        SyncCombatEquipmentToCharacterData();
        return true;
    }

    public List<Item> GetEnabledCombatItemsSnapshot()
    {
        EnsureEnabledCombatList();
        SanitizeEnabledCombatItems();
        return new List<Item>(enabledCombatItems);
    }

    public List<Item> GetEnabledCombatDefensiveItems()
    {
        EnsureEnabledCombatList();

        List<Item> result = new List<Item>(MaxEnabledCombatItems);
        for (int i = 0; i < enabledCombatItems.Count; i++)
        {
            Item item = enabledCombatItems[i];
            if (item == null || !item.CanUseInCombatReaction())
            {
                continue;
            }

            result.Add(item);
        }

        return result;
    }

    public bool TryGetCombatDefenseItemHitPoints(Item item, out int currentHitPoints, out int maxHitPoints)
    {
        currentHitPoints = 0;
        maxHitPoints = item != null ? item.GetCombatDefenseHitPoints() : 0;
        if (item == null || maxHitPoints <= 0)
        {
            return false;
        }

        currentHitPoints = GetCombatDefenseItemRemainingHitPoints(item);
        return currentHitPoints > 0;
    }

    public int GetCombatDefenseItemRemainingHitPoints(Item item)
    {
        int maxHitPoints = item != null ? item.GetCombatDefenseHitPoints() : 0;
        if (item == null || maxHitPoints <= 0)
        {
            return 0;
        }

        EnsureInventoryList();
        EnsureCombatDefenseItemHitPointsList();
        string itemId = ItemIdUtils.GetItemId(item);
        if (string.IsNullOrWhiteSpace(itemId) || CountInventoryItemById(itemId) <= 0)
        {
            return 0;
        }

        SanitizeCombatDefenseItemHitPoints();

        int selectedHitPoints = maxHitPoints;
        bool hasDamagedStack = false;
        for (int i = 0; i < combatDefenseItemHitPoints.Count; i++)
        {
            CombatDefenseItemHitPointData entry = combatDefenseItemHitPoints[i];
            if (entry == null
                || entry.quantity <= 0
                || !string.Equals(entry.itemId, itemId, System.StringComparison.Ordinal))
            {
                continue;
            }

            int clampedHitPoints = Mathf.Clamp(entry.hitPoints, 1, maxHitPoints - 1);
            selectedHitPoints = Mathf.Min(selectedHitPoints, clampedHitPoints);
            hasDamagedStack = true;
        }

        return hasDamagedStack ? selectedHitPoints : maxHitPoints;
    }

    public void SetCombatDefenseItemRemainingHitPoints(Item item, int hitPoints)
    {
        ApplyCombatDefenseItemHitPointChange(item, GetCombatDefenseItemRemainingHitPoints(item), hitPoints);
    }

    public void ApplyCombatDefenseItemHitPointChange(Item item, int previousHitPoints, int remainingHitPoints)
    {
        int maxHitPoints = item != null ? item.GetCombatDefenseHitPoints() : 0;
        if (item == null || maxHitPoints <= 0)
        {
            return;
        }

        EnsureInventoryList();
        EnsureCombatDefenseItemHitPointsList();
        string itemId = ItemIdUtils.GetItemId(item);
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        int previous = Mathf.Clamp(previousHitPoints, 0, maxHitPoints);
        if (previous > 0 && previous < maxHitPoints)
        {
            RemoveCombatDefenseItemHitPointUnits(itemId, previous, 1);
        }

        int remaining = Mathf.Clamp(remainingHitPoints, 0, maxHitPoints);
        if (remaining > 0 && remaining < maxHitPoints && CountInventoryItemById(itemId) > 0)
        {
            AddCombatDefenseItemHitPointUnits(itemId, remaining, 1);
        }

        SanitizeCombatDefenseItemHitPoints();
        SyncCombatDefenseItemHitPointsToCharacterData();
    }

    public List<CombatDefenseItemHitPointData> GetCombatDefenseItemHitPointStacks(Item item, int totalQuantity)
    {
        List<CombatDefenseItemHitPointData> stacks = new List<CombatDefenseItemHitPointData>();
        int maxHitPoints = item != null ? item.GetCombatDefenseHitPoints() : 0;
        int clampedTotal = Mathf.Max(0, totalQuantity);
        if (item == null || maxHitPoints <= 0 || clampedTotal <= 0)
        {
            return stacks;
        }

        string itemId = ItemIdUtils.GetItemId(item);
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return stacks;
        }

        SanitizeCombatDefenseItemHitPoints();

        Dictionary<int, int> damagedCountsByHitPoints = new Dictionary<int, int>();
        List<int> damagedHitPoints = new List<int>();
        int damagedQuantity = 0;
        for (int i = 0; i < combatDefenseItemHitPoints.Count; i++)
        {
            CombatDefenseItemHitPointData entry = combatDefenseItemHitPoints[i];
            if (entry == null || !string.Equals(entry.itemId, itemId, System.StringComparison.Ordinal))
            {
                continue;
            }

            int hitPoints = Mathf.Clamp(entry.hitPoints, 1, maxHitPoints - 1);
            int quantity = Mathf.Max(1, entry.quantity);
            if (!damagedCountsByHitPoints.TryGetValue(hitPoints, out int currentQuantity))
            {
                damagedCountsByHitPoints[hitPoints] = quantity;
                damagedHitPoints.Add(hitPoints);
            }
            else
            {
                damagedCountsByHitPoints[hitPoints] = currentQuantity + quantity;
            }

            damagedQuantity += quantity;
        }

        int fullQuantity = Mathf.Max(0, clampedTotal - damagedQuantity);
        if (fullQuantity > 0)
        {
            stacks.Add(new CombatDefenseItemHitPointData
            {
                itemId = itemId,
                hitPoints = maxHitPoints,
                quantity = fullQuantity
            });
        }

        damagedHitPoints.Sort((left, right) => right.CompareTo(left));
        for (int i = 0; i < damagedHitPoints.Count; i++)
        {
            int hitPoints = damagedHitPoints[i];
            int quantity = damagedCountsByHitPoints.TryGetValue(hitPoints, out int count) ? count : 0;
            if (quantity <= 0)
            {
                continue;
            }

            stacks.Add(new CombatDefenseItemHitPointData
            {
                itemId = itemId,
                hitPoints = hitPoints,
                quantity = quantity
            });
        }

        return stacks;
    }

    public List<CombatDefenseItemHitPointData> GetCombatDefenseItemHitPointsSnapshot()
    {
        EnsureCombatDefenseItemHitPointsList();
        SanitizeCombatDefenseItemHitPoints();

        List<CombatDefenseItemHitPointData> snapshot = new List<CombatDefenseItemHitPointData>();
        for (int i = 0; i < combatDefenseItemHitPoints.Count; i++)
        {
            CombatDefenseItemHitPointData entry = combatDefenseItemHitPoints[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.itemId) || entry.hitPoints <= 0)
            {
                continue;
            }

            snapshot.Add(new CombatDefenseItemHitPointData
            {
                itemId = entry.itemId,
                hitPoints = entry.hitPoints,
                quantity = Mathf.Max(1, entry.quantity)
            });
        }

        return snapshot;
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
        EnsureEnabledCombatList();
        EnsureCombatDefenseItemHitPointsList();
        MarkInventoryInitialized();

        if (IsFlameItem(item))
        {
            if (count <= 0)
            {
                return false;
            }

            if (!RemoveFlameItem())
            {
                return false;
            }

            flameSecondsRemaining = 0;
            SetFlameEquipped(false);
            SyncFlameStateToCharacterData();
            return true;
        }

        bool removed = ConsumeItem(item, count);
        if (removed)
        {
            NormalizeReactiveInventoryItems();
            SanitizeCombatDefenseItemHitPoints();
            SanitizeEquippedInteractionItems();
            SanitizeEnabledCombatItems();
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
        EnsureEnabledCombatList();
        MarkInventoryInitialized();

        if (IsFlameItem(item))
        {
            if (!HasFlameItem)
            {
                return false;
            }

            int available = Mathf.Max(0, flameSecondsRemaining);
            if (quantity > available)
            {
                return false;
            }

            flameSecondsRemaining = available - quantity;
            if (flameSecondsRemaining <= 0)
            {
                flameSecondsRemaining = 0;
                RemoveFlameItem();
                SetFlameEquipped(false);
            }

            SyncFlameStateToCharacterData();
            return true;
        }

        bool removed = ConsumeItem(item, quantity);
        if (removed)
        {
            NormalizeReactiveInventoryItems();
            SanitizeCombatDefenseItemHitPoints();
            SanitizeEquippedInteractionItems();
            SanitizeEnabledCombatItems();
        }

        return removed;
    }

    public void ApplyStarterItems(CharacterData data, bool clearExisting = true)
    {
        EnsureInventoryList();
        EnsureEquippedInteractionList();
        EnsureEnabledCombatList();
        MarkInventoryInitialized();
        List<Item> enabledCombatItemsToRestore = clearExisting && enabledCombatItems != null
            ? new List<Item>(enabledCombatItems)
            : null;

        if (clearExisting)
        {
            items.Clear();
            equippedInteractionItems.Clear();
            enabledCombatItems.Clear();
            combatDefenseItemHitPoints.Clear();
            flameSecondsRemaining = 0;
        }

        if (data == null)
        {
            flameSecondsRemaining = 0;
            SetFlameEquipped(false);
            SyncFlameStateToCharacterData();
            SyncInteractionEquipmentToCharacterData();
            SyncCombatEquipmentToCharacterData();
            SyncCombatDefenseItemHitPointsToCharacterData();
            return;
        }

        bool hasFlame = false;
        int flameSecondsTarget = 0;
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

                if (IsFlameItem(item))
                {
                    hasFlame = true;
                    if (items == null)
                    {
                        items = new List<Item>();
                    }

                    if (!items.Contains(item))
                    {
                        items.Add(item);
                    }

                    flameSecondsTarget += quantity;
                    continue;
                }

                AddItem(item, quantity);
            }
        }

        if (hasFlame)
        {
            int target = flameSecondsTarget > 0 ? flameSecondsTarget : startingFlameSeconds;
            flameSecondsRemaining = Mathf.Max(flameSecondsRemaining, target);
            InitializeFlameState();
        }
        else
        {
            flameSecondsRemaining = 0;
            SetFlameEquipped(false);
        }

        if (enabledCombatItemsToRestore != null)
        {
            ApplyEnabledCombatItems(enabledCombatItemsToRestore);
        }

        SyncFlameStateToCharacterData();
        SyncInteractionEquipmentToCharacterData();
        SyncCombatEquipmentToCharacterData();
        SanitizeCombatDefenseItemHitPoints();

        if (logInventoryInitialization && data != null)
        {
            Debug.Log(
                $"[InventoryInit] apply_starter_items character='{data.name}' clearExisting={clearExisting} starterStackCount={data.starterItemsWithQuantity?.Count ?? -1} resultInventoryCount={items?.Count ?? -1} flameSeconds={flameSecondsRemaining} flameEquipped={flameEquipped}",
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

    private void EnsureEnabledCombatList()
    {
        if (characterData != null)
        {
            if (characterData.enabledCombatItems == null)
            {
                characterData.enabledCombatItems = new List<Item>();
            }

            if (!ReferenceEquals(enabledCombatItems, characterData.enabledCombatItems))
            {
                enabledCombatItems = characterData.enabledCombatItems;
            }

            return;
        }

        if (enabledCombatItems == null)
        {
            enabledCombatItems = new List<Item>();
        }
    }

    private void EnsureCombatDefenseItemHitPointsList()
    {
        if (characterData != null)
        {
            if (characterData.combatDefenseItemHitPoints == null)
            {
                characterData.combatDefenseItemHitPoints = new List<CombatDefenseItemHitPointData>();
            }

            if (!ReferenceEquals(combatDefenseItemHitPoints, characterData.combatDefenseItemHitPoints))
            {
                combatDefenseItemHitPoints = characterData.combatDefenseItemHitPoints;
            }

            return;
        }

        if (combatDefenseItemHitPoints == null)
        {
            combatDefenseItemHitPoints = new List<CombatDefenseItemHitPointData>();
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

    private void ApplyEnabledCombatItems(List<Item> source)
    {
        EnsureEnabledCombatList();

        if (ReferenceEquals(source, enabledCombatItems))
        {
            source = source != null ? new List<Item>(source) : null;
        }

        enabledCombatItems.Clear();
        if (source != null)
        {
            for (int i = 0; i < source.Count && enabledCombatItems.Count < MaxEnabledCombatItems; i++)
            {
                Item item = source[i];
                if (item == null || enabledCombatItems.Contains(item))
                {
                    continue;
                }

                enabledCombatItems.Add(item);
            }
        }

        SanitizeEnabledCombatItems();
    }

    private void ApplyCombatDefenseItemHitPoints(List<CombatDefenseItemHitPointData> source)
    {
        EnsureCombatDefenseItemHitPointsList();

        if (ReferenceEquals(source, combatDefenseItemHitPoints))
        {
            source = source != null ? new List<CombatDefenseItemHitPointData>(source) : null;
        }

        combatDefenseItemHitPoints.Clear();
        if (source != null)
        {
            for (int i = 0; i < source.Count; i++)
            {
                CombatDefenseItemHitPointData entry = source[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.itemId) || entry.hitPoints <= 0)
                {
                    continue;
                }

                AddCombatDefenseItemHitPointUnits(entry.itemId, entry.hitPoints, Mathf.Max(1, entry.quantity));
            }
        }

        SanitizeCombatDefenseItemHitPoints();
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

    private void SanitizeEnabledCombatItems()
    {
        EnsureInventoryList();
        EnsureEnabledCombatList();

        for (int i = enabledCombatItems.Count - 1; i >= 0; i--)
        {
            Item item = enabledCombatItems[i];
            if (item == null || !items.Contains(item) || !item.CanUseInCombatReaction())
            {
                enabledCombatItems.RemoveAt(i);
            }
        }

        while (enabledCombatItems.Count > MaxEnabledCombatItems)
        {
            enabledCombatItems.RemoveAt(enabledCombatItems.Count - 1);
        }

        SyncCombatEquipmentToCharacterData();
    }

    private void SanitizeCombatDefenseItemHitPoints()
    {
        EnsureInventoryList();
        EnsureCombatDefenseItemHitPointsList();

        if (combatDefenseItemHitPoints.Count == 0)
        {
            SyncCombatDefenseItemHitPointsToCharacterData();
            return;
        }

        List<CombatDefenseItemHitPointData> merged = new List<CombatDefenseItemHitPointData>();
        for (int i = 0; i < combatDefenseItemHitPoints.Count; i++)
        {
            CombatDefenseItemHitPointData entry = combatDefenseItemHitPoints[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.itemId) || entry.hitPoints <= 0)
            {
                continue;
            }

            Item item = ResolveInventoryItemById(entry.itemId);
            int maxHitPoints = item != null ? item.GetCombatDefenseHitPoints() : 0;
            if (item == null || maxHitPoints <= 0)
            {
                continue;
            }

            if (entry.hitPoints >= maxHitPoints)
            {
                continue;
            }

            int hitPoints = Mathf.Clamp(entry.hitPoints, 1, maxHitPoints - 1);
            AddCombatDefenseItemHitPointUnits(merged, entry.itemId, hitPoints, Mathf.Max(1, entry.quantity));
        }

        combatDefenseItemHitPoints.Clear();
        List<string> itemIds = new List<string>();
        for (int i = 0; i < merged.Count; i++)
        {
            CombatDefenseItemHitPointData entry = merged[i];
            if (entry != null && !itemIds.Contains(entry.itemId))
            {
                itemIds.Add(entry.itemId);
            }
        }

        for (int i = 0; i < itemIds.Count; i++)
        {
            string itemId = itemIds[i];
            int remainingCapacity = CountInventoryItemById(itemId);
            while (remainingCapacity > 0)
            {
                int mostDamagedIndex = FindMostDamagedHitPointEntryIndex(merged, itemId);
                if (mostDamagedIndex < 0)
                {
                    break;
                }

                CombatDefenseItemHitPointData entry = merged[mostDamagedIndex];
                int keepQuantity = Mathf.Min(Mathf.Max(0, entry.quantity), remainingCapacity);
                if (keepQuantity > 0)
                {
                    combatDefenseItemHitPoints.Add(new CombatDefenseItemHitPointData
                    {
                        itemId = entry.itemId,
                        hitPoints = entry.hitPoints,
                        quantity = keepQuantity
                    });
                    remainingCapacity -= keepQuantity;
                }

                merged.RemoveAt(mostDamagedIndex);
            }
        }

        SyncCombatDefenseItemHitPointsToCharacterData();
    }

    private static int FindMostDamagedHitPointEntryIndex(List<CombatDefenseItemHitPointData> entries, string itemId)
    {
        int bestIndex = -1;
        int bestHitPoints = int.MaxValue;
        if (entries == null || string.IsNullOrWhiteSpace(itemId))
        {
            return bestIndex;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            CombatDefenseItemHitPointData entry = entries[i];
            if (entry == null || !string.Equals(entry.itemId, itemId, System.StringComparison.Ordinal))
            {
                continue;
            }

            if (entry.hitPoints < bestHitPoints)
            {
                bestHitPoints = entry.hitPoints;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private Item ResolveInventoryItemById(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return null;
        }

        EnsureInventoryList();
        if (items == null)
        {
            return null;
        }

        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];
            if (item == null)
            {
                continue;
            }

            string candidateId = ItemIdUtils.GetItemId(item);
            if (string.Equals(candidateId, itemId, System.StringComparison.Ordinal))
            {
                return item;
            }
        }

        return null;
    }

    private int CountInventoryItemById(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return 0;
        }

        EnsureInventoryList();
        int count = 0;
        if (items == null)
        {
            return count;
        }

        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];
            if (item == null)
            {
                continue;
            }

            if (string.Equals(ItemIdUtils.GetItemId(item), itemId, System.StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private void AddCombatDefenseItemHitPointUnits(string itemId, int hitPoints, int quantity)
    {
        AddCombatDefenseItemHitPointUnits(combatDefenseItemHitPoints, itemId, hitPoints, quantity);
    }

    private static void AddCombatDefenseItemHitPointUnits(
        List<CombatDefenseItemHitPointData> entries,
        string itemId,
        int hitPoints,
        int quantity)
    {
        if (entries == null || string.IsNullOrWhiteSpace(itemId) || hitPoints <= 0 || quantity <= 0)
        {
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            CombatDefenseItemHitPointData entry = entries[i];
            if (entry == null
                || !string.Equals(entry.itemId, itemId, System.StringComparison.Ordinal)
                || entry.hitPoints != hitPoints)
            {
                continue;
            }

            entry.quantity = Mathf.Max(1, entry.quantity) + quantity;
            return;
        }

        entries.Add(new CombatDefenseItemHitPointData
        {
            itemId = itemId,
            hitPoints = hitPoints,
            quantity = quantity
        });
    }

    private void RemoveCombatDefenseItemHitPointUnits(string itemId, int hitPoints, int quantity)
    {
        if (string.IsNullOrWhiteSpace(itemId) || hitPoints <= 0 || quantity <= 0)
        {
            return;
        }

        EnsureCombatDefenseItemHitPointsList();
        int remaining = quantity;
        for (int i = combatDefenseItemHitPoints.Count - 1; i >= 0 && remaining > 0; i--)
        {
            CombatDefenseItemHitPointData entry = combatDefenseItemHitPoints[i];
            if (entry == null)
            {
                combatDefenseItemHitPoints.RemoveAt(i);
                continue;
            }

            if (!string.Equals(entry.itemId, itemId, System.StringComparison.Ordinal) || entry.hitPoints != hitPoints)
            {
                continue;
            }

            int available = Mathf.Max(1, entry.quantity);
            int removed = Mathf.Min(available, remaining);
            entry.quantity = available - removed;
            remaining -= removed;
            if (entry.quantity <= 0)
            {
                combatDefenseItemHitPoints.RemoveAt(i);
            }
        }
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
            SanitizeEnabledCombatItems();
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
        EnsureEnabledCombatList();
        EnsureCombatDefenseItemHitPointsList();
        flameSecondsRemaining = Mathf.Max(0, characterData.flameSecondsRemaining);
        InitializeFlameState();
        if (HasFlameItem && flameSecondsRemaining > 0)
        {
            SetFlameEquipped(characterData.flameEquipped);
        }
        else
        {
            SetFlameEquipped(false);
        }

        ApplyEquippedInteractionItems(characterData.equippedInteractionItems);
        ApplyEnabledCombatItems(characterData.enabledCombatItems);
        SanitizeCombatDefenseItemHitPoints();

        if (logInventoryInitialization)
        {
            Debug.Log(
                $"[InventoryInit] load_runtime_inventory character='{characterData.name}' resultInventoryCount={items?.Count ?? -1} equippedCount={equippedInteractionItems?.Count ?? -1} flameSeconds={flameSecondsRemaining} flameEquipped={flameEquipped}",
                this);
        }
    }

    private void SyncFlameStateToCharacterData()
    {
        if (characterData == null)
        {
            return;
        }

        characterData.flameSecondsRemaining = CharacterFlameSystemEnabled ? Mathf.Max(0, flameSecondsRemaining) : 0;
        characterData.flameEquipped = CharacterFlameSystemEnabled && flameEquipped;
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

    private void SyncCombatEquipmentToCharacterData()
    {
        if (characterData == null)
        {
            return;
        }

        EnsureEnabledCombatList();
        characterData.inventoryInitialized = true;
    }

    private void SyncCombatDefenseItemHitPointsToCharacterData()
    {
        if (characterData == null)
        {
            return;
        }

        EnsureCombatDefenseItemHitPointsList();
        characterData.inventoryInitialized = true;
    }

    private void BindMuninChargeController()
    {
        BindMuninChargeController(GetComponentInChildren<MuninController>(true));
    }

    private void BindMuninChargeController(MuninController munin)
    {
        if (syncedMuninChargeController == munin)
        {
            return;
        }

        if (syncedMuninChargeController != null)
        {
            syncedMuninChargeController.ChargesChanged -= OnMuninChargesChanged;
        }

        syncedMuninChargeController = munin;
        if (syncedMuninChargeController != null)
        {
            syncedMuninChargeController.ChargesChanged += OnMuninChargesChanged;
        }
    }

    private void ApplyMuninChargeStateFromCharacterData()
    {
        if (characterData == null || syncedMuninChargeController == null)
        {
            return;
        }

        if (characterData.muninChargesInitialized)
        {
            if (characterData.muninMaxCharges > 0)
            {
                syncedMuninChargeController.SetMaxCharges(characterData.muninMaxCharges, false);
            }

            syncedMuninChargeController.SetCharges(characterData.muninChargesRemaining);
            return;
        }

        SyncMuninChargesToCharacterData();
    }

    private void OnMuninChargesChanged(MuninController munin, int current, int max)
    {
        if (munin != syncedMuninChargeController)
        {
            return;
        }

        SyncMuninChargesToCharacterData();
    }

    private void SyncMuninChargesToCharacterData()
    {
        if (characterData == null || syncedMuninChargeController == null)
        {
            return;
        }

        characterData.muninChargesRemaining = syncedMuninChargeController.ChargesRemaining;
        characterData.muninMaxCharges = syncedMuninChargeController.MaxCharges;
        characterData.muninChargesInitialized = true;
    }

    private void SyncFlameStateToCharacterDataIfChanged(int prevSeconds, bool prevEquipped)
    {
        if (prevSeconds == flameSecondsRemaining && prevEquipped == flameEquipped)
        {
            return;
        }

        SyncFlameStateToCharacterData();
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

        ResolveFacialExpressionController();
        facialMovementInputThreshold = Mathf.Clamp01(facialMovementInputThreshold);
        facialMovementIdleFadeDuration = Mathf.Max(0f, facialMovementIdleFadeDuration);
        walkMoveSpeed = Mathf.Max(0f, walkMoveSpeed);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        walkMoveSpeed = Mathf.Min(walkMoveSpeed, moveSpeed);
        movementInputDeadZone = Mathf.Clamp01(movementInputDeadZone);
        fixedCameraMovementInputRefreshAngle = Mathf.Clamp(fixedCameraMovementInputRefreshAngle, 0f, 180f);
        fixedCameraMovementReferenceBlendSharpness = Mathf.Max(0f, fixedCameraMovementReferenceBlendSharpness);
        speedDampTime = Mathf.Max(0f, speedDampTime);
        ValidateSittingSettings();
        ApplyAnimatorSettings();
        EnsureRigidbodyCollisionSafety();
    }

    public void ToggleFlame()
    {
        if (!CharacterFlameSystemEnabled)
        {
            DisableCharacterFlameState();
            return;
        }

        if (!allowFlameToggle)
        {
            return;
        }

        if (!HasFlameItem)
        {
            return;
        }

        if (!flameEquipped && flameSecondsRemaining <= 0)
        {
            return;
        }

        EnsureFlameCached();
        if (flameTransform == null)
        {
            return;
        }

        SetFlameEquipped(!flameEquipped);
        PlayActionAudio(ActionAudioCue.FlameToggle);
    }

    public void TriggerMunin()
    {
        DisableCharacterFlameState();
    }

    public void ApplyFlameState(int flameSeconds, bool equipFlame)
    {
        if (!CharacterFlameSystemEnabled)
        {
            DisableCharacterFlameState();
            return;
        }

        flameSecondsRemaining = Mathf.Max(0, flameSeconds);

        if (HasFlameItem && flameSecondsRemaining > 0)
        {
            SetFlameEquipped(equipFlame);
        }
        else
        {
            SetFlameEquipped(false);
        }
    }

    public void Move(Vector2 input)
    {
        TryForwardMoveToUcc(input, isWorldSpace: false);
    }

    public void MoveWorld(Vector2 worldInput)
    {
        TryForwardMoveToUcc(worldInput, isWorldSpace: true);
    }

    public void SetSprintModifier(bool pressed)
    {
        sprintModifierPressed = pressed;
        TryForwardSprintToUcc(pressed);
    }

    public void Jump()
    {
        TryForwardJumpToUcc();
    }

    public void Stop()
    {
        TryForwardStopToUcc();

        ResetUccLocomotionIntent();
        sprintModifierPressed = false;
        ClearStoredMovementReference();
        StopHorizontalVelocity();
        SetAnimatorBoolIfValid(isMovingParam, false);
        SetAnimatorBoolIfValid(turnInPlaceParam, false);
        SetAnimatorFloatIfValid(locomotionTierParam, WalkLocomotionTier);
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
        if (IsExternalLocomotionDriverActive)
        {
            UpdateGroundedState();
            ClearScriptDrivenHorizontalVelocity();
            return;
        }

        UpdateGroundedState();
        ClearScriptDrivenHorizontalVelocity();
    }

    private void ApplyMovementFacialNeutral(float inputMagnitude)
    {
        if (!forceIdleFacialExpressionWhileMoving || inputMagnitude < facialMovementInputThreshold)
        {
            return;
        }

        ResolveFacialExpressionController();
        if (facialExpressionController == null)
        {
            return;
        }

        if (facialExpressionController.CurrentPassiveEmotion == FacialEmotion.Idle &&
            !facialExpressionController.IsPlayingOneShot)
        {
            return;
        }

        facialExpressionController.ForceIdleExpression(facialMovementIdleFadeDuration);
    }

    private void ResolveFacialExpressionController()
    {
        if (facialExpressionController != null)
        {
            return;
        }

        facialExpressionController = GetComponent<FacialExpressionController>();
        if (facialExpressionController != null)
        {
            return;
        }

        facialExpressionController = GetComponentInChildren<FacialExpressionController>(true);
        if (facialExpressionController != null)
        {
            return;
        }

        facialExpressionController = GetComponentInParent<FacialExpressionController>();
    }

    private void ClearScriptDrivenHorizontalVelocity()
    {
    }

    private void StopHorizontalVelocity()
    {
    }

    private void UpdateGroundedState()
    {
        if (TryGetUccGrounded(out bool uccGrounded))
        {
            isGrounded = uccGrounded;
            return;
        }

        isGrounded = false;
    }

    private bool TryGetLocomotionCapsule(out Vector3 center, out float radius, out float height)
    {
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

    private void InitializeFlameState()
    {
        flameInitialized = false;
        EnsureFlameCached();
        ClearPendingFlameVisualTransition();

        if (!CharacterFlameSystemEnabled)
        {
            DisableCharacterFlameState();
            return;
        }

        if (flameTransform == null)
        {
            return;
        }

        if (!HasFlameItem)
        {
            flameEquipped = false;
            ApplyFlameVisualState(false);
            SetFlameAnimatorBool(false, true);
            UpdateFlameAnimationLayerWeight(immediate: true);
            return;
        }

        if (initializeFlameFromHierarchy)
        {
            flameEquipped = flameTransform.gameObject.activeSelf;
        }
        else
        {
            flameEquipped = flameStartsActive;
        }

        ApplyFlameVisualState(flameEquipped);

        SetFlameAnimatorBool(flameEquipped, true);

        UpdateFlameAnimationLayerWeight(immediate: true);

        if (flameSecondsRemaining <= 0 && flameEquipped)
        {
            flameEquipped = false;
            ApplyFlameVisualState(false);
            SetFlameAnimatorBool(false, true);
            UpdateFlameAnimationLayerWeight(immediate: true);
        }
    }

    public void TickFlameLifetimeForExternalLocomotion(float deltaTime)
    {
        if (!CharacterFlameSystemEnabled)
        {
            return;
        }

        UpdateFlameLifetime(deltaTime);
    }

    private void UpdateFlameLifetime(float deltaTime)
    {
        if (!CharacterFlameSystemEnabled)
        {
            DisableCharacterFlameState();
            return;
        }

        int prevSeconds = flameSecondsRemaining;
        bool prevEquipped = flameEquipped;

        if (!Zone.ShouldConsumeFlame(gameObject))
        {
            flameDrainTimer = 0f;
            SyncFlameStateToCharacterDataIfChanged(prevSeconds, prevEquipped);
            return;
        }

        if (!flameEquipped)
        {
            flameDrainTimer = 0f;
            SyncFlameStateToCharacterDataIfChanged(prevSeconds, prevEquipped);
            return;
        }

        if (!HasFlameItem)
        {
            flameDrainTimer = 0f;
            SetFlameEquipped(false);
            SyncFlameStateToCharacterDataIfChanged(prevSeconds, prevEquipped);
            return;
        }

        if (flameSecondsRemaining <= 0)
        {
            SetFlameEquipped(false);
            SyncFlameStateToCharacterDataIfChanged(prevSeconds, prevEquipped);
            return;
        }

        flameDrainTimer += deltaTime;
        while (flameDrainTimer >= 1f && flameSecondsRemaining > 0)
        {
            flameSecondsRemaining -= 1;
            flameDrainTimer -= 1f;
        }

        if (flameSecondsRemaining <= 0)
        {
            flameSecondsRemaining = 0;
            SetFlameEquipped(false);
        }

        SyncFlameStateToCharacterDataIfChanged(prevSeconds, prevEquipped);
    }

    public void AddFlameSeconds(int seconds)
    {
        if (!CharacterFlameSystemEnabled)
        {
            DisableCharacterFlameState();
            return;
        }

        if (seconds <= 0)
        {
            return;
        }

        MarkInventoryInitialized();
        flameSecondsRemaining = Mathf.Max(0, flameSecondsRemaining + seconds);
        SyncFlameStateToCharacterData();
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

    private bool IsFlameItem(Item item)
    {
        return CharacterFlameSystemEnabled && item != null && item.isFlame;
    }

    private void DisableCharacterFlameState()
    {
        flameSecondsRemaining = 0;
        flameDrainTimer = 0f;
        flameEquipped = false;
        EnsureFlameCached();
        ApplyFlameVisualState(false);
        ClearPendingFlameVisualTransition();

        SetFlameAnimatorBool(false, true);

        UpdateFlameAnimationLayerWeight(immediate: true);
        SyncFlameStateToCharacterData();
    }

    private void SetFlameEquipped(bool equipped)
    {
        EnsureFlameCached();
        if (flameTransform == null)
        {
            return;
        }

        flameEquipped = equipped;

        SetFlameAnimatorBool(flameEquipped, false);

        QueueFlameVisualTransition(equipped);
        UpdateFlameAnimationLayerWeight(immediate: true);

        SyncFlameStateToCharacterData();
    }

    private void ApplyFlameVisualState(bool equipped)
    {
        if (flameTransform == null)
        {
            return;
        }

        flameVisualEquipped = equipped;
        if (flameTransform.gameObject.activeSelf != equipped)
        {
            flameTransform.gameObject.SetActive(equipped);
        }
    }

    private void QueueFlameVisualTransition(bool equipped)
    {
        if (flameVisualEquipped == equipped)
        {
            ClearPendingFlameVisualTransition();
            UpdateFlameAnimationLayerWeight(immediate: true);
            return;
        }

        if (!CanDelayFlameVisualTransition())
        {
            ApplyFlameVisualState(equipped);
            ClearPendingFlameVisualTransition();
            UpdateFlameAnimationLayerWeight(immediate: true);
            return;
        }

        pendingFlameVisualTransition = equipped ? FlameVisualTransition.Equip : FlameVisualTransition.Unequip;
        flameVisualTransitionStateObserved = false;
        flameVisualTransitionTimer = 0f;
        UpdateFlameAnimationLayerWeight(immediate: true);
    }

    private void UpdateFlameVisualTransition()
    {
        if (pendingFlameVisualTransition == FlameVisualTransition.None)
        {
            return;
        }

        EnsureFlameCached();
        if (flameTransform == null)
        {
            ClearPendingFlameVisualTransition();
            return;
        }

        if (!CanDelayFlameVisualTransition())
        {
            ApplyFlameVisualState(flameEquipped);
            ClearPendingFlameVisualTransition();
            return;
        }

        flameVisualTransitionTimer += Time.deltaTime;
        if (!flameVisualTransitionStateObserved && IsFlameAnimationStateActive(pendingFlameVisualTransition))
        {
            flameVisualTransitionStateObserved = true;
            flameVisualTransitionTimer = 0f;
            return;
        }

        if (!flameVisualTransitionStateObserved && flameVisualTransitionTimer < FlameAnimationStateFallbackDelay)
        {
            return;
        }

        flameVisualTransitionStateObserved = true;
        if (flameVisualTransitionTimer < FlameAnimationVisualDelay)
        {
            return;
        }

        if (pendingFlameVisualTransition == FlameVisualTransition.Unequip &&
            !IsFlameVisualTransitionAnimationComplete(FlameVisualTransition.Unequip))
        {
            return;
        }

        ApplyFlameVisualState(flameEquipped);
        ClearPendingFlameVisualTransition();
    }

    private bool CanDelayFlameVisualTransition()
    {
        return GetFlameAnimationLayerIndex() >= 0
            && HasAnimatorParameter(flameBoolParam, AnimatorControllerParameterType.Bool);
    }

    private void SetFlameAnimatorBool(bool value, bool syncImmediate)
    {
        if (!HasAnimatorParameter(flameBoolParam, AnimatorControllerParameterType.Bool))
        {
            return;
        }

        animator.SetBool(flameBoolParam, value);
        if (syncImmediate)
        {
            SyncFlameAnimationStateImmediate();
        }
    }

    private bool IsFlameAnimationStateActive(FlameVisualTransition transition)
    {
        int layerIndex = GetFlameAnimationLayerIndex();
        if (layerIndex < 0)
        {
            return false;
        }

        int stateHash = transition == FlameVisualTransition.Equip
            ? FlameEquipStateHash
            : FlameUnequipStateHash;

        if (MatchesFlameAnimationState(animator.GetCurrentAnimatorStateInfo(layerIndex), stateHash))
        {
            return true;
        }

        return animator.IsInTransition(layerIndex)
            && MatchesFlameAnimationState(animator.GetNextAnimatorStateInfo(layerIndex), stateHash);
    }

    private bool IsFlameVisualTransitionAnimationComplete(FlameVisualTransition transition)
    {
        int layerIndex = GetFlameAnimationLayerIndex();
        if (layerIndex < 0)
        {
            return true;
        }

        int stateHash = transition == FlameVisualTransition.Equip
            ? FlameEquipStateHash
            : FlameUnequipStateHash;

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(layerIndex);
        if (MatchesFlameAnimationState(currentState, stateHash))
        {
            return currentState.normalizedTime >= 1f;
        }

        if (animator.IsInTransition(layerIndex) &&
            MatchesFlameAnimationState(animator.GetNextAnimatorStateInfo(layerIndex), stateHash))
        {
            return false;
        }

        return flameVisualTransitionStateObserved;
    }

    private int GetFlameAnimationLayerIndex()
    {
        if (animator == null || !animator.isActiveAndEnabled)
        {
            return -1;
        }

        return animator.GetLayerIndex(FlameAnimationLayerName);
    }

    private void SyncFlameAnimationStateImmediate()
    {
        int layerIndex = GetFlameAnimationLayerIndex();
        if (layerIndex < 0)
        {
            return;
        }

        int stateHash = flameEquipped ? FlameLocomotionStateHash : FlameOffStateHash;
        animator.Play(stateHash, layerIndex, 0f);
    }

    private void UpdateFlameAnimationLayerWeight(bool immediate = false)
    {
        int layerIndex = GetFlameAnimationLayerIndex();
        if (layerIndex < 0)
        {
            return;
        }

        float targetWeight = ResolveFlameAnimationLayerWeightTarget();
        float nextWeight = targetWeight;

        if (!immediate && Application.isPlaying)
        {
            float currentWeight = animator.GetLayerWeight(layerIndex);
            if (flameUpperBodyLayerWeightResponsiveness > 0f)
            {
                float t = 1f - Mathf.Exp(-flameUpperBodyLayerWeightResponsiveness * Time.deltaTime);
                nextWeight = Mathf.Lerp(currentWeight, targetWeight, t);
            }
        }

        if (!Mathf.Approximately(animator.GetLayerWeight(layerIndex), nextWeight))
        {
            animator.SetLayerWeight(layerIndex, nextWeight);
        }
    }

    private float ResolveFlameAnimationLayerWeightTarget()
    {
        if (pendingFlameVisualTransition != FlameVisualTransition.None)
        {
            return 1f;
        }

        if (!flameEquipped)
        {
            return 0f;
        }

        float maxMoveSpeed = Mathf.Max(0.01f, moveSpeed);
        float normalizedSpeed = Mathf.Clamp01(GetCurrentHorizontalVelocity().magnitude / maxMoveSpeed);
        return Mathf.Lerp(flameUpperBodyIdleLayerWeight, flameUpperBodyMovingLayerWeight, normalizedSpeed);
    }

    private static bool MatchesFlameAnimationState(AnimatorStateInfo stateInfo, int stateHash)
    {
        return stateInfo.shortNameHash == stateHash;
    }

    private void ClearPendingFlameVisualTransition()
    {
        pendingFlameVisualTransition = FlameVisualTransition.None;
        flameVisualTransitionStateObserved = false;
        flameVisualTransitionTimer = 0f;
        UpdateFlameAnimationLayerWeight(immediate: true);
    }

    private Item GetFlameItem()
    {
        if (items == null || items.Count == 0)
        {
            return null;
        }

        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];
            if (IsFlameItem(item))
            {
                return item;
            }
        }

        return null;
    }

    private bool RemoveFlameItem()
    {
        if (items == null || items.Count == 0)
        {
            return false;
        }

        for (int i = items.Count - 1; i >= 0; i--)
        {
            Item item = items[i];
            if (IsFlameItem(item))
            {
                items.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    private void EnsureFlameCached()
    {
        if (flameInitialized)
        {
            return;
        }

        flameInitialized = true;
        flameTransform = FindFlameTransform();
        if (flameTransform != null)
        {
            ConfigureFlamePhysics(flameTransform);
        }
    }

    private void ConfigureFlamePhysics(Transform root)
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

    private Transform FindFlameTransform()
    {
        Transform root = motionRoot != null ? motionRoot : transform;
        Transform parent = FindChildByName(root, flameParentName);
        if (parent == null)
        {
            parent = root;
        }

        Transform flame = FindChildByName(parent, flameChildName);
        if (flame == null)
        {
            flame = root.Find($"{flameParentName}/{flameChildName}");
        }

        return flame;
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

    private Vector3 GetCurrentHorizontalVelocity()
    {
        if (TryGetUccPlanarVelocity(out Vector3 uccPlanarVelocity))
        {
            return uccPlanarVelocity;
        }

        return Vector3.zero;
    }

    private Vector3 GetWorldPosition()
    {
        if (TryGetUccWorldPosition(out Vector3 uccWorldPosition))
        {
            return uccWorldPosition;
        }

        return Vector3.zero;
    }

    private Vector3 GetMoveDirection(Vector2 input, bool inputRepresentsRawMovement = false)
    {
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
        return input;
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
        Camera[] cameras = FindObjectsByType<Camera>();
#else
        Camera[] cameras = FindObjectsByType<Camera>();
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

    public void AddImpulse(Vector3 worldImpulse, float lockInputForSeconds = -1f)
    {
        float duration = lockInputForSeconds < 0f ? inputLockTime : lockInputForSeconds;
        TryAddImpulseToUcc(worldImpulse, knockbackForceMode, duration);
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
