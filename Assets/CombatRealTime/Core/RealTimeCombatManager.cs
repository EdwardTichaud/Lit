using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RealTimeCombatManager : MonoBehaviour
{
    public static RealTimeCombatManager Instance { get; private set; }

    [Header("Session")]
    [SerializeField] private Transform playerRoot;
    [SerializeField] private RealTimeCombatLoadout playerLoadout;
    [SerializeField] private CombatHealth playerHealth;
    [SerializeField] private SquadCharacterController playerController;
    [SerializeField] private LitOpsiveLocomotionBridge playerLocomotionBridge;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private RealTimeCombatInput combatInput;
    [SerializeField] private VisionField playerVision;
    [SerializeField] private RealTimeCombatEnemy lockedEnemy;

    [Header("Lock")]
    [SerializeField, Min(0.1f)] private float lockRange = 6f;
    [SerializeField, Min(0.1f)] private float automaticUnlockRange = 7f;

    [Header("Clarity")]
    [SerializeField, Min(1f)] private float clarityForS = 100f;
    [SerializeField, Min(0f)] private float successfulReactionClarity = 5f;

    private readonly Dictionary<CombatAttackDefinition, float> cooldowns = new Dictionary<CombatAttackDefinition, float>();
    private readonly HashSet<RealTimeCombatReaction> receivedReactions = new HashSet<RealTimeCombatReaction>();
    private readonly HashSet<RealTimeCombatEnemy> attackModeEnemies = new HashSet<RealTimeCombatEnemy>();
    private bool combatActive;
    private bool reactionWindowOpen;
    private bool reactionSucceeded;
    private bool stopPlayerWhenMovementReleased;
    private float clarity;
    private int combatMusicOverrideToken;
    private Coroutine playerCombatAnimationRoutine;
    private Coroutine playerSkillAnimationRoutine;

    public event Action<RealTimeCombatEnemy> LockChanged;
    public event Action<float, CombatClarityRank> ClarityChanged;
    public event Action<RealTimeCombatReactionWindow> ReactionWindowChanged;
    public event Action<CombatAttackDefinition, int> PlayerAttackResolved;
    public event Action<SkillSO, int> EnemyAttackStarted;
    public event Action<int> PlayerDamaged;
    public event Action<bool> CombatResolved;
    public event Action<bool> CombatStateChanged;

    public bool IsCombatActive => combatActive;
    public Transform PlayerRoot => playerRoot;
    public RealTimeCombatLoadout PlayerLoadout => playerLoadout;
    public RealTimeCombatEnemy LockedEnemy => lockedEnemy;
    public float Clarity => clarity;
    public CombatClarityRank ClarityRank => ResolveClarityRank(clarity, clarityForS);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        ResolvePlayerReferences();
        RegisterExistingAttackModes();
    }

    private void OnDestroy()
    {
        ReleaseCombatMusicOverride();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        ResolvePlayerReferences();
        StopResidualPlayerMovementAfterCombat();

        if (combatActive && lockedEnemy != null && IsOutsideAutomaticUnlockRange(lockedEnemy))
        {
            EndCombat();
            return;
        }

    }

    /// <summary>
    /// Recoit l'etat d'agression d'un ennemi et maintient un unique override musical local.
    /// </summary>
    public void SetEnemyAttackMode(RealTimeCombatEnemy enemy, bool active)
    {
        if (enemy == null)
        {
            return;
        }

        if (active)
        {
            attackModeEnemies.Add(enemy);
        }
        else
        {
            attackModeEnemies.Remove(enemy);
        }

        RefreshCombatMusicOverride();
    }

    public bool BeginCombat(Transform player, RealTimeCombatEnemy enemy)
    {
        if (player == null || enemy == null)
        {
            return false;
        }

        playerRoot = player;
        ResolvePlayerReferences();
        combatActive = true;
        stopPlayerWhenMovementReleased = false;
        clarity = 0f;
        cooldowns.Clear();
        SetLockedEnemy(null);
        SetLockedEnemy(enemy);
        combatInput?.SetInputActive(true);
        ClarityChanged?.Invoke(clarity, ClarityRank);
        CombatStateChanged?.Invoke(true);
        return true;
    }

    public void EndCombat()
    {
        combatActive = false;
        reactionWindowOpen = false;
        receivedReactions.Clear();
        reactionSucceeded = false;
        if (lockedEnemy != null)
        {
            lockedEnemy.CompleteRetaliation();
        }

        ReactionWindowChanged?.Invoke(default);
        SetLockedEnemy(null);
        combatInput?.SetInputActive(false);
        stopPlayerWhenMovementReleased = true;
        CombatStateChanged?.Invoke(false);
    }

    /// <summary>
    /// Bascule manuelle disponible tant qu'un ennemi est a portee.
    /// </summary>
    public bool TryToggleManualLock()
    {
        ResolvePlayerReferences();
        if (playerRoot == null)
        {
            return false;
        }

        if (lockedEnemy != null)
        {
            EndCombat();
            return true;
        }

        RealTimeCombatEnemy candidate = FindClosestEnemy(lockRange);
        if (candidate == null)
        {
            return false;
        }

        return BeginCombat(playerRoot, candidate);
    }

    public bool TrySwitchEnemyLock()
    {
        if (!combatActive || lockedEnemy == null)
        {
            return false;
        }

        List<RealTimeCombatEnemy> candidates = FindLockableEnemies(requireVision: true);
        if (candidates.Count < 2)
        {
            return false;
        }

        int currentIndex = candidates.IndexOf(lockedEnemy);
        RealTimeCombatEnemy next = candidates[(currentIndex + 1 + candidates.Count) % candidates.Count];
        if (next == lockedEnemy)
        {
            return false;
        }

        lockedEnemy.CompleteRetaliation();
        reactionWindowOpen = false;
        reactionSucceeded = false;
        receivedReactions.Clear();
        ReactionWindowChanged?.Invoke(default);
        SetLockedEnemy(next);
        return true;
    }

    public RealTimeCombatEnemy FindClosestEnemy(float maximumDistance = 20f)
    {
        if (playerRoot == null)
        {
            return null;
        }

        RealTimeCombatEnemy[] enemies = FindObjectsOfType<RealTimeCombatEnemy>();
        RealTimeCombatEnemy closest = null;
        float closestDistanceSqr = maximumDistance * maximumDistance;
        for (int i = 0; i < enemies.Length; i++)
        {
            RealTimeCombatEnemy candidate = enemies[i];
            if (candidate == null || !candidate.gameObject.activeInHierarchy || (candidate.Health != null && candidate.Health.IsDead))
            {
                continue;
            }

            float distanceSqr = (candidate.transform.position - playerRoot.position).sqrMagnitude;
            if (distanceSqr < closestDistanceSqr)
            {
                closest = candidate;
                closestDistanceSqr = distanceSqr;
            }
        }

        return closest;
    }

    private bool IsOutsideAutomaticUnlockRange(RealTimeCombatEnemy enemy)
    {
        if (playerRoot == null || enemy == null)
        {
            return true;
        }

        RealTimeCombatEnemyBehaviour behaviour = enemy.GetComponent<RealTimeCombatEnemyBehaviour>();
        if (behaviour != null)
        {
            // Une fois l'ennemi alerte, le lock reste valable sur son rayon
            // d'action et pendant sa memoire. Quand il se calme vraiment, le
            // combat local peut se fermer meme si le joueur est encore proche.
            if (!behaviour.IsAlerted && !behaviour.IsInAttackMode)
            {
                return true;
            }

            float alertRange = Mathf.Max(automaticUnlockRange, behaviour.CurrentDisengageDistance);
            return Vector3.Distance(playerRoot.position, enemy.transform.position) > alertRange;
        }

        return Vector3.Distance(playerRoot.position, enemy.transform.position) > automaticUnlockRange;
    }

    private List<RealTimeCombatEnemy> FindLockableEnemies(bool requireVision)
    {
        List<RealTimeCombatEnemy> candidates = new List<RealTimeCombatEnemy>();
        if (playerRoot == null)
        {
            return candidates;
        }

        RealTimeCombatEnemy[] enemies = FindObjectsOfType<RealTimeCombatEnemy>();
        for (int i = 0; i < enemies.Length; i++)
        {
            RealTimeCombatEnemy candidate = enemies[i];
            if (candidate == null || !candidate.gameObject.activeInHierarchy || (candidate.Health != null && candidate.Health.IsDead))
            {
                continue;
            }

            if (Vector3.Distance(playerRoot.position, candidate.transform.position) > lockRange)
            {
                continue;
            }

            if (requireVision && !playerVision.CanSee(candidate.transform))
            {
                continue;
            }

            candidates.Add(candidate);
        }

        candidates.Sort((left, right) =>
        {
            float leftDistance = (left.transform.position - playerRoot.position).sqrMagnitude;
            float rightDistance = (right.transform.position - playerRoot.position).sqrMagnitude;
            return leftDistance.CompareTo(rightDistance);
        });
        return candidates;
    }

    public void SetLockedEnemy(RealTimeCombatEnemy enemy)
    {
        if (lockedEnemy == enemy)
        {
            return;
        }

        if (lockedEnemy != null)
        {
            lockedEnemy.SetLockPresentation(false, false);
        }

        lockedEnemy = enemy;
        if (lockedEnemy != null)
        {
            lockedEnemy.SetLockPresentation(true, true);
        }

        LockChanged?.Invoke(lockedEnemy);
    }

    public bool TryUseAttack(int slotIndex)
    {
        if (!combatActive || lockedEnemy == null || playerLoadout == null || (lockedEnemy.Health != null && lockedEnemy.Health.IsDead))
        {
            return false;
        }

        CombatAttackDefinition attack = playerLoadout.GetAttack(slotIndex);
        if (attack == null || IsOnCooldown(attack) || !IsInRange(attack, lockedEnemy.transform))
        {
            return false;
        }

        int lightDamage = Mathf.Max(1, Mathf.RoundToInt(attack.LightDamage * ResolveKnowledgeModifier().lightDamageMultiplier));
        int applied = lockedEnemy.ReceiveLightDamage(lightDamage);
        if (applied <= 0)
        {
            return false;
        }

        if (playerAnimator != null && !string.IsNullOrWhiteSpace(attack.AnimatorState))
        {
            if (playerSkillAnimationRoutine != null)
            {
                StopCoroutine(playerSkillAnimationRoutine);
                playerSkillAnimationRoutine = null;
            }

            playerAnimator.CrossFade(attack.AnimatorState, 0.06f, 0);
            if (playerCombatAnimationRoutine != null)
            {
                StopCoroutine(playerCombatAnimationRoutine);
            }

            playerCombatAnimationRoutine = StartCoroutine(ReturnToLocomotionAfterCombatAnimation());
        }

        if (attack.ImpactVfxPrefab != null)
        {
            Instantiate(attack.ImpactVfxPrefab, lockedEnemy.transform.position, Quaternion.identity);
        }

        if (attack.ImpactSfx != null)
        {
            AudioManager.PlayClipAtPoint(attack.ImpactSfx, lockedEnemy.transform.position);
        }

        cooldowns[attack] = Time.time + attack.CooldownSeconds;
        AddClarity(attack.ClarityGain * applied);
        PlayerAttackResolved?.Invoke(attack, applied);
        EvaluateCombatOutcome();
        return true;
    }

    /// <summary>
    /// Lance l'animation de la competence selectionnee dans la roue. Les VFX et
    /// degats restent synchronises par ses Animation Events joueur.
    /// </summary>
    public bool TryUseSkill(SkillSO skill)
    {
        if (!combatActive || lockedEnemy == null || skill == null || skill.AnimationClip == null ||
            (lockedEnemy.Health != null && lockedEnemy.Health.IsDead) || playerAnimator == null || playerRoot == null)
        {
            return false;
        }

        if (!TryResolveSkillAnimatorState(skill, out int stateHash, out string animatorStateName))
        {
            Debug.LogWarning("[RealTimeCombatManager] Etat Animator introuvable pour le SkillSO '" + skill.SkillName + "': " + animatorStateName, this);
            return false;
        }

        Vector3 targetDirection = lockedEnemy.LockPoint.position - playerRoot.position;
        targetDirection.y = 0f;
        if (targetDirection.sqrMagnitude > 0.0001f)
        {
            playerRoot.rotation = Quaternion.LookRotation(targetDirection.normalized, Vector3.up);
        }

        playerAnimator.CrossFade(stateHash, 0.06f, 0);
        if (playerCombatAnimationRoutine != null)
        {
            StopCoroutine(playerCombatAnimationRoutine);
            playerCombatAnimationRoutine = null;
        }

        if (playerSkillAnimationRoutine != null)
        {
            StopCoroutine(playerSkillAnimationRoutine);
        }

        playerSkillAnimationRoutine = StartCoroutine(ReturnToLocomotionAfterSkillAnimation(stateHash, skill.AnimationClip.length));
        return true;
    }

    private bool TryResolveSkillAnimatorState(SkillSO skill, out int stateHash, out string attemptedStateName)
    {
        string configuredState = skill.AnimatorState;
        if (!string.IsNullOrWhiteSpace(configuredState))
        {
            attemptedStateName = configuredState.Trim();
            stateHash = Animator.StringToHash(attemptedStateName);
            if (playerAnimator.HasState(0, stateHash))
            {
                return true;
            }
        }

        attemptedStateName = "Base Layer." + skill.AnimationClip.name;
        stateHash = Animator.StringToHash(attemptedStateName);
        if (playerAnimator.HasState(0, stateHash))
        {
            return true;
        }

        attemptedStateName = skill.AnimationClip.name;
        stateHash = Animator.StringToHash(attemptedStateName);
        return playerAnimator.HasState(0, stateHash);
    }

    /// <summary>
    /// Applique un impact de SkillSO declenche par un Animation Event joueur.
    /// Le montant est volontairement celui configure dans le SkillSO, sans
    /// multiplicateur, afin de garder le timing et la lecture auteur exacts.
    /// </summary>
    public int ApplySkillDamageToLockedEnemy(SkillSO skill)
    {
        if (!combatActive || skill == null || lockedEnemy == null ||
            playerRoot == null || (lockedEnemy.Health != null && lockedEnemy.Health.IsDead))
        {
            return 0;
        }

        if (!IsLockedEnemyWithinSkillHitRange(skill))
        {
            return 0;
        }

        int applied = lockedEnemy.ReceiveLightDamage(Mathf.Max(0, Mathf.RoundToInt(skill.Damages)));
        if (applied <= 0)
        {
            return 0;
        }

        EvaluateCombatOutcome();
        return applied;
    }

    public bool IsLockedEnemyWithinSkillHitRange(SkillSO skill)
    {
        if (!combatActive || skill == null || lockedEnemy == null || playerRoot == null ||
            (lockedEnemy.Health != null && lockedEnemy.Health.IsDead))
        {
            return false;
        }

        Vector3 playerPosition = playerRoot.position;
        Vector3 targetPosition = lockedEnemy.LockPoint.position;
        playerPosition.y = 0f;
        targetPosition.y = 0f;
        return skill.IsWithinHitRange(Vector3.Distance(playerPosition, targetPosition));
    }

    /// <summary>
    /// Applique un impact de SkillSO ennemi declenche par un Animation Event.
    /// </summary>
    public int ApplyEnemySkillDamageToPlayer(RealTimeCombatEnemy caster, SkillSO skill)
    {
        if (!combatActive || caster == null || caster != lockedEnemy || skill == null ||
            (caster.Health != null && caster.Health.IsDead))
        {
            return 0;
        }

        int damage = caster.ActiveSkill != null
            ? caster.CommittedRetaliationDamage
            : Mathf.Max(0, Mathf.RoundToInt(skill.Damages));
        int applied = ApplyPlayerDamage(damage);
        if (applied > 0)
        {
            PlayerDamaged?.Invoke(applied);
            EvaluateCombatOutcome();
        }

        return applied;
    }

    public void BeginEnemyAttackWindow(RealTimeCombatEnemy enemy)
    {
        if (!combatActive || enemy == null || enemy != lockedEnemy || enemy.ActiveSkill == null)
        {
            return;
        }

        receivedReactions.Clear();
        reactionSucceeded = false;
        reactionWindowOpen = true;
        ReactionWindowChanged?.Invoke(new RealTimeCombatReactionWindow(enemy.transform, enemy.ActiveSkill, enemy.CommittedRetaliationDamage, true));
        EnemyAttackStarted?.Invoke(enemy.ActiveSkill, enemy.CommittedRetaliationDamage);
    }

    public void RegisterReaction(RealTimeCombatReaction reaction)
    {
        if (!reactionWindowOpen || lockedEnemy == null || lockedEnemy.ActiveSkill == null ||
            (reaction != RealTimeCombatReaction.Dodge && reaction != RealTimeCombatReaction.Jump))
        {
            return;
        }

        SkillSO skill = lockedEnemy.ActiveSkill;
        if (!skill.AcceptsEnemyReaction(reaction))
        {
            return;
        }

        receivedReactions.Add(reaction);
        bool complete = skill.RequireAllEnemyReactions
            ? receivedReactions.Count >= skill.AcceptedEnemyReactions.Count
            : true;
        if (!complete || reactionSucceeded)
        {
            return;
        }

        reactionSucceeded = true;
        AddClarity(successfulReactionClarity);
    }

    public void ResolveEnemyAttackImpact(RealTimeCombatEnemy enemy)
    {
        if (!combatActive || enemy == null || enemy != lockedEnemy || enemy.ActiveSkill == null)
        {
            return;
        }

        reactionWindowOpen = false;
        ReactionWindowChanged?.Invoke(new RealTimeCombatReactionWindow(enemy.transform, enemy.ActiveSkill, enemy.CommittedRetaliationDamage, false));
        if (!reactionSucceeded)
        {
            int damage = enemy.CommittedRetaliationDamage;
            int applied = ApplyPlayerDamage(damage);
            PlayerDamaged?.Invoke(applied);
            EvaluateCombatOutcome();
        }
    }

    public void CompleteEnemyAttack(RealTimeCombatEnemy enemy)
    {
        if (enemy == null || enemy != lockedEnemy)
        {
            return;
        }

        enemy.CompleteRetaliation();
        reactionWindowOpen = false;
        reactionSucceeded = false;
        receivedReactions.Clear();
    }

    public static CombatClarityRank ResolveClarityRank(float value, float sThreshold)
    {
        float normalized = Mathf.Clamp01(value / Mathf.Max(1f, sThreshold));
        if (normalized >= 1f) return CombatClarityRank.S;
        if (normalized >= .80f) return CombatClarityRank.A;
        if (normalized >= .62f) return CombatClarityRank.B;
        if (normalized >= .46f) return CombatClarityRank.C;
        if (normalized >= .30f) return CombatClarityRank.D;
        if (normalized >= .15f) return CombatClarityRank.E;
        return CombatClarityRank.F;
    }

    private void ResolvePlayerReferences()
    {
        if (playerRoot == null)
        {
            playerRoot = LocalPlayerContext.LocalCharacterRoot;
        }

        if (playerRoot == null)
        {
            return;
        }

        if (playerLoadout == null) playerLoadout = playerRoot.GetComponentInChildren<RealTimeCombatLoadout>(true);
        if (playerHealth == null) playerHealth = playerRoot.GetComponentInChildren<CombatHealth>(true);
        if (playerController == null) playerController = playerRoot.GetComponentInChildren<SquadCharacterController>(true);
        if (playerLocomotionBridge == null) playerLocomotionBridge = playerRoot.GetComponentInChildren<LitOpsiveLocomotionBridge>(true);
        if (playerAnimator == null) playerAnimator = playerRoot.GetComponentInChildren<Animator>(true);
        if (playerVision == null) playerVision = playerRoot.GetComponentInChildren<VisionField>(true);
        if (combatInput == null) combatInput = FindAnyObjectByType<RealTimeCombatInput>();
    }

    private void StopResidualPlayerMovementAfterCombat()
    {
        if (!stopPlayerWhenMovementReleased || combatActive ||
            LocalInputRouter.MoveValue.sqrMagnitude > 0.01f)
        {
            return;
        }

        if (playerRoot != null)
        {
            playerLocomotionBridge?.SetExternalPositionAndRotation(playerRoot.position, playerRoot.rotation, true);
        }

        playerController?.Stop();
        playerLocomotionBridge?.StopBridgeInput();
        stopPlayerWhenMovementReleased = false;
    }

    private bool IsInRange(CombatAttackDefinition attack, Transform enemy)
    {
        if (attack.Range == RealTimeCombatRange.Ranged)
        {
            return true;
        }

        return playerRoot != null && enemy != null && Vector3.Distance(playerRoot.position, enemy.position) <= attack.MaximumRange;
    }

    private bool IsOnCooldown(CombatAttackDefinition attack)
    {
        return cooldowns.TryGetValue(attack, out float readyAt) && Time.time < readyAt;
    }

    private void AddClarity(float amount)
    {
        CombatKnowledgeModifier modifier = ResolveKnowledgeModifier();
        clarity = Mathf.Max(0f, clarity + amount + modifier.clarityBonus);
        ClarityChanged?.Invoke(clarity, ClarityRank);
    }

    private System.Collections.IEnumerator ReturnToLocomotionAfterCombatAnimation()
    {
        const string combatRootMotionTag = "RealTimeCombatRootMotion";
        const string locomotionState = "Base Layer.Locomotion";
        bool enteredCombatState = false;

        while (playerAnimator != null)
        {
            AnimatorStateInfo state = playerAnimator.GetCurrentAnimatorStateInfo(0);
            enteredCombatState |= state.IsTag(combatRootMotionTag);
            if (enteredCombatState && state.IsTag(combatRootMotionTag) && state.normalizedTime >= 0.98f)
            {
                PreservePlayerAnimationEndPose();
                playerAnimator.CrossFade(locomotionState, 0.08f, 0);
                break;
            }

            yield return null;
        }

        playerCombatAnimationRoutine = null;
    }

    private System.Collections.IEnumerator ReturnToLocomotionAfterSkillAnimation(int stateHash, float clipDuration)
    {
        const string locomotionState = "Base Layer.Locomotion";
        bool enteredSkillState = false;
        float elapsed = 0f;
        float maximumWaitSeconds = Mathf.Max(0.1f, clipDuration) + 0.25f;

        while (playerAnimator != null && elapsed < maximumWaitSeconds)
        {
            AnimatorStateInfo state = playerAnimator.GetCurrentAnimatorStateInfo(0);
            bool isSkillState = state.fullPathHash == stateHash || state.shortNameHash == stateHash;
            enteredSkillState |= isSkillState;
            if (enteredSkillState && isSkillState && state.normalizedTime >= 0.98f)
            {
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (playerAnimator != null)
        {
            ReturnPlayerToIdle(locomotionState);
        }

        playerSkillAnimationRoutine = null;
    }

    private void ReturnPlayerToIdle(string locomotionState)
    {
        PreservePlayerAnimationEndPose();
        playerAnimator.SetFloat("Speed", 0f);
        playerAnimator.SetFloat("HorizontalMovement", 0f);
        playerAnimator.SetFloat("ForwardMovement", 0f);
        playerAnimator.SetBool("Moving", false);
        playerAnimator.SetBool("IsMoving", false);
        playerAnimator.ResetTrigger("MoveStartTrigger");
        playerAnimator.SetTrigger("MoveStopTrigger");
        playerAnimator.CrossFade(locomotionState, 0.08f, 0);
    }

    private void PreservePlayerAnimationEndPose()
    {
        if (playerRoot == null)
        {
            return;
        }

        if (playerLocomotionBridge == null)
        {
            playerLocomotionBridge = playerRoot.GetComponentInChildren<LitOpsiveLocomotionBridge>(true);
        }

        // Le clip root motion a fini : synchroniser sa pose puis annuler toute capacite
        // UCC encore active afin qu'elle ne conserve pas sa vitesse residuelle.
        playerLocomotionBridge?.SetExternalPositionAndRotation(playerRoot.position, playerRoot.rotation, true);
        playerController?.Stop();
    }

    private int ApplyPlayerDamage(int damage)
    {
        int sanitizedDamage = Mathf.Max(0, damage);
        int applied = playerController != null
            ? playerController.ApplyDamage(sanitizedDamage, "RealTimeCombat")
            : playerHealth != null ? playerHealth.ApplyDamage(sanitizedDamage) : sanitizedDamage;

        CombatDamageWorldFeedback.Show(playerRoot, applied, new Color(1f, 0.48f, 0.48f), 2.05f);
        return applied;
    }

    private void EvaluateCombatOutcome()
    {
        bool enemyDead = lockedEnemy != null && lockedEnemy.Health != null && lockedEnemy.Health.IsDead;
        bool playerDead = playerController != null ? playerController.CurrentHp <= 0 : playerHealth != null && playerHealth.IsDead;
        if (!enemyDead && !playerDead)
        {
            return;
        }

        CombatResolved?.Invoke(enemyDead && !playerDead);
        EndCombat();
    }

    private void RefreshCombatMusicOverride()
    {
        attackModeEnemies.RemoveWhere(enemy => enemy == null || !enemy.gameObject.activeInHierarchy || (enemy.Health != null && enemy.Health.IsDead));
        if (attackModeEnemies.Count > 0)
        {
            if (combatMusicOverrideToken != 0)
            {
                return;
            }

            AudioManager manager = AudioManager.Instance != null ? AudioManager.Instance : AudioManager.EnsureInstance();
            combatMusicOverrideToken = manager.PushMusicOverride(manager.ResolveCombatAudioClip(CombatAudioCue.CombatMusic));
            return;
        }

        ReleaseCombatMusicOverride();
    }

    private void RegisterExistingAttackModes()
    {
        RealTimeCombatEnemyBehaviour[] behaviours = FindObjectsByType<RealTimeCombatEnemyBehaviour>(FindObjectsInactive.Exclude);
        for (int i = 0; i < behaviours.Length; i++)
        {
            RealTimeCombatEnemyBehaviour behaviour = behaviours[i];
            if (behaviour != null && behaviour.IsInAttackMode)
            {
                SetEnemyAttackMode(behaviour.GetComponent<RealTimeCombatEnemy>(), true);
            }
        }
    }

    private void ReleaseCombatMusicOverride()
    {
        if (combatMusicOverrideToken == 0)
        {
            return;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PopMusicOverride(combatMusicOverrideToken);
        }

        combatMusicOverrideToken = 0;
    }

    private static CombatKnowledgeModifier ResolveKnowledgeModifier()
    {
        CombatKnowledgeModifier result = CombatKnowledgeModifier.Identity;
        KnowledgeManager manager = KnowledgeManager.Instance;
        if (manager == null)
        {
            return result;
        }

        IReadOnlyList<KnowledgeSO> knowledge = manager.UnlockedKnowledge;
        for (int i = 0; i < knowledge.Count; i++)
        {
            if (knowledge[i] == null || !knowledge[i].CombatBonusEnabled)
            {
                continue;
            }

            CombatKnowledgeModifier bonus = knowledge[i].CombatModifier;
            result.clarityBonus += bonus.clarityBonus;
            result.lightDamageMultiplier *= Mathf.Max(0f, bonus.lightDamageMultiplier);
        }

        return result;
    }
}
