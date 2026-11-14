using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class LitLensRainLightingTests
{
    [TestCase(.18f, .005f)]
    [TestCase(.002f, .001f)]
    public void LensCompositeStaysBlackAndRevealsDropsWithLight(float brightness, float minimumContrast)
    {
        // This wrapper executes the same lens and composite HLSL as HDRP.
        string pipeline = Shader.globalRenderPipeline;
        var previousTarget = RenderTexture.active;
        var source = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
        var pixels = new Texture2D(128, 128, TextureFormat.RGBAFloat, false, true);
        var output = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        Material material = null;
        var floats = new Dictionary<string, float>();
        var vectors = new Dictionary<string, Vector4>();
        void SetFloat(string name, float value)
        {
            floats.Add(name, Shader.GetGlobalFloat(name));
            Shader.SetGlobalFloat(name, value);
        }
        void SetVector(string name, Vector4 value)
        {
            vectors.Add(name, Shader.GetGlobalVector(name));
            Shader.SetGlobalVector(name, value);
        }
        try
        {
            Shader.globalRenderPipeline = "";
            var shader = Shader.Find("Hidden/RealRain/RealRain_BuiltIn");
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.isSupported, Is.True);
            material = new Material(shader);
            SetFloat("_RealRain_Intensity", 1);
            SetFloat("_RealRain_Lightning", 0);
            SetFloat("_RealRain_LocalLightCount", 0);
            SetVector("_RealRain_Time", new Vector4(2.5f, 0, 0, 0));
            SetVector("_RealRain_Streak", Vector4.zero);
            SetVector("_RealRain_Wet", Vector4.zero);
            SetVector("_RealRain_Wet2", Vector4.zero);
            SetVector("_RealRain_Haze", Vector4.zero);
            SetVector("_RealRain_Surface", Vector4.zero);
            SetVector("_RealRain_Sheeting", Vector4.zero);
            SetVector("_RealRain_LensAtlasPlayback", Vector4.zero);
            SetVector("_RealRain_LensTrail", new Vector4(.75f, .9f, 0, 0));
            SetVector("_RealRain_Lens", new Vector4(.65f, .85f, .42f, .4f));
            // No glow, including when lens fog is enabled.
            Color[] Render(float sourceBrightness)
            {
                source.SetPixel(0, 0, new Color(sourceBrightness, sourceBrightness, sourceBrightness, 1));
                source.Apply();
                Graphics.Blit(source, output, material);
                RenderTexture.active = output;
                pixels.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
                return pixels.GetPixels();
            }
            foreach (var pixel in Render(0))
            {
                Assert.That(pixel.r, Is.EqualTo(0).Within(1e-6));
                Assert.That(pixel.g, Is.EqualTo(0).Within(1e-6));
                Assert.That(pixel.b, Is.EqualTo(0).Within(1e-6));
            }
            Shader.SetGlobalVector("_RealRain_Lens", new Vector4(.28f, .85f, .42f, 0));
            Color[] illuminated = Render(brightness);
            Shader.SetGlobalVector("_RealRain_Lens", Vector4.zero);
            Color[] dry = Render(brightness);
            int changed = 0;
            for (int i = 0; i < dry.Length; i++)
                if (Mathf.Abs(illuminated[i].r - dry[i].r) > minimumContrast) changed++;
            Assert.That(changed, Is.GreaterThan(10), "Gouttes visibles sur une image éclairée.");
        }
        finally
        {
            foreach (var value in floats) Shader.SetGlobalFloat(value.Key, value.Value);
            foreach (var value in vectors) Shader.SetGlobalVector(value.Key, value.Value);
            RenderTexture.active = previousTarget;
            Shader.globalRenderPipeline = pipeline;
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(output);
            Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(source);
        }
    }
}
