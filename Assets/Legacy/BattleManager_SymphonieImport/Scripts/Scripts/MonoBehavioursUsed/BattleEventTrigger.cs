using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Timeline;

/// <summary>
/// Surveille des evenements de combat et declenche un motif camera, une animation ou une timeline lorsqu'une condition est atteinte.
/// </summary>
public class BattleEventTrigger : MonoBehaviour
{
    private static readonly List<BattleEventTrigger> Instances = new();
    private static bool searchedInactiveTriggers;
    private static bool eventInProgress;

    public enum BattleEventCategory
    {
        [InspectorName("HP Treshold")]
        HPTreshold,
        LastStandUnit,
        LastStandEnemy,
        [InspectorName("Last Stand (Toutes les unites)")]
        LastStandAllUnits
    }

    public enum TriggerDelayMode
    {
        [InspectorName("Delai (secondes)")]
        DelaySeconds,
        [InspectorName("Au tour de l'unite")]
        OnUnitTurn,
        [InspectorName("Fin du tour en cours")]
        EndOfCurrentTurn
    }

    [System.Serializable]
    public class ThresholdData
    {
        [Tooltip("Categorie de declenchement pour cet evenement.")]
        public BattleEventCategory category = BattleEventCategory.HPTreshold;
        [Tooltip("Seuil de PV en pourcentage (0-1). L'evenement se declenche lorsque les PV sont inferieurs ou egaux a ce ratio.")]
        public float hpRatio = 0.5f;
        [Tooltip("Mode de delai avant le declenchement de l'evenement.")]
        public TriggerDelayMode triggerDelayMode = TriggerDelayMode.DelaySeconds;
        [Tooltip("Delai en secondes avant le declenchement si le mode Delai est selectionne.")]
        public float triggerDelaySeconds = 0f;
        [Tooltip("CameraMotif verouille lorsque le seuil est atteint (ignore si une Timeline est definie).")]
        public CameraMotifSO cameraMotif;
        [Tooltip("Animation jouee sur l'unite lorsque le seuil est atteint (ignore si une Timeline est definie).")]
        public AnimationClip animationClip;
        [Tooltip("AudioClip joue lorsque le seuil est atteint (ignore si une Timeline est definie).")]
        public AudioClipSO audioClip;
        [Tooltip("Timeline jouee lorsque le seuil est atteint. Prioritaire sur le CameraMotif et l'AnimationClip (ignore pour LastStandAllUnits).")]
        public TimelineAsset timeline;
        [Tooltip("Deverrouille le CameraMotif a la fin de l'animation.")]
        public bool unlockMotifAfterAnimation = true;
        [Tooltip("Delai en secondes avant de deverrouiller le CameraMotif si l'option UnlockMotifAfterAnimation est desactivee.")]
        public float unlockMotifDelay = 0f;
        [HideInInspector] public bool triggered = false;
        private HashSet<CharacterUnit> triggeredUnits;

        public bool HasTriggeredFor(CharacterUnit unit)
        {
            if (unit == null)
                return false;
            if (triggeredUnits == null)
                triggeredUnits = new HashSet<CharacterUnit>();
            return triggeredUnits.Contains(unit);
        }

        public void MarkTriggeredFor(CharacterUnit unit)
        {
            if (unit == null)
                return;
            if (triggeredUnits == null)
                triggeredUnits = new HashSet<CharacterUnit>();
            triggeredUnits.Add(unit);
        }
    }

    [System.Serializable]
    public class UnitThresholds
    {
        [Tooltip("Données de l'unité à surveiller. La résolution se fait via CharacterBattleModel (ignore pour LastStandAllUnits).")]
        public CharacterData unitData;
        [Tooltip("Liste des déclencheurs associés")] public List<ThresholdData> thresholds = new();
    }

    [Header("Unités et seuils à surveiller")]
    public List<UnitThresholds> units = new();

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;
    [SerializeField] private float debugInterval = 1f;
    private float nextDebugTime;

