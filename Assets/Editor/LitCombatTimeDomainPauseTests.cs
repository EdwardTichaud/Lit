using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class LitCombatTimeDomainPauseTests
{
    private GameObject root;
    private CombatTimeDomain clock;
    private Animator animator;
    [SetUp] public void CreateClock()
    {
        root = new GameObject("Local pause test");
        root.SetActive(false);
        animator = root.AddComponent<Animator>();
        animator.speed = 2f;
        clock = root.AddComponent<CombatTimeDomain>();
    }
    [TearDown] public void Cleanup() => Object.DestroyImmediate(root);
    [Test] public void RemovingFlamePauseDoesNotRemoveAnotherOwner()
    {
        object flame = new object(), cinematic = new object();
        clock.SetIntrinsicPause(flame, true);
        clock.SetIntrinsicPause(cinematic, true);
        clock.SetIntrinsicPause(flame, false);
        Assert.That(clock.Scale, Is.Zero);
        Assert.That(animator.speed, Is.Zero);
        clock.SetIntrinsicPause(cinematic, false);
        Assert.That(clock.Scale, Is.EqualTo(1));
        Assert.That(animator.speed, Is.EqualTo(2));
    }
    [Test] public void ManagerPauseSurvivesFlameRemoval()
    {
        object flame = new object();
        clock.SetIntrinsicPause(flame, true);
        ApplyManagerScale(0f);
        clock.SetIntrinsicPause(flame, false);
        Assert.That(clock.Scale, Is.Zero);
        ApplyManagerScale(.5f);
        Assert.That(clock.Scale, Is.EqualTo(.5f));
        Assert.That(animator.speed, Is.EqualTo(1f));
    }
    [Test] public void RemovingManagerPauseDoesNotWakeDormantActor()
    {
        clock.SetIntrinsicPause(this, true);
        ApplyManagerScale(1f);
        Assert.That(clock.DeltaTime, Is.Zero);
        Assert.That(clock.FixedDeltaTime, Is.Zero);
        Assert.That(animator.speed, Is.Zero);
    }
    [Test] public void DuplicateNotificationsDoNotAccumulatePauseLeases()
    {
        clock.SetIntrinsicPause(this, true);
        clock.SetIntrinsicPause(this, true);
        clock.SetIntrinsicPause(this, false);
        Assert.That(clock.Scale, Is.EqualTo(1f));
    }
    [Test] public void PendingWaitAndLocalClockDoNotAdvanceWhilePaused()
    {
        clock.SetIntrinsicPause(this, true);
        var wait = clock.WaitForLocalSeconds(.1f);
        for (int frame = 0; frame < 120; frame++)
        {
            Assert.That(wait.MoveNext(), Is.True);
            typeof(CombatTimeDomain).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(clock, null);
        }
        Assert.That(clock.LocalTime, Is.Zero);
    }
    private void ApplyManagerScale(float value) => typeof(CombatTimeDomain)
        .GetMethod("ApplyManagerScale", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(clock, new object[] { value });
}
