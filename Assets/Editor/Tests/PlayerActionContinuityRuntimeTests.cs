using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class PlayerActionContinuityRuntimeTests
{
    private GameObject root;
    private AnimatorController controller;
    private AnimationClip clip;

    [UnityTest]
    public IEnumerator MissingEndEventInterruptionPauseAndDisabledAnimatorRecover()
    {
        bool reload = SessionState.GetBool("PlayerInPlace.Test.Reload", false);
        SessionState.SetBool("ActionContinuity.RestoreOptions", true);
        SessionState.SetBool("ActionContinuity.OptionsEnabled", EditorSettings.enterPlayModeOptionsEnabled);
        SessionState.SetInt("ActionContinuity.Options", (int)EditorSettings.enterPlayModeOptions);
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = reload ? EnterPlayModeOptions.None : EnterPlayModeOptions.DisableDomainReload;
        yield return new EnterPlayMode(reload);

        root = new GameObject("Action continuity runtime fixture");
        controller = new AnimatorController();
        controller.AddLayer("Base Layer");
        clip = new AnimationClip { name = "Action without end event" };
        clip.SetCurve("", typeof(Transform), "m_LocalScale.x", AnimationCurve.Constant(0, 2, 1));
        foreach (var stateName in new[] { "Locomotion", "Attack", "Hurt" })
            controller.layers[0].stateMachine.AddState(stateName).motion = clip;
        var animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var presentation = root.AddComponent<PlayerActionPresentationController>();
        presentation.ResolveReferences(animator, null);
        var profile = PlayerActionPresentationProfile.CreateDefault();
        profile.recoveryNormalizedTime = 0.2f;
        profile.chainNormalizedTime = 0.1f;
        profile.allowMoveAfterRecovery = false;
        int cleaned = 0;
        Assert.That(presentation.TryPlayCombatState("Attack", profile, "No end event"), Is.True);
        presentation.RegisterActionCleanup(presentation.ActionGeneration, () => cleaned++);
        yield return new WaitForSecondsRealtime(0.8f);
        Assert.That(presentation.IsActionActive, Is.False);
        Assert.That(cleaned, Is.EqualTo(1));

        profile.recoveryNormalizedTime = 1f;
        for (int i = 0; i < 4; i++)
        {
            Assert.That(presentation.TryPlayCombatState("Attack", profile, "Interrupted"), Is.True);
            int generation = presentation.ActionGeneration;
            yield return new WaitForSecondsRealtime(0.15f);
            LogAssert.Expect(LogType.Warning, new Regex("\\[PlayerAction\\] Recovery.*Animator state replaced"));
            animator.CrossFade("Hurt", .05f, 0, 0f);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(presentation.IsCurrentSession(generation), Is.False);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Hurt") ||
                animator.GetNextAnimatorStateInfo(0).IsName("Hurt"), Is.True);
        }

        Assert.That(presentation.TryPlayCombatState("Attack", profile, "Disabled Animator"), Is.True);
        yield return new WaitForSecondsRealtime(.15f);
        LogAssert.Expect(LogType.Warning, new Regex("\\[PlayerAction\\] Recovery.*Animator disabled"));
        animator.enabled = false;
        yield return null;
        yield return null;
        Assert.That(presentation.IsActionActive, Is.False);
        animator.enabled = true;

        Assert.That(presentation.TryPlayCombatState("Attack", profile, "Paused then stalled"), Is.True);
        yield return new WaitForSecondsRealtime(.15f);
        animator.speed = 0;
        var time = TimeManager.EnsureInstance();
        var pause = time.AcquireGlobalPause(root);
        yield return new WaitForSecondsRealtime(1.2f);
        Assert.That(presentation.IsActionActive, Is.True, "An authorized pause must not expire the watchdog.");
        LogAssert.Expect(LogType.Warning, new Regex("\\[PlayerAction\\] Recovery.*No local animation progress"));
        time.Release(pause);
        yield return new WaitForSecondsRealtime(1.3f);
        Assert.That(presentation.IsActionActive, Is.False);
        animator.speed = 1;

        var owner = new GameObject("Disabled session owner");
        owner.transform.SetParent(root.transform);
        int external = presentation.BeginExternalPresentation(owner);
        presentation.RegisterActionCleanup(external, () => cleaned++);
        owner.SetActive(false);
        yield return null;
        yield return null;
        Assert.That(presentation.IsCurrentSession(external), Is.False);
        Assert.That(cleaned, Is.EqualTo(2));
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (root != null)
        {
            TimeManager.Instance?.ReleaseOwner(root);
            Object.DestroyImmediate(root);
        }
        if (controller != null) Object.DestroyImmediate(controller);
        if (clip != null) Object.DestroyImmediate(clip);
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
        if (SessionState.GetBool("ActionContinuity.RestoreOptions", false))
        {
            EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool("ActionContinuity.OptionsEnabled", false);
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt("ActionContinuity.Options", 0);
            SessionState.SetBool("ActionContinuity.RestoreOptions", false);
        }
    }
}
