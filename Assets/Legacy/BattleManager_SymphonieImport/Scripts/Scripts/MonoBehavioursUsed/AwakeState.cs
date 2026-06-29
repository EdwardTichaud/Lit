using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Gère désormais l'intégralité des transitions harmoniques d'une unité :
///     - déclenchement de l'éveil (Awake) lorsque suffisamment d'harmoniques sont accumulées ;
///     - bascule vers la dissonance quand la réserve tombe en dessous du seuil critique ;
///     - restitution des effets visuels, sonores et d'animation définis dans la <see cref="CharacterData"/>.
///     L'objectif est de centraliser la logique afin de faciliter l'équilibrage et la maintenance du récit.
/// </summary>
[RequireComponent(typeof(CharacterUnit))]
public class AwakeState : UnitStateEffects
{
    #region Types internes

    /// <summary>
    ///     Enumération descriptive des deux phases harmoniques manipulées par ce composant.
    /// </summary>
    private enum HarmonicPhase
    {
        Awake,
        Dissonant
    }

    /// <summary>
    ///     Structure de configuration alimentée par la <see cref="CharacterData"/>.
    ///     Chaque phase dispose de ses clips audio, animations et effets propres.
    /// </summary>
    private sealed class PhaseConfig
    {
        public AnimationClip idleOverride;
        public GameObject startEffectPrefab;
        public GameObject loopEffectPrefab;
        public GameObject endEffectPrefab;
        public AudioClipSO enterClip;
        public AudioClipSO exitClip;
        public AnimationClip enterAnimationOverride;
        public AnimationClip exitAnimationOverride;
    }

    /// <summary>
    ///     Données runtime associées à une phase : instances d'effets, coroutines en cours et durées mises en cache.
    /// </summary>
    private sealed class PhaseRuntime
    {
        public float cachedEnterDuration;
        public float cachedExitDuration;
        public GameObject startInstance;
        public GameObject loopInstance;
        public Coroutine transitionCoroutine;
    }

    #endregion

    #region Constantes Animator

    private const string AnimatorIdleStateName = "Idle_Battle";
    private const string AnimatorAwakeOnStateName = "Awake_On";
    private const string AnimatorAwakeOffStateName = "Awake_Off";
    private const string AnimatorDissonanceOnStateName = "Dissonance_On";
    private const string AnimatorDissonanceOffStateName = "Dissonance_Off";
    private const int AnimatorBaseLayerIndex = 0;
    private const float AnimatorCrossFadeDuration = 0.1f;
    private const float DefaultLoopTransitionDelay = 1f;

    private static readonly int AnimatorAwakeOnHash = Animator.StringToHash(AnimatorAwakeOnStateName);
    private static readonly int AnimatorAwakeOffHash = Animator.StringToHash(AnimatorAwakeOffStateName);
    private static readonly int AnimatorDissonanceOnHash = Animator.StringToHash(AnimatorDissonanceOnStateName);
    private static readonly int AnimatorDissonanceOffHash = Animator.StringToHash(AnimatorDissonanceOffStateName);

    #endregion

    #region Références & états

    private CharacterUnit unit;
    private bool isAwake;
    private bool isDissonant;

    private AnimatorOverrideController animatorOverrideController;
    private AnimationClip idleClipOriginalKey;
    private AnimationClip idleClipOriginalValue;
    private AnimationClip awakeOnClipKey;
    private AnimationClip awakeOnOriginalValue;
    private AnimationClip awakeOffClipKey;
    private AnimationClip awakeOffOriginalValue;
    private AnimationClip dissonanceOnClipKey;
    private AnimationClip dissonanceOnOriginalValue;
    private AnimationClip dissonanceOffClipKey;
    private AnimationClip dissonanceOffOriginalValue;

    private readonly Dictionary<HarmonicPhase, PhaseConfig> phaseConfigs = new();
    private readonly Dictionary<HarmonicPhase, PhaseRuntime> phaseRuntimes = new();

    private Coroutine idleRestoreCoroutine;

