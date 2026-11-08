#if UNITY_INCLUDE_TESTS
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class SceneIntegrityDependencyQualificationTests
{
    [Test]
    public void ActualLucianTemplateUsesStandardHqSkinWithValidDiffuseAndNormalMaps()
    {
        var data = AssetDatabase.LoadMainAssetAtPath("Assets/Characters/1_Squad/Lucian/Lucian.asset");
        var prefab = (GameObject)new SerializedObject(data).FindProperty("worldPrefab").objectReferenceValue;
        var skin = prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
            .Where(m => m != null && m.name.StartsWith("Ga_Skin_")).Distinct().ToArray();
        Assert.GreaterOrEqual(skin.Length, 4, "Inspect the actual production actor, not just its imported source materials.");
        foreach (var material in skin)
        {
            TestContext.WriteLine("Production skin: " + AssetDatabase.GetAssetPath(material) + "; MaterialID=" + material.GetFloat("_MaterialID"));
            Assert.AreEqual("HDRP/Lit", material.shader.name);
            Assert.AreEqual(1, material.GetFloat("_MaterialID"), "The authored HQ skin uses LitStandard, not the imported SSS source.");
            Assert.IsFalse(material.IsKeywordEnabled("_MATERIAL_FEATURE_SUBSURFACE_SCATTERING"));
            Assert.IsFalse(material.IsKeywordEnabled("_MATERIAL_FEATURE_TRANSMISSION"));
            Assert.IsNotNull(material.GetTexture("_BaseColorMap"));
            Assert.IsNotNull(material.GetTexture("_NormalMap"));
        }
    }

    [TestCase("Assets/0 - UnityPackages/UnityAssets/FullOpaqueSpell/Prefabs/VFX/Spell_SpellManager/VFX_Damage_Burst.prefab", "")]
    [TestCase("Assets/0 - UnityPackages/UnityAssets/FullOpaqueSpell/Prefabs/VFX/Spell_SpellManager/VFX_Melt_Burst.prefab", "")]
    [TestCase("Assets/Characters/7_VFX/VFX_Spawn_Lightning.prefab", "Pos/Thunder")]
    [TestCase("Assets/Gameplay/Items/Item_Pickable/Item_Orbe/VFX_Item_Orbe_White.prefab", "")]
    [TestCase("Assets/Gameplay/Items/Item_Pickable/Item_Orbe_Blue/Item_Orbe bleu_Model.prefab", "")]
    [TestCase("Assets/Gameplay/Items/Item_Pickable/Item_Orbe_Blue/Item_Orbe rouge_Model.prefab", "")]
    [TestCase("Assets/Interactive/Ghost/Ulric/VFX_BlackSmoke.prefab", "")]
    public void DisabledControlRendererKeepsValidVisibleChildEmitters(string path, string controlPath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var root = string.IsNullOrEmpty(controlPath) ? prefab : prefab.transform.Find(controlPath).gameObject;
        Assert.IsFalse(root.GetComponent<ParticleSystemRenderer>().enabled, "This root intentionally delegates rendering to its children.");
        Assert.AreEqual(ParticleSystemRenderMode.None, root.GetComponent<ParticleSystemRenderer>().renderMode,
            "Even a visibility toggle must not turn the controller into a material-less quad.");
        var children = root.GetComponentsInChildren<ParticleSystemRenderer>(true)
            .Where(r => r.gameObject != root && r.enabled && r.renderMode != ParticleSystemRenderMode.None).ToArray();
        Assert.IsNotEmpty(children);
        foreach (var renderer in children)
        {
            Assert.IsNotNull(renderer.sharedMaterial, renderer.name);
            Assert.IsNotNull(renderer.sharedMaterial.shader, renderer.name);
            if (renderer.renderMode == ParticleSystemRenderMode.Mesh) Assert.IsNotNull(renderer.mesh, renderer.name);
        }
    }

    [TestCase("Aurell")]
    [TestCase("Soren")]
    [TestCase("Vesta")]
    public void PresentationHasVisibleBodyAndAnEmptyOptionalWeaponSocket(string actor)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Narrative/StorySequence/MaisonReunion/Presentations/{actor}_Maison_Presentation.prefab");
        var socket = prefab.GetComponentsInChildren<MeshFilter>(true).Single(m => m.name == "SM_Sword");
        var reference = new SerializedObject(socket).FindProperty("m_Mesh");
        Assert.IsTrue(reference.objectReferenceEntityIdValue == default, "The weapon slot is explicitly empty, not a lost mesh asset.");
        Assert.IsNull(socket.GetComponent<Collider>());
        Assert.IsTrue(socket.transform.parent.name == "sword" && socket.transform.parent.parent.name == "hand_l_items");
        Assert.IsTrue(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .Any(r => r.sharedMesh != null && r.sharedMaterials.Any(m => m != null)), "The actual presentation body must still exist.");
    }

    [Test]
    public void BalconyTemplateBuildsItsAuthoredMeshAndColliderWhenInstantiated()
    {
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Environment/Castle/Balconies/Balcony_1.prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
            var floor = instance.GetComponentInChildren<UnityEngine.ProBuilder.ProBuilderMesh>(true);
            var mesh = floor.GetComponent<MeshFilter>().sharedMesh;
            Assert.IsNotNull(mesh, "ProBuilder must rebuild the authored geometry at instantiation.");
            Assert.AreEqual(24, mesh.vertexCount);
            Assert.AreEqual(36, mesh.triangles.Length);
            Assert.AreSame(mesh, floor.GetComponent<MeshCollider>().sharedMesh);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    [TestCase("Material_Grapple")]
    [TestCase("Material_VFX_GlitteringStars_Unlit_LightBall")]
    [TestCase("Material_VFX_GlitteringStars_Unlit_Trail")]
    public void ActualGlowMaskHasTransparentRedChannelEdgesAndBrightCenter(string name)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Environment/3_VFX/VFX_GlitteringStars/" + name + ".mat");
        var texture = material.GetTexture("_MainTex");
        Assert.IsNotNull(texture);
        Assert.AreEqual(1, material.GetFloat("_SurfaceType"));
        var rt = new RenderTexture(32, 32, 0, RenderTextureFormat.ARGB32);
        var pixels = new Texture2D(32, 32, TextureFormat.RGBA32, false, true);
        var previous = RenderTexture.active;
        try
        {
            rt.Create(); Graphics.Blit(texture, rt); RenderTexture.active = rt;
            pixels.ReadPixels(new Rect(0, 0, 32, 32), 0, 0); pixels.Apply();
            // This graph converts the sprite RGBA output to scalar Alpha, so its red channel controls opacity.
            foreach (var point in new[] { new Vector2Int(0, 0), new Vector2Int(0, 31), new Vector2Int(31, 0), new Vector2Int(31, 31) })
                Assert.Less(pixels.GetPixel(point.x, point.y).r, .03f, "A glow particle must not expose a rectangular background.");
            Assert.Greater(pixels.GetPixel(16, 16).r, .8f);
            Assert.That(pixels.GetPixel(8, 16).r, Is.InRange(.01f, .79f), "The contour should fade gradually.");
        }
        finally
        {
            RenderTexture.active = previous; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(pixels);
        }
    }
}
#endif
