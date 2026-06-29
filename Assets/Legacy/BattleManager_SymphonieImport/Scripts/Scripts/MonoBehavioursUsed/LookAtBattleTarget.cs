using UnityEngine;

/// Oriente la caméra vers la cible actuelle avec un offset de visée.
public class LookAtBattleTarget : MonoBehaviour
{
    [Tooltip("Décalage appliqué lors du ciblage de la cible.")]
    public Vector3 offset;

    void LateUpdate()
    {
        // Récupère la cible actuelle pour orienter la caméra.
        var target = NewBattleManager.Instance?.currentTargetCharacter;
        if (!target) return;

        // Calcule la position visée en ajoutant l'offset puis oriente le transform vers cette position.
        Vector3 lookPosition = target.transform.position + offset;
        transform.LookAt(lookPosition);
    }
}
