#if UNITY_INCLUDE_TESTS
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed class ChargedSkillTests
{
    [Test]
    public void DeviceRemovalDoesNotReadDisconnectedChargeControls()
    {
        var owner = new GameObject("Inactive charge input test");
        owner.SetActive(false);
        var input = owner.AddComponent<RealTimeCombatInput>();
        var pad = InputSystem.AddDevice<Gamepad>();
        var map = new InputActionMap("Charge device test");
        try
        {
            map.devices = new InputDevice[] { pad };
            var action = map.AddAction("Basic", InputActionType.Button, "<Gamepad>/buttonWest");
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(RealTimeCombatInput).GetField("basicAttackAction", flags).SetValue(input, action);
            var read = typeof(RealTimeCombatInput).GetMethod("IsBasicAttackPhysicallyHeld", flags);
            bool canceled = false;
            action.canceled += _ => { canceled = true; Assert.That(read.Invoke(input, null), Is.False); };
            map.Enable();
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.West));
            InputSystem.Update();
            Assert.That(read.Invoke(input, null), Is.True);
            InputSystem.RemoveDevice(pad);
            Assert.That(canceled, Is.True);
            Assert.That(read.Invoke(input, null), Is.False);
        }
        finally
        {
            map.Dispose();
            if (pad.added) InputSystem.RemoveDevice(pad);
            Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public void ChargeFramingIsRenderOnlyAndOldReleaseCannotCancelNewCharge()
    {
        var cameraObject = new GameObject("Charge test camera");
        var actor = new GameObject("Charge test actor");
        try
        {
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60;
            camera.transform.position = new Vector3(0, 2, -4);
            var director = cameraObject.AddComponent<LitCameraDirector>();
            int oldHandle = director.BeginChargeFraming(actor.transform, 10, .6f, .25f);
            int currentHandle = director.BeginChargeFraming(actor.transform, 10, .6f, .25f);
            director.EndChargeFraming(oldHandle);
            Assert.That(director.IsChargeFramingActive, Is.True);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(LitCameraDirector).GetField("chargeLastRenderAt", flags).SetValue(director, Time.realtimeSinceStartupAsDouble - 1);
            var begin = typeof(LitCameraDirector).GetMethod("BeginRender", flags);
            var end = typeof(LitCameraDirector).GetMethod("EndRender", flags);
            var args = new object[] { default(UnityEngine.Rendering.ScriptableRenderContext), camera };
            begin.Invoke(director, args);
            Assert.That(camera.fieldOfView, Is.EqualTo(50).Within(.001f));
            Assert.That(camera.transform.position.z, Is.EqualTo(-4.6f).Within(.001f));
            end.Invoke(director, args);
            Assert.That(camera.fieldOfView, Is.EqualTo(60).Within(.001f));
            Assert.That(camera.transform.position.z, Is.EqualTo(-4).Within(.001f));
            director.EndChargeFraming(currentHandle);
            Assert.That(director.IsChargeFramingActive, Is.False);
            typeof(LitCameraDirector).GetField("chargeLastRenderAt", flags).SetValue(director, Time.realtimeSinceStartupAsDouble - 1);
            begin.Invoke(director, args);
            Assert.That(camera.fieldOfView, Is.EqualTo(60).Within(.001f));
            end.Invoke(director, args);
        }
        finally
        {
            cameraObject.GetComponent<LitCameraDirector>()?.EndOutputPreview();
            Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void LucianLaboratoryOwnsOnePersonalChargedSkillOutsideWheel()
    {
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>(GroundImpactSkillSetup.DataPath);
        var skill = AssetDatabase.LoadAssetAtPath<ChargedSkillSO>(ChargedSkillSetup.SkillPath);
        Assert.That(skill, Is.Not.Null);
        Assert.That(data.chargedSkill, Is.SameAs(skill));
        Assert.That(data.combatSkills.Contains(skill), Is.False);
        Assert.That(skill.chargeSeconds, Is.EqualTo(2));
        Assert.That(skill.cooldownSeconds, Is.EqualTo(8));
        Assert.That(skill.IsWithinLaunchRange(0), Is.True);
        Assert.That(skill.IsWithinLaunchRange(3), Is.True);
        Assert.That(skill.IsWithinLaunchRange(3.001f), Is.False);
        Assert.That(skill.IsWithinLaunchRange(-.001f), Is.False);
        foreach (string name in new[] { "Aurell", "Vesta", "Soren" })
        {
            var other = AssetDatabase.LoadAssetAtPath<CharacterData>($"Assets/Characters/1_Squad/{name}/{name}.asset");
            Assert.That(other.chargedSkill, Is.Null, name);
        }
    }

    [Test]
    public void PreparationHoldsBasicOneFinalPoseWithoutReplayingItsEvents()
    {
        var skill = AssetDatabase.LoadAssetAtPath<ChargedSkillSO>(ChargedSkillSetup.SkillPath);
        var basic = AssetDatabase.LoadAssetAtPath<BasicSkillsSO>(ChargedSkillSetup.Folder + "/Clash/ClashBasic_0.asset").AnimationClip;
        Assert.That(AnimationUtility.GetAnimationEvents(skill.preparationClip), Is.Empty);
        var bindings = AnimationUtility.GetCurveBindings(basic);
        Assert.That(bindings, Is.Not.Empty);
        Assert.That(AnimationUtility.GetCurveBindings(skill.preparationClip).Length, Is.EqualTo(bindings.Length));
        foreach (var binding in bindings)
        {
            float finalValue = AnimationUtility.GetEditorCurve(basic, binding).Evaluate(basic.length);
            var held = AnimationUtility.GetEditorCurve(skill.preparationClip, binding);
            Assert.That(held, Is.Not.Null, binding.propertyName);
            foreach (float time in new[] { 0f, .25f, .5f, 1f })
                Assert.That(held.Evaluate(time), Is.EqualTo(finalValue).Within(.00001f), binding.propertyName);
        }
    }

    [Test]
    public void SingleLaunchContactAndCosmeticFloatingClips()
    {
        var skill = AssetDatabase.LoadAssetAtPath<ChargedSkillSO>(ChargedSkillSetup.SkillPath);
        Assert.That(skill.preparationClip, Is.Not.Null);
        Assert.That(skill.preparationClip.name, Is.EqualTo("ChargedWindup"));
        Assert.That(skill.preparationClip.isHumanMotion, Is.True);
        Assert.That(AnimationUtility.GetAnimationEvents(skill.preparationClip), Is.Empty);
        Assert.That(skill.chargeCameraFovReduction, Is.EqualTo(10));
        Assert.That(skill.chargeCameraBehindOffset, Is.EqualTo(.6f));
        Assert.That(skill.AnimationClip.isHumanMotion, Is.True);
        var events = AnimationUtility.GetAnimationEvents(skill.AnimationClip);
        var contacts = events.Where(e => e.functionName == "ChargedSkillContact").OrderBy(e => e.time).ToArray();
        Assert.That(contacts.Length, Is.EqualTo(1));
        Assert.That(contacts[0].intParameter, Is.EqualTo(0));
        foreach (var clip in new[] { skill.playerFloatClip, skill.enemyFloatClip })
        {
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.isHumanMotion, Is.True);
            Assert.That(AnimationUtility.GetAnimationEvents(clip), Is.Empty);
            Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime, Is.True);
        }
        Assert.That(events.Any(e => e.functionName == "HitEnemy" || e.functionName == "ResolveSkillImpact"), Is.False);
        Assert.That(skill.presentation.movementPolicy, Is.EqualTo(PlayerActionMovementPolicy.VisualOnly));
        Assert.That(skill.airborneHoldSeconds, Is.EqualTo(1.2f));
    }
}
#endif
