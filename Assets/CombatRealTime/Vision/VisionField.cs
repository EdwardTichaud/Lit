using UnityEngine;

[DisallowMultipleComponent]
public sealed class VisionField : MonoBehaviour
{
    [SerializeField] private Transform origin;
    [SerializeField, Min(0.1f)] private float maximumDistance = 6f;
    [SerializeField, Range(1f, 360f)] private float fieldOfViewDegrees = 110f;
    [SerializeField] private float eyeHeight = 1.35f;
    [SerializeField] private float targetHeight = 1f;
    [SerializeField] private LayerMask obstructionMask = ~0;

    public float MaximumDistance => maximumDistance;

    /// <summary>
    /// Permet a un comportement d'alerte de modifier temporairement la portee
    /// sans dupliquer les controles de champ de vision et d'obstruction.
    /// </summary>
    public void SetMaximumDistance(float distance)
    {
        maximumDistance = Mathf.Max(0.1f, distance);
    }

    public bool CanSee(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            return false;
        }

        Transform source = origin != null ? origin : transform;
        Vector3 eyePosition = source.position + Vector3.up * eyeHeight;
        Vector3 targetPosition = target.position + Vector3.up * targetHeight;
        Vector3 direction = targetPosition - eyePosition;
        float distance = direction.magnitude;
        if (distance > maximumDistance || distance <= Mathf.Epsilon)
        {
            return false;
        }

        if (Vector3.Angle(source.forward, direction) > fieldOfViewDegrees * 0.5f)
        {
            return false;
        }

        if (Physics.Raycast(eyePosition, direction / distance, out RaycastHit hit, distance, obstructionMask, QueryTriggerInteraction.Ignore))
        {
            Transform hitTransform = hit.transform;
            return hitTransform == target || hitTransform.IsChildOf(target);
        }

        return true;
    }

    private void Reset()
    {
        origin = transform;
    }
}
