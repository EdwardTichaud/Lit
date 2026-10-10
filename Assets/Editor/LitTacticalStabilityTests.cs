using NUnit.Framework;
using UnityEngine;

public sealed class LitTacticalStabilityTests
{
    [TestCase(30)] [TestCase(60)] [TestCase(120)]
    public void AimUsesTheSameInterpolationFractionAsUccPosition(int fps)
    {
        var aim = new LitTacticalAimHistory();
        Quaternion start = Quaternion.Euler(50, 0, 0), end = Quaternion.Euler(50, 30, 0);
        aim.Capture(start, 1, true);
        aim.Capture(end, 1.02, false);
        for (int frame = 0; frame < fps; frame++)
        {
            double now = 1.02 + frame / (double)fps;
            float fraction = Mathf.Clamp01((float)((now - 1.02) / .02f));
            Assert.That(Quaternion.Angle(aim.Evaluate(now, .02f, true, Quaternion.identity),
                Quaternion.Slerp(start, end, fraction)), Is.LessThan(.05f));
        }
    }
    [Test] public void DuplicatePoseEvaluationDoesNotResetAimInterpolation()
    {
        var aim = new LitTacticalAimHistory();
        Quaternion start = Quaternion.Euler(50, 0, 0), end = Quaternion.Euler(50, 30, 0);
        aim.Capture(start, 1, true);
        aim.Capture(end, 1.02, false);
        aim.Capture(end, 1.02, false);
        Assert.That(Quaternion.Angle(aim.Evaluate(1.02, .02f, true, Quaternion.identity), start), Is.LessThan(.05f));
        Assert.That(Quaternion.Angle(aim.Evaluate(1.03, .02f, false, Quaternion.identity), end), Is.LessThan(.05f));
    }
    [Test] public void ImmediatePoseDiscardsAimHistoryAfterTeleport()
    {
        var aim = new LitTacticalAimHistory();
        aim.Capture(Quaternion.identity, 1, true);
        Quaternion destination = Quaternion.Euler(60, 180, 0);
        aim.Capture(destination, 2, true);
        Assert.That(Quaternion.Angle(aim.Evaluate(2, .02f, true, Quaternion.identity), destination), Is.LessThan(.05f));
    }
    [Test] public void InputAccumulatesUntilConsumedExactlyOnce()
    {
        var buffer = new LitTacticalInputBuffer();
        buffer.Add(Vector2.one, Vector2.right, 2);
        buffer.Add(Vector2.one, Vector2.right, 3);
        buffer.Consume(out var pan, out var orbit, out var zoom);
        Assert.That(pan, Is.EqualTo(Vector2.one * 2));
        Assert.That(orbit, Is.EqualTo(Vector2.right * 2));
        Assert.That(zoom, Is.EqualTo(5));
        buffer.Consume(out pan, out orbit, out zoom);
        Assert.That(pan, Is.EqualTo(Vector2.zero));
        Assert.That(orbit, Is.EqualTo(Vector2.zero));
        Assert.That(zoom, Is.Zero);
    }
    [Test] public void ContextChangeDiscardsResidualInput()
    {
        var buffer = new LitTacticalInputBuffer();
        buffer.Add(Vector2.one, Vector2.one, 1);
        buffer.Clear();
        buffer.Consume(out var pan, out var orbit, out var zoom);
        Assert.That(pan, Is.EqualTo(Vector2.zero));
        Assert.That(orbit, Is.EqualTo(Vector2.zero));
        Assert.That(zoom, Is.Zero);
    }
    [Test] public void MultipleFixedStepsInOneDisplayFrameAreNotDropped()
    {
        var clock = new LitTacticalSimulationClock();
        Assert.That(clock.TryAdvance(true, 1, 10), Is.True);
        Assert.That(clock.TryAdvance(true, 1, 10), Is.False);
        Assert.That(clock.TryAdvance(true, 1.02, 10), Is.True);
        Assert.That(clock.TryAdvance(false, 1.02, 10), Is.True);
        Assert.That(clock.TryAdvance(false, 1.02, 10), Is.False);
    }
    [Test] public void ImmediateEvaluationDoesNotConsumeTheNextSimulationStep()
    {
        var clock = new LitTacticalSimulationClock();
        Assert.That(clock.TryAdvance(true, 1, 10, true), Is.False);
        Assert.That(clock.TryAdvance(true, 1, 10), Is.True);
        clock.Reset();
        Assert.That(clock.TryAdvance(true, 1, 10), Is.True);
    }
    [TestCase(30)] [TestCase(60)] [TestCase(120)]
    public void CollisionRetractsImmediatelyAndRestoresAfterStableClearance(int fps)
    {
        var recovery = new LitTacticalDistanceRecovery();
        float dt = 1f / fps;
        recovery.Reset(10);
        Assert.That(recovery.Resolve(3, 10, dt, false, .08f, .15f), Is.EqualTo(3));
        Assert.That(recovery.Resolve(10, 10, dt, false, .08f, .15f), Is.EqualTo(3));
        float distance = 3;
        for (int i = 0; i < fps; i++) distance = recovery.Resolve(10, 10, dt, false, .08f, .15f);
        Assert.That(distance, Is.InRange(9.9f, 10));
        Assert.That(recovery.Resolve(2, 10, dt, false, .08f, .15f), Is.EqualTo(2));
    }
    [Test] public void FluctuatingClearanceDoesNotPumpDistanceOutward()
    {
        var recovery = new LitTacticalDistanceRecovery();
        recovery.Reset(3);
        recovery.Resolve(2, 10, .02f, false, .08f, .15f);
        for (int i = 0; i < 20; i++)
            Assert.That(recovery.Resolve(i % 2 == 0 ? 4 : 5, 10, .02f, false, .08f, .15f), Is.EqualTo(2));
    }

    [Test]
    public void AimInterpolationClampsBeforeAndAfterTheSimulationInterval()
    {
        var aim = new LitTacticalAimHistory();
        Quaternion start = Quaternion.Euler(50, 0, 0), end = Quaternion.Euler(50, 60, 0);
        aim.Capture(start, 1, true);
        aim.Capture(end, 1.02, false);
        Assert.That(Quaternion.Angle(aim.Evaluate(1, .02f, true, Quaternion.identity), start), Is.LessThan(.05f));
        Assert.That(Quaternion.Angle(aim.Evaluate(2, .02f, true, Quaternion.identity), end), Is.LessThan(.05f));
        aim.Reset();
        Assert.That(aim.Evaluate(2, .02f, true, start), Is.EqualTo(start));
    }
}
