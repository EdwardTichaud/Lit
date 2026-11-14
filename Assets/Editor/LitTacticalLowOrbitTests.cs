using NUnit.Framework;
using UnityEngine;

public sealed class LitTacticalLowOrbitTests
{
    [TestCase(0f, 42f)]
    [TestCase(1f, 42f)]
    [TestCase(5f, 44.05f)]
    [TestCase(10f, 50.29f)]
    [TestCase(20f, 60f)]
    [TestCase(30f, 60f)]
    public void TacticalFovOpensSmoothlyWithZoom(float distance, float expected)
    {
        Assert.That(LitTacticalCameraMath.ResolveOrbitFieldOfView(distance, new Vector2(1, 20), 42, 90),
            Is.EqualTo(expected).Within(.06f));
    }

    [TestCase(1f)]
    [TestCase(10f)]
    [TestCase(20f)]
    public void LowOrbitReachesKneeHeightWithoutChangingZoom(float distance)
    {
        const float pivotHeight = 1.35f;
        float pitch = LitTacticalCameraMath.LimitLowOrbitPitch(-55, distance, pivotHeight, .5f);
        Assert.That(pivotHeight + Mathf.Sin(pitch * Mathf.Deg2Rad) * distance, Is.EqualTo(.5f).Within(.04f));
        Assert.That(LitTacticalCameraMath.LimitLowOrbitPitch(50, distance, pivotHeight, .5f), Is.EqualTo(50));
    }

    [Test]
    public void GroundProbeLiftsOverRaisedFloorAndKeepsYawAndDistanceAlongGround()
    {
        var floor = new GameObject("Raised floor");
        try
        {
            floor.layer = 30;
            var origin = new Vector3(12000, 12000, 12000);
            floor.transform.position = origin + Vector3.up * .25f;
            floor.AddComponent<BoxCollider>().size = new Vector3(2, .5f, 2);
            Physics.SyncTransforms();
            RaycastHit[] hits = null;
            Vector3 pivot = origin + new Vector3(0, 1.35f, 5);
            Vector3 desired = origin + Vector3.up * .5f;
            var corrected = LitTacticalCameraMath.KeepOrbitAboveGround(pivot, desired, .25f, .5f, 1 << 30, null, ref hits);
            Assert.That(corrected, Is.EqualTo(origin + Vector3.up));
            var above = desired + Vector3.up;
            Assert.That(LitTacticalCameraMath.KeepOrbitAboveGround(pivot, above, .25f, .5f, 1 << 30, null, ref hits), Is.EqualTo(above));
        }
        finally { Object.DestroyImmediate(floor); }
    }
}
