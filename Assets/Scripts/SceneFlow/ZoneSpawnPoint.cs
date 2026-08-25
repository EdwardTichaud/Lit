using UnityEngine;
using System.Collections.Generic;

/// <summary>Point d'arrivee stable, reference par les portails plutot que par un Transform d'une autre scene.</summary>
[DisallowMultipleComponent]
public sealed class ZoneSpawnPoint : MonoBehaviour
{
    [SerializeField] private string spawnId = "Default";
    [SerializeField, Tooltip("Ordre dans un groupe de spawns. Utiliser 0 a 3 pour une arrivee coop a quatre.")]
    private int partySlot;
    public string SpawnId => spawnId;
    public int PartySlot => partySlot;

    public static ZoneSpawnPoint Find(string requestedSpawnId)
    {
        IReadOnlyList<ZoneSpawnPoint> points = FindAll(requestedSpawnId);
        return points.Count > 0 ? points[0] : null;
    }

    /// <summary>Retourne tous les points d'un meme identifiant, ordonnes par slot coop.</summary>
    public static IReadOnlyList<ZoneSpawnPoint> FindAll(string requestedSpawnId)
    {
        List<ZoneSpawnPoint> results = new List<ZoneSpawnPoint>();
        if (string.IsNullOrWhiteSpace(requestedSpawnId))
        {
            return results;
        }

        ZoneSpawnPoint[] points = FindObjectsByType<ZoneSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < points.Length; i++)
        {
            ZoneSpawnPoint point = points[i];
            if (point != null && string.Equals(point.spawnId, requestedSpawnId, System.StringComparison.OrdinalIgnoreCase))
            {
                results.Add(point);
            }
        }

        results.Sort((left, right) => left.partySlot.CompareTo(right.partySlot));
        return results;
    }

    /// <summary>Retourne le premier point de spawn appartenant a une scene precise.</summary>
    public static ZoneSpawnPoint FindFirstInScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return null;
        }

        ZoneSpawnPoint[] points = FindObjectsByType<ZoneSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < points.Length; i++)
        {
            ZoneSpawnPoint point = points[i];
            if (point != null && string.Equals(point.gameObject.scene.name, sceneName, System.StringComparison.OrdinalIgnoreCase))
            {
                return point;
            }
        }

        return null;
    }
}
