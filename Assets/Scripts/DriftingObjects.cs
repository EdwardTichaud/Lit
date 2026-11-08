using System.Collections.Generic;
using UnityEngine;

public class DriftingObjects : MonoBehaviour
{
    [Header("Speeds")]
    public float moveSpeed = 0.5f;
    public float rotationSpeed = 15f;

    [Header("Direction Changes")]
    public float directionChangeSpeed = 0.5f;

    private class DriftingChild
    {
        public Transform transform;
        public float moveSeed;
        public float rotationSeed;
    }

    private readonly List<DriftingChild> children = new List<DriftingChild>();

    void Start()
    {
        // Récupère uniquement les enfants directs
        foreach (Transform child in transform)
        {
            children.Add(new DriftingChild
            {
                transform = child,
                moveSeed = Random.Range(0f, 10000f),
                rotationSeed = Random.Range(0f, 10000f)
            });
        }
    }

    void Update()
    {
        float deltaTime = Time.deltaTime;
        float noiseTime = Time.time * directionChangeSpeed;

        foreach (DriftingChild child in children)
        {
            if (child.transform == null)
                continue;

            // Direction de déplacement aléatoire et fluide
            Vector3 moveDirection = new Vector3(
                Mathf.PerlinNoise(child.moveSeed, noiseTime) * 2f - 1f,
                Mathf.PerlinNoise(child.moveSeed + 10f, noiseTime) * 2f - 1f,
                Mathf.PerlinNoise(child.moveSeed + 20f, noiseTime) * 2f - 1f
            );

            // Déplacement continu
            if (moveDirection.sqrMagnitude > 0.0001f)
            {
                child.transform.localPosition +=
                    moveDirection.normalized * moveSpeed * deltaTime;
            }

            // Direction de rotation aléatoire et fluide
            Vector3 rotationDirection = new Vector3(
                Mathf.PerlinNoise(child.rotationSeed, noiseTime) * 2f - 1f,
                Mathf.PerlinNoise(child.rotationSeed + 10f, noiseTime) * 2f - 1f,
                Mathf.PerlinNoise(child.rotationSeed + 20f, noiseTime) * 2f - 1f
            );

            // Rotation autour des axes locaux de l'objet
            if (rotationDirection.sqrMagnitude > 0.0001f)
            {
                child.transform.Rotate(
                    rotationDirection.normalized *
                    rotationSpeed * deltaTime,
                    Space.Self
                );
            }
        }
    }
}