using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public class PersistentContainerState : MonoBehaviour, IPersistentStateProvider
{
    [Serializable]
    private sealed class ContainerStateData
    {
        public string ContainerItemId;
        public bool Collectable;
        public bool DestroyWhenEmpty;
        public bool Locked;
        public bool TrapTriggered;
        public List<ItemEntry> Items = new List<ItemEntry>();
    }

    [Serializable]
    private sealed class ItemEntry
    {
        public string ItemId;
        public int Quantity;
    }

    [SerializeField] private InteractableItem container;

    public string ProviderId => "container";

    private void Awake()
    {
        if (container == null)
        {
            container = GetComponent<InteractableItem>();
        }
    }

    public byte[] CaptureState(PersistentStateContext context)
    {
        if (container == null)
        {
            PersistentStateValidation.LogValidation(
                "container_partial_loot",
                false,
                $"persistentId='{PersistentStateValidation.ResolvePersistentId(this)}' provider='{ProviderId}' containerMissing=true capture=true",
                this,
                context);
            return Array.Empty<byte>();
        }

        ContainerStateData data = new ContainerStateData
        {
            ContainerItemId = container.representedItem != null ? ItemIdUtils.GetItemId(container.representedItem) : string.Empty,
            Collectable = container.allowTake,
            DestroyWhenEmpty = container.destroyWhenStorageEmpty,
            Locked = container.isLocked,
            TrapTriggered = container.HasTriggeredTrap
        };

        if (container.storedItems != null)
        {
            for (int i = 0; i < container.storedItems.Count; i++)
            {
                InteractableItem.LootItemEntry entry = container.storedItems[i];
                if (entry == null || entry.item == null || entry.quantity <= 0)
                {
                    continue;
                }

                string itemId = ItemIdUtils.GetItemId(entry.item);
                if (string.IsNullOrWhiteSpace(itemId))
                {
                    continue;
                }

                data.Items.Add(new ItemEntry
                {
                    ItemId = itemId,
                    Quantity = entry.quantity
                });
            }
        }

        return PersistentStateJson.ToBytes(data);
    }

    public void ApplyState(byte[] state, PersistentApplyPhase phase, PersistentStateContext context)
    {
        if (phase != PersistentApplyPhase.ApplyGameplayState)
        {
            return;
        }

        if (container == null)
        {
            PersistentStateValidation.LogValidation(
                "container_partial_loot",
                false,
                $"persistentId='{PersistentStateValidation.ResolvePersistentId(this)}' provider='{ProviderId}' containerMissing=true apply=true",
                this,
                context);
            return;
        }

        if (!PersistentStateJson.TryFromBytes(state, ProviderId, container, context, out ContainerStateData data))
        {
            return;
        }

        string scenario = PersistentStateValidation.ResolveContainerScenario(container);
        if (string.Equals(scenario, "dropped_loot", StringComparison.Ordinal))
        {
            container.interactableCategory = InteractableItem.InteractableCategory.RecoverableItem;
        }

        List<InteractableItem.LootItemEntry> entries = new List<InteractableItem.LootItemEntry>();
        int expectedStacks = 0;
        int expectedQuantity = 0;
        int missingItemDefinitions = 0;
        Item resolvedContainerItem = null;

        if (!string.IsNullOrWhiteSpace(data.ContainerItemId))
        {
            resolvedContainerItem = ItemRegistry.Resolve(data.ContainerItemId);
            if (resolvedContainerItem == null)
            {
                PersistentStateValidation.LogValidation(
                    scenario,
                    false,
                    $"persistentId='{PersistentStateValidation.ResolvePersistentId(container)}' unresolvedContainerItemId='{data.ContainerItemId}'",
                    container,
                    context);
            }
        }

        if (data.Items != null)
        {
            for (int i = 0; i < data.Items.Count; i++)
            {
                ItemEntry itemEntry = data.Items[i];
                if (itemEntry == null || string.IsNullOrWhiteSpace(itemEntry.ItemId) || itemEntry.Quantity <= 0)
                {
                    continue;
                }

                expectedStacks++;
                expectedQuantity += itemEntry.Quantity;

                Item item = ItemRegistry.Resolve(itemEntry.ItemId);
                if (item == null)
                {
                    missingItemDefinitions++;
                    PersistentStateValidation.LogValidation(
                        scenario,
                        false,
                        $"persistentId='{PersistentStateValidation.ResolvePersistentId(container)}' unresolvedItemId='{itemEntry.ItemId}'",
                        container,
                        context);
                    continue;
                }

                entries.Add(new InteractableItem.LootItemEntry
                {
                    item = item,
                    quantity = itemEntry.Quantity
                });
            }
        }

        container.representedItem = string.IsNullOrWhiteSpace(data.ContainerItemId) ? null : resolvedContainerItem;
        container.allowTake = data.Collectable;
        container.destroyWhenStorageEmpty = data.DestroyWhenEmpty;
        container.RestoreLockedState(data.Locked);
        container.RestoreTrapTriggeredState(data.TrapTriggered);
        container.SetLootItems(entries, false);

        int actualStacks = PersistentStateValidation.CountStacks(container.storedItems);
        int actualQuantity = PersistentStateValidation.CountQuantity(container.storedItems);
        bool success =
            missingItemDefinitions == 0 &&
            actualStacks == expectedStacks &&
            actualQuantity == expectedQuantity &&
            string.Equals(ItemIdUtils.GetItemId(container.representedItem), data.ContainerItemId ?? string.Empty, StringComparison.Ordinal) &&
            container.allowTake == data.Collectable &&
            container.destroyWhenStorageEmpty == data.DestroyWhenEmpty &&
            container.isLocked == data.Locked &&
            container.HasTriggeredTrap == data.TrapTriggered;
        PersistentStateValidation.LogValidation(
            scenario,
            success,
            $"persistentId='{PersistentStateValidation.ResolvePersistentId(container)}' containerItemId='{ItemIdUtils.GetItemId(container.representedItem)}' expectedStacks={expectedStacks} actualStacks={actualStacks} expectedQuantity={expectedQuantity} actualQuantity={actualQuantity} collectable={container.allowTake} destroyWhenEmpty={container.destroyWhenStorageEmpty} locked={container.isLocked} trapTriggered={container.HasTriggeredTrap}",
            container,
            context);
    }
}

