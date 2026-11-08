using System;
using System.Linq;
using Lit.Story;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class BootstrapDistrictPlayProbe
{
    public static void VerifyAndStartDistrictConnectionBatch()
    {
        SecretPassagesAuthoring.Verify();
        StartDistrictConnectionBatch();
    }
    public static void StartDistrictConnectionBatch()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Bootstrap, UnityEditor.SceneManagement.OpenSceneMode.Single);
        EditorApplication.delayCall += () => Begin(false, false, true, false, false, false, true, true, true);
    }
    private static void TickDistrictConnection()
    {
        string courtsPath = CourtsAuthoring.Folder + "/ZoneManifest_District_2_Courts.asset";
        if (report.stage == 246)
        {
            var flow = GameFlowService.Instance;
            if (flow.IsTransitioning || flow.HasGameplaySession ||
                !UnityEngine.SceneManagement.SceneManager.GetSceneByName("MainMenu").isLoaded) return;
            Check(LocalPlayerContext.LocalCharacterRoot == null, "Menu released the previous District_2 avatar.");
            Check(GameFlowService.StartOrLoadGame(), "Resume District_2 from saved metadata, without an explicit destination fixture.");
            Advance(247); return;
        }
        foreach (var runner in Object.FindObjectsByType<StorySequenceRunner>())
            if (runner.IsPlaying) typeof(StorySequenceRunner).GetField("advanceRequested", Private).SetValue(runner, true);
        var actor = LocalPlayerContext.LocalCharacterRoot != null ? LocalPlayerContext.LocalCharacterRoot.GetComponent<SquadCharacterController>() : null;
        var service = CycleProgressionService.Instance; var catalog = SecretPassageNotes.Catalog;
        if (actor == null || service == null || !service.IsReady || catalog == null) return;
        if (actor.CurrentHp < 200) actor.SetHealth(500, 500);
        const string districtPath = "Assets/Scenes/Maison/ZoneManifest_District_1.asset";
        if (report.stage == 230)
        {
            if (!ZoneReady(districtPath) || !ControlReady(out _)) return;
            var balconyFloor = Object.FindObjectsByType<UnityEngine.ProBuilder.ProBuilderMesh>(FindObjectsInactive.Include)
                .Single(mesh => mesh.gameObject.scene.name == "District_1_Balcony_Environment");
            var floorCollider = balconyFloor.GetComponent<MeshCollider>();
            Check(floorCollider.enabled && !floorCollider.isTrigger && floorCollider.sharedMesh != null &&
                floorCollider.sharedMesh == balconyFloor.GetComponent<MeshFilter>().sharedMesh,
                "The authored balcony floor retains matching solid collision and visible geometry.");
            Check(LitTacticalMaskProperties.Supports(balconyFloor.GetComponent<MeshRenderer>().sharedMaterial),
                "The repaired balcony floor material supports tactical visibility and ice rendering.");
            var bounds = floorCollider.bounds;
            Check(floorCollider.Raycast(new Ray(bounds.center + Vector3.up * (bounds.extents.y + 2), Vector3.down), out var floorHit, bounds.size.y + 4) && floorHit.normal.y > .9f,
                "A downward physical query hits the actual balcony walking surface.");
            var liveSkin = Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Exclude)
                .Where(renderer => renderer.enabled).SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null && material.name.StartsWith("Ga_Skin_", StringComparison.Ordinal)).Distinct().ToArray();
            Check(liveSkin.Length >= 4, "The production party exposes actual live skin renderers for material validation.");
            Check(liveSkin.All(material => material.GetFloat("_MaterialID") == 1 &&
                !material.IsKeywordEnabled("_MATERIAL_FEATURE_SUBSURFACE_SCATTERING") &&
                material.GetTexture("_BaseColorMap") != null && material.GetTexture("_NormalMap") != null),
                "Live party skin uses Standard HQ shading with valid diffuse/normal maps; the archived SSS maps are not sampled.");
            var clairmont = AssetDatabase.LoadAssetAtPath<CycleDefinition>("Assets/Resources/Narrative/ClairmontCycle.asset");
            Check(!service.IsCompleted(clairmont), "Clairmont is incomplete before the first District-to-Courts departure.");
            Check(Object.FindObjectsByType<PortalController>().Any(p => p.name == "Acces_libre_aux_Cours_interieures" && p.isActiveAndEnabled), "Free authored entrance is available without any quest completion.");
            UsePortal(actor, AssetDatabase.LoadAssetAtPath<ZoneManifest>(courtsPath)); Advance(231); return;
        }
        if (report.stage == 231)
        {
            if (!ZoneReady(courtsPath) || !ControlReady(out _)) return;
            Check(catalog.CountRead(service) == 0, "First actual campaign entrance reached Courts before any global note was read.");
            Advance(160); return;
        }
        if (report.stage == 232)
        {
            if (!ZoneReady(districtPath) || !ControlReady(out _)) return;
            Check(catalog.CountRead(service) == 9, "Nine cross-zone note readings survive return to District 1.");
            var note = Object.FindObjectsByType<InteractableItem>().Single(n => n.representedItem != null && n.representedItem.itemId == "secret.note.07");
            Check(actor.TrySetUccExternalPositionAndRotation(note.transform.position + Vector3.back * .7f, Quaternion.identity, true), "Approach the District note for the already-observed Courts passage."); Advance(233); return;
        }
        if (report.stage == 233)
        {
            if (!ControlReady(out _)) return;
            var note = Object.FindObjectsByType<InteractableItem>().Single(n => n.representedItem != null && n.representedItem.itemId == "secret.note.07");
            var inventory = Object.FindAnyObjectByType<InventoryPanelController>(FindObjectsInactive.Include);
            Check(inventory.TryOpenReadableItemFromWorld(note.representedItem, actor.gameObject), "Read the real District note after Observer had already granted its knowledge.");
            Check(catalog.CountRead(service) == 10 && !service.IsCompleted(catalog.notesCycle), "Ten actual notes counted; ten future reservations remain incomplete.");
            Check(KnowledgeManager.Instance.HasKnowledge(catalog.entries[1].definition.knowledge), "The Limbes note revealed the new District shortcut.");
            typeof(InventoryPanelController).GetMethod("CloseReadablePanel", Private).Invoke(inventory, null); report.limbesRouteIndex = 0; Advance(240); return;
        }
        if (report.stage >= 240 && report.stage <= 243)
        {
            if (TravelSecret(actor, new[] { 1, 2 }, 240)) Advance(244); return;
        }
        if (report.stage == 244)
        {
            if (!ControlReady(out _)) return;
            Check(catalog.CountRead(service) == 10, "Both District passages traversed without fabricating read facts.");
            Check(NetcodeSceneIdUtility.GetStableId(Physical(1).transform) == 1734355439, "Legacy District passage identity preserved in actual runtime.");
            UsePortal(actor, AssetDatabase.LoadAssetAtPath<ZoneManifest>(courtsPath)); Advance(245); return;
        }
        if (report.stage == 245)
        {
            if (!ZoneReady(courtsPath) || !ControlReady(out _)) return;
            Check(catalog.CountRead(service) == 10 && !service.IsCompleted(catalog.notesCycle), "Ten current notes retained through the actual District-to-Courts return.");
            var progression = Object.FindAnyObjectByType<CourtsProgression>();
            Check(service.IsCompleted(progression.family) && service.IsCompleted(progression.finale) && progression.nextZonePortal.activeSelf, "Completed Courts route remains open after full campaign circuit.");
            AbilityService.Ensure().Restore(contentPreviousAbilities);
            var store = CharacterStateStore.Instance;
            bool capture = store.captureScreenshotOnSave, force = store.forceScreenshotOnSave;
            try { store.captureScreenshotOnSave = store.forceScreenshotOnSave = false; store.Save(); }
            finally { store.captureScreenshotOnSave = capture; store.forceScreenshotOnSave = force; }
            Check(store.LastSaveSucceeded, "District_2 character/world/metadata save committed through production path.");
            Check(SaveSessionManager.Instance.GetActiveSaveSceneName() == "District_2_Courts" &&
                MainMenuSaveCatalog.SceneLabel(SaveSessionManager.Instance.GetActiveSaveSceneName()) == "District 2 — Stillhaven",
                "Saved district identity is Stillhaven while scene IDs remain compatible.");
            Check(GameFlowService.OpenMainMenu(), "Return to menu before a real saved District_2 resume."); Advance(246); return;
        }
        if (report.stage == 247)
        {
            if (!ZoneReady(courtsPath) || !ControlReady(out _)) return;
            var progression = Object.FindAnyObjectByType<CourtsProgression>();
            Check(progression != null && service.IsCompleted(progression.family) && service.IsCompleted(progression.finale) &&
                !progression.commonAccessBarrier.activeSelf && progression.nextZonePortal.activeSelf,
                "Menu resume loaded District_2 environment, completed cycles and open physical accesses.");
            Check(catalog.CountRead(service) == 10 && !service.IsCompleted(catalog.notesCycle), "Ten read facts survived disk save and menu resume.");
            Check(!StorySequenceRunner.IsAnySequencePlaying, "Completed District_2 memories do not replay after saved resume.");
            Check(GameFlowService.OpenMainMenu(), "Return to menu after qualified District_2 resume."); Advance(5);
        }
    }
}
