using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Unity.Cinemachine;

public sealed class LitTacticalCameraTests
{
    [TestCase(GameplayCameraMode.ThirdPerson, false, typeof(LitSmoothAdventureViewType))]
    [TestCase(GameplayCameraMode.ThirdPerson, true, typeof(CombatLockAdventureViewType))]
    [TestCase(GameplayCameraMode.Tactical, false, typeof(LitTacticalUccViewType))]
    [TestCase(GameplayCameraMode.Tactical, true, typeof(LitTacticalUccViewType))]
    public void ViewResolutionPreservesFreeTacticalLock(GameplayCameraMode mode, bool locked, System.Type expected)
        => Assert.That(LitGameplayCameraModeController.ResolveView(mode, locked), Is.EqualTo(expected));

    [TestCase(GameplayCameraMode.ThirdPerson)] [TestCase(GameplayCameraMode.Tactical)]
    public void CinematicAuthorityOverridesBothGameplayModes(GameplayCameraMode mode)
        => Assert.That(LitGameplayCameraModeController.ResolveView(mode, true, true), Is.Null);

    [Test] public void InspectionBindingReplacesLegacyCameraGesture()
    {
        var input = new PlayerInputs();
        try
        {
            Assert.That(input.Camera.TacticalInspection.interactions, Does.Contain("duration=0.6"));
            Assert.That(input.asset.FindAction("Camera/ToggleFreeCamera", false), Is.Null);
            Assert.That(input.Camera.TacticalInspection.bindings[0].path, Is.EqualTo("<Gamepad>/leftStickPress"));
        }
        finally { input.Dispose(); }
    }

    [Test] public void PanClampDoesNotAlterHeight()
    {
        Vector3 actual = LitTacticalCameraMath.ClampPivot(new Vector3(100, 7, 0), Vector3.zero, 20);
        Assert.That(actual, Is.EqualTo(new Vector3(20, 7, 0)));
    }
    [Test] public void SoftBoundarySlowsOutwardMotionButAllowsReturn()
    {
        Assert.That(LitTacticalCameraMath.SlowOutwardPan(Vector3.right, Vector3.right * 18, 20, 4).x, Is.EqualTo(.5f));
        Assert.That(LitTacticalCameraMath.SlowOutwardPan(Vector3.left, Vector3.right * 20, 20, 4), Is.EqualTo(Vector3.left));
    }
    [Test] public void InspectionEnablesOnlyCameraMap()
    {
        var method = typeof(InputModeCoordinator).GetMethod("GetMaps", BindingFlags.Static | BindingFlags.NonPublic);
        CollectionAssert.AreEqual(new[] { "Camera" }, (IEnumerable)method.Invoke(null, new object[] { InputMode.TacticalInspection }));
    }
    [TestCase(false)] [TestCase(true)]
    public void OcclusionRestoresPriorRendererState(bool originallyHidden)
    {
        var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            var renderer = root.GetComponent<Renderer>();
            renderer.forceRenderingOff = originallyHidden;
            var group = root.AddComponent<LitCameraOcclusionGroup>();
            group.Configure(new[] { renderer });
            var a = new object(); var b = new object();
            group.SetHidden(a, true); group.SetHidden(b, true);
            Assert.That(renderer.forceRenderingOff, Is.True);
            group.SetHidden(a, false);
            Assert.That(renderer.forceRenderingOff, Is.True);
            group.SetHidden(b, false);
            Assert.That(renderer.forceRenderingOff, Is.EqualTo(originallyHidden));
            group.SetHidden(a, true); group.enabled = false;
            Assert.That(renderer.forceRenderingOff, Is.EqualTo(originallyHidden));
        }
        finally { Object.DestroyImmediate(root); }
    }
    [Test] public void BootstrapCameraRemainsSerializedAdventureAndBrainDisabled()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Core/System/GameplaySessionRoot.prefab");
        var selector = prefab.GetComponentInChildren<LitGameplayCameraModeController>(true);
        Assert.That(selector, Is.Not.Null);
        var data = new SerializedObject(selector);
        Assert.That(data.FindProperty("initialMode").enumValueIndex, Is.Zero);
        Assert.That(data.FindProperty("profile").objectReferenceValue, Is.Not.Null);
        Assert.That(selector.GetComponent<CinemachineBrain>().enabled, Is.False);
        var ucc = new SerializedObject(selector.GetComponent<Opsive.UltimateCharacterController.Camera.CameraController>());
        Assert.That(ucc.FindProperty("m_ViewTypeFullName").stringValue, Does.EndWith(".Adventure"));
    }
    [Test] public void TestRoomHasExplicitOccludersAndBounds()
    {
        var room = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CombatRealTime/Camera/TacticalCameraTestRoom.prefab");
        Assert.That(room.GetComponent<LitTacticalBounds>(), Is.Not.Null);
        Assert.That(room.GetComponent<BoxCollider>().isTrigger, Is.True);
        Assert.That(room.GetComponentsInChildren<LitCameraOcclusionGroup>().Length, Is.EqualTo(6));
        foreach (var group in room.GetComponentsInChildren<LitCameraOcclusionGroup>())
            Assert.That(new SerializedObject(group).FindProperty("renderers").arraySize, Is.GreaterThan(0));
    }
    [Test] public void CursorLeaseRestoresStateAndKeepsUiPriority()
    {
        bool visible = Cursor.visible;
        CursorLockMode locked = Cursor.lockState;
        var gameplay = new object(); var ui = new object();
        try
        {
            LitSystemCursorLease.Acquire(ui, false, CursorLockMode.None, 100);
            LitSystemCursorLease.Acquire(gameplay, true, CursorLockMode.None);
            Assert.That(Cursor.visible, Is.False);
            LitSystemCursorLease.Release(ui);
            Assert.That(Cursor.visible, Is.True);
            LitSystemCursorLease.Release(gameplay);
            Assert.That(Cursor.visible, Is.EqualTo(visible));
            Assert.That(Cursor.lockState, Is.EqualTo(locked));
        }
        finally { LitSystemCursorLease.Release(ui); LitSystemCursorLease.Release(gameplay); }
    }
}
