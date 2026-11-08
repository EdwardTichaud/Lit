using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

/// <summary>Read-only flat-plane diagnostic of identical vertices remaining in adjacent sole patches.</summary>
public sealed class ActorContactPatchTracker
{
    public struct Observation
    {
        public int window,points,common;
        public float age,step,path,height,coveredTime,totalTime;
        public string Csv()=>window+","+points+","+common+","+string.Join(",",Array.ConvertAll(new[]{age,step,path,height,coveredTime,totalTime},v=>v.ToString("R",CultureInfo.InvariantCulture)));
    }
    readonly ActorSoleSampler soles;
    readonly ActorSoleSampler.ContactPoint[] points;
    readonly Vector3[] previous,current;
    readonly bool[] previousPatch,valid;
    readonly List<float> distances=new List<float>(64);
    bool active;
    int window;
    float started,lastTime,path,coveredTime,totalTime;
    public ActorContactPatchTracker(ActorSoleSampler soles,bool left)
    {
        this.soles=soles;points=soles.FootPoints(left).ToArray();
        previous=new Vector3[points.Length];current=new Vector3[points.Length];previousPatch=new bool[points.Length];valid=new bool[points.Length];
    }
    public Observation Sample(float time,bool grounded,float floor)
    {
        float minimum=float.PositiveInfinity;
        for(int i=0;i<points.Length;i++)
        {
            valid[i]=soles.TryPosition(points[i],out current[i]);
            if(valid[i])minimum=Mathf.Min(minimum,current[i].y);
        }
        float height=minimum-floor;
        bool contact=grounded&&!float.IsNaN(height)&&!float.IsInfinity(height)&&Mathf.Abs(height)<=.02f;
        if(!contact)
        {
            active=false;Array.Clear(previousPatch,0,previousPatch.Length);lastTime=time;
            return new Observation {height=height};
        }
        if(!active){active=true;window++;started=time;path=coveredTime=totalTime=0;Array.Clear(previousPatch,0,previousPatch.Length);}
        float delta=Mathf.Max(0,time-lastTime);distances.Clear();int count=0;
        for(int i=0;i<points.Length;i++)
        {
            bool member=valid[i]&&current[i].y<=minimum+.002f;
            if(member)
            {
                count++;
                if(previousPatch[i])distances.Add(Vector3.ProjectOnPlane(current[i]-previous[i],Vector3.up).magnitude);
            }
            previousPatch[i]=member;previous[i]=current[i];
        }
        float step=0;
        if(distances.Count>0)
        {
            distances.Sort();int n=distances.Count;step=n%2==1?distances[n/2]:(distances[n/2-1]+distances[n/2])*.5f;
            path+=step;coveredTime+=delta;
        }
        if(time>started)totalTime+=delta;
        lastTime=time;
        return new Observation {window=window,points=count,common=distances.Count,age=time-started,step=step,path=path,height=height,coveredTime=coveredTime,totalTime=totalTime};
    }
}
