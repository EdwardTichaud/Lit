// Role: decrit quel camp possede le tour dans une session de combat.
// Usage: partage par CombatSessionState, CombatSessionManager, HUD et messages reseau.
// Responsibilities: fournir une valeur simple et serialisable pour l'etat de tour.
// Dependencies: aucune.
// Precautions: ne pas changer les valeurs numeriques sans verifier la serialisation reseau.
/// <summary>
/// Etat de tour simplifie d'un combat.
/// </summary>
public enum CombatTurn
{
    /// <summary>Aucun tour actif.</summary>
    None = 0,
    /// <summary>Le tour des ennemis est actif.</summary>
    Enemy = 1,
    /// <summary>Le tour du joueur est actif.</summary>
    Player = 2,
    /// <summary>La session est terminee ou en resolution finale.</summary>
    Finished = 3
}
