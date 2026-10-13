#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

public sealed class LucianCombatFacingTests
{
    [TestCase("Idle")]
    [TestCase("Strafe_Walk_F")][TestCase("Strafe_Walk_B")]
    [TestCase("Strafe_Walk_L")][TestCase("Strafe_Walk_R")]
    [TestCase("Strafe_Walk_FL")][TestCase("Strafe_Walk_FR")]
    [TestCase("Strafe_Walk_BL")][TestCase("Strafe_Walk_BR")]
    [TestCase("Strafe_Run_F")][TestCase("Strafe_Run_B")]
    [TestCase("Strafe_Run_L")][TestCase("Strafe_Run_R")]
    [TestCase("Strafe_Run_FL")][TestCase("Strafe_Run_FR")]
    [TestCase("Strafe_Run_BL")][TestCase("Strafe_Run_BR")]
    public void TorsoFacesForwardWithoutMovingAuthoredLowerBody(string cycle)
    {
        var clip=ActorAnimationAudit.Source(ActorAnimationAudit.Sources+"Twinblades_"+cycle+"_Inplace.FBX");
        using (var sampler=new ActorPoseSampler(ActorAnimationAudit.Actor(false)))
        {
            var source=sampler.Sample(clip);
            var corrected=sampler.Sample(clip,poseCorrection:rig=>LucianCombatTorsoFacing.AlignUpperBody(rig,Vector3.forward,Vector3.up,65,1));
            for (int i=0;i<source.Length;i++)
            {
                var s=source[i]; var c=corrected[i];
                var right=c.bonePositions[(int)HumanBodyBones.RightUpperArm]-c.bonePositions[(int)HumanBodyBones.LeftUpperArm];
                Assert.Less(Mathf.Abs(Vector3.SignedAngle(Vector3.forward,Vector3.Cross(right,Vector3.up),Vector3.up)),1,cycle+" torso at "+i);
                Assert.Less(Vector3.Distance(s.root,c.root),.00001f);
                Assert.Less(Quaternion.Angle(s.rotation,c.rotation),.03f);
                foreach (var bone in new[] {HumanBodyBones.Hips,HumanBodyBones.LeftUpperLeg,HumanBodyBones.RightUpperLeg,
                    HumanBodyBones.LeftLowerLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot,
                    HumanBodyBones.LeftToes,HumanBodyBones.RightToes})
                {
                    Assert.Less(Vector3.Distance(s.bonePositions[(int)bone],c.bonePositions[(int)bone]),.00001f,cycle+" "+bone);
                    Assert.Less(Quaternion.Angle(s.boneRotations[(int)bone],c.boneRotations[(int)bone]),.03f,cycle+" "+bone);
                }
            }
        }
    }
}
#endif
