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

    [Test]
    public void UccIgnoresActorSnapsWhileTheStoryOwnsTheCamera()
    {
        var camera = new GameObject("Suspended cinematic camera");
        camera.SetActive(false);
        try
        {
            var ucc = camera.AddComponent<Opsive.UltimateCharacterController.Camera.CameraController>();
            ucc.enabled = false;
            var callback = typeof(Opsive.UltimateCharacterController.Camera.CameraController)
                .GetMethod("OnImmediateTransformChange", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(callback, Is.Not.Null);
            Assert.DoesNotThrow(() => callback.Invoke(ucc, new object[] { true }));
        }
        finally { UnityEngine.Object.DestroyImmediate(camera); }
    }

    [Test]
    public void MaisonOpeningDialogueKeepsTheConversationViewInsteadOfCuttingToLucian()
    {
        var intro = AssetDatabase.LoadAssetAtPath<StorySequenceAsset>(
            "Assets/Narrative/StorySequence/Sequences/1_Munin_Meeting.asset");
        Assert.That(intro, Is.Not.Null);
        Assert.That(intro.startFromBlack, Is.True);
        Assert.That(intro.steps[0].type, Is.EqualTo(StorySequenceStepType.Dialogue));
        Assert.That(intro.steps[0].focusCameraOnSpeaker, Is.False);
        var root = new GameObject("Passive Maison intro");
        root.SetActive(false);
        try
        {
            var driver = root.AddComponent<StorySequenceCameraDriver>();
            var runner = root.AddComponent<StorySequenceRunner>();
            const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic;
            typeof(StorySequenceRunner).GetField("cameraDriver", fields).SetValue(runner, driver);
            var prepare = typeof(StorySequenceRunner).GetMethod("PrepareOpeningShot", fields);
            Assert.That(prepare.Invoke(runner, new object[] { intro }), Is.EqualTo(-1));
            Assert.That(driver.IsCinematicActive, Is.False);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void ConversationViewStagesMuninAheadAtSeatedEyeHeight()
    {
        var viewObject = new GameObject("Conversation view");
        var playerObject = new GameObject("Seated player");
        try
        {
            var view = viewObject.AddComponent<StorySequenceConversationView>();
            var player = playerObject.AddComponent<StorySequenceActor>();
            var eyes = new GameObject("Seated eye anchor").transform;
            eyes.SetParent(playerObject.transform, false);
            eyes.localPosition = new Vector3(0f, 1.1f, 0f);
            playerObject.transform.SetPositionAndRotation(new Vector3(2f, 3f, 4f), Quaternion.Euler(0f, 90f, 0f));
            var actorSettings = new SerializedObject(player);
            actorSettings.FindProperty("cameraAnchor").objectReferenceValue = eyes;
            actorSettings.ApplyModifiedPropertiesWithoutUndo();
            var resolve = typeof(StorySequenceConversationView).GetMethod("ResolveStaging",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            object[] pose = { player, Vector3.zero, Vector3.zero, Quaternion.identity };
            resolve.Invoke(view, pose);
            var position = (Vector3)pose[1];
            var target = (Vector3)pose[2];
            var rotation = (Quaternion)pose[3];
            Assert.That(position.y, Is.EqualTo(eyes.position.y + .04f).Within(.001f));
            Assert.That(target.x - position.x, Is.EqualTo(1.41f).Within(.001f));
            Assert.That(Vector3.Dot(rotation * Vector3.forward, (target - position).normalized), Is.GreaterThan(.9999f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(viewObject);
            UnityEngine.Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void DialogueNodDipsOnceWithoutDisplacingTheCameraAndClearsOnReturn()
    {
        var rig = new GameObject("Nod test rig");
        var marker = new GameObject("Conversation mark");
        try
        {
            var camera = rig.AddComponent<Camera>();
            var driver = rig.AddComponent<StorySequenceCameraDriver>();
            var driverSettings = new SerializedObject(driver);
            driverSettings.FindProperty("controlledCamera").objectReferenceValue = camera;
            driverSettings.ApplyModifiedPropertiesWithoutUndo();
            var point = marker.AddComponent<StorySequenceCameraPoint>();
            var tick = typeof(StorySequenceCameraDriver).GetMethod("UpdateCamera",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            Assert.That(driver.PlayNod(4f, .56f), Is.False, "Do not touch the gameplay camera.");
            driver.SetCameraPoint(point, 0f);
            Assert.That(driver.PlayNod(4f, .56f), Is.True);
            tick.Invoke(driver, new object[] { .56f * .38f });
            Assert.That(camera.transform.forward.y, Is.EqualTo(-Mathf.Sin(4f * Mathf.Deg2Rad)).Within(.001f));
            Assert.That(camera.transform.position, Is.EqualTo(marker.transform.position));
            tick.Invoke(driver, new object[] { .56f });
            Assert.That(driver.IsNodding, Is.False);
            Assert.That(Quaternion.Angle(camera.transform.rotation, marker.transform.rotation), Is.LessThan(.05f));

            Assert.That(driver.PlayNod(4f, .56f), Is.True);
            tick.Invoke(driver, new object[] { .2f });
            driver.EndCinematic();
            Assert.That(driver.IsNodding, Is.False);
            Assert.That(Quaternion.Angle(camera.transform.rotation, marker.transform.rotation), Is.LessThan(.05f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rig);
            UnityEngine.Object.DestroyImmediate(marker);
        }
    }

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

    [Test] public void PlayerDialoguePortraitFollowsLocalCharacterEvenWithoutASequenceActor()
    {
        const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.NonPublic;
        var rootField = typeof(LocalPlayerContext).GetField("localCharacterRoot", fields | System.Reflection.BindingFlags.Static);
        var previous = rootField.GetValue(null);
        var owner = new GameObject("Dialogue portrait test");
        owner.SetActive(false);
        var first = ScriptableObject.CreateInstance<CharacterData>();
        var second = ScriptableObject.CreateInstance<CharacterData>();
        try
        {
            var bindings = owner.AddComponent<StorySequenceSceneBindings>();
            var step = new StorySequenceStep { actorId = "player", speakerNameOverride = "Lucian" };
            var dataField = typeof(SquadCharacterController).GetField("characterData", fields | System.Reflection.BindingFlags.Instance);
            foreach (var data in new[] { first, second })
            {
                var avatar = new GameObject("Controlled avatar");
                avatar.transform.SetParent(owner.transform);
                dataField.SetValue(avatar.AddComponent<SquadCharacterController>(), data);
                rootField.SetValue(null, avatar.transform);
                Assert.That(bindings.ResolveDialogueSpeaker(step, null), Is.SameAs(data));
            }
        }
        finally
        {
            rootField.SetValue(null, previous);
            UnityEngine.Object.DestroyImmediate(owner);
            UnityEngine.Object.DestroyImmediate(first);
            UnityEngine.Object.DestroyImmediate(second);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DialoguePortraitUsesTheExistingImageAndClearsForAnUnknownSpeaker(bool useRawImage)
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic;
        var root = new GameObject("DialoguePanel", typeof(RectTransform), typeof(CanvasGroup));
        root.SetActive(false);
        var texture = new Texture2D(4, 4);
        var portrait = Sprite.Create(texture, new Rect(1, 1, 2, 2), Vector2.one * .5f);
        try
        {
            var ui = root.AddComponent<DialoguePanelUI>();
            ui.dialoguePanelRoot = root;
            var text = new GameObject("DialogueBox_Text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            text.transform.SetParent(root.transform, false);
            ui.dialogueText = text.GetComponent<TMPro.TMP_Text>();
            var image = new GameObject("DialoguePortrait_Image", typeof(RectTransform),
                useRawImage ? typeof(UnityEngine.UI.RawImage) : typeof(UnityEngine.UI.Image));
            image.transform.SetParent(root.transform, false);
            typeof(DialoguePanelUI).GetMethod("ResolveReferences", flags).Invoke(ui, null);
            var setPortrait = typeof(DialoguePanelUI).GetMethod("SetPortrait", flags);
            setPortrait.Invoke(ui, new object[] { portrait });
            var graphic = image.GetComponent<UnityEngine.UI.Graphic>();
            Assert.That(graphic.enabled, Is.True);
            if (graphic is UnityEngine.UI.RawImage raw)
            {
                Assert.That(raw.texture, Is.SameAs(texture));
                Assert.That(raw.uvRect, Is.EqualTo(new Rect(.25f, .25f, .5f, .5f)));
            }
            else Assert.That(ui.portraitImage.sprite, Is.SameAs(portrait));
            setPortrait.Invoke(ui, new object[] { null });
            Assert.That(graphic.enabled, Is.False);
            if (graphic is UnityEngine.UI.RawImage cleared) Assert.That(cleared.texture, Is.Null);
            else Assert.That(ui.portraitImage.sprite, Is.Null);
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
        Assert.That(runner.Sequence.startFromBlack, Is.True);
        Assert.That(runner.Sequence.playOnce, Is.True);
        Assert.That(runner.Sequence.TryValidate(out string issue), Is.True, issue);
        Assert.That(runner.Sequence.steps.Count, Is.EqualTo(4));
        Assert.That(runner.Sequence.steps[0].type, Is.EqualTo(StorySequenceStepType.Dialogue));
        Assert.That(runner.Sequence.steps[1].type, Is.EqualTo(StorySequenceStepType.ChooseMuninName));
        Assert.That(runner.Sequence.steps[1].skippable, Is.False);
        Assert.That(runner.Sequence.steps[2].type, Is.EqualTo(StorySequenceStepType.Dialogue));
        StringAssert.Contains("[Munin]", runner.Sequence.steps[2].voiceLine.voiceLineText);
        Assert.That(runner.Sequence.steps[3].type, Is.EqualTo(StorySequenceStepType.Dialogue));
        Assert.That(runner.Sequence.steps[3].dialogueNodDegrees, Is.EqualTo(4f));
        Assert.That(runner.Sequence.steps[3].dialogueNodDuration, Is.EqualTo(.56f));
        for (int i = 0; i < 3; i++)
            Assert.That(runner.Sequence.steps[i].dialogueNodDegrees, Is.Zero);
        var view = prefab.GetComponent<StorySequenceConversationView>();
        Assert.That(view, Is.Not.Null);
        Assert.That(new SerializedObject(runner).FindProperty("conversationView").objectReferenceValue, Is.SameAs(view));
        var viewSettings = new SerializedObject(view);
        var mark = viewSettings.FindProperty("cameraPoint").objectReferenceValue as StorySequenceCameraPoint;
        Assert.That(mark, Is.Not.Null);
        Assert.That(mark.transform.IsChildOf(prefab.transform), Is.True);
        var orb = viewSettings.FindProperty("interlocutorVisual").objectReferenceValue as GameObject;
        Assert.That(orb, Is.Not.Null);
        Assert.That(orb.GetComponentInChildren<ParticleSystem>(true), Is.Not.Null);
        Assert.That(orb.GetComponentInChildren<MuninController>(true), Is.Null);
        var lucian = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Characters/1_Squad/Lucian/Lucian.asset");
        Assert.That(lucian.portrait, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(lucian.portrait),
            Is.EqualTo("Assets/Characters/1_Squad/Lucian/Lucian_Portrait.png"));
        foreach (var step in runner.Sequence.steps)
        {
            if (step.type != StorySequenceStepType.Dialogue) continue;
            Assert.That(step.actorId, Is.EqualTo("player"));
            Assert.That(step.speakerCharacterData, Is.Null, "The portrait must follow the controlled character.");
        }
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

        const string sequencesPath = "Assets/Scenes/Maison/Maison_Sequences.unity";
        var manifest = AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/Maison/ZoneManifest_Maison.asset");
        CollectionAssert.Contains(manifest.LoadingSceneNames, "Maison_Sequences");
        Assert.That(System.Array.Exists(EditorBuildSettings.scenes, s => s.enabled && s.path == sequencesPath), Is.True);
        var maison = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(sequencesPath);
        try
        {
            var resolver = typeof(GameFlowService).GetMethod("FindSceneOpeningSequence",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var sceneRunner = (StorySequenceRunner)resolver.Invoke(null, new object[] { maison, runner.Sequence });
            Assert.That(sceneRunner, Is.Not.Null);
            var settings = new SerializedObject(sceneRunner);
            Assert.That(settings.FindProperty("startWithLocalPlayerSeated").boolValue, Is.True);
            Assert.That(settings.FindProperty("conversationView").objectReferenceValue,
                Is.SameAs(sceneRunner.GetComponent<StorySequenceConversationView>()));
            Assert.That(settings.FindProperty("playOnStart").boolValue, Is.False);
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(maison); }
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
            groupNames = new[] { "^StorySequenceAuthoringTests", "^BootstrapVideoPanelTests", "^MainMenuLoadBrowserTests" } }) { runSynchronously = true });
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
