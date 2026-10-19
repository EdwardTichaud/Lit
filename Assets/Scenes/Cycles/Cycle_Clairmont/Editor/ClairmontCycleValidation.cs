using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ClairmontCycleValidation
{
    public const string Root = "Assets/Scenes/Cycles/Cycle_Clairmont";
    public const string Env = "Assets/Scenes/District_1/District_1_NefDesToursInterrompus_Environment.unity";
    public const string Quest = Root + "/District_1_Cycle_Clairmont.unity";
    public const string Shortcuts = "Assets/Scenes/District_1/District_1_AbilityShortcuts.unity";
    [MenuItem("Lit/Validation/Valider Clairmont et les raccourcis")]
    public static void Run()
    {
        var issues = Audit(); foreach (var issue in issues) Debug.LogError("[Clairmont] " + issue);
        System.IO.Directory.CreateDirectory("Library/ClairmontAuthoring");
        System.IO.File.WriteAllText("Library/ClairmontAuthoring/LastValidation.txt", issues.Count == 0 ? "PASS Clairmont authoring audit" : string.Join("\n", issues));
        if (issues.Count == 0) Debug.Log("[Clairmont] Cycle, environnement et raccourcis valides.");
    }
    private static T[] Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();
    public static List<string> Audit()
    {
        var issues = new List<string>(); var opened = new List<Scene>(); var previous = SceneManager.GetActiveScene();
        var def = AssetDatabase.LoadAssetAtPath<CycleDefinition>("Assets/Resources/Narrative/ClairmontCycle.asset");
        if (def == null) { issues.Add("Definition absente"); return issues; }
        issues.AddRange(def.ValidateConfiguration());
        if (def.prerequisites.conditions.Length != 0 || def.cycleId != "district1.clairmont") issues.Add("Le cycle doit rester independant.");
        if (def.steps.Count(s => s.terminal) != 1 || !def.FindStep("lucian_after_clairmont").terminal) issues.Add("Fin de cycle incorrecte.");
        foreach (string id in new[] { "relief_register_read", "old_route_read" })
            if (def.FindStep(id)?.prerequisites.conditions.Single().stepId != "nef_entered") issues.Add("Enquete non parallele: " + id);
        var manifest = AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/Maison/ZoneManifest_District_1.asset");
        try
        {
            foreach (string path in new[] { Env, Quest, Shortcuts })
            {
                if (!EditorBuildSettings.scenes.Any(s => s.enabled && s.path == path) || !manifest.loadingScenes.Any(s => AssetDatabase.GetAssetPath(s) == path) ||
                    !manifest.LoadingSceneNames.Contains(System.IO.Path.GetFileNameWithoutExtension(path))) issues.Add("Scene non declaree: " + path);
                var scene = SceneManager.GetSceneByPath(path);
                if (!scene.IsValid() || !scene.isLoaded) { scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); opened.Add(scene); }
                foreach (var t in Find<Transform>(scene)) if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) != 0) issues.Add("Script manquant: " + t.name);
            }
            var env = SceneManager.GetSceneByPath(Env); var quest = SceneManager.GetSceneByPath(Quest); var shortcuts = SceneManager.GetSceneByPath(Shortcuts);
            var controller = Find<CycleController>(quest).Single();
            if (controller.definition != def || controller.IsDevSimulationActive) issues.Add("Controleur incorrect ou Dev actif.");
            if (Find<CycleController>(env).Length != 0 || Find<GhostController>(env).Length != 0 || Find<Flame>(quest).Length != 0) issues.Add("Separation decor/quete incorrecte.");
            var proofs = Find<InteractableItem>(quest);
            if (proofs.Length != 2 || proofs.Any(p => !p.allowTake || p.interactionMaxDistance != 2 || p.representedItem == null || p.representedItem.knowledgeUnlockedOnRead.Count != 1 || p.representedItem.knowledgeUnlockedOnPickup.Count != 1)) issues.Add("Preuves mal configurees.");
            var ghost = Find<GhostController>(quest).Single();
            if (ghost.Data == null || ghost.Data.ghostId != "ghost_agnes_clairmont" || ghost.Data.reactions.Single().requirement.requiredKnowledge.Count != 3) issues.Add("Resolution du fantome incorrecte.");
            if (controller.interactions.Length != 2 || controller.interactions.Any(i => i == null || i.cycle != controller)) issues.Add("Interactions non liees.");
            var gate = Find<CycleActivationId>(env).Single();
            if (gate.activationId != "district1.clairmont.statue_belvedere_access" || gate.target == null || !gate.target.activeSelf || gate.activeWhenSet) issues.Add("Sortie persistante incorrecte.");
            var flames = Find<Flame>(env);
            if (flames.Length != 6 || flames.Any(f => !f.IsLit || f.IsAncientFlame) || flames.Select(f => f.FlameId).Distinct().Count() != flames.Length) issues.Add("Flames de la nef incorrectes.");
            foreach (var flame in flames)
                if (flame.flameLight == null || flame.flameLight.gameObject.scene != env || !flame.flameLight.transform.IsChildOf(flame.transform)) issues.Add("Lumiere de Flame absente ou externe: " + flame.name);
            foreach (var actor in proofs.Select(p => p.transform).Concat(new[] { ghost.transform, controller.interactions.Single(i => i.Ghost == null).transform }))
                if (!flames.Any(f => Vector3.Distance(actor.position, f.transform.position) < 14f)) issues.Add("Interaction hors influence: " + actor.name);
            foreach (var passage in Find<TrouEtroit>(env).Concat(Find<TrouEtroit>(shortcuts)))
            {
                if (passage.observateurAbility == null || passage.saufConduitAbility == null || !passage.requireDetection || passage.startDetected || passage.hideWhenUndetected) issues.Add("Conditions de raccourci incorrectes: " + passage.name);
                if (passage.teleportPointA == null || passage.teleportPointB == null || passage.additionalTriggers.Length != 1 || passage.additionalTriggers[0] == null || passage.keepOriginalHeight) issues.Add("Extremites non configurees: " + passage.name);
                if (passage.GetComponent<Rigidbody>()?.isKinematic != true || passage.GetComponent<PersistentSecretPassageState>() == null || passage.GetComponent<PersistentNetworkObject>()?.HasPersistentId != true) issues.Add("Physique/persistance du raccourci incorrecte: " + passage.name);
                if (!passage.GetComponentsInChildren<Renderer>(true).Any(r => r.enabled && r.sharedMaterial != null)) issues.Add("Raccourci invisible: " + passage.name);
                if (!passage.IsInsideInteractionVolume(passage.teleportPointA.position) || !passage.IsInsideInteractionVolume(passage.teleportPointB.position)) issues.Add("Extremite non interactive: " + passage.name);
                if (passage.ServerTryPassage(null, true)) issues.Add("Le raccourci accepte un acteur absent.");
            }
            if (Find<TrouEtroit>(env).Length != 1 || Find<TrouEtroit>(shortcuts).Length != 1) issues.Add("Deux raccourcis attendus.");
        }
        catch (Exception ex) { issues.Add(ex.ToString()); }
        finally { foreach (var scene in opened.AsEnumerable().Reverse()) EditorSceneManager.CloseScene(scene, true); if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
        return issues;
    }
}
