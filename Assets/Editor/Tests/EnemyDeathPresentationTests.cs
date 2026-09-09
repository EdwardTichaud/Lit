#if UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class EnemyDeathPresentationTests
{
    [Test]
    public void DeadEnemyClearsItsTargetAndIgnoresFacingRequests()
    {
        var root = new GameObject("Dead enemy test");
        root.SetActive(false);
        var target = new GameObject("Player target");
        try
        {
            var health = root.AddComponent<CombatHealth>();
            var enemy = root.AddComponent<RealTimeCombatEnemy>();
            var locomotion = root.AddComponent<CombatEnemyLocomotionController>();
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(RealTimeCombatEnemy).GetField("health", fields).SetValue(enemy, health);
            typeof(CombatEnemyLocomotionController).GetField("enemy", fields).SetValue(locomotion, enemy);
            locomotion.SetCombatTarget(target.transform);
            health.SetHealth(0, 10);
            root.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            Quaternion before = root.transform.rotation;
            typeof(CombatEnemyLocomotionController).GetMethod("Update", fields).Invoke(locomotion, null);
            locomotion.FaceTarget(new Vector3(10f, 0f, 0f));
            typeof(CombatEnemyLocomotionController).GetMethod("LateUpdate", fields).Invoke(locomotion, null);
            Assert.IsNull(typeof(CombatEnemyLocomotionController).GetField("combatTarget", fields).GetValue(locomotion));
            Assert.That(Quaternion.Angle(before, root.transform.rotation), Is.LessThan(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(target);
        }
    }
}
#endif
