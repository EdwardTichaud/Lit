using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider)), DisallowMultipleComponent]
public sealed class LitTacticalBounds : MonoBehaviour
{
    private static readonly List<LitTacticalBounds> active = new List<LitTacticalBounds>();
    private BoxCollider volume;
    private void OnEnable() { volume = GetComponent<BoxCollider>(); active.Add(this); }
    private void OnDisable() { active.Remove(this); }
    private void Reset() { GetComponent<BoxCollider>().isTrigger = true; }
    public static Vector3 Constrain(Vector3 point, Vector3 player)
    {
        // A volume applies only to the character inside it. No scene-wide singleton.
        foreach (var item in active)
        {
            Vector3 localPlayer = item.transform.InverseTransformPoint(player) - item.volume.center;
            Vector3 half = item.volume.size * .5f;
            if (Mathf.Abs(localPlayer.x) > half.x || Mathf.Abs(localPlayer.y) > half.y || Mathf.Abs(localPlayer.z) > half.z) continue;
            Vector3 local = item.transform.InverseTransformPoint(point) - item.volume.center;
            local.x = Mathf.Clamp(local.x, -half.x, half.x);
            local.z = Mathf.Clamp(local.z, -half.z, half.z);
            return item.transform.TransformPoint(local + item.volume.center);
        }
        return point;
    }
    private void OnDrawGizmosSelected()
    {
        var box = GetComponent<BoxCollider>();
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(box.center, box.size);
    }
}
