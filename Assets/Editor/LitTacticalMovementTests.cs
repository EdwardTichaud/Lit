using NUnit.Framework;
using UnityEngine;

public sealed class LitTacticalMovementTests
{
    [Test] public void TriggersZoomInOppositeDirectionsAndCancelTogether()
    {
        Assert.That(LitTacticalCameraMath.TriggerZoomDelta(1, 0, 8, .1f), Is.EqualTo(.8f).Within(.0001f));
        Assert.That(LitTacticalCameraMath.TriggerZoomDelta(0, 1, 8, .1f), Is.EqualTo(-.8f).Within(.0001f));
        Assert.That(LitTacticalCameraMath.TriggerZoomDelta(1, 1, 8, .1f), Is.Zero);
        Assert.That(LitTacticalCameraMath.TriggerZoomDelta(0, 0, 8, .1f), Is.Zero);
    }
    [TestCase(30)] [TestCase(60)] [TestCase(120)]
    public void TriggerZoomIsFrameRateIndependentAndClampsToOneAndTen(int fps)
    {
        float near = 10, far = 1;
        for (int i = 0; i < fps * 3; i++)
        {
            near = Mathf.Clamp(near - LitTacticalCameraMath.TriggerZoomDelta(1, 0, 8, 1f / fps), 1, 10);
            far = Mathf.Clamp(far - LitTacticalCameraMath.TriggerZoomDelta(0, 1, 8, 1f / fps), 1, 10);
        }
        Assert.That(near, Is.EqualTo(1));
        Assert.That(far, Is.EqualTo(10));
    }
    [TestCase(0)] [TestCase(90)] [TestCase(180)] [TestCase(270)]
    public void DirectionsFollowScreenRegardlessOfCharacterFacing(float cameraYaw)
    {
        Assert.That(LitTacticalCameraMath.TryMovementBasis(Quaternion.Euler(50, cameraYaw, 0), out var forward, out var right), Is.True);
        Vector2[] inputs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right,
            new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) };
        foreach (var input in inputs)
        {
            var world = LitTacticalCameraMath.ScreenToWorldInput(input, forward, right);
            Vector3 desired = Quaternion.Euler(0, cameraYaw, 0) * new Vector3(input.normalized.x, 0, input.normalized.y);
            Assert.That(Vector3.Distance(new Vector3(world.x, 0, world.y), desired), Is.LessThan(.0001f));
            foreach (float characterYaw in new[] { 0f, 90f, 180f, 270f })
            {
                Quaternion facing = Quaternion.Euler(0, characterYaw, 0);
                Vector3 local = Quaternion.Inverse(facing) * new Vector3(world.x, 0, world.y);
                Assert.That(Vector3.Distance(facing * local, desired), Is.LessThan(.0001f));
            }
        }
    }
    [TestCase(25)] [TestCase(50)] [TestCase(75)] [TestCase(90)]
    public void PitchDoesNotReduceSpeedOrBoostDiagonals(float pitch)
    {
        Assert.That(LitTacticalCameraMath.TryMovementBasis(Quaternion.Euler(pitch, 123, 0), out var forward, out var right), Is.True);
        var analog = LitTacticalCameraMath.ScreenToWorldInput(new Vector2(.3f, .4f), forward, right);
        Assert.That(analog.magnitude, Is.EqualTo(.5f).Within(.0001f));
        Assert.That(LitTacticalCameraMath.ScreenToWorldInput(Vector2.one, forward, right).magnitude, Is.EqualTo(1).Within(.0001f));
        Assert.That(LitTacticalCameraMath.ScreenToWorldInput(Vector2.zero, forward, right), Is.EqualTo(Vector2.zero));
    }
    [Test] public void HeldInputUsesTheNewCameraBasisWithoutOldAngleMemory()
    {
        LitTacticalCameraMath.TryMovementBasis(Quaternion.Euler(50, 0, 0), out var forward, out var right);
        Assert.That(LitTacticalCameraMath.ScreenToWorldInput(Vector2.up, forward, right), Is.EqualTo(Vector2.up));
        LitTacticalCameraMath.TryMovementBasis(Quaternion.Euler(50, 90, 0), out forward, out right);
        Assert.That(Vector2.Distance(LitTacticalCameraMath.ScreenToWorldInput(Vector2.up, forward, right), Vector2.right), Is.LessThan(.0001f));
    }
}
