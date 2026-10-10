using NUnit.Framework;
using UnityEngine;

public sealed class LitTacticalMotorTests
{
    [TestCase(0f)] [TestCase(90f)] [TestCase(180f)] [TestCase(270f)]
    public void InputRemainsWorldRelativeWhileUccTurns(float bodyYaw)
    {
        Quaternion current = Quaternion.Euler(0, bodyYaw, 0);
        Vector3 facing = Quaternion.Euler(0, 75, 0) * Vector3.forward;
        float yaw = LitTacticalMotorMath.ResolveYaw(current, facing);
        foreach (float rotationBlend in new[] { 0f, .06f, .15f, 1f })
        foreach (float influence in new[] { 0f, .5f, 1f })
        foreach (Vector2 world in new[] { Vector2.up, Vector2.right, Vector2.down, Vector2.left, new Vector2(.3f, .4f) })
        {
            Vector2 local = LitTacticalMotorMath.ResolveInput(world, world.magnitude, current, yaw, rotationBlend, influence);
            Quaternion appliedBasis = LitTacticalMotorMath.MotorInputRotation(current, yaw, rotationBlend, influence);
            Vector3 actual = appliedBasis * new Vector3(local.x, 0, local.y);
            Assert.That(Vector3.Distance(actual, new Vector3(world.x, 0, world.y)), Is.LessThan(.0001f));
            Assert.That(local.magnitude, Is.EqualTo(world.magnitude).Within(.0001f));
        }
    }
    [Test] public void NeutralInputNeverMovesWhileFacingContinues()
    {
        Assert.That(LitTacticalMotorMath.ResolveInput(Vector2.zero, 1, Quaternion.identity, 90, .15f, 1), Is.EqualTo(Vector2.zero));
        Assert.That(LitTacticalMotorMath.ResolveInput(Vector2.up, 0, Quaternion.identity, 90, .15f, 1), Is.EqualTo(Vector2.zero));
        Assert.That(LitTacticalMotorMath.ResolveYaw(Quaternion.identity, Vector3.zero), Is.Zero);
    }
    [Test] public void LockFacingUsesTheShortestYawWithoutChangingWorldMovement()
    {
        Quaternion current = Quaternion.Euler(0, 350, 0);
        Assert.That(LitTacticalMotorMath.ResolveYaw(current, Quaternion.Euler(0, 10, 0) * Vector3.forward), Is.EqualTo(20).Within(.001f));
        Assert.That(LitTacticalMotorMath.ResolveYaw(current, Quaternion.Euler(0, 330, 0) * Vector3.forward), Is.EqualTo(-20).Within(.001f));
    }
}
