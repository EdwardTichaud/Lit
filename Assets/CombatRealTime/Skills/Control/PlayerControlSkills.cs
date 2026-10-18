using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerControlSkills : MonoBehaviour
{
    private SquadCharacterController caster;
    private PlayerActionPresentationController presentation;
    private LitOpsiveLocomotionBridge bridge;
    private Coroutine movement;
    private int motionSession;
    public bool IsMoving => movement != null;
    private void Awake() => ResolveReferences();
    private void ResolveReferences()
    {
        if (caster == null) caster = GetComponent<SquadCharacterController>();
        if (presentation == null) presentation = GetComponent<PlayerActionPresentationController>();
        if (bridge == null) bridge = GetComponent<LitOpsiveLocomotionBridge>();
    }
    public static PlayerControlSkills Get(SquadCharacterController actor)
    {
        var skill = actor.GetComponent<PlayerControlSkills>();
        return skill != null ? skill : actor.gameObject.AddComponent<PlayerControlSkills>();
    }
    public Vector3 ResolveDirection(Transform target)
        => ResolveDirection(caster, bridge, target);

    public static Vector3 ResolveDirection(SquadCharacterController caster, LitOpsiveLocomotionBridge bridge, Transform target)
    {
        if (caster == null) return Vector3.back;
        var transform = caster.transform;
        Vector2 input = bridge != null ? bridge.CurrentWorldMoveInput : Vector2.zero;
        if (LocalPlayerContext.LocalCharacterRoot == transform && LocalInputRouter.RawMoveValue.sqrMagnitude > .0064f)
        {
            var raw = LocalInputRouter.RawMoveValue;
            if (bridge == null || !bridge.TryResolveCombatLockMove(raw, out input)) input = caster.GetWorldSpaceInput(raw);
        }
        if (input.sqrMagnitude > .0064f) return new Vector3(input.x, 0, input.y).normalized;
        var away = target != null ? Vector3.ProjectOnPlane(transform.position - target.position, Vector3.up) : -transform.forward;
        return away.sqrMagnitude > .0001f ? away.normalized : -transform.forward;
    }
    public bool TryActivate(SkillSO skill, ICombatTarget target, Vector3 direction = default, Vector3? zoneOrigin = null)
    {
        ResolveReferences();
        if (!CombatProtectionDamage.HasAuthority || !SkillHealing.CanCast(caster) || skill == null || !skill.IsControlSkill) return false;
        if (skill.control.kind == SkillControlKind.PasDeRecul) return TryMove(skill, direction);
        if (target == null || target.IsDead || !EnemyRevelation.IsLivingEnemy(target.Root) ||
            Vector3.ProjectOnPlane(target.Root.position - transform.position, Vector3.up).magnitude > skill.control.range) return false;
        if (skill.control.kind == SkillControlKind.Revelation)
        {
            var mark = target.Root.GetComponent<EnemyRevelation>();
            if (mark == null) mark = target.Root.gameObject.AddComponent<EnemyRevelation>();
            mark.Apply(caster, skill.control.durationSeconds, skill.control.strength); return true;
        }
        Vector3 point;
        if (zoneOrigin.HasValue) point = zoneOrigin.Value;
        else if (!EnemyEntraveZone.TryFindGround(target.Root, out point)) return false;
        EnemyEntraveZone.Create(caster, point, skill); return true;
    }
    public bool TryMove(SkillSO skill, Vector3 direction)
    {
        ResolveReferences();
        direction = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (!SkillHealing.CanCast(caster) || bridge == null || presentation == null || direction.sqrMagnitude < .0001f ||
            !presentation.CanPlayState(skill.AnimatorState)) return false;
        direction.Normalize();
        float distance = presentation.GetSafePlanarTravel(direction, skill.control.distance);
        if (distance <= .02f || !bridge.BeginScriptedPlanarMotion(this)) return false;
        motionSession = presentation.BeginExternalPresentation(this, allowMobility: true);
        if (motionSession == 0) { bridge.EndScriptedPlanarMotion(this); return false; }
        int session = motionSession;
        presentation.RegisterActionCleanup(session, () => { if (session == motionSession) CancelMovement(); });
        bridge.BeginDodgeDirectionFacing(direction, true);
        int state = Animator.StringToHash(skill.AnimatorState);
        GetComponent<PlayerStateMotionController>()?.SetActionPolicy(state, PlayerActionMovementPolicy.VisualOnly);
        GetComponent<Animator>().CrossFadeInFixedTime(state, .08f, 0, 0f);
        movement = StartCoroutine(Move(session, direction, distance, skill.control.durationSeconds));
        return true;
    }
    private IEnumerator Move(int session, Vector3 direction, float distance, float duration)
    {
        yield return new WaitForFixedUpdate();
        Vector3 start = transform.position;
        float elapsed = 0;
        var clock = GetComponent<CombatTimeDomain>();
        while (presentation.IsCurrentSession(session) && elapsed < duration + .04f)
        {
            float dt = Mathf.Max(.001f, Time.fixedDeltaTime);
            float scale = clock != null ? clock.Scale : 1;
            if (scale <= 0) { yield return new WaitForFixedUpdate(); continue; }
            float remaining = distance - Vector3.Dot(transform.position - start, direction);
            if (remaining <= .025f) break;
            float step = Mathf.Min(remaining, distance / duration * dt * scale);
            step = presentation.GetSafePlanarTravel(direction, step);
            if (step <= .001f || !bridge.DriveScriptedPlanarMotion(this, direction * (step / dt))) break;
            yield return new WaitForFixedUpdate(); elapsed += clock != null ? clock.FixedDeltaTime : Time.fixedDeltaTime;
        }
        movement = null;
        if (presentation.IsCurrentSession(session)) presentation.EndExternalPresentation(session);
        else CancelMovement();
    }
    private void CancelMovement()
    {
        if (movement != null) StopCoroutine(movement);
        movement = null;
        bridge?.DriveScriptedPlanarMotion(this, Vector3.zero);
        bridge?.EndScriptedPlanarMotion(this); bridge?.EndDodgeDirectionFacing();
    }
    public void ClearCombatEffects()
    {
        foreach (var mark in FindObjectsByType<EnemyRevelation>(FindObjectsInactive.Include)) mark.ClearIfOwner(caster);
        foreach (var zone in FindObjectsByType<EnemyEntraveZone>(FindObjectsInactive.Include)) zone.ClearIfOwner(caster);
    }
    public void PlayRemoteMove(SkillSO skill)
    {
        ResolveReferences();
        if (!SkillHealing.CanCast(caster) || presentation == null || !presentation.CanPlayState(skill.AnimatorState)) return;
        motionSession = presentation.BeginExternalPresentation(this, allowMobility: true);
        GetComponent<Animator>().CrossFadeInFixedTime(skill.AnimatorState, .08f, 0);
        StartCoroutine(FinishRemoteMove(motionSession, skill.control.durationSeconds));
    }
    private IEnumerator FinishRemoteMove(int session, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (presentation != null && presentation.IsCurrentSession(session)) presentation.EndExternalPresentation(session);
    }
    public void AbortOwnedMovement()
    {
        if (presentation != null && presentation.IsCurrentSession(motionSession)) presentation.EndExternalPresentation(motionSession);
        CancelMovement();
    }
    private void OnDisable() { AbortOwnedMovement(); ClearCombatEffects(); }
}
