using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;

public sealed class TimeAuthorityAuditTests
{
    private static Regex AuditPattern => (Regex)typeof(TimeAuthorityAudit)
        .GetField("IllegalWrite", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

    [TestCase("timeScale = 0f;")]
    [TestCase("fixedDeltaTime *= .5f;")]
    [TestCase("timeScale += .1f;")]
    [TestCase("fixedDeltaTime -= .01f;")]
    [TestCase("timeScale /= 2f;")]
    public void RejectsAssignments(string expression)
    {
        Assert.That(AuditPattern.IsMatch("Time." + expression), Is.True);
    }

    [TestCase("timeScale == 0f")]
    [TestCase("fixedDeltaTime != .02f")]
    [TestCase("timeScale <= 1f")]
    [TestCase("fixedDeltaTime >= .01f")]
    public void AllowsComparisons(string expression)
    {
        Assert.That(AuditPattern.IsMatch("Time." + expression), Is.False);
    }
}