    private void Awake()
    {
        if (!Instances.Contains(this))
            Instances.Add(this);
    }

    private void OnDestroy()
    {
        Instances.Remove(this);
    }

    public static IReadOnlyList<BattleEventTrigger> GetInstances()
    {
        return Instances;
    }

    public static void EvaluateAll(NewBattleManager battleManager)
    {
        if (battleManager == null)
            return;

        EnsureInactiveTriggersCached();
        foreach (var trigger in Instances)
        {
            if (trigger == null)
                continue;

            trigger.Evaluate(battleManager);
        }
    }

    private void Update()
    {
        Evaluate(NewBattleManager.Instance);
    }

    public void Evaluate(NewBattleManager battleManager)
    {
        if (units.Count == 0 || battleManager == null)
            return;

        if (eventInProgress || battleManager.IsBattlePaused || battleManager.IsBattleEventActive)
            return;

        if (debugLogs && Time.unscaledTime >= nextDebugTime)
        {
            nextDebugTime = Time.unscaledTime + Mathf.Max(0.1f, debugInterval);
            DebugEvaluate(battleManager);
        }

        foreach (var unitData in units)
        {
            if (unitData == null || unitData.thresholds.Count == 0)
                continue;

            CharacterUnit resolvedUnit = null;
            if (unitData.unitData != null)
                resolvedUnit = ResolveUnit(unitData, battleManager);

            foreach (var t in unitData.thresholds)
            {
                if (t == null)
                    continue;

                bool usesPerUnitTracking = t.category == BattleEventCategory.LastStandAllUnits;
                if (!usesPerUnitTracking && t.triggered)
                    continue;

                CharacterUnit trackedUnit = resolvedUnit;
                bool shouldTrigger = false;

                switch (t.category)
                {
                    case BattleEventCategory.HPTreshold:
                        if (trackedUnit == null || trackedUnit.Data == null)
                            continue;
                        shouldTrigger = trackedUnit.currentHP <= trackedUnit.Data.baseHP * t.hpRatio;
                        break;
                    case BattleEventCategory.LastStandUnit:
                        if (trackedUnit == null)
                            continue;
                        shouldTrigger = IsLastStandUnit(trackedUnit, battleManager);
                        break;
                    case BattleEventCategory.LastStandEnemy:
                        trackedUnit = FindLastStandEnemy(battleManager);
                        shouldTrigger = trackedUnit != null;
                        break;
                    case BattleEventCategory.LastStandAllUnits:
                        trackedUnit = FindLastStandUnitForAll(battleManager, t);
                        shouldTrigger = trackedUnit != null;
                        break;
                }

                if (debugLogs)
                {
                    string unitName = trackedUnit != null ? trackedUnit.name : "(none)";
                    string motifName = t.cameraMotif != null ? t.cameraMotif.name : "(none)";
                    string clipName = t.animationClip != null ? t.animationClip.name : "(none)";
                    AudioClipSO resolvedAudio = ResolveAudioClip(t, trackedUnit);
                    string audioName = resolvedAudio != null ? resolvedAudio.name : "(none)";
                    string timelineName = t.timeline != null ? t.timeline.name : "(none)";
                    Debug.Log($"[BattleEventTrigger] check unit={unitName} category={t.category} triggered={t.triggered} shouldTrigger={shouldTrigger} motif={motifName} anim={clipName} audio={audioName} timeline={timelineName}");
                }

                if (shouldTrigger && trackedUnit != null)
                {
                    if (usesPerUnitTracking)
                        t.MarkTriggeredFor(trackedUnit);
                    else
                        t.triggered = true;
                    StartCoroutine(TriggerEvent(trackedUnit, t, battleManager));
                    if (debugLogs)
                    {
                        AudioClipSO resolvedAudio = ResolveAudioClip(t, trackedUnit);
                        Debug.Log($"[BattleEventTrigger] triggered unit={trackedUnit.name} motif={t.cameraMotif?.name} anim={t.animationClip?.name} audio={resolvedAudio?.name} timeline={t.timeline?.name}");
                    }
                    break;
                }
            }
        }
    }

