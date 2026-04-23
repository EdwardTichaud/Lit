using System.Collections.Generic;
using UnityEngine;

public static class CombatArenaInstance
{
    private static readonly Dictionary<string, GameObject> arenas = new Dictionary<string, GameObject>();

    public static GameObject CreateOrReplace(string sessionId, Vector3 center, float size)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            sessionId = "local";
        }

        Destroy(sessionId);

        float resolvedSize = Mathf.Max(4f, size);
        GameObject root = new GameObject($"CombatArena_{sessionId}");
        Object.DontDestroyOnLoad(root);
        root.transform.position = center;

        CreateCube(root.transform, "Floor", center + Vector3.down * 0.06f, new Vector3(resolvedSize, 0.12f, resolvedSize), new Color(0.13f, 0.13f, 0.14f, 1f));

        float half = resolvedSize * 0.5f;
        float wallHeight = 1.4f;
        float wallThickness = 0.18f;
        CreateCube(root.transform, "Wall_North", center + new Vector3(0f, wallHeight * 0.5f, half), new Vector3(resolvedSize, wallHeight, wallThickness), new Color(0.08f, 0.08f, 0.09f, 1f));
        CreateCube(root.transform, "Wall_South", center + new Vector3(0f, wallHeight * 0.5f, -half), new Vector3(resolvedSize, wallHeight, wallThickness), new Color(0.08f, 0.08f, 0.09f, 1f));
        CreateCube(root.transform, "Wall_East", center + new Vector3(half, wallHeight * 0.5f, 0f), new Vector3(wallThickness, wallHeight, resolvedSize), new Color(0.08f, 0.08f, 0.09f, 1f));
        CreateCube(root.transform, "Wall_West", center + new Vector3(-half, wallHeight * 0.5f, 0f), new Vector3(wallThickness, wallHeight, resolvedSize), new Color(0.08f, 0.08f, 0.09f, 1f));

        arenas[sessionId] = root;
        return root;
    }

    public static void Destroy(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            sessionId = "local";
        }

        if (!arenas.TryGetValue(sessionId, out GameObject arena) || arena == null)
        {
            arenas.Remove(sessionId);
            return;
        }

        Object.Destroy(arena);
        arenas.Remove(sessionId);
    }

    private static void CreateCube(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, true);
        cube.transform.position = position;
        cube.transform.localScale = scale;

        Renderer renderer = cube.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
        }
    }
}
