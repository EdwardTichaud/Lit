using System;
using System.Collections;
using System.Collections.Generic;
using Lit.Combat.Migration.Network;
using UnityEngine;

public sealed partial class CombatLabClashController
{
    private bool worldMode;
    private readonly Dictionary<CombatLabClashActor, WorldActor> worldActors = new Dictionary<CombatLabClashActor, WorldActor>();
    private readonly List<WorldImpact> worldImpacts = new List<WorldImpact>();

    private sealed class WorldActor
    {
        public CombatLabClashActor volume;
        public SquadCharacterController player;
        public LitBrainsEnemy enemy;
        public PlayerActionPresentationController presentation;
        public LitOpsiveLocomotionBridge bridge;
        public int frame = -1, neutral = -1, session;
        public bool planar, recovering;
        public Coroutine routine;
    }

    private struct WorldImpact
    {
        public WorldActor actor;
        public int generation, binding;
        public Action apply;
    }

    public bool UsesWorldActors => worldMode;
    public event Action<Transform, Transform, int, int, Vector3> WorldClashResolved;

    public void BindWorld(RealTimeCombatManager manager)
    {
        if (worldMode && combat == manager) return;
        Unbind();
        if (manager == null) return;
        combat = manager;
        worldMode = true;
        if (!RegisterArbitrator()) Unbind();
    }

    // Called by the world scan, not by lock changes. Existing authored volumes keep their geometry.
    public void RefreshWorldActors()
    {
        if (!worldMode) return;
        foreach (var actor in FindObjectsByType<SquadCharacterController>())
            if (actor.CharacterData != null && !actor.CharacterData.isEnemy &&
                actor.GetComponent<PlayerActionPresentationController>() != null && actor.GetComponent<LitOpsiveLocomotionBridge>() != null)
                RegisterWorldActor(actor.transform, actor, null);
        foreach (var actor in FindObjectsByType<LitBrainsEnemy>()) RegisterWorldActor(actor.transform, null, actor);
        foreach (var entry in new List<WorldActor>(worldActors.Values))
            if (entry.volume == null || !entry.volume.gameObject.activeInHierarchy ||
                entry.player != null && !entry.player.isActiveAndEnabled || entry.enemy != null && !entry.enemy.isActiveAndEnabled)
                RemoveWorldActor(entry);
    }

    private void RegisterWorldActor(Transform root, SquadCharacterController playerActor, LitBrainsEnemy enemyActor)
    {
        var volume = root.GetComponent<CombatLabClashActor>();
        if (volume != null && volume.controller != null && volume.controller != this) return;
        if (volume == null) return; // Explicitly authored on the actor prefab before spawn.
        if (worldActors.ContainsKey(volume)) return;
        volume.controller = this;
        volume.ResetWindow(); volume.Sample();
        worldActors.Add(volume, new WorldActor {
            volume = volume, player = playerActor, enemy = enemyActor,
            presentation = playerActor != null ? root.GetComponent<PlayerActionPresentationController>() : null,
            bridge = playerActor != null ? root.GetComponent<LitOpsiveLocomotionBridge>() : null
        });
    }

    private bool WorldReady => worldMode && enableClashes && isActiveAndEnabled && combat != null &&
        CombatProtectionDamage.HasAuthority && Time.timeScale > 0;

    private bool ActorReady(WorldActor actor)
    {
        if (actor.volume == null || !actor.volume.isActiveAndEnabled || actor.recovering ||
            CombatCinematicReservations.ContainsParticipant(actor.volume.transform)) return false;
        if (actor.enemy != null)
            return actor.enemy.isActiveAndEnabled && !actor.enemy.IsDead && !actor.enemy.IsCinematicSuspended && actor.enemy.IsClashGrounded;
        return actor.player != null && actor.player.isActiveAndEnabled && actor.player.CurrentHp > 0 &&
            actor.presentation != null && actor.bridge != null && actor.bridge.Grounded && !actor.bridge.IsCinematicMotionSessionActive;
    }

    private bool PlayerClipCompatible(WorldActor actor, AnimationClip clip)
    {
        if (clip == null || actor.player == null || actor.player.CharacterData == null) return false;
        if (actor.player.CharacterData.groundBasicSkills != null)
            foreach (var skill in actor.player.CharacterData.groundBasicSkills)
                if (skill != null && skill.Context == BasicSkillContext.Grounded && skill.AnimationClip == clip) return true;
        foreach (var skill in compatiblePlayerSkills ?? Array.Empty<BasicSkillsSO>())
            if (skill != null && skill.Context == BasicSkillContext.Grounded && skill.AnimationClip == clip) return true;
        return false;
    }

