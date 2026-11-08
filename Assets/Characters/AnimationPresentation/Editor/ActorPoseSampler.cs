using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>Samples an exact avatar without instantiating gameplay behaviours or firing events.</summary>
public sealed class ActorPoseSampler : IDisposable
{
    public struct Frame
    {
        public float time;
        public Vector3 root, delta, body, leftFoot, rightFoot, leftToe, rightToe;
        public Quaternion rotation, bodyRotation, leftRotation, rightRotation;
        public Vector3[] bonePositions;
        public Quaternion[] boneRotations;
        public Vector3 leftSole, rightSole;
        public Vector3 leftGoal,rightGoal;
        public Quaternion leftGoalRotation,rightGoalRotation;
    }
    private readonly Animator source;
    private GameObject root;
    public ActorPoseSampler(Animator source)
    {
        if (source == null || source.avatar == null || !source.avatar.isHuman || !source.avatar.isValid)
            throw new InvalidOperationException("A valid actor Humanoid avatar is required.");
        this.source = source;
    }
    private static Transform Clone(Transform original, Dictionary<Transform,Transform> map)
    {
        var t = new GameObject(original.name).transform;
        t.SetLocalPositionAndRotation(original.localPosition, original.localRotation);
        t.localScale = original.localScale;
        map.Add(original,t);
        foreach (Transform child in original) Clone(child,map).SetParent(t, false);
        t.gameObject.SetActive(original.gameObject.activeSelf);
        return t;
    }
    public Frame[] Sample(AnimationClip clip, bool rootMotion = false, int sampleCount = 0, bool measureSoles = false, bool applyFootIK = false, Action<Animator> poseCorrection = null, bool manualSoles = true, int cycles = 1, RuntimeAnimatorController controller = null, Action<AnimatorControllerPlayable> configureController = null,bool captureIKGoals=false,Action<Animator> modifyIKGoals=null,Action<Animator,ActorSoleSampler,int> geometrySample=null,bool warmControllerPose=false)
    {
        if(cycles<1)throw new ArgumentOutOfRangeException(nameof(cycles));
        if (root != null) UnityEngine.Object.DestroyImmediate(root);
        var map = new Dictionary<Transform,Transform>();
        root = Clone(source.transform,map).gameObject;
        root.hideFlags = HideFlags.HideAndDontSave;
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = source.transform.lossyScale;
        root.SetActive(true);
        if (measureSoles)
            foreach (var skin in source.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var copy = map[skin.transform].gameObject.AddComponent<SkinnedMeshRenderer>();
                copy.sharedMesh = skin.sharedMesh; copy.sharedMaterials = skin.sharedMaterials;
                copy.bones = Array.ConvertAll(skin.bones,b => b != null && map.TryGetValue(b,out var t) ? t : null);
                copy.rootBone = skin.rootBone != null && map.TryGetValue(skin.rootBone,out var rt) ? rt : null;
                copy.localBounds = skin.localBounds; copy.enabled = skin.enabled;
                for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++) copy.SetBlendShapeWeight(i,skin.GetBlendShapeWeight(i));
            }
        var animator = root.AddComponent<Animator>();
        animator.avatar = source.avatar;
        animator.applyRootMotion = rootMotion;
        animator.fireEvents = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind();
        ActorIKGoalCapture goalCapture=null;
        if(captureIKGoals||modifyIKGoals!=null){goalCapture=root.AddComponent<ActorIKGoalCapture>();goalCapture.animator=animator;goalCapture.beforeCapture=modifyIKGoals;}
        var soles = measureSoles ? new ActorSoleSampler(animator,manualSoles) : null;
        var graph = PlayableGraph.Create("Actor pose audit");
        try
        {
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            Playable playable;
            if(controller==null)
            {
                var direct=AnimationClipPlayable.Create(graph,clip);
                direct.SetApplyFootIK(applyFootIK);direct.SetApplyPlayableIK(goalCapture!=null);playable=direct;
            }
            else
            {
                var controlled=AnimatorControllerPlayable.Create(graph,controller);
                configureController?.Invoke(controlled);playable=controlled;
            }
            AnimationPlayableOutput.Create(graph, "Pose", animator).SetSourcePlayable(playable);
            graph.Play();
            if(warmControllerPose&&controller!=null)
            {
                // Resolve the initial controller pose at zero time before an IK
                // callback reads its muscles. No measured callbacks are consumed.
                if(goalCapture!=null)goalCapture.beforeCapture=null;
                graph.Evaluate(0);
                if(goalCapture!=null)goalCapture.beforeCapture=modifyIKGoals;
            }
            graph.Evaluate(0);
            float duration=clip.length*cycles;
            int count = sampleCount > 0 ? sampleCount : Mathf.Max(2, Mathf.CeilToInt(duration * 60));
            var frames = new Frame[count + 1];
            for (int i = 0; i <= count; i++)
            {
                if (i > 0) graph.Evaluate(duration / count);
                poseCorrection?.Invoke(animator);
                if(goalCapture!=null&&goalCapture.evaluations==0)throw new InvalidOperationException("The native IK callback was not evaluated");
                Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                Transform lt = animator.GetBoneTransform(HumanBodyBones.LeftToes);
                Transform rt = animator.GetBoneTransform(HumanBodyBones.RightToes);
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                var positions = new Vector3[(int)HumanBodyBones.LastBone];
                var rotations = new Quaternion[positions.Length];
                for (int bone = 0; bone < positions.Length; bone++)
                {
                    var transform = animator.GetBoneTransform((HumanBodyBones)bone);
                    positions[bone] = transform != null ? transform.position : Vector3.zero;
                    rotations[bone] = transform != null ? transform.rotation : Quaternion.identity;
                }
                Vector3 leftSole = new Vector3(float.NaN,float.NaN,float.NaN),rightSole = leftSole;
                soles?.Sample(out leftSole,out rightSole);
                geometrySample?.Invoke(animator,soles,i);
                frames[i] = new Frame { time = duration * i / count, root = root.transform.position,
                    rotation = root.transform.rotation, delta = animator.deltaPosition,
                    body = hips.position, bodyRotation = hips.rotation,
                    leftFoot = left.position, rightFoot = right.position,
                    leftToe = lt != null ? lt.position : left.position,
                    rightToe = rt != null ? rt.position : right.position,
                    leftRotation = left.rotation, rightRotation = right.rotation,
                    bonePositions = positions, boneRotations = rotations,
                    leftSole = leftSole, rightSole = rightSole,leftGoal=goalCapture!=null?goalCapture.left:default,rightGoal=goalCapture!=null?goalCapture.right:default,
                    leftGoalRotation=goalCapture!=null?goalCapture.leftRotation:default,rightGoalRotation=goalCapture!=null?goalCapture.rightRotation:default };
            }
            return frames;
        }
        finally { graph.Destroy(); soles?.Dispose(); }
    }
    public void Dispose() { if (root != null) UnityEngine.Object.DestroyImmediate(root); }
}
