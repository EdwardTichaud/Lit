#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ArdentCycleValidation
{
    public const string Root="Assets/Scenes/Cycles/Cycle_Ardent";
    public const string EnvironmentPath="Assets/Scenes/District_1/District_1_ConduitsNoyés_Environement.unity";
    public const string CyclePath=Root+"/District_1_Cycle_Ardent.unity";
    public const string DefinitionPath="Assets/Resources/Narrative/ArdentCycle.asset";
    [MenuItem("Lit/Validation/Valider le cycle Ardent")]
    public static void ValidateMenu()
    {
        var issues=Audit();foreach(var issue in issues)Debug.LogError("[Ardent] "+issue);
        if(issues.Count==0)Debug.Log("[Ardent] Configuration des scènes, de la progression et de la Timeline valide.");
    }
    public static List<string> Audit()
    {
        var issues=new List<string>();var d=AssetDatabase.LoadAssetAtPath<CycleDefinition>(DefinitionPath);
        if(d==null){issues.Add("Définition absente");return issues;}
        issues.AddRange(d.ValidateConfiguration());
        if(d.cycleId!="district1.ardent"||d.prerequisites.conditions.Length!=0)issues.Add("Le cycle doit être indépendant.");
        if(d.steps.Count(s=>s.terminal)!=1||!d.FindStep("lucian_after_ardent").terminal)issues.Add("Étape terminale incorrecte.");
        foreach(var id in new[]{"transfer_register_read","conduit_instruction_read","iris_ribbon_found"})
            if(d.FindStep(id).knowledge==null||d.FindStep(id).prerequisites.conditions.Length!=1||d.FindStep(id).prerequisites.conditions[0].stepId!="conduits_entered")issues.Add("Preuve non parallèle: "+id);
        foreach(var id in new[]{"echo_relay_one_tuned","echo_relay_two_tuned","echo_relay_three_tuned"})
            if(d.FindStep(id).prerequisites.conditions.Length!=3)issues.Add("Relais sans les trois preuves: "+id);
        var loaded=new List<Scene>();var previous=SceneManager.GetActiveScene();
        try
        {
            foreach(var path in new[]{EnvironmentPath,CyclePath})
            {
                if(!EditorBuildSettings.scenes.Any(s=>s.enabled&&s.path==path))issues.Add("Build Settings: "+path);
                var scene=SceneManager.GetSceneByPath(path);if(!scene.IsValid()||!scene.isLoaded){scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);loaded.Add(scene);}
            }
            var quest=SceneManager.GetSceneByPath(CyclePath);var cycle=quest.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CycleController>(true)).Single();
            var boss=cycle.GetComponentInChildren<FalseChoirBoss>(true);
            if(boss==null||boss.Definition==null)issues.Add("Boss/définition absents.");
            else
            {
                if(boss.Definition.BossId!="district1.ardent.false_choir")issues.Add("Boss ID: "+boss.Definition.BossId);
                if(boss.Enemy.Health.SourceData.hp!=300)issues.Add("Boss HP: "+boss.Enemy.Health.SourceData.hp+" data="+AssetDatabase.GetAssetPath(boss.Enemy.Health.SourceData));
                if(boss.Definition.ShowBossBar||boss.SuppressDefaultEnemyBrain)issues.Add("HUD/IA du boss incorrects.");
                if(boss.Enemy.Health.SourceData.worldPrefab!=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/Prefab_FalseChoir.prefab"))issues.Add("Boss worldPrefab: "+AssetDatabase.GetAssetPath(boss.Enemy.Health.SourceData.worldPrefab));
                if(boss.visualDecoys==null||boss.visualDecoys.Length!=2||boss.visualDecoys.Any(g=>g==null||g.GetComponentsInChildren<Collider>(true).Length!=0||g.GetComponentsInChildren<EnemyController>(true).Length!=0||g.GetComponentsInChildren<Unity.Netcode.NetworkObject>(true).Length!=0))issues.Add("Les leurres doivent rester strictement visuels.");
            }
            var proofs=cycle.GetComponentsInChildren<InteractableItem>(true);if(proofs.Length!=3||proofs.Any(p=>p.representedItem==null||p.interactionMaxDistance!=2||p.GetComponentInChildren<Collider>(true)==null))issues.Add("Preuves mal configurées.");
            var relays=cycle.GetComponentsInChildren<ArdentEchoRelay>(true);if(relays.Length!=3||relays.Any(r=>r.boss!=boss||r.cycle!=cycle||r.pulseLight==null||r.interactionDistance!=2))issues.Add("Relais mal câblés.");
            var ghost=AssetDatabase.LoadAssetAtPath<GhostData>(Root+"/Data/GhostData_Nora_Ardent.asset");
            if(ghost==null||ghost.ghostId!="ghost_nora_ardent"||ghost.reactions.Count!=1||ghost.reactions[0].requirement.requiredKnowledge.Count!=4||ghost.evidence.Count!=3)issues.Add("Données de Nora incomplètes.");
            foreach(var id in new[]{"transfer_register_read","conduit_instruction_read","iris_ribbon_found","iris_departure_understood","nora_wait_released"}){var k=AssetDatabase.LoadAssetAtPath<KnowledgeSO>(Root+"/Data/Knowledge_Ardent_"+id+".asset");if(k==null||k.knowledgeId!="district1.ardent."+id||string.IsNullOrEmpty(k.title))issues.Add("Connaissance incomplète: "+id);}
            var sequence=cycle.sequences.Single();if(sequence.startGate==null||sequence.director==null||sequence.profile==null||sequence.director.playableAsset.duration!=24)issues.Add("Souvenir/gate mal configuré.");
            else
            {
                var targets=sequence.director.GetComponent<Lit.Timeline.TimelineBindingTarget>();
                if(!sequence.profile.Matches(sequence.director.playableAsset)||targets==null)issues.Add("Profil Timeline incompatible.");
                else foreach(var binding in sequence.profile.Bindings)
                    if(binding.required && binding.bindingId!="camera.cinemachine_brain" && !targets.Targets.Any(t=>t.bindingId==binding.bindingId&&t.target!=null))issues.Add("Liaison Timeline absente: "+binding.bindingId);
                var timeline=(UnityEngine.Timeline.TimelineAsset)sequence.director.playableAsset;
                foreach(var shot in timeline.GetOutputTracks().OfType<Unity.Cinemachine.CinemachineTrack>().SelectMany(t=>t.GetClips()).Select(c=>(Unity.Cinemachine.CinemachineShot)c.asset))
                    if(sequence.director.GetReferenceValue(shot.VirtualCamera.exposedName,out var valid)==null||!valid)issues.Add("Caméra de plan absente.");
            }
            var env=SceneManager.GetSceneByPath(EnvironmentPath);var activation=env.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CycleActivationId>(true)).Single();
            if(activation.activationId!="district1.ardent.lower_procession_access"||activation.target==null||activation.activeWhenSet)issues.Add("Sortie persistante incorrecte.");
            var flames=env.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Flame>(true)).ToArray();if(flames.Length<7||flames.Any(f=>!f.IsLit||f.IsAncientFlame)||flames.Select(f=>f.FlameId).Distinct().Count()!=flames.Length)issues.Add("Couverture Flame/identifiants incorrects.");
            var interactions=proofs.Select(p=>p.transform).Concat(relays.Select(r=>r.transform)).Concat(cycle.GetComponentsInChildren<GhostController>(true).Select(g=>g.transform)).Concat(new[]{sequence.startGate.transform});
            foreach(var target in interactions)
                if(!flames.Any(f=>{var p=new SerializedObject(f).FindProperty("litInfluence");return f.IsLit && p.FindPropertyRelative("enabled").boolValue && Vector3.Distance(target.position,f.transform.TransformPoint(p.FindPropertyRelative("center").vector3Value))<=p.FindPropertyRelative("radius").floatValue;}))issues.Add("Interaction hors de la couverture Flame: "+target.name);
        }
        catch(Exception e){issues.Add(e.Message);}
        finally{foreach(var scene in loaded.AsEnumerable().Reverse())EditorSceneManager.CloseScene(scene,true);if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);}
        return issues;
    }
}
#endif
