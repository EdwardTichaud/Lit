#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

public sealed class AbilitySystemTests
{
    [Test]
    public void AuthoringPersistenceAndSingleSlotContract() => Assert.DoesNotThrow(() => AbilitySystemValidation.Validate());

    [Test]
    public void SessionPrefabAuthorsOneAbilityServiceOnAbilitiesManager()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(
            "Assets/Core/System/GameplaySessionRoot.prefab");
        Assert.That(prefab, Is.Not.Null);
        var manager = prefab.transform.Find("AbilitiesManager");
        Assert.That(manager, Is.Not.Null);
        var services = prefab.GetComponentsInChildren<AbilityService>(true);
        Assert.That(services.Length, Is.EqualTo(1));
        Assert.That(services[0].transform, Is.EqualTo(manager));
        Assert.That(manager.gameObject.activeSelf && services[0].enabled, Is.True);
    }

    [Test]
    public void AuthoredTestDefaultsInitializeSeparatelyFromSavedAbilities()
    {
        Assert.That(AbilityService.Instance, Is.Null);
        var root = new UnityEngine.GameObject("Ability defaults fixture");
        root.SetActive(false);
        try
        {
            var service = root.AddComponent<AbilityService>();
            var climber = UnityEditor.AssetDatabase.LoadAssetAtPath<AbilitySO>("Assets/Resources/Abilities/Ability_Grimpeur.asset");
            Assert.That(climber, Is.Not.Null);
            var properties = new UnityEditor.SerializedObject(service);
            var defaults = properties.FindProperty("editorTestDefaults");
            defaults.arraySize = 1;
            defaults.GetArrayElementAtIndex(0).objectReferenceValue = climber;
            properties.ApplyModifiedPropertiesWithoutUndo();

            const System.Reflection.BindingFlags member = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(AbilityService).GetMethod("Initialize", member).Invoke(service, null);
            var active = (System.Collections.Generic.HashSet<AbilitySO>)typeof(AbilityService)
                .GetField("editorTestAbilities", member).GetValue(service);
            Assert.That(active, Does.Contain(climber));
            Assert.That(service.CanEditTestAbilities, Is.False, "Edit-time configuration is not live equipment.");
            Assert.That(service.Capture().IsUnlocked(climber.abilityId), Is.False);
            active.Clear();
            properties.Update();
            Assert.That(defaults.GetArrayElementAtIndex(0).objectReferenceValue, Is.EqualTo(climber));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void RejectedEquipDoesNotReplaceCurrentAbility()
    {
        var model = new AbilitySaveData(); model.Unlock("observateur"); model.Equip("alice", "observateur");
        Assert.That(model.Equip("alice", "sauf_conduit"), Is.False);
        Assert.That(model.Equipped("alice"), Is.EqualTo("observateur"));
    }

    [Test]
    public void EmptySlotDoesNotBorrowAnotherPlayersAbility()
    {
        var model = new AbilitySaveData(); model.Unlock("sauf_conduit"); model.Equip("alice", "sauf_conduit");
        Assert.That(model.Equipped("bob"), Is.Empty);
    }

    [Test]
    public void AbilityMenuCannotOpenWithoutAnOpenPauseOwner()
    {
        var root = new UnityEngine.GameObject("Ability pause test");
        root.SetActive(false);
        try
        {
            var panel = root.AddComponent<AbilitySelectionPanel>();
            Assert.That(panel.OpenFromPause(null), Is.False);
            Assert.That(panel.IsOpen, Is.False);
            Assert.That(root.transform.childCount, Is.Zero);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void ClosingAbilityMenuIsIdempotentAndDoesNotResumeTime()
    {
        var root = new UnityEngine.GameObject("Ability close test");
        root.SetActive(false);
        float originalTimeScale = UnityEngine.Time.timeScale;
        try
        {
            var panel = root.AddComponent<AbilitySelectionPanel>();
            typeof(AbilitySelectionPanel).GetField("open", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).SetValue(panel, true);
            int closed = 0;
            panel.Closed += () => closed++;
            UnityEngine.Time.timeScale = 0f;
            panel.Close();
            panel.Close();
            Assert.That(closed, Is.EqualTo(1));
            Assert.That(panel.IsOpen, Is.False);
            Assert.That(UnityEngine.Time.timeScale, Is.Zero);
        }
        finally
        {
            UnityEngine.Time.timeScale = originalTimeScale;
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
#endif
