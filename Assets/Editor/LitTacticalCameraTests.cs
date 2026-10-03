using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Unity.Cinemachine;

public sealed class LitTacticalCameraTests
{
    [TestCase(TacticalObstacleMode.Sliding, false, false, TacticalObstacleMode.Sliding)]
    [TestCase(TacticalObstacleMode.VisibilityMask, false, false, TacticalObstacleMode.VisibilityMask)]
    [TestCase(TacticalObstacleMode.VisibilityMask, true, false, TacticalObstacleMode.Sliding)]
    [TestCase(TacticalObstacleMode.VisibilityMask, false, true, TacticalObstacleMode.Sliding)]
    [TestCase(TacticalObstacleMode.VisibilityMask, true, true, TacticalObstacleMode.Sliding)]
    public void ObstacleResolutionRestoresSlidingOutsideFollow(TacticalObstacleMode requested, bool free, bool cinematic, TacticalObstacleMode expected)
        => Assert.That(LitTacticalCameraProfile.ResolveObstacleMode(requested, free, cinematic), Is.EqualTo(expected));

    [Test] public void ProfileKeepsSlidingAsDefault()
    {
        var profile = ScriptableObject.CreateInstance<LitTacticalCameraProfile>();
        try { Assert.That(profile.obstacleMode, Is.EqualTo(TacticalObstacleMode.Sliding)); }
        finally { Object.DestroyImmediate(profile); }
    }

