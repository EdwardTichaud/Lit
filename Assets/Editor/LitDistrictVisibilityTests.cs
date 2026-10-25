using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class LitDistrictVisibilityTests
{
    [Test] public void RuntimeShaderResourcesAreIncludedAndCompatible()
    {
        foreach (string resource in new[] { "MaisonVisibilityNativeLit", "DistrictVisibilityAutodesk" })
        {
            var template = Resources.Load<Material>(resource);
            Assert.That(template, Is.Not.Null, resource);
            Assert.That(LitTacticalMaskProperties.Supports(template), Is.True, resource);
        }
    }
    [Test] public void AutodeskAdapterPreservesOriginalContractTexturesAndCompiles()
    {
        string pipeline = Shader.globalRenderPipeline;
        Shader.globalRenderPipeline = "HDRenderPipeline";
        Material source = null, copy = null; Texture2D texture = null;
        try
        {
            var original = AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipelineResources/ShaderGraph/AutodeskInteractive.shadergraph");
            Assert.That(original, Is.Not.Null);
            Assert.That(LitMaisonVisibilityZone.IsAutodesk(original), Is.True, original.name);
            source = new Material(original); texture = new Texture2D(2, 2);
            for (int i = 0; i < original.GetPropertyCount(); i++)
                if (original.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Texture &&
                    original.GetPropertyTextureDimension(i) == UnityEngine.Rendering.TextureDimension.Tex2D)
                    source.SetTexture(original.GetPropertyName(i), texture);
            copy = LitMaisonVisibilityZone.CreateMaskMaterial(source);
            Assert.That(copy.shader.name, Is.EqualTo("Lit/Camera/LitDistrictAutodeskMask"));
            Assert.That(LitTacticalMaskProperties.Supports(copy), Is.True);
            for (int i = 0; i < original.GetPropertyCount(); i++)
            {
                string name = original.GetPropertyName(i);
                Assert.That(copy.HasProperty(name), Is.True, name);
                if (original.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Texture &&
                    original.GetPropertyTextureDimension(i) == UnityEngine.Rendering.TextureDimension.Tex2D)
                    Assert.That(copy.GetTexture(name), Is.SameAs(texture), name);
            }
            Assert.That(copy.passCount, Is.GreaterThan(5));
            for (int pass = 0; pass < copy.passCount; pass++) ShaderUtil.CompilePass(copy, pass, true);
            foreach (var message in ShaderUtil.GetShaderMessages(copy.shader))
                Assert.That(message.severity.ToString(), Is.Not.EqualTo("Error"), message.message);
        }
        finally
        {
            Shader.globalRenderPipeline = pipeline;
            if (copy != null) Object.DestroyImmediate(copy);
            if (source != null) Object.DestroyImmediate(source);
            if (texture != null) Object.DestroyImmediate(texture);
        }
    }

    [TestCase("Assets/Scenes/District_1/District_1_Core.unity")]
    [TestCase("Assets/Scenes/District_1/District_1_Arena_Environment.unity")]
    [TestCase("Assets/Scenes/District_1/District_1_Balcony_Environment.unity")]
    [TestCase("Assets/Scenes/District_1/District_1_Corridor_Environment.unity")]
    [TestCase("Assets/Scenes/District_1/District_1_Corridor_Flammes.unity")]
    [TestCase("Assets/Scenes/District_1/District_1_Crypt_Environment.unity")]
    [TestCase("Assets/Scenes/District_1/District_1_ConduitsNoyés_Environement.unity")]
    [TestCase("Assets/Scenes/District_1/District_1_AbilityShortcuts.unity")]
    [TestCase("Assets/Scenes/District_1/District_1_Sequences.unity")]
    [TestCase("Assets/Scenes/District_1/District_1_Rooms_Flammes.unity")]
    [TestCase("Assets/Scenes/District_1/District_1_Rooms_Environment.unity")]
    [TestCase("Assets/Scenes/District_1/District_1_PuitsDeLaReleve_Environment.unity")]
    [TestCase("Assets/Scenes/District_1/District_1_NefDesToursInterrompus_Environment.unity")]
    [TestCase("Assets/Scenes/District_1/District_1_Interactives.unity")]
    [TestCase("Assets/Scenes/District_1/District_1_Environment_OldBarn.unity")]
    [TestCase("Assets/Scenes/District_1/District_1_Enigme_Levers.unity")]
    [TestCase("Assets/Scenes/District_1/District_1_Enemies.unity")]
    [TestCase("Assets/Scenes/District_1/District_1_Cycle_Gustave.unity")]
    [TestCase("Assets/Scenes/Cycles/Cycle_Nina/District_1_Cycle_Nina.unity")]
    [TestCase("Assets/Scenes/Cycles/Cycle_Luc/District_1_Cycle_Luc.unity")]
    [TestCase("Assets/Scenes/Cycles/Cycle_Etienne/District_1_Cycle_Etienne.unity")]
    [TestCase("Assets/Scenes/Cycles/Cycle_Clairmont/District_1_Cycle_Clairmont.unity")]
    [TestCase("Assets/Scenes/Cycles/Cycle_Belmont/District_1_Cycle_Belmont.unity")]
    [TestCase("Assets/Scenes/Cycles/Cycle_Ardent/District_1_Cycle_Ardent.unity")]
    [TestCase("Assets/District_1_Indoor_Lights.unity")]
    public void EveryDistrictSceneHasStandaloneAndAdditiveCoverage(string path)
        => Assert.That(LitDistrictOneVisibilityInstaller.IsDistrictScene(path, Path.GetFileNameWithoutExtension(path)), Is.True);

    [TestCase("Assets/Scenes/District_10/District_10_Core.unity")]
    [TestCase("Assets/Scenes/Maison/Maison_Core.unity")]
    [TestCase("Assets/Other/District_1_Fake.unity")]
    public void OtherAreasAreNotRegistered(string path)
        => Assert.That(LitDistrictOneVisibilityInstaller.IsDistrictScene(path, Path.GetFileNameWithoutExtension(path)), Is.False);

    [TestCase("Wall_Stone_01_Arch_Sml", true)]
    [TestCase("GuardRail_15_1", true)]
    [TestCase("Conduit_09", true)]
    [TestCase("Cube.006", true)]
    [TestCase("Marche_09", false)]
    [TestCase("Terrain", false)]
    [TestCase("Flame_Sconce_01", false)]
    public void DistrictEnvironmentGeometryIsClassified(string name, bool expected)
    {
        var item = new GameObject(name);
        try { Assert.That(LitMaisonVisibilityZone.IsDistrictArchitecture(item.transform, "District_1_Test_Environment"), Is.EqualTo(expected)); }
        finally { Object.DestroyImmediate(item); }
    }

    [Test] public void UnnamedFloorChildStaysVisibleButWallChildIsPrepared()
    {
        var parent = new GameObject("Floor_01"); var child = new GameObject("Cube"); child.transform.SetParent(parent.transform);
        try
        {
            Assert.That(LitMaisonVisibilityZone.IsDistrictArchitecture(child.transform, "District_1_Rooms_Environment"), Is.False);
            parent.name = "Mur_01";
            Assert.That(LitMaisonVisibilityZone.IsDistrictArchitecture(child.transform, "District_1_Rooms_Environment"), Is.True);
        }
        finally { Object.DestroyImmediate(parent); }
    }

    [Test] public void NarrativeRockTracesAreNotTreatedAsArchitecture()
    {
        var parent = new GameObject("Nina's blood"); var child = new GameObject("sm_Campfire_rock_LOD0");
        child.transform.SetParent(parent.transform);
        try { Assert.That(LitMaisonVisibilityZone.IsDistrictArchitecture(child.transform, "District_1_Cycle_Nina"), Is.False); }
        finally { Object.DestroyImmediate(parent); }
    }

    [Test] public void PreparationAndCleanupPreserveOriginalMaterialsFloorsAndPhysics()
    {
        Scene scene = default; GameObject zoneRoot = null; Material source = null, autodesk = null;
        try
        {
            scene = EditorSceneManager.NewPreviewScene();
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "Mur_Validation";
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Floor_Validation";
            SceneManager.MoveGameObjectToScene(wall, scene); SceneManager.MoveGameObjectToScene(floor, scene);
            source = new Material(Shader.Find("HDRP/Lit"));
            autodesk = new Material(AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipelineResources/ShaderGraph/AutodeskInteractive.shadergraph"));
            wall.GetComponent<Renderer>().sharedMaterials = new[] { source, autodesk };
            floor.GetComponent<Renderer>().sharedMaterial = source;
            zoneRoot = new GameObject("District validator");
            var zone = zoneRoot.AddComponent<LitMaisonVisibilityZone>(); zone.ConfigureForDistrictOne();
            typeof(LitMaisonVisibilityZone).GetMethod("PrepareSceneContent", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(zone, new object[] { scene });
            Assert.That(zone.PreparedRendererCount, Is.EqualTo(1)); Assert.That(zone.UnsupportedRendererCount, Is.Zero);
            var local = wall.GetComponent<Renderer>().sharedMaterial;
            Assert.That(LitTacticalMaskProperties.Supports(local), Is.True);
            Assert.That(local, Is.Not.SameAs(source));
            var secondSlot = wall.GetComponent<Renderer>().sharedMaterials[1];
            Assert.That(LitTacticalMaskProperties.Supports(secondSlot), Is.True);
            Assert.That(secondSlot, Is.Not.SameAs(autodesk));
            Assert.That(floor.GetComponent<Renderer>().sharedMaterial, Is.SameAs(source));
            Assert.That(wall.GetComponent<Collider>().enabled, Is.True);
            typeof(LitMaisonVisibilityZone).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(zone, null);
            Object.DestroyImmediate(zoneRoot); zoneRoot = null;
            Assert.That(wall.GetComponent<Renderer>().sharedMaterial, Is.SameAs(source));
            Assert.That(local == null, Is.True); Assert.That(source.shader.name, Is.EqualTo("HDRP/Lit"));
            Assert.That(wall.GetComponent<Renderer>().sharedMaterials[1], Is.SameAs(autodesk));
            Assert.That(secondSlot == null, Is.True);
        }
        finally
        {
            if (zoneRoot != null) Object.DestroyImmediate(zoneRoot);
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            if (source != null) Object.DestroyImmediate(source);
            if (autodesk != null) Object.DestroyImmediate(autodesk);
        }
    }
}
