using System.Collections.Generic;
using UnityEngine;
using UccCharacterLocomotion = Opsive.UltimateCharacterController.Character.UltimateCharacterLocomotion;

// Role: centralise les effets de temps de presentation utilises par le combat.
// Usage: attache au GameObject TimeManager de Maison; appele par la camera et l'orchestrateur de combat.
// Responsibilities: ralentir/restaurer les acteurs de combat sans toucher a Time.timeScale.
// Dependencies: Animator, Opsive UltimateCharacterLocomotion.
// Precautions: effet de presentation uniquement; le serveur Netcode pur ne doit pas l'appliquer directement.
public sealed class TimeManager : MonoBehaviour
{
    private const float MaxDynamicCombatBlendInSeconds = 0.25f;
    private const float MinDynamicCombatTimeScale = 0.1f;
    private const float DefaultEnemyActionTimeScale = 0.55f;

    public enum CombatPresentationTimeProfile
    {
        None = 0,
        DefensiveReaction = 1,
        EnemyAction = 2
    }

    public static TimeManager Instance { get; private set; }

    [Header("Combat Time")]
    [SerializeField, Range(0.05f, 1f)] private float defensiveReactionTimeScale = 0.35f;
    [SerializeField, Range(0.05f, 1f)] private float enemyActionTimeScale = DefaultEnemyActionTimeScale;
    [SerializeField, Min(0.05f)] private float defensiveReactionBlendInSeconds = 0.2f;
    [SerializeField, Min(0.05f)] private float defensiveReactionBlendOutSeconds = 0.45f;
    [SerializeField, Min(0.05f)] private float enemyActionBlendInSeconds = 0.12f;
    [SerializeField, Min(0.05f)] private float enemyActionBlendOutSeconds = 0.35f;
    [SerializeField] private AnimationCurve combatTimeBlendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Combat Hit Stop")]
    [SerializeField, Range(0.01f, 1f)] private float hitStopTimeScale = 0.08f;
    [SerializeField, Min(0f)] private float hitStopHoldSeconds = 0.08f;
    [SerializeField, Min(0.01f)] private float hitStopBlendOutSeconds = 0.08f;

    private float defensiveReactionWeight;
    private float enemyActionWeight;
    private CombatPresentationTimeProfile localTimeProfile = CombatPresentationTimeProfile.None;
    private bool globalDefensiveReactionActive;
    private float globalDefensiveReactionWeight;
    private bool presentationTimeScaleActive;
    private float presentationTimeScale = 1f;
    private float hitStopWeight;
    private float hitStopEndsAt;
    private readonly Dictionary<Animator, float> combatAnimatorSpeeds = new Dictionary<Animator, float>();
    private readonly Dictionary<UccCharacterLocomotion, float> combatCharacterTimeScales = new Dictionary<UccCharacterLocomotion, float>();
    private readonly List<Animator> animatorRemovalBuffer = new List<Animator>();
    private readonly List<UccCharacterLocomotion> locomotionRemovalBuffer = new List<UccCharacterLocomotion>();

    public float CombatPresentationDeltaTime => Time.unscaledDeltaTime * CombatTimeMultiplier;

    public static TimeManager EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

#if UNITY_2023_1_OR_NEWER
        Instance = FindAnyObjectByType<TimeManager>();
#else
        Instance = FindObjectOfType<TimeManager>();
#endif
        if (Instance != null)
        {
            return Instance;
        }

        GameObject host = new GameObject("TimeManager");
        Instance = host.AddComponent<TimeManager>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDisable()
    {
        RestoreAllCombatTime();
    }

