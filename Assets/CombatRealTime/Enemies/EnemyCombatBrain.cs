using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RealTimeCombatEnemy), typeof(EnemySkills), typeof(EnemyNavigationController))]
public sealed class EnemyCombatBrain : NetworkBehaviour
{
    public enum CombatPhase { Idle, Alert, Chase, Position, Observe, Windup, Active, Recovery, Stagger, Suspended, Return, Dead }
    [SerializeField] private bool logDecisions;
    private EnemyCombatProfileSO profile;
    private RealTimeCombatEnemy enemy;
    private EnemySkills skills;
    private CombatEnemyLocomotionController locomotion;
    private CombatEnemyPhysicsMotor motor;
    private EnemyNavigationController navigation;
    private EnemyCinematicState cinematic;
    private CombatTimeDomain clock;
    private SquadCharacterController target;
    private EnemyCombatPattern pattern;
    private EnemyCombatPattern previousPattern;
    private readonly Dictionary<EnemyCombatPattern, float> cooldowns = new Dictionary<EnemyCombatPattern, float>();
    private readonly HashSet<SquadCharacterController> hitPlayers = new HashSet<SquadCharacterController>();
    private Vector3 home;
    private Quaternion homeRotation;
    private float readyAt, observeUntil, guardUntil, nextGuardAt, returnAt;
    private int stepIndex, consecutiveUses, actionId, windowId;
    private bool returning, suspended, hitboxOpen, finishing;
    private bool visibilitySampled;
    private bool previousVisibility;
    private Vector3 previousHitboxCenter;
    private CombatPhase phase;
    private NavMeshPath path;
    private bool airborneChoiceMade;
    private EnemyCombatPattern airborneChoice;

    public SquadCharacterController Target => target;
    public bool HasProfile => profile != null;
    public bool IsAutonomousActionActive => pattern != null;
    public CombatPhase Phase => phase;
    public int ActionId => actionId;
    public bool IsGuarding => Now < guardUntil && pattern == null && !suspended;
    public bool OwnsPresentation => suspended || pattern != null || IsGuarding || phase == CombatPhase.Dead;
    private float Now => clock != null ? clock.LocalTime : Time.time;
    private bool Authority => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;

    private static SquadCharacterController ResolvePlayerController(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        SquadCharacterController controller = root.GetComponent<SquadCharacterController>();
        return controller != null ? controller : root.GetComponentInChildren<SquadCharacterController>(true);
    }

    private void Awake()
    {
        path = new NavMeshPath();
        enemy = GetComponent<RealTimeCombatEnemy>();
        skills = GetComponent<EnemySkills>();
        locomotion = GetComponent<CombatEnemyLocomotionController>();
        motor = GetComponent<CombatEnemyPhysicsMotor>();
        navigation = GetComponent<EnemyNavigationController>();
        cinematic = GetComponent<EnemyCinematicState>();
        clock = GetComponent<CombatTimeDomain>();
        ResolveProfile();
        home = transform.position;
        homeRotation = transform.rotation;
    }

    private void Start()
    {
        // SceneMarker applies the final world pose after Instantiate/Awake.
        home = transform.position;
        homeRotation = transform.rotation;
    }

    private void OnDisable()
    {
        if (enemy == null) return;
        CancelAction("desactivation");
        target = null;
        cooldowns.Clear();
        enemy.CompleteRetaliation();
    }

