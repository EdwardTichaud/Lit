using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class DirectionalSkillKitTests
{
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
    public void CatalogHasFifteenSkillsAndAnEmptyLucianB()
    {
        var catalog = Resources.Load<CombatSkillKitCatalog>("CombatSkillKits/Default");
        Assert.That(catalog, Is.Not.Null);
        int count = 0;
        foreach (CombatSkillKit kit in System.Enum.GetValues(typeof(CombatSkillKit)))
        {
            Assert.That(catalog.GetSkills(kit).Length, Is.EqualTo(4));
            for (int i = 0; i < 4; i++) if (catalog.GetSkill(kit, i) != null) count++;
        }
        Assert.That(count, Is.EqualTo(15));
        Assert.That(catalog.GetSkill(CombatSkillKit.Lucian, 3), Is.Null);
        Assert.That(catalog.GetSkill(CombatSkillKit.Lucian, -1), Is.Null);
        Assert.That(catalog.GetSkill(CombatSkillKit.Lucian, 4), Is.Null);
    }
    [Test]
    public void WheelFaceBindingsAreExclusiveAndLockUsesR3()
    {
        var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/PlayerInputs.inputactions");
        var wheel = asset.FindActionMap("CombatWheel", true);
        var paths = new System.Collections.Generic.HashSet<string>();
        foreach (var binding in wheel.bindings)
            if (binding.path.StartsWith("<Gamepad>")) Assert.That(paths.Add(binding.path), Is.True, binding.path);
        var lockAction = asset.FindAction("RealTimeCombat/SwitchEnemyLock", true);
        foreach (var binding in lockAction.bindings)
            if (binding.path.StartsWith("<Gamepad>"))
            {
                Assert.That(binding.path, Is.EqualTo("<Gamepad>/rightStickPress"));
                Assert.That(binding.interactions, Does.Contain("tap").And.Contain("hold"));
            }
    }
}
