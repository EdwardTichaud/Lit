using UnityEngine;

[DisallowMultipleComponent]
public class TreasureFinder : MonoBehaviour
{
    [Header("Treasure")]
    [SerializeField] private Item treasureItem;
    [SerializeField, Min(1)] private int treasureQuantity = 1;
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0.15f, 0f);
    [SerializeField] private bool createLootContainer = true;
    [SerializeField] private bool destroyLootContainerWhenEmpty = true;
    [SerializeField] private bool collectable = true;

    [Header("Vision")]
    [SerializeField] private TorchVisionDefinition requiredVision;
    [SerializeField] private TorchLightReceiver torchReceiver;
    [SerializeField] private bool requireMatchingVision = true;
    [SerializeField] private bool gateSpawnedTreasureWithVision = true;
    [SerializeField, Min(0f)] private float revealDistance = 5f;
    [SerializeField, Min(0f)] private float fullRevealDistance = 2f;

    [Header("Trigger")]
    [SerializeField] private Collider triggerCollider;
    [SerializeField, Min(0.1f)] private float fallbackTriggerRadius = 1.1f;

    [Header("Lifecycle")]
    [SerializeField] private bool spawnOnlyOnce = true;
    [SerializeField] private bool disableTriggerAfterSpawn = true;

    private bool hasSpawnedTreasure;
    private GameObject spawnedTreasureRoot;

    public Vector3 FinderPosition => transform.position;

    private void Awake()
    {
        CacheComponents();
        ConfigureTorchSource();
    }

    private void OnEnable()
    {
        CacheComponents();
        ConfigureTorchSource();
    }

    private void OnValidate()
    {
        treasureQuantity = Mathf.Max(1, treasureQuantity);
        fallbackTriggerRadius = Mathf.Max(0.1f, fallbackTriggerRadius);
        revealDistance = Mathf.Max(0f, revealDistance);
        fullRevealDistance = Mathf.Max(0f, fullRevealDistance);

        CacheComponents();
        ConfigureTorchSource();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryRevealTreasure(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryRevealTreasure(other);
    }

    public void ConfigureRuntime(TorchVisionDefinition targetVision, Item targetItem, int quantity)
    {
        requiredVision = targetVision;
        treasureItem = targetItem;
        treasureQuantity = Mathf.Max(1, quantity);
        CacheComponents();
        ConfigureTorchSource();
    }

    private void CacheComponents()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider>();
        }

        if (triggerCollider == null)
        {
            SphereCollider fallbackCollider = GetComponent<SphereCollider>();
            if (fallbackCollider == null)
            {
                fallbackCollider = gameObject.AddComponent<SphereCollider>();
            }

            fallbackCollider.radius = Mathf.Max(fallbackCollider.radius, fallbackTriggerRadius);
            triggerCollider = fallbackCollider;
        }

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }

        if (torchReceiver == null)
        {
            torchReceiver = GetComponent<TorchLightReceiver>();
        }

        if (torchReceiver == null)
        {
            torchReceiver = gameObject.AddComponent<TorchLightReceiver>();
        }
    }

    private void ConfigureTorchSource()
    {
        if (torchReceiver == null)
        {
            return;
        }

        torchReceiver.ConfigureWorldTorchSource(requiredVision, countsAsEquipped: true);
    }

    private void TryRevealTreasure(Collider other)
    {
        if (other == null)
        {
            return;
        }

        if (spawnOnlyOnce && hasSpawnedTreasure)
        {
            return;
        }

        SquadCharacterController controller = other.GetComponentInParent<SquadCharacterController>();
        if (controller == null)
        {
            return;
        }

        if (requireMatchingVision)
        {
            if (!TorchVisionSystem.IsTorchEquipped(controller))
            {
                return;
            }

            TorchVisionDefinition activeVision = TorchVisionSystem.GetVisionFor(controller);
            if (requiredVision != null && activeVision != requiredVision)
            {
                return;
            }
        }

        SpawnTreasure();
    }

    private void SpawnTreasure()
    {
        if (treasureItem == null)
        {
            Debug.LogWarning($"TreasureFinder '{name}' n'a aucun item configure.", this);
            return;
        }

        Vector3 spawnPosition = transform.position + spawnOffset;
        GameObject instance = treasureItem.CreateWorldInstance(spawnPosition, Quaternion.identity);
        if (instance == null)
        {
            return;
        }

        GameObject root = instance;
        if (createLootContainer)
        {
            LootContainer lootContainer = treasureItem.CreateDroppedLootContainer(
                instance,
                treasureQuantity,
                destroyLootContainerWhenEmpty,
                collectable);

            if (lootContainer != null)
            {
                root = lootContainer.gameObject;
            }
        }

        if (gateSpawnedTreasureWithVision && requiredVision != null && root != null)
        {
            TorchVisionSensitive treasureGate = root.GetComponent<TorchVisionSensitive>();
            if (treasureGate == null)
            {
                treasureGate = root.AddComponent<TorchVisionSensitive>();
            }

            treasureGate.ConfigureRuntime(
                requiredVision,
                TorchVisionSensitive.VisibilityMode.VisibleOnlyWhenVisionMatches,
                requireEquippedTorch: true,
                runtimeVisualRoot: root.transform,
                includeChildRenderers: true,
                maxRevealDistance: revealDistance,
                fullRevealDistance: fullRevealDistance,
                driveColliders: true,
                driveBehaviours: false);
        }

        hasSpawnedTreasure = true;
        spawnedTreasureRoot = root;

        if (disableTriggerAfterSpawn && triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }
    }
}
