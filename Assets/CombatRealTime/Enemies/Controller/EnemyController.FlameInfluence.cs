using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public sealed partial class EnemyController
{
    [SerializeField, Tooltip("Disable only for an explicit authored exception. Ordinary enemies and bosses require a fixed Flame.")]
    private bool requiresFlameInfluence = true;
    private readonly NetworkVariable<bool> replicatedFlameInfluence = new(false);
    private readonly HashSet<EntityId> flameSourceIds = new HashSet<EntityId>();
    private bool activeFlameInfluence, flamePaused, savedAgentStopped, savedKinematic, agentPauseCaptured;
    private Vector3 savedBodyVelocity, savedBodyAngularVelocity;
    private float nextFlameCheck;
    private TimeManager flamePauseManager;
    private TimeManager.TimeRequestHandle flamePauseHandle;
    private Collider[] flameContactColliders;
    public bool RequiresFlameInfluence => requiresFlameInfluence;
    public bool HasActiveFlameInfluence => activeFlameInfluence;
    public bool IsFlameDormant => requiresFlameInfluence && !activeFlameInfluence && (Health == null || !Health.IsDead);
    public static bool IsActivationFlame(LitInfluenceInfo info) => info.Source is Flame flame && flame.isActiveAndEnabled &&
        flame.IsEffectivelyLit && (info.SourceKind == LitInfluenceSourceKind.Flame || info.SourceKind == LitInfluenceSourceKind.AncientFlame);
    public void OnLitInfluenceEnter(LitInfluenceInfo info) { if (IsActivationFlame(info)) FlameInfluenceRefresh(true); }
    public void OnLitInfluenceStay(LitInfluenceInfo info) { if (IsActivationFlame(info)) FlameInfluenceRefresh(); }
    public void OnLitInfluenceExit(LitInfluenceInfo info) { if (info.Source is Flame) FlameInfluenceRefresh(true); }
    private void FlameInfluenceRefresh(bool force = false)
    {
        if (!initialized) return;
        if (BrainAuthority && (force || Time.unscaledTime >= nextFlameCheck))
        {
            nextFlameCheck = Time.unscaledTime + .25f;
            activeFlameInfluence = SampleActiveFlameInfluence();
            if (IsSpawned && IsServer && replicatedFlameInfluence.Value != activeFlameInfluence)
                replicatedFlameInfluence.Value = activeFlameInfluence;
        }
        ApplyFlamePause();
    }
    private bool SampleActiveFlameInfluence()
    {
        flameContactColliders ??= GetComponentsInChildren<Collider>(true);
        flameSourceIds.Clear();
        foreach (Flame flame in Flame.ActiveInfluenceFlames)
        {
            if (flame == null) continue;
            foreach (Collider collider in flameContactColliders)
            {
                if (!flame.ProvidesEnemyActivationTo(collider)) continue;
                flameSourceIds.Add(flame.GetEntityId());
                break;
            }
        }
        return flameSourceIds.Count > 0;
    }
    private void FlameInfluenceNetworkSpawn()
    {
        replicatedFlameInfluence.OnValueChanged += OnFlameInfluenceReplicated;
        if (!IsServer) activeFlameInfluence = replicatedFlameInfluence.Value;
        FlameInfluenceRefresh(true);
    }
    private void FlameInfluenceNetworkDespawn() => replicatedFlameInfluence.OnValueChanged -= OnFlameInfluenceReplicated;
    private void OnFlameInfluenceReplicated(bool previous, bool current) { activeFlameInfluence = current; ApplyFlamePause(); }
    private void ApplyFlamePause()
    {
        bool paused = IsFlameDormant;
        if (paused == flamePaused)
        {
            if (paused) { EnsureFlameTimePause(); KeepFlameNavigationPaused(); }
            return;
        }
        flamePaused = paused;
        // This owner is independent of cinematic/QTE owners. Never call Suspend(),
        // which cancels the pending attack instead of preserving it.
        TimeDomain?.SetIntrinsicPause(this, paused);
        if (paused)
        {
            EnsureFlameTimePause();
            agentPauseCaptured = false;
            KeepFlameNavigationPaused();
            if (PhysicsBody != null)
            {
                savedKinematic = PhysicsBody.isKinematic;
                savedBodyVelocity = PhysicsBody.linearVelocity; savedBodyAngularVelocity = PhysicsBody.angularVelocity;
                PhysicsBody.isKinematic = true;
            }
        }
        else
        {
            if (flamePauseManager != null) flamePauseManager.Release(flamePauseHandle);
            flamePauseHandle = default; flamePauseManager = null;
            if (PhysicsBody != null && !cinematicSuspended)
            {
                PhysicsBody.isKinematic = savedKinematic;
                if (!savedKinematic) { PhysicsBody.linearVelocity = savedBodyVelocity; PhysicsBody.angularVelocity = savedBodyAngularVelocity; }
            }
            if (!IsSuspended && agentPauseCaptured && PhysicsNavigationAgent != null && PhysicsNavigationAgent.isActiveAndEnabled && PhysicsNavigationAgent.isOnNavMesh)
                PhysicsNavigationAgent.isStopped = savedAgentStopped;
            agentPauseCaptured = false;
            if (!cinematicSuspended && State == CombatEnemyPhysicsState.Navigation && PhysicsNavigationSuppressed)
                PhysicsResumeNavigation();
            if (CombatEnabled && isActiveAndEnabled && !IsBossBrainSuppressed) NavigationOnEnable();
        }
    }
    private void EnsureFlameTimePause()
    {
        if (flamePauseManager != null) return;
        flamePauseManager = TimeManager.EnsureInstance();
        if (flamePauseManager != null) flamePauseHandle = flamePauseManager.AcquireLocalPause(TimeDomain, this);
    }
    private void KeepFlameNavigationPaused()
    {
        // NavMesh can become ready after spawn. Freeze it without ResetPath so
        // a pursuit already in progress resumes with its destination intact.
        if (PhysicsNavigationAgent == null || !PhysicsNavigationAgent.isActiveAndEnabled || !PhysicsNavigationAgent.isOnNavMesh) return;
        if (!agentPauseCaptured) { savedAgentStopped = PhysicsNavigationAgent.isStopped; agentPauseCaptured = true; }
        PhysicsNavigationAgent.isStopped = true;
    }
    private void FlameInfluenceRelease()
    {
        TimeDomain?.SetIntrinsicPause(this, false);
        if (flamePauseManager != null) flamePauseManager.Release(flamePauseHandle);
        if (flamePaused && PhysicsBody != null && !cinematicSuspended) PhysicsBody.isKinematic = savedKinematic;
        flamePauseHandle = default; flamePauseManager = null; flamePaused = false;
        activeFlameInfluence = false; flameSourceIds.Clear();
    }
}
