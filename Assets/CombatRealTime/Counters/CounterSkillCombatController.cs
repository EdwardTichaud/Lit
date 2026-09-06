using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[DisallowMultipleComponent]
public sealed class CounterSkillCombatController : MonoBehaviour
{
    public static CounterSkillCombatController Instance { get; private set; }

    [Header("References")]
    [SerializeField] private RealTimeCombatManager combatManager;
    [SerializeField] private PlayableDirector director;
    [SerializeField] private CombatCinematicPlaybackService cinematicPlayback;
    [SerializeField] private CinemachineCamera counterVirtualCamera;
    [SerializeField] private CounterSkillCameraRig cameraRig;
    [SerializeField] private CombatImpactFeedbackController impactFeedback;

    [Header("Counter Skills")]
    [SerializeField] private List<CounterSkillSO> availableSkills = new List<CounterSkillSO>();
    [SerializeField, Tooltip("Riposte cinematique lancee uniquement apres un QTE d'attaque ennemie reussi.")]
    private CounterSkillSO defaultCounterSkill;
    [SerializeField, Range(0f, 1f), Tooltip("Part des degats recus lorsque l'input de garde est maintenu.")]
    private float guardedDamageMultiplier = 0.4f;

    [Header("Guard Feedback")]
    [SerializeField] private string guardAnimatorState = "Base Layer.RealTimeCombat_RootMotion.Guard_Block";
    [SerializeField] private string guardFallbackAnimatorState = "Base Layer.RealTimeCombat_RootMotion.Twinblades_Defense_Hit_Root";
    [SerializeField] private string guardReleaseAnimatorState = "Base Layer.Locomotion";
    [SerializeField, Range(0f, 0.25f)] private float guardAnimationBlendSeconds = 0.08f;
    [SerializeField] private GameObject guardStartVfx;
    [SerializeField] private AudioClipSO guardStartAudio;
    [SerializeField] private GameObject guardedImpactVfx;
    [SerializeField] private AudioClipSO guardedImpactAudio;

    private bool guardHeld;
    private bool cinematicPlaying;
    private bool impactResolved;
    private bool playerLockHeld;
    private bool finishing;
    private CounterSkillSO activeSkill;
    private Animator guardAnimator;
    private bool usingPooledRig;
    private bool abortingPooledRig;
    private TimeManager.TimeRequestHandle counterPauseHandle;
    private readonly List<EnemyCinematicState> suspendedForCounter = new List<EnemyCinematicState>();

