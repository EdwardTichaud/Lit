using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

/// <summary>Synthetic validator fixtures only; their artifacts are NOT migration/readiness evidence.</summary>
public static class CombatReadinessEvidenceChecks
{
    public static IEnumerable<KeyValuePair<string, Action>> Cases()
    {
        yield return Case("All gates pending without evidence", () => WithFixture(f =>
        {
            var result = Evaluate(f, Array.Empty<CombatReadinessEvidence.Receipt>());
            Check(!result.ReadyForProduction && result.gates.Count == 11 && result.gates.All(g => !g.passed && g.status == "pending"));
        }));
        yield return Case("Revision mismatch invalidates", () => WithFixture(f => { f.Receipt.revision = "different"; Invalid(f, "mismatch"); }));
        yield return Case("Fingerprint mismatch invalidates", () => WithFixture(f => { f.Receipt.fingerprint = "different"; Invalid(f, "mismatch"); }));
        yield return Case("Artifact tampering invalidates", () => WithFixture(f => { File.AppendAllText(f.ArtifactPath, "changed"); Invalid(f, "hash mismatch"); }));
        yield return Case("Missing artifact invalidates", () => WithFixture(f => { File.Delete(f.ArtifactPath); Invalid(f, "missing/empty"); }));
        yield return Case("Empty artifact invalidates", () => WithFixture(f => { File.WriteAllText(f.ArtifactPath, ""); Invalid(f, "missing/empty"); }));
        yield return Case("Unknown schemas rejected", () => WithFixture(f => { f.Receipt.schemaVersion = 99; Invalid(f, "schema"); }));
        yield return Case("Unknown gate rejected", () => WithFixture(f => { f.Receipt.gate = "fictional"; Invalid(f, "gate"); }));
        yield return Case("Incomplete source snapshot rejects evidence", () => WithFixture(f => { f.Snapshot.errors.Add("unreadable source"); Invalid(f, "snapshot"); }));
        yield return Case("Missing provenance rejected", () => WithFixture(f => { f.Receipt.attestedBy = ""; Invalid(f, "attestation"); }));
        yield return Case("Plain PASS log rejected", () => WithFixture(f => {
            File.WriteAllText(f.ArtifactPath, "PASS"); f.Rehash(); Invalid(f, "validation failed");
        }));
        yield return Case("Missing checks rejected", () => WithFixture(f => {
            File.WriteAllText(f.ArtifactPath, "<test-run result='Passed' failed='0' passed='1'><test-case result='Passed'/></test-run>");
            f.Rehash(); Invalid(f, "checks are missing");
        }));
        yield return Case("Failed runner never passes", () => WithFixture(f => { f.Receipt.passed = false; Invalid(f, "failure"); }));
        yield return Case("Failed XML run rejected", () => WithFixture(f => {
            File.WriteAllText(f.ArtifactPath, "<test-run result='Failed' failed='1' passed='1'/>"); f.Rehash(); Invalid(f, "passing run");
        }));
        yield return Case("XML external entities prohibited", () => WithFixture(f => {
            File.WriteAllText(f.ArtifactPath, "<!DOCTYPE test-run [<!ENTITY secret SYSTEM 'file:///not-read'>]><test-run>&secret;</test-run>");
            f.Rehash(); Invalid(f, "validation failed");
        }));
        yield return Case("Structured attestations need independent runner log", () => WithFixture(f => {
            var root = new XElement("combat-validation", new XAttribute("result", "Passed"), new XAttribute("gate", "editMode"),
                new XAttribute("revision", f.Snapshot.revision), new XAttribute("fingerprint", f.Snapshot.fingerprint), new XAttribute("runId", f.Receipt.runId),
                new XElement("check", new XAttribute("id", "combat-edit-suite"), new XAttribute("result", "Passed")));
            root.Save(f.ArtifactPath); f.Receipt.artifacts[0].format = "combat-validation-v1"; f.Rehash(); Invalid(f, "runner-log");
        }));
        yield return Case("Missing inventory subjects rejected", () => WithFixture(f => {
            string error = CombatReadinessEvidence.Validate(f.Receipt, f.Snapshot, f.Root, new[] { "enemy:uncovered" });
            Check(error != null && error.Contains("every required"));
        }));
        yield return Case("Future timestamps rejected", () => WithFixture(f => { f.Receipt.completedUtc = DateTimeOffset.UtcNow.AddDays(1).ToString("O"); Invalid(f, "timestamps"); }));
        yield return Case("Newer failure revokes historical pass", () => WithFixture(f => {
            var failed = f.CreateReceipt("editMode"); failed.passed = false;
            failed.completedUtc = DateTimeOffset.UtcNow.ToString("O");
            var result = Evaluate(f, new[] { f.Receipt, failed });
            Check(!result.gates.Single(g => g.id == "editMode").passed);
        }));
        yield return Case("Current valid NUnit artifact passes only its gate", () => WithFixture(f => {
            Check(CombatReadinessEvidence.Validate(f.Receipt, f.Snapshot, f.Root, Array.Empty<string>()) == null);
            var result = Evaluate(f, new[] { f.Receipt });
            Check(!result.ReadyForProduction && result.gates.Count(g => g.passed) == 1);
        }));
        yield return Case("All gates plus clean authoring required", () => WithFixture(f => {
            var receipts = CombatReadinessEvidence.GateIds.Select(f.CreateReceipt).ToArray();
            Check(Evaluate(f, receipts).ReadyForProduction);
            Check(!Evaluate(f, receipts, blockers: true).ReadyForProduction);
            Check(!Evaluate(f, receipts, clean: false).ReadyForProduction);
        }));
        yield return Case("Network needs real distinct host and client artifacts", () => WithFixture(f => {
            var receipt = f.CreateReceipt("network");
            receipt.artifacts.RemoveAll(a => a.role == "client-log");
            Check(CombatReadinessEvidence.Validate(receipt, f.Snapshot, f.Root, Array.Empty<string>()).Contains("host/client"));
        }));
        yield return Case("Source edit addition removal and meta invalidate fingerprint", () => WithFixture(f => {
            string source = Path.Combine(f.Root, "actor.cs"), meta = source + ".meta";
            File.WriteAllText(source, "first"); File.WriteAllText(meta, "guid: first");
            string before = CombatReadinessEvidence.Fingerprint(f.Root, "revision", new[] { "actor.cs", "actor.cs.meta" }).fingerprint;
            File.WriteAllText(meta, "guid: second");
            string changed = CombatReadinessEvidence.Fingerprint(f.Root, "revision", new[] { "actor.cs", "actor.cs.meta" }).fingerprint;
            Check(before != changed);
            Check(changed != CombatReadinessEvidence.Fingerprint(f.Root, "revision", new[] { "actor.cs" }).fingerprint);
            File.Delete(source);
            Check(CombatReadinessEvidence.Fingerprint(f.Root, "revision", new[] { "actor.cs" }).errors.Count > 0);
        }));
        yield return Case("Fingerprint deterministic across ordering", () => WithFixture(f => {
            File.WriteAllText(Path.Combine(f.Root, "a"), "A"); File.WriteAllText(Path.Combine(f.Root, "b"), "B");
            Check(CombatReadinessEvidence.Fingerprint(f.Root, "revision", new[] { "a", "b" }).fingerprint ==
                CombatReadinessEvidence.Fingerprint(f.Root, "revision", new[] { "b", "a", "a" }).fingerprint);
        }));
        yield return Case("Fingerprint rejects paths outside project", () => WithFixture(f => {
            Check(CombatReadinessEvidence.Fingerprint(f.Root, "revision", new[] { "../outside.cs" }).errors.Count > 0);
        }));
    }

