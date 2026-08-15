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

    public void Configure(string key, bool configureDefaults)
    {
        timelineCameraKey = key;
    }

    public bool Begin(CombatCinematicContext context)
    {
        if (virtualCamera == null) virtualCamera = GetComponent<CinemachineCamera>();
        if (!ValidateRuntimeContract(context, out string error))
        {
            Debug.LogError("[LightSkill Camera] " + error, this);
            return false;
        }

        CameraTarget target = virtualCamera.Target;
        target.TrackingTarget = ResolveCameraAnchor(context, followTarget);
        target.LookAtTarget = ResolveCameraAnchor(context, lookAtTarget);
        target.CustomLookAtTarget = true;
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

    public bool ValidateRuntimeContract(CombatCinematicContext context, out string error)
    {
        error = null;
        if (virtualCamera == null)
        {
            error = "CinemachineCamera manquante sur '" + name + "'.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(timelineCameraKey))
        {
            error = "Cle Timeline manquante sur '" + name + "'.";
            return false;
        }
        if (followTarget != LightSkillRuntimeAnchor.None && GetComponent<CinemachineFollow>() == null)
        {
            error = "CinemachineFollow manquant sur la camera baked '" + name + "'. Rebake requis.";
            return false;
        }
        if (lookAtTarget != LightSkillRuntimeAnchor.None &&
            GetComponent<CinemachineHardLookAt>() == null && GetComponent<CinemachineRotationComposer>() == null)
        {
            error = "Module de visee Cinemachine manquant sur la camera baked '" + name + "'. Rebake requis.";
            return false;
        }
        if ((followTarget != LightSkillRuntimeAnchor.None && ResolveCameraAnchor(context, followTarget) == null) ||
            (lookAtTarget != LightSkillRuntimeAnchor.None && ResolveCameraAnchor(context, lookAtTarget) == null))
        {
            error = "Les cibles runtime de '" + name + "' ne sont pas resolues.";
            return false;
        }
        return true;
    }
}
