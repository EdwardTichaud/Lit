using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public sealed partial class AnimationEvents
{
    [Header("Event destinations")]
    [SerializeField] private Transform actorOwner;
    [SerializeField] private Transform signalTarget;
    [SerializeField] private Transform cameraRoot;
    [SerializeField] private bool previewOnly;
    [SerializeField] private string lastSignal;
    private readonly HashSet<string> missingDestinations = new HashSet<string>();
    public string LastSignal => lastSignal;
    public bool PreviewOnly { get => previewOnly; set => previewOnly = value; }

    private T ResolveLocal<T>() where T : Component
    {
        var director = GetComponent<PlayableDirector>();
        var local = GetComponent<T>();
        if (director != null && local != null) return local;
        var explicitOwner = signalTarget != null ? signalTarget : actorOwner;
        if (explicitOwner != null) return explicitOwner.GetComponent<T>() ?? explicitOwner.GetComponentInParent<T>() ?? explicitOwner.GetComponentInChildren<T>(true);
        if (director != null && director.playableAsset != null)
        {
            T boundOwner = null;
            foreach (var output in director.playableAsset.outputs)
            {
                var binding = director.GetGenericBinding(output.sourceObject);
                var root = binding is Component component ? component.gameObject : binding as GameObject;
                if (root == null) continue;
                var candidate = root.GetComponent<T>() ?? root.GetComponentInParent<T>() ?? root.GetComponentInChildren<T>(true);
                if (candidate == null) continue;
                if (boundOwner != null && boundOwner != candidate) { Missing("Ambiguous Timeline target: " + typeof(T).Name); return null; }
                boundOwner = candidate;
            }
            if (boundOwner != null) return boundOwner;
        }
        if (director != null) return local ?? GetComponentInParent<T>();
        return GetComponent<T>() ?? GetComponentInParent<T>() ?? GetComponentInChildren<T>(true);
    }
    private bool Preview(string method)
    {
        if (Application.isPlaying && !previewOnly) return false;
        lastSignal = method;
#if UNITY_EDITOR
        GetComponent<LightSkillTimelinePreviewSignals>()?.RecordSignal(method);
#endif
        return true;
    }
    private void Missing(string method)
    {
        if (missingDestinations.Add(method)) Debug.LogWarning("[AnimationEvents] " + method + ": destination missing on " + name + ". Assign its owner/target in the Inspector.", this);
    }
    private void Route<T>(Action<T> action, string method) where T : Component
    {
        if (Preview(method)) return;
        T owner = ResolveLocal<T>();
        if (owner == null) { Missing(method); return; }
        if (owner is Behaviour behaviour && !behaviour.isActiveAndEnabled) return;
        action(owner);
    }
    private void OnDisable() => missingDestinations.Clear();

    public void KnockedOut()
    {
        if (Preview(nameof(KnockedOut))) return;
        if (signalTarget != null)
        {
            foreach (var component in signalTarget.GetComponentsInChildren<MonoBehaviour>(true))
                if (component is ICombatKnockoutReceiver target) { target.KnockedOut(); return; }
            Missing(nameof(KnockedOut)); return;
        }
        var rig = GetComponent<CombatCinematicRig>();
        if (rig != null) { rig.ApplyKnockedOut(); return; }
        var actor = ResolveLocal<LitBrainsEnemy>();
        if (actor != null) { ((ICombatKnockoutReceiver)actor).KnockedOut(); return; }
        Missing(nameof(KnockedOut));
    }
    public void CameraShake()
    {
        if (Preview(nameof(CameraShake))) return;
        if (cameraRoot != null)
        {
            var shakes = cameraRoot.GetComponentsInChildren<CombatCinematicCameraShake>(true);
            if (shakes.Length == 0) Missing(nameof(CameraShake));
            foreach (var shake in shakes) shake.PlayShake();
            return;
        }
        var rig = GetComponent<CombatCinematicRig>();
        if (rig != null) { rig.PlayCameraShake(); return; }
        Missing(nameof(CameraShake));
    }
    public void SpawnProjectile() => Route<LightSkillCinematicSequenceController>(o => o.SpawnProjectile(), nameof(SpawnProjectile));
    public void LaunchProjectile() => Route<LightSkillCinematicSequenceController>(o => o.LaunchProjectile(), nameof(LaunchProjectile));
    public void SpawnImpactVfx() => Route<LightSkillCinematicSequenceController>(o => o.SpawnImpactVfx(), nameof(SpawnImpactVfx));
    public void ResolveDamage() => Route<LightSkillCinematicSequenceController>(o => o.ResolveImpact(), nameof(ResolveDamage));
    public void StartSequence() { if (!Preview(nameof(StartSequence))) Missing(nameof(StartSequence)); }
    public void RearShot() { if (!Preview(nameof(RearShot))) Missing(nameof(RearShot)); }
    public void Impulse() { if (!Preview(nameof(Impulse))) Missing(nameof(Impulse)); }
    public void SetActive(bool value) { if (!Preview(nameof(SetActive))) (signalTarget != null ? signalTarget.gameObject : gameObject).SetActive(value); }
    public void SetLeverAnimatorInactive() => Route<Lever>(o => o.HandleSetLeverAnimatorInactive(), nameof(SetLeverAnimatorInactive));

    public void EnemyAttack(SkillSO skill) => Route<EnemyController>(o => o.HandleEnemyAttack(skill), nameof(EnemyAttack));
    public void LockEnemyAttackDirection() => Route<EnemyController>(o => o.HandleLockEnemyAttackDirection(), nameof(LockEnemyAttackDirection));
    public void OpenEnemyReactionOpportunity() => Route<EnemyController>(o => o.HandleOpenEnemyReactionOpportunity(), nameof(OpenEnemyReactionOpportunity));
    public void EndEnemyAttack() => Route<EnemyController>(o => o.HandleEndEnemyAttack(), nameof(EndEnemyAttack));
    public void BeginEnemyRush() => Route<EnemyController>(o => o.HandleBeginEnemyRush(), nameof(BeginEnemyRush));
    public void EndEnemyRush() => Route<EnemyController>(o => o.HandleEndEnemyRush(), nameof(EndEnemyRush));
    public void BeginEnemyAirborne() => Route<EnemyController>(o => o.HandleBeginEnemyAirborne(), nameof(BeginEnemyAirborne));
    public void RequestEnemyLanding() => Route<EnemyController>(o => o.HandleRequestEnemyLanding(), nameof(RequestEnemyLanding));
    public void BeginEnemyAdvance() => Route<EnemyController>(o => o.HandleBeginEnemyAdvance(), nameof(BeginEnemyAdvance));
    public void EndEnemyAdvance() => Route<EnemyController>(o => o.HandleEndEnemyAdvance(), nameof(EndEnemyAdvance));
    public void ShowInput(Sprite input) => Route<EnemyController>(o => o.HandleShowInput(input), nameof(ShowInput));
    public void HideInput() => Route<EnemyController>(o => o.HandleHideInput(), nameof(HideInput));
    public void OpenBrainsReactionOpportunity(AnimationEvent source) => Route<LitBrainsEnemy>(o => o.HandleOpenBrainsReactionOpportunity(source), nameof(OpenBrainsReactionOpportunity));
    public void ResolveBrainsAttackImpact(AnimationEvent source) => Route<LitBrainsEnemy>(o => o.HandleResolveBrainsAttackImpact(source), nameof(ResolveBrainsAttackImpact));
    public void BeginBrainsStrike(AnimationEvent source) => Route<LitBrainsEnemy>(o => o.HandleBeginBrainsStrike(source), nameof(BeginBrainsStrike));
    public void BeginBrainsRecovery(AnimationEvent source) => Route<LitBrainsEnemy>(o => o.HandleBeginBrainsRecovery(source), nameof(BeginBrainsRecovery));
    public void TriggerHolyEffect() => Route<SpiritBondAnimationActions>(o => o.HandleTriggerHolyEffect(), nameof(TriggerHolyEffect));
    public void PlayEffect_CharacterEffect() => Route<SpiritBondAnimationActions>(o => o.HandlePlayEffect_CharacterEffect(), nameof(PlayEffect_CharacterEffect));
    public void StopEffect_CharacterEffect() => Route<SpiritBondAnimationActions>(o => o.HandleStopEffect_CharacterEffect(), nameof(StopEffect_CharacterEffect));
    public void StopEffect() => Route<SpiritBondAnimationActions>(o => o.HandleStopEffect(), nameof(StopEffect));
    public void ConfirmMeltFusion() => Route<SpiritBondAnimationActions>(o => o.HandleConfirmMeltFusion(), nameof(ConfirmMeltFusion));
    public void ConfirmRuptureDefusion() => Route<SpiritBondAnimationActions>(o => o.HandleConfirmRuptureDefusion(), nameof(ConfirmRuptureDefusion));
    public void InstantiateAtSpine() => Route<SpiritBondAnimationActions>(o => o.HandleInstantiateAtSpine(), nameof(InstantiateAtSpine));
    public void PlayFootstepLeft() => Route<LocomotionAnimationEvent>(o => o.HandlePlayFootstepLeft(), nameof(PlayFootstepLeft));
    public void PlayFootstepRight() => Route<LocomotionAnimationEvent>(o => o.HandlePlayFootstepRight(), nameof(PlayFootstepRight));
    public void FootstepLeft() => Route<LocomotionAnimationEvent>(o => o.HandleFootstepLeft(), nameof(FootstepLeft));
    public void FootstepRight() => Route<LocomotionAnimationEvent>(o => o.HandleFootstepRight(), nameof(FootstepRight));
    public void LeftFootstep() => Route<LocomotionAnimationEvent>(o => o.HandleLeftFootstep(), nameof(LeftFootstep));
    public void RightFootstep() => Route<LocomotionAnimationEvent>(o => o.HandleRightFootstep(), nameof(RightFootstep));
    public void PlayFootstep(AnimationEvent source) => Route<LocomotionAnimationEvent>(o => o.HandlePlayFootstep(source), nameof(PlayFootstep));
    public void PlayFootstep() => Route<LocomotionAnimationEvent>(o => o.HandlePlayFootstep(), nameof(PlayFootstep));
    public void OnFootstep(AnimationEvent source) => Route<LocomotionAnimationEvent>(o => o.HandleOnFootstep(source), nameof(OnFootstep));
    public void SignalPlayFootstep() => Route<LocomotionAnimationEvent>(o => o.HandlePlayFootstep(), nameof(SignalPlayFootstep));
    public void SignalOnFootstep() => SignalPlayFootstep();
    public void SignalFootstep(int footIndex) => Route<LocomotionAnimationEvent>(o => o.HandleFootstep(footIndex), nameof(SignalFootstep));
    public void Footstep() => Route<LocomotionAnimationEvent>(o => o.HandleFootstep(), nameof(Footstep));
    public void Footstep(int footIndex) => Route<LocomotionAnimationEvent>(o => o.HandleFootstep(footIndex), nameof(Footstep));
    public void Footstep(string footName) => Route<LocomotionAnimationEvent>(o => o.HandleFootstep(footName), nameof(Footstep));
    public void SignalQTE(string input) { if (Preview(nameof(SignalQTE))) return; if (Presentation != null) Presentation.HandleQTE(input); else Route<EnemyController>(o => o.HandleQTE(input), nameof(SignalQTE)); }
    public void SignalResolveLightSkillImpact() => Route<PlayerActionPresentationController>(o => o.HandleResolveLightSkillImpact(), nameof(SignalResolveLightSkillImpact));
    public void SignalResolveCounterSkillImpact() => Route<PlayerActionPresentationController>(o => o.HandleResolveCounterSkillImpact(), nameof(SignalResolveCounterSkillImpact));
    public void SignalResolveCinematicSkillImpact() => Route<PlayerActionPresentationController>(o => o.HandleResolveCinematicSkillImpact(), nameof(SignalResolveCinematicSkillImpact));
    public void SignalInstantiateSkillVFX() => Route<PlayerActionPresentationController>(o => o.HandleInstantiateSkillVFX(), nameof(SignalInstantiateSkillVFX));
    public void SignalInstantiateSkillVFXAtIndex(int index) => Route<PlayerActionPresentationController>(o => o.HandleInstantiateSkillVFXAtIndex(index), nameof(SignalInstantiateSkillVFXAtIndex));
    public void SignalShowBow() => Route<PlayerActionPresentationController>(o => o.HandleShowBow(), nameof(SignalShowBow));
    public void SignalHideBow() => Route<PlayerActionPresentationController>(o => o.HandleHideBow(), nameof(SignalHideBow));
    public void SignalShowSword() => Route<PlayerActionPresentationController>(o => o.HandleShowSword(), nameof(SignalShowSword));
    public void SignalHideSword() => Route<PlayerActionPresentationController>(o => o.HandleHideSword(), nameof(SignalHideSword));
    public void SignalHideSwordWhenComboEnds() => Route<PlayerActionPresentationController>(o => o.HandleHideSwordWhenComboEnds(), nameof(SignalHideSwordWhenComboEnds));
    public void SignalDash() => Route<PlayerActionPresentationController>(o => o.HandleDash(), nameof(SignalDash));
    public void SignalStopDash() => Route<PlayerActionPresentationController>(o => o.HandleStopDash(), nameof(SignalStopDash));
    public void SignalHitEnemy() => Route<PlayerActionPresentationController>(o => o.HandleHitEnemy(), nameof(SignalHitEnemy));
    public void SignalResolveSkillImpact() => Route<PlayerActionPresentationController>(o => o.HandleResolveSkillImpact(), nameof(SignalResolveSkillImpact));
    public void SignalResolveSkillImpactAndRetreat() => Route<PlayerActionPresentationController>(o => o.HandleResolveSkillImpactAndRetreat(), nameof(SignalResolveSkillImpactAndRetreat));
}
