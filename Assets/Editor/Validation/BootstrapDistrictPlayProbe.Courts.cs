using System;
using System.Linq;
using Lit.Story;
using UnityEditor;
using UnityEngine;

public static partial class BootstrapDistrictPlayProbe
{
    public static void StartCourtsBatch()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Bootstrap, UnityEditor.SceneManagement.OpenSceneMode.Single);
        EditorApplication.delayCall += () => Begin(false, false, true, false, false, false, true);
    }
    private static void TickCourts()
    {
        if (report.secretContent && report.stage >= 180) { TickAuthoredSecrets(); return; }
        foreach (var runner in UnityEngine.Object.FindObjectsByType<StorySequenceRunner>())
            if (runner.IsPlaying) typeof(StorySequenceRunner).GetField("advanceRequested", Private).SetValue(runner, true);
        foreach (var panel in UnityEngine.Object.FindObjectsByType<RealTimeCombatSceneUiController>())
            if (panel.IsResultVisible) panel.HandleInputModeAction(InputModeAction.Submit, default);
        var service = CycleProgressionService.Instance;
        var actor = LocalPlayerContext.LocalCharacterRoot != null ? LocalPlayerContext.LocalCharacterRoot.GetComponent<SquadCharacterController>() : null;
        if (actor == null || service == null || !service.IsReady) return;
        if (actor.CurrentHp < 200) actor.SetHealth(500, 500); // Survival fixture, never a combat/balance assertion.
        var progression = UnityEngine.Object.FindAnyObjectByType<CourtsProgression>();
        string path = CourtsAuthoring.Folder + "/ZoneManifest_District_2_Courts.asset";
        if (report.stage == 160)
        {
            if (!ZoneReady(path) || !ControlReady(out _)) return;
            Check(progression != null && progression.commonAccessBarrier.activeSelf && !progression.nextZonePortal.activeSelf, "Common access and onward portal start gated.");
            Check(UnityEngine.Object.FindObjectsByType<InteractableItem>().Count(i => i.gameObject.scene.name == "District_2_Courts") == 9, "Nine authored documents present in loaded zone.");
            report.limbesRouteIndex = 0; Advance(161); return;
        }
        if (report.stage == 161 || report.stage == 162)
        {
            var notes = UnityEngine.Object.FindObjectsByType<InteractableItem>().Where(i => i.gameObject.scene.name == "District_2_Courts").OrderBy(i => i.representedItem.itemId, StringComparer.Ordinal).ToArray();
            if (report.limbesRouteIndex >= notes.Length) { Advance(165); return; }
            var note = notes[report.limbesRouteIndex]; var item = note.representedItem;
            if (report.stage == 161)
            {
                if (!ControlReady(out _)) return;
                Check(!KnowledgeManager.Instance.HasKnowledge(item.knowledgeUnlockedOnRead[0]), "Knowledge absent before actual reading " + item.itemId);
                Check(actor.TrySetUccExternalPositionAndRotation(note.transform.position + Vector3.back, Quaternion.identity, true), "UCC approach to document " + item.itemId);
                Advance(162); return;
            }
            if (!ControlReady(out _)) return;
            Check(note.CanBeDetectedBy(actor) && CharacterInteractionDetection.IsInActiveFlameInfluence(note), "Document detectable and lit " + item.itemId);
            var inventory = UnityEngine.Object.FindAnyObjectByType<InventoryPanelController>(FindObjectsInactive.Include);
            Check(inventory != null && inventory.TryOpenReadableItemFromWorld(item, actor.gameObject), "Production readable UI opened " + item.itemId);
            Check(KnowledgeManager.Instance.HasKnowledge(item.knowledgeUnlockedOnRead[0]), "Read unlocked knowledge " + item.itemId);
            typeof(InventoryPanelController).GetMethod("CloseReadablePanel", Private).Invoke(inventory, null);
            report.limbesRouteIndex++; Advance(161); return;
        }
        if (StorySequenceRunner.IsAnySequencePlaying) return;
        if (report.stage == 165)
        {
            if (!ControlReady(out _)) return;
            var family = progression.family;
            foreach (string source in new[] { "curiosity_memory", "ties_memory", "help_memory" })
            {
                var gate = UnityEngine.Object.FindObjectsByType<LimbesMemorySequence>().Single(g => g.cycle == family && g.completionSource == source);
                if (StorySequenceCompletionStore.IsCompleted(gate.runner.Sequence)) continue;
                Check(actor.TrySetUccExternalPositionAndRotation(gate.transform.position + Vector3.back, Quaternion.identity, true), "Approach family memory " + source);
                return;
            }
            report.limbesRouteIndex = 0; Advance(166); return;
        }
        if (report.stage == 166 || report.stage == 167 || report.stage == 168)
        {
            string[] ids = { "restore", "visits_return", "shared", "compare" };
            if (report.limbesRouteIndex >= ids.Length) { Advance(170); return; }
            var action = UnityEngine.Object.FindObjectsByType<CycleInteraction>().Single(i => i.gameObject.scene.name == "District_2_Courts" && i.dialogueId == ids[report.limbesRouteIndex]);
            if (service.IsCompleted(action.cycle.definition)) { Check(true, "Completed local cycle " + action.cycle.definition.cycleId); report.limbesRouteIndex++; Advance(166); return; }
            if (report.stage == 168) return;
            if (!ControlReady(out _)) return;
            if (report.stage == 166)
            {
                Check(actor.TrySetUccExternalPositionAndRotation(action.transform.position + Vector3.back, Quaternion.identity, true), "Approach ordinary world action " + action.name);
                Advance(167); return;
            }
            Check(action.TryInteract(), "Normal cycle interaction accepted " + action.dialogueId);
            Advance(168); return;
        }
        if (report.stage == 170)
        {
            if (!ControlReady(out _)) return;
            Check(service.IsCompleted(progression.family) && !progression.commonAccessBarrier.activeSelf, "Family resolved and common route physically open.");
            Check(actor.TrySetUccExternalPositionAndRotation(CourtsAuthoring.Origin + new Vector3(0, .1f, 73), Quaternion.identity, true), "Approach final introduction.");
            Advance(171); return;
        }
        if (report.stage == 171)
        {
            if (!service.IsStepCompleted(progression.finale, "introduced") || !ControlReady(out _)) return;
            var gate = UnityEngine.Object.FindAnyObjectByType<CourtsGargoyleGate>(); var enemy = gate.GetComponent<LitBrainsEnemy>();
            Check(gate.AllowsDecisions && enemy.Health.CurrentHp > 0, "Introduction unlocked the real Brains Gargoyle.");
            Check(enemy.ReceiveDamage(enemy.Health.CurrentHp) > 0, "Normal enemy damage path defeats Gargoyle (damage fixture).");
            Check(actor.TrySetUccExternalPositionAndRotation(CourtsAuthoring.Origin + new Vector3(0, .1f, 82), Quaternion.identity, true), "Approach conclusion.");
            Advance(172); return;
        }
        if (report.stage == 172)
        {
            if (!service.IsCompleted(progression.finale) || !progression.nextZonePortal.activeSelf || !ControlReady(out _)) return;
            Check(UnityEngine.Object.FindObjectsByType<CycleController>().Where(c => c.gameObject.scene.name == "District_2_Courts").All(c => service.IsCompleted(c.definition)), "All five local cycles completed.");
            foreach (var runner in UnityEngine.Object.FindObjectsByType<StorySequenceRunner>().Where(r => r.gameObject.scene.name == "District_2_Courts"))
                Check(StorySequenceCompletionStore.IsCompleted(runner.Sequence), "Presentation persisted " + runner.Sequence.sequenceId);
            if (report.secretContent) { Advance(180); return; }
            // Capture the actual production screen camera, without a second debug camera.
            CaptureLiveLimbes(); // Existing native render helper; file prefix retained, actual camera is Courts.
            Check(GameFlowService.ReturnToHub(), "Depart completed Courts through production flow."); Advance(173); return;
        }
        if (report.stage == 173)
        {
            if (!ZoneReady("Assets/Scenes/Maison/ZoneManifest_Maison.asset") || !ControlReady(out _)) return;
            Check(GameFlowService.TravelToZone(AssetDatabase.LoadAssetAtPath<ZoneManifest>(path), CourtsSpawnPoses()), "Re-enter Courts through production flow."); Advance(174); return;
        }
        if (report.stage == 174)
        {
            if (!ZoneReady(path) || !ControlReady(out _) || progression == null || !progression.nextZonePortal.activeSelf) return;
            Check(service.IsCompleted(progression.family) && service.IsCompleted(progression.finale) && !progression.commonAccessBarrier.activeSelf, "Cycle state and common access survive real zone reload.");
            Check(!StorySequenceRunner.IsAnySequencePlaying, "Completed presentations do not replay on zone return.");
            Check(GameFlowService.OpenMainMenu(), "Return to menu after Courts local qualification."); Advance(5);
        }
    }
    private static Pose[] CourtsSpawnPoses() => Enumerable.Range(0, 4).Select(i =>
        new Pose(CourtsAuthoring.Origin + new Vector3((i % 2) * 2 - 1, .1f, (i / 2) * 2), Quaternion.identity)).ToArray();
}
