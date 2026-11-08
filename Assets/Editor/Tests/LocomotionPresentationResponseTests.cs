using NUnit.Framework;
using UnityEngine;

public sealed class LocomotionPresentationResponseTests
{
    LocomotionPresentationResponse response;
    [SetUp] public void SetUp()
    {
        response=new LocomotionPresentationResponse();
        response.Evaluate(0,.02f,Vector2.up,1,360,10,out _,out _);
    }
    [Test] public void AbruptDirectionAndGaitChangesAreBounded()
    {
        response.Evaluate(1,.02f,Vector2.right,3,360,10,out var direction,out var blend);
        Assert.That(Vector2.Angle(Vector2.up,direction),Is.EqualTo(7.2f).Within(.001f));
        Assert.That(direction.magnitude,Is.EqualTo(1).Within(.00001f));
        Assert.That(blend,Is.EqualTo(1.2f).Within(.00001f));
    }
    [Test] public void RefreshesInOneFrameDoNotApplyTimeTwice()
    {
        response.Evaluate(1,.02f,Vector2.right,3,360,10,out var expected,out var expectedBlend);
        for(int i=0;i<10;i++)
        {
            response.Evaluate(1,.02f,Vector2.right,3,360,10,out var direction,out var blend);
            Assert.That(Vector2.Distance(direction,expected),Is.LessThan(.000001f));
            Assert.That(blend,Is.EqualTo(expectedBlend));
        }
    }
    [Test] public void LatestTargetWinsWithoutExtraTimeInSameFrame()
    {
        response.Evaluate(1,.02f,Vector2.right,3,360,10,out _,out _);
        response.Evaluate(1,.02f,Vector2.left,0,360,10,out var direction,out var blend);
        Assert.That(direction.x,Is.LessThan(0));
        Assert.That(Vector2.Angle(Vector2.up,direction),Is.EqualTo(7.2f).Within(.001f));
        Assert.That(blend,Is.EqualTo(.8f).Within(.00001f));
    }
    [Test] public void ActorPauseKeepsThePresentedPose()
    {
        response.Evaluate(1,0,Vector2.right,3,360,10,out var direction,out var blend);
        Assert.That(direction,Is.EqualTo(Vector2.up));Assert.That(blend,Is.EqualTo(1));
    }
    [Test] public void ReturningAfterAnActionDoesNotUseAStaleDirection()
    {
        response.Evaluate(20,.02f,Vector2.down,3,360,10,out var direction,out var blend);
        Assert.That(direction,Is.EqualTo(Vector2.down));Assert.That(blend,Is.EqualTo(3));
    }
    [Test] public void DisabledResponsePreservesImmediateBehaviour()
    {
        response.Evaluate(1,.02f,Vector2.down,3,0,0,out var direction,out var blend);
        Assert.That(Vector2.Distance(direction,Vector2.down),Is.LessThan(.000001f));Assert.That(blend,Is.EqualTo(3));
    }
    [Test] public void ActorTimeScalingDoesNotChangeTheResultAtEqualActorTime()
    {
        var slow=response;Vector2 a=default,b=default;float ga=0,gb=0;
        for(int i=1;i<=5;i++)response.Evaluate(i,.02f,Vector2.right,3,360,10,out a,out ga);
        for(int i=1;i<=10;i++)slow.Evaluate(i,.01f,Vector2.right,3,360,10,out b,out gb);
        Assert.That(Vector2.Distance(a,b),Is.LessThan(.000001f));Assert.That(ga,Is.EqualTo(gb).Within(.00001f));
    }
}
