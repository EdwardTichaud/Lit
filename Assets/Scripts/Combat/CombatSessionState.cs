using System;
using UnityEngine;

public enum CombatSessionPhase
{
    Created = 0,
    TurnActive = 1,
    PlayerAction = 2,
    Resolving = 3,
    Finished = 4
}

[Serializable]
public sealed class CombatSessionState
{
    public CombatTurn Turn { get; private set; } = CombatTurn.None;
    public CombatSessionPhase Phase { get; private set; } = CombatSessionPhase.Created;
    public float TurnEndsAt { get; private set; }
    public float NextEnemyActionAt { get; private set; } = float.PositiveInfinity;
    public float NextSnapshotAt { get; private set; }
    public float PlayerActionEndsAt { get; private set; }
    public int PendingPlayerAttackDamage { get; private set; }
    public bool ResolutionPlayerVictory { get; private set; }
    public float ResolutionEndsAt { get; private set; }
    public string LastMessage { get; private set; } = string.Empty;

    public bool PlayerActionLocked => Phase == CombatSessionPhase.PlayerAction;
    public bool Resolving => Phase == CombatSessionPhase.Resolving;
    public bool Finished => Phase == CombatSessionPhase.Finished;

    public bool CanUsePlayerAction()
    {
        return Phase == CombatSessionPhase.TurnActive && Turn == CombatTurn.Player;
    }

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

    public int ConsumePendingPlayerAttackDamage()
    {
        int damage = Mathf.Max(1, PendingPlayerAttackDamage);
        PendingPlayerAttackDamage = 0;
        PlayerActionEndsAt = 0f;
        return damage;
    }

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

    public void Finish()
    {
        Phase = CombatSessionPhase.Finished;
        Turn = CombatTurn.Finished;
        TurnEndsAt = 0f;
        NextEnemyActionAt = float.PositiveInfinity;
        PlayerActionEndsAt = 0f;
        PendingPlayerAttackDamage = 0;
    }

    public void ScheduleNextSnapshot(float now, float intervalSeconds)
    {
        NextSnapshotAt = now + Mathf.Max(0.05f, intervalSeconds);
    }

    public float GetTimerRemaining(float now)
    {
        return Mathf.Max(0f, TurnEndsAt - now);
    }
}
