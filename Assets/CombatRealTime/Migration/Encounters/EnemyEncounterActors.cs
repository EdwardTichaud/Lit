using UnityEngine;

public static class EnemyEncounterActors
{
    public static bool TryEngageIntroducedPlayer(IEnemyEncounterActor actor, SquadCharacterController participant)
    {
        var network = Unity.Netcode.NetworkManager.Singleton;
        if (network != null && network.IsListening && !network.IsServer ||
            participant == null || !participant.isActiveAndEnabled || participant.CurrentHp <= 0 || !IsAvailable(actor)) return false;
        CombatWorldRuntime world = null;
        foreach (var candidate in Object.FindObjectsByType<CombatWorldRuntime>(FindObjectsSortMode.None))
        {
            if (!candidate.isActiveAndEnabled) continue;
            if (world != null) return false;
            world = candidate;
        }
        return world != null && world.TryEngageActor(participant, actor.EncounterTarget);
    }

    public static bool IsAlive(IEnemyEncounterActor actor) => actor != null &&
        (!(actor is Object owner) || owner != null);

    /// <summary>Resolve an existing actor on this root, without creating or activating one.</summary>
    public static IEnemyEncounterActor Resolve(Component source)
    {
        if (source == null) return null;
        IEnemyEncounterActor found = null;
        foreach (var component in source.GetComponents<MonoBehaviour>())
        {
            if (!(component is IEnemyEncounterActor actor)) continue;
            // Two brains are an invalid migration, not an invitation to pick one silently.
            if (found != null) return null;
            found = actor;
        }
        return found;
    }

    public static bool IsAvailable(IEnemyEncounterActor actor)
    {
        if (!IsAlive(actor) || actor.EncounterRoot == null || !actor.EncounterRoot.gameObject.activeInHierarchy ||
            actor is Behaviour behaviour && !behaviour.isActiveAndEnabled) return false;
        var boss = actor.BossEncounter;
        if (boss is Behaviour bossBehaviour && !bossBehaviour.isActiveAndEnabled) return false;
        if (boss is BossEncounterBehaviour typedBoss && !ReferenceEquals(typedBoss.EncounterActor, actor)) return false;
        var target = actor.EncounterTarget;
        return actor.IsEncounterAvailable && !actor.IsFlameDormant && target != null &&
            target.Root == actor.EncounterRoot && !target.IsDead;
    }

    /// <summary>Open local presentation only; never enables a brain or bypasses an introduction.</summary>
    public static bool BeginLocalCombat(IEnemyEncounterActor actor, RealTimeCombatManager manager, Transform player)
    {
        if (manager == null || player == null || !IsAvailable(actor) || manager.IsCinematicSequenceActive) return false;
        var boss = actor.BossEncounter;
        if (boss != null && (!boss.IsBossEngaged || boss.IsBossResolved)) return false;
        var character = player.GetComponent<SquadCharacterController>();
        if (character != null && character.CurrentHp <= 0) return false;
        // Boss state is global; only the owner's confirmed snapshot may open its production UI.
        if (manager.AllowsFreeAttacks || Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
            return IsPresenting(actor, manager, player);
        if (manager.IsCombatActive) return false;
        if (actor is EnemyController legacy)
        {
            if (!manager.BeginCombat(player, legacy)) return false;
            manager.BeginEnemyAggro(player, legacy);
        }
        else manager.BeginExternalEncounter(player, actor.EncounterTarget);
        return manager.IsCombatActive && manager.CombatTarget?.Root == actor.EncounterRoot;
    }

    public static bool IsPresenting(IEnemyEncounterActor actor, RealTimeCombatManager manager, Transform player)
    {
        if (!IsAvailable(actor) || manager == null || !manager.IsCombatActive || manager.PlayerRoot != player) return false;
        var boss = actor.BossEncounter;
        if (boss != null && (!boss.IsBossEngaged || boss.IsBossResolved)) return false;
        if (manager.AllowsFreeAttacks || Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
            return manager.HasConfirmedEncounterThreat(player, actor.EncounterRoot);
        return manager.EngagedEnemy != null && manager.EngagedEnemy.transform == actor.EncounterRoot ||
            manager.CombatTarget?.Root == actor.EncounterRoot;
    }

    /// <summary>A bespoke boss may own engagement while deliberately suppressing ordinary AI.</summary>
    public static bool IsEngagedWith(IEnemyEncounterActor actor, Transform player)
    {
        if (player == null || !IsAvailable(actor)) return false;
        var boss = actor.BossEncounter;
        if (boss != null) return boss.IsBossEngaged && !boss.IsBossResolved;
        if (actor is LitBrainsEnemy brain) return brain.IsEngagedWith(player);
        if (actor is EnemyController legacy)
            return !legacy.ShouldEndCombatForPursuit && (legacy.HasActiveBattleWall || !legacy.IsPositionOutsidePursuitZone(player.position));
        return false;
    }

    public static bool TryFilterIncomingDamage(IEnemyEncounterActor actor, int incomingDamage,
        SquadCharacterController source, out int permittedDamage)
    {
        permittedDamage = 0;
        if (!IsAvailable(actor)) return false;
        int requested = Mathf.Max(0, incomingDamage);
        var boss = actor.BossEncounter;
        if (boss == null) { permittedDamage = requested; return true; }
        if (!boss.IsBossEngaged || boss.IsBossResolved) return false;
        if (!boss.FilterIncomingDamage(requested, source, out int filtered)) return false;
        permittedDamage = Mathf.Clamp(filtered, 0, requested);
        return true;
    }
}
