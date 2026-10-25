using System;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

public static partial class BootstrapDistrictPlayProbe
{
    private static void TickLimbesRoutes()
    {
        if(!ControlReady(out var actor)) return;
        var links=UnityEngine.Object.FindObjectsByType<NavMeshLink>()
            .Where(link=>link.gameObject.scene.name=="Reve_Limbes_Environment" && link.transform.parent!=null && link.transform.parent.name=="03_Liaisons_des_sauts")
            .OrderBy(link=>link.name,StringComparer.Ordinal).ToArray();
        if(links.Length!=19) throw new InvalidOperationException("Expected nineteen authored traversal links, found "+links.Length);
        if(report.limbesRouteIndex==0 && report.stage==90) Check(true,"Nineteen authored traversal links available.");
        if(report.limbesRouteIndex>=links.Length)
        {
            UsePortal(actor,AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/Maison/ZoneManifest_Maison.asset")); Advance(62);return;
        }
        var link=links[report.limbesRouteIndex];Vector3 a=link.transform.TransformPoint(link.startPoint),b=link.transform.TransformPoint(link.endPoint);
        Vector3 heading=b-a;heading.y=0;heading.Normalize();
        // Navigation endpoints sit close to the ledge. A player aims inside
        // the landing platform, rather than braking at its leading edge.
        b+=heading*.8f;
        if(report.stage==90)
        {
            if(EditorApplication.timeSinceStartup-report.stageStarted<.75) return;
            Check(actor.TrySetUccExternalPositionAndRotation(a+heading*.2f,Quaternion.LookRotation(heading),true),"Approached takeoff for "+link.name);
            Advance(91);return;
        }
        var driver=actor.GetComponent<LimbesJumpProbeDriver>();
        if(driver==null)
        {
            var floorHits=Physics.RaycastAll(b+Vector3.up,Vector3.down,2f,~0,QueryTriggerInteraction.Ignore)
                .Where(hit=>hit.collider.gameObject.scene.name=="Reve_Limbes_Environment").OrderBy(hit=>hit.distance).ToArray();
            if(floorHits.Length==0) throw new InvalidOperationException("Missing landing floor for "+link.name);
            driver=actor.gameObject.AddComponent<LimbesJumpProbeDriver>();driver.actor=actor;driver.useTarget=true;driver.target=b;driver.targetFloor=floorHits[0].collider;return;
        }
        if(!driver.finished) return;
        Check(driver.succeeded,"UCC traversal "+link.name+"; from="+a+"; target="+b+"; landed="+actor.transform.position+"; peak="+driver.peak+"; reason="+driver.reason+"; trace="+driver.trace);
        UnityEngine.Object.Destroy(driver);actor.Stop();report.limbesRouteIndex++;Advance(90);
    }
}

