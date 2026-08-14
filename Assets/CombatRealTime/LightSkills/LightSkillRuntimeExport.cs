using UnityEngine;

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
