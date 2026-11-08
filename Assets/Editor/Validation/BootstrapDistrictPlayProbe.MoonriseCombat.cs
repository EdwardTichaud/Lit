using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

public static partial class BootstrapDistrictPlayProbe
{
    private static bool moonriseAttackTapPressed;
    private static float moonriseNextAttackTap;
    private static LitBrainsEnemy moonriseEnemy;
    private static Flame moonriseFlame;
    private static Gamepad moonrisePad;
    private static Vector3 moonriseEnemyOrigin;
    private static int moonriseTorchBefore, moonriseHealthBefore, moonriseEnemyHealthBefore;
    private static int moonriseCounterGeneration = -1;
    private static readonly HashSet<int> moonriseImpacts = new();
    private static readonly HashSet<LitBrainsAttackPhase> moonrisePhases = new();
    private static bool moonriseCinematicSeen, moonriseKnockoutSeen, moonriseDeathSeen;
    private static float moonrisePeakTravel, moonriseNextDiagnostic, moonriseCounterPressAt = -1;
    private static float moonriseCounterFinishedAt = -1;
    private static float moonriseAttackLockAt = -1;
    private static bool moonriseAttackLockReported;
    private static bool moonrisePostCounterCaptured;
    private static bool moonriseMeleeImpact;
    private static float moonriseKnockoutInitialRemaining;
    private static int moonriseKnockoutHealth, moonriseKnockoutImpacts;
    private static bool moonriseKnockoutComplete, moonriseKnockoutDamage;
    private static string moonrisePlayerPoseKey;

    public static void StartMoonriseCombatBatch()
    {
        EditorSceneManager.OpenScene(Bootstrap, OpenSceneMode.Single);
        EditorApplication.delayCall += () => Begin(false, moonriseCombat: true);
    }

