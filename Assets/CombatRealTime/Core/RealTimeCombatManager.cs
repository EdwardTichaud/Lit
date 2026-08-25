using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    [SerializeField] private PlayerActionPresentationController playerActionPresentation;
    [SerializeField] private CombatMobilityController playerMobility;
    [SerializeField] private RealTimeCombatInput combatInput;
    [SerializeField] private CombatSkillCinematicController combatSkillCinematicController;
    [SerializeField] private VisionField playerVision;
    [SerializeField] private RealTimeCombatEnemy lockedEnemy;

    [Header("Lock")]
    [SerializeField, Min(0.1f)] private float lockRange = 6f;
    [SerializeField, Min(0.1f)] private float automaticUnlockRange = 7f;
    [SerializeField, Tooltip("Etend les portees de lock selon la plus grande composante de scale de l'ennemi. Les ennemis de scale inferieure a 1 gardent la portee de base.")]
    private bool scaleLockRangeWithEnemy = true;

    [Header("Clarity")]
    [SerializeField, Min(1f)] private float clarityForS = 100f;
    [SerializeField, Min(0f)] private float successfulReactionClarity = 5f;

    [Header("Defeat")]
    [SerializeField] private string playerDeathAnimatorState = "Base Layer.Death";
    [SerializeField, Min(0f)] private float defeatPanelExtraDelaySeconds = 0.25f;
    [SerializeField, Min(0.1f)] private float playerDeathFallbackDuration = 1f;
    [SerializeField, Tooltip("Journalise les sorties automatiques de combat pour diagnostiquer les cinematiques.")]
    private bool logCombatDisengageDiagnostics = true;

    [Header("Damage Reaction")]
    [SerializeField] private string playerHurtAnimatorState = "Base Layer.RealTimeCombat_RootMotion.TwinSword_Defense_Hit_Root";
    [SerializeField, Range(0f, 0.25f)] private float playerHurtTransitionDuration = 0.05f;

    private readonly Dictionary<CombatAttackDefinition, float> cooldowns = new Dictionary<CombatAttackDefinition, float>();
    private readonly HashSet<RealTimeCombatReaction> receivedReactions = new HashSet<RealTimeCombatReaction>();
    private readonly HashSet<RealTimeCombatEnemy> attackModeEnemies = new HashSet<RealTimeCombatEnemy>();
    private bool combatActive;
    private bool reactionWindowOpen;
    private bool reactionSucceeded;
    private int reactionWindowToken;
    private Coroutine reactionWindowRoutine;
    private bool stopPlayerWhenMovementReleased;
    private float clarity;
    private int combatMusicOverrideToken;
    private Coroutine playerDefeatRoutine;

    public event Action<RealTimeCombatEnemy> LockChanged;
    public event Action<float, CombatClarityRank> ClarityChanged;
    public event Action<RealTimeCombatReactionWindow> ReactionWindowChanged;
    public event Action<CombatAttackDefinition, int> PlayerAttackResolved;
    public event Action<int> PlayerLightDamageApplied;
    public event Action<SkillSO, int> PlayerSkillImpactApplied;
    public event Action<SkillSO, int> EnemyAttackStarted;
    public event Action<SkillSO, bool> ReactionImpactResolved;
    public event Action<int> PlayerDamaged;
    public event Action<bool> CombatResolved;
    public event Action<bool> CombatStateChanged;

    public bool IsCombatActive => combatActive;
    public bool IsCinematicSequenceActive { get; private set; }
    public Transform PlayerRoot => playerRoot;
    public Animator PlayerAnimator => playerAnimator;
    public RealTimeCombatLoadout PlayerLoadout => playerLoadout;
    public RealTimeCombatEnemy LockedEnemy => lockedEnemy;
    public float Clarity => clarity;
    public CombatClarityRank ClarityRank => ResolveClarityRank(clarity, clarityForS);
    public bool CanAcceptBasicSkillInput => playerActionPresentation == null || playerActionPresentation.CanAcceptBasicSkillInput;
    public CombatSkillCinematicController CombatSkillCinematicController => combatSkillCinematicController;
    public bool IsPlayerActionActive => playerActionPresentation != null && playerActionPresentation.IsActionActive;

    /// <summary>Faces Lucian toward the current manual lock without starting an action.</summary>
    public bool FacePlayerTowardsLockedEnemy()
    {
        return lockedEnemy != null && FacePlayerTowards(lockedEnemy.LockPoint.position);
    }

    /// <summary>Faces Lucian toward a world direction. Used by intentional directional evasions.</summary>
    public bool FacePlayerTowardsDirection(Vector3 worldDirection)
    {
        if (playerRoot == null)
        {
            return false;
        }

        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        LitOpsiveLocomotionBridge bridge = playerRoot.GetComponentInChildren<LitOpsiveLocomotionBridge>(true);
        if (bridge != null && bridge.SetActionFacingDirection(worldDirection))
        {
            return true;
        }

        // Lock movement has one yaw authority: the UCC bridge. Do not fall
        // back to a direct Transform write while combat is active.
        if (combatActive)
        {
            return false;
        }

        playerRoot.rotation = Quaternion.LookRotation(worldDirection.normalized, Vector3.up);
        return true;
    }

    private bool FacePlayerTowards(Vector3 worldPosition)
    {
        if (playerRoot == null)
        {
            return false;
        }

        Vector3 direction = worldPosition - playerRoot.position;
        return FacePlayerTowardsDirection(direction);
    }
    public bool CanChainBasicSkill => playerActionPresentation != null && playerActionPresentation.CanChainBasicSkill;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        if (combatSkillCinematicController == null)
        {
            combatSkillCinematicController = GetComponent<CombatSkillCinematicController>();
            if (combatSkillCinematicController == null)
            {
                combatSkillCinematicController = gameObject.AddComponent<CombatSkillCinematicController>();
            }
        }
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
        RefreshLockedEnemyStrafeBinding();
        StopResidualPlayerMovementAfterCombat();

        // A cinematic owns the player and enemy transforms. The enemy AI is intentionally
        // suspended then, so it must not be interpreted as a normal combat disengagement.
        if (combatActive && !IsCinematicSequenceActive && !HasValidLockedEnemy())
        {
            if (logCombatDisengageDiagnostics)
            {
                Debug.Log("[RealTimeCombat Debug] EndCombat automatique | cible verrouillee invalide ou detruite.", this);
            }

            EndCombat();
            return;
        }

        if (combatActive && !IsCinematicSequenceActive && IsOutsideAutomaticUnlockRange(lockedEnemy))
        {
            if (logCombatDisengageDiagnostics)
            {
                Debug.Log("[RealTimeCombat Debug] EndCombat automatique | enemy='" + lockedEnemy.name +
                          "' | player=" + (playerRoot != null ? playerRoot.position.ToString("F2") : "None") +
                          " | enemyPos=" + lockedEnemy.transform.position.ToString("F2") + ".", this);
            }
            EndCombat();
            return;
        }

    }

    private bool HasValidLockedEnemy()
    {
        return lockedEnemy != null &&
               lockedEnemy.gameObject.activeInHierarchy &&
               (lockedEnemy.Health == null || !lockedEnemy.Health.IsDead);
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
        IsCinematicSequenceActive = false;
        CloseReactionWindow(notify: false);
        if (lockedEnemy != null)
        {
            lockedEnemy.CompleteRetaliation();
        }

        ReactionWindowChanged?.Invoke(default);
        SetLockedEnemy(null);
        playerLocomotionBridge?.ClearCombatLockTarget();
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

        RealTimeCombatEnemy candidate = FindPreferredLockableEnemy();
        if (candidate == null)
        {
            return false;
        }

        return TryLockEnemy(candidate);
    }

    /// <summary>
    /// Lance un combat auteur : verrouille l'ennemi prioritaire, demarre la
    /// musique et lui applique des degats de lumiere comme un coup joueur.
    /// </summary>
    public bool LaunchCombat(float openingDamage = 50f)
    {
        ResolvePlayerReferences();
        RealTimeCombatEnemy enemy = lockedEnemy != null
            ? lockedEnemy
            : FindPreferredLockableEnemy();
        if (enemy == null || !TryLockEnemy(enemy))
        {
            return false;
        }

        SetEnemyAttackMode(enemy, true);
        int applied = enemy.ReceiveLightDamage(Mathf.Max(0, Mathf.RoundToInt(openingDamage)));
        if (applied > 0)
        {
            EvaluateCombatOutcome();
        }

        return true;
    }

    public bool TryLockEnemy(RealTimeCombatEnemy enemy)
    {
        ResolvePlayerReferences();
        if (playerRoot == null || enemy == null || !enemy.gameObject.activeInHierarchy ||
            (enemy.Health != null && enemy.Health.IsDead) ||
            Vector3.Distance(playerRoot.position, enemy.transform.position) > GetLockRange(enemy))
        {
            return false;
        }

        if (combatActive)
        {
            return lockedEnemy == enemy;
        }

        return BeginCombat(playerRoot, enemy);
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
        CloseReactionWindow(notify: false);
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

            float alertRange = Mathf.Max(GetAutomaticUnlockRange(enemy), behaviour.CurrentDisengageDistance);
            return Vector3.Distance(playerRoot.position, enemy.transform.position) > alertRange;
        }

        return Vector3.Distance(playerRoot.position, enemy.transform.position) > GetAutomaticUnlockRange(enemy);
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

            if (Vector3.Distance(playerRoot.position, candidate.transform.position) > GetLockRange(candidate))
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

    private RealTimeCombatEnemy FindPreferredLockableEnemy()
    {
        if (playerRoot == null)
        {
            return null;
        }

        RealTimeCombatEnemy[] enemies = FindObjectsOfType<RealTimeCombatEnemy>();
        RealTimeCombatEnemy preferred = null;
        float preferredScale = float.NegativeInfinity;
        float preferredDistanceSqr = float.PositiveInfinity;
        for (int i = 0; i < enemies.Length; i++)
        {
            RealTimeCombatEnemy candidate = enemies[i];
            if (candidate == null || !candidate.gameObject.activeInHierarchy ||
                (candidate.Health != null && candidate.Health.IsDead))
            {
                continue;
            }

            float distanceSqr = (candidate.transform.position - playerRoot.position).sqrMagnitude;
            float range = GetLockRange(candidate);
            if (distanceSqr > range * range)
            {
                continue;
            }

            float scale = GetEnemyLockScaleMultiplier(candidate);
            if (scale > preferredScale || (Mathf.Approximately(scale, preferredScale) && distanceSqr < preferredDistanceSqr))
            {
                preferred = candidate;
                preferredScale = scale;
                preferredDistanceSqr = distanceSqr;
            }
        }

        return preferred;
    }

    private float GetLockRange(RealTimeCombatEnemy enemy)
    {
        return lockRange * GetEnemyLockScaleMultiplier(enemy);
    }

    private float GetAutomaticUnlockRange(RealTimeCombatEnemy enemy)
    {
        return automaticUnlockRange * GetEnemyLockScaleMultiplier(enemy);
    }

    private float GetEnemyLockScaleMultiplier(RealTimeCombatEnemy enemy)
    {
        if (!scaleLockRangeWithEnemy || enemy == null)
        {
            return 1f;
        }

        Vector3 scale = enemy.transform.lossyScale;
        return Mathf.Max(1f, Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
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

        ResolvePlayerReferences();
        RefreshLockedEnemyStrafeBinding();

        LockChanged?.Invoke(lockedEnemy);
    }

    public bool TryUseAttack(int slotIndex)
    {
        if (!combatActive || IsPlayerDead() || lockedEnemy == null || playerLoadout == null ||
            (lockedEnemy.Health != null && lockedEnemy.Health.IsDead))
        {
            return false;
        }

        CombatAttackDefinition attack = playerLoadout.GetAttack(slotIndex);
        if (attack == null || IsOnCooldown(attack) || !IsInRange(attack, lockedEnemy.transform))
        {
            return false;
        }

        if (playerActionPresentation == null || !playerActionPresentation.CanStartAction)
        {
            return false;
        }

        if (playerAnimator != null && !string.IsNullOrWhiteSpace(attack.AnimatorState) &&
            !playerAnimator.HasState(0, Animator.StringToHash(attack.AnimatorState)))
        {
            Debug.LogWarning("[RealTimeCombatManager] Etat Animator introuvable pour l'attaque '" + attack.name + "': " + attack.AnimatorState, this);
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
            FaceLockedEnemyForAction();
            if (playerActionPresentation == null ||
                !playerActionPresentation.TryPlayCombatState(
                    attack.AnimatorState,
                    PlayerActionPresentationProfile.CreateDefault(),
                    attack.name))
            {
                return false;
            }
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
        PlayerLightDamageApplied?.Invoke(applied);
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
        if (!combatActive || IsPlayerDead() || lockedEnemy == null || skill == null ||
            (lockedEnemy.Health != null && lockedEnemy.Health.IsDead) || playerAnimator == null || playerRoot == null)
        {
            return false;
        }

        if (skill.RequireValidRangeToStart && !ValidateLockedEnemySkillRange(skill, true))
        {
            return false;
        }

        if (skill.HasCombatCinematic)
        {
            FaceLockedEnemyForAction();
            bool cinematicStarted = combatSkillCinematicController != null && combatSkillCinematicController.TryPlayPlayerSkill(skill);
            PlayPlayerSkillStartSfx(skill, cinematicStarted);
            return cinematicStarted;
        }

        if (skill.AnimationClip == null)
        {
            return false;
        }

        if (!TryResolveSkillAnimatorState(skill, out int stateHash, out string animatorStateName))
        {
            Debug.LogWarning("[RealTimeCombatManager] Etat Animator introuvable pour le SkillSO '" + skill.SkillName + "': " + animatorStateName, this);
            return false;
        }

        FaceLockedEnemyForAction();
        bool actionStarted = playerActionPresentation != null && playerActionPresentation.TryPlaySkill(skill, stateHash);
        PlayPlayerSkillStartSfx(skill, actionStarted);
        return actionStarted;
    }

    private void PlayPlayerSkillStartSfx(SkillSO skill, bool actionStarted)
    {
        if (!actionStarted || skill == null || skill.PlayerAttackSfx == null || playerRoot == null)
        {
            return;
        }

        AudioManager.PlayClipAtPoint(skill.PlayerAttackSfx, playerRoot.position);
    }

    public bool TryPlayEnemySkillCinematic(RealTimeCombatEnemy caster, SkillSO skill)
    {
        return combatSkillCinematicController != null && combatSkillCinematicController.TryPlayEnemySkill(caster, skill);
    }

    public System.Collections.IEnumerator WaitForPlayerActionChainWindow()
    {
        if (playerActionPresentation != null)
        {
            yield return playerActionPresentation.WaitForChainWindow();
        }
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

    private void FaceLockedEnemyForAction()
    {
        if (playerRoot == null || lockedEnemy == null)
        {
            return;
        }

        if (playerLocomotionBridge == null)
        {
            playerLocomotionBridge = playerRoot.GetComponentInChildren<LitOpsiveLocomotionBridge>(true);
        }

        Transform target = lockedEnemy.LockPoint != null ? lockedEnemy.LockPoint : lockedEnemy.transform;
        playerActionPresentation?.SetActionFacingTarget(target);
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

        if (!TryGetLockedEnemyHitDistance(out float distance) || !skill.IsWithinHitRange(distance))
        {
            string message = distance < skill.MinimumHitDistance
                ? "Raté (trop près)"
                : "Raté (trop loin)";
            CombatDamageWorldFeedback.ShowMessage(
                lockedEnemy.transform,
                message,
                new Color(1f, 0.82f, 0.38f),
                2.25f);
            return 0;
        }

        int applied = lockedEnemy.ReceiveLightDamage(Mathf.Max(0, Mathf.RoundToInt(skill.Damages)));
        if (applied <= 0)
        {
            return 0;
        }

        PlayerLightDamageApplied?.Invoke(applied);
        PlayerSkillImpactApplied?.Invoke(skill, applied);
        EvaluateCombatOutcome();
        return applied;
    }

    /// <summary>
    /// Resolves the single impact of an active LightSkill cinematic. The
    /// LightSkill meter is intentionally not fed by this damage.
    /// </summary>
    public int ApplyLightSkillDamage(LightSkillSO skill, bool resolveCombatOutcome = true)
    {
        if (!combatActive || IsPlayerDead() || skill == null || lockedEnemy == null ||
            (lockedEnemy.Health != null && lockedEnemy.Health.IsDead))
        {
            return 0;
        }

        int applied = lockedEnemy.ReceiveLightDamage(Mathf.Max(0, skill.Damage));
        if (applied <= 0)
        {
            return 0;
        }

        AddClarity(skill.ClarityGain);
        if (resolveCombatOutcome)
        {
            EvaluateCombatOutcome();
        }
        return applied;
    }

    /// <summary>Applies a counter cinematic impact without feeding the enemy retaliation ledger.</summary>
    public int ApplyCounterSkillDamage(CounterSkillSO skill, bool resolveCombatOutcome = true)
    {
        if (!combatActive || IsPlayerDead() || skill == null || lockedEnemy == null ||
            (lockedEnemy.Health != null && lockedEnemy.Health.IsDead))
        {
            return 0;
        }

        int applied = lockedEnemy.ReceiveDamage(Mathf.Max(0, skill.Damage), canPrepareRetaliation: false);
        if (applied <= 0)
        {
            return 0;
        }

        AddClarity(skill.ClarityGain);
        if (resolveCombatOutcome)
        {
            EvaluateCombatOutcome();
        }
        return applied;
    }

    public void ResolveDeferredCombatOutcome()
    {
        EvaluateCombatOutcome();
    }

    public void CancelPlayerActionForCinematic()
    {
        playerActionPresentation?.CancelAction();
    }

    /// <summary>Prevents enemy reaction windows and impacts while a LightSkill owns the scene.</summary>
    public void SetCinematicSequenceActive(bool active)
    {
        IsCinematicSequenceActive = active;
        if (active)
        {
            CloseReactionWindow(notify: false);
            reactionSucceeded = false;
        }
    }

    /// <summary>Closes the current reaction window and freezes its attack for an immediate CounterSkill.</summary>
    public bool TryBeginCounterCinematic()
    {
        if (IsCinematicSequenceActive || !combatActive || !reactionWindowOpen || lockedEnemy == null ||
            lockedEnemy.ActiveSkill == null || !lockedEnemy.ActiveSkill.AcceptsEnemyReaction(RealTimeCombatReaction.Counter))
        {
            return false;
        }

        CloseReactionWindow(notify: true);
        reactionSucceeded = true;
        SetCinematicSequenceActive(true);
        return true;
    }

    public void CancelCounterCinematic()
    {
        if (IsCinematicSequenceActive)
        {
            SetCinematicSequenceActive(false);
        }
    }

    /// <summary>Finalizes the suspended attack without allowing its original Animation Events to resume.</summary>
    public void CompleteCounterAttack()
    {
        RealTimeCombatEnemy enemy = lockedEnemy;
        if (enemy != null)
        {
            enemy.CompleteRetaliationAndPrepareNext();
            enemy.ReturnToIdleAnimation();
        }

        CloseReactionWindow(notify: false);
        reactionSucceeded = false;
        SetCinematicSequenceActive(false);
    }

    public bool TryLockPlayerForCinematic()
    {
        return playerController != null && playerController.TryBeginUccExternalLock(
            disableGameplayInput: true,
            stopActiveAbilities: true);
    }

    public void UnlockPlayerAfterCinematic()
    {
        playerController?.EndUccExternalLock();
    }

    /// <summary>Restores Lucian's authored locomotion state after a cinematic Timeline releases UCC.</summary>
    public void ResumePlayerLocomotionAfterCinematic(bool movementHeld, bool sprintHeld)
    {
        playerActionPresentation?.ResumeLocomotionFromCinematic(movementHeld, sprintHeld);
    }

    public bool IsLockedEnemyWithinSkillHitRange(SkillSO skill)
    {
        if (!combatActive || skill == null ||
            !TryGetLockedEnemyHitDistance(out float distance))
        {
            return false;
        }

        return skill.IsWithinHitRange(distance);
    }

    private bool ValidateLockedEnemySkillRange(SkillSO skill, bool showFeedback)
    {
        if (skill == null || !TryGetLockedEnemyHitDistance(out float distance))
        {
            return false;
        }

        if (skill.IsWithinHitRange(distance))
        {
            return true;
        }

        if (showFeedback && lockedEnemy != null)
        {
            string message = distance < skill.MinimumHitDistance
                ? "Raté (trop près)"
                : "Raté (trop loin)";
            CombatDamageWorldFeedback.ShowMessage(
                lockedEnemy.transform,
                message,
                new Color(1f, 0.82f, 0.38f),
                2.25f);
        }

        return false;
    }

    private bool TryGetLockedEnemyHitDistance(out float distance)
    {
        distance = 0f;
        if (lockedEnemy == null || playerRoot == null ||
            (lockedEnemy.Health != null && lockedEnemy.Health.IsDead))
        {
            return false;
        }

        Vector3 playerPosition = playerRoot.position;
        Vector3 targetPosition = lockedEnemy.LockPoint.position;
        playerPosition.y = 0f;
        targetPosition.y = 0f;
        distance = Vector3.Distance(playerPosition, targetPosition);
        return true;
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

        if (!IsEnemySkillInHitRange(caster, skill))
        {
            return 0;
        }

        int damage = caster.ActiveSkill != null
            ? caster.CommittedRetaliationDamage
            : Mathf.Max(0, Mathf.RoundToInt(skill.Damages));
        damage = CounterSkillCombatController.ModifyGuardDamage(damage);
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
        BeginEnemyAttackWindow(enemy, 0f);
    }

    /// <summary>
    /// Opens an authored reaction window. The timeout only closes input eligibility;
    /// the attack impact remains exclusively driven by its Animation Event.
    /// </summary>
    public void BeginEnemyAttackWindow(RealTimeCombatEnemy enemy, float durationSeconds)
    {
        if (IsCinematicSequenceActive || !combatActive || enemy == null || enemy != lockedEnemy || enemy.ActiveSkill == null)
        {
            return;
        }

        CloseReactionWindow(notify: false);
        receivedReactions.Clear();
        reactionSucceeded = false;
        reactionWindowOpen = true;
        ReactionWindowChanged?.Invoke(new RealTimeCombatReactionWindow(enemy.transform, enemy.ActiveSkill, enemy.CommittedRetaliationDamage, true));
        EnemyAttackStarted?.Invoke(enemy.ActiveSkill, enemy.CommittedRetaliationDamage);

        if (durationSeconds > 0f)
        {
            int token = ++reactionWindowToken;
            reactionWindowRoutine = StartCoroutine(CloseReactionWindowAfterRealtime(enemy, enemy.ActiveSkill, token, durationSeconds));
        }
    }

    public void RegisterReaction(RealTimeCombatReaction reaction)
    {
        if (!reactionWindowOpen || lockedEnemy == null || lockedEnemy.ActiveSkill == null ||
            (reaction != RealTimeCombatReaction.Counter &&
             reaction != RealTimeCombatReaction.Dodge &&
             reaction != RealTimeCombatReaction.Jump))
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

    /// <summary>
    /// Cancels eligibility for the currently authored attack without resolving
    /// it. Damage and attack completion remain owned by their Animation Events.
    /// </summary>
    public void CancelEnemyAttackWindow(RealTimeCombatEnemy enemy)
    {
        if (enemy == null || enemy != lockedEnemy)
        {
            return;
        }

        CloseReactionWindow(notify: true);
    }

    public void ResolveEnemyAttackImpact(RealTimeCombatEnemy enemy)
    {
        if (IsCinematicSequenceActive || !combatActive || enemy == null || enemy != lockedEnemy || enemy.ActiveSkill == null)
        {
            return;
        }

        CloseReactionWindow(notify: true);
        bool succeeded = reactionSucceeded;
        if (!succeeded)
        {
            ApplyEnemySkillDamageToPlayer(enemy, enemy.ActiveSkill);
        }
        ReactionImpactResolved?.Invoke(enemy.ActiveSkill, succeeded);
    }

    private System.Collections.IEnumerator CloseReactionWindowAfterRealtime(
        RealTimeCombatEnemy enemy,
        SkillSO skill,
        int token,
        float durationSeconds)
    {
        yield return new WaitForSecondsRealtime(durationSeconds);
        if (token != reactionWindowToken || !reactionWindowOpen || enemy == null || enemy != lockedEnemy || enemy.ActiveSkill != skill)
        {
            yield break;
        }

        CloseReactionWindow(notify: true);
    }

    private void CloseReactionWindow(bool notify)
    {
        if (reactionWindowRoutine != null)
        {
            StopCoroutine(reactionWindowRoutine);
            reactionWindowRoutine = null;
        }

        reactionWindowToken++;
        bool wasOpen = reactionWindowOpen;
        RealTimeCombatEnemy enemy = lockedEnemy;
        SkillSO skill = enemy != null ? enemy.ActiveSkill : null;
        reactionWindowOpen = false;
        receivedReactions.Clear();
        if (notify && wasOpen && enemy != null && skill != null)
        {
            ReactionWindowChanged?.Invoke(new RealTimeCombatReactionWindow(
                enemy.transform,
                skill,
                enemy.CommittedRetaliationDamage,
                false));
        }
    }

    private bool IsEnemySkillInHitRange(RealTimeCombatEnemy enemy, SkillSO skill)
    {
        if (enemy == null || skill == null || playerRoot == null)
        {
            return false;
        }

        Vector3 enemyPosition = enemy.transform.position;
        Vector3 playerPosition = playerRoot.position;
        enemyPosition.y = 0f;
        playerPosition.y = 0f;
        return skill.IsWithinHitRange(Vector3.Distance(enemyPosition, playerPosition));
    }

    public void CompleteEnemyAttack(RealTimeCombatEnemy enemy)
    {
        if (enemy == null || enemy != lockedEnemy)
        {
            return;
        }

        enemy.CompleteRetaliationAndPrepareNext();
        CloseReactionWindow(notify: false);
        reactionSucceeded = false;
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
        CombatActorAnimationRoot playerAnimationContract = playerRoot.GetComponent<CombatActorAnimationRoot>();
        if (playerAnimationContract != null && playerAnimationContract.ValidateContract(out _))
        {
            playerAnimator = playerAnimationContract.Animator;
        }
        if (playerActionPresentation == null)
        {
            playerActionPresentation = playerRoot.GetComponentInChildren<PlayerActionPresentationController>(true);
            if (playerActionPresentation == null)
            {
                playerActionPresentation = playerRoot.gameObject.AddComponent<PlayerActionPresentationController>();
            }
        }

        playerActionPresentation.ResolveReferences(playerAnimator, playerLocomotionBridge);
        if (playerMobility == null) playerMobility = GetComponent<CombatMobilityController>();
        if (playerVision == null) playerVision = playerRoot.GetComponentInChildren<VisionField>(true);
        if (combatInput == null) combatInput = FindAnyObjectByType<RealTimeCombatInput>();
    }

    private void RefreshLockedEnemyStrafeBinding()
    {
        if (playerLocomotionBridge == null)
        {
            return;
        }

        // Timelines own actor orientation themselves. Outside cinematics, the
        // bridge alone owns the target-relative movement and facing state.
        Transform target = combatActive && !IsCinematicSequenceActive && HasValidLockedEnemy()
            ? (lockedEnemy.LockPoint != null ? lockedEnemy.LockPoint : lockedEnemy.transform)
            : null;
        if (target != null)
        {
            playerLocomotionBridge.SetCombatLockTarget(target);
        }
        else
        {
            playerLocomotionBridge.ClearCombatLockTarget();
        }
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

    private int ApplyPlayerDamage(int damage)
    {
        if (playerMobility != null && playerMobility.IsDamageInvulnerable)
        {
            return 0;
        }

        int sanitizedDamage = Mathf.Max(0, damage);
        int applied = playerController != null
            ? playerController.ApplyDamage(sanitizedDamage, "RealTimeCombat")
            : playerHealth != null ? playerHealth.ApplyDamage(sanitizedDamage) : sanitizedDamage;

        CombatDamageWorldFeedback.Show(playerRoot, applied, new Color(1f, 0.48f, 0.48f), 2.05f);
        if (applied > 0 && !IsPlayerDead())
        {
            PlayPlayerHurtAnimation();
        }

        return applied;
    }

    private void PlayPlayerHurtAnimation()
    {
        if (playerAnimator == null || string.IsNullOrWhiteSpace(playerHurtAnimatorState))
        {
            return;
        }

        int stateHash = Animator.StringToHash(playerHurtAnimatorState);
        if (!playerAnimator.HasState(0, stateHash))
        {
            Debug.LogWarning("[RealTimeCombat] State de hurt introuvable : " + playerHurtAnimatorState, playerAnimator);
            return;
        }

        if (playerActionPresentation == null ||
            !playerActionPresentation.TryPlayCombatState(
                playerHurtAnimatorState,
                PlayerActionPresentationProfile.CreateDefault(),
                "Hurt"))
        {
            playerAnimator.CrossFade(stateHash, playerHurtTransitionDuration, 0);
        }
    }

    private void EvaluateCombatOutcome()
    {
        bool enemyDead = lockedEnemy != null && lockedEnemy.Health != null && lockedEnemy.Health.IsDead;
        bool playerDead = IsPlayerDead();
        if (!enemyDead && !playerDead)
        {
            return;
        }

        if (playerDead)
        {
            PlayPlayerDeathAnimation();
            CombatResolved?.Invoke(false);
            EndCombat();
            if (playerDefeatRoutine == null)
            {
                playerDefeatRoutine = StartCoroutine(PlayPlayerDefeatSequence());
            }

            return;
        }

        CombatResolved?.Invoke(true);
        EndCombat();
    }

    private bool IsPlayerDead()
    {
        return playerController != null
            ? playerController.CurrentHp <= 0
            : playerHealth != null && playerHealth.IsDead;
    }

    private System.Collections.IEnumerator PlayPlayerDefeatSequence()
    {
        yield return null;
        float deathDuration = ResolvePlayerDeathAnimationDuration();
        yield return new WaitForSecondsRealtime(deathDuration + defeatPanelExtraDelaySeconds);

        if (RealTimeCombatSceneUiController.Instance != null)
        {
            RealTimeCombatSceneUiController.Instance.ShowDefeat();
        }
        else
        {
            Debug.LogWarning("[RealTimeCombat] DefeatPanel non affiche : RealTimeCombatSceneUiController introuvable.", this);
        }

        playerDefeatRoutine = null;
    }

    private void PlayPlayerDeathAnimation()
    {
        playerController?.Stop();
        if (playerAnimator == null || string.IsNullOrWhiteSpace(playerDeathAnimatorState))
        {
            return;
        }

        int stateHash = Animator.StringToHash(playerDeathAnimatorState);
        if (!playerAnimator.HasState(0, stateHash))
        {
            Debug.LogWarning("[RealTimeCombat] State de mort introuvable : " + playerDeathAnimatorState, playerAnimator);
            return;
        }

        if (playerActionPresentation != null &&
            playerActionPresentation.LockDeathAnimation(playerDeathAnimatorState, 0.05f))
        {
            return;
        }

        playerAnimator.CrossFade(stateHash, 0.05f, 0, 0f);
    }

    private float ResolvePlayerDeathAnimationDuration()
    {
        if (playerAnimator == null)
        {
            return playerDeathFallbackDuration;
        }

        AnimatorStateInfo state = playerAnimator.IsInTransition(0)
            ? playerAnimator.GetNextAnimatorStateInfo(0)
            : playerAnimator.GetCurrentAnimatorStateInfo(0);
        if (state.shortNameHash != Animator.StringToHash("Death"))
        {
            return playerDeathFallbackDuration;
        }

        return Mathf.Max(playerDeathFallbackDuration, state.length / Mathf.Max(0.01f, state.speed));
    }

    public void ReviveFromDefeat()
    {
        playerActionPresentation?.ClearDeathAnimationLock();
        string targetSceneName = SaveSessionManager.Instance != null
            ? SaveSessionManager.Instance.GetActiveSaveSceneName()
            : null;
        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            targetSceneName = SceneManager.GetActiveScene().name;
        }

        CharacterStateStore.Instance?.SuppressNextAutomaticSave("realtime_combat_defeat_checkpoint");
        LoadingScreenService.LoadScene(targetSceneName, "Retour au dernier checkpoint...", LoadSceneMode.Single);
    }

    public static void QuitFromDefeat()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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
            combatMusicOverrideToken = manager.PushCombatMusicOverride(manager.ResolveCombatAudioClip(CombatAudioCue.CombatMusic));
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