[DisallowMultipleComponent]
public class PersistentBeaconState : MonoBehaviour, IPersistentStateProvider
{
    [Serializable]
    private sealed class BeaconStateData
    {
        public float R;
        public float G;
        public float B;
        public float A = 1f;
    }

    [SerializeField] private BeaconMarker beacon;

    public string ProviderId => "beacon";

    private void Awake()
    {
        ResolveBeacon();
    }

    public byte[] CaptureState(PersistentStateContext context)
    {
        ResolveBeacon();
        if (beacon == null)
        {
            return Array.Empty<byte>();
        }

        Color color = beacon.MarkerColor;
        return PersistentStateJson.ToBytes(new BeaconStateData
        {
            R = color.r,
            G = color.g,
            B = color.b,
            A = color.a
        });
    }

    public void ApplyState(byte[] state, PersistentApplyPhase phase, PersistentStateContext context)
    {
        if (phase != PersistentApplyPhase.ApplyGameplayState)
        {
            return;
        }

        ResolveBeacon();
        if (beacon == null)
        {
            return;
        }

        if (!PersistentStateJson.TryFromBytes(state, ProviderId, beacon, context, out BeaconStateData data))
        {
            return;
        }

        beacon.SetColor(new Color(data.R, data.G, data.B, data.A));
    }

    private void ResolveBeacon()
    {
        if (beacon == null)
        {
            beacon = GetComponent<BeaconMarker>();
        }

        if (beacon == null)
        {
            beacon = GetComponentInChildren<BeaconMarker>(true);
        }
    }
}

[DisallowMultipleComponent]
public class PersistentKnowledgeState : MonoBehaviour, IPersistentStateProvider
{
    [Serializable]
    private sealed class KnowledgeStateData
    {
        public List<string> UnlockedKnowledgeIds = new List<string>();
    }

    [SerializeField] private KnowledgeManager knowledgeManager;

    public string ProviderId => "knowledge";

    private void Awake()
    {
        ResolveKnowledgeManager();
    }

    public byte[] CaptureState(PersistentStateContext context)
    {
        ResolveKnowledgeManager();
        if (knowledgeManager == null)
        {
            PersistentStateValidation.LogValidation(
                "treasure_found",
                false,
                $"persistentId='{PersistentStateValidation.ResolvePersistentId(this)}' provider='{ProviderId}' knowledgeManagerMissing=true capture=true",
                this,
                context);
            return Array.Empty<byte>();
        }

        KnowledgeStateData data = new KnowledgeStateData();
        IReadOnlyList<KnowledgeSO> unlocked = knowledgeManager.UnlockedKnowledge;
        if (unlocked != null)
        {
            for (int i = 0; i < unlocked.Count; i++)
            {
                string knowledgeId = PersistentGameplayLookup.GetKnowledgeId(unlocked[i]);
                if (string.IsNullOrWhiteSpace(knowledgeId))
                {
                    continue;
                }

                data.UnlockedKnowledgeIds.Add(knowledgeId);
            }
        }

        data.UnlockedKnowledgeIds.Sort(StringComparer.Ordinal);
        return PersistentStateJson.ToBytes(data);
    }

    public void ApplyState(byte[] state, PersistentApplyPhase phase, PersistentStateContext context)
    {
        if (phase != PersistentApplyPhase.ApplyGameplayState)
        {
            return;
        }

        ResolveKnowledgeManager();
        if (knowledgeManager == null)
        {
            PersistentStateValidation.LogValidation(
                "treasure_found",
                false,
                $"persistentId='{PersistentStateValidation.ResolvePersistentId(this)}' knowledgeManagerMissing=true",
                this,
                context);
            return;
        }

        if (!PersistentStateJson.TryFromBytes(state, ProviderId, knowledgeManager, context, out KnowledgeStateData data))
        {
            return;
        }

        List<KnowledgeSO> restored = new List<KnowledgeSO>();
        List<string> expectedIds = new List<string>();
        HashSet<string> seenIds = new HashSet<string>(StringComparer.Ordinal);
        int unresolvedCount = 0;

        if (data.UnlockedKnowledgeIds != null)
        {
            for (int i = 0; i < data.UnlockedKnowledgeIds.Count; i++)
            {
                string knowledgeId = data.UnlockedKnowledgeIds[i];
                if (string.IsNullOrWhiteSpace(knowledgeId) || !seenIds.Add(knowledgeId))
                {
                    continue;
                }

                expectedIds.Add(knowledgeId);
                KnowledgeSO knowledge = PersistentGameplayLookup.ResolveKnowledge(knowledgeId);
                if (knowledge == null)
                {
                    unresolvedCount++;
                    PersistentStateValidation.LogValidation(
                        "treasure_found",
                        false,
                        $"persistentId='{PersistentStateValidation.ResolvePersistentId(this)}' unresolvedKnowledgeId='{knowledgeId}'",
                        knowledgeManager,
                        context);
                    continue;
                }

                restored.Add(knowledge);
            }
        }

        knowledgeManager.RestoreUnlockedKnowledge(restored);

        List<string> actualIds = new List<string>();
        IReadOnlyList<KnowledgeSO> unlocked = knowledgeManager.UnlockedKnowledge;
        if (unlocked != null)
        {
            for (int i = 0; i < unlocked.Count; i++)
            {
                string knowledgeId = PersistentGameplayLookup.GetKnowledgeId(unlocked[i]);
                if (string.IsNullOrWhiteSpace(knowledgeId))
                {
                    continue;
                }

                actualIds.Add(knowledgeId);
            }
        }

        bool success =
            unresolvedCount == 0 &&
            PersistentStateValidation.MatchStringSet(expectedIds, actualIds);
        PersistentStateValidation.LogValidation(
            "treasure_found",
            success,
            $"persistentId='{PersistentStateValidation.ResolvePersistentId(this)}' expectedKnowledge='{PersistentStateValidation.DescribeStringSet(expectedIds)}' actualKnowledge='{PersistentStateValidation.DescribeStringSet(actualIds)}'",
            knowledgeManager,
            context);
    }

