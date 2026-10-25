using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>Zone-local fall safety. The host observes positions and chooses
/// authored safe landings; clients cannot submit arbitrary teleport poses.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(NetworkObject))]
public sealed class LimbesFallRecovery : NetworkBehaviour
{
    [Serializable] public sealed class SafeLanding
    {
        public string id;
        public Transform arrival;
        public BoxCollider volume;
    }
    public SafeLanding[] landings = Array.Empty<SafeLanding>();
    public BoxCollider participationVolume;
    public float voidHeight = 95f;
    [Min(.1f)] public float recoveryCooldown = 1f;
    private readonly Dictionary<ulong, int> visited = new();
    private readonly Dictionary<ulong, float> nextRecovery = new();
    private readonly HashSet<ulong> participants = new();
    private float nextObserve;
    private bool Online => NetworkManager != null && NetworkManager.IsListening;

    private void Update()
    {
        if (Time.unscaledTime < nextObserve || landings.Length == 0 ||
            GameFlowService.Instance == null || GameFlowService.Instance.IsTransitioning ||
            !GameFlowService.Instance.HasGameplaySession) return;
        nextObserve = Time.unscaledTime + .1f;
        if (Online)
        {
            if (!IsSpawned || !IsServer) return;
            foreach (ulong client in NetworkManager.ConnectedClientsIds) Observe(client, NetcodePlayerUtils.GetPlayerTransform(client));
        }
        else Observe(0, LocalPlayerContext.LocalCharacterRoot);
    }

    private void Observe(ulong client, Transform root)
    {
        var actor = root != null ? root.GetComponent<SquadCharacterController>() : null;
        if (actor == null || actor.CurrentHp <= 0 || !Finite(root.position)) return;
        // A late joiner still being prepared in Maison is not a falling
        // participant of this zone merely because their altitude is lower.
        if (!participants.Contains(client))
        {
            if (!Contains(participationVolume, root.position)) return;
            participants.Add(client);
        }
        for (int i = 0; i < landings.Length; i++)
            if (Contains(landings[i]?.volume, root.position + Vector3.up * .4f) && HasFloor(root.position)) visited[client] = i;
        if (root.position.y >= voidHeight || nextRecovery.TryGetValue(client, out float next) && Time.unscaledTime < next) return;
        nextRecovery[client] = Time.unscaledTime + recoveryCooldown;
        int index = visited.TryGetValue(client, out int saved) ? saved : 0;
        int slot = Online && SquadManager.Instance != null ? SquadManager.Instance.GetPlayerSpawnIndex(actor.gameObject) : -1;
        if (!TryGetArrival(index, slot, out Pose pose)) return;
        bool local = !Online || client == NetworkManager.LocalClientId;
        if (local) Apply(actor, pose);
        else
        {
            // Same server + owner application as existing portal travel.
            actor.TrySetUccExternalPositionAndRotation(pose.position, pose.rotation, true);
            RecoverOwnerClientRpc(pose.position, pose.rotation,
                new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { client } } });
        }
    }

    private bool TryGetArrival(int index, int slot, out Pose pose)
    {
        pose = default;
        if (index < 0 || index >= landings.Length || landings[index]?.arrival == null) return false;
        Transform anchor = landings[index].arrival;
        Vector3 position = anchor.TransformPoint(slot >= 0 ? new Vector3((slot - 1.5f) * .9f, 0, 0) : Vector3.zero);
        if (!Finite(position) || !HasFloor(position)) return false;
        pose = new Pose(position, anchor.rotation);
        return true;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer) NetworkManager.OnClientDisconnectCallback += Forget;
    }
    public override void OnNetworkDespawn()
    {
        if (NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= Forget;
        participants.Clear(); visited.Clear(); nextRecovery.Clear();
    }
    private void Forget(ulong client) { participants.Remove(client); visited.Remove(client); nextRecovery.Remove(client); }

    private static bool HasFloor(Vector3 point)
    {
        foreach (var hit in Physics.RaycastAll(point + Vector3.up, Vector3.down, 2.5f, ~0, QueryTriggerInteraction.Ignore))
            if (hit.normal.y > .6f && hit.collider.GetComponentInParent<SquadCharacterController>() == null) return true;
        return false;
    }

    [ClientRpc]
    private void RecoverOwnerClientRpc(Vector3 position, Quaternion rotation, ClientRpcParams targets = default)
    {
        var root = LocalPlayerContext.LocalCharacterRoot;
        var actor = root != null ? root.GetComponent<SquadCharacterController>() : null;
        if (actor != null) Apply(actor, new Pose(position, rotation));
    }

    private static void Apply(SquadCharacterController actor, Pose pose)
    {
        if (actor.TrySetUccExternalPositionAndRotation(pose.position, pose.rotation, true))
        {
            Physics.SyncTransforms();
            LocalInputRouter.RaiseCameraRecenter();
        }
    }

    public static bool Contains(BoxCollider volume, Vector3 point)
    {
        if (volume == null || !volume.enabled || !volume.gameObject.activeInHierarchy || !Finite(point)) return false;
        Vector3 p = volume.transform.InverseTransformPoint(point) - volume.center;
        Vector3 half = volume.size * .5f;
        return Mathf.Abs(p.x) <= Mathf.Abs(half.x) && Mathf.Abs(p.y) <= Mathf.Abs(half.y) && Mathf.Abs(p.z) <= Mathf.Abs(half.z);
    }
    private static bool Finite(Vector3 point) => !float.IsNaN(point.x) && !float.IsNaN(point.y) && !float.IsNaN(point.z) &&
        !float.IsInfinity(point.x) && !float.IsInfinity(point.y) && !float.IsInfinity(point.z);

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(.3f, .8f, 1f, .8f);
        foreach (var landing in landings)
        {
            if (landing?.arrival == null) continue;
            Gizmos.DrawWireSphere(landing.arrival.position, .5f);
            if (landing.volume != null) { Gizmos.matrix = landing.volume.transform.localToWorldMatrix; Gizmos.DrawWireCube(landing.volume.center, landing.volume.size); Gizmos.matrix = Matrix4x4.identity; }
        }
    }
}
