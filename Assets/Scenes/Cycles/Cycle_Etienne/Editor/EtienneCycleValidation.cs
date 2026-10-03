#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Lit.Timeline;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Read-only authoring audit. Does not rebuild scenes, actors or quest progress.</summary>
public static class EtienneCycleValidation
{
    public const string Root = "Assets/Scenes/Cycles/Cycle_Etienne";
    public const string EnvironmentPath = "Assets/Scenes/District_1/District_1_PuitsDeLaReleve_Environment.unity";
    public const string CyclePath = Root + "/District_1_Cycle_Etienne.unity";
    public const string DefinitionPath = "Assets/Resources/Narrative/EtienneCycle.asset";

    [MenuItem("Lit/Validation/Valider le cycle Étienne")]
    public static void ValidateMenu()
    {
        var issues = Audit();
        foreach (string issue in issues) Debug.LogError("[Étienne] " + issue);
        if (issues.Count == 0) Debug.Log("[Étienne] Définition, scènes, preuves, boss, sortie et liaisons Timeline valides.");
    }

    public static List<string> Audit()
    {
        var issues = new List<string>();
        var definition = AssetDatabase.LoadAssetAtPath<CycleDefinition>(DefinitionPath);
        if (definition == null) { issues.Add("Définition absente."); return issues; }
        issues.AddRange(definition.ValidateConfiguration());
        if (definition.steps.Count(s => s.terminal) != 1 || !definition.FindStep("lucian_after_etienne").terminal)
            issues.Add("La réplique finale doit être l'unique étape terminale.");
        if (definition.prerequisites.conditions.Length != 0)
            issues.Add("Étienne doit rester indépendant : l'entrée est pilotée par le trigger du Puits.");
        foreach (string id in new[] {"relief_register_read", "conduit_route_read", "emergency_brake_read"})
        {
            var step = definition.FindStep(id);
            if (step == null || step.knowledge == null || step.prerequisites.conditions.Length != 1 || step.prerequisites.conditions[0].stepId != "puits_entered")
                issues.Add("Preuve absente ou non parallèle : " + id);
        }

        Scene previous = SceneManager.GetActiveScene();
        var opened = new List<Scene>();
        try
        {
            Scene Load(string path)
            {
                var scene = SceneManager.GetSceneByPath(path);
                if (scene.IsValid() && scene.isLoaded) return scene;
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); opened.Add(scene); return scene;
            }
            var quest = Load(CyclePath); var env = Load(EnvironmentPath);
            var objects = quest.GetRootGameObjects().Concat(env.GetRootGameObjects()).SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToArray();
            foreach (GameObject go in objects)
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) > 0) issues.Add("Script manquant : " + go.name);
            var hashes = new HashSet<long>();
            foreach (NetworkObject network in objects.Select(g => g.GetComponent<NetworkObject>()).Where(n => n != null))
            {
                long hash = new SerializedObject(network).FindProperty("GlobalObjectIdHash").longValue;
                if (hash == 0 || !hashes.Add(hash)) issues.Add("ID réseau nul ou dupliqué : " + network.name);
            }
            var controller = quest.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CycleController>(true)).SingleOrDefault();
            if (controller == null || controller.definition != definition) { issues.Add("CycleController absent ou mal relié."); return issues; }
            var boss = controller.encounters.FirstOrDefault(e => e.id == "dead_weight")?.enemy;
            if (boss == null || !EnemyController.HasRequiredComponents(boss.gameObject)) issues.Add("Socle du Poids mort invalide.");
            else
            {
                var behaviour = boss.GetComponent<DeadWeightBoss>(); var data = boss.GetComponent<CharacterInfo>().SourceData;
                if (behaviour == null || behaviour.Definition == null || behaviour.SuppressDefaultEnemyBrain || behaviour.Definition.ShowBossBar || data == null || data.hp != 300 || data.worldPrefab == null || data.worldPrefab.GetComponent<DeadWeightBoss>() == null)
                    issues.Add("Le Poids mort doit utiliser 300 PV, son propre prefab et l'IA ordinaire sans seconde barre.");
                if (boss.GetComponent<NavMeshAgent>().enabled) issues.Add("L'agent doit attendre le NavMesh.");
            }
            var documents = quest.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<InteractableItem>(true)).ToArray();
            if (documents.Length != 3 || documents.Any(i => i.representedItem == null || !i.representedItem.IsReadable() || !i.allowTake || i.interactableCategory != InteractableItem.InteractableCategory.RecoverableItem || i.interactionMaxDistance != 2))
                issues.Add("Les trois preuves doivent être récupérables et lisibles à 2 m.");
            bool HasVisibleOutline(GameObject go) => go.GetComponentsInChildren<Renderer>(true).Any(r =>
                (r is MeshRenderer || r is SkinnedMeshRenderer) && r.GetComponent<RuntimeOutlineTarget>() != null);
            foreach (var document in documents)
                if (!HasVisibleOutline(document.gameObject)) issues.Add("Outline sans renderer pour la preuve : " + document.name);
            var ghost = quest.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GhostController>(true)).SingleOrDefault();
            if (ghost == null || !HasVisibleOutline(ghost.gameObject)) issues.Add("Étienne doit posséder un outline sur son modèle visible.");
            var brake = controller.GetComponentInChildren<EtienneEmergencyBrake>(true);
            if (brake == null || brake.cycle != controller || brake.boss == null || brake.boss.Enemy != boss || !HasVisibleOutline(brake.gameObject))
                issues.Add("Frein absent, mal relié ou sans outline visible.");
            var flames = env.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Flame>(true)).ToArray();
            if (flames.Length != 3 || flames.Any(f => !new SerializedObject(f).FindProperty("isLit").boolValue || new SerializedObject(f).FindProperty("ancientFlame").boolValue || f.flameLight == null))
                issues.Add("Trois Flames communes allumées avec lumière sont requises.");
            foreach (Transform target in documents.Select(d => d.transform).Concat(new[]{ghost != null ? ghost.transform : null, brake != null ? brake.transform : null}).Where(t => t != null))
                if (!flames.Any(f => {
                    var state = new SerializedObject(f);
                    var influence = state.FindProperty("litInfluence");
                    Vector3 center = f.transform.TransformPoint(influence.FindPropertyRelative("center").vector3Value);
                    return state.FindProperty("isLit").boolValue && influence.FindPropertyRelative("enabled").boolValue &&
                        Vector3.Distance(target.position, center) <= influence.FindPropertyRelative("radius").floatValue;
                })) issues.Add("Interaction hors de l'influence d'une Flame allumée : " + target.name);
            var gate = env.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CycleActivationId>(true)).SingleOrDefault(a => a.activationId == "district1.etienne.flooded_conduits_access");
            if (gate == null || gate.target == null || gate.activeWhenSet) issues.Add("La sortie persistante doit s'effacer après la récompense.");
            var memory = controller.sequences.FirstOrDefault(s => s.id == "last_relief");
            if (memory == null || memory.director == null || memory.profile == null || !memory.profile.Matches(memory.director.playableAsset) || Math.Abs(memory.director.playableAsset.duration - 30) > .1)
                issues.Add("Souvenir de 30 secondes ou profil absent.");
            else
            {
                var targets = objects.SelectMany(g => g.GetComponents<TimelineBindingTarget>()).SelectMany(t => t.Targets).ToArray();
                foreach (var binding in memory.profile.Bindings)
                    if (binding.required && binding.bindingId != "camera.cinemachine_brain" && !targets.Any(t => t.bindingId == binding.bindingId && t.target != null)) issues.Add("Cible Timeline absente : " + binding.bindingId);
                var timeline = memory.director.playableAsset as UnityEngine.Timeline.TimelineAsset;
                var cameraTrack = timeline?.GetOutputTracks().OfType<Unity.Cinemachine.CinemachineTrack>().SingleOrDefault();
                if (memory.director.GetComponent<LitTimelineCinemachineBridge>() == null || cameraTrack == null || cameraTrack.GetClips().Count() != 4)
                    issues.Add("Le souvenir doit utiliser quatre plans Cinemachine et la passerelle de caméra.");
                else foreach (var clip in cameraTrack.GetClips())
                    if (!(clip.asset is Unity.Cinemachine.CinemachineShot shot) || shot.VirtualCamera.Resolve(memory.director) == null)
                        issues.Add("Caméra de plan Timeline absente : " + clip.displayName);
            }
            var manifest = AssetDatabase.LoadAssetAtPath<ZoneManifest>("Assets/Scenes/Maison/ZoneManifest_District_1.asset");
            foreach (string path in new[] {CyclePath, EnvironmentPath})
                if (manifest == null || !manifest.LoadingSceneNames.Contains(System.IO.Path.GetFileNameWithoutExtension(path)) || !EditorBuildSettings.scenes.Any(s => s.enabled && s.path == path))
                    issues.Add("Scène non déclarée : " + path);
        }
        finally
        {
            foreach (var scene in opened.AsEnumerable().Reverse()) if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
        return issues;
    }
}
#endif
