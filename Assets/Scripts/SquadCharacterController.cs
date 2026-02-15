using System.Collections.Generic;
using UnityEngine;

// Controle le mouvement et l'inventaire runtime d'un personnage de la squad.
[RequireComponent(typeof(Animator))]
public class SquadCharacterController : MonoBehaviour
{
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
    private Vector3 currentHorizontalVelocity;
    private Transform torchTransform;
    private bool torchInitialized;
    private bool torchEquipped;
    private float torchDrainTimer;
    private float nextCollisionRefreshTime;
    private bool collidersDirty = true;
    private readonly List<Collider> cachedColliders = new List<Collider>();

    private static readonly List<SquadCharacterController> activeCharacters = new List<SquadCharacterController>();

    public CharacterData CharacterData => characterData;

    public IReadOnlyList<Item> Items => items;

    public IReadOnlyList<Skill> Skills => characterData != null ? characterData.skills : null;

    public int CurrentHp => currentHp;

    public int MaxHp => maxHp;

    private void Reset()
    {
        animator = GetComponent<Animator>();
        characterController = GetComponent<CharacterController>();
        rigidbodyTarget = GetComponent<Rigidbody>();
        motionRoot = transform;
        ApplyAnimatorSettings();
        InitializeTorchState();
    }

    private void Update()
    {
        // Torche + collisions en runtime.
        UpdateTorchLifetime(Time.deltaTime);
        RefreshCharacterCollisionsIfNeeded();
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

        if (motionRoot == null)
        {
            motionRoot = transform;
        }

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
    }

    private void OnEnable()
    {
        RegisterCharacter();
    }

    private void OnDisable()
    {
        UnregisterCharacter();
    }

    private void OnTransformChildrenChanged()
    {
        MarkCollidersDirty();
    }

