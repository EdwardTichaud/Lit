using System;
using UnityEditor;
using UnityEngine;

public static partial class BootstrapDistrictPlayProbe
{
    private static void BeginLimbesChecks()
    {
        if(!ZoneReady("Assets/Scenes/Reve/ZoneManifest_Reve_Limbes.asset") || !ControlReady(out var actor)) return;
        Check(true,"Six authored Limbes scenes loaded through actual Maison portal; actor grounded, navigation and exploration restored.");
        if(report.limbesRoutes) {Advance(90);return;}
        if(report.limbesCycles) {CaptureLiveLimbes(); Advance(70); return;}
        Check(actor.TrySetUccExternalPositionAndRotation(new Vector3(600,120.05f,4.3f),Quaternion.identity,true),"UCC fixture positioned at first takeoff point.");
        Advance(60);
    }
    private static void TickLimbes()
    {
        if(EditorApplication.timeSinceStartup-report.stageStarted>45) throw new TimeoutException("Limbes gameplay probe timed out at stage "+report.stage);
        if(report.stage>=90) {TickLimbesRoutes();return;}
        if(report.stage>=70) {TickLimbesCycles();return;}
        if(report.stage==60)
        {
            if(!ControlReady(out var actor)) return;
            var driver=actor.GetComponent<LimbesJumpProbeDriver>();
            if(driver==null) {driver=actor.gameObject.AddComponent<LimbesJumpProbeDriver>();driver.actor=actor;return;}
            if(!driver.finished) return;
            Check(driver.succeeded,"Real UCC jump reached first raised stone; peak="+driver.peak+"; final="+actor.transform.position+"; reason="+driver.reason);
            UnityEngine.Object.Destroy(driver); actor.Stop();
            Check(actor.TrySetUccExternalPositionAndRotation(new Vector3(600,90,10),Quaternion.identity,true),"Owned fixture entered void after participating in zone.");
            Advance(61); return;
        }
        if(report.stage==61)
        {
            if(!ControlReady(out var actor)) return;
            if(actor.transform.position.y<119 || actor.transform.position.y>122 || Mathf.Abs(actor.transform.position.z)>4)
            {if(EditorApplication.timeSinceStartup-report.stageStarted>15) throw new TimeoutException("Real fall recovery did not return to entry landing.");return;}
            Check(true,"Actual LimbesFallRecovery returned UCC actor to the authored safe landing, grounded and with exploration input.");
            UsePortal(actor,AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/Maison/ZoneManifest_Maison.asset")); Advance(62); return;
        }
        if(report.stage==62)
        {
            if(!ZoneReady("Assets/Scenes/Maison/ZoneManifest_Maison.asset") || !ControlReady(out _)) return;
            Check(true,"Authored Limbes return portal restored Maison.");
            Check(GameFlowService.OpenMainMenu(),"Production return-to-menu accepted after Limbes visit.");Advance(5);
        }
    }
}

public sealed class LimbesJumpProbeDriver : MonoBehaviour
{
    public SquadCharacterController actor;
    public bool finished,succeeded;
    public float peak;
    public string reason;
    public bool useTarget;
    public Vector3 target;
    public Collider targetFloor;
    public string trace;
    private bool started,leftGround;
    private float began;
    private Vector3 origin;
    private Vector2 input;
    private float nextSample;
    private float runupStarted=-1;
    private void LateUpdate()
    {
        if(actor==null || finished) return;
        if(!started)
        {
            if(!actor.IsGrounded) return;
            var scriptedJump=actor.GetComponentInChildren<PlayerScriptedJumpController>(true);
            if(scriptedJump!=null && scriptedJump.IsActive) {reason="waiting for previous jump presentation";return;}
            origin=actor.transform.position;
            Vector3 delta=target-origin;input=useTarget?new Vector2(delta.x,delta.z).normalized:Vector2.up;
            var bridge=actor.GetComponent<LitOpsiveLocomotionBridge>();
            if(bridge==null || !bridge.IsDriving || !bridge.Grounded || bridge.IsInputSuppressedByUcc)
            {runupStarted=-1;reason="waiting for grounded movement eligibility";actor.Stop();return;}
            if(runupStarted<0) runupStarted=Time.unscaledTime;
            actor.MoveWorld(input);
            if(Time.unscaledTime-runupStarted<.2f) {reason="building normal run-up inertia";return;}
            if(bridge==null || !bridge.Jump(input,true))
            {runupStarted=-1;reason="waiting for jump eligibility; driving="+bridge?.IsDriving+"; grounded="+bridge?.Grounded+"; suppressed="+bridge?.IsInputSuppressedByUcc;actor.Stop();return;}
            reason="jump accepted";
            peak=actor.transform.position.y;began=Time.unscaledTime;started=true;actor.MoveWorld(input);
        }
        peak=Mathf.Max(peak,actor.transform.position.y);
        if(Time.unscaledTime>=nextSample && (trace==null || trace.Length<4000))
        {nextSample=Time.unscaledTime+.1f;trace+=(Time.unscaledTime-began).ToString("F2")+":"+actor.transform.position+";";}
        if(!actor.IsGrounded) leftGround=true;
        Vector3 remaining=target-actor.transform.position;
        bool move=useTarget?Vector2.Dot(new Vector2(remaining.x,remaining.z),input)>.15f:actor.transform.position.z<9.7f;
        actor.MoveWorld(move?input:Vector2.zero);
        if(leftGround && actor.IsGrounded && Time.unscaledTime-began>.4f)
        {
            bool onTargetFloor=targetFloor!=null && Array.Exists(Physics.RaycastAll(actor.transform.position+Vector3.up*.5f,Vector3.down,1f,~0,QueryTriggerInteraction.Ignore),hit=>hit.collider==targetFloor);
            finished=true;succeeded=useTarget?peak>origin.y+.2f && onTargetFloor && Mathf.Abs(remaining.y)<.6f:
                peak>120.8f && actor.transform.position.z>=8 && actor.transform.position.z<=12 && actor.transform.position.y>120.3f;
            reason=succeeded?"landed on raised stone":"landed outside target stone";actor.Stop();
        }
        else if(Time.unscaledTime-began>12) {finished=true;reason="jump timed out";actor.Stop();}
    }
    private void OnDisable() {if(actor!=null) actor.Stop();}
}