    private void OnDestroy()
    {
        RestoreAllCombatTime();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void LateUpdate()
    {
        if (!HasActiveCombatTime())
        {
            return;
        }

        UpdateGlobalDefensiveReactionWeight(globalDefensiveReactionActive);
        UpdateHitStopWeight();

        if (globalDefensiveReactionActive)
        {
            TrackGlobalCombatTimeTargets();
        }

        ApplyCombatCharacterTimeScales();
        ApplyCombatAnimatorSpeeds();
        if (!globalDefensiveReactionActive &&
            globalDefensiveReactionWeight <= 0f &&
            defensiveReactionWeight <= 0f &&
            enemyActionWeight <= 0f &&
            hitStopWeight <= 0f &&
            !presentationTimeScaleActive)
        {
            RestoreAllCombatTime();
        }
    }

    public void SetCombatTimeTargets(Transform player, Transform enemy, bool defensiveReactionActive)
    {
        SetCombatTimeTargets(
            player,
            enemy,
            defensiveReactionActive
                ? CombatPresentationTimeProfile.DefensiveReaction
                : CombatPresentationTimeProfile.None);
    }

    public void SetCombatTimeTargets(Transform player, Transform enemy, CombatPresentationTimeProfile profile)
    {
        localTimeProfile = profile;
        UpdateDefensiveReactionWeight(profile == CombatPresentationTimeProfile.DefensiveReaction);
        UpdateEnemyActionWeight(profile == CombatPresentationTimeProfile.EnemyAction);
        UpdateHitStopWeight();

        if (profile != CombatPresentationTimeProfile.None)
        {
            TrackCharacterLocomotions(player);
            TrackCharacterLocomotions(enemy);
            TrackAnimators(player);
            TrackAnimators(enemy);
        }

        ApplyCombatCharacterTimeScales();
        ApplyCombatAnimatorSpeeds();
        if (profile == CombatPresentationTimeProfile.None &&
            defensiveReactionWeight <= 0f &&
            enemyActionWeight <= 0f &&
            hitStopWeight <= 0f &&
            !presentationTimeScaleActive)
        {
            RestoreCombatTime();
        }
    }

    public void SetCombatPresentationTimeScale(Transform target, float timeScale, bool active)
    {
        presentationTimeScaleActive = active && target != null;
        presentationTimeScale = presentationTimeScaleActive ? Mathf.Clamp(timeScale, 0.05f, 1f) : 1f;

        if (presentationTimeScaleActive)
        {
            TrackCharacterLocomotions(target);
            TrackAnimators(target);
        }

        ApplyCombatCharacterTimeScales();
        ApplyCombatAnimatorSpeeds();
        if (!presentationTimeScaleActive &&
            defensiveReactionWeight <= 0f &&
            enemyActionWeight <= 0f &&
            hitStopWeight <= 0f)
        {
            RestoreCombatTime();
        }
    }

    public void TriggerCombatHitStop(Transform primary, Transform secondary = null)
    {
        if (hitStopHoldSeconds <= 0f || hitStopTimeScale >= 1f)
        {
            return;
        }

        hitStopWeight = 1f;
        hitStopEndsAt = Mathf.Max(hitStopEndsAt, Time.unscaledTime + hitStopHoldSeconds);
        TrackCharacterLocomotions(primary);
        TrackCharacterLocomotions(secondary);
        TrackAnimators(primary);
        TrackAnimators(secondary);
        ApplyCombatCharacterTimeScales();
        ApplyCombatAnimatorSpeeds();
    }

    public void SetGlobalCombatDefensiveReaction(bool active)
    {
        globalDefensiveReactionActive = active;

        if (active)
        {
            TrackGlobalCombatTimeTargets();
        }

        ApplyCombatCharacterTimeScales();
        ApplyCombatAnimatorSpeeds();
        if (!active &&
            globalDefensiveReactionWeight <= 0f &&
            defensiveReactionWeight <= 0f &&
            enemyActionWeight <= 0f &&
            hitStopWeight <= 0f &&
            !presentationTimeScaleActive)
        {
            RestoreAllCombatTime();
        }
    }

    public void RestoreGlobalCombatTime()
    {
        globalDefensiveReactionActive = false;
        globalDefensiveReactionWeight = 0f;
        if (defensiveReactionWeight <= 0f &&
            enemyActionWeight <= 0f &&
            hitStopWeight <= 0f &&
            !presentationTimeScaleActive)
        {
            RestoreAllCombatTime();
            return;
        }

        ApplyCombatCharacterTimeScales();
        ApplyCombatAnimatorSpeeds();
    }

    public void RestoreCombatTime()
    {
        localTimeProfile = CombatPresentationTimeProfile.None;
        presentationTimeScaleActive = false;
        presentationTimeScale = 1f;
        defensiveReactionWeight = 0f;
        enemyActionWeight = 0f;
        hitStopWeight = 0f;
        hitStopEndsAt = 0f;

        if (globalDefensiveReactionActive || globalDefensiveReactionWeight > 0f)
        {
            ApplyCombatCharacterTimeScales();
            ApplyCombatAnimatorSpeeds();
            return;
        }

        RestoreAllCombatTime();
    }

    private void UpdateDefensiveReactionWeight(bool active)
    {
        float target = active ? 1f : 0f;
        float duration = ResolveDefensiveReactionBlendDuration(defensiveReactionWeight, target);
        defensiveReactionWeight = Mathf.MoveTowards(
            defensiveReactionWeight,
            target,
            Time.unscaledDeltaTime / Mathf.Max(0.05f, duration));
    }

    private void UpdateGlobalDefensiveReactionWeight(bool active)
    {
        float target = active ? 1f : 0f;
        float duration = ResolveDefensiveReactionBlendDuration(globalDefensiveReactionWeight, target);
        globalDefensiveReactionWeight = Mathf.MoveTowards(
            globalDefensiveReactionWeight,
            target,
            Time.unscaledDeltaTime / Mathf.Max(0.05f, duration));
    }

    private float ResolveDefensiveReactionBlendDuration(float currentWeight, float targetWeight)
    {
        if (targetWeight > currentWeight)
        {
            return Mathf.Min(defensiveReactionBlendInSeconds, MaxDynamicCombatBlendInSeconds);
        }

        return defensiveReactionBlendOutSeconds;
    }

    private void UpdateEnemyActionWeight(bool active)
    {
        float target = active ? 1f : 0f;
        float duration = target > enemyActionWeight
            ? enemyActionBlendInSeconds
            : enemyActionBlendOutSeconds;
        enemyActionWeight = Mathf.MoveTowards(
            enemyActionWeight,
            target,
            Time.unscaledDeltaTime / Mathf.Max(0.05f, duration));
    }

    private void UpdateHitStopWeight()
    {
        if (hitStopWeight <= 0f)
        {
            return;
        }

        if (Time.unscaledTime < hitStopEndsAt)
        {
            hitStopWeight = 1f;
            return;
        }

        hitStopWeight = Mathf.MoveTowards(
            hitStopWeight,
            0f,
            Time.unscaledDeltaTime / Mathf.Max(0.01f, hitStopBlendOutSeconds));
        if (hitStopWeight <= 0f)
        {
            hitStopEndsAt = 0f;
        }
    }

    private float CombatTimeMultiplier
    {
        get
        {
            float localDefensiveMultiplier = Mathf.Lerp(
                1f,
                ResolveProfileTimeScale(CombatPresentationTimeProfile.DefensiveReaction),
                EvaluateCombatTimeBlend(defensiveReactionWeight));
            float globalDefensiveMultiplier = Mathf.Lerp(
                1f,
                ResolveProfileTimeScale(CombatPresentationTimeProfile.DefensiveReaction),
                EvaluateCombatTimeBlend(globalDefensiveReactionWeight));
            float enemyActionMultiplier = Mathf.Lerp(
                1f,
                ResolveProfileTimeScale(CombatPresentationTimeProfile.EnemyAction),
                EvaluateCombatTimeBlend(enemyActionWeight));
            float hitStopMultiplier = Mathf.Lerp(
                1f,
                Mathf.Clamp(hitStopTimeScale, 0.01f, 1f),
                hitStopWeight);
            float profileMultiplier = Mathf.Min(
                Mathf.Min(localDefensiveMultiplier, globalDefensiveMultiplier),
                Mathf.Min(enemyActionMultiplier, hitStopMultiplier));
            return presentationTimeScaleActive
                ? Mathf.Min(profileMultiplier, presentationTimeScale)
                : profileMultiplier;
        }
    }

    public static float GetCombatPresentationDeltaTime()
    {
        return Instance != null ? Instance.CombatPresentationDeltaTime : Time.deltaTime;
    }

    public static float EstimateCombatPresentationDuration(float duration, CombatPresentationTimeProfile profile)
    {
        float timeScale = Instance != null
            ? Instance.ResolveProfileTimeScale(profile)
            : ResolveDefaultProfileTimeScale(profile);
        return Mathf.Max(0.05f, duration / Mathf.Max(0.05f, timeScale));
    }

    private bool HasActiveCombatTime()
    {
        return localTimeProfile != CombatPresentationTimeProfile.None ||
               defensiveReactionWeight > 0f ||
               enemyActionWeight > 0f ||
               globalDefensiveReactionActive ||
               globalDefensiveReactionWeight > 0f ||
               presentationTimeScaleActive ||
               hitStopWeight > 0f;
    }

    private float EvaluateCombatTimeBlend(float weight)
    {
        float normalizedWeight = Mathf.Clamp01(weight);
        if (combatTimeBlendCurve == null || combatTimeBlendCurve.length == 0)
        {
            return normalizedWeight;
        }

        return Mathf.Clamp01(combatTimeBlendCurve.Evaluate(normalizedWeight));
    }

    private float ResolveProfileTimeScale(CombatPresentationTimeProfile profile)
    {
        switch (profile)
        {
            case CombatPresentationTimeProfile.DefensiveReaction:
                return Mathf.Clamp(defensiveReactionTimeScale, MinDynamicCombatTimeScale, 1f);
            case CombatPresentationTimeProfile.EnemyAction:
                return Mathf.Clamp(enemyActionTimeScale, MinDynamicCombatTimeScale, 1f);
            default:
                return 1f;
        }
    }

    private static float ResolveDefaultProfileTimeScale(CombatPresentationTimeProfile profile)
    {
        switch (profile)
        {
            case CombatPresentationTimeProfile.DefensiveReaction:
                return MinDynamicCombatTimeScale;
            case CombatPresentationTimeProfile.EnemyAction:
                return DefaultEnemyActionTimeScale;
            default:
                return 1f;
        }
    }

    private void TrackGlobalCombatTimeTargets()
    {
#if UNITY_2023_1_OR_NEWER
        SquadCharacterController[] controllers = FindObjectsByType<SquadCharacterController>(FindObjectsInactive.Exclude);
#else
        SquadCharacterController[] controllers = FindObjectsOfType<SquadCharacterController>();
#endif
        if (controllers != null)
        {
            for (int i = 0; i < controllers.Length; i++)
            {
                SquadCharacterController controller = controllers[i];
                if (controller == null)
                {
                    continue;
                }

                TrackCharacterLocomotions(controller.transform);
                TrackAnimators(controller.transform);
            }
        }

#if UNITY_2023_1_OR_NEWER
        CombatAggroEnemy[] enemies = FindObjectsByType<CombatAggroEnemy>(FindObjectsInactive.Exclude);
#else
        CombatAggroEnemy[] enemies = FindObjectsOfType<CombatAggroEnemy>();
#endif
        if (enemies == null)
        {
            return;
        }

        for (int i = 0; i < enemies.Length; i++)
        {
            CombatAggroEnemy enemy = enemies[i];
            if (enemy == null)
            {
                continue;
            }

            TrackCharacterLocomotions(enemy.transform);
            TrackAnimators(enemy.transform);
        }
    }

    private void TrackCharacterLocomotions(Transform root)
    {
        if (root == null)
        {
            return;
        }

        UccCharacterLocomotion[] locomotions = root.GetComponentsInChildren<UccCharacterLocomotion>(true);
        if (locomotions == null)
        {
            return;
        }

        for (int i = 0; i < locomotions.Length; i++)
        {
            UccCharacterLocomotion locomotion = locomotions[i];
            if (locomotion == null || combatCharacterTimeScales.ContainsKey(locomotion))
            {
                continue;
            }

            combatCharacterTimeScales.Add(locomotion, locomotion.TimeScale);
        }
    }

    private void TrackAnimators(Transform root)
    {
        if (root == null)
        {
            return;
        }

        Animator[] animators = root.GetComponentsInChildren<Animator>(true);
        if (animators == null)
        {
            return;
        }

        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null || combatAnimatorSpeeds.ContainsKey(animator))
            {
                continue;
            }

            combatAnimatorSpeeds.Add(animator, animator.speed);
        }
    }

    private void ApplyCombatAnimatorSpeeds()
    {
        if (combatAnimatorSpeeds.Count == 0)
        {
            return;
        }

        float multiplier = CombatTimeMultiplier;
        animatorRemovalBuffer.Clear();
        foreach (KeyValuePair<Animator, float> pair in combatAnimatorSpeeds)
        {
            if (pair.Key == null)
            {
                animatorRemovalBuffer.Add(pair.Key);
                continue;
            }

            pair.Key.speed = pair.Value * multiplier;
        }

        for (int i = 0; i < animatorRemovalBuffer.Count; i++)
        {
            combatAnimatorSpeeds.Remove(animatorRemovalBuffer[i]);
        }
    }

    private void ApplyCombatCharacterTimeScales()
    {
        if (combatCharacterTimeScales.Count == 0)
        {
            return;
        }

        float multiplier = CombatTimeMultiplier;
        locomotionRemovalBuffer.Clear();
        foreach (KeyValuePair<UccCharacterLocomotion, float> pair in combatCharacterTimeScales)
        {
            if (pair.Key == null)
            {
                locomotionRemovalBuffer.Add(pair.Key);
                continue;
            }

            pair.Key.TimeScale = Mathf.Max(0f, pair.Value * multiplier);
        }

        for (int i = 0; i < locomotionRemovalBuffer.Count; i++)
        {
            combatCharacterTimeScales.Remove(locomotionRemovalBuffer[i]);
        }
    }

    private void RestoreCombatCharacterTimeScales()
    {
        if (combatCharacterTimeScales.Count == 0)
        {
            return;
        }

        foreach (KeyValuePair<UccCharacterLocomotion, float> pair in combatCharacterTimeScales)
        {
            if (pair.Key != null)
            {
                pair.Key.TimeScale = pair.Value;
            }
        }

        combatCharacterTimeScales.Clear();
        locomotionRemovalBuffer.Clear();
    }

    private void RestoreCombatAnimatorSpeeds()
    {
        if (combatAnimatorSpeeds.Count == 0)
        {
            return;
        }

        foreach (KeyValuePair<Animator, float> pair in combatAnimatorSpeeds)
        {
            if (pair.Key != null)
            {
                pair.Key.speed = pair.Value;
            }
        }

        combatAnimatorSpeeds.Clear();
        animatorRemovalBuffer.Clear();
    }

    private void RestoreAllCombatTime()
    {
        presentationTimeScaleActive = false;
        presentationTimeScale = 1f;
        localTimeProfile = CombatPresentationTimeProfile.None;
        defensiveReactionWeight = 0f;
        enemyActionWeight = 0f;
        globalDefensiveReactionActive = false;
        globalDefensiveReactionWeight = 0f;
        hitStopWeight = 0f;
        hitStopEndsAt = 0f;
        RestoreCombatCharacterTimeScales();
        RestoreCombatAnimatorSpeeds();
    }
}