    /// <summary>
    ///     Mémorise le multiplicateur effectivement appliqué lors de l'activation d'Awake.
    ///     Cette valeur provient désormais du <see cref="CharacterData"/> pour éviter toute duplication
    ///     de configuration dans l'inspecteur Unity.
    /// </summary>
    private float activeStatMultiplier = 1f;

    /// <summary>
    ///     Valeur de repli utilisée si la fiche personnage ne définit pas explicitement de multiplicateur.
    /// </summary>
    private const float DefaultStatMultiplier = 1.5f;

    #endregion

    #region Accesseurs publics

    /// <summary>Expose l'état Awake afin de simplifier les vérifications côté <see cref="CharacterUnit"/>.</summary>
    public bool IsAwake => isAwake;

    /// <summary>Indique si la phase dissonante est active.</summary>
    public bool IsDissonant => isDissonant;

    #endregion

    #region Cycle de vie

    protected override void Awake()
    {
        base.Awake();

        unit = GetComponent<CharacterUnit>();

        // L'animator utilisé pour les transitions doit impérativement provenir des enfants.
        // Cela évite les confusions avec d'éventuels composants techniques placés sur l'objet racine.
        var childAnimator = GetComponentInChildren<Animator>(includeInactive: true);
        if (childAnimator != null)
            animator = childAnimator;

        InitializeAnimatorOverrideData();
        BuildPhaseConfigurations();
        ApplyAnimatorOverridesFromData();
        CachePhaseDurations();
    }

    #endregion

    #region API publique

    /// <summary>
    ///     Active l'éveil : boost de statistiques, override d'animations et déclenchement des effets spécifiques.
    ///     Toutes les ressources proviennent de la fiche personnage pour respecter la narration.
    /// </summary>
    public void EnterAwake()
    {
        if (isAwake)
            return;

        // On s'assure que les résidus de dissonance sont nettoyés avant d'appliquer le buff.
        StopPhaseEffects(HarmonicPhase.Dissonant, false);

        isAwake = true;
        isDissonant = false;

        ApplyStatBonus();
        unit?.NotifyIdleStateExit();

        ApplyIdleOverride(GetPhaseConfig(HarmonicPhase.Awake).idleOverride);

        float transitionDuration = PlayPhaseEnterAnimation(HarmonicPhase.Awake);

        StartPhaseEffects(HarmonicPhase.Awake, transitionDuration);

        PrepareAudioForPhase(HarmonicPhase.Awake, true);
        EnterState();
        ClearAudioCacheAfterEnter();

        ScheduleIdlePlayback(transitionDuration);
    }

    /// <summary>Met fin à l'éveil et restaure les valeurs de base de l'unité.</summary>
    public void ExitAwake()
    {
        if (!isAwake)
            return;

        isAwake = false;

        float transitionDuration = PlayPhaseExitAnimation(HarmonicPhase.Awake);

        RemoveStatBonus();
        StopPhaseEffects(HarmonicPhase.Awake, true);
        ApplyIdleOverride(null);

        PrepareAudioForPhase(HarmonicPhase.Awake, false);
        ExitState();
        ClearAudioCacheAfterExit();

        ScheduleIdlePlayback(transitionDuration);
    }

    /// <summary>
    ///     Déclenche la dissonance : effets négatifs visuels et audio, sans modifier les statistiques pour l'instant.
    /// </summary>
    public void EnterDissonant()
    {
        if (isDissonant)
            return;

        StopPhaseEffects(HarmonicPhase.Awake, false);

        isDissonant = true;
        isAwake = false;

        unit?.NotifyIdleStateExit();

        ApplyIdleOverride(GetPhaseConfig(HarmonicPhase.Dissonant).idleOverride);

        float transitionDuration = PlayPhaseEnterAnimation(HarmonicPhase.Dissonant);

        StartPhaseEffects(HarmonicPhase.Dissonant, transitionDuration);

        PrepareAudioForPhase(HarmonicPhase.Dissonant, true);
        EnterState();
        ClearAudioCacheAfterEnter();

        ScheduleIdlePlayback(transitionDuration);
    }

