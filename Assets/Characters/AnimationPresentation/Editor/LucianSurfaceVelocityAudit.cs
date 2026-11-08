using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Read-only contact-surface velocity on the actual avatar, including source/copy comparison.</summary>
public static class LucianSurfaceVelocityAudit
{
    [Serializable] sealed class Entry {public string source,candidate;public long fileId;}
    [Serializable] sealed class Manifest {public Entry[] entries;}
    [Serializable] sealed class Row
    {
        public string clip,kind;
        public Vector2 requestedDirection,measuredVelocity;
        public float nativeSpeed,measuredSpeed,measuredAngle,angleDifference;
        public int leftPairs,rightPairs,missingPatchPairs;
    }
    [Serializable] sealed class Report
    {
        public string scope="Read-only native IK on Lucian's actual avatar. Source stance masks, identical surface vertices within 2 mm of both adjacent sole minima. No physics, blend, runtime or visual acceptance.";
        public string status="AUDITED",measurementInputs;
        public List<Row> rows=new List<Row>();
    }
    public static void Run()
    {
        var profile=AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>(LucianCadencePreview.Folder+"/LocomotionMeasurements.asset");
        var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText("Library/LucianCoherence/stride-cycles/manifest.json"));
        var report=new Report {measurementInputs=MeasurementInputs(profile)};
        using(var sampler=new ActorPoseSampler(ActorAnimationAudit.Actor(false)))
        foreach(var cycle in profile.run.Where(c=>c.clip.name.Contains("Run_FL_")||c.clip.name.Contains("Run_FR_")))
        {
            var entry=manifest.entries.Single(e=>e.candidate==AssetDatabase.GetAssetPath(cycle.clip));
            var mask=File.ReadAllLines("Library/LucianCoherence/stride-cycles/"+cycle.clip.name+"/contact-phases.csv").Skip(1).Select(l=>l.Split(',')).ToArray();
            foreach(var clip in new[]{ActorAnimationAudit.Source(entry.source,entry.fileId),cycle.clip})
            {
                var positions=new List<Vector3[]>[2] {new List<Vector3[]>(),new List<Vector3[]>()};
                var points=new ActorSoleSampler.ContactPoint[2][];
                sampler.Sample(clip,sampleCount:240,measureSoles:true,applyFootIK:true,geometrySample:(a,soles,index)=>
                {
                    for(int side=0;side<2;side++)
                    {
                        if(points[side]==null)points[side]=soles.FootPoints(side==0).ToArray();
                        var frame=new Vector3[points[side].Length];
                        for(int j=0;j<frame.Length;j++)if(!soles.TryPosition(points[side][j],out frame[j]))throw new InvalidOperationException("Foot surface unavailable");
                        positions[side].Add(frame);
                    }
                });
                var row=new Row {clip=AssetDatabase.GetAssetPath(clip),kind=clip==cycle.clip?"candidate":"provider",nativeSpeed=cycle.nativeSpeed,requestedDirection=cycle.direction.normalized};
                var velocities=new List<Vector2>();
                for(int side=0;side<2;side++)for(int i=1;i<=240;i++)
                {
                    int aMask=Mathf.RoundToInt((i-1)/240f*(mask.Length-1)),bMask=Mathf.RoundToInt(i/240f*(mask.Length-1));
                    if(mask[aMask][side+1]!="1"||mask[bMask][side+1]!="1")continue;
                    var a=positions[side][i-1];var b=positions[side][i];float ay=a.Min(p=>p.y),by=b.Min(p=>p.y);
                    var patch=Enumerable.Range(0,a.Length).Where(j=>a[j].y<=ay+.002f&&b[j].y<=by+.002f).ToArray();
                    if(patch.Length==0){row.missingPatchPairs++;continue;}
                    float dt=clip.length/240;
                    velocities.Add(new Vector2(Median(patch.Select(j=>(a[j].x-b[j].x)/dt)),Median(patch.Select(j=>(a[j].z-b[j].z)/dt))));
                    if(side==0)row.leftPairs++;else row.rightPairs++;
                }
                if(row.leftPairs<5||row.rightPairs<5)throw new InvalidOperationException("Both supports must be exercised");
                row.measuredVelocity=new Vector2(Median(velocities.Select(v=>v.x)),Median(velocities.Select(v=>v.y)));
                row.measuredSpeed=row.measuredVelocity.magnitude;
                row.measuredAngle=Mathf.Atan2(row.measuredVelocity.x,row.measuredVelocity.y)*Mathf.Rad2Deg;
                row.angleDifference=Vector2.SignedAngle(row.requestedDirection,row.measuredVelocity);
                report.rows.Add(row);
            }
        }
        Directory.CreateDirectory("Library/LucianCoherence");
        File.WriteAllText("Library/LucianCoherence/surface-velocity-source-copy.json",JsonUtility.ToJson(report,true));
    }
    public static void ApplyMeasuredDirections()
    {
        var profile=AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>(NativeFootSupportPreparation.ProfilePath);
        var data=profile.footSupports;
        NativeFootSupportPreparation.Validate(profile,data,true);
        if(profile.supportValidated)throw new InvalidOperationException("Released profiles require a separate migration.");
        const string application="Library/LucianCoherence/surface-direction-application.json";
        var approved=File.Exists(application)?JsonUtility.FromJson<Report>(File.ReadAllText(application)):null;
        bool reuse=approved!=null&&approved.status=="APPLIED_NUMERIC_GATE_PASSED";
        if(reuse&&approved.measurementInputs!=MeasurementInputs(profile))throw new InvalidOperationException("Measured source/mask contract changed; prepare a new calibration before replacing authoring.");
        if(!reuse)Run();
        var report=reuse?approved:JsonUtility.FromJson<Report>(File.ReadAllText("Library/LucianCoherence/surface-velocity-source-copy.json"));
        var replacements=report.rows.Where(r=>r.kind=="candidate").ToArray();
        if(replacements.Length!=2)throw new InvalidOperationException("Both forward diagonals required.");
        foreach(var row in replacements)
        {
            var original=report.rows.Single(r=>r.kind=="provider"&&Path.GetFileNameWithoutExtension(r.clip).Contains(row.clip.Contains("Run_FL_")?"Run_FL_":"Run_FR_"));
            if(Vector2.Distance(row.measuredVelocity,original.measuredVelocity)>.02f)throw new InvalidOperationException("Source/copy trajectory disagreement: "+row.clip);
            if(row.measuredSpeed<3||row.measuredSpeed>5||Mathf.Abs(row.measuredAngle)<20||Mathf.Abs(row.measuredAngle)>40)throw new InvalidOperationException("Unexpected source motion: "+row.clip);
        }
        var graph=(AnimatorController)data.baseController;
        var state=LocomotionPresentationCalibration.States(graph.layers[0].stateMachine).Single(x=>x.name=="CombatLocomotion");
        var root=(BlendTree)state.motion;
        var run=(BlendTree)root.children.Single(c=>Mathf.Abs(c.threshold-profile.blendThresholds.y)<.001f).motion;
        if(run.blendType!=BlendTreeType.SimpleDirectional2D||AssetDatabase.GetAssetPath(run)!=AssetDatabase.GetAssetPath(graph))throw new InvalidOperationException("Expected owned directional run tree.");
        var overrides=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(LucianCadencePreview.Folder+"/LucianCadence.overrideController");
        var before=run.children;var after=run.children;
        var cycles=replacements.Select(r=>profile.run.Single(c=>AssetDatabase.GetAssetPath(c.clip)==r.clip)).ToArray();
        var directions=cycles.Select(c=>c.direction).ToArray();var speeds=cycles.Select(c=>c.nativeSpeed).ToArray();
        bool changed=false;
        for(int j=0;j<cycles.Length;j++)
        {
            int i=Array.FindIndex(after,c=>c.motion is AnimationClip clip&&(overrides[clip]??clip)==cycles[j].clip);
            if(i<0)throw new InvalidOperationException("Animator consumer unresolved: "+cycles[j].clip.name);
            var direction=replacements[j].measuredVelocity.normalized;
            changed|=Vector2.Distance(after[i].position,direction)>.00001f||Vector2.Distance(cycles[j].direction,direction)>.00001f||Mathf.Abs(cycles[j].nativeSpeed-replacements[j].measuredSpeed)>.0001f;
            after[i].position=direction;
        }
        if(!changed){report.status="APPLIED_NUMERIC_GATE_PASSED";SaveReport();return;}
        if(reuse)throw new InvalidOperationException("Previously calibrated values were edited; author changes preserved.");
        string oldController=data.controllerSignature;
        var backup=UnityEngine.Object.Instantiate(data);
        report.status="APPLYING_UNQUALIFIED";SaveReport();
        try
        {
            run.children=after;EditorUtility.SetDirty(run);AssetDatabase.SaveAssetIfDirty(run);
            for(int i=0;i<cycles.Length;i++){cycles[i].direction=replacements[i].measuredVelocity.normalized;cycles[i].nativeSpeed=replacements[i].measuredSpeed;}
            EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);
            // Geometry and source goals are unchanged. Invalidate the verification
            // certificate until the complete new weight domain passes the 80-case gate.
            LucianContactBlendAudit.PrepareSupportForControllerChange(oldController);
            LucianContactBlendAudit.VerifyPreparedSupport();
            NativeFootSupportPreparation.Validate(profile,data,true);
            if(data.runtimeValidationSignature!=data.dataSignature)throw new InvalidOperationException("New blend domain has no verification certificate.");
            report.status="APPLIED_NUMERIC_GATE_PASSED";SaveReport();
        }
        catch
        {
            run.children=before;EditorUtility.SetDirty(run);AssetDatabase.SaveAssetIfDirty(run);
            for(int i=0;i<cycles.Length;i++){cycles[i].direction=directions[i];cycles[i].nativeSpeed=speeds[i];}
            EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);
            EditorUtility.CopySerialized(backup,data);
            EditorUtility.SetDirty(data);AssetDatabase.SaveAssetIfDirty(data);
            report.status="ROLLED_BACK_AFTER_FAILED_GATE";SaveReport();throw;
        }
        finally{UnityEngine.Object.DestroyImmediate(backup);}
        void SaveReport()=>File.WriteAllText(application,JsonUtility.ToJson(report,true));
    }
    public static void VerifyRepeatApplication()
    {
        var profile=AssetDatabase.LoadAssetAtPath<LocomotionPresentationProfile>(NativeFootSupportPreparation.ProfilePath);
        var paths=new[]{NativeFootSupportPreparation.ProfilePath,NativeFootSupportPreparation.DataPath,AssetDatabase.GetAssetPath(profile.footSupports.baseController)};
        var bytes=paths.Select(File.ReadAllBytes).ToArray();var timestamps=paths.Select(File.GetLastWriteTimeUtc).ToArray();
        ApplyMeasuredDirections();
        for(int i=0;i<paths.Length;i++)if(!bytes[i].SequenceEqual(File.ReadAllBytes(paths[i]))||timestamps[i]!=File.GetLastWriteTimeUtc(paths[i]))throw new InvalidOperationException("Repeated calibration wrote an asset: "+paths[i]);
        File.WriteAllText("Library/LucianCoherence/surface-calibration-idempotence.txt","PASS: repeated approved calibration changed no asset bytes or timestamps.\n"+string.Join("\n",paths));
    }
    static string MeasurementInputs(LocomotionPresentationProfile profile)
    {
        var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText("Library/LucianCoherence/stride-cycles/manifest.json"));
        var parts=new List<string> {Application.unityVersion,profile.footSupports.inputSignature,profile.footSupports.rigSignature};
        foreach(var cycle in profile.run.Where(c=>c.clip.name.Contains("Run_FL_")||c.clip.name.Contains("Run_FR_")))
        {
            var entry=manifest.entries.Single(e=>e.candidate==AssetDatabase.GetAssetPath(cycle.clip));
            parts.Add(entry.source);parts.Add(AssetDatabase.GetAssetDependencyHash(entry.source).ToString());
            parts.Add(entry.candidate);parts.Add(AssetDatabase.GetAssetDependencyHash(entry.candidate).ToString());
            parts.Add(Hash128.Compute(File.ReadAllText("Library/LucianCoherence/stride-cycles/"+cycle.clip.name+"/contact-phases.csv")).ToString());
        }
        return Hash128.Compute(string.Join("\n",parts)).ToString();
    }
    static float Median(IEnumerable<float> values)
    {
        var ordered=values.OrderBy(v=>v).ToArray();int n=ordered.Length;
        return n%2==1?ordered[n/2]:(ordered[n/2-1]+ordered[n/2])*.5f;
    }
}
