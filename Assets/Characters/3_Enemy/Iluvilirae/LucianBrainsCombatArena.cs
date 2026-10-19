using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public enum CombatLabCharacter { Lucian, Aurell, Soren, Vesta }

/// <summary>Standalone combat laboratory. Uses Lucian's UCC and action presentation, never EnemyController.</summary>
[DefaultExecutionOrder(100)]
public sealed class LucianBrainsCombatArena : MonoBehaviour, IInputModeHandler
{
    [Header("Authored assets")]
    public NavMeshData navigation;
    public GameObject lucianPrefab;
    [Header("Justice prototype")]
    public GameObject aurellPrefab;
    public GameObject sorenPrefab;
    public GameObject vestaPrefab;
    public CombatLabCharacter character;
    [SerializeField, HideInInspector, FormerlySerializedAs("useAurell")] private bool migrateAurellMode;
    public bool useAurell { get => character == CombatLabCharacter.Aurell; set { if (value) character = CombatLabCharacter.Aurell; else if (useAurell) character = CombatLabCharacter.Lucian; } }
    private bool IsSpecialCharacter => character != CombatLabCharacter.Lucian;
    public bool spawnProtectionAlly;
    public SquadCharacterController ProtectionAlly { get; private set; }
    public GameObject enemyPrefab;
    public BasicSkillsSO[] basicSkills;
    public SkillSO[] additionalCombatSkills;
    public SkillSO[] enemyAttackSkills;
    public Camera arenaCamera;
    [Tooltip("Aim Lucian's combat torso without rotating the authored hips/feet or the physical motor.")]
    public bool alignPlayerCombatTorso = true;
    [Header("Encounter tuning (test only)")]
    public Vector3 playerSpawn = new Vector3(0, 0.1f, 5);
    public Vector3 enemySpawn = new Vector3(0, 0, -4);
    [Min(1)] public int playerMaxHealth = 100;
    public int enemyDamage => Mathf.RoundToInt(Enemy?.ActiveAttack?.skill != null ? Enemy.ActiveAttack.skill.Damages :
        enemyAttackSkills != null && enemyAttackSkills.Length > 0 && enemyAttackSkills[0] != null ? enemyAttackSkills[0].Damages : 0f);
    public string hurtState = "Base Layer.RealTimeCombat_RootMotion.TwinSword_Defense_Hit_Root";
    public bool showCombatDiagnostics;
    public SquadCharacterController Player { get; private set; }
    public LitBrainsEnemy Enemy { get; private set; }
    public bool EncounterOver => Player != null && Player.CurrentHp <= 0 || Enemy != null && Enemy.IsDead;
    public string LastContact { get; private set; } = "Approchez-vous de Juggernaut";
    private NavMeshDataInstance navInstance;
    private LitOpsiveLocomotionBridge bridge;
    private PlayerActionPresentationController presentation;
    private PlayerScriptedDodgeController dodge;
    private Animator enemyAnimator;
    private RealTimeCombatManager combat;
    private RealTimeCombatInput combatInput;
    private CombatMobilityController mobility;
    private SkillsManager skills;
    private bool paused, started;
    private int pauseChangedFrame = -1;
    public bool IsPaused => paused;
    private Transform previousLocalPlayer;
    private LitBrainsCombatProfile runtimeEnemyProfile;

    private void OnEnable()
    {
        if (migrateAurellMode && character == CombatLabCharacter.Lucian) character = CombatLabCharacter.Aurell;
        migrateAurellMode = false;
        if (navigation != null) navInstance = NavMesh.AddNavMeshData(navigation);
        LocalPlayerInput.EnsureInstance();
        LocalInputRouter.Start += OnPause;
        if (started) RestartEncounter();
    }

