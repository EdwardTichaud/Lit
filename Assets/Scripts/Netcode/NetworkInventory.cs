using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// Inventaire synchronise via Netcode pour un personnage.
[RequireComponent(typeof(NetworkObject))]
public class NetworkInventory : NetworkBehaviour
{
    [SerializeField] private SquadCharacterController controller;

    private readonly NetworkList<NetItemStack> netItems = new NetworkList<NetItemStack>(
        null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> torchSeconds = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> torchEquipped = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkList<FixedString64Bytes> netEquippedInteractionItems = new NetworkList<FixedString64Bytes>(
        null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public event System.Action InventoryChanged;
    [SerializeField] private bool logInventoryDebug = true;

    private void Awake()
    {
        if (controller == null)
        {
            controller = GetComponent<SquadCharacterController>();
        }
    }

    public override void OnNetworkSpawn()
    {
        if (logInventoryDebug)
        {
            Debug.Log($"NetworkInventory: OnNetworkSpawn (IsServer={IsServer}, IsClient={IsClient}, IsOwner={IsOwner}) on {name}", this);
        }
        netItems.OnListChanged += OnNetItemsChanged;
        torchSeconds.OnValueChanged += OnTorchChanged;
        torchEquipped.OnValueChanged += OnTorchChanged;
        netEquippedInteractionItems.OnListChanged += OnEquippedInteractionItemsChanged;

        if (IsServer)
        {
            EnsureStarterInventoryIfEmpty();
            if (logInventoryDebug)
            {
                Debug.Log($"NetworkInventory: After EnsureStarterInventoryIfEmpty -> items={controller?.Items?.Count ?? -1}, torchSeconds={controller?.TorchSecondsRemaining ?? -1}", this);
            }
            SyncFromController();
        }

        ApplyToController();
    }

    public override void OnNetworkDespawn()
    {
        netItems.OnListChanged -= OnNetItemsChanged;
        torchSeconds.OnValueChanged -= OnTorchChanged;
        torchEquipped.OnValueChanged -= OnTorchChanged;
        netEquippedInteractionItems.OnListChanged -= OnEquippedInteractionItemsChanged;
    }

    public void SyncFromController()
    {
        if (!IsServer || controller == null)
        {
            return;
        }

        Dictionary<string, int> counts = new Dictionary<string, int>();
        IReadOnlyList<Item> items = controller.Items;
        if (items != null)
        {
            for (int i = 0; i < items.Count; i++)
            {
                Item item = items[i];
                if (item == null)
                {
                    continue;
                }

                string id = ItemIdUtils.GetItemId(item);
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                if (!counts.TryGetValue(id, out int count))
                {
                    counts[id] = 1;
                }
                else
                {
                    counts[id] = count + 1;
                }
            }
        }

        torchSeconds.Value = controller.TorchSecondsRemaining;
        torchEquipped.Value = controller.IsTorchEquipped;

        netItems.Clear();
        foreach (KeyValuePair<string, int> pair in counts)
        {
            netItems.Add(new NetItemStack(pair.Key, pair.Value));
        }

        netEquippedInteractionItems.Clear();
        IReadOnlyList<Item> equippedItems = controller.EquippedInteractionItems;
        if (equippedItems != null)
        {
            HashSet<string> equippedIds = new HashSet<string>();
            for (int i = 0; i < equippedItems.Count; i++)
            {
                Item item = equippedItems[i];
                string id = ItemIdUtils.GetItemId(item);
                if (string.IsNullOrWhiteSpace(id) || !equippedIds.Add(id))
                {
                    continue;
                }

                netEquippedInteractionItems.Add(new FixedString64Bytes(id));
            }
        }

        if (logInventoryDebug)
        {
            Debug.Log($"NetworkInventory: SyncFromController -> netItems={netItems.Count}, equippedItems={netEquippedInteractionItems.Count}, torchSeconds={torchSeconds.Value}, torchEquipped={torchEquipped.Value}", this);
        }
    }

    public bool TryAddItem(Item item, int quantity)
    {
        if (!IsServer || controller == null || item == null || quantity <= 0)
        {
            return false;
        }

        controller.AddItem(item, quantity);
        SyncFromController();
        return true;
    }

    public bool TryRemoveItemQuantity(Item item, int quantity)
    {
        if (!IsServer || controller == null || item == null || quantity <= 0)
        {
            return false;
        }

        bool removed = controller.TryRemoveItemQuantity(item, quantity);
        if (removed)
        {
            SyncFromController();
        }

        return removed;
    }

    public bool RequestUseItem(Item item)
    {
        if (item == null)
        {
            return false;
        }

        if (!IsSpawned || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            if (controller == null)
            {
                return false;
            }

            CombatSessionManager combatManager = CombatSessionManager.Instance;
            if (combatManager != null && !combatManager.CanUseItemNow(controller, out string combatReason))
            {
                InfoBoxUI.TryShow(combatReason);
                return false;
            }

            if (controller.TryUseItem(item, out string reason))
            {
                PlayActionAudio(ActionAudioCue.InventoryUse);
                InfoBoxUI.TryShow(item.GetUseSuccessMessage());
                CombatSessionManager.EnsureInstance()?.NotifyInventoryItemUsed(controller);
                return true;
            }

            InfoBoxUI.TryShow(reason);
            return false;
        }

        if (IsServer)
        {
            bool success = ExecuteUseItem(item, out string feedback);
            if (success)
            {
                PlayActionAudio(ActionAudioCue.InventoryUse);
            }
            if (!string.IsNullOrWhiteSpace(feedback))
            {
                InfoBoxUI.TryShow(feedback);
            }
            return success;
        }

        string itemId = ItemIdUtils.GetItemId(item);
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        RequestUseItemServerRpc(itemId);
        return true;
    }

    public bool RequestBreakItem(Item item)
    {
        if (item == null)
        {
            return false;
        }

        if (!IsSpawned || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            if (controller == null)
            {
                return false;
            }

            if (item.TryBreak(controller, out string reason))
            {
                PlayActionAudio(ActionAudioCue.InventoryBreak);
                InfoBoxUI.TryShow(item.GetBreakSuccessMessage());
                return true;
            }

            InfoBoxUI.TryShow(reason);
            return false;
        }

        if (IsServer)
        {
            bool success = ExecuteBreakItem(item, out string feedback);
            if (success)
            {
                PlayActionAudio(ActionAudioCue.InventoryBreak);
            }
            if (!string.IsNullOrWhiteSpace(feedback))
            {
                InfoBoxUI.TryShow(feedback);
            }
            return success;
        }

        string itemId = ItemIdUtils.GetItemId(item);
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        RequestBreakItemServerRpc(itemId);
        return true;
    }

    public bool RequestDropItem(Item item, int quantity, Vector3 position, Quaternion rotation, bool allowDropWithoutPrefab, bool destroyWhenEmpty)
    {
        if (item == null || quantity <= 0)
        {
            return false;
        }

        if (!IsSpawned || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            if (controller == null)
            {
                return false;
            }

            if (!item.CanInstantDropFromInventory(controller, allowDropWithoutPrefab, out string reason))
            {
                InfoBoxUI.TryShow(reason);
                return false;
            }

            if (!controller.TryRemoveItemQuantity(item, quantity))
            {
                return false;
            }

            SpawnWorldItem(item, quantity, position, rotation, true, destroyWhenEmpty, true, 0u, false);
            SyncFromController();
            PlayActionAudio(ActionAudioCue.InventoryDrop);
            InfoBoxUI.TryShow(item.GetDropSuccessMessage());
            return true;
        }

        if (IsServer)
        {
            bool success = ExecuteDropItem(item, quantity, position, rotation, allowDropWithoutPrefab, destroyWhenEmpty, out string feedback);
            if (success)
            {
                PlayActionAudio(ActionAudioCue.InventoryDrop);
            }
            if (!string.IsNullOrWhiteSpace(feedback))
            {
                InfoBoxUI.TryShow(feedback);
            }
            return success;
        }

        string itemId = ItemIdUtils.GetItemId(item);
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        RequestDropItemServerRpc(itemId, quantity, position, rotation, allowDropWithoutPrefab, destroyWhenEmpty);
        return true;
    }

    public bool RequestPlaceItem(
        Item item,
        Vector3 position,
        Quaternion rotation,
        bool createLootContainer,
        bool destroyWhenEmpty,
        bool allowDropWithoutPrefab,
        uint placementColorPacked = 0u,
        bool usePlacementColor = false)
    {
        if (item == null)
        {
            return false;
        }

        if (!IsSpawned || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            if (controller == null)
            {
                return false;
            }

            if (!item.CanPlaceFromInventory(controller, out string reason))
            {
                InfoBoxUI.TryShow(reason);
                return false;
            }

            if (!controller.TryRemoveItem(item, 1))
            {
                return false;
            }

            SpawnWorldItem(item, 1, position, rotation, createLootContainer, destroyWhenEmpty, true, placementColorPacked, usePlacementColor);
            SyncFromController();
            PlayActionAudio(ActionAudioCue.InventoryPlaceConfirm);
            InfoBoxUI.TryShow(item.GetPlaceSuccessMessage());
            return true;
        }

        if (IsServer)
        {
            bool success = ExecutePlaceItem(
                item,
                position,
                rotation,
                createLootContainer,
                destroyWhenEmpty,
                allowDropWithoutPrefab,
                placementColorPacked,
                usePlacementColor,
                out string feedback);
            if (!string.IsNullOrWhiteSpace(feedback))
            {
                if (success)
                {
                    PlayActionAudio(ActionAudioCue.InventoryPlaceConfirm);
                }
                InfoBoxUI.TryShow(feedback);
            }
            return success;
        }

        string itemId = ItemIdUtils.GetItemId(item);
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        RequestPlaceItemServerRpc(itemId, position, rotation, createLootContainer, destroyWhenEmpty, allowDropWithoutPrefab, placementColorPacked, usePlacementColor);
        return true;
    }

    public void RefreshControllerFromNetworkState()
    {
        ApplyToController();
    }

    private bool ExecuteUseItem(Item item, out string feedback)
    {
        feedback = string.Empty;
        if (!IsServer || controller == null || item == null)
        {
            return false;
        }

        CombatSessionManager combatManager = CombatSessionManager.Instance;
        if (combatManager != null && !combatManager.CanUseItemNow(controller, out string combatReason))
        {
            feedback = combatReason;
            return false;
        }

        if (controller.TryUseItem(item, out string reason))
        {
            SyncFromController();
            CombatSessionManager.EnsureInstance()?.NotifyInventoryItemUsed(controller);
            feedback = item.GetUseSuccessMessage();
            return true;
        }

        feedback = reason;
        return false;
    }

    private bool ExecuteBreakItem(Item item, out string feedback)
    {
        feedback = string.Empty;
        if (!IsServer || controller == null || item == null)
        {
            return false;
        }

        if (item.TryBreak(controller, out string reason))
        {
            SyncFromController();
            feedback = item.GetBreakSuccessMessage();
            return true;
        }

        feedback = reason;
        return false;
    }

    private bool ExecuteDropItem(Item item, int quantity, Vector3 position, Quaternion rotation, bool allowDropWithoutPrefab, bool destroyWhenEmpty, out string feedback)
    {
        feedback = string.Empty;
        if (!IsServer || controller == null || item == null || quantity <= 0)
        {
            return false;
        }

        if (!item.CanInstantDropFromInventory(controller, allowDropWithoutPrefab, out string reason))
        {
            feedback = reason;
            return false;
        }

        if (!controller.TryRemoveItemQuantity(item, quantity))
        {
            return false;
        }

        SpawnWorldItem(item, quantity, position, rotation, true, destroyWhenEmpty, true, 0u, false);
        SyncFromController();
        feedback = item.GetDropSuccessMessage();
        return true;
    }

    private bool ExecutePlaceItem(
        Item item,
        Vector3 position,
        Quaternion rotation,
        bool createLootContainer,
        bool destroyWhenEmpty,
        bool allowDropWithoutPrefab,
        uint placementColorPacked,
        bool usePlacementColor,
        out string feedback)
    {
        feedback = string.Empty;
        if (!IsServer || controller == null || item == null)
        {
            return false;
        }

        if (!item.CanPlaceFromInventory(controller, out string reason))
        {
            feedback = reason;
            return false;
        }

        if (!controller.TryRemoveItem(item, 1))
        {
            return false;
        }

        SpawnWorldItem(item, 1, position, rotation, createLootContainer, destroyWhenEmpty, true, placementColorPacked, usePlacementColor);
        SyncFromController();
        feedback = item.GetPlaceSuccessMessage();
        return true;
    }

    private void SpawnWorldItem(
        Item item,
        int quantity,
        Vector3 position,
        Quaternion rotation,
        bool createLootContainer,
        bool destroyWhenEmpty,
        bool collectable,
        uint placementColorPacked,
        bool usePlacementColor)
    {
        if (item == null)
        {
            return;
        }

        bool networked = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (!networked)
        {
            GameObject fallbackInstance = item.CreateWorldInstance(position, rotation);
            if (fallbackInstance == null)
            {
                return;
            }

            ApplyPlacementColor(fallbackInstance, placementColorPacked, usePlacementColor);

            if (createLootContainer)
            {
                item.CreateDroppedLootContainer(fallbackInstance, quantity, destroyWhenEmpty, collectable);
            }

            return;
        }

        NetcodePrefabRegistry.EnsureInitialized();
        GameObject instance = NetcodePrefabRegistry.SpawnItemInstance(item, createLootContainer, position, rotation);
        if (instance == null)
        {
            return;
        }

        ApplyPlacementColor(instance, placementColorPacked, usePlacementColor);

        string itemId = ItemIdUtils.GetItemId(item);
        string persistentId;
        if (SpawnManager.Instance != null)
        {
            persistentId = SpawnManager.Instance.AllocatePersistentId(createLootContainer ? "dropped-loot" : "item", itemId);
        }
        else
        {
            persistentId = $"runtime:{(createLootContainer ? "dropped-loot" : "item")}:{itemId}:{System.Guid.NewGuid():N}";
            PersistentWorldDebug.Warn(
                $"spawned runtime {(createLootContainer ? "dropped-loot" : "item")} without SpawnManager allocator itemId='{itemId}' persistentId='{persistentId}'",
                instance);
        }

        InteractableItem loot = instance.GetComponent<InteractableItem>();
        if (createLootContainer)
        {
            if (loot == null)
            {
                loot = instance.GetComponentInChildren<InteractableItem>();
            }

            if (loot != null)
            {
                item.ConfigureDroppedLootContainer(loot, quantity, destroyWhenEmpty, collectable);
            }
        }

        PersistentWorldSceneInstaller.EnsureRuntimeItemInstance(instance, item, persistentId, createLootContainer);

        NetworkObject networkObject = instance.GetComponent<NetworkObject>();
        if (networkObject != null && !networkObject.IsSpawned)
        {
            networkObject.Spawn(true);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestUseItemServerRpc(string itemId, ServerRpcParams rpcParams = default)
    {
        if (!IsRequestFromOwner(rpcParams))
        {
            return;
        }

        Item item = ItemRegistry.Resolve(itemId);
        if (item == null)
        {
            return;
        }

        if (ExecuteUseItem(item, out string feedback))
        {
            ShowFeedbackWithAudioClientRpc(
                feedback,
                ActionAudioCue.InventoryUse,
                BuildClientRpcParams(rpcParams));
            return;
        }

        if (!string.IsNullOrWhiteSpace(feedback))
        {
            ShowFeedbackWithAudioClientRpc(
                feedback,
                ActionAudioCue.UiInvalid,
                BuildClientRpcParams(rpcParams));
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestBreakItemServerRpc(string itemId, ServerRpcParams rpcParams = default)
    {
        if (!IsRequestFromOwner(rpcParams))
        {
            return;
        }

        Item item = ItemRegistry.Resolve(itemId);
        if (item == null)
        {
            return;
        }

        if (ExecuteBreakItem(item, out string feedback))
        {
            ShowFeedbackWithAudioClientRpc(
                feedback,
                ActionAudioCue.InventoryBreak,
                BuildClientRpcParams(rpcParams));
            return;
        }

        if (!string.IsNullOrWhiteSpace(feedback))
        {
            ShowFeedbackWithAudioClientRpc(
                feedback,
                ActionAudioCue.UiInvalid,
                BuildClientRpcParams(rpcParams));
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestDropItemServerRpc(string itemId, int quantity, Vector3 position, Quaternion rotation, bool allowDropWithoutPrefab, bool destroyWhenEmpty, ServerRpcParams rpcParams = default)
    {
        if (!IsRequestFromOwner(rpcParams))
        {
            return;
        }

        Item item = ItemRegistry.Resolve(itemId);
        if (item == null)
        {
            return;
        }

        if (ExecuteDropItem(item, quantity, position, rotation, allowDropWithoutPrefab, destroyWhenEmpty, out string feedback))
        {
            ShowFeedbackWithAudioClientRpc(
                feedback,
                ActionAudioCue.InventoryDrop,
                BuildClientRpcParams(rpcParams));
            return;
        }

        if (!string.IsNullOrWhiteSpace(feedback))
        {
            ShowFeedbackWithAudioClientRpc(
                feedback,
                ActionAudioCue.UiInvalid,
                BuildClientRpcParams(rpcParams));
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlaceItemServerRpc(
        string itemId,
        Vector3 position,
        Quaternion rotation,
        bool createLootContainer,
        bool destroyWhenEmpty,
        bool allowDropWithoutPrefab,
        uint placementColorPacked,
        bool usePlacementColor,
        ServerRpcParams rpcParams = default)
    {
        if (!IsRequestFromOwner(rpcParams))
        {
            return;
        }

        Item item = ItemRegistry.Resolve(itemId);
        if (item == null)
        {
            return;
        }

        if (ExecutePlaceItem(
            item,
            position,
            rotation,
            createLootContainer,
            destroyWhenEmpty,
            allowDropWithoutPrefab,
            placementColorPacked,
            usePlacementColor,
            out string feedback))
        {
            ShowFeedbackWithAudioClientRpc(
                feedback,
                ActionAudioCue.InventoryPlaceConfirm,
                BuildClientRpcParams(rpcParams));
            return;
        }

        if (!string.IsNullOrWhiteSpace(feedback))
        {
            ShowFeedbackWithAudioClientRpc(
                feedback,
                ActionAudioCue.UiInvalid,
                BuildClientRpcParams(rpcParams));
        }
    }

    [ClientRpc]
    private void ShowFeedbackClientRpc(string message, ClientRpcParams rpcParams = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        InfoBoxUI.TryShow(message);
    }

    [ClientRpc]
    private void ShowFeedbackWithAudioClientRpc(
        string message,
        ActionAudioCue audioCue,
        ClientRpcParams rpcParams = default)
    {
        PlayActionAudio(audioCue);

        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        InfoBoxUI.TryShow(message);
    }

    private void PlayActionAudio(ActionAudioCue cue)
    {
        if (cue == ActionAudioCue.None)
        {
            return;
        }

        AudioManager manager = AudioManager.EnsureInstance();
        if (manager != null)
        {
            Vector3 position = controller != null ? controller.transform.position : transform.position;
            manager.PlayActionCue(cue, position);
        }
    }

    private static ClientRpcParams BuildClientRpcParams(ServerRpcParams rpcParams)
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { rpcParams.Receive.SenderClientId }
            }
        };
    }

    private bool IsRequestFromOwner(ServerRpcParams rpcParams)
    {
        return rpcParams.Receive.SenderClientId == OwnerClientId;
    }

    private static void ApplyPlacementColor(GameObject instance, uint packedColor, bool usePlacementColor)
    {
        if (!usePlacementColor || instance == null)
        {
            return;
        }

        BeaconMarker.TrySetColor(instance, UnpackPlacementColor(packedColor));
    }

    private static Color UnpackPlacementColor(uint packedColor)
    {
        byte r = (byte)(packedColor & 0xFFu);
        byte g = (byte)((packedColor >> 8) & 0xFFu);
        byte b = (byte)((packedColor >> 16) & 0xFFu);
        byte a = (byte)((packedColor >> 24) & 0xFFu);
        return new Color32(r, g, b, a);
    }

    private void OnNetItemsChanged(NetworkListEvent<NetItemStack> change)
    {
        ApplyToController();
    }

    private void OnTorchChanged(int previous, int current)
    {
        ApplyToController();
    }

    private void OnTorchChanged(bool previous, bool current)
    {
        ApplyToController();
    }

    private void OnEquippedInteractionItemsChanged(NetworkListEvent<FixedString64Bytes> change)
    {
        ApplyToController();
    }

    private void ApplyToController()
    {
        if (controller == null)
        {
            controller = GetComponent<SquadCharacterController>();
        }

        if (controller == null)
        {
            return;
        }

        List<Item> resolved = new List<Item>();
        List<Item> resolvedEquippedItems = new List<Item>();
        int unresolvedCount = 0;
        List<string> unresolvedItemIds = logInventoryDebug ? new List<string>() : null;
        for (int i = 0; i < netItems.Count; i++)
        {
            NetItemStack stack = netItems[i];
            if (stack.Quantity <= 0)
            {
                continue;
            }

            Item item = ItemRegistry.Resolve(stack.ItemId.ToString());
            if (item == null)
            {
                unresolvedCount++;
                if (unresolvedItemIds != null)
                {
                    unresolvedItemIds.Add(stack.ItemId.ToString());
                }
                continue;
            }

            int count = Mathf.Max(0, stack.Quantity);
            for (int j = 0; j < count; j++)
            {
                resolved.Add(item);
            }
        }

        for (int i = 0; i < netEquippedInteractionItems.Count; i++)
        {
            Item item = ItemRegistry.Resolve(netEquippedInteractionItems[i].ToString());
            if (item == null || resolvedEquippedItems.Contains(item))
            {
                continue;
            }

            resolvedEquippedItems.Add(item);
        }

        controller.ApplyInventoryState(resolved, torchSeconds.Value, torchEquipped.Value, resolvedEquippedItems);
        if (logInventoryDebug)
        {
            Debug.Log($"NetworkInventory: ApplyToController -> netItems={netItems.Count}, equippedItems={resolvedEquippedItems.Count}, resolved={resolved.Count}, unresolved={unresolvedCount}, torchSeconds={torchSeconds.Value}", this);
            if (unresolvedItemIds != null && unresolvedItemIds.Count > 0)
            {
                Debug.LogWarning(
                    $"NetworkInventory: unresolved item IDs for {name}: [{string.Join(", ", unresolvedItemIds)}]. Ces items n'ont pas pu etre reappliques au controller.",
                    this);
            }
        }
        InventoryChanged?.Invoke();
    }

    private void EnsureStarterInventoryIfEmpty()
    {
        if (controller == null)
        {
            controller = GetComponent<SquadCharacterController>();
        }

        if (controller == null || HasSavedCharacterEntry(controller.CharacterData))
        {
            return;
        }

        CharacterData data = controller.CharacterData;
        if (data == null || data.starterItemsWithQuantity == null || data.starterItemsWithQuantity.Count == 0)
        {
            return;
        }

        IReadOnlyList<Item> items = controller.Items;
        if (items != null && items.Count > 0)
        {
            return;
        }

        if (controller.TorchSecondsRemaining > 0 || controller.IsTorchEquipped)
        {
            return;
        }

        controller.ApplyStarterItems(data, true);
        if (logInventoryDebug)
        {
            Debug.Log($"NetworkInventory: EnsureStarterInventoryIfEmpty applied for {data.name} -> items={controller.Items?.Count ?? -1}, torchSeconds={controller.TorchSecondsRemaining}", this);
        }
    }

    private static bool HasSaveFile()
    {
        CharacterStateStore store = CharacterStateStore.Instance;
        if (store != null)
        {
            return store.HasSaveFile;
        }

        SaveSessionManager session = SaveSessionManager.Instance;
        if (session != null && session.HasActiveSave)
        {
            string path = session.GetActiveSaveFilePath("CharacterState.json");
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        }

        return false;
    }

    private static bool HasSavedCharacterEntry(CharacterData character)
    {
        CharacterStateStore store = CharacterStateStore.Instance;
        if (store != null && store.TryGetLoadedCharacterEntry(character, out _))
        {
            return true;
        }

        return false;
    }
}