    private void Update()
    {
        if (profile == null)
        {
            ResolveProfile();
        }
        if (!Authority || profile == null || enemy == null) return;
        // Do not rely on RealTimeCombatEnemy.Update having run first. The
        // player can enter the cone between two AI ticks, and the decision
        // must use the current visibility result immediately.
        enemy.RefreshPlayerVisibility();
        if (logDecisions && (!visibilitySampled || previousVisibility != enemy.CanSeePlayer))
        {
            visibilitySampled = true;
            previousVisibility = enemy.CanSeePlayer;
            Debug.Log("[EnemyCombatBrain] " + name + " | vision=" + enemy.CanSeePlayer +
                      " | distance=" + enemy.PlayerVisibilityDistance.ToString("F2") +
                      " | angle=" + enemy.PlayerVisibilityAngle.ToString("F1") +
                      " | range=" + (enemy.VisionField != null ? enemy.VisionField.MaximumDistance.ToString("F1") : "n/a") +
                      " | reason=" + enemy.PlayerVisibilityReason, this);
        }
        if (enemy.Health != null && enemy.Health.IsDead)
        {
            CancelAction("mort");
            SetPhase(CombatPhase.Dead, "mort");
            locomotion?.StopNavigation();
            return;
        }
        bool blocked = cinematic != null && cinematic.IsSuspended ||
            CombatHealthThresholdController.Instance != null && CombatHealthThresholdController.Instance.BlocksEnemyActions(enemy);
        if (blocked)
        {
            Suspend();
            return;
        }
        if (suspended)
        {
            suspended = false;
            readyAt = Now + .4f;
            SetPhase(CombatPhase.Recovery, "reprise");
        }
        if (pattern != null)
        {
            if (target == null || target.CurrentHp <= 0) { CancelAction("cible perdue"); return; }
            if (hitboxOpen) SampleHitbox();
            return;
        }
        // Foreign actions include the authored QTE failure retaliation.
        if (enemy.ActiveSkill != null || motor == null || !motor.IsOperational ||
            motor.State != CombatEnemyPhysicsState.Navigation) return;
        // Vision is now a valid combat trigger. The player does not need to
        // land the first hit before this brain can pursue and attack.
        if (target == null && !returning && enemy.CanSeePlayer)
        {
            Transform visiblePlayerRoot = LocalPlayerContext.LocalCharacterRoot;
            if (visiblePlayerRoot == null && RealTimeCombatManager.Instance != null)
            {
                visiblePlayerRoot = RealTimeCombatManager.Instance.PlayerRoot;
            }
            SquadCharacterController visiblePlayer = ResolvePlayerController(visiblePlayerRoot);
            if (visiblePlayer != null && visiblePlayer.CurrentHp > 0)
            {
                target = visiblePlayer;
                observeUntil = 0f;
                returning = false;
                Transform playerRoot = visiblePlayer.transform;
                RealTimeCombatManager.Instance?.BeginEnemyAggro(playerRoot, enemy);
                SetPhase(CombatPhase.Alert, "joueur visible");
            }
        }

        if (!navigation.EnsureReady())
        {
            // A retry window or a world still being built must not erase the
            // target or force a fake Idle transition. Only a confirmed invalid
            // local projection is reported as a real navigation failure.
            if (navigation.Status == EnemyNavigationController.ReadinessStatus.Invalid && target == null)
            {
                SetPhase(CombatPhase.Idle, "NavMesh invalide | " + navigation.LastFailure);
            }
            return;
        }
        if (target == null || target.CurrentHp <= 0 || Distance(home, target.transform.position) > profile.pursuitRadius)
        {
            TickReturn();
            return;
        }
        returning = false;
        locomotion.SetCombatTarget(target.transform);
        if (Now < readyAt || IsGuarding)
        {
            locomotion.StopNavigation();
            if (IsGuarding) SetPhase(CombatPhase.Observe, "garde");
            else SetPhase(CombatPhase.Recovery, "recuperation");
            return;
        }
        if (guardUntil > 0f) { guardUntil = 0f; enemy.ReturnToIdleAnimation(); }
        if (enemy.IsHitRecovering) return;
        float distance = Distance(transform.position, target.transform.position);
        if (!TryResolveApproach(distance, out bool inRange, out float attackDistance))
        {
            locomotion.StopNavigation();
            SetPhase(CombatPhase.Observe, "aucun pattern equipe valide");
            return;
        }
        if (!inRange)
        {
            observeUntil = 0f;
            bool pursuing = locomotion.ApproachTarget(attackDistance);
            SetPhase(distance > attackDistance ? CombatPhase.Chase : CombatPhase.Position,
                pursuing ? "rejoindre portee skill" : locomotion.PursuitFailure);
            return;
        }
        locomotion.StopNavigation();
        if (observeUntil <= 0f)
        {
            observeUntil = Now + Random.Range(profile.observationSeconds.x, profile.observationSeconds.y);
            SetPhase(CombatPhase.Observe, "a portee");
            return;
        }
        if (Now < observeUntil) return;
        EnemyCombatPattern choice = ChoosePattern();
        if (choice == null)
        {
            SetPhase(CombatPhase.Observe, ExplainAttackWait());
            return;
        }
        locomotion.StopNavigation();
        if (Now >= nextGuardAt && Distance(transform.position, target.transform.position) < 3.5f &&
            Random.value < profile.guardChance && enemy.Animator.HasState(0, Animator.StringToHash("Guard")))
        {
            guardUntil = Now + profile.guardDurationSeconds;
            nextGuardAt = Now + profile.guardCooldownSeconds;
            enemy.Animator.CrossFade("Guard", .08f, 0, 0f);
            observeUntil = 0f;
            return;
        }
        pattern = choice;
        stepIndex = 0;
        consecutiveUses = previousPattern == choice ? consecutiveUses + 1 : 1;
        previousPattern = choice;
        StartStep();
    }

