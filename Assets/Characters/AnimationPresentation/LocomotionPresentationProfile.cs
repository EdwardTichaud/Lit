using System;
using UnityEngine;

/// <summary>Presentation only. Never sets physical speed, position or Animator.speed.</summary>
[CreateAssetMenu(menuName = "Lit/Animation/Locomotion Presentation Profile")]
public sealed class LocomotionPresentationProfile : ScriptableObject
{
    public const string PlaybackParameter = "LocomotionPlaybackRate";
    [Serializable] public sealed class Cycle
    {
        public AnimationClip clip;
        public Vector2 direction = Vector2.up;
        [Min(.001f)] public float nativeSpeed;
        [Range(0,1)] public float leftContactPhase, rightContactPhase;
        [Range(0,1)] public float cycleOffset;
    }
    public Avatar measuredAvatar;
    public Cycle[] walk = Array.Empty<Cycle>(), run = Array.Empty<Cycle>();
    public Cycle explorationWalk, explorationRun;
    public bool validated;
    [Tooltip("Select and blend the measured walk/run cycles by obtained speed, independent of the requested physical sprint modifier.")]
    public bool chooseGaitFromObtainedSpeed;
    [Tooltip("Use the actual neighbouring unit-circle points of a SimpleDirectional2D tree, including uneven directions.")]
    public bool matchSimpleDirectionalBlend;
    [Min(0)] public float stopSpeed = .05f;
    [Min(.01f)] public float minimumCadence = .25f;
    [Min(.01f)] public float maximumCadence = 4f;
    [Tooltip("Existing controller's walk/run thresholds; these do not set physical speed.")]
    public Vector2 blendThresholds = new Vector2(1.35f, 3.25f);
    public Vector2 explorationBlendThresholds = new Vector2(1.35f, 3.25f);
    [Tooltip("For a NavMesh actor, reconcile the baked surface height with the collider through the Agent's base offset.")]
    public bool alignNavMeshGround;
    [Min(.01f)] public float maximumNavMeshGroundCorrection = .25f;

