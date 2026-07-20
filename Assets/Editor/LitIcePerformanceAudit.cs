using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Lit.Performance;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class LitIcePerformanceAudit
{
    private const string GeneratedRoot = "Assets/Environment/Prefabs_Ice";
    private const string LegacyGeneratedRoot = "Assets/Materials/IceShader/BakedMeshes";
    private const string CatalogPath = GeneratedRoot + "/LitIcePrefabCatalog.asset";
    private const string DefaultReportPath = ".codex-temp/Phase2/ice-asset-audit.json";

    [Serializable]
    public sealed class Report
    {
        public int schemaVersion = 1;
        public string generatedUtc;
        public int maxGeneratedVertexCount;
        public long maxGeneratedMeshBytes;
        public int maxLocalFlameInfluences;
        public int generatedMeshCount;
        public int compliantMeshCount;
        public int violationCount;
        public long totalGeneratedMeshBytes;
        public string largestMeshPath;
        public long largestMeshBytes;
        public List<MeshRecord> meshes = new List<MeshRecord>();

        public bool IsValid => violationCount == 0;
    }

    [Serializable]
    public sealed class MeshRecord
    {
        public string assetPath;
        public string meshName;
        public int vertexCount;
        public long indexCount;
        public int subMeshCount;
        public long serializedBytes;
        public string sourceAssetPath;
        public string sourceMeshName;
        public int sourceVertexCount;
        public long predictedBakedVertexCount;
        public bool compliant;
        public List<string> violations = new List<string>();
    }

    [MenuItem("Lit/Performance/Audit Ice Assets (Report Only)")]
    public static void AuditFromMenu()
    {
        Report report = CreateReport();
        string outputPath = ResolveOutputPath();
        Export(report, outputPath);
        Debug.Log(
            $"[Lit Ice Audit] {report.generatedMeshCount} generated mesh(es), "
            + $"{report.violationCount} violation(s), report: {outputPath}");
    }

    [MenuItem("Lit/Performance/Validate Ice Assets (Blocking)")]
    public static void ValidateFromMenu()
    {
        ValidateForCi();
    }

    // CI entry point:
    // -executeMethod LitIcePerformanceAudit.ValidateForCi
    public static void ValidateForCi()
    {
        Report report = CreateReport();
        string outputPath = ResolveOutputPath();
        Export(report, outputPath);
        if (!report.IsValid)
        {
            throw new BuildFailedException(
                $"Ice asset validation failed with {report.violationCount} violation(s). "
                + $"See '{outputPath}'.");
        }

        Debug.Log(
            $"[Lit Ice Audit] Validation passed for {report.generatedMeshCount} generated mesh(es). "
            + $"Report: {outputPath}");
    }

    // Non-blocking batch entry point used to establish the before/after report.
    public static void ExportBatchAudit()
    {
        Report report = CreateReport();
        string outputPath = ResolveOutputPath();
        Export(report, outputPath);
        Debug.Log(
            $"[Lit Ice Audit] Exported {report.generatedMeshCount} generated mesh(es), "
            + $"{report.violationCount} violation(s), to '{outputPath}'.");
    }

    public static Report CreateReport()
    {
        var report = new Report
        {
            generatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            maxGeneratedVertexCount = IcePerformanceBudgetPolicy.MaxGeneratedVertexCount,
            maxGeneratedMeshBytes = IcePerformanceBudgetPolicy.MaxGeneratedMeshBytes,
            maxLocalFlameInfluences = IcePerformanceBudgetPolicy.MaxLocalFlameInfluences
        };

        string[] roots = new[] { GeneratedRoot, LegacyGeneratedRoot }
            .Where(AssetDatabase.IsValidFolder)
            .ToArray();
        string[] paths = roots.Length == 0
            ? Array.Empty<string>()
            : AssetDatabase.FindAssets("t:Mesh", roots)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(IsGeneratedMeshAssetPath)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

        var recordsByGuid = new Dictionary<string, MeshRecord>(StringComparer.Ordinal);
        foreach (string path in paths)
        {
            MeshRecord record = ReadSerializedMeshHeader(path);
            if (record == null)
                continue;

            string guid = AssetDatabase.AssetPathToGUID(path);
            if (!string.IsNullOrEmpty(guid))
                recordsByGuid[guid] = record;
            report.meshes.Add(record);
        }

        PopulateCatalogSources(recordsByGuid);

        foreach (MeshRecord record in report.meshes)
        {
            long serializedBytes = record.serializedBytes;

            if (record.vertexCount > IcePerformanceBudgetPolicy.MaxGeneratedVertexCount)
            {
                record.violations.Add(
                    $"vertex-count:{record.vertexCount}>{IcePerformanceBudgetPolicy.MaxGeneratedVertexCount}");
            }

            if (serializedBytes > IcePerformanceBudgetPolicy.MaxGeneratedMeshBytes)
            {
                record.violations.Add(
                    $"serialized-bytes:{serializedBytes}>{IcePerformanceBudgetPolicy.MaxGeneratedMeshBytes}");
            }

            if (!string.IsNullOrEmpty(record.sourceAssetPath)
                && record.predictedBakedVertexCount > IcePerformanceBudgetPolicy.MaxGeneratedVertexCount)
            {
                record.violations.Add(
                    "source-barycentric-output-exceeds-vertex-budget:"
                    + record.predictedBakedVertexCount);
            }

            record.compliant = record.violations.Count == 0;
            report.generatedMeshCount++;
            report.totalGeneratedMeshBytes += serializedBytes;
            if (record.compliant)
                report.compliantMeshCount++;
            else
                report.violationCount++;

            if (serializedBytes > report.largestMeshBytes)
            {
                report.largestMeshBytes = serializedBytes;
                report.largestMeshPath = record.assetPath;
            }
        }

        return report;
    }

    private static MeshRecord ReadSerializedMeshHeader(string assetPath)
    {
        string absolutePath = Path.GetFullPath(assetPath);
        if (!File.Exists(absolutePath))
            return null;

        var record = new MeshRecord
        {
            assetPath = assetPath,
            serializedBytes = new FileInfo(absolutePath).Length
        };

        try
        {
            using (var reader = new StreamReader(absolutePath))
            {
                bool readingSubMeshes = false;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("m_Name:", StringComparison.Ordinal))
                    {
                        record.meshName = trimmed.Substring("m_Name:".Length).Trim();
                    }
                    else if (trimmed == "m_SubMeshes:")
                    {
                        readingSubMeshes = true;
                    }
                    else if (readingSubMeshes && trimmed == "m_Shapes:")
                    {
                        break;
                    }
                    else if (readingSubMeshes
                        && trimmed.StartsWith("- serializedVersion:", StringComparison.Ordinal))
                    {
                        record.subMeshCount++;
                    }
                    else if (readingSubMeshes
                        && TryReadLong(trimmed, "indexCount:", out long indexCount))
                    {
                        record.indexCount += indexCount;
                    }
                    else if (readingSubMeshes
                        && TryReadLong(trimmed, "vertexCount:", out long vertexCount))
                    {
                        record.vertexCount = checked(record.vertexCount + (int)vertexCount);
                    }
                }
            }

            if (record.subMeshCount > 0 && record.vertexCount > 0)
                return record;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[Lit Ice Audit] Could not parse '{assetPath}' as text: {exception.Message}");
        }

        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        if (mesh == null)
            return null;

        record.meshName = mesh.name;
        record.vertexCount = mesh.vertexCount;
        record.indexCount = GetIndexCount(mesh);
        record.subMeshCount = mesh.subMeshCount;
        Resources.UnloadAsset(mesh);
        return record;
    }

    private static bool TryReadLong(string line, string key, out long value)
    {
        value = 0L;
        if (!line.StartsWith(key, StringComparison.Ordinal))
            return false;

        return long.TryParse(
            line.Substring(key.Length).Trim(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static void PopulateCatalogSources(Dictionary<string, MeshRecord> recordsByGuid)
    {
        string absoluteCatalogPath = Path.GetFullPath(CatalogPath);
        if (recordsByGuid.Count == 0 || !File.Exists(absoluteCatalogPath))
            return;

        string sourceAssetPath = null;
        string sourceMeshName = null;
        using (var reader = new StreamReader(absoluteCatalogPath))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("- SourceMeshId:", StringComparison.Ordinal))
                {
                    sourceAssetPath = null;
                    sourceMeshName = null;
                }
                else if (trimmed.StartsWith("SourceMeshName:", StringComparison.Ordinal))
                {
                    sourceMeshName = trimmed.Substring("SourceMeshName:".Length).Trim();
                }
                else if (trimmed.StartsWith("SourceAssetPath:", StringComparison.Ordinal))
                {
                    sourceAssetPath = trimmed.Substring("SourceAssetPath:".Length).Trim();
                }
                else if (trimmed.StartsWith("BakedMesh:", StringComparison.Ordinal))
                {
                    string bakedGuid = ReadGuid(trimmed);
                    if (!string.IsNullOrEmpty(bakedGuid)
                        && recordsByGuid.TryGetValue(bakedGuid, out MeshRecord record))
                    {
                        record.sourceAssetPath = sourceAssetPath;
                        record.sourceMeshName = sourceMeshName;
                        // The current barycentric bake creates one output vertex
                        // per source index, so this is the exact pre-allocation size.
                        record.predictedBakedVertexCount = record.indexCount;
                    }
                }
            }
        }
    }

    private static string ReadGuid(string objectReference)
    {
        const string prefix = "guid:";
        int start = objectReference.IndexOf(prefix, StringComparison.Ordinal);
        if (start < 0)
            return null;

        start += prefix.Length;
        int end = objectReference.IndexOf(',', start);
        if (end < 0)
            end = objectReference.IndexOf('}', start);
        if (end < 0)
            return null;
        return objectReference.Substring(start, end - start).Trim();
    }

    private static bool IsGeneratedMeshAssetPath(string path)
    {
        if (string.IsNullOrEmpty(path)
            || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string fileName = Path.GetFileNameWithoutExtension(path);
        return fileName.StartsWith("Mesh_IceEdges_", StringComparison.Ordinal)
            || fileName.StartsWith("IceEdges", StringComparison.Ordinal);
    }

    private static long GetIndexCount(Mesh mesh)
    {
        if (mesh == null)
            return 0L;

        long total = 0L;
        for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            total += (long)mesh.GetIndexCount(subMesh);
        return total;
    }

    private static string ResolveOutputPath()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int i = 0; i < arguments.Length - 1; i++)
        {
            if (string.Equals(arguments[i], "-litIceAuditOutput", StringComparison.OrdinalIgnoreCase))
                return Path.GetFullPath(arguments[i + 1]);
        }

        return Path.GetFullPath(DefaultReportPath);
    }

    private static void Export(Report report, string outputPath)
    {
        string directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(outputPath, JsonUtility.ToJson(report, true));
    }
}
