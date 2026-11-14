using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class LitTacticalMaskRenderTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void PixelsRevealPlayerThroughEveryLayerAndKeepGroundAndBackground(bool fireBall)
    {
        string pipeline = Shader.globalRenderPipeline; Shader.globalRenderPipeline = "";
        var objects = new List<GameObject>(); var materials = new List<Material>();
        var rt = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32);
        var pixels = new Texture2D(128, 128, TextureFormat.RGBA32, false, true);
        var previous = RenderTexture.active;
        var previousShape = Shader.GetGlobalTexture(LitTacticalMaskProperties.ShapeTexture);
        var previousMotion = Shader.GetGlobalVector(LitTacticalMaskProperties.ShapeMotion);
        try
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Editor/LitMaskStackRenderTest.shader");
            Assert.That(shader, Is.Not.Null);
            var shape = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Camera/Masks/CameraMask_FireBall_White.png");
            if (fireBall) Assert.That(shape, Is.Not.Null);
            var cameraRoot = new GameObject("Raster stack camera"); objects.Add(cameraRoot);
            var camera = cameraRoot.AddComponent<Camera>(); camera.enabled = false; camera.aspect = 1;
            camera.nearClipPlane = .1f; camera.farClipPlane = 30; camera.cullingMask = 1 << 30;
            cameraRoot.transform.position = new Vector3(0, 3, -10); var target = Vector3.up;
            cameraRoot.transform.LookAt(target);
            var draw = new List<Renderer>();
            Renderer Cube(string name, Vector3 position, Vector3 scale, Color color)
            {
                var root = GameObject.CreatePrimitive(PrimitiveType.Cube); root.name = name; root.layer = 30;
                objects.Add(root); root.transform.position = position; root.transform.localScale = scale;
                var renderer = root.GetComponent<Renderer>();
                var mat = new Material(shader); materials.Add(mat); mat.SetColor("_Color", color);
                // ShaderLab translates LEqual for reversed-Z platforms itself.
                mat.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
                renderer.sharedMaterial = mat; draw.Add(renderer); return renderer;
            }
            LitCameraOcclusionGroup Group(Renderer renderer, bool floor = false)
            {
                var group = renderer.gameObject.AddComponent<LitCameraOcclusionGroup>(); group.Configure(new[] { renderer }, floor); return group;
            }
            var background = Group(Cube("Background", new Vector3(0, 1, 2), new Vector3(20, 6, .1f), Color.blue));
            Cube("Player", target, new Vector3(1, 2, .4f), Color.green);
            var support = Group(Cube("Supporting floor", new Vector3(0, -.1f, 0), new Vector3(20, .2f, 20), Color.yellow), true);
            Group(Cube("Upper floor", new Vector3(0, 2, -5), new Vector3(10, .2f, 8), Color.magenta), true);
            Group(Cube("Rear wall", new Vector3(0, 1, -3), new Vector3(10, 10, .15f), Color.red));
            Group(Cube("Middle wall", new Vector3(0, 1, -5), new Vector3(10, 10, .15f), Color.red));
            var nearest = Group(Cube("Near wall", new Vector3(0, 1, -7), new Vector3(10, 10, .15f), Color.red));
            Vector4 position = cameraRoot.transform.position, forward = cameraRoot.transform.forward;
            position.w = forward.w = .25f;
            Vector3 projected = camera.WorldToViewportPoint(target);
            var data = new LitTacticalMaskProperties.Data
            {
                cameraPosition = position, cameraForward = forward,
                target0 = new Vector4(projected.x, projected.y, projected.z, 1),
                settings = new Vector4(.32f, .01f, 1, .05f),
                useShape = fireBall
            };
            rt.Create();
            int renderNumber = 0;
            void Render(float flameIntensity = 0, float flamePhase = 0)
            {
                var cmd = new CommandBuffer();
                try
                {
                    cmd.SetRenderTarget(rt);
                    cmd.SetViewport(new Rect(0, 0, 128, 128)); cmd.DisableScissorRect();
                    cmd.ClearRenderTarget(true, true, Color.black, 1);
                    cmd.SetGlobalMatrix("_TestVP", GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * camera.worldToCameraMatrix);
                    cmd.SetGlobalVector("_TestCameraPosition", cameraRoot.transform.position);
                    cmd.SetGlobalVector("_TestCameraForward", cameraRoot.transform.forward);
                    cmd.SetGlobalVector("_ProjectionParams", new Vector4(-1, camera.nearClipPlane, camera.farClipPlane, 1 / camera.farClipPlane));
                    cmd.SetGlobalVector("_ScreenParams", new Vector4(128, 128, 1 + 1f / 128, 1 + 1f / 128));
                    if (fireBall) cmd.SetGlobalTexture(LitTacticalMaskProperties.ShapeTexture, shape);
                    cmd.SetGlobalVector(LitTacticalMaskProperties.ShapeMotion, new Vector4(flameIntensity, flamePhase, 0, 0));
                    foreach (var renderer in draw)
                    {
                        var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block, 0);
                        block.SetMatrix("_TestObjectToWorld", renderer.localToWorldMatrix);
                        cmd.DrawMesh(renderer.GetComponent<MeshFilter>().sharedMesh, renderer.localToWorldMatrix, renderer.sharedMaterial, 0, 0, block);
                    }
                    Graphics.ExecuteCommandBuffer(cmd);
                    RenderTexture.active = rt; pixels.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); pixels.Apply();
                    string capture = System.IO.Path.Combine(Application.temporaryCachePath, $"mask-stack-{fireBall}-{renderNumber++}.png");
                    System.IO.File.WriteAllBytes(capture, pixels.EncodeToPNG()); TestContext.WriteLine(capture + " center=" + pixels.GetPixel(64, 64));
                }
                finally { cmd.Release(); }
            }
            Render(); Assert.That(pixels.GetPixel(64, 64).r, Is.GreaterThan(.8f), "Control: a wall must initially hide the player.");
            Assert.That(pixels.GetPixel(64, 64).g, Is.LessThan(.2f), "Control must show a wall, not a floor or the player.");
            nearest.ApplyMask(this, data); Render();
            Assert.That(pixels.GetPixel(64, 64).r, Is.GreaterThan(.8f), "Cutting only the first layer must still leave an occluder.");
            nearest.RestoreMask(this);
            var groups = new List<LitCameraOcclusionGroup>();
            LitCameraOcclusionGroup.Collect(camera, target, .33f, .25f, groups);
            Assert.That(groups.Count, Is.EqualTo(4));
            var stack = new LitTacticalMaskStack(); foreach (var group in groups) stack.Mark(group, false, 0);
            stack.Advance(0, .1f, .1f, .15f, false);
            foreach (var layer in stack.Layers) { data.target0.w = layer.Value.playerStrength; layer.Key.ApplyMask(this, data); }
            // Even if applied conservatively, depth and ground guards must retain these two surfaces.
            background.ApplyMask(this, data); support.ApplyMask(this, data);
            Render();
            Color center = pixels.GetPixel(64, 64);
            Assert.That(center.g, Is.GreaterThan(.8f), "The player must be visible through all three walls AND the upper floor.");
            Assert.That(center.r, Is.LessThan(.2f));
            Assert.That(pixels.GetPixel(8, 64).r, Is.GreaterThan(.8f), "Walls outside the opening remain visible.");
            if (fireBall)
            {
                var textured = pixels.GetPixels32();
                void ApplyShape(bool enabled)
                {
                    data.useShape = enabled;
                    foreach (var layer in stack.Layers)
                    {
                        layer.Key.RestoreMask(this);
                        layer.Key.ApplyMask(this, data);
                    }
                }
                ApplyShape(false);
                Render();
                var circle = pixels.GetPixels32();
                int changed = 0;
                for (int i = 0; i < circle.Length; i++)
                    if (!circle[i].Equals(textured[i])) changed++;
                Assert.That(changed, Is.GreaterThan(30), "The FireBall alpha must change the outline, not render the original circle.");
                ApplyShape(true);
                Render(0, .5f);
                CollectionAssert.AreEqual(textured, pixels.GetPixels32(), "Zero intensity must keep the texture still.");
                Render(.025f, 0);
                var firstPhase = pixels.GetPixels32();
                Render(.025f, .5f);
                var secondPhase = pixels.GetPixels32();
                int animatedPixels = 0;
                for (int i = 0; i < firstPhase.Length; i++)
                    if (!firstPhase[i].Equals(secondPhase[i])) animatedPixels++;
                Assert.That(animatedPixels, Is.GreaterThan(0), "The edge should visibly change with phase.");
                for (int y = 60; y <= 68; y++)
                    for (int x = 60; x <= 68; x++)
                    {
                        int i = y * 128 + x;
                        Assert.That(firstPhase[i], Is.EqualTo(textured[i]));
                        Assert.That(secondPhase[i], Is.EqualTo(textured[i]), "The opening over the player must remain unchanged.");
                    }
                Render();
            }
            Vector3 floorPixel = camera.WorldToViewportPoint(new Vector3(0, 0, -4));
            int fy = Mathf.RoundToInt(floorPixel.y * 128);
            Color ground = pixels.GetPixel(64, fy);
            Assert.That(ground.r, Is.GreaterThan(.8f), "Supporting ground remains yellow, not a hole.");
            Assert.That(ground.g, Is.GreaterThan(.8f));
            Vector3 backPixel = camera.WorldToViewportPoint(new Vector3(1.8f, 1, 2));
            Color back = pixels.GetPixel(Mathf.RoundToInt(backPixel.x * 128), Mathf.RoundToInt(backPixel.y * 128));
            Assert.That(back.b, Is.GreaterThan(.8f), "Geometry behind the player remains blue.");
            foreach (var group in groups) group.RestoreMask(this);
            background.RestoreMask(this); support.RestoreMask(this);
            Render(); Assert.That(pixels.GetPixel(64, 64).r, Is.GreaterThan(.8f), "Restoring the mask restores every wall.");
        }
        finally
        {
            RenderTexture.active = previous; Shader.globalRenderPipeline = pipeline;
            Shader.SetGlobalTexture(LitTacticalMaskProperties.ShapeTexture, previousShape);
            Shader.SetGlobalVector(LitTacticalMaskProperties.ShapeMotion, previousMotion);
            foreach (var root in objects) if (root != null) Object.DestroyImmediate(root);
            foreach (var mat in materials) Object.DestroyImmediate(mat);
            rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(pixels);
        }
    }
}
