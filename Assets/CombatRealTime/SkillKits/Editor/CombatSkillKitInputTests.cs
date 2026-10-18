using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed class CombatSkillKitInputTests
{
    private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    private static IEnumerable<TestCaseData> KitPairs()
    {
        foreach (CombatSkillKit actor in System.Enum.GetValues(typeof(CombatSkillKit)))
            foreach (CombatSkillKit direction in System.Enum.GetValues(typeof(CombatSkillKit)))
                yield return new TestCaseData(actor, direction);
    }

    private static bool Resolve(CombatSkillKitCatalog catalog, CharacterData character, bool solo,
        CombatSkillKit direction, out CombatSkillKit kit)
    {
        var args = new object[] { catalog, character, solo, direction, default(CombatSkillKit) };
        bool resolved = (bool)typeof(CombatSkillKitInput).GetMethod("TryResolveActorKit", PrivateStatic).Invoke(null, args);
        kit = (CombatSkillKit)args[4];
        return resolved;
    }

    [TestCaseSource(nameof(KitPairs))]
    public void MultiplayerEveryOpeningDirectionResolvesOnlyActorKit(CombatSkillKit actorKit, CombatSkillKit direction)
    {
        var catalog = Resources.Load<CombatSkillKitCatalog>("CombatSkillKits/Default");
        var character = Object.Instantiate(catalog.GetCharacter(actorKit));
        try
        {
            character.name = "Unrelated runtime name";
            Assert.That(Resolve(catalog, character, false, direction, out var actual), Is.True);
            Assert.That(actual, Is.EqualTo(actorKit));
            Assert.That(catalog.IsCharacter(character, actorKit), Is.True);
        }
        finally { Object.DestroyImmediate(character); }
    }

    [TestCaseSource(nameof(KitPairs))]
    public void SoloKeepsDirectionalSupportSelection(CombatSkillKit actorKit, CombatSkillKit direction)
    {
        var catalog = Resources.Load<CombatSkillKitCatalog>("CombatSkillKits/Default");
        Assert.That(Resolve(catalog, catalog.GetCharacter(actorKit), true, direction, out var actual), Is.True);
        Assert.That(actual, Is.EqualTo(direction));
    }

    [TestCase(CombatSkillKit.Lucian)] [TestCase(CombatSkillKit.Vesta)]
    [TestCase(CombatSkillKit.Soren)] [TestCase(CombatSkillKit.Aurell)]
    public void MultiplayerAcceptsLegacyCharacterIdWithoutRenamingIt(CombatSkillKit expected)
    {
        var catalog = Resources.Load<CombatSkillKitCatalog>("CombatSkillKits/Default");
        var character = ScriptableObject.CreateInstance<CharacterData>();
        try
        {
            character.characterId = catalog.GetCharacter(expected).characterId.ToUpperInvariant();
            string id = character.characterId;
            Assert.That(Resolve(catalog, character, false, CombatSkillKit.Lucian, out var actual), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(character.characterId, Is.EqualTo(id));
            Assert.That(catalog.IsCharacter(character, expected), Is.True);
        }
        finally { Object.DestroyImmediate(character); }
    }

    [Test]
    public void MultiplayerRuntimeCloneCanResolveBySavedAssetGuidAlone()
    {
        var catalog = Resources.Load<CombatSkillKitCatalog>("CombatSkillKits/Default");
        var character = Object.Instantiate(catalog.GetCharacter(CombatSkillKit.Aurell));
        try
        {
            string id = character.UniqueId;
            Assert.That(id, Is.Not.Empty);
            character.characterId = null;
            Assert.That(Resolve(catalog, character, false, CombatSkillKit.Lucian, out var actual), Is.True);
            Assert.That(actual, Is.EqualTo(CombatSkillKit.Aurell));
            Assert.That(character.UniqueId, Is.EqualTo(id));
            Assert.That(catalog.IsCharacter(character, CombatSkillKit.Aurell), Is.True);
        }
        finally { Object.DestroyImmediate(character); }
    }

    [Test]
    public void MultiplayerUnknownOrMissingIdentityNeverBorrowsRequestedKit()
    {
        var catalog = Resources.Load<CombatSkillKitCatalog>("CombatSkillKits/Default");
        var character = ScriptableObject.CreateInstance<CharacterData>();
        try
        {
            character.name = "Lucian";
            character.characterId = "mia";
            foreach (CombatSkillKit requested in System.Enum.GetValues(typeof(CombatSkillKit)))
            {
                Assert.That(Resolve(catalog, character, false, requested, out _), Is.False);
                Assert.That(Resolve(catalog, null, false, requested, out _), Is.False);
                Assert.That(Resolve(null, character, false, requested, out _), Is.False);
                Assert.That(catalog.IsCharacter(character, requested), Is.False);
                Assert.That(catalog.IsCharacter(null, requested), Is.False);
            }
            Assert.That(catalog.IsCharacter(character, (CombatSkillKit)int.MaxValue), Is.False);
        }
        finally { Object.DestroyImmediate(character); }
    }

    [Test]
    public void HeldDirectionSurvivesMapDisableButPhysicalReleaseAllowsSameDirection()
    {
        using var fixture = new SelectionFixture();
        fixture.Combat.Enable();
        fixture.Push(new GamepadState().WithButton(GamepadButton.DpadUp));
        Assert.That(fixture.Accepted, Is.EqualTo(new[] { CombatSkillKit.Lucian }));

        fixture.Combat.Disable();
        Assert.That(fixture.CanceledCount, Is.EqualTo(1), "Map cancellation must actually reach the latch.");
        fixture.Wheel.Enable();
        fixture.Flush();
        Assert.That(fixture.PerformedCount, Is.EqualTo(2), "The destination map must replay the held state.");
        Assert.That(fixture.Accepted.Count, Is.EqualTo(1), "Map cancellation is not a physical release.");

        fixture.Push(new GamepadState().WithButton(GamepadButton.DpadRight));
        Assert.That(fixture.Accepted, Is.EqualTo(new[] { CombatSkillKit.Lucian, CombatSkillKit.Vesta }),
            "A different direction is a new slot request.");
        fixture.Flush();
        Assert.That(fixture.Accepted.Count, Is.EqualTo(2), "Holding must not repeat a slot.");
        fixture.Push(new GamepadState());
        Assert.That(fixture.CanceledCount, Is.EqualTo(2), "Physical release must deliver an action cancellation.");
        Assert.That(fixture.Pad.dpad.ReadValue(), Is.EqualTo(Vector2.zero));
        fixture.Push(new GamepadState().WithButton(GamepadButton.DpadRight));
        Assert.That(fixture.Accepted, Is.EqualTo(new[] { CombatSkillKit.Lucian, CombatSkillKit.Vesta, CombatSkillKit.Vesta }),
            "Physical release permits another press.");
    }

    [Test]
    public void PhysicalReleaseAndRepressInOneInputUpdateAllowsSameDirection()
    {
        using var fixture = new SelectionFixture();
        fixture.Combat.Enable();
        fixture.Push(new GamepadState().WithButton(GamepadButton.DpadUp));
        InputSystem.QueueStateEvent(fixture.Pad, new GamepadState());
        InputSystem.QueueStateEvent(fixture.Pad, new GamepadState().WithButton(GamepadButton.DpadUp));
        fixture.Flush();
        Assert.That(fixture.CanceledCount, Is.EqualTo(1));
        Assert.That(fixture.Accepted, Is.EqualTo(new[] { CombatSkillKit.Lucian, CombatSkillKit.Lucian }),
            "A real neutral event releases the latch without waiting for MonoBehaviour.Update.");
    }

    [Test]
    public void PhysicalReleaseWhileBothMapsAreDisabledIsObservedByPolling()
    {
        using var fixture = new SelectionFixture();
        fixture.Combat.Enable();
        fixture.Push(new GamepadState().WithButton(GamepadButton.DpadLeft));
        fixture.Combat.Disable();
        fixture.Push(new GamepadState());
        fixture.PollRelease();
        fixture.Push(new GamepadState().WithButton(GamepadButton.DpadLeft));
        fixture.Wheel.Enable();
        fixture.Flush();
        Assert.That(fixture.Accepted, Is.EqualTo(new[] { CombatSkillKit.Aurell, CombatSkillKit.Aurell }));
    }

    [Test]
    public void AnotherControlsCancellationCannotReleaseHeldSelection()
    {
        using var fixture = new SelectionFixture();
        var otherPad = InputSystem.AddDevice<Gamepad>();
        using var otherMap = new InputActionMap("Other device selection");
        var other = otherMap.AddAction("SelectSkillKit", InputActionType.Value, "<Gamepad>/dpad");
        otherMap.devices = new InputDevice[] { otherPad };
        int otherCanceled = 0;
        other.canceled += context => { otherCanceled++; fixture.Release(context); };
        try
        {
            fixture.Combat.Enable();
            fixture.Push(new GamepadState().WithButton(GamepadButton.DpadDown));
            otherMap.Enable();
            InputSystem.QueueStateEvent(otherPad, new GamepadState().WithButton(GamepadButton.DpadUp));
            fixture.Flush();
            InputSystem.QueueStateEvent(otherPad, new GamepadState());
            fixture.Flush();
            Assert.That(otherCanceled, Is.EqualTo(1));
            fixture.Combat.Disable();
            fixture.Wheel.Enable();
            fixture.Flush();
            Assert.That(fixture.Accepted, Is.EqualTo(new[] { CombatSkillKit.Soren }));
        }
        finally
        {
            otherMap.Disable();
            InputSystem.RemoveDevice(otherPad);
        }
    }

    [Test]
    public void DisconnectedSelectionControlIsNotReadOrConsumed()
    {
        using var fixture = new SelectionFixture();
        fixture.Combat.Enable();
        fixture.Push(new GamepadState().WithButton(GamepadButton.DpadUp));
        fixture.Combat.Disable();
        InputSystem.RemoveDevice(fixture.Pad);
        Assert.DoesNotThrow(fixture.PollRelease);
        Assert.That(fixture.Consume(fixture.Pad.dpad, CombatSkillKit.Lucian), Is.False);
        Assert.That(typeof(CombatSkillKitInput).GetField("heldSelectControl", PrivateInstance).GetValue(fixture.Input), Is.Null);
    }

    [Test]
    public void SelectionFixtureRestoresOriginalSettingsObjectAndFlags()
    {
        var original = InputSystem.settings;
        var flags = original.hideFlags;
        using (var fixture = new SelectionFixture())
        {
            Assert.That(original != null, Is.True, "Replacing settings must not destroy the original temporary object.");
            Assert.That(InputSystem.settings, Is.Not.SameAs(original));
        }
        Assert.That(InputSystem.settings, Is.SameAs(original));
        Assert.That(original.hideFlags, Is.EqualTo(flags));
    }

    private sealed class SelectionFixture : System.IDisposable
    {
        private readonly InputSettings previousSettings;
        private readonly HideFlags previousSettingsFlags;
        private bool disposed;
        private InputSettings settings;
        private GameObject root;
        public CombatSkillKitInput Input { get; private set; }
        public Gamepad Pad { get; private set; }
        public InputActionMap Combat { get; private set; }
        public InputActionMap Wheel { get; private set; }
        public readonly List<CombatSkillKit> Accepted = new List<CombatSkillKit>();
        public int PerformedCount { get; private set; }
        public int CanceledCount { get; private set; }

        public SelectionFixture()
        {
            previousSettings = InputSystem.settings;
            previousSettingsFlags = previousSettings.hideFlags;
            try
            {
                // Edit Mode defaults to Editor updates, where action state monitors intentionally do not run.
                // Use temporary in-memory settings so these queued events exercise real gameplay callbacks.
                settings = Object.Instantiate(previousSettings);
                settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
                settings.SetInternalFeatureFlag("RUN_PLAYER_UPDATES_IN_EDIT_MODE", true);
                // InputManager destroys HideAndDontSave settings when replacing them.
                // Preserve the original object, not just its values, until it is restored.
                if (previousSettingsFlags == HideFlags.HideAndDontSave) previousSettings.hideFlags = HideFlags.None;
                InputSystem.settings = settings;
                root = new GameObject("Kit input latch test");
                root.SetActive(false);
                Input = root.AddComponent<CombatSkillKitInput>();
                Pad = InputSystem.AddDevice<Gamepad>();
                Combat = CreateMap("RealTimeCombat");
                Wheel = CreateMap("CombatWheel");
            }
            catch { Dispose(); throw; }
        }

        private InputActionMap CreateMap(string name)
        {
            var map = new InputActionMap(name);
            map.devices = new InputDevice[] { Pad };
            var action = map.AddAction("SelectSkillKit", InputActionType.Value, "<Gamepad>/dpad");
            action.wantsInitialStateCheck = true;
            action.performed += context =>
            {
                PerformedCount++;
                if (CombatSkillKitCatalog.TryDirection(context.ReadValue<Vector2>(), out var direction) &&
                    Consume(context.control, direction)) Accepted.Add(direction);
            };
            action.canceled += context => { CanceledCount++; Release(context); };
            return map;
        }

        public bool Consume(InputControl control, CombatSkillKit direction) =>
            (bool)typeof(CombatSkillKitInput).GetMethod("TryConsumeSelection", PrivateInstance)
                .Invoke(Input, new object[] { control, direction });

        public void Release(InputAction.CallbackContext context) => typeof(CombatSkillKitInput)
            .GetMethod("OnSelectionReleased", PrivateInstance).Invoke(Input, new object[] { context });

        public void PollRelease() => typeof(CombatSkillKitInput)
            .GetMethod("RefreshSelectionRelease", PrivateInstance).Invoke(Input, null);

        public void Push(GamepadState state)
        {
            InputSystem.QueueStateEvent(Pad, state);
            Flush();
        }

        public void Flush()
        {
            InputSystem.Update();
            Assert.That(InputState.currentUpdateType, Is.EqualTo(InputUpdateType.Manual),
                "The fixture must process player actions, not just update Editor device buffers.");
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try
            {
                Combat?.Dispose();
                Wheel?.Dispose();
                if (Pad != null && Pad.added) InputSystem.RemoveDevice(Pad);
                if (root != null) Object.DestroyImmediate(root);
            }
            finally
            {
                try { InputSystem.settings = previousSettings; }
                finally
                {
                    if (previousSettings != null) previousSettings.hideFlags = previousSettingsFlags;
                    if (settings != null) Object.DestroyImmediate(settings);
                }
            }
        }
    }
}
