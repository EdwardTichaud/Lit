using System;
using System.Globalization;
using UnityEngine;

/// <summary>Read-only contact diagnostic. Holds a mesh vertex identity, never a character or foot position.</summary>
public sealed class ActorFootContactTracker
{
    public struct Observation
    {
        public int window;
        public string vertex;
        public Vector3 position;
        public float duration,path,net,height;
        public string Csv()=>window+","+(vertex??"")+","+string.Join(",",Array.ConvertAll(new[]{position.x,position.y,position.z,duration,path,net,height},v=>v.ToString("R",CultureInfo.InvariantCulture)));
    }
    private readonly ActorSoleSampler soles;
    private readonly bool left;
    private ActorSoleSampler.ContactPoint? anchor;
    private Vector3 origin,previous;
    private float started,path;
    private int window;
    public ActorFootContactTracker(ActorSoleSampler soles,bool left){this.soles=soles;this.left=left;}
    public Observation Sample(float time,bool grounded,Func<Vector3,float> groundAt)
    {
        var position=Vector3.zero;float height=float.NaN;
        if(anchor.HasValue)
        {
            if(!grounded||!soles.TryPosition(anchor.Value,out position))anchor=null;
            else
            {
                height=position.y-groundAt(position);
                if(float.IsNaN(height)||Mathf.Abs(height)>.03f)anchor=null;
            }
        }
        if(!anchor.HasValue&&grounded)
        {
            var candidate=soles.CaptureLowest(left);
            if(candidate.HasValue&&soles.TryPosition(candidate.Value,out position))
            {
                height=position.y-groundAt(position);
                if(!float.IsNaN(height)&&Mathf.Abs(height)<=.02f)
                {
                    anchor=candidate;origin=previous=position;started=time;path=0;window++;
                }
            }
        }
        if(!anchor.HasValue)return new Observation {position=new Vector3(float.NaN,float.NaN,float.NaN),height=float.NaN};
        path+=Vector3.ProjectOnPlane(position-previous,Vector3.up).magnitude;previous=position;
        return new Observation {window=window,vertex=anchor.Value.Id,position=position,duration=time-started,path=path,net=Vector3.ProjectOnPlane(position-origin,Vector3.up).magnitude,height=height};
    }
}
