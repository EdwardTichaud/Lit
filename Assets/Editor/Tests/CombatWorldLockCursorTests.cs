#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class CombatWorldLockCursorTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> owned = new List<GameObject>();
    private RealTimeCombatManager manager;
    private CombatWorldRuntime world;
    private GameObject prefab;

    [SetUp]
    public void SetUp()
    {
        Assert.That(Application.isPlaying, Is.False);
        Assert.That(RealTimeCombatManager.Instance, Is.Null, "Do not modify a live manager.");
        var host = Own("Lock world");
        host.SetActive(false);
        manager = host.AddComponent<RealTimeCombatManager>();
        world = host.AddComponent<CombatWorldRuntime>();
        prefab = Own("Authored world cursor");
        prefab.SetActive(false);
        prefab.transform.localScale = new Vector3(.4f, .6f, .8f);
        world.lockCursorPrefab = prefab;
        Assert.That(world.lockCursorOffset, Is.EqualTo(Vector3.up * 3f));
    }

    [TearDown]
    public void TearDown()
    {
        typeof(RealTimeCombatManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, null);
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null)
            {
                var indicator = owned[i].GetComponent<CombatLockIndicator>();
                if (indicator != null) Call(indicator, "OnDisable");
                Object.DestroyImmediate(owned[i]);
            }
        owned.Clear();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ExternalAndLocalWorldFacadeConfigureBeforeLockWithoutChangingTarget(bool worldFacade)
    {
        var target = new Target(Own("Target").transform);
        target.Root.localScale = new Vector3(2, 4, 6);
        target.Root.position = new Vector3(5, 7, 9);
        Select(target, worldFacade);
        Refresh();
        var indicator = target.Root.GetComponent<CombatLockIndicator>();
        var visual = Visual(indicator);
        AssertWorldVisual(indicator, visual, target.Root);
        for (int i = 0; i < 3; i++) Refresh();
        Assert.That(Visual(indicator), Is.SameAs(visual));
        Assert.That(manager.LockedCombatTarget, Is.SameAs(target));
        Assert.That(target.ActionSequenceId, Is.EqualTo(17));
        Assert.That(target.Mutations, Is.Zero);
        Assert.That(manager.IsCombatActive, Is.False, "Presentation must not engage combat.");
        Select(null, worldFacade);
        Refresh();
        Assert.That(visual.activeSelf, Is.False);
        Select(target, worldFacade);
        Refresh();
        Assert.That(Visual(indicator), Is.SameAs(visual));
        Assert.That(visual.activeSelf, Is.True);
    }

    [Test]
    public void LegacyPresentationUsesTheSameWorldConfigurationAndKeepsItsVisual()
    {
        // Publish only the existing manager reference; no network transport or gameplay host is started.
        typeof(RealTimeCombatManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, manager);
        var root = Own("Legacy target");
        root.SetActive(false);
        var enemy = root.AddComponent<EnemyController>();
        enemy.enabled = false;
        root.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
        root.SetActive(true);
        enemy.SetLockPresentation(true, false);
        var indicator = root.GetComponent<CombatLockIndicator>();
        var visual = Visual(indicator);
        AssertWorldVisual(indicator, visual, root.transform);
        enemy.SetLockPresentation(true, false);
        Assert.That(Visual(indicator), Is.SameAs(visual));
        enemy.SetLockPresentation(false, false);
        Assert.That(visual.activeSelf, Is.False);
        enemy.SetLockPresentation(true, false);
        Assert.That(Visual(indicator), Is.SameAs(visual));
        Assert.That(enemy.ActionSequenceId, Is.Zero);
        Assert.That(manager.LockedCombatTarget, Is.Null);
    }

    [Test]
    public void SamePrefabUpdatesWorldOffsetAndFollowsRootWithoutInheritedScale()
    {
        var indicator = Own("Scaled target").AddComponent<CombatLockIndicator>();
        indicator.transform.localScale = Vector3.one * 8f;
        world.ConfigureLockIndicator(indicator);
        indicator.SetLocked(true, false);
        var visual = Visual(indicator);
        world.lockCursorOffset = new Vector3(1, 3, 2);
        world.ConfigureLockIndicator(indicator);
        Assert.That(Visual(indicator), Is.SameAs(visual));
        Assert.That(visual.activeSelf, Is.True);
        Assert.That(visual.transform.position, Is.EqualTo(indicator.transform.position + world.lockCursorOffset));
        indicator.transform.position = new Vector3(10, 20, 30);
        Call(indicator, "LateUpdate");
        Assert.That(visual.transform.position, Is.EqualTo(indicator.transform.position + world.lockCursorOffset));
        Assert.That(visual.transform.parent, Is.Null);
    }

    [Test]
    public void PrefabReplacementKeepsLockButRetiresThePreviousVisualGeneration()
    {
        var indicator = Own("Target").AddComponent<CombatLockIndicator>();
        world.ConfigureLockIndicator(indicator);
        indicator.SetLocked(true, false);
        var first = Visual(indicator);
        world.lockCursorPrefab = Own("Replacement cursor");
        world.ConfigureLockIndicator(indicator);
        var second = Visual(indicator);
        Assert.That(first == null, Is.True);
        Assert.That(second.activeSelf, Is.True);
        world.ConfigureLockIndicator(indicator);
        Assert.That(Visual(indicator), Is.SameAs(second));
        Assert.That(Read<Canvas>(indicator, "cursorCanvas"), Is.Null);
    }

    [Test]
    public void OwnerDisableReleasesVisualAndNextLockCreatesOnlyANewGeneration()
    {
        var root = Own("Target");
        var indicator = root.AddComponent<CombatLockIndicator>();
        world.ConfigureLockIndicator(indicator);
        indicator.SetLocked(true, false);
        var first = Visual(indicator);
        // Invoke the Unity callback directly so this remains a deterministic Edit Mode contract test.
        Call(indicator, "OnDisable");
        Assert.That(first == null, Is.True);
        Assert.That(Visual(indicator), Is.Null);
        indicator.SetLocked(true, false);
        var second = Visual(indicator);
        AssertWorldVisual(indicator, second, root.transform);
        Call(indicator, "OnDisable");
        Call(indicator, "OnDestroy");
        Assert.That(second == null, Is.True);
        Assert.That(Visual(indicator), Is.Null);
    }

    [Test]
    public void DisabledOwnerRejectsALateLockRefreshWithoutCreatingAnotherVisual()
    {
        var indicator = Own("Disabled target").AddComponent<CombatLockIndicator>();
        world.ConfigureLockIndicator(indicator);
        indicator.SetLocked(true, false);
        var visual = Visual(indicator);
        indicator.enabled = false;
        Call(indicator, "OnDisable");
        Assert.That(visual == null, Is.True);
        indicator.SetLocked(true, false);
        Assert.That(Visual(indicator), Is.Null);
        indicator.enabled = true;
        indicator.SetLocked(true, false);
        AssertWorldVisual(indicator, Visual(indicator), indicator.transform);
    }

    [Test]
    public void OwnerDestroyReleasesItsUnparentedVisual()
    {
        var root = Own("Target");
        var indicator = root.AddComponent<CombatLockIndicator>();
        world.ConfigureLockIndicator(indicator);
        indicator.SetLocked(true, false);
        var visual = Visual(indicator);
        // Edit Mode does not dispatch this non-ExecuteAlways component's destruction callback.
        Call(indicator, "OnDestroy");
        Object.DestroyImmediate(root);
        Assert.That(visual == null, Is.True);
    }

    [Test]
    public void MissingVisualIsRecreatedActiveWithoutReplacingTheLock()
    {
        var target = new Target(Own("Target").transform);
        Select(target, true);
        Refresh();
        var indicator = target.Root.GetComponent<CombatLockIndicator>();
        Object.DestroyImmediate(Visual(indicator));
        Refresh();
        AssertWorldVisual(indicator, Visual(indicator), target.Root);
        Assert.That(manager.LockedCombatTarget, Is.SameAs(target));
        Assert.That(target.ActionSequenceId, Is.EqualTo(17));
    }

    [Test]
    public void CursorBelongsToOwnerSceneEvenWhenAnotherSceneIsActive()
    {
        var previous = SceneManager.GetActiveScene();
        const string path = "Assets/Scenes/Bootstrap.unity";
        Assert.That(SceneManager.GetSceneByPath(path).isLoaded, Is.False, "Run this fixture without the author's Bootstrap open.");
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(previous);
        GameObject visual = null;
        try
        {
            var root = Own("Additive target");
            SceneManager.MoveGameObjectToScene(root, scene);
            var indicator = root.AddComponent<CombatLockIndicator>();
            world.ConfigureLockIndicator(indicator);
            indicator.SetLocked(true, false);
            visual = Visual(indicator);
            Assert.That(visual.scene, Is.EqualTo(scene));
            Assert.That(visual.transform.parent, Is.Null);
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
        Assert.That(visual == null, Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TargetSwitchOrDeathHidesPreviousCursorWithoutChangingGeneration(bool death)
    {
        var first = new Target(Own("First target").transform);
        Select(first, true);
        Refresh();
        var visual = Visual(first.Root.GetComponent<CombatLockIndicator>());
        if (death) first.IsDead = true;
        else Select(new Target(Own("Second target").transform), true);
        Refresh();
        Assert.That(visual.activeSelf, Is.False);
        Assert.That(first.ActionSequenceId, Is.EqualTo(17));
        Assert.That(first.Mutations, Is.Zero);
    }

    [Test]
    public void UnconfiguredWorldPreservesLegacyFallbackAndReleasesAllOwnedResources()
    {
        world.lockCursorPrefab = null;
        var indicator = Own("Old scene target").AddComponent<CombatLockIndicator>();
        world.ConfigureLockIndicator(indicator);
        indicator.SetLocked(true, false);
        var canvas = Read<Canvas>(indicator, "cursorCanvas");
        var sprite = Read<Sprite>(indicator, "cursorSprite");
        var texture = Read<Texture2D>(indicator, "cursorTexture");
        Assert.That(canvas, Is.Not.Null);
        Assert.That(canvas.gameObject.scene, Is.EqualTo(indicator.gameObject.scene));
        Call(indicator, "OnDisable");
        Assert.That(canvas == null && sprite == null && texture == null, Is.True);
        Assert.That(Visual(indicator), Is.Null);
    }

    [Test]
    public void ConfiguringALockedFallbackRemovesUiAndPreservesLock()
    {
        var indicator = Own("Old target").AddComponent<CombatLockIndicator>();
        indicator.SetLocked(true, false);
        var canvas = Read<Canvas>(indicator, "cursorCanvas");
        var sprite = Read<Sprite>(indicator, "cursorSprite");
        var texture = Read<Texture2D>(indicator, "cursorTexture");
        world.ConfigureLockIndicator(indicator);
        Assert.That(canvas == null && sprite == null && texture == null, Is.True);
        AssertWorldVisual(indicator, Visual(indicator), indicator.transform);
    }

    [Test]
    public void UnconfiguredWorldDoesNotOverrideAuthoredVisualOrExplicitLabConfiguration()
    {
        world.lockCursorPrefab = null;
        var indicator = Own("Authored target").AddComponent<CombatLockIndicator>();
        var authored = Own("Authored visual");
        Set(indicator, "visualRoot", authored);
        Set(indicator, "initialScale", Vector3.one);
        world.ConfigureLockIndicator(indicator);
        indicator.SetLocked(true, false);
        Assert.That(Visual(indicator), Is.SameAs(authored));
        Call(indicator, "OnDisable");
        Assert.That(authored != null, Is.True);
        Assert.That(authored.activeSelf, Is.False);
        indicator.ConfigureWorldCursor(prefab, new Vector3(0, 4, 0));
        world.ConfigureLockIndicator(indicator);
        indicator.SetLocked(true, false);
        Assert.That(Visual(indicator).transform.position, Is.EqualTo(indicator.transform.position + new Vector3(0, 4, 0)));
        Assert.That(Read<Canvas>(indicator, "cursorCanvas"), Is.Null);
    }

    private void AssertWorldVisual(CombatLockIndicator indicator, GameObject visual, Transform target)
    {
        Assert.That(visual, Is.Not.Null);
        Assert.That(visual.activeSelf, Is.True);
        Assert.That(visual.transform.parent, Is.Null);
        Assert.That(visual.transform.position, Is.EqualTo(target.position + world.lockCursorOffset));
        Assert.That(visual.transform.localScale, Is.EqualTo(world.lockCursorPrefab.transform.localScale));
        Assert.That(Read<Canvas>(indicator, "cursorCanvas"), Is.Null);
        Assert.That(visual.GetComponentInChildren<Canvas>(true), Is.Null);
        Assert.That(Read<Sprite>(indicator, "cursorSprite"), Is.Null);
        Assert.That(Read<Texture2D>(indicator, "cursorTexture"), Is.Null);
    }

    private GameObject Own(string name)
    {
        var root = new GameObject(name);
        owned.Add(root);
        return root;
    }

    private void Select(ICombatTarget target, bool worldFacade)
    {
        Set(manager, "worldLockedTarget", worldFacade ? target : null);
        Set(manager, "externalTarget", worldFacade ? null : target);
        Set(manager, "externalTargetLocked", !worldFacade && target != null);
    }

    private void Refresh() => Call(manager, "RefreshExternalLockIndicator");
    private static GameObject Visual(CombatLockIndicator indicator) => Read<GameObject>(indicator, "visualRoot");
    private static T Read<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);
    private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);
    private static void Call(object owner, string name) => owner.GetType().GetMethod(name, Private).Invoke(owner, null);

    private sealed class Target : ICombatTarget
    {
        public Transform Root { get; }
        public Transform LockPoint => Root;
        public Animator Animator => null;
        public bool IsDead { get; set; }
        public bool IsAttackCommitted => false;
        public int ActionSequenceId => 17;
        public int Mutations { get; private set; }
        public Target(Transform root) { Root = root; }
        public int ReceiveDamage(int damage) { Mutations++; return damage; }
        public void SetCinematicSuspended(bool suspended) { Mutations++; }
        public bool PlaceForCinematic(Vector3 position, Quaternion rotation) { Mutations++; return false; }
    }
}
#endif
