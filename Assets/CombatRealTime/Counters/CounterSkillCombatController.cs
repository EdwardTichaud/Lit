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
    [SerializeField] private CounterSkillWheel wheel;
    [SerializeField] private CinemachineCamera counterVirtualCamera;
    [SerializeField] private CounterSkillCameraRig cameraRig;
    [SerializeField] private CombatImpactFeedbackController impactFeedback;

    [Header("Counter Skills")]
    [SerializeField] private List<CounterSkillSO> availableSkills = new List<CounterSkillSO>();
    [SerializeField, Range(0f, 1f), Tooltip("Part des degats recus lorsque South est maintenu hors parade parfaite.")]
    private float guardedDamageMultiplier = 0.4f;

    [Header("Guard Feedback")]
    [SerializeField, Range(0f, 0.25f)] private float guardAnimationBlendSeconds = 0.08f;
    [SerializeField] private GameObject guardStartVfx;
    [SerializeField] private AudioClipSO guardStartAudio;
    [SerializeField] private GameObject guardedImpactVfx;
    [SerializeField] private AudioClipSO guardedImpactAudio;

    private bool guardHeld;
    private bool selectionOpen;
    private bool cinematicPlaying;
    private bool impactResolved;
    private bool playerLockHeld;
    private bool finishing;
    private CounterSkillSO activeSkill;
    private Animator guardAnimator;
    private bool usingPooledRig;
    private bool abortingPooledRig;

    public bool IsSelectionOpen => selectionOpen;
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
            combatManager?.FacePlayerTowardsLockedEnemy();
            PlayGuardAnimation();
            PlayGuardStartFeedback();
        }

        guardHeld = true;
        if (selectionOpen || cinematicPlaying || combatManager == null || !combatManager.TryBeginCounterSelection())
        {
            return;
        }

        // The perfect-window prompt must be gone before the counter wheel owns the screen.
        CombatReactionTelegraphController.Instance?.Clear();

        List<CounterSkillSO> usableSkills = GetUsableSkills();
        if (wheel == null || !wheel.Open(usableSkills))
        {
            combatManager.CancelCounterSelection();
            return;
        }

        selectionOpen = true;
        combatManager.CancelPlayerActionForCinematic();
        playerLockHeld = combatManager.TryLockPlayerForCinematic();
        impactFeedback?.PushExternalPause();
    }

    public void EndGuard()
    {
        if (guardHeld && !selectionOpen && !cinematicPlaying)
        {
            StopGuardAnimation();
        }

        guardHeld = false;
    }

    public void Navigate(Vector2 direction)
    {
        if (selectionOpen) wheel?.Navigate(direction);
    }

    public bool ConfirmSelection()
    {
        if (!selectionOpen || wheel == null)
        {
            return false;
        }

        CounterSkillSO skill = wheel.SelectedSkill;
        if (skill == null || skill.Timeline == null ||
            (skill.CombatCinematicRigPrefab == null && director == null))
        {
            return false;
        }

        activeSkill = skill;
        selectionOpen = false;
        wheel.Close();
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
                combatManager?.CancelCounterSelection();
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
        cameraRig?.Begin(combatManager.PlayerRoot, combatManager.LockedEnemy != null ? combatManager.LockedEnemy.LockPoint : null);
        if (skill.StartSfx != null && combatManager != null && combatManager.PlayerRoot != null)
        {
            AudioManager.PlayClipAtPoint(skill.StartSfx, combatManager.PlayerRoot.position);
        }

        director.time = 0d;
        director.Evaluate();
        director.Play();
        return true;
    }

    public void CancelSelection()
    {
        if (!selectionOpen)
        {
            return;
        }

        selectionOpen = false;
        wheel?.Close();
        impactFeedback?.PopExternalPause();
        combatManager?.CancelCounterSelection();
        UnlockPlayer();
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
        if (applied > 0 && activeSkill.ImpactSfx != null && combatManager.LockedEnemy != null)
        {
            AudioManager.PlayClipAtPoint(activeSkill.ImpactSfx, combatManager.LockedEnemy.LockPoint.position);
        }
    }

    public static int ModifyGuardDamage(int damage)
    {
        if (damage <= 0 || Instance == null || !Instance.guardHeld || Instance.selectionOpen || Instance.cinematicPlaying)
        {
            return damage;
        }

        Instance.PlayGuardedImpactFeedback();
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
        impactFeedback?.PopExternalPause();
        combatManager?.CompleteCounterAttack();
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
        bool hadCounterState = selectionOpen || cinematicPlaying;
        selectionOpen = false;
        cinematicPlaying = false;
        wheel?.Close();
        if (director != null && director.state == PlayState.Playing) director.Stop();
        impactFeedback?.PopExternalPause();
        if (hadCounterState) combatManager?.CancelCounterSelection();
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
        CrossFadeGuardState(PlayerModelAnimationState.Guard, PlayerModelAnimationState.GuardFallback);
    }

    private void StopGuardAnimation()
    {
        ResolveGuardAnimator();
        CrossFadeGuardState(PlayerModelAnimationState.GuardRelease, PlayerModelAnimationState.Locomotion);
    }

    private void ResolveGuardAnimator()
    {
        if (guardAnimator == null && combatManager != null)
        {
            guardAnimator = combatManager.PlayerAnimator;
        }
    }

    private void CrossFadeGuardState(PlayerModelAnimationState state, PlayerModelAnimationState fallback)
    {
        PlayerModelAnimationProfile profile = combatManager != null ? combatManager.PlayerAnimationProfile : null;
        if (!PlayerAnimatorStateResolver.TryResolve(guardAnimator, profile, state, out int stateHash, out _))
        {
            if (!PlayerAnimatorStateResolver.TryResolve(guardAnimator, profile, fallback, out stateHash, out _))
            {
                Debug.LogWarning("[CounterSkill] Etat de garde introuvable : " + state, this);
                return;
            }
        }

        guardAnimator.CrossFade(stateHash, guardAnimationBlendSeconds, 0);
    }

    private void PlayGuardedImpactFeedback()
    {
        Transform player = combatManager != null ? combatManager.PlayerRoot : null;
        if (player == null) return;
        if (guardedImpactVfx != null) Instantiate(guardedImpactVfx, player.position, player.rotation, player);
        if (guardedImpactAudio != null) AudioManager.PlayClipAtPoint(guardedImpactAudio, player.position);
    }

    private List<CounterSkillSO> GetUsableSkills()
    {
        List<CounterSkillSO> result = new List<CounterSkillSO>();
        for (int i = 0; i < availableSkills.Count; i++)
        {
            CounterSkillSO skill = availableSkills[i];
            if (skill != null && skill.Timeline != null && !result.Contains(skill)) result.Add(skill);
        }
        return result;
    }

    private void ResolveReferences()
    {
        if (combatManager == null) combatManager = GetComponent<RealTimeCombatManager>();
        if (director == null) director = GetComponent<PlayableDirector>();
        if (cinematicPlayback == null) cinematicPlayback = GetComponent<CombatCinematicPlaybackService>();
        if (impactFeedback == null) impactFeedback = GetComponent<CombatImpactFeedbackController>();
        if (wheel == null) wheel = FindAnyObjectByType<CounterSkillWheel>(FindObjectsInactive.Include);
        if (cameraRig == null) cameraRig = GetComponentInChildren<CounterSkillCameraRig>(true);
        if (counterVirtualCamera == null && cameraRig != null) counterVirtualCamera = cameraRig.GetComponent<CinemachineCamera>();
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
        selectionOpen = false;
        cinematicPlaying = false;
        wheel?.Close();
        impactFeedback?.PopExternalPause();
        combatManager?.CancelCounterSelection();
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
            else if (output.streamName == skill.EnemyAnimatorTrackName && combatManager.LockedEnemy != null)
            {
                director.SetGenericBinding(output.sourceObject, combatManager.LockedEnemy.Animator);
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
