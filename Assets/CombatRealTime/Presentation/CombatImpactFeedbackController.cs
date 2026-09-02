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
    [SerializeField] private ScreenWaveController screenWave;

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

    public void PlayImpact(SkillSO skill, RealTimeCombatEnemy target)
    {
        CombatImpactFeedbackProfile profile = skill != null ? skill.ImpactFeedback : null;
        if (profile == null || !profile.enabled || target == null)
        {
            return;
        }

        ResolveDependencies();
        Transform impactPoint = target.LockPoint != null ? target.LockPoint : target.transform;
        if (profile.additionalImpactVfx != null)
        {
            Instantiate(profile.additionalImpactVfx, impactPoint.position, impactPoint.rotation, impactPoint);
        }

        if (profile.additionalImpactAudio != null)
        {
            AudioManager.PlayClipAtPoint(profile.additionalImpactAudio, impactPoint.position);
        }

        if (profile.screenWave != null && profile.screenWave.enabled)
        {
            (screenWave != null ? screenWave : ScreenWaveController.EnsureInstance())?.TryPlayScreenWavePhase(
                impactPoint.position,
                profile.screenWave.settings);
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

        if (screenWave == null)
        {
            screenWave = ScreenWaveController.EnsureInstance();
        }
    }
}
