using Unity.Netcode;
using UnityEngine;

/// <summary>Host observes real player positions; no client can claim to have entered a zone.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
public sealed class CycleZoneTrigger : MonoBehaviour
{
    public CycleController cycle;
    public string sourceId = "puits_entry";
    private BoxCollider volume;
    private float nextCheck;

    private void Awake() { volume = GetComponent<BoxCollider>(); volume.isTrigger = true; }
    private void Update()
    {
        if (Time.unscaledTime < nextCheck || cycle == null) return;
        nextCheck = Time.unscaledTime + .15f;
        if (!cycle.IsSceneEventActive(CycleStepKind.ZoneEntered, sourceId)) return;
        NetworkManager network = NetworkManager.Singleton;
        if (network != null && network.IsListening)
        {
            if (!network.IsServer) return;
            foreach (ulong id in network.ConnectedClientsIds) Observe(NetcodePlayerUtils.GetPlayerTransform(id));
        }
        else Observe(LocalPlayerContext.LocalCharacterRoot);
    }
    private void Observe(Transform player)
    {
        if (player == null || !volume.bounds.Contains(player.position + Vector3.up * .5f)) return;
        SquadCharacterController character = player.GetComponentInChildren<SquadCharacterController>();
        if (character == null) character = player.GetComponentInParent<SquadCharacterController>();
        if (character != null && character.CurrentHp > 0) cycle.TryReportSceneEvent(CycleStepKind.ZoneEntered, sourceId);
    }
    private void OnDrawGizmosSelected()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return;
        Gizmos.color = new Color(.35f, .8f, 1f, .6f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(box.center, box.size);
    }
}
