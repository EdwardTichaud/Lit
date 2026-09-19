using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class RuntimeOutlineAlphaTests
{
    [Test]
    public void LadderMaskUsesItsTextureAndRedChannelAndPreservesRendererOverrides()
    {
        var source = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Environment/3_VFX/VFX_GlitteringStars/Material_VFX_GlitteringStars_Unlit_Ladder_Leaf.mat");
        Assert.That(source, Is.Not.Null);
        var root = new GameObject("Outline alpha fixture", typeof(ParticleSystem));
        try
        {
            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = source;
            var block = new MaterialPropertyBlock();
            block.SetFloat("_UnrelatedValue", 42f);
            block.SetVector("_MainTex_ST", new Vector4(2, 3, .1f, .2f));
            renderer.SetPropertyBlock(block);
            root.AddComponent<RuntimeOutlineTarget>().SetOutlined(true);
            renderer.GetPropertyBlock(block);
            Assert.That(block.GetTexture("_RuntimeOutlineTexture"), Is.EqualTo(source.GetTexture("_MainTex")));
            Assert.That(block.GetFloat("_RuntimeOutlineRedChannel"), Is.EqualTo(1f));
            Assert.That(block.GetFloat("_RuntimeOutlineAlphaEnabled"), Is.EqualTo(1f));
            Assert.That(block.GetFloat("_RuntimeOutlineVertexAlpha"), Is.EqualTo(1f));
            Assert.That(block.GetFloat("_RuntimeOutlineOpacity"), Is.EqualTo(1f), "HDR glow color alpha is not opacity in this graph.");
            Assert.That(block.GetVector("_RuntimeOutlineTexture_ST"), Is.EqualTo(new Vector4(2, 3, .1f, .2f)));
            Assert.That(block.GetFloat("_UnrelatedValue"), Is.EqualTo(42f));
            Assert.That(renderer.sharedMaterial, Is.SameAs(source));
        }
        finally { Object.DestroyImmediate(root); }
    }
}