    private void OnPause(InputAction.CallbackContext context)
    {
        // Player.Start and System.Pause share the same physical button.
        if (pauseChangedFrame != Time.frameCount) SetPaused(!paused);
    }
    public void SetPaused(bool value)
    {
        if (paused == value) return;
        paused = value;
        pauseChangedFrame = Time.frameCount;
        if (paused)
        {
            bridge?.StopBridgeInput();
            TimeManager.EnsureInstance()?.AcquireGlobalPause(this);
            InputModeCoordinator.Enter(this, InputMode.UserInterface);
            // Keep the existing pause binding available alongside UI cancel/submit.
            LocalPlayerInput.FindSharedActionMap("Player")?.FindAction("Start", false)?.Enable();
        }
        else
        {
            TimeManager.Instance?.ReleaseOwner(this);
            InputModeCoordinator.Exit(this);
        }
    }
    public bool HandleInputModeAction(InputModeAction action, InputAction.CallbackContext context)
    {
        if (!paused) return false;
        if (action == InputModeAction.Pause && pauseChangedFrame == Time.frameCount) return true;
        if (action == InputModeAction.Pause || action == InputModeAction.Cancel) SetPaused(false);
        else if (action == InputModeAction.Submit) { SetPaused(false); RestartEncounter(); }
        return true;
    }

    private void Start()
    {
        previousLocalPlayer = LocalPlayerContext.LocalCharacterRoot;
        if (lucianPrefab == null || enemyPrefab == null || navigation == null || arenaCamera == null ||
            basicSkills == null || basicSkills.Length == 0 || enemyPrefab.GetComponent<LitBrainsEnemy>() == null)
        {
            Debug.LogError("[Brains Combat Lab] Lucian, Juggernaut, skills, camera or NavMesh reference missing.", this);
            enabled = false;
            return;
        }
        started = true;
        if (TimeManager.Instance == null) gameObject.AddComponent<TimeManager>();
        combat = GetComponent<RealTimeCombatManager>();
        if (combat == null) combat = gameObject.AddComponent<RealTimeCombatManager>();
        combatInput = GetComponent<RealTimeCombatInput>();
        if (combatInput == null) combatInput = gameObject.AddComponent<RealTimeCombatInput>();
        mobility = GetComponent<CombatMobilityController>();
        if (mobility == null) mobility = gameObject.AddComponent<CombatMobilityController>();
        if (GetComponent<CounterSkillCombatController>() == null) gameObject.AddComponent<CounterSkillCombatController>();
        if (GetComponent<CombatCinematicPlaybackService>() == null) gameObject.AddComponent<CombatCinematicPlaybackService>();
        if (GetComponent<LightSkillCombatController>() == null) gameObject.AddComponent<LightSkillCombatController>();
        if (GetComponent<CombatLockOnCameraController>() == null) gameObject.AddComponent<CombatLockOnCameraController>();
        if (GetComponent<CombatImpactFeedbackController>() == null) gameObject.AddComponent<CombatImpactFeedbackController>();
        skills = GetComponent<SkillsManager>();
        if (skills == null) skills = gameObject.AddComponent<SkillsManager>();
        RestartEncounter();
    }

