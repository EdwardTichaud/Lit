using UnityEngine;

[DisallowMultipleComponent]
public sealed class FallingObstacle : MonoBehaviour
{
    [SerializeField, Range(0.25f, 2f)] private float impactMultiplier = 1f;

    public float ImpactMultiplier => impactMultiplier;
}
