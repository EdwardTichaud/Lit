using UnityEngine;

/// <summary>
/// Plays the presentation associated with a confirmed player impact. The time
/// scale is always restored in unscaled time, including when this object dies.
/// </summary>
[DefaultExecutionOrder(520)]
[DisallowMultipleComponent]
public sealed class CombatImpactFeedbackController : MonoBehaviour
{
    public static CombatImpactFeedbackController Instance { get; private set; }

    [SerializeField] private CombatLockOnCameraController lockCamera;
    [SerializeField] private ScreenWaveController screenWave;

    private bool hitStopActive;
    private float timeScaleBeforeHitStop = 1f;
    private float hitStopReleaseTime;

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

    private void Update()
    {
        if (hitStopActive && Time.unscaledTime >= hitStopReleaseTime)
        {
            RestoreTimeScale();
        }
    }

    private void OnDisable()
    {
        RestoreTimeScale();
    }

    private void OnDestroy()
    {
        RestoreTimeScale();
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

    private void StartHitStop(CombatImpactFeedbackProfile profile)
    {
        if (!profile.useHitStop || profile.hitStopSeconds <= 0f)
        {
            return;
        }

        if (!hitStopActive)
        {
            timeScaleBeforeHitStop = Time.timeScale;
            hitStopActive = true;
        }

        float slowedTimeScale = timeScaleBeforeHitStop * Mathf.Clamp01(profile.hitStopTimeScale);
        Time.timeScale = Mathf.Min(Time.timeScale, slowedTimeScale);
        hitStopReleaseTime = Mathf.Max(hitStopReleaseTime, Time.unscaledTime + profile.hitStopSeconds);
    }

    private void RestoreTimeScale()
    {
        if (!hitStopActive)
        {
            return;
        }

        Time.timeScale = timeScaleBeforeHitStop;
        hitStopActive = false;
        hitStopReleaseTime = 0f;
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
