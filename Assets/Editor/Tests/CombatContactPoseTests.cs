#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CombatContactPoseTests
{
    [TestCase(-1f)] [TestCase(0f)] [TestCase(1f)] [TestCase(2f)]
    public void ContactEnvelopeReturnsToAuthoredPose(float time)
        => Assert.That(CombatContactPose.Envelope(time), Is.Zero);

    [Test]
    public void ContactIsFastThenSettlesWithoutOvershoot()
    {
        Assert.That(CombatContactPose.Envelope(.18f), Is.EqualTo(1).Within(.0001f));
        Assert.That(CombatContactPose.Envelope(.08f), Is.GreaterThan(CombatContactPose.Envelope(.8f)));
        for (int i = 0; i <= 100; i++)
            Assert.That(CombatContactPose.Envelope(i / 100f), Is.InRange(0f, 1f));
    }

    [Test]
    public void InvalidRigCannotLeaveAContactOrMoveTheRoot()
    {
        var root = new GameObject("Contact without actor");
        try
        {
            root.transform.SetPositionAndRotation(new Vector3(1, 2, 3), Quaternion.Euler(0, 32, 0));
            var pose = root.AddComponent<CombatContactPose>();
            Assert.That(pose.PlayContact(Vector3.zero, 1), Is.False);
            pose.enabled = false;
            Assert.That(pose.AppliedAngle, Is.Zero);
            Assert.That(root.transform.position, Is.EqualTo(new Vector3(1, 2, 3)));
            Assert.That(Quaternion.Angle(root.transform.rotation, Quaternion.Euler(0, 32, 0)), Is.LessThan(.001f));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void OnlyLaboratoryCameraAndEnemyOptIntoPresentation()
    {
        var profile = AssetDatabase.LoadAssetAtPath<LitTacticalCameraProfile>(CombatFeelLabSetup.CameraPath);
        var shared = AssetDatabase.LoadAssetAtPath<LitTacticalCameraProfile>("Assets/CombatRealTime/Camera/TacticalDefault.asset");
        Assert.That(profile, Is.Not.Null);
        Assert.That(profile, Is.Not.SameAs(shared));
        Assert.That(profile.initialDistance, Is.EqualTo(shared.initialDistance));
        Assert.That(profile.initialPitch, Is.EqualTo(shared.initialPitch));
        Assert.That(profile.actionFraming.enabled, Is.True);
        Assert.That(shared.actionFraming.enabled, Is.False);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChargedSkillSetup.EnemyPath);
        Assert.That(prefab.GetComponent<CombatContactPose>(), Is.Not.Null);
        Assert.That(prefab.GetComponents<CombatContactPose>().Length, Is.EqualTo(1));
    }
}
#endif
