using System;
using System.Linq;
using Lit.Story;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class BootstrapDistrictPlayProbe
{
    private static AbilitySaveData contentPreviousAbilities;
    public static void FinalizeAndStartAuthoredSecretsBatch()
    {
        SecretPassagesAuthoring.RefineEndpointSpacing();
        StartAuthoredSecretsBatch();
    }
    public static void StartAuthoredSecretsBatch()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Bootstrap, UnityEditor.SceneManagement.OpenSceneMode.Single);
        EditorApplication.delayCall += () => Begin(false, false, true, false, false, false, true, true);
    }
    private static void EquipSecretAbility(bool traversal)
    {
        var abilities = AbilityService.Ensure();
        var ability = AssetDatabase.LoadAssetAtPath<AbilitySO>("Assets/Resources/Abilities/Ability_" + (traversal ? "SaufConduit" : "Observateur") + ".asset");
        var state = abilities.Capture(); state.Unlock(ability.abilityId); state.Equip(AbilityService.SoloPlayerId, ability.abilityId); abilities.Restore(state);
    }
    private static KnowledgePassageDiscovery Physical(int number) => Object.FindObjectsByType<KnowledgePassageDiscovery>()
        .Single(d => d.definition == SecretPassageNotes.Catalog.entries[number - 1].definition);
    private static void TickAuthoredSecrets()
    {
        foreach (var runner in Object.FindObjectsByType<StorySequenceRunner>())
            if (runner.IsPlaying) typeof(StorySequenceRunner).GetField("advanceRequested", Private).SetValue(runner, true);
        var actor = LocalPlayerContext.LocalCharacterRoot != null ? LocalPlayerContext.LocalCharacterRoot.GetComponent<SquadCharacterController>() : null;
        var service = CycleProgressionService.Instance; var catalog = SecretPassageNotes.Catalog;
        if (actor == null || service == null || !service.IsReady || catalog == null) return;
        if (actor.CurrentHp < 200) actor.SetHealth(500, 500);
        if (report.stage == 180)
        {
            if (!ControlReady(out _)) return;
            Check(catalog.CountRead(service) == 0, "Family documents did not advance the twenty-note cycle.");
            contentPreviousAbilities = AbilityService.Ensure().Capture(); EquipSecretAbility(false);
            var discovery = Physical(3); var narrow = discovery.GetComponent<TrouEtroit>();
            var rules = service.GetComponent<WorldRulesStateManager>();
            rules.SetInt(SecretPassageAttemptLedger.AttemptKey(discovery.definition.passageId, KnowledgePassageDiscovery.ResolveCharacterId(actor.gameObject)), 1);
            Check(actor.TrySetUccExternalPositionAndRotation(narrow.teleportPointA.position, Quaternion.identity, true), "Approach an authored passage with a saved failed attempt."); Advance(181); return;
        }
        if (report.stage == 181)
        {
            if (!ControlReady(out _)) return;
            Check(!Physical(3).GetComponent<TrouEtroit>().ServerTryPassage(actor.gameObject, false), "Stored Observer failure is respected on authored passage.");
            Check(catalog.CountRead(service) == 0, "Observer failure counts no read."); report.limbesRouteIndex = 0; Advance(182); return;
        }
        if (report.stage == 182 || report.stage == 183)
        {
            if (ReadSecretNotes(actor, "District_2_Courts_Secrets", 182, 183)) Advance(184);
            return;
        }
        if (report.stage == 184)
        {
            if (!ControlReady(out _)) return;
            Check(catalog.CountRead(service) == 6, "Six actual Courts note openings counted, with no duplicates.");
            var discovery = Physical(3); var narrow = discovery.GetComponent<TrouEtroit>();
            actor.TrySetUccExternalPositionAndRotation(narrow.teleportPointA.position, Quaternion.identity, true); Advance(185); return;
        }
        if (report.stage == 185)
        {
            var discovery = Physical(3); var narrow = discovery.GetComponent<TrouEtroit>();
            if (!ControlReady(out _) || !StorySequenceCompletionStore.IsCompleted(discovery.firstRecognition.Sequence)) return;
            Check(narrow.IsDetected && discovery.IsKnown, "Real reading bypassed the old failure and triggered recognition.");
            Check(!narrow.ServerTryPassage(actor.gameObject, true), "Observer cannot traverse a known authored passage.");
            var other = Physical(7); var rules = service.GetComponent<WorldRulesStateManager>();
            rules.SetInt(SecretPassageAttemptLedger.AttemptKey(other.definition.passageId, KnowledgePassageDiscovery.ResolveCharacterId(actor.gameObject)), 2);
            actor.TrySetUccExternalPositionAndRotation(other.GetComponent<TrouEtroit>().teleportPointA.position, Quaternion.identity, true); Advance(186); return;
        }
        if (report.stage == 186)
        {
            if (!ControlReady(out _)) return;
            var discovery = Physical(7);
            Check(discovery.GetComponent<TrouEtroit>().ServerTryPassage(actor.gameObject, false), "Cached successful Observer result reveals the passage whose note is in District 1.");
            Check(catalog.CountRead(service) == 6 && !service.HasFact(catalog.notesCycle, CycleStepKind.Read, catalog.entries[6].noteId), "Observer success grants knowledge but never a read fact.");
            EquipSecretAbility(true);
            UsePortal(actor, AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/Reve/ZoneManifest_Reve_Limbes.asset")); Advance(187); return;
        }
        if (report.stage == 187)
        {
            if (!ZoneReady("Assets/Scenes/Reve/ZoneManifest_Reve_Limbes.asset") || !ControlReady(out _)) return;
            Check(catalog.CountRead(service) == 6, "Read facts survive the authored Courts-to-Limbes portal."); report.limbesRouteIndex = 0; Advance(188); return;
        }
        if (report.stage == 188 || report.stage == 189)
        {
            if (ReadSecretNotes(actor, "Reve_Limbes_Secrets", 188, 189)) { report.limbesRouteIndex = 0; Advance(210); }
            return;
        }
        if (report.stage >= 210 && report.stage <= 213)
        {
            if (TravelSecret(actor, new[] { 9, 10 }, 210)) Advance(193); return;
        }
        if (report.stage == 193)
        {
            if (!ControlReady(out _)) return;
            Check(catalog.CountRead(service) == 9, "Three Limbes readings counted; both actual passages traversed without increasing reads.");
            Check(KnowledgeManager.Instance.HasKnowledge(catalog.entries[7].definition.knowledge), "A Limbes note revealed the optional Courts passage.");
            UsePortal(actor, AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/Maison/ZoneManifest_Maison.asset")); Advance(194); return;
        }
        if (report.stage == 194)
        {
            if (!ZoneReady("Assets/Scenes/Maison/ZoneManifest_Maison.asset") || !ControlReady(out _)) return;
            Check(GameFlowService.TravelToZone(AssetDatabase.LoadAssetAtPath<ZoneManifest>(CourtsAuthoring.Folder + "/ZoneManifest_District_2_Courts.asset"), CourtsSpawnPoses()), "Return-to-Courts loading fixture accepted (not a campaign entrance gate test).");
            report.limbesRouteIndex = 0; Advance(195); return;
        }
        if (report.stage == 195)
        {
            if (!ZoneReady(CourtsAuthoring.Folder + "/ZoneManifest_District_2_Courts.asset") || !ControlReady(out _)) return;
            Check(catalog.CountRead(service) == 9, "Read count and cross-zone knowledge retained on return to Courts."); Advance(220); return;
        }
        if (report.stage >= 220 && report.stage <= 223)
        {
            if (TravelSecret(actor, new[] { 3, 4, 5, 6, 7, 8 }, 220)) Advance(199); return;
        }
        if (report.stage == 199)
        {
            if (!ControlReady(out _)) return;
            Check(catalog.CountRead(service) == 9 && !service.IsCompleted(catalog.notesCycle), "Travelling all eight Courts/Limbes passages never fabricates missing notes or future content.");
            var episode = Object.FindObjectsByType<InteractableItem>().Single(n => n.representedItem != null && n.representedItem.itemId == "courts.optional.visit");
            Check(actor.TrySetUccExternalPositionAndRotation(episode.transform.position + Vector3.back * .7f, Quaternion.identity, true), "Approach the episode in the optional room already reached through Sauf-Conduit.");
            Advance(224); return;
        }
        if (report.stage == 224)
        {
            if (!ControlReady(out _)) return;
            var episode = Object.FindObjectsByType<InteractableItem>().Single(n => n.representedItem != null && n.representedItem.itemId == "courts.optional.visit");
            Check(episode.CanBeDetectedBy(actor) && CharacterInteractionDetection.IsCharacterWithinRange(actor.transform, episode.GetInteractionDetectionCollider(), episode.GetInteractionAnchor(), episode.interactionMaxDistance), "Optional episode is actually readable at this position.");
            var inventory = Object.FindAnyObjectByType<InventoryPanelController>(FindObjectsInactive.Include);
            Check(inventory.TryOpenReadableItemFromWorld(episode.representedItem, actor.gameObject), "Open the optional family episode through production UI.");
            Check(KnowledgeManager.Instance.HasKnowledge(episode.representedItem.knowledgeUnlockedOnRead[0]) && catalog.CountRead(service) == 9, "Optional family episode grants its knowledge without pretending to be a passage note.");
            typeof(InventoryPanelController).GetMethod("CloseReadablePanel", Private).Invoke(inventory, null); Advance(225); return;
        }
        if (report.stage == 225)
        {
            if (!ControlReady(out _)) return;
            if (report.districtConnection)
            {
                UsePortal(actor, AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/Maison/ZoneManifest_District_1.asset")); Advance(232); return;
            }
            AbilityService.Ensure().Restore(contentPreviousAbilities);
            Check(GameFlowService.OpenMainMenu(), "Return to menu after authored secret content qualification."); Advance(5);
        }
    }
    private static bool ReadSecretNotes(SquadCharacterController actor, string scene, int approachStage, int readStage)
    {
        if (!ControlReady(out _)) return false;
        var notes = Object.FindObjectsByType<InteractableItem>().Where(i => i.gameObject.scene.name == scene && i.representedItem != null && i.representedItem.itemId.StartsWith("secret.note.")).OrderBy(i => i.representedItem.itemId, StringComparer.Ordinal).ToArray();
        if (report.limbesRouteIndex >= notes.Length) return true;
        var note = notes[report.limbesRouteIndex];
        if (report.stage == approachStage)
        {
            Check(actor.TrySetUccExternalPositionAndRotation(note.transform.position + Vector3.back * .7f, Quaternion.identity, true), "Approach authored secret note " + note.representedItem.itemId); Advance(readStage); return false;
        }
        int before = SecretPassageNotes.Catalog.CountRead(CycleProgressionService.Instance);
        var inventory = Object.FindAnyObjectByType<InventoryPanelController>(FindObjectsInactive.Include);
        Check(inventory.TryOpenReadableItemFromWorld(note.representedItem, actor.gameObject), "Open real secret readable UI " + note.representedItem.itemId);
        Check(SecretPassageNotes.Catalog.CountRead(CycleProgressionService.Instance) == before + 1, "Actual reading advances exactly one Read fact.");
        typeof(InventoryPanelController).GetMethod("CloseReadablePanel", Private).Invoke(inventory, null);
        report.limbesRouteIndex++; Advance(approachStage); return false;
    }
    private static bool TravelSecret(SquadCharacterController actor, int[] numbers, int firstStage)
    {
        if (!ControlReady(out _)) return false;
        if (report.limbesRouteIndex >= numbers.Length) return true;
        var discovery = Physical(numbers[report.limbesRouteIndex]); var passage = discovery.GetComponent<TrouEtroit>();
        if (report.stage == firstStage)
        {
            Check(actor.TrySetUccExternalPositionAndRotation(passage.teleportPointA.position, Quaternion.identity, true), "Approach secret endpoint A " + discovery.definition.passageId); Advance(firstStage + 1); return false;
        }
        if (report.stage == firstStage + 1)
        {
            if (!StorySequenceCompletionStore.IsCompleted(discovery.firstRecognition.Sequence)) return false;
            if (Time.unscaledTime < (float)typeof(TrouEtroit).GetField("nextRequestAt", Private).GetValue(passage) + .02f ||
                EditorApplication.timeSinceStartup - report.stageStarted < .2) return false;
            Check(passage.IsDetected && discovery.IsKnown && passage.ServerTryPassage(actor.gameObject, true), "Individual traversal authorized " + discovery.definition.passageId);
            typeof(TrouEtroit).GetMethod("RequestPassage", Private).Invoke(passage, new object[] { true }); Advance(firstStage + 2); return false;
        }
        if (report.stage == firstStage + 2)
        {
            if ((actor.transform.position - passage.teleportPointB.position).sqrMagnitude > 1.5f || !actor.IsGrounded ||
                EditorApplication.timeSinceStartup - report.stageStarted < .7) return false;
            if (Time.unscaledTime < (float)typeof(TrouEtroit).GetField("nextRequestAt", Private).GetValue(passage) + .02f) return false;
            Check(!discovery.firstRecognition.IsPlaying, "Recognition not replayed at second endpoint " + discovery.definition.passageId);
            Check(passage.ServerTryPassage(actor.gameObject, true), "Return authorized from endpoint B " + discovery.definition.passageId);
            typeof(TrouEtroit).GetMethod("RequestPassage", Private).Invoke(passage, new object[] { true }); Advance(firstStage + 3); return false;
        }
        if ((actor.transform.position - passage.teleportPointA.position).sqrMagnitude > 1.5f || !actor.IsGrounded) return false;
        Check(!discovery.firstRecognition.IsPlaying, "Actual UCC return landed at A without replay " + discovery.definition.passageId);
        report.limbesRouteIndex++; Advance(firstStage); return false;
    }
}
