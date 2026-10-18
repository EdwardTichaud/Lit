using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class SoloTeamProgressionTests
{
    private GameObject root;
    private SoloTeamProgression team;
    [SetUp] public void SetUp() { root = new GameObject("Solo team test"); team = root.AddComponent<SoloTeamProgression>(); team.InitializeNewGame(); }
    [TearDown] public void TearDown() => Object.DestroyImmediate(root);

    [Test] public void NewGameHasOnlyLucianAndThreeSkills()
    {
        Assert.That(team.GetRoster().Count, Is.EqualTo(1));
        Assert.That(team.IsRecruited(CombatSkillKit.Lucian), Is.True);
        Assert.That(team.GetAvailableSkills(CombatSkillKit.Lucian)[3], Is.Null);
        foreach (var kit in new[] { CombatSkillKit.Vesta, CombatSkillKit.Soren, CombatSkillKit.Aurell })
        { Assert.That(team.IsRecruited(kit), Is.False); Assert.That(team.GetAvailableSkills(kit), Is.All.Null); }
    }

    [TestCase(CombatSkillKit.Vesta)] [TestCase(CombatSkillKit.Soren)] [TestCase(CombatSkillKit.Aurell)]
    public void RecruitmentGrantsOnlyFirstSlotAndEmitsOnce(CombatSkillKit kit)
    {
        int events = 0; team.MemberRecruited += _ => events++;
        Assert.That(team.RecruitMember(kit), Is.True);
        Assert.That(team.RecruitMember(kit), Is.False);
        Assert.That(events, Is.EqualTo(1));
        Assert.That(team.GetAvailableSkills(kit)[0], Is.SameAs(team.Catalog.GetSkill(kit, 0)));
        for (int i = 1; i < 4; i++) Assert.That(team.GetAvailableSkills(kit)[i], Is.Null);
        Assert.That(root.GetComponentsInChildren<SquadCharacterController>(), Is.Empty);
    }

    [Test] public void UnlockKeepsFixedSlotsAndRequiresRecruitment()
    {
        Assert.That(team.UnlockSkill(CombatSkillKit.Soren, 2), Is.False);
        team.RecruitMember(CombatSkillKit.Soren);
        Assert.That(team.UnlockSkill(CombatSkillKit.Soren, 2), Is.True);
        Assert.That(team.UnlockSkill(CombatSkillKit.Soren, 2), Is.False);
        Assert.That(team.GetAvailableSkills(CombatSkillKit.Soren)[1], Is.Null);
        Assert.That(team.GetAvailableSkills(CombatSkillKit.Soren)[2], Is.Not.Null);
        Assert.That(team.UnlockSkill(CombatSkillKit.Lucian, 3), Is.True);
        Assert.That(team.UnlockSkill(CombatSkillKit.Soren, -1), Is.False);
    }

    [Test] public void SaveRoundTripReplacesPreviousSessionWithoutReplayingRecruitment()
    {
        team.RecruitMember(CombatSkillKit.Vesta); team.UnlockSkill(CombatSkillKit.Vesta, 3);
        var saved = JsonUtility.FromJson<SoloTeamSaveData>(JsonUtility.ToJson(team.Capture()));
        team.RecruitMember(CombatSkillKit.Aurell);
        int notifications = 0; team.MemberRecruited += _ => notifications++;
        team.Restore(saved);
        Assert.That(team.IsUnlocked(CombatSkillKit.Vesta, 3), Is.True);
        Assert.That(team.IsRecruited(CombatSkillKit.Aurell), Is.False);
        Assert.That(notifications, Is.Zero);
        team.InitializeNewGame();
        Assert.That(team.GetRoster().Count, Is.EqualTo(1));
    }

    [Test] public void LegacyRosterNeverConvertsOldCharacters()
    {
        team.Restore(null, new List<string> { "lucian", "mia", "luna", "link", "Soren" });
        Assert.That(team.IsRecruited(CombatSkillKit.Soren), Is.True);
        Assert.That(team.IsUnlocked(CombatSkillKit.Soren, 0), Is.True);
        Assert.That(team.IsUnlocked(CombatSkillKit.Soren, 1), Is.False);
        Assert.That(team.IsRecruited(CombatSkillKit.Vesta), Is.False);
        Assert.That(team.IsRecruited(CombatSkillKit.Aurell), Is.False);
    }

    [Test] public void LegacyAssetGuidRosterRestoresOnlyMatchingSupports()
    {
        team.Restore(null, new List<string> { team.Catalog.GetCharacter(CombatSkillKit.Aurell).UniqueId });
        Assert.That(team.IsRecruited(CombatSkillKit.Aurell), Is.True);
        Assert.That(team.IsUnlocked(CombatSkillKit.Aurell, 0), Is.True);
        Assert.That(team.IsUnlocked(CombatSkillKit.Aurell, 3), Is.False);
        Assert.That(team.IsRecruited(CombatSkillKit.Vesta), Is.False);
    }

    [Test] public void SnapshotDoesNotAliasMutableStateAndCatalogDoesNotChange()
    {
        var before = team.Catalog.GetSkill(CombatSkillKit.Aurell, 3);
        var vestaPrefab = team.Catalog.GetCharacter(CombatSkillKit.Vesta).worldPrefab;
        var snapshot = team.Capture();
        team.RecruitMember(CombatSkillKit.Aurell);
        Assert.That(snapshot.members.Find(m => m.kit == CombatSkillKit.Aurell).recruited, Is.False);
        Assert.That(team.Catalog.GetSkill(CombatSkillKit.Aurell, 3), Is.SameAs(before));
        Assert.That(team.IsUnlocked(CombatSkillKit.Aurell, 3), Is.False);
        Assert.That(team.Catalog.GetCharacter(CombatSkillKit.Vesta).worldPrefab, Is.SameAs(vestaPrefab));
    }

    [Test] public void SoloSpawnAndHubSwapCannotMaterializeRecruits()
    {
        var squad = root.AddComponent<SquadManager>();
        var spawn = typeof(SquadManager).GetMethod("SpawnCharacterAt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        foreach (var kit in new[] { CombatSkillKit.Vesta, CombatSkillKit.Soren, CombatSkillKit.Aurell })
        {
            team.RecruitMember(kit);
            Assert.That(spawn.Invoke(squad, new object[] { team.Catalog.GetCharacter(kit), Vector3.zero, Quaternion.identity, false }), Is.Null);
            Assert.That(squad.TrySwapWithHubCharacter(team.Catalog.GetCharacter(kit)), Is.False);
        }
        Assert.That(root.GetComponentsInChildren<SquadCharacterController>(), Is.Empty);
    }
}
