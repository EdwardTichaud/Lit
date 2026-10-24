using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

public static class SaveMetadataWriter
{
    private const string JournalName = ".lit-save-journal";
    private const string StagePrefix = ".lit-save-stage-";
    private const int JournalMagic = 0x4C495453;
    private static readonly object BatchGate = new object();
    private static readonly StringComparer PathComparer = Path.DirectorySeparatorChar == '\\'
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static void WriteAtomic(string path, string json)
    {
        if (string.IsNullOrWhiteSpace(path) || json == null) throw new ArgumentException("Métadonnées manquantes.");
        WriteBytesAtomic(path, Encoding.UTF8.GetBytes(json));
    }

    /// <summary>Replaces one complete save file only after its new payload has been flushed.</summary>
    public static void WriteBytesAtomic(string path, byte[] bytes)
    {
        if (string.IsNullOrWhiteSpace(path) || bytes == null) throw new ArgumentException("Données de sauvegarde manquantes.");
        string temporary = null;
        bool ownsTemporary = false;
        try
        {
            using (FileStream stream = CreateTemporaryStream(path, out temporary))
            {
                ownsTemporary = true;
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        finally { if (ownsTemporary && File.Exists(temporary)) File.Delete(temporary); }
    }

    private static FileStream CreateTemporaryStream(string target, out string temporary)
    {
        string fullTarget = Path.GetFullPath(target);
        string directory = Path.GetDirectoryName(fullTarget);
        // A transaction already adds a staging directory. Appending another
        // GUID to the target's name exceeded Mono's Windows path limit even
        // when every final save and journal path was valid.
        int length = 20;
        if (Path.DirectorySeparatorChar == '\\' && directory.Length < 258)
            length = Math.Min(length, 258 - directory.Length);
        temporary = null;
        for (int attempt = 0; attempt < 32; attempt++)
        {
            string token = Guid.NewGuid().ToString("N");
            string name = length > 1 ? "." + token.Substring(0, length - 1) : token.Substring(0, 1);
            temporary = Path.Combine(directory, name);
            if (PathComparer.Equals(temporary, fullTarget)) continue;
            try { return new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None); }
            catch (IOException) when (File.Exists(temporary)) { /* Collision: never overwrite or delete another writer's file. */ }
        }
        throw new IOException("Could not reserve a temporary save file.");
    }

    /// <summary>
    /// Publishes the character, world and metadata as one recoverable save.
    /// A durable journal precedes the first replacement. Until all files have
    /// been replaced and the commit marker flushed, recovery restores all old
    /// files. Readers call RecoverPendingBatch before consuming any payload.
    /// </summary>
    public static void WriteBatchAtomic(IReadOnlyDictionary<string, byte[]> payloads)
    {
        if (payloads == null || payloads.Count == 0 || payloads.Count > 64)
            throw new ArgumentException("Invalid save batch.");
        string root = null;
        var files = new List<KeyValuePair<string, byte[]>>();
        var names = new HashSet<string>(PathComparer);
        foreach (var payload in payloads)
        {
            string fullPath = Path.GetFullPath(payload.Key);
            string directory = Path.GetDirectoryName(fullPath);
            if (root == null) root = directory;
            if (!PathComparer.Equals(root, directory) || payload.Value == null ||
                !IsSaveFileName(Path.GetFileName(fullPath)) || !names.Add(Path.GetFileName(fullPath)))
                throw new ArgumentException("Save batch files must be distinct files in the same save directory.");
            files.Add(new KeyValuePair<string, byte[]>(Path.GetFileName(fullPath), payload.Value));
        }
        files.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);

        lock (BatchGate)
        using (AcquireBatchLock(root))
        {
            RecoverCore(root);
            if (File.Exists(Path.Combine(root, JournalName)))
                throw new IOException("Previous save transaction could not be cleaned up.");
            string id = Guid.NewGuid().ToString("N");
            string stage = Path.Combine(root, StagePrefix + id);
            Directory.CreateDirectory(stage);
            var existed = new bool[files.Count];
            try
            {
                for (int i = 0; i < files.Count; i++)
                {
                    string target = Path.Combine(root, files[i].Key);
                    existed[i] = File.Exists(target);
                    if (existed[i]) WriteBytesAtomic(Path.Combine(stage, "old-" + i), File.ReadAllBytes(target));
                    WriteBytesAtomic(Path.Combine(stage, "new-" + i), files[i].Value);
                }
                using (var journal = new MemoryStream())
                {
                    using (var writer = new BinaryWriter(journal, Encoding.UTF8, true))
                    {
                        writer.Write(JournalMagic);
                        writer.Write(id);
                        writer.Write(files.Count);
                        for (int i = 0; i < files.Count; i++)
                        {
                            writer.Write(files[i].Key);
                            writer.Write(existed[i]);
                        }
                    }
                    WriteBytesAtomic(Path.Combine(root, JournalName), journal.ToArray());
                }
                for (int i = 0; i < files.Count; i++)
                {
                    string target = Path.Combine(root, files[i].Key);
                    string prepared = Path.Combine(stage, "new-" + i);
                    if (File.Exists(target)) File.Replace(prepared, target, null);
                    else File.Move(prepared, target);
                }
                WriteBytesAtomic(Path.Combine(stage, "committed"), new byte[] { 1 });
            }
            catch (Exception writeError)
            {
                if (File.Exists(Path.Combine(root, JournalName)))
                {
                    try { RecoverCore(root); }
                    catch (Exception restoreError) { throw new AggregateException("Save failed; recovery must finish before loading.", writeError, restoreError); }
                }
                else TryCleanup(root, stage, files.Count, false);
                throw;
            }
            TryCleanup(root, stage, files.Count, true);
        }
    }

