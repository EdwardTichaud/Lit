using UnityEngine;

/// <summary>
/// Gère l'affichage du panneau Game Over.
/// S'assure que son Animator utilise le temps non ralenti.
/// </summary>
public class GameOverPanelManager : MonoBehaviour
{
    void Awake()
    {
        // On force l'Animator à fonctionner en temps réel
        Animator anim = GetComponent<Animator>();
        if (anim != null)
            anim.updateMode = AnimatorUpdateMode.UnscaledTime;
    }
}
