using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CinemachineCamera))]
public sealed class LightSkillCinematicCameraAuthoring : MonoBehaviour, ICombatCinematicParticipant
{
    [SerializeField] private string timelineCameraKey;
    [SerializeField] private LightSkillRuntimeAnchor followTarget = LightSkillRuntimeAnchor.None;
    [SerializeField] private LightSkillRuntimeAnchor lookAtTarget = LightSkillRuntimeAnchor.None;

    private CinemachineCamera virtualCamera;
    private CameraTarget authoredTarget;

    public string TimelineCameraKey => timelineCameraKey;
    public LightSkillRuntimeAnchor FollowTarget => followTarget;
    public LightSkillRuntimeAnchor LookAtTarget => lookAtTarget;

    private void Awake()
    {
        virtualCamera = GetComponent<CinemachineCamera>();
        authoredTarget = virtualCamera != null ? virtualCamera.Target : default;
    }

    public void Configure(string key)
    {
        timelineCameraKey = key;
    }

    public bool Begin(CombatCinematicContext context)
    {
        if (virtualCamera == null) virtualCamera = GetComponent<CinemachineCamera>();
        if (virtualCamera == null) return false;

        CameraTarget target = virtualCamera.Target;
        target.TrackingTarget = ResolveCameraAnchor(context, followTarget);
        target.LookAtTarget = ResolveCameraAnchor(context, lookAtTarget);
        target.CustomLookAtTarget = target.LookAtTarget != null;
        virtualCamera.Target = target;
        return true;
    }

    public void End()
    {
        if (virtualCamera != null) virtualCamera.Target = authoredTarget;
    }

    private Transform ResolveCameraAnchor(CombatCinematicContext context, LightSkillRuntimeAnchor anchor)
    {
        if (anchor == LightSkillRuntimeAnchor.None) return null;
        return anchor == LightSkillRuntimeAnchor.Rig ? transform.parent : LightSkillRuntimeExport.ResolveAnchor(context, anchor);
    }
}