    private void ResolveProfile()
    {
        if (profile == null)
        {
            profile = GetComponent<CharacterInfo>()?.CharacterData?.enemyCombatProfile;
        }

        if (profile == null)
        {
            return;
        }

        // SceneMarker can assign CharacterInfo immediately after Instantiate.
        // Once the profile becomes available, the legacy executor must be
        // disabled as well, otherwise it can stop the same NavMeshAgent.
        var legacy = GetComponent<RealTimeCombatEnemyBehaviour>();
        if (legacy != null && legacy.enabled)
        {
            legacy.enabled = false;
        }

        var tactical = GetComponent<EnemyTacticalResponseController>();
        if (tactical != null && tactical.enabled)
        {
            tactical.enabled = false;
        }

        locomotion?.SetFacingSpeed(profile.trackingDegreesPerSecond);
    }

    public void RegisterThreat(SquadCharacterController source, float amount)
    {
        if (!Authority || amount <= 0 || profile == null) return;
        if (source == null)
        {
            Transform localPlayer = LocalPlayerContext.LocalCharacterRoot;
            source = ResolvePlayerController(localPlayer);
            if (source == null && RealTimeCombatManager.Instance != null)
            {
                source = ResolvePlayerController(RealTimeCombatManager.Instance.PlayerRoot);
            }
        }

        if (source == null)
        {
            if (logDecisions)
            {
                Debug.LogWarning("[EnemyCombatBrain] " + name + " menace recue sans joueur source resolvable.", this);
            }
            return;
        }

        // Keep the target stable throughout a committed attack.
        if (pattern == null || target == null) target = source;
        returning = false;
        observeUntil = 0f;
        RealTimeCombatManager.Instance?.BeginEnemyAggro(source.transform, enemy);
        SetPhase(CombatPhase.Alert, "degat recu");
    }

    public static float ResolveApproachDistance(float minimum, float maximum)
    {
        maximum = Mathf.Max(minimum, maximum);
        return maximum - Mathf.Min(.2f, (maximum - minimum) * .5f);
    }

    private bool TryResolveApproach(float distance, out bool inRange, out float approachDistance)
    {
        if (profile.preferMeleeApproach && !airborneChoiceMade)
        {
            foreach (var candidate in profile.patterns)
            {
                if (candidate == null || !candidate.IsConfigured || !candidate.skills[0].EnemyActionMotion.IsAirborne ||
                    !IsPatternAvailable(candidate) || distance < candidate.minimumStartDistance ||
                    !CanStart(candidate.skills[0], candidate.maximumStartAngle)) continue;
                airborneChoiceMade = true;
                if (Random.value < profile.airborneAlternativeChance) airborneChoice = candidate;
                break;
            }
        }
        inRange = false;
        approachDistance = 0f;
        float bestGap = float.PositiveInfinity;
        int bestPriority = int.MaxValue;
        foreach (var candidate in profile.patterns)
        {
            if (candidate == null || !candidate.IsConfigured ||
                !System.Linq.Enumerable.Contains(skills.Skills, candidate.skills[0])) continue;
            SkillSO skill = candidate.skills[0];
            if (profile.preferMeleeApproach && skill.EnemyActionMotion.IsAirborne && candidate != airborneChoice) continue;
            float minimum = Mathf.Max(candidate.minimumStartDistance, skill.MinimumHitDistance);
            float maximum = skill.MaximumHitDistance;
            if (minimum > maximum) continue;
            float desired = profile.preferMeleeApproach && !skill.EnemyActionMotion.IsAirborne
                ? Mathf.Clamp(profile.preferredCombatDistance, minimum, ResolveApproachDistance(minimum, maximum))
                : ResolveApproachDistance(minimum, maximum);
            inRange |= distance >= minimum && distance <= (profile.preferMeleeApproach && !skill.EnemyActionMotion.IsAirborne
                ? Mathf.Min(maximum, desired + .15f) : maximum);
            int priority = IsPatternAvailable(candidate) ? 0 : 1;
            float gap = Mathf.Abs(distance - desired);
            if (priority > bestPriority || priority == bestPriority && gap >= bestGap) continue;
            bestPriority = priority;
            bestGap = gap;
            approachDistance = desired;
        }
        return bestPriority != int.MaxValue;
    }

    private bool IsPatternAvailable(EnemyCombatPattern candidate) =>
        !(cooldowns.TryGetValue(candidate, out float until) && Now < until) &&
        !(candidate == previousPattern && consecutiveUses >= candidate.maximumConsecutiveUses);

