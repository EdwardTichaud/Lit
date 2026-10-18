#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

public sealed class CombatRosterIdentityTests
{
    [Test]
    public void RuntimeIdentityDoesNotDependOnDisplayName()
    {
        var catalog = Resources.Load<CombatSkillKitCatalog>("CombatSkillKits/Default");
        Assert.That(catalog, Is.Not.Null);
        var lucian = Object.Instantiate(catalog.GetCharacter(CombatSkillKit.Lucian));
        var npc = ScriptableObject.CreateInstance<CharacterData>();
        try
        {
            lucian.name = "Runtime clone";
            lucian.characterName = "Different localized name";
            Assert.That(catalog.IsCharacter(lucian, CombatSkillKit.Lucian), Is.True);
            Assert.That(catalog.IsCharacter(lucian, CombatSkillKit.Vesta), Is.False);
            npc.characterName = "Lucian";
            Assert.That(catalog.IsCharacter(npc, CombatSkillKit.Lucian), Is.False);
            Assert.That(catalog.IsCharacter(null, CombatSkillKit.Lucian), Is.False);
        }
        finally { Object.DestroyImmediate(lucian); Object.DestroyImmediate(npc); }
    }

    [Test]
    public void DamageReactionUsesActorDataAndEmptyStateRemainsDisabled()
    {
        var root = new GameObject("Authored presentation test");
        root.SetActive(false);
        var data = ScriptableObject.CreateInstance<CharacterData>();
        try
        {
            var actor = root.AddComponent<SquadCharacterController>();
            typeof(SquadCharacterController).GetField("characterData", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).SetValue(actor, data);
            data.playerSettings.combatPresentation.damageReactionAnimatorState = "DifferentAvatar.Hurt";
            Assert.That(CombatActorPresentation.DamageReactionState(actor), Is.EqualTo("DifferentAvatar.Hurt"));
            data.playerSettings.combatPresentation.damageReactionAnimatorState = "";
            Assert.That(CombatActorPresentation.DamageReactionState(actor), Is.Empty);
            Assert.That(CombatActorPresentation.TryDamageReaction(actor), Is.False);
            Assert.That(CombatActorPresentation.TryDamageReaction(null), Is.False);
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(data); }
    }
}
#endif
