using System;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>Short isolated migration, Edit Mode and Play Mode smoke. Never starts a real save session.</summary>
public static class ProductionCombatValidation
{
    private static TestRunnerApi runner;
    private static Callbacks callbacks;
    private static double cleanupDeadline;
    private static int idleFrames;

    private static void WaitForEditCleanup()
    {
        var active = typeof(TestRunnerApi).GetMethod("IsRunActive", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || active != null && (bool)active.Invoke(null, null))
            idleFrames = 0;
        else idleFrames++;
        if (idleFrames < 3 && EditorApplication.timeSinceStartup < cleanupDeadline) return;
        EditorApplication.update -= WaitForEditCleanup;
        if (idleFrames < 3) { Debug.LogError("[ProductionCombatValidation] Edit Mode cleanup did not finish."); EditorApplication.Exit(1); return; }
        ChargedSkillPlayValidation.RunPreparationArmorBatch();
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch Editor.");
        try
        {
            var applied = ProductionCombatMigration.Run(true);
            if (!applied.succeeded) throw new InvalidOperationException("Authoring migration failed.");
            var repeated = ProductionCombatMigration.Run(false);
            if (repeated.changedAssets.Count != 0)
                throw new InvalidOperationException("Migration is not idempotent: " + string.Join(", ", repeated.changedAssets));
            Debug.Log("[ProductionCombatValidation] PASS authoring migration and second read-only pass with zero writes. Full production/network readiness remains separate.");
            RunTestsBatch();
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    public static void RunTestsBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch Editor.");
        try
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);
            runner = ScriptableObject.CreateInstance<TestRunnerApi>();
            callbacks = new Callbacks();
            runner.RegisterCallbacks(callbacks);
            runner.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                testNames = new[] { "CombatWorldMigrationTests", "FreeAttackTests", "MultiplayerCombatTimeTests",
                    "CombatSkillKitInputTests.MultiplayerEveryOpeningDirectionResolvesOnlyActorKit",
                    "CombatSkillKitInputTests.SoloKeepsDirectionalSupportSelection",
                    "CombatSkillKitInputTests.MultiplayerAcceptsLegacyCharacterIdWithoutRenamingIt",
                    "CombatSkillKitInputTests.MultiplayerRuntimeCloneCanResolveBySavedAssetGuidAlone",
                    "CombatSkillKitInputTests.MultiplayerUnknownOrMissingIdentityNeverBorrowsRequestedKit" }
            }));
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    private sealed class Callbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor tests) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.FailCount > 0 && !result.HasChildren)
                Debug.LogError("[ProductionCombatValidation] FAIL " + result.FullName + ": " + result.Message);
        }
        public void RunFinished(ITestResultAdaptor result)
        {
            TestRunnerApi.SaveResultToFile(result, "Library/ProductionCombatMigration/edit-results.xml");
            Debug.Log("[ProductionCombatValidation] Edit Mode pass=" + result.PassCount + " fail=" + result.FailCount + " skip=" + result.SkipCount);
            if (result.FailCount > 0 || result.PassCount == 0) { EditorApplication.Exit(1); return; }
            runner.UnregisterCallbacks(this);
            // RunFinished precedes the test runner's own scene restoration.
            cleanupDeadline = EditorApplication.timeSinceStartup + 30;
            idleFrames = 0;
            EditorApplication.update += WaitForEditCleanup;
        }
    }
}
