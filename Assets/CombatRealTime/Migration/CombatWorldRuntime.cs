using System.Collections.Generic;
using Lit.Combat.Migration.Network;
using UnityEngine;

/// <summary>Connects authored actors to shared combat services without spawning or moving them.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RealTimeCombatManager))]
public sealed class CombatWorldRuntime : MonoBehaviour
{
    public FreeAttackProfile freeAttacks = new FreeAttackProfile();
    public Camera gameplayCamera;
    public GameObject lockCursorPrefab;
    public Vector3 lockCursorOffset = Vector3.up * 3f;
    private RealTimeCombatManager manager;
    private readonly Dictionary<LitBrainsEnemy, EnemySubscription> subscriptions = new Dictionary<LitBrainsEnemy, EnemySubscription>();
    private float nextScan;
    private readonly CombatEngagementLedger engagements = new CombatEngagementLedger();
    private NavMeshWorldService navigationWorld;
    private CombatNetworkActorRuntime networkActor;
    private NetworkCharacterInput networkInput;
    public event System.Action<SquadCharacterController, bool> EngagementChanged;
    public event System.Action<SquadCharacterController> ThreatsChanged;

    public void ConfigureLockIndicator(CombatLockIndicator indicator)
    {
        if (indicator != null && lockCursorPrefab != null)
            indicator.ConfigureWorldCursor(lockCursorPrefab, lockCursorOffset);
    }

    public int CountThreats(SquadCharacterController actor) => actor != null ? engagements.Count(actor.transform) : 0;
    public List<ICombatTarget> GetThreats(SquadCharacterController actor) => engagements.Snapshot(actor != null ? actor.transform : null);

    public bool TryEngageActor(SquadCharacterController actor, ICombatTarget threat)
    {
        if (!isActiveAndEnabled || !CombatProtectionDamage.HasAuthority || actor == null || !actor.isActiveAndEnabled ||
            actor.CurrentHp <= 0 || actor.CharacterData == null || actor.CharacterData.isEnemy || !SkillTargetingContract.Alive(threat)) return false;
        var legacy = threat.Root.GetComponent<EnemyController>();
        var encounter = EnemyEncounterActors.Resolve(threat.Root);
        if (encounter != null && (!EnemyEncounterActors.IsAvailable(encounter) ||
            encounter.BossEncounter != null && (!encounter.BossEncounter.IsBossEngaged || encounter.BossEncounter.IsBossResolved))) return false;
        if (legacy != null && (!legacy.CombatEnabled || legacy.IsFlameDormant ||
            legacy.BossEncounter != null && !legacy.BossEncounter.IsBossEngaged)) return false;
        threat.Root.GetComponent<LitBrainsEnemy>()?.NotifyPlayerContact(actor.transform);
        bool entering = engagements.Count(actor.transform) == 0;
        bool added = engagements.Add(actor.transform, threat);
        if (added)
        {
            if (entering) EngagementChanged?.Invoke(actor, true);
            ThreatsChanged?.Invoke(actor);
        }
        if (manager != null && manager.PlayerRoot == actor.transform)
            manager.ApplyConfirmedWorldEngagement(threat, true);
        return true;
    }

    private sealed class EnemySubscription
    {
        public System.Action<AnimationEvent> impact, reaction;
        public int resolvedGeneration = -1;
    }

    private void OnEnable()
    {
        manager = GetComponent<RealTimeCombatManager>();
        LocalPlayerContext.LocalCharacterChanged += BindPlayer;
        manager.ConfigureFreeAttacks(freeAttacks, gameplayCamera != null ? gameplayCamera : Camera.main);
        BindPlayer(LocalPlayerContext.LocalCharacterRoot);
        RefreshEnemies();
        BindNavigationWorld();
    }

    private void BindPlayer(Transform actor)
    {
        UnbindNetworkActor();
        if (manager != null) manager.RegisterControlledActor(actor);
        networkActor = actor != null ? actor.GetComponent<CombatNetworkActorRuntime>() : null;
        networkInput = actor != null ? actor.GetComponent<NetworkCharacterInput>() : null;
        if (networkActor != null) networkActor.ConfirmedAction += OnNetworkActionConfirmed;
        if (networkInput != null) networkInput.CombatCommandResult += OnNetworkCommandResult;
    }

    private void UnbindNetworkActor()
    {
        if (networkActor != null) networkActor.ConfirmedAction -= OnNetworkActionConfirmed;
        if (networkInput != null) networkInput.CombatCommandResult -= OnNetworkCommandResult;
        networkActor = null; networkInput = null;
    }

