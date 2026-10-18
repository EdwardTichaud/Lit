using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class DirectionalSkillKitTests
{
    [Test]
    public void WheelOpensOnlyFromDirectionalPadAndKeepsCameraWithoutCombatActions()
    {
        var inputs = new PlayerInputs();
        var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/PlayerInputs.inputactions");
        try
        {
            foreach (var source in new[] { asset, inputs.asset })
            {
                foreach (var binding in source.FindAction("RealTimeCombat/OpenPalette", true).bindings)
                    Assert.That(binding.path, Is.Not.EqualTo("<Gamepad>/leftTrigger"));
                foreach (string map in new[] { "RealTimeCombat", "CombatWheel" })
                    Assert.That(source.FindAction(map + "/SelectSkillKit", true).bindings[0].path, Is.EqualTo("<Gamepad>/dpad"));
            }
            var getMaps = typeof(InputModeCoordinator).GetMethod("GetMaps", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var maps = (System.Collections.Generic.IEnumerable<string>)getMaps.Invoke(null, new object[] { InputMode.CombatWheel });
            CollectionAssert.AreEquivalent(new[] { "CombatWheel", "Camera", "System" }, maps);
        }
        finally { inputs.Dispose(); }
    }

    [TestCase(0, 1, CombatSkillKit.Lucian)]
    [TestCase(1, 0, CombatSkillKit.Vesta)]
    [TestCase(0, -1, CombatSkillKit.Soren)]
    [TestCase(-1, 0, CombatSkillKit.Aurell)]
    public void DirectionSelectsOnlyOneKit(float x, float y, CombatSkillKit expected)
    {
        Assert.That(CombatSkillKitCatalog.TryDirection(new Vector2(x, y), out var actual), Is.True);
        Assert.That(actual, Is.EqualTo(expected));
    }
    [TestCase(0, 0)] [TestCase(1, 1)] [TestCase(-1, 1)] [TestCase(1, -1)] [TestCase(-1, -1)]
    public void NeutralAndDiagonalsSelectNothing(float x, float y) =>
        Assert.That(CombatSkillKitCatalog.TryDirection(new Vector2(x, y), out _), Is.False);

    [Test]
    public void CatalogHasSixteenSkillsAndGroundImpactInLucianLastSlot()
    {
        var catalog = Resources.Load<CombatSkillKitCatalog>("CombatSkillKits/Default");
        Assert.That(catalog, Is.Not.Null);
        int count = 0;
        foreach (CombatSkillKit kit in System.Enum.GetValues(typeof(CombatSkillKit)))
        {
            Assert.That(catalog.GetSkills(kit).Length, Is.EqualTo(4));
            for (int i = 0; i < 4; i++) if (catalog.GetSkill(kit, i) != null) count++;
        }
        Assert.That(count, Is.EqualTo(16));
        Assert.That(catalog.GetSkill(CombatSkillKit.Lucian, 3).IsGroundImpactSkill, Is.True);
        Assert.That(catalog.GetSkill(CombatSkillKit.Lucian, -1), Is.Null);
        Assert.That(catalog.GetSkill(CombatSkillKit.Lucian, 4), Is.Null);
    }
    [Test]
    public void WheelCancelUsesBAndFacesNeverCastSkills()
    {
        var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/PlayerInputs.inputactions");
        var wheel = asset.FindActionMap("CombatWheel", true);
        var paths = new System.Collections.Generic.HashSet<string>();
        foreach (var binding in wheel.bindings)
            if (binding.path.StartsWith("<Gamepad>")) Assert.That(paths.Add(binding.path), Is.True, binding.path);
        Assert.That(wheel.FindAction("Cancel").bindings[0].path, Is.EqualTo("<Gamepad>/buttonEast"));
        foreach (string name in new[] { "SkillX", "SkillY", "SkillA", "SkillB" })
            foreach (var binding in wheel.FindAction(name).bindings)
                Assert.That(binding.path, Is.Empty, "Face skill binding must stay inactive.");
        var lockAction = asset.FindAction("RealTimeCombat/SwitchEnemyLock", true);
        foreach (var binding in lockAction.bindings)
            if (binding.path.StartsWith("<Gamepad>"))
            {
                Assert.That(binding.path, Is.EqualTo("<Gamepad>/rightStickPress"));
                Assert.That(binding.interactions, Does.Contain("tap").And.Contain("hold"));
            }
    }

    [TestCase(-1, 0, 0)] [TestCase(0, 1, 1)]
    [TestCase(0, -1, 2)] [TestCase(1, 0, 3)]
    public void WheelDirectionsKeepStableSavedSlots(float x, float y, int expected)
    {
        Assert.That(CombatSkillKitCatalog.TrySkillDirection(new Vector2(x, y), out int actual), Is.True);
        Assert.That(actual, Is.EqualTo(expected));
    }

    [TestCase(3f)] [TestCase(15f)] [TestCase(30f)]
    public void LightArrowArcRisesThenDescendsAndKeepsEndpoints(float distance)
    {
        var skill = Resources.Load<CombatSkillKitCatalog>("CombatSkillKits/Default").GetSkill(CombatSkillKit.Soren, 0);
        var cue = System.Array.Find(System.Linq.Enumerable.ToArray(skill.VfxCues),
            entry => entry.delivery == SkillVfxDelivery.ProjectileFromPlayerHand);
        Assert.That(cue, Is.Not.Null);
        var start = Vector3.up;
        var end = start + Vector3.forward * distance;
        Assert.That(cue.EvaluateProjectilePosition(start, end, 0), Is.EqualTo(start));
        Assert.That(cue.EvaluateProjectilePosition(start, end, 1), Is.EqualTo(end));
        Assert.That(cue.EvaluateProjectilePosition(start, end, .5f).y - start.y,
            Is.EqualTo(Mathf.Min(distance * .2f, 4)).Within(.001f));
        Assert.That(cue.EvaluateProjectileTangent(start, end, 0).y, Is.GreaterThan(0));
        Assert.That(cue.EvaluateProjectileTangent(start, end, 1).y, Is.LessThan(0));
    }

    [Test]
    public void ExistingProjectileWithoutArcKeepsStraightTrajectory()
    {
        var cue = new SkillVfxCue();
        var end = new Vector3(4, 2, 8);
        Assert.That(cue.EvaluateProjectilePosition(Vector3.zero, end, .5f), Is.EqualTo(end * .5f));
        Assert.That(cue.EvaluateProjectileTangent(Vector3.zero, end, .5f), Is.EqualTo(end));
    }
}
