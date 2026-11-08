using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lit.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class LimbesExpansionVerification
{
    public static void Run()
    {
        LimbesCompletionAudit.Run();
        var manifest = AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/Reve/ZoneManifest_Reve_Limbes.asset");
        var owned = new List<Scene>();
        var checks = new List<string>();
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks.Add(message);
        }
        var setup = EditorSceneManager.GetSceneManagerSetup();
        if (string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
            EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(manifest.primaryScene), OpenSceneMode.Single);
        try
        {
            foreach (var asset in new[] { manifest.primaryScene }.Concat(manifest.loadingScenes))
            {
                string path = AssetDatabase.GetAssetPath(asset);
                if (SceneManager.GetSceneByPath(path).isLoaded) continue;
                owned.Add(EditorSceneManager.OpenScene(path, OpenSceneMode.Additive));
            }
            var cycles = Object.FindObjectsByType<CycleController>().Where(c => c.gameObject.scene.name.StartsWith("Reve_Limbes")).ToArray();
            Check(cycles.Length >= 6, "Five exploration cycles and one boss cycle are loaded.");
            Check(cycles.Select(c => c.definition.cycleId).Distinct().Count() == cycles.Length, "Cycle IDs are distinct.");
            foreach (var cycle in cycles) Check(cycle.definition.ValidateConfiguration().Count == 0, "Valid cycle " + cycle.definition.cycleId);
            var sequences = Object.FindObjectsByType<StorySequenceRunner>().Where(r => r.gameObject.scene.name.StartsWith("Reve_Limbes")).ToArray();
            Check(sequences.Select(r => r.Sequence.sequenceId).Distinct().Count() >= 6, "Six distinct bound StorySequences.");
            foreach (var runner in sequences) Check(runner.Sequence.TryValidate(out _), "Valid sequence " + runner.Sequence.sequenceId);
            var progression = Object.FindAnyObjectByType<LimbesZoneProgression>();
            Check(progression != null && progression.explorationCycles.Length == 5 && progression.explorationCycles.All(c => c != null), "Arena requires five explicit exploration cycles.");
            Check(progression.arenaAccess != null && !progression.arenaAccess.activeSelf, "Arena access starts closed.");
            Check(progression.victoryExit != null && !progression.victoryExit.activeSelf, "Victory exit starts closed.");
            var boss = Object.FindAnyObjectByType<LimbesAngelBoss>();
            Check(boss != null && EnemyEncounterActors.Resolve(boss) != null && boss.arenaCenter != null && boss.boltPrefab != null && boss.visual != null, "Angel boss has native actor, arena, stone visual and projectile.");
            Check(boss.Definition != null && boss.Definition.DamagePolicy == BossDamagePolicy.Normal && boss.Definition.SuppressDefaultEnemyBrain, "Boss uses ordinary health and exclusive custom attack driver.");
            Check(boss.GetComponents<MonoBehaviour>().Any(c => c != boss && c is ILitBrainsEncounterPolicy), "Boss has the production Flame activation policy.");
            var visibleRenderers = boss.visual.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
            Check(visibleRenderers.Length > 0, "Angel has enabled stone renderers.");
            Bounds visualBounds = visibleRenderers[0].bounds;
            foreach (var renderer in visibleRenderers) visualBounds.Encapsulate(renderer.bounds);
            Check(visualBounds.size.y > 3.4f && visualBounds.size.y < 3.6f && Vector3.Distance(visualBounds.center, boss.transform.position + Vector3.up * 1.75f) < .2f, "Angel visual is centred on its actor and stands 3.5 metres tall.");
            Physics.SyncTransforms();
            foreach (var item in Object.FindObjectsByType<InteractableItem>().Where(i => i.representedItem != null && i.representedItem.itemId.StartsWith("reve.limbes.") && i.representedItem.itemId.Contains(".proof.")))
            {
                Check(Physics.RaycastAll(item.transform.position + Vector3.up, Vector3.down, 3, ~0, QueryTriggerInteraction.Ignore).Any(h => h.collider.gameObject.scene.name == "Reve_Limbes_Progression"), "Proof has a real floor: " + item.representedItem.itemId);
                Check(item.representedItem.worldPrefab != null && item.representedItem.knowledgeUnlockedOnPickup.Count == 1, "Recoverable and knowledge-linked proof: " + item.representedItem.itemId);
            }
            Check(manifest.BakedNavMeshData != null, "Expanded zone has baked navigation.");
            var nativeNavigation = UnityEngine.AI.NavMesh.AddNavMeshData(manifest.BakedNavMeshData);
            try { Check(UnityEngine.AI.NavMesh.SamplePosition(boss.arenaCenter.position, out _, 2, UnityEngine.AI.NavMesh.AllAreas), "Boss arena exists in baked navigation."); }
            finally { nativeNavigation.Remove(); }
            File.WriteAllText("Library/ReveLimbesCompletion/AuthoringVerification.txt", string.Join("\n", checks) + "\nPASS authoring checks only; runtime and rendered traversal remain required.");
            Debug.Log("Limbes authoring verification passed: " + checks.Count + " checks.");
        }
        finally
        {
            foreach (var scene in owned.AsEnumerable().Reverse()) if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
