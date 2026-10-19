#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

public sealed class CombatContactAudioTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void ConfirmedContactContextIsRestoredForLocalAndExternalSessions(bool external)
    {
        var root = new GameObject("Confirmed audio context fixture");
        root.SetActive(false);
        var skill = ScriptableObject.CreateInstance<SkillSO>();
        var previous = ScriptableObject.CreateInstance<SkillSO>();
        try
        {
            var presentation = root.AddComponent<PlayerActionPresentationController>();
            const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(PlayerActionPresentationController).GetField("externallyDriven", fields).SetValue(presentation, external);
            typeof(PlayerActionPresentationController).GetField("confirmedContactSkill", fields).SetValue(presentation, previous);
            typeof(PlayerActionPresentationController).GetField("confirmedContactGeneration", fields).SetValue(presentation, 17);
            presentation.PlayConfirmedSkillContactVfx(skill, root.transform, Vector3.one, 42);
            Assert.That(typeof(PlayerActionPresentationController).GetField("confirmedContactSkill", fields).GetValue(presentation), Is.SameAs(previous));
            Assert.That(typeof(PlayerActionPresentationController).GetField("confirmedContactGeneration", fields).GetValue(presentation), Is.EqualTo(17));
            Assert.That(typeof(PlayerActionPresentationController).GetField("resolvingExternalImpactVfx", fields).GetValue(presentation), Is.False);
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(skill); Object.DestroyImmediate(previous); }
    }

    [Test]
    public void HeavyContactsAreLowerAndVariationIsStablePerAction()
    {
        var root = new GameObject("Contact pitch fixture");
        try
        {
            var audio = root.AddComponent<CombatContactAudio>();
            bool varied = false;
            float first = audio.ResolvePitch(5, 1);
            for (int generation = 1; generation <= 32; generation++)
            {
                float light = audio.ResolvePitch(5, generation), heavy = audio.ResolvePitch(15, generation);
                Assert.That(light, Is.InRange(1.02f, 1.08f));
                Assert.That(heavy, Is.InRange(.88f, .94f));
                Assert.That(heavy, Is.LessThan(light));
                Assert.That(audio.ResolvePitch(5, generation), Is.EqualTo(light));
                varied |= Mathf.Abs(light - first) > .001f;
            }
            Assert.That(varied, Is.True);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void CosmeticVariationDoesNotChangeGameplayRandomness()
    {
        var root = new GameObject("Contact random fixture");
        var original = Random.state;
        try
        {
            var audio = root.AddComponent<CombatContactAudio>();
            Random.InitState(193);
            var before = Random.state;
            float expected = Random.value;
            Random.state = before;
            audio.ResolvePitch(10, 7);
            Assert.That(Random.value, Is.EqualTo(expected));
        }
        finally { Random.state = original; Object.DestroyImmediate(root); }
    }

    [Test]
    public void ChargedLaunchUsesHeavyRegisterEvenWithTenDamage()
    {
        var root = new GameObject("Charged contact pitch fixture");
        var skill = ScriptableObject.CreateInstance<ChargedSkillSO>();
        try
        {
            var audio = root.AddComponent<CombatContactAudio>();
            for (int generation = 1; generation <= 8; generation++)
                Assert.That(audio.ResolvePitch(skill, generation), Is.InRange(.88f, .94f));
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(skill); }
    }

    [Test]
    public void MissingClipOrDisabledPresentationCannotClaimPlayback()
    {
        var root = new GameObject("Contact missing clip fixture");
        var clip = ScriptableObject.CreateInstance<AudioClipSO>();
        var skill = ScriptableObject.CreateInstance<SkillSO>();
        try
        {
            var audio = root.AddComponent<CombatContactAudio>();
            Assert.That(audio.TryPlay(null, Vector3.zero, skill, 1), Is.False);
            Assert.That(audio.TryPlay(clip, Vector3.zero, skill, 1), Is.False);
            audio.enabled = false;
            Assert.That(audio.TryPlay(clip, Vector3.zero, skill, 1), Is.False);
            Assert.That(audio.ContactSoundsPlayed, Is.Zero);
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(clip); Object.DestroyImmediate(skill); }
    }
}
#endif
