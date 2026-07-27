using UnityEngine;

/// <summary>Point d'arrivee stable, reference par les portails plutot que par un Transform d'une autre scene.</summary>
[DisallowMultipleComponent]
public sealed class ZoneSpawnPoint : MonoBehaviour
{
    [SerializeField] private string spawnId = "Default";
    public string SpawnId => spawnId;

    public static ZoneSpawnPoint Find(string requestedSpawnId)
    {
        ZoneSpawnPoint[] points = FindObjectsByType<ZoneSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] != null && string.Equals(points[i].spawnId, requestedSpawnId, System.StringComparison.OrdinalIgnoreCase))
            {
                return points[i];
            }
        }

        return null;
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
