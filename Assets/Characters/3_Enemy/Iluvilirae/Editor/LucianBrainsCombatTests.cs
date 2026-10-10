#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class LucianBrainsCombatTests
{
    [Test]
    public void ContactRequiresRangeFacingAndUnobstructedPath()
    {
        var temporaryRoot = new GameObject("ContactTest_Temporary");
        var attacker = new GameObject("attacker");
        var target = new GameObject("target");
        attacker.transform.SetParent(temporaryRoot.transform);
        target.transform.SetParent(temporaryRoot.transform);
        GameObject obstacle = null;
        try
        {
            // Far from any open authoring scene's colliders.
            attacker.transform.position = new Vector3(10000, 0, 0);
            target.transform.position = attacker.transform.position + Vector3.forward * 2;
            Assert.IsTrue(LucianBrainsCombatArena.HasContact(attacker.transform, target.transform, 3, 110));
            Assert.IsFalse(LucianBrainsCombatArena.HasContact(attacker.transform, target.transform, 1, 110));
            attacker.transform.rotation = Quaternion.Euler(0, 180, 0);
            Assert.IsFalse(LucianBrainsCombatArena.HasContact(attacker.transform, target.transform, 3, 110));
            attacker.transform.rotation = Quaternion.identity;
            obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.transform.SetParent(temporaryRoot.transform);
            obstacle.transform.position = attacker.transform.position + Vector3.up + Vector3.forward;
            Physics.SyncTransforms();
            Assert.IsFalse(LucianBrainsCombatArena.HasContact(attacker.transform, target.transform, 3, 110));
        }
        finally { Object.DestroyImmediate(temporaryRoot); }
    }

    [Test]
    public void CombatSceneReferencesRealLucianAndOnlyBrainsJuggernaut()
    {
        var scene = EditorSceneManager.OpenScene(IluviliraeSetup.ScenePath, OpenSceneMode.Additive);
        try
        {
            LucianBrainsCombatArena arena = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                Assert.IsNull(root.GetComponentInChildren<GameFlowService>(true));
                Assert.IsNull(root.GetComponentInChildren<IluviliraeTestArena>(true));
                if (root.TryGetComponent<LucianBrainsCombatArena>(out var found)) arena = found;
            }
            Assert.IsNotNull(arena);
            Assert.IsNotNull(arena.navigation);
            Assert.IsNotNull(arena.arenaCamera);
            Assert.IsNotNull(arena.arenaCamera.GetComponent<LitGameplayCameraModeController>());
            Assert.IsNotNull(arena.GetComponent<TimeManager>());
            Assert.IsNotNull(arena.GetComponent<RealTimeCombatInput>());
            Assert.IsNotNull(arena.GetComponent<CombatMobilityController>());
            var mobility = new SerializedObject(arena.GetComponent<CombatMobilityController>());
            Assert.That(mobility.FindProperty("dodge.dashProfiles").arraySize, Is.GreaterThanOrEqualTo(4));
            Assert.That(arena.enemyAttackSkills.Length, Is.EqualTo(3));
            foreach (var skill in arena.enemyAttackSkills) Assert.IsNotNull(skill);
            var input = new SerializedObject(arena.GetComponent<RealTimeCombatInput>());
            Assert.IsNotNull(input.FindProperty("skillWheel").objectReferenceValue);
            var counter = new SerializedObject(arena.GetComponent<CounterSkillCombatController>());
            var counterSkill = counter.FindProperty("defaultCounterSkill").objectReferenceValue as CounterSkillSO;
            Assert.IsNotNull(counterSkill);
            Assert.IsNotNull(counterSkill.CombatCinematicRigPrefab);
            Assert.IsNotNull(counterSkill.Timeline);
            Assert.IsNotNull(arena.lucianPrefab.GetComponent<SquadCharacterController>());
            Assert.IsNotNull(arena.lucianPrefab.GetComponent<LitOpsiveLocomotionBridge>());
            Assert.IsNotNull(arena.enemyPrefab.GetComponent<JuggernautV2Brain>());
            Assert.IsNull(arena.enemyPrefab.GetComponentInChildren<EnemyController>(true));
            Assert.AreEqual(3, arena.basicSkills.Length);
            foreach (var skill in arena.basicSkills) Assert.IsNotNull(skill.AnimationClip);
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }
}
#endif
