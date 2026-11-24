using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Lit.DreamFractures
{
    [Serializable]
    public sealed class DreamFractureRecord
    {
        public string fractureId;
        public int rulesVersion, players, bestScore;
        public double bestSeconds;
        public bool hasSuccess;
        public DreamFractureResult lastResult;
    }

    public sealed class DreamFractureRecords
    {
        [Serializable] private sealed class FileData
        {
            public int version = 1;
            public List<DreamFractureRecord> records = new List<DreamFractureRecord>();
        }
        private readonly string path;
        private FileData data;

        public DreamFractureRecords(string filePath)
        {
            path = filePath;
            data = File.Exists(path) ? JsonUtility.FromJson<FileData>(File.ReadAllText(path)) : new FileData();
            if (data == null || data.version != 1 || data.records == null)
                throw new InvalidDataException("Fichier de records oniriques invalide.");
        }

        public DreamFractureRecord Find(string id, int version, int players) =>
            data.records.Find(r => r.fractureId == id && r.rulesVersion == version && r.players == players);

        public bool Record(DreamFractureResult result)
        {
            if (result == null || string.IsNullOrEmpty(result.runId)) return false;
            var record = Find(result.fractureId, result.rulesVersion, result.initialPlayers);
            // A repeated delivery of an already committed run is idempotent.
            if (record != null && record.lastResult?.runId == result.runId) return true;
            var next = JsonUtility.FromJson<FileData>(JsonUtility.ToJson(data));
            record = next.records.Find(r => r.fractureId == result.fractureId &&
                r.rulesVersion == result.rulesVersion && r.players == result.initialPlayers);
            if (record == null)
            {
                record = new DreamFractureRecord
                { fractureId = result.fractureId, rulesVersion = result.rulesVersion, players = result.initialPlayers };
                next.records.Add(record);
            }
            record.lastResult = result;
            if (result.outcome == DreamFractureOutcome.Success)
            {
                record.bestScore = record.hasSuccess ? Math.Max(record.bestScore, result.score) : result.score;
                record.bestSeconds = record.hasSuccess ? Math.Min(record.bestSeconds, result.seconds) : result.seconds;
                record.hasSuccess = true;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            SaveMetadataWriter.WriteAtomic(path, JsonUtility.ToJson(next, true));
            data = next;
            return true;
        }
    }
}
