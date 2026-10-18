using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

/// <summary>Evidence validation only. Never enables gameplay or executes a migration.</summary>
public static class CombatReadinessEvidence
{
    public const int SchemaVersion = 2;
    public static readonly string[] GateIds = {
        "compilation", "editMode", "playMode", "zone", "save", "domainReloadOn", "domainReloadOff",
        "network", "allEnemyMappings", "inventoryCoverage", "idempotence"
    };

    [Serializable] public sealed class Snapshot
    {
        public int schemaVersion = SchemaVersion;
        public string revision, fingerprint, editorContext;
        public string revisionSource, revisionAttestationSha256, revisionVerifiedBy, revisionVerifiedUtc;
        public List<string> files = new List<string>();
        public List<string> errors = new List<string>();
    }

    [Serializable] public sealed class Artifact
    {
        public string path, sha256, format, role;
    }

    [Serializable] public sealed class Receipt
    {
        public int schemaVersion;
        public string gate, revision, fingerprint, runId, runner, attestedBy, startedUtc, completedUtc;
        public bool passed;
        public List<Artifact> artifacts = new List<Artifact>();
        public List<string> subjects = new List<string>();
    }

    [Serializable] public sealed class Gate
    {
        public string id, status = "pending", reason = "No current evidence imported.", runId;
        public bool passed;
        public List<Artifact> artifacts = new List<Artifact>();
        public List<string> requiredChecks = new List<string>();
    }

    [Serializable] public sealed class Readiness
    {
        public int schemaVersion = SchemaVersion;
        public Snapshot snapshot;
        public bool ReadyForProduction;
        public bool authoringClean, inventoryClear;
        public string trustBoundary = "Imported runner attestations with verified artifact bytes, not authenticated proof of runner identity. No runtime activation is performed.";
        public List<Gate> gates = new List<Gate>();
    }

    public static string[] RequiredChecks(string gate)
    {
        switch (gate)
        {
            case "compilation": return new[] { "runtime-editor-compilation" };
            case "editMode": return new[] { "combat-edit-suite" };
            case "playMode": return new[] { "production-combat-flow", "actor-swap-cleanup", "death-disable-cleanup" };
            case "zone": return new[] { "all-zone-loads", "scene-overrides", "encounter-gates", "navmesh-placement" };
            case "save": return new[] { "save-load-roundtrip", "stable-identities", "progression-preserved" };
            case "domainReloadOn": case "domainReloadOff": return new[] { "subscriptions-cleanup", "repeat-play-entry", "static-state-reset" };
            case "network": return new[] { "real-host-client", "trusted-gameplay-executor", "authority-rejection", "impact-replication", "disconnect-cleanup", "unsupported-families-covered", "remote-reactions", "remote-health-thresholds", "cinematic-replication" };
            case "allEnemyMappings": return new[] { "all-enemy-patterns", "ghost-boss-gates", "authored-skill-events", "no-approximations" };
            case "inventoryCoverage": return new[] { "dynamic-references-reviewed", "addressables-resources", "marker-overrides" };
            case "idempotence": return new[] { "second-apply-zero-writes", "dry-run-zero-writes", "guid-id-preservation" };
            default: throw new ArgumentException("Unknown readiness gate: " + gate);
        }
    }

    public static string HashFile(string path)
    {
        using (var stream = File.OpenRead(path))
        using (var hash = SHA256.Create()) return Hex(hash.ComputeHash(stream));
    }

    public static string HashText(string value)
    {
        using (var hash = SHA256.Create()) return Hex(hash.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }

    private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();

    // Exact relative names AND bytes are hashed; file additions/removals and GUID/meta changes invalidate receipts.
    public static Snapshot Fingerprint(string root, string revision, IEnumerable<string> relativePaths)
    {
        var result = new Snapshot { revision = revision };
        if (string.IsNullOrWhiteSpace(revision)) result.errors.Add("Git revision unavailable.");
        var lines = new List<string> { "combat-readiness-v" + SchemaVersion };
        foreach (string relative in relativePaths.Select(path => path.Replace('\\', '/')).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal))
        {
            string path = Path.GetFullPath(Path.Combine(root, relative));
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { result.errors.Add("Out-of-root fingerprint path: " + relative); continue; }
            try { lines.Add(relative + "\0" + HashFile(path)); result.files.Add(relative); }
            catch (Exception exception) { result.errors.Add(relative + ": " + exception.Message); }
        }
        if (result.files.Count == 0) result.errors.Add("Empty source/asset fingerprint.");
        result.fingerprint = HashText(string.Join("\n", lines));
        return result;
    }

