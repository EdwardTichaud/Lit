using System.Collections.Generic;
using System.IO;
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

        if (logInventoryDebug)
        {
            Debug.Log($"NetworkInventory: SyncFromController -> netItems={netItems.Count}, torchSeconds={torchSeconds.Value}, torchEquipped={torchEquipped.Value}", this);
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

            if (controller.TryUseItem(item, out string reason))
            {
                InfoBoxUI.TryShow(item.GetUseSuccessMessage());
                return true;
            }

            InfoBoxUI.TryShow(reason);
            return false;
        }

        if (IsServer)
        {
            bool success = ExecuteUseItem(item, out string feedback);
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
                InfoBoxUI.TryShow(item.GetBreakSuccessMessage());
                return true;
            }

            InfoBoxUI.TryShow(reason);
            return false;
        }

        if (IsServer)
        {
            bool success = ExecuteBreakItem(item, out string feedback);
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

            SpawnWorldItem(item, quantity, position, rotation, true, destroyWhenEmpty, true);
            SyncFromController();
            InfoBoxUI.TryShow(item.GetDropSuccessMessage());
            return true;
        }

        if (IsServer)
        {
            bool success = ExecuteDropItem(item, quantity, position, rotation, allowDropWithoutPrefab, destroyWhenEmpty, out string feedback);
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

    public bool RequestPlaceItem(Item item, Vector3 position, Quaternion rotation, bool createLootContainer, bool destroyWhenEmpty, bool allowDropWithoutPrefab)
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

            SpawnWorldItem(item, 1, position, rotation, createLootContainer, destroyWhenEmpty, true);
            SyncFromController();
            InfoBoxUI.TryShow(item.GetPlaceSuccessMessage());
            return true;
        }

        if (IsServer)
        {
            bool success = ExecutePlaceItem(item, position, rotation, createLootContainer, destroyWhenEmpty, allowDropWithoutPrefab, out string feedback);
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

        RequestPlaceItemServerRpc(itemId, position, rotation, createLootContainer, destroyWhenEmpty, allowDropWithoutPrefab);
        return true;
    }

    private bool ExecuteUseItem(Item item, out string feedback)
    {
        feedback = string.Empty;
        if (!IsServer || controller == null || item == null)
        {
            return false;
        }

        if (controller.TryUseItem(item, out string reason))
        {
            SyncFromController();
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

        SpawnWorldItem(item, quantity, position, rotation, true, destroyWhenEmpty, true);
        SyncFromController();
        feedback = item.GetDropSuccessMessage();
        return true;
    }

    private bool ExecutePlaceItem(Item item, Vector3 position, Quaternion rotation, bool createLootContainer, bool destroyWhenEmpty, bool allowDropWithoutPrefab, out string feedback)
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

        SpawnWorldItem(item, 1, position, rotation, createLootContainer, destroyWhenEmpty, true);
        SyncFromController();
        feedback = item.GetPlaceSuccessMessage();
        return true;
    }

    private void SpawnWorldItem(Item item, int quantity, Vector3 position, Quaternion rotation, bool createLootContainer, bool destroyWhenEmpty, bool collectable)
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

        LootContainer loot = instance.GetComponent<LootContainer>();
        if (createLootContainer)
        {
            if (loot == null)
            {
                loot = instance.GetComponentInChildren<LootContainer>();
            }

            if (loot != null)
            {
                loot.lootItems = new List<LootContainer.LootItemEntry>
                {
                    new LootContainer.LootItemEntry { item = item, quantity = Mathf.Max(1, quantity) }
                };
                loot.containerItem = item;
                loot.destroyWhenEmpty = destroyWhenEmpty;
                loot.collectable = collectable;
            }
        }

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
            ShowFeedbackClientRpc(feedback, BuildClientRpcParams(rpcParams));
            return;
        }

        if (!string.IsNullOrWhiteSpace(feedback))
        {
            ShowFeedbackClientRpc(feedback, BuildClientRpcParams(rpcParams));
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
            ShowFeedbackClientRpc(feedback, BuildClientRpcParams(rpcParams));
            return;
        }

        if (!string.IsNullOrWhiteSpace(feedback))
        {
            ShowFeedbackClientRpc(feedback, BuildClientRpcParams(rpcParams));
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
            ShowFeedbackClientRpc(feedback, BuildClientRpcParams(rpcParams));
            return;
        }

        if (!string.IsNullOrWhiteSpace(feedback))
        {
            ShowFeedbackClientRpc(feedback, BuildClientRpcParams(rpcParams));
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlaceItemServerRpc(string itemId, Vector3 position, Quaternion rotation, bool createLootContainer, bool destroyWhenEmpty, bool allowDropWithoutPrefab, ServerRpcParams rpcParams = default)
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

        if (ExecutePlaceItem(item, position, rotation, createLootContainer, destroyWhenEmpty, allowDropWithoutPrefab, out string feedback))
        {
            ShowFeedbackClientRpc(feedback, BuildClientRpcParams(rpcParams));
            return;
        }

        if (!string.IsNullOrWhiteSpace(feedback))
        {
            ShowFeedbackClientRpc(feedback, BuildClientRpcParams(rpcParams));
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
        int unresolvedCount = 0;
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
                continue;
            }

            int count = Mathf.Max(0, stack.Quantity);
            for (int j = 0; j < count; j++)
            {
                resolved.Add(item);
            }
        }

        controller.ApplyInventoryState(resolved, torchSeconds.Value, torchEquipped.Value);
        if (logInventoryDebug)
        {
            Debug.Log($"NetworkInventory: ApplyToController -> netItems={netItems.Count}, resolved={resolved.Count}, unresolved={unresolvedCount}, torchSeconds={torchSeconds.Value}", this);
        }
        InventoryChanged?.Invoke();
    }

    private void EnsureStarterInventoryIfEmpty()
    {
        if (controller == null)
        {
            controller = GetComponent<SquadCharacterController>();
        }

        if (controller == null || HasSaveFile())
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
}