    private void ResolveKnowledgeManager()
    {
        if (knowledgeManager == null)
        {
            knowledgeManager = GetComponent<KnowledgeManager>();
        }

        if (knowledgeManager == null)
        {
            knowledgeManager = KnowledgeManager.GetOrCreate();
        }
    }
}

[DisallowMultipleComponent]
public class PersistentSecretPassageState : MonoBehaviour, IPersistentStateProvider
{
    [Serializable]
    private sealed class SecretPassageStateData
    {
        public bool Detected;
    }

    [SerializeField] private TrouEtroit secretPassage;

    public string ProviderId => "secret_passage";

    private void Awake()
    {
        if (secretPassage == null)
        {
            secretPassage = GetComponent<TrouEtroit>();
        }
    }

    public byte[] CaptureState(PersistentStateContext context)
    {
        if (secretPassage == null)
        {
            PersistentStateValidation.LogValidation(
                "interactable_custom_state",
                false,
                $"persistentId='{PersistentStateValidation.ResolvePersistentId(this)}' provider='{ProviderId}' secretPassageMissing=true capture=true",
                this,
                context);
            return Array.Empty<byte>();
        }

        return PersistentStateJson.ToBytes(new SecretPassageStateData
        {
            Detected = secretPassage.IsDetected
        });
    }

    public void ApplyState(byte[] state, PersistentApplyPhase phase, PersistentStateContext context)
    {
        if (phase != PersistentApplyPhase.ApplyGameplayState)
        {
            return;
        }

        if (secretPassage == null)
        {
            PersistentStateValidation.LogValidation(
                "interactable_custom_state",
                false,
                $"persistentId='{PersistentStateValidation.ResolvePersistentId(this)}' provider='{ProviderId}' secretPassageMissing=true apply=true",
                this,
                context);
            return;
        }

        if (!PersistentStateJson.TryFromBytes(state, ProviderId, secretPassage, context, out SecretPassageStateData data))
        {
            return;
        }

        secretPassage.RestoreDetectedState(data.Detected);
        PersistentStateValidation.LogValidation(
            "interactable_custom_state",
            secretPassage.IsDetected == data.Detected,
            $"persistentId='{PersistentStateValidation.ResolvePersistentId(secretPassage)}' detected={secretPassage.IsDetected}",
            secretPassage,
            context);
    }
}

[DisallowMultipleComponent]
public class PersistentPuzzleElementState : MonoBehaviour, IPersistentStateProvider
{
    [Serializable]
    private sealed class PuzzleStateData
    {
        public bool LeverAActive;
        public bool LeverBActive;
        public bool PuzzleSolved;
    }

    [SerializeField] private TwoLeverPuzzle puzzle;
    [SerializeField] private Lever leverA;
    [SerializeField] private Lever leverB;

    public string ProviderId => "puzzle";

    private void Awake()
    {
        if (puzzle == null)
        {
            puzzle = GetComponent<TwoLeverPuzzle>();
        }

        if (puzzle != null)
        {
            leverA = leverA != null ? leverA : puzzle.leverA;
            leverB = leverB != null ? leverB : puzzle.leverB;
        }
    }

    public byte[] CaptureState(PersistentStateContext context)
    {
        if (puzzle == null && leverA == null && leverB == null)
        {
            PersistentStateValidation.LogValidation(
                "puzzle_progress",
                false,
                $"persistentId='{PersistentStateValidation.ResolvePersistentId(this)}' provider='{ProviderId}' puzzleMissing=true capture=true",
                this,
                context);
            return Array.Empty<byte>();
        }

        PuzzleStateData data = new PuzzleStateData
        {
            LeverAActive = leverA != null && leverA.IsActive,
            LeverBActive = leverB != null && leverB.IsActive,
            PuzzleSolved = puzzle != null && puzzle.IsTriggered
        };

        return PersistentStateJson.ToBytes(data);
    }

    public void ApplyState(byte[] state, PersistentApplyPhase phase, PersistentStateContext context)
    {
        if (phase != PersistentApplyPhase.ApplyGameplayState)
        {
            return;
        }

        if (puzzle == null && leverA == null && leverB == null)
        {
            PersistentStateValidation.LogValidation(
                "puzzle_progress",
                false,
                $"persistentId='{PersistentStateValidation.ResolvePersistentId(this)}' provider='{ProviderId}' puzzleMissing=true apply=true",
                this,
                context);
            return;
        }

        if (!PersistentStateJson.TryFromBytes(state, ProviderId, puzzle != null ? (Component)puzzle : this, context, out PuzzleStateData data))
        {
            return;
        }

        if (puzzle != null)
        {
            puzzle.RestoreState(data.LeverAActive, data.LeverBActive, data.PuzzleSolved);
        }
        else
        {
            leverA?.RestoreActiveState(data.LeverAActive, data.LeverAActive);
            leverB?.RestoreActiveState(data.LeverBActive, data.LeverBActive);
        }

        bool actualLeverA = leverA != null && leverA.IsActive;
        bool actualLeverB = leverB != null && leverB.IsActive;
        bool actualSolved = puzzle != null ? puzzle.IsTriggered : actualLeverA && actualLeverB;
        bool success =
            actualLeverA == data.LeverAActive &&
            actualLeverB == data.LeverBActive &&
            actualSolved == data.PuzzleSolved;
        PersistentStateValidation.LogValidation(
            "puzzle_progress",
            success,
            $"persistentId='{PersistentStateValidation.ResolvePersistentId(puzzle != null ? puzzle : transform)}' leverA={actualLeverA} leverB={actualLeverB} solved={actualSolved}",
            puzzle != null ? puzzle : (UnityEngine.Object)this,
            context);
    }
}

