using UnityEngine;

/// <summary>
/// Gère l'orientation d'un PNJ lorsqu'il entre en interaction avec le joueur.
/// Selon l'angle à parcourir, déclenche l'animation adéquate.
/// </summary>
[RequireComponent(typeof(Animator))]
public class NPCTurnHandler : MonoBehaviour
{
    [Header("Réglages d'animation")]
    [Tooltip("Nom du clip joué pour un quart de tour (≤ 90°).")]
    public string turn90Animation = "Turn_90";

    [Tooltip("Nom du clip joué pour un demi-tour (> 90°).")]
    public string turn180Animation = "Turn_180";

    private Animator animator;          // Référence vers l'Animator du PNJ
    private Transform playerTransform;  // Transform du joueur, recherché au premier appel

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    /// <summary>
    /// Fait pivoter instantanément le PNJ vers le joueur en jouant
    /// l'animation correspondant à l'angle de rotation nécessaire.
    /// </summary>
    public void TurnTowardPlayer()
    {
        // Récupère le transform du joueur si besoin
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                playerTransform = playerObj.transform;
        }

        if (playerTransform == null)
        {
            Debug.LogWarning("NPCTurnHandler : impossible de trouver le joueur.");
            return;
        }

        // Direction horizontale vers le joueur
        Vector3 toPlayer = playerTransform.position - transform.position;
        toPlayer.y = 0f;

        if (toPlayer.sqrMagnitude < Mathf.Epsilon)
            return; // Évite les calculs inutiles si le joueur est très proche

        // Calcule l'angle entre l'avant du PNJ et la direction du joueur
        float angle = Vector3.Angle(transform.forward, toPlayer);

        // Choix de l'animation en fonction de l'angle
        string animationToPlay = angle <= 90f ? turn90Animation : turn180Animation;
        animator.Play(animationToPlay);

        // Oriente finalement le PNJ vers le joueur
        Quaternion targetRot = Quaternion.LookRotation(toPlayer);
        transform.rotation = targetRot;
    }
}
