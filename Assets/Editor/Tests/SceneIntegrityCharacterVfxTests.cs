#if UNITY_INCLUDE_TESTS
using System.Linq;
using INab.VFXAssets;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

public sealed class SceneIntegrityCharacterVfxTests
{
    [TestCase("Dark")]
    [TestCase("Fire")]
    [TestCase("Ice Fire")]
    [TestCase("Mist")]
    [TestCase("Rage")]
    public void CharacterGraphUsesTheActualStaticOrAnimatedActorWithoutADemoMesh(string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/CharacterEffect/Vfx Assets/Character Effects/Effect Prefabs/" + name + ".prefab");
        var authored = prefab.GetComponent<VisualEffect>();
        Assert.IsNotNull(authored.visualEffectAsset);
        var entries = new SerializedObject(authored).FindProperty("m_PropertySheet.m_NamedObject.m_Array");
        foreach (int index in Enumerable.Range(0, entries.arraySize))
        {
            var entry = entries.GetArrayElementAtIndex(index);
            if (entry.FindPropertyRelative("m_Name").stringValue != MeshSetup.MeshProperty) continue;
            var reference = entry.FindPropertyRelative("m_Value");
            Assert.IsTrue(reference.objectReferenceEntityIdValue == default, "The missing demo body must not remain serialized.");
        }

        var carrier = new GameObject("Inactive graph binding fixture"); carrier.SetActive(false);
        var actor = GameObject.CreatePrimitive(PrimitiveType.Cube); actor.SetActive(false);
        var animated = new GameObject("Animated actor binding fixture"); animated.SetActive(false);
        try
        {
            var effect = carrier.AddComponent<VisualEffect>(); effect.visualEffectAsset = authored.visualEffectAsset;
            Assert.IsTrue(effect.HasMesh(MeshSetup.MeshProperty));
            Assert.IsTrue(effect.HasSkinnedMeshRenderer(MeshSetup.SkinnedMeshProperty));
            Assert.IsTrue(effect.HasBool(MeshSetup.UseSkinnedMeshProperty));

            MeshSetup.SetupRenderer(actor.GetComponent<MeshRenderer>(), effect);
            Assert.IsFalse(effect.GetBool(MeshSetup.UseSkinnedMeshProperty));
            Assert.AreSame(actor.GetComponent<MeshFilter>().sharedMesh, effect.GetMesh(MeshSetup.MeshProperty));

            var skin = animated.AddComponent<SkinnedMeshRenderer>(); skin.enabled = false;
            skin.sharedMesh = actor.GetComponent<MeshFilter>().sharedMesh;
            MeshSetup.SetupRenderer(skin, effect);
            Assert.IsTrue(effect.GetBool(MeshSetup.UseSkinnedMeshProperty));
            Assert.AreSame(skin, effect.GetSkinnedMeshRenderer(MeshSetup.SkinnedMeshProperty));
        }
        finally
        {
            Object.DestroyImmediate(carrier); Object.DestroyImmediate(actor); Object.DestroyImmediate(animated);
        }
    }
}
#endif
