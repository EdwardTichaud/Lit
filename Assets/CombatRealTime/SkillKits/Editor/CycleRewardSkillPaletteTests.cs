using System;
using System.Collections.Generic;
using System.Reflection;
using Lit.Combat.Migration.Network;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

public sealed class CycleRewardSkillPaletteTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private WorldRulesStateManager rules;
    private CycleProgressionService previousService;
    private CombatSkillKitInput palette;
    private CycleDefinition nina;
    private SkillSO cicatrice;
    private CombatSkillKitCatalog kits;
    private static FieldInfo InstanceField => typeof(CycleProgressionService).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);

    [SetUp] public void SetUp()
    {
        root = new GameObject("Cycle reward skill fixture");
        root.SetActive(false);
        rules = root.AddComponent<WorldRulesStateManager>();
        var service = root.AddComponent<CycleProgressionService>();
        previousService = CycleProgressionService.Instance;
        InstanceField.SetValue(null, service);
        palette = root.AddComponent<CombatSkillKitInput>();
        kits = Resources.Load<CombatSkillKitCatalog>("CombatSkillKits/Default");
        typeof(CombatSkillKitInput).GetField("catalog", Private).SetValue(palette, kits);
        nina = AssetDatabase.LoadAssetAtPath<CycleDefinition>("Assets/Scenes/Cycles/Cycle_Nina/NinaCycle.asset");
        cicatrice = AssetDatabase.LoadAssetAtPath<SkillSO>("Assets/CombatRealTime/Skills/Skill_3_Cicatrice.asset");
        Assert.That(cicatrice, Is.Not.Null);
    }

    [TearDown] public void TearDown()
    {
        InstanceField.SetValue(null, previousService);
        Object.DestroyImmediate(root);
    }

    private void Refresh() => typeof(CombatSkillKitInput).GetMethod("RefreshCycleRewards", Private).Invoke(palette, null);
    private void Page(int page) => typeof(CombatSkillKitInput).GetField("palettePage", Private).SetValue(palette, page);
    private SkillSO Slot(int slot) => (SkillSO)typeof(CombatSkillKitInput).GetMethod("GetSlotSkill", Private)
        .Invoke(palette, new object[] { CombatSkillKit.Lucian, slot });
    private bool Allowed(int slot) => (bool)typeof(CombatSkillKitInput).GetMethod("IsSlotAvailable", Private)
        .Invoke(palette, new object[] { CombatSkillKit.Lucian, slot });

    [Test] public void NinaRewardAppearsOnlyAfterScarAndKeepsAllFourLucianSkills()
    {
        var before = (SkillSO[])kits.GetSkills(CombatSkillKit.Lucian).Clone();
        Refresh();
        Assert.That(palette.PalettePageCount, Is.EqualTo(1));
        rules.SetInt(nina.StepKey("scientist_defeated"), 1);
        rules.SetInt(nina.StepKey("nina_spoken"), 1);
        Refresh();
        Assert.That(CycleSharedSkills.IsUnlocked(cicatrice), Is.False);
        Assert.That(palette.PalettePageCount, Is.EqualTo(1));
        rules.SetInt(nina.StepKey("scar_reward"), 1);
        Refresh(); Page(1);
        Assert.That(palette.ShowingCycleRewards, Is.True);
        Assert.That(palette.PalettePageCount, Is.EqualTo(2));
        Assert.That(Slot(0), Is.SameAs(cicatrice));
        Assert.That(Allowed(0), Is.True);
        Assert.That(Slot(1), Is.Null);
        CollectionAssert.AreEqual(before, kits.GetSkills(CombatSkillKit.Lucian));
        Assert.That(kits.GetSkill(CombatSkillKit.Lucian, 3).IsGroundImpactSkill, Is.True);
    }

    [Test] public void SavedRewardReturnsWithoutAnotherDialogueAndCannotRemainUsableAfterReset()
    {
        rules.SetInt(nina.StepKey("scar_reward"), 1);
        var saved = rules.CaptureVariables();
        Refresh(); Page(1);
        Assert.That(Allowed(0), Is.True);
        rules.ApplyVariables(Array.Empty<WorldVariableSnapshot>());
        Assert.That(Allowed(0), Is.False, "A stale displayed reward must not authorize a cast.");
        Refresh();
        Assert.That(palette.PalettePageCount, Is.EqualTo(1));
        Assert.That(palette.ShowingCycleRewards, Is.False);
        rules.ApplyVariables(saved);
        Refresh(); Page(1);
        Assert.That(Slot(0), Is.SameAs(cicatrice));
        Assert.That(Allowed(0), Is.True);
    }

    [Test] public void FurtherRewardsArePagedWithoutReplacingOrTruncatingSlots()
    {
        var rewards = (List<SkillSO>)typeof(CombatSkillKitInput).GetField("cycleRewardSkills", Private).GetValue(palette);
        var allocated = new List<SkillSO>();
        try
        {
            for (int i = 0; i < 5; i++) { var skill = ScriptableObject.CreateInstance<SkillSO>(); allocated.Add(skill); rewards.Add(skill); }
            Assert.That(palette.PalettePageCount, Is.EqualTo(3));
            Page(1);
            for (int i = 0; i < 4; i++) Assert.That(Slot(i), Is.SameAs(allocated[i]));
            Page(2);
            Assert.That(Slot(0), Is.SameAs(allocated[4]));
            Assert.That(Slot(1), Is.Null);
            Assert.That(Slot(-1), Is.Null);
            Assert.That(Slot(4), Is.Null);
            Assert.That(Allowed(0), Is.False, "Being in a palette is not proof of a saved reward.");
        }
        finally { foreach (var skill in allocated) Object.DestroyImmediate(skill); }
    }

    [TestCase(CombatSkillKit.Lucian)] [TestCase(CombatSkillKit.Vesta)]
    [TestCase(CombatSkillKit.Soren)] [TestCase(CombatSkillKit.Aurell)]
    public void NetworkOwnershipRequiresTheSessionRewardForEveryPlayableCharacter(CombatSkillKit kit)
    {
        var actor = root.AddComponent<SquadCharacterController>();
        typeof(SquadCharacterController).GetField("characterData", Private).SetValue(actor, kits.GetCharacter(kit));
        var network = root.AddComponent<CombatNetworkActorRuntime>();
        typeof(CombatNetworkActorRuntime).GetField("actor", Private).SetValue(network, actor);
        var owns = typeof(CombatNetworkActorRuntime).GetMethod("Owns", Private);
        Assert.That(owns.Invoke(network, new object[] { cicatrice }), Is.False);
        rules.SetInt(nina.StepKey("scar_reward"), 1);
        Assert.That(owns.Invoke(network, new object[] { cicatrice }), Is.True);
        rules.ApplyVariables(Array.Empty<WorldVariableSnapshot>());
        Assert.That(owns.Invoke(network, new object[] { cicatrice }), Is.False);
    }

    [Test] public void CicatriceHasAnAuthoritativeNetworkEntryAndExactlyOneImpact()
    {
        var catalog = Resources.Load<CombatNetworkSkillCatalog>("CombatNetwork/Default");
        Assert.That(catalog.Rebuild(out string issue), Is.True, issue);
        Assert.That(catalog.TryGet(cicatrice, out var entry), Is.True);
        Assert.That(entry.CanonicalGuid, Is.EqualTo("eca322da99dd7bf40b06c5ebae562eb4"));
        Assert.That(CombatNetworkSkillCatalog.Classify(cicatrice, out issue), Is.EqualTo(CombatNetworkSkillFamily.DirectAttack), issue);
        Assert.That(CombatNetworkSkillCatalog.TryGetImpactEvent(cicatrice, out _, out issue), Is.True, issue);
    }

    [Test] public void BumpersNavigateOnlyTheModalPaletteInTheAssetAndGeneratedInputs()
    {
        using var inputs = new PlayerInputs();
        var authored = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/PlayerInputs.inputactions");
        var pad = InputSystem.AddDevice<Gamepad>();
        try
        {
            foreach (var asset in new[] { authored, inputs.asset })
            {
                var action = asset.FindAction("CombatWheel/CycleSkillPalette", true);
                Assert.That(action.expectedControlType, Is.EqualTo("Axis"));
                action.Enable();
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.LeftShoulder)); InputSystem.Update();
                Assert.That(action.ReadValue<float>(), Is.EqualTo(-1));
                InputSystem.QueueStateEvent(pad, new GamepadState()); InputSystem.Update();
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.RightShoulder)); InputSystem.Update();
                Assert.That(action.ReadValue<float>(), Is.EqualTo(1));
                InputSystem.QueueStateEvent(pad, new GamepadState()); InputSystem.Update();
                action.Disable();
                Assert.That(asset.FindAction("RealTimeCombat/CycleSkillPalette", false), Is.Null);
            }
        }
        finally { InputSystem.RemoveDevice(pad); }
    }
}