    /// <summary>Quitte l'état dissonant et restaure l'Idle d'origine.</summary>
    public void ExitDissonant()
    {
        if (!isDissonant)
            return;

        isDissonant = false;

        float transitionDuration = PlayPhaseExitAnimation(HarmonicPhase.Dissonant);

        StopPhaseEffects(HarmonicPhase.Dissonant, true);
        ApplyIdleOverride(null);

        PrepareAudioForPhase(HarmonicPhase.Dissonant, false);
        ExitState();
        ClearAudioCacheAfterExit();

        ScheduleIdlePlayback(transitionDuration);
    }

    #endregion

    #region Construction des configurations

    private void BuildPhaseConfigurations()
    {
        phaseConfigs.Clear();

        var data = unit != null ? unit.Data : null;
        phaseConfigs[HarmonicPhase.Awake] = new PhaseConfig
        {
            idleOverride = data?.awakenIdleAnimation,
            startEffectPrefab = data?.awakenEffect_Start,
            loopEffectPrefab = data?.awakenEffect_Loop,
            endEffectPrefab = data?.awakenEffect_End,
            enterClip = data?.awakeEnterClip,
            exitClip = data?.awakeExitClip,
            enterAnimationOverride = data?.awakeEnterAnimation,
            exitAnimationOverride = data?.awakeExitAnimation
        };

        phaseConfigs[HarmonicPhase.Dissonant] = new PhaseConfig
        {
            idleOverride = data?.dissonantIdleAnimation,
            startEffectPrefab = data?.dissonanceEffect_Start,
            loopEffectPrefab = data?.dissonanceEffect_Loop,
            endEffectPrefab = data?.dissonanceEffect_End,
            enterClip = data?.dissonanceEnterClip,
            exitClip = data?.dissonanceExitClip,
            enterAnimationOverride = data?.dissonanceEnterAnimation,
            exitAnimationOverride = data?.dissonanceExitAnimation
        };
    }

    private void InitializeAnimatorOverrideData()
    {
        if (animator == null)
            return;

        if (animator.runtimeAnimatorController is AnimatorOverrideController existingOverride)
        {
            animatorOverrideController = existingOverride;
        }
        else if (animator.runtimeAnimatorController != null)
        {
            animatorOverrideController = new AnimatorOverrideController(animator.runtimeAnimatorController);
            animator.runtimeAnimatorController = animatorOverrideController;
        }

        if (animatorOverrideController == null)
            return;

        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(animatorOverrideController.overridesCount);
        animatorOverrideController.GetOverrides(overrides);
        foreach (var pair in overrides)
        {
            if (pair.Key == null)
                continue;

            var value = pair.Value != null ? pair.Value : pair.Key;

            switch (pair.Key.name)
            {
                case AnimatorIdleStateName:
                    idleClipOriginalKey = pair.Key;
                    idleClipOriginalValue = value;
                    break;
                case AnimatorAwakeOnStateName:
                    awakeOnClipKey = pair.Key;
                    awakeOnOriginalValue = value;
                    break;
                case AnimatorAwakeOffStateName:
                    awakeOffClipKey = pair.Key;
                    awakeOffOriginalValue = value;
                    break;
                case AnimatorDissonanceOnStateName:
                    dissonanceOnClipKey = pair.Key;
                    dissonanceOnOriginalValue = value;
                    break;
                case AnimatorDissonanceOffStateName:
                    dissonanceOffClipKey = pair.Key;
                    dissonanceOffOriginalValue = value;
                    break;
            }
        }
    }

    private void ApplyAnimatorOverridesFromData()
    {
        if (animatorOverrideController == null)
            return;

        ApplyAnimatorOverrideClip(awakeOnClipKey, awakeOnOriginalValue, GetPhaseConfig(HarmonicPhase.Awake).enterAnimationOverride);
        ApplyAnimatorOverrideClip(awakeOffClipKey, awakeOffOriginalValue, GetPhaseConfig(HarmonicPhase.Awake).exitAnimationOverride);
        ApplyAnimatorOverrideClip(dissonanceOnClipKey, dissonanceOnOriginalValue, GetPhaseConfig(HarmonicPhase.Dissonant).enterAnimationOverride);
        ApplyAnimatorOverrideClip(dissonanceOffClipKey, dissonanceOffOriginalValue, GetPhaseConfig(HarmonicPhase.Dissonant).exitAnimationOverride);
    }

