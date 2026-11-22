using Unity.Netcode;
using UnityEngine;
using Opsive.UltimateCharacterController.Character;

public struct DialogueSequenceLookState : INetworkSerializable, System.IEquatable<DialogueSequenceLookState>
{
    public bool active;
    public int generation,revision;
    public float bodyYaw,headYaw;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T:IReaderWriter
    {
        serializer.SerializeValue(ref active);serializer.SerializeValue(ref generation);serializer.SerializeValue(ref revision);
        serializer.SerializeValue(ref bodyYaw);serializer.SerializeValue(ref headYaw);
    }
    public bool Equals(DialogueSequenceLookState other)=>active==other.active && generation==other.generation && revision==other.revision && bodyYaw.Equals(other.bodyYaw) && headYaw.Equals(other.headYaw);
}

public sealed partial class CharacterDialoguePresentationController
{
    public const float MaximumSequenceHeadYaw=45f;
    private readonly NetworkVariable<DialogueSequenceLookState> sequenceLook=new(default,NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Owner);
    private DialogueSequenceLookState lookState;
    private Object lookOwner;
    private Transform previousSequenceSpeaker,lookControlledRoot,lookHead;
    private CharacterIK lookIK;
    private int lookGeneration;
    private float lookYaw,lookBlend,oldBodyWeight,oldHeadWeight,oldEyesWeight,oldLookSpeed;
    private bool lookSettingsSaved;

    public int BeginSequenceLook(Object owner)
    {
        Resolve();
        if(owner==null || !isActiveAndEnabled || Profile==null || IsSpawned&&!IsOwner)return 0;
        if(IsSpawned)lookGeneration=Mathf.Max(lookGeneration,sequenceLook.Value.generation);
        ResetSequenceLookImmediate();
        lookOwner=owner;lookControlledRoot=LocalPlayerContext.LocalCharacterRoot;
        lookState=new DialogueSequenceLookState {active=true,generation=++lookGeneration,bodyYaw=transform.eulerAngles.y};
        previousSequenceSpeaker=null;lookYaw=0;
        PublishSequenceLook();return lookState.generation;
    }
    public void SetSequenceSpeaker(int token,Transform physicalSpeaker)
    {
        if(!lookState.active || token==0 || token!=lookState.generation || IsSpawned&&!IsOwner || physicalSpeaker==null ||
            physicalSpeaker==transform || physicalSpeaker.IsChildOf(transform) || physicalSpeaker==previousSequenceSpeaker)return;
        previousSequenceSpeaker=physicalSpeaker;
        var actor=physicalSpeaker.GetComponent<Lit.Story.StorySequenceActor>();
        Vector3 position=actor!=null?actor.FaceAnchor.position:physicalSpeaker.position;
        Vector3 direction=Vector3.ProjectOnPlane(position-transform.position,Vector3.up);
        if(direction.sqrMagnitude<.0001f)return;
        lookState.headYaw=ClampSequenceHeadYaw(lookState.bodyYaw,direction);
        lookState.revision++;PublishSequenceLook();
    }
    public static float ClampSequenceHeadYaw(float bodyYaw,Vector3 direction)
    {
        direction.y=0;
        if(direction.sqrMagnitude<.0001f)return 0;
        return Mathf.Clamp(Vector3.SignedAngle(Quaternion.Euler(0,bodyYaw,0)*Vector3.forward,direction,Vector3.up),-MaximumSequenceHeadYaw,MaximumSequenceHeadYaw);
    }
    public void EndSequenceLook(int token)
    {
        if(token==0 || token!=lookState.generation || IsSpawned&&!IsOwner)return;
        lookState.active=false;lookOwner=null;PublishSequenceLook();
    }
    private void OnSequenceLookReplication(DialogueSequenceLookState oldState,DialogueSequenceLookState newState)
    {
        if(IsOwner)return;
        if(oldState.generation!=newState.generation)lookYaw=0;
        lookState=newState;
    }
    private void PublishSequenceLook(){if(IsSpawned&&IsOwner)sequenceLook.Value=lookState;}
    private void RefreshSequenceLook(float delta)
    {
        bool local=!IsSpawned||IsOwner;
        if(local&&lookState.active&&(lookOwner==null || lookOwner is Behaviour ownerBehaviour&&!ownerBehaviour.isActiveAndEnabled ||
            lookControlledRoot==transform&&LocalPlayerContext.LocalCharacterRoot!=transform || squad!=null&&squad.CurrentHp<=0))
        {lookState.active=false;PublishSequenceLook();}
        bool priority=action!=null&&(action.IsActionActive||action.IsDeathAnimationLocked) ||
            animationController!=null&&animationController.IsCinematicMotionActive ||
            bridge!=null&&(bridge.IsCinematicMotionSessionActive||bridge.IsScriptedTraversalActive);
        if(priority){RestoreSequenceIK();return;}
        if(local&&lookState.active&&bridge!=null)
            bridge.SetNarrativeFacingDirection(Quaternion.Euler(0,lookState.bodyYaw,0)*Vector3.forward);
        lookBlend=Mathf.MoveTowards(lookBlend,lookState.active?1:0,delta/Mathf.Max(.05f,Profile!=null?Profile.blendSeconds:.25f));
        if(lookBlend<=0){RestoreSequenceIK();return;}
        if(lookIK==null&&animator!=null)lookIK=animator.GetComponent<CharacterIK>();
        if(lookHead==null&&animator!=null&&animator.isHuman)lookHead=animator.GetBoneTransform(HumanBodyBones.Head);
        if(lookIK==null||!lookIK.isActiveAndEnabled||lookHead==null)return;
        if(!lookSettingsSaved)
        {
            oldBodyWeight=lookIK.LookAtBodyWeight;oldHeadWeight=lookIK.LookAtHeadWeight;
            oldEyesWeight=lookIK.LookAtEyesWeight;oldLookSpeed=lookIK.LookAtAdjustmentSpeed;lookSettingsSaved=true;
        }
        lookYaw=Mathf.MoveTowardsAngle(lookYaw,lookState.headYaw,(Profile!=null?Profile.facingDegreesPerSecond:100)*delta);
        float bodyYaw=local?lookState.bodyYaw:transform.eulerAngles.y;
        Vector3 target=lookHead.position+Quaternion.Euler(0,bodyYaw+Mathf.Clamp(lookYaw,-45,45),0)*Vector3.forward*3;
        lookIK.SetLookAtPosition(true,target);
        lookIK.LookAtBodyWeight=0;
        lookIK.LookAtHeadWeight=(Profile!=null?Profile.headLookWeight:.65f)*lookBlend;
        lookIK.LookAtEyesWeight=0;lookIK.LookAtAdjustmentSpeed=1;
    }
    private void RestoreSequenceIK()
    {
        if(lookSettingsSaved&&lookIK!=null)
        {
            lookIK.SetLookAtPosition(false,Vector3.zero);
            lookIK.LookAtBodyWeight=oldBodyWeight;lookIK.LookAtHeadWeight=oldHeadWeight;
            lookIK.LookAtEyesWeight=oldEyesWeight;lookIK.LookAtAdjustmentSpeed=oldLookSpeed;
        }
        lookSettingsSaved=false;lookBlend=0;
    }
    private void ResetSequenceLookImmediate()
    {
        lookState.active=false;lookOwner=null;previousSequenceSpeaker=null;
        RestoreSequenceIK();PublishSequenceLook();
    }
}
