using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class SecretPassageNotesTests
{
    private readonly List<Object> owned = new();
    private WorldRulesStateManager rules;
    private CycleProgressionService service;
    private SecretPassageCatalog catalog;
    private SecretPassageDefinition passage;
    private Item note;
    private SquadCharacterController actor;
    private InteractableItem worldNote;
    private GameObject Root(string name) { var go = new GameObject(name); owned.Add(go); return go; }
    private T Data<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); owned.Add(value); return value; }
    private static void Invoke(Object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

    [SetUp] public void Setup()
    {
        var world = Root("Owned note world"); world.SetActive(false);
        rules = world.AddComponent<WorldRulesStateManager>(); service = world.AddComponent<CycleProgressionService>();
        Invoke(service, "Awake");
        var manager = Root("Owned knowledge manager").AddComponent<KnowledgeManager>();
        var managerProps = new SerializedObject(manager); managerProps.FindProperty("dontDestroyOnLoad").boolValue = false;
        managerProps.FindProperty("playUnlockEffects").boolValue = false; managerProps.ApplyModifiedPropertiesWithoutUndo(); Invoke(manager, "Awake");
        actor = Root("Owned note reader").AddComponent<SquadCharacterController>(); actor.SetHealth(100, 100);
        passage = Data<SecretPassageDefinition>(); passage.passageId = "test.secret";
        passage.knowledge = Data<KnowledgeSO>(); passage.knowledge.knowledgeId = "test.secret.knowledge";
        note = Object.Instantiate(AssetDatabase.LoadAssetAtPath<Item>("Assets/Scenes/Reve/Cycles/Seuil/Data/Item_0.asset")); owned.Add(note);
        note.itemId = "test.secret.note"; note.knowledgeUnlockedOnPickup = new List<KnowledgeSO>(); note.knowledgeUnlockedOnRead = new List<KnowledgeSO> { passage.knowledge }; passage.note = note;
        catalog = Data<SecretPassageCatalog>(); catalog.notesCycle = Data<CycleDefinition>(); catalog.notesCycle.cycleId = "test.secret.notes.cycle";
        catalog.notesCycle.steps = new[] { new CycleStep { id = "note_1", kind = CycleStepKind.Read, sourceId = note.itemId, terminal = true },
            new CycleStep { id = "note_2", kind = CycleStepKind.Read, sourceId = "test.reserved", terminal = true } };
        catalog.entries = new[] { new SecretPassageCatalog.Entry { number = 1, noteId = note.itemId, definition = passage },
            new SecretPassageCatalog.Entry { number = 2, noteId = "test.reserved", reserved = true } };
        service.RegisterDefinition(catalog.notesCycle);
        worldNote = Root("Owned physical note").AddComponent<InteractableItem>(); worldNote.representedItem = note;
        worldNote.gameObject.AddComponent<BoxCollider>().isTrigger = true;
        var props = new SerializedObject(worldNote); props.FindProperty("requireLitInfluenceForInteraction").boolValue = false; props.ApplyModifiedPropertiesWithoutUndo();
        Physics.SyncTransforms();
    }
    [TearDown] public void Cleanup()
    {
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]); owned.Clear();
    }
    [Test] public void ObserverKnowledgeDoesNotCountButLaterReadingStillDoes()
    {
        KnowledgeReveal.Reveal(passage.knowledge, actor.gameObject, "secret_passage_observer");
        Assert.That(catalog.CountRead(service), Is.Zero);
        Assert.That(service.IsCompleted(catalog.notesCycle), Is.False);
        Assert.That(SecretPassageNotes.ServerRecordRead(note, actor.gameObject, catalog), Is.True);
        Assert.That(catalog.CountRead(service), Is.EqualTo(1));
    }
    [Test] public void RealReadFactIsIdempotentAndDoesNotCompleteReservedNotes()
    {
        Assert.That(SecretPassageNotes.ServerRecordRead(note, actor.gameObject, catalog), Is.True);
        Assert.That(SecretPassageNotes.ServerRecordRead(note, actor.gameObject, catalog), Is.True);
        Assert.That(catalog.CountRead(service), Is.EqualTo(1));
        Assert.That(service.IsCompleted(catalog.notesCycle), Is.False);
        Assert.That(service.HasFact(catalog.notesCycle, CycleStepKind.Read, "test.reserved"), Is.False);
    }
    [Test] public void ARemoteUnownedNoteCannotBeClaimed()
    {
        actor.transform.position = Vector3.one * 100; Physics.SyncTransforms();
        Assert.That(SecretPassageNotes.ServerRecordRead(note, actor.gameObject, catalog), Is.False);
        Assert.That(catalog.CountRead(service), Is.Zero);
    }
    [Test] public void DeadReaderCannotRecordARead()
    {
        actor.SetCurrentHp(0);
        Assert.That(SecretPassageNotes.ServerRecordRead(note, actor.gameObject, catalog), Is.False);
    }
    [Test] public void ReservedEntriesCannotPretendToBeAuthoredNotes()
    {
        catalog.entries[0].reserved = true;
        Assert.That(SecretPassageNotes.ServerRecordRead(note, actor.gameObject, catalog), Is.False);
    }
    [Test] public void WorldRestoreRetainsReadCountWithoutDerivingItFromKnowledge()
    {
        Assert.That(SecretPassageNotes.ServerRecordRead(note, actor.gameObject, catalog), Is.True);
        var saved = rules.CaptureVariables(); rules.ResetRuntimeState(); rules.ApplyVariables(saved); service.Refresh();
        Assert.That(catalog.CountRead(service), Is.EqualTo(1));
    }
    [Test] public void LegacyDetectedStateGrantsOnlyPassageKnowledge()
    {
        var go = Root("Owned legacy passage"); go.AddComponent<BoxCollider>().isTrigger = true;
        var narrow = go.AddComponent<TrouEtroit>(); var discovery = go.AddComponent<KnowledgePassageDiscovery>(); discovery.definition = passage;
        narrow.RestoreDetectedState(true);
        Assert.That(discovery.TryImportLegacyDetection(), Is.True);
        Assert.That(KnowledgeManager.Instance.HasKnowledge(passage.knowledge), Is.True);
        Assert.That(catalog.CountRead(service), Is.Zero);
        Assert.That(SecretPassageAttemptLedger.HasAttempted(rules, passage.passageId, "any"), Is.False);
    }
}
