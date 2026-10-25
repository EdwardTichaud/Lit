using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static partial class BootstrapDistrictPlayProbe
{
    private static readonly string[] LimbesProofIds={"reve.limbes.seuil.fragment.2","reve.limbes.chambre.fragment.0","reve.limbes.seuil.fragment.0","reve.limbes.seuil.fragment.1"};
    private static void TickLimbesCycles()
    {
        if(!ControlReady(out var actor)) return;
        if(report.stage<=77)
        {
            int index=(report.stage-70)/2; string id=LimbesProofIds[index];
            var item=UnityEngine.Object.FindObjectsByType<InteractableItem>().Single(target=>target.representedItem!=null && target.representedItem.itemId==id);
            if(report.stage%2==0)
            {
                Check(actor.TrySetUccExternalPositionAndRotation(item.transform.position+Vector3.back,Quaternion.identity,true),"UCC fixture approached proof "+id); Advance(report.stage+1);return;
            }
            Check(item.CanBeDetectedBy(actor) && CharacterInteractionDetection.IsInActiveFlameInfluence(item) &&
                CharacterInteractionDetection.IsCharacterWithinRange(actor.transform,item.GetInteractionDetectionCollider(),item.GetInteractionAnchor(),item.GetInteractionMaxDistance(actor)),"Proof has valid light, range and interaction eligibility: "+id);
            item.SetDetectedCharacter(actor.gameObject);
            typeof(InteractableItem).GetMethod("TakeAllItems",Private).Invoke(item,null);
            Check(actor.Items.Any(held=>held!=null && held.itemId==id),"Real inventory contains recovered proof: "+id);
            Check(KnowledgeManager.Instance.HasKnowledgeId(id),"Actual pickup granted knowledge: "+id);
            Advance(report.stage+1);return;
        }
        if(report.stage==78 || report.stage==81)
        {
            string cycle=report.stage==78?"reve.limbes.seuil":"reve.limbes.chambre";
            var interaction=UnityEngine.Object.FindObjectsByType<CycleInteraction>().Single(target=>target.cycle!=null && target.cycle.definition.cycleId==cycle);
            Check(actor.TrySetUccExternalPositionAndRotation(interaction.transform.position+Vector3.back,Quaternion.identity,true),"UCC fixture approached voluntary resolution: "+cycle);
            Advance(report.stage+1);return;
        }
        if(report.stage==79 || report.stage==82)
        {
            string cycle=report.stage==79?"reve.limbes.seuil":"reve.limbes.chambre";
            var interaction=UnityEngine.Object.FindObjectsByType<CycleInteraction>().Single(target=>target.cycle!=null && target.cycle.definition.cycleId==cycle);
            Check(interaction.CanBeDetectedBy(actor) && interaction.TryInteract(),"Real cycle dialogue accepted after recovered knowledge: "+cycle);
            Advance(report.stage+1);return;
        }
        if(report.stage==80 || report.stage==83)
        {
            var definition=AssetDatabase.LoadAssetAtPath<CycleDefinition>("Assets/Resources/Narrative/Limbes"+(report.stage==80?"Seuil":"Chambre")+"Cycle.asset");
            if(!CycleProgressionService.Instance.IsCompleted(definition)) return;
            if(report.stage==80 && !BridgeVisible() && EditorApplication.timeSinceStartup-report.stageStarted<15) return;
            Check(true,"Actual cycle resolved: "+definition.cycleId);
            if(report.stage==80)
            {
                Check(BridgeVisible(),"Environment bridge activation survives terminal cycle progression."); Advance(81);return;
            }
            UsePortal(actor,AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/Maison/ZoneManifest_Maison.asset")); Advance(84);return;
        }
        if(report.stage==84)
        {
            if(!ZoneReady("Assets/Scenes/Maison/ZoneManifest_Maison.asset")) return;
            Check(true,"Resolved zone captured through production portal departure.");
            UsePortal(actor,AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/Reve/ZoneManifest_Reve_Limbes.asset")); Advance(85);return;
        }
        if(report.stage==85)
        {
            if(!ZoneReady("Assets/Scenes/Reve/ZoneManifest_Reve_Limbes.asset")) return;
            if(!BridgeVisible() && EditorApplication.timeSinceStartup-report.stageStarted<15) return;
            foreach(string name in new[]{"Seuil","Chambre"})
                Check(CycleProgressionService.Instance.IsCompleted(AssetDatabase.LoadAssetAtPath<CycleDefinition>("Assets/Resources/Narrative/Limbes"+name+"Cycle.asset")),"Completion retained after zone re-entry: "+name);
            Check(BridgeVisible(),"Bridge remains visible after zone unload/reload.");
            foreach(string id in LimbesProofIds) Check(actor.Items.Any(item=>item!=null && item.itemId==id) && KnowledgeManager.Instance.HasKnowledgeId(id),"Item and knowledge retained after reload: "+id);
            CaptureLiveLimbes();UsePortal(actor,AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/Maison/ZoneManifest_Maison.asset"));Advance(62);
        }
    }
    private static bool BridgeVisible() => UnityEngine.Object.FindObjectsByType<CycleActivationId>().Any(gate=>gate.activationId=="reve.limbes.seuil_recompose" && gate.target!=null && gate.target.activeInHierarchy);
    private static void CaptureLiveLimbes()
    {
        var camera=Camera.main ?? Camera.allCameras.FirstOrDefault(value=>value.isActiveAndEnabled && value.targetTexture==null);
        Check(camera!=null,"Live gameplay camera available in Limbes.");
        var render=new RenderTexture(1440,900,24,RenderTextureFormat.ARGB32); var pixels=new Texture2D(1440,900,TextureFormat.RGB24,false); var previous=RenderTexture.active;
        try
        {
            render.Create();var request=new RenderPipeline.StandardRequest{destination=render};
            Check(RenderPipeline.SupportsRenderRequest(camera,request),"Gameplay pipeline supports native rendering.");
            RenderPipeline.SubmitRenderRequest(camera,request);RenderTexture.active=render;
            pixels.ReadPixels(new Rect(0,0,1440,900),0,0);pixels.Apply();
            File.WriteAllBytes(Path.Combine(report.run,"Limbes_live_"+report.stage+".png"),pixels.EncodeToPNG());
        }
        finally {RenderTexture.active=previous;render.Release();UnityEngine.Object.Destroy(render);UnityEngine.Object.Destroy(pixels);}
    }
}
