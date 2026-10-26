#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

public sealed class AbilitySystemTests
{
    [Test]
    public void AuthoringPersistenceAndSingleSlotContract() => Assert.DoesNotThrow(() => AbilitySystemValidation.Validate());

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