    private void ApplyAnimatorOverrideClip(AnimationClip key, AnimationClip defaultClip, AnimationClip overrideClip)
    {
        if (key == null || defaultClip == null)
            return;

        animatorOverrideController[key] = overrideClip != null ? overrideClip : defaultClip;
    }

    private void CachePhaseDurations()
    {
        CachePhaseDuration(HarmonicPhase.Awake, true);
        CachePhaseDuration(HarmonicPhase.Awake, false);
        CachePhaseDuration(HarmonicPhase.Dissonant, true);
        CachePhaseDuration(HarmonicPhase.Dissonant, false);
    }

    private void CachePhaseDuration(HarmonicPhase phase, bool entering)
    {
        var config = GetPhaseConfig(phase);
        var runtime = GetPhaseRuntime(phase);

        AnimationClip overrideClip = entering ? config.enterAnimationOverride : config.exitAnimationOverride;
        string stateName = entering ? GetPhaseEnterStateName(phase) : GetPhaseExitStateName(phase);

        float duration = 0f;
        if (overrideClip != null)
            duration = overrideClip.length;
        else
            duration = GetAnimationClipDuration(stateName);

        if (entering)
            runtime.cachedEnterDuration = duration;
        else
            runtime.cachedExitDuration = duration;
    }

    #endregion

    #region Gestion des animations

    private float PlayPhaseEnterAnimation(HarmonicPhase phase)
    {
        return PlayAnimatorState(phase, true);
    }

    private float PlayPhaseExitAnimation(HarmonicPhase phase)
    {
        return PlayAnimatorState(phase, false);
    }

    private float PlayAnimatorState(HarmonicPhase phase, bool entering)
    {
        if (animator == null)
            return 0f;

        string stateName = entering ? GetPhaseEnterStateName(phase) : GetPhaseExitStateName(phase);
        int stateHash = entering ? GetPhaseEnterHash(phase) : GetPhaseExitHash(phase);

        if (animator.HasState(AnimatorBaseLayerIndex, stateHash))
            animator.CrossFade(stateHash, AnimatorCrossFadeDuration, AnimatorBaseLayerIndex, 0f);
        else
            animator.Play(stateName, AnimatorBaseLayerIndex, 0f);

        var runtime = GetPhaseRuntime(phase);
        return entering ? runtime.cachedEnterDuration : runtime.cachedExitDuration;
    }

    private string GetPhaseEnterStateName(HarmonicPhase phase)
    {
        return phase == HarmonicPhase.Awake ? AnimatorAwakeOnStateName : AnimatorDissonanceOnStateName;
    }

    private string GetPhaseExitStateName(HarmonicPhase phase)
    {
        return phase == HarmonicPhase.Awake ? AnimatorAwakeOffStateName : AnimatorDissonanceOffStateName;
    }

    private int GetPhaseEnterHash(HarmonicPhase phase)
    {
        return phase == HarmonicPhase.Awake ? AnimatorAwakeOnHash : AnimatorDissonanceOnHash;
    }

    private int GetPhaseExitHash(HarmonicPhase phase)
    {
        return phase == HarmonicPhase.Awake ? AnimatorAwakeOffHash : AnimatorDissonanceOffHash;
    }

    private float GetAnimationClipDuration(string clipName)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return 0f;

