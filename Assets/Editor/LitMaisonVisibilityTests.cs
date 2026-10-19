using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class LitMaisonVisibilityTests
{
    [Test] public void NativeMaskPreservesTexturesSurfaceValuesAndCompilesRasterPasses()
    {
        string previousPipelineTag = Shader.globalRenderPipeline;
        Shader.globalRenderPipeline = "HDRenderPipeline";
        var source = new Material(Shader.Find("HDRP/Lit"));
        var texture = new Texture2D(2, 2);
        Material copy = null;
        try
        {
            source.SetTexture("_BaseColorMap", texture);
            source.SetColor("_BaseColor", new Color(.2f, .4f, .6f, 1));
            source.SetFloat("_Smoothness", .42f);
            source.SetFloat("_Metallic", .17f);
            copy = LitMaisonVisibilityZone.CreateMaskMaterial(source);
            Assert.That(copy.GetTexture("_BaseColorMap"), Is.SameAs(texture));
            Assert.That(copy.GetColor("_BaseColor"), Is.EqualTo(source.GetColor("_BaseColor")));
            Assert.That(copy.GetFloat("_Smoothness"), Is.EqualTo(.42f));
            Assert.That(copy.GetFloat("_Metallic"), Is.EqualTo(.17f));
            copy.SetFloat("_LitMaskEnabled", 1);
            Assert.That(copy.passCount, Is.GreaterThan(5));
            TestContext.WriteLine("HDRP/Lit raster passes compiled: " + copy.passCount);
            for (int pass = 0; pass < copy.passCount; pass++) ShaderUtil.CompilePass(copy, pass, true);
            foreach (var message in ShaderUtil.GetShaderMessages(copy.shader))
                Assert.That(message.severity.ToString(), Is.Not.EqualTo("Error"), message.message);
        }
        finally
        {
            Shader.globalRenderPipeline = previousPipelineTag;
            Object.DestroyImmediate(source); Object.DestroyImmediate(texture);
            if (copy != null) Object.DestroyImmediate(copy);
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void NativeMaskRestoresOtherEffectsAndDoesNotChangePhysics(bool indexed)
    {
        var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var material = new Material(Shader.Find("Lit/Camera/Maison HDRP Lit"));
        try
        {
            var renderer = root.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            var collider = root.GetComponent<Collider>();
            var group = root.AddComponent<LitCameraOcclusionGroup>(); group.Configure(new[] { renderer });
            var block = new MaterialPropertyBlock();
            int other = Shader.PropertyToID("_MaisonOtherEffect"); block.SetFloat(other, .37f);
            if (indexed) renderer.SetPropertyBlock(block, 0); else renderer.SetPropertyBlock(block);
            group.ApplyMask(this, new LitTacticalMaskProperties.Data { target0 = new Vector4(.5f, .5f, 10, 1) });
            block.Clear(); renderer.GetPropertyBlock(block, 0);
            Assert.That(block.GetFloat(LitTacticalMaskProperties.Enabled), Is.EqualTo(1));
            Assert.That(block.GetFloat(other), Is.EqualTo(.37f));
            group.enabled = false; // Timeline/zone teardown must also restore an in-flight render.
            block.Clear();
            if (indexed) renderer.GetPropertyBlock(block, 0); else renderer.GetPropertyBlock(block);
            Assert.That(block.GetFloat(LitTacticalMaskProperties.Enabled), Is.Zero);
            Assert.That(block.GetFloat(other), Is.EqualTo(.37f));
            Assert.That(renderer.sharedMaterial, Is.SameAs(material));
            Assert.That(renderer.enabled, Is.True); Assert.That(renderer.forceRenderingOff, Is.False);
            Assert.That(collider.enabled, Is.True); Assert.That(collider.isTrigger, Is.False);
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(material); }
    }

    [TestCase("sm_ChapelWall_05_01_OneSide_LOD0", true)]
    [TestCase("sm_Collumn_01_154", true)]
    [TestCase("SM_StairsForHouse_Roof_LOD3", true)]
    [TestCase("sm_PathRocky_01_04_rocks_LOD0", false)]
    [TestCase("sm_ChapelFloor_LOD0", false)]
    [TestCase("sm_CryptStairs_LOD0", false)]
    [TestCase("Flame", false)]
    [TestCase("Player_Model_Lucian", false)]
    [TestCase("sm_SpiderWeb_01_10_Wall", false)]
    public void OnlyArchitectureIsPrepared(string name, bool expected)
        => Assert.That(LitMaisonVisibilityZone.IsArchitecture(name), Is.EqualTo(expected));

    [TestCase(1f)] [TestCase(16f / 9)] [TestCase(21f / 9)]
    public void VisualDetectionFindsForegroundEvenWithoutCollider(float aspect)
    {
        var root = new GameObject("Projection");
        try
        {
            var camera = root.AddComponent<Camera>(); camera.aspect = aspect; camera.fieldOfView = 60;
            Vector3 target = Vector3.forward * 10;
            Assert.That(LitTacticalMaskGeometry.IntersectsOpening(camera, new Bounds(Vector3.forward * 5, Vector3.one), target, .16f), Is.True);
            Assert.That(LitTacticalMaskGeometry.IntersectsOpening(camera, new Bounds(Vector3.forward * 12, Vector3.one), target, .16f), Is.False);
            Assert.That(LitTacticalMaskGeometry.IntersectsOpening(camera, new Bounds(Vector3.back * 5, Vector3.one), target, .16f), Is.False);
            Assert.That(LitTacticalMaskGeometry.IntersectsOpening(camera, new Bounds(Vector3.zero, Vector3.one * 2), target, .16f), Is.True);
            Assert.That(LitTacticalMaskGeometry.IntersectsOpening(camera, new Bounds(Vector3.forward * 5, Vector3.one), -target, .16f), Is.False);
            var side = new Bounds(camera.ViewportToWorldPoint(new Vector3(.5f + .12f / aspect, .5f, 5)), Vector3.one * .05f);
            Assert.That(LitTacticalMaskGeometry.IntersectsOpening(camera, side, target, .16f), Is.True);
            var outside = new Bounds(camera.ViewportToWorldPoint(new Vector3(.5f + .3f / aspect, .5f, 5)), Vector3.one * .05f);
            Assert.That(LitTacticalMaskGeometry.IntersectsOpening(camera, outside, target, .16f), Is.False);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase("Assets/CombatRealTime/Camera/LitTacticalMaskLit.shadergraph")]
    [TestCase("Assets/Materials/MasterShader/ShaderGraph_MasterShader.shadergraph")]
    [TestCase("Assets/Materials/IceShader/ShaderGraph_LitIceFrostedEdges_v3.shadergraph")]
    [TestCase("HDRP/Lit")]
    public void LocalMaterialEnablesCutoutWithoutMutatingTheSource(string path)
    {
        var shader = path == "HDRP/Lit" ? Shader.Find(path) : AssetDatabase.LoadAssetAtPath<Shader>(path);
        Assert.That(shader, Is.Not.Null);
        var source = new Material(shader);
        Material copy = null;
        try
        {
            source.SetFloat("_AlphaCutoffEnable", 0);
            if (source.HasProperty("_AlphaCutoff")) source.SetFloat("_AlphaCutoff", .37f);
            source.DisableKeyword("_ALPHATEST_ON");
            copy = LitMaisonVisibilityZone.CreateMaskMaterial(source);
            Assert.That(copy, Is.Not.SameAs(source));
            Assert.That(copy.shader.name, Is.EqualTo(path == "HDRP/Lit" ? "Lit/Camera/Maison HDRP Lit" : source.shader.name));
            Assert.That(copy.GetFloat("_AlphaCutoffEnable"), Is.EqualTo(1));
            Assert.That(copy.IsKeywordEnabled("_ALPHATEST_ON"), Is.True);
            if (copy.HasProperty("_AlphaCutoff")) Assert.That(copy.GetFloat("_AlphaCutoff"), Is.Zero);
            Assert.That(source.GetFloat("_AlphaCutoffEnable"), Is.Zero);
            if (source.HasProperty("_AlphaCutoff")) Assert.That(source.GetFloat("_AlphaCutoff"), Is.EqualTo(.37f));
            Assert.That(source.IsKeywordEnabled("_ALPHATEST_ON"), Is.False);
            Assert.That(LitTacticalMaskProperties.Supports(copy), Is.True);
        }
        finally { Object.DestroyImmediate(source); if (copy != null) Object.DestroyImmediate(copy); }
    }
}
