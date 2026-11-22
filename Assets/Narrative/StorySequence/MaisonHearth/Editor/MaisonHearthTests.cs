using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Lit.Story;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class MaisonHearthTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private GameObject root;
    private MaisonHearthVigil vigil;
    private Func<StorySequenceAsset, bool> oldCompletion;
    private bool reunionComplete, vigilComplete;
    [SetUp] public void Setup()
    {
        reunionComplete = vigilComplete = false;
        root = new GameObject("Isolated hearth story fixture"); root.SetActive(false);
        vigil = root.AddComponent<MaisonHearthVigil>();
        Set("rules", root.AddComponent<WorldRulesStateManager>());
        vigil.runner = root.AddComponent<StorySequenceRunner>();
        typeof(StorySequenceRunner).GetField("sequence", Private).SetValue(vigil.runner,
            AssetDatabase.LoadAssetAtPath<StorySequenceAsset>(MaisonHearthAuthoring.SequencePath));
        vigil.prerequisite = AssetDatabase.LoadAssetAtPath<StorySequenceAsset>(MaisonCompanionReunionAuthoring.SequencePath);
        oldCompletion = StorySequenceCompletionStore.IsCompletedOverride;
        StorySequenceCompletionStore.IsCompletedOverride = sequence => sequence == vigil.prerequisite ? reunionComplete : vigilComplete;
    }
    [TearDown] public void Cleanup()
    {
        StorySequenceCompletionStore.IsCompletedOverride = oldCompletion;
        Object.DestroyImmediate(root);
    }
    private void Set(string field, object value) => typeof(MaisonHearthVigil).GetField(field, Private).SetValue(vigil, value);

    [Test] public void OnlySuccessfulGroupReunionUnlocksTheVigilAndCompletionPreventsReplay()
    {
        Assert.That(vigil.IsUnlocked, Is.False);
        reunionComplete = true;
        Assert.That(vigil.IsUnlocked, Is.True);
        vigilComplete = true;
        Assert.That(vigil.IsUnlocked, Is.False);
        Assert.That(vigil.IsCompleted, Is.True);
    }
    [Test] public void SharedCompletionPreventsReplayWithoutLocalStoryMetadata()
    {
        reunionComplete = true;
        root.GetComponent<WorldRulesStateManager>().SetBool(MaisonHearthVigil.PortalOpenKey, true);
        Assert.That(vigil.IsCompleted, Is.True);
        Assert.That(vigil.IsUnlocked, Is.False);
    }
    [Test] public void SuccessfulCompletionPublishesTheSharedGateAndAbortDoesNotUndoIt()
    {
        var portal = new GameObject("Completed portal fixture"); portal.transform.SetParent(root.transform);
        Set("portal", portal); Set("ownsPortalVisibility", true); Set("portalRevealed", true);
        vigil.CompleteVigil(); vigil.AbortVigil();
        Assert.That(root.GetComponent<WorldRulesStateManager>().TryGetBool(MaisonHearthVigil.PortalOpenKey, out bool open) && open, Is.True);
        Assert.That(portal.activeSelf, Is.True);
    }
    [Test] public void SharedGateIsIncludedInTheExistingAuthoritativeNarrativeSnapshot()
    {
        var rules = root.GetComponent<WorldRulesStateManager>();
        rules.SetBool(MaisonHearthVigil.PortalOpenKey, true);
        var service = root.AddComponent<CycleProgressionService>();
        typeof(CycleProgressionService).GetField("rules", Private).SetValue(service, rules);
        var packet = typeof(CycleProgressionService).GetMethod("Capture", Private).Invoke(service, new object[] { null, null });
        var values = (System.Collections.Generic.List<WorldVariableSnapshot>)packet.GetType().GetField("values").GetValue(packet);
        Assert.That(values.Any(value => value.Key == MaisonHearthVigil.PortalOpenKey && value.BoolValue), Is.True);
    }
    [Test] public void OtherFoyersDoNotConsumeTheirUsualSittingInteraction()
    {
        var other = root.AddComponent<Foyer>();
        Assert.That(vigil.TryHandleFoyer(other, null, out bool accepted), Is.False);
        Assert.That(accepted, Is.False);
    }
    [Test] public void StorySeatingRefusesAnUnavailableBodyWithoutAffectingItsSittingState()
    {
        var character = root.AddComponent<SquadCharacterController>();
        Assert.That(character.TryBeginStorySitting(), Is.False);
        Assert.That(character.IsSittingRequested, Is.False);
    }
    [Test] public void MissingSpawnAnchorIsAConfigurationErrorButAbsentCompanionsKeepNormalSittingAvailable()
    {
        Set("group", root.AddComponent<MaisonCompanionReunion>());
        Set("portal", root);
        vigil.bindings = root.AddComponent<StorySequenceSceneBindings>();
        vigil.seatedController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(MaisonHearthAuthoring.Folder + "/Hearth_Seated.controller");
        vigil.companionSeats = new[] { "Vesta", "Soren", "Aurell" }.Select(id =>
            new MaisonHearthVigil.Seat { actorId = id, point = root.transform }).ToArray();
        var bind = typeof(MaisonHearthVigil).GetMethod("BindGroup", Private, null, new[] { typeof(string).MakeByRefType() }, null);
        object[] result = { null };
        Assert.That(bind.Invoke(vigil, result), Is.False);
        Assert.That((string)result[0], Does.Contain(MaisonHearthVigil.PortalSpawnPointId));
        typeof(StorySequenceSceneBindings).GetField("spawnPoints", Private).SetValue(vigil.bindings,
            new System.Collections.Generic.List<StorySequenceSpawnBinding> { new StorySequenceSpawnBinding {
                spawnPointId = MaisonHearthVigil.PortalSpawnPointId, point = root.transform } });
        result[0] = null;
        Assert.That(bind.Invoke(vigil, result), Is.False);
        Assert.That(result[0], Is.Null);
    }
    [Test] public void InterruptionRestoresPortalVisibilityAndOriginalCompanionPose()
    {
        var portal = new GameObject("Portal visibility fixture"); portal.transform.SetParent(root.transform); portal.SetActive(false);
        Set("portal", portal); Set("portalWasActive", false); Set("ownsPortalVisibility", true); Set("staged", true);
        var actorRoot = new GameObject("Narrative actor fixture"); actorRoot.transform.SetParent(root.transform);
        actorRoot.AddComponent<Animator>(); var actor = actorRoot.AddComponent<StorySequenceActor>();
        var poseType = typeof(MaisonHearthVigil).GetNestedType("ActorPose", BindingFlags.NonPublic);
        var pose = Activator.CreateInstance(poseType);
        poseType.GetField("position").SetValue(pose, new Vector3(2, 3, 4));
        poseType.GetField("rotation").SetValue(pose, Quaternion.Euler(0, 70, 0));
        ((IDictionary)typeof(MaisonHearthVigil).GetField("poses", Private).GetValue(vigil)).Add(actor, pose);
        portal.SetActive(true);
        vigil.AbortVigil();
        Assert.That(portal.activeSelf, Is.False);
        Assert.That(actor.transform.position, Is.EqualTo(new Vector3(2, 3, 4)));
        Assert.That(Quaternion.Angle(actor.transform.rotation, Quaternion.Euler(0, 70, 0)), Is.LessThan(.01));
        Assert.That(vigil.IsCompleted, Is.False);
    }
    [Test] public void AuthoredChoreographySeatsBeforeBlackAndRevealsAfterTheWaveringLight()
    {
        var sequence = vigil.runner.Sequence;
        Assert.That(sequence, Is.Not.Null);
        Assert.That(sequence.playOnce, Is.True);
        Assert.That(sequence.TryValidate(out string issue), Is.True, issue);
        int seat = sequence.steps.FindIndex(step => step.eventId == "hearth.seat_lucian");
        int black = sequence.steps.FindIndex(step => step.type == StorySequenceStepType.Fade && step.fadeAlpha == 1);
        int gather = sequence.steps.FindIndex(step => step.eventId == "hearth.gather");
        int reopen = sequence.steps.FindIndex(step => step.type == StorySequenceStepType.Fade && step.fadeAlpha == 0);
        int light = sequence.steps.FindIndex(step => step.type == StorySequenceStepType.Dialogue && step.voiceLine.voiceLineText == "Je sens que ma lumi\u00e8re vacille.");
        int portal = sequence.steps.FindIndex(step => step.type == StorySequenceStepType.InstantiateGameObject && step.spawnId == MaisonHearthVigil.PortalSpawnId);
        Assert.That(seat, Is.GreaterThanOrEqualTo(0)); Assert.That(black, Is.GreaterThan(seat));
        Assert.That(gather, Is.GreaterThan(black)); Assert.That(reopen, Is.GreaterThan(gather));
        Assert.That(light, Is.GreaterThan(reopen)); Assert.That(portal, Is.GreaterThan(light));
        var lines = sequence.steps.Where(step => step.type == StorySequenceStepType.Dialogue).ToArray();
        Assert.That(lines.Length, Is.EqualTo(27));
        Assert.That(lines.Last().voiceLine.voiceLineText, Is.EqualTo("C'est l'heure."));
        Assert.That(lines.Last().actorId, Is.EqualTo("Soren"));
        Assert.That(lines.All(line => line.skippable && line.voiceLine.voiceLineAudioClip == null), Is.True);
        Assert.That(lines.Select(line => line.actorId).Distinct().Count(), Is.EqualTo(4));
        Assert.That(lines.Count(line => line.focusCameraOnSpeaker), Is.EqualTo(1));
        Assert.That(lines[25].focusCameraOnSpeaker, Is.True);
        Assert.That(sequence.steps.Count(step => step.type == StorySequenceStepType.CameraShot), Is.EqualTo(3));
        Assert.That(sequence.startFromBlack, Is.False);
    }

    [Test] public void GroupOrbitIsSlowContinuousAndKeepsTheAuthoredHeight()
    {
        var point = root.AddComponent<StorySequenceCameraPoint>();
        var target = new GameObject("Orbit focus"); target.transform.SetParent(root.transform, false);
        point.transform.position = new Vector3(1.8f, 3.6f, .65f);
        var data = new SerializedObject(point);
        data.FindProperty("lookTarget").objectReferenceValue = target.transform;
        data.FindProperty("lookAtOffset").vector3Value = new Vector3(-.2f, .15f, 3.15f);
        data.FindProperty("orbitArcDegrees").floatValue = 18;
        data.FindProperty("orbitPeriod").floatValue = 180;
        data.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(Vector3.Distance(point.EvaluatePosition(0), point.transform.position), Is.LessThan(.0001f));
        Assert.That(Vector3.Distance(point.EvaluatePosition(20), point.EvaluatePosition(21)), Is.InRange(.001f, .04f));
        foreach (float time in new[] { 0f, 45f, 90f, 135f, 180f, 600f })
            Assert.That(point.EvaluatePosition(time).y, Is.EqualTo(3.6f).Within(.0001f));
        Assert.That(Vector3.Distance(point.EvaluatePosition(180), point.EvaluatePosition(0)), Is.LessThan(.0001f));
        Assert.That(Vector3.Distance(point.EvaluatePosition(45), point.EvaluatePosition(45.01f)), Is.LessThan(.001f));
        data.FindProperty("orbitArcDegrees").floatValue = 0; data.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(point.EvaluatePosition(50), Is.EqualTo(point.transform.position));
    }
    [Test] public void SceneHasOneExplicitVigilWithThreeSeatsAndNoAutomaticPlayback()
    {
        var scene = EditorSceneManager.OpenPreviewScene(MaisonHearthAuthoring.ScenePath);
        try
        {
            var authored = scene.GetRootGameObjects().SelectMany(item => item.GetComponentsInChildren<MaisonHearthVigil>(true)).Single();
            Assert.That(authored.runner.Sequence, Is.SameAs(vigil.runner.Sequence));
            Assert.That(authored.companionSeats.Select(seat => seat.actorId), Is.EquivalentTo(new[] { "Vesta", "Soren", "Aurell" }));
            Assert.That(authored.companionSeats.Select(seat => seat.point).Distinct().Count(), Is.EqualTo(3));
            var runner = new SerializedObject(authored.runner);
            Assert.That(runner.FindProperty("playOnStart").boolValue, Is.False);
            Assert.That(runner.FindProperty("playerTransform").objectReferenceValue, Is.Not.Null);
            foreach (string field in new[] { "onSequenceCompleted", "onSequenceAborted" })
                Assert.That(runner.FindProperty(field).FindPropertyRelative("m_PersistentCalls.m_Calls").arraySize, Is.EqualTo(1));
            var bindings = new SerializedObject(authored.bindings);
            Assert.That(bindings.FindProperty("events").arraySize, Is.EqualTo(2));
            Assert.That(authored.bindings.ResolveSpawnPoint(MaisonHearthVigil.PortalSpawnPointId)?.point, Is.Not.Null);
            Assert.That(authored.seatedController.animationClips.All(clip => clip.isLooping), Is.True);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}

// Executes these same NUnit assertions without TestRunner's save-scene dialog.
// Every case owns and destroys its fixture and restores the global completion hook.
[InitializeOnLoad]
internal static class MaisonHearthNativeValidation
{
    static MaisonHearthNativeValidation() => EditorApplication.update += Poll;
    private static void Poll()
    {
        const string folder = "Library/MaisonHearthVigil";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(folder + "/Validate.request")) return;
        if (!File.Exists(MaisonHearthAuthoring.SequencePath)) return;
        File.Delete(folder + "/Validate.request");
        int pass = 0, fail = 0; var report = new System.Text.StringBuilder();
        foreach (var test in typeof(MaisonHearthTests).GetMethods().Where(method => method.IsDefined(typeof(TestAttribute), false)))
        {
            var fixture = new MaisonHearthTests();
            try { fixture.Setup(); test.Invoke(fixture, null); pass++; report.AppendLine("PASS " + test.Name); }
            catch (Exception e) { fail++; report.AppendLine("FAIL " + test.Name + "\n" + (e.InnerException ?? e)); }
            finally { fixture.Cleanup(); }
        }
        File.WriteAllText(folder + "/NativeChecks.txt", $"{pass} passed; {fail} failed. Native NUnit assertions, not a Play Mode run.\n" + report);
    }
}