    private IEnumerator TriggerEvent(CharacterUnit trackedUnit, ThresholdData threshold, NewBattleManager battleManager)
    {
        if (trackedUnit == null || threshold == null)
            yield break;

        var manager = battleManager != null ? battleManager : NewBattleManager.Instance;
        bool eventStarted = false;
        bool eventLocked = false;
        int battlePauseToken = -1;
        try
        {
            yield return WaitBeforeTrigger(threshold, trackedUnit, battleManager);
            if (battleManager != null && !IsBattleActive(battleManager))
                yield break;
            if (trackedUnit == null || trackedUnit.IsDead)
                yield break;

            while (eventInProgress)
                yield return null;
            eventInProgress = true;
            eventLocked = true;

            if (battleManager != null && !IsBattleActive(battleManager))
                yield break;
            if (trackedUnit == null || trackedUnit.IsDead)
                yield break;

            manager?.RegisterBattleEventStart();
            eventStarted = true;

            var cameraManager = BattleCameraManager.Instance;
            bool allowTimeline = threshold.category != BattleEventCategory.LastStandAllUnits;
            bool useTimeline = allowTimeline && threshold.timeline != null;
            bool hasCameraMotif = !useTimeline && threshold.cameraMotif != null;
            bool hasAnimationClip = !useTimeline && threshold.animationClip != null;
            if (hasCameraMotif && cameraManager != null)
            {
                cameraManager.ConfigureActionTargets(trackedUnit, trackedUnit);
                cameraManager.LockCameraMotif(threshold.cameraMotif, -1f, BattleCameraManager.MotifLockPriority.High);
            }

            if (manager != null && manager.IsBattleActive)
                battlePauseToken = manager.RequestBattlePause("BattleEventTrigger");

            AudioClipSO resolvedAudio = ResolveAudioClip(threshold, trackedUnit);
            if (!useTimeline && resolvedAudio != null)
                AudioManager.Instance?.PlaySound(resolvedAudio);

            if (useTimeline)
            {
                yield return PlayTimelineAndWait(trackedUnit, threshold.timeline);
            }
            else
            {
                if (hasAnimationClip)
                {
                    trackedUnit.PlayPerformingAnimation(threshold.animationClip);
                }
                else if (debugLogs)
                {
                    Debug.LogWarning($"[BattleEventTrigger] animation manquante pour {trackedUnit.name}.");
                }

                float holdDuration = 0f;
                if (hasAnimationClip && threshold.animationClip != null)
                    holdDuration = threshold.animationClip.length;
                if (hasCameraMotif && !threshold.unlockMotifAfterAnimation && threshold.unlockMotifDelay > 0f)
                    holdDuration = Mathf.Max(holdDuration, threshold.unlockMotifDelay);

                if (holdDuration > 0f)
                    yield return new WaitForSecondsRealtime(holdDuration);
                else
                    yield return null;

                if (hasCameraMotif && cameraManager != null)
                {
                    if (threshold.unlockMotifAfterAnimation || threshold.unlockMotifDelay > 0f)
                    {
                        cameraManager.UnlockCameraMotif(threshold.cameraMotif);
                        if (!cameraManager.motifLocked)
                            cameraManager.ClearRigTargets();
                    }
                }
            }
        }
        finally
        {
            if (battlePauseToken >= 0 && manager != null)
                manager.ReleaseBattlePause(battlePauseToken);
            if (eventStarted)
                manager?.RegisterBattleEventEnd();
            if (eventLocked)
                eventInProgress = false;
        }
    }