    private string ExplainAttackWait()
    {
        foreach (var candidate in profile.patterns)
        {
            if (candidate == null || !candidate.IsConfigured || !IsPatternAvailable(candidate) ||
                !System.Linq.Enumerable.Contains(skills.Skills, candidate.skills[0])) continue;
            Vector3 delta = target.transform.position - transform.position;
            delta.y = 0f;
            if (!candidate.skills[0].IsWithinHitRange(delta.magnitude) || delta.magnitude < candidate.minimumStartDistance) continue;
            if (Vector3.Angle(transform.forward, delta) > candidate.maximumStartAngle) return "orientation vers cible";
            return "chemin vers cible indisponible";
        }
        return "cooldown ou limite de repetition";
    }

    private EnemyCombatPattern ChoosePattern()
    {
        EnemyCombatPattern selected = null;
        int weight = 0;
        foreach (var candidate in profile.patterns)
        {
            if (candidate == null || !candidate.IsConfigured ||
                profile.preferMeleeApproach && candidate.skills[0].EnemyActionMotion.IsAirborne && candidate != airborneChoice ||
                cooldowns.TryGetValue(candidate, out float until) && Now < until ||
                candidate == previousPattern && consecutiveUses >= candidate.maximumConsecutiveUses ||
                Distance(transform.position, target.transform.position) < candidate.minimumStartDistance ||
                !CanStart(candidate.skills[0], candidate.maximumStartAngle)) continue;
            weight += Mathf.Max(1, candidate.weight);
            if (Random.Range(0, weight) < candidate.weight) selected = candidate;
        }
        return selected;
    }

    private bool CanStart(SkillSO skill, float angle)
    {
        if (target == null || !System.Linq.Enumerable.Contains(skills.Skills, skill)) return false;
        Vector3 delta = target.transform.position - transform.position;
        delta.y = 0f;
        return skill.IsWithinHitRange(delta.magnitude) && Vector3.Angle(transform.forward, delta) <= angle &&
            navigation.Agent.CalculatePath(target.transform.position, path) && path.status == NavMeshPathStatus.PathComplete;
    }

    private void StartStep()
    {
        hitboxOpen = false;
        finishing = false;
        windowId = 0;
        hitPlayers.Clear();
        actionId++;
        locomotion.StopNavigation();
        locomotion.SetAttackFacingLocked(false);
        if (!enemy.TryStartAutonomousAttack(pattern.skills[stepIndex]))
        {
            CancelAction("lancement refuse");
            readyAt = Now + .5f;
            return;
        }
        SetPhase(CombatPhase.Windup, pattern.name + " coup " + (stepIndex + 1));
    }

    public void LockAttackDirection()
    {
        if (pattern == null) return;
        locomotion.SetAttackFacingLocked(true);
        SetPhase(CombatPhase.Active, "direction engagee");
    }

    public void OpenHitbox()
    {
        if (!Authority || pattern == null || finishing || hitboxOpen) return;
        windowId++;
        hitPlayers.Clear();
        hitboxOpen = true;
        previousHitboxCenter = transform.TransformPoint(enemy.ActiveSkill.enemyImpact.offset);
        SampleHitbox();
    }

    public void CloseHitbox() { hitboxOpen = false; }

    private void SampleHitbox()
    {
        if (!hitboxOpen || enemy.ActiveSkill == null) return;
        SkillSO skill = enemy.ActiveSkill;
        EnemySkillImpactShape shape = skill.enemyImpact;
        Vector3 center = transform.TransformPoint(shape.offset);
        // Swept volume prevents a moving attack from tunnelling between frames.
        Collider[] hits = Physics.OverlapCapsule(previousHitboxCenter, center, shape.radius, shape.targetMask, QueryTriggerInteraction.Ignore);
        previousHitboxCenter = center;
        foreach (Collider collider in hits)
        {
            var player = collider.GetComponentInParent<SquadCharacterController>();
            if (player == null || player.CurrentHp <= 0 || hitPlayers.Contains(player)) continue;
            Vector3 delta = player.transform.position - transform.position;
            delta.y = 0f;
            if (Vector3.Angle(transform.forward, delta) > shape.arcDegrees * .5f ||
                !skill.IsWithinHitRange(delta.magnitude)) continue;
            hitPlayers.Add(player);
            RealTimeCombatManager.Instance?.ResolveEnemyPatternImpact(enemy, player, skill, actionId, windowId);
        }
    }

    public void ResolveAnimationAttackEnded()
    {
        if (!Authority || pattern == null || finishing) return;
        finishing = true;
        CloseHitbox();
        int token = actionId;
        enemy.CompleteEnemyAttackWhenGrounded(() => CompleteStep(token, false));
    }

