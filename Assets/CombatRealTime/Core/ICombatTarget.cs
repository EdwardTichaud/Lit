using UnityEngine;

/// <summary>Combat presentation and damage contract, independent of the enemy decision system.</summary>
public interface ICombatTarget
{
    Transform Root { get; }
    Transform LockPoint { get; }
    Animator Animator { get; }
    bool IsDead { get; }
    bool IsAttackCommitted { get; }
    int ActionSequenceId { get; }
    int ReceiveDamage(int damage);
    void SetCinematicSuspended(bool suspended);
    bool PlaceForCinematic(Vector3 position, Quaternion rotation);
}

public sealed class LegacyCombatTarget : ICombatTarget
{
    private readonly EnemyController enemy;
    public LegacyCombatTarget(EnemyController enemy) { this.enemy = enemy; }
    public Transform Root => enemy != null ? enemy.transform : null;
    public Transform LockPoint => enemy != null ? enemy.LockPoint : null;
    public Animator Animator => enemy != null ? enemy.Animator : null;
    public bool IsDead => enemy == null || enemy.Health != null && enemy.Health.IsDead;
    public bool IsAttackCommitted => enemy != null && enemy.IsAttackCommitted;
    public int ActionSequenceId => enemy != null ? enemy.ActionSequenceId : 0;
    public int ReceiveDamage(int damage) => enemy != null ? enemy.ReceiveLightDamage(damage) : 0;
    public void SetCinematicSuspended(bool suspended) { if (enemy != null) enemy.SetCinematicSuspended(suspended); }
    public bool PlaceForCinematic(Vector3 position, Quaternion rotation) => enemy != null && enemy.PlaceForCinematic(position, rotation);
}
