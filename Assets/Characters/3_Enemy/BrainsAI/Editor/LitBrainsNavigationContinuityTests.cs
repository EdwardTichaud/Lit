#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using Ultrabolt.BrainsAI;

public sealed class LitBrainsNavigationContinuityTests
{
    [Test]
    public void CinematicPlacementRejectsNearbyDifferentFloorWithoutMovingActorOrEnablingAgent()
    {
        if (NavMesh.GetSettingsCount() == 0) Assert.Ignore("A project-authored NavMesh agent type is required.");
        var settings = NavMesh.GetSettingsByIndex(0);
        Vector3 origin = new Vector3(21000, 0, 21000);
        var sources = new List<NavMeshBuildSource> {
            new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = new Vector3(12, .2f, 12),
                transform = Matrix4x4.TRS(origin - Vector3.up * .1f, Quaternion.identity, Vector3.one), area = 0 }
        };
        var data = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(origin, new Vector3(16, 6, 16)), Vector3.zero, Quaternion.identity);
        Assert.IsNotNull(data);
        var instance = NavMesh.AddNavMeshData(data);
        var actor = new GameObject("Cinematic continuity fixture");
        actor.SetActive(false);
        try
        {
            var fov = actor.AddComponent<FOV>();
            var enemy = actor.AddComponent<LitBrainsEnemy>();
            enemy.fov = fov;
            var agent = actor.GetComponent<NavMeshAgent>();
            agent.enabled = false;
            agent.agentTypeID = settings.agentTypeID;
            typeof(Brain).GetField("agent", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(enemy, agent);
            var filter = new NavMeshQueryFilter { agentTypeID = settings.agentTypeID, areaMask = NavMesh.AllAreas };
            Assert.IsTrue(NavMesh.SamplePosition(origin, out var floor, 1, filter));
            actor.transform.position = floor.position + Vector3.up * .6f;
            var before = actor.transform.position;
            Assert.IsFalse(enemy.PlaceForCinematic(before, Quaternion.identity));
            Assert.AreEqual(before, actor.transform.position);
            Assert.IsFalse(agent.enabled);
            Assert.IsFalse(enemy.NavigationReady);
        }
        finally
        {
            instance.Remove();
            Object.DestroyImmediate(actor);
            Object.DestroyImmediate(data);
        }
    }
}
#endif
