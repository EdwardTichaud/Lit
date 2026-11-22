#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class SceneTransitionRepairTests
{
    private Scene scene;
    private Scene other;
    private Scene previous;
    private readonly List<string> temporaryPaths = new();
    private bool createdBaseline;
    private readonly List<GameObject> players = new();
    private HashSet<string> scope;
    [SetUp] public void SetUp()
    {
        previous = SceneManager.GetActiveScene();
        createdBaseline = string.IsNullOrEmpty(previous.path);
        if (createdBaseline)
        {
            Assert.That(previous.isDirty, Is.False, "Do not replace an unsaved user scene.");
            previous = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SaveFixture(previous);
        }
        scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SaveFixture(scene);
        other = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SaveFixture(other);
        scope = new HashSet<string> { scene.name };
        var actor = InScene("actor", scene);
        var capsule = actor.AddComponent<CapsuleCollider>();
        capsule.center = Vector3.up * .9f; capsule.height = 1.8f; capsule.radius = .3f;
        players.Add(actor);
        Floor(scene);
    }
    [TearDown] public void TearDown()
    {
        players.Clear();
        if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        if (other.IsValid()) EditorSceneManager.CloseScene(other, true);
        if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
        if (createdBaseline) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        foreach (string path in temporaryPaths) AssetDatabase.DeleteAsset(path);
        temporaryPaths.Clear();
    }
    private void SaveFixture(Scene fixture)
    {
        string path = "Assets/__SceneTransitionFixture_" + Guid.NewGuid().ToString("N") + ".unity";
        temporaryPaths.Add(path);
        Assert.That(EditorSceneManager.SaveScene(fixture, path), Is.True);
    }
    private GameObject InScene(string name, Scene destination)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, destination);
        return go;
    }
    private void Floor(Scene destination)
    {
        var go = InScene("floor", destination);
        go.transform.position = Vector3.down * .1f;
        go.AddComponent<BoxCollider>().size = new Vector3(20, .2f, 20);
    }
    private bool Validate(Pose[] poses, out string issue) => SceneTransitionPlacement.Validate(players, poses, scope, out issue);
    [Test] public void ValidFloorAndClearCapsuleAreAccepted() =>
        Assert.That(Validate(new[] { new Pose(Vector3.zero, Quaternion.identity) }, out var issue), Is.True, issue);
    [Test] public void MissingDestinationIsRejected() => Assert.That(Validate(Array.Empty<Pose>(), out _), Is.False);
    [Test] public void MissingPlayerIsRejected()
    { players[0] = null; Assert.That(Validate(new[] { Pose.identity }, out _), Is.False); }
    [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
    public void NonFiniteDestinationIsRejected(float number) =>
        Assert.That(Validate(new[] { new Pose(new Vector3(number, 0, 0), Quaternion.identity) }, out _), Is.False);
    [Test] public void MissingFloorIsRejected() =>
        Assert.That(Validate(new[] { new Pose(Vector3.right * 50, Quaternion.identity) }, out _), Is.False);
    [Test] public void SourceFloorCannotValidateDestination()
    {
        scope = new HashSet<string> { other.name };
        Assert.That(Validate(new[] { Pose.identity }, out _), Is.False);
    }
    [Test] public void SolidDestinationObstacleIsRejected()
    {
        var obstacle = InScene("wall", scene); obstacle.transform.position = Vector3.up;
        obstacle.AddComponent<BoxCollider>().size = new Vector3(1, 1, 1);
        Assert.That(Validate(new[] { Pose.identity }, out _), Is.False);
    }
    [Test] public void SourceObstacleDoesNotBlockDestination()
    {
        var obstacle = InScene("source wall", other); obstacle.transform.position = Vector3.up;
        obstacle.AddComponent<BoxCollider>();
        Assert.That(Validate(new[] { Pose.identity }, out var issue), Is.True, issue);
    }
    [Test] public void CooperativeArrivalsMustBeDistinct()
    {
        var second = InScene("second actor", scene); players.Add(second);
        Assert.That(Validate(new[] { Pose.identity, Pose.identity }, out _), Is.False);
        Assert.That(Validate(new[] { Pose.identity, new Pose(Vector3.right * 1.5f, Quaternion.identity) }, out var issue), Is.True, issue);
    }
    [Test] public void DefaultSpawnSearchIsScopedAndOrdered()
    {
        Spawn(scene, 1); Spawn(other, 0); Spawn(scene, 0);
        var found = ZoneSpawnPoint.FindAll("Default", scope);
        Assert.That(found.Count, Is.EqualTo(2));
        Assert.That(found[0].PartySlot, Is.Zero);
        Assert.That(found[1].PartySlot, Is.EqualTo(1));
        Assert.That(found[0].gameObject.scene, Is.EqualTo(scene));
    }
    [Test] public void ExplicitMissingSpawnDoesNotBorrowAnotherZone()
    { Spawn(other, 0); Assert.That(ZoneSpawnPoint.FindAll("Default", scope), Is.Empty); }
    [Test] public void FailedUccPlacementDoesNotMoveTheTransform()
    {
        var managerObject = InScene("inactive squad fixture", scene); managerObject.SetActive(false);
        var manager = managerObject.AddComponent<SquadManager>();
        typeof(SquadManager).GetField("currentCharacter", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SetValue(manager, players[0]);
        Assert.That(manager.TryMoveSquadToDestinationPoints(new[] { new Pose(Vector3.right * 2, Quaternion.identity) }, out string issue), Is.False);
        Assert.That(issue, Does.Contain("UCC"));
        Assert.That(players[0].transform.position, Is.EqualTo(Vector3.zero));
    }
    [Test] public void RecoveryLatchRefusesCaptureUntilSessionReset()
    {
        var owner = InScene("inactive recovery fixture", scene); owner.SetActive(false);
        var world = owner.AddComponent<WorldStateManager>();
        world.RequireRecovery("test refusal");
        Assert.That(world.RequiresRecovery, Is.True);
        Assert.That(world.CaptureSnapshot(), Is.Null);
        Assert.That(world.LastCaptureSucceeded, Is.False);
        world.ResetSceneArchives();
        Assert.That(world.RequiresRecovery, Is.False);
    }
    [TestCase(false)] [TestCase(true)]
    public void OverlayVisibilityAndPendingPresentationCountAsLoading(bool pendingPresentation)
    {
        var owner = InScene("inactive loading fixture", scene); owner.SetActive(false);
        var service = owner.AddComponent<LoadingScreenService>();
        var group = owner.AddComponent<CanvasGroup>();
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var singleton = typeof(LoadingScreenService).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);
        var previousService = singleton.GetValue(null);
        try
        {
            singleton.SetValue(null, service);
            typeof(LoadingScreenService).GetField("canvasGroup", flags).SetValue(service, group);
            group.alpha = pendingPresentation ? 0 : 1;
            typeof(LoadingScreenService).GetField("currentFadeTarget", flags).SetValue(service, pendingPresentation ? 1f : 0f);
            Assert.That(LoadingScreenService.IsLoading, Is.True);
            group.alpha = 0;
            typeof(LoadingScreenService).GetField("currentFadeTarget", flags).SetValue(service, 0f);
            Assert.That(LoadingScreenService.IsLoading, Is.False);
        }
        finally { singleton.SetValue(null, previousService); }
    }
    [Test] public void NavigationCoverageIgnoresTheRetainedSourceZone()
    {
        var owner = InScene("inactive navigation fixture", scene); owner.SetActive(false);
        var service = owner.AddComponent<NavMeshWorldService>();
        const BindingFlags instanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        const BindingFlags staticFlags = BindingFlags.Static | BindingFlags.NonPublic;
        var singleton = typeof(NavMeshWorldService).GetField("<Instance>k__BackingField", staticFlags);
        var previousService = singleton.GetValue(null);
        try
        {
            singleton.SetValue(null, service);
            var names = (HashSet<string>)typeof(NavMeshWorldService).GetField("currentWorldScenes", instanceFlags).GetValue(service);
            names.Add(scene.name);
            var classify = typeof(SquadAIManager).GetMethod("IsLoadedGameplayScene", staticFlags);
            Assert.That(classify.Invoke(null, new object[] { scene }), Is.True);
            Assert.That(classify.Invoke(null, new object[] { other }), Is.False);
        }
        finally { singleton.SetValue(null, previousService); }
    }
    [TestCase(false)] [TestCase(true)]
    public void RollbackDoesNotResurrectADisconnectedPlayer(bool stillConnected)
    {
        var snapshot = new WorldSnapshot();
        snapshot.Players.Add(new PlayerSnapshot { OwnerClientId = 0, ControlledObjectId = "actor:host" });
        snapshot.Players.Add(new PlayerSnapshot { OwnerClientId = 7, PlayerId = "identity:guest", ControlledObjectId = "actor:guest" });
        snapshot.RuntimeObjects.Add(new PersistentObjectSnapshot { PersistentId = "actor:host" });
        snapshot.RuntimeObjects.Add(new PersistentObjectSnapshot { PersistentId = "actor:guest" });
        snapshot.RuntimeObjects.Add(new PersistentObjectSnapshot { PersistentId = "world:drop" });
        var archive = new PersistentSceneArchive();
        archive.RuntimeObjects.Add(new PersistentObjectSnapshot { PersistentId = "actor:guest" });
        snapshot.SceneArchives.Add(archive);
        var connected = new HashSet<ulong> { 0 };
        if (stillConnected) connected.Add(7);
        typeof(GameFlowService).GetMethod("RetainConnectedCheckpointPlayers", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { snapshot, connected });
        Assert.That(snapshot.Players.Count, Is.EqualTo(stillConnected ? 2 : 1));
        Assert.That(snapshot.RuntimeObjects.Count, Is.EqualTo(stillConnected ? 3 : 2));
        Assert.That(archive.RuntimeObjects.Count, Is.EqualTo(stillConnected ? 1 : 0));
        Assert.That(snapshot.RuntimeObjects.Exists(record => record.PersistentId == "world:drop"), Is.True);
    }
    private void Spawn(Scene destination, int slot)
    {
        var spawn = InScene("spawn " + slot, destination).AddComponent<ZoneSpawnPoint>();
        var data = new SerializedObject(spawn); data.FindProperty("partySlot").intValue = slot;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
