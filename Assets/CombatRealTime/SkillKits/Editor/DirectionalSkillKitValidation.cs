using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class DirectionalSkillKitValidation
{
    private const string Key = "Lit.DirectionalSkillKit.Validation";
    private const string SceneProfileKey = "Lit.DirectionalSkillKit.SceneProfile";
    private static int stage;
    private static double started, at;
    private static Gamepad pad;
    private static Vector3 retreatStart;
    private static Vector3 wheelMoveStart;
    private static int beforeEnemyHealth;
    static DirectionalSkillKitValidation() => EditorApplication.update += Tick;
    public static void RunBatch() => Begin(false);
    public static void RunBatchSceneProfile() => Begin(true);
    private static void Begin(bool useSceneProfile)
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch Editor.");
        SessionState.SetBool(Key, true);
        SessionState.SetBool(SceneProfileKey, useSceneProfile);
        EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
        var arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
        if (!useSceneProfile)
        {
            // The recruitment test needs campaign defaults, not the authored
            // all-skills lab profile. Change only the unsaved test fixture.
            UnityEngine.Object.DestroyImmediate(arena.GetComponent<SoloTeamTestProfile>());
            arena.character = CombatLabCharacter.Lucian;
        }
        else Require(arena.character == CombatLabCharacter.Lucian && arena.GetComponent<SoloTeamTestProfile>() != null,
            "Authored scene must start with Lucian and a full solo test profile.");
        EditorApplication.isPlaying = true;
    }
    public static void RunBatchWithoutDomainReload()
    {
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        RunBatch();
    }
    public static void RunBatchWithDomainReload()
    {
        EditorSettings.enterPlayModeOptionsEnabled = false;
        RunBatch();
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Next(int value) { stage = value; at = EditorApplication.timeSinceStartup; }
    private static void Send(GamepadState state) => InputSystem.QueueStateEvent(pad, state);
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (started == 0) started = at = EditorApplication.timeSinceStartup;
            Require(EditorApplication.timeSinceStartup - started < 90, "Kit test timeout at stage " + stage);
            var arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            if (arena == null || arena.Player == null || arena.Enemy == null || stage == 0 && !arena.Enemy.NavigationReady) return;
            var manager = arena.GetComponent<RealTimeCombatManager>();
            var kits = arena.GetComponent<CombatSkillKitInput>();
            var player = arena.Player;
            double elapsed = EditorApplication.timeSinceStartup - at;
            switch (stage)
            {
                case 0:
                    var team = SoloTeamProgression.Resolve();
                    if (SessionState.GetBool(SceneProfileKey, false))
                    {
                        foreach (CombatSkillKit kit in Enum.GetValues(typeof(CombatSkillKit)))
                            for (int slot = 0; slot < 4; slot++)
                                Require(team.IsUnlocked(kit, slot) == (team.Catalog.GetSkill(kit, slot) != null),
                                    "Scene profile did not unlock " + kit + " slot " + slot);
                    }
                    else
                    {
                    Require(team != null && !team.IsRecruited(CombatSkillKit.Vesta), "Default solo must start without supports.");
                    Require(!kits.Open(CombatSkillKit.Vesta), "Unrecruited kit can be opened.");
                    Require(!manager.TryUseSkill(team.Catalog.GetSkill(CombatSkillKit.Vesta, 0)), "Direct cast bypassed recruitment.");
                    Require(team.RecruitMember(CombatSkillKit.Vesta) && !team.RecruitMember(CombatSkillKit.Vesta), "Recruitment must be idempotent.");
                    Require(team.IsUnlocked(CombatSkillKit.Vesta, 0) && !team.IsUnlocked(CombatSkillKit.Vesta, 1), "Recruitment grants more than X.");
                    Require(!manager.TryUseSkill(team.Catalog.GetSkill(CombatSkillKit.Vesta, 1)), "Direct cast bypassed skill lock.");
                    }
                    var profile = arena.GetComponent<SoloTeamTestProfile>() ?? arena.gameObject.AddComponent<SoloTeamTestProfile>();
                    profile.Apply();
                    typeof(SquadCharacterController).GetField("enableIdleSitting", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(player, false);
                    if (player.IsInSittingCycle) { player.TrySetSitting(false); return; }
                    Require(player.CharacterData.characterName == "Lucian", "Fixture must incarnate Lucian.");
                    Require(kits != null && kits.IsAvailable, "Kit catalog/input unavailable.");
                    var catalog = Resources.Load<CombatSkillKitCatalog>("CombatSkillKits/Default");
                    int count = 0;
                    foreach (CombatSkillKit kit in Enum.GetValues(typeof(CombatSkillKit)))
                        for (int i = 0; i < 4; i++) if (catalog.GetSkill(kit, i) != null) count++;
                    Require(count == 15 && catalog.GetSkill(CombatSkillKit.Lucian, 3) == null, "Lucian must have three skills and an empty B slot.");
                    Require(!CombatSkillKitCatalog.TryDirection(Vector2.one, out _), "Diagonal selects a kit.");
                    arena.Enemy.enabled = false;
                    player.SetHealth(50, 100);
                    player.GetComponent<LitOpsiveLocomotionBridge>().TeleportForSceneTransition(arena.Enemy.transform.position + arena.Enemy.transform.forward * 4, Quaternion.LookRotation(-arena.Enemy.transform.forward));
                    pad = InputSystem.AddDevice<Gamepad>();
                    Send(new GamepadState { leftStick = Vector2.right }.WithButton(GamepadButton.DpadUp));
                    Next(1); break;
                case 1:
                    if (elapsed < .2) return;
                    Require(kits.IsOpen && kits.ActiveKit == CombatSkillKit.Lucian && InputModeCoordinator.CurrentMode == InputMode.CombatWheel, "D-pad up did not open exclusive Lucian kit.");
                    Require(!LocalPlayerInput.FindSharedActionMap("RealTimeCombat").enabled &&
                        LocalPlayerInput.FindSharedActionMap("Player").FindAction("Move").enabled &&
                        !LocalPlayerInput.FindSharedActionMap("Player").FindAction("Interact").enabled,
                        "Wheel must keep only locomotion and its own skill commands enabled.");
                    wheelMoveStart = player.transform.position;
                    Send(new GamepadState { leftStick = Vector2.right }); Next(30); break;
                case 30:
                    if (elapsed < .5) return;
                    Require(kits.IsOpen && Vector3.Distance(wheelMoveStart, player.transform.position) > .2f,
                        "Player cannot move with wheel open/held stick at map handoff.");
                    Send(new GamepadState().WithButton(GamepadButton.DpadUp)); Next(31); break;
                case 31:
                    if (elapsed < .2) return;
                    Require(!kits.IsOpen && InputModeCoordinator.CurrentMode == InputMode.Combat,
                        "Second press on active kit did not close wheel.");
                    Send(new GamepadState()); Next(32); break;
                case 32:
                    if (elapsed < .2) return;
                    Send(new GamepadState().WithButton(GamepadButton.DpadUp)); Next(33); break;
                case 33:
                    if (elapsed < .2) return;
                    Require(kits.IsOpen && kits.ActiveKit == CombatSkillKit.Lucian,
                        "New press did not reopen wheel, or held input toggled twice.");
                    Send(new GamepadState().WithButton(GamepadButton.East)); Next(2); break;
                case 2:
                    if (elapsed < .2) return;
                    Require(kits.IsOpen && player.GetComponent<PlayerScriptedDodgeController>()?.IsActive != true, "Empty B slot closed the kit or dodged.");
                    Send(new GamepadState().WithButton(GamepadButton.DpadRight)); Next(3); break;
                case 3:
                    if (elapsed < .2) return;
                    Require(kits.ActiveKit == CombatSkillKit.Vesta, "D-pad right did not select Vesta.");
                    Send(new GamepadState().WithButton(GamepadButton.North)); Next(4); break;
                case 4:
                    if (elapsed < .2) return;
                    Require(!kits.IsOpen && player.CurrentHp == 100 && !CounterSkillCombatController.Instance.IsGuardHeld, "Y did not heal exclusively or failed to close.");
                    Send(new GamepadState().WithButton(GamepadButton.DpadLeft)); Next(5); break;
                case 5:
                    if (elapsed < .2) return;
                    Require(kits.ActiveKit == CombatSkillKit.Aurell, "D-pad left did not select Aurell.");
                    Send(new GamepadState().WithButton(GamepadButton.North)); Next(6); break;
                case 6:
                    if (elapsed < .2) return;
                    var effects = PlayerProtectionEffects.Get(player);
                    Require(!kits.IsOpen && effects.HasAura && !CounterSkillCombatController.Instance.IsGuardHeld, "Borrowed Rempart failed or Y guarded.");
                    effects.AddPrevented(20);
                    Require(effects.Reserve == 20, "Lucian cannot earn the borrowed Aurell reserve.");
                    player.SetHealth(60, 100);
                    Send(new GamepadState().WithButton(GamepadButton.DpadRight)); Next(7); break;
                case 7:
                    if (elapsed < .2) return;
                    Send(new GamepadState().WithButton(GamepadButton.South)); Next(8); break;
                case 8:
                    if (elapsed < .2) return;
                    var regeneration = player.GetComponent<PlayerRegenerationEffect>();
                    Require(!kits.IsOpen && regeneration != null && regeneration.Recipient == player && !PlayerProtectionEffects.Get(player).HasAura, "Solo regeneration did not target Lucian exclusively.");
                    Require(player.GetComponent<PlayerScriptedJumpController>()?.IsActive != true, "Regeneration also jumped.");
                    Send(new GamepadState()); Next(9); break;
                case 9:
                    if (elapsed < 3.2) return;
                    Require(player.CurrentHp > 60, "Solo self-regeneration produced no ticks.");
                    Send(new GamepadState().WithButton(GamepadButton.DpadDown)); Next(10); break;
                case 10:
                    if (elapsed < .2) return;
                    Require(kits.IsOpen && kits.ActiveKit == CombatSkillKit.Soren && manager.HasLockedCombatTarget, "D-pad down did not open Soren exclusively.");
                    retreatStart = player.transform.position;
                    Send(new GamepadState().WithButton(GamepadButton.East)); Next(11); break;
                case 11:
                    if (elapsed < .5) return;
                    Require(!kits.IsOpen && Vector3.Distance(retreatStart, player.transform.position) > .5f && player.GetComponent<PlayerScriptedDodgeController>()?.IsActive != true, "Borrowed retreat failed or B dodged.");
                    Send(new GamepadState()); Next(12); break;
                case 12:
                    if (elapsed < .3) return;
                    Require(!player.IsMovementInputSuppressed, "Kit/control exit locked movement.");
                    Require(kits.Open(CombatSkillKit.Lucian), "Kit reopening failed.");
                    InputModeCoordinator.Enter(typeof(DirectionalSkillKitValidation), InputMode.UserInterface);
                    Require(!kits.IsOpen && InputModeCoordinator.CurrentMode == InputMode.UserInterface, "Modal did not close the kit.");
                    InputModeCoordinator.Exit(typeof(DirectionalSkillKitValidation));
                    Require(InputModeCoordinator.CurrentMode == InputMode.Combat, "Modal restored a stale wheel owner.");
                    player.GetComponent<LitOpsiveLocomotionBridge>().TeleportForSceneTransition(arena.Enemy.transform.position + arena.Enemy.transform.forward * 2, Quaternion.LookRotation(-arena.Enemy.transform.forward));
                    beforeEnemyHealth = arena.Enemy.CurrentHealth;
                    Require(kits.Open(CombatSkillKit.Aurell), "Aurell reopening failed.");
                    Send(new GamepadState().WithButton(GamepadButton.East)); Next(20); break;
                case 20:
                    if (elapsed < .2) return;
                    Require(!kits.IsOpen && PlayerProtectionEffects.Get(player).Reserve == 0, "Borrowed Juste Retour did not consume reserve/start.");
                    Send(new GamepadState()); Next(21); break;
                case 21:
                    if (elapsed < 2) return;
                    Require(arena.Enemy.CurrentHealth < beforeEnemyHealth, "Borrowed Juste Retour authored event did not damage the enemy.");
                    Require(kits.Open(CombatSkillKit.Lucian), "Lucian reopening failed.");
                    Send(new GamepadState().WithButton(GamepadButton.West)); Next(22); break;
                case 22:
                    if (elapsed < .2) return;
                    Require(!kits.IsOpen && (manager.IsPlayerActionActive || manager.IsCinematicSequenceActive), "X did not launch Bond offensif.");
                    arena.GetComponent<CombatSkillCinematicController>()?.AbortForActionTermination();
                    player.GetComponent<PlayerActionPresentationController>().CancelAction();
                    Send(new GamepadState()); Next(23); break;
                case 23:
                    if (elapsed < .3) return;
                    Send(new GamepadState().WithButton(GamepadButton.RightStick)); Next(13); break;
                case 13:
                    if (elapsed < .8) return;
                    Require(!manager.HasLockedCombatTarget, "R3 hold did not unlock.");
                    Send(new GamepadState()); Next(14); break;
                case 14:
                    if (elapsed < .2) return;
                    Send(new GamepadState().WithButton(GamepadButton.RightStick)); Next(15); break;
                case 15:
                    if (elapsed < .1) return;
                    Send(new GamepadState()); Next(16); break;
                case 16:
                    if (elapsed < .2) return;
                    Require(manager.HasLockedCombatTarget, "R3 tap did not acquire lock.");
                    Require(kits.Open(CombatSkillKit.Lucian), "Kit reopening failed before cancel.");
                    Send(new GamepadState { leftTrigger = 1 }); Next(17); break;
                case 17:
                    if (elapsed < .2) return;
                    Require(!kits.IsOpen && InputModeCoordinator.CurrentMode == InputMode.Combat, "LT did not cancel without launching a skill.");
                    Send(new GamepadState());
                    Require(kits.Open(CombatSkillKit.Lucian), "Final kit reopening failed.");
                    manager.EndCombat();
                    Require(!kits.IsOpen && InputModeCoordinator.CurrentMode == InputMode.Exploration, "Combat exit kept the wheel alive.");
                    Debug.Log("[SkillKits] PLAY PASS | active-kit toggle, held-input/map-handoff debounce, movement with wheel open, 15 skills, four D-pad kits, empty slot, face exclusivity, solo regeneration ticks, borrowed reserve/JusteRetour impact/retreat, Bond offensif X, R3 tap/hold, LT cancel, modal and combat restoration.");
                    InputSystem.RemoveDevice(pad);
                    SessionState.SetBool(Key, false); EditorApplication.Exit(0); break;
            }
        }
        catch (Exception e) { Debug.LogException(e); SessionState.SetBool(Key, false); EditorApplication.Exit(1); }
    }
}
