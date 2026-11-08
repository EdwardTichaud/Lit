using System;
using System.Linq;
using Lit.Story;
using UnityEditor;
using UnityEngine;

public static partial class BootstrapDistrictPlayProbe
{
    public static void StartLimbesPresentationBatch()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
        EditorApplication.delayCall += () =>
        {
            RunLimbesExpansion();
            report.scope = "Focused presentation regression: production Bootstrap, portal, thirteen pickups, five exploration cycles, six StorySequences, boss flight/projectiles/melee, completion and zone reload. Branch walking was verified by the preceding full run and is deliberately omitted here. Positioning, survival and dialogue advance are fixtures; not manual balance, cold-save or multiplayer proof.";
            Write();
        };
    }

    public static void StartLimbesExpansionBatch()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
        EditorApplication.delayCall += RunLimbesExpansion;
    }
    private static string[] ExpansionProofIds => LimbesProofIds.Concat(new[] { "creation", "captivite", "invocation" }
        .SelectMany(name => Enumerable.Range(0, 3).Select(i => "reve.limbes." + name + ".proof." + i))).ToArray();
    private static void TickLimbesExpansion()
    {
        var root = LocalPlayerContext.LocalCharacterRoot;
        var actor = root != null ? root.GetComponent<SquadCharacterController>() : null;
        if (actor == null) return;
        if (report.stage == 132)
            foreach (var panel in UnityEngine.Object.FindObjectsByType<RealTimeCombatSceneUiController>())
                if (panel.IsResultVisible)
                {
                    var button = typeof(RealTimeCombatSceneUiController).GetField("victoryContinueButton", Private).GetValue(panel) as UnityEngine.UI.Button;
                    if (button != null) { button.onClick.Invoke(); Check(true, "Production victory Continue button pressed."); }
                    else if (panel.HandleInputModeAction(InputModeAction.Submit, default))
                        Check(true, "Production victory Submit input accepted.");
                }
        // Exercise the real runner through its input latch; never mark completion directly.
        foreach (var runner in UnityEngine.Object.FindObjectsByType<StorySequenceRunner>())
            if (runner.IsPlaying) typeof(StorySequenceRunner).GetField("advanceRequested", Private).SetValue(runner, true);
        if (StorySequenceRunner.IsAnySequencePlaying) return;
        var service = CycleProgressionService.Instance;
        if (service == null || !service.IsReady) return;
        var flow = UnityEngine.Object.FindAnyObjectByType<LimbesZoneProgression>();
        if (report.stage == 100 || report.stage == 101)
        {
            if (report.limbesRouteIndex >= ExpansionProofIds.Length) { report.limbesRouteIndex = 0; report.limbesWalkIndex = 0; Advance(report.scope.StartsWith("Focused presentation", StringComparison.Ordinal) ? 110 : 105); return; }
            string id = ExpansionProofIds[report.limbesRouteIndex];
            if (actor.Items.Any(i => i != null && i.itemId == id)) { report.limbesRouteIndex++; Advance(100); return; }
            var item = UnityEngine.Object.FindObjectsByType<InteractableItem>().Single(i => i.representedItem != null && i.representedItem.itemId == id);
            if (report.stage == 100)
            {
                Check(actor.TrySetUccExternalPositionAndRotation(item.transform.position + Vector3.back, Quaternion.identity, true), "Approach proof " + id);
                Advance(101); return;
            }
            if (!ControlReady(out _)) return;
            Check(item.CanBeDetectedBy(actor) && CharacterInteractionDetection.IsInActiveFlameInfluence(item), "Proof is lit and detectable " + id);
            item.SetDetectedCharacter(actor.gameObject);
            typeof(InteractableItem).GetMethod("TakeAllItems", Private).Invoke(item, null);
            Check(actor.Items.Any(i => i != null && i.itemId == id) && KnowledgeManager.Instance.HasKnowledgeId(id), "Actual item and knowledge acquired " + id);
            report.limbesRouteIndex++; Advance(100); return;
        }
        if (report.stage == 105)
        {
            if (report.limbesRouteIndex >= 3) { actor.Stop(); report.limbesRouteIndex = 0; Advance(110); return; }
            string label = new[] { "Creation", "Captivite", "Invocation" }[report.limbesRouteIndex];
            var branch = UnityEngine.Object.FindObjectsByType<Transform>().Single(t => t.gameObject.scene.name == "Reve_Limbes_Progression" && t.name == "Branche_" + label);
            var nodes = branch.Cast<Transform>().Where(t => t.name.StartsWith("Montee_basse_") || t.name.StartsWith("Retour_en_surplomb_")).OrderBy(t => t.name, StringComparer.Ordinal).ToArray();
            if (report.limbesWalkIndex == 0)
            {
                Check(actor.TrySetUccExternalPositionAndRotation(nodes[0].position + Vector3.up * .35f, Quaternion.identity, true), "Approach lower landing " + label);
                report.limbesWalkIndex = 1; return;
            }
            if (report.limbesWalkIndex >= nodes.Length)
            {
                Check(actor.IsGrounded, "Grounded at the high landing " + label);
                Check(actor.transform.position.y >= nodes[nodes.Length - 1].position.y, "Actual UCC climb reached the authored high landing height " + label);
                Check(true, "Actual UCC walking traversed the complete lower climb and elevated return " + label);
                actor.Stop(); report.limbesRouteIndex++; report.limbesWalkIndex = 0; Advance(105); return;
            }
            Vector3 delta = nodes[report.limbesWalkIndex].position + Vector3.up * .25f - actor.transform.position;
            if (Vector2.Distance(new Vector2(delta.x, delta.z), Vector2.zero) < .45f && actor.IsGrounded) report.limbesWalkIndex++;
            else actor.MoveWorld(new Vector2(delta.x, delta.z).normalized);
            return;
        }
        if (report.stage == 110 || report.stage == 111 || report.stage == 112)
        {
            string name = report.limbesRouteIndex == 0 ? "Seuil" : "Chambre";
            var definition = AssetDatabase.LoadAssetAtPath<CycleDefinition>("Assets/Resources/Narrative/Limbes" + name + "Cycle.asset");
            if (service.IsCompleted(definition))
            {
                Check(true, "Exploration cycle resolved " + definition.cycleId);
                report.limbesRouteIndex++;
                Advance(report.limbesRouteIndex == 2 ? 120 : 110); return;
            }
            var interaction = UnityEngine.Object.FindObjectsByType<CycleInteraction>().Single(i => i.cycle != null && i.cycle.definition == definition);
            if (report.stage == 110)
            {
                Check(actor.TrySetUccExternalPositionAndRotation(interaction.transform.position + Vector3.back, Quaternion.identity, true), "Approach " + name);
                Advance(111); return;
            }
            if (report.stage == 111 && ControlReady(out _)) { Check(interaction.TryInteract(), "Dialogue accepted " + name); Advance(112); }
            return;
        }
        if (report.stage == 120)
        {
            foreach (string name in new[] { "Creation", "Captivite", "Invocation" })
            {
                var definition = AssetDatabase.LoadAssetAtPath<CycleDefinition>("Assets/Resources/Narrative/Limbes" + name + "Cycle.asset");
                if (service.IsCompleted(definition)) continue;
                var gate = UnityEngine.Object.FindObjectsByType<LimbesMemorySequence>().Single(g => g.cycle == definition);
                actor.TrySetUccExternalPositionAndRotation(gate.transform.position + Vector3.back, Quaternion.identity, true);
                return;
            }
            Check(flow != null && flow.explorationCycles.All(service.IsCompleted), "All five exploration cycles completed by actual evidence and dialogue.");
            if (!flow.arenaAccess.activeSelf) return;
            Check(flow.arenaAccess.activeInHierarchy, "Five cycles opened the actual arena bridge.");
            actor.TrySetUccExternalPositionAndRotation(new Vector3(574, 142.1f, 85), Quaternion.identity, true);
            Advance(121); return;
        }
        if (report.stage == 121)
        {
            if (EditorApplication.timeSinceStartup - report.stageStarted < 1) return;
            if (report.grapple) { Advance(500); return; }
            CaptureLiveLimbes();
            actor.TrySetUccExternalPositionAndRotation(new Vector3(604, 129.1f, 114), Quaternion.identity, true);
            Advance(130); return;
        }
        if (report.stage == 130)
        {
            if (!StorySequenceCompletionStore.IsCompleted(flow.bossIntroduction) || !flow.boss.unlocked) return;
            Check(flow.bossIntroduction != null && flow.boss.unlocked, "Boss introduction completed; actor unlocked.");
            Advance(129); return;
        }
        if (report.stage == 129)
        {
            if (EditorApplication.timeSinceStartup - report.stageStarted < 1) return;
            CaptureLiveLimbes();
            actor.TrySetUccExternalPositionAndRotation(new Vector3(604, 129.1f, 122), Quaternion.identity, true);
            actor.SetHealth(500, 500);
            Advance(131); return;
        }
        if (report.stage == 131)
        {
            var boss = flow.boss;
            if (!boss.IsBossEngaged) return;
            report.limbesBossPeak = Mathf.Max(report.limbesBossPeak, boss.transform.position.y - boss.arenaCenter.position.y);
            // Survival fixture; does not suppress attack execution or alter boss health.
            if (actor.CurrentHp < 250) actor.SetCurrentHp(500);
            if (boss.BoltsFired < 2 || boss.SweepsPerformed < 1) return;
            Check(report.limbesBossPeak >= 4, "Boss physically flew at least four metres above the arena.");
            Check(boss.BoltsFired >= 2 && boss.SweepsPerformed >= 1, "Live boss executed flight projectiles and a telegraphed grounded sweep.");
            CaptureLiveLimbes();
            var enemy = boss.GetComponent<LitBrainsEnemy>();
            Check(enemy != null && enemy.ReceiveDamage(enemy.Health.CurrentHp) > 0, "Production actor damage defeats the boss.");
            actor.TrySetUccExternalPositionAndRotation(new Vector3(604, 129.1f, 123), Quaternion.identity, true);
            Advance(132); return;
        }
        if (report.stage == 132)
        {
            if (!service.IsCompleted(flow.conclusionCycle) || !flow.victoryExit.activeInHierarchy) return;
            if (report.grapple && !GrappleReturnComplete)
            {
                var anchor = Lit.Traversal.GrappleAnchor.Find("limbes.statue.retour");
                Check(actor.TrySetUccExternalPositionAndRotation(anchor.launch.position, Quaternion.identity, true), "Post-victory approach fixture for the grapple return; combat gating respected.");
                Advance(503); return;
            }
            Check(true, "Boss defeat and successful departure sequence complete the zone and reveal the victory exit.");
            foreach (var runner in UnityEngine.Object.FindObjectsByType<StorySequenceRunner>().Where(r => r.gameObject.scene.name.StartsWith("Reve_Limbes") &&
                r.Sequence != null && r.Sequence.sequenceId.StartsWith("limbes.", StringComparison.Ordinal)))
                Check(StorySequenceCompletionStore.IsCompleted(runner.Sequence), "Sequence completion persisted " + runner.Sequence.sequenceId);
            if (report.grapple) Check(GameFlowService.ReturnToHub(), "Production ReturnToHub saves the post-victory grapple state and leaves the zone.");
            else UsePortal(actor, AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/Maison/ZoneManifest_Maison.asset"));
            Advance(133); return;
        }
        if (report.stage == 133)
        {
            if (!ZoneReady("Assets/Scenes/Maison/ZoneManifest_Maison.asset")) return;
            UsePortal(actor, AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/Reve/ZoneManifest_Reve_Limbes.asset")); Advance(134); return;
        }
        if (report.stage == 134)
        {
            if (!ZoneReady("Assets/Scenes/Reve/ZoneManifest_Reve_Limbes.asset") || flow == null || !flow.victoryExit.activeSelf) return;
            Check(flow.explorationCycles.All(service.IsCompleted) && service.IsCompleted(flow.conclusionCycle), "Six cycles and final exit survive zone departure and re-entry.");
            Check(GameFlowService.OpenMainMenu(), "Return to menu after expanded zone verification."); Advance(5);
        }
    }
}
