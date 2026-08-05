using Unity.Cinemachine;
using UnityEngine;

[DefaultExecutionOrder(-300)]
[DisallowMultipleComponent]
public sealed class LightSkillFurieCameraRig : MonoBehaviour
{
    private enum ShotPhase { Opening, Rear, Dash }

    [SerializeField] private CinemachineCamera virtualCamera;
    [SerializeField] private Vector3 openingLocalOffset = new Vector3(0f, 1.35f, 3.25f);
    [SerializeField] private Vector3 openingLookOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField] private Vector3 rearLocalOffset = new Vector3(0f, 0f, -6f);
    [SerializeField, Min(0.01f)] private float dashFollowSharpness = 5.5f;

    private Transform player;
    private Transform target;
    private ShotPhase phase;
    private bool prepared;

    public CinemachineCamera VirtualCamera => virtualCamera;

    private void Reset()
    {
        virtualCamera = GetComponent<CinemachineCamera>();
    }

    public void Prepare(Transform playerRoot, Transform targetPoint)
    {
        player = playerRoot;
        target = targetPoint;
        phase = ShotPhase.Opening;
        prepared = player != null;
        SnapToShot();
    }

    public void SetRearShot()
    {
        phase = ShotPhase.Rear;
        SnapToShot();
    }

    public void SetDashFollow()
    {
        phase = ShotPhase.Dash;
    }

    public void Clear()
    {
        prepared = false;
        player = null;
        target = null;
    }

    private void LateUpdate()
    {
        if (!prepared || player == null)
        {
            return;
        }

        if (phase == ShotPhase.Dash)
        {
            float blend = 1f - Mathf.Exp(-dashFollowSharpness * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, GetDesiredPosition(), blend);
            ApplyLookAt();
        }
    }

    private void SnapToShot()
    {
        if (!prepared || player == null)
        {
            return;
        }

        transform.position = GetDesiredPosition();
        ApplyLookAt();
    }

    private Vector3 GetDesiredPosition()
    {
        return phase == ShotPhase.Opening
            ? player.TransformPoint(openingLocalOffset)
            : player.TransformPoint(rearLocalOffset);
    }

    private void ApplyLookAt()
    {
        Vector3 lookPoint = phase == ShotPhase.Opening || target == null
            ? player.position + openingLookOffset
            : target.position;
        Vector3 direction = lookPoint - transform.position;
        if (direction.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }
}
