using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class CycleZoneTriggerTests
{
    private Scene preview;
    private GameObject parent, target;
    private CycleZoneTrigger trigger;
    private BoxCollider box;

    [SetUp]
    public void SetUp()
    {
        preview = EditorSceneManager.NewPreviewScene();
        parent = new GameObject("Trigger test parent");
        SceneManager.MoveGameObjectToScene(parent, preview);
        target = new GameObject("Trigger test volume");
        target.transform.SetParent(parent.transform, false);
        trigger = target.AddComponent<CycleZoneTrigger>();
        box = target.GetComponent<BoxCollider>();
        box.size = new Vector3(2, 4, 10);
    }

    [TearDown]
    public void TearDown() => EditorSceneManager.ClosePreviewScene(preview);

    [Test]
    public void RotatedVolumeRejectsPointInsideItsEnclosingWorldBox()
    {
        target.transform.rotation = Quaternion.Euler(0, 45, 0);
        Vector3 outside = target.transform.TransformPoint(new Vector3(2, 0, 0));
        Physics.SyncTransforms();
        Assert.That(box.bounds.Contains(outside), Is.True, "Reproduces the old AABB false positive.");
        Assert.That(trigger.ContainsWorldPoint(outside), Is.False);
        Assert.That(trigger.ContainsWorldPoint(target.transform.TransformPoint(new Vector3(.5f, 0, 4))), Is.True);
    }

    [Test]
    public void OffsetAndScaledParentUseTheAuthoredLocalBox()
    {
        parent.transform.SetPositionAndRotation(new Vector3(-90, -116, 100), Quaternion.Euler(0, 90, 0));
        parent.transform.localScale = new Vector3(2, 3, .5f);
        target.transform.localPosition = new Vector3(3, 1, -2);
        box.center = new Vector3(2, 1, 3);
        Assert.That(trigger.ContainsWorldPoint(target.transform.TransformPoint(box.center + new Vector3(.9f, 1.9f, 4.9f))), Is.True);
        Assert.That(trigger.ContainsWorldPoint(target.transform.TransformPoint(box.center + new Vector3(1.1f, 0, 0))), Is.False);
    }

    [Test]
    public void NegativeScaleKeepsTheSameLocalInclusionRule()
    {
        target.transform.localScale = new Vector3(-2, 1, -3);
        Assert.That(trigger.ContainsWorldPoint(target.transform.TransformPoint(new Vector3(.9f, 0, 4))), Is.True);
        Assert.That(trigger.ContainsWorldPoint(target.transform.TransformPoint(new Vector3(1.1f, 0, 0))), Is.False);
    }

    [Test]
    public void BoxFacesAreIncludedButOutsideIsRejected()
    {
        Assert.That(trigger.ContainsWorldPoint(new Vector3(1, 2, 5)), Is.True);
        Assert.That(trigger.ContainsWorldPoint(new Vector3(1.001f, 0, 0)), Is.False);
    }

    [Test]
    public void DisabledColliderCannotStartACycle()
    {
        box.enabled = false;
        Assert.That(trigger.ContainsWorldPoint(Vector3.zero), Is.False);
    }

    [Test]
    public void DisabledBehaviourCannotStartACycle()
    {
        trigger.enabled = false;
        Assert.That(trigger.ContainsWorldPoint(Vector3.zero), Is.False);
    }

    [Test]
    public void InactiveHierarchyCannotStartACycle()
    {
        parent.SetActive(false);
        Assert.That(trigger.ContainsWorldPoint(Vector3.zero), Is.False);
    }

    [Test]
    public void CollapsedTransformCannotContainAPlayer()
    {
        target.transform.localScale = new Vector3(1, 0, 1);
        Assert.That(trigger.ContainsWorldPoint(Vector3.zero), Is.False);
    }

    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(float.NegativeInfinity)]
    public void NonFinitePositionCannotStartACycle(float invalid)
    {
        Assert.That(trigger.ContainsWorldPoint(new Vector3(invalid, 0, 0)), Is.False);
    }
}
