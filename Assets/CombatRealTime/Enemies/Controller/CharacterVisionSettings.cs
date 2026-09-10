using UnityEngine;

[System.Serializable]
public sealed class CharacterVisionSettings
{
    [Min(0.1f), Tooltip("Distance maximale de detection, en metres.")] public float maximumDistance = 6f;
    [Range(1f, 360f), Tooltip("Ouverture du champ de vision en degres.")] public float fieldOfViewDegrees = 110f;
    [Tooltip("Decalage vertical depuis l origine du regard.")] public float eyeHeight = 1.35f;
    [Tooltip("Hauteur visee au-dessus de la cible.")] public float targetHeight = 1f;
    [Tooltip("Couches pouvant bloquer la vue. Les triggers sont ignores.")] public LayerMask obstructionMask = ~0;

    public bool TryEvaluate(Transform source, Transform target, float range, float fieldOfView, out float distance, out float angle, out string reason)
    {
        distance = 0f;
        angle = 0f;
        reason = "cible absente";
        if (source == null || target == null || !target.gameObject.activeInHierarchy)
        {
            return false;
        }

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
        if (distance > range)
        {
            reason = "hors portee";
            return false;
        }

        if (angle > fieldOfView * 0.5f)
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

}
