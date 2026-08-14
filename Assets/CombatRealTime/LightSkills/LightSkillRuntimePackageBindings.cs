using Unity.Cinemachine;
using UnityEngine;

public enum LightSkillRuntimeAnchor
{
    None,
    Rig,
    Player,
    Enemy,
    EnemyLockPoint
}

[DisallowMultipleComponent]
public sealed class LightSkillRuntimeExport : MonoBehaviour, ICombatCinematicParticipant
{
    [SerializeField] private LightSkillRuntimeAnchor runtimeParent = LightSkillRuntimeAnchor.Rig;
    [SerializeField] private bool preserveWorldPosition = true;

    private Transform authoredParent;
    private Vector3 authoredLocalPosition;
    private Quaternion authoredLocalRotation;
    private Vector3 authoredLocalScale;

    public LightSkillRuntimeAnchor RuntimeParent => runtimeParent;

    private void Awake()
    {
        CacheAuthoredTransform();
    }

    public bool Begin(CombatCinematicContext context)
    {
        CacheAuthoredTransform();
        if (runtimeParent == LightSkillRuntimeAnchor.Rig) return true;
        Transform parent = ResolveAnchor(context, runtimeParent);
        if (parent == null) return false;
        transform.SetParent(parent, preserveWorldPosition);
        return true;
    }

    public void End()
    {
        if (authoredParent == null) return;
        transform.SetParent(authoredParent, false);
        transform.localPosition = authoredLocalPosition;
        transform.localRotation = authoredLocalRotation;
        transform.localScale = authoredLocalScale;
    }

    private void CacheAuthoredTransform()
    {
        if (authoredParent != null) return;
        authoredParent = transform.parent;
        authoredLocalPosition = transform.localPosition;
        authoredLocalRotation = transform.localRotation;
        authoredLocalScale = transform.localScale;
    }

    internal static Transform ResolveAnchor(CombatCinematicContext context, LightSkillRuntimeAnchor anchor)
    {
        if (context == null) return null;
        return anchor switch
        {
            LightSkillRuntimeAnchor.Player => context.PlayerRoot,
            LightSkillRuntimeAnchor.Enemy => context.TargetEnemy != null ? context.TargetEnemy.transform : null,
            LightSkillRuntimeAnchor.EnemyLockPoint => context.TargetLockPoint != null
                ? context.TargetLockPoint
                : context.TargetEnemy != null ? context.TargetEnemy.transform : null,
            _ => null
        };
    }
}

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
