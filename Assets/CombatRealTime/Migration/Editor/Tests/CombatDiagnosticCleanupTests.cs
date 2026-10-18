#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Linq;
using NUnit.Framework;

public sealed class CombatDiagnosticCleanupTests
{
    public static IEnumerable Cases()
    {
        foreach (var test in CombatDiagnosticCleanupChecks.Cases()) yield return new TestCaseData(test.Value).SetName(test.Key);
    }
    [TestCaseSource(nameof(Cases))]
    public void PlansOnlyProvenDiagnosticRemoval(System.Action test) => test();

    [Test]
    public void CurrentControllersAreReadOnlyAndPreserveStatesAndMissingMotions()
    {
        Assert.That(System.IO.Directory.GetFiles("Assets", "*.controller", System.IO.SearchOption.AllDirectories).Length, Is.GreaterThan(0));
        Assert.DoesNotThrow(() => CombatDiagnosticCleanupChecks.InspectControllers(System.IO.Path.GetFullPath(".")).ToArray());
    }

    [Test]
    public void ApplyRejectsMainCheckoutBeforeReadingPlanOrWritingAssets()
    {
        string root = System.IO.Path.GetFullPath(".");
        Assert.Throws<System.InvalidOperationException>(() => CombatDiagnosticCleanupBatch.ApplyApproved(root, root, "nonexistent-plan.json", "unapproved"));
    }
}
#endif