    public static void RecoverPendingBatch(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !File.Exists(Path.Combine(directory, JournalName))) return;
        string root = Path.GetFullPath(directory);
        lock (BatchGate)
        using (AcquireBatchLock(root)) RecoverCore(root);
    }

    private static FileStream AcquireBatchLock(string root) => new FileStream(
        Path.Combine(root, ".lit-save-lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite,
        FileShare.None, 1, FileOptions.DeleteOnClose);

    private static void RecoverCore(string root)
    {
        string path = Path.Combine(root, JournalName);
        if (!File.Exists(path)) return;
        string id;
        var names = new List<string>();
        var existed = new List<bool>();
        using (var reader = new BinaryReader(File.OpenRead(path), Encoding.UTF8))
        {
            if (reader.ReadInt32() != JournalMagic) throw new InvalidDataException("Invalid save transaction journal.");
            id = reader.ReadString();
            if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidDataException("Invalid save transaction identifier.");
            int count = reader.ReadInt32();
            if (count < 1 || count > 64) throw new InvalidDataException("Invalid save transaction file count.");
            var unique = new HashSet<string>(PathComparer);
            for (int i = 0; i < count; i++)
            {
                string name = reader.ReadString();
                if (!IsSaveFileName(name) || !unique.Add(name)) throw new InvalidDataException("Invalid save transaction file name.");
                names.Add(name);
                existed.Add(reader.ReadBoolean());
            }
            if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException("Unexpected save journal data.");
        }
        string stage = Path.Combine(root, StagePrefix + id);
        if (!Directory.Exists(stage)) throw new InvalidDataException("Save transaction recovery data is missing.");
        if (!File.Exists(Path.Combine(stage, "committed")))
        {
            // Check every backup before changing even one file. If recovery
            // cannot finish, retain the journal and refuse reads of mixed data.
            for (int i = 0; i < names.Count; i++)
                if (existed[i] && !File.Exists(Path.Combine(stage, "old-" + i)))
                    throw new InvalidDataException("Save transaction backup is missing.");
            for (int i = 0; i < names.Count; i++)
            {
                string target = Path.Combine(root, names[i]);
                if (existed[i]) WriteBytesAtomic(target, File.ReadAllBytes(Path.Combine(stage, "old-" + i)));
                else if (File.Exists(target)) File.Delete(target);
            }
            // A finished rollback is also a settled transaction. Preserve
            // that decision across an interruption during backup cleanup.
            WriteBytesAtomic(Path.Combine(stage, "committed"), new byte[] { 1 });
        }
        TryCleanup(root, stage, names.Count, true);
    }

    private static bool IsSaveFileName(string name) =>
        !string.IsNullOrWhiteSpace(name) && name != "." && name != ".." &&
        name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
        name.IndexOf('/') < 0 && name.IndexOf('\\') < 0 &&
        !name.StartsWith(".lit-save-", StringComparison.OrdinalIgnoreCase);

    private static void TryCleanup(string root, string stage, int count, bool removeJournal)
    {
        try
        {
            for (int i = 0; i < count; i++)
            {
                File.Delete(Path.Combine(stage, "old-" + i));
                File.Delete(Path.Combine(stage, "new-" + i));
            }
            // The marker must survive until the journal is gone, so a crash
            // during cleanup never rolls a committed save back.
            if (removeJournal) File.Delete(Path.Combine(root, JournalName));
            File.Delete(Path.Combine(stage, "committed"));
            Directory.Delete(stage);
        }
        catch (IOException) { /* A durable journal makes cleanup safely retryable. */ }
        catch (UnauthorizedAccessException) { /* Keep committed data usable. */ }
    }
}
