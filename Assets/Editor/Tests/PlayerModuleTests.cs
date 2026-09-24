using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public sealed class PlayerModuleTests
{
    [Test]
    public void EarlyLookupDoesNotPermanentlyCacheMissingCharacterComponents()
    {
        var root = new GameObject("Character assembled after module validation");
        root.SetActive(false);
        var data = ScriptableObject.CreateInstance<CharacterData>();
        try
        {
            var configuration = new PlayerModuleConfiguration<PlayerLocomotionSettings>();
            Assert.That(configuration.Resolve(root.transform, settings => settings.locomotion).driveLitLocomotionAnimatorParameters, Is.False);
            data.playerSettings.locomotion.driveLitLocomotionAnimatorParameters = true;
            data.playerSettings.locomotion.speedParam = "LitSpeed";
            root.AddComponent<CharacterInfo>().SetCharacterData(data);
            var resolved = configuration.Resolve(root.transform, settings => settings.locomotion);
            Assert.That(resolved.driveLitLocomotionAnimatorParameters, Is.True, "An early lookup must not retain defaults after the character is assembled.");
            Assert.That(resolved.speedParam, Is.EqualTo("LitSpeed"));
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(data); }
    }
    [Test]
    public void UnboundSquadUsesCharacterInfoUntilItsOwnDataArrives()
    {
        var root = new GameObject("Unbound squad settings");
        root.SetActive(false);
        var source = ScriptableObject.CreateInstance<CharacterData>();
        var replacement = ScriptableObject.CreateInstance<CharacterData>();
        try
        {
            var squad = root.AddComponent<SquadCharacterController>();
            root.AddComponent<CharacterInfo>().SetCharacterData(source);
            source.playerSettings.locomotion.driveLitLocomotionAnimatorParameters = true;
            source.playerSettings.locomotion.speedParam = "LitSpeed";
            replacement.playerSettings.locomotion.speedParam = "OtherSpeed";
            var configuration = new PlayerModuleConfiguration<PlayerLocomotionSettings>();
            var first = configuration.Resolve(root.transform, data => data.locomotion);
            Assert.That(first.driveLitLocomotionAnimatorParameters, Is.True);
            Assert.That(first.speedParam, Is.EqualTo("LitSpeed"));
            typeof(SquadCharacterController).GetField("characterData", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(squad, replacement);
            Assert.That(configuration.Resolve(root.transform, data => data.locomotion).speedParam, Is.EqualTo("OtherSpeed"));
            Assert.That(source.playerSettings.locomotion.speedParam, Is.EqualTo("LitSpeed"));
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(source); Object.DestroyImmediate(replacement); }
    }

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
        Assert.That(data.playerSettings.locomotion.driveLitLocomotionAnimatorParameters, Is.True);
        Assert.That(data.playerSettings.locomotion.speedParam, Is.EqualTo("LitSpeed"));
        var controller = prefab.GetComponent<Animator>().runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
        Assert.That(controller, Is.Not.Null);
        Assert.That(System.Array.Exists(controller.parameters, p => p.name == data.playerSettings.locomotion.speedParam &&
            p.type == AnimatorControllerParameterType.Float), Is.True);
        foreach (var component in prefab.GetComponentsInChildren<MonoBehaviour>(true)) Assert.That(component, Is.Not.Null);
        if (character == "Lucian")
        {
            Assert.That(prefab.GetComponent<PlayerCombatAnimationEvents>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<PlayerActionPresentationController>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<PlayerRootMotionRelay>(), Is.Not.Null);
        }
    }

    [TestCase("Link")]
    [TestCase("Lucian")]
    [TestCase("Luna")]
    [TestCase("Mia")]
    public void PlayerPrefabEnablesTheTorchAnimationLayer(string character)
    {
        string folder = "Assets/Characters/1_Squad/" + character + "/";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "Player_Model_" + character + ".prefab");
        var squad = prefab.GetComponent<SquadCharacterController>();
        var animator = prefab.GetComponent<Animator>();
        var controller = animator.runtimeAnimatorController as AnimatorController;
        var serializedSquad = new SerializedObject(squad);

        Assert.That(serializedSquad.FindProperty("useFlameAnimationLayer").boolValue, Is.True);
        Assert.That(controller, Is.Not.Null);
        Assert.That(System.Array.Exists(controller.layers, layer => layer.name == "Upper Body Flame"), Is.True);
        Assert.That(System.Array.Exists(controller.parameters, parameter =>
            parameter.name == "Flame" && parameter.type == AnimatorControllerParameterType.Bool), Is.True);
    }

    [Test]
    public void LucianTorchUsesTheCarriedLightAndAnUpperBodyOnlyMask()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Characters/1_Squad/Lucian/Player_Model_Lucian.prefab");
        try
        {
            var squad = root.GetComponent<SquadCharacterController>();
            var controller = root.GetComponent<Animator>().runtimeAnimatorController as AnimatorController;
            var findFlame = typeof(SquadCharacterController).GetMethod("FindFlameTransform", BindingFlags.Instance | BindingFlags.NonPublic);
            Transform torch = (Transform)findFlame.Invoke(squad, null);

            Assert.That(torch, Is.Not.Null);
            Assert.That(torch.name, Is.EqualTo("torch"));
            Assert.That(torch.parent.name, Is.EqualTo("hand_l_items"));
            Assert.That(torch.GetComponentInChildren<PlayerTorchInfluence>(true), Is.Not.Null);

            torch.gameObject.SetActive(true);
            typeof(SquadCharacterController).GetMethod("InitializeFlameState", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(squad, null);
            Assert.That(torch.gameObject.activeSelf, Is.False, "The carried torch must not start with a persistent light.");

            AnimatorControllerLayer flameLayer = System.Array.Find(controller.layers, layer => layer.name == "Upper Body Flame");
            Assert.That(flameLayer.avatarMask, Is.Not.Null);
            Assert.That(flameLayer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Body), Is.False);
            Assert.That(flameLayer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg), Is.False);
            Assert.That(flameLayer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg), Is.False);
            Assert.That(flameLayer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm), Is.True);
            Assert.That(flameLayer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm), Is.True);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [TestCase("Mixamo_Idle_Flame_41f0ac36_UpperBody.anim")]
    [TestCase("Mixamo_Walk_Flame_f1807c0d_UpperBody.anim")]
    [TestCase("Mixamo_Flame_Equip_3c63a863_UpperBody.anim")]
    public void LucianFlameClipsDoNotAnimateTheLowerBody(string clipName)
    {
        const string folder = "Assets/Characters/1_Squad/Lucian/Animation/PlayerInPlace/";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + clipName);
        Assert.That(clip, Is.Not.Null);

        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
        {
            Assert.That(IsLowerBodyFlameCurve(binding.propertyName), Is.False,
                $"{clip.name} must not animate '{binding.propertyName}'.");
        }
    }

    private static bool IsLowerBodyFlameCurve(string propertyName)
    {
        return propertyName.StartsWith("RootT.") || propertyName.StartsWith("RootQ.") ||
            propertyName.StartsWith("LeftFoot") || propertyName.StartsWith("RightFoot") ||
            propertyName.StartsWith("Left Foot ") || propertyName.StartsWith("Right Foot ") ||
            propertyName.StartsWith("Left Toes ") || propertyName.StartsWith("Right Toes ") ||
            propertyName.StartsWith("Left Lower Leg ") || propertyName.StartsWith("Right Lower Leg ") ||
            propertyName.StartsWith("Left Upper Leg ") || propertyName.StartsWith("Right Upper Leg ");
    }

}
