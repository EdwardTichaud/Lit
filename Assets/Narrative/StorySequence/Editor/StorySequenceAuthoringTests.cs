using System;
using System.IO;
using Lit.Story;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public sealed class StorySequenceAuthoringTests
{
    private StorySequenceAsset sequence;
    [SetUp] public void SetUp() => sequence = ScriptableObject.CreateInstance<StorySequenceAsset>();
    [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(sequence);

    [Test] public void EmptyDraftCannotBePlayedOrSilentlyCompleted()
    {
        Assert.That(sequence.TryValidate(out string issue), Is.False);
        StringAssert.Contains("vide", issue);
        Assert.That(StorySequenceCompletionStore.IsCompleted(sequence), Is.False);
    }

    [Test] public void NullAndUnknownStepsAreRejected()
    {
        sequence.steps.Add(null);
        Assert.That(sequence.TryValidate(out _), Is.False);
        sequence.steps[0] = new StorySequenceStep { type = (StorySequenceStepType)999 };
        Assert.That(sequence.TryValidate(out _), Is.False);
    }

    [Test] public void DialogueMustHaveContentAndAnExit()
    {
        var step = new StorySequenceStep { type = StorySequenceStepType.Dialogue, skippable = false };
        sequence.steps.Add(step);
        Assert.That(sequence.TryValidate(out _), Is.False);
        var line = ScriptableObject.CreateInstance<VoiceLineData>();
        try
        {
            step.voiceLine = line;
            Assert.That(sequence.TryValidate(out string issue), Is.False);
            StringAssert.Contains("sans fin", issue);
            step.skippable = true;
            Assert.That(sequence.TryValidate(out _), Is.True);
            step.skippable = false;
            step.dialogueMaxDisplayDuration = 3f;
            Assert.That(sequence.TryValidate(out _), Is.True);
        }
        finally { UnityEngine.Object.DestroyImmediate(line); }
    }

    [TestCase(StorySequenceStepType.AnimatorTrigger)]
    [TestCase(StorySequenceStepType.SceneEvent)]
    [TestCase(StorySequenceStepType.CameraShot)]
    [TestCase(StorySequenceStepType.Timeline)]
    [TestCase(StorySequenceStepType.Sitting)]
    public void MissingRequiredStepReferencesAreRejected(StorySequenceStepType type)
    {
        sequence.steps.Add(new StorySequenceStep { type = type });
        Assert.That(sequence.TryValidate(out _), Is.False);
    }

    [Test] public void SimpleWaitIsValidAndSerializedNewStepsHaveSafeDefaults()
    {
        var serialized = new SerializedObject(sequence);
        var steps = serialized.FindProperty("steps");
        steps.arraySize = 1;
        steps.GetArrayElementAtIndex(0).boxedValue = new StorySequenceStep { type = StorySequenceStepType.Wait };
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(sequence.steps[0].duration, Is.EqualTo(1f));
        Assert.That(sequence.steps[0].skippable, Is.True);
        Assert.That(sequence.TryValidate(out _), Is.True);
    }

    [TestCase(null, "Munin")]
    [TestCase("", "Munin")]
    [TestCase("   ", "Munin")]
    [TestCase("  Eowyn  ", "Eowyn")]
    [TestCase("Anne-Marie", "Anne-Marie")]
    public void ChosenNameIsNormalizedWithAnEmptyFallback(string input, string expected)
    {
        Assert.That(StorySequenceNames.NormalizeName(input), Is.EqualTo(expected));
    }

    [Test] public void ChosenNameIsPlainTextAndBounded()
    {
        string value = StorySequenceNames.NormalizeName("<[]X>" + new string('a', 60));
        Assert.That(value.Length, Is.EqualTo(StorySequenceNames.MaximumNameLength));
        Assert.That(value.IndexOfAny(new[] { '<', '>', '[', ']' }), Is.EqualTo(-1));
    }

    [Test] public void NameUsesTheRealWorldSnapshotSerializerAndResetsBetweenGames()
    {
        var owner = new GameObject("Name state test");
        owner.SetActive(false);
        var rules = owner.AddComponent<WorldRulesStateManager>();
        try
        {
            StorySequenceNames.SetMuninName(rules, "Ada");
            var serializer = new SnapshotSerializer();
            byte[] bytes = serializer.Serialize(new WorldSnapshot { WorldVariables = rules.CaptureVariables() });
            rules.ResetRuntimeState("test");
            Assert.That(StorySequenceNames.GetMuninName(rules), Is.EqualTo("Munin"));
            rules.ApplyVariables(serializer.Deserialize(bytes).WorldVariables);
            Assert.That(StorySequenceNames.GetMuninName(rules), Is.EqualTo("Ada"));
            Assert.That(StorySequenceNames.Resolve("[Munin] ? Bonjour [Munin].", rules), Is.EqualTo("Ada ? Bonjour Ada."));
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
    }

    [Test] public void DialogueTokenResolutionDoesNotMutateTheAuthoredVoiceLine()
    {
        var line = AssetDatabase.LoadAssetAtPath<VoiceLineData>(
            "Assets/Narrative/StorySequence/VoiceLines/1_Munin_Meeting/Munin_Meeting_02_Lucian.asset");
        string original = line.voiceLineText;
        StringAssert.StartsWith("Munin ?", StorySequenceNames.Resolve(original, null));
        Assert.That(line.voiceLineText, Is.EqualTo(original));
    }

    [Test] public void NamePromptDoesNotEmbedOrInstantiateItsOwnKeyboard()
    {
        var keyboardField = typeof(StorySequenceNamePrompt).GetField("virtualKeyboardPrefab",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.That(keyboardField, Is.Null);
    }

    [Test] public void OpeningMeetingIsReferencedByMenuAndDoesNotAutoplayDuringInstantiation()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Narrative/StorySequence/Prefabs/1_Munin_Meeting.prefab");
        Assert.That(prefab, Is.Not.Null);
        var runner = prefab.GetComponent<StorySequenceRunner>();
        Assert.That(runner.Sequence.name, Is.EqualTo("1_Munin_Meeting"));
        Assert.That(runner.Sequence.playOnce, Is.True);
        Assert.That(runner.Sequence.TryValidate(out string issue), Is.True, issue);
        Assert.That(runner.Sequence.steps.Count, Is.EqualTo(4));
        Assert.That(runner.Sequence.steps[0].type, Is.EqualTo(StorySequenceStepType.Dialogue));
        Assert.That(runner.Sequence.steps[1].type, Is.EqualTo(StorySequenceStepType.ChooseMuninName));
        Assert.That(runner.Sequence.steps[1].skippable, Is.False);
        Assert.That(runner.Sequence.steps[2].type, Is.EqualTo(StorySequenceStepType.Dialogue));
        StringAssert.Contains("[Munin]", runner.Sequence.steps[2].voiceLine.voiceLineText);
        Assert.That(runner.Sequence.steps[3].type, Is.EqualTo(StorySequenceStepType.Dialogue));
        // The keyboard is a persistent Bootstrap panel, deliberately absent
        // from this isolated prefab authoring fixture.
        Assert.That(runner.GetComponent<StorySequenceNamePrompt>(), Is.Not.Null);
        Assert.That(new SerializedObject(runner).FindProperty("playOnStart").boolValue, Is.False);
        foreach (Type required in new[] { typeof(StorySequenceSceneBindings), typeof(StorySequenceCameraDriver),
                     typeof(StorySequenceDialoguePresenter), typeof(StorySequenceFadeController) })
            Assert.That(prefab.GetComponent(required), Is.Not.Null);
        string menu = File.ReadAllText("Assets/Scenes/MainMenu/MainMenu.unity");
        StringAssert.Contains("newGameOpeningSequencePrefab: {fileID: 2171863246060046666, guid: " +
            AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)), menu);
    }
}

// Opt-in, read-only authoring checks; no Play Mode, scenes or user saves are modified.
[InitializeOnLoad]
public sealed class StorySequenceAuthoringTestRunner : ICallbacks
{
    private const string Request = "Library/IntroValidation/StorySequenceTests.request";
    private static TestRunnerApi api;
    static StorySequenceAuthoringTestRunner() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || api != null || !File.Exists(Request)) return;
        File.Delete(Request);
        api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new StorySequenceAuthoringTestRunner());
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
            groupNames = new[] { "^StorySequenceAuthoringTests", "^BootstrapVideoPanelTests" } }) { runSynchronously = true });
    }
    public void RunStarted(ITestAdaptor test) { }
    public void TestStarted(ITestAdaptor test) { }
    public void TestFinished(ITestResultAdaptor result) { }
    public void RunFinished(ITestResultAdaptor result)
    {
        TestRunnerApi.SaveResultToFile(result, "Library/IntroValidation/StorySequenceTests.xml");
        api.UnregisterCallbacks(this);
        UnityEngine.Object.DestroyImmediate(api);
        api = null;
    }
}