    public bool IsUsable(Avatar avatar) => validated && avatar == measuredAvatar &&
        avatar != null && avatar.isValid && Complete(walk) && Complete(run) &&
        Finite(minimumCadence) && Finite(maximumCadence) && minimumCadence > 0 && maximumCadence >= minimumCadence &&
        Finite(stopSpeed) && stopSpeed >= 0 && blendThresholds.x > 0 && blendThresholds.y > blendThresholds.x;
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool Complete(Cycle[] cycles)
    {
        if (cycles == null || cycles.Length == 0) return false;
        foreach (var cycle in cycles) if (cycle == null || cycle.clip == null || !Finite(cycle.nativeSpeed) || cycle.nativeSpeed <= 0 ||
            !Finite(cycle.direction.x) || !Finite(cycle.direction.y) || cycle.direction.sqrMagnitude < .001f) return false;
        return true;
    }
    public float NativeSpeed(bool running, Vector2 direction, bool exploration = false)
    {
        var forwardCycle = running ? explorationRun : explorationWalk;
        if (exploration && forwardCycle != null && forwardCycle.nativeSpeed > 0) return forwardCycle.nativeSpeed;
        var cycles = running ? run : walk;
        if (cycles == null || cycles.Length == 0) return 0;
        if(matchSimpleDirectionalBlend)return DirectionalValue(cycles,direction,CycleMeasure.Speed);
        // Blend the neighbouring directional measurements by their angle.
        float speed = 0, weight = 0;
        direction = direction.sqrMagnitude > .0001f ? direction.normalized : Vector2.up;
        foreach (var cycle in cycles)
        {
            float angle = Vector2.Angle(direction, cycle.direction);
            float w = cycles.Length == 1 ? 1 : Mathf.Max(0, 1 - angle / 45.01f);
            speed += cycle.nativeSpeed * w; weight += w;
        }
        return weight > 0 ? speed / weight : cycles[0].nativeSpeed;
    }
    private enum CycleMeasure { Speed, Duration, Distance }
    private static float CycleValue(Cycle cycle,CycleMeasure measure)
    {
        if(measure==CycleMeasure.Speed)return cycle.nativeSpeed;
        float length=cycle.clip!=null?cycle.clip.length:0;
        return measure==CycleMeasure.Duration?length:cycle.nativeSpeed*length;
    }
    private static float DirectionalValue(Cycle[] cycles,Vector2 direction,CycleMeasure measure)
    {
        if(cycles.Length==1)return CycleValue(cycles[0],measure);
        if(direction.sqrMagnitude<.0001f)direction=Vector2.up;
        float target=Mathf.Atan2(direction.x,direction.y)*Mathf.Rad2Deg;
        Cycle lower=null,upper=null;float before=360,after=360;
        foreach(var cycle in cycles)
        {
            float angle=Mathf.Atan2(cycle.direction.x,cycle.direction.y)*Mathf.Rad2Deg;
            float a=Mathf.Repeat(target-angle,360),b=Mathf.Repeat(angle-target,360);
            if(Mathf.Min(a,b)<.0001f)return CycleValue(cycle,measure);
            if(a<before){before=a;lower=cycle;}
            if(b<after){after=b;upper=cycle;}
        }
        if(lower==upper)return CycleValue(lower,measure);
        float lw=Mathf.Sin(after*Mathf.Deg2Rad),uw=Mathf.Sin(before*Mathf.Deg2Rad);
        if(lw<0||uw<0||lw+uw<.0001f){lw=after;uw=before;}
        return (CycleValue(lower,measure)*lw+CycleValue(upper,measure)*uw)/(lw+uw);
    }
    private float NativeCycleValue(bool running,Vector2 direction,bool exploration,CycleMeasure measure)
    {
        var forward=running?explorationRun:explorationWalk;
        if(exploration&&forward!=null&&forward.clip!=null)return CycleValue(forward,measure);
        return DirectionalValue(running?run:walk,direction,measure);
    }
    private float MixedNativeSpeed(Vector2 direction,float gait,bool exploration)
    {
        if(!matchSimpleDirectionalBlend)return Mathf.Lerp(NativeSpeed(false,direction,exploration),NativeSpeed(true,direction,exploration),gait);
        // Unity advances a shared normalized phase over the weighted clip duration.
        // Average distance per cycle, then divide by that duration; speed averages
        // alone are wrong when walk and run have different lengths.
        float distance=Mathf.Lerp(NativeCycleValue(false,direction,exploration,CycleMeasure.Distance),NativeCycleValue(true,direction,exploration,CycleMeasure.Distance),gait);
        float duration=Mathf.Lerp(NativeCycleValue(false,direction,exploration,CycleMeasure.Duration),NativeCycleValue(true,direction,exploration,CycleMeasure.Duration),gait);
        return distance/Mathf.Max(.001f,duration);
    }
    public void Evaluate(float physicalSpeed, bool running, Vector2 direction, out float blend, out float cadence, bool exploration = false)
    {
        if (physicalSpeed <= stopSpeed) { blend = 0; cadence = 1; return; }
        float gait=running?1:0;
        float native = Mathf.Max(.001f, NativeSpeed(running, direction, exploration));
        if(chooseGaitFromObtainedSpeed)
        {
            float walkSpeed=Mathf.Max(.001f,NativeSpeed(false,direction,exploration));
            float runSpeed=Mathf.Max(.001f,NativeSpeed(true,direction,exploration));
            gait=runSpeed>walkSpeed?Mathf.InverseLerp(walkSpeed,runSpeed,physicalSpeed):gait;
            native=MixedNativeSpeed(direction,gait,exploration);
        }
        float requested = physicalSpeed / native;
        cadence = Mathf.Clamp(requested, minimumCadence, maximumCadence);
        // At very low speeds the Idle weight completes the remaining slowdown.
        var thresholds = exploration ? explorationBlendThresholds : blendThresholds;
        float selectedBlend=chooseGaitFromObtainedSpeed?Mathf.Lerp(thresholds.x,thresholds.y,gait):(running?thresholds.y:thresholds.x);
        blend = selectedBlend * Mathf.Min(1, requested / minimumCadence);
    }
}
