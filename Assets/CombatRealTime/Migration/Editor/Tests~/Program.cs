using System;
using System.Linq;

internal static class Program
{
    private static int Main()
    {
        int passed = 0, failed = 0;
        foreach (var test in CombatReadinessEvidenceChecks.Cases().Concat(CombatAuditScopeChecks.Cases()).Concat(CombatInventoryChecks.Cases()))
        {
            try { test.Value(); passed++; Console.WriteLine("PASS " + test.Key); }
            catch (Exception exception) { failed++; Console.WriteLine("FAIL " + test.Key + ": " + exception); }
        }
        Console.WriteLine("Synthetic readiness-validator fixtures only: " + passed + " passed, " + failed + " failed. No Unity/readiness evidence generated.");
        return failed == 0 ? 0 : 1;
    }
}
