using System;
using UnityEngine;

// Role: machine d'etat pure d'une session de combat.
// Usage: possedee par CombatSessionManager pour piloter tours, timers et resolution.
// Responsibilities: stocker la phase courante, les deadlines et le message HUD.
// Dependencies: CombatTurn, Mathf.
// Precautions: cette classe ne doit pas toucher aux GameObjects; garder la logique Unity dans le manager.
/// <summary>
/// Phase de haut niveau d'une session de combat.
/// </summary>
public enum CombatSessionPhase
{
    /// <summary>La session vient d'etre creee.</summary>
    Created = 0,
    /// <summary>Un tour normal est actif.</summary>
    TurnActive = 1,
    /// <summary>Une action joueur est en animation/resolution courte.</summary>
    PlayerAction = 2,
    /// <summary>La fin de combat est en cours de resolution.</summary>
    Resolving = 3,
    /// <summary>La session est terminee.</summary>
    Finished = 4
}

/// <summary>
/// Etat mutable d'une session de combat, sans dependance directe a la scene.
/// </summary>
[Serializable]
public sealed class CombatSessionState
{
    /// <summary>Camp dont le tour est actif.</summary>
    public CombatTurn Turn { get; private set; } = CombatTurn.None;
    /// <summary>Phase interne de la session.</summary>
    public CombatSessionPhase Phase { get; private set; } = CombatSessionPhase.Created;
    /// <summary>Temps absolu auquel le tour courant expire.</summary>
    public float TurnEndsAt { get; private set; }
    /// <summary>Temps absolu de la prochaine action ennemie.</summary>
    public float NextEnemyActionAt { get; private set; } = float.PositiveInfinity;
    /// <summary>Temps absolu de la prochaine diffusion d'un snapshot reseau.</summary>
    public float NextSnapshotAt { get; private set; }
    /// <summary>Temps absolu auquel l'action joueur en cours se termine.</summary>
    public float PlayerActionEndsAt { get; private set; }
    /// <summary>Degats en attente d'application apres l'action joueur.</summary>
    public int PendingPlayerAttackDamage { get; private set; }
    /// <summary>Resultat prevu de la phase de resolution.</summary>
    public bool ResolutionPlayerVictory { get; private set; }
    /// <summary>Temps absolu auquel la phase de resolution se termine.</summary>
    public float ResolutionEndsAt { get; private set; }
    /// <summary>Dernier message affiche au HUD.</summary>
    public string LastMessage { get; private set; } = string.Empty;

    /// <summary>Indique si une action joueur est en cours et bloque les nouvelles actions.</summary>
    public bool PlayerActionLocked => Phase == CombatSessionPhase.PlayerAction;
    /// <summary>Indique si la fin de combat est en cours.</summary>
    public bool Resolving => Phase == CombatSessionPhase.Resolving;
    /// <summary>Indique si la session ne doit plus evoluer.</summary>
    public bool Finished => Phase == CombatSessionPhase.Finished;

    /// <summary>
    /// Indique si le joueur peut lancer une action maintenant.
    /// </summary>
    public bool CanUsePlayerAction()
    {
        return Phase == CombatSessionPhase.TurnActive && Turn == CombatTurn.Player;
    }

    /// <summary>
    /// Demarre un tour joueur ou ennemi et initialise les timers associes.
    /// </summary>
    public void BeginTurn(CombatTurn turn, float now, float turnDurationSeconds, float enemyActionDelay, string message)
    {
        if (Finished || Resolving)
        {
            return;
        }

        Turn = turn;
        Phase = CombatSessionPhase.TurnActive;
        TurnEndsAt = now + Mathf.Max(1f, turnDurationSeconds);
        NextEnemyActionAt = turn == CombatTurn.Enemy
            ? now + Mathf.Max(0f, enemyActionDelay)
            : float.PositiveInfinity;
        PlayerActionEndsAt = 0f;
        PendingPlayerAttackDamage = 0;
        LastMessage = message ?? string.Empty;
    }

    /// <summary>
    /// Passe en sous-phase d'action joueur avant l'application des degats.
    /// </summary>
    public void BeginPlayerAction(int pendingDamage, float now, float actionDurationSeconds, string message)
    {
        if (!CanUsePlayerAction())
        {
            return;
        }

        Phase = CombatSessionPhase.PlayerAction;
        PendingPlayerAttackDamage = Mathf.Max(1, pendingDamage);
        PlayerActionEndsAt = now + Mathf.Max(0.05f, actionDurationSeconds);
        TurnEndsAt = PlayerActionEndsAt;
        NextEnemyActionAt = float.PositiveInfinity;
        LastMessage = message ?? string.Empty;
    }

    /// <summary>
    /// Recupere puis vide les degats de l'action joueur en attente.
    /// </summary>
    public int ConsumePendingPlayerAttackDamage()
    {
        int damage = Mathf.Max(1, PendingPlayerAttackDamage);
        PendingPlayerAttackDamage = 0;
        PlayerActionEndsAt = 0f;
        return damage;
    }

    /// <summary>
    /// Lance la phase de resolution finale avant de quitter le combat.
    /// </summary>
    public void BeginResolution(bool playerVictory, float now, float durationSeconds, string message)
    {
        if (Finished || Resolving)
        {
            return;
        }

        Phase = CombatSessionPhase.Resolving;
        ResolutionPlayerVictory = playerVictory;
        Turn = CombatTurn.Finished;
        TurnEndsAt = now;
        NextEnemyActionAt = float.PositiveInfinity;
        PlayerActionEndsAt = 0f;
        PendingPlayerAttackDamage = 0;
        LastMessage = message ?? string.Empty;
        ResolutionEndsAt = now + Mathf.Max(0f, durationSeconds);
    }

    /// <summary>
    /// Marque definitivement la session comme terminee.
    /// </summary>
    public void Finish()
    {
        Phase = CombatSessionPhase.Finished;
        Turn = CombatTurn.Finished;
        TurnEndsAt = 0f;
        NextEnemyActionAt = float.PositiveInfinity;
        PlayerActionEndsAt = 0f;
        PendingPlayerAttackDamage = 0;
    }

    /// <summary>
    /// Planifie le prochain snapshot reseau ou HUD.
    /// </summary>
    public void ScheduleNextSnapshot(float now, float intervalSeconds)
    {
        NextSnapshotAt = now + Mathf.Max(0.05f, intervalSeconds);
    }

    /// <summary>
    /// Retourne le temps restant du tour courant.
    /// </summary>
    public float GetTimerRemaining(float now)
    {
        return Mathf.Max(0f, TurnEndsAt - now);
    }
}
