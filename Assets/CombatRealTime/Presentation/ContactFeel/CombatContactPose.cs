using UnityEngine;

/// <summary>Opt-in cosmetic contact response. Never changes an action, root pose or clock.</summary>
[DefaultExecutionOrder(10010)]
[DisallowMultipleComponent]
public sealed class CombatContactPose : MonoBehaviour
{
    [Range(0, 20)] public float lightAngle = 5;
    [Range(0, 25)] public float heavyAngle = 11;
    [Min(.05f)] public float duration = .22f;
    [Range(0, 1)] public float committedAttackWeight = .4f;
    private Animator animator;
    private LitBrainsEnemy enemy;
    private CombatTimeDomain clock;
    private Transform spine, chest;
    private Quaternion spineBefore, chestBefore, spineApplied, chestApplied;
    private Vector3 localAxis;
    private float elapsed, angle;
    private bool applied;
    public float AppliedAngle { get; private set; }

    private void Awake()
    {
        enemy = GetComponent<LitBrainsEnemy>();
        animator = enemy != null ? enemy.Animator : GetComponentInChildren<Animator>();
        clock = GetComponent<CombatTimeDomain>();
    }

    public bool PlayContact(Vector3 sourcePosition, float strength)
    {
        if (!isActiveAndEnabled || !CanPresent() || !ResolveBones()) return false;
        var away = Vector3.ProjectOnPlane(transform.position - sourcePosition, transform.up);
        if (away.sqrMagnitude < .0001f) away = -transform.forward;
        localAxis = transform.InverseTransformDirection(Vector3.Cross(transform.up, away.normalized));
        angle = Mathf.Lerp(lightAngle, heavyAngle, Mathf.Clamp01(strength));
        if (enemy.IsAttackCommitted) angle *= committedAttackWeight;
        elapsed = 0;
        return true;
    }

    private bool CanPresent()
    {
        if (enemy == null || !enemy.isActiveAndEnabled || enemy.IsDead || enemy.IsCinematicSuspended || enemy.IsChargedAirborne ||
            animator == null || !animator.isActiveAndEnabled || !animator.isInitialized || !animator.isHuman) return false;
        string hurt = enemy.combatProfile != null ? enemy.combatProfile.hurtState : "Hurt";
        return !IsHurt(animator.GetCurrentAnimatorStateInfo(0), hurt) &&
            (!animator.IsInTransition(0) || !IsHurt(animator.GetNextAnimatorStateInfo(0), hurt));
    }

    private static bool IsHurt(AnimatorStateInfo state, string name) =>
        !string.IsNullOrEmpty(name) && (state.IsName(name) || state.IsName("Base Layer." + name));

    private bool ResolveBones()
    {
        if (spine == null) spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        return spine != null && chest != null;
    }

    public static float Envelope(float normalized)
    {
        if (normalized <= 0 || normalized >= 1) return 0;
        if (normalized < .18f) return Mathf.SmoothStep(0, 1, normalized / .18f);
        return 1 - Mathf.SmoothStep(0, 1, (normalized - .18f) / .82f);
    }

    private void LateUpdate()
    {
        ReleasePose();
        AppliedAngle = 0;
        if (angle <= 0) return;
        if (!CanPresent() || !ResolveBones()) { angle = 0; return; }
        elapsed += clock != null ? clock.DeltaTime : Time.deltaTime;
        if (elapsed >= duration) { angle = 0; return; }
        AppliedAngle = angle * Envelope(elapsed / Mathf.Max(.05f, duration));
        var axis = transform.TransformDirection(localAxis);
        spineBefore = spine.localRotation;
        chestBefore = chest.localRotation;
        spine.rotation = Quaternion.AngleAxis(AppliedAngle * .4f, axis) * spine.rotation;
        chest.rotation = Quaternion.AngleAxis(AppliedAngle * .6f, axis) * chest.rotation;
        spineApplied = spine.localRotation;
        chestApplied = chest.localRotation;
        applied = true;
    }

    private void ReleasePose()
    {
        if (!applied) return;
        // A fresh Animator pose wins; only undo an unevaluated additive pose (pause/culling).
        if (chest != null && Quaternion.Angle(chest.localRotation, chestApplied) < .001f) chest.localRotation = chestBefore;
        if (spine != null && Quaternion.Angle(spine.localRotation, spineApplied) < .001f) spine.localRotation = spineBefore;
        applied = false;
    }

    private void OnDisable() { ReleasePose(); angle = 0; AppliedAngle = 0; }
}
