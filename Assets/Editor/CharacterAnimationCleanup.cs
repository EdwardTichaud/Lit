using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static partial class CharacterAnimationCleanup
{
    const string Request = "Library/CharacterAnimationCleanup.request";
    const string Output = "Library/CharacterAnimationCleanup";
    const string ExecutionFolder = "Assets/Characters/AnimationPresentation/FacingCalibration/Execution";
    const string GolemMain = "Assets/StoneGolem/Mesh/StoneGolem.fbx";
    const string GolemHelper = ExecutionFolder + "/EditorSources/StoneGolem_Humanoid.fbx";
    public const string JuggernautController = "Assets/Characters/3_Enemy/Juggernaut/BrainsProduction/Production.controller";
    public const string GolemController = "Assets/Characters/3_Enemy/Juggernaut_v2/StoneGolem_Humanoid.controller";
    [Serializable] public sealed class Report
    {
        public string archive, status;
        public int mergedStates, rewrittenAssets;
        public List<string> deleted = new List<string>(), pendingClosedEditor = new List<string>(), notes = new List<string>();
    }
    static Report report;
    static void Checkpoint(string status) { report.status=status;File.WriteAllText(Output+"/report.json",JsonUtility.ToJson(report,true)); }
    static readonly Dictionary<Object,Object> replacements = new Dictionary<Object,Object>();
    static readonly List<string> retired = new List<string>();
    static readonly Dictionary<string,string> pathReplacements = new Dictionary<string,string>();
    static CharacterAnimationCleanup() => EditorApplication.update += Poll;
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var command = File.ReadAllText(Request).Trim(); File.Delete(Request);
        try
        {
            if(command=="apply")Apply();
            else if(command=="finish-editor")FinishEditorChanges();
            else if(command=="validate")ValidateCleanup();
            else throw new InvalidOperationException("Unknown cleanup command");
            File.WriteAllText(Output + "/result.txt", "PASS: "+command);
        }
        catch (Exception e) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/result.txt", e.ToString()); Debug.LogException(e); }
    }
    static string Hash(string path)
    { using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-",""); }
    static void Backup(string path)
    {
        if (!File.Exists(path)) return;
        string destination = Path.Combine(report.archive, path);
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        if (!File.Exists(destination)) File.Copy(path, destination);
        if (File.Exists(path + ".meta") && !File.Exists(destination + ".meta")) File.Copy(path + ".meta", destination + ".meta");
    }
    static T Asset<T>(string path) where T:Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing asset: " + path);
    static void Retire(string path) { if (AssetDatabase.LoadMainAssetAtPath(path) != null && !retired.Contains(path)) { Backup(path); retired.Add(path); } }
    static void MapSubAssets(string source, string destination)
    {
        var targets = AssetDatabase.LoadAllAssetsAtPath(destination);
        foreach (var old in AssetDatabase.LoadAllAssetsAtPath(source))
        {
            if (old == null) continue;
            Object replacement = null;
            if (old is Avatar) replacement = targets.OfType<Avatar>().SingleOrDefault();
            else if (old is AnimationClip && !old.name.StartsWith("__preview__")) replacement = targets.OfType<AnimationClip>().SingleOrDefault(c => !c.name.StartsWith("__preview__"));
            else if (old is Mesh || old is Material) replacement = targets.SingleOrDefault(t => t.GetType() == old.GetType() && t.name == old.name);
            if (replacement != null) replacements[old] = replacement;
        }
    }
    static IEnumerable<string> AuthoredAssets() => AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/",StringComparison.Ordinal) &&
        new[]{".prefab",".unity",".asset",".controller",".overrideController",".playable",".anim",".mat"}.Contains(Path.GetExtension(p).ToLowerInvariant()));
    static bool ReferencesReplacement(string path)
    {
        if (retired.Contains(path) || !File.Exists(path)) return false;
        string text = File.ReadAllText(path);
        foreach (var key in replacements.Keys)
        {
            string source = AssetDatabase.GetAssetPath(key);
        if (!string.IsNullOrEmpty(source) && text.Contains(AssetDatabase.AssetPathToGUID(source))) return true;
        }
        return pathReplacements.Keys.Any(text.Contains);
    }
    static Object Resolve(Object value) => value != null && replacements.TryGetValue(value,out var result) ? result : value;
    static void RewriteScene(string path)
    {
        var existing = SceneManager.GetSceneByPath(path); bool opened = !existing.IsValid() || !existing.isLoaded;
        var scene = opened ? EditorSceneManager.OpenScene(path,OpenSceneMode.Additive) : existing;
        bool changed = false;
        try
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true).ToArray())
                {
                    if (t == null || !PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) != GolemHelper) continue;
                    PrefabUtility.ReplacePrefabAssetOfPrefabInstance(t.gameObject, Asset<GameObject>(GolemMain), new PrefabReplacingSettings {
                        objectMatchMode=ObjectMatchMode.ByHierarchy, prefabOverridesOptions=PrefabOverridesOptions.KeepAllPossibleOverrides, changeRootNameToAssetName=false
                    },InteractionMode.AutomatedAction); changed = true;
                }
                foreach (var component in root.GetComponentsInChildren<Component>(true)) changed |= RemapProperties(component,Resolve);
                changed |= ConfigureAnimators(root);
            }
            if (changed) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); report.rewrittenAssets++; }
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene,true); }
    }
    static bool ConfigureAnimators(GameObject root)
    {
        bool changed = false;
        foreach (var animator in root.GetComponentsInChildren<Animator>(true))
        {
            if (animator.runtimeAnimatorController == null) continue;
            string path=AssetDatabase.GetAssetPath(animator.runtimeAnimatorController);
            string model=path==GolemController?GolemMain:path==JuggernautController?"Assets/Characters/3_Enemy/Juggernaut/Iron_Juggernaut/Meshes/MidPoly.fbx":null;
            if(model==null)continue;
            var avatar=AssetDatabase.LoadAllAssetsAtPath(model).OfType<Avatar>().SingleOrDefault();
            if(avatar==null || !avatar.isValid)throw new InvalidOperationException("Invalid canonical avatar: "+model);
            if(animator.avatar!=avatar){animator.avatar=avatar;EditorUtility.SetDirty(animator);changed=true;}
        }
        return changed;
    }
    [MenuItem("Lit/Animation/Cleanup Recent Execution Assets And Character Controllers")]
    public static void Apply()
    {
        if (!File.Exists(GolemHelper))
            throw new InvalidOperationException("Cleanup source already retired. Keep the existing report; use validation instead of applying again.");
        Directory.CreateDirectory(Output);
        report=new Report {archive="D:/LitAssetArchive/2026-10-25-animation-cleanup-"+DateTime.UtcNow.ToString("HHmmss"),status="Running"};
        Directory.CreateDirectory(report.archive); replacements.Clear(); retired.Clear();pathReplacements.Clear();
        // Store the complete current worktree delta as well as every asset immediately before changing it.
        foreach(var path in AuthoredAssets().Where(p=>p.StartsWith(ExecutionFolder) || p.StartsWith("Assets/StoneGolem/Animations/Calibrated")))Backup(path);
        try
        {
            var sourceImporter=(ModelImporter)AssetImporter.GetAtPath(GolemHelper);
            var importer=(ModelImporter)AssetImporter.GetAtPath(GolemMain); Backup(GolemMain);
            if(sourceImporter==null || sourceImporter.animationType!=ModelImporterAnimationType.Human)throw new InvalidOperationException("Requested existing Golem Humanoid source unavailable");
            var description=sourceImporter.humanDescription;
            importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar=null;importer.humanDescription=description;importer.optimizeGameObjects=false;importer.SaveAndReimport();
            var golemAvatar=AssetDatabase.LoadAllAssetsAtPath(GolemMain).OfType<Avatar>().SingleOrDefault();
            if(golemAvatar==null || !golemAvatar.isHuman || !golemAvatar.isValid)throw new InvalidOperationException("Canonical Golem Humanoid avatar invalid");
            MapSubAssets(GolemHelper,GolemMain);
            Checkpoint("Canonical StoneGolem Humanoid avatar prepared");
            var groups = new[] {
                new[]{JuggernautController,"Assets/Characters/3_Enemy/Juggernaut_v2/Juggernaut_v2.controller","Assets/Characters/3_Enemy/Iluvilirae/Iluvilirae.controller","Assets/Characters/3_Enemy/Juggernaut/Juggernaut_Model.controller","Assets/Characters/3_Enemy/Juggernaut/Juggernaut.controller","Assets/Characters/3_Enemy/Juggernaut/Iron_Juggernaut/Animations/New Animator Controller.controller"},
                new[]{GolemController,"Assets/StoneGolem/StoneGolem Animator Controller.controller"},
                new[]{"Assets/Characters/3_Enemy/Iluvilirae/Lucian_ImpactLab.controller","Assets/Characters/1_Squad/Lucian/Lucian_CC5_Embed/Lucian_CC5_Character_Model_animator.controller"}
            };
            foreach(var group in groups)
            {
                var target=Asset<AnimatorController>(group[0]); Backup(group[0]);
                foreach(var oldPath in group.Skip(1))
                {
                    var old=AssetDatabase.LoadAssetAtPath<AnimatorController>(oldPath);if(old==null)continue;
                    Backup(oldPath); MergeGraph(old,target,true); replacements[old]=target;pathReplacements[oldPath]=group[0];Retire(oldPath);
                }
                Checkpoint("Merged controller graph: "+group[0]);
            }
            const string vendor="Assets/0 - UnityPackages/Fab/TwinBladesBundle/Twinblades_Expansion_V2/Animation/Inplace/Battle/";
            foreach(var pair in new[]{("Twinblades_Attack_Execution1_Inplace_Humanoid","Twinblades_Attack_Execution1_Inplace"),("Twinblades_Attack_Executed1_Inplace_Generic","Twinblades_Attack_Executed1_Inplace")})
            {
                string old=vendor+pair.Item1+".FBX",canonical=vendor+pair.Item2+".FBX";
                if(!File.Exists(old))continue;
                if(Hash(old)!=Hash(canonical))throw new InvalidOperationException("Execution copy changed: "+old);
                MapSubAssets(old,canonical);Retire(old);
            }
            string baked="Assets/StoneGolem/Animations/Calibrated/Twinblades_Attack_Executed1_Inplace_Generic.anim";
            if(AssetDatabase.LoadMainAssetAtPath(baked)!=null){MapSubAssets(baked,vendor+"Twinblades_Attack_Executed1_Inplace.FBX");Retire(baked);}
            foreach(var path in AuthoredAssets().Where(ReferencesReplacement).ToArray())
            {
                Backup(path);
                if(path.EndsWith(".unity")){RewriteScene(path);continue;}
                if(path.EndsWith(".prefab"))
                {
                    var root=PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        bool changed=false;foreach(var component in root.GetComponentsInChildren<Component>(true))changed|=RemapProperties(component,Resolve);
                        changed|=ConfigureAnimators(root);
                        if(changed){PrefabUtility.SaveAsPrefabAsset(root,path);report.rewrittenAssets++;}
                    }
                    finally{PrefabUtility.UnloadPrefabContents(root);}
                }
                else foreach(var value in AssetDatabase.LoadAllAssetsAtPath(path))if(RemapProperties(value,Resolve)){AssetDatabase.SaveAssetIfDirty(value);report.rewrittenAssets++;}
            }
            foreach(var code in AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/")&&p.EndsWith(".cs")&&!p.StartsWith("Assets/Editor/CharacterAnimationCleanup")).ToArray())
            {
                string original=File.ReadAllText(code),updated=original;
                foreach(var pair in pathReplacements)updated=updated.Replace("\""+pair.Key+"\"","\""+pair.Value+"\"");
                if(updated==original)continue;Backup(code);File.WriteAllText(code,updated,new System.Text.UTF8Encoding(false));report.notes.Add("Updated controller path in "+code);
            }
            Checkpoint("Serialized references remapped");
            // Imported-model prefab source references require replacement, not raw GUID editing.
            foreach(var scenePath in AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/")&&p.EndsWith(".unity")&&File.ReadAllText(p).Contains(AssetDatabase.AssetPathToGUID(GolemHelper))).ToArray())
            {Backup(scenePath);RewriteScene(scenePath);}
            foreach(var path in retired)
            {
                if(path.StartsWith("Assets/0 - UnityPackages/") || path.StartsWith("Assets/StoneGolem/") || path.StartsWith("Assets/Werewolf/"))
                {report.pendingClosedEditor.Add(path);continue;}
                var consumers=AuthoredAssets().Where(p=>!retired.Contains(p)&&p!=path&&AssetDatabase.GetDependencies(p,false).Contains(path)).ToArray();
                if(consumers.Length>0){report.notes.Add("Retained dependency: "+path+" -> "+string.Join(",",consumers.Take(4)));continue;}
                if(!AssetDatabase.DeleteAsset(path))throw new InvalidOperationException("Cannot remove retired asset: "+path);
                report.deleted.Add(path);
            }
            // Execution-only helpers are retired after their external references have been rewritten.
            foreach(var path in AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith(ExecutionFolder+"/")&&!AssetDatabase.IsValidFolder(p)).ToArray())
            {
                Backup(path);
                var consumers=AuthoredAssets().Where(p=>!p.StartsWith(ExecutionFolder+"/")&&p!=path&&AssetDatabase.GetDependencies(p,false).Contains(path)).ToArray();
                if(consumers.Length>0){report.notes.Add("Execution helper still referenced: "+path);continue;}
                if(AssetDatabase.DeleteAsset(path))report.deleted.Add(path);
            }
            foreach(var path in new[]{"Assets/Characters/AnimationPresentation/FacingCalibration/Editor/ActorFacingCalibration.Execution.cs","Assets/Characters/AnimationPresentation/FacingCalibration/Editor/ActorFacingCalibration.ExecutedStoneGolem.cs"})
            {Backup(path);if(AssetDatabase.DeleteAsset(path))report.deleted.Add(path);}
            report.status="Applied; downloaded-pack removals require closed Unity";
        }
        finally {File.WriteAllText(Output+"/report.json",JsonUtility.ToJson(report,true));File.WriteAllText(Path.Combine(report.archive,"report.json"),JsonUtility.ToJson(report,true));}
    }
}
