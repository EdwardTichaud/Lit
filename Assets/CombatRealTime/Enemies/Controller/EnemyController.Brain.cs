using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public sealed partial class EnemyController
{
    public enum CombatPhase
    {
        Idle,
        Alert,
        Chase,
        Position,
        Observe,
        Windup,
        Active,
        Recovery,
        Stagger,
        Suspended,
        Return,
        Dead
    }

    private bool BrainLogDecisions { get => Configuration.BrainLogDecisions; set => Configuration.BrainLogDecisions = value; }
    private EnemyCombatProfileSO BrainProfile;
    private EnemyController BrainEnemy;
    private EnemyController BrainSkills;
    private EnemyController BrainLocomotion;
    private EnemyController BrainMotor;
    private EnemyController BrainNavigation;
    private EnemyController BrainCinematic;
    private CombatTimeDomain BrainClock;
    private SquadCharacterController BrainTarget;
    private EnemyCombatPattern BrainPattern;
    private EnemyCombatPattern BrainPreviousPattern;
    private readonly Dictionary<EnemyCombatPattern, float> BrainCooldowns = new Dictionary<EnemyCombatPattern, float>();
    private Vector3 BrainHome;
    private Quaternion BrainHomeRotation;
    private float BrainReadyAt, BrainObserveUntil, BrainGuardUntil, BrainNextGuardAt, BrainReturnAt;
    private int BrainStepIndex, BrainConsecutiveUses, BrainActionId;
    private bool BrainReturning, BrainSuspended, BrainFinishing;
    private bool BrainVisibilitySampled;
    private bool BrainPreviousVisibility;
    private CombatPhase BrainPhase;
    private NavMeshPath BrainPath;
    private bool BrainAirborneChoiceMade;
    private EnemyCombatPattern BrainAirborneChoice;
    public SquadCharacterController Target => BrainTarget;
    public bool HasProfile => BrainProfile != null;
    public bool IsAutonomousActionActive => BrainPattern != null;
    public CombatPhase Phase => BrainPhase;
    public int ActionId => BrainActionId;
    public bool IsGuarding => BrainNow < BrainGuardUntil && BrainPattern == null && !BrainSuspended;
    public bool OwnsPresentation => BrainSuspended || BrainPattern != null || IsGuarding || BrainPhase == CombatPhase.Dead;
    private float BrainNow => BrainClock != null ? BrainClock.LocalTime : Time.time;
    private bool BrainAuthority => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || (NetworkManager.Singleton.IsServer && IsSpawned);
    private static SquadCharacterController BrainResolvePlayerController(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        SquadCharacterController controller = root.GetComponent<SquadCharacterController>();
        return controller != null ? controller : root.GetComponentInChildren<SquadCharacterController>(true);
    }

    private void BrainAwake()
    {
        BrainPath = new NavMeshPath();
        BrainEnemy = GetComponent<EnemyController>();
        BrainSkills = GetComponent<EnemyController>();
        BrainLocomotion = GetComponent<EnemyController>();
        BrainMotor = GetComponent<EnemyController>();
        BrainNavigation = GetComponent<EnemyController>();
        BrainCinematic = GetComponent<EnemyController>();
        BrainClock = GetComponent<CombatTimeDomain>();
        BrainResolveProfile();
        BrainHome = transform.position;
        BrainHomeRotation = transform.rotation;
    }

    private void BrainStart()
    {
        // SceneMarker applies the final world pose after Instantiate/Awake.
        BrainHome = transform.position;
        BrainHomeRotation = transform.rotation;
    }

    private void BrainOnDisable()
    {
        if (BrainEnemy == null)
            return;
        CancelAction("desactivation");
        BrainTarget = null;
        BrainCooldowns.Clear();
        BrainEnemy.CompleteRetaliation();
    }

    private void BrainUpdate()
    {
        if (BrainProfile == null)
        {
            BrainResolveProfile();
        }

        if (!BrainAuthority || BrainProfile == null || BrainEnemy == null)
            return;
        // Do not rely on RealTimeCombatEnemy.Update having run first. The
        // player can enter the cone between two AI ticks, and the decision
        // must use the current visibility result immediately.
        BrainEnemy.RefreshPlayerVisibility();
        if (BrainLogDecisions && (!BrainVisibilitySampled || BrainPreviousVisibility != BrainEnemy.CanSeePlayer))
        {
            BrainVisibilitySampled = true;
            BrainPreviousVisibility = BrainEnemy.CanSeePlayer;
            Debug.Log("[EnemyCombatBrain] " + name + " | vision=" + BrainEnemy.CanSeePlayer + " | distance=" + BrainEnemy.PlayerVisibilityDistance.ToString("F2") + " | angle=" + BrainEnemy.PlayerVisibilityAngle.ToString("F1") + " | range=" + (BrainEnemy.VisionField != null ? BrainEnemy.VisionField.MaximumDistance.ToString("F1") : "n/a") + " | reason=" + BrainEnemy.PlayerVisibilityReason, this);
        }

        if (BrainEnemy.Health != null && BrainEnemy.Health.IsDead)
        {
            CancelAction("mort");
            BrainSetPhase(CombatPhase.Dead, "mort");
            BrainLocomotion?.StopNavigation();
            return;
        }

        bool blocked = BrainCinematic != null && BrainCinematic.IsSuspended || CombatHealthThresholdController.Instance != null && CombatHealthThresholdController.Instance.ShouldSuspendEnemy(BrainEnemy);
        if (blocked)
        {
            Suspend();
            return;
        }

        if (BrainSuspended)
        {
            BrainSuspended = false;
            BrainReadyAt = BrainNow + .4f;
            BrainSetPhase(CombatPhase.Recovery, "reprise");
        }

        if (BrainPattern != null)
        {
            if (BrainTarget == null || BrainTarget.CurrentHp <= 0)
            {
                CancelAction("cible perdue");
                return;
            }

            return;
        }

        // Foreign actions include the authored QTE failure retaliation.
        if (BrainEnemy.ActiveSkill != null || BrainMotor == null || !BrainMotor.IsOperational || BrainMotor.State != CombatEnemyPhysicsState.Navigation)
            return;
        // Vision is now a valid combat trigger. The player does not need to
        // land the first hit before this brain can pursue and attack.
        if (BrainTarget == null)
        {
            Transform visiblePlayerRoot = LocalPlayerContext.LocalCharacterRoot;
            if (visiblePlayerRoot == null && RealTimeCombatManager.Instance != null)
            {
                visiblePlayerRoot = RealTimeCombatManager.Instance.PlayerRoot;
            }

            SquadCharacterController visiblePlayer = BrainResolvePlayerController(visiblePlayerRoot);
            bool canDetect = visiblePlayer != null && (BrainEnemy.CanSeePlayer || BrainReturning && BrainEnemy.VisionField != null && BrainEnemy.VisionField.CanSenseNearby(visiblePlayer.transform, BrainProfile.returnReengageDistance));
            float entryRadius = BrainReturning ? Mathf.Max(0f, BrainProfile.pursuitRadius - .5f) : BrainProfile.pursuitRadius;
            if (canDetect && visiblePlayer.CurrentHp > 0 && BrainDistance(BrainHome, visiblePlayer.transform.position) <= entryRadius)
            {
                BrainLocomotion.StopNavigation();
                BrainTarget = visiblePlayer;
                BrainObserveUntil = 0f;
                BrainReturning = false;
                BrainReturnAt = 0f;
                BrainReadyAt = BrainNow;
                BrainGuardUntil = 0f;
                BrainAirborneChoiceMade = false;
                BrainAirborneChoice = null;
                Transform playerRoot = visiblePlayer.transform;
                RealTimeCombatManager.Instance?.BeginEnemyAggro(playerRoot, BrainEnemy);
                BrainSetPhase(CombatPhase.Alert, "joueur detecte, reprise decision");
            }
        }

        // Disengagement is a combat decision, not a navigation operation.
        // A temporarily unavailable NavMesh must not keep world interactions locked.
        if (BrainTarget == null || BrainTarget.CurrentHp <= 0 || BrainDistance(BrainHome, BrainTarget.transform.position) > BrainProfile.pursuitRadius)
        {
            BrainTickReturn();
            return;
        }

        if (!BrainNavigation.EnsureReady())
        {
            // A retry window or a world still being built must not erase the
            // target or force a fake Idle transition. Only a confirmed invalid
            // local projection is reported as a real navigation failure.
            if (BrainNavigation.Status == EnemyController.ReadinessStatus.Invalid && BrainTarget == null)
            {
                BrainSetPhase(CombatPhase.Idle, "NavMesh invalide | " + BrainNavigation.LastFailure);
            }

            return;
        }

        BrainReturning = false;
        BrainLocomotion.SetCombatTarget(BrainTarget.transform);
        if (BrainNow < BrainReadyAt || IsGuarding)
        {
            BrainLocomotion.StopNavigation();
            if (IsGuarding)
                BrainSetPhase(CombatPhase.Observe, "garde");
            else
                BrainSetPhase(CombatPhase.Recovery, "recuperation");
            return;
        }

        if (BrainGuardUntil > 0f)
        {
            BrainGuardUntil = 0f;
            BrainEnemy.ReturnToIdleAnimation();
        }

        if (BrainEnemy.IsHitRecovering)
            return;
        float distance = BrainDistance(transform.position, BrainTarget.transform.position);
        if (!BrainTryResolveApproach(distance, out bool inRange, out float attackDistance))
        {
            BrainLocomotion.StopNavigation();
            BrainSetPhase(CombatPhase.Observe, "aucun pattern equipe valide");
            return;
        }

        if (!inRange)
        {
            BrainObserveUntil = 0f;
            bool pursuing = BrainLocomotion.ApproachTarget(attackDistance);
            BrainSetPhase(distance > attackDistance ? CombatPhase.Chase : CombatPhase.Position, pursuing ? "rejoindre portee skill" : BrainLocomotion.PursuitFailure);
            return;
        }

        BrainLocomotion.StopNavigation();
        if (BrainObserveUntil <= 0f)
        {
            BrainObserveUntil = BrainNow + Random.Range(BrainProfile.observationSeconds.x, BrainProfile.observationSeconds.y);
            BrainSetPhase(CombatPhase.Observe, "a portee");
            return;
        }

        if (BrainNow < BrainObserveUntil)
            return;
        EnemyCombatPattern choice = BrainChoosePattern();
        if (choice == null)
        {
            BrainSetPhase(CombatPhase.Observe, BrainExplainAttackWait());
            return;
        }

        BrainLocomotion.StopNavigation();
        if (BrainNow >= BrainNextGuardAt && BrainDistance(transform.position, BrainTarget.transform.position) < 3.5f && Random.value < BrainProfile.guardChance && BrainEnemy.Animator.HasState(0, Animator.StringToHash("Guard")))
        {
            BrainGuardUntil = BrainNow + BrainProfile.guardDurationSeconds;
            BrainNextGuardAt = BrainNow + BrainProfile.guardCooldownSeconds;
            BrainEnemy.Animator.CrossFade("Guard", .08f, 0, 0f);
            BrainObserveUntil = 0f;
            return;
        }

        BrainPattern = choice;
        BrainStepIndex = 0;
        BrainConsecutiveUses = BrainPreviousPattern == choice ? BrainConsecutiveUses + 1 : 1;
        BrainPreviousPattern = choice;
        BrainStartStep();
    }

    private void BrainResolveProfile()
    {
        if (BrainProfile == null)
        {
            BrainProfile = GetComponent<CharacterInfo>()?.CharacterData?.enemyCombatProfile;
        }

        if (BrainProfile == null)
        {
            return;
        }

        // SceneMarker can assign CharacterInfo immediately after Instantiate.
        // Once the profile becomes available, the legacy executor must be
        // disabled as well, otherwise it can stop the same NavMeshAgent.
        var tactical = GetComponent<EnemyTacticalResponseController>();
        if (tactical != null && tactical.enabled)
        {
            tactical.enabled = false;
        }

        BrainLocomotion?.SetFacingSpeed(BrainProfile.trackingDegreesPerSecond);
    }

    public void RegisterThreat(SquadCharacterController source, float amount)
    {
        if (!BrainAuthority || amount <= 0 || BrainProfile == null)
            return;
        if (source == null)
        {
            Transform localPlayer = LocalPlayerContext.LocalCharacterRoot;
            source = BrainResolvePlayerController(localPlayer);
            if (source == null && RealTimeCombatManager.Instance != null)
            {
                source = BrainResolvePlayerController(RealTimeCombatManager.Instance.PlayerRoot);
            }
        }

        if (source == null)
        {
            if (BrainLogDecisions)
            {
                Debug.LogWarning("[EnemyCombatBrain] " + name + " menace recue sans joueur source resolvable.", this);
            }

            return;
        }

        // Keep the target stable throughout a committed attack.
        if (BrainPattern == null || BrainTarget == null)
            BrainTarget = source;
        BrainReturning = false;
        BrainObserveUntil = 0f;
        RealTimeCombatManager.Instance?.BeginEnemyAggro(source.transform, BrainEnemy);
        BrainSetPhase(CombatPhase.Alert, "degat recu");
    }

    public static float ResolveApproachDistance(float minimum, float maximum)
    {
        maximum = Mathf.Max(minimum, maximum);
        return maximum - Mathf.Min(.2f, (maximum - minimum) * .5f);
    }

    private bool BrainTryResolveApproach(float distance, out bool inRange, out float approachDistance)
    {
        bool preferMelee = BrainShouldPreferMelee();
        if (!preferMelee)
        {
            BrainAirborneChoiceMade = false;
            BrainAirborneChoice = null;
        }

        if (preferMelee && !BrainAirborneChoiceMade)
        {
            foreach (var candidate in BrainProfile.patterns)
            {
                if (!BrainIsPatternEquipped(candidate) || !candidate.skills[0].EnemyActionMotion.IsAirborne || !BrainIsPatternAvailable(candidate) || distance < candidate.minimumStartDistance || !BrainCanStart(candidate.skills[0], candidate.maximumStartAngle))
                    continue;
                BrainAirborneChoiceMade = true;
                if (Random.value < BrainProfile.airborneAlternativeChance)
                    BrainAirborneChoice = candidate;
                break;
            }
        }

        inRange = false;
        approachDistance = 0f;
        float bestGap = float.PositiveInfinity;
        int bestPriority = int.MaxValue;
        foreach (var candidate in BrainProfile.patterns)
        {
            if (!BrainIsPatternEquipped(candidate))
                continue;
            SkillSO skill = candidate.skills[0];
            if (preferMelee && skill.EnemyActionMotion.IsAirborne && candidate != BrainAirborneChoice)
                continue;
            float minimum = Mathf.Max(candidate.minimumStartDistance, skill.MinimumHitDistance);
            float maximum = skill.MaximumHitDistance;
            if (minimum > maximum)
                continue;
            float desired = preferMelee && !skill.EnemyActionMotion.IsAirborne ? Mathf.Clamp(BrainProfile.preferredCombatDistance, minimum, ResolveApproachDistance(minimum, maximum)) : ResolveApproachDistance(minimum, maximum);
            inRange |= distance >= minimum && distance <= (preferMelee && !skill.EnemyActionMotion.IsAirborne ? Mathf.Min(maximum, desired + .15f) : maximum);
            int priority = BrainIsPatternAvailable(candidate) ? 0 : 1;
            float gap = Mathf.Abs(distance - desired);
            if (priority > bestPriority || priority == bestPriority && gap >= bestGap)
                continue;
            bestPriority = priority;
            bestGap = gap;
            approachDistance = desired;
        }

        return bestPriority != int.MaxValue;
    }

    private bool BrainIsPatternEquipped(EnemyCombatPattern candidate)
    {
        if (candidate == null || !candidate.IsConfigured || candidate.weight <= 0 || BrainSkills == null)
            return false;
        var equipped = BrainSkills.Skills;
        foreach (var skill in candidate.skills)
            if (!System.Linq.Enumerable.Contains(equipped, skill))
                return false;
        return Mathf.Max(candidate.minimumStartDistance, candidate.skills[0].MinimumHitDistance) <= candidate.skills[0].MaximumHitDistance;
    }

    private bool BrainShouldPreferMelee()
    {
        if (BrainProfile == null || !BrainProfile.preferMeleeApproach)
            return false;
        foreach (var candidate in BrainProfile.patterns)
            if (BrainIsPatternEquipped(candidate) && !candidate.skills[0].EnemyActionMotion.IsAirborne)
                return true;
        return false;
    }

    private bool BrainIsPatternAvailable(EnemyCombatPattern candidate)
    {
        if (!BrainIsPatternEquipped(candidate) || BrainCooldowns.TryGetValue(candidate, out float until) && BrainNow < until)
            return false;
        if (candidate != BrainPreviousPattern || BrainConsecutiveUses < candidate.maximumConsecutiveUses)
            return true;
        // Repetition limits require an equipped alternative, never an unequipped profile entry.
        foreach (var other in BrainProfile.patterns)
            if (other != candidate && BrainIsPatternEquipped(other))
                return false;
        return true;
    }

    private string BrainExplainAttackWait()
    {
        foreach (var candidate in BrainProfile.patterns)
        {
            if (!BrainIsPatternAvailable(candidate))
                continue;
            Vector3 delta = BrainTarget.transform.position - transform.position;
            delta.y = 0f;
            if (!candidate.skills[0].IsWithinHitRange(delta.magnitude) || delta.magnitude < candidate.minimumStartDistance)
                continue;
            if (Vector3.Angle(transform.forward, delta) > candidate.maximumStartAngle)
                return "orientation vers cible";
            return "chemin vers cible indisponible";
        }

        return "cooldown ou limite de repetition";
    }

    private EnemyCombatPattern BrainChoosePattern()
    {
        bool preferMelee = BrainShouldPreferMelee();
        EnemyCombatPattern selected = null;
        int weight = 0;
        foreach (var candidate in BrainProfile.patterns)
        {
            if (!BrainIsPatternAvailable(candidate) || preferMelee && candidate.skills[0].EnemyActionMotion.IsAirborne && candidate != BrainAirborneChoice || BrainDistance(transform.position, BrainTarget.transform.position) < candidate.minimumStartDistance || !BrainCanStart(candidate.skills[0], candidate.maximumStartAngle))
                continue;
            weight += Mathf.Max(1, candidate.weight);
            if (Random.Range(0, weight) < candidate.weight)
                selected = candidate;
        }

        return selected;
    }

    private bool BrainCanStart(SkillSO skill, float angle)
    {
        if (BrainTarget == null || !System.Linq.Enumerable.Contains(BrainSkills.Skills, skill))
            return false;
        Vector3 delta = BrainTarget.transform.position - transform.position;
        delta.y = 0f;
        return skill.IsWithinHitRange(delta.magnitude) && Vector3.Angle(transform.forward, delta) <= angle && BrainNavigation.Agent.CalculatePath(BrainTarget.transform.position, BrainPath) && BrainPath.status == NavMeshPathStatus.PathComplete;
    }

    private void BrainStartStep()
    {
        BrainFinishing = false;
        BrainActionId++;
        BrainLocomotion.StopNavigation();
        BrainLocomotion.SetAttackFacingLocked(false);
        if (!BrainEnemy.TryStartAutonomousAttack(BrainPattern.skills[BrainStepIndex]))
        {
            CancelAction("lancement refuse");
            BrainReadyAt = BrainNow + .5f;
            return;
        }

        BrainSetPhase(CombatPhase.Windup, BrainPattern.name + " coup " + (BrainStepIndex + 1));
    }

    public void LockAttackDirection()
    {
        if (BrainPattern == null)
            return;
        BrainLocomotion.SetAttackFacingLocked(true);
        BrainSetPhase(CombatPhase.Active, "direction engagee");
    }

    public void ResolveAnimationAttackEnded()
    {
        if (!BrainAuthority || BrainPattern == null || BrainFinishing)
            return;
        BrainFinishing = true;
        int token = BrainActionId;
        BrainEnemy.CompleteEnemyAttackWhenGrounded(() => BrainCompleteStep(token, false));
    }

    public void ResolveAttackSafetyTimeout()
    {
        if (BrainPattern != null)
            BrainCompleteStep(BrainActionId, true);
    }

    private void BrainCompleteStep(int token, bool abortCombo)
    {
        if (BrainPattern == null || token != BrainActionId)
            return;
        BrainEnemy.CompleteAutonomousAttack();
        if (!abortCombo && CombatHealthThresholdController.Instance?.HasPendingStage(BrainEnemy) != true && BrainTarget != null && BrainDistance(BrainHome, BrainTarget.transform.position) <= BrainProfile.pursuitRadius && BrainStepIndex + 1 < BrainPattern.skills.Count && BrainCanStart(BrainPattern.skills[BrainStepIndex + 1], BrainPattern.maximumStartAngle))
        {
            BrainStepIndex++;
            BrainStartStep();
            return;
        }

        float recovery = BrainPattern.recoverySeconds;
        BrainCooldowns[BrainPattern] = BrainNow + BrainPattern.cooldownSeconds;
        BrainPattern = null;
        BrainAirborneChoiceMade = false;
        BrainAirborneChoice = null;
        BrainFinishing = false;
        BrainLocomotion.SetAttackFacingLocked(false);
        BrainReadyAt = BrainNow + recovery;
        BrainObserveUntil = 0f;
        BrainEnemy.ReturnToIdleAnimation();
        BrainSetPhase(CombatPhase.Recovery, "pattern termine");
    }

    public void Suspend()
    {
        if (BrainSuspended)
            return;
        BrainSuspended = true;
        CancelAction("suspension");
        BrainGuardUntil = 0f;
        BrainLocomotion.StopNavigation();
        BrainSetPhase(CombatPhase.Suspended, "QTE ou cinematique");
    }

    public void EnterStagger(float seconds)
    {
        if (!BrainAuthority || BrainEnemy.IsAttackCommitted)
            return;
        CancelAction("stagger");
        BrainReadyAt = BrainNow + Mathf.Max(0f, seconds);
        BrainSetPhase(CombatPhase.Stagger, "interruption forcee");
    }

    public void CancelAction(string reason)
    {
        BrainAirborneChoiceMade = false;
        BrainAirborneChoice = null;
        BrainMotor?.EndEnemyAdvance();
        BrainActionId++;
        BrainPattern = null;
        BrainFinishing = false;
        BrainObserveUntil = 0f;
        BrainLocomotion?.SetAttackFacingLocked(false);
        if (BrainEnemy.ActiveSkill != null)
        {
            BrainEnemy.CompleteAutonomousAttack();
            BrainMotor?.PhysicsEndEnemyRush();
            if (BrainMotor != null && BrainMotor.State != CombatEnemyPhysicsState.Cinematic)
                BrainMotor.InterruptEnemyAction(null);
        }

        RealTimeCombatManager.Instance?.CancelEnemyAttackWindow(BrainEnemy);
        RealTimeCombatManager.Instance?.GetComponent<CombatWarningPresentationController>()?.EndWarning(BrainEnemy);
    }

    private void BrainTickReturn()
    {
        // No encounter has started: waiting at home must leave vision armed.
        if (BrainTarget == null && !BrainReturning && BrainDistance(transform.position, BrainHome) <= .25f)
        {
            BrainLocomotion.StopNavigation();
            BrainSetPhase(CombatPhase.Idle, "attente vision");
            return;
        }

        if (!BrainReturning)
        {
            BrainReturning = true;
            BrainReturnAt = BrainNow + BrainProfile.disengagePauseSeconds;
            BrainEnemy.CompleteRetaliation();
            RealTimeCombatManager.Instance?.SetEnemyAttackMode(BrainEnemy, false);
            if (RealTimeCombatManager.Instance != null && RealTimeCombatManager.Instance.EngagedEnemy == BrainEnemy)
                RealTimeCombatManager.Instance.EndCombat();
            BrainTarget = null;
            BrainConsecutiveUses = 0;
            BrainPreviousPattern = null;
            BrainLocomotion.SetCombatTarget(null);
            BrainLocomotion.StopNavigation();
        }

        BrainLocomotion.SetReturnFacing(BrainHome);
        if (BrainNow < BrainReturnAt)
            return;
        if (BrainDistance(transform.position, BrainHome) <= .25f)
        {
            BrainLocomotion.StopNavigation();
            transform.rotation = Quaternion.RotateTowards(transform.rotation, BrainHomeRotation, 180f * Time.deltaTime);
            if (Quaternion.Angle(transform.rotation, BrainHomeRotation) <= 1f)
            {
                BrainReturning = false;
                BrainObserveUntil = 0f;
            }

            BrainSetPhase(CombatPhase.Idle, "spawn");
        }
        else if (BrainNavigation != null && BrainNavigation.EnsureReady())
        {
            BrainLocomotion.NavigateTo(BrainHome, .15f);
            BrainSetPhase(CombatPhase.Return, "retour spawn");
        }
        else
            BrainLocomotion.StopNavigation();
    }

    public int ResolveGuardDamage(int damage)
    {
        if (!IsGuarding || BrainTarget == null)
            return damage;
        Vector3 direction = BrainTarget.transform.position - transform.position;
        return Vector3.Angle(transform.forward, direction) <= 70f ? Mathf.CeilToInt(damage * BrainProfile.guardedDamageMultiplier) : damage;
    }

    private static float BrainDistance(Vector3 a, Vector3 b)
    {
        a.y = b.y;
        return Vector3.Distance(a, b);
    }

    private void BrainSetPhase(CombatPhase next, string reason)
    {
        if (BrainPhase == next)
            return;
        BrainPhase = next;
        if (BrainLogDecisions)
            Debug.Log("[EnemyCombatBrain] " + name + " | " + next + " | " + reason + " | action=" + BrainActionId, this);
    }
}