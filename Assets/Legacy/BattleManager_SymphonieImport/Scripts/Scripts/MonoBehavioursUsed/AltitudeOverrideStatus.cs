using UnityEngine;

/// <summary>
///     Gère les effets temporaires qui forcent une unité à rester collée au sol
///     ou, au contraire, à flotter dans les airs pendant un certain nombre de tours.
///     Cette classe agit comme un tampon centralisé afin que les MusicalMoves,
///     les Items et les systèmes de gestion de tour puissent consulter ou
///     prolonger facilement ces contraintes sans dupliquer la logique.
/// </summary>
[DisallowMultipleComponent]
public class AltitudeOverrideStatus : MonoBehaviour
{
    [SerializeField, Tooltip("Nombre de tours restants durant lesquels l'unité est contrainte au sol.")]
    private int forcedGroundRemainingTurns;

    [SerializeField, Tooltip("Nombre de tours restants durant lesquels l'unité est suspendue dans les airs.")]
    private int forcedAirRemainingTurns;

    /// <summary>
    ///     Indique si un override est encore actif (sol ou air).
    ///     Utile pour savoir si le composant peut être ignoré par les routines de fin de tour.
    /// </summary>
    public bool HasActiveOverride => forcedGroundRemainingTurns > 0 || forcedAirRemainingTurns > 0;

    /// <summary>
    ///     Indique si l'unité doit être considérée comme rivée au sol quel que soit son type d'origine.
    /// </summary>
    public bool IsForcedGrounded => forcedGroundRemainingTurns > 0;

    /// <summary>
    ///     Indique si l'unité est forcée de flotter dans les airs et doit être traitée comme telle
    ///     lors des vérifications d'altitude.
    /// </summary>
    public bool IsSuspendedInAir => forcedAirRemainingTurns > 0;

    /// <summary>
    ///     Applique (ou prolonge) un ancrage au sol pour un nombre de tours donné.
    ///     Toute suspension aérienne active est annulée pour éviter les états contradictoires.
    /// </summary>
    public void AnchorToGround(int turns)
    {
        forcedGroundRemainingTurns = Mathf.Max(forcedGroundRemainingTurns, Mathf.Max(0, turns));
        forcedAirRemainingTurns = 0;
    }

    /// <summary>
    ///     Applique (ou prolonge) une suspension dans les airs pour un nombre de tours donné.
    ///     Toute contrainte au sol active est annulée dans la foulée.
    /// </summary>
    public void SuspendInAir(int turns)
    {
        forcedAirRemainingTurns = Mathf.Max(forcedAirRemainingTurns, Mathf.Max(0, turns));
        forcedGroundRemainingTurns = 0;
    }

    /// <summary>
    ///     Décrémente les compteurs en fin de tour afin que les overrides expirent naturellement.
    ///     La méthode renvoie vrai si un état reste actif, ce qui facilite la surveillance côté appelant.
    /// </summary>
    public bool TickTurn()
    {
        if (forcedGroundRemainingTurns > 0)
            forcedGroundRemainingTurns = Mathf.Max(0, forcedGroundRemainingTurns - 1);

        if (forcedAirRemainingTurns > 0)
            forcedAirRemainingTurns = Mathf.Max(0, forcedAirRemainingTurns - 1);

        return HasActiveOverride;
    }

    /// <summary>
    ///     Réinitialise complètement les overrides en cours.
    ///     Pratique si un effet annule brutalement toutes les contraintes d'altitude.
    /// </summary>
    public void ResetOverrides()
    {
        forcedGroundRemainingTurns = 0;
        forcedAirRemainingTurns = 0;
    }
}
