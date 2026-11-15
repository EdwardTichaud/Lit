#if UNITY_INCLUDE_TESTS
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ExplorationAbilityTests
{
    [Test]
    public void AuthoredSeuilBeamHasAReachableHorizontalGrabPose()
    {
        const string path = "Assets/Scenes/Reve/Reve_Limbes_Secrets.unity";
        var scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.isLoaded;
        const string environmentPath = "Assets/Scenes/Reve/Reve_Limbes_Environment.unity";
        var environment = SceneManager.GetSceneByPath(environmentPath);
        bool openedEnvironment = !environment.isLoaded;
        if (openedEnvironment) environment = EditorSceneManager.OpenScene(environmentPath, OpenSceneMode.Additive);
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var point = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Lit.Traversal.ClingPoint>(true))
                .Single(p => p.PointId == "limbes.seuil.poutre");
            var data = new SerializedObject(point);
            var anchor = (Transform)data.FindProperty("holdPose").objectReferenceValue;
            var surface = (Collider)data.FindProperty("supportSurface").objectReferenceValue;
            Assert.That(surface, Is.Not.Null);
            Assert.That(data.FindProperty("grabAlongSurface").boolValue, Is.True);
            Assert.That(Mathf.Abs(anchor.forward.y), Is.LessThan(.15f));
            Physics.SyncTransforms();
            var fit = typeof(Lit.Traversal.ClingPoint).GetMethod("TryFitSurface",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            object[] args = { surface, anchor.position, anchor.forward, point.GripPosition.y,
                Vector3.up, .4f, 1.8f, null };
            Assert.That(fit.Invoke(null, args), Is.True, "Anchor " + anchor.position + "; grip " + point.GripPosition +
                "; collider " + surface.bounds + "; mesh " + (surface as MeshCollider)?.sharedMesh?.bounds);
            var pose = (Pose)args[7];
            Assert.That(Vector3.Distance(pose.position, anchor.position),
                Is.LessThan(data.FindProperty("contactRadius").floatValue));
            const float sweepRadius = .385f;
            var forward = pose.rotation * Vector3.forward;
            var c = pose.position + Vector3.up;
            var from = c - forward * .50f;
            var hits = Physics.OverlapCapsule(c - Vector3.up * .5f, c + Vector3.up * .5f,
                    sweepRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                .Select(x => "overlap " + x.name)
                .Concat(Physics.CapsuleCastAll(from - Vector3.up * .5f, from + Vector3.up * .5f, sweepRadius,
                    forward, .50f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    .Select(h => h.collider.name + " at " + h.distance + ": " + h.normal))
                .ToArray();
            Assert.That(hits, Is.Empty, "Authored grab pose " + pose.position + " heading " + forward);
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            if (openedEnvironment) EditorSceneManager.CloseScene(environment, true);
        }
    }

    [TestCase(-.54f, true, true)]
    [TestCase(.54f, true, true)]
    [TestCase(1.2f, true, false)]
    [TestCase(.54f, false, false)]
    public void ClingCanUseEitherFaceAwayFromTheAnchorButRequiresReachAndClearance(float x, bool clear, bool expected)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            wall.transform.position = Vector3.up;
            wall.transform.localScale = new Vector3(.25f, 2f, 6f);
            Physics.SyncTransforms();
            var find = typeof(Lit.Traversal.ClingPoint).GetMethod("TryFindContactPose",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            object[] args = { wall.GetComponent<Collider>(), new Vector3(x, 0f, 2f), Vector3.up, Vector3.up,
                .4f, 1.8f, Vector3.right, (System.Predicate<Pose>)(_ => clear), null };
            Assert.That(find.Invoke(null, args), Is.EqualTo(expected));
            if (expected)
            {
                var pose = (Pose)args[8];
                Assert.That(Vector3.Dot(pose.rotation * Vector3.forward, -Mathf.Sign(x) * Vector3.right), Is.GreaterThan(.99f));
                Assert.That(pose.position.z, Is.EqualTo(2f).Within(.05f));
            }
        }
        finally { Object.DestroyImmediate(wall); }
    }

    [TestCase("long-wall", true)]
    [TestCase("narrow-trim", true)]
    [TestCase("rounded", true)]
    [TestCase("underside", true)]
    [TestCase("walkable-top", false)]
    public void SurfaceGripUsesTheWholeCapsuleAndDoesNotRequireAFlatTorso(string shape, bool expected)
    {
        var target = GameObject.CreatePrimitive(shape == "rounded" ? PrimitiveType.Cylinder : PrimitiveType.Cube);
        try
        {
            // Local contact can occur arbitrarily far from the pivot, or only at one end of the capsule.
            Vector3 feet = new Vector3(.54f, 0f, 0f);
            target.transform.position = Vector3.up;
            target.transform.localScale = new Vector3(.25f, 2f, 2f);
            if (shape == "long-wall")
            {
                target.transform.localScale = new Vector3(.25f, 2f, 400f);
                feet.z = 177f;
            }
            else if (shape == "narrow-trim")
            {
                target.transform.position = Vector3.up * .55f;
                target.transform.localScale = new Vector3(.25f, .2f, 2f);
            }
            else if (shape == "rounded")
            {
                target.transform.localScale = new Vector3(1f, 3f, 1f);
                feet = new Vector3(.64f, 0f, .64f);
            }
            else if (shape == "underside")
            {
                target.transform.position = Vector3.up * 2f;
                target.transform.localScale = new Vector3(3f, .15f, 3f);
                feet = Vector3.zero;
            }
            else
            {
                target.transform.position = Vector3.down * .50f;
                target.transform.localScale = new Vector3(3f, 1f, 3f);
                feet = Vector3.zero;
            }
            Physics.SyncTransforms();
            var find = typeof(Lit.Traversal.ClingPoint).GetMethod("TryFindContactPose",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Collider surface = target.GetComponent<Collider>();
            var axis = Vector3.up * .5f;
            System.Predicate<Pose> clear = candidate =>
            {
                var center = candidate.position + Vector3.up;
                return !Physics.OverlapCapsule(center - axis, center + axis, .399f).Contains(surface);
            };
            object[] args = { surface, feet, Vector3.up, Vector3.up, .4f, 1.8f, Vector3.forward, clear, null };
            Assert.That(find.Invoke(null, args), Is.EqualTo(expected));
            if (expected) Assert.That(Vector3.Distance(((Pose)args[8]).position, feet), Is.LessThan(.35f));
        }
        finally { Object.DestroyImmediate(target); }
    }

    [TestCase(2f, 2f, 0f, true)]
    [TestCase(2f, .20f, 0f, false)]
    [TestCase(.17f, 2f, 0f, false)]
    [TestCase(2f, 2f, 1.08f, false)]
    public void ClingNeedsFourReachableAndDistinctLimbSupports(float width, float height, float sideOffset, bool expected)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            wall.transform.position = Vector3.up;
            wall.transform.localScale = new Vector3(.25f, height, width);
            Physics.SyncTransforms();
            var type = typeof(Lit.Traversal.ClingPoint).Assembly.GetType("Lit.Traversal.ClingGripTargets");
            var fit = type.GetMethod("TryBuild", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var pose = new Pose(new Vector3(.55f, 0f, sideOffset), Quaternion.LookRotation(Vector3.left));
            object[] args = { wall.GetComponent<Collider>(), pose, pose.position + Vector3.up, Vector3.right,
                .4f, 1.8f, new Vector2(.62f, .83f), null };
            Assert.That(fit.Invoke(null, args), Is.EqualTo(expected));
        }
        finally { Object.DestroyImmediate(wall); }
    }

    [TestCase(.38f, 0f, true)]
    [TestCase(.12f, 0f, false)]
    [TestCase(2f, .30f, false)]
    public void TopGripTurnsToFitAndRequiresStandingReach(float width, float feetHeight, bool expected)
    {
        var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            platform.transform.position = Vector3.down * .25f;
            platform.transform.localScale = new Vector3(width, .5f, 3f);
            Physics.SyncTransforms();
            var find = typeof(Lit.Traversal.ClingPoint).GetMethod("TryFindTopBrace",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            object[] args = { platform.GetComponent<Collider>(), Vector3.up * feetHeight,
                Vector3.up * .9f, Vector3.up * .9f, .4f, 1.8f, new Vector2(.62f, .83f), Vector3.right,
                (System.Predicate<Pose>)(_ => true), null, null, null };
            Assert.That(find.Invoke(null, args), Is.EqualTo(expected));
            if (expected)
            {
                var pose = (Pose)args[9];
                Assert.That(Mathf.Abs(Vector3.Dot(pose.rotation * Vector3.forward, Vector3.forward)), Is.GreaterThan(.8f));
                Assert.That(pose.position.y, Is.GreaterThanOrEqualTo(0f));
            }
        }
        finally { Object.DestroyImmediate(platform); }
    }

    [TestCase(.0001f, 0f, false)]
    [TestCase(.002f, 0f, true)]
    [TestCase(0f, 1f, true)]
    public void ClingReappliesOnlyMeaningfulPoseChanges(float distance, float angle, bool expected)
    {
        var traversal = typeof(Lit.Traversal.ClingPoint).Assembly.GetType("Lit.Traversal.ClingTraversal");
        var changed = traversal.GetMethod("PoseChanged",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var pose = new Pose(Vector3.right * distance, Quaternion.Euler(0, angle, 0));
        Assert.That(changed.Invoke(null, new object[] { Vector3.zero, Quaternion.identity, pose }), Is.EqualTo(expected));
    }

    [TestCase(2.0f, true)]
    [TestCase(.35f, false)]
    public void ClingFitsTheRealRotatedFaceAndRejectsHandOnlyContact(float wallHeight, bool expected)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            wall.transform.SetPositionAndRotation(new Vector3(8, 1.05f, 12), Quaternion.Euler(0, 37f, 0));
            wall.transform.localScale = new Vector3(2f, wallHeight, .25f);
            Physics.SyncTransforms();
            Vector3 facing = wall.transform.forward;
            Vector3 anchor = new Vector3(8, .05f, 12) - facing * .75f;
            object[] args = { wall.GetComponent<Collider>(), anchor, facing, 1.05f,
                Vector3.up, .4f, 1.8f, null };
            bool accepted = (bool)typeof(Lit.Traversal.ClingPoint)
                .GetMethod("TryFitSurface", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, args);
            Assert.That(accepted, Is.EqualTo(expected));
            if (!accepted) return;
            var pose = (Pose)args[7];
            Assert.That(Vector3.Dot(pose.rotation * Vector3.forward, facing), Is.GreaterThan(.999f));
            Assert.That(Vector3.Dot(wall.transform.position - pose.position, facing), Is.EqualTo(.55f).Within(.003f));
            Assert.That(pose.position.y, Is.EqualTo(anchor.y).Within(.003f));
        }
        finally { Object.DestroyImmediate(wall); }
    }

    [Test]
    public void SoloEquipmentAccumulatesAndRoundTripsAndToggleDoesNotRelock()
    {
        var state = new AbilitySaveData(); state.Unlock("observateur"); state.Unlock("grimpeur");
        Assert.That(state.AddEquipped("solo", "observateur"), Is.True);
        Assert.That(state.AddEquipped("solo", "grimpeur"), Is.True);
        Assert.That(state.AddEquipped("solo", "grimpeur"), Is.False);
        var copy = JsonUtility.FromJson<AbilitySaveData>(JsonUtility.ToJson(state));
        Assert.That(copy.HasEquipped("solo", "observateur") && copy.HasEquipped("solo", "grimpeur"), Is.True);
        copy.Toggle("solo", "observateur");
        Assert.That(copy.IsUnlocked("observateur") && !copy.HasEquipped("solo", "observateur") && copy.HasEquipped("solo", "grimpeur"), Is.True);
        Assert.That(copy.AddEquipped("solo", "locked"), Is.False);
        var serializer = new SnapshotSerializer();
        var restored = serializer.Deserialize(serializer.Serialize(new WorldSnapshot { Abilities = state }));
        Assert.That(restored.Abilities.slots.Count, Is.EqualTo(2));
    }
    [TestCase(1, 4, 0, 0, 0)]
    [TestCase(2, 2, 2, 0, 0)]
    [TestCase(3, 2, 1, 1, 0)]
    [TestCase(4, 1, 1, 1, 1)]
    public void AllocationCoversAllCapabilitiesWithoutDuplicates(int count, int a, int b, int c, int d)
    {
        var state = new AbilitySaveData();
        foreach (string id in new[] { "observer", "jumper", "passage", "fourth" }) state.Unlock(id);
        var players = Enumerable.Range(0, count).Select(i => "player" + i).ToArray();
        state.Distribute(players); state.Distribute(players);
        int[] expected = { a, b, c, d };
        for (int i = 0; i < count; i++) Assert.That(state.slots.Count(s => s.playerId == players[i]), Is.EqualTo(expected[i]));
        Assert.That(state.slots.Select(s => s.abilityId).Distinct().Count(), Is.EqualTo(4));
        state.Distribute(new[] { "remaining" });
        Assert.That(state.slots.All(s => s.playerId == "remaining"), Is.True);
        Assert.That(state.slots.Count, Is.EqualTo(4));
    }
    [Test]
    public void LegacyEquipmentStillResolvesAndAddPreservesIt()
    {
        var model = JsonUtility.FromJson<AbilitySaveData>("{\"unlockedIds\":[\"observateur\",\"grimpeur\"],\"slots\":[{\"playerId\":\"solo\",\"abilityId\":\"observateur\"}]}");
        Assert.That(model.HasEquipped("solo", "observateur"), Is.True);
        model.AddEquipped("solo", "grimpeur");
        Assert.That(model.slots.Count, Is.EqualTo(2));
    }
    [Test]
    public void CompleteRestoredTeamAllocationPreservesExchangesButNotDisconnectedOwners()
    {
        var model = new AbilitySaveData(); model.Unlock("observateur"); model.Unlock("grimpeur");
        model.AddEquipped("alice", "grimpeur"); model.AddEquipped("bob", "observateur");
        Assert.That(model.IsCompleteDistribution(new[] { "alice", "bob" }), Is.True);
        Assert.That(model.IsCompleteDistribution(new[] { "alice", "charlie" }), Is.False);
        model.Unlock("new");
        Assert.That(model.IsCompleteDistribution(new[] { "alice", "bob" }), Is.False);
    }
    [Test]
    public void SavedClimberMigratesUnlockAndEquipmentAndDropsRetiredAbility()
    {
        var root = new GameObject("Ability migration test");
        root.SetActive(false);
        try
        {
            var service = root.AddComponent<AbilityService>();
            var catalog = (System.Collections.Generic.List<AbilitySO>)service.Catalog;
            catalog.AddRange(Resources.LoadAll<AbilitySO>("Abilities"));
            var old = new AbilitySaveData { equipmentVersion = 2 };
            old.Unlock("accrocheur"); old.Unlock("bondisseur");
            old.AddEquipped("solo", "accrocheur"); old.AddEquipped("solo", "bondisseur");
            old.AddEquipped("alice", "accrocheur");
            service.Restore(old);
            var current = service.Capture();
            Assert.That(service.Resolve("accrocheur").displayName, Is.EqualTo("Grimpeur"));
            Assert.That(current.IsUnlocked("accrocheur") || current.IsUnlocked("bondisseur"), Is.False);
            Assert.That(current.HasEquipped("solo", "grimpeur") && current.HasEquipped("alice", "grimpeur"), Is.True);
            old.slots.Clear();
            service.Restore(old);
            Assert.That(service.Capture().HasEquipped("solo", "grimpeur"), Is.False);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void LimbesKeepsAbilityFreePortalBeyondTheFiveCycleGate()
    {
        const string path = "Assets/Scenes/Reve/Reve_Limbes_Progression.unity";
        var active = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var progression = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LimbesZoneProgression>(true)).Single();
            Assert.That(progression.explorationCycles.Length, Is.EqualTo(5));
            var portal = progression.arenaAccess.GetComponentsInChildren<PortalController>(true)
                .Single(p => p.name == "Portail_vers_la_Gargouille");
            Assert.That(portal.gameObject.activeSelf, Is.True);
            var portalData = new SerializedObject(portal);
            var destinations = portalData.FindProperty("destinationPoints");
            Assert.That(destinations.arraySize, Is.GreaterThan(0));
            for (int i = 0; i < destinations.arraySize; i++)
                Assert.That(destinations.GetArrayElementAtIndex(i).objectReferenceValue, Is.Not.Null);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); if (active.isLoaded) SceneManager.SetActiveScene(active); }
    }
}
#endif
