using NUnit.Framework;
using UnityEngine;

public sealed class InteractionSelectionTests
{
    [TestCase(1f, 45, 4f, 120)]
    [TestCase(1f, 110, 4f, 120)]
    [TestCase(1f, 45, 4f, 90)]
    [TestCase(1f, 80, 4f, 110)]
    [TestCase(1f, -100, 1.01f, 1000)]
    public void CloserTargetAlwaysWinsRegardlessOfAuthoredPriority(float close, int closePriority, float far, int farPriority)
    {
        Assert.That(CharacterInteractionSelection.IsBetter(close, closePriority, 2, far, farPriority, 1), Is.True);
        Assert.That(CharacterInteractionSelection.IsBetter(far, farPriority, 1, close, closePriority, 2), Is.False);
    }

    [Test] public void EqualDistanceUsesPriorityThenStableInstanceId()
    {
        Assert.That(CharacterInteractionSelection.IsBetter(1, 120, 5, 1, 110, 1), Is.True);
        Assert.That(CharacterInteractionSelection.IsBetter(1, 110, 1, 1, 120, 5), Is.False);
        Assert.That(CharacterInteractionSelection.IsBetter(1, 110, 1, 1, 110, 5), Is.True);
        Assert.That(CharacterInteractionSelection.IsBetter(1, 110, 5, 1, 110, 1), Is.False);
    }

    [Test] public void WinnerDoesNotDependOnOverlapOrderOrGhostException()
    {
        float[] distances = { 4, 1, 9 }; int[] priorities = { 90, 45, 120 };
        foreach (var order in new[] { new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 },
            new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 } })
        {
            int best = order[0];
            foreach (int i in order)
                if (CharacterInteractionSelection.IsBetter(distances[i], priorities[i], i, distances[best], priorities[best], best)) best = i;
            Assert.That(best, Is.EqualTo(1));
        }
    }

    [Test] public void LargeTriggerDoesNotBecomeAZeroDistanceTarget()
    {
        var root = new GameObject("Portal access volume");
        try
        {
            root.transform.position = new Vector3(10000, 10000, 10003);
            var collider = root.AddComponent<BoxCollider>(); collider.isTrigger = true; collider.size = Vector3.one * 10;
            Vector3 player = root.transform.position - Vector3.forward * 3;
            Assert.That(CharacterInteractionSelection.SelectionPoint(collider, root.transform, player), Is.EqualTo(root.transform.position));
            Assert.That(CharacterInteractionSelection.SelectionPoint(collider, null, player), Is.EqualTo(collider.bounds.center));
            collider.isTrigger = false;
            Vector3 surface = root.transform.position - Vector3.forward;
            Assert.That(CharacterInteractionSelection.SelectionPoint(collider, root.transform, surface), Is.EqualTo(surface));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test] public void DenseOverlapDoesNotSilentlyDropCandidatesAndReusesCapacity()
    {
        var root = new GameObject("Dense interaction test"); root.transform.position = Vector3.one * 20000;
        try
        {
            for (int i = 0; i < 180; i++)
            {
                var child = new GameObject("Decoration " + i); child.transform.SetParent(root.transform, false);
                child.AddComponent<BoxCollider>().isTrigger = i % 2 == 0;
            }
            Physics.SyncTransforms();
            Collider[] buffer = new Collider[128];
            int count = CharacterInteractionSelection.CollectCandidates(ref buffer, root.transform.position, 2, ~0);
            Assert.That(count, Is.EqualTo(180)); Assert.That(buffer.Length, Is.GreaterThan(180));
            var grown = buffer;
            Assert.That(CharacterInteractionSelection.CollectCandidates(ref buffer, root.transform.position, 2, ~0), Is.EqualTo(180));
            Assert.That(buffer, Is.SameAs(grown));
        }
        finally { Object.DestroyImmediate(root); }
    }
}
