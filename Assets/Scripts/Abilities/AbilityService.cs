using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Host-owned abilities. No CharacterData mutation, stat rolls or character-switch exploit.</summary>
public sealed class AbilityService : MonoBehaviour
{
    private const string StateMessage = "lit.abilities.state.v1";
    private const string RequestMessage = "lit.abilities.request.v1";
    public const string SoloPlayerId = "solo";
    public static AbilityService Instance { get; private set; }
    private AbilitySaveData state = new AbilitySaveData();
    private readonly List<AbilitySO> catalog = new List<AbilitySO>();
    private NetworkManager network;
    private bool registered, receivedState;
    private float nextRefresh, nextSyncRequest;
    private string lastPublished;
    public event Action Changed;
    public IReadOnlyList<AbilitySO> Catalog => catalog;
    public bool Authority => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;
    public bool IsReady => Authority || receivedState && !JoinSyncSystem.IsGameplayBlocked;

    [Serializable] private sealed class Request { public string abilityId; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;
    public static AbilityService Ensure()
    {
        if (Instance != null) return Instance;
        return new GameObject("AbilityService").AddComponent<AbilityService>();
    }
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (Application.isPlaying) DontDestroyOnLoad(gameObject);
        foreach (var ability in Resources.LoadAll<AbilitySO>("Abilities"))
        {
            if (ability == null || string.IsNullOrWhiteSpace(ability.abilityId)) continue;
            if (Resolve(ability.abilityId) != null) { Debug.LogError("Duplicate Ability ID: " + ability.abilityId, ability); continue; }
            catalog.Add(ability);
        }
        catalog.Sort((a, b) => string.CompareOrdinal(a.abilityId, b.abilityId));
        RefreshUnlocks();
        if (Application.isPlaying) gameObject.AddComponent<AbilitySelectionPanel>();
    }
    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .25f;
        HookNetwork();
        if (!Authority)
        {
            if (!receivedState && registered && Time.unscaledTime >= nextSyncRequest)
            { nextSyncRequest = Time.unscaledTime + 2f; SendRequest(string.Empty); }
            return;
        }
        RefreshUnlocks();
        PublishIfChanged();
    }
    public AbilitySO Resolve(string id) => catalog.Find(a => a.abilityId == id || a.legacySkillId == id);
    public bool IsUnlocked(AbilitySO ability) => ability != null && state.IsUnlocked(ability.abilityId);
    public AbilitySO Equipped(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId) || !IsReady) return null;
        var equipped = Resolve(state.Equipped(playerId));
        if (IsUnlocked(equipped)) return equipped;
        // A newly joined player starts with the first available starting ability, never a second slot.
        return catalog.Find(a => a.availableAtStart && IsUnlocked(a));
    }
    public string LocalPlayerId => PlayerIdForClient(NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening
        ? NetworkManager.Singleton.LocalClientId : 0);
    public static string PlayerIdForClient(ulong clientId)
    {
        var manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsListening) return SoloPlayerId;
        // No transient clientId fallback: it could transfer a previous player's loadout on reconnect.
        if (NetcodePlayerSessionRegistry.TryGetPlayerId(clientId, out string id)) return id;
        return !manager.IsServer && clientId == manager.LocalClientId ? NetcodeClientIdentity.GetOrCreatePlayerId() : null;
    }
    public bool HasEquipped(GameObject character, AbilitySO ability)
    {
        if (character == null || ability == null || !IsReady) return false;
        string playerId;
        var manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsListening)
        {
            var local = LocalPlayerUtils.GetControlledCharacter();
            if (local == null || !(character.transform == local.transform || character.transform.IsChildOf(local.transform))) return false;
            playerId = SoloPlayerId;
        }
        else
        {
            var control = NetcodePlayerUtils.ResolveCharacterControlState(character);
            if (!control.IsPlayerControlled) return false;
            playerId = PlayerIdForClient(control.HasAssignedClientId ? control.AssignedClientId : control.OwnerClientId);
        }
        return Equipped(playerId) == ability;
    }
    public bool IsInMaison()
    {
        var flow = GameFlowService.Instance;
        if (flow != null && (flow.IsTransitioning || GameFlowService.IsPreparingGameplayScene)) return false;
        string scene = flow != null && flow.HasGameplaySession ? GameFlowService.InitialGameplaySceneName
            : SceneManager.GetActiveScene().name;
        string home = flow != null ? flow.HubSceneName : "Maison_Core";
        return scene == home || scene == "Maison" || scene == "Maison_Core";
    }
    public bool RequestEquip(AbilitySO ability)
    {
        if (ability == null || !IsReady || !IsInMaison()) return false;
        if (!Authority)
        {
            if (!registered) return false;
            SendRequest(ability.abilityId);
            return true;
        }
        return TryEquipForPlayer(LocalPlayerId, ability, LocalPlayerUtils.GetControlledCharacter());
    }
    private bool TryEquipForPlayer(string playerId, AbilitySO ability, GameObject character)
    {
        if (!Authority || !IsInMaison() || character == null || !IsUnlocked(ability)) return false;
        var controller = character.GetComponent<SquadCharacterController>();
        var movement = character.GetComponent<LitOpsiveLocomotionBridge>();
        if (controller == null || controller.CurrentHp <= 0 || movement != null && (movement.IsCombatLockActive || movement.IsExternalLockActive)) return false;
        if (!state.Equip(playerId, ability.abilityId)) return false;
        NotifyChanged();
        return true;
    }
    /// <summary>Trusted host gameplay/event API; deliberately no client unlock RPC.</summary>
    public bool Unlock(AbilitySO ability, bool notify = true)
    {
        if (!Authority || ability == null || Resolve(ability.abilityId) != ability || !state.Unlock(ability.abilityId)) return false;
        if (notify) SkillUnlockPanel.TryShow(ability);
        NotifyChanged();
        return true;
    }
    public void RefreshUnlocks()
    {
        if (!Authority) return;
        foreach (var ability in catalog)
            if (!IsUnlocked(ability) && ability.CanUnlockFromProgression()) Unlock(ability, !ability.availableAtStart);
    }
    public AbilitySaveData Capture() => JsonUtility.FromJson<AbilitySaveData>(JsonUtility.ToJson(state));
    public void Restore(AbilitySaveData data)
    {
        state = new AbilitySaveData();
        if (data != null)
        {
            foreach (var id in data.unlockedIds ?? new List<string>())
            { var a = Resolve(id); if (a != null) state.Unlock(a.abilityId); }
            foreach (var slot in data.slots ?? new List<AbilitySlot>())
                if (slot != null && !string.IsNullOrWhiteSpace(slot.playerId)) state.Equip(slot.playerId, slot.abilityId);
        }
        if (Authority) RefreshUnlocks();
        NotifyChanged();
    }
    public void RestoreCharacterSave(CharacterSaveData data)
    {
        if (data == null) return;
        if (data.abilities != null) { Restore(data.abilities); return; }
        // v9 character lists were often innate defaults (including Sauf-Conduit).
        // They cannot bypass the new progression gates or create extra equipment slots.
        var migration = new AbilitySaveData();
        foreach (var character in data.characters ?? new List<CharacterSaveEntry>())
            foreach (var oldId in character?.skillIds ?? new List<string>())
            { var ability = Resolve(oldId); if (ability != null && ability.CanUnlockFromProgression()) migration.Unlock(ability.abilityId); }
        Restore(migration);
    }
    private void NotifyChanged() { Changed?.Invoke(); PublishIfChanged(); }
    private void HookNetwork()
    {
        var current = NetworkManager.Singleton;
        if (current == network && registered && current != null && current.IsListening) return;
        UnhookNetwork();
        if (current == null || !current.IsListening || current.CustomMessagingManager == null) return;
        network = current;
        network.CustomMessagingManager.RegisterNamedMessageHandler(StateMessage, ReceiveState);
        network.CustomMessagingManager.RegisterNamedMessageHandler(RequestMessage, ReceiveRequest);
        network.OnClientConnectedCallback += SendState;
        registered = true;
        receivedState = false;
        lastPublished = null;
        nextSyncRequest = 0f;
    }
    private void UnhookNetwork()
    {
        if (network != null)
        {
            network.OnClientConnectedCallback -= SendState;
            if (registered && network.CustomMessagingManager != null)
            {
                network.CustomMessagingManager.UnregisterNamedMessageHandler(StateMessage);
                network.CustomMessagingManager.UnregisterNamedMessageHandler(RequestMessage);
            }
        }
        network = null; registered = false; receivedState = false;
    }
    private void PublishIfChanged()
    {
        if (!Authority || !registered || network == null || !network.IsServer) return;
        string json = JsonUtility.ToJson(state);
        if (json == lastPublished) return;
        lastPublished = json;
        foreach (ulong id in network.ConnectedClientsIds) if (id != NetworkManager.ServerClientId) SendState(id);
    }
    private void SendState(ulong clientId)
    {
        if (registered && network.IsServer && clientId != NetworkManager.ServerClientId)
            SendJson(StateMessage, clientId, JsonUtility.ToJson(state));
    }
    private void SendRequest(string id) => SendJson(RequestMessage, NetworkManager.ServerClientId, JsonUtility.ToJson(new Request { abilityId = id }));
    private void SendJson(string message, ulong target, string json)
    {
        using (var writer = new FastBufferWriter(System.Text.Encoding.UTF8.GetMaxByteCount(json.Length) + 8, Allocator.Temp))
        {
            writer.WriteValueSafe(json);
            network.CustomMessagingManager.SendNamedMessage(message, target, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }
    }
    private void ReceiveState(ulong sender, FastBufferReader reader)
    {
        if (Authority || sender != NetworkManager.ServerClientId || reader.Length > 128 * 1024) return;
        try
        {
            reader.ReadValueSafe(out string json);
            var previouslyUnlocked = new HashSet<string>(state.unlockedIds);
            bool notifyUnlock = receivedState;
            Restore(JsonUtility.FromJson<AbilitySaveData>(json));
            receivedState = true;
            Changed?.Invoke();
            if (notifyUnlock) foreach (var ability in catalog)
                if (IsUnlocked(ability) && !previouslyUnlocked.Contains(ability.abilityId)) SkillUnlockPanel.TryShow(ability);
        }
        catch (Exception exception) { Debug.LogWarning("[Abilities] Invalid state: " + exception.Message); }
    }
    private void ReceiveRequest(ulong sender, FastBufferReader reader)
    {
        if (!Authority || reader.Length > 512) return;
        try
        {
            reader.ReadValueSafe(out string json);
            var request = JsonUtility.FromJson<Request>(json);
            if (request == null) return;
            if (string.IsNullOrEmpty(request.abilityId)) { SendState(sender); return; }
            var player = NetcodePlayerUtils.GetPlayerTransform(sender);
            TryEquipForPlayer(PlayerIdForClient(sender), Resolve(request.abilityId), player != null ? player.gameObject : null);
            SendState(sender);
        }
        catch (Exception exception) { Debug.LogWarning("[Abilities] Invalid request: " + exception.Message); }
    }
    public void Shutdown()
    {
        UnhookNetwork();
        if (Instance == this) Instance = null;
        Destroy(gameObject);
    }
    private void OnDestroy() { UnhookNetwork(); if (Instance == this) Instance = null; }
}
