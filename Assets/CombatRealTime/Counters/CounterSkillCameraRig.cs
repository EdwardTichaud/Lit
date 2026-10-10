using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(CinemachineCamera))]
public sealed class CounterSkillCameraRig : MonoBehaviour, ICombatCinematicParticipant
{
    [SerializeField] private Vector3 openingOffset = new Vector3(1.7f, 1.45f, -3.6f);
    [SerializeField] private Vector3 impactOffset = new Vector3(-1.35f, 1.1f, -2.45f);
    [SerializeField, Range(0f, 1f)] private float impactMoveStartNormalized = 0.35f;
    [SerializeField, Min(0.01f)] private float moveSharpness = 8f;
    [SerializeField, Min(0f)] private float actorClearance;
    [SerializeField] private bool fixedStageFraming;
    private Vector3 stagePlayerPosition, stageLookTarget;
    private Quaternion stageRotation;

    private Transform player;
    private Transform enemy;
    private bool active;
    private float normalizedTime;
    private UnityEngine.Playables.PlayableDirector director;

    public void Begin(Transform playerRoot, Transform enemyLockPoint)
    {
        player = playerRoot;
        enemy = enemyLockPoint;
        active = player != null && enemy != null;
        normalizedTime = 0f;
        if (active)
        {
            stagePlayerPosition = player.position;
            stageLookTarget = Vector3.Lerp(player.position, enemy.position, .5f) + Vector3.up * 1.15f;
            Vector3 forward = Vector3.ProjectOnPlane(enemy.position - player.position, Vector3.up);
            stageRotation = Quaternion.LookRotation(forward.sqrMagnitude > .0001f ? forward : player.forward, Vector3.up);
        }
        SnapToShot(0f);
    }

    public void SetTimelineNormalizedTime(float value)
    {
        normalizedTime = Mathf.Clamp01(value);
    }

    public void End()
    {
        active = false;
        player = null;
        enemy = null;
    }

    public bool Begin(CombatCinematicContext context)
    {
        Begin(context != null ? context.PlayerRoot : null, context != null ? context.TargetLockPoint : null);
        director = GetComponentInParent<UnityEngine.Playables.PlayableDirector>();
        return active;
    }

    private void LateUpdate()
    {
        if (!active || player == null || enemy == null) return;
        if (director != null && director.duration > 0d)
            normalizedTime = Mathf.Clamp01((float)(director.time / director.duration));
        if (fixedStageFraming) { SnapToShot(normalizedTime); return; }
        Vector3 targetPosition = GetShotPosition(normalizedTime);
        Vector3 lookTarget = GetLookTarget();
        Quaternion targetRotation = Quaternion.LookRotation((lookTarget - targetPosition).normalized, Vector3.up);
        float blend = 1f - Mathf.Exp(-moveSharpness * Time.unscaledDeltaTime);
        transform.SetPositionAndRotation(
            Vector3.Lerp(transform.position, targetPosition, blend),
            Quaternion.Slerp(transform.rotation, targetRotation, blend));
    }

    private void SnapToShot(float timelineTime)
    {
        if (player == null || enemy == null) return;
        Vector3 position = GetShotPosition(timelineTime);
        Vector3 lookTarget = GetLookTarget();
        transform.SetPositionAndRotation(position, Quaternion.LookRotation((lookTarget - position).normalized, Vector3.up));
    }

    private Vector3 GetShotPosition(float timelineTime)
    {
        Vector3 playerToEnemy = enemy.position - player.position;
        playerToEnemy.y = 0f;
        if (playerToEnemy.sqrMagnitude < 0.001f) playerToEnemy = player.forward;
        Quaternion basis = Quaternion.LookRotation(playerToEnemy.normalized, Vector3.up);
        float travel = Mathf.InverseLerp(impactMoveStartNormalized, 1f, timelineTime);
        Vector3 offset = Vector3.Lerp(openingOffset, impactOffset, travel * travel * (3f - 2f * travel));
        Vector3 position = fixedStageFraming ? stagePlayerPosition + stageRotation * offset : player.position + basis * offset;
        if (actorClearance > 0)
        {
            position = KeepOutsideActor(position, player.position, actorClearance);
            position = KeepOutsideActor(position, enemy.position, actorClearance);
        }
        return position;
    }

    private Vector3 GetLookTarget() => fixedStageFraming ? stageLookTarget : actorClearance > 0
        ? Vector3.Lerp(player.position, enemy.position, .5f) + Vector3.up * 1.15f
        : Vector3.Lerp(player.position + Vector3.up * 1.15f, enemy.position, .68f);

    public static Vector3 KeepOutsideActor(Vector3 cameraPosition, Vector3 actorPosition, float clearance)
    {
        Vector3 center = actorPosition + Vector3.up * 1.1f;
        Vector3 delta = cameraPosition - center;
        if (delta.sqrMagnitude >= clearance * clearance) return cameraPosition;
        return center + (delta.sqrMagnitude > .0001f ? delta.normalized : Vector3.back) * clearance;
    }
}
