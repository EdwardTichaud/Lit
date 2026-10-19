#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

public sealed class TacticalActionFramingTests
{
    [TestCase(60f, 1.77778f)] [TestCase(40f, 1.33333f)] [TestCase(75f, 2.33333f)]
    public void FitKeepsHeadAndLateralPointInsideTheSafeFrame(float fov, float aspect)
    {
        var point = new Vector3(3, 2.5f, -1);
        float distance = LitTacticalActionFraming.RequiredDistance(point, fov, aspect);
        float depth = distance + point.z;
        float halfHeight = depth * Mathf.Tan(fov * .5f * Mathf.Deg2Rad);
        Assert.That(Mathf.Abs(point.y), Is.LessThanOrEqualTo(halfHeight * .8001f));
        Assert.That(Mathf.Abs(point.x), Is.LessThanOrEqualTo(halfHeight * aspect * .8001f));
        Assert.That(depth, Is.GreaterThan(0));
    }

    [Test]
    public void NarrowerFovOrGreaterSeparationNeverAllowsACloserCamera()
    {
        float wide = LitTacticalActionFraming.RequiredDistance(new Vector3(0, 2, 0), 60, 1.7f);
        Assert.That(LitTacticalActionFraming.RequiredDistance(new Vector3(0, 2, 0), 40, 1.7f), Is.GreaterThan(wide));
        Assert.That(LitTacticalActionFraming.RequiredDistance(new Vector3(0, 4, 0), 60, 1.7f), Is.GreaterThan(wide));
    }

    [Test]
    public void SharedProfilesRemainWideAndFramingIsOptIn()
    {
        var profile = ScriptableObject.CreateInstance<LitTacticalCameraProfile>();
        try
        {
            Assert.That(profile.actionFraming.enabled, Is.False);
            Assert.That(profile.initialDistance, Is.EqualTo(10));
            Assert.That(profile.initialPitch, Is.EqualTo(50));
            Assert.That(profile.actionFraming.skillDistanceRatio, Is.LessThan(profile.actionFraming.basicDistanceRatio));
            Assert.That(profile.actionFraming.returnSeconds, Is.GreaterThan(profile.actionFraming.entrySeconds));
        }
        finally { Object.DestroyImmediate(profile); }
    }
}
#endif