    private static void TickMoonriseCombat()
    {
        if (report.stage >= 300 && report.stage <= 304) { TickMoonriseLucPrologue(); return; }
        if (report.stage >= 291 && report.stage <= 295) { TickMoonriseWestRoute(); return; }
        if (report.stage >= 285 && report.stage <= 290) { TickMoonriseLadderReturn(); return; }
        if (report.stage >= 267 && report.stage <= 274) { TickMoonriseContinuity(); return; }
        var actor = LocalPlayerContext.LocalCharacterRoot != null
            ? LocalPlayerContext.LocalCharacterRoot.GetComponent<SquadCharacterController>() : null;
        if (actor == null) return;
        var manager = RealTimeCombatManager.Instance;
        if (manager == null) return;
        if (report.stage >= 262 && report.stage <= 266) TraceMoonrisePlayerPose(actor);
        if (actor.CurrentHp <= 0) throw new InvalidOperationException("Lucian died during the Moonrise recipe.");
        if (report.errors.Count != 0) throw new InvalidOperationException("Runtime errors during Moonrise combat: " + report.errors[0]);
        if (report.stage >= 275 && report.stage <= 279) { TickMoonriseWalkingApproach(actor); return; }
        if (Time.realtimeSinceStartup >= moonriseNextDiagnostic)
        {
            moonriseNextDiagnostic = Time.realtimeSinceStartup + 8;
            File.WriteAllText(Path.Combine(report.run, "moonrise-combat.txt"), MoonriseDiagnostic(actor, manager));
        }
        switch (report.stage)
        {
            case 260:
                if (!ZoneReady("Assets/Scenes/Maison/ZoneManifest_District_1.asset") || !ControlReady(out _)) return;
                RecordMoonriseRouteCheckpoint("moonrise_arrival");
                moonriseImpacts.Clear(); moonrisePhases.Clear(); moonrisePeakTravel = 0;
                moonriseCinematicSeen = moonriseKnockoutSeen = moonriseDeathSeen = false;
                moonriseCounterPressAt = -1; moonriseCounterGeneration = -1;
                moonriseCounterFinishedAt = -1;
                moonriseAttackLockAt = -1; moonriseAttackLockReported = false;
                moonrisePostCounterCaptured = false;
                moonriseMeleeImpact = false;
                moonriseKnockoutComplete = moonriseKnockoutDamage = false;
                moonrisePlayerPoseKey = null;
                var marker = Object.FindObjectsByType<SceneMarker>().Single(candidate => candidate.CharacterData != null &&
                    candidate.CharacterData.UniqueId == "c54e94b14c7826c4482529b6d4d739ec" && candidate.RuntimeInstance != null);
                moonriseEnemy = marker.RuntimeInstance.GetComponent<LitBrainsEnemy>();
                Check(moonriseEnemy != null && moonriseEnemy.ValidateRuntimeContract(out _), "Moonrise uses the actual marker-owned production Brains Juggernaut.");
                Check(moonriseEnemy.detectionTargetOverride == null && !moonriseEnemy.IsDead &&
                    !manager.IsCombatActive, "Authored enemy is alive; no test target, forced combat or laboratory arena.");
                Check(actor.FlameSecondsRemaining == 1000, "Fresh Lucian inventory retains the requested 1000 starting torch points.");
                moonriseEnemyOrigin = moonriseEnemy.Root.position;
                moonriseHealthBefore = actor.CurrentHp;
                moonriseEnemyHealthBefore = moonriseEnemy.CurrentHealth;
                moonriseEnemy.AttackImpact += RecordMoonriseImpact;
                WriteMoonriseLayout();
                var influenceField = typeof(Flame).GetField("litInfluence", Private);
                var contacts = moonriseEnemy.GetComponentsInChildren<Collider>().Where(collider => !collider.isTrigger).ToArray();
                moonriseFlame = Object.FindObjectsByType<Flame>().Where(flame => !flame.IsLit && !flame.IsExternallySuppressed)
                    .OrderBy(flame => Vector3.SqrMagnitude(flame.transform.position - moonriseEnemy.Root.position))
                    .FirstOrDefault(flame => influenceField.GetValue(flame) is LitInfluenceSource source &&
                        contacts.Any(contact => source.AcceptsCollider(contact) && source.TouchesCollider(flame.transform, contact, contact.transform.position)));
                Check(moonriseFlame != null, "An authored unlit Flame covers the encounter; no light is invented or relocated.");
                moonriseCorridorFlame = moonriseFlame;
                moonriseTorchBefore = actor.FlameSecondsRemaining;
                moonrisePad = InputSystem.AddDevice<Gamepad>();
                moonriseWalkingFlame = Object.FindObjectsByType<Flame>().Single(flame => flame.FlameId == "scene-flame:Maison:E6C2F108");
                moonriseRoomDoor = Object.FindObjectsByType<Door>().Single(door => door.name == "Moon_Room_Door_Maelle");
                Check(!moonriseWalkingFlame.IsEffectivelyLit && !moonriseRoomDoor.IsOpen, "The actual Moonrise arrival starts in Maelle's frozen room, before its Ancient Flame and door are used.");
                BeginMoonriseWalkingApproach(actor, moonriseWalkingFlame.GetInteractionAnchor().position, 275);
                return;
            case 261:
                if (!moonriseFlame.IsEffectivelyLit || moonriseEnemy.IsFlameDormant || !moonriseEnemy.NavigationReady) return;
                Check(moonriseTorchBefore - actor.FlameSecondsRemaining == moonriseFlame.TorchPointCostToLight,
                    "Authored Flame consumes exactly its torch point cost: " + moonriseFlame.TorchPointCostToLight);
                Advance(262); return;
            case 262:
                if (moonriseCoverStarted > 0) { TickMoonriseCoverProbe(actor); return; }
                DriveMoonriseUnarmedApproach(actor);
                moonrisePhases.Add(moonriseEnemy.AttackPhase);
                moonrisePeakTravel = Mathf.Max(moonrisePeakTravel,
                    Vector3.ProjectOnPlane(moonriseEnemy.Root.position - moonriseEnemyOrigin, Vector3.up).magnitude);
                if (!moonriseEnemy.IsEngagedWith(actor.transform) || !manager.IsCombatActive ||
                    moonriseImpacts.Count < 2 || !moonriseMeleeImpact || actor.CurrentHp >= moonriseHealthBefore || moonrisePeakTravel < .5f ||
                    !moonrisePhases.Contains(LitBrainsAttackPhase.Recovery)) return;
                Check(!manager.HasLockedCombatTarget, "Natural acquisition, pursuit and two unique real impacts require no manual camera lock.");
                Check(true, "Production movement/attacks: travel=" + moonrisePeakTravel + "; player HP=" + actor.CurrentHp + "; distinct impacts=" + moonriseImpacts.Count);
                Check(moonriseMeleeImpact, "The equipped production kit executes a real close-range melee impact, beyond its ranged jump.");
                var hud = RealTimeCombatSceneUiController.Instance;
                var text = (TMP_Text)typeof(RealTimeCombatSceneUiController).GetField("enemyHpText", Private).GetValue(hud);
                var fill = (Image)typeof(RealTimeCombatSceneUiController).GetField("enemyHpFill", Private).GetValue(hud);
                Check(text.text.StartsWith(moonriseEnemy.RuntimeCharacterData.ResolveDisplayName(), StringComparison.Ordinal) && fill.fillAmount > .99f,
                    "The production HUD shows the actual Brains enemy name and full live health before the player's first hit.");
                if (Environment.GetCommandLineArgs().Contains("-moonriseCoverProbe"))
                {
                    moonriseCoverStarted = Time.realtimeSinceStartup;
                    TickMoonriseCoverProbe(actor); return;
                }
                InputSystem.QueueStateEvent(moonrisePad, new GamepadState());
                Advance(263); return;
            case 263:
                var counter = CounterSkillCombatController.Instance;
                CheckCounterConfiguration(counter);
                if (counter.IsCinematicPlaying)
                {
                    moonriseCinematicSeen = true;
                    InputSystem.QueueStateEvent(moonrisePad, new GamepadState());
                    Check(true, "A real Y press in the authored enemy reaction window started the production CounterSkill.");
                    Advance(264); return;
                }
                if (moonriseCounterPressAt >= 0 && Time.realtimeSinceStartup - moonriseCounterPressAt > .07f)
                    InputSystem.QueueStateEvent(moonrisePad, new GamepadState());
                if (CombatHealthThresholdController.Instance?.IsExternalCounterWindowOpen == true &&
                    moonriseCounterGeneration != moonriseEnemy.ActionSequenceId)
                {
                    moonriseCounterGeneration = moonriseEnemy.ActionSequenceId;
                    moonriseCounterPressAt = Time.realtimeSinceStartup;
                    InputSystem.QueueStateEvent(moonrisePad, new GamepadState().WithButton(GamepadButton.North));
                }
                return;
            case 264:
                moonriseKnockoutSeen |= moonriseEnemy.IsCounterStunned;
                if (CounterSkillCombatController.Instance.IsCinematicPlaying) return;
                // CrossFade requests from graph teardown become observable only
                // after the Animator has had an ordinary evaluation again.
                if (moonriseCounterFinishedAt < 0) { moonriseCounterFinishedAt = Time.realtimeSinceStartup; return; }
                if (Time.realtimeSinceStartup - moonriseCounterFinishedAt < .1f) return;
                File.WriteAllText(Path.Combine(report.run, "counter-return-pose.txt"), MoonriseDiagnostic(actor, manager));
                Check(moonriseCinematicSeen && moonriseKnockoutSeen && moonriseEnemy.IsCounterStunned,
                    "Counter Timeline applied Knocked Out and preserved its vulnerability window on return to gameplay.");
                Check(moonriseEnemy.CurrentHealth < moonriseEnemyHealthBefore, "Counter Timeline applied damage to the captured production enemy.");
                Check(typeof(LitBrainsEnemy).GetField("counterStunVfx", Private).GetValue(moonriseEnemy) is GameObject vfx && vfx.activeInHierarchy,
                    "The actual Knocked Out prefab is instantiated and active on the production victim.");
                Check(moonriseEnemy.combatProfile.knockoutState == "Base Layer.Knocked Out" &&
                    (moonriseEnemy.Animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Knocked Out") ||
                     moonriseEnemy.Animator.IsInTransition(0) && moonriseEnemy.Animator.GetNextAnimatorStateInfo(0).IsName("Base Layer.Knocked Out")),
                    "The actual Knocked Out animation owns the victim, rather than the ordinary Hurt state.");
                Check(!moonriseEnemy.IsCinematicSuspended && !actor.GetComponent<LitOpsiveLocomotionBridge>().IsInputSuppressedByUcc,
                    "Counter released actor/target locks and suspension.");
                ScreenCapture.CaptureScreenshot(Path.Combine(report.run, "Moonrise_GameView_KnockedOut.png"));
                WriteMoonriseVisibilityDiagnostic(actor, "counter-return");
                moonriseKnockoutInitialRemaining = (float)typeof(LitBrainsEnemy).GetField("hurtRemaining", Private).GetValue(moonriseEnemy);
                Check(moonriseKnockoutInitialRemaining >= 1.7f && moonriseKnockoutInitialRemaining <= 2f,
                    "The counter returns a full two-second gameplay stun, allowing the Animator observation delay.");
                moonriseKnockoutHealth = moonriseEnemy.CurrentHealth;
                moonriseKnockoutImpacts = moonriseImpacts.Count;
                // Next stage intentionally remains a genuine gameplay test, not a forced death.
                Advance(265); return;
            case 265:
                if (Environment.GetCommandLineArgs().Contains("-moonriseLightingProbe")) { TickMoonriseLightingProbe(actor); return; }
                if (Environment.GetCommandLineArgs().Contains("-moonriseBackProbe")) { TickMoonriseBackProbe(actor); return; }
                ObserveMoonriseKnockout();
                TickMoonriseMaskComparison();
                if (report.status != "Playing") return;
                if (!moonrisePostCounterCaptured && Time.realtimeSinceStartup - moonriseCounterFinishedAt > 1)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(report.run, "Moonrise_GameView_AfterCounter.png"));
                    WriteMoonriseVisibilityDiagnostic(actor, "after-counter");
                    moonrisePostCounterCaptured = true;
                    BeginMoonriseMaskComparison();
                }
                DriveMoonriseAttackInputs(actor);
                if (!moonriseEnemy.IsDead) return;
                InputSystem.QueueStateEvent(moonrisePad, new GamepadState());
                Check(true, "Lucian's gamepad approach and fresh X attacks defeated the actual encounter; no ForceDefeat or direct damage fixture.");
                Advance(266); return;
            case 266:
                if (moonriseEnemy.gameObject.activeInHierarchy)
                {
                    var animator = moonriseEnemy.Animator;
                    var current = animator.GetCurrentAnimatorStateInfo(0);
                    var next = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : default;
                    moonriseDeathSeen |= current.IsName("Death") || current.IsTag("Death") || next.IsName("Death") || next.IsTag("Death");
                    return;
                }
                Check(moonriseDeathSeen, "Death state played before the marker-owned corpse dissolved and deactivated.");
                moonriseTorchStarted = Time.realtimeSinceStartup;
                Advance(274);
                return;
        }
    }

    private static void ObserveMoonriseKnockout()
    {
        if (moonriseKnockoutComplete) return;
        var effect = typeof(LitBrainsEnemy).GetField("counterStunVfx", Private).GetValue(moonriseEnemy) as GameObject;
        if (moonriseEnemy.IsDead)
            throw new InvalidOperationException("This KO recovery recipe requires a surviving victim; death does not prove normal stun recovery.");
        if (moonriseEnemy.IsCounterStunned)
        {
            var animator = moonriseEnemy.Animator;
            if (moonriseEnemy.AttackPhase != LitBrainsAttackPhase.None || moonriseImpacts.Count != moonriseKnockoutImpacts ||
                effect == null || !effect.activeInHierarchy ||
                !(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Knocked Out") ||
                  animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("Base Layer.Knocked Out")))
                throw new InvalidOperationException("Knocked Out lost its animation, VFX or no-attack contract before expiry.");
            moonriseKnockoutDamage |= moonriseEnemy.CurrentHealth < moonriseKnockoutHealth;
            File.AppendAllText(Path.Combine(report.run, "knockout-window.txt"),
                $"time={Time.time:F3} remaining={typeof(LitBrainsEnemy).GetField("hurtRemaining", Private).GetValue(moonriseEnemy)} HP={moonriseEnemy.CurrentHealth} phase={moonriseEnemy.AttackPhase}\n");
            return;
        }
        Check(effect == null || !effect.activeInHierarchy, "Knocked Out VFX is removed when the surviving enemy's stun expires.");
        Check(moonriseKnockoutDamage, "Real player attacks damage the enemy during Knocked Out without replacing its pose or restarting attacks.");
        moonriseKnockoutComplete = true;
    }

    private static void TraceMoonrisePlayerPose(SquadCharacterController actor)
    {
        var action = actor.GetComponent<PlayerActionPresentationController>();
        var animator = actor.GetComponent<CharacterAnimationController>()?.Animator;
        if (action == null || animator == null || !animator.isInitialized) return;
        var current = animator.GetCurrentAnimatorStateInfo(0);
        var next = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : default;
        string key = $"generation={action.ActionGeneration} active={action.IsActionActive} current={current.fullPathHash} next={next.fullPathHash}";
        if (key == moonrisePlayerPoseKey) return;
        moonrisePlayerPoseKey = key;
        File.AppendAllText(Path.Combine(report.run, "player-pose-transitions.txt"),
            $"time={Time.time:F3} {key} normalized={current.normalizedTime:F3} expected={typeof(PlayerActionPresentationController).GetField("activeStateHash", Private).GetValue(action)}\n");
    }

    private static void CheckCounterConfiguration(CounterSkillCombatController counter)
    {
        if (counter == null || counter.ConfiguredCounterSkill == null || counter.ConfiguredCounterSkill.Timeline == null)
            throw new InvalidOperationException("Production counter skill or Timeline is missing.");
    }

    private static void DriveMoonriseAttackInputs(SquadCharacterController actor)
    {
        if (moonriseAttackLockAt < 0)
        {
            moonriseAttackLockAt = Time.realtimeSinceStartup;
            moonriseAttackTapPressed = false;
            moonriseNextAttackTap = 0;
        }
        float locking = Time.realtimeSinceStartup - moonriseAttackLockAt;
        if (locking < .12f)
        {
            InputSystem.QueueStateEvent(moonrisePad, new GamepadState().WithButton(GamepadButton.RightStick)); return;
        }
        if (locking < .3f) { InputSystem.QueueStateEvent(moonrisePad, new GamepadState()); return; }
        if (!moonriseAttackLockReported)
        {
            Check(RealTimeCombatManager.Instance.HasLockedCombatTarget,
                "A fresh R3 uses the production target-lock binding for the attack phase, after vision-only pursuit was verified.");
            moonriseAttackLockReported = true;
        }
        Vector3 delta = Vector3.ProjectOnPlane(moonriseEnemy.Root.position - actor.transform.position, Vector3.up);
        var state = new GamepadState();
        var action = actor.GetComponent<PlayerActionPresentationController>();
        if (delta.magnitude > 2 && !action.IsActionActive && actor.IsGrounded)
        {
            var camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("No gameplay camera for camera-relative combat approach.");
            Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized;
            Vector3 direction = MoonriseApproachDirection(actor, delta);
            state.leftStick = new Vector2(Vector3.Dot(direction, right), Vector3.Dot(direction, forward)) * .85f;
            state.rightTrigger = delta.magnitude > 3 ? 1 : 0;
        }
        // Release between taps, including after damage: the real input contract
        // intentionally drops old held attacks when an action is interrupted.
        if (moonriseAttackTapPressed) moonriseAttackTapPressed = false;
        else if (delta.magnitude <= 2 && Time.realtimeSinceStartup >= moonriseNextAttackTap)
        {
            state = state.WithButton(GamepadButton.West);
            moonriseAttackTapPressed = true;
            moonriseNextAttackTap = Time.realtimeSinceStartup + .7f;
        }
        InputSystem.QueueStateEvent(moonrisePad, state);
    }

    private static Vector3 MoonriseApproachDirection(SquadCharacterController actor, Vector3 direct, Vector3? goal = null)
    {
        // This driver controls inputs, not the motor. Follow actual navigation
        // corners instead of pushing the player's capsule indefinitely into scenery.
        var agent = moonriseEnemy.GetComponent<NavMeshAgent>();
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        if (NavMesh.SamplePosition(actor.transform.position, out var from, .75f, filter) &&
            NavMesh.SamplePosition(goal ?? moonriseEnemy.Root.position, out var to, .75f, filter))
        {
            var path = new NavMeshPath();
            if (NavMesh.CalculatePath(from.position, to.position, filter, path) && path.status == NavMeshPathStatus.PathComplete)
                foreach (var corner in path.corners.Skip(1))
                {
                    Vector3 direction = Vector3.ProjectOnPlane(corner - actor.transform.position, Vector3.up);
                    if (direction.magnitude > .4f) return direction.normalized;
                }
        }
        return direct.normalized;
    }

    private static void RecordMoonriseImpact(AnimationEvent source)
    {
        var skill = moonriseEnemy.ActiveAttack?.skill;
        var victim = LocalPlayerContext.LocalCharacterRoot?.GetComponent<SquadCharacterController>();
        moonriseMeleeImpact |= skill != null && skill.MinimumHitDistance <= 1 && skill.MaximumHitDistance <= 3.6f &&
            moonriseEnemy.HasAuthoredImpactContact(victim);
        if (!moonriseImpacts.Add(moonriseEnemy.ActionSequenceId))
            throw new InvalidOperationException("Duplicate Moonrise attack impact generation " + moonriseEnemy.ActionSequenceId);
    }

    private static void WriteMoonriseLayout()
    {
        var lines = Object.FindObjectsByType<SceneMarker>().Where(marker => marker.gameObject.scene.name.StartsWith("District_1"))
            .Select(marker => "Marker " + marker.gameObject.scene.name + "/" + marker.name + " | " + marker.transform.position + " | " + marker.MarkerId);
        lines = lines.Concat(Object.FindObjectsByType<LadderController>().Select(ladder => "Ladder " + ladder.gameObject.scene.name + "/" + ladder.name +
            " | " + ladder.leafs1?.position + " -> " + ladder.leafs2?.position));
        lines = lines.Concat(Object.FindObjectsByType<Flame>().Select(flame => "Flame " + flame.gameObject.scene.name + "/" + flame.name +
            " | " + flame.transform.position + " | lit=" + flame.IsLit + " | cost=" + flame.TorchPointCostToLight));
        File.WriteAllLines(Path.Combine(report.run, "moonrise-layout.txt"), lines);
    }

    private static string MoonriseDiagnostic(SquadCharacterController actor, RealTimeCombatManager manager) =>
        "stage=" + report.stage + " time=" + Time.time + " scale=" + Time.timeScale + " actor=" + actor.transform.position +
        " HP=" + actor.CurrentHp + " torch=" + actor.FlameSecondsRemaining + " mode=" + InputModeCoordinator.CurrentMode +
        " combat=" + manager.IsCombatActive + " target=" + manager.CombatTarget?.Root?.name +
        " enemy=" + (moonriseEnemy != null ? moonriseEnemy.Root.position + " HP=" + moonriseEnemy.CurrentHealth +
            " phase=" + moonriseEnemy.AttackPhase + " detection=" + moonriseEnemy.DetectionReason + " dormant=" + moonriseEnemy.IsFlameDormant +
            " KO=" + moonriseEnemy.IsCounterStunned + " suspended=" + moonriseEnemy.IsCinematicSuspended + " nav=" + moonriseEnemy.NavigationReady : "none") +
        " impacts=" + moonriseImpacts.Count + " counterWindow=" + CombatHealthThresholdController.Instance?.IsExternalCounterWindowOpen +
        " counterPlaying=" + CounterSkillCombatController.Instance?.IsCinematicPlaying +
        " locked=" + manager.HasLockedCombatTarget + " playerAction=" + actor.GetComponent<PlayerActionPresentationController>()?.IsActionActive +
        " basic=" + actor.GetComponent<PlayerActionPresentationController>()?.IsBasicSkillAction + " forward=" + actor.transform.forward +
        " thresholdBlock=" + CombatHealthThresholdController.Instance?.BlocksEnemyActions(moonriseEnemy) +
        " thresholdSuspension=" + CombatHealthThresholdController.Instance?.ShouldSuspendEnemy(moonriseEnemy) +
        " rawMove=" + LocalInputRouter.RawMoveValue + " suppressed=" + actor.IsMovementInputSuppressed +
        " sitting=" + actor.IsInSittingCycle +
        " UCC=" + actor.GetComponent<LitUccInteractionBridge>()?.GetInteractionBlockReason() +
        " focus=" + InputFocusStack.HasAnyFocus() + " navigation=" + MoonriseNavigationDiagnostic() +
        " camera=" + MoonriseCameraDiagnostic() +
        " pose=" + (moonriseEnemy != null && moonriseEnemy.Animator != null ?
            "configuredKO=" + moonriseEnemy.combatProfile.knockoutState + " current=" + moonriseEnemy.Animator.GetCurrentAnimatorStateInfo(0).fullPathHash +
            " next=" + moonriseEnemy.Animator.GetNextAnimatorStateInfo(0).fullPathHash +
            " clips=" + string.Join(",", moonriseEnemy.Animator.GetCurrentAnimatorClipInfo(0).Select(info => info.clip.name + ":" + info.weight)) : "none");

    private static string MoonriseCameraDiagnostic()
    {
        var mode = Object.FindAnyObjectByType<LitGameplayCameraModeController>();
        var camera = Camera.main;
        return "requested=" + mode?.RequestedMode + " effective=" + mode?.EffectiveMode + " external=" + mode?.ExternalControl +
            " blend=" + mode?.IsBlending + " mask=" + mode?.MaskActive + " layers=" + mode?.MaskObstacleLayerCount +
            " position=" + (camera != null ? camera.transform.position.ToString() : "none");
    }

    private static string MoonriseNavigationDiagnostic()
    {
        var agent = moonriseEnemy != null ? moonriseEnemy.GetComponent<NavMeshAgent>() : null;
        string navigation = agent != null && agent.enabled && agent.isOnNavMesh
            ? "stopped=" + agent.isStopped + " path=" + agent.pathStatus + " pending=" + agent.pathPending +
              " destination=" + agent.destination + " remaining=" + agent.remainingDistance + " velocity=" + agent.velocity
            : "not attached";
        return navigation + " stops=" + (moonriseEnemy != null ? string.Join(",", moonriseEnemy.StopControlConditions
            .Where(pair => pair.Value).Select(pair => pair.Key)) : "none");
    }

    private static void CleanupMoonriseCombat()
    {
        CleanupMoonriseCoverProbe();
        CleanupMoonriseMaskComparison();
        CleanupMoonriseLightingProbe();
        if (moonriseEnemy != null) moonriseEnemy.AttackImpact -= RecordMoonriseImpact;
        if (moonrisePad != null && moonrisePad.added) InputSystem.RemoveDevice(moonrisePad);
        moonrisePad = null;
    }
}
