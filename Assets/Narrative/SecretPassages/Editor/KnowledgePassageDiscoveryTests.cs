using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class KnowledgePassageDiscoveryTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<Object> owned = new List<Object>();
    private GameObject actor;
    private Transform previousLocal;
    private CharacterData character;
    private TrouEtroit passage;
    private KnowledgePassageDiscovery discovery;
    private SecretPassageDefinition definition;
    private WorldRulesStateManager rules;
    private KnowledgeManager knowledge;
    private AbilityService abilities;
    private AbilitySO observer, traversal;

    private T Data<T>() where T : ScriptableObject
    { var value = ScriptableObject.CreateInstance<T>(); owned.Add(value); return value; }
    private GameObject Root(string name, bool active = false)
    { var root = new GameObject(name); root.SetActive(active); owned.Add(root); return root; }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static void Invoke(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
    private void Equip(AbilitySO ability)
    {
        var state = new AbilitySaveData(); state.Unlock(observer.abilityId); state.Unlock(traversal.abilityId);
        if (ability != null) state.Equip(AbilityService.SoloPlayerId, ability.abilityId);
        abilities.Restore(state);
    }

    [SetUp] public void Setup()
    {
        Assert.That(Application.isPlaying, Is.False);
        Assert.That(AbilityService.Instance == null, Is.True, "Do not replace a running session.");
        Assert.That(KnowledgeManager.Instance == null, Is.True, "Do not replace a running knowledge manager.");
        previousLocal = LocalPlayerContext.LocalCharacterRoot;
        rules = Root("Owned world variables").AddComponent<WorldRulesStateManager>();
        knowledge = Root("Owned knowledge manager").AddComponent<KnowledgeManager>();
        Set(knowledge, "dontDestroyOnLoad", false); Set(knowledge, "playUnlockEffects", false); Invoke(knowledge, "Awake");
        abilities = Root("Owned ability service").AddComponent<AbilityService>(); Invoke(abilities, "Awake");
        observer = AssetDatabase.LoadAssetAtPath<AbilitySO>("Assets/Resources/Abilities/Ability_Observateur.asset");
        traversal = AssetDatabase.LoadAssetAtPath<AbilitySO>("Assets/Resources/Abilities/Ability_SaufConduit.asset");
        Assert.That(observer, Is.Not.Null); Assert.That(traversal, Is.Not.Null);
        actor = Root("Owned player identity"); actor.AddComponent<Rigidbody>(); actor.AddComponent<CapsuleCollider>();
        var controller = actor.AddComponent<SquadCharacterController>();
        character = Data<CharacterData>(); character.characterId = "test.character.one";
        Set(controller, "characterData", character);
        LocalPlayerContext.SetLocalCharacter(actor.transform, "secret passage test");
        var passageRoot = Root("Owned physical passage", true);
        var trigger = passageRoot.AddComponent<BoxCollider>(); trigger.isTrigger = true; trigger.size = Vector3.one * 4;
        passage = passageRoot.AddComponent<TrouEtroit>(); passage.observateurAbility = observer; passage.saufConduitAbility = traversal;
        discovery = passageRoot.AddComponent<KnowledgePassageDiscovery>(); discovery.worldRules = rules;
        definition = Data<SecretPassageDefinition>(); definition.passageId = "test.passage";
        definition.knowledge = Data<KnowledgeSO>(); definition.knowledge.knowledgeId = "test.passage.knowledge";
        discovery.definition = definition;
        Physics.SyncTransforms();
    }

    [TearDown] public void Cleanup()
    {
        if (actor != null) LocalPlayerContext.ClearIfMatch(actor.transform, "secret passage test cleanup");
        if (previousLocal != null) LocalPlayerContext.SetLocalCharacter(previousLocal, "restore test context");
        if (knowledge != null) knowledge.PrepareForRuntimeReset("secret passage fixture cleanup");
        if (abilities != null) Invoke(abilities, "OnDestroy");
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
    }

    [Test] public void KnownPassageCanBeRecognizedWithoutObserverAndTraversalRemainsSeparate()
    {
        Equip(traversal);
        Assert.That(passage.ServerTryPassage(actor, false), Is.False);
        KnowledgeReveal.Reveal(definition.knowledge, actor, "test note reading");
        Assert.That(passage.ServerTryPassage(actor, false), Is.True);
        Assert.That(passage.IsDetected, Is.True); Assert.That(rules.CaptureVariables(), Is.Empty);
        Assert.That(passage.ServerTryPassage(actor, true), Is.True);
        Equip(observer); Assert.That(passage.ServerTryPassage(actor, true), Is.False);
    }

    [Test] public void RealObserverRollIsStoredOnceAndOnlySuccessRevealsKnowledge()
    {
        Equip(observer);
        var original = Random.state;
        try
        {
            Random.InitState(9471); var start = Random.state; int expected = Random.Range(1, 7); Random.state = start;
            Assert.That(passage.ServerTryPassage(actor, false), Is.EqualTo(expected >= 2));
            Assert.That(discovery.IsKnown, Is.EqualTo(expected >= 2));
            Assert.That(knowledge.UnlockedKnowledge.Count, Is.EqualTo(expected >= 2 ? 1 : 0));
            var after = Random.state;
            passage.ServerTryPassage(actor, false);
            Assert.That(Random.state, Is.EqualTo(after));
            Assert.That(SecretPassageAttemptLedger.HasAttempted(rules, definition.passageId, character.characterId), Is.True);
        }
        finally { Random.state = original; }
    }

    [Test] public void FailedAttemptDoesNotBlockTheNoteOrSpendAnotherCharactersAttempt()
    {
        Equip(observer);
        rules.SetInt(SecretPassageAttemptLedger.AttemptKey(definition.passageId, character.characterId), 1);
        Assert.That(passage.ServerTryPassage(actor, false), Is.False);
        Assert.That(discovery.ShouldRequest(actor, true), Is.False);
        KnowledgeReveal.Reveal(definition.knowledge, actor, "test note reading");
        Assert.That(discovery.ShouldRequest(actor, false), Is.True);
        Assert.That(passage.ServerTryPassage(actor, false), Is.True);
        character.characterId = "test.character.two";
        Assert.That(SecretPassageAttemptLedger.HasAttempted(rules, definition.passageId, character.characterId), Is.False);
        Assert.That(passage.ServerTryPassage(actor, false), Is.True);
    }

    [Test] public void BothPhysicalEndsRequireRealProximityAndRemoteRequestsNeverSpendADie()
    {
        Equip(observer); actor.transform.position = Vector3.right * 30; Physics.SyncTransforms();
        Assert.That(passage.ServerTryPassage(actor, false), Is.False); Assert.That(rules.CaptureVariables(), Is.Empty);
        var end = Root("Owned second endpoint", true); end.transform.position = Vector3.right * 30;
        var trigger = end.AddComponent<BoxCollider>(); trigger.isTrigger = true; trigger.size = Vector3.one * 4;
        passage.additionalTriggers = new[] { trigger }; Physics.SyncTransforms();
        KnowledgeReveal.Reveal(definition.knowledge, actor, "test note reading");
        Assert.That(passage.ServerTryPassage(actor, false), Is.True);
        Equip(traversal); Assert.That(passage.ServerTryPassage(actor, true), Is.True);
        actor.transform.position += Vector3.up * 20; Assert.That(passage.ServerTryPassage(actor, true), Is.False);
    }

    [Test] public void EmptyOrDisabledDefinitionNeverFallsBackToGuaranteedLegacyDiscovery()
    {
        Equip(observer); definition.passageId = "";
        Assert.That(passage.ServerTryPassage(actor, false), Is.False);
        definition.passageId = "test.passage"; discovery.enabled = false;
        Assert.That(passage.ServerTryPassage(actor, false), Is.False);
        Assert.That(rules.CaptureVariables(), Is.Empty);
    }

    [Test] public void SecondEndpointIsRecognizedEvenWithoutAPhysicsEnterCallback()
    {
        Equip(observer);
        var endpoint = Root("Owned additional approach volume", true); endpoint.transform.position = Vector3.right * 30;
        var trigger = endpoint.AddComponent<BoxCollider>(); trigger.isTrigger = true; trigger.size = Vector3.one * 4;
        passage.additionalTriggers = new[] { trigger }; actor.transform.position = endpoint.transform.position;
        Physics.SyncTransforms(); Invoke(passage, "RefreshCurrentCharacter");
        Assert.That(typeof(TrouEtroit).GetField("currentCharacter", Private).GetValue(passage), Is.SameAs(actor));
    }

    [Test] public void MissingCharacterIdentityNeverUsesTransientObjectOrClientIds()
    {
        Equip(observer); character.characterId = "";
        Assert.That(passage.ServerTryPassage(actor, false), Is.False); Assert.That(rules.CaptureVariables(), Is.Empty);
    }

    [Test] public void SuccessfulSavedRollCanDeliverKnowledgeWithoutRollingAgain()
    {
        Equip(observer);
        rules.SetInt(SecretPassageAttemptLedger.AttemptKey(definition.passageId, character.characterId), 2);
        Assert.That(discovery.ShouldRequest(actor, true), Is.True);
        var state = Random.state;
        Assert.That(passage.ServerTryPassage(actor, false), Is.True);
        Assert.That(Random.state, Is.EqualTo(state)); Assert.That(discovery.IsKnown, Is.True);
    }

    [Test] public void ReadingAndPickupLinksAreDistinctFromObserverKnowledge()
    {
        var note = Data<Item>(); definition.note = note;
        note.knowledgeUnlockedOnRead = new List<KnowledgeSO> { definition.knowledge };
        note.knowledgeUnlockedOnPickup = new List<KnowledgeSO>();
        Assert.That(definition.HasReadLinkedNote, Is.True);
        note.knowledgeUnlockedOnPickup.Add(definition.knowledge);
        Assert.That(definition.HasReadLinkedNote, Is.False);
        Assert.That(knowledge.UnlockedKnowledge, Is.Empty);
    }

    [Test] public void LegacyPassageWithoutTheNewAdapterKeepsItsExistingDiscoveryContract()
    {
        Object.DestroyImmediate(discovery);
        Equip(traversal); Assert.That(passage.ServerTryPassage(actor, true), Is.False);
        Equip(observer); Assert.That(passage.ServerTryPassage(actor, false), Is.True);
        Assert.That(knowledge.UnlockedKnowledge, Is.Empty);
        Equip(traversal); Assert.That(passage.ServerTryPassage(actor, true), Is.True);
    }

    [Test] public void KnownPassageOutlineIsLimitedToTheControlledCharactersProximity()
    {
        Equip(observer);
        rules.SetInt(SecretPassageAttemptLedger.AttemptKey(definition.passageId, character.characterId), 1);
        var visual = GameObject.CreatePrimitive(PrimitiveType.Cube); visual.transform.SetParent(passage.transform, false);
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        var outline = visual.AddComponent<RuntimeOutlineTarget>();
        Invoke(passage, "Update"); Assert.That(outline.IsOutlined(), Is.False);
        KnowledgeReveal.Reveal(definition.knowledge, actor, "test note reading");
        Assert.That(passage.ServerTryPassage(actor, false), Is.True);
        Invoke(passage, "Update"); Assert.That(outline.IsOutlined(), Is.True);
        actor.transform.position = Vector3.right * 100; Physics.SyncTransforms();
        Invoke(passage, "Update"); Assert.That(outline.IsOutlined(), Is.False);
    }

    [Test] public void AttemptAcknowledgementIsPerCharacterAndDoesNotPreventLaterNoteRecognition()
    {
        Equip(observer); discovery.AcknowledgeAttempt(character.characterId);
        Assert.That(discovery.ShouldRequest(actor, true), Is.False);
        character.characterId = "test.character.other";
        Assert.That(discovery.ShouldRequest(actor, true), Is.True);
        KnowledgeReveal.Reveal(definition.knowledge, actor, "test shared note");
        discovery.AcknowledgeAttempt(character.characterId);
        Assert.That(discovery.ShouldRequest(actor, false), Is.True);
    }
}