[DisallowMultipleComponent]
public class PersistentFlameState : MonoBehaviour, IPersistentStateProvider
{
    [Serializable]
    private sealed class FlameStateData
    {
        public bool IsLit;
    }

    [SerializeField] private Flame flame;

    public string ProviderId => "flame";

    private void Awake()
    {
        if (flame == null)
        {
            flame = GetComponent<Flame>();
        }
    }

    public byte[] CaptureState(PersistentStateContext context)
    {
        if (flame == null)
        {
            PersistentStateValidation.LogValidation(
                "flame_world_rules",
                false,
                $"persistentId='{PersistentStateValidation.ResolvePersistentId(this)}' provider='{ProviderId}' flameMissing=true capture=true",
                this,
                context);
            return Array.Empty<byte>();
        }

        return PersistentStateJson.ToBytes(new FlameStateData
        {
            IsLit = flame.IsLit
        });
    }

    public void ApplyState(byte[] state, PersistentApplyPhase phase, PersistentStateContext context)
    {
        if (flame == null)
        {
            PersistentStateValidation.LogValidation(
                "flame_world_rules",
                false,
                $"persistentId='{PersistentStateValidation.ResolvePersistentId(this)}' provider='{ProviderId}' flameMissing=true apply=true phase='{phase}'",
                this,
                context);
            return;
        }

        if (!PersistentStateJson.TryFromBytes(state, ProviderId, flame, context, out FlameStateData data))
        {
            return;
        }

        if (phase == PersistentApplyPhase.ApplyGameplayState)
        {
            flame.SetLit(data.IsLit);
            PersistentStateValidation.LogValidation(
                "flame_world_rules",
                flame.IsLit == data.IsLit,
                $"persistentId='{PersistentStateValidation.ResolvePersistentId(flame)}' expectedLit={data.IsLit} actualLit={flame.IsLit}",
                flame,
                context);
            return;
        }

        if (phase == PersistentApplyPhase.FinalizeReferences)
        {
            context.WorldRules?.RebuildDerivedFlameVariables();
            ValidateWorldRules(context);
        }
    }

    private void ValidateWorldRules(PersistentStateContext context)
    {
        AgeManager ageManager = ResolveAgeManager();
        if (context == null || context.WorldRules == null || ageManager == null)
        {
            PersistentStateValidation.LogValidation(
                "world_rules_extended",
                false,
                $"persistentId='{PersistentStateValidation.ResolvePersistentId(flame)}' worldRulesMissing={context == null || context.WorldRules == null} ageManagerMissing={ageManager == null}",
                flame,
                context);
            return;
        }

        int expectedLitCount = ageManager.LitAncientFlameCount;
        int expectedCurrentYear = ageManager.CurrentYear;
        int expectedTotalCount = ageManager.TotalAncientFlameCount;

        bool hasLitCount = context.WorldRules.TryGetInt(WorldRulesStateManager.FlameLitCountKey, out int litCount);
        bool hasTotalCount = context.WorldRules.TryGetInt(WorldRulesStateManager.FlameTotalCountKey, out int totalCount);
        bool hasCurrentYear = context.WorldRules.TryGetInt(WorldRulesStateManager.CurrentYearKey, out int currentYear);
        bool baseSuccess =
            hasLitCount &&
            hasCurrentYear &&
            litCount == expectedLitCount &&
            currentYear == expectedCurrentYear;
        PersistentStateValidation.LogValidation(
            "flame_world_rules",
            baseSuccess,
            $"persistentId='{PersistentStateValidation.ResolvePersistentId(flame)}' expectedLitCount={expectedLitCount} actualLitCount={litCount} expectedYear={expectedCurrentYear} actualYear={currentYear}",
            flame,
            context);

        bool hasVolumeProfiles = context.WorldRules.TryGetString(WorldRulesStateManager.ActiveVolumeProfilesKey, out string activeVolumeProfiles);
        string actualVolumeProfiles = context.WorldRules.DescribeActiveVolumeProfiles();
        bool extendedSuccess =
            baseSuccess &&
            hasTotalCount &&
            hasVolumeProfiles &&
            totalCount == expectedTotalCount &&
            string.Equals(activeVolumeProfiles, actualVolumeProfiles, StringComparison.Ordinal);
        PersistentStateValidation.LogValidation(
            "world_rules_extended",
            extendedSuccess,
            $"persistentId='{PersistentStateValidation.ResolvePersistentId(flame)}' expectedTotalFlames={expectedTotalCount} actualTotalFlames={totalCount} expectedVolumeProfiles='{actualVolumeProfiles}' actualVolumeProfiles='{activeVolumeProfiles}'",
            flame,
            context);
    }

    private static AgeManager ResolveAgeManager()
    {
        if (AgeManager.ActiveInstance != null)
        {
            return AgeManager.ActiveInstance;
        }

#if UNITY_2023_1_OR_NEWER
        return FindAnyObjectByType<AgeManager>();
#else
        return FindAnyObjectByType<AgeManager>();
#endif
    }
}

