using System;
using System.Reflection;
using Lit.Story;
using UnityEditor;
using UnityEngine;

/// <summary>Explicit, bounded actual-Lucian integration check; never saves scene changes.</summary>
public static partial class SimpleLucianAnimationPlayProbe
{
    private static StorySequenceRunner narrativeRunner;
    private static Transform narrativeMarker,narrativeSpeakerA;
    private static bool narrativeStarted,narrativeAborted;
    private static float narrativeYaw;
    private static int initialLookRevision;
    [MenuItem("Lit/Story/Validate Lucian Placement In Play Mode")]
    public static void RunNarrative()
    {if(System.IO.File.Exists("Library/DialoguePlacement/Play.txt"))System.IO.File.Delete("Library/DialoguePlacement/Play.txt");SessionState.SetBool(Key+".narrative",true);Run();}
    public static void RunNarrativeBatch()
    {SessionState.SetBool(Key+".batchNarrative",true);RunNarrative();}
    private static void TickNarrative(SquadCharacterController player,LitOpsiveLocomotionBridge bridge,CharacterDialoguePresentationController driver,double now)
    {
        DialogueSequenceLookState Look()=>(DialogueSequenceLookState)typeof(CharacterDialoguePresentationController).GetField("lookState",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(driver);
        if(stage==0)
        {
            if(Time.frameCount<15 || LocalPlayerContext.LocalCharacterRoot!=player.transform)return;
            began=now;
            owner=new GameObject("Narrative placement probe");owner.SetActive(false);
            owner.AddComponent<StorySequenceSceneBindings>();owner.AddComponent<StorySequenceFadeController>();
            owner.AddComponent<StorySequenceCameraDriver>();owner.AddComponent<StorySequenceDialoguePresenter>();
            narrativeRunner=owner.AddComponent<StorySequenceRunner>();
            narrativeMarker=new GameObject("playerTransform validation marker").transform;
            narrativeMarker.position=player.transform.position+Vector3.right*2;
            narrativeMarker.rotation=Quaternion.Euler(0,90,0);narrativeYaw=90;
            var settings=new SerializedObject(narrativeRunner);
            settings.FindProperty("playOnStart").boolValue=false;
            settings.FindProperty("playerTransform").objectReferenceValue=narrativeMarker;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var bindings=owner.GetComponent<StorySequenceSceneBindings>();
            var a=new GameObject("Dialogue speaker A").AddComponent<StorySequenceActor>();a.transform.position=narrativeMarker.position+Vector3.back*3;
            var b=new GameObject("Dialogue speaker B").AddComponent<StorySequenceActor>();b.transform.position=narrativeMarker.position+Vector3.forward*3;
            narrativeSpeakerA=a.transform;
            var local=player.GetComponent<StorySequenceActor>();if(local==null)local=player.gameObject.AddComponent<StorySequenceActor>();
            bindings.BindRuntimeActor("probeA",a);bindings.BindRuntimeActor("probeB",b);bindings.BindRuntimeActor("probePlayer",local);
            var sequence=ScriptableObject.CreateInstance<StorySequenceAsset>();sequence.name="Placement validation";
            sequence.startFromBlack=false;sequence.fadeToBlackBeforeGameplay=false;sequence.fadeFromBlackAfterGameplayRestore=false;
            foreach(string id in new[]{"probeA","probeA","probeB","probePlayer"})
            {
                var line=ScriptableObject.CreateInstance<VoiceLineData>();line.voiceLineText="Validation "+id;
                sequence.steps.Add(new StorySequenceStep {type=StorySequenceStepType.Dialogue,actorId=id,listenerId=id=="probePlayer"?"probeB":"probePlayer",voiceLine=line,dialogueMaxDisplayDuration=1,focusCameraOnSpeaker=false});
            }
            narrativeStarted=false;narrativeAborted=false;
            narrativeRunner.PlaybackStarted+=()=>{
                Require(Vector3.ProjectOnPlane(player.transform.position-narrativeMarker.position,Vector3.up).magnitude<=.16f,"Arrival radius before content");
                Require(Mathf.Abs(Mathf.DeltaAngle(player.transform.eulerAngles.y,narrativeYaw))<=2.1f,"Arrival heading before content");
                Require(bridge.PlanarVelocity.magnitude<=.06f,"Stopped before content");
                narrativeStarted=true;
            };
            narrativeRunner.PreparationFailed+=()=>narrativeAborted=true;
            owner.SetActive(true);Require(narrativeRunner.Play(sequence),"Sequence accepted");Next(1);return;
        }
        double elapsed=now-since;
        if(stage==1)
        {
            Require(!narrativeAborted,"Approach must succeed");
            if(!narrativeStarted)return;
            Record("arrival",bridge,player.GetComponent<CharacterAnimationController>().Animator);Next(2);return;
        }
        if(stage==2)
        {
            if(elapsed<.5)return;
            Require(Look().active && Mathf.Abs(Look().headYaw-45)<.1f,"Head target clamped towards speaker A");
            initialLookRevision=Look().revision;
            narrativeSpeakerA.position=narrativeMarker.position+Vector3.forward*3;
            Next(3);return;
        }
        if(stage==3)
        {
            if(elapsed<.9)return;
            Require(Look().revision==initialLookRevision,"Repeated speaker must retain its snapshot");
            Require(Mathf.Abs(Mathf.DeltaAngle(player.transform.eulerAngles.y,narrativeYaw))<=2.1f,"Body remains fixed");
            Next(4);return;
        }
        if(stage==4)
        {
            if(Look().revision==initialLookRevision)return;
            Require(Mathf.Abs(Look().headYaw+45)<.1f,"New speaker updates the clamped target");Next(5);return;
        }
        if(stage==5)
        {
            if(narrativeRunner.IsPlaying)return;
            if(elapsed<1)return;
            Require(Mathf.Abs(Mathf.DeltaAngle(player.transform.eulerAngles.y,narrativeYaw))<=2.1f,"Final body heading");
            Require(!bridge.IsExternalLockActive && !Look().active,"Sequence releases controls and gaze");
            narrativeMarker.position=player.transform.position+Vector3.forward*2;
            Require(narrativeRunner.Play(),"Sequence can restart");Next(6);return;
        }
        if(stage==6)
        {if(elapsed<.2)return;narrativeRunner.Abort();Next(7);return;}
        if(stage==7)
        {
            if(elapsed<.5)return;
            Require(!narrativeRunner.IsPlaying && !bridge.IsExternalLockActive && !Look().active,"Abort releases movement and gaze");
            Record("abort-released",bridge,player.GetComponent<CharacterAnimationController>().Animator);Finish("PASS narrative placement, stable speaker snapshots and cleanup");
        }
    }
}
