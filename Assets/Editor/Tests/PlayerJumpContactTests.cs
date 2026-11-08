using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>UCC contact notifications must not consume a jump before actual flight.</summary>
public sealed class PlayerJumpContactTests
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    GameObject root;
    PlayerScriptedJumpController jump;
    void Set(string field,bool value)=>typeof(PlayerScriptedJumpController).GetField(field,Private).SetValue(jump,value);
    bool Get(string field)=>(bool)typeof(PlayerScriptedJumpController).GetField(field,Private).GetValue(jump);
    void Call(string method,object value)=>typeof(PlayerScriptedJumpController).GetMethod(method,Private).Invoke(jump,new[]{value});
    [SetUp] public void SetUp()
    {
        root=new GameObject("Jump contact event fixture");root.SetActive(false);
        jump=root.AddComponent<PlayerScriptedJumpController>();
        Set("jumpActive",true);
    }
    [TearDown] public void TearDown()=>Object.DestroyImmediate(root);

    [Test] public void LandDuringPreparationCannotCancelTakeoff()
    {
        Call("OnLanded",.6f);
        Assert.That(Get("landingRequested"),Is.False);
        Assert.That(jump.IsActive,Is.True);
    }
    [Test] public void ReacquiredContactDuringPreparationClearsStaleAirborneFlag()
    {
        Call("OnGroundedChanged",false);
        Assert.That(Get("leftGround"),Is.True);
        Call("OnGroundedChanged",true);
        Call("OnLanded",.6f);
        Assert.That(Get("leftGround"),Is.False);
        Assert.That(Get("landingRequested"),Is.False);
        Assert.That(jump.IsActive,Is.True);
    }
    [Test] public void ImpulseWaitingForTheMotorCannotLandBeforeFlight()
    {
        Set("takeoffImpulseApplied",true);
        Call("OnGroundedChanged",true);
        Call("OnLanded",.6f);
        Assert.That(Get("landingRequested"),Is.False);
    }
    [Test] public void ActualFlightAndContactStillRequestLanding()
    {
        Set("takeoffImpulseApplied",true);
        Call("OnGroundedChanged",false);
        Call("OnGroundedChanged",true);
        Call("OnLanded",2f);
        Assert.That(Get("landingRequested"),Is.True);
        Assert.That(jump.IsActive,Is.True);
    }
    [Test] public void PredictedLandingStillAcceptsItsContact()
    {
        Set("takeoffImpulseApplied",true);Set("landingRequested",true);
        Call("OnLanded",2f);
        Assert.That(Get("landingRequested"),Is.True);
        Assert.That((float)typeof(PlayerScriptedJumpController).GetField("landingContactStartedAt",Private).GetValue(jump),Is.GreaterThanOrEqualTo(0));
    }
    [Test] public void InactiveJumpCannotBeRevivedByALateLandWithStaleFlightFlag()
    {
        Set("jumpActive",false);Set("leftGround",true);Set("takeoffImpulseApplied",true);
        Call("OnLanded",2f);
        Assert.That(Get("landingRequested"),Is.False);
    }
    [Test] public void ResetRejectsLateContactNotifications()
    {
        typeof(PlayerScriptedJumpController).GetMethod("ResetPresentation",Private).Invoke(jump,null);
        Call("OnLanded",2f);
        Assert.That(jump.IsActive,Is.False);
        Assert.That(Get("landingRequested"),Is.False);
    }
}