[DisallowMultipleComponent]
public class PersistentBuildingState : MonoBehaviour, IPersistentStateProvider
{
    [Serializable]
    private sealed class BuildingStateData
    {
        public string BuildId;
        public string ItemId;
        public int Level;
        public ulong BuilderInstanceId;
        public bool IsHomeChest;
        public bool IsCraftingBuilding;
        public int CraftSlotCount;
        public bool ContainerCollectable = true;
        public bool ContainerDestroyWhenEmpty;
        public List<string> UnlockedCraftIds = new List<string>();
        public List<BuildingContainerItemData> ContainerItems = new List<BuildingContainerItemData>();
    }

    [Serializable]
    private sealed class BuildingContainerItemData
    {
        public string ItemId;
        public int Quantity;
    }

    [SerializeField] private BuildingInfoInteractable building;
    [SerializeField] private BuilderController builderController;
    [SerializeField] private InteractableItem childContainer;

    public string ProviderId => "building";

    private void Awake()
    {
        if (building == null)
        {
            building = GetComponent<BuildingInfoInteractable>();
        }

        if (builderController == null)
        {
            builderController = GetComponentInParent<BuilderController>();
        }

        if (childContainer == null)
        {
            childContainer = GetComponentInChildren<InteractableItem>(true);
        }
    }

    public byte[] CaptureState(PersistentStateContext context)
    {
        if (!LegacyBuildingSystem.Enabled)
        {
            return Array.Empty<byte>();
        }

        if (building == null)
        {
            PersistentStateValidation.LogValidation(
                "building_placement",
                false,
                $"persistentId='{PersistentStateValidation.ResolvePersistentId(this)}' provider='{ProviderId}' buildingMissing=true capture=true",
                this,
                context);
            return Array.Empty<byte>();
        }

        BuildingStateData data = new BuildingStateData
        {
            BuildId = building.BuildId,
            ItemId = building.BuildingItemId,
            Level = building.Level,
            BuilderInstanceId = building.NetworkBuildingId,
            IsHomeChest = building.IsHomeChest,
            IsCraftingBuilding = building.BuildingItem != null && building.BuildingItem.isCraftingBuilding
        };

        if (building.BuildingItem != null && building.BuildingItem.isCraftingBuilding)
        {
            data.CraftSlotCount = building.BuildingItem.GetCraftSlotsForLevel(building.Level);
            data.UnlockedCraftIds = PersistentGameplayLookup.CollectCraftIds(building.BuildingItem, building.Level);
        }

        if (childContainer != null)
        {
            data.ContainerCollectable = childContainer.allowTake;
            data.ContainerDestroyWhenEmpty = childContainer.destroyWhenStorageEmpty;

            if (childContainer.storedItems != null)
            {
                for (int i = 0; i < childContainer.storedItems.Count; i++)
                {
                    InteractableItem.LootItemEntry entry = childContainer.storedItems[i];
                    if (entry == null || entry.item == null || entry.quantity <= 0)
                    {
                        continue;
                    }

                    string itemId = ItemIdUtils.GetItemId(entry.item);
                    if (string.IsNullOrWhiteSpace(itemId))
                    {
                        continue;
                    }

                    data.ContainerItems.Add(new BuildingContainerItemData
                    {
                        ItemId = itemId,
                        Quantity = entry.quantity
                    });
                }
            }
        }

        return PersistentStateJson.ToBytes(data);
    }