    public static string Validate(Receipt receipt, Snapshot snapshot, string artifactRoot, IEnumerable<string> requiredSubjects)
    {
        if (receipt == null || receipt.schemaVersion != SchemaVersion || !GateIds.Contains(receipt.gate)) return "Unknown evidence schema/gate.";
        if (snapshot == null || snapshot.errors.Count != 0 || string.IsNullOrEmpty(snapshot.revision) || string.IsNullOrEmpty(snapshot.fingerprint)) return "Source snapshot unavailable/incomplete.";
        if (receipt.revision != snapshot.revision || receipt.fingerprint != snapshot.fingerprint) return "Evidence revision/source fingerprint mismatch.";
        if (!receipt.passed) return "Runner reported failure.";
        if (string.IsNullOrWhiteSpace(receipt.runId) || string.IsNullOrWhiteSpace(receipt.runner) || string.IsNullOrWhiteSpace(receipt.attestedBy)) return "Runner/run ID/attestation missing.";
        if (!DateTimeOffset.TryParse(receipt.startedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var start) ||
            !DateTimeOffset.TryParse(receipt.completedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var end) ||
            start > end || end > DateTimeOffset.UtcNow.AddMinutes(5)) return "Invalid runner timestamps.";
        var subjects = receipt.subjects ?? new List<string>();
        if ((requiredSubjects ?? Array.Empty<string>()).Except(subjects, StringComparer.Ordinal).Any()) return "Evidence does not cover every required inventory subject.";
        if (receipt.artifacts == null || receipt.artifacts.Count == 0) return "No real artifacts supplied.";
        var checks = new HashSet<string>(StringComparer.Ordinal);
        bool structuredAttestation = false;
        try
        {
            foreach (var artifact in receipt.artifacts)
            {
                if (artifact == null || string.IsNullOrWhiteSpace(artifact.path) || string.IsNullOrWhiteSpace(artifact.sha256)) return "Artifact path/hash missing.";
                string path = Path.IsPathRooted(artifact.path) ? artifact.path : Path.Combine(artifactRoot, artifact.path);
                if (!File.Exists(path) || new FileInfo(path).Length == 0) return "Artifact missing/empty: " + artifact.path;
                if (!string.Equals(HashFile(path), artifact.sha256, StringComparison.OrdinalIgnoreCase)) return "Artifact hash mismatch: " + artifact.path;
                if (artifact.format == "attachment") continue;
                using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                {
                    var document = XDocument.Load(reader);
                    var root = document.Root;
                    if (root == null) return "Empty artifact XML.";
                    if (artifact.format == "nunit3")
                    {
                        if (root.Name.LocalName != "test-run" || (string)root.Attribute("result") != "Passed" ||
                            (string)root.Attribute("failed") != "0" || !int.TryParse((string)root.Attribute("passed"), out int passed) || passed < 1)
                            return "NUnit run is not a nonempty passing run.";
                        foreach (var test in root.Descendants("test-case").Where(test => (string)test.Attribute("result") == "Passed"))
                            foreach (var property in test.Descendants("property").Where(property => (string)property.Attribute("name") == "CombatReadinessCheck"))
                                checks.Add((string)property.Attribute("value") ?? "");
                    }
                    else if (artifact.format == "combat-validation-v1")
                    {
                        structuredAttestation = true;
                        if (root.Name.LocalName != "combat-validation" || (string)root.Attribute("result") != "Passed" ||
                            (string)root.Attribute("gate") != receipt.gate || (string)root.Attribute("revision") != receipt.revision ||
                            (string)root.Attribute("fingerprint") != receipt.fingerprint || (string)root.Attribute("runId") != receipt.runId)
                            return "Validation artifact metadata/result mismatch.";
                        foreach (var check in root.Elements("check"))
                        {
                            if ((string)check.Attribute("result") != "Passed") return "Validation artifact contains a nonpassing check.";
                            checks.Add((string)check.Attribute("id") ?? "");
                        }
                    }
                    else return "Unknown artifact format (plain PASS logs are not evidence).";
                }
            }
        }
        catch (Exception exception) { return "Artifact validation failed: " + exception.Message; }
        if (structuredAttestation && !receipt.artifacts.Any(artifact => artifact.format == "attachment" && artifact.role == "runner-log" &&
            receipt.artifacts.Where(other => other.format != "attachment").All(other => !string.Equals(other.sha256, artifact.sha256, StringComparison.OrdinalIgnoreCase))))
            return "Structured attestation requires a separate hashed runner-log attachment.";
        if (receipt.gate == "network" && (!receipt.artifacts.Any(artifact => artifact.format == "attachment" && artifact.role == "host-log") ||
            !receipt.artifacts.Any(artifact => artifact.format == "attachment" && artifact.role == "client-log") ||
            receipt.artifacts.Where(artifact => artifact.role == "host-log" || artifact.role == "client-log").Select(artifact => artifact.sha256).Distinct(StringComparer.OrdinalIgnoreCase).Count() < 2))
            return "Network evidence requires distinct real host/client log artifacts.";
        if (RequiredChecks(receipt.gate).Except(checks, StringComparer.Ordinal).Any()) return "Required gate checks are missing from artifacts.";
        return null;
    }

    public static Readiness Evaluate(Snapshot snapshot, IEnumerable<Receipt> receipts, string artifactRoot,
        IDictionary<string, string[]> subjects, bool authoringClean, bool hasBlockers)
    {
        var result = new Readiness { snapshot = snapshot, authoringClean = authoringClean, inventoryClear = !hasBlockers };
        var supplied = (receipts ?? Array.Empty<Receipt>()).Where(receipt => receipt != null).ToArray();
        foreach (string id in GateIds)
        {
            var gate = new Gate { id = id, requiredChecks = RequiredChecks(id).ToList() };
            var candidates = supplied.Where(receipt => receipt.gate == id).ToArray();
            // A newer failed/invalid run revokes an older pass. Never select a convenient historical green run.
            foreach (var receipt in candidates.OrderByDescending(receipt =>
                DateTimeOffset.TryParse(receipt.completedUtc, out var completed) ? completed : DateTimeOffset.MaxValue).Take(1))
            {
                string error = Validate(receipt, snapshot, artifactRoot, subjects != null && subjects.TryGetValue(id, out var required) ? required : Array.Empty<string>());
                if (error != null) { gate.status = "invalid"; gate.reason = error; continue; }
                gate.passed = true; gate.status = "passed"; gate.reason = "Artifact bytes and runner attestation validated for this snapshot.";
                gate.runId = receipt.runId; gate.artifacts = receipt.artifacts; break;
            }
            result.gates.Add(gate);
        }
        result.ReadyForProduction = authoringClean && !hasBlockers && snapshot != null && snapshot.errors.Count == 0 && result.gates.All(gate => gate.passed);
        return result;
    }
}
