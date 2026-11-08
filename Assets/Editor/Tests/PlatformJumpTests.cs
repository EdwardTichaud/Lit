using NUnit.Framework;

public sealed class PlatformJumpTests
{
    [Test]
    public void CoyoteIsShortAndCannotGiveASecondJump()
    {
        var window = new JumpRequestWindow();
        window.Tick(0, true, true, false, 0);
        window.Tick(.08f, false, true, false, 0);
        window.Request(.15f);
        Assert.That(window.CanConsume(false, .1f), Is.True);
        window.Consume();
        window.Request(.15f);
        Assert.That(window.CanConsume(false, .1f), Is.False);
    }

    [Test]
    public void WalkingOffALedgeDoesNotAllowAnUnlimitedAirJump()
    {
        var window = new JumpRequestWindow();
        window.Tick(0, true, true, false, 0);
        window.Tick(.11f, false, true, false, 0);
        window.Request(.15f);
        Assert.That(window.CanConsume(false, .1f), Is.False);
    }

    [Test]
    public void EarlyInputSurvivesContactAndExistingLandingRecoveryOnce()
    {
        var window = new JumpRequestWindow();
        window.Request(.15f);
        window.Tick(.1f, false, false, false, .8f);
        window.Tick(.02f, true, false, true, .8f);
        window.Tick(.55f, true, false, true, .8f);
        Assert.That(window.CanConsume(true, .1f), Is.True);
        window.Consume();
        Assert.That(window.CanConsume(true, .1f), Is.False);
    }

    [Test]
    public void ExpiredRequestCannotBeRevivedByContact()
    {
        var window = new JumpRequestWindow();
        window.Request(.15f);
        window.Tick(.16f, false, false, false, .8f);
        window.Tick(.02f, true, false, true, .8f);
        Assert.That(window.HasRequest, Is.False);
    }

    [Test]
    public void LandingReservationIsBoundedAndResetCancelsEverything()
    {
        var window = new JumpRequestWindow();
        window.Request(.15f);
        window.Tick(.01f, true, false, true, .8f);
        window.Tick(.81f, true, false, true, .8f);
        Assert.That(window.HasRequest, Is.False);
        window.Tick(0, true, true, false, 0);
        window.Request(.15f);
        window.Reset();
        Assert.That(window.HasRequest, Is.False);
        window.Request(.15f);
        Assert.That(window.CanConsume(false, .1f), Is.False);
    }

    [Test]
    public void LocalPauseDoesNotConsumeTheInputWindow()
    {
        var window = new JumpRequestWindow();
        window.Request(.15f);
        window.Tick(0, false, false, false, 0);
        Assert.That(window.HasRequest, Is.True);
    }

    [Test]
    public void LateContactInALongFrameCannotExtendAnExpiredBuffer()
    {
        var window = new JumpRequestWindow();
        window.Request(.15f);
        window.Tick(.2f, true, false, true, .8f);
        Assert.That(window.HasRequest, Is.False);
    }

    [TestCase("Assets/Characters/1_Squad/Lucian/Lucian.asset")]
    [TestCase("Assets/Characters/3_Enemy/Iluvilirae/Lucian_ImpactLab.asset")]
    public void LucianUsesResponsiveTakeoffWithoutChangingHisArc(string path)
    {
        var data = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterData>(path);
        Assert.That(data, Is.Not.Null);
        Assert.That(data.playerSettings.jump.jumpHeight, Is.EqualTo(100));
        Assert.That(data.playerSettings.jump.jumpStartTakeoffNormalizedTime, Is.EqualTo(.13f));
        Assert.That(data.playerSettings.jump.coyoteSeconds, Is.EqualTo(.1f));
        Assert.That(data.playerSettings.jump.inputBufferSeconds, Is.EqualTo(.15f));
    }
}

[UnityEditor.InitializeOnLoad]
public sealed class PlatformJumpTestRunner : UnityEditor.TestTools.TestRunner.Api.ICallbacks
{
    private const string Request = "Library/PlatformJumpValidation/Tests.request";
    private static UnityEditor.TestTools.TestRunner.Api.TestRunnerApi api;
    static PlatformJumpTestRunner() => UnityEditor.EditorApplication.update += Poll;
    private static void Poll()
    {
        if (UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating ||
            UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode || api != null || !System.IO.File.Exists(Request)) return;
        System.IO.File.Delete(Request);
        api = UnityEngine.ScriptableObject.CreateInstance<UnityEditor.TestTools.TestRunner.Api.TestRunnerApi>();
        api.RegisterCallbacks(new PlatformJumpTestRunner());
        api.Execute(new UnityEditor.TestTools.TestRunner.Api.ExecutionSettings(new UnityEditor.TestTools.TestRunner.Api.Filter {
            testMode = UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode, groupNames = new[] { "^PlatformJumpTests" }
        }) { runSynchronously = true });
    }
    public void RunStarted(UnityEditor.TestTools.TestRunner.Api.ITestAdaptor test) { }
    public void TestStarted(UnityEditor.TestTools.TestRunner.Api.ITestAdaptor test) { }
    public void TestFinished(UnityEditor.TestTools.TestRunner.Api.ITestResultAdaptor result) { }
    public void RunFinished(UnityEditor.TestTools.TestRunner.Api.ITestResultAdaptor result)
    {
        UnityEditor.TestTools.TestRunner.Api.TestRunnerApi.SaveResultToFile(result, "Library/PlatformJumpValidation/Tests.xml");
        api.UnregisterCallbacks(this);
        UnityEngine.Object.DestroyImmediate(api);
        api = null;
    }
}
