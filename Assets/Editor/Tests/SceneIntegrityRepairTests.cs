#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

public sealed class SceneIntegrityRepairTests
{
    [Test]
    public void GuidanceAdoptsAuthoredLinesWithoutDuplicatingOrReplacingTheirMaterial()
    {
        var root = new GameObject("Guidance fixture"); root.SetActive(false);
        var material = new Material(Shader.Find("Sprites/Default"));
        try
        {
            foreach (string name in new[] { "AncientFlameArc", "CommonFlameArc" })
            {
                var line = new GameObject(name, typeof(LineRenderer)); line.transform.SetParent(root.transform, false);
                line.GetComponent<LineRenderer>().sharedMaterial = material;
            }
            var renderer = root.AddComponent<FlameGuidanceArcRenderer>();
            var build = typeof(FlameGuidanceArcRenderer).GetMethod("BuildRenderers", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            build.Invoke(renderer, null); build.Invoke(renderer, null);
            Assert.AreEqual(2, root.GetComponentsInChildren<LineRenderer>(true).Length);
            foreach (var line in root.GetComponentsInChildren<LineRenderer>(true)) Assert.AreSame(material, line.sharedMaterial);
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(material); }
    }

    [Test]
    public void MissingCharacterShaderPreservesSavedTextureColorAndTransparency()
    {
        var material = new Material(Shader.Find("HDRP/Lit")) { name = "Hair1_Transparency" };
        var texture = new Texture2D(2, 2);
        Color tint = new Color(.23f, .4f, .6f, .7f);
        try
        {
            material.SetTexture("_BaseColorMap", texture); material.SetColor("_BaseColor", tint);
            material.shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.IsTrue(LucianCC5HqIntegration.ConfigureMaterialForHq(material, material));
            Assert.AreEqual("HDRP/Lit", material.shader.name);
            Assert.AreSame(texture, material.GetTexture("_BaseColorMap"));
            Assert.That(Vector4.Distance(tint, material.GetColor("_BaseColor")), Is.LessThan(.00001f));
            Assert.AreEqual(1, material.GetFloat("_SurfaceType"));
            Assert.AreEqual(1, material.GetFloat("_AlphaCutoffEnable"));
        }
        finally { Object.DestroyImmediate(material); Object.DestroyImmediate(texture); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CollisionMeshIsOnlyAssignedWhenVisualAndColliderShareLocalSpace(bool transformed)
    {
        var root = new GameObject("Collision fixture");
        try
        {
            var collider = root.AddComponent<MeshCollider>();
            var child = GameObject.CreatePrimitive(PrimitiveType.Cube); child.transform.SetParent(root.transform, false);
            Object.DestroyImmediate(child.GetComponent<BoxCollider>());
            if (transformed) child.transform.localRotation = Quaternion.Euler(0, 45, 0);
            var mesh = child.GetComponent<MeshFilter>().sharedMesh;
            SceneIntegrityRepair.RepairHierarchy(root, "test fixture");
            if (transformed) Assert.IsNull(collider.sharedMesh, "A transformed child requires conversion, never direct assignment.");
            else Assert.AreSame(mesh, collider.sharedMesh);
        }
        finally { Object.DestroyImmediate(root); }
    }
}
#endif
