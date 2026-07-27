using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FallingObstacleSpawner : MonoBehaviour
{
    [SerializeField] private FallingPlayerController player;
    [SerializeField] private GameObject[] obstaclePrefabs;
    [SerializeField, Min(20f)] private float spawnAheadDistance = 170f;
    [SerializeField, Min(5f)] private float despawnBehindDistance = 35f;
    [SerializeField, Min(2f)] private float minSpacing = 9f;
    [SerializeField, Min(2f)] private float maxSpacing = 18f;
    [SerializeField] private Vector2 horizontalRange = new Vector2(-12f, 12f);
    [SerializeField] private Vector2 verticalRange = new Vector2(-7f, 7f);

    [Header("Grapple Obstacles")]
    [SerializeField, Range(0f, 1f)] private float grappleObstacleChance = 0.1f;
    [SerializeField] private Material grappleMaterial;

    private readonly List<GameObject> activeObstacles = new List<GameObject>();
    private float nextSpawnZ;

    private void Start()
    {
        nextSpawnZ = player != null ? player.transform.position.z + spawnAheadDistance : spawnAheadDistance;
    }

    private void Update()
    {
        if (player == null || obstaclePrefabs == null || obstaclePrefabs.Length == 0)
        {
            return;
        }

        float playerZ = player.transform.position.z;
        while (nextSpawnZ < playerZ + spawnAheadDistance)
        {
            SpawnObstacle(nextSpawnZ);
            nextSpawnZ += Random.Range(minSpacing, maxSpacing);
        }

        for (int i = activeObstacles.Count - 1; i >= 0; i--)
        {
            GameObject obstacle = activeObstacles[i];
            if (obstacle == null || obstacle.transform.position.z < playerZ - despawnBehindDistance)
            {
                if (obstacle != null)
                {
                    Destroy(obstacle);
                }

                activeObstacles.RemoveAt(i);
            }
        }

    }

    private void SpawnObstacle(float zPosition)
    {
        GameObject prefab = obstaclePrefabs[Random.Range(0, obstaclePrefabs.Length)];
        if (prefab == null)
        {
            return;
        }

        Vector3 position = new Vector3(
            Random.Range(horizontalRange.x, horizontalRange.y),
            Random.Range(verticalRange.x, verticalRange.y),
            zPosition);
        GameObject instance = Instantiate(prefab, position, Random.rotation, transform);
        if (Random.value <= grappleObstacleChance)
        {
            ConfigureAsGrapple(instance);
        }

        activeObstacles.Add(instance);
    }

    private void ConfigureAsGrapple(GameObject obstacle)
    {
        if (obstacle == null)
        {
            return;
        }

        Renderer[] renderers = obstacle.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].sharedMaterial = grappleMaterial;
        }

        Light glow = obstacle.GetComponent<Light>();
        if (glow == null)
        {
            glow = obstacle.AddComponent<Light>();
        }

        glow.type = LightType.Point;
        glow.color = new Color(0.38f, 0.9f, 1f);
        glow.range = 5f;
        glow.intensity = 0f;

        if (obstacle.GetComponent<FallingGrapplePoint>() == null)
        {
            obstacle.AddComponent<FallingGrapplePoint>();
        }
    }
}
