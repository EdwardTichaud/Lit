#if UNITY_INCLUDE_TESTS
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class SceneIntegrityRestoredGeometryRenderTests
{
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(5)]
    [TestCase(0)]
    public void RestoredSurfaceContainsVisibleGpuGeometry(int portal)
    {
        Mesh mesh;
        if (portal != 0)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Interactive/Portal/Portal_{portal}.prefab");
            mesh = prefab.transform.Find("Portal").GetComponent<MeshFilter>().sharedMesh;
        }
        else
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scenes/MainMenu/Loading_Orbe.prefab");
            var renderer = prefab.GetComponentsInChildren<ParticleSystemRenderer>(true).Single(r =>
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(r, out string guid, out long id);
                return id == 7397080656195245637;
            });
            mesh = renderer.mesh;
        }
        Assert.IsNotNull(mesh);
        using var data = MeshUtility.AcquireReadOnlyMeshData(mesh);
        using var vertices = new NativeArray<Vector3>(mesh.vertexCount, Allocator.Temp);
        data[0].GetVertices(vertices);
        var bounds = new Bounds(vertices[0], Vector3.zero);
        foreach (var vertex in vertices) bounds.Encapsulate(vertex);
        float radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
        Assert.Greater(radius, .00001f, "An assigned mesh must contain actual non-degenerate positions.");

        var cameraRoot = new GameObject("Restored geometry raster fixture");
        var rt = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32);
        var pixels = new Texture2D(128, 128, TextureFormat.RGBA32, false, true);
        var material = new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/Editor/LitMaskStackRenderTest.shader"));
        string pipeline = Shader.globalRenderPipeline;
        var previous = RenderTexture.active;
        try
        {
            Shader.globalRenderPipeline = "";
            material.SetColor("_Color", new Color(.95f, .72f, .28f));
            material.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
            var camera = cameraRoot.AddComponent<Camera>(); camera.enabled = false;
            camera.orthographic = true; camera.orthographicSize = 1.6f; camera.aspect = 1;
            camera.nearClipPlane = .1f; camera.farClipPlane = 20;
            cameraRoot.transform.position = new Vector3(2, 2, -3); cameraRoot.transform.LookAt(Vector3.zero);
            var matrix = Matrix4x4.Scale(Vector3.one / radius) * Matrix4x4.Translate(-bounds.center);
            var block = new MaterialPropertyBlock(); block.SetMatrix("_TestObjectToWorld", matrix);
            rt.Create();
            using (var command = new CommandBuffer())
            {
                command.SetRenderTarget(rt); command.SetViewport(new Rect(0, 0, 128, 128)); command.DisableScissorRect();
                command.ClearRenderTarget(true, true, Color.black, 1);
                command.SetGlobalMatrix("_TestVP", GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * camera.worldToCameraMatrix);
                command.SetGlobalVector("_TestCameraPosition", cameraRoot.transform.position);
                command.SetGlobalVector("_TestCameraForward", cameraRoot.transform.forward);
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                    command.DrawMesh(mesh, matrix, material, submesh, 0, block);
                Graphics.ExecuteCommandBuffer(command);
            }
            RenderTexture.active = rt; pixels.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); pixels.Apply();
            int covered = pixels.GetPixels32().Count(color => color.r > 80);
            Assert.That(covered, Is.InRange(50, 15000), "The actual vertex/index buffers must produce a visible surface.");
            Directory.CreateDirectory("Library/SceneIntegrity/render-checks");
            string path = "Library/SceneIntegrity/render-checks/" + (portal == 0 ? "loading-burst" : "portal-" + portal) + ".png";
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            TestContext.WriteLine(path + "; visible pixels=" + covered + "; positions=" + bounds);
        }
        finally
        {
            RenderTexture.active = previous; Shader.globalRenderPipeline = pipeline;
            rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(material); Object.DestroyImmediate(cameraRoot);
        }
    }
}
#endif
