#if UNITY_INCLUDE_TESTS
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public sealed class SceneIntegrityCampaignTests
{
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(5)]
    public void PortalTemplateHasMatchingVisibleAndPhysicalGeometry(int number)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Interactive/Portal/Portal_{number}.prefab");
        var surface = prefab.transform.Find("Portal");
        var mesh = surface.GetComponent<MeshFilter>().sharedMesh;
        Assert.IsNotNull(mesh);
        Assert.Greater(mesh.vertexCount, 0);
        Assert.AreSame(mesh, surface.GetComponent<MeshCollider>().sharedMesh);
        Assert.IsNotNull(surface.GetComponent<MeshRenderer>().sharedMaterial);
    }
    [TestCase("Item")]
    [TestCase("Building")]
    public void WorldInformationFrameKeepsItsSpriteAndUsesValidUiMaterial(string kind)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/UI/World/UI_World_Local{kind}InformationsPanel.prefab");
        var frame = prefab.transform.Find("Root/Frame").GetComponent<Image>();
        Assert.IsNotNull(frame.sprite, "The authored frame artwork must remain assigned.");
        Assert.IsNotNull(frame.material);
        Assert.AreEqual("UI/Default", frame.material.shader.name);
        Assert.Greater(frame.color.a, 0);
        var reference = new SerializedObject(frame).FindProperty("m_Material");
        Assert.IsTrue(reference.objectReferenceEntityIdValue == default, "Use the default explicitly, without a dangling material reference.");
    }

    [Test]
    public void SavedBalconyFloorLoadsWithItsAuthoredGeometryCollisionAndIceMaterial()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/District_1/District_1_Balcony_Environment.unity");
        try
        {
            var floor = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UnityEngine.ProBuilder.ProBuilderMesh>(true)).Single();
            var visual = floor.GetComponent<MeshFilter>().sharedMesh;
            Assert.IsNotNull(visual);
            Assert.AreEqual(24, visual.vertexCount);
            Assert.AreEqual(36, visual.triangles.Length);
            Assert.AreSame(visual, floor.GetComponent<MeshCollider>().sharedMesh);
            Assert.IsTrue(floor.GetComponent<MeshCollider>().enabled);
            Assert.IsFalse(floor.GetComponent<MeshCollider>().isTrigger);
            var material = floor.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.IsNotNull(material);
            Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader));
            Assert.IsTrue(LitTacticalMaskProperties.Supports(material), "The replacement floor must participate in the tactical visibility mask.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void LoadingOrbHasGeometryForEveryEnabledMeshEmitter()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scenes/MainMenu/Loading_Orbe.prefab");
        var missing = new System.Collections.Generic.List<string>();
        foreach (var renderer in prefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
            if (renderer.enabled && renderer.renderMode == ParticleSystemRenderMode.Mesh)
            {
                if (renderer.mesh != null) continue;
                string path = renderer.name;
                for (var parent = renderer.transform.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
                var slot = new SerializedObject(renderer).FindProperty("m_Mesh");
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer, out string guid, out long localId);
                missing.Add(path + "; rendererId=" + localId + "; meshCount=" + renderer.meshCount +
                    "; serialized=" + slot.objectReferenceValue + "; entity=" + slot.objectReferenceEntityIdValue);
            }
        Assert.IsEmpty(missing, "Every enabled mesh emitter needs geometry: " + string.Join("\n", missing));
    }
}
#endif
