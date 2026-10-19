#if UNITY_INCLUDE_TESTS
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CombatEnemyRecoveryTests
{
    [Test]
    public void LabFollowThroughReturnsNearNeutralWithoutRetimingTheImpact()
    {
        var profile = AssetDatabase.LoadAssetAtPath<LitBrainsCombatProfile>(CombatEnemyRecoveryAudit.ProfilePath);
        var report = CombatEnemyRecoveryAudit.Measure();
        Assert.That(profile.attacks.Length, Is.EqualTo(3));
        float[] impacts = { .56666666f, .7f, .6f };
        for (int i = 0; i < profile.attacks.Length; i++)
        {
            var action = profile.attacks[i];
            Assert.That(action.impactSeconds, Is.EqualTo(impacts[i]).Within(.0001f));
            Assert.That(action.recoveryDurationSeconds, Is.InRange(.7f, 1.2f));
            Assert.That(action.recoverySeconds + action.recoveryDurationSeconds, Is.LessThan(action.clip.length));
            Assert.That(action.visualHandoff.validated, Is.False, "Do not enable experimental playback-rate/IK retiming.");
            Assert.That(report.exits[i].currentMismatch, Is.LessThan(.08f));
            Assert.That(report.exits[i].candidate, Is.False, "Installed exit should already be the measured candidate.");
        }
    }

    [Test]
    public void ReinstallingMeasuredRecoveriesDoesNotRewriteTheProfile()
    {
        var before = File.ReadAllBytes(CombatEnemyRecoveryAudit.ProfilePath);
        CombatEnemyRecoveryAudit.Install();
        Assert.That(File.ReadAllBytes(CombatEnemyRecoveryAudit.ProfilePath).SequenceEqual(before), Is.True);
        Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(ChargedSkillSetup.EnemyPath).GetComponent<Animator>().applyRootMotion, Is.False);
    }
}
#endif