    private void OnNetworkActionConfirmed(SkillSO skill, CombatActionTicket ticket)
    {
        if (networkActor == null || !networkActor.IsOwner || manager == null || manager.PlayerRoot != networkActor.transform) return;
        manager.ConfirmHealingSkill(skill, manager.PlayerRoot, networkActor.GetSkillCooldownRemaining(skill));
    }

    private void OnNetworkCommandResult(ulong sequence, CombatCommandRejection result, CombatActionTicket ticket)
    {
        if (result == CombatCommandRejection.None || manager == null || networkActor == null ||
            !networkActor.IsOwner || manager.PlayerRoot != networkActor.transform) return;
        string message = result switch {
            CombatCommandRejection.Cooldown => "Competence en recharge",
            CombatCommandRejection.OutOfRange => "Cible hors portee",
            CombatCommandRejection.Busy => "Action deja en cours",
            CombatCommandRejection.InvalidTarget => "Cible indisponible",
            _ => "Action refusee par le serveur : " + result
        };
        CombatDamageWorldFeedback.ShowMessage(manager.PlayerRoot, message, Color.yellow, 2.3f);
    }

    private void Update()
    {
        if (manager == null) return;
        BindNavigationWorld();
        ForwardStandaloneLocomotion();
        if (gameplayCamera == null)
            manager.ConfigureFreeAttacks(freeAttacks, Camera.main);
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + .25f;
        RefreshEnemies();
        RefreshEngagements();
        manager.RefreshWorldEngagements();
    }

    private void RefreshEngagements()
    {
        foreach (var root in engagements.Prune((actor, threat) =>
        {
            var player = actor.GetComponent<SquadCharacterController>();
            if (player == null || !player.isActiveAndEnabled || player.CurrentHp <= 0) return false;
            var encounter = EnemyEncounterActors.Resolve(threat.Root);
            if (encounter != null && encounter.BossEncounter != null)
                return EnemyEncounterActors.IsEngagedWith(encounter, actor);
            var brain = threat.Root.GetComponent<LitBrainsEnemy>();
            if (brain != null && brain.IsCinematicSuspended) return true;
            if (brain != null) return brain.IsThreatFor(actor);
            var legacy = threat.Root.GetComponent<EnemyController>();
            return legacy == null || !legacy.ShouldEndCombatForPursuit &&
                (legacy.HasActiveBattleWall || !legacy.IsPositionOutsidePursuitZone(actor.position));
        }))
        {
            var actor = root != null ? root.GetComponent<SquadCharacterController>() : null;
            if (actor == null) continue;
            bool engaged = engagements.Count(root) > 0;
            if (!engaged) EngagementChanged?.Invoke(actor, false);
            ThreatsChanged?.Invoke(actor);
            if (!engaged && manager.PlayerRoot == root) manager.ApplyConfirmedWorldEngagement(null, false);
        }
    }

    private void BindNavigationWorld()
    {
        var world = NavMeshWorldService.Instance;
        if (navigationWorld == world) return;
        if (navigationWorld != null) navigationWorld.StateChanged -= OnWorldStateChanged;
        if (navigationWorld != null) ClearEngagements();
        navigationWorld = world;
        if (world != null) world.StateChanged += OnWorldStateChanged;
    }

    private void OnWorldStateChanged(NavMeshWorldState state)
    {
        if (state == NavMeshWorldState.Invalidating || state == NavMeshWorldState.Unloaded) ClearEngagements();
    }

    private void ClearEngagements()
    {
        var clash = GetComponent<CombatLabClashController>();
        if (clash != null && clash.UsesWorldActors) clash.Cancel();
        foreach (var root in engagements.Clear())
        {
            var actor = root != null ? root.GetComponent<SquadCharacterController>() : null;
            if (actor == null) continue;
            EngagementChanged?.Invoke(actor, false);
            ThreatsChanged?.Invoke(actor);
            if (manager != null && manager.PlayerRoot == root) manager.ApplyConfirmedWorldEngagement(null, false);
        }
    }

    private void ForwardStandaloneLocomotion()
    {
        // Production movement is already forwarded by SquadManager or NetworkCharacterInput.
        var root = manager.PlayerRoot;
        if (root == null || Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening ||
            SquadManager.Instance != null && SquadManager.Instance.currentCharacter != null) return;
        var player = root.GetComponent<SquadCharacterController>();
        if (player == null) return;
        bool allowed = !InputFocusStack.HasAnyFocus() && !manager.IsCinematicSequenceActive &&
            (InputModeCoordinator.CurrentMode == InputMode.Exploration || InputModeCoordinator.CurrentMode == InputMode.Combat ||
             InputModeCoordinator.CurrentMode == InputMode.CombatWheel);
        player.SetSprintModifier(allowed && LocalInputRouter.SprintPressed);
        player.Move(allowed ? LocalInputRouter.MoveValue : Vector2.zero);
    }

