#if UNITY_INCLUDE_TESTS
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public sealed class LucianAirComboTests
{
    [Test]
    public void LaboratoryEquipsThreeDistinctAerialBasicsWithoutChangingProduction()
    {
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>(GroundImpactSkillSetup.DataPath);
        Assert.That(data.airBasicSkills.Count, Is.EqualTo(3));
        Assert.That(data.airBasicSkills.Distinct().Count(), Is.EqualTo(3));
        Assert.That(data.airBasicSkills.Select(s => s.AnimationClip).Distinct().Count(), Is.EqualTo(3));
        Assert.That(AssetDatabase.GetAssetPath(data.airBasicSkills[0]), Is.EqualTo(LucianAirComboSetup.FirstPath));
        foreach (var guid in AssetDatabase.FindAssets("t:CharacterData"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (path == GroundImpactSkillSetup.DataPath) continue;
            var other = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            Assert.That(other.airBasicSkills == null || !other.airBasicSkills.Any(s => s != null &&
                AssetDatabase.GetAssetPath(s).StartsWith(LucianAirComboSetup.Folder + "/")), Is.True, path);
        }
    }

    [TestCase(2)] [TestCase(3)]
    public void NewAttackHasOneImpactBeforeChainAndItsOwnAnimatorState(int index)
    {
        var skill = AssetDatabase.LoadAssetAtPath<BasicSkillsSO>(LucianAirComboSetup.Folder + "/AirBasicSkill_" + index + ".asset");
        Assert.That(skill, Is.Not.Null);
        Assert.That(skill.Context, Is.EqualTo(BasicSkillContext.Airborne));
        Assert.That(skill.targetingMode, Is.EqualTo(SkillTargetingMode.FreeAttack));
        Assert.That(skill.RequestsLandingDuringAnimation, Is.True);
        Assert.That(skill.HoldsAirborneDuringAnimation, Is.False);
        var events = AnimationUtility.GetAnimationEvents(skill.AnimationClip);
        var impacts = events.Where(e => e.functionName == "ResolveSkillImpact" || e.functionName == "HitEnemy").ToArray();
        Assert.That(impacts.Length, Is.EqualTo(1));
        Assert.That(impacts[0].time, Is.LessThan(skill.AnimationClip.length * skill.presentation.chainNormalizedTime));
        Assert.That(events.Count(e => e.functionName == "InstantiateSkillVFXAtIndex"), Is.EqualTo(1));
        Assert.That(AnimationUtility.GetAnimationClipSettings(skill.AnimationClip).loopTime, Is.False);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(GroundImpactSkillSetup.ControllerPath);
        var state = controller.layers[0].stateMachine.states.Single(s => s.state.name == "AirBasicSkill_" + index).state;
        Assert.That(state.motion, Is.EqualTo(skill.AnimationClip));
    }

    [Test]
    public void ReinstallDoesNotRewriteAuthoredAssets()
    {
        var paths = AssetDatabase.FindAssets("", new[] { LucianAirComboSetup.Folder })
            .Select(AssetDatabase.GUIDToAssetPath).Concat(new[] { GroundImpactSkillSetup.DataPath, GroundImpactSkillSetup.ControllerPath }).ToArray();
        var before = paths.Select(System.IO.File.ReadAllBytes).ToArray();
        LucianAirComboSetup.Configure();
        for (int i = 0; i < paths.Length; i++) Assert.That(System.IO.File.ReadAllBytes(paths[i]), Is.EqualTo(before[i]), paths[i]);
    }

    [TestCase(2)] [TestCase(3)]
    public void AerialPosesComeFromTwinbladesWithoutGroundSwingOrLegReplacement(int index)
    {
        var source = ActorAnimationAudit.Source(LucianAirComboSetup.AerialSourcePath(index));
        var skill = AssetDatabase.LoadAssetAtPath<BasicSkillsSO>(LucianAirComboSetup.Folder + "/AirBasicSkill_" + index + ".asset");
        Assert.That(skill.AnimationClip.isHumanMotion, Is.True);
        Assert.That(skill.AnimationClip.length, Is.EqualTo(source.length).Within(.0001f));
        Assert.That(AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(skill.AnimationClip)).userData,
            Does.EndWith(LucianAirComboSetup.AerialSourcePath(index)));
        using (var sampler = new ActorPoseSampler(ActorAnimationAudit.Actor(false)))
        {
            var expected = sampler.Sample(source, sampleCount: 8);
            var actual = sampler.Sample(skill.AnimationClip, sampleCount: 8);
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (int frame = 0; frame < expected.Length; frame++)
                for (int bone = 0; bone < expected[frame].bonePositions.Length; bone++)
                    Assert.That(Vector3.Distance(actual[frame].bonePositions[bone], expected[frame].bonePositions[bone]),
                        Is.LessThan(.002f), "frame=" + frame + " bone=" + bone);
        }
    }
}
#endif