    public void ApplyState(byte[] state, PersistentApplyPhase phase, PersistentStateContext context)
    {
        if (!LegacyBuildingSystem.Enabled)
        {
            return;
        }

        if (building == null)
        {
            PersistentStateValidation.LogValidation(
                "building_placement",
                false,
                $"persistentId='{PersistentStateValidation.ResolvePersistentId(this)}' provider='{ProviderId}' buildingMissing=true apply=true phase='{phase}'",
                this,
                context);
            return;
        }

        if (!PersistentStateJson.TryFromBytes(state, ProviderId, building, context, out BuildingStateData data))
        {
            return;
        }

        Item item = ItemRegistry.Resolve(data.ItemId) ?? building.BuildingItem;

        if (phase == PersistentApplyPhase.ApplyGameplayState)
        {
            if (item == null)
            {
                PersistentStateValidation.LogValidation(
                    data.Level > 1 ? "building_upgrade" : "building_placement",
                    false,
                    $"persistentId='{PersistentStateValidation.ResolvePersistentId(building)}' unresolvedBuildingItemId='{data.ItemId}' buildId='{data.BuildId}'",
                    building,
                    context);
            }

            building.Initialize(data.BuildId, item, Mathf.Max(1, data.Level));
            building.SetNetworkBuildingId(data.BuilderInstanceId);
            building.MarkPresentationOrigin("snapshot_reconstruction", overwrite: false);
            building.RefreshPresentation("snapshot_reconstruction_apply");
            Debug.Log(
                $"[BuildingSync] event='building_reconstructed' path='{PersistentWorldDebug.DescribeTransform(building.transform)}' persistentId='{PersistentStateValidation.ResolvePersistentId(building)}' buildId='{building.BuildId}' itemId='{building.BuildingItemId}' networkId={building.NetworkBuildingId} displayedLevel={building.Level} authoritativeSyncedLevel={Mathf.Max(1, data.Level)} worldUiBound={false} proximityActive={false} visibilityLogicActive={building.openOnProximity} upgradeRefreshCallbackRan=False visualRefreshRan=True source='{building.PresentationOrigin}' reason='snapshot reconstruction restored gameplay state'",
                building);

            int expectedStacks = 0;
            int expectedQuantity = 0;
            int unresolvedContainerItems = 0;
            if (childContainer != null)
            {
                List<InteractableItem.LootItemEntry> entries = new List<InteractableItem.LootItemEntry>();
                if (data.ContainerItems != null)
                {
                    for (int i = 0; i < data.ContainerItems.Count; i++)
                    {
                        BuildingContainerItemData itemData = data.ContainerItems[i];
                        if (itemData == null || string.IsNullOrWhiteSpace(itemData.ItemId) || itemData.Quantity <= 0)
                        {
                            continue;
                        }

                        expectedStacks++;
                        expectedQuantity += itemData.Quantity;

                        Item lootItem = ItemRegistry.Resolve(itemData.ItemId);
                        if (lootItem == null)
                        {
                            unresolvedContainerItems++;
                            PersistentStateValidation.LogValidation(
                                data.Level > 1 ? "building_upgrade" : "building_placement",
                                false,
                                $"persistentId='{PersistentStateValidation.ResolvePersistentId(building)}' unresolvedContainerItemId='{itemData.ItemId}'",
                                building,
                                context);
                            continue;
                        }

                        entries.Add(new InteractableItem.LootItemEntry
                        {
                            item = lootItem,
                            quantity = itemData.Quantity
                        });
                    }
                }

                childContainer.allowTake = data.IsHomeChest ? false : data.ContainerCollectable;
                childContainer.destroyWhenStorageEmpty = data.ContainerDestroyWhenEmpty;
                childContainer.SetLootItems(entries, false);
            }

            ValidateBuildingState(data, expectedStacks, expectedQuantity, unresolvedContainerItems, context);
            ValidateCraftingState(data, item, context);
            return;
        }

        if (phase == PersistentApplyPhase.FinalizeReferences)
        {
            if (item == null)
            {
                PersistentStateValidation.LogValidation(
                    data.Level > 1 ? "building_upgrade" : "building_placement",
                    false,
                    $"persistentId='{PersistentStateValidation.ResolvePersistentId(building)}' unresolvedBuildingItemId='{data.ItemId}' finalize=true",
                    building,
                    context);
                return;
            }

            string scenario = data.Level > 1 ? "building_upgrade" : "building_placement";
            string controllerBindingStatus = ResolveBuilderControllerForFinalize();
            if (builderController == null)
            {
                if (context != null && !context.IsServer)
                {
                    PersistentWorldDebug.Warn(
                        $"building finalize builderControllerStatus='client_optional_absent' persistentId='{PersistentStateValidation.ResolvePersistentId(building)}' networkId={data.BuilderInstanceId} level={data.Level} itemId='{data.ItemId}' note='builder controller intentionally optional on non-authoritative client late-join'",
                        building);
                    return;
                }

                PersistentStateValidation.LogValidation(
                    scenario,
                    false,
                    $"persistentId='{PersistentStateValidation.ResolvePersistentId(building)}' builderControllerMissing=true builderControllerStatus='{controllerBindingStatus}' authoritativeRequired=true",
                    building,
                context);
                return;
            }

            if (context != null && !context.IsServer)
            {
                PersistentWorldDebug.Log(
                    $"building finalize builderControllerStatus='{controllerBindingStatus}' persistentId='{PersistentStateValidation.ResolvePersistentId(building)}' controllerPath='{PersistentWorldDebug.DescribeTransform(builderController.transform)}' networkId={data.BuilderInstanceId} level={data.Level} authoritativeAction='skipped_on_client' note='non-authoritative client does not register or validate builder-owned runtime bookkeeping'",
                    building);
                return;
            }

            PersistentWorldDebug.Log(
                $"building finalize builderControllerStatus='{controllerBindingStatus}' persistentId='{PersistentStateValidation.ResolvePersistentId(building)}' controllerPath='{PersistentWorldDebug.DescribeTransform(builderController.transform)}' networkId={data.BuilderInstanceId} level={data.Level}",
                building);
            builderController.RegisterBuiltBuilding(item, Mathf.Max(1, data.Level), building);
            ValidateBuilderRegistration(data, item, context);
        }
    }

    private string ResolveBuilderControllerForFinalize()
    {
        if (builderController != null)
        {
            return "already_bound";
        }

        builderController = GetComponentInParent<BuilderController>(true);
        if (builderController != null)
        {
            return "rebound_from_parent";
        }

#if UNITY_2023_1_OR_NEWER
        builderController = FindAnyObjectByType<BuilderController>(FindObjectsInactive.Include);
#else
        builderController = FindAnyObjectByType<BuilderController>(FindObjectsInactive.Include);
#endif
        if (builderController != null)
        {
            return "rebound_from_scene";
        }

        return "missing";
    }

    private void ValidateBuildingState(
        BuildingStateData data,
        int expectedStacks,
        int expectedQuantity,
        int unresolvedContainerItems,
        PersistentStateContext context)
    {
        string scenario = data.Level > 1 ? "building_upgrade" : "building_placement";
        int actualStacks = childContainer != null ? PersistentStateValidation.CountStacks(childContainer.storedItems) : 0;
        int actualQuantity = childContainer != null ? PersistentStateValidation.CountQuantity(childContainer.storedItems) : 0;
        bool success =
            unresolvedContainerItems == 0 &&
            string.Equals(building.BuildId ?? string.Empty, data.BuildId ?? string.Empty, StringComparison.Ordinal) &&
            string.Equals(building.BuildingItemId ?? string.Empty, data.ItemId ?? string.Empty, StringComparison.Ordinal) &&
            building.Level == Mathf.Max(1, data.Level) &&
            building.NetworkBuildingId == data.BuilderInstanceId &&
            (childContainer == null ||
             (childContainer.allowTake == (data.IsHomeChest ? false : data.ContainerCollectable) &&
              childContainer.destroyWhenStorageEmpty == data.ContainerDestroyWhenEmpty &&
              actualStacks == expectedStacks &&
              actualQuantity == expectedQuantity));
        PersistentStateValidation.LogValidation(
            scenario,
            success,
            $"persistentId='{PersistentStateValidation.ResolvePersistentId(building)}' buildId='{building.BuildId}' itemId='{building.BuildingItemId}' level={building.Level} networkId={building.NetworkBuildingId} expectedStacks={expectedStacks} actualStacks={actualStacks} expectedQuantity={expectedQuantity} actualQuantity={actualQuantity}",
            building,
            context);
    }

