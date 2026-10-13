using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Checks encounter-owned fusion with the real playable laboratory actor.</summary>
[InitializeOnLoad]
public static class CombatSpiritFusionValidation
{
    private const string Key = "Lit.CombatSpiritFusion.Validation";
    private static double started;
    private static int stage;
    private static SpiritBondController bond;
    private static SpiritBondController unrelated;
    static CombatSpiritFusionValidation() => EditorApplication.update += Tick;

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch Editor.");
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath);
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

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (started == 0) started = EditorApplication.timeSinceStartup;
            Require(EditorApplication.timeSinceStartup - started < 60, "Fusion validation timed out.");
            var arena = UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            if (arena == null || arena.Player == null || arena.Enemy == null || !arena.Enemy.NavigationReady) return;
            // Sitting owns a separate locomotion lock; isolate the fusion flow
            // from the actor's automatic idle-sitting timer in a slow batch run.
            typeof(SquadCharacterController).GetField("enableIdleSitting", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(arena.Player, false);
            if (arena.Player.IsInSittingCycle)
            {
                arena.Player.TrySetSitting(false);
                return;
            }
            var manager = arena.GetComponent<RealTimeCombatManager>();
            switch (stage)
            {
                case 0:
                    arena.Enemy.enabled = false;
                    manager.EndCombat();
                    unrelated = new GameObject("Uncontrolled spirit bond").AddComponent<SpiritBondController>();
                    manager.BeginExternalEncounter(arena.Player.transform, arena.Enemy);
                    bond = SpiritBondController.FindForCharacter(arena.Player.gameObject);
                    Require(bond != null && bond.IsCombatFused && bond.IsMelted, "Combat did not fuse the playable actor immediately.");
                    Require(!unrelated.IsFused, "An unrelated actor was fused.");
                    Require(!arena.Player.IsMovementInputSuppressed, "Automatic fusion locked locomotion.");
                    int notifications = 0;
                    bond.FusionStateChanged += (_, __) => notifications++;
                    bond.SetCombatFusion(true);
                    Require(notifications == 0, "Repeated entry retriggered fusion.");
                    Require(!bond.RequestMeltAnimation(), "Manual input can toggle combat fusion.");
                    bond.ConfirmRuptureDefusionFromAnimationEvent();
                    Require(bond.IsFused, "A stale Rupture event removed combat fusion.");
                    bond.BeginLightSkillFusion();
                    Require(bond.IsCinematicFusion && bond.IsFused, "LightSkill overlay failed.");
                    bond.EndLightSkillFusion();
                    Require(bond.IsCombatFused && bond.IsMelted, "LightSkill completion lost combat fusion.");
                    bond.BeginLightSkillFusion();
                    manager.EndCombat();
                    bond.EndLightSkillFusion();
                    bond.ConfirmMeltFusionFromAnimationEvent();
                    bond.TriggerHolyEffectFromAnimationEvent();
                    Require(!bond.IsFused && !bond.IsCinematicFusion, "Combat exit or a late event left fusion active.");
                    manager.BeginExternalEncounter(arena.Player.transform, arena.Enemy);
                    manager.enabled = false;
                    Require(!bond.IsFused, "Manager disable kept fusion alive.");
                    manager.enabled = true;
                    stage = 1;
                    break;
                case 1:
                    if (!bond.IsCombatFused) return;
                    Require(bond.IsCombatFused && bond.IsFused, "Manager enable did not restore an ongoing encounter.");
                    bond.enabled = false;
                    Require(!bond.IsFused, "Bond disable kept fusion alive.");
                    bond.enabled = true;
                    stage = 2;
                    break;
                case 2:
                    if (!bond.IsCombatFused) return;
                    Require(bond.IsCombatFused && bond.IsFused, "Bond enable did not restore combat fusion.");
                    manager.EndCombat();
                    Require(!bond.IsFused && !arena.Player.IsMovementInputSuppressed,
                        "Exit left fusion or locomotion locked: fused=" + bond.IsFused + " hp=" + arena.Player.CurrentHp +
                        " suppression=" + typeof(SquadCharacterController).GetField("scriptedMovementSuppressionCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(arena.Player));
                    manager.BeginExternalEncounter(arena.Player.transform, arena.Enemy);
                    arena.Player.ApplyDamage(arena.Player.CurrentHp + 10000, "Fusion validation defeat");
                    stage = 3;
                    break;
                case 3:
                    if (bond.IsFused) return;
                    Require(arena.Player.CurrentHp <= 0, "Defeat fixture did not kill the actor.");
                    Require(!bond.IsFused, "Defeat left fusion active.");
                    manager.EndCombat();
                    Debug.Log("[CombatSpiritFusion] PLAY PASS | entry/exit, isolated actor, idempotence, manual/stale events, LightSkill interruption, disable/enable, no movement lock.");
                    SessionState.SetBool(Key, false);
                    EditorApplication.Exit(0);
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(1);
        }
    }
}