    [Test] public void ProtectedTargetBehindCameraDoesNotOpenMask()
    {
        var root = new GameObject("Projection test");
        try
        {
            var camera = root.AddComponent<Camera>();
            Assert.That(LitTacticalVisibilityMask.ProjectTarget(camera, Vector3.back * 5, 1).w, Is.Zero);
            Assert.That(LitTacticalVisibilityMask.ProjectTarget(camera, Vector3.forward * 5, 1).w, Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase(false)] [TestCase(true)]
    public void MaskRestoresBlocksWithoutChangingMaterialsCollidersOrOtherEffects(bool indexed)
    {
        var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Material material = null;
        try
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/CombatRealTime/Camera/LitTacticalMaskLit.shadergraph");
            Assert.That(shader, Is.Not.Null);
            material = new Material(shader);
            var renderer = root.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            var group = root.AddComponent<LitCameraOcclusionGroup>(); group.Configure(new[] { renderer });
            Assert.That(group.IsMaskCompatible(out string diagnostic), Is.True, diagnostic);
            var block = new MaterialPropertyBlock();
            int effect = Shader.PropertyToID("_LitIceFlameTest"), enabled = LitTacticalMaskProperties.Enabled;
            block.SetFloat(effect, .37f); block.SetFloat(enabled, .2f);
            if (indexed) renderer.SetPropertyBlock(block, 0); else renderer.SetPropertyBlock(block);
            var owner = new object();
            group.ApplyMask(owner, new LitTacticalMaskProperties.Data { target0 = new Vector4(.5f, .5f, 5, 1) });
            renderer.GetPropertyBlock(block, 0);
            Assert.That(block.GetFloat(effect), Is.EqualTo(.37f));
            Assert.That(block.GetFloat(enabled), Is.EqualTo(1));
            if (indexed) { block.SetFloat(effect, .7f); renderer.SetPropertyBlock(block, 0); }
            group.RestoreMask(owner);
            if (indexed) renderer.GetPropertyBlock(block, 0);
            else { renderer.GetPropertyBlock(block, 0); Assert.That(block.isEmpty, Is.True); renderer.GetPropertyBlock(block); }
            Assert.That(block.GetFloat(enabled), Is.EqualTo(.2f));
            Assert.That(block.GetFloat(effect), Is.EqualTo(indexed ? .7f : .37f));
            Assert.That(renderer.sharedMaterial, Is.SameAs(material));
            Assert.That(material.GetFloat(enabled), Is.Zero);
            Assert.That(root.GetComponent<Collider>().enabled, Is.True);
            Assert.That(renderer.forceRenderingOff, Is.False);
            group.ApplyMask(owner, default); group.enabled = false;
            if (indexed) renderer.GetPropertyBlock(block, 0); else renderer.GetPropertyBlock(block);
            Assert.That(block.GetFloat(enabled), Is.EqualTo(.2f));
        }
        finally { Object.DestroyImmediate(root); if (material != null) Object.DestroyImmediate(material); }
    }

    [Test] public void AnyUnsupportedMaterialSlotPreservesCollision()
    {
        var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Material compatible = null, unsupported = null;
        try
        {
            compatible = new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/CombatRealTime/Camera/LitTacticalMaskLit.shadergraph"));
            unsupported = new Material(Shader.Find("Hidden/InternalErrorShader"));
            var renderer = root.GetComponent<Renderer>();
            renderer.sharedMaterials = new[] { compatible, unsupported };
            var group = root.AddComponent<LitCameraOcclusionGroup>(); group.Configure(new[] { renderer });
            Assert.That(group.IsMaskCompatible(out string diagnostic), Is.False);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, "[TacticalMask] Glissement conservé pour " + root.name + ": " + diagnostic);
            Assert.That(group.CanBypassCameraCollision(), Is.False);
            Assert.That(group.CanBypassCameraCollision(), Is.False); // Only one warning per group.
            group.ApplyMask(this, default);
            var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block, 0);
            Assert.That(block.isEmpty, Is.True);
        }
        finally { Object.DestroyImmediate(root); if (compatible != null) Object.DestroyImmediate(compatible); if (unsupported != null) Object.DestroyImmediate(unsupported); }
    }

    [TestCase("Assets/CombatRealTime/Camera/LitTacticalMaskLit.shadergraph")]
    [TestCase("Assets/Materials/MasterShader/ShaderGraph_MasterShader.shadergraph")]
    [TestCase("Assets/Materials/IceShader/ShaderGraph_LitIceFrostedEdges_v3.shadergraph")]
    public void ReferenceAndProductionGraphsExposeDisabledMaskContract(string path)
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
        Assert.That(shader, Is.Not.Null);
        var material = new Material(shader);
        try
        {
            Assert.That(LitTacticalMaskProperties.Supports(material), Is.True);
            Assert.That(material.GetFloat(LitTacticalMaskProperties.Enabled), Is.Zero);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
        }
        finally { Object.DestroyImmediate(material); }
    }

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
            Assert.That(input.Camera.TacticalInspection.interactions, Is.Empty);
            Assert.That(input.asset.FindAction("Camera/ToggleFreeCamera", false), Is.Null);
            Assert.That(input.Camera.TacticalInspection.bindings[0].path, Is.EqualTo("<Gamepad>/leftStickPress"));
        }
        finally { input.Dispose(); }
    }

    [Test] public void FreeCameraIsExplicitAndFollowIsRestored()
    {
        var view = new LitTacticalUccViewType();
        Assert.That(view.Following, Is.True);
        view.SetFreeCamera(true);
        Assert.That(view.Following, Is.False);
        view.SetFreeCamera(false);
        Assert.That(view.Following, Is.True);
    }
    [Test] public void FreeCameraLimitIncludesZoomAndHeight()
    {
        Vector3 player = new Vector3(5, 2, 8);
        Vector3 camera = LitTacticalCameraMath.ClampCameraDistance(player + new Vector3(30, 40, 0), player, 24);
        Assert.That(Vector3.Distance(camera, player), Is.EqualTo(24).Within(.0001f));
    }
    [Test] public void BlockedMotionSlidesAlongWall()
    {
        Vector3 slide = LitTacticalCameraMath.SlideMotion(new Vector3(2, 0, 3), Vector3.left);
        Assert.That(slide, Is.EqualTo(new Vector3(0, 0, 3)));
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
        {
            Assert.That(new SerializedObject(group).FindProperty("renderers").arraySize, Is.GreaterThan(0));
            Assert.That(group.IsMaskCompatible(out string diagnostic), Is.True, diagnostic);
        }
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