    private void ValidateCraftingState(BuildingStateData data, Item item, PersistentStateContext context)
    {
        if ((!data.IsCraftingBuilding && (item == null || !item.isCraftingBuilding)) || building == null)
        {
            return;
        }

        List<string> actualCraftIds = PersistentGameplayLookup.CollectCraftIds(item, Mathf.Max(1, data.Level));
        int actualCraftSlots = item != null && item.isCraftingBuilding
            ? item.GetCraftSlotsForLevel(Mathf.Max(1, data.Level))
            : 0;
        bool success =
            item != null &&
            item.isCraftingBuilding == data.IsCraftingBuilding &&
            actualCraftSlots == data.CraftSlotCount &&
            PersistentStateValidation.MatchStringSet(data.UnlockedCraftIds, actualCraftIds);
        PersistentStateValidation.LogValidation(
            "crafting_station",
            success,
            $"persistentId='{PersistentStateValidation.ResolvePersistentId(building)}' isCraftingBuilding={item != null && item.isCraftingBuilding} expectedCraftSlots={data.CraftSlotCount} actualCraftSlots={actualCraftSlots} expectedCrafts='{PersistentStateValidation.DescribeStringSet(data.UnlockedCraftIds)}' actualCrafts='{PersistentStateValidation.DescribeStringSet(actualCraftIds)}'",
            building,
            context);
    }

    private void ValidateBuilderRegistration(BuildingStateData data, Item item, PersistentStateContext context)
    {
        if (builderController == null || building == null || item == null)
        {
            return;
        }

        builderController.EnsureBuiltBuildings();
        bool found = false;
        int matchesByInfo = 0;
        int matchesByNetworkId = 0;
        if (builderController.builtBuildings != null)
        {
            for (int i = 0; i < builderController.builtBuildings.Count; i++)
            {
                BuilderController.BuiltBuildingEntry entry = builderController.builtBuildings[i];
                if (entry == null)
                {
                    continue;
                }

                if (entry.info == building)
                {
                    matchesByInfo++;
                    found =
                        entry.networkId == data.BuilderInstanceId &&
                        entry.level == Mathf.Max(1, data.Level) &&
                        entry.building == item;
                }

                if (data.BuilderInstanceId != 0 && entry.networkId == data.BuilderInstanceId)
                {
                    matchesByNetworkId++;
                }
            }
        }

        bool duplicateDetected = matchesByInfo > 1 || matchesByNetworkId > 1;
        if (duplicateDetected)
        {
            PersistentWorldDebug.Error(
                $"duplicated building reconstruction detected persistentId='{PersistentStateValidation.ResolvePersistentId(building)}' matchesByInfo={matchesByInfo} matchesByNetworkId={matchesByNetworkId}",
                building);
        }

        PersistentStateValidation.LogValidation(
            data.Level > 1 ? "building_upgrade" : "building_placement",
            found && !duplicateDetected,
            $"persistentId='{PersistentStateValidation.ResolvePersistentId(building)}' builderRegistered={found} networkId={data.BuilderInstanceId} level={data.Level} matchesByInfo={matchesByInfo} matchesByNetworkId={matchesByNetworkId}",
            building,
            context);
    }
}

internal static class PersistentGameplayLookup
{
    private static readonly Dictionary<string, KnowledgeSO> knowledgeById = new Dictionary<string, KnowledgeSO>(StringComparer.Ordinal);

    public static string GetKnowledgeId(KnowledgeSO knowledge)
    {
        if (knowledge == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(knowledge.knowledgeId))
        {
            return knowledge.knowledgeId;
        }

        if (!string.IsNullOrWhiteSpace(knowledge.UniqueId))
        {
            return knowledge.UniqueId;
        }

        return knowledge.name;
    }

    public static KnowledgeSO ResolveKnowledge(string knowledgeId)
    {
        if (string.IsNullOrWhiteSpace(knowledgeId))
        {
            return null;
        }

        BuildKnowledgeLookup();
        if (knowledgeById.TryGetValue(knowledgeId, out KnowledgeSO resolved) && resolved != null)
        {
            return resolved;
        }

        BuildKnowledgeLookup();
        return knowledgeById.TryGetValue(knowledgeId, out resolved) ? resolved : null;
    }

    public static List<string> CollectCraftIds(Item building, int level)
    {
        List<string> results = new List<string>();
        if (building == null || !building.isCraftingBuilding)
        {
            return results;
        }

        List<Item> unlocked = building.GetUnlockedCraftsForLevel(Mathf.Max(1, level));
        if (unlocked != null)
        {
            for (int i = 0; i < unlocked.Count; i++)
            {
                string craftId = ItemIdUtils.GetItemId(unlocked[i]);
                if (string.IsNullOrWhiteSpace(craftId))
                {
                    continue;
                }

                results.Add(craftId);
            }
        }

        results.Sort(StringComparer.Ordinal);
        return results;
    }

    private static void BuildKnowledgeLookup()
    {
        knowledgeById.Clear();
        KnowledgeSO[] knowledgeAssets = Resources.FindObjectsOfTypeAll<KnowledgeSO>();
        if (knowledgeAssets == null)
        {
            return;
        }

        for (int i = 0; i < knowledgeAssets.Length; i++)
        {
            KnowledgeSO knowledge = knowledgeAssets[i];
            if (knowledge == null)
            {
                continue;
            }

            string knowledgeId = GetKnowledgeId(knowledge);
            if (string.IsNullOrWhiteSpace(knowledgeId))
            {
                continue;
            }

            knowledgeById[knowledgeId] = knowledge;
        }
    }
}

