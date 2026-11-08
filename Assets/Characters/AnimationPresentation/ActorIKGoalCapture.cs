using System;
using UnityEngine;

/// <summary>Temporary probe component. IK access is confined to Unity's IK callback.</summary>
[ExecuteAlways]
public sealed class ActorIKGoalCapture : MonoBehaviour
{
    public Animator animator;
    public Vector3 left,right;
    public Quaternion leftRotation,rightRotation;
    public int evaluations;
    [NonSerialized] public Action<Animator> beforeCapture;
    private void OnAnimatorIK(int layer)
    {
        if(layer!=0)return;
        beforeCapture?.Invoke(animator);
        left=animator.transform.InverseTransformPoint(animator.GetIKPosition(AvatarIKGoal.LeftFoot));right=animator.transform.InverseTransformPoint(animator.GetIKPosition(AvatarIKGoal.RightFoot));
        leftRotation=Quaternion.Inverse(animator.transform.rotation)*animator.GetIKRotation(AvatarIKGoal.LeftFoot);rightRotation=Quaternion.Inverse(animator.transform.rotation)*animator.GetIKRotation(AvatarIKGoal.RightFoot);evaluations++;
    }
}
