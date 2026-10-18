using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Late-frame arbitration for an explicitly bound combat pair.</summary>
[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public sealed partial class CombatLabClashController : MonoBehaviour
{
    public bool enableClashes = true;
    public BasicSkillsSO[] compatiblePlayerSkills;
    public string[] compatibleEnemyStates = { "Attack", "Attack_Followup" };
    [Min(0)] public float recoilDistance = .6f;
    [Min(.01f)] public float recoilSeconds = .18f;
    [Min(0)] public float recoverySeconds = .15f;
    public GameObject hitVfx;
    public AudioClipSO contactAudio;
    public bool diagnostics;
    [Serializable] public struct ClipReplacement { public AnimationClip source, replacement; }
    public ClipReplacement[] playerClips, enemyClips;
    public LitBrainsCombatProfile labEnemyProfile;
    [HideInInspector] public int authoringVersion;
    public Vector3 playerCenter = new Vector3(0, 1.1f, .8f), enemyCenter = new Vector3(0, 1.1f, .8f);
    public float playerRadius = .55f, enemyRadius = .55f;
    public CombatLabClashActor playerVolume, enemyVolume;
    public int ResolvedCount { get; private set; }
    private SquadCharacterController player;
    private LitBrainsEnemy enemy;
    private string hurtState;
    private RealTimeCombatManager combat;
    private PlayerActionPresentationController presentation;
    private LitOpsiveLocomotionBridge bridge;
    private readonly List<PendingImpact> pending = new List<PendingImpact>();
    private readonly List<CombatLabClashOverrideLifetime> clipOverrides = new List<CombatLabClashOverrideLifetime>();
    private int playerFrame = -1, enemyFrame = -1;
    private int neutralPlayer = -1, neutralEnemy = -1;
    private Coroutine recoil;
    private int recoilSession;
    private bool planarOwned;
    private bool cleaning;
    private int contactTraceGeneration = -1;
    private int bindingGeneration;
    private bool enemyRecoveryOwned;
    public bool IsBound => worldMode ? combat != null : player != null && enemy != null && combat != null;
    private struct PendingImpact { public CombatLabClashActor actor; public int generation, binding; public Action apply; }
    private sealed class ImpactRouter
    {
        public readonly List<CombatLabClashController> pairs = new List<CombatLabClashController>();
        public bool Queue(SkillSO skill, Action apply)
        {
            foreach (var pair in pairs.ToArray())
                if (pair != null && pair.QueuePlayerImpact(skill, apply)) return true;
            return false;
        }
    }
    private static readonly Dictionary<RealTimeCombatManager, ImpactRouter> routers = new Dictionary<RealTimeCombatManager, ImpactRouter>();

    public void Bind(LucianBrainsCombatArena encounter, RealTimeCombatManager manager)
    {
        Bind(encounter != null ? encounter.Player : null, encounter != null ? encounter.Enemy : null,
            manager, encounter != null ? encounter.hurtState : null);
    }

    public void Bind(SquadCharacterController player, LitBrainsEnemy enemy, RealTimeCombatManager manager, string hurtState)
    {
        Unbind();
        if (player == null || enemy == null || manager == null) return;
        var playerActor = player.GetComponent<CombatLabClashActor>();
        var enemyActor = enemy.GetComponent<CombatLabClashActor>();
        if (playerActor == null || enemyActor == null)
        {
            Debug.LogWarning("[CombatClash] Missing authored CombatLabClashActor on a participant prefab.", this);
            return;
        }
        if (playerActor != null && playerActor.controller != null && playerActor.controller != this ||
            enemyActor != null && enemyActor.controller != null && enemyActor.controller != this)
        {
            Debug.LogWarning("[CombatClash] Pair binding refused: an actor already belongs to another pair.", this);
            return;
        }
        presentation = player.GetComponent<PlayerActionPresentationController>();
        bridge = player.GetComponent<LitOpsiveLocomotionBridge>();
        if (presentation == null || bridge == null) return;
        this.player = player; this.enemy = enemy; combat = manager; this.hurtState = hurtState;
        if (isActiveAndEnabled && !RegisterArbitrator()) { Unbind(); return; }
        playerVolume = playerActor;
        enemyVolume = enemyActor;
        playerVolume.controller = enemyVolume.controller = this;
        playerVolume.localCenter = playerCenter; playerVolume.radius = playerRadius;
        enemyVolume.localCenter = enemyCenter; enemyVolume.radius = enemyRadius;
        OverrideClips(player.GetComponent<Animator>(), playerClips);
        OverrideClips(enemy.Animator, enemyClips);
        playerVolume.ResetWindow(); enemyVolume.ResetWindow();
        playerVolume.Sample(); enemyVolume.Sample();
        neutralPlayer = neutralEnemy = -1;
        ResolvedCount = 0;
        contactTraceGeneration = -1;
    }
    private void OverrideClips(Animator animator, ClipReplacement[] replacements)
    {
        if (animator == null || animator.runtimeAnimatorController == null || replacements == null || replacements.Length == 0) return;
        var controller = new AnimatorOverrideController(animator.runtimeAnimatorController);
        var mappings = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        controller.GetOverrides(mappings);
        for (int i = 0; i < mappings.Count; i++)
            foreach (var replacement in replacements ?? Array.Empty<ClipReplacement>())
                if (mappings[i].Key == replacement.source)
                    mappings[i] = new KeyValuePair<AnimationClip, AnimationClip>(mappings[i].Key, replacement.replacement);
        controller.ApplyOverrides(mappings);
        var lifetime = animator.gameObject.AddComponent<CombatLabClashOverrideLifetime>();
        lifetime.animator = animator;
        lifetime.original = animator.runtimeAnimatorController;
        lifetime.controller = controller;
        clipOverrides.Add(lifetime);
        animator.runtimeAnimatorController = controller;
    }

    public bool Accept(CombatLabClashActor actor, AnimationEvent source)
    {
        if (worldMode) return AcceptWorldEvent(actor, source);
        if (!CanArbitrate || source == null) return false;
        if (actor == playerVolume)
        {
            if (!bridge.Grounded || !presentation.AcceptAnimationEvent(source)) return false;
            foreach (var skill in compatiblePlayerSkills ?? Array.Empty<BasicSkillsSO>())
                if (skill != null && skill.Context == BasicSkillContext.Grounded && skill.AnimationClip == source.animatorClipInfo.clip) return true;
            return false;
        }
        if (actor != enemyVolume || !enemy.IsClashGrounded || !enemy.AcceptClashEvent(source)) return false;
        foreach (var state in compatibleEnemyStates ?? Array.Empty<string>())
            if (source.animatorStateInfo.shortNameHash == Animator.StringToHash(state)) return true;
        return false;
    }
    private bool CanArbitrate => enableClashes && isActiveAndEnabled && IsBound && playerVolume != null && enemyVolume != null &&
        player.isActiveAndEnabled && enemy.isActiveAndEnabled && player.CurrentHp > 0 && !enemy.IsDead &&
        playerVolume.isActiveAndEnabled && enemyVolume.isActiveAndEnabled && presentation != null && bridge != null &&
        combat.IsCombatActive && Time.timeScale > 0 && !combat.IsCinematicSequenceActive;
    private bool IsCurrentPlayerTarget => combat != null && player != null && enemy != null &&
        combat.PlayerRoot == player.transform && combat.CombatTarget?.Root == enemy.transform;

    public void RecordWindow(CombatLabClashActor actor, bool open)
    {
        if (worldMode) { RecordWorldWindow(actor); return; }
        if (actor == playerVolume) playerFrame = Time.frameCount;
        if (actor == enemyVolume) enemyFrame = Time.frameCount;
    }
    private bool QueuePlayerImpact(SkillSO skill, Action apply)
    {
        if (worldMode) return QueueActorImpact(combat != null ? combat.PlayerRoot : null, skill, apply);
        if (!CanArbitrate || !IsCurrentPlayerTarget || apply == null) return false;
        bool compatible = false;
        foreach (var entry in compatiblePlayerSkills ?? Array.Empty<BasicSkillsSO>()) if (entry == skill) compatible = true;
        if (!compatible || !bridge.Grounded || !(skill is BasicSkillsSO basic) || basic.Context != BasicSkillContext.Grounded ||
            !presentation.IsBasicSkillAction) return false;
        Enqueue(playerVolume, apply);
        return true;
    }
    public bool QueueEnemyImpact(Action apply)
    {
        return QueueEnemyImpact(enemy, player, apply);
    }
    public bool QueueEnemyImpact(LitBrainsEnemy attacker, SquadCharacterController victim, Action apply)
    {
        if (worldMode) return QueueWorldEnemyImpact(attacker, victim, apply);
        if (!CanArbitrate || attacker != enemy || victim != player || apply == null || !enemy.IsClashGrounded ||
            enemy.CommittedAttackTarget != player.transform || !IsCompatibleEnemyState()) return false;
        Enqueue(enemyVolume, apply);
        return true;
    }
    private bool IsCompatibleEnemyState()
    {
        if (enemy.Animator == null) return false;
        int stateHash = enemy.Animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
        foreach (var state in compatibleEnemyStates ?? Array.Empty<string>())
            if (stateHash == Animator.StringToHash(state)) return true;
        return false;
    }
    private void Enqueue(CombatLabClashActor actor, Action apply) => pending.Add(new PendingImpact {
        actor = actor, generation = actor.Generation, binding = bindingGeneration, apply = apply });

    public static bool SweptContact(Vector3 a0, Vector3 a1, float ar, Vector3 b0, Vector3 b1, float br, out Vector3 contact)
    {
        Vector3 delta = a0 - b0, velocity = (a1 - a0) - (b1 - b0);
        float t = velocity.sqrMagnitude > .000001f ? Mathf.Clamp01(-Vector3.Dot(delta, velocity) / velocity.sqrMagnitude) : 0;
        Vector3 a = Vector3.Lerp(a0, a1, t), b = Vector3.Lerp(b0, b1, t);
        contact = (a + b) * .5f;
        return (a - b).sqrMagnitude <= (ar + br) * (ar + br);
    }
    public static bool ShouldDeliver(int queuedGeneration, int currentGeneration, int neutralizedGeneration) =>
        queuedGeneration == currentGeneration && queuedGeneration != neutralizedGeneration;

    private void LateUpdate()
    {
        combat?.GetComponent<CombatHealthThresholdController>()?.ResolvePendingAnimationCounter();
        if (worldMode) { TickWorldClashes(); return; }
        if (!CanArbitrate) { Cancel(); return; }
        if (playerVolume.WindowGeneration != playerVolume.Generation) playerVolume.ResetWindow();
        if (enemyVolume.WindowGeneration != enemyVolume.Generation) enemyVolume.ResetWindow();
        bool playerActive = (playerVolume.Open || playerFrame == Time.frameCount) && playerVolume.WindowGeneration == playerVolume.Generation;
        bool enemyActive = (enemyVolume.Open || enemyFrame == Time.frameCount) && enemyVolume.WindowGeneration == enemyVolume.Generation;
        if (diagnostics && playerActive && enemyActive && contactTraceGeneration != playerVolume.Generation)
        {
            contactTraceGeneration = playerVolume.Generation;
            Debug.Log($"[CombatClash] Pair centers={playerVolume.Center}/{enemyVolume.Center} distance={Vector3.Distance(playerVolume.Center, enemyVolume.Center):F3} radius={playerVolume.radius + enemyVolume.radius:F3} ground={bridge.Grounded}/{enemy.IsClashGrounded}", this);
        }
        if (recoil == null && playerActive && enemyActive && IsCurrentPlayerTarget && enemy.CommittedAttackTarget == player.transform &&
            bridge.Grounded && enemy.IsClashGrounded &&
            neutralPlayer != playerVolume.Generation && neutralEnemy != enemyVolume.Generation &&
            SweptContact(playerVolume.PreviousCenter, playerVolume.Center, playerVolume.radius,
                enemyVolume.PreviousCenter, enemyVolume.Center, enemyVolume.radius, out var contact)) Resolve(contact);
        var impacts = pending.ToArray(); pending.Clear();
        foreach (var impact in impacts)
        {
            int neutral = impact.actor == playerVolume ? neutralPlayer : neutralEnemy;
            if (impact.binding == bindingGeneration && impact.actor != null && impact.actor.controller == this &&
                ShouldDeliver(impact.generation, impact.actor.Generation, neutral) && CanArbitrate &&
                (impact.actor == playerVolume ? IsCurrentPlayerTarget : enemy.CommittedAttackTarget == player.transform))
                impact.apply?.Invoke();
        }
        playerVolume?.Sample(); enemyVolume?.Sample();
    }

    private void Resolve(Vector3 contact)
    {
        ResolvedCount++;
        neutralPlayer = playerVolume.Generation; neutralEnemy = enemyVolume.Generation;
        playerVolume.ResetWindow(); enemyVolume.ResetWindow();
        combat.GetComponent<RealTimeCombatInput>()?.CancelBufferedBasicSkills();
        combat.GetComponent<SkillsManager>()?.ResetAllBasicSkillCombos();
        presentation.CancelAction();
        recoilSession = presentation.BeginExternalPresentation(this);
        Vector3 away = Vector3.ProjectOnPlane(player.transform.position - enemy.transform.position, Vector3.up).normalized;
        enemyRecoveryOwned = enemy.BeginClashRecovery(-away, recoilDistance, recoilSeconds, recoverySeconds);
        combat.GetComponent<CombatHealthThresholdController>()?.CloseExternalReactionAtContact(enemy);
        planarOwned = bridge.BeginScriptedPlanarMotion(this);
        presentation.RegisterActionCleanup(recoilSession, Cancel);
        var animator = player.GetComponent<Animator>();
        if (animator != null && !string.IsNullOrEmpty(hurtState) && presentation.CanPlayState(hurtState)) animator.CrossFadeInFixedTime(hurtState, ClashBlendSeconds, 0);
        PlayClashCosmetics(contact);
        if (diagnostics) Debug.Log($"[CombatClash] Neutralized player={neutralPlayer} enemy={neutralEnemy} contact={contact}", this);
        recoil = StartCoroutine(Recoil(away));
    }
    private IEnumerator Recoil(Vector3 direction)
    {
        float elapsed = 0;
        while (elapsed < recoilSeconds + recoverySeconds && presentation.IsCurrentSession(recoilSession))
        {
            yield return new WaitForFixedUpdate();
            float velocity = elapsed < recoilSeconds ? 2 * recoilDistance / recoilSeconds * (1 - elapsed / recoilSeconds) : 0;
            Vector3 probe = player.transform.position + direction * (velocity * Time.fixedDeltaTime + .15f) + Vector3.up * .3f;
            if (!Physics.Raycast(probe, Vector3.down, .75f, LayerMask.GetMask("Default", "Ground", "Obstacle"), QueryTriggerInteraction.Ignore)) velocity = 0;
            if (planarOwned) bridge.DriveScriptedPlanarMotion(this, direction * velocity);
            elapsed += Time.fixedDeltaTime;
        }
        recoil = null;
        presentation.EndExternalPresentation(recoilSession);
    }
    public void Cancel()
    {
        if (worldMode) { CancelWorldClashes(); return; }
        if (cleaning) return;
        cleaning = true;
        bindingGeneration++;
        pending.Clear();
        playerVolume?.ResetWindow(); enemyVolume?.ResetWindow();
        playerFrame = enemyFrame = -1;
        if (recoil != null) { StopCoroutine(recoil); recoil = null; }
        if (planarOwned && bridge != null) { bridge.DriveScriptedPlanarMotion(this, Vector3.zero); bridge.EndScriptedPlanarMotion(this); }
        planarOwned = false;
        if (enemyRecoveryOwned && enemy != null) enemy.CancelClashRecovery();
        enemyRecoveryOwned = false;
        if (presentation != null && presentation.IsCurrentSession(recoilSession)) presentation.EndExternalPresentation(recoilSession);
        recoilSession = 0;
        cleaning = false;
    }
    private void OnDisable()
    {
        Cancel();
        UnregisterArbitrator();
    }
    private void OnEnable()
    {
        if (IsBound && !RegisterArbitrator()) Unbind();
    }
    public void Unbind()
    {
        Cancel();
        UnregisterArbitrator();
        ReleaseWorldActors();
        worldMode = false;
        foreach (var lifetime in clipOverrides)
            if (lifetime != null) { lifetime.Restore(); Destroy(lifetime); }
        clipOverrides.Clear();
        if (playerVolume != null && playerVolume.controller == this) playerVolume.controller = null;
        if (enemyVolume != null && enemyVolume.controller == this) enemyVolume.controller = null;
        playerVolume = enemyVolume = null;
        player = null; enemy = null; combat = null; presentation = null; bridge = null;
        neutralPlayer = neutralEnemy = -1;
    }
    private bool RegisterArbitrator()
    {
        if (combat == null) return false;
        routers.TryGetValue(combat, out var router);
        if (combat.ExternalImpactArbitrator != null && (router == null || combat.ExternalImpactArbitrator != router.Queue))
        {
            Debug.LogWarning("[CombatClash] Pair binding refused: manager arbitration is owned by another system.", this);
            return false;
        }
        if (router == null) routers.Add(combat, router = new ImpactRouter());
        if (!router.pairs.Contains(this)) router.pairs.Add(this);
        combat.ExternalImpactArbitrator = router.Queue;
        return true;
    }
    private void UnregisterArbitrator()
    {
        if (ReferenceEquals(combat, null) || !routers.TryGetValue(combat, out var router)) return;
        router.pairs.Remove(this);
        if (router.pairs.Count != 0) return;
        if (combat != null && combat.ExternalImpactArbitrator == router.Queue) combat.ExternalImpactArbitrator = null;
        routers.Remove(combat);
    }
    private void OnDestroy() => Unbind();
}

internal sealed class CombatLabClashOverrideLifetime : MonoBehaviour
{
    public AnimatorOverrideController controller;
    public Animator animator;
    public RuntimeAnimatorController original;
    public void Restore()
    {
        if (controller != null && animator != null && animator.runtimeAnimatorController == controller) animator.runtimeAnimatorController = original;
        if (controller != null) Destroy(controller);
        controller = null;
    }
    private void OnDestroy() => Restore();
}