    public void ResolveAttackSafetyTimeout()
    {
        if (pattern != null) CompleteStep(actionId, true);
    }

    private void CompleteStep(int token, bool abortCombo)
    {
        if (pattern == null || token != actionId) return;
        enemy.CompleteAutonomousAttack();
        if (!abortCombo && target != null && Distance(home, target.transform.position) <= profile.pursuitRadius &&
            stepIndex + 1 < pattern.skills.Count && CanStart(pattern.skills[stepIndex + 1], pattern.maximumStartAngle))
        {
            stepIndex++;
            StartStep();
            return;
        }
        float recovery = pattern.recoverySeconds;
        cooldowns[pattern] = Now + pattern.cooldownSeconds;
        pattern = null;
        airborneChoiceMade = false;
        airborneChoice = null;
        finishing = false;
        CloseHitbox();
        locomotion.SetAttackFacingLocked(false);
        readyAt = Now + recovery;
        observeUntil = 0f;
        enemy.ReturnToIdleAnimation();
        SetPhase(CombatPhase.Recovery, "pattern termine");
    }

    public void Suspend()
    {
        if (suspended) return;
        suspended = true;
        CancelAction("suspension");
        guardUntil = 0f;
        locomotion.StopNavigation();
        SetPhase(CombatPhase.Suspended, "QTE ou cinematique");
    }

    public void EnterStagger(float seconds)
    {
        if (!Authority) return;
        CancelAction("stagger");
        readyAt = Now + Mathf.Max(0f, seconds);
        SetPhase(CombatPhase.Stagger, "interruption forcee");
    }

    public void CancelAction(string reason)
    {
        airborneChoiceMade = false;
        airborneChoice = null;
        motor?.EndEnemyAdvance();
        actionId++;
        CloseHitbox();
        hitPlayers.Clear();
        pattern = null;
        finishing = false;
        observeUntil = 0f;
        locomotion?.SetAttackFacingLocked(false);
        if (enemy.ActiveSkill != null)
        {
            enemy.CompleteAutonomousAttack();
            motor?.EndEnemyRush();
            if (motor != null && motor.State != CombatEnemyPhysicsState.Cinematic)
                motor.InterruptEnemyAction(null);
        }
        RealTimeCombatManager.Instance?.CancelEnemyAttackWindow(enemy);
        RealTimeCombatManager.Instance?.GetComponent<CombatWarningPresentationController>()?.EndWarning(enemy);
    }

    private void TickReturn()
    {
        // No encounter has started: waiting at home must leave vision armed.
        if (target == null && !returning && Distance(transform.position, home) <= .25f)
        {
            locomotion.StopNavigation();
            SetPhase(CombatPhase.Idle, "attente vision");
            return;
        }
        if (!returning)
        {
            returning = true;
            returnAt = Now + profile.disengagePauseSeconds;
            enemy.CompleteRetaliation();
            RealTimeCombatManager.Instance?.SetEnemyAttackMode(enemy, false);
            if (RealTimeCombatManager.Instance != null && RealTimeCombatManager.Instance.EngagedEnemy == enemy)
                RealTimeCombatManager.Instance.EndCombat();
            target = null;
            consecutiveUses = 0;
            previousPattern = null;
            locomotion.SetCombatTarget(null);
            locomotion.StopNavigation();
        }
        if (Now < returnAt) return;
        if (Distance(transform.position, home) <= .25f)
        {
            locomotion.StopNavigation();
            transform.rotation = Quaternion.RotateTowards(transform.rotation, homeRotation, 180f * Time.deltaTime);
            if (Quaternion.Angle(transform.rotation, homeRotation) <= 1f)
            {
                returning = false;
                observeUntil = 0f;
            }
            SetPhase(CombatPhase.Idle, "spawn");
        }
        else { locomotion.NavigateTo(home, .15f); SetPhase(CombatPhase.Return, "retour spawn"); }
    }

    public int ResolveGuardDamage(int damage)
    {
        if (!IsGuarding || target == null) return damage;
        Vector3 direction = target.transform.position - transform.position;
        return Vector3.Angle(transform.forward, direction) <= 70f
            ? Mathf.CeilToInt(damage * profile.guardedDamageMultiplier) : damage;
    }
    private static float Distance(Vector3 a, Vector3 b) { a.y = b.y; return Vector3.Distance(a, b); }
    private void SetPhase(CombatPhase next, string reason)
    {
        if (phase == next) return;
        phase = next;
        if (logDecisions) Debug.Log("[EnemyCombatBrain] " + name + " | " + next + " | " + reason + " | action=" + actionId, this);
    }
}