    public bool IsCinematicPlaying => cinematicPlaying;
    public bool IsGuardHeld => guardHeld;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }

        Instance = this;
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (director != null) director.stopped += OnDirectorStopped;
    }

    private void Update()
    {
        if (!usingPooledRig && cinematicPlaying && director != null && director.duration > 0d)
        {
            cameraRig?.SetTimelineNormalizedTime((float)(director.time / director.duration));
        }
    }

    private void LateUpdate()
    {
        // Guard is a committed facing state: camera/look-source updates must
        // not let Lucian rotate away from the manually locked enemy.
        if (guardHeld && !cinematicPlaying)
        {
            combatManager?.FacePlayerTowardsEngagedEnemy();
        }
    }

    private void OnDisable()
    {
        if (director != null) director.stopped -= OnDirectorStopped;
        AbortAndRestore();
    }

    private void OnDestroy()
    {
        AbortAndRestore();
        if (Instance == this) Instance = null;
    }

    public void BeginGuard()
    {
        if (!guardHeld)
        {
            combatManager?.FacePlayerTowardsEngagedEnemy();
            PlayGuardAnimation();
            PlayGuardStartFeedback();
        }

        guardHeld = true;
    }

    public bool TryStartFromSuccessfulQte(RealTimeCombatEnemy attacker, SkillSO attack)
    {
        ResolveReferences();
        CounterSkillSO skill = ResolveDefaultCounterSkill();
        if (cinematicPlaying || combatManager == null || skill == null || skill.Timeline == null ||
            (skill.CombatCinematicRigPrefab == null && director == null) ||
            !combatManager.TryBeginCounterCinematic(attacker, attack))
        {
            Debug.LogWarning("[CounterSkill] QTE reussi mais riposte cinematique indisponible. L'attaque continue sans contre simple.", this);
            return false;
        }

        CombatReactionTelegraphController.Instance?.Clear();
        CombatWarningPresentationController.Instance?.ClearImmediate();
        combatManager.CancelPlayerActionForCinematic();
        playerLockHeld = combatManager.TryLockPlayerForCinematic();
        TimeManager manager = TimeManager.EnsureInstance();
        counterPauseHandle = manager != null ? manager.AcquireGlobalPause(this) : default;
        foreach (var state in FindObjectsByType<EnemyCinematicState>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (state.IsSuspended) continue;
            suspendedForCounter.Add(state);
            state.SetSuspended(true);
        }
        if (!StartCounterSkill(skill))
        {
            RestoreCounterEnemies();
            ReleaseCounterPause();
            combatManager.CancelCounterCinematic();
            UnlockPlayer();
            return false;
        }
        guardHeld = false;
        EnemySkills.PlayOutcomeFeedback(attack, combatManager.PlayerRoot, EnemyAttackOutcome.Countered);
        return true;
    }

    public void EndGuard()
    {
        if (guardHeld && !cinematicPlaying)
        {
            StopGuardAnimation();
            LocalPlayerInput.RequestHeldLocomotionReconciliation("Guard released");
        }

        guardHeld = false;
    }

    private bool StartCounterSkill(CounterSkillSO skill)
    {
        if (skill == null || skill.Timeline == null ||
            (skill.CombatCinematicRigPrefab == null && director == null))
        {
            return false;
        }

        activeSkill = skill;
        cinematicPlaying = true;
        impactResolved = false;

        if (skill.CombatCinematicRigPrefab != null)
        {
            if (cinematicPlayback == null) cinematicPlayback = GetComponent<CombatCinematicPlaybackService>();
            string error = "CombatCinematicPlaybackService manquant.";
            if (cinematicPlayback == null || !cinematicPlayback.TryPlay(
                    skill.CombatCinematicRigPrefab,
                    new CombatCinematicContext(combatManager, skill),
                    skill.Timeline,
                    skill.PlayerAnimatorTrackName,
                    skill.EnemyAnimatorTrackName,
                    OnRuntimeRigCompleted,
                    out error))
            {
                Debug.LogWarning("[CounterSkill] Impossible de lancer le rig : " + error, this);
                cinematicPlaying = false;
                combatManager?.CancelCounterCinematic();
                UnlockPlayer();
                return false;
            }

            usingPooledRig = true;
            if (skill.StartSfx != null && combatManager != null && combatManager.PlayerRoot != null)
                AudioManager.PlayClipAtPoint(skill.StartSfx, combatManager.PlayerRoot.position);
            return true;
        }

        director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
        director.playableAsset = skill.Timeline;
        BindTimelineTargets(skill);
        cameraRig?.Begin(combatManager.PlayerRoot, combatManager.EngagedEnemy != null ? combatManager.EngagedEnemy.LockPoint : null);
        if (skill.StartSfx != null && combatManager != null && combatManager.PlayerRoot != null)
        {
            AudioManager.PlayClipAtPoint(skill.StartSfx, combatManager.PlayerRoot.position);
        }

        director.time = 0d;
        director.Evaluate();
        director.Play();
        return true;
    }

    /// <summary>Player Animation Event placed on the authored CounterSkill Timeline contact frame.</summary>
    public void ResolveCounterSkillImpact()
    {
        if (!cinematicPlaying || impactResolved || activeSkill == null || combatManager == null)
        {
            return;
        }

        impactResolved = true;
        int applied = combatManager.ApplyCounterSkillDamage(activeSkill, resolveCombatOutcome: false);
        if (applied > 0 && activeSkill.ImpactSfx != null && combatManager.EngagedEnemy != null)
        {
            AudioManager.PlayClipAtPoint(activeSkill.ImpactSfx, combatManager.EngagedEnemy.LockPoint.position);
        }
    }

    public static int ModifyGuardDamage(int damage, bool playFeedback = true)
    {
        if (damage <= 0 || Instance == null || !Instance.guardHeld || Instance.cinematicPlaying)
        {
            return damage;
        }

        if (playFeedback) Instance.PlayGuardedImpactFeedback();
        return Mathf.Max(0, Mathf.CeilToInt(damage * Instance.guardedDamageMultiplier));
    }

    private void OnDirectorStopped(PlayableDirector stoppedDirector)
    {
        if (stoppedDirector == director && cinematicPlaying)
        {
            FinishCounterCinematic();
        }
    }

    private void FinishCounterCinematic()
    {
        if (finishing) return;
        finishing = true;
        cinematicPlaying = false;
        ReleaseCounterPause();
        combatManager?.CompleteCounterAttack();
        RestoreCounterEnemies();
        UnlockPlayer();
        combatManager?.ResolveDeferredCombatOutcome();
        activeSkill = null;
        if (!usingPooledRig) cameraRig?.End();
        usingPooledRig = false;
        finishing = false;
    }

    private void AbortAndRestore()
    {
        if (finishing) return;
        if (usingPooledRig && cinematicPlayback != null && cinematicPlayback.IsPlaying)
        {
            abortingPooledRig = true;
            cinematicPlayback.StopActive();
            return;
        }
        finishing = true;
        bool hadCounterState = cinematicPlaying;
        cinematicPlaying = false;
        if (director != null && director.state == PlayState.Playing) director.Stop();
        ReleaseCounterPause();
        if (hadCounterState) combatManager?.CancelCounterCinematic();
        RestoreCounterEnemies();
        UnlockPlayer();
        activeSkill = null;
        if (!usingPooledRig) cameraRig?.End();
        usingPooledRig = false;
        finishing = false;
    }

    private void UnlockPlayer()
    {
        if (!playerLockHeld) return;
        combatManager?.UnlockPlayerAfterCinematic();
        playerLockHeld = false;
    }

    private void ReleaseCounterPause()
    {
        if (!counterPauseHandle.IsValid) return;
        TimeManager.Instance?.Release(counterPauseHandle);
        counterPauseHandle = default;
    }

    private void RestoreCounterEnemies()
    {
        foreach (var state in suspendedForCounter)
            if (state != null) state.SetSuspended(false);
        suspendedForCounter.Clear();
    }

    private void PlayGuardStartFeedback()
    {
        Transform player = combatManager != null ? combatManager.PlayerRoot : null;
        if (player == null) return;
        if (guardStartVfx != null) Instantiate(guardStartVfx, player.position, player.rotation, player);
        if (guardStartAudio != null) AudioManager.PlayClipAtPoint(guardStartAudio, player.position);
    }

    private void PlayGuardAnimation()
    {
        ResolveGuardAnimator();
        CrossFadeGuardState(guardAnimatorState);
    }

    private void StopGuardAnimation()
    {
        ResolveGuardAnimator();
        CrossFadeGuardState(guardReleaseAnimatorState);
    }

    private void ResolveGuardAnimator()
    {
        if (guardAnimator == null && combatManager != null)
        {
            guardAnimator = combatManager.PlayerAnimator;
        }
    }

    private void CrossFadeGuardState(string stateName)
    {
        if (guardAnimator == null || string.IsNullOrWhiteSpace(stateName)) return;
        int stateHash = Animator.StringToHash(stateName);
        if (!guardAnimator.HasState(0, stateHash) && stateName == guardAnimatorState)
        {
            stateHash = Animator.StringToHash(guardFallbackAnimatorState);
        }

        if (guardAnimator.HasState(0, stateHash))
        {
            guardAnimator.CrossFade(stateHash, guardAnimationBlendSeconds, 0);
        }
    }

    private void PlayGuardedImpactFeedback()
    {
        Transform player = combatManager != null ? combatManager.PlayerRoot : null;
        if (player == null) return;
        if (guardedImpactVfx != null) Instantiate(guardedImpactVfx, player.position, player.rotation, player);
        if (guardedImpactAudio != null) AudioManager.PlayClipAtPoint(guardedImpactAudio, player.position);
    }

    private void ResolveReferences()
    {
        if (combatManager == null) combatManager = GetComponent<RealTimeCombatManager>();
        if (director == null) director = GetComponent<PlayableDirector>();
        if (cinematicPlayback == null) cinematicPlayback = GetComponent<CombatCinematicPlaybackService>();
        if (impactFeedback == null) impactFeedback = GetComponent<CombatImpactFeedbackController>();
        if (cameraRig == null) cameraRig = GetComponentInChildren<CounterSkillCameraRig>(true);
        if (counterVirtualCamera == null && cameraRig != null) counterVirtualCamera = cameraRig.GetComponent<CinemachineCamera>();
    }

    private CounterSkillSO ResolveDefaultCounterSkill()
    {
        if (IsUsable(defaultCounterSkill)) return defaultCounterSkill;
        for (int i = 0; i < availableSkills.Count; i++)
        {
            if (IsUsable(availableSkills[i])) return availableSkills[i];
        }

        return null;
    }

    private static bool IsUsable(CounterSkillSO skill)
    {
        return skill != null && skill.Timeline != null;
    }

    private void OnRuntimeRigCompleted(CombatCinematicRig rig)
    {
        if (abortingPooledRig)
        {
            abortingPooledRig = false;
            FinishAbortedPooledCinematic();
        }
        else if (cinematicPlaying)
        {
            FinishCounterCinematic();
        }
    }

    private void FinishAbortedPooledCinematic()
    {
        cinematicPlaying = false;
        ReleaseCounterPause();
        combatManager?.CancelCounterCinematic();
        UnlockPlayer();
        activeSkill = null;
        usingPooledRig = false;
    }

    private void BindTimelineTargets(CounterSkillSO skill)
    {
        if (skill == null || skill.Timeline == null || director == null || combatManager == null) return;
        foreach (PlayableBinding output in skill.Timeline.outputs)
        {
            if (output.sourceObject == null) continue;
            if (output.streamName == skill.PlayerAnimatorTrackName && combatManager.PlayerAnimator != null)
            {
                director.SetGenericBinding(output.sourceObject, combatManager.PlayerAnimator);
            }
            else if (output.streamName == skill.EnemyAnimatorTrackName && combatManager.EngagedEnemy != null)
            {
                director.SetGenericBinding(output.sourceObject, combatManager.EngagedEnemy.Animator);
            }
            else if (output.sourceObject is CinemachineTrack track && counterVirtualCamera != null)
            {
                foreach (TimelineClip clip in track.GetClips())
                {
                    if (clip.asset is CinemachineShot shot)
                    {
                        director.SetReferenceValue(shot.VirtualCamera.exposedName, counterVirtualCamera);
                    }
                }
            }
        }
    }
}
