#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// Native EditMode branch test. The opaque overlay/camera snapshot is seeded;
// no rendered-frame wait or actual scene loading is simulated as having passed.
public sealed class GameFlowDepartureFailureTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private Scene fixture;
    private GameObject root;
    private GameFlowService flow;
    private WorldStateManager world;
    private LoadingScreenService loading;
    private CanvasGroup overlay;
    private Camera camera;
    private ZoneManifest destination;
    private object previousWorld, previousLoading;
    private FieldInfo worldSingleton, loadingSingleton;
    private DepartureFailingProvider provider;
    private ZoneRuntimeContext presentation;

    [SetUp]
    public void SetUp()
    {
        fixture = EditorSceneManager.NewPreviewScene();
        fixture.name = "DepartureFailure_" + Guid.NewGuid().ToString("N");
        root = new GameObject("Owned departure fixture");
        root.SetActive(false);
        SceneManager.MoveGameObjectToScene(root, fixture);
        flow = root.AddComponent<GameFlowService>();
        presentation = root.AddComponent<ZoneRuntimeContext>();
        Set(flow, "activeZoneRuntimeContext", presentation);
        var registry = root.AddComponent<NetworkObjectRegistry>();
        world = root.AddComponent<WorldStateManager>();
        Set(world, "registry", registry);
        worldSingleton = typeof(WorldStateManager).GetField("<Instance>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static);
        previousWorld = worldSingleton.GetValue(null); worldSingleton.SetValue(null, world);

        var broken = new GameObject("Uncapturable departure object");
        broken.SetActive(false); broken.transform.SetParent(root.transform, false);
        var persistent = broken.AddComponent<PersistentNetworkObject>();
        persistent.AssignSceneIdentity("scene:departure:uncapturable");
        provider = broken.AddComponent<DepartureFailingProvider>();
        registry.Register(persistent);
        Set(flow, "activeGameplaySceneName", fixture.name);
        ((List<string>)Get(flow, "loadedZoneSceneNames")).Add(fixture.name);

        var ui = new GameObject("Opaque loading fixture", typeof(RectTransform), typeof(CanvasGroup));
        ui.transform.SetParent(root.transform, false);
        loading = ui.AddComponent<LoadingScreenService>();
        overlay = ui.GetComponent<CanvasGroup>(); overlay.alpha = 1; overlay.blocksRaycasts = true;
        Set(loading, "canvasGroup", overlay); Set(loading, "currentFadeTarget", 1f);
        loadingSingleton = typeof(LoadingScreenService).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);
        previousLoading = loadingSingleton.GetValue(null); loadingSingleton.SetValue(null, loading);
        var cameraObject = new GameObject("Suspended gameplay camera");
        cameraObject.transform.SetParent(root.transform, false);
        camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
        Type stateType = typeof(LoadingScreenService).GetNestedType("SuspendedCameraState", BindingFlags.NonPublic);
        object state = Activator.CreateInstance(stateType);
        stateType.GetField("camera").SetValue(state, camera); stateType.GetField("wasEnabled").SetValue(state, true);
        ((IList)Get(loading, "suspendedCameras")).Add(state);
        destination = ScriptableObject.CreateInstance<ZoneManifest>();
    }

    [TearDown]
    public void TearDown()
    {
        if (fixture.IsValid()) EditorSceneManager.ClosePreviewScene(fixture);
        if (destination != null) Object.DestroyImmediate(destination);
        worldSingleton?.SetValue(null, previousWorld);
        loadingSingleton?.SetValue(null, previousLoading);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RejectedCaptureKeepsTheDepartureZoneVisibleAndUsable(bool returnToHub)
    {
        IEnumerator routine = (IEnumerator)typeof(GameFlowService).GetMethod(returnToHub ? "ReturnToHubRoutine" : "TravelToZoneRoutine", Private)
            .Invoke(flow, returnToHub ? new object[] { null } : new object[] { destination, null });
        // Seeded state represents the first yield having presented an opaque
        // overlay. EditMode cannot prove WaitForEndOfFrame presentation.
        Assert.That(routine.MoveNext(), Is.True);
        LogAssert.Expect(LogType.Error, new Regex("capture provider failed persistentId='scene:departure:uncapturable'"));
        LogAssert.Expect(LogType.Log, new Regex(@"\[SceneTransition\].*Depart annule ; zone conservee"));
        Drain(routine);
        Assert.That(provider.CaptureCount, Is.EqualTo(1), "The real capture must reject the provider; recovery must not bypass or repeat it.");
        Assert.That(overlay.alpha, Is.Zero, "A rejected capture must reveal the intact departure world.");
        Assert.That(overlay.blocksRaycasts, Is.False, "The loading screen must release pointer input.");
        Assert.That(camera.enabled, Is.True, "The loading screen must restore the camera snapshot.");
        Assert.That(flow.IsTransitioning, Is.False);
        Assert.That((List<string>)Get(flow, "loadedZoneSceneNames"), Is.EqualTo(new[] { fixture.name }));
        Assert.That(Get(flow, "activeGameplaySceneName"), Is.EqualTo(fixture.name));
        Assert.That(Get(flow, "activeZoneRuntimeContext"), Is.SameAs(presentation), "A refused capture must not release the original presentation.");
        Assert.That(world != null && root != null, Is.True, "The live session must not be torn down or saved partially.");
        LogAssert.NoUnexpectedReceived();
    }

    private static void Drain(IEnumerator routine)
    {
        int budget = 100;
        while (routine.MoveNext())
        {
            Assert.That(--budget, Is.GreaterThan(0), "Unexpected unbounded wait in rejection branch.");
            if (routine.Current is IEnumerator child) Drain(child);
        }
    }
    private static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
}

public sealed class DepartureFailingProvider : MonoBehaviour, IPersistentStateProvider
{
    public int CaptureCount;
    public string ProviderId => "departure_failure_fixture";
    public byte[] CaptureState(PersistentStateContext context)
    {
        CaptureCount++;
        throw new InvalidOperationException("Intentional departure capture failure.");
    }
    public void ApplyState(byte[] data, PersistentApplyPhase phase, PersistentStateContext context) { }
}
#endif
