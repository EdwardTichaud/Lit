using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

public sealed class RuntimeStabilizationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void ScientistEncounterHasNoKnowledgePrerequisite()
    {
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Narrative/NinaCycle/Data/Enemy_ScientifiqueFou.asset");
        Assert.That(data, Is.Not.Null);
        Assert.That(data.enemyEncounterOptions.requiredKnowledge, Is.Null);
    }

    [Test]
    public void EncounterKnowledgeGateTracksTheCurrentSessionKnowledge()
    {
        var root = new GameObject("Encounter knowledge test");
        root.SetActive(false);
        var data = ScriptableObject.CreateInstance<CharacterData>();
        var required = ScriptableObject.CreateInstance<KnowledgeSO>();
        var unrelated = ScriptableObject.CreateInstance<KnowledgeSO>();
        required.knowledgeId = "test.required";
        unrelated.knowledgeId = "test.unrelated";
        var instance = typeof(KnowledgeManager).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
        var previous = KnowledgeManager.Instance;
        try
        {
            var enemy = root.AddComponent<EnemyController>();
            enemy.Health.SetCharacterData(data);
            var gate = typeof(EnemyController).GetProperty("HasEncounterKnowledge", Private);
            instance.SetValue(null, null);
            Assert.That(gate.GetValue(enemy), Is.True, "Ordinary encounters have no prerequisite.");
            data.enemyEncounterOptions.requiredKnowledge = required;
            Assert.That(gate.GetValue(enemy), Is.False, "A missing service must not bypass the requirement.");
            var manager = root.AddComponent<KnowledgeManager>();
            instance.SetValue(null, manager);
            var list = typeof(KnowledgeManager).GetField("unlockedKnowledge", Private);
            var ready = typeof(KnowledgeManager).GetField("lookupReady", Private);
            foreach (bool known in new[] { false, true, false })
            {
                list.SetValue(manager, new System.Collections.Generic.List<KnowledgeSO> { known ? required : unrelated });
                ready.SetValue(manager, false);
                Assert.That(gate.GetValue(enemy), Is.EqualTo(known));
            }
        }
        finally
        {
            instance.SetValue(null, previous);
            Object.DestroyImmediate(root); Object.DestroyImmediate(data);
            Object.DestroyImmediate(required); Object.DestroyImmediate(unrelated);
        }
    }

    [Test]
    public void LateAssignedGhostDataBlocksCombatEvenWithSerializedCombatEnabled()
    {
        var root = new GameObject("Late assigned scientist");
        root.SetActive(false);
        var data = ScriptableObject.CreateInstance<CharacterData>();
        try
        {
            var enemy = root.AddComponent<EnemyController>();
            var ghost = root.AddComponent<GhostController>();
            Assert.That(enemy.CombatEnabled, Is.True);
            data.enemyEncounterOptions.startAsGhost = true;
            enemy.Health.SetCharacterData(data);
            enemy.CombatEnabled = true;
            Assert.That(enemy.CombatEnabled, Is.False);
            Assert.That(enemy.ReceiveLightDamage(10), Is.Zero);
            Assert.That(enemy.TryStartRetaliation(), Is.False);
            ghost.SetGameplayMode(GhostController.GameplayMode.Introduction);
            Assert.That(enemy.CombatEnabled, Is.False);
            ghost.SetGameplayMode(GhostController.GameplayMode.Enemy);
            Assert.That(enemy.CombatEnabled, Is.True);
            ghost.SetGameplayMode(GhostController.GameplayMode.Ghost);
            Assert.That(enemy.CombatEnabled, Is.False);
            Assert.That(ghost.enabled, Is.True);
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(data); }
    }

    [Test]
    public void SoloEncounterTransitionsDoNotWriteNetworkVariable()
    {
        var root = new GameObject("Solo scientist test");
        root.SetActive(false);
        try
        {
            root.AddComponent<BoxCollider>();
            var encounter = root.AddComponent<EnemyController>();
            Assert.That(encounter, Is.Not.Null);
            Type stateType = typeof(EnemyController).GetNestedType("EncounterState", BindingFlags.NonPublic);
            MethodInfo set = typeof(EnemyController).GetMethod("SetState", Private);
            foreach (string next in new[] { "Dialogue", "Active" })
            {
                set.Invoke(encounter, new[] { Enum.Parse(stateType, next) });
                Assert.That(typeof(EnemyController).GetProperty("CurrentState", Private).GetValue(encounter).ToString(), Is.EqualTo(next));
                object variable = typeof(EnemyController).GetField("state", Private).GetValue(encounter);
                Assert.That(variable.GetType().GetProperty("Value").GetValue(variable).ToString(), Is.EqualTo("Dormant"));
            }
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void MarkerWaitsWhenWorldServiceHasNotAwakened()
    {
        Assert.That(NavMeshWorldService.Instance, Is.Null, "Run in an empty EditMode fixture.");
        var root = new GameObject("Marker waiting test");
        root.SetActive(false);
        try
        {
            var marker = root.AddComponent<SceneMarker>();
            var routine = (IEnumerator)typeof(SceneMarker).GetMethod("ValidateEnemyNavigationAfterWorldBake", Private).Invoke(marker, null);
            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(routine.MoveNext(), Is.True, "Absence of the service must not end validation permanently.");
            (routine as IDisposable)?.Dispose();
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase("Luc/Ghost_Model_Luc")]
    [TestCase("Scar/Ghost_Model_Scar")]
    [TestCase("Luc/Enemy_Model_MadScientist")]
    public void NonUccPrefabsHaveNoOrphanColliderPositioner(string relativePath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/9_Ghosts/" + relativePath + ".prefab");
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponentsInChildren<Opsive.UltimateCharacterController.Character.CapsuleColliderPositioner>(true), Is.Empty);
        foreach (var agent in prefab.GetComponentsInChildren<NavMeshAgent>(true))
            Assert.That(agent.enabled, Is.False, "Agent activation belongs to the ready world service.");
    }

    [Test]
    public void CorridorFlamesHaveDistinctAuthoredIdsAndKeepOriginalIdentity()
    {
        string scene = File.ReadAllText("Assets/Scenes/District_1/District_1_Corridor_Flammes.unity");
        string[] ids = Regex.Matches(scene, @"(?m)^  flameId: (\S+)").Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
        Assert.That(ids, Does.Contain("scene-flame:Maison:0DE640398"));
        Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
    }

    [Test]
    public void LocomotionSpeedIsNotDrivenByCrouchClip()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Characters/1_Squad/Lucian/Animation/PlayerInPlace/Idle_Crouch_7ac9514a_Inplace.anim");
        Assert.That(clip, Is.Not.Null);
        Assert.That(AnimationUtility.GetCurveBindings(clip).Any(b => b.type == typeof(Animator) && b.propertyName == "Speed"), Is.False);
    }

    [Test]
    public void WorldInvalidationDisablesRegisteredEnemyAgent()
    {
        var root = new GameObject("Inactive navigation fixture");
        var worldRoot = new GameObject("Inactive world fixture");
        root.SetActive(false);
        worldRoot.SetActive(false);
        try
        {
            var agent = root.AddComponent<NavMeshAgent>();
            EnemyController navigation = root.AddComponent<EnemyController>();
            var world = worldRoot.AddComponent<NavMeshWorldService>();
            typeof(EnemyController).GetField("NavigationNavigationAgent", Private).SetValue(navigation, agent);
            typeof(EnemyController).GetMethod("NavigationBindWorld", Private).Invoke(navigation, new object[] { world });
            typeof(NavMeshWorldService).GetMethod("SetState", Private).Invoke(world, new object[] { NavMeshWorldState.Invalidating });
            Assert.That(agent.enabled, Is.False);
            Assert.That(navigation.Status, Is.EqualTo(EnemyController.ReadinessStatus.WaitingForWorld));
            typeof(EnemyController).GetMethod("NavigationBindWorld", Private).Invoke(navigation, new object[] { null });
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(worldRoot); }
    }

    [Test]
    public void OtherCharacterBonesOnDefaultLayerAreNotGround()
    {
        var root = new GameObject("Ground filtering fixture");
        var other = new GameObject("Other character");
        root.SetActive(false);
        other.SetActive(false);
        try
        {
            EnemyController motor = root.AddComponent<EnemyController>();
            other.AddComponent<CharacterInfo>();
            var bone = new GameObject("foot_l");
            bone.transform.SetParent(other.transform);
            var collider = bone.AddComponent<BoxCollider>();
            var accepts = typeof(EnemyController).GetMethod("PhysicsIsGroundCollider", Private);
            Assert.That(accepts.Invoke(motor, new object[] { collider }), Is.False);
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(other); }
    }

    [Test]
    public void GroundProbeFindsNegativeAltitudeFloorWithoutInitialOverlap()
    {
        var root = new GameObject("Negative altitude actor");
        root.SetActive(false);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            root.transform.position = new Vector3(500f, -98f, 500f);
            floor.transform.position = new Vector3(500f, -98.5f, 500f);
            floor.transform.localScale = new Vector3(10f, 1f, 10f);
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.radius = 0.5f;
            capsule.height = 2f;
            capsule.center = Vector3.up;
            EnemyController motor = root.AddComponent<EnemyController>();
            typeof(EnemyController).GetField("PhysicsBodyCollider", Private).SetValue(motor, capsule);
            Physics.SyncTransforms();
            object[] args = { root.transform.position, 0f };
            bool found = (bool)typeof(EnemyController).GetMethod("PhysicsTryGetGroundY", Private).Invoke(motor, args);
            Assert.That(found, Is.True);
            Assert.That((float)args[1], Is.EqualTo(-97.97f).Within(0.01f));
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(floor); }
    }

    [Test]
    public void ScientistAuthoredPoseHasLocalBakedNavigation()
    {
        var data = AssetDatabase.LoadAssetAtPath<NavMeshData>("Assets/Navigation/NavMeshData/District_1_Core_NavMeshData.asset");
        Assert.That(data, Is.Not.Null);
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/District_1/District_1_Enigme_Ghost_Nina.unity");
        var instance = NavMesh.AddNavMeshData(data, Vector3.zero, Quaternion.identity);
        try
        {
            var marker = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SceneMarker>(true))
                .Single(m => m.name == "SceneMarker_ScientifiqueFou");
            Vector3 position = marker.transform.position;
            bool found = NavMesh.SamplePosition(position, out NavMeshHit hit, 1.5f, NavMesh.AllAreas);
            Directory.CreateDirectory("Library/Stabilization");
            File.WriteAllText("Library/Stabilization/scientist-navmesh.txt", $"Authored marker: {position}; local sample: {found}; hit: {hit.position}; delta: {Vector3.Distance(position, hit.position)}");
            Assert.That(found, Is.True, "Authored scientist marker has no local baked navigation.");
            Assert.That(Vector3.Distance(position, hit.position), Is.LessThanOrEqualTo(0.15f));
        }
        finally { instance.Remove(); EditorSceneManager.ClosePreviewScene(scene); }
    }
}
