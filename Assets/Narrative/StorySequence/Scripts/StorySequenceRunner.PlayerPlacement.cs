using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Lit.Story
{
    public sealed partial class StorySequenceRunner
    {
        [Header("Player placement before dialogue")]
        [SerializeField,Tooltip("Optional Empty: position to walk to and initial body rotation (Y).")]
        private Transform playerTransform;
        [SerializeField,Min(1f)] private float playerApproachTimeout=30f;
        [SerializeField] private int playerNavMeshAgentTypeId;
        private LitOpsiveLocomotionBridge approachBridge;
        private LitOpsiveLocomotionBridge.ExternalLockHandle approachLock;
        private CharacterDialoguePresentationController conversationLook;
        private int conversationLookToken;
        private bool sequenceContentStarted;
        private int playbackGeneration;
        private bool hasPlayerPlacement;
        private Pose playerPlacementPose;
        public bool IsPreparingPlayer { get; private set; }
        public event Action PlaybackStarted;
        public event Action PreparationFailed;

        private void BeginPlayerConversationLook(StorySequenceAsset asset)
        {
            if(!asset.lockPlayerControl && !hasPlayerPlacement)return;
            conversationLook=LocalPlayerContext.LocalCharacterRoot?.GetComponent<CharacterDialoguePresentationController>();
            if(conversationLook!=null)conversationLookToken=conversationLook.BeginSequenceLook(this);
        }
        private IEnumerator PreparePlayerPlacement()
        {
            IsPreparingPlayer=true;
            var root=LocalPlayerContext.LocalCharacterRoot;
            var character=root!=null?root.GetComponent<SquadCharacterController>():null;
            approachBridge=root!=null?root.GetComponent<LitOpsiveLocomotionBridge>():null;
            if(root==null || approachBridge==null || character==null || character.CurrentHp<=0 ||
                !approachBridge.TryBeginNarrativeWalk(this,out approachLock))
            { FailPlayerPlacement("Player unavailable or movement already owned.");yield break; }
            if(playerTransform==null){FailPlayerPlacement("Placement marker disappeared.");yield break;}
            Vector3 destination=playerPlacementPose.position;
            Vector3 facing=Vector3.ProjectOnPlane(playerPlacementPose.rotation*Vector3.forward,Vector3.up).normalized;
            if(facing.sqrMagnitude<.001f){FailPlayerPlacement("Invalid marker orientation.");yield break;}
            var filter=new NavMeshQueryFilter {agentTypeID=playerNavMeshAgentTypeId,areaMask=NavMesh.AllAreas};
            var path=new NavMeshPath();
            if(!TryPlacementPath(root.position,destination,filter,path))
            {FailPlayerPlacement("No complete NavMesh path to playerTransform.");yield break;}
            var domain=root.GetComponent<CombatTimeDomain>();
            float elapsed=0,repath=0,noProgress=0;
            Vector3 lastProgressPosition=root.position;
            int corner=1,settled=0;
            while(elapsed<playerApproachTimeout)
            {
                if(playerTransform==null || root==null || character==null || character.CurrentHp<=0 || root!=LocalPlayerContext.LocalCharacterRoot ||
                    approachLock==null || approachLock.IsReleased || !approachBridge.SetNarrativeWalkInput(approachLock,Vector2.zero))
                {FailPlayerPlacement("Placement interrupted. "+(approachBridge!=null?approachBridge.DriveDiagnostic+" grounded="+approachBridge.Grounded+" lockReleased="+(approachLock==null||approachLock.IsReleased):"Bridge missing"));yield break;}
                float dt=Mathf.Max(0,domain!=null?domain.FixedDeltaTime:Time.fixedDeltaTime);
                elapsed+=dt;repath+=dt;
                float distance=Vector3.ProjectOnPlane(destination-root.position,Vector3.up).magnitude;
                if(distance<=.15f && Mathf.Abs(destination.y-root.position.y)<=.3f && corner>=path.corners.Length-1 && approachBridge.PlanarVelocity.magnitude<=.05f)
                {
                    settled++;
                    Vector3 turn=Vector3.RotateTowards(root.forward,facing,120f*Mathf.Deg2Rad*dt,0);
                    approachBridge.SetNarrativeFacingDirection(turn);
                    if(settled>=3 && Vector3.Angle(root.forward,facing)<=2f)break;
                    yield return new WaitForFixedUpdate();continue;
                }
                settled=0;
                if(repath>=.5f)
                {
                    repath=0;
                    if(!TryPlacementPath(root.position,destination,filter,path))
                    {FailPlayerPlacement("Path became inaccessible.");yield break;}
                    corner=1;
                }
                while(corner<path.corners.Length-1 && Vector3.ProjectOnPlane(path.corners[corner]-root.position,Vector3.up).magnitude<.25f)corner++;
                Vector3 waypoint=path.corners[Mathf.Min(corner,path.corners.Length-1)];
                Vector3 travel=Vector3.ProjectOnPlane(waypoint-root.position,Vector3.up);
                if(Vector3.ProjectOnPlane(root.position-lastProgressPosition,Vector3.up).magnitude>.01f)
                {lastProgressPosition=root.position;noProgress=0;}else noProgress+=dt;
                if(noProgress>=2f){FailPlayerPlacement("No movement progress for two seconds.");yield break;}
                Vector3 intent=distance<=.15f && corner>=path.corners.Length-1?Vector3.zero:travel.normalized*Mathf.Clamp01((corner<path.corners.Length-1?travel.magnitude:distance)/.6f);
                approachBridge.SetNarrativeWalkInput(approachLock,new Vector2(intent.x,intent.z));
                if(travel.sqrMagnitude>.001f && intent.sqrMagnitude>.001f)
                    approachBridge.SetNarrativeFacingDirection(Vector3.RotateTowards(root.forward,travel.normalized,120f*Mathf.Deg2Rad*dt,0));
                yield return new WaitForFixedUpdate();
            }
            if(elapsed>=playerApproachTimeout){FailPlayerPlacement("Placement timeout.");yield break;}
            // Take over the stationary lock before releasing walking: no input gap.
            AcquirePlayerLock(sequence);
            ReleasePlayerPreparation();
        }
        internal static bool TryPlacementPath(Vector3 start,Vector3 destination,NavMeshQueryFilter filter,NavMeshPath path)
        {
            if(!NavMesh.SamplePosition(start,out var from,.5f,filter) || !NavMesh.SamplePosition(destination,out var to,.5f,filter))return false;
            return NavMesh.CalculatePath(from.position,to.position,filter,path) && path.status==NavMeshPathStatus.PathComplete && path.corners.Length>0;
        }
        private void FailPlayerPlacement(string reason)
        {
            Debug.LogWarning("[StorySequence] Player placement aborted: "+reason,this);
            stepFailed=true;ReleasePlayerPreparation();
        }
        private void ReleasePlayerPreparation()
        {
            approachLock?.Dispose();approachLock=null;approachBridge=null;IsPreparingPlayer=false;
        }
    }
}
