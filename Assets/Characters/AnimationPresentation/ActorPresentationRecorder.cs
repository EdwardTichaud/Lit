using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Opt-in Play Mode capture on the actual rendered actors. No pose or movement writes.</summary>
[DisallowMultipleComponent]
public sealed class ActorPresentationRecorder : MonoBehaviour
{
    public Animator animator;
    public string authority;
    private StreamWriter writer;
    private Vector3 previous;
    private float elapsed;
    private int rateHash = Animator.StringToHash(LocomotionPresentationProfile.PlaybackParameter);
    private bool hasRate;
    private ActorSoleSampler soles;
    private int speedHash;
    private bool hasSpeed;
    private void OnEnable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) return;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman) return;
        Directory.CreateDirectory("Library/ActorAnimationValidation");
        writer = new StreamWriter("Library/ActorAnimationValidation/" + name + "_play_" + System.DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".csv");
        writer.WriteLine("time,actual_speed,agent_speed,state,next_state,normalized,transition,rate,session,authority,root_x,root_y,root_z,body_y,left_x,left_y,left_z,right_x,right_y,right_z,leftsole_x,leftsole_y,leftsole_z,rightsole_x,rightsole_y,rightsole_z,floor_y,lit_speed,actor_clock,requested_input,clips,left_floor_y,right_floor_y,root_qx,root_qy,root_qz,root_qw,animator_x,animator_y,animator_z,animator_qx,animator_qy,animator_qz,animator_qw,hips_qx,hips_qy,hips_qz,hips_qw");
        soles = new ActorSoleSampler(animator);
        foreach (var parameter in animator.parameters) if (parameter.nameHash == rateHash) hasRate = true;
        foreach (var parameter in animator.parameters)
            if (parameter.name == "LitSpeed" || parameter.name == "Speed" && !hasSpeed)
            { speedHash = parameter.nameHash; hasSpeed = true; }
        previous = transform.position; elapsed = 0;
#endif
    }
    private void LateUpdate()
    {
        if (writer == null || Time.deltaTime <= 0) return;
        elapsed += Time.deltaTime;
        var root = transform.position;
        float speed = Vector3.ProjectOnPlane(root - previous, Vector3.up).magnitude / Time.deltaTime;
        previous = root;
        var state = animator.GetCurrentAnimatorStateInfo(0);
        var left = animator.GetBoneTransform(HumanBodyBones.LeftFoot).position;
        var right = animator.GetBoneTransform(HumanBodyBones.RightFoot).position;
        var enemy = GetComponent<LitBrainsEnemy>();
        var player = GetComponent<PlayerActionPresentationController>();
        var bridge = GetComponent<LitOpsiveLocomotionBridge>();
        string activeOwner = bridge != null && bridge.IsCinematicMotionSessionActive ? "Cinematic" :
            player != null && player.IsActionActive ? "PlayerAction" : enemy != null && enemy.AttackPhase != LitBrainsAttackPhase.None ? "EnemyAction" : authority;
        var agent = GetComponent<NavMeshAgent>();
        float[] values = { elapsed, speed, agent != null && agent.enabled ? agent.velocity.magnitude : 0,
            state.fullPathHash, animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0).fullPathHash : 0,
            state.normalizedTime, animator.IsInTransition(0) ? animator.GetAnimatorTransitionInfo(0).normalizedTime : 0,
            hasRate ? animator.GetFloat(rateHash) : 1, enemy != null ? enemy.ActionSequenceId : player != null ? player.ActionGeneration : 0 };
        var text = System.Array.ConvertAll(values, v => v.ToString("R", CultureInfo.InvariantCulture));
        text[3] = state.fullPathHash.ToString(CultureInfo.InvariantCulture);
        text[4] = (animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0).fullPathHash : 0).ToString(CultureInfo.InvariantCulture);
        writer.Write(string.Join(",",text));
        writer.Write("," + activeOwner + ",");
        Vector3 leftSole = default,rightSole = default;
        bool resolvedSoles = soles != null && soles.Sample(out leftSole,out rightSole);
        if (!resolvedSoles) leftSole = rightSole = new Vector3(float.NaN,float.NaN,float.NaN);
        float floor = float.NaN, closest = float.PositiveInfinity;
        foreach (var hit in Physics.RaycastAll(root + Vector3.up * 2,Vector3.down,5,~0,QueryTriggerInteraction.Ignore))
            if (!hit.collider.transform.IsChildOf(transform) && hit.distance < closest)
            { closest = hit.distance; floor = hit.point.y; }
        float[] pose = { root.x,root.y,root.z,animator.GetBoneTransform(HumanBodyBones.Hips).position.y,left.x,left.y,left.z,right.x,right.y,right.z,
            leftSole.x,leftSole.y,leftSole.z,rightSole.x,rightSole.y,rightSole.z,floor };
        writer.Write(string.Join(",", System.Array.ConvertAll(pose, v => v.ToString("R", CultureInfo.InvariantCulture))));
        writer.Write("," + (hasSpeed ? animator.GetFloat(speedHash) : float.NaN).ToString("R",CultureInfo.InvariantCulture));
        writer.Write("," + animator.speed.ToString("R",CultureInfo.InvariantCulture));
        writer.Write("," + (bridge != null ? bridge.CurrentWorldMoveInput.magnitude : 0).ToString("R",CultureInfo.InvariantCulture));
        var clips = animator.GetCurrentAnimatorClipInfo(0);
        writer.Write(",\"" + string.Join("|",System.Array.ConvertAll(clips,c => c.clip.name.Replace("\"","\"\"") + ":" + c.weight.ToString("R",CultureInfo.InvariantCulture))) + "\",");
        var rotation = transform.rotation; var ar = animator.transform.rotation; var ap = animator.transform.position;
        var hips = animator.GetBoneTransform(HumanBodyBones.Hips).rotation;
        float[] orientation = {GroundAt(leftSole),GroundAt(rightSole),rotation.x,rotation.y,rotation.z,rotation.w,
            ap.x,ap.y,ap.z,ar.x,ar.y,ar.z,ar.w,hips.x,hips.y,hips.z,hips.w};
        writer.WriteLine(string.Join(",",System.Array.ConvertAll(orientation,v => v.ToString("R",CultureInfo.InvariantCulture))));
    }
    private float GroundAt(Vector3 sole)
    {
        if (float.IsNaN(sole.y)) return float.NaN;
        float ground = float.NaN, nearest = float.PositiveInfinity;
        foreach (var hit in Physics.RaycastAll(sole + Vector3.up*.25f,Vector3.down,5,LayerMask.GetMask("Ground","Default"),QueryTriggerInteraction.Ignore))
            if (!hit.collider.transform.IsChildOf(transform) && hit.normal.y > .5f && hit.distance < nearest)
            { nearest = hit.distance; ground = hit.point.y; }
        return ground;
    }
    private void OnDisable() { writer?.Dispose(); writer = null; soles?.Dispose(); soles = null; }
}
