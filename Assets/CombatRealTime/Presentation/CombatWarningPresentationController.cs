using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

[DefaultExecutionOrder(510)]
[DisallowMultipleComponent]
public sealed class CombatWarningPresentationController : MonoBehaviour
{
    private static readonly int OriginId = Shader.PropertyToID("_WarningOrigin");
    private static readonly int DirectionId = Shader.PropertyToID("_WarningDirection");
    private static readonly int ColorId = Shader.PropertyToID("_WarningColor");
    private static readonly int IntensityId = Shader.PropertyToID("_WarningIntensity");
    private static readonly int PulseId = Shader.PropertyToID("_WarningPulse");
    private static readonly int VignetteId = Shader.PropertyToID("_WarningVignette");
    private static readonly int ChromaticId = Shader.PropertyToID("_WarningChromatic");

    public static CombatWarningPresentationController Instance { get; private set; }

    [SerializeField] private RealTimeCombatManager combatManager;
    [SerializeField] private CombatLockOnCameraController lockCamera;
    [SerializeField] private CustomPassVolume customPassVolume;
    [SerializeField] private Material warningMaterial;
    [SerializeField] private string customPassName = "Combat Warning";

    private FullScreenCustomPass customPass;
    private RealTimeCombatEnemy activeEnemy;
    private CombatWarningProfile activeProfile;
    private bool requested;
    private float blend;
    private float elapsed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }

        Instance = this;
        ResolveReferences();
        SetPassEnabled(false);
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (combatManager != null)
        {
            combatManager.CombatStateChanged += OnCombatStateChanged;
            combatManager.LockChanged += OnLockChanged;
        }
    }

    private void OnDisable()
    {
        if (combatManager != null)
        {
            combatManager.CombatStateChanged -= OnCombatStateChanged;
            combatManager.LockChanged -= OnLockChanged;
        }

        ClearImmediate();
    }

    private void OnDestroy()
    {
        ClearImmediate();
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (activeProfile == null)
        {
            return;
        }

        float fadeSeconds = Mathf.Max(0.01f, activeProfile.fadeOutSeconds);
        blend = Mathf.MoveTowards(blend, requested ? 1f : 0f, Time.unscaledDeltaTime / fadeSeconds);
        elapsed += Time.unscaledDeltaTime;
        ApplyMaterial();

        if (!requested && blend <= 0f)
        {
            SetPassEnabled(false);
            activeEnemy = null;
            activeProfile = null;
        }
    }

    public void BeginWarning(RealTimeCombatEnemy enemy)
    {
        if (enemy == null || enemy.ActiveSkill == null || combatManager == null ||
            !combatManager.IsCombatActive || combatManager.EngagedEnemy != enemy)
        {
            return;
        }

        CombatWarningProfile profile = enemy.ActiveSkill.CombatWarning;
        if (profile == null || !profile.enabled || !EnsurePass())
        {
            return;
        }

        activeEnemy = enemy;
        activeProfile = profile;
        requested = true;
        elapsed = 0f;
        SetPassEnabled(true);
        lockCamera?.BeginAttackWarning(enemy.LockPoint, profile);
        if (profile.useSlowMotion && profile.slowMotionSeconds > 0f)
        {
            TimeManager.EnsureInstance()?.AcquireGlobal(
                profile.slowMotionTimeScale,
                this,
                profile.slowMotionSeconds);
        }
        if (profile.warningAudio != null)
        {
            AudioManager.PlayClipAtPoint(profile.warningAudio, enemy.LockPoint.position);
        }
    }

    public void EndWarning(RealTimeCombatEnemy enemy)
    {
        if (enemy != null && activeEnemy != null && enemy != activeEnemy)
        {
            return;
        }

        requested = false;
        lockCamera?.EndAttackWarning(activeEnemy != null ? activeEnemy.LockPoint : null);
    }

    public void ClearImmediate()
    {
        TimeManager.Instance?.ReleaseOwner(this);
        requested = false;
        blend = 0f;
        elapsed = 0f;
        lockCamera?.EndAttackWarning(activeEnemy != null ? activeEnemy.LockPoint : null);
        SetPassEnabled(false);
        activeEnemy = null;
        activeProfile = null;
    }

    private void OnCombatStateChanged(bool active)
    {
        if (!active) ClearImmediate();
    }

    private void OnLockChanged(RealTimeCombatEnemy enemy)
    {
        if (activeEnemy != null && enemy != activeEnemy) ClearImmediate();
    }

    private bool EnsurePass()
    {
        ResolveReferences();
        if (customPass != null && warningMaterial != null)
        {
            return true;
        }

        if (customPassVolume == null || warningMaterial == null)
        {
            Debug.LogWarning("[CombatWarning] Configure the scene CustomPassVolume and Combat Warning material on BattleManager.", this);
            return false;
        }

        for (int i = 0; i < customPassVolume.customPasses.Count; i++)
        {
            FullScreenCustomPass candidate = customPassVolume.customPasses[i] as FullScreenCustomPass;
            if (candidate != null && candidate.name == customPassName)
            {
                customPass = candidate;
                return true;
            }
        }

        Debug.LogWarning("[CombatWarning] Full Screen Custom Pass '" + customPassName + "' is missing from the assigned volume.", this);
        return false;
    }

    private void ApplyMaterial()
    {
        if (warningMaterial == null || activeProfile == null || activeEnemy == null)
        {
            return;
        }

        Camera camera = lockCamera != null ? lockCamera.ControlledCamera : null;
        Vector2 origin = new Vector2(0.5f, 0.5f);
        if (camera != null)
        {
            Vector3 viewport = camera.WorldToViewportPoint(activeEnemy.LockPoint.position);
            if (viewport.z > 0f) origin = new Vector2(Mathf.Clamp01(viewport.x), Mathf.Clamp01(viewport.y));
        }

        Vector2 direction = origin - new Vector2(0.5f, 0.5f);
        if (direction.sqrMagnitude > 0.0001f) direction.Normalize();
        float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * Mathf.Max(0.01f, activeProfile.pulseFrequency) * Mathf.PI * 2f);
        warningMaterial.SetVector(OriginId, origin);
        warningMaterial.SetVector(DirectionId, direction);
        warningMaterial.SetColor(ColorId, activeProfile.color);
        warningMaterial.SetFloat(IntensityId, activeProfile.intensity * blend);
        warningMaterial.SetFloat(PulseId, pulse);
        warningMaterial.SetFloat(VignetteId, activeProfile.vignette * blend);
        warningMaterial.SetFloat(ChromaticId, activeProfile.chromaticAberration * blend);
    }

    private void SetPassEnabled(bool enabled)
    {
        if (EnsurePassReference()) customPass.enabled = enabled;
    }

    private bool EnsurePassReference()
    {
        if (customPass != null) return true;
        if (customPassVolume == null) return false;
        for (int i = 0; i < customPassVolume.customPasses.Count; i++)
        {
            FullScreenCustomPass candidate = customPassVolume.customPasses[i] as FullScreenCustomPass;
            if (candidate != null && candidate.name == customPassName)
            {
                customPass = candidate;
                return true;
            }
        }
        return false;
    }

    private void ResolveReferences()
    {
        if (combatManager == null) combatManager = GetComponent<RealTimeCombatManager>();
        if (lockCamera == null) lockCamera = GetComponent<CombatLockOnCameraController>();
    }
}
