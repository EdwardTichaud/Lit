using UnityEngine;

public partial class LitOpsiveLocomotionBridge
{
    private ExternalLockHandle narrativeWalkLock;
    private Vector2 narrativeWalkInput;
    private bool HasNarrativeWalk => narrativeWalkLock != null && !narrativeWalkLock.IsReleased;

    public bool TryBeginNarrativeWalk(object owner, out ExternalLockHandle handle)
    {
        handle=null;
        if (HasNarrativeWalk || IsExternalLockActive || !Grounded || IsFlightModeActive || combatLockActive ||
            GetComponent<PlayerActionPresentationController>()?.IsActionActive == true ||
            IsCinematicMotionSessionActive || IsScriptedTraversalActive) return false;
        // The regular input-disable event also disables the UCC handler. Keep
        // that handler alive: focus + this lease reject human input instead.
        if (!TryAcquireExternalLock(owner,out handle,false,true)) return false;
        narrativeWalkLock=handle;narrativeWalkInput=Vector2.zero;
        var acquired=handle;
        handle.Released+=()=>{
            if(!ReferenceEquals(narrativeWalkLock,acquired))return;
            narrativeWalkLock=null;narrativeWalkInput=Vector2.zero;
        };
        return true;
    }
    public bool SetNarrativeWalkInput(ExternalLockHandle handle, Vector2 worldInput)
    {
        if (!HasNarrativeWalk || !ReferenceEquals(handle,narrativeWalkLock) || externalLockCount!=1 ||
            !IsDriving || !Grounded || IsCinematicMotionSessionActive || IsScriptedTraversalActive ||
            GetComponent<PlayerActionPresentationController>()?.IsActionActive == true) return false;
        var network=GetComponent<Unity.Netcode.NetworkObject>();
        if(network!=null&&network.IsSpawned&&!network.IsOwner)return false;
        narrativeWalkInput=Vector2.ClampMagnitude(worldInput,1);
        return true;
    }
    private bool TickNarrativeWalk()
    {
        if (!HasNarrativeWalk || externalLockCount!=1 ||
            GetComponent<PlayerActionPresentationController>()?.IsActionActive == true) return false;
        sprintPressed=false;
        ApplyWorldMoveInput(narrativeWalkInput);
        UpdateAnimatorParameters();
        RefreshSquadFacadeSystems();
        return true;
    }
    public bool SetNarrativeFacingDirection(Vector3 direction)
    {
        ResolveReferences();direction.y=0;
        if(!HasActiveLocomotionBody || direction.sqrMagnitude<.0001f)return false;
        direction.Normalize();ForceOrientationLookDirection(direction);
        var rotation=Quaternion.LookRotation(direction,Vector3.up);
        if(Quaternion.Angle(locomotion.transform.rotation,rotation)>.05f)
            locomotion.SetRotation(rotation,snapAnimator:false);
        return true;
    }
}
