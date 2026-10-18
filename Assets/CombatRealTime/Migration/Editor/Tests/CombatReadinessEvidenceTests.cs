#if UNITY_INCLUDE_TESTS
using System.Collections;
using NUnit.Framework;

public sealed class CombatReadinessEvidenceTests
{
    public static IEnumerable Cases()
    {
        foreach (var test in CombatReadinessEvidenceChecks.Cases()) yield return new TestCaseData(test.Value).SetName(test.Key);
        foreach (var test in CombatAuditScopeChecks.Cases()) yield return new TestCaseData(test.Value).SetName(test.Key);
    }

    [TestCaseSource(nameof(Cases))]
    public void ValidatesEvidenceWithoutUnityOrAssetWrites(System.Action test) => test();
}
#endif
