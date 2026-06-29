using UnityEngine;

[ExecuteAlways]
public class LookAt : MonoBehaviour
{
    [Header("Cible")]

    public bool Enabled = true;
    public Transform target; // La cible à regarder

    [Header("Options")]
    public Vector3 positionOffset = Vector3.zero; // Décalage de position (avant de calculer la direction)
    public Vector3 rotationOffset = Vector3.zero; // Décalage d'orientation en Euler
    public bool smooth = false; // Si vrai, interpolation de la rotation
    public float smoothSpeed = 5f;

    void Update()
    {
        if (!Enabled) return;
        if (target == null) return;

        // Calcul direction vers la cible avec offset de position
        Vector3 targetPosition = target.position + positionOffset;
        Vector3 direction = targetPosition - transform.position;

        if (direction.sqrMagnitude > 0.001f) // éviter LookRotation sur un vecteur nul
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);

            // Appliquer l'offset de rotation
            lookRotation *= Quaternion.Euler(rotationOffset);

            if (smooth)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * smoothSpeed);
            }
            else
            {
                transform.rotation = lookRotation;
            }
        }
    }
}
