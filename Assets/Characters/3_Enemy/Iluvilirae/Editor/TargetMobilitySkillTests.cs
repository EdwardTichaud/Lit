using NUnit.Framework;
using UnityEngine;

public sealed class TargetMobilitySkillTests
{
    [TestCase(0, 10, 0, true)]
    [TestCase(0, 10, 10, true)]
    [TestCase(0, 10, 10.01f, false)]
    [TestCase(2, 30, 1.99f, false)]
    [TestCase(2, 30, 2, true)]
    [TestCase(2, 30, 30, true)]
    [TestCase(2, 30, 30.01f, false)]
    public void LaunchBoundsAreInclusiveAndSeparateFromImpact(float min, float max, float distance, bool expected)
    {
        var skill = ScriptableObject.CreateInstance<SkillSO>();
        try
        {
            skill.useLaunchRange = true; skill.minimumLaunchDistance = min; skill.maximumLaunchDistance = max;
            skill.maximumHitDistance = 1;
            Assert.AreEqual(expected, skill.IsWithinLaunchRange(distance));
            Assert.IsFalse(skill.IsWithinHitRange(2));
        }
        finally { Object.DestroyImmediate(skill); }
    }
    [Test]
    public void LongApproachHasEnoughTimeAtTheSpeedLimit()
    {
        Assert.Greater(PlayerActionPresentationController.ContactApproachDuration(29.15f, 65, .25f), 29.15f / 65);
    }
}
