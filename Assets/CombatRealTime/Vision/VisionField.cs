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

    public bool TryEvaluate(Transform target, out float distance, out float angle, out string reason)
    {
        distance = 0f;
        angle = 0f;
        reason = "cible absente";
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            return false;
        }

        Transform source = origin != null ? origin : transform;
        Vector3 eyePosition = source.position + Vector3.up * eyeHeight;
        Vector3 targetPosition = target.position + Vector3.up * targetHeight;
        Vector3 direction = targetPosition - eyePosition;
        distance = direction.magnitude;
        if (distance <= Mathf.Epsilon)
        {
            reason = "distance nulle";
            return false;
        }

        angle = Vector3.Angle(source.forward, direction);
        if (distance > maximumDistance)
        {
            reason = "hors portee";
            return false;
        }

        if (angle > fieldOfViewDegrees * 0.5f)
        {
            reason = "hors angle";
            return false;
        }

        if (Physics.Raycast(eyePosition, direction / distance, out RaycastHit hit, distance, obstructionMask, QueryTriggerInteraction.Ignore))
        {
            Transform hitTransform = hit.transform;
            if (hitTransform != target && !hitTransform.IsChildOf(target))
            {
                reason = "obstrue par " + hitTransform.name;
                return false;
            }
        }

        reason = "visible";
        return true;
    }

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
        return TryEvaluate(target, out _, out _, out _);
    }

    private void Reset()
    {
        origin = transform;
    }
}
