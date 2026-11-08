using System;
using System.Collections.Generic;
using System.Linq;
using Lit.Story;
using UnityEditor;
using UnityEngine;

public static partial class BootstrapDistrictPlayProbe
{
    private static GameObject secretFixture;
    private static KnowledgePassageDiscovery secretDiscovery;
    private static TrouEtroit secretPassage;
    private static StorySequenceRunner secretRecognition;
    private static SecretPassageDefinition secretDefinition;
    private static readonly List<UnityEngine.Object> secretAssets = new();
    private static AbilitySaveData secretPreviousAbilities;

    public static void StartSecretPassageKnowledgeBatch()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Bootstrap, UnityEditor.SceneManagement.OpenSceneMode.Single);
        EditorApplication.delayCall += () => Begin(false, false, true, false, false, true);
    }

    private static void BeginSecretPassageDiscovery(SquadCharacterController actor)
    {
        var rules = CycleProgressionService.Instance?.GetComponent<WorldRulesStateManager>();
        Check(rules != null, "Production world rules are available for persistent discovery attempts.");
        string characterId = KnowledgePassageDiscovery.ResolveCharacterId(actor.gameObject);
        Check(!string.IsNullOrWhiteSpace(characterId), "Production avatar supplies a stable character identity.");
        secretPreviousAbilities = AbilityService.Ensure().Capture();
        secretDefinition = ScriptableObject.CreateInstance<SecretPassageDefinition>(); secretAssets.Add(secretDefinition);
        secretDefinition.passageId = "validation.passage." + report.fixtureId;
        secretDefinition.knowledge = ScriptableObject.CreateInstance<KnowledgeSO>(); secretAssets.Add(secretDefinition.knowledge);
        secretDefinition.knowledge.knowledgeId = "validation.passage.knowledge." + report.fixtureId;
        secretDefinition.knowledge.title = "Connaissance de validation uniquement";
        // A deterministic failure fixture checks the route through later shared knowledge.
        rules.SetInt(SecretPassageAttemptLedger.AttemptKey(secretDefinition.passageId, characterId), 1);
        secretFixture = new GameObject("Owned secret-passage Play fixture"); secretFixture.SetActive(false);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(secretFixture, actor.gameObject.scene);
        secretFixture.transform.position = actor.transform.position;
        var trigger = secretFixture.AddComponent<BoxCollider>(); trigger.isTrigger = true; trigger.size = Vector3.one * 4;
        secretPassage = secretFixture.AddComponent<TrouEtroit>();
        secretPassage.observateurAbility = AssetDatabase.LoadAssetAtPath<AbilitySO>("Assets/Resources/Abilities/Ability_Observateur.asset");
        secretPassage.saufConduitAbility = AssetDatabase.LoadAssetAtPath<AbilitySO>("Assets/Resources/Abilities/Ability_SaufConduit.asset");
        var first = new GameObject("Entry").transform; first.SetParent(secretFixture.transform, false);
        var second = new GameObject("Exit").transform; second.SetParent(secretFixture.transform, false);
        second.position = SecretPassageSafeDestination(actor);
        var otherTrigger = second.gameObject.AddComponent<BoxCollider>(); otherTrigger.isTrigger = true; otherTrigger.size = Vector3.one * 3;
        secretPassage.additionalTriggers = new[] { otherTrigger };
        secretPassage.teleportPointA = first; secretPassage.teleportPointB = second;
        var rig = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Narrative/StorySequence/Prefabs/StorySequenceRig.prefab"), secretFixture.transform);
        secretRecognition = rig.GetComponent<StorySequenceRunner>();
        var sequence = ScriptableObject.CreateInstance<StorySequenceAsset>(); secretAssets.Add(sequence);
        sequence.sequenceId = "validation.passage.recognition." + report.fixtureId; sequence.playOnce = true;
        sequence.startFromBlack = false; sequence.fadeToBlackBeforeGameplay = false; sequence.fadeFromBlackAfterGameplayRestore = false;
        var line = ScriptableObject.CreateInstance<VoiceLineData>(); secretAssets.Add(line);
        line.voiceLineText = "Réplique de validation du passage, hors contenu narratif.";
        sequence.steps.Add(new StorySequenceStep { type = StorySequenceStepType.Dialogue, actorId = "Lucian", speakerNameOverride = "Validation", voiceLine = line, focusCameraOnSpeaker = false });
        typeof(StorySequenceRunner).GetField("sequence", Private).SetValue(secretRecognition, sequence);
        typeof(StorySequenceRunner).GetField("playOnStart", Private).SetValue(secretRecognition, false);
        secretDiscovery = secretFixture.AddComponent<KnowledgePassageDiscovery>();
        secretDiscovery.definition = secretDefinition; secretDiscovery.worldRules = rules; secretDiscovery.firstRecognition = secretRecognition;
        secretFixture.SetActive(true); Physics.SyncTransforms();
    }

    private static Vector3 SecretPassageSafeDestination(SquadCharacterController actor)
    {
        foreach (float distance in new[] { 2f, 3f, 4f })
        foreach (var direction in new[] { Vector3.right, Vector3.forward, Vector3.left, Vector3.back })
        foreach (var hit in Physics.RaycastAll(actor.transform.position + direction * distance + Vector3.up * 2, Vector3.down, 5, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
        {
            if (hit.collider.transform.IsChildOf(actor.transform) || hit.normal.y < .7f || Mathf.Abs(hit.point.y - actor.transform.position.y) > .4f) continue;
            Vector3 point = hit.point + Vector3.up * .02f;
            if (Physics.OverlapCapsule(point + Vector3.up * .3f, point + Vector3.up * 1.5f, .25f, ~0, QueryTriggerInteraction.Ignore)
                .Any(c => !c.transform.IsChildOf(actor.transform))) continue;
            return point;
        }
        throw new InvalidOperationException("No safe actual floor for the temporary passage destination.");
    }

    private static void TickSecretPassageDiscovery()
    {
        var root = LocalPlayerContext.LocalCharacterRoot;
        var actor = root != null ? root.GetComponent<SquadCharacterController>() : null;
        if (actor == null) return;
        if (report.stage == 140)
        {
            if (EditorApplication.timeSinceStartup - report.stageStarted < 2) return;
            Check(!secretDiscovery.IsKnown && !secretPassage.IsDetected, "A stored failed Observer attempt does not reveal the nearby passage.");
            Check(!secretDiscovery.ShouldRequest(actor.gameObject, true), "Stored failure suppresses repeated local requests without rerolling.");
            Check(KnowledgeReveal.Reveal(secretDefinition.knowledge, actor.gameObject, "validation reading source"), "Production knowledge reveal accepts the later reading fixture.");
            Advance(141); return;
        }
        if (report.stage == 141)
        {
            if (!secretPassage.IsDetected || !secretRecognition.IsPlaying) return;
            Check(secretDiscovery.IsKnown, "Knowledge automatically reveals a passage while the avatar is already in its approach volume.");
            Check(true, "The authored playOnce recognition runner started through the production discovery component.");
            typeof(StorySequenceRunner).GetField("advanceRequested", Private).SetValue(secretRecognition, true);
            Advance(142); return;
        }
        if (report.stage == 142)
        {
            if (secretRecognition.IsPlaying)
            { typeof(StorySequenceRunner).GetField("advanceRequested", Private).SetValue(secretRecognition, true); return; }
            if (!StorySequenceCompletionStore.IsCompleted(secretRecognition.Sequence) || !ControlReady(out _) ||
                EditorApplication.timeSinceStartup - report.stageStarted < 1) return;
            Check(!secretPassage.ServerTryPassage(actor.gameObject, true), "Observer alone cannot traverse the known passage.");
            var abilities = AbilityService.Ensure();
            if (!abilities.IsUnlocked(secretPassage.saufConduitAbility)) Check(abilities.Unlock(secretPassage.saufConduitAbility, false), "Trusted traversal-ability fixture unlocked the production ability.");
            Check(abilities.RequestEquip(secretPassage.saufConduitAbility), "Individual Sauf-Conduit equipped through the normal Maison API.");
            Check(secretPassage.ServerTryPassage(actor.gameObject, true), "Server accepts traversal only after the individual ability gate.");
            typeof(TrouEtroit).GetMethod("RequestPassage", Private).Invoke(secretPassage, new object[] { true });
            Advance(143); return;
        }
        if (report.stage == 143)
        {
            if ((actor.transform.position - secretPassage.teleportPointB.position).sqrMagnitude > .15f || !ControlReady(out _) ||
                EditorApplication.timeSinceStartup - report.stageStarted < 1) return;
            Check(true, "Actual production passage request moved the real UCC avatar onto the opposite endpoint floor.");
            Check(!secretRecognition.IsPlaying && StorySequenceCompletionStore.IsCompleted(secretRecognition.Sequence), "The recognition dialogue did not replay at the second endpoint.");
            AbilityService.Ensure().Restore(secretPreviousAbilities);
            KnowledgeManager.Instance.RemoveKnowledge(secretDefinition.knowledge);
            UnityEngine.Object.Destroy(secretFixture);
            foreach (var asset in secretAssets) if (asset != null) UnityEngine.Object.Destroy(asset);
            secretAssets.Clear();
            Check(GameFlowService.OpenMainMenu(), "Return to menu after the temporary passage fixture.");
            Advance(5);
        }
    }
}
