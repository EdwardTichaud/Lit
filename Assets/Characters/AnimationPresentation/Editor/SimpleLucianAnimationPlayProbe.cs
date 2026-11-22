using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

/// <summary>Opt-in bounded Play Mode smoke test. Uses a temporary play-start scene and restores the user's Editor setup.</summary>
[InitializeOnLoad]
public static partial class SimpleLucianAnimationPlayProbe
{
    private const string Key="Lit.SimpleLucian.PlayProbe";
    private const string Folder=SimpleLucianAnimationSetup.Report;
    private static LucianBrainsCombatArena arena;
    private static SimpleLucianProbeInput input;
    private static GameObject owner;
    private static int stage, token, externalToken;
    private static double began, since;
    private static readonly List<string> records=new();
    static SimpleLucianAnimationPlayProbe()
    {
        EditorApplication.update+=Tick;
        EditorApplication.playModeStateChanged+=OnMode;
    }
    private static void OnMode(PlayModeStateChange mode)
    {
        if(mode==PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key,false))
        {
            var previous=SessionState.GetString(Key+".scene","");
            EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previous)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            SessionState.SetBool(Key,false);
            SessionState.SetBool(Key+".narrative",false);
            if(SessionState.GetBool(Key+".batchNarrative",false))
            {
                SessionState.SetBool(Key+".batchNarrative",false);
                EditorApplication.Exit(File.Exists("Library/DialoguePlacement/Play.txt")&&File.ReadAllText("Library/DialoguePlacement/Play.txt").StartsWith("PASS")?0:3);
            }
        }
    }
    [MenuItem("Lit/Animation/Simple Lucian/4 Play Mode Smoke Test")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before starting the probe.");
        SessionState.SetString(Key+".scene",EditorSceneManager.playModeStartScene!=null?AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene):"");
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Characters/3_Enemy/Iluvilirae/Iluvilirae_Test.unity");
        SessionState.SetBool(Key,true);arena=null;input=null;owner=null;stage=0;records.Clear();began=since=EditorApplication.timeSinceStartup;
        EditorApplication.isPlaying=true;
    }
    private static void Tick()
    {
        string request=Folder+"/play.request";
        if(File.Exists(request) && !EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode)
        {File.Delete(request);Run();return;}
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try
        {
            double now=EditorApplication.timeSinceStartup;
            if(began==0)began=since=now;
            if(now-began>(SessionState.GetBool(Key+".narrative",false)?120:45))throw new InvalidOperationException("Play probe timed out at stage "+stage);
            if(arena==null)arena=UnityEngine.Object.FindAnyObjectByType<LucianBrainsCombatArena>();
            if(arena==null||arena.Player==null)return;
            var player=arena.Player;
            var bridge=player.GetComponent<LitOpsiveLocomotionBridge>();
            var animator=player.GetComponent<CharacterAnimationController>().Animator;
            var driver=player.GetComponent<CharacterDialoguePresentationController>();
            var action=player.GetComponent<PlayerActionPresentationController>();
            if(arena.Enemy!=null)
            {
                arena.Enemy.canAttack=false;arena.Enemy.enabled=false;
                var nav=arena.Enemy.GetComponent<NavMeshAgent>();if(nav!=null&&nav.isOnNavMesh)nav.isStopped=true;
            }
            if(SessionState.GetBool(Key+".narrative",false))
            {if(stage==0&&!bridge.Grounded)return;TickNarrative(player,bridge,driver,now);return;}
            if(!bridge.Grounded)return;
            if(stage==0)
            {
                Require(bridge.UsesSimpleWholeBodyPresentation,"Lucian lab must use the new profile");
                Require(animator.layerCount==2 && !animator.applyRootMotion,"Two layers and no automatic Root");
                owner=new GameObject("Dialogue validation owner");input=owner.AddComponent<SimpleLucianProbeInput>();input.bridge=bridge;
                Next(1);return;
            }
            double elapsed=now-since;
            if(stage==1)
            {input.direction=Vector2.up;if(elapsed<1)return;Record("walk",bridge,animator);Require(bridge.PlanarVelocity.magnitude>.2f,"Scripted input must move through the public UCC bridge");Next(2);return;}
            if(stage==2)
            {input.direction=Vector2.zero;if(elapsed<1)return;token=driver.Begin(owner,arena.Enemy!=null?arena.Enemy.transform:null,true,null,DialogueRole.Speaking);Require(token>0,"Stationary dialogue acquired");Next(3);return;}
            if(stage==3)
            {if(elapsed<.6)return;Require(driver.OwnsFullBody && action.HasDialoguePresentation,"Dialogue owns complete pose");Record("speaking",bridge,animator);driver.FinishSpeaking(token);Next(4);return;}
            if(stage==4)
            {if(elapsed<.4)return;Require(driver.CurrentGesture>=0 && driver.Profile.gestures[driver.CurrentGesture].role==DialogueRole.Waiting,"Voice end returns to attentive waiting");driver.End(token);Next(5);return;}
            if(stage==5)
            {if(elapsed<.5)return;Require(!driver.IsEngaged && !action.HasDialoguePresentation,"Dialogue releases presentation");input.direction=Vector2.up;Next(6);return;}
            if(stage==6)
            {input.direction=Vector2.up;if(elapsed<.8)return;token=driver.Begin(owner,null,false,null,DialogueRole.Speaking);Next(7);return;}
            if(stage==7)
            {input.direction=Vector2.up;if(elapsed<.4)return;Require(!driver.OwnsFullBody && bridge.PlanarVelocity.magnitude>.2f,"Mobile bark keeps physical locomotion");Record("mobile-bark",bridge,animator);driver.End(token);input.direction=Vector2.zero;Next(8);return;}
            if(stage==8)
            {if(elapsed<.8)return;token=driver.Begin(owner,null,true,null,DialogueRole.Speaking);Next(9);return;}
            if(stage==9)
            {if(elapsed<.5)return;externalToken=action.BeginExternalPresentation(owner);Require(externalToken>0 && !driver.OwnsFullBody,"Action immediately preempts dialogue");action.EndExternalPresentation(externalToken);driver.End(token);action.ResumeLocomotionFromCinematic(true,true,.18f);Next(10);return;}
            if(stage==10)
            {
                if(elapsed<.4)return;
                var state=animator.GetCurrentAnimatorStateInfo(0);
                Require(state.IsName("Base Layer.Locomotion") || state.IsName("Base Layer.CombatIdle") || state.IsName("Base Layer.CombatLocomotion"),"Cinematic return must use the complete ground cycles");
                Record("cinematic-resume",bridge,animator);Finish("PASS");
            }
        }
        catch(Exception e){Finish("FAIL "+e);}
    }
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static void Record(string label,LitOpsiveLocomotionBridge b,Animator a)
    {records.Add(label+" speed="+b.PlanarVelocity.magnitude.ToString("F3")+" LitSpeed="+a.GetFloat("LitSpeed").ToString("F3")+" state="+a.GetCurrentAnimatorStateInfo(0).fullPathHash+" "+b.DriveDiagnostic+" input="+b.CurrentWorldMoveInput);}
    private static void Next(int next){stage=next;since=EditorApplication.timeSinceStartup;}
    private static void Finish(string result)
    {
        if(SessionState.GetBool(Key+".narrative",false))File.WriteAllText("Library/DialoguePlacement/Play.txt",result+"\n"+string.Join("\n",records));
        Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/play-result.txt",result+"\n"+string.Join("\n",records));
        if(input!=null){input.direction=Vector2.zero;UnityEngine.Object.Destroy(input);input=null;}
        EditorApplication.isPlaying=false;
    }
}
