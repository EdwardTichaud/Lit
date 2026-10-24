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
        if (player == null || !ContainsWorldPoint(player.position + Vector3.up * .5f)) return;
        SquadCharacterController character = player.GetComponentInChildren<SquadCharacterController>();
        if (character == null) character = player.GetComponentInParent<SquadCharacterController>();
        if (character != null && character.CurrentHp > 0) cycle.TryReportSceneEvent(CycleStepKind.ZoneEntered, sourceId);
    }
    /// <summary>Tests the actual oriented trigger, not its world-space enclosing bounds.</summary>
    public bool ContainsWorldPoint(Vector3 worldPoint)
    {
        if (volume == null) volume = GetComponent<BoxCollider>();
        if (!isActiveAndEnabled || volume == null || !volume.enabled || !IsFinite(worldPoint) ||
            !IsFinite(volume.center) || !IsFinite(volume.size)) return false;
        Vector3 scale = transform.lossyScale;
        if (!IsFinite(scale) || Mathf.Abs(scale.x) <= Mathf.Epsilon ||
            Mathf.Abs(scale.y) <= Mathf.Epsilon || Mathf.Abs(scale.z) <= Mathf.Epsilon) return false;
        Vector3 local = transform.InverseTransformPoint(worldPoint) - volume.center;
        Vector3 half = volume.size * .5f;
        return IsFinite(local) && Mathf.Abs(local.x) <= Mathf.Abs(half.x) &&
            Mathf.Abs(local.y) <= Mathf.Abs(half.y) && Mathf.Abs(local.z) <= Mathf.Abs(half.z);
    }
    private static bool IsFinite(Vector3 value) =>
        !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
        !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
        !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    private void OnDrawGizmosSelected()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return;
        Gizmos.color = new Color(.35f, .8f, 1f, .6f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(box.center, box.size);
    }
}
