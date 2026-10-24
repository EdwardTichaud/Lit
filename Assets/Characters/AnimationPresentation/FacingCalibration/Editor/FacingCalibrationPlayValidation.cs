using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>Batch-only lifecycle/pose tests of the actual comparison player. Never enters Play Mode in the user's Editor.</summary>
[InitializeOnLoad]
public static class FacingCalibrationPlayValidation
{
    const string Key="Lit.FacingCalibrationPlayValidation";
    [Serializable] public sealed class Report
    {
        public bool passed, domainReloadDisabled;
        public int assertions, comparedFrames;
        public float maximumFootDifference, maximumHipDifference;
        public string error;
        public string scope="Actual four rigs and preview player in Play Mode; not UCC/NavMesh gameplay acceptance";
    }
    static FacingCalibrationPlayValidation() {EditorApplication.update+=Tick;}
    public static void Run() => Start(false);
    public static void RunWithoutDomainReload() => Start(true);
    static void Start(bool noReload)
    {
        if(!Application.isBatchMode)throw new InvalidOperationException("Run this test in the isolated batch project, never in the live Editor.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),ActorFacingCalibration.Folder+"/PlayBootstrap.unity");
        ActorFacingCalibration.AuditTransitions();ActorFacingCalibration.RebuildScene();
        EditorSceneManager.OpenScene(ActorFacingCalibration.Folder+"/FacingCalibrationLab.unity");
        SessionState.SetBool(Key+".previousEnabled",EditorSettings.enterPlayModeOptionsEnabled);
        SessionState.SetInt(Key+".previousOptions",(int)EditorSettings.enterPlayModeOptions);
        SessionState.SetBool(Key+".noReload",noReload);SessionState.SetBool(Key+".done",false);SessionState.SetBool(Key+".active",true);
        SessionState.SetFloat(Key+".started",(float)EditorApplication.timeSinceStartup);
        EditorSettings.enterPlayModeOptionsEnabled=true;
        EditorSettings.enterPlayModeOptions=noReload?EnterPlayModeOptions.DisableDomainReload:EnterPlayModeOptions.None;
        EditorApplication.isPlaying=true;
    }
    static void Check(Report report,bool condition,string message)
    {
        report.assertions++;if(!condition)throw new InvalidOperationException(message);
    }
    static Transform Bone(Animator animator,HumanBodyBones human,string generic)
        =>animator.isHuman?animator.GetBoneTransform(human):animator.GetComponentsInChildren<Transform>(true).Single(t=>t.name.Equals(generic,StringComparison.OrdinalIgnoreCase));
    static void Compare(FacingCalibrationLab lab,Report report,Vector3[] origins,Quaternion[] rotations)
    {
        for(int i=0;i<lab.actors.Length;i++)
        {
            var actor=lab.actors[i];
            Check(report,!actor.animator.applyRootMotion && !actor.animator.fireEvents && actor.animator.speed==1,"Physical/event/global cadence policy changed: "+actor.label);
            Check(report,Vector3.Distance(actor.animator.transform.position,origins[i])<1e-5f && Quaternion.Angle(actor.animator.transform.rotation,rotations[i])<.01f,"Preview root moved: "+actor.label);
            Check(report,Mathf.Abs(Enumerable.Range(0,3).Sum(n=>actor.mixer.GetInputWeight(n))-1)<.001f,"Invalid blend weights");
        }
        for(int i=0;i<lab.actors.Length;i+=2)
        {
            var source=lab.actors[i].animator;var target=lab.actors[i+1].animator;
            foreach(var bone in new[]{HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot,HumanBodyBones.Hips})
            {
                string name=bone==HumanBodyBones.Hips?"pelvis":bone==HumanBodyBones.LeftFoot?"foot_l":"foot_r";
                float delta=Vector3.Distance(source.transform.InverseTransformPoint(Bone(source,bone,name).position),target.transform.InverseTransformPoint(Bone(target,bone,name).position));
                if(bone==HumanBodyBones.Hips)report.maximumHipDifference=Mathf.Max(report.maximumHipDifference,delta);
                else report.maximumFootDifference=Mathf.Max(report.maximumFootDifference,delta);
                Check(report,delta<.001f,"Source/candidate protected pose differs during transition: "+lab.actors[i].label+" "+bone+" "+delta);
            }
        }
        report.comparedFrames++;
    }
    static void Exercise(Report report)
    {
        var lab=UnityEngine.Object.FindAnyObjectByType<FacingCalibrationLab>();Check(report,lab!=null && lab.actors.Length==8,"Expected source/candidate pairs for four actors");
        lab.enabled=false;lab.enabled=true;lab.cycle=0;lab.paused=false;
        var origins=lab.actors.Select(a=>a.animator.transform.position).ToArray();var rotations=lab.actors.Select(a=>a.animator.transform.rotation).ToArray();
        lab.AdvancePreview(.37f);
        var outgoing=lab.actors.Select(a=>a.mixer.GetInput(0).GetTime()).ToArray();lab.cycle=1;lab.AdvancePreview(0);
        for(int i=0;i<lab.actors.Length;i++)Check(report,Math.Abs(lab.actors[i].mixer.GetInput(0).GetTime()-outgoing[i])<1e-6,"Outgoing phase reset on transition");
        foreach(int cycle in new[]{0,1,2,1,0,2,0,1,0})
        {
            lab.cycle=cycle;
            for(int frame=0;frame<72;frame++){lab.AdvancePreview(1f/60);Compare(lab,report,origins,rotations);}
        }
        var phase=lab.actors.Select(a=>a.mixer.GetInput(0).GetTime()).ToArray();lab.paused=true;lab.AdvancePreview(1);
        for(int i=0;i<lab.actors.Length;i++)Check(report,Math.Abs(phase[i]-lab.actors[i].mixer.GetInput(0).GetTime())<1e-6,"Pause advanced animation");
        lab.paused=false;
        foreach(int cycle in new[]{1,2,0,2,1,0}){lab.cycle=cycle;lab.AdvancePreview(1f/60);Compare(lab,report,origins,rotations);}
        for(int repeat=0;repeat<3;repeat++)
        {
            lab.enabled=false;foreach(var actor in lab.actors)Check(report,!actor.graph.IsValid(),"Graph survived disable");
            lab.enabled=true;foreach(var actor in lab.actors)Check(report,actor.graph.IsValid(),"Graph missing after re-enable");
            lab.cycle=1;lab.AdvancePreview(1f/60);Compare(lab,report,origins,rotations);
        }
        lab.enabled=false;
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key+".active",false))return;
        if(!SessionState.GetBool(Key+".done",false) && EditorApplication.isPlaying)
        {
            var report=new Report {domainReloadDisabled=SessionState.GetBool(Key+".noReload",false)};
            try {Exercise(report);report.passed=true;}catch(Exception e){report.error=e.ToString();Debug.LogException(e);}
            Directory.CreateDirectory(ActorFacingCalibration.ReportFolder);
            File.WriteAllText(ActorFacingCalibration.ReportFolder+(report.domainReloadDisabled?"/play-no-reload.json":"/play-reload.json"),JsonUtility.ToJson(report,true));
            SessionState.SetBool(Key+".passed",report.passed);SessionState.SetBool(Key+".done",true);EditorApplication.isPlaying=false;
        }
        if(SessionState.GetBool(Key+".done",false) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorSettings.enterPlayModeOptionsEnabled=SessionState.GetBool(Key+".previousEnabled",false);
            EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)SessionState.GetInt(Key+".previousOptions",0);
            SessionState.SetBool(Key+".active",false);EditorApplication.Exit(SessionState.GetBool(Key+".passed",false)?0:1);
        }
        else if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+".started",0)>90)
        {
            SessionState.SetBool(Key+".done",true);SessionState.SetBool(Key+".passed",false);EditorApplication.isPlaying=false;
        }
    }
}
