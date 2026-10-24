#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor.Animations;
using UnityEngine;

public sealed class CombatEnemyNetworkAuthoringTests
{
    private GameObject root;
    private AnimatorController controller;
    private Animator animator;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Unspawned enemy authoring fixture");
        root.SetActive(false);
        root.AddComponent<NetworkObject>();
        animator = root.AddComponent<Animator>();
        controller = new AnimatorController();
        animator.runtimeAnimatorController = controller;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(controller);
    }

    [Test]
    public void InstallsOnlyServerPresentationBeforeSpawnAndIsIdempotent()
    {
        Assert.That(CombatEnemyNetworkAuthoring.Validate(root, animator), Is.Null);
        Assert.That(CombatEnemyNetworkAuthoring.Ensure(root, animator), Is.True);
        var animation = root.GetComponent<NetworkAnimator>();
        var movement = root.GetComponent<NetworkTransform>();
        Assert.That(animation.Animator, Is.SameAs(animator));
        Assert.That(animation.IsServerAuthoritative(), Is.True);
        Assert.That(movement.IsServerAuthoritative(), Is.True);
        Assert.That(movement.SwitchTransformSpaceWhenParented, Is.True);
        Assert.That(CombatEnemyNetworkAuthoring.Ensure(root, animator), Is.False);
        Assert.That(root.GetComponents<NetworkAnimator>().Length, Is.EqualTo(1));
        Assert.That(root.GetComponents<NetworkTransform>().Length, Is.EqualTo(1));
    }

    [Test]
    public void DoesNotReplaceAnExistingBindingToAnotherAnimator()
    {
        var other = new GameObject("Authored other Animator");
        other.transform.SetParent(root.transform);
        var network = root.AddComponent<NetworkAnimator>();
        network.Animator = other.AddComponent<Animator>();
        Assert.That(CombatEnemyNetworkAuthoring.Validate(root, animator), Does.Contain("different"));
        Assert.Throws<System.InvalidOperationException>(() => CombatEnemyNetworkAuthoring.Ensure(root, animator));
        Assert.That(network.Animator.gameObject, Is.SameAs(other));
    }

    [Test]
    public void PlayerSpawnDependenciesAreAuthoredOnceWithoutReplacingTheActor()
    {
        var actor = root.AddComponent<SquadCharacterController>();
        Assert.That(CombatPlayerNetworkAuthoring.Validate(root), Is.Null);
        Assert.That(CombatPlayerNetworkAuthoring.Ensure(root), Is.True);
        Assert.That(CombatPlayerNetworkAuthoring.NeedsChanges(root), Is.False);
        Assert.That(CombatPlayerNetworkAuthoring.Ensure(root), Is.False);
        Assert.That(root.GetComponent<SquadCharacterController>(), Is.SameAs(actor));
        Assert.That(root.GetComponents<NetworkCharacterInput>().Length, Is.EqualTo(1));
        Assert.That(root.GetComponents<NetworkInventory>().Length, Is.EqualTo(1));
        Assert.That(root.GetComponents<NetcodeCharacterIdentity>().Length, Is.EqualTo(1));
        Assert.That(root.GetComponents<NetcodeLocalPlayer>().Length, Is.EqualTo(1));
        Assert.That(root.GetComponents<PersistentNetworkObject>().Length, Is.EqualTo(1));
        Assert.That(root.GetComponent<NetworkTransform>().IsServerAuthoritative(), Is.True);
    }

    [Test]
    public void PlayerAuthoringRejectsAnOldTransformInsteadOfAddingASecondAuthority()
    {
        root.AddComponent<SquadCharacterController>();
        var existing = root.AddComponent<NetworkTransform>();
        Assert.That(CombatPlayerNetworkAuthoring.Validate(root), Does.Contain("UCC motor cache"));
        Assert.Throws<System.InvalidOperationException>(() => CombatPlayerNetworkAuthoring.Ensure(root));
        Assert.That(root.GetComponents<NetworkTransform>().Length, Is.EqualTo(1));
        Assert.That(root.GetComponent<NetworkTransform>(), Is.SameAs(existing));
    }

    [Test]
    public void RequiresARealControllerAndDoesNotCreateOne()
    {
        animator.runtimeAnimatorController = null;
        Assert.That(CombatEnemyNetworkAuthoring.Validate(root, animator), Does.Contain("controller"));
        Assert.That(root.GetComponent<NetworkAnimator>(), Is.Null);
    }
}
#endif