    private static IEnumerator PlayTimelineAndWait(CharacterUnit trackedUnit, TimelineAsset timeline)
    {
        if (trackedUnit == null || timeline == null)
            yield break;

        var timelineManager = BattleTimelineManager.Instance;
        if (timelineManager != null)
        {
            timelineManager.PlayCasterTimeline(timeline, trackedUnit, trackedUnit.GetCasterBindingTarget());
            while (timelineManager.IsCasterTimelinePlaying(trackedUnit))
                yield return null;
            yield break;
        }

        trackedUnit.PlayBattleTimeline(timeline, trackedUnit.GetCasterBindingTarget());
        while (trackedUnit.IsBattleTimelinePlaying)
            yield return null;
    }

    private IEnumerator WaitBeforeTrigger(ThresholdData threshold, CharacterUnit trackedUnit, NewBattleManager battleManager)
    {
        if (threshold == null)
            yield break;

        switch (threshold.triggerDelayMode)
        {
            case TriggerDelayMode.DelaySeconds:
                if (threshold.triggerDelaySeconds > 0f)
                    yield return new WaitForSecondsRealtime(threshold.triggerDelaySeconds);
                break;
            case TriggerDelayMode.OnUnitTurn:
                if (battleManager == null || trackedUnit == null)
                    yield break;
                while (IsBattleActive(battleManager))
                {
                    if (trackedUnit == null || trackedUnit.IsDead)
                        yield break;
                    if (battleManager.currentCharacterUnit == trackedUnit)
                        yield break;
                    yield return null;
                }
                break;
            case TriggerDelayMode.EndOfCurrentTurn:
                if (battleManager == null)
                    yield break;
                CharacterUnit currentTurnUnit = battleManager.currentCharacterUnit;
                if (currentTurnUnit == null)
                    yield break;
                while (IsBattleActive(battleManager))
                {
                    if (battleManager.currentBattleState == BattleState.EndTurn
                        && battleManager.currentCharacterUnit == currentTurnUnit)
                        yield break;
                    if (battleManager.currentCharacterUnit != currentTurnUnit
                        && battleManager.currentBattleState == BattleState.NewTurn)
                        yield break;
                    yield return null;
                }
                break;
        }
    }

    private static bool IsBattleActive(NewBattleManager battleManager)
    {
        if (battleManager == null)
            return false;

        return battleManager.currentBattleState != BattleState.None
               && battleManager.currentBattleState != BattleState.VictoryScreen_Await
               && battleManager.currentBattleState != BattleState.VictoryScreen_CanContinue
               && battleManager.currentBattleState != BattleState.GameOverScreen_Await
               && battleManager.currentBattleState != BattleState.GameOverScreen_CanContinue;
    }

    private static CharacterUnit ResolveUnit(UnitThresholds unitData, NewBattleManager battleManager)
    {
        if (unitData == null || battleManager == null)
            return null;

        if (unitData.unitData == null)
            return null;

        var model = unitData.unitData.characterBattleModel;
        if (model != null)
        {
            var modelUnit = model.GetComponent<CharacterUnit>();
            if (modelUnit != null && battleManager.unitsInBattle.Contains(modelUnit))
                return modelUnit;
        }

        return FindUnitByData(unitData.unitData, battleManager.unitsInBattle);
    }

    private static CharacterUnit FindUnitByData(CharacterData data, List<CharacterUnit> units)
    {
        if (data == null || units == null)
            return null;

        CharacterUnit firstMatch = null;
        foreach (var unit in units)
        {
            if (unit == null || !MatchesData(unit.Data, data))
                continue;

            if (unit.currentHP > 0f)
                return unit;

            if (firstMatch == null)
                firstMatch = unit;
        }

        return firstMatch;
    }

    private static bool MatchesData(CharacterData candidate, CharacterData reference)
    {
        if (candidate == null || reference == null)
            return false;

        if (candidate == reference)
            return true;

        if (reference.characterBattleModel != null && candidate.characterBattleModel == reference.characterBattleModel)
            return true;

        if (!string.IsNullOrEmpty(reference.characterName)
            && candidate.characterName == reference.characterName)
            return true;

        return false;
    }