    private void OnTransformParentChanged()
    {
        MarkCollidersDirty();
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
            if (!characterData.inventoryInitialized)
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
        if (characterData == null || characterData.starterItems == null)
        {
            return null;
        }

        for (int i = 0; i < characterData.starterItems.Count; i++)
        {
            Item item = characterData.starterItems[i];
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

        if (data == null || data.starterItems == null)
        {
            torchSecondsRemaining = 0;
            SetTorchEquipped(false);
            SyncTorchStateToCharacterData();
            return;
        }

        bool hasTorch = false;
        for (int i = 0; i < data.starterItems.Count; i++)
        {
            Item item = data.starterItems[i];
            if (item == null)
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
                continue;
            }

            AddItem(item, 1);
        }

        if (hasTorch)
        {
            torchSecondsRemaining = Mathf.Max(torchSecondsRemaining, startingTorchSeconds);
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

    public void Move(Vector2 input)
    {
        moveInput = input;
    }

    public void Stop()
    {
        moveInput = Vector2.zero;
        smoothedInput = Vector2.zero;
        currentHorizontalVelocity = Vector3.zero;
        SetSpeed(0f);
    }

    private void FixedUpdate()
    {
        UpdateInputLock(Time.fixedDeltaTime);
        SmoothInput(Time.fixedDeltaTime);
        ApplyMovement(Time.fixedDeltaTime);
        UpdateAnimationSpeed();
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
        bool hasInput = smoothedInput.sqrMagnitude > 0.0001f;
        Vector3 desiredVelocity = Vector3.zero;
        if (hasInput)
        {
            Vector3 direction = GetMoveDirection(smoothedInput);
            if (direction.sqrMagnitude > 0.0001f)
            {
                direction = direction.normalized;
            }

            desiredVelocity = direction * (Mathf.Clamp01(smoothedInput.magnitude) * moveSpeed);
        }

        if (inputLockTimer > 0f)
        {
            return;
        }

        if (ShouldUseRigidbody())
        {
            Vector3 currentVelocity = rigidbodyTarget.linearVelocity;
            Vector3 currentHorizontal = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            Vector3 targetHorizontal = new Vector3(desiredVelocity.x, 0f, desiredVelocity.z);
            Vector3 newHorizontal = hasInput
                ? Vector3.MoveTowards(currentHorizontal, targetHorizontal, acceleration * deltaTime)
                : Vector3.Lerp(currentHorizontal, Vector3.zero, 1f - Mathf.Exp(-deceleration * deltaTime));
            rigidbodyTarget.linearVelocity = new Vector3(newHorizontal.x, currentVelocity.y, newHorizontal.z);
            currentHorizontalVelocity = newHorizontal;

            if (rotateToInput && desiredVelocity.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(desiredVelocity);
                rigidbodyTarget.MoveRotation(
                    Quaternion.Slerp(rigidbodyTarget.rotation, targetRotation, rotationSpeed * deltaTime));
            }
            return;
        }

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

    private void SetSpeed(float speed)
    {
        if (animator == null || string.IsNullOrWhiteSpace(speedParam))
        {
            return;
        }

        if (useSpeedDamping)
        {
            animator.SetFloat(speedParam, speed, speedDampTime, Time.deltaTime);
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

        if (torchTransform == null)
        {
            return;
        }

        if (!HasTorchItem)
        {
            torchEquipped = false;
            torchTransform.gameObject.SetActive(false);
            if (animator != null && !string.IsNullOrWhiteSpace(torchBoolParam))
            {
                animator.SetBool(torchBoolParam, false);
            }
            return;
        }

        if (initializeTorchFromHierarchy)
        {
            torchEquipped = torchTransform.gameObject.activeSelf;
        }
        else
        {
            torchEquipped = torchStartsActive;
            torchTransform.gameObject.SetActive(torchEquipped);
        }

        if (animator != null && !string.IsNullOrWhiteSpace(torchBoolParam))
        {
            animator.SetBool(torchBoolParam, torchEquipped);
        }

        if (torchSecondsRemaining <= 0 && torchEquipped)
        {
            SetTorchEquipped(false);
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
        torchTransform.gameObject.SetActive(torchEquipped);

        if (animator != null && !string.IsNullOrWhiteSpace(torchBoolParam))
        {
            animator.SetBool(torchBoolParam, torchEquipped);
        }

        SyncTorchStateToCharacterData();
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

    private void UpdateAnimationSpeed()
    {
        if (animator == null || string.IsNullOrWhiteSpace(speedParam))
        {
            return;
        }

        float rawSpeed;
        if (useVelocityForAnimation)
        {
            Vector3 velocity = GetCurrentHorizontalVelocity();
            rawSpeed = moveSpeed > 0f ? velocity.magnitude / moveSpeed : 0f;
        }
        else
        {
            rawSpeed = smoothedInput.magnitude;
        }

        float animSpeed = rawSpeed;
        if (useDiscreteLocomotion)
        {
            if (rawSpeed <= walkSpeedThreshold)
            {
                animSpeed = idleAnimValue;
            }
            else if (rawSpeed <= runSpeedThreshold)
            {
                animSpeed = walkAnimValue;
            }
            else
            {
                animSpeed = runAnimValue;
            }
        }

        SetSpeed(animSpeed);
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

    private Vector3 GetMoveDirection(Vector2 input)
    {
        Vector3 move = new Vector3(input.x, 0f, input.y);
        if (!useCameraRelative)
        {
            return move;
        }

        Camera cam = referenceCamera != null ? referenceCamera : Camera.main;
        if (cam == null)
        {
            return move;
        }

        Vector3 camForward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
        return camRight * input.x + camForward * input.y;
    }

    public Vector2 GetInputFromWorldDirection(Vector3 worldDirection)
    {
        Vector3 planar = new Vector3(worldDirection.x, 0f, worldDirection.z);
        if (planar.sqrMagnitude < 0.0001f)
        {
            return Vector2.zero;
        }

        planar.Normalize();

        if (!useCameraRelative)
        {
            return new Vector2(planar.x, planar.z);
        }

        Camera cam = referenceCamera != null ? referenceCamera : Camera.main;
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
            if (a == null)
            {
                continue;
            }

            for (int j = 0; j < theirs.Count; j++)
            {
                Collider b = theirs[j];
                if (b == null)
                {
                    continue;
                }

                Physics.IgnoreCollision(a, b, ignore);
            }
        }
    }
}