    private bool AcceptWorldEvent(CombatLabClashActor volume, AnimationEvent source)
    {
        if (!WorldReady || source == null || !source.isFiredByAnimator || source.animatorClipInfo.weight <= 0 ||
            !worldActors.TryGetValue(volume, out var actor) || !ActorReady(actor)) return false;
        if (actor.enemy != null)
        {
            if (!actor.enemy.AcceptClashEvent(source)) return false;
            foreach (var state in compatibleEnemyStates ?? Array.Empty<string>())
                if (source.animatorStateInfo.shortNameHash == Animator.StringToHash(state)) return true;
            return false;
        }
        if (!actor.presentation.IsBasicSkillAction || !PlayerClipCompatible(actor, source.animatorClipInfo.clip)) return false;
        if (actor.presentation.AcceptAnimationEvent(source)) return true;
        // Network native attacks use externally owned sessions, not Timeline event admission.
        var context = actor.presentation.AttackContext;
        if (context == null || context.Generation != volume.Generation || context.Skill?.AnimationClip != source.animatorClipInfo.clip) return false;
        var animator = actor.player.GetComponent<Animator>();
        if (animator == null || !animator.isActiveAndEnabled) return false;
        int expected = Animator.StringToHash(context.Skill.AnimatorState);
        var current = animator.GetCurrentAnimatorStateInfo(0);
        bool transition = animator.IsInTransition(0);
        var next = transition ? animator.GetNextAnimatorStateInfo(0) : current;
        return source.animatorStateInfo.fullPathHash == expected &&
            !(transition && current.fullPathHash == expected && next.fullPathHash == expected) &&
            (current.fullPathHash == expected || next.fullPathHash == expected);
    }

    private void RecordWorldWindow(CombatLabClashActor volume)
    {
        if (worldActors.TryGetValue(volume, out var actor)) actor.frame = Time.frameCount;
    }

    // Authority callers capture their ticket/recipient before queuing and revalidate them in apply.
    public bool QueueActorImpact(Transform attacker, SkillSO skill, Action apply)
    {
        var volume = attacker != null ? attacker.GetComponent<CombatLabClashActor>() : null;
        if (!WorldReady || apply == null || volume == null || !worldActors.TryGetValue(volume, out var actor) ||
            !ActorReady(actor) || actor.player == null || !(skill is BasicSkillsSO basic) ||
            basic.Context != BasicSkillContext.Grounded || !actor.presentation.IsBasicSkillAction ||
            !PlayerClipCompatible(actor, skill.AnimationClip)) return false;
        QueueWorldImpact(actor, apply);
        return true;
    }