        foreach (var clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip != null && clip.name == clipName)
                return clip.length;
        }

        return 0f;
    }

    private void ApplyIdleOverride(AnimationClip overrideClip)
    {
        if (animatorOverrideController == null || idleClipOriginalKey == null || idleClipOriginalValue == null)
            return;

        animatorOverrideController[idleClipOriginalKey] = overrideClip != null ? overrideClip : idleClipOriginalValue;
    }

    #endregion

    #region Gestion des effets visuels

    private void StartPhaseEffects(HarmonicPhase phase, float transitionDuration)
    {
        var config = GetPhaseConfig(phase);
        var runtime = GetPhaseRuntime(phase);

        StopPhaseEffects(phase, false);

        if (config.startEffectPrefab != null)
            runtime.startInstance = InstantiatePhaseEffect(config.startEffectPrefab);

        float effectDuration = EstimateEffectDuration(runtime.startInstance);
        float delay = Mathf.Max(effectDuration, transitionDuration);
        if (delay <= 0f)
            delay = DefaultLoopTransitionDelay;

        if (config.loopEffectPrefab != null)
            runtime.transitionCoroutine = StartCoroutine(SpawnLoopAfterDelay(phase, delay, config.loopEffectPrefab));
        else if (runtime.startInstance != null)
            runtime.transitionCoroutine = StartCoroutine(DestroyStartAfterDelay(phase, delay));
    }

    private void StopPhaseEffects(HarmonicPhase phase, bool spawnEndEffect)
    {
        var runtime = GetPhaseRuntime(phase);
        var config = GetPhaseConfig(phase);

        if (runtime.transitionCoroutine != null)
        {
            StopCoroutine(runtime.transitionCoroutine);
            runtime.transitionCoroutine = null;
        }

        DestroyEffectInstance(ref runtime.startInstance);
        DestroyEffectInstance(ref runtime.loopInstance);

        if (spawnEndEffect && config.endEffectPrefab != null)
            InstantiatePhaseEffect(config.endEffectPrefab);
    }

    private IEnumerator SpawnLoopAfterDelay(HarmonicPhase phase, float delaySeconds, GameObject loopPrefab)
    {
        if (delaySeconds > 0f)
            yield return new WaitForSeconds(delaySeconds);
        else
            yield return null;

        var runtime = GetPhaseRuntime(phase);
        runtime.transitionCoroutine = null;

        if (!IsPhaseActive(phase))
            yield break;

        DestroyEffectInstance(ref runtime.startInstance);

        if (loopPrefab != null)
            runtime.loopInstance = InstantiatePhaseEffect(loopPrefab);
    }

    private IEnumerator DestroyStartAfterDelay(HarmonicPhase phase, float delaySeconds)
    {
        if (delaySeconds > 0f)
            yield return new WaitForSeconds(delaySeconds);
        else
            yield return null;

        var runtime = GetPhaseRuntime(phase);
        runtime.transitionCoroutine = null;

        DestroyEffectInstance(ref runtime.startInstance);
    }

    private GameObject InstantiatePhaseEffect(GameObject prefab)
    {
        if (prefab == null)
            return null;

        var instance = Instantiate(prefab, transform.position, Quaternion.identity, transform);
        instance.transform.localPosition = Vector3.zero;
        return instance;
    }

    private static void DestroyEffectInstance(ref GameObject instance)
    {
        if (instance == null)
            return;

        Destroy(instance);
        instance = null;
    }

    private static float EstimateEffectDuration(GameObject effectInstance)
    {
        if (effectInstance == null)
            return 0f;

        float maxDuration = 0f;
        var particleSystems = effectInstance.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in particleSystems)
        {
            var main = ps.main;

            if (main.loop)
                return 0f;

            float simulationSpeed = Mathf.Max(main.simulationSpeed, 0.01f);
            float duration = main.duration / simulationSpeed;
            duration += main.startDelay.constantMax;
            duration += main.startLifetime.constantMax;

            if (duration > maxDuration)
                maxDuration = duration;
        }

        return maxDuration;
    }

    private bool IsPhaseActive(HarmonicPhase phase)
    {
        return phase switch
        {
            HarmonicPhase.Awake => isAwake,
            HarmonicPhase.Dissonant => isDissonant,
            _ => false
        };
    }

    #endregion

    #region Audio & Idle helpers

    private void PrepareAudioForPhase(HarmonicPhase phase, bool entering)
    {
        var config = GetPhaseConfig(phase);
        if (entering)
        {
            enterClip = config.enterClip;
            exitClip = null;
        }
        else
        {
            exitClip = config.exitClip;
            enterClip = null;
        }

        enterAnimation = null;
        exitAnimation = null;
        enterEffectPrefab = null;
        exitEffectPrefab = null;
    }

    private void ClearAudioCacheAfterEnter()
    {
        enterClip = null;
    }

    private void ClearAudioCacheAfterExit()
    {
        exitClip = null;
    }

    private void ScheduleIdlePlayback(float delaySeconds)
    {
        if (unit == null)
            return;

        if (idleRestoreCoroutine != null)
        {
            StopCoroutine(idleRestoreCoroutine);
            idleRestoreCoroutine = null;
        }

        float safeDelay = delaySeconds > 0f ? delaySeconds : AnimatorCrossFadeDuration;
        idleRestoreCoroutine = StartCoroutine(PlayIdleAfterDelay(safeDelay));
    }

    private IEnumerator PlayIdleAfterDelay(float delaySeconds)
    {
        if (delaySeconds > 0f)
            yield return new WaitForSeconds(delaySeconds);
        else
            yield return null;

        unit?.PlayIdleAnimation();
        idleRestoreCoroutine = null;
    }

    #endregion

    #region Accès aux dictionnaires

    private PhaseConfig GetPhaseConfig(HarmonicPhase phase)
    {
        if (!phaseConfigs.TryGetValue(phase, out var config) || config == null)
        {
            config = new PhaseConfig();
            phaseConfigs[phase] = config;
        }

        return config;
    }

    private PhaseRuntime GetPhaseRuntime(HarmonicPhase phase)
    {
        if (!phaseRuntimes.TryGetValue(phase, out var runtime) || runtime == null)
        {
            runtime = new PhaseRuntime();
            phaseRuntimes[phase] = runtime;
        }

        return runtime;
    }

    #endregion

    #region Gestion des statistiques

    private void ApplyStatBonus()
    {
        if (unit == null)
            return;

        activeStatMultiplier = ResolveAwakeStatMultiplier();

        // Les statistiques sont multipliées en se basant sur la donnée centralisée dans la fiche personnage.
        unit.currentStrength *= activeStatMultiplier;
        unit.currentDefense *= activeStatMultiplier;
        unit.currentReflex *= activeStatMultiplier;
        unit.currentMobility *= activeStatMultiplier;
        unit.currentPower *= activeStatMultiplier;
        unit.currentStability *= activeStatMultiplier;
        unit.currentVitality *= activeStatMultiplier;
        unit.currentSagacity *= activeStatMultiplier;
    }

    private void RemoveStatBonus()
    {
        if (unit == null)
            return;

        // Sécurise la valeur afin de ne jamais diviser par zéro dans les situations de données incomplètes.
        float safeMultiplier = activeStatMultiplier <= 0f ? DefaultStatMultiplier : activeStatMultiplier;

        unit.currentStrength /= safeMultiplier;
        unit.currentDefense /= safeMultiplier;
        unit.currentReflex /= safeMultiplier;
        unit.currentMobility /= safeMultiplier;
        unit.currentPower /= safeMultiplier;
        unit.currentStability /= safeMultiplier;
        unit.currentVitality /= safeMultiplier;
        unit.currentSagacity /= safeMultiplier;

        activeStatMultiplier = 1f;
    }

    /// <summary>
    ///     Récupère le multiplicateur d'Awake en consultant directement la fiche personnage.
    ///     Cette méthode centralise la logique de secours en cas de valeur non définie ou incohérente.
    /// </summary>
    private float ResolveAwakeStatMultiplier()
    {
        if (unit == null || unit.Data == null)
            return DefaultStatMultiplier;

        float configuredValue = unit.Data.awakeStatMultiplier;
        if (configuredValue <= 0f)
            return DefaultStatMultiplier;

        return configuredValue;
    }

    #endregion
}
