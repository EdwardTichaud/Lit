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

    [TestCase(false)]
    [TestCase(true)]
    public void OversizedPortalColliderCannotGrantRemoteUse(bool trigger)
    {
        portalRoot.transform.position = origin + Vector3.forward * 30;
        var box = portalRoot.GetComponent<BoxCollider>();
        box.isTrigger = trigger;
        box.size = Vector3.one * 100;
        Physics.SyncTransforms();
        Assert.That(Evaluate(), Is.False, "Being inside a detection volume is not being near the portal.");
        Assert.That(CanUseAuthoritatively(), Is.False);
        portal.SetDetectedCharacter(player);
        Assert.That(portal.ServerTryUse(player, out _, out _), Is.False);
    }

    [Test]
    public void MovingAwayImmediatelyInvalidatesCachedPortal()
    {
        Assert.That(CanUseAuthoritatively(), Is.True);
        portal.SetDetectedCharacter(player);
        player.transform.position += Vector3.right * 50;
        Physics.SyncTransforms();
        Assert.That(CanUseAuthoritatively(), Is.False);
        Assert.That(portal.ServerTryUse(player, out _, out _), Is.False);
    }

    [Test]
    public void AuthoredPortalAnchorDefinesUsePointInsteadOfOversizedVisualRoot()
    {
        var anchor = new GameObject("Portal use anchor");
        anchor.transform.SetParent(portalRoot.transform);
        anchor.transform.position = origin + Vector3.forward;
        typeof(PortalController).GetField("interactionAnchor", Private).SetValue(portal, anchor.transform);
        portalRoot.transform.position += Vector3.forward * 40;
        portalRoot.GetComponent<BoxCollider>().size = Vector3.one * 100;
        Physics.SyncTransforms();
        Assert.That(CanUseAuthoritatively(), Is.False);
        anchor.transform.position = origin + Vector3.forward;
        Assert.That(CanUseAuthoritatively(), Is.True);
        Assert.That(Evaluate(), Is.True);
    }

    [Test]
    public void DisabledPortalOrColliderCannotBeUsedAuthoritatively()
    {
        Assert.That(CanUseAuthoritatively(), Is.True);
        portalRoot.GetComponent<BoxCollider>().enabled = false;
        Assert.That(CanUseAuthoritatively(), Is.False);
        Assert.That(Evaluate(), Is.False);
        portalRoot.GetComponent<BoxCollider>().enabled = true;
        portal.enabled = false;
        Assert.That(CanUseAuthoritatively(), Is.False);
    }

    [Test]
    public void GenericTriggerUsesAnchorDistanceAndNotContainingVolume()
    {
        var box = portalRoot.GetComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = Vector3.one * 100;
        portalRoot.transform.position = origin + Vector3.forward * 30;
        Physics.SyncTransforms();
        Assert.That(CharacterInteractionDetection.IsCharacterWithinRange(player.transform, box, portalRoot.transform, 2), Is.False);
        Assert.That(CharacterInteractionDetection.GetInteractionRangePoint(box, portalRoot.transform, origin), Is.EqualTo(portalRoot.transform.position));
        portalRoot.transform.position = origin + Vector3.forward;
        Physics.SyncTransforms();
        Assert.That(CharacterInteractionDetection.IsCharacterWithinRange(player.transform, box, portalRoot.transform, 2), Is.True);
    }

    [Test]
    public void ConcaveMeshEmptyBoundsDoNotCreateAnInteractionSurface()
    {
        var meshRoot = new GameObject("Sparse concave interaction mesh");
        var mesh = new Mesh();
        try
        {
            meshRoot.transform.position = origin;
            mesh.vertices = new[] { new Vector3(-20,0,-20),new Vector3(20,0,-20),new Vector3(20,0,20) };
            mesh.triangles = new[] { 0,2,1 };
            var collider = meshRoot.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            Vector3 emptyPoint = origin + new Vector3(-19,0,19);
            player.transform.position = emptyPoint - Vector3.up * .9f;
            Physics.SyncTransforms();
            Assert.That(collider.bounds.Contains(emptyPoint), Is.True);
            Assert.That(CharacterInteractionDetection.IsCharacterWithinRange(player.transform,collider,meshRoot.transform,2), Is.False);
        }
        finally { Object.DestroyImmediate(meshRoot); Object.DestroyImmediate(mesh); }
    }

    [Test]
    public void InteractRefreshesInvalidCachedSelectionBeforeDispatch()
    {
        Transform previous = LocalPlayerContext.LocalCharacterRoot;
        var authority = LocalPlayerContext.LocalCharacterAuthority;
        string source = LocalPlayerContext.LocalCharacterSource;
        float debounce = LocalInputRouter.InputDebounceSeconds;
        bool dispatched = false;
        System.Action<UnityEngine.InputSystem.InputAction.CallbackContext> observer = _ =>
        {
            dispatched = true;
            Assert.That(RuntimeOutlineSelectionManager.ActiveInteractable, Is.Null);
            Assert.That(typeof(SquadCharacterController).GetField("currentDetectedInteractable", Private).GetValue(controller), Is.Null);
        };
        try
        {
            LocalPlayerContext.SetLocalCharacter(player.transform, "Interaction range test", authority);
            LocalInputRouter.InputDebounceSeconds = 0;
            typeof(SquadCharacterController).GetMethod("ApplyLocalInteractionTarget", Private).Invoke(controller, new object[] { portal });
            Assert.That(RuntimeOutlineSelectionManager.ActiveInteractable, Is.SameAs(portal));
            player.transform.position += Vector3.right * 50;
            LocalInputRouter.PrioritizedInteract += observer;
            typeof(LocalInputRouter).GetMethod("RaiseInteract", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { default(UnityEngine.InputSystem.InputAction.CallbackContext) });
            Assert.That(dispatched, Is.True);
        }
        finally
        {
            LocalInputRouter.PrioritizedInteract -= observer;
            LocalInputRouter.InputDebounceSeconds = debounce;
            RuntimeOutlineSelectionManager.Clear(controller);
            if (previous != null) LocalPlayerContext.SetLocalCharacter(previous, source, authority);
            else LocalPlayerContext.Clear("Interaction range test", authority);
        }
    }
    [TestCase("Assets/Interactive/Portal/Portal_1.prefab")]
    [TestCase("Assets/Interactive/Portal/Portal_2.prefab")]
    public void ProductionPortalPrefabsRespectAnchorRange(string path)
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null);
        var instance = Object.Instantiate(prefab);
        try
        {
            instance.transform.position = origin + Vector3.forward * 1.5f;
            var actual = instance.GetComponent<PortalController>();
            Assert.That(actual, Is.Not.Null);
            // Destination assignment normally comes from the scene; keep this
            // geometry/range fixture independent of loaded destination scenes.
            typeof(PortalController).GetField("destinationMode", Private).SetValue(actual, PortalController.DestinationMode.LocalTeleport);
            typeof(PortalController).GetField("destinationPoints", Private).SetValue(actual, new List<Transform> { instance.transform });
            Physics.SyncTransforms();
            Assert.That(actual.CanBeDetectedBy(controller), Is.True, "Configured portal remains usable nearby.");
            Assert.That((bool)typeof(PortalController).GetMethod("CanUse", Private)
                .Invoke(actual, new object[] { player, false, .35f }), Is.True);
            instance.transform.localScale *= 50;
            instance.transform.position = origin + Vector3.forward * 40;
            Physics.SyncTransforms();
            Assert.That(actual.CanBeDetectedBy(controller), Is.False);
            Assert.That(actual.ServerTryUse(player, out _, out _), Is.False);
        }
        finally { Object.DestroyImmediate(instance); }
    }
    private bool CanUseAuthoritatively()
    {
        return (bool)typeof(PortalController).GetMethod("CanUse", Private)
            .Invoke(portal, new object[] { player, false, .35f });
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
