using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CinemachineCamera))]
public sealed class CounterSkillCameraRig : MonoBehaviour
{
    [SerializeField] private Vector3 openingOffset = new Vector3(1.7f, 1.45f, -3.6f);
    [SerializeField] private Vector3 impactOffset = new Vector3(-1.35f, 1.1f, -2.45f);
    [SerializeField, Range(0f, 1f)] private float impactMoveStartNormalized = 0.35f;
    [SerializeField, Min(0.01f)] private float moveSharpness = 8f;

    private Transform player;
    private Transform enemy;
    private bool active;
    private float normalizedTime;

    public void Begin(Transform playerRoot, Transform enemyLockPoint)
    {
        player = playerRoot;
        enemy = enemyLockPoint;
        active = player != null && enemy != null;
        normalizedTime = 0f;
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

    private void LateUpdate()
    {
        if (!active || player == null || enemy == null) return;
        Vector3 targetPosition = GetShotPosition(normalizedTime);
        Vector3 lookTarget = Vector3.Lerp(player.position + Vector3.up * 1.15f, enemy.position, 0.68f);
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
        Vector3 lookTarget = Vector3.Lerp(player.position + Vector3.up * 1.15f, enemy.position, 0.68f);
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
        return player.position + basis * offset;
    }
}
