using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Opt-in diagnostic of rendered soles, rather than treating the ankle joint as the floor contact.</summary>
public sealed class ActorSoleSampler : IDisposable
{
    private sealed class Skin
    {
        public SkinnedMeshRenderer renderer;
        public Mesh baked;
        public int[] left, right;
        public Vector3[] sourceVertices;
        public BoneWeight[] weights;
        public Matrix4x4[] bindposes;
    }
    private readonly List<Skin> skins = new List<Skin>();
    private readonly bool manualSkinning;
    public ActorSoleSampler(Animator actor,bool manualSkinning = false)
    {
        this.manualSkinning = manualSkinning;
        var leftFoot = actor.GetBoneTransform(HumanBodyBones.LeftFoot);
        var rightFoot = actor.GetBoneTransform(HumanBodyBones.RightFoot);
        foreach (var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (renderer.sharedMesh == null || !renderer.enabled) continue;
            var weights = renderer.sharedMesh.boneWeights;
            if (weights == null || weights.Length == 0) continue;
            var left = new List<int>(); var right = new List<int>();
            var bones = renderer.bones;
            for (int i = 0; i < weights.Length; i++)
            {
                var w = weights[i];
                int[] indices = {w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};
                float[] values = {w.weight0,w.weight1,w.weight2,w.weight3};
                float lw = 0,rw = 0;
                for (int j = 0; j < 4; j++)
                {
                    if (indices[j] < 0 || indices[j] >= bones.Length || bones[indices[j]] == null) continue;
                    if (bones[indices[j]] == leftFoot || bones[indices[j]].IsChildOf(leftFoot)) lw += values[j];
                    if (bones[indices[j]] == rightFoot || bones[indices[j]].IsChildOf(rightFoot)) rw += values[j];
                }
                if (lw >= .5f) left.Add(i);
                if (rw >= .5f) right.Add(i);
            }
            if (left.Count == 0 && right.Count == 0) continue;
            if (manualSkinning)
            {
                var counts = renderer.sharedMesh.GetBonesPerVertex();
                foreach (int index in left.Concat(right)) if (counts[index] > 4)
                    throw new InvalidOperationException("Manual sole audit requires supported skin weights: " + renderer.name);
            }
            skins.Add(new Skin {renderer=renderer,baked=new Mesh(),left=left.ToArray(),right=right.ToArray(),
                sourceVertices=manualSkinning ? MorphVertices(renderer) : null,weights=weights,
                bindposes=manualSkinning ? renderer.sharedMesh.bindposes : null});
        }
    }
    private static Vector3[] MorphVertices(SkinnedMeshRenderer renderer)
    {
        var mesh = renderer.sharedMesh; var vertices = mesh.vertices;
        for (int shape = 0; shape < mesh.blendShapeCount; shape++)
        {
            float weight = renderer.GetBlendShapeWeight(shape);
            if (Mathf.Abs(weight) <= .001f) continue;
            if (mesh.GetBlendShapeFrameCount(shape) != 1 || Mathf.Abs(mesh.GetBlendShapeFrameWeight(shape,0)) <= .001f)
                throw new InvalidOperationException("Multiframe morph requires a rendered sole capture: " + renderer.name);
            var delta = new Vector3[vertices.Length]; mesh.GetBlendShapeFrameVertices(shape,0,delta,null,null);
            float factor = weight / mesh.GetBlendShapeFrameWeight(shape,0);
            for (int i = 0; i < vertices.Length; i++) vertices[i] += delta[i] * factor;
        }
        return vertices;
    }
    public bool Sample(out Vector3 left,out Vector3 right)
    {
        left = right = new Vector3(0,float.PositiveInfinity,0);
        foreach (var skin in skins)
        {
            if (skin.renderer == null) continue;
            if (manualSkinning)
            {
                var bones = skin.renderer.bones;
                var matrices = new Matrix4x4[bones.Length];
                for (int i = 0; i < bones.Length; i++) matrices[i] = bones[i] != null ? bones[i].localToWorldMatrix * skin.bindposes[i] : Matrix4x4.identity;
                ManualLowest(skin,matrices,skin.left,ref left); ManualLowest(skin,matrices,skin.right,ref right);
                continue;
            }
            skin.renderer.BakeMesh(skin.baked);
            var vertices = skin.baked.vertices;
            Lowest(skin.renderer.transform,vertices,skin.left,ref left);
            Lowest(skin.renderer.transform,vertices,skin.right,ref right);
        }
        return !float.IsInfinity(left.y) && !float.IsInfinity(right.y);
    }
    private static void ManualLowest(Skin skin,Matrix4x4[] matrices,int[] indices,ref Vector3 lowest)
    {
        foreach (int index in indices)
        {
            var w = skin.weights[index]; var v = skin.sourceVertices[index];
            Vector3 point = matrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0 + matrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1 +
                matrices[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2 + matrices[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;
            if (point.y < lowest.y) lowest = point;
        }
    }
    private static void Lowest(Transform transform,Vector3[] vertices,int[] indices,ref Vector3 lowest)
    {
        foreach (int index in indices)
        {
            var position = transform.TransformPoint(vertices[index]);
            if (position.y < lowest.y) lowest = position;
        }
    }
    public void Dispose()
    {
        foreach (var skin in skins) if (skin.baked != null)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(skin.baked);
            else UnityEngine.Object.DestroyImmediate(skin.baked);
        }
        skins.Clear();
    }
}
