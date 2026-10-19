#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Ultrabolt.BrainsAI;

public sealed class ProductionLandingRecoveryTests
{
    private readonly List<Object> objects = new List<Object>();
    private readonly Vector3 origin = new Vector3(7000, 0, 7000);
    private NavMeshDataInstance navInstance;
    private LitBrainsEnemy enemy;
    private Rigidbody body;
    private NavMeshAgent agent;
    private GameObject support;
    private SimulationMode previousSimulation;
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

    private void Set(string name, object value) => typeof(LitBrainsEnemy).GetField(name, Fields).SetValue(enemy, value);
    private T Get<T>(string name) => (T)typeof(LitBrainsEnemy).GetField(name, Fields).GetValue(enemy);
    private bool Begin() => (bool)typeof(LitBrainsEnemy).GetMethod("TryBeginProductionLandingRecovery", Fields).Invoke(enemy, new object[] { body.position });
    private void Tick() => typeof(LitBrainsEnemy).GetMethod("TickProductionMotion", Fields).Invoke(enemy, null);

    [SetUp] public void Setup() { previousSimulation = Physics.simulationMode; Physics.simulationMode = SimulationMode.Script; }
    [TearDown] public void Cleanup()
    {
        if (navInstance.valid) navInstance.Remove();
        foreach (var item in objects) if (item != null) Object.DestroyImmediate(item);
        objects.Clear();
        Physics.simulationMode = previousSimulation;
    }
    private GameObject Box(string name, Vector3 position, Vector3 size)
    {
        var root = new GameObject(name); objects.Add(root); root.transform.position = position;
        root.AddComponent<BoxCollider>().size = size; return root;
    }
    private void Create(float floor = 0)
    {
        if (NavMesh.GetSettingsCount() == 0) Assert.Ignore("Project-authored NavMesh agent type required.");
        var settings = NavMesh.GetSettingsByIndex(0);
        var nav = NavMeshBuilder.BuildNavMeshData(settings, new List<NavMeshBuildSource> {
            new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = new Vector3(4, .2f, 6),
                transform = Matrix4x4.TRS(origin + new Vector3(3, floor - .1f, 0), Quaternion.identity, Vector3.one), area = 0 }
        }, new Bounds(origin + new Vector3(3, floor, 0), new Vector3(12, 6, 12)), Vector3.zero, Quaternion.identity);
        Assert.IsNotNull(nav); objects.Add(nav); navInstance = NavMesh.AddNavMeshData(nav);
        Box("Walkable physical floor", origin + new Vector3(2, floor - .1f, 0), new Vector3(10, .2f, 10));
        support = Box("Unnavigable prop support", origin + Vector3.up * .2f, new Vector3(.8f, .4f, .8f));
        var root = new GameObject("Landing recovery actor"); objects.Add(root); root.SetActive(false);
        root.transform.position = origin + Vector3.up * .43f;
        var fov = root.AddComponent<FOV>(); enemy = root.AddComponent<LitBrainsEnemy>(); enemy.fov = fov; enemy.enabled = false;
        body = root.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
        var capsule = root.AddComponent<CapsuleCollider>(); capsule.center = Vector3.up; capsule.height = 2; capsule.radius = .35f;
        agent = root.GetComponent<NavMeshAgent>(); agent.enabled = false; agent.agentTypeID = settings.agentTypeID; agent.areaMask = NavMesh.AllAreas;
        typeof(Brain).GetField("agent", Fields).SetValue(enemy, agent);
        var data = ScriptableObject.CreateInstance<CharacterData>(); objects.Add(data); data.hp = 50;
        data.enemySettings.PhysicsGroundMask = ~0; enemy.ConfigureCharacterData(data);
        var skill = ScriptableObject.CreateInstance<SkillSO>(); objects.Add(skill);
        Set("productionBody", body); Set("productionCapsule", capsule); Set("productionMotionSkill", skill);
        Set("productionMotionActive", true); Set("productionEndRequested", true);
        Assert.IsTrue(NavMesh.SamplePosition(origin + new Vector3(3, floor, 0), out var start, 1, NavMesh.AllAreas));
        Set("productionLandingOrigin", start.position);
        root.SetActive(true); Physics.SyncTransforms();
    }

    [Test] public void GroundedPropEgressUsesBoundedPhysicalStepsAndLeavesNavigationDisabledUntilAdmission()
    {
        Create(); Vector3 start = body.position;
        Assert.IsFalse(NavMesh.SamplePosition(start, out _, .15f, NavMesh.AllAreas));
        Assert.IsTrue(Begin()); Assert.AreEqual(start, body.position);
        bool arrived = false;
        for (int index = 0; index < 160 && Get<bool>("productionMotionActive"); index++)
        {
            Vector3 before = body.position; Tick(); Physics.Simulate(Time.fixedDeltaTime); Physics.SyncTransforms();
            Assert.Less(Vector3.Distance(before, body.position), .2f, "No instantaneous correction to the distant polygon.");
            Assert.IsFalse(agent.enabled);
            bool penetrating = Physics.ComputePenetration(enemy.GetComponent<CapsuleCollider>(), body.position, body.rotation,
                support.GetComponent<BoxCollider>(), support.transform.position, support.transform.rotation, out _, out float depth);
            Assert.IsFalse(penetrating && depth > .001f, "The egress must not pass through its physical prop support.");
            if (body.position.y < .16f && NavMesh.SamplePosition(body.position, out _, .15f, NavMesh.AllAreas)) { arrived = true; break; }
        }
        Assert.IsTrue(arrived, body.position.ToString());
        Assert.Greater(body.position.x - origin.x, .8f);
        Assert.IsTrue(enemy.GetComponent<CapsuleCollider>().enabled);
    }

    [Test] public void DifferentFloorIsRejectedWithoutMovementOrAgentActivation()
    {
        Create(-4); Vector3 before = body.position;
        Assert.IsFalse(Begin()); Assert.AreEqual(before, body.position); Assert.IsFalse(agent.enabled);
    }

    [TestCase(.45f, false)]
    [TestCase(.55f, false)]
    [TestCase(.45f, true)]
    [TestCase(.55f, true)]
    public void DescentUsesTheWholeCapsuleInsteadOfSinkingIntoAnOffsetSupport(float offset, bool rush)
    {
        Create();
        var overhang = Box("Offset support missed by the central ground ray", origin + new Vector3(offset, .5f, 0), new Vector3(.5f, .3f, .8f));
        body.position = origin + Vector3.up * 2;
        Set("productionAirborne", true); Set("productionVerticalSpeed", -2f); Physics.SyncTransforms();
        if (rush)
        {
            Set("productionRush", true); Set("productionRushDirection", Vector3.down);
            Set("productionRushDistance", 2f); Set("productionRushTime", .25f);
        }
        bool landed = false;
        for (int index = 0; index < 100; index++)
        {
            Tick(); Physics.Simulate(Time.fixedDeltaTime); Physics.SyncTransforms();
            var capsule = enemy.GetComponent<CapsuleCollider>();
            bool penetrating = Physics.ComputePenetration(capsule, body.position, body.rotation,
                overhang.GetComponent<BoxCollider>(), overhang.transform.position, overhang.transform.rotation, out _, out float depth);
            Assert.IsFalse(penetrating && depth > .001f, "Ground must be detected before the full capsule penetrates an offset prop.");
            if (!Get<bool>("productionAirborne")) { landed = true; break; }
        }
        Assert.IsTrue(landed); Assert.IsTrue(Get<bool>("productionCapsuleSupported"));
        Assert.Greater(body.position.y, .5f, "The actor rests on its actual capsule contact, not the lower central ray hit.");
        Assert.IsFalse(agent.enabled);
    }

    [Test] public void WallBlocksPhysicalEgressAndBoundedFailureDoesNotWarpOrEnableAgent()
    {
        Create(); Box("Blocking wall", origin + new Vector3(.7f, 1.5f, 0), new Vector3(.1f, 3, 8)); Physics.SyncTransforms();
        Assert.IsTrue(Begin());
        LogAssert.Expect(LogType.Warning, new Regex("\\[LitBrainsLanding\\].*bounded physical egress timed out"));
        for (int index = 0; index < 180 && Get<bool>("productionMotionActive"); index++)
        { Tick(); Physics.Simulate(Time.fixedDeltaTime); Physics.SyncTransforms(); }
        Assert.IsFalse(Get<bool>("productionMotionActive")); Assert.IsFalse(Get<bool>("productionLandingRecovery"));
        Assert.Less(body.position.x - origin.x, .4f); Assert.IsFalse(agent.enabled);
    }

    [Test] public void StaleGenerationCannotContinueThePreviousRecovery()
    {
        Create(); Assert.IsTrue(Begin()); Vector3 before = body.position;
        typeof(LitBrainsEnemy).GetProperty("ActionSequenceId").SetValue(enemy, enemy.ActionSequenceId + 1);
        Tick(); Assert.AreEqual(before, body.position); Assert.IsFalse(Get<bool>("productionLandingRecovery"));
        Assert.IsFalse(Get<bool>("productionMotionActive")); Assert.IsFalse(agent.enabled);
    }

    [Test] public void CinematicOwnershipStopsCapturedRecoveryWithoutMovingTheActor()
    {
        Create(); Assert.IsTrue(Begin()); Vector3 before = body.position;
        enemy.SetCinematicSuspended(true); Tick();
        Assert.IsFalse(Get<bool>("productionLandingRecovery")); Assert.AreEqual(before, body.position); Assert.IsFalse(agent.enabled);
    }
}
#endif
