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
    static CharacterEffectDiagnostics()
    {
        EditorApplication.update+=Poll;
        EditorApplication.playModeStateChanged+=mode=>{
            if(mode!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool("Lit.CharacterVfxPreview",false))return;
            var previous=SessionState.GetString("Lit.CharacterVfxPreview.scene","");
            UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previous)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            SessionState.SetBool("Lit.CharacterVfxPreview",false);
            EditorApplication.isPaused=SessionState.GetBool("Lit.CharacterVfxPreview.paused",false);
        };
    }
    private static void Poll()
    {
        if(SessionState.GetBool("Lit.CharacterVfxPreview",false)&&EditorApplication.isPlaying)
        {
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat("Lit.CharacterVfxPreview.deadline",0))
            {Debug.LogWarning("Character effect preview timed out.");EditorApplication.isPlaying=false;return;}
            EditorApplication.QueuePlayerLoopUpdate();
            var effect=Resources.FindObjectsOfTypeAll<CharacterEffect>().FirstOrDefault(e=>!EditorUtility.IsPersistent(e)&&e.isActiveAndEnabled&&e.meshRenderer is SkinnedMeshRenderer&&e.gameObject.scene.IsValid());
            if(effect!=null&&Time.frameCount>10)
            {
                float started=SessionState.GetFloat("Lit.CharacterVfxPreview.started",0);
                if(started==0){effect.StartEffect();SessionState.SetFloat("Lit.CharacterVfxPreview.started",(float)EditorApplication.timeSinceStartup);}
                else if(EditorApplication.timeSinceStartup-started>2)
                {
                    try{CaptureActiveBody();Audit();}
                    finally{EditorApplication.isPlaying=false;}
                }
            }
        }
        const string request="Library/CharacterEffectDiagnostics.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        string mode=File.ReadAllText(request).Trim();File.Delete(request);
        if(mode=="preview"){PreviewBody();return;}
        if(mode=="recompile")RecompileCharacterGraphs();
        if(mode=="validate"){CharacterEffectSamplingValidator.Validate();return;}
        if(mode=="capture"){CaptureActiveBody();return;}
        if(mode=="play")
            foreach(var effect in Resources.FindObjectsOfTypeAll<CharacterEffect>().Where(e=>!EditorUtility.IsPersistent(e)&&e.gameObject.scene.IsValid()&&e.isActiveAndEnabled))effect.StartEffect();
        Audit();
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
        ExportShaders();
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)); File.WriteAllText(ReportPath,report.ToString());
        Debug.Log("[CharacterEffectDiagnostics] Read-only report: "+ReportPath);
    }
    [MenuItem("Lit/VFX/Recompile Character Effect Graphs")]
    public static void RecompileCharacterGraphs()
    {
        const string folder="Assets/VFX/CharacterEffect/Vfx Assets/Character Effects/Core/";
        AssetDatabase.ImportAsset(folder+"Blocks/Character Mesh Spawn.vfxblock",ImportAssetOptions.ForceUpdate);
        foreach(string path in Directory.GetFiles(folder+"Graphs","*.vfx"))AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
    }
    [MenuItem("Lit/VFX/Verify Body Effect In Laboratory")]
    public static void PreviewBody()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        SessionState.SetString("Lit.CharacterVfxPreview.scene",UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene!=null?AssetDatabase.GetAssetPath(UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene):"");
        SessionState.SetFloat("Lit.CharacterVfxPreview.started",0);
        SessionState.SetFloat("Lit.CharacterVfxPreview.deadline",(float)EditorApplication.timeSinceStartup+90);
        SessionState.SetBool("Lit.CharacterVfxPreview.paused",EditorApplication.isPaused);
        EditorApplication.isPaused=false;
        SessionState.SetBool("Lit.CharacterVfxPreview",true);
        UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Characters/3_Enemy/Iluvilirae/Iluvilirae_Test.unity");
        EditorApplication.isPlaying=true;
    }
    [MenuItem("Lit/VFX/Capture Active Character Effect")]
    public static void CaptureActiveBody()
    {
        var effect=Resources.FindObjectsOfTypeAll<CharacterEffect>().FirstOrDefault(e=>!EditorUtility.IsPersistent(e)&&e.isActiveAndEnabled&&e.meshRenderer is SkinnedMeshRenderer&&e.gameObject.scene.IsValid());
        if(effect==null)throw new InvalidOperationException("No active character effect to capture.");
        var go=new GameObject("CharacterEffect verification camera");
        var occluders=UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)
            .Where(r=>r.enabled&&r.GetComponentInParent<LitBrainsEnemy>()!=null).ToArray();
        try
        {
            var camera=go.AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=40;
            var actor=effect.GetComponentInParent<SquadCharacterController>();
            var forward=actor!=null?actor.transform.forward:Vector3.forward;
            var animator=actor!=null?actor.GetComponent<CharacterAnimationController>()?.Animator:null;
            var head=animator!=null&&animator.isHuman?animator.GetBoneTransform(HumanBodyBones.Head):null;
            var foot=animator!=null&&animator.isHuman?animator.GetBoneTransform(HumanBodyBones.LeftFoot):null;
            var center=head!=null&&foot!=null?(head.position+foot.position)*.5f:effect.meshRenderer.bounds.center;
            foreach(var renderer in occluders)renderer.enabled=false;
            go.transform.position=center+forward*3.8f+Vector3.up*.3f;go.transform.LookAt(center);
            ActorAnimationLabValidation.ConfigureInspectionCamera(camera);
            Directory.CreateDirectory("Library/CharacterEffectDiagnostics");
            ActorAnimationLabValidation.RenderSnapshot(camera,"Library/CharacterEffectDiagnostics/Body-after.png");
        }
        finally{foreach(var renderer in occluders)if(renderer!=null)renderer.enabled=true;UnityEngine.Object.DestroyImmediate(go);}
    }
    private static void ExportShaders()
    {
        Directory.CreateDirectory("Library/CharacterEffectDiagnostics/Shaders");
        foreach(string path in CharacterEffectSamplingValidator.GraphPaths)
        {
            var sources=CharacterEffectSamplingValidator.GeneratedSources(path);
            for(int i=0;i<sources.Length;i++)
                File.WriteAllText("Library/CharacterEffectDiagnostics/Shaders/"+Path.GetFileNameWithoutExtension(path)+"_"+i+".hlsl",sources[i]);
        }
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
        for(int submesh=0;submesh<mesh.subMeshCount;submesh++)
            report.AppendLine("submesh="+submesh+" baseVertex="+mesh.GetBaseVertex(submesh)+" descriptor="+mesh.GetSubMesh(submesh).firstVertex+" start="+mesh.GetIndexStart(submesh)+" count="+mesh.GetIndexCount(submesh));
        foreach(var attribute in mesh.GetVertexAttributes()) report.AppendLine("attribute="+attribute.attribute+" format="+attribute.format+" dimension="+attribute.dimension+" stream="+attribute.stream);
        if(skin!=null) report.AppendLine("rootBone="+(skin.rootBone!=null?PathOf(skin.rootBone):"NONE")+" offscreen="+skin.updateWhenOffscreen+" localBounds="+skin.localBounds);
        if(skin!=null)
        {
            var baked=new Mesh();
            try{skin.BakeMesh(baked);report.AppendLine("currentBakedBounds="+baked.bounds+" localToWorld="+skin.localToWorldMatrix);}
            finally{UnityEngine.Object.DestroyImmediate(baked);}
        }
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
