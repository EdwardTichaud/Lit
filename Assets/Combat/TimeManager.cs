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
    public static TimeManager Instance { get; private set; }

    [Header("Combat Time")]
    [SerializeField, Range(0.05f, 1f)] private float defensiveReactionTimeScale = 0.2f;
    [SerializeField, Min(0.05f)] private float defensiveReactionBlendInSeconds = 2f;
    [SerializeField, Min(0.05f)] private float defensiveReactionBlendOutSeconds = 0.35f;

    private float defensiveReactionWeight;
    private bool globalDefensiveReactionActive;
    private float globalDefensiveReactionWeight;
    private bool presentationTimeScaleActive;
    private float presentationTimeScale = 1f;
    private readonly Dictionary<Animator, float> combatAnimatorSpeeds = new Dictionary<Animator, float>();
    private readonly Dictionary<UccCharacterLocomotion, float> combatCharacterTimeScales = new Dictionary<UccCharacterLocomotion, float>();
    private readonly List<Animator> animatorRemovalBuffer = new List<Animator>();
    private readonly List<UccCharacterLocomotion> locomotionRemovalBuffer = new List<UccCharacterLocomotion>();

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
        if (!globalDefensiveReactionActive && globalDefensiveReactionWeight <= 0f)
        {
            return;
        }

        UpdateGlobalDefensiveReactionWeight(globalDefensiveReactionActive);

        if (globalDefensiveReactionActive)
        {
            TrackGlobalCombatTimeTargets();
        }

        ApplyCombatCharacterTimeScales();
        ApplyCombatAnimatorSpeeds();
        if (!globalDefensiveReactionActive &&
            globalDefensiveReactionWeight <= 0f &&
            defensiveReactionWeight <= 0f &&
            !presentationTimeScaleActive)
        {
            RestoreAllCombatTime();
        }
    }

    public void SetCombatTimeTargets(Transform player, Transform enemy, bool defensiveReactionActive)
    {
        UpdateDefensiveReactionWeight(defensiveReactionActive);

        if (defensiveReactionActive)
        {
            TrackCharacterLocomotions(player);
            TrackCharacterLocomotions(enemy);
            TrackAnimators(player);
            TrackAnimators(enemy);
        }

        ApplyCombatCharacterTimeScales();
        ApplyCombatAnimatorSpeeds();
        if (!defensiveReactionActive && defensiveReactionWeight <= 0f)
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
        if (!presentationTimeScaleActive && defensiveReactionWeight <= 0f)
        {
            RestoreCombatTime();
        }
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
            !presentationTimeScaleActive)
        {
            RestoreAllCombatTime();
        }
    }

    public void RestoreGlobalCombatTime()
    {
        globalDefensiveReactionActive = false;
        globalDefensiveReactionWeight = 0f;
        if (defensiveReactionWeight <= 0f && !presentationTimeScaleActive)
        {
            RestoreAllCombatTime();
            return;
        }

        ApplyCombatCharacterTimeScales();
        ApplyCombatAnimatorSpeeds();
    }

    public void RestoreCombatTime()
    {
        presentationTimeScaleActive = false;
        presentationTimeScale = 1f;
        defensiveReactionWeight = 0f;

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
        float duration = target > defensiveReactionWeight
            ? defensiveReactionBlendInSeconds
            : defensiveReactionBlendOutSeconds;
        defensiveReactionWeight = Mathf.MoveTowards(
            defensiveReactionWeight,
            target,
            Time.unscaledDeltaTime / Mathf.Max(0.05f, duration));
    }

    private void UpdateGlobalDefensiveReactionWeight(bool active)
    {
        float target = active ? 1f : 0f;
        float duration = target > globalDefensiveReactionWeight
            ? defensiveReactionBlendInSeconds
            : defensiveReactionBlendOutSeconds;
        globalDefensiveReactionWeight = Mathf.MoveTowards(
            globalDefensiveReactionWeight,
            target,
            Time.unscaledDeltaTime / Mathf.Max(0.05f, duration));
    }

    private float CombatTimeMultiplier
    {
        get
        {
            float localDefensiveMultiplier = Mathf.Lerp(1f, Mathf.Clamp01(defensiveReactionTimeScale), defensiveReactionWeight);
            float globalDefensiveMultiplier = Mathf.Lerp(1f, Mathf.Clamp01(defensiveReactionTimeScale), globalDefensiveReactionWeight);
            float defensiveMultiplier = Mathf.Min(localDefensiveMultiplier, globalDefensiveMultiplier);
            return presentationTimeScaleActive
                ? Mathf.Min(defensiveMultiplier, presentationTimeScale)
                : defensiveMultiplier;
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
        defensiveReactionWeight = 0f;
        globalDefensiveReactionActive = false;
        globalDefensiveReactionWeight = 0f;
        RestoreCombatCharacterTimeScales();
        RestoreCombatAnimatorSpeeds();
    }
}
