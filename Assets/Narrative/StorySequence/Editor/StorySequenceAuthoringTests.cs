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

    [Test] public void SequenceActorResolvesCharacterDataFromItsCharacterInsteadOfItsDisplayName()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic;
        var root = new GameObject("Speaker data test");
        root.SetActive(false);
        var data = ScriptableObject.CreateInstance<CharacterData>();
        var overrideData = ScriptableObject.CreateInstance<CharacterData>();
        try
        {
            var controller = root.AddComponent<SquadCharacterController>();
            typeof(SquadCharacterController).GetField("characterData", flags).SetValue(controller, data);
            var child = new GameObject("Speaker anchor");
            child.transform.SetParent(root.transform);
            var actor = child.AddComponent<StorySequenceActor>();
            actor.ConfigureRuntime("player", "A display name unrelated to the data");
            Assert.That(actor.CharacterData, Is.SameAs(data));
            typeof(StorySequenceActor).GetField("characterData", flags).SetValue(actor, overrideData);
            Assert.That(actor.CharacterData, Is.SameAs(overrideData));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(data);
            UnityEngine.Object.DestroyImmediate(overrideData);
        }
    }

    [Test] public void DialoguePortraitUsesTheExistingImageAndClearsForAnUnknownSpeaker()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic;
        var root = new GameObject("DialoguePanel", typeof(RectTransform), typeof(CanvasGroup));
        root.SetActive(false);
        var texture = new Texture2D(2, 2);
        var portrait = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f);
        try
        {
            var ui = root.AddComponent<DialoguePanelUI>();
            ui.dialoguePanelRoot = root;
            var text = new GameObject("DialogueBox_Text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            text.transform.SetParent(root.transform, false);
            ui.dialogueText = text.GetComponent<TMPro.TMP_Text>();
            var image = new GameObject("DialoguePortrait_Image", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            image.transform.SetParent(root.transform, false);
            typeof(DialoguePanelUI).GetMethod("ResolveReferences", flags).Invoke(ui, null);
            Assert.That(ui.portraitImage, Is.SameAs(image.GetComponent<UnityEngine.UI.Image>()));
            var setPortrait = typeof(DialoguePanelUI).GetMethod("SetPortrait", flags);
            setPortrait.Invoke(ui, new object[] { portrait });
            Assert.That(ui.portraitImage.sprite, Is.SameAs(portrait));
            Assert.That(ui.portraitImage.enabled, Is.True);
            setPortrait.Invoke(ui, new object[] { null });
            Assert.That(ui.portraitImage.sprite, Is.Null);
            Assert.That(ui.portraitImage.enabled, Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(portrait);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    [Test] public void NamePromptUsesExistingMenuCursorKeysWithoutButtonsAndRestoresTheirAppearance()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance;
        var owner = new GameObject("Name prompt regression test");
        var keyboard = new GameObject("VirtualKeyboard", typeof(RectTransform), typeof(CanvasGroup));
        var buttons = new GameObject("VirtualKeyboardButtons", typeof(RectTransform));
        buttons.transform.SetParent(keyboard.transform, false);
        var key = new GameObject("A", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(MenuCursorAction));
        key.transform.SetParent(buttons.transform, false);
        var action = key.GetComponent<MenuCursorAction>();
        action.Configure(null, MenuCursorAction.MenuAction.Vk_Input);
        var image = key.GetComponent<UnityEngine.UI.Image>();
        Color original = new Color(.2f, .4f, .6f, .8f);
        image.color = original;
        var prompt = owner.AddComponent<StorySequenceNamePrompt>();
        Type type = typeof(StorySequenceNamePrompt);
        var group = keyboard.GetComponent<CanvasGroup>();
        try
        {
            type.GetField("bootstrapKeyboardGroup", flags).SetValue(prompt, group);
            type.GetField("bootstrapKeyboardButtons", flags).SetValue(prompt, buttons.transform);
            type.GetMethod("BuildView", flags).Invoke(prompt, null);
            Assert.That(keyboard.GetComponentsInChildren<UnityEngine.UI.Button>(true), Is.Empty);
            type.GetMethod("SetBootstrapKeyboardVisible", flags).Invoke(prompt, new object[] { true });
            Assert.That(group.alpha, Is.EqualTo(1f));
            Assert.That(group.interactable && group.blocksRaycasts, Is.True);
            type.GetField("opened", flags).SetValue(prompt, true);
            type.GetField("openedFrame", flags).SetValue(prompt, -1);
            action.OnCursorSubmit();
            Assert.That(type.GetField("draft", flags).GetValue(prompt), Is.EqualTo("A"));
            Assert.That(image.color, Is.Not.EqualTo(original));
            type.GetField("opened", flags).SetValue(prompt, false);
            prompt.Cancel();
            Assert.That(group.alpha, Is.Zero);
            Assert.That(group.interactable || group.blocksRaycasts, Is.False);
            Assert.That(image.color, Is.EqualTo(original));
            Assert.That(action.enabled, Is.True);
            Assert.That(keyboard.activeSelf, Is.True, "Closing uses alpha, not SetActive.");
        }
        finally
        {
            type.GetField("opened", flags).SetValue(prompt, false);
            UnityEngine.Object.DestroyImmediate(owner);
            UnityEngine.Object.DestroyImmediate(keyboard);
        }
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
