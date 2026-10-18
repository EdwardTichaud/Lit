using NUnit.Framework;
using UnityEngine;

public sealed class LitTacticalNearClipTests
{
    [Test] public void DefaultsIgnoreObstaclesAndPreserveSerializedEnumValues()
    {
        var profile = ScriptableObject.CreateInstance<LitTacticalCameraProfile>();
        try
        {
            Assert.That((int)TacticalObstacleMode.Sliding, Is.Zero);
            Assert.That((int)TacticalObstacleMode.VisibilityMask, Is.EqualTo(1));
            Assert.That((int)TacticalObstacleMode.IgnoreObstacles, Is.EqualTo(2));
            Assert.That(profile.obstacleMode, Is.EqualTo(TacticalObstacleMode.IgnoreObstacles));
            Assert.That(profile.adaptiveNearClip, Is.False);
            Assert.That(profile.nearClipPlayerMargin, Is.EqualTo(5));
            Assert.That(profile.nearClipSmoothTime, Is.EqualTo(.15f));
            Assert.That(profile.nearClipBodySafety, Is.EqualTo(.5f));
        }
        finally { Object.DestroyImmediate(profile); }
    }

    [TestCase(2, 1, .3f)]
    [TestCase(10, 9, 5)]
    [TestCase(20, 19, 15)]
    [TestCase(20, 8, 7.5f)]
    [TestCase(-10, -11, .3f)]
    public void TargetUsesDepthAndProtectsNearestBody(float depth, float nearest, float expected)
        => Assert.That(LitTacticalNearClip.ResolveTarget(.3f, 100, depth, nearest, 5, .5f), Is.EqualTo(expected).Within(.0001f));

    [Test] public void MissingBodyKeepsBaselineAndFarPlaneIsRespected()
    {
        Assert.That(LitTacticalNearClip.ResolveTarget(.3f, 100, 20, float.PositiveInfinity, 5, .5f), Is.EqualTo(.3f));
        Assert.That(LitTacticalNearClip.ResolveTarget(.3f, 8, 20, 19, 5, .5f), Is.EqualTo(7.99f).Within(.0001f));
    }

    [TestCase(25)] [TestCase(50)] [TestCase(75)]
    public void BodyProtectionUsesDisplayedPoseAtEveryPitch(float pitch)
    {
        var root = new GameObject("Body");
        try
        {
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = 2; capsule.radius = .5f; capsule.center = Vector3.up;
            Assert.That(LitTacticalNearClip.TryCaptureBody(root.transform, out var body), Is.True);
            Vector3 forward = Quaternion.Euler(pitch, 45, 0) * Vector3.forward;
            Vector3 camera = Vector3.up - forward * 20;
            float depth = LitTacticalNearClip.NearestDepth(body, root.transform, camera, forward);
            float target = LitTacticalNearClip.ResolveTarget(.3f, 100, 20, depth, 0, .5f);
            Assert.That(target, Is.LessThanOrEqualTo(depth - .5f + .0001f));
            root.transform.position = -forward * 3;
            Assert.That(LitTacticalNearClip.NearestDepth(body, root.transform, camera, forward), Is.EqualTo(depth - 3).Within(.0001f));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test] public void CaptureRejectsTriggersAnimatedBonesAndRagdollBodies()
    {
        var root = new GameObject("Character");
        try
        {
            var body = root.AddComponent<CapsuleCollider>(); body.height = 2;
            var trigger = new GameObject("Detection"); trigger.transform.SetParent(root.transform);
            trigger.AddComponent<SphereCollider>().isTrigger = true;
            trigger.transform.localScale = Vector3.one * 100;
            var skeleton = new GameObject("Animated skeleton"); skeleton.transform.SetParent(root.transform);
            skeleton.AddComponent<Animator>(); skeleton.AddComponent<BoxCollider>().size = Vector3.one * 100;
            var bone = new GameObject("Ragdoll"); bone.transform.SetParent(root.transform);
            bone.AddComponent<Rigidbody>(); bone.AddComponent<BoxCollider>().size = Vector3.one * 100;
            Assert.That(LitTacticalNearClip.TryCaptureBody(root.transform, out var bounds), Is.True);
            Assert.That(bounds.size, Is.EqualTo(new Vector3(1, 2, 1)));
            body.enabled = false;
            Assert.That(LitTacticalNearClip.TryCaptureBody(root.transform, out _), Is.False);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase(30)] [TestCase(60)] [TestCase(120)]
    public void AdvanceIsSmoothRetractionImmediateAndRestorationExact(int fps)
    {
        var root = new GameObject("Rendering camera");
        try
        {
            var camera = root.AddComponent<Camera>();
            camera.nearClipPlane = .27f; camera.farClipPlane = 200; camera.fieldOfView = 63;
            var clip = new LitTacticalNearClip(camera);
            for (int frame = 0; frame < fps; frame++)
            {
                float previous = camera.nearClipPlane;
                clip.Apply(20, 19, 5, .5f, .15f, 1f / fps);
                Assert.That(camera.nearClipPlane, Is.InRange(previous, 15));
            }
            Assert.That(camera.nearClipPlane, Is.EqualTo(15).Within(.01f));
            clip.Apply(20, 4, 5, .5f, .15f, 1f / fps);
            Assert.That(camera.nearClipPlane, Is.EqualTo(3.5f).Within(.0001f));
            clip.Restore(); clip.Restore();
            Assert.That(camera.nearClipPlane, Is.EqualTo(.27f));
            Assert.That(camera.farClipPlane, Is.EqualTo(200));
            Assert.That(camera.fieldOfView, Is.EqualTo(63));
            Assert.That(root.transform.position, Is.EqualTo(Vector3.zero));
            camera.nearClipPlane = .4f; // A Timeline can now own the lens.
            clip.Apply(10, 9, 5, .5f, .15f, 1f / fps);
            clip.Restore();
            Assert.That(camera.nearClipPlane, Is.EqualTo(.4f));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase(TacticalObstacleMode.Sliding, true, TacticalObstacleMode.Sliding)]
    [TestCase(TacticalObstacleMode.VisibilityMask, true, TacticalObstacleMode.Sliding)]
    [TestCase(TacticalObstacleMode.IgnoreObstacles, true, TacticalObstacleMode.IgnoreObstacles)]
    [TestCase(TacticalObstacleMode.IgnoreObstacles, false, TacticalObstacleMode.IgnoreObstacles)]
    public void FreeCameraKeepsIgnoreButRestoresMask(TacticalObstacleMode requested, bool free, TacticalObstacleMode expected)
    {
        Assert.That(LitTacticalCameraProfile.ResolveObstacleMode(requested, free, false), Is.EqualTo(expected));
        Assert.That(LitTacticalCameraProfile.ResolveObstacleMode(requested, free, true), Is.EqualTo(TacticalObstacleMode.Sliding));
    }
}
