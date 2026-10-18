using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;

public sealed class MultiplayerCombatTimeTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private GameObject root, networkRoot, actor;
    private TimeManager time;
    private NetworkManager network;
    private CombatTimeDomain domain;
    private float originalScale, originalFixedDelta;

    [SetUp]
    public void SetUp()
    {
        originalScale = Time.timeScale;
        originalFixedDelta = Time.fixedDeltaTime;
        Assert.That(originalScale, Is.EqualTo(1f), "Run from normal simulation time; TimeManager owns restoration.");
        Assert.That(TimeManager.Instance, Is.Null, "Run in an empty Edit Mode scene.");
        Assert.That(NetworkManager.Singleton, Is.Null, "Do not replace a live session.");
        root = new GameObject("Multiplayer time fixture");
        root.SetActive(false);
        time = root.AddComponent<TimeManager>();
        Set(time, "slowMotionPresentation", null);
        Invoke(time, "Awake");
        networkRoot = new GameObject("Inactive NGO time fixture");
        networkRoot.SetActive(false);
        network = networkRoot.AddComponent<NetworkManager>();
        typeof(NetworkManager).GetProperty("Singleton").SetValue(null, network);
        actor = new GameObject("Inactive time domain fixture");
        actor.SetActive(false);
        domain = actor.AddComponent<CombatTimeDomain>();
        time.RegisterDomain(domain);
    }

    [TearDown]
    public void TearDown()
    {
        if (network != null) SetListening(false);
        if (time != null) Invoke(time, "OnDestroy");
        if (actor != null) Object.DestroyImmediate(actor);
        if (root != null) Object.DestroyImmediate(root);
        if (network != null && NetworkManager.Singleton == network)
            typeof(NetworkManager).GetProperty("Singleton").SetValue(null, null);
        if (networkRoot != null) Object.DestroyImmediate(networkRoot);
        Assert.That(Time.timeScale, Is.EqualTo(originalScale));
        Assert.That(Time.fixedDeltaTime, Is.EqualTo(originalFixedDelta).Within(.000001f));
    }

    [Test]
    public void NonListeningManagerKeepsSoloSlowestOwnerAndPausePolicy()
    {
        object slowOwner = new object(), menuOwner = new object();
        var slow = time.AcquireGlobal(.4f, slowOwner);
        var local = time.AcquireLocal(domain, .5f, slowOwner);
        var pause = time.AcquireGlobalPause(menuOwner);
        Assert.That(slow.IsValid && local.IsValid && pause.IsValid, Is.True);
        Assert.That(time.GlobalScale, Is.Zero);
        time.Release(pause);
        Assert.That(time.GlobalScale, Is.EqualTo(.4f));
        Assert.That(Time.fixedDeltaTime, Is.EqualTo(originalFixedDelta * .4f).Within(.000001f));
        Assert.That(domain.Scale, Is.EqualTo(.5f));
        time.ReleaseOwner(slowOwner);
        AssertNormalTime();
    }

    [Test]
    public void ListeningSessionRejectsGlobalLocalAndMenuSimulationLeases()
    {
        SetListening(true);
        Assert.That(time.AcquireGlobal(.4f, this).IsValid, Is.False);
        Assert.That(time.AcquireGlobalPause(this).IsValid, Is.False);
        Assert.That(time.AcquireLocal(domain, .4f, this).IsValid, Is.False);
        Assert.That(time.AcquireLocalPause(domain, this).IsValid, Is.False);
        AssertNormalTime();
    }

    [Test]
    public void JoiningClearsExistingPausesAndDisconnectDoesNotResurrectLeases()
    {
        var global = time.AcquireGlobalPause(this);
        var local = time.AcquireLocalPause(domain, this);
        SetListening(true);
        Invoke(time, "Update");
        AssertNormalTime();
        SetListening(false);
        Invoke(time, "Update");
        AssertNormalTime();
        time.Release(global);
        time.Release(local);
        AssertNormalTime();
        Assert.That(time.AcquireGlobal(.4f, this).IsValid, Is.True);
        Assert.That(time.GlobalScale, Is.EqualTo(.4f));
    }

    [Test]
    public void JoiningAtAcquireClearsSoloLeasesBeforeTheNextUpdate()
    {
        time.AcquireGlobalPause(this);
        time.AcquireLocalPause(domain, this);
        SetListening(true);
        Assert.That(time.AcquireGlobal(.4f, this).IsValid, Is.False);
        AssertNormalTime();
    }

    [Test]
    public void OnlineIgnoresAuthoredBaseSlowMotionButRestoresItOffline()
    {
        Set(time, "baseGlobalScale", .6f);
        SetListening(true);
        Invoke(time, "Update");
        AssertNormalTime();
        SetListening(false);
        Invoke(time, "Update");
        Assert.That(time.GlobalScale, Is.EqualTo(.6f));
    }

    [Test]
    public void GatingDoesNotRemoveIntrinsicGameplayStops()
    {
        domain.SetIntrinsicPause(this, true);
        SetListening(true);
        Invoke(time, "Update");
        Assert.That(domain.Scale, Is.Zero, "Gameplay state such as dormancy is not a time-effect lease.");
        domain.SetIntrinsicPause(this, false);
        AssertNormalTime();
    }

    [TestCase(false, .4, 1, .2)]
    [TestCase(true, .4, 1, .5)]
    [TestCase(true, .4, .3, .3)]
    [TestCase(true, 1, 1, .2)]
    public void CounterCompensatesOnlyOnlineAndIsBoundedByAttack(bool online, double soloScale, double impact, double expected)
    {
        double duration = EnemyReactionTiming.CounterWindowSeconds(.2, soloScale, impact, online);
        Assert.That(duration, Is.EqualTo(expected).Within(.000001));
        Assert.That(EnemyReactionTiming.Contains(impact - duration, impact, duration), Is.True);
        Assert.That(EnemyReactionTiming.Contains(impact - duration - .00001, impact, duration), Is.False);
        Assert.That(EnemyReactionTiming.Contains(impact, impact, duration), Is.False);
    }

    [TestCase(30)] [TestCase(60)] [TestCase(120)]
    public void OnlineCompensationDoesNotRescaleInputProjection(int fps)
    {
        double phase = EnemyReactionTiming.ProjectPhase(.75 - 1d / fps, 1d / fps, 1, .005, 1);
        Assert.That(phase, Is.EqualTo(.745).Within(.000001));
        Assert.That(EnemyReactionTiming.Contains(phase, 1,
            EnemyReactionTiming.CounterWindowSeconds(.2, .4, 1, true)), Is.True);
        Assert.That(EnemyReactionTiming.Contains(phase, 1,
            EnemyReactionTiming.CounterWindowSeconds(.2, .4, 1, false)), Is.False);
    }

    [Test]
    public void InvalidCompensationCannotOpenAWindow()
    {
        Assert.That(EnemyReactionTiming.CounterWindowSeconds(.2, 0, 1, true), Is.Zero);
        Assert.That(EnemyReactionTiming.CounterWindowSeconds(double.NaN, .4, 1, true), Is.Zero);
        Assert.That(EnemyReactionTiming.CounterWindowSeconds(.2, .4, double.PositiveInfinity, true), Is.Zero);
    }

    private void AssertNormalTime()
    {
        Assert.That(time.GlobalScale, Is.EqualTo(1f));
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(Time.fixedDeltaTime, Is.EqualTo(originalFixedDelta).Within(.000001f));
        Assert.That(domain.Scale, Is.EqualTo(1f));
    }

    private void SetListening(bool value) => typeof(NetworkManager).GetProperty("IsListening").SetValue(network, value);
    private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Flags).SetValue(owner, value);
    private static void Invoke(object owner, string method) => owner.GetType().GetMethod(method, Flags).Invoke(owner, null);
}