    public void RestartEncounter()
    {
        SetPaused(false);
        ReleaseActors();
        var selected = character switch
        {
            CombatLabCharacter.Aurell => aurellPrefab,
            CombatLabCharacter.Soren => sorenPrefab,
            CombatLabCharacter.Vesta => vestaPrefab,
            _ => lucianPrefab
        };
        if (selected == null) { Debug.LogError("[Combat Lab] Selected character prefab is missing: " + character, this); return; }
        var playerObject = Instantiate(selected, playerSpawn, Quaternion.Euler(0, 180, 0));
        if (alignPlayerCombatTorso && playerObject.GetComponent<LucianCombatTorsoFacing>() == null)
            playerObject.AddComponent<LucianCombatTorsoFacing>();
        Player = playerObject.GetComponent<SquadCharacterController>();
        bridge = playerObject.GetComponent<LitOpsiveLocomotionBridge>();
        presentation = playerObject.GetComponent<PlayerActionPresentationController>();
        if (presentation == null) presentation = playerObject.AddComponent<PlayerActionPresentationController>();
        presentation.ResolveReferences(playerObject.GetComponent<Animator>(), bridge);
        dodge = playerObject.GetComponent<PlayerScriptedDodgeController>();
        var info = playerObject.GetComponent<CharacterInfo>();
        if (Player == null || bridge == null || presentation == null || dodge == null || info == null || info.SourceData == null)
        {
            Debug.LogError("[Brains Combat Lab] Lucian prefab is missing its existing UCC/action stack.", playerObject);
            playerObject.SetActive(false); Destroy(playerObject); enabled = false; return;
        }
        Player.BindCharacterData(info.SourceData, initializeInventory: false);
        Player.SetHealth(playerMaxHealth, playerMaxHealth);
        Player.ApplyFlameState(Player.FlameSecondsRemaining, false);
        // This laboratory owns input; no session or network player is launched.
        var opsiveInput = playerObject.GetComponent<LitOpsivePlayerInput>();
        if (opsiveInput != null)
            opsiveInput.SetMovementOverride(Vector2.zero, true);
        foreach (var listener in playerObject.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
        // Existing AnimationEvents resolves impacts through the shared services.
        LocalPlayerContext.SetLocalCharacter(playerObject.transform, source: "BrainsCombatLab");
        if ((useAurell || character == CombatLabCharacter.Vesta) && spawnProtectionAlly)
        {
            var allyObject = Instantiate(lucianPrefab, playerSpawn + Vector3.right * 3, Quaternion.Euler(0, 180, 0));
            allyObject.name = character == CombatLabCharacter.Vesta ? "HealingAlly_Laboratory" : "ProtectionAlly_Laboratory";
            ProtectionAlly = allyObject.GetComponent<SquadCharacterController>();
            ProtectionAlly.BindCharacterData(allyObject.GetComponent<CharacterInfo>().SourceData, initializeInventory: false);
            ProtectionAlly.SetHealth(100, 100);
            allyObject.GetComponent<LitOpsiveLocomotionBridge>()?.StopBridgeInput();
            var allyBridge = allyObject.GetComponent<LitOpsiveLocomotionBridge>();
            if (allyBridge != null) allyBridge.enabled = false;
            allyObject.GetComponent<LitOpsivePlayerInput>()?.SetMovementOverride(Vector2.zero, true);
            foreach (var listener in allyObject.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
        }
        Enemy = Instantiate(enemyPrefab, enemySpawn, Quaternion.identity).GetComponent<LitBrainsEnemy>();
        if (Enemy.combatProfile != null)
        {
            var clashProfile = GetComponent<CombatLabClashController>()?.labEnemyProfile;
            runtimeEnemyProfile = Instantiate(clashProfile != null ? clashProfile : Enemy.combatProfile);
            Enemy.combatProfile = runtimeEnemyProfile;
        }
        Enemy.detectionTargetOverride = playerObject.transform;
        enemyAnimator = Enemy.GetComponent<Animator>();
        LastContact = "Approchez-vous de Juggernaut";
        combat.BeginExternalEncounter(Player.transform, Enemy, startEngaged: !combat.AllowsFreeAttacks);
        GetComponent<CombatWorldRuntime>()?.RefreshEnemies();
        GetComponent<CombatLabClashController>()?.Bind(this, combat);
        skills.ResetAllBasicSkillCombos();
        skills.SetAdditionalCombatSkills(IsSpecialCharacter ? null : additionalCombatSkills);
        skills.RefreshSkills(true);
        skills.SetGroundBasicSkillOverride(basicSkills);
        for (int i = 0; i < SkillsManager.MaxEquippedSkills; i++) skills.UnequipSkillAt(i);
        var known = new System.Collections.Generic.List<SkillSO>(IsSpecialCharacter ? Player.CharacterData.combatSkills : skills.KnownSkills);
        foreach (var skill in known) skills.EquipSkill(skill);
        arenaCamera.GetComponent<LitGameplayCameraModeController>()?.SetMode(GameplayCameraMode.Tactical);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) { RestartEncounter(); return; }
        if (paused) return;
        if (Player == null || Enemy == null) return;
        if (EncounterOver)
        {
            EndGuard(); bridge.StopBridgeInput();
            if (Player.CurrentHp <= 0)
            {
                presentation.LockDeathAnimation("Base Layer.Death");
                if (!Enemy.IsDead && Enemy.enabled)
                {
                    Enemy.enabled = false;
                    var agent = Enemy.GetComponent<NavMeshAgent>();
                    if (agent.enabled && agent.isOnNavMesh) { agent.ResetPath(); agent.isStopped = true; agent.velocity = Vector3.zero; }
                    enemyAnimator.SetFloat("Speed", 0);
                }
            }
            else { bridge.ClearCombatLockTarget(); presentation.ClearActionFacingTarget(); }
            return;
        }
        if (Player.transform.position.y < -5) Player.SetCurrentHp(0);
    }

    public bool TryAttack()
    {
        if (EncounterOver || combat == null || !combat.CanChainBasicSkill) return false;
        var context = bridge.Grounded ? BasicSkillContext.Grounded : BasicSkillContext.Airborne;
        if (!skills.TryReserveNextBasicSkill(context, out var skill)) return false;
        skills.SetAnimationEventSkill(skill);
        return combat.TryUseSkill(skill);
    }

    public void ResolvePlayerContact(AnimationEvent source)
    {
        if (presentation != null && presentation.AcceptAnimationEvent(source)) presentation.HandleResolveSkillImpact();
    }

    private bool guardInputHeld;
    public void SetGuardHeld(bool held)
    {
        if (!held) { EndGuard(); return; }
        if (guardInputHeld || EncounterOver) return;
        guardInputHeld = true;
        if (GetComponent<CombatHealthThresholdController>()?.TryHandleEnemyReaction(EnemyAttackReaction.Counter) == true) return;
        CounterSkillCombatController.Instance?.BeginGuard();
    }
    private void EndGuard()
    {
        guardInputHeld = false;
        GetComponent<CombatHealthThresholdController>()?.ReleaseEnemyReactionButton(EnemyAttackReaction.Counter);
        CounterSkillCombatController.Instance?.EndGuard();
    }
    public bool TryDodge() => !EncounterOver && mobility != null && mobility.TryDodgeImmediate();

    public void ResolveEnemyContact() => GetComponent<CombatWorldRuntime>()?.ResolveEnemyContact(Enemy);

    public static bool HasContact(Transform attacker, Transform target, float reach, float arc, float heightTolerance = float.PositiveInfinity)
    {
        return CombatWorldRuntime.HasContact(attacker, target, reach, arc, heightTolerance);
    }

    private void DrawCombatDiagnostics()
    {
        if (!showCombatDiagnostics || Enemy == null) return;
        GUI.Label(new Rect(15, 90, 1000, 60), $"Phase: {Enemy.AttackPhase} | vitesse: {Enemy.ActualSpeed:F2} | délai: {Enemy.AttackCooldownRemaining:F2}s | résistance: {Enemy.ResistanceDamage:F0}/100 | temps: {Time.timeScale:F2}\n{LastContact}");
    }

    private void ReleaseActors()
    {
        skills?.SetAdditionalCombatSkills(null);
        GetComponent<CombatLabClashController>()?.Cancel();
        if (combat != null) combat.ExternalImpactArbitrator = null;
        skills?.SetGroundBasicSkillOverride(null);
        EndGuard();
        var counter = GetComponent<CounterSkillCombatController>();
        if (counter != null && counter.IsCinematicPlaying) counter.AbortForActionTermination();
        var light = GetComponent<LightSkillCombatController>();
        if (light != null && light.IsCinematicPlaying) light.AbortForActionTermination();
        GetComponent<CombatSkillCinematicController>()?.AbortForActionTermination();
        combat?.EndCombat();
        if (Player != null)
        {
            LocalPlayerContext.ClearIfMatch(Player.transform, source: "BrainsCombatLab");
            dodge?.CancelDodge(); presentation?.CancelAction(); bridge?.StopBridgeInput();
            // The cached UCC index can already be -1 while a smoothing entry remains.
            // Retire this laboratory actor by identity before destroying its Transform.
            var locomotion = Player.GetComponent<Opsive.UltimateCharacterController.Character.UltimateCharacterLocomotion>();
            if (locomotion != null)
            {
                foreach (var simulation in FindObjectsByType<Opsive.UltimateCharacterController.SimulationManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var characters = simulation.Characters;
                    for (int i = characters.Count - 1; i >= 0; i--)
                        if (characters[i].Locomotion == locomotion) characters.RemoveAt(i);
                    for (int i = 0; i < characters.Count; i++)
                        if (characters[i].Locomotion != null) characters[i].Locomotion.SimulationIndex = i;
                }
                locomotion.SimulationIndex = -1;
                locomotion.enabled = false;
            }
            Player.gameObject.SetActive(false); Destroy(Player.gameObject);
        }
        if (Enemy != null) { Enemy.gameObject.SetActive(false); Destroy(Enemy.gameObject); }
        if (runtimeEnemyProfile != null) Destroy(runtimeEnemyProfile);
        runtimeEnemyProfile = null;
        if (ProtectionAlly != null) { ProtectionAlly.gameObject.SetActive(false); Destroy(ProtectionAlly.gameObject); ProtectionAlly = null; }
        Player = null; Enemy = null;
    }

    private void OnDisable()
    {
        LocalInputRouter.Start -= OnPause;
        SetPaused(false);
        ReleaseActors();
        if (previousLocalPlayer != null) LocalPlayerContext.SetLocalCharacter(previousLocalPlayer, source: "BrainsCombatLab restore");
        if (navInstance.valid) navInstance.Remove();
    }

    private void OnGUI()
    {
        DrawCombatDiagnostics();
        GUI.Box(new Rect(12, 12, 730, 140), character + " contre Juggernaut v2 — laboratoire Brains AI");
        GUI.Label(new Rect(25, 37, 700, 22), "Stick gauche : déplacement | X : combo | B : esquive | A : saut | D-pad bas : verrouillage");
        GUI.Label(new Rect(25, 59, 700, 22), "Y : garde | RB : palette, stick droit + A : compétence | L3 : LightSkill | Start : pause | R : reprendre");
        if (paused) GUI.Label(new Rect(25, 155, 700, 24), "Pause — Start/B : reprendre | A : recommencer le combat");
        if (Player == null || Enemy == null) return;
        GUI.Label(new Rect(25, 81, 700, 22), character + " : " + Player.CurrentHp + "/" + Player.MaxHp + " PV | Juggernaut : " + Enemy.CurrentHealth +
            " PV | " + (CounterSkillCombatController.Instance != null && CounterSkillCombatController.Instance.IsGuardHeld ? "Garde" : combat != null && combat.HasLockedCombatTarget ? "Cible verrouillée" : "Déplacement libre"));
        GUI.Label(new Rect(25, 103, 700, 22), EncounterOver ? (Player.CurrentHp <= 0 ? "Défaite — R pour reprendre" : "Victoire — R pour reprendre") : LastContact);
        GUI.Label(new Rect(25, 125, 700, 22), "Clarté : " + (combat != null ? combat.Clarity.ToString("0") : "0") + " | Détection : " + Enemy.DetectionReason + " | NavMesh : " + Enemy.NavigationReady);
    }
}
