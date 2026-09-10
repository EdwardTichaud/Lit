using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class PlayerModuleTests
{
    [Test]
    public void ModuleCopiesDoNotChangeTheSourceOrAnotherActor()
    {
        var source = ScriptableObject.CreateInstance<CharacterData>();
        var first = new GameObject("First actor");
        var second = new GameObject("Second actor");
        first.SetActive(false); second.SetActive(false);
        try
        {
            first.AddComponent<CharacterInfo>().SetCharacterData(source);
            second.AddComponent<CharacterInfo>().SetCharacterData(source);
            var a = new PlayerModuleConfiguration<PlayerJumpSettings>();
            var b = new PlayerModuleConfiguration<PlayerJumpSettings>();
            a.Resolve(first.transform, data => data.jump).jumpHeight = 123;
            Assert.That(source.playerSettings.jump.jumpHeight, Is.EqualTo(5));
            Assert.That(b.Resolve(second.transform, data => data.jump).jumpHeight, Is.EqualTo(5));
            Assert.That(a.Resolve(first.transform, data => data.jump).jumpHeight, Is.EqualTo(123));
            Assert.That(a.Resolve(second.transform, data => data.jump).jumpHeight, Is.EqualTo(5), "Changing the controlled actor must release the previous actor configuration.");
        }
        finally { Object.DestroyImmediate(first); Object.DestroyImmediate(second); Object.DestroyImmediate(source); }
    }

    [Test]
    public void ForeignMotionOwnerCannotAcquireOrReleaseTheExistingReservation()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Characters/1_Squad/Lucian/Player_Model_Lucian.prefab");
        try
        {
            var bridge = root.GetComponent<LitOpsiveLocomotionBridge>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var ownerField = typeof(LitOpsiveLocomotionBridge).GetField("scriptedPlanarMotionOwner", flags);
            var countField = typeof(LitOpsiveLocomotionBridge).GetField("scriptedPlanarMotionLockCount", flags);
            var first = new object(); var other = new object();
            ownerField.SetValue(bridge, first); countField.SetValue(bridge, 1);
            Assert.That(bridge.BeginScriptedPlanarMotion(other), Is.False);
            bridge.EndScriptedPlanarMotion(other);
            bridge.EndScriptedPlanarMotion(null);
            Assert.That(ownerField.GetValue(bridge), Is.SameAs(first));
            Assert.That(countField.GetValue(bridge), Is.EqualTo(1));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [TestCase("Link")]
    [TestCase("Lucian")]
    [TestCase("Luna")]
    [TestCase("Mia")]
    public void PlayerPrefabHasValidModulesAndMigratedSettings(string character)
    {
        string folder = "Assets/Characters/1_Squad/" + character + "/";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "Player_Model_" + character + ".prefab");
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>(folder + character + ".asset");
        Assert.That(data, Is.Not.Null);
        Assert.That(data.playerSettings.jump, Is.Not.Null);
        Assert.That(data.playerSettings.locomotion, Is.Not.Null);
        foreach (var component in prefab.GetComponentsInChildren<MonoBehaviour>(true)) Assert.That(component, Is.Not.Null);
        if (character == "Lucian")
        {
            Assert.That(prefab.GetComponent<PlayerCombatAnimationEvents>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<PlayerActionPresentationController>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<PlayerRootMotionRelay>(), Is.Not.Null);
        }
    }
}