    private static void EnsureInactiveTriggersCached()
    {
        if (searchedInactiveTriggers && Instances.Count > 0)
            return;

        searchedInactiveTriggers = true;
        var found = Resources.FindObjectsOfTypeAll<BattleEventTrigger>();
        foreach (var trigger in found)
        {
            if (trigger == null)
                continue;

            if (!trigger.gameObject.scene.IsValid())
                continue;

            if (!Instances.Contains(trigger))
                Instances.Add(trigger);
        }
    }

    private static bool IsLastStandUnit(CharacterUnit unit, NewBattleManager battleManager)
    {
        if (unit == null || battleManager == null || unit.currentHP <= 0f)
            return false;

        bool isPlayerSide = unit.IsPlayerControlled;
        int aliveCount = 0;

        foreach (var candidate in battleManager.unitsInBattle)
        {
            if (candidate == null || candidate.IsPermanentlyDead || candidate.IsPlayerControlled != isPlayerSide)
                continue;

            if (candidate.currentHP > 0f)
            {
                aliveCount++;
                if (aliveCount > 1)
                    return false;
            }
        }

        return aliveCount == 1;
    }

    private static CharacterUnit FindLastStandEnemy(NewBattleManager battleManager)
    {
        if (battleManager == null)
            return null;

        CharacterUnit lastEnemy = null;
        foreach (var candidate in battleManager.unitsInBattle)
        {
            if (candidate == null || candidate.IsPermanentlyDead || candidate.IsPlayerControlled)
                continue;

            if (candidate.currentHP > 0f)
            {
                if (lastEnemy != null)
                    return null;

                lastEnemy = candidate;
            }
        }

        return lastEnemy;
    }

    private static bool IsLastStandCategory(BattleEventCategory category)
    {
        return category == BattleEventCategory.LastStandUnit
            || category == BattleEventCategory.LastStandEnemy
            || category == BattleEventCategory.LastStandAllUnits;
    }

    private static AudioClipSO ResolveAudioClip(ThresholdData threshold, CharacterUnit trackedUnit)
    {
        if (threshold == null)
            return null;

        if (threshold.audioClip != null)
            return threshold.audioClip;

        if (!IsLastStandCategory(threshold.category))
            return null;

        if (trackedUnit == null || trackedUnit.Data == null)
            return null;

        return trackedUnit.Data.lastStandAudioClip;
    }

    private static CharacterUnit FindLastStandUnitForAll(NewBattleManager battleManager, ThresholdData threshold)
    {
        if (battleManager == null || threshold == null)
            return null;

        foreach (var candidate in battleManager.unitsInBattle)
        {
            if (candidate == null || candidate.IsPermanentlyDead || candidate.currentHP <= 0f)
                continue;

            if (!IsLastStandUnit(candidate, battleManager))
                continue;

            if (threshold.HasTriggeredFor(candidate))
                continue;

            return candidate;
        }

        return null;
    }

    private void DebugEvaluate(NewBattleManager battleManager)
    {
        foreach (var unitData in units)
        {
            if (unitData == null)
                continue;

            CharacterUnit tracked = ResolveUnit(unitData, battleManager);
            string unitName = tracked != null ? tracked.name : "(none)";
            string dataName = unitData.unitData != null ? unitData.unitData.characterName : "(no data)";
            string side = tracked != null ? (tracked.IsPlayerControlled ? "Player" : "Enemy") : "(unknown)";
            float hp = tracked != null ? tracked.currentHP : -1f;

            int aliveSameSide = 0;
            int totalSameSide = 0;
            if (tracked != null)
            {
                foreach (var candidate in battleManager.unitsInBattle)
                {
                    if (candidate == null || candidate.IsPermanentlyDead)
                        continue;

                    if (candidate.IsPlayerControlled != tracked.IsPlayerControlled)
                        continue;

                    totalSameSide++;
                    if (candidate.currentHP > 0f)
                        aliveSameSide++;
                }
            }

            Debug.Log($"[BattleEventTrigger] unit={unitName} data={dataName} side={side} hp={hp} alive={aliveSameSide}/{totalSameSide}");
        }
    }
}