internal static class PersistentStateValidation
{
    public static void LogValidation(string scenario, bool success, string message, UnityEngine.Object logContext, PersistentStateContext applyContext)
    {
        string fullMessage = $"validation scenario={scenario} success={success} {message}";
        if (success)
        {
            PersistentWorldDebug.Log(fullMessage, logContext);
            return;
        }

        PersistentWorldDebug.Error(fullMessage, logContext);
        applyContext?.ReportValidationIssue(fullMessage);
    }

    public static string ResolvePersistentId(Component component)
    {
        if (component == null)
        {
            return "<null>";
        }

        PersistentNetworkObject persistentObject = component.GetComponent<PersistentNetworkObject>();
        if (persistentObject == null)
        {
            persistentObject = component.GetComponentInParent<PersistentNetworkObject>(true);
        }

        return persistentObject != null && !string.IsNullOrWhiteSpace(persistentObject.PersistentId)
            ? persistentObject.PersistentId
            : PersistentWorldDebug.DescribeTransform(component.transform);
    }

    public static int CountStacks(List<InteractableItem.LootItemEntry> entries)
    {
        if (entries == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            InteractableItem.LootItemEntry entry = entries[i];
            if (entry == null || entry.item == null || entry.quantity <= 0)
            {
                continue;
            }

            count++;
        }

        return count;
    }

    public static int CountQuantity(List<InteractableItem.LootItemEntry> entries)
    {
        if (entries == null)
        {
            return 0;
        }

        int quantity = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            InteractableItem.LootItemEntry entry = entries[i];
            if (entry == null || entry.item == null || entry.quantity <= 0)
            {
                continue;
            }

            quantity += entry.quantity;
        }

        return quantity;
    }

    public static string ResolveContainerScenario(InteractableItem container)
    {
        if (container == null)
        {
            return "container_partial_loot";
        }

        PersistentNetworkObject persistentObject = container.GetComponent<PersistentNetworkObject>();
        if (persistentObject == null)
        {
            persistentObject = container.GetComponentInParent<PersistentNetworkObject>(true);
        }

        if (persistentObject != null &&
            persistentObject.IsRuntimeObject &&
            !string.IsNullOrWhiteSpace(persistentObject.RuntimePrefabId) &&
            persistentObject.RuntimePrefabId.StartsWith(PersistentWorldSceneInstaller.DroppedLootPrefabPrefix, StringComparison.Ordinal))
        {
            return "dropped_loot";
        }

        return "container_partial_loot";
    }

    public static bool MatchStringSet(IReadOnlyList<string> expected, IReadOnlyList<string> actual)
    {
        HashSet<string> expectedSet = BuildStringSet(expected);
        HashSet<string> actualSet = BuildStringSet(actual);
        return expectedSet.SetEquals(actualSet);
    }

    public static string DescribeStringSet(IReadOnlyList<string> values)
    {
        HashSet<string> normalized = BuildStringSet(values);
        if (normalized.Count == 0)
        {
            return string.Empty;
        }

        List<string> sorted = new List<string>(normalized);
        sorted.Sort(StringComparer.Ordinal);
        return string.Join("|", sorted);
    }

    private static HashSet<string> BuildStringSet(IReadOnlyList<string> values)
    {
        HashSet<string> results = new HashSet<string>(StringComparer.Ordinal);
        if (values == null)
        {
            return results;
        }

        for (int i = 0; i < values.Count; i++)
        {
            string value = values[i];
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            results.Add(value);
        }

        return results;
    }
}

internal static class PersistentStateJson
{
    public static byte[] ToBytes(object value)
    {
        if (value == null)
        {
            return Array.Empty<byte>();
        }

        string json = JsonUtility.ToJson(value);
        if (string.IsNullOrEmpty(json))
        {
            return Array.Empty<byte>();
        }

        return Encoding.UTF8.GetBytes(json);
    }

    public static T FromBytes<T>(byte[] bytes) where T : class
    {
        if (bytes == null || bytes.Length == 0)
        {
            return null;
        }

        string json = Encoding.UTF8.GetString(bytes);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonUtility.FromJson<T>(json);
    }

    public static bool TryFromBytes<T>(
        byte[] bytes,
        string providerId,
        UnityEngine.Object logContext,
        PersistentStateContext context,
        out T value) where T : class
    {
        value = null;

        string persistentId = ResolvePersistentId(logContext);
        if (bytes == null || bytes.Length == 0)
        {
            ReportInvalidPayload(providerId, persistentId, "empty payload", logContext, context);
            return false;
        }

        string json;
        try
        {
            json = Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex)
        {
            ReportInvalidPayload(providerId, persistentId, $"payload decode failed error='{ex.Message}'", logContext, context);
            return false;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            ReportInvalidPayload(providerId, persistentId, "payload json is empty", logContext, context);
            return false;
        }

        try
        {
            value = JsonUtility.FromJson<T>(json);
        }
        catch (Exception ex)
        {
            ReportInvalidPayload(providerId, persistentId, $"payload json parse failed error='{ex.Message}'", logContext, context);
            return false;
        }

        if (value == null)
        {
            ReportInvalidPayload(providerId, persistentId, "payload deserialized to null", logContext, context);
            return false;
        }

        return true;
    }

    private static void ReportInvalidPayload(
        string providerId,
        string persistentId,
        string reason,
        UnityEngine.Object logContext,
        PersistentStateContext context)
    {
        string message =
            $"provider payload invalid provider='{providerId}' persistentId='{persistentId}' phase='{context?.CurrentPhase}' reason='{reason}'";
        PersistentWorldDebug.Error(message, logContext);
        context?.ReportValidationIssue(message);
    }

    private static string ResolvePersistentId(UnityEngine.Object logContext)
    {
        if (logContext is Component component)
        {
            return PersistentStateValidation.ResolvePersistentId(component);
        }

        return logContext != null ? logContext.name : "<null>";
    }
}
