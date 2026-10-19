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
}
#endif
