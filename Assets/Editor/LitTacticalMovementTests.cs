using NUnit.Framework;
using UnityEngine;

public sealed class LitTacticalMovementTests
{
    [TestCase(30)] [TestCase(60)] [TestCase(120)]
    public void DisplayAimStaysConstantAcrossFixedStepBoundaries(int fps)
    {
        const float fixedStep = .02f, speed = 4;
        Quaternion orbit = Quaternion.Euler(50, 25, 0);
        Vector3 offset = new Vector3(.2f, 1.35f, .1f);
        Quaternion expected = Quaternion.LookRotation(-(orbit * Vector3.back), Vector3.up);
        float previousImplementationPeakError = 0;
        for (int frame = 1; frame <= fps; frame++)
        {
            float time = (float)frame / fps;
            float fixedTime = Mathf.Floor(time / fixedStep) * fixedStep;
            float alpha = (time - fixedTime) / fixedStep;
            Vector3 previousRoot = Vector3.right * (fixedTime - fixedStep) * speed;
            Vector3 fixedRoot = Vector3.right * fixedTime * speed;
            Vector3 renderedRoot = Vector3.Lerp(previousRoot, fixedRoot, alpha);
            Vector3 previousTarget = LitTacticalCameraMath.StableFollowAnchor(previousRoot, Quaternion.identity, orbit, offset);
            Vector3 fixedTarget = LitTacticalCameraMath.StableFollowAnchor(fixedRoot, Quaternion.identity, orbit, offset);
            Vector3 camera = Vector3.Lerp(previousTarget + orbit * Vector3.back * 10, fixedTarget + orbit * Vector3.back * 10, alpha);
            Vector3 renderedTarget = LitTacticalCameraMath.StableFollowAnchor(renderedRoot, Quaternion.identity, orbit, offset);
            Assert.That(Quaternion.Angle(Quaternion.LookRotation(renderedTarget - camera, Vector3.up), expected), Is.LessThan(.05f));
            previousImplementationPeakError = Mathf.Max(previousImplementationPeakError,
                Quaternion.Angle(Quaternion.LookRotation(fixedTarget - camera, Vector3.up), expected));
        }
        Assert.That(previousImplementationPeakError, Is.GreaterThan(.05f), "The fixture must reproduce the old mixed-time-domain jitter.");
    }
    [Test] public void StableTargetDoesNotFollowBodyYawOrCorrectedCameraRotation()
    {
        Vector3 root = new Vector3(2, 3, 4);
        Quaternion orbit = Quaternion.Euler(50, 20, 0);
        Vector3 offset = new Vector3(.2f, 1.35f, .4f);
        Vector3 target = LitTacticalCameraMath.StableFollowAnchor(root, Quaternion.identity, orbit, offset);
        for (int yaw = 0; yaw < 360; yaw += 45)
            Assert.That(Vector3.Distance(target, LitTacticalCameraMath.StableFollowAnchor(root, Quaternion.Euler(0, yaw, 0), orbit, offset)), Is.LessThan(.0001f));
    }
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
