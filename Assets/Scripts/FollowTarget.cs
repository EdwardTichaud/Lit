using UnityEngine;

// Suit un Transform avec option d'offset (utile pour camera, UI worldspace, etc.).
[DisallowMultipleComponent]
[ExecuteAlways]
public class FollowTarget : MonoBehaviour
{
    [Header("Target")]
    [SerializeField, Tooltip("Cible a suivre.")]
    private Transform target;
    [SerializeField, Tooltip("Suivre la position.")]
    private bool followPosition = true;
    [SerializeField, Tooltip("Suivre la rotation.")]
    private bool followRotation = true;
    [SerializeField, Tooltip("Regarder la cible.")]
    private bool lookAtTarget = true;

    [Header("Offset")]
    [SerializeField, Tooltip("Offset de position.")]
    private Vector3 positionOffset = Vector3.zero;
    [SerializeField, Tooltip("Offset de rotation en Euler.")]
    private Vector3 rotationOffsetEuler = Vector3.zero;
    [SerializeField, Tooltip("Offset en espace local de la cible.")]
    private bool offsetInTargetSpace = true;
    [SerializeField, Tooltip("Offset du LookAt.")]
    private Vector3 lookAtOffset = Vector3.zero;

    [Header("Update")]
    [SerializeField, Tooltip("Applique le follow en LateUpdate.")]
    private bool useLateUpdate = true;

    private void Update()
    {
        if (!useLateUpdate)
        {
            ApplyFollow();
        }
    }

    private void LateUpdate()
    {
        if (useLateUpdate)
        {
            ApplyFollow();
        }
    }

    private void ApplyFollow()
    {
        if (target == null)
        {
            return;
        }

        if (followRotation)
        {
            transform.rotation = target.rotation * Quaternion.Euler(rotationOffsetEuler);
        }

        if(lookAtTarget)
        {
            transform.LookAt(target.position + (Vector3)lookAtOffset);
        }

        if (followPosition)
        {
            if (offsetInTargetSpace)
            {
                transform.position = target.position + (target.rotation * positionOffset);
            }
            else
            {
                transform.position = target.position + positionOffset;
            }
        }
    }
}