    private static KeyValuePair<string, Action> Case(string name, Action test) => new KeyValuePair<string, Action>(name, test);
    private static void Check(bool condition) { if (!condition) throw new InvalidOperationException("Readiness validator fixture assertion failed."); }
    private static void Invalid(Fixture fixture, string contains)
    {
        string error = CombatReadinessEvidence.Validate(fixture.Receipt, fixture.Snapshot, fixture.Root, Array.Empty<string>());
        Check(error != null && error.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0);
    }
    private static CombatReadinessEvidence.Readiness Evaluate(Fixture f, IEnumerable<CombatReadinessEvidence.Receipt> receipts, bool clean = true, bool blockers = false) =>
        CombatReadinessEvidence.Evaluate(f.Snapshot, receipts, f.Root, new Dictionary<string, string[]>(), clean, blockers);
    private static void WithFixture(Action<Fixture> check) { using (var fixture = new Fixture()) check(fixture); }

    private sealed class Fixture : IDisposable
    {
        public readonly string Root = Path.Combine(Path.GetTempPath(), "CombatReadinessValidatorFixture-" + Guid.NewGuid().ToString("N"));
        public readonly CombatReadinessEvidence.Snapshot Snapshot = new CombatReadinessEvidence.Snapshot { revision = "fixture-revision", fingerprint = "fixture-fingerprint" };
        public readonly CombatReadinessEvidence.Receipt Receipt;
        public string ArtifactPath => Path.Combine(Root, "editMode.xml");
        public Fixture() { Directory.CreateDirectory(Root); Receipt = CreateReceipt("editMode"); }
        public void Rehash() { Receipt.artifacts[0].sha256 = CombatReadinessEvidence.HashFile(ArtifactPath); }
        public CombatReadinessEvidence.Receipt CreateReceipt(string gate)
        {
            string path = Path.Combine(Root, gate + ".xml");
            var root = new XElement("test-run", new XAttribute("result", "Passed"), new XAttribute("failed", "0"), new XAttribute("passed", CombatReadinessEvidence.RequiredChecks(gate).Length));
            foreach (string check in CombatReadinessEvidence.RequiredChecks(gate))
                root.Add(new XElement("test-case", new XAttribute("result", "Passed"),
                    new XElement("properties", new XElement("property", new XAttribute("name", "CombatReadinessCheck"), new XAttribute("value", check)))));
            root.Save(path);
            var receipt = new CombatReadinessEvidence.Receipt {
                schemaVersion = CombatReadinessEvidence.SchemaVersion, gate = gate, revision = Snapshot.revision, fingerprint = Snapshot.fingerprint,
                runId = "SYNTHETIC-FIXTURE-NOT-PRODUCTION-EVIDENCE", runner = "validator-fixture", attestedBy = "synthetic-unit-test",
                startedUtc = DateTimeOffset.UtcNow.AddMinutes(-2).ToString("O"), completedUtc = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O"), passed = true
            };
            receipt.artifacts.Add(new CombatReadinessEvidence.Artifact { path = path, sha256 = CombatReadinessEvidence.HashFile(path), format = "nunit3" });
            if (gate == "network")
                foreach (string role in new[] { "host-log", "client-log" })
                {
                    string log = Path.Combine(Root, role + ".log"); File.WriteAllText(log, "SYNTHETIC FIXTURE " + role);
                    receipt.artifacts.Add(new CombatReadinessEvidence.Artifact { path = log, sha256 = CombatReadinessEvidence.HashFile(log), format = "attachment", role = role });
                }
            return receipt;
        }
        public void Dispose()
        {
            string resolved = Path.GetFullPath(Root);
            string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!resolved.StartsWith(temp, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(resolved).StartsWith("CombatReadinessValidatorFixture-", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing cleanup outside the named validator fixture directory.");
            Directory.Delete(resolved, true);
        }
    }
}
