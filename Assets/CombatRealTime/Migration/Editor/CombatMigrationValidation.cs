using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>Bounded batch validation; source scenes and authoring assets are never saved by this runner.</summary>
public static class CombatMigrationValidation
{
    private static TestRunnerApi runner;
    private static ICallbacks callbacks;
    private static double deadline;
    private static int idleFrames;
    private static bool failed;

    public static void RunEditBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run validation in an isolated batch Editor.");
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-combatMigrationSpatialDiagnostics") >= 0)
                CombatMigrationAuthoringDiagnostics.WriteReport();
            runner = ScriptableObject.CreateInstance<TestRunnerApi>();
            callbacks = new Callbacks();
            runner.RegisterCallbacks(callbacks);
            deadline = EditorApplication.timeSinceStartup + 300;
            EditorApplication.update += Watchdog;
            var names = new List<string> {
                    "CombatWorldMigrationTests", "CombatEngagementLedgerTests", "CombatRosterIdentityTests",
                    "CombatSkillKitInputTests", "FreeAttackTests", "ContactContinuityTests",
                    "MultiplayerCombatTimeTests", "FairCounterTimingTests", "CombatThresholdParticipantTests",
                    "EnemyEncounterActorTests", "LitBrainsEnemyRuntimeTests", "LitBrainsProductionPolicyTests",
                    "ProductionBrainsEnemyConversionTests", "NinaCycleTests", "EtienneCycleTests",
                    "BelmontCycleTests", "ArdentCycleTests", "ProductionCombatMigrationReadOnlyTests",
                    "CombatReadinessEvidenceTests", "CombatNetworkCatalogTests", "CombatEnemyNetworkAuthoringTests",
                    "CombatNetworkExecutionCompletionTests", "CombatCinematicParticipantLifecycleTests", "CombatInventoryTests",
                    "CombatDiagnosticCleanupTests", "ProductionBrainsLifecycleTests", "ProductionImpactContinuityTests",
                    "CombatNetworkContactDashTests", "EnemyDeathPresentationTests",
                    "ProductionCombatFeelMigrationTests", "CombatNetworkBasicTailChargeTests",
                    "ChargedBasicTailTests", "ChargedSkillTests", "CombatContactAudioTests",
                    "CombatContactPoseTests", "TacticalActionFramingTests", "CombatEnemyRecoveryTests",
                    "CombatWorldLockCursorTests", "CombatAuthoredControlTests", "CombatSceneReturnTests",
                    "CombatPlayerHealthPersistenceTests"
            };
            bool coreOnly = Array.IndexOf(Environment.GetCommandLineArgs(), "-combatMigrationCoreOnly") >= 0;
            if (coreOnly)
            {
                names.RemoveAll(name => name == "NinaCycleTests" || name == "EtienneCycleTests" || name == "ArdentCycleTests");
                names.AddRange(new[] { "NinaCycleTests.ScientistUsesGhostInteractionBeforeCombat",
                    "NinaCycleTests.LeavingGhostModeMakesBodyVisibleAndDisablesGhostInteraction",
                    "NinaCycleTests.ScientistDeathVoiceUsesForgiveMeAndIsAssignedToPrefab",
                    "EtienneCycleTests.BrakeStunsFourSecondsWithSharedCooldownAndNoDamage",
                    "ArdentCycleTests.FalseChoirOnlyTakesDamageDuringRevealAndRejectsDuplicateOrWrongRelay" });
                Debug.Log("[CombatMigrationValidation] Core-only regression selection. Full narrative audit failures remain separate production blockers.");
            }
            runner.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, testNames = names.ToArray() }));
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    private static void Watchdog()
    {
        if (EditorApplication.timeSinceStartup < deadline) return;
        EditorApplication.update -= Watchdog;
        Debug.LogError("[CombatMigrationValidation] Validation timeout; no production readiness inferred.");
        EditorApplication.Exit(1);
    }

    private static void WaitForCleanup()
    {
        var active = typeof(TestRunnerApi).GetMethod("IsRunActive", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || active != null && (bool)active.Invoke(null, null)) idleFrames = 0;
        else idleFrames++;
        if (idleFrames < 3) return;
        EditorApplication.update -= WaitForCleanup;
        EditorApplication.update -= Watchdog;
        if (runner != null) UnityEngine.Object.DestroyImmediate(runner);
        runner = null;
        if (!failed && Array.IndexOf(Environment.GetCommandLineArgs(), "-combatMigrationAuthoringAudit") >= 0)
        {
            try
            {
                var migration = ProductionCombatMigration.Run(false);
                var network = CombatNetworkSetup.Run(false);
                Debug.Log("[CombatMigrationValidation] Read-only authoring audit: proposed=" + migration.changedAssets.Count +
                    " blockers=" + migration.blockers.Count + "; network proposed=" + network.changedAssets.Count +
                    " blockers=" + network.blockers.Count + " unsupported=" + network.unsupportedSkills.Count);
            }
            catch (Exception exception) { Debug.LogException(exception); failed = true; }
        }
        if (!failed && Array.IndexOf(Environment.GetCommandLineArgs(), "-combatMigrationStageAfterTests") >= 0)
        {
            CombatMigrationStaging.RunBatch();
            return;
        }
        if (!failed && Array.IndexOf(Environment.GetCommandLineArgs(), "-combatMigrationPlaySmoke") >= 0)
            ChargedSkillPlayValidation.RunPreparationArmorBatch();
        else EditorApplication.Exit(failed ? 1 : 0);
    }

    private sealed class Callbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor tests) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.FailCount > 0 && !result.HasChildren)
                Debug.LogError("[CombatMigrationValidation] FAIL " + result.FullName + ": " + result.Message);
        }
        public void RunFinished(ITestResultAdaptor result)
        {
            TestRunnerApi.SaveResultToFile(result, "Library/ProductionCombatMigration/continuation-edit-results.xml");
            Debug.Log("[CombatMigrationValidation] Edit Mode pass=" + result.PassCount + " fail=" + result.FailCount + " skip=" + result.SkipCount);
            failed = result.FailCount > 0 || result.PassCount == 0;
            runner.UnregisterCallbacks(this);
            idleFrames = 0;
            deadline = EditorApplication.timeSinceStartup + 30;
            EditorApplication.update += WaitForCleanup;
        }
    }
}
