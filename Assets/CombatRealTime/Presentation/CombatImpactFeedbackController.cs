using UnityEngine;

/// <summary>
/// Plays the presentation associated with a confirmed player impact. Time is
/// requested from TimeManager; this component never owns Unity time directly.
/// </summary>
[DefaultExecutionOrder(520)]
[DisallowMultipleComponent]
public sealed class CombatImpactFeedbackController : MonoBehaviour
{
    public static CombatImpactFeedbackController Instance { get; private set; }

    [SerializeField] private CombatLockOnCameraController lockCamera;

    private readonly System.Collections.Generic.Stack<TimeManager.TimeRequestHandle> externalPauseHandles =
        new System.Collections.Generic.Stack<TimeManager.TimeRequestHandle>();

    public static CombatImpactFeedbackController EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        return FindAnyObjectByType<CombatImpactFeedbackController>(FindObjectsInactive.Include);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }

        Instance = this;
        ResolveDependencies();
    }

    private void OnDisable()
    {
        TimeManager.Instance?.ReleaseOwner(this);
        externalPauseHandles.Clear();
    }

    private void OnDestroy()
    {
        TimeManager.Instance?.ReleaseOwner(this);
        externalPauseHandles.Clear();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public static Vector3 ResolvePlayerImpactPosition(Transform targetPoint, Transform playerRoot, Vector3 offset = default)
    {
        Vector3 position = targetPoint.position;
        if (playerRoot != null) position.y = playerRoot.position.y + 1.5f;
        return position + offset;
    }

    public void PlayImpact(SkillSO skill, EnemyController target)
    {
        CombatImpactFeedbackProfile profile = skill != null ? skill.ImpactFeedback : null;
        if (profile == null || !profile.enabled || target == null)
        {
            return;
        }

        ResolveDependencies();
        Transform impactPoint = target.LockPoint != null ? target.LockPoint : target.transform;
        Vector3 impactPosition = ResolvePlayerImpactPosition(impactPoint, RealTimeCombatManager.Instance?.PlayerRoot);
        if (profile.additionalImpactVfx != null)
        {
            Instantiate(profile.additionalImpactVfx, impactPosition, impactPoint.rotation, impactPoint);
        }

        if (profile.additionalImpactAudio != null)
        {
            AudioManager.PlayClipAtPoint(profile.additionalImpactAudio, impactPosition);
        }

        lockCamera?.PlayImpact(profile.camera);
        StartHitStop(profile);
    }

    public void PushExternalPause()
    {
        TimeManager manager = TimeManager.EnsureInstance();
        if (manager != null) externalPauseHandles.Push(manager.AcquireGlobalPause(this));
    }

    public void PlayReactionSlowMotion(float timeScale, float durationSeconds)
    {
        if (durationSeconds > 0f) TimeManager.EnsureInstance()?.AcquireGlobal(timeScale, this, durationSeconds);
    }

    public void PopExternalPause()
    {
        if (externalPauseHandles.Count > 0)
            TimeManager.Instance?.Release(externalPauseHandles.Pop());
    }

    private void StartHitStop(CombatImpactFeedbackProfile profile)
    {
        if (profile.useHitStop && profile.hitStopSeconds > 0f)
            TimeManager.EnsureInstance()?.AcquireGlobal(profile.hitStopTimeScale, this, profile.hitStopSeconds);
    }

    private void ResolveDependencies()
    {
        if (lockCamera == null)
        {
            lockCamera = FindAnyObjectByType<CombatLockOnCameraController>(FindObjectsInactive.Include);
        }

    }
}
