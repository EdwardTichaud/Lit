using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Late-frame arbitration for this laboratory only. Production damage is unchanged.</summary>
[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public sealed class CombatLabClashController : MonoBehaviour
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
    private LucianBrainsCombatArena arena;
    private RealTimeCombatManager combat;
    private PlayerActionPresentationController presentation;
    private LitOpsiveLocomotionBridge bridge;
    private readonly List<PendingImpact> pending = new List<PendingImpact>();
    private int playerFrame = -1, enemyFrame = -1;
    private int neutralPlayer = -1, neutralEnemy = -1;
    private Coroutine recoil;
    private int recoilSession;
    private bool planarOwned;
    private bool cleaning;
    private int contactTraceGeneration = -1;
    private struct PendingImpact { public CombatLabClashActor actor; public int generation; public Action apply; }

    public void Bind(LucianBrainsCombatArena encounter, RealTimeCombatManager manager)
    {
        Cancel();
        recoilSession = 0;
        if (encounter.gameObject.scene.name != "Iluvilirae_Test") return;
        arena = encounter; combat = manager;
        playerVolume = encounter.Player.gameObject.AddComponent<CombatLabClashActor>();
        enemyVolume = encounter.Enemy.gameObject.AddComponent<CombatLabClashActor>();
        playerVolume.controller = enemyVolume.controller = this;
        playerVolume.localCenter = playerCenter; playerVolume.radius = playerRadius;
        enemyVolume.localCenter = enemyCenter; enemyVolume.radius = enemyRadius;
        OverrideClips(encounter.Player.GetComponent<Animator>(), playerClips);
        OverrideClips(encounter.Enemy.Animator, enemyClips);
        presentation = encounter.Player.GetComponent<PlayerActionPresentationController>();
        bridge = encounter.Player.GetComponent<LitOpsiveLocomotionBridge>();
        combat.ExternalImpactArbitrator = QueuePlayerImpact;
        neutralPlayer = neutralEnemy = -1;
        ResolvedCount = 0;
        contactTraceGeneration = -1;
    }
    private static void OverrideClips(Animator animator, ClipReplacement[] replacements)
    {
        var controller = new AnimatorOverrideController(animator.runtimeAnimatorController);
        var mappings = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        controller.GetOverrides(mappings);
        for (int i = 0; i < mappings.Count; i++)
            foreach (var replacement in replacements ?? Array.Empty<ClipReplacement>())
                if (mappings[i].Key == replacement.source)
                    mappings[i] = new KeyValuePair<AnimationClip, AnimationClip>(mappings[i].Key, replacement.replacement);
        controller.ApplyOverrides(mappings);
        animator.runtimeAnimatorController = controller;
        animator.gameObject.AddComponent<CombatLabClashOverrideLifetime>().controller = controller;
    }

    public bool Accept(CombatLabClashActor actor, AnimationEvent source)
    {
        if (!CanArbitrate || source == null) return false;
        if (actor == playerVolume)
        {
            if (!bridge.Grounded || !presentation.AcceptAnimationEvent(source)) return false;
            foreach (var skill in compatiblePlayerSkills ?? Array.Empty<BasicSkillsSO>())
                if (skill != null && skill.Context == BasicSkillContext.Grounded && skill.AnimationClip == source.animatorClipInfo.clip) return true;
            return false;
        }
        if (actor != enemyVolume || !arena.Enemy.IsClashGrounded || !arena.Enemy.AcceptClashEvent(source)) return false;
        foreach (var state in compatibleEnemyStates)
            if (source.animatorStateInfo.shortNameHash == Animator.StringToHash(state)) return true;
        return false;
    }
    private bool CanArbitrate => enableClashes && isActiveAndEnabled && arena != null && arena.Player != null &&
        arena.Enemy != null && !arena.EncounterOver && !arena.IsPaused && combat != null && !combat.IsCinematicSequenceActive;

    public void RecordWindow(CombatLabClashActor actor, bool open)
    {
        if (actor == playerVolume) playerFrame = Time.frameCount;
        if (actor == enemyVolume) enemyFrame = Time.frameCount;
    }
    private bool QueuePlayerImpact(SkillSO skill, Action apply)
    {
        if (!CanArbitrate) return false;
        bool compatible = false;
        foreach (var entry in compatiblePlayerSkills ?? Array.Empty<BasicSkillsSO>()) if (entry == skill) compatible = true;
        if (!compatible || !bridge.Grounded) return false;
        Enqueue(playerVolume, apply);
        return true;
    }
    public bool QueueEnemyImpact(Action apply)
    {
        if (!CanArbitrate) return false;
        Enqueue(enemyVolume, apply);
        return true;
    }
    private void Enqueue(CombatLabClashActor actor, Action apply) => pending.Add(new PendingImpact {
        actor = actor, generation = actor.Generation, apply = apply });

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
        if (!CanArbitrate) { Cancel(); return; }
        if (playerVolume.WindowGeneration != playerVolume.Generation) playerVolume.ResetWindow();
        if (enemyVolume.WindowGeneration != enemyVolume.Generation) enemyVolume.ResetWindow();
        bool playerActive = (playerVolume.Open || playerFrame == Time.frameCount) && playerVolume.WindowGeneration == playerVolume.Generation;
        bool enemyActive = (enemyVolume.Open || enemyFrame == Time.frameCount) && enemyVolume.WindowGeneration == enemyVolume.Generation;
        if (diagnostics && playerActive && enemyActive && contactTraceGeneration != playerVolume.Generation)
        {
            contactTraceGeneration = playerVolume.Generation;
            Debug.Log($"[CombatClash] Pair centers={playerVolume.Center}/{enemyVolume.Center} distance={Vector3.Distance(playerVolume.Center, enemyVolume.Center):F3} radius={playerVolume.radius + enemyVolume.radius:F3} ground={bridge.Grounded}/{arena.Enemy.IsClashGrounded}", this);
        }
        if (recoil == null && playerActive && enemyActive && bridge.Grounded && arena.Enemy.IsClashGrounded &&
            neutralPlayer != playerVolume.Generation && neutralEnemy != enemyVolume.Generation &&
            SweptContact(playerVolume.PreviousCenter, playerVolume.Center, playerVolume.radius,
                enemyVolume.PreviousCenter, enemyVolume.Center, enemyVolume.radius, out var contact)) Resolve(contact);
        var impacts = pending.ToArray(); pending.Clear();
        foreach (var impact in impacts)
        {
            int neutral = impact.actor == playerVolume ? neutralPlayer : neutralEnemy;
            if (impact.actor != null && ShouldDeliver(impact.generation, impact.actor.Generation, neutral) && CanArbitrate)
                impact.apply?.Invoke();
        }
        playerVolume.Sample(); enemyVolume.Sample();
    }

    private void Resolve(Vector3 contact)
    {
        ResolvedCount++;
        neutralPlayer = playerVolume.Generation; neutralEnemy = enemyVolume.Generation;
        playerVolume.ResetWindow(); enemyVolume.ResetWindow();
        GetComponent<RealTimeCombatInput>()?.CancelBufferedBasicSkills();
        FindAnyObjectByType<SkillsManager>()?.ResetAllBasicSkillCombos();
        presentation.CancelAction();
        recoilSession = presentation.BeginExternalPresentation(this);
        Vector3 away = Vector3.ProjectOnPlane(arena.Player.transform.position - arena.Enemy.transform.position, Vector3.up).normalized;
        arena.Enemy.BeginClashRecovery(-away, recoilDistance, recoilSeconds, recoverySeconds);
        GetComponent<CombatHealthThresholdController>()?.CloseExternalReactionAtContact(arena.Enemy);
        planarOwned = bridge.BeginScriptedPlanarMotion(this);
        presentation.RegisterActionCleanup(recoilSession, Cancel);
        var animator = arena.Player.GetComponent<Animator>();
        if (presentation.CanPlayState(arena.hurtState)) animator.CrossFadeInFixedTime(arena.hurtState, .08f, 0);
        if (hitVfx != null) Destroy(Instantiate(hitVfx, contact, Quaternion.identity), 2f);
        if (contactAudio != null) AudioManager.PlayClipAtPoint(contactAudio, contact);
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
            Vector3 probe = arena.Player.transform.position + direction * (velocity * Time.fixedDeltaTime + .15f) + Vector3.up * .3f;
            if (!Physics.Raycast(probe, Vector3.down, .75f, LayerMask.GetMask("Default", "Ground", "Obstacle"), QueryTriggerInteraction.Ignore)) velocity = 0;
            if (planarOwned) bridge.DriveScriptedPlanarMotion(this, direction * velocity);
            elapsed += Time.fixedDeltaTime;
        }
        recoil = null;
        presentation.EndExternalPresentation(recoilSession);
    }
    public void Cancel()
    {
        if (cleaning) return;
        cleaning = true;
        pending.Clear();
        playerVolume?.ResetWindow(); enemyVolume?.ResetWindow();
        playerFrame = enemyFrame = -1;
        if (recoil != null) { StopCoroutine(recoil); recoil = null; }
        if (planarOwned && bridge != null) { bridge.DriveScriptedPlanarMotion(this, Vector3.zero); bridge.EndScriptedPlanarMotion(this); }
        planarOwned = false;
        arena?.Enemy?.CancelClashRecovery();
        if (presentation != null && presentation.IsCurrentSession(recoilSession)) presentation.EndExternalPresentation(recoilSession);
        recoilSession = 0;
        cleaning = false;
    }
    private void OnDisable()
    {
        Cancel();
        if (combat != null && combat.ExternalImpactArbitrator == QueuePlayerImpact) combat.ExternalImpactArbitrator = null;
        presentation?.EndExternalPresentation(recoilSession);
    }
    private void OnEnable()
    {
        if (combat != null && arena != null && arena.Player != null && arena.Enemy != null)
            combat.ExternalImpactArbitrator = QueuePlayerImpact;
    }
}

internal sealed class CombatLabClashOverrideLifetime : MonoBehaviour
{
    public AnimatorOverrideController controller;
    private void OnDestroy() { if (controller != null) Destroy(controller); }
}
