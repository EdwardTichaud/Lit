using System;
using System.IO;

internal static class DiagnosticCleanupProgram
{
    private static int Main()
    {
        int passed = 0, failed = 0;
        foreach (var test in CombatDiagnosticCleanupChecks.Cases())
        {
            try { test.Value(); passed++; Console.WriteLine("PASS " + test.Key); }
            catch (Exception exception) { failed++; Console.WriteLine("FAIL " + test.Key + ": " + exception); }
        }
        try
        {
            int controllers = 0;
            foreach (string detail in CombatDiagnosticCleanupChecks.InspectControllers(Path.GetFullPath("."))) { controllers++; Console.WriteLine("READ-ONLY " + detail); }
            Console.WriteLine("Read-only controller plans: " + controllers + ". No controller or production asset written.");
        }
        catch (Exception exception) { failed++; Console.WriteLine("FAIL real controller preflight: " + exception); }
        Console.WriteLine("Diagnostic cleanup checks: " + passed + " passed, " + failed + " failed. Not Unity execution/readiness evidence.");
        return failed == 0 ? 0 : 1;
    }
}
