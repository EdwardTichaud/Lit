using System;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public static class CombatAnimationLabSetup
{
    public const string ScenePath = "Assets/Scenes/Workshop/AnimationLab.unity";
    [MenuItem("Lit/Combat/Reconfigure AnimationLab (Editor Preview)")]
    public static void Reconfigure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || AnimationMode.InAnimationMode())
            throw new InvalidOperationException("Arrêter Play et les aperçus avant de reconfigurer AnimationLab.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var root = scene.GetRootGameObjects().Single(g => g.name == "AnimationLab");
            Transform Find(string name) => root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
            var playerAnchor = Find("Lucian_Anchor"); var enemyAnchor = Find("Enemy_Anchor");
            playerAnchor.localPosition = new Vector3(0, 0, -.85f); playerAnchor.localRotation = Quaternion.identity;
            enemyAnchor.localPosition = new Vector3(0, 0, .85f); enemyAnchor.localRotation = Quaternion.Euler(0, 180, 0);
            var player = ReplaceActor(Find("Lucian_Preview"), "Assets/Characters/1_Squad/Lucian/Player_Model_Lucian.prefab");
            var enemy = ReplaceActor(Find("Enemy_Preview"), JuggernautV2Setup.PrefabPath);
            // The old holder referenced deleted Skill/Timeline assets. Keep this scene
            // independent of runtime skill eligibility and combat/cinematic services.
            foreach (var old in root.GetComponents<CombatSkillTimelineAuthoringRig>()) UnityEngine.Object.DestroyImmediate(old);
            var lab = root.GetComponent<CombatAnimationLab>() ?? root.AddComponent<CombatAnimationLab>();
            lab.player = player; lab.enemy = enemy; lab.playerAnchor = playerAnchor; lab.enemyAnchor = enemyAnchor;
            lab.director = root.GetComponentInChildren<PlayableDirector>(true);
            EnsureActive(lab.director.transform);
            var directorData = new SerializedObject(lab.director);
            directorData.FindProperty("m_SceneBindings").arraySize = 0;
            directorData.ApplyModifiedPropertiesWithoutUndo();
            lab.director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            lab.director.extrapolationMode = DirectorWrapMode.Hold;
            lab.cameraBrain = root.GetComponentInChildren<CinemachineBrain>(true);
            EnsureActive(lab.cameraBrain.transform);
            var overview = root.transform.Find("AnimationLab_Overview");
            if (overview == null) { overview = new GameObject("AnimationLab_Overview").transform; overview.SetParent(root.transform, false); }
            lab.previewCamera = overview.GetComponent<CinemachineCamera>() ?? overview.gameObject.AddComponent<CinemachineCamera>();
            lab.previewCamera.Priority = 100;
            lab.previewCamera.transform.SetPositionAndRotation(root.transform.TransformPoint(new Vector3(3.5f, 2, -4.5f)), Quaternion.LookRotation(root.transform.TransformPoint(Vector3.up) - root.transform.TransformPoint(new Vector3(3.5f, 2, -4.5f))));
            var signals = root.transform.Find("PreviewSignals");
            if (signals == null) { signals = new GameObject("PreviewSignals").transform; signals.SetParent(root.transform, false); }
            (signals.GetComponent<AnimationEvents>() ?? signals.gameObject.AddComponent<AnimationEvents>()).PreviewOnly = true;
            lab.previewSignals = signals.GetComponent<SignalReceiver>() ?? signals.gameObject.AddComponent<SignalReceiver>();
            if (lab.timeline == null) lab.timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(IluviliraeSetup.Folder + "/CombatLab_CounterSkill.playable");
            if (lab.playerClip == null) lab.playerClip = ((AnimationPlayableAsset)lab.timeline.GetOutputTracks().OfType<AnimationTrack>().First(t => t.name == "Player.Animator").GetClips().First().asset).clip;
            if (lab.enemyClip == null) lab.enemyClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(JuggernautV2Setup.Folder + "/Animations/Juggernaut_v2_Hurt.anim");
            CombatAnimationLabEditor.BindTimeline(lab);
            Validate(lab);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[AnimationLab] Lucian/Juggernaut_v2 face à face, previews sans gameplay et bindings Timeline configurés.");
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    private static Animator ReplaceActor(Transform actor, string sourcePath)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        if (source == null) throw new InvalidOperationException("Prefab preview manquant : " + sourcePath);
        foreach (Transform child in actor.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
        StripBehaviours(actor.gameObject);
        (actor.GetComponent<AnimationEvents>() ?? actor.gameObject.AddComponent<AnimationEvents>()).PreviewOnly = true;
        foreach (var collider in actor.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
        foreach (var body in actor.GetComponents<Rigidbody>()) UnityEngine.Object.DestroyImmediate(body);
        foreach (var agent in actor.GetComponents<UnityEngine.AI.NavMeshAgent>()) UnityEngine.Object.DestroyImmediate(agent);
        var clone = UnityEngine.Object.Instantiate(source);
        try
        {
            var sourceAnimator = clone.GetComponent<Animator>();
            var animator = actor.GetComponent<Animator>() ?? actor.gameObject.AddComponent<Animator>();
            EditorUtility.CopySerialized(sourceAnimator, animator);
            animator.enabled = true;
            animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            StripBehaviours(clone);
            foreach (var receiverAnimator in clone.GetComponentsInChildren<Animator>(true))
                (receiverAnimator.GetComponent<AnimationEvents>() ?? receiverAnimator.gameObject.AddComponent<AnimationEvents>()).PreviewOnly = true;
            foreach (var collider in clone.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            foreach (var body in clone.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
            foreach (var agent in clone.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)) UnityEngine.Object.DestroyImmediate(agent);
            foreach (var extra in clone.GetComponentsInChildren<Animator>(true).Where(a => a != sourceAnimator)) UnityEngine.Object.DestroyImmediate(extra);
            foreach (Transform child in clone.transform.Cast<Transform>().ToArray()) child.SetParent(actor, false);
            actor.localPosition = Vector3.zero; actor.localRotation = Quaternion.identity; actor.localScale = source.transform.localScale;
            EnsureActive(actor);
            animator.Rebind(); animator.Update(0);
            return animator;
        }
        finally { UnityEngine.Object.DestroyImmediate(clone); }
    }

    private static void EnsureActive(Transform target)
    {
        for (var parent = target; parent != null; parent = parent.parent) parent.gameObject.SetActive(true);
    }

    private static void StripBehaviours(GameObject root)
    {
        var remaining = root.GetComponentsInChildren<MonoBehaviour>(true).Where(b => b != null).ToList();
        while (remaining.Count > 0)
        {
            var removable = remaining.FirstOrDefault(candidate => !remaining.Any(other => other != candidate && other.gameObject == candidate.gameObject &&
                other.GetType().GetCustomAttributes(typeof(RequireComponent), true).Cast<RequireComponent>().Any(r =>
                    new[] { r.m_Type0, r.m_Type1, r.m_Type2 }.Any(t => t != null && t.IsAssignableFrom(candidate.GetType())))));
            if (removable == null) throw new InvalidOperationException("Dépendances circulaires entre composants de preview.");
            remaining.Remove(removable);
            UnityEngine.Object.DestroyImmediate(removable);
        }
    }

    public static void Validate(CombatAnimationLab lab)
    {
        if (lab.player == null || lab.enemy == null || !lab.player.isHuman || !lab.enemy.isHuman || lab.director == null || lab.timeline == null)
            throw new InvalidOperationException("AnimationLab : acteurs humanoïdes et Timeline requis.");
        foreach (var actor in new[] { lab.player, lab.enemy })
        {
            if (actor.GetComponentsInChildren<MonoBehaviour>(true).Any(b => !(b is AnimationEvents)) || actor.GetComponentsInChildren<Renderer>(true).All(r => !r.enabled || !r.gameObject.activeInHierarchy))
                throw new InvalidOperationException("AnimationLab : preview invalide ou composants gameplay présents.");
        }
        foreach (var track in lab.timeline.GetOutputTracks().OfType<AnimationTrack>())
        {
            if (track.name == "Player.Animator" && lab.director.GetGenericBinding(track) != lab.player ||
                track.name == "Enemy.Animator" && lab.director.GetGenericBinding(track) != lab.enemy)
                throw new InvalidOperationException("AnimationLab : binding acteur incorrect.");
        }
    }

    public static void ReconfigureAndVerify()
    {
        Reconfigure();
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var lab = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CombatAnimationLab>(true)).Single();
            Validate(lab);
            if (EditorApplication.isPlaying || lab.playerClip == null || lab.enemyClip == null ||
                Mathf.Abs(Vector3.Distance(lab.player.transform.position, lab.enemy.transform.position) - 1.7f) > .01f ||
                Vector3.Dot(lab.player.transform.forward, lab.enemy.transform.forward) > -.99f)
                throw new InvalidOperationException("Plateau/Clips de preview invalides.");
            var hand = lab.player.GetBoneTransform(HumanBodyBones.RightHand);
            Quaternion original = hand.localRotation;
            AnimationMode.StartAnimationMode();
            try
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(lab.player.gameObject, lab.playerClip, .4f);
                AnimationMode.EndSampling();
                Quaternion first = hand.localRotation;
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(lab.player.gameObject, lab.playerClip, 1.1f);
                AnimationMode.EndSampling();
                if (Quaternion.Angle(first, hand.localRotation) < .1f) throw new InvalidOperationException("Le clip ne deplace pas les os de Lucian en Edit Mode.");
            }
            finally { AnimationMode.StopAnimationMode(); }
            if (Quaternion.Angle(original, hand.localRotation) > .1f) throw new InvalidOperationException("L'arret du preview ne restitue pas la pose initiale.");
            lab.director.RebuildGraph();
            lab.director.time = .4; lab.director.Evaluate();
            Quaternion timelineFirst = hand.localRotation;
            lab.director.time = 1.6; lab.director.Evaluate();
            if (Quaternion.Angle(timelineFirst, hand.localRotation) < .1f) throw new InvalidOperationException("La Timeline ne joue pas l'animation de Lucian en Edit Mode.");
            var spine = lab.enemy.GetBoneTransform(HumanBodyBones.Spine);
            Quaternion enemyFirst = spine.localRotation;
            lab.director.time = 2.1; lab.director.Evaluate();
            if (Quaternion.Angle(enemyFirst, spine.localRotation) < .1f) throw new InvalidOperationException("La Timeline ne joue pas l'animation de l'ennemi en Edit Mode.");
            lab.director.Stop();
            Debug.Log("[AnimationLab] PASS Edit Mode: actors, facing/spacing, clips/bone sampling, pose restoration and player/enemy Timeline evaluation; never entered Play.");
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }
}
