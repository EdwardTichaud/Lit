using UnityEngine;

/// <summary>Marks small scenic geometry that must not reject a combat cinematic trajectory.</summary>
[DisallowMultipleComponent]
public sealed class CinematicPassThrough : MonoBehaviour
{
    [SerializeField, Tooltip("Le marqueur s'applique aussi aux colliders enfants.")]
    private bool includeChildren = true;

    public bool Allows(Collider collider)
    {
        return collider != null && (includeChildren || collider.transform == transform);
    }
}
