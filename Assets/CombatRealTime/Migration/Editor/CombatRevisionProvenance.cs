using System;
using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>A parent export is explicitly an attestation, never a fabricated local Git verification.</summary>
public static class CombatRevisionProvenance
{
    [Serializable] public sealed class ParentExport
    {
        public int schemaVersion = 1;
        public string revision, fingerprint, verifiedBy, verifiedUtc, sourceRepository, exportId;
    }

    public static string Validate(ParentExport export, string fingerprint)
    {
        if (export == null || export.schemaVersion != 1) return "Missing/unsupported parent revision attestation.";
        if (!Regex.IsMatch(export.revision ?? "", "^[a-fA-F0-9]{40,64}$")) return "Parent revision is not a full Git object ID.";
        if (export.fingerprint != fingerprint) return "Parent export/source fingerprint mismatch; sandbox is not the exported source snapshot.";
        if (string.IsNullOrWhiteSpace(export.verifiedBy) || string.IsNullOrWhiteSpace(export.sourceRepository) || string.IsNullOrWhiteSpace(export.exportId))
            return "Parent provenance identity/repository/export ID missing.";
        if (!DateTimeOffset.TryParse(export.verifiedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time) || time > DateTimeOffset.UtcNow.AddMinutes(5))
            return "Invalid parent verification timestamp.";
        return null;
    }
}
