using UnityEngine;

/// <summary>Bounded blend-parameter response. Multiple writers in one frame cannot consume time twice.</summary>
public struct LocomotionPresentationResponse
{
    private bool initialized;
    private int frame;
    private Vector2 direction, startDirection;
    private float blend, startBlend;

    public void Reset() => initialized=false;
    public void Evaluate(int currentFrame,float delta,Vector2 targetDirection,float targetBlend,
        float degreesPerSecond,float blendUnitsPerSecond,out Vector2 resultDirection,out float resultBlend)
    {
        if(!initialized||currentFrame<frame||currentFrame>frame+1)
        {
            initialized=true;frame=currentFrame;
            direction=startDirection=targetDirection;blend=startBlend=targetBlend;
        }
        else if(currentFrame!=frame)
        {
            frame=currentFrame;startDirection=direction;startBlend=blend;
        }
        delta=Mathf.Max(0,delta);
        float angle=Vector2.SignedAngle(startDirection,targetDirection);
        float step=degreesPerSecond>0?Mathf.Clamp(angle,-degreesPerSecond*delta,degreesPerSecond*delta):angle;
        float radians=step*Mathf.Deg2Rad;
        direction=new Vector2(startDirection.x*Mathf.Cos(radians)-startDirection.y*Mathf.Sin(radians),
            startDirection.x*Mathf.Sin(radians)+startDirection.y*Mathf.Cos(radians));
        blend=blendUnitsPerSecond>0?Mathf.MoveTowards(startBlend,targetBlend,blendUnitsPerSecond*delta):targetBlend;
        resultDirection=direction;resultBlend=blend;
    }
}
