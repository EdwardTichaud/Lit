using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Checks the compiled shader, not merely the CPU sampling buffer.</summary>
[InitializeOnLoad]
public sealed class CharacterEffectSamplingValidator : IPreprocessBuildWithReport
{
    public static readonly string[] GraphPaths=new[]{"Character Local Connect","Character Fire","Character Smoke","Character Noisy","Character Points"}
        .Select(name=>"Assets/VFX/CharacterEffect/Vfx Assets/Character Effects/Core/Graphs/"+name+".vfx").ToArray();
    private const string CheckedKey="Lit.CharacterEffectSamplingChecked";
    static CharacterEffectSamplingValidator()=>EditorApplication.update+=CheckAfterUpgrade;
    private static void CheckAfterUpgrade()
    {
        if(SessionState.GetBool(CheckedKey,false)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        SessionState.SetBool(CheckedKey,true);
        if(GraphPaths.Any(path=>!ConsumesSurfaceSamples(GeneratedSources(path))))
        {
            CharacterEffectDiagnostics.RecompileCharacterGraphs();
            Validate();
            Debug.Log("[CharacterEffect] Rebuilt stale compiled surface-sampling shaders.");
        }
    }
    public int callbackOrder=>10;
    public void OnPreprocessBuild(BuildReport report)=>Validate();
    [MenuItem("Lit/VFX/Validate Compiled Character Sampling")]
    public static void Validate()
    {
        var report=new StringBuilder();
        foreach(string path in GraphPaths)
        {
            bool valid=ConsumesSurfaceSamples(GeneratedSources(path));
            report.AppendLine((valid?"PASS ":"FAIL ")+path);
            if(!valid)throw new BuildFailedException("CharacterEffect shader ignores mesh surface samples: "+path+". Run Lit/VFX/Recompile Character Effect Graphs.");
        }
        Directory.CreateDirectory("Library/CharacterEffectDiagnostics");
        File.WriteAllText("Library/CharacterEffectDiagnostics/shader-validation.txt",report.ToString());
    }
    public static bool ConsumesSurfaceSamples(string[] sources)=>sources.Any(source=>
        source.Contains("StructuredBuffer<BarycentricTriangleSampling>") &&
        Regex.Matches(source,@"SampleStructuredBuffer\(").Count>1 && source.Contains(".coord")&&source.Contains(".index"));
    public static string[] GeneratedSources(string path)
    {
        AssetDatabase.LoadMainAssetAtPath(path);
        var flags=BindingFlags.Public|BindingFlags.NonPublic;
        var type=Type.GetType("UnityEditor.VFX.VisualEffectResource, UnityEditor.VFXModule",false);
        if(type==null)throw new BuildFailedException("Unity VFX shader inspection is unavailable.");
        var resource=type.GetMethod("GetResourceAtPath",flags|BindingFlags.Static,null,new[]{typeof(string)},null)?.Invoke(null,new object[]{path});
        if(resource==null)throw new BuildFailedException("Missing CharacterEffect graph: "+path);
        int count=(int)type.GetMethod("GetShaderSourceCount",flags|BindingFlags.Instance).Invoke(resource,null);
        var read=type.GetMethod("GetShaderSource",flags|BindingFlags.Instance);
        return Enumerable.Range(0,count).Select(i=>(string)read.Invoke(resource,new object[]{i})).ToArray();
    }
}
