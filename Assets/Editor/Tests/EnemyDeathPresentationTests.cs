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
            var health = root.AddComponent<CharacterInfo>();
            EnemyController enemy = root.AddComponent<EnemyController>();
            EnemyController locomotion = enemy;
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(EnemyController).GetField("ActorHealth", fields).SetValue(enemy, health);
            typeof(EnemyController).GetField("LocomotionEnemy", fields).SetValue(locomotion, enemy);
            locomotion.SetCombatTarget(target.transform);
            health.SetHealth(0, 10);
            root.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            Quaternion before = root.transform.rotation;
            typeof(EnemyController).GetMethod("LocomotionUpdate", fields).Invoke(locomotion, null);
            locomotion.FaceTarget(new Vector3(10f, 0f, 0f));
            typeof(EnemyController).GetMethod("LocomotionLateUpdate", fields).Invoke(locomotion, null);
            Assert.IsNull(typeof(EnemyController).GetField("LocomotionCombatTarget", fields).GetValue(locomotion));
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
