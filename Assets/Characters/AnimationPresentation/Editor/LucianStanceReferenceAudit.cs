using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Read-only comparison of intended root-reference stance and retargeted Inplace feet.</summary>
public static class LucianStanceReferenceAudit
{
    const string Folder="Library/LucianCoherence/stance-reference";
    [Serializable] sealed class Measurements {public LucianCycleSupportAudit.Measurement[] cycles;}
    public static void Run()
    {
        Directory.CreateDirectory(Folder);
        var measurements=JsonUtility.FromJson<Measurements>(File.ReadAllText("Library/LucianCoherence/cycles/report.json"));
        using(var sampler=new ActorPoseSampler(ActorAnimationAudit.Actor(false)))
        foreach(var entry in measurements.cycles.Where(c=>!c.footIK&&c.clip.Contains("Common_")))
        foreach(string mode in new[]{"root-fk","root-ik","inplace-fk","inplace-ik","support-ik"})
        {
            var clip=mode.StartsWith("root")?ActorAnimationAudit.Source(entry.rootSource):ActorAnimationAudit.Source(entry.clip,entry.fileId);
            if(mode=="support-ik")
            {
                string name=entry.clip.Contains("_Run_")?"Lucian_Common_Run_SupportIK":"Lucian_Common_Walk_Loop_SupportIK";
                clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Characters/1_Squad/Lucian/Animation/CoherencePilot/"+name+".anim");
            }
            var frames=sampler.Sample(clip,rootMotion:mode.StartsWith("root"),sampleCount:240,measureSoles:true,applyFootIK:mode.EndsWith("ik"));
            var rows=frames.Select(f=>string.Join(",",new[]{f.time,f.root.x,f.root.y,f.root.z,f.body.y,f.leftFoot.x,f.leftFoot.y,f.leftFoot.z,f.rightFoot.x,f.rightFoot.y,f.rightFoot.z,f.leftToe.x,f.leftToe.y,f.leftToe.z,f.rightToe.x,f.rightToe.y,f.rightToe.z,f.leftSole.x,f.leftSole.y,f.leftSole.z,f.rightSole.x,f.rightSole.y,f.rightSole.z}.Select(v=>v.ToString("R",CultureInfo.InvariantCulture))));
            File.WriteAllLines(Folder+"/"+(entry.clip.Contains("_Run_")?"run":"walk")+"-"+mode+".csv",new[]{"time,root_x,root_y,root_z,body_y,left_x,left_y,left_z,right_x,right_y,right_z,lefttoe_x,lefttoe_y,lefttoe_z,righttoe_x,righttoe_y,righttoe_z,leftsole_x,leftsole_y,leftsole_z,rightsole_x,rightsole_y,rightsole_z"}.Concat(rows));
        }
    }
}
