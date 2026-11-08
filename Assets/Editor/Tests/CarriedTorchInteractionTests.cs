using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class CarriedTorchInteractionTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject player, targetRoot, torch;
    private SquadCharacterController actor;
    private PlayerTorchInfluence source;
    private Light light;
    private LadderController target;

    [SetUp]
    public void SetUp()
    {
        player = new GameObject("Carried interaction light owner");
        player.SetActive(false); player.transform.position = Vector3.one * 12000;
        actor = player.AddComponent<SquadCharacterController>();
        foreach (var behaviour in player.GetComponents<Behaviour>()) behaviour.enabled = false;
        player.SetActive(true);
        torch = new GameObject("Carried interaction light"); torch.transform.SetParent(player.transform, false);
        light = torch.AddComponent<Light>(); light.range = 5;
        source = torch.AddComponent<PlayerTorchInfluence>();
        typeof(PlayerTorchInfluence).GetField("owner", Private).SetValue(source, actor);
        typeof(PlayerTorchInfluence).GetField("torchLight", Private).SetValue(source, light);
        typeof(PlayerTorchInfluence).GetMethod("OnEnable", Private).Invoke(source, null);
        targetRoot = new GameObject("Actual ladder interaction target");
        targetRoot.transform.position = player.transform.position + Vector3.right * 2;
        target = targetRoot.AddComponent<LadderController>();
        target.interactionCollider = targetRoot.AddComponent<BoxCollider>();
        Physics.SyncTransforms();
        typeof(CharacterInteractionDetection).GetField("activeFlamesFrame", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, -1);
    }

    private void Equipped(bool value) => typeof(SquadCharacterController).GetField("flameEquipped", Private).SetValue(actor, value);

    [TearDown]
    public void TearDown()
    {
        if (source != null) typeof(PlayerTorchInfluence).GetMethod("OnDisable", Private).Invoke(source, null);
        Object.DestroyImmediate(targetRoot); Object.DestroyImmediate(torch); Object.DestroyImmediate(player);
        typeof(CharacterInteractionDetection).GetField("activeFlamesFrame", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, -1);
    }

    [Test]
    public void EquippedTorchMakesNearbyLadderEligibleWithoutFixedFlame()
    {
        Equipped(false);
        Assert.That(CharacterInteractionDetection.IsInActiveFlameInfluence(target), Is.False);
        Equipped(true);
        Assert.That(CharacterInteractionDetection.IsInActiveFlameInfluence(target), Is.True);
        Equipped(false);
        Assert.That(CharacterInteractionDetection.IsInActiveFlameInfluence(target), Is.False);
    }

    [Test]
    public void GateUsesTheCurrentLightRangeAndEnabledState()
    {
        Equipped(true);
        light.range = 1;
        Assert.That(CharacterInteractionDetection.IsInActiveFlameInfluence(target), Is.False);
        light.range = 5;
        Assert.That(CharacterInteractionDetection.IsInActiveFlameInfluence(target), Is.True);
        light.enabled = false;
        Assert.That(CharacterInteractionDetection.IsInActiveFlameInfluence(target), Is.False);
    }

    [Test]
    public void RegistryCanRebuildExistingEnabledTorchesAfterSubsystemReset()
    {
        Equipped(true);
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        typeof(PlayerTorchInfluence).GetMethod("ResetRegistry", flags).Invoke(null, null);
        typeof(PlayerTorchInfluence).GetMethod("InitializeRegistry", flags).Invoke(null, null);
        Assert.That(CharacterInteractionDetection.IsInActiveFlameInfluence(target), Is.True);
        source.enabled = false;
        Assert.That(CharacterInteractionDetection.IsInActiveFlameInfluence(target), Is.False);
    }

    [Test]
    public void TorchWithoutPlayerOwnerCannotRevealInteractions()
    {
        Equipped(true);
        torch.transform.SetParent(null, true);
        typeof(PlayerTorchInfluence).GetField("owner", Private).SetValue(source, null);
        Assert.That(CharacterInteractionDetection.IsInActiveFlameInfluence(target), Is.False);
    }
}
