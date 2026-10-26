using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class LitTacticalMaskStackTests
{
    private readonly List<GameObject> roots = new List<GameObject>();
    private Material material;
    private static readonly Vector3 Origin = new Vector3(12000, 12000, 12000);
    [SetUp] public void Setup() => material = new Material(Resources.Load<Material>("MaisonVisibilityNativeLit"));
    [TearDown] public void Cleanup()
    {
        foreach (var root in roots) if (root != null) Object.DestroyImmediate(root);
        roots.Clear(); if (material != null) Object.DestroyImmediate(material);
    }
    private LitCameraOcclusionGroup Group(Vector3 position, bool overhead = false)
    {
        var root = GameObject.CreatePrimitive(PrimitiveType.Cube); roots.Add(root);
        root.layer = 30; root.transform.position = position; root.transform.localScale = Vector3.one * .2f;
        Object.DestroyImmediate(root.GetComponent<Collider>()); // Visual detection must not need one.
        root.GetComponent<Renderer>().sharedMaterial = material;
        var group = root.AddComponent<LitCameraOcclusionGroup>(); group.Configure(new[] { root.GetComponent<Renderer>() }, overhead);
        return group;
    }

    [Test] public void EveryForegroundLayerIsCollectedIncludingManualColliderlessGroups()
    {
        var root = new GameObject("Stack camera"); roots.Add(root); var camera = root.AddComponent<Camera>();
        camera.cullingMask = 1 << 30; root.transform.position = Origin; camera.aspect = 1;
        var a = Group(Origin + Vector3.forward * 2); var b = Group(Origin + Vector3.forward * 4); var c = Group(Origin + Vector3.forward * 6);
        var behind = Group(Origin + Vector3.forward * 12); var outside = Group(Origin + Vector3.right * 50 + Vector3.forward * 5);
        var found = new List<LitCameraOcclusionGroup>();
        LitCameraOcclusionGroup.Collect(camera, Origin + Vector3.forward * 10, .2f, Origin.y + .25f, found);
        CollectionAssert.AreEquivalent(new[] { a, b, c }, found);
        CollectionAssert.DoesNotContain(found, behind); CollectionAssert.DoesNotContain(found, outside);
        var stack = new LitTacticalMaskStack(); foreach (var group in found) stack.Mark(group, false, 0);
        stack.Advance(0, .1f, .1f, .15f, false);
        foreach (var layer in stack.Layers.Values) Assert.That(layer.playerStrength, Is.EqualTo(1));
    }

    [Test] public void ANewLayerInheritsTheExistingOpeningInsteadOfHidingThePlayerAgain()
    {
        var first = Group(Origin); var second = Group(Origin + Vector3.forward); var third = Group(Origin + Vector3.forward * 2);
        var stack = new LitTacticalMaskStack(); stack.Mark(first, false, 0); stack.Advance(0, .1f, .1f, .15f, false);
        stack.Mark(second, false, .01f); stack.Mark(third, false, .01f);
        Assert.That(stack.Layers[second].playerStrength, Is.EqualTo(1));
        Assert.That(stack.Layers[third].playerStrength, Is.EqualTo(1));
        stack.Mark(third, true, .01f); stack.Advance(.01f, .1f, .1f, .15f, true);
        Assert.That(stack.Layers[third].enemyStrength, Is.EqualTo(1));
        Assert.That(stack.Layers[first].enemyStrength, Is.Zero);
        stack.ResetEnemy(); Assert.That(stack.Layers[third].enemyStrength, Is.Zero);
        Assert.That(stack.PlayerReveal, Is.EqualTo(1));
        stack.Advance(1, .1f, .1f, .15f, false); Assert.That(stack.Layers.Count, Is.Zero);
    }

    [Test] public void UpperFloorsAreCollectedButTheSupportingFloorIsPreserved()
    {
        var root = new GameObject("Floor camera"); roots.Add(root); var camera = root.AddComponent<Camera>();
        camera.cullingMask = 1 << 30; camera.aspect = 1; root.transform.position = Origin + new Vector3(0, 3, -10);
        var target = Origin + Vector3.up; root.transform.LookAt(target);
        var upper = Group(Origin + new Vector3(0, 2, -2), true);
        var support = Group(Origin + new Vector3(0, -.5f, -2), true);
        var found = new List<LitCameraOcclusionGroup>();
        LitCameraOcclusionGroup.Collect(camera, target, .45f, Origin.y + .25f, found);
        CollectionAssert.Contains(found, upper); CollectionAssert.DoesNotContain(found, support);
        var block = new MaterialPropertyBlock();
        upper.ApplyMask(this, new LitTacticalMaskProperties.Data { cameraPosition = new Vector4(0, 0, 0, Origin.y + .25f) });
        upper.GetComponent<Renderer>().GetPropertyBlock(block, 0);
        Assert.That(block.GetFloat(LitTacticalMaskProperties.Enabled), Is.EqualTo(2));
        upper.RestoreMask(this);
    }

    [Test] public void SaturatedRayAndOverlapQueriesReturnTheEntireStackAndReuseTheirBuffers()
    {
        var root = new GameObject("150 collider layers"); roots.Add(root); root.transform.position = Origin;
        for (int i = 0; i < 150; i++)
        {
            var child = new GameObject("Layer " + i); child.layer = 30; child.transform.SetParent(root.transform, false);
            child.transform.localPosition = Vector3.forward * (.1f + i * .1f);
            child.AddComponent<BoxCollider>().size = Vector3.one * .04f;
        }
        Physics.SyncTransforms();
        var hits = new RaycastHit[128]; var overlaps = new Collider[64];
        Assert.That(LitTacticalMaskQueries.Raycast(ref hits, Origin, Vector3.forward, 20, 1 << 30), Is.EqualTo(150));
        Assert.That(LitTacticalMaskQueries.Overlap(ref overlaps, Origin, 20, 1 << 30), Is.EqualTo(150));
        var oldHits = hits; var oldOverlaps = overlaps;
        Assert.That(LitTacticalMaskQueries.Raycast(ref hits, Origin, Vector3.forward, 20, 1 << 30), Is.EqualTo(150));
        Assert.That(LitTacticalMaskQueries.Overlap(ref overlaps, Origin, 20, 1 << 30), Is.EqualTo(150));
        Assert.That(hits, Is.SameAs(oldHits)); Assert.That(overlaps, Is.SameAs(oldOverlaps));
    }

    [TestCase(false)] [TestCase(true)]
    public void OverlappingGroupsCannotLeaveAnotherLayersMaskOnTheRenderer(bool reverse)
    {
        var child = Group(Origin); var renderer = child.GetComponent<Renderer>();
        var root = new GameObject("Authored parent group"); roots.Add(root);
        var parent = root.AddComponent<LitCameraOcclusionGroup>(); parent.Configure(new[] { renderer });
        var first = reverse ? parent : child; var second = reverse ? child : parent;
        var data = new LitTacticalMaskProperties.Data { target0 = new Vector4(.5f, .5f, 10, 1) };
        first.ApplyMask(this, data); second.ApplyMask(this, data);
        first.RestoreMask(this); second.RestoreMask(this);
        var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block, 0);
        Assert.That(block.GetFloat(LitTacticalMaskProperties.Enabled), Is.Zero);
        first.ApplyMask(this, data); first.RestoreMask(this);
        renderer.GetPropertyBlock(block, 0);
        Assert.That(block.GetFloat(LitTacticalMaskProperties.Enabled), Is.Zero);
    }
}
