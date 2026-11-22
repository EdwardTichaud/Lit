using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Lit.Story;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class StorySequenceInstantiationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root, prefab;
    private StorySequenceRunner runner;
    private StorySequenceAsset sequence;
    private StorySequenceSpawnBinding binding;
    private StorySequenceStep step;
    [SetUp] public void Setup()
    {
        root = new GameObject("Isolated sequence spawn fixture"); root.SetActive(false);
        var bindings = root.AddComponent<StorySequenceSceneBindings>();
        root.AddComponent<StorySequenceFadeController>();
        runner = root.AddComponent<StorySequenceRunner>();
        var anchor = new GameObject("Spawn anchor").transform; anchor.SetParent(root.transform);
        anchor.SetPositionAndRotation(new Vector3(4, 2, -1), Quaternion.Euler(0, 65, 0));
        binding = new StorySequenceSpawnBinding { spawnPointId = "fixture.anchor", point = anchor };
        typeof(StorySequenceSceneBindings).GetField("spawnPoints", Private).SetValue(bindings, new List<StorySequenceSpawnBinding> { binding });
        prefab = new GameObject("Fixture source"); prefab.transform.SetParent(root.transform);
        step = new StorySequenceStep { type = StorySequenceStepType.InstantiateGameObject, spawnId = "fixture.object", spawnPointId = binding.spawnPointId, spawnPrefab = prefab };
        sequence = ScriptableObject.CreateInstance<StorySequenceAsset>(); sequence.steps.Add(step);
        typeof(StorySequenceRunner).GetField("sequence", Private).SetValue(runner, sequence);
    }
    [TearDown] public void Cleanup()
    {
        typeof(StorySequenceRunner).GetMethod("ReleaseSequenceSpawns", Private).Invoke(runner, new object[] { false });
        Object.DestroyImmediate(root); Object.DestroyImmediate(sequence);
    }
    [Test] public void InstanceUsesAssignedTransformAndRepeatedStepDoesNotDuplicate()
    {
        int callbacks = 0; binding.onSpawned.AddListener(_ => callbacks++);
        Assert.That(runner.TryInstantiateStep(step), Is.True);
        var instance = runner.GetSpawnedObject(step.spawnId);
        Assert.That(instance, Is.Not.SameAs(prefab));
        Assert.That(instance.transform.parent, Is.SameAs(binding.point));
        Assert.That(Vector3.Distance(instance.transform.position, binding.point.position), Is.LessThan(.0001f));
        Assert.That(Quaternion.Angle(instance.transform.rotation, binding.point.rotation), Is.LessThan(.001f));
        Assert.That(runner.TryInstantiateStep(step), Is.True);
        Assert.That(runner.GetSpawnedObject(step.spawnId), Is.SameAs(instance));
        Assert.That(callbacks, Is.EqualTo(1));
    }
    [Test] public void SuccessfulSequenceRemovesTemporaryObjectsButKeepsAndRestoresPersistentOnes()
    {
        runner.TryInstantiateStep(step);
        typeof(StorySequenceRunner).GetMethod("ReleaseSequenceSpawns", Private).Invoke(runner, new object[] { true });
        Assert.That(runner.GetSpawnedObject(step.spawnId) == null, Is.True);
        step.keepSpawnAfterCompletion = true;
        runner.TryInstantiateStep(step); var kept = runner.GetSpawnedObject(step.spawnId);
        typeof(StorySequenceRunner).GetMethod("ReleaseSequenceSpawns", Private).Invoke(runner, new object[] { true });
        Assert.That(runner.GetSpawnedObject(step.spawnId), Is.SameAs(kept));
        runner.Abort(); Assert.That(runner.GetSpawnedObject(step.spawnId) == null, Is.True);
        Assert.That(kept == null || !kept.activeSelf, Is.True);
        runner.RestoreCompletedSpawns(); var restored = runner.GetSpawnedObject(step.spawnId);
        Assert.That(restored != null, Is.True);
        runner.RestoreCompletedSpawns(); Assert.That(runner.GetSpawnedObject(step.spawnId), Is.SameAs(restored));
    }
    [Test] public void DetachedInstanceBelongsToAnchorSceneAndAbortRemovesIt()
    {
        step.parentToSpawnPoint = false;
        Assert.That(runner.TryInstantiateStep(step), Is.True);
        var instance = runner.GetSpawnedObject(step.spawnId);
        Assert.That(instance.transform.parent, Is.Null);
        Assert.That(instance.scene, Is.EqualTo(binding.point.gameObject.scene));
        runner.Abort(); Assert.That(runner.GetSpawnedObject(step.spawnId) == null, Is.True);
        Assert.That(instance == null || !instance.activeSelf, Is.True);
    }
    [Test] public void ValidationRequiresPrefabAndStableUniqueIdentifiers()
    {
        Assert.That(sequence.TryValidate(out _), Is.True);
        step.spawnPrefab = null; Assert.That(sequence.TryValidate(out _), Is.False);
        step.spawnPrefab = prefab; step.spawnPointId = ""; Assert.That(sequence.TryValidate(out _), Is.False);
        step.spawnPointId = binding.spawnPointId;
        sequence.steps.Add(new StorySequenceStep { type = step.type, spawnId = " FIXTURE.OBJECT ", spawnPointId = step.spawnPointId, spawnPrefab = prefab });
        Assert.That(sequence.TryValidate(out string issue), Is.False);
        Assert.That(issue, Does.Contain("duplique"));
    }
    [Test] public void HearthSequenceAuthorsARealPrefabInstantiationAtItsSceneAnchor()
    {
        var hearth = AssetDatabase.LoadAssetAtPath<StorySequenceAsset>(MaisonHearthAuthoring.SequencePath);
        var spawn = hearth.steps.Single(item => item.type == StorySequenceStepType.InstantiateGameObject);
        Assert.That(spawn.spawnId, Is.EqualTo(MaisonHearthVigil.PortalSpawnId));
        Assert.That(spawn.keepSpawnAfterCompletion, Is.True);
        Assert.That(spawn.spawnPrefab.GetComponent<PortalController>(), Is.Not.Null);
        Assert.That(hearth.steps.Any(item => item.eventId == "hearth.reveal_portal"), Is.False);
    }
    [Test] public void CreatedPortalReusesOriginalArrivalReferencesWithoutMovingThem()
    {
        var originalObject = new GameObject("Original portal"); originalObject.transform.SetParent(root.transform);
        var original = originalObject.AddComponent<PortalController>();
        var destination = new GameObject("Authored arrival").transform; destination.SetParent(root.transform);
        destination.position = new Vector3(-20, 4, 12);
        typeof(PortalController).GetField("destinationPoints", Private).SetValue(original, new List<Transform> { destination });
        var createdObject = new GameObject("Created portal"); createdObject.transform.SetParent(root.transform);
        var created = createdObject.AddComponent<PortalController>(); created.CopyDestinationFrom(original);
        Assert.That(created.DestinationPoints.Single(), Is.SameAs(destination));
        Assert.That(destination.position, Is.EqualTo(new Vector3(-20, 4, 12)));
    }
}

[InitializeOnLoad]
internal static class StorySequenceInstantiationNativeChecks
{
    static StorySequenceInstantiationNativeChecks() => EditorApplication.update += Poll;
    private static void Poll()
    {
        const string folder = "Library/StoryInstantiation";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(folder + "/Validate.request")) return;
        File.Delete(folder + "/Validate.request");
        int passed = 0, failed = 0; var report = new System.Text.StringBuilder();
        foreach (var method in typeof(StorySequenceInstantiationTests).GetMethods().Where(method => method.IsDefined(typeof(TestAttribute), false)))
        {
            var fixture = new StorySequenceInstantiationTests();
            try { fixture.Setup(); method.Invoke(fixture, null); passed++; report.AppendLine("PASS " + method.Name); }
            catch (Exception exception) { failed++; report.AppendLine("FAIL " + method.Name + "\n" + (exception.InnerException ?? exception)); }
            finally { fixture.Cleanup(); }
        }
        File.WriteAllText(folder + "/NativeChecks.txt", $"{passed} passed; {failed} failed. Native NUnit assertions.\n" + report);
    }
}