    public void RefreshEnemies()
    {
        foreach (var enemy in FindObjectsByType<LitBrainsEnemy>())
        {
            if (enemy == null || subscriptions.ContainsKey(enemy)) continue;
            var subscription = new EnemySubscription();
            subscription.impact = source => ResolveEnemyContact(enemy, subscription);
            subscription.reaction = source => OpenReaction(enemy);
            subscriptions.Add(enemy, subscription);
            enemy.AttackImpact += subscription.impact;
            enemy.ReactionOpportunity += subscription.reaction;
        }
        var removed = new List<LitBrainsEnemy>();
        foreach (var pair in subscriptions)
            if (pair.Key == null || !pair.Key.isActiveAndEnabled) removed.Add(pair.Key);
        foreach (var enemy in removed) Unsubscribe(enemy);
        BindWorldClashes();
    }

    private void BindWorldClashes()
    {
        var clash = GetComponent<CombatLabClashController>();
        if (clash == null || manager == null) return;
        if (clash.IsBound && !clash.UsesWorldActors) return; // An explicit standalone fixture keeps its binding.
        clash.BindWorld(manager);
        clash.RefreshWorldActors();
    }

    public bool QueueClashImpact(Transform attacker, SkillSO skill, System.Action apply) =>
        GetComponent<CombatLabClashController>()?.QueueActorImpact(attacker, skill, apply) == true;

    private void OpenReaction(LitBrainsEnemy enemy)
    {
        if (enemy == null) return;
        var attack = enemy.ActiveAttack;
        if (attack?.skill == null) return;
        var network = Unity.Netcode.NetworkManager.Singleton;
        if (network != null && network.IsListening)
        {
            if (network.IsServer && enemy.CommittedAttackTarget != null)
                enemy.CommittedAttackTarget.GetComponent<CombatNetworkActorRuntime>()?.ServerOpenEnemyReaction(
                    enemy.GetComponent<Unity.Netcode.NetworkObject>(),
                    new EnemyReactionTiming(enemy.Animator, attack.clip, attack.stateName, attack.impactSeconds, enemy.ActionSequenceId));
            return;
        }
        if (enemy.CommittedAttackTarget != manager.PlayerRoot) return;
        manager.GetComponent<CombatHealthThresholdController>()?.OpenEnemyReactionOpportunity(enemy, attack.skill,
            new EnemyReactionTiming(enemy.Animator, attack.clip, attack.stateName, attack.impactSeconds, enemy.ActionSequenceId));
    }

    public void ResolveEnemyContact(LitBrainsEnemy enemy)
    {
        if (enemy != null && subscriptions.TryGetValue(enemy, out var entry)) ResolveEnemyContact(enemy, entry);
    }

    private void ResolveEnemyContact(LitBrainsEnemy enemy, EnemySubscription subscription)
    {
        if (!CombatProtectionDamage.HasAuthority || enemy == null || enemy.IsDead || enemy.IsCinematicSuspended ||
            enemy.IsFlameDormant || CombatCinematicReservations.ContainsParticipant(enemy.transform) ||
            subscription.resolvedGeneration == enemy.ActionSequenceId) return;
        subscription.resolvedGeneration = enemy.ActionSequenceId;
        var attack = enemy.ActiveAttack;
        if (attack?.skill == null) return;
        var victims = new List<SquadCharacterController>();
        if (enemy.UsesAuthoredProductionCombat) enemy.CollectAuthoredImpactVictims(victims);
        else
        {
            var victim = enemy.CommittedAttackTarget != null ? enemy.CommittedAttackTarget.GetComponent<SquadCharacterController>() : null;
            if (victim != null) victims.Add(victim);
        }
        int generation = enemy.ActionSequenceId;
        int damage = enemy.UsesAuthoredProductionCombat ? enemy.AuthoredAttackDamage : Mathf.RoundToInt(attack.skill.Damages);
        foreach (var victim in victims)
            ResolveEnemyVictimContact(enemy, victim, attack, generation, damage);
    }

