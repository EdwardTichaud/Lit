using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class CharacterAnimationCleanup
{
    static void FinishEditorChanges()
    {
        report=JsonUtility.FromJson<Report>(File.ReadAllText(Output+"/report.json"));replacements.Clear();
        const string plain="Assets/Characters/1_Squad/Lucian/Animation/TwinSword_Attack15_Inplace.FBX";
        const string original="Assets/Characters/1_Squad/Lucian/Animation/TwinSword_Attack15_Inplace_0679cb06.FBX";
        if(File.Exists(original)&&File.Exists(plain))
        {
            if(Hash(original)!=Hash(plain)||!ActorFacingCalibration.SameAuthoring(ActorAnimationAudit.Source(original),ActorAnimationAudit.Source(plain)))
                throw new InvalidOperationException("Attack15 files are not equivalent imported animations");
            Backup(original);Backup(plain);MapSubAssets(plain,original);
            foreach(var path in AuthoredAssets().Where(ReferencesReplacement).ToArray())
            {
                Backup(path);
                if(path.EndsWith(".unity")){RewriteScene(path);continue;}
                if(path.EndsWith(".prefab"))
                {
                    var root=PrefabUtility.LoadPrefabContents(path);
                    try{bool changed=false;foreach(var c in root.GetComponentsInChildren<Component>(true))changed|=RemapProperties(c,Resolve);if(changed)PrefabUtility.SaveAsPrefabAsset(root,path);}
                    finally{PrefabUtility.UnloadPrefabContents(root);}
                }
                else foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(path))if(RemapProperties(asset,Resolve))AssetDatabase.SaveAssetIfDirty(asset);
            }
            if(!AssetDatabase.DeleteAsset(plain))throw new InvalidOperationException("Cannot remove exact animation duplicate");
            string error=AssetDatabase.MoveAsset(original,plain);if(!string.IsNullOrEmpty(error))throw new InvalidOperationException(error);
            report.deleted.Add(original+" (consolidated under unsuffixed path, original GUID retained)");
        }
        foreach(string folder in new[]{ExecutionFolder+"/EditorSources/Materials",ExecutionFolder+"/EditorSources",ExecutionFolder})
            if(AssetDatabase.IsValidFolder(folder)&&Directory.GetFiles(folder,"*",SearchOption.AllDirectories).All(p=>p.EndsWith(".meta")))
            {AssetDatabase.DeleteAsset(folder);report.deleted.Add(folder);}
        const string oldValidation="Assets/StoneGolem/Animations/Calibrated/Executed1_Validation.json";
        if(File.Exists(oldValidation)){Backup(oldValidation);if(!report.pendingClosedEditor.Contains(oldValidation))report.pendingClosedEditor.Add(oldValidation);}
        File.WriteAllText(Output+"/report.json",JsonUtility.ToJson(report,true));
        File.WriteAllText(Path.Combine(report.archive,"report.json"),JsonUtility.ToJson(report,true));
    }
    static void ValidateCleanup()
    {
        var report=JsonUtility.FromJson<Report>(File.ReadAllText(Output+"/report.json"));
        var avatar=AssetDatabase.LoadAllAssetsAtPath(GolemMain).OfType<Avatar>().SingleOrDefault();
        if(avatar==null||!avatar.isHuman||!avatar.isValid)throw new InvalidOperationException("Canonical Golem Humanoid avatar missing");
        foreach(var path in report.deleted.Where(p=>!p.Contains(" (")))
            if(AssetDatabase.LoadMainAssetAtPath(path)!=null)throw new InvalidOperationException("Retired asset recreated: "+path);
        CounterHitSignalValidation.Run();
        File.WriteAllText(Output+"/validation.txt","PASS: Golem canonical Humanoid avatar valid; retired generated assets absent; "+File.ReadAllText("Library/CounterHitSignals.validation.txt"));
    }
}