    private bool QueueWorldEnemyImpact(LitBrainsEnemy attacker, SquadCharacterController victim, Action apply)
    {
        var volume = attacker != null ? attacker.GetComponent<CombatLabClashActor>() : null;
        if (!WorldReady || victim == null || apply == null || volume == null ||
            !worldActors.TryGetValue(volume, out var actor) || !ActorReady(actor)) return false;
        var animator = attacker.Animator;
        if (animator == null) return false;
        int state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0).shortNameHash : animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
        foreach (var compatible in compatibleEnemyStates ?? Array.Empty<string>())
            if (state == Animator.StringToHash(compatible)) { QueueWorldImpact(actor, apply); return true; }
        return false;
    }

    private void QueueWorldImpact(WorldActor actor, Action apply) => worldImpacts.Add(new WorldImpact {
        actor = actor, generation = actor.volume.Generation, binding = bindingGeneration, apply = apply
    });

    private bool WindowActive(WorldActor actor) => ActorReady(actor) && actor.volume.WindowGeneration == actor.volume.Generation &&
        actor.neutral != actor.volume.Generation && (actor.volume.Open || actor.frame == Time.frameCount);

    private void TickWorldClashes()
    {
        if (!WorldReady) { CancelWorldClashes(); return; }
        var actors = new List<WorldActor>(worldActors.Values);
        actors.Sort((a, b) => StableActorId(a.volume).CompareTo(StableActorId(b.volume)));
        foreach (var actor in actors)
            if (actor.volume != null && actor.volume.WindowGeneration != actor.volume.Generation) actor.volume.ResetWindow();
        foreach (var p in actors)
        {
            if (p.player == null || !WindowActive(p)) continue;
            foreach (var e in actors)
            {
                if (e.enemy == null || !WindowActive(e) || !WindowActive(p)) continue;
                if (SweptContact(p.volume.PreviousCenter, p.volume.Center, p.volume.radius,
                    e.volume.PreviousCenter, e.volume.Center, e.volume.radius, out var contact) && ClearClashLine(p, e))
                    ResolveWorldClash(p, e, contact);
            }
        }
        var impacts = worldImpacts.ToArray(); worldImpacts.Clear();
        foreach (var impact in impacts)
            if (impact.binding == bindingGeneration && impact.actor.volume != null &&
                worldActors.ContainsKey(impact.actor.volume) && ActorReady(impact.actor) &&
                ShouldDeliver(impact.generation, impact.actor.volume.Generation, impact.actor.neutral)) impact.apply?.Invoke();
        foreach (var actor in actors) actor.volume?.Sample();
    }

    private static ulong StableActorId(CombatLabClashActor actor)
    {
        if (actor == null) return ulong.MaxValue;
        var identity = actor.GetComponent<Unity.Netcode.NetworkObject>();
        return identity != null && identity.IsSpawned ? identity.NetworkObjectId : (ulong)(uint)actor.GetEntityId().GetHashCode();
    }

    private static bool ClearClashLine(WorldActor a, WorldActor b)
    {
        Vector3 delta = b.volume.Center - a.volume.Center;
        if (delta.sqrMagnitude < .000001f) return true;
        foreach (var hit in Physics.RaycastAll(a.volume.Center, delta.normalized, delta.magnitude,
            LayerMask.GetMask("Default", "Ground", "Obstacle"), QueryTriggerInteraction.Ignore))
            if (!hit.transform.IsChildOf(a.volume.transform) && !hit.transform.IsChildOf(b.volume.transform)) return false;
        return true;
    }

    private void ResolveWorldClash(WorldActor p, WorldActor e, Vector3 contact)
    {
        Vector3 away = Vector3.ProjectOnPlane(p.player.transform.position - e.enemy.transform.position, Vector3.up).normalized;
        if (away.sqrMagnitude < .001f) away = -p.player.transform.forward;
        // The existing enemy motor must admit recovery before either attack is neutralized.
        int playerGeneration = p.volume.Generation, enemyGeneration = e.volume.Generation;
        if (!e.enemy.BeginClashRecovery(-away, recoilDistance, recoilSeconds, recoverySeconds)) return;
        p.neutral = playerGeneration; e.neutral = enemyGeneration;
        p.volume.ResetWindow(); e.volume.ResetWindow();
        e.recovering = true;
        e.routine = StartCoroutine(WaitEnemyRecovery(e));
        if (combat.PlayerRoot == p.player.transform)
        {
            combat.GetComponent<RealTimeCombatInput>()?.CancelBufferedBasicSkills();
            combat.GetComponent<SkillsManager>()?.ResetAllBasicSkillCombos();
        }
        combat.GetComponent<CombatHealthThresholdController>()?.CloseExternalReactionAtContact(e.enemy);
        p.presentation.CancelAction(); // Its termination hook also invalidates a network authority ticket.
        p.session = p.presentation.BeginExternalPresentation(this);
        if (p.session != 0)
        {
            p.recovering = true;
            p.planar = p.bridge.BeginScriptedPlanarMotion(p.volume);
            p.presentation.RegisterActionCleanup(p.session, () => CancelWorldActorRecovery(p));
            string state = CombatActorPresentation.DamageReactionState(p.player);
            var animator = p.player.GetComponent<Animator>();
            if (animator != null && p.presentation.CanPlayState(state)) animator.CrossFadeInFixedTime(state, ClashBlendSeconds, 0);
            p.routine = StartCoroutine(WorldRecoil(p, away));
        }
        ResolvedCount++;
        try { WorldClashResolved?.Invoke(p.player.transform, e.enemy.transform, playerGeneration, enemyGeneration, contact); }
        catch (Exception exception) { Debug.LogException(exception, this); }
        PlayClashCosmetics(contact);
        if (diagnostics) Debug.Log($"[CombatClash] World pair={p.volume.name}/{e.volume.name} generations={p.neutral}/{e.neutral}", this);
    }

    private IEnumerator WaitEnemyRecovery(WorldActor actor)
    {
        while (actor.enemy != null && actor.enemy.IsClashRecovering && !actor.enemy.IsDead && !actor.enemy.IsCinematicSuspended)
            yield return null;
        actor.routine = null;
        actor.recovering = false;
    }

    private IEnumerator WorldRecoil(WorldActor actor, Vector3 direction)
    {
        float elapsed = 0, duration = Mathf.Max(.01f, recoilSeconds);
        while (elapsed < duration + recoverySeconds && actor.presentation != null && actor.presentation.IsCurrentSession(actor.session) &&
            actor.player != null && actor.player.CurrentHp > 0 && !CombatCinematicReservations.ContainsParticipant(actor.player.transform))
        {
            yield return new WaitForFixedUpdate();
            var clock = actor.player.GetComponent<CombatTimeDomain>();
            float dt = clock != null ? clock.FixedDeltaTime : Time.fixedDeltaTime;
            float velocity = elapsed < duration ? 2 * recoilDistance / duration * (1 - elapsed / duration) : 0;
            Vector3 probe = actor.player.transform.position + direction * (velocity * dt + .15f) + Vector3.up * .3f;
            if (!Physics.Raycast(probe, Vector3.down, .75f, LayerMask.GetMask("Default", "Ground", "Obstacle"), QueryTriggerInteraction.Ignore)) velocity = 0;
            if (actor.planar) actor.bridge.DriveScriptedPlanarMotion(actor.volume, direction * velocity);
            elapsed += dt;
        }
        actor.routine = null;
        CancelWorldActorRecovery(actor);
    }

    private void CancelWorldActorRecovery(WorldActor actor)
    {
        if (!actor.recovering && !actor.planar && actor.session == 0) return;
        actor.recovering = false;
        if (actor.routine != null) { StopCoroutine(actor.routine); actor.routine = null; }
        if (actor.planar && actor.bridge != null)
        { actor.bridge.DriveScriptedPlanarMotion(actor.volume, Vector3.zero); actor.bridge.EndScriptedPlanarMotion(actor.volume); }
        actor.planar = false;
        if (actor.enemy != null) actor.enemy.CancelClashRecovery();
        int session = actor.session; actor.session = 0;
        if (actor.presentation != null && actor.presentation.IsCurrentSession(session)) actor.presentation.EndExternalPresentation(session);
    }

    public void ActorDisabled(CombatLabClashActor volume)
    {
        if (!worldMode) { Cancel(); return; }
        if (worldActors.TryGetValue(volume, out var actor)) RemoveWorldActor(actor);
    }

    private void RemoveWorldActor(WorldActor actor)
    {
        CancelWorldActorRecovery(actor);
        worldImpacts.RemoveAll(impact => impact.actor == actor);
        if (!ReferenceEquals(actor.volume, null)) worldActors.Remove(actor.volume);
        if (actor.volume != null && actor.volume.controller == this) { actor.volume.ResetWindow(); actor.volume.controller = null; }
    }

    private void CancelWorldClashes()
    {
        bindingGeneration++;
        worldImpacts.Clear();
        foreach (var actor in new List<WorldActor>(worldActors.Values))
        { actor.volume?.ResetWindow(); actor.frame = -1; CancelWorldActorRecovery(actor); }
    }

    private void ReleaseWorldActors()
    {
        foreach (var actor in new List<WorldActor>(worldActors.Values)) RemoveWorldActor(actor);
    }

    public float ClashBlendSeconds => .08f;

    public bool TryGetClashPresentation(out float recoil, out float recovery, out float distance)
    {
        recoil = recovery = distance = 0;
        if (!enableClashes || !isActiveAndEnabled || !Finite(recoilSeconds) || !Finite(recoverySeconds) || !Finite(recoilDistance)) return false;
        recoil = Mathf.Clamp(recoilSeconds, .01f, 10);
        recovery = Mathf.Clamp(recoverySeconds, 0, 10);
        distance = Mathf.Clamp(recoilDistance, 0, 10);
        return true;
    }

    public void PlayClashCosmetics(Vector3 contact)
    {
        if (!enableClashes || !isActiveAndEnabled || !Finite(contact.x) || !Finite(contact.y) || !Finite(contact.z)) return;
        if (hitVfx != null) Destroy(Instantiate(hitVfx, contact, Quaternion.identity), 2);
        if (contactAudio != null) AudioManager.PlayClipAtPoint(contactAudio, contact);
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
