#if UNITY_INCLUDE_TESTS
using System;
using System.Linq;
using INab.VFXAssets;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.VFX.Utility;
using Object = UnityEngine.Object;

public sealed class CharacterEffectLifecycleTests
{
    private GameObject actor;
    private CharacterEffect effect;

    [SetUp] public void Create()
    {
        actor=GameObject.CreatePrimitive(PrimitiveType.Cube);
        actor.SetActive(false);
        effect=actor.AddComponent<CharacterEffect>();
        effect.meshRenderer=actor.GetComponent<Renderer>();
        effect.effectPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/CharacterEffect/Vfx Assets/Character Effects/Effect Prefabs/Holy.prefab");
        Assert.That(effect.effectPrefab,Is.Not.Null);
    }
    [TearDown] public void Destroy() { if(actor!=null)Object.DestroyImmediate(actor); }

    [Test] public void ActivationInitializesAndPlaysWithoutStartOrSceneScan()
    {
        Assert.That(effect.playOnEnable,Is.True);
        actor.SetActive(true);
        Assert.That(effect.EnsureReady(),Is.True);
        Assert.That(effect.currentEffectState,Is.EqualTo(UniformMeshSample.EffectState.On));
        Assert.That(effect.effectState,Is.EqualTo(effect.currentEffectState));
        Assert.That(effect.vfxComponent.GetMesh(MeshSetup.MeshProperty),Is.SameAs(actor.GetComponent<MeshFilter>().sharedMesh));
        var binder=effect.vfxBinder.GetPropertyBinders<INab.CommonVFX.VFXLossyTransformBinder>().Single();
        Assert.That(binder.Target,Is.SameAs(actor.transform));
        Assert.That(binder.IsValid(effect.vfxComponent),Is.True);
        var instance=effect.instantiatedEffectPrefab;
        effect.EnsureReady();
        Assert.That(effect.instantiatedEffectPrefab,Is.SameAs(instance));
    }

    [Test] public void ManualOptOutAndLegacyEventsUseTheSameState()
    {
        effect.playOnEnable=false;
        actor.SetActive(true);
        Assert.That(effect.currentEffectState,Is.EqualTo(UniformMeshSample.EffectState.Off));
        effect.PlayEffect_CharacterEffect();
        Assert.That(effect.currentEffectState,Is.EqualTo(UniformMeshSample.EffectState.On));
        effect.StopEffect_CharacterEffect();
        effect.SendMessage("Update");
        Assert.That(effect.currentEffectState,Is.EqualTo(UniformMeshSample.EffectState.Off));
        Assert.That(effect.effectState,Is.EqualTo(effect.currentEffectState));
        effect.SetupVfxGraph();
        Assert.That(effect.currentEffectState,Is.EqualTo(UniformMeshSample.EffectState.Off));
    }

    [Test] public void PooledAndComponentReactivationRestoreBuffersAndPlayback()
    {
        actor.SetActive(true);
        var instance=effect.instantiatedEffectPrefab;
        var buffer=effect.meshBaker.m_Buffer;
        actor.SetActive(false);
        Assert.That(buffer.IsValid(),Is.False);
        actor.SetActive(true);
        Assert.That(effect.meshBaker.m_Buffer.IsValid(),Is.True);
        Assert.That(effect.currentEffectState,Is.EqualTo(UniformMeshSample.EffectState.On));
        Assert.That(effect.instantiatedEffectPrefab,Is.SameAs(instance));
        effect.enabled=false;
        effect.enabled=true;
        Assert.That(effect.EnsureReady(),Is.True);
        Assert.That(effect.currentEffectState,Is.EqualTo(UniformMeshSample.EffectState.On));
    }

    [Test] public void ShowingTheRendererRestartsOnlyAutomaticEffects()
    {
        actor.SetActive(true);
        effect.meshRenderer.enabled=false;effect.SendMessage("Update");
        Assert.That(effect.currentEffectState,Is.EqualTo(UniformMeshSample.EffectState.Off));
        effect.meshRenderer.enabled=true;effect.SendMessage("Update");
        Assert.That(effect.currentEffectState,Is.EqualTo(UniformMeshSample.EffectState.On));
        effect.StopEffect();effect.SendMessage("Update");
        Assert.That(effect.currentEffectState,Is.EqualTo(UniformMeshSample.EffectState.Off));
    }

    [Test] public void MissingBinderDoesNotBlockMeshInitialization()
    {
        effect.instantiatedEffectPrefab=Object.Instantiate(effect.effectPrefab,actor.transform);
        foreach(var binder in effect.instantiatedEffectPrefab.GetComponents<VFXBinderBase>())Object.DestroyImmediate(binder);
        Object.DestroyImmediate(effect.instantiatedEffectPrefab.GetComponent<VFXPropertyBinder>());
        actor.SetActive(true);
        Assert.That(effect.EnsureReady(),Is.True);
        Assert.That(effect.vfxBinder,Is.Not.Null);
        Assert.That(effect.vfxBinder.GetPropertyBinders<INab.CommonVFX.VFXLossyTransformBinder>().Count(),Is.EqualTo(1));
    }

    [Test] public void GpuSamplingRoundTripRetainsTriangleIndicesAndBarycentricCoordinates()
    {
        actor.SetActive(true);
        var baker=effect.meshBaker;
        var gpu=new BarycentricTriangleSampling[baker.m_Buffer.count];
        baker.m_Buffer.GetData(gpu);
        Assert.That(gpu.Select(s=>s.index).Distinct().Count(),Is.GreaterThan(1));
        for(int i=0;i<gpu.Length;i++)
        {
            Assert.That(gpu[i].index,Is.EqualTo(baker.m_BakedSampling[i].index));
            Assert.That(gpu[i].coord,Is.EqualTo(baker.m_BakedSampling[i].coord));
        }
        Assert.That(EditorJsonUtility.ToJson(effect),Does.Contain("m_BakedSampling"));
    }

    [Test] public void RealLucianBodyAndSwordMeshesCanBothInitialize()
    {
        var lucian=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/1_Squad/Lucian/Player_Model_Lucian.prefab");
        var configured=lucian.GetComponentsInChildren<CharacterEffect>(true);
        Assert.That(configured.Any(e=>e.meshRenderer is SkinnedMeshRenderer),Is.True);
        Assert.That(configured.Any(e=>e.meshRenderer is MeshRenderer),Is.True);
        foreach(var source in configured)
        {
            var mesh=UniformMeshSamplingHelper.RendererToMesh(source.meshRenderer);
            Assert.That(mesh,Is.Not.Null);
            Assert.That(mesh.isReadable,Is.True,source.name);
            actor.SetActive(false);
            if(source.meshRenderer is SkinnedMeshRenderer)
            {
                var skinObject=new GameObject("Animated mesh source");
                skinObject.transform.SetParent(actor.transform,false);
                var skin=skinObject.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh=mesh;effect.meshRenderer=skin;
            }
            else
            {
                actor.GetComponent<MeshFilter>().sharedMesh=mesh;
                effect.meshRenderer=actor.GetComponent<MeshRenderer>();
            }
            actor.SetActive(true);
            Assert.That(effect.EnsureReady(),Is.True,source.name);
            Assert.That(effect.vfxComponent.GetBool(MeshSetup.UseSkinnedMeshProperty),Is.EqualTo(source.IsSkinnedMesh));
            Assert.That(effect.meshBaker.m_BakedSampling.Length,Is.GreaterThan(1));
        }
    }
}
#endif
