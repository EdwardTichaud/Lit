using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using INab.VFXAssets;

/// <summary>Read-only diagnostics of the actual loaded effects, including GPU sampling data.</summary>
[InitializeOnLoad]
public static class CharacterEffectDiagnostics
{
    public const string ReportPath="Library/CharacterEffectDiagnostics/report.txt";
    static CharacterEffectDiagnostics() { EditorApplication.update+=Poll; }
    private static void Poll()
    {
        const string request="Library/CharacterEffectDiagnostics.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        File.Delete(request); Audit();
    }
    [MenuItem("Lit/VFX/Audit Character Effects (Read Only)")]
    public static void Audit()
    {
        var report=new StringBuilder("Unity "+Application.unityVersion+" | "+SystemInfo.graphicsDeviceType+" | playing="+Application.isPlaying+"\n");
        foreach (var effect in Resources.FindObjectsOfTypeAll<CharacterEffect>().Where(e=>!EditorUtility.IsPersistent(e)&&e.gameObject.scene.IsValid()))
        {
            report.AppendLine("\nEFFECT "+PathOf(effect.transform)+" active="+effect.isActiveAndEnabled);
            try { Inspect(effect,report); } catch(Exception e) {report.AppendLine(e.ToString());}
        }
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)); File.WriteAllText(ReportPath,report.ToString());
        Debug.Log("[CharacterEffectDiagnostics] Read-only report: "+ReportPath);
    }
    private static string PathOf(Transform t) => t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
    private static void Inspect(CharacterEffect effect,StringBuilder report)
    {
        var renderer=effect.meshRenderer;
        report.AppendLine("renderer="+(renderer!=null?PathOf(renderer.transform):"MISSING")+" prefab="+(effect.effectPrefab!=null?AssetDatabase.GetAssetPath(effect.effectPrefab):"MISSING"));
        var vfx=effect.vfxComponent;
        report.AppendLine("instance="+(effect.instantiatedEffectPrefab!=null?PathOf(effect.instantiatedEffectPrefab.transform):"MISSING")+" vfx="+(vfx!=null?vfx.name:"MISSING"));
        if (vfx!=null) report.AppendLine("graph="+AssetDatabase.GetAssetPath(vfx.visualEffectAsset)+" hasBuffer="+vfx.HasGraphicsBuffer("UniformMeshBuffer")+" alive="+vfx.aliveParticleCount+" culled="+vfx.culled);
        var skin=renderer as SkinnedMeshRenderer;
        Mesh mesh=skin!=null?skin.sharedMesh:(renderer!=null?renderer.GetComponent<MeshFilter>()?.sharedMesh:null);
        if (mesh==null) return;
        report.AppendLine("mesh="+mesh.name+" readable="+mesh.isReadable+" vertices="+mesh.vertexCount+" submeshes="+mesh.subMeshCount+" bounds="+mesh.bounds);
        foreach(var attribute in mesh.GetVertexAttributes()) report.AppendLine("attribute="+attribute.attribute+" format="+attribute.format+" dimension="+attribute.dimension+" stream="+attribute.stream);
        if(skin!=null) report.AppendLine("rootBone="+(skin.rootBone!=null?PathOf(skin.rootBone):"NONE")+" offscreen="+skin.updateWhenOffscreen+" localBounds="+skin.localBounds);
        var baker=effect.meshBaker;
        var buffer=baker?.m_Buffer;
        report.AppendLine("bufferValid="+(buffer!=null&&buffer.IsValid())+" cpuSamples="+(baker?.m_BakedSampling?.Length??0));
        if(buffer==null || !buffer.IsValid() || !mesh.isReadable) return;
        report.AppendLine("bufferCount="+buffer.count+" stride="+buffer.stride+" expectedStride="+System.Runtime.InteropServices.Marshal.SizeOf(typeof(BarycentricTriangleSampling)));
        var samples=new BarycentricTriangleSampling[buffer.count]; buffer.GetData(samples);
        var vertices=mesh.vertices; var triangles=mesh.triangles; int invalid=0;
        var bounds=new Bounds(); bool first=true;
        foreach(var s in samples)
        {
            int index=checked((int)s.index*3);
            if(index<0 || index+2>=triangles.Length) {invalid++;continue;}
            var point=vertices[triangles[index]]*s.coord.x+vertices[triangles[index+1]]*s.coord.y+vertices[triangles[index+2]]*(1-s.coord.x-s.coord.y);
            if(first) {bounds=new Bounds(point,Vector3.zero);first=false;} else bounds.Encapsulate(point);
        }
        report.AppendLine("GPU_DATA distinctTriangles="+samples.Select(s=>s.index).Distinct().Count()+" invalid="+invalid+" expectedBindPoseBounds="+bounds);
        if (samples.Length>0) report.AppendLine("firstSample="+samples[0].index+" barycentric="+samples[0].coord);
    }
}
