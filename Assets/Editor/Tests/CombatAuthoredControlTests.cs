#if UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CombatAuthoredControlTests
{
    [TestCase("Lucian")]
    [TestCase("Aurell")]
    [TestCase("Vesta")]
    [TestCase("Soren")]
    public void PlayerUsesValidatedRotationFromCharacterDataWithoutAnArenaOverride(string character)
    {
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Characters/1_Squad/" + character + "/" + character + ".asset");
        var settings = data.playerSettings.locomotion;
        Assert.That(settings.orientationWalkTurnRate, Is.EqualTo(720));
        Assert.That(settings.orientationSprintTurnRate, Is.EqualTo(1080));
        Assert.That(settings.orientationSharpTurnRate, Is.EqualTo(1440));
        Assert.That(settings.combatFacingSpeedDegreesPerSecond, Is.EqualTo(1440));
        Assert.That(settings.rateLimitedCombatFacing, Is.True);
        Assert.That(typeof(LucianBrainsCombatArena).GetField("playerWalkTurnRate"), Is.Null);
        Assert.That(typeof(LucianBrainsCombatArena).GetField("lockCursorPrefab"), Is.Null);
        Assert.That(typeof(LucianBrainsCombatArena).GetField("freeAttacks"), Is.Null);
    }

    [Test]
    public void BridgeResolvesAuthoredFacingBeforeSquadBindingAndKeepsItsPrivateCopy()
    {
        var root = new GameObject("Authored rotation actor");
        root.SetActive(false);
        var data = ScriptableObject.CreateInstance<CharacterData>();
        var replacement = ScriptableObject.CreateInstance<CharacterData>();
        try
        {
            data.playerSettings.locomotion.rateLimitedCombatFacing = true;
            data.playerSettings.locomotion.orientationSprintTurnRate = 1080;
            var info = root.AddComponent<CharacterInfo>();
            info.SetCharacterData(data);
            var bridge = root.AddComponent<LitOpsiveLocomotionBridge>();
            var property = typeof(LitOpsiveLocomotionBridge).GetProperty("ModuleSettings", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(bridge.UseRateLimitedCombatFacing, Is.True);
            var settings = (PlayerLocomotionSettings)property.GetValue(bridge);
            Assert.That(settings.orientationSprintTurnRate, Is.EqualTo(1080));
            settings.orientationSprintTurnRate = 37;
            Assert.That(data.playerSettings.locomotion.orientationSprintTurnRate, Is.EqualTo(1080));
            info.SetCharacterData(replacement);
            Assert.That(bridge.UseRateLimitedCombatFacing, Is.False);
            Assert.That(((PlayerLocomotionSettings)property.GetValue(bridge)).orientationSprintTurnRate,
                Is.EqualTo(replacement.playerSettings.locomotion.orientationSprintTurnRate));
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(data); Object.DestroyImmediate(replacement); }
    }

    [Test]
    public void ProductionWorldOwnsAnActualWorldPrefabAndNotAScreenSpaceCanvas()
    {
        var session = AssetDatabase.LoadAssetAtPath<GameObject>(ProductionCombatMigration.SessionPath);
        var world = session.GetComponentInChildren<CombatWorldRuntime>(true);
        Assert.That(world.lockCursorPrefab, Is.Not.Null);
        Assert.That(world.lockCursorOffset, Is.EqualTo(Vector3.up * 3));
        Assert.That(world.lockCursorPrefab.GetComponentsInChildren<Canvas>(true), Is.Empty);
        Assert.That(world.lockCursorPrefab.GetComponentsInChildren<Renderer>(true), Is.Not.Empty);
        var reactions = world.GetComponent<CombatHealthThresholdController>();
        var serialized = new SerializedObject(reactions);
        Assert.That(serialized.FindProperty("reactionTimeScale").floatValue, Is.EqualTo(1));
        Assert.That(serialized.FindProperty("externalSuccessSlowMotion").boolValue, Is.True);
        Assert.That(new SerializedObject(world.GetComponent<CombatMobilityController>())
            .FindProperty("mobilityInputBufferSeconds").floatValue, Is.EqualTo(.15f));
    }

    [Test]
    public void JuggernautKeepsItsOwnPatternsWithValidatedTurnRate()
    {
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Characters/3_Enemy/Juggernaut/Juggernaut.asset");
        var brain = data.ResolveWorldPrefab().GetComponent<LitBrainsEnemy>();
        Assert.That(brain.combatProfile.turnDegreesPerSecond, Is.EqualTo(1080));
        Assert.That(data.enemyCombatProfile.trackingDegreesPerSecond, Is.EqualTo(1080));
        Assert.That(brain.combatProfile.productionSourceProfile, Is.SameAs(data.enemyCombatProfile));
        Assert.That(brain.combatProfile.attacks, Has.Length.EqualTo(4));
    }
}
#endif
