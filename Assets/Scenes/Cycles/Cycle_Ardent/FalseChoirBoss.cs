using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Mobile ordinary enemy, revealed by an authoritative echo relay. No quest rules in EnemyController.</summary>
public sealed class FalseChoirBoss : BossEncounterBehaviour
{
    public CycleController cycle;
    public Transform arenaCenter;
    [Min(1)] public float arenaRadius = 12;
    [Min(.1f)] public float revealSeconds = 8;
    [Min(.1f)] public float relayCooldownSeconds = 12;
    public GameObject[] visualDecoys;
    private readonly NetworkVariable<int> activeRelay = new(0);
    private readonly NetworkVariable<double> exposedUntil = new(0), readyAt = new(0);
    private int offlineRelay;
    private double offlineExposed, offlineReady;
    private CharacterInfo health;
    private double Now => EncounterTime;
    public int ActiveRelay => IsSpawned ? activeRelay.Value : offlineRelay;
    public bool IsExposed => IsBossEngaged && Now < (IsSpawned ? exposedUntil.Value : offlineExposed);
    public double CooldownRemaining => System.Math.Max(0,(IsSpawned ? readyAt.Value : offlineReady)-Now);
    public override bool SuppressDefaultEnemyBrain => false;
    protected override void Awake() { base.Awake(); BindHealth(); }
    public override void OnNetworkSpawn() { base.OnNetworkSpawn(); BindHealth(); HealthChanged(health); }
    private void OnEnable() { BindHealth(); if(Enemy != null && !IsBossEngaged) Enemy.SetSuspended(true); }
    protected override void OnBossEngagedAuthoritatively() { if(Enemy != null) Enemy.SetSuspended(false); }
    private void OnDisable()
    {
        if (health != null) health.HealthChanged -= HealthChanged;
        health = null; EndLocalCombatPresentation();
        foreach(var decoy in visualDecoys ?? System.Array.Empty<GameObject>()) if(decoy != null) decoy.SetActive(false);
    }
    protected override bool CanEngage(Transform player)
    {
        if(cycle == null || !cycle.IsSceneStepCompleted("echo_relay_one_tuned") ||
           !cycle.IsSceneStepCompleted("echo_relay_two_tuned") || !cycle.IsSceneStepCompleted("echo_relay_three_tuned") ||
           Enemy == null || Enemy.Health == null || Enemy.Health.IsDead ||
           !NavMesh.SamplePosition(transform.position,out _,1.5f,NavMesh.AllAreas)) return false;
        Vector3 center=arenaCenter != null ? arenaCenter.position : transform.position;
        bool nearby=false, found=false;
        foreach(var candidate in CycleGroupSequenceGate.LivingPlayers())
        {
            found=true;
            if((candidate.player.position-center).sqrMagnitude > arenaRadius*arenaRadius) return false;
            if(base.CanEngage(candidate.player)) nearby=true;
        }
        return found && nearby;
    }
    protected override void Update()
    {
        BindHealth();
        if(Authority && IsBossEngaged && Enemy != null && !Enemy.IsFlameDormant)
        {
            double end=IsSpawned ? exposedUntil.Value : offlineExposed;
            if(end > 0 && Now >= end)
            {
                int next=(ActiveRelay+1)%3;
                if(IsSpawned) { activeRelay.Value=next; exposedUntil.Value=0; }
                else { offlineRelay=next; offlineExposed=0; }
            }
        }
        base.Update();
        foreach(var decoy in visualDecoys ?? System.Array.Empty<GameObject>())
            if(decoy != null) decoy.SetActive(IsBossEngaged && !IsExposed);
    }
    public bool TryReveal(int index)
    {
        if(!Authority || !IsBossEngaged || Enemy == null || Enemy.IsFlameDormant || IsExposed || CooldownRemaining > 0 || index != ActiveRelay) return false;
        if(IsSpawned) { exposedUntil.Value=Now+revealSeconds; readyAt.Value=Now+Mathf.Max(revealSeconds,relayCooldownSeconds); }
        else { offlineExposed=Now+revealSeconds; offlineReady=Now+Mathf.Max(revealSeconds,relayCooldownSeconds); }
        return true;
    }
    public override bool FilterIncomingDamage(int incomingDamage,SquadCharacterController source,out int permittedDamage)
    {
        if(IsExposed) return base.FilterIncomingDamage(incomingDamage,source,out permittedDamage);
        permittedDamage=0;
        if(Application.isPlaying && IsBossEngaged) CombatDamageWorldFeedback.ShowMessage(transform,"Révélez le vrai corps",new Color(.65f,.8f,1),2.2f);
        return false;
    }
    private void BindHealth()
    {
        var next=Enemy != null ? Enemy.Health : null;
        if(next == health) return;
        if(health != null) health.HealthChanged -= HealthChanged;
        health=next;
        if(health != null) { health.HealthChanged += HealthChanged; HealthChanged(health); }
    }
    private void HealthChanged(CharacterInfo value)
    {
        if(!Authority || value == null) return;
        if(value.IsDead) ResolveAuthoritatively();
        else if(!IsBossResolved) SetSegments(value.CurrentHp);
    }
}
