using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class CharacterInteractionDetectionTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject player;
    private GameObject portalRoot;
    private GameObject cameraRoot;
    private GameObject wall;
    private SquadCharacterController controller;
    private PortalController portal;
    private Vector3 origin;

    [SetUp]
    public void Setup()
    {
        player = new GameObject("Interaction player test");
        player.SetActive(false);
        player.transform.position = Vector3.one * 10000;
        controller = player.AddComponent<SquadCharacterController>();
        origin = player.transform.position + Vector3.up * 0.9f;

        portalRoot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        portalRoot.SetActive(false);
        portalRoot.transform.position = origin + Vector3.forward * 1.5f;
        portal = portalRoot.AddComponent<PortalController>();
        typeof(PortalController).GetField("destinationPoints", Private)
            .SetValue(portal, new List<Transform> { portalRoot.transform });
        portalRoot.SetActive(true);
        Physics.SyncTransforms();
    }

    [TearDown]
    public void Cleanup()
    {
        Object.DestroyImmediate(wall);
        Object.DestroyImmediate(cameraRoot);
        Object.DestroyImmediate(portalRoot);
        Object.DestroyImmediate(player);
    }

    [Test]
    public void NearbyPortalRemainsUsableWithoutCameraOrVisibleRenderer()
    {
        portalRoot.GetComponent<Renderer>().enabled = false;
        Assert.That(Evaluate(), Is.True);

        cameraRoot = new GameObject("Unrelated camera test", typeof(Camera));
        var camera = cameraRoot.GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.cullingMask = 0;
        camera.farClipPlane = 1;
        cameraRoot.transform.position = origin + Vector3.back * 100;
        cameraRoot.transform.rotation = Quaternion.Euler(0, 180, 0);
        Assert.That(Evaluate(), Is.True);

        cameraRoot.transform.position = origin + Vector3.right * 50;
        cameraRoot.transform.rotation = Quaternion.Euler(0, 90, 0);
        Assert.That(Evaluate(), Is.True);
    }

    [Test]
    public void WallBetweenPlayerAndPortalStillBlocksInteraction()
    {
        wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = origin + Vector3.forward * 0.6f;
        wall.transform.localScale = new Vector3(4, 4, 0.2f);
        Physics.SyncTransforms();
        Assert.That(Evaluate(), Is.False);
    }

    [Test]
    public void PortalOutsidePlayerRangeOrBehindPlayerIsRejected()
    {
        portalRoot.transform.position = origin + Vector3.forward * 10;
        Physics.SyncTransforms();
        Assert.That(Evaluate(), Is.False);

        portalRoot.transform.position = origin + Vector3.back * 1.5f;
        Physics.SyncTransforms();
        Assert.That(Evaluate(), Is.False);
    }

    private bool Evaluate()
    {
        var method = typeof(SquadCharacterController).GetMethod("TryEvaluateInteractionCandidate", Private);
        object[] arguments = { portal, origin, Vector3.forward, false, float.PositiveInfinity };
        return (bool)method.Invoke(controller, arguments);
    }

    [TestCase(false)] [TestCase(true)]
    public void CloserPortalWinsOverHigherPriorityPortalInEitherCandidateOrder(bool reverse)
    {
        GameObject fartherRoot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            fartherRoot.SetActive(false);
            fartherRoot.transform.position = origin + new Vector3(-1.2f, 0, 1.8f);
            var farther = fartherRoot.AddComponent<PortalController>();
            typeof(PortalController).GetField("destinationPoints", Private).SetValue(farther, new List<Transform> { fartherRoot.transform });
            typeof(PortalController).GetField("interactionPriority", Private).SetValue(farther, 1000);
            portalRoot.transform.position = origin + new Vector3(.6f, 0, 1);
            fartherRoot.SetActive(true); Physics.SyncTransforms();
            object[] farArguments = { farther, origin, Vector3.forward, false, float.PositiveInfinity };
            Assert.That((bool)typeof(SquadCharacterController).GetMethod("TryEvaluateInteractionCandidate", Private)
                .Invoke(controller, farArguments), Is.True, "The farther target must be eligible for this priority regression test.");
            var candidates = (List<ICharacterDetectedInteractable>)typeof(SquadCharacterController)
                .GetField("interactionDetectionCandidates", Private).GetValue(controller);
            candidates.Add(reverse ? farther : portal); candidates.Add(reverse ? portal : farther);
            var best = typeof(SquadCharacterController).GetMethod("SelectBestInteractionTarget", Private).Invoke(controller, new object[] { origin });
            Assert.That(best, Is.SameAs(portal));
        }
        finally { Object.DestroyImmediate(fartherRoot); }
    }

    [Test] public void PortalWithoutOutlineRenderersRetainsDetectorOwnership()
    {
        RuntimeOutlineSelectionManager.Clear(RuntimeOutlineSelectionManager.ActiveOwner);
        try
        {
            // A valid selected component can temporarily have no drawable outline.
            var renderer = portalRoot.GetComponent<Renderer>(); Object.DestroyImmediate(renderer);
            RuntimeOutlineSelectionManager.SetActiveInteractable(controller, portal);
            Assert.That(RuntimeOutlineSelectionManager.ActiveOwner, Is.SameAs(controller));
            Assert.That(RuntimeOutlineSelectionManager.ActiveInteractable, Is.SameAs(portal));
            RuntimeOutlineSelectionManager.RefreshActiveInteractable();
            RuntimeOutlineSelectionManager.SetActiveInteractable((ICharacterDetectedInteractable)null);
            RuntimeOutlineSelectionManager.Clear();
            Assert.That(RuntimeOutlineSelectionManager.ActiveOwner, Is.SameAs(controller));
            Assert.That(RuntimeOutlineSelectionManager.ActiveInteractable, Is.SameAs(portal));
            RuntimeOutlineSelectionManager.Clear(controller);
            Assert.That(RuntimeOutlineSelectionManager.ActiveInteractable, Is.Null);
        }
        finally { RuntimeOutlineSelectionManager.Clear(controller); }
    }
}
