using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

/// <summary>Reject stale/mismatched support contracts that are actually dependencies of a build.</summary>
public sealed class NativeFootSupportBuildValidator : IPreprocessBuildWithReport
{
    public int callbackOrder=>0;
    public void OnPreprocessBuild(BuildReport report)
    {
        var scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
        foreach(string path in AssetDatabase.GetDependencies(scenes,true).Where(p=>p.EndsWith(".asset")))
        {
            var profile=AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>(path);
            if(profile==null||profile.footSupports==null)continue;
            try
            {
                NativeFootSupportPreparation.Validate(profile,profile.footSupports,true);
                if(profile.footSupports.runtimeValidationSignature!=profile.footSupports.dataSignature)throw new System.InvalidOperationException("Runtime verification certificate is stale");
            }
            catch(System.Exception e){throw new BuildFailedException(path+": "+e.Message);}
        }
    }
}