    private void ResolveEnemyVictimContact(LitBrainsEnemy enemy, SquadCharacterController victim,
        LitBrainsAttackProfile attack, int generation, int damageAmount)
    {
        if (victim == null || !victim.isActiveAndEnabled || victim.CurrentHp <= 0 ||
            CombatCinematicReservations.ContainsParticipant(victim.transform)) return;
        var target = victim.transform;
        var networkVictim = victim.GetComponent<CombatNetworkActorRuntime>();
        var enemyIdentity = enemy.GetComponent<Unity.Netcode.NetworkObject>();
        if (networkVictim != null && enemyIdentity != null &&
            (networkVictim.ServerResolveReactionAtContact(enemyIdentity, generation) || networkVictim.IsProtectedFromEnemyAction(enemyIdentity, generation))) return;
        var reactions = manager.GetComponent<CombatHealthThresholdController>();
        bool local = target == manager.PlayerRoot;
        bool dodged = local && reactions != null && reactions.IsAttackDodged((ICombatTarget)enemy);
        var bridge = victim.GetComponent<LitOpsiveLocomotionBridge>();
        if (dodged || local && manager.GetComponent<CombatMobilityController>()?.IsDamageInvulnerable == true ||
            victim.GetComponent<PlayerScriptedDodgeController>()?.IsDamageInvulnerable == true ||
            bridge != null && bridge.IsCinematicMotionSessionActive ||
            !HasContact(enemy.transform, target, attack.reach, attack.arcDegrees, attack.heightTolerance))
        {
            if (local && dodged) reactions?.CloseExternalReactionAtContact(enemy);
            return;
        }
        bool guarded = PlayerGuardState.IsGuarding(victim) && Vector3.Dot(target.forward,
            Vector3.ProjectOnPlane(enemy.transform.position - target.position, Vector3.up).normalized) > 0;
        System.Action apply = () =>
        {
            if (local) reactions?.ResolvePendingAnimationCounter();
            if (enemy == null || enemy.IsDead || enemy.ActionSequenceId != generation || enemy.IsCinematicSuspended ||
                victim == null || !victim.isActiveAndEnabled || victim.CurrentHp <= 0 ||
                CombatCinematicReservations.ContainsParticipant(enemy.transform) || CombatCinematicReservations.ContainsParticipant(victim.transform)) return;
            // Pending counters are resolved by the late-frame arbiter before this closure.
            if (local)
            {
                bool avoided = reactions != null && reactions.IsAttackDodged((ICombatTarget)enemy);
                reactions?.CloseExternalReactionAtContact(enemy);
                if (avoided) return;
            }
            if (victim.GetComponent<PlayerScriptedDodgeController>()?.IsDamageInvulnerable == true) return;
            if (networkVictim != null && enemyIdentity != null &&
                (networkVictim.ServerResolveReactionAtContact(enemyIdentity, generation) || networkVictim.IsProtectedFromEnemyAction(enemyIdentity, generation))) return;
            var feedback = attack.skill.enemyAttackFeedback;
            bool ownGuardFeedback = feedback != null && (feedback.guardVfx != null || feedback.guardAudio != null);
            int damage = CombatProtectionDamage.ApplyAttack(victim, damageAmount, attack.skill.SkillName, guarded, !ownGuardFeedback);
            if (enemy.UsesAuthoredProductionCombat && (damage > 0 || guarded))
            {
                EnemyController.PlayOutcomeFeedback(attack.skill, target, guarded ? EnemyAttackOutcome.Guarded : EnemyAttackOutcome.Damaged);
                victim.GetComponent<CombatNetworkActorRuntime>()?.PublishEnemyImpact(enemy.transform, attack.skill, damage,
                    guarded ? EnemyAttackOutcome.Guarded : EnemyAttackOutcome.Damaged);
            }
            if (local) reactions?.NotifyFailureRetaliationImpact(enemy, attack.skill, damage);
            if (damage > 0 && victim.CurrentHp > 0 && !guarded)
                CombatActorPresentation.TryDamageReaction(victim);
        };
        if (GetComponent<CombatLabClashController>()?.QueueEnemyImpact(enemy, victim, apply) != true) apply();
    }

    public static bool HasContact(Transform attacker, Transform victim, float reach, float arc, float heightTolerance)
    {
        return CombatContactGeometry.HasContact(attacker, victim, reach, arc, heightTolerance);
    }

    private void Unsubscribe(LitBrainsEnemy enemy)
    {
        if (!subscriptions.TryGetValue(enemy, out var entry)) return;
        if (enemy != null) { enemy.AttackImpact -= entry.impact; enemy.ReactionOpportunity -= entry.reaction; }
        subscriptions.Remove(enemy);
    }

    private void OnDisable()
    {
        UnbindNetworkActor();
        var clash = GetComponent<CombatLabClashController>();
        if (clash != null && clash.UsesWorldActors) clash.Unbind();
        if (navigationWorld != null) navigationWorld.StateChanged -= OnWorldStateChanged;
        navigationWorld = null;
        ClearEngagements();
        LocalPlayerContext.LocalCharacterChanged -= BindPlayer;
        foreach (var enemy in new List<LitBrainsEnemy>(subscriptions.Keys)) Unsubscribe(enemy);
        manager?.EndCombat();
        manager?.ConfigureFreeAttacks(null, null);
    }
}
