using NUnit.Framework;

public sealed class RealTimeCombatClarityTests
{
    [TestCase(0f, CombatClarityRank.E)]
    [TestCase(19.99f, CombatClarityRank.E)]
    [TestCase(20f, CombatClarityRank.D)]
    [TestCase(40f, CombatClarityRank.C)]
    [TestCase(60f, CombatClarityRank.B)]
    [TestCase(80f, CombatClarityRank.A)]
    [TestCase(99.99f, CombatClarityRank.A)]
    [TestCase(100f, CombatClarityRank.S)]
    [TestCase(120f, CombatClarityRank.S)]
    public void ResolveClarityRankUsesConfiguredBoundaries(float clarity, CombatClarityRank expectedRank)
    {
        Assert.That(RealTimeCombatManager.ResolveClarityRank(clarity, 100f), Is.EqualTo(expectedRank));
    }

    [TestCase(LightSkillClarityTier.E, 0.20f)]
    [TestCase(LightSkillClarityTier.D, 0.40f)]
    [TestCase(LightSkillClarityTier.C, 0.60f)]
    [TestCase(LightSkillClarityTier.B, 0.80f)]
    [TestCase(LightSkillClarityTier.A, 1f)]
    [TestCase(LightSkillClarityTier.S, 1.20f)]
    public void LightSkillTierUsesExpectedClarityMultiplier(LightSkillClarityTier tier, float expectedMultiplier)
    {
        Assert.That(RealTimeCombatManager.GetLightSkillClarityMultiplier(tier), Is.EqualTo(expectedMultiplier));
    }

    [Test]
    public void LegacyLightChargeOnHitMigratesToClarityGainOnHit()
    {
        Assert.That(typeof(SkillSO).GetField("clarityGainOnHit") != null, Is.True);
        Assert.That(typeof(SkillSO).GetProperty("ClarityGainOnHit") != null, Is.True);
    }
}
