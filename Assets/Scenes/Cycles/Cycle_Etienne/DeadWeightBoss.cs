using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Ordinary enemy combat, with a shared emergency-brake stun. CharacterInfo owns all damage.</summary>
[DisallowMultipleComponent]
public sealed class DeadWeightBoss : BossEncounterBehaviour
{
    [SerializeField, Min(.1f)] private float brakeStunSeconds = 4f;
    [SerializeField, Min(.1f)] private float brakeCooldownSeconds = 12f;
    private readonly NetworkVariable<double> stunUntil = new(0);
    private readonly NetworkVariable<double> brakeReadyAt = new(0);
    private double offlineStunUntil, offlineBrakeReadyAt;
    private CharacterInfo observedHealth;
    private bool ownsSuspension;

    private double Now => EncounterTime;
    public bool IsBrakeHolding => IsBossEngaged && Now < (IsSpawned ? stunUntil.Value : offlineStunUntil);
    public double BrakeCooldownRemaining => System.Math.Max(0, (IsSpawned ? brakeReadyAt.Value : offlineBrakeReadyAt) - Now);

    protected override void Awake() { base.Awake(); BindHealth(); }
    private void OnEnable() => BindHealth();
    private void OnDisable()
    {
        if (observedHealth != null) observedHealth.HealthChanged -= OnHealthChanged;
        observedHealth = null;
        ReleaseBrakeSuspension();
        EndLocalCombatPresentation();
    }

    public override void OnNetworkSpawn() { base.OnNetworkSpawn(); BindHealth(); SynchronizeHealth(); }

    protected override void Update()
    {
        BindHealth();
        SynchronizeHealth();
        base.Update();
        if (IsBrakeHolding && Enemy != null && !Enemy.IsSuspended)
        {
            ownsSuspension = true;
            Enemy.SetSuspended(true);
        }
        else if (!IsBrakeHolding) ReleaseBrakeSuspension();
    }

    protected override bool CanEngage(Transform player)
    {
        if (Enemy == null || Enemy.Health == null || Enemy.Health.IsDead ||
            !NavMesh.SamplePosition(Enemy.transform.position, out _, 1.5f, NavMesh.AllAreas)) return false;
        if (NetworkManager != null && NetworkManager.IsListening && IsServer)
        {
            foreach (ulong id in NetworkManager.ConnectedClientsIds)
                if (base.CanEngage(NetcodePlayerUtils.GetPlayerTransform(id))) return true;
            return false;
        }
        return base.CanEngage(player);
    }

    public override bool FilterIncomingDamage(int incomingDamage, SquadCharacterController source, out int permittedDamage)
    {
        if (!IsBossEngaged) { permittedDamage = 0; return false; }
        return base.FilterIncomingDamage(incomingDamage, source, out permittedDamage);
    }

    public bool TryApplyEmergencyBrake()
    {
        if (!Authority || !IsBossEngaged || Enemy == null || Enemy.IsFlameDormant || BrakeCooldownRemaining > 0 || observedHealth == null || observedHealth.IsDead)
            return false;
        double stop = Now + brakeStunSeconds, ready = Now + Mathf.Max(brakeStunSeconds, brakeCooldownSeconds);
        if (IsSpawned) { stunUntil.Value = stop; brakeReadyAt.Value = ready; }
        else { offlineStunUntil = stop; offlineBrakeReadyAt = ready; }
        return true;
    }

    private void BindHealth()
    {
        CharacterInfo next = Enemy != null ? Enemy.Health : null;
        if (observedHealth == next) return;
        if (observedHealth != null) observedHealth.HealthChanged -= OnHealthChanged;
        observedHealth = next;
        if (next != null) next.HealthChanged += OnHealthChanged;
    }
    private void OnHealthChanged(CharacterInfo _) => SynchronizeHealth();
    private void SynchronizeHealth()
    {
        if (!Authority || observedHealth == null) return;
        if (observedHealth.IsDead) { if (!IsBossResolved) ResolveAuthoritatively(); return; }
        if (!IsBossResolved && CurrentSegments != observedHealth.CurrentHp) SetSegments(observedHealth.CurrentHp);
    }
    protected override void OnBossResolvedAuthoritatively()
    {
        ReleaseBrakeSuspension();
        // A boss can resolve through a scripted segment or restored cycle,
        // without a final ordinary hit. Use the common defeat path in both
        // cases so the death animation and dissolution are never skipped.
        Enemy?.ForceDefeatFromThreshold();
    }
    private void ReleaseBrakeSuspension()
    {
        if (!ownsSuspension) return;
        ownsSuspension = false;
        if (Enemy != null) Enemy.SetSuspended(false);
    }
}
