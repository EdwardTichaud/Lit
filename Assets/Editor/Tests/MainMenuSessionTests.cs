using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

public sealed class MainMenuSessionTests
{
    [Test] public void BuildScenePathsAreUniqueForNativeNetworkSynchronization()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var scene in UnityEditor.EditorBuildSettings.scenes)
            Assert.That(paths.Add(scene.path), Is.True, "Duplicate build scene: " + scene.path);
    }

    private static void WithStorySave(Action<SaveSessionManager> verify)
    {
        string root = Path.Combine(Path.GetTempPath(), "LitStorySave-" + Guid.NewGuid().ToString("N"));
        var owner = new UnityEngine.GameObject("Story save test");
        owner.SetActive(false);
        try
        {
            var manager = owner.AddComponent<SaveSessionManager>();
            typeof(SaveSessionManager).GetField("savesRootFolder",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(manager, root);
            var session = manager.CreateSession("Test", SaveSessionType.Solo);
            var save = manager.CreateSave(session.sessionId, "Initial");
            manager.SetActiveSave(session.sessionId, save.saveId);
            verify(manager);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test] public void StoryCompletionRemainsRuntimeUntilSaveAndIsDiscardedOnReload()
    {
        WithStorySave(manager =>
        {
            string path = manager.GetActiveSaveFilePath("meta.json");
            byte[] original = File.ReadAllBytes(path);
            Assert.That(manager.MarkStorySequenceCompleted(" Intro "), Is.True);
            Assert.That(manager.IsStorySequenceCompleted("INTRO"), Is.True);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            Assert.That(manager.TryPrepareSaveMetadata("Maison_Core", out _, out byte[] bytes), Is.True);
            Assert.That(UnityEngine.JsonUtility.FromJson<SaveMeta>(Encoding.UTF8.GetString(bytes))
                .completedStorySequenceIds, Is.EquivalentTo(new[] { "Intro" }));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            manager.ResetRuntimeStorySequenceState();
            Assert.That(manager.IsStorySequenceCompleted("intro"), Is.False);
        });
    }

    [Test] public void CommittedStoryCompletionReloadsAndUnsavedResetDoesNotAlterTheSlot()
    {
        WithStorySave(manager =>
        {
            manager.MarkStorySequenceCompleted("intro");
            manager.TryPrepareSaveMetadata("Maison_Core", out string path, out byte[] bytes);
            SaveMetadataWriter.WriteBatchAtomic(new Dictionary<string, byte[]> { { path, bytes } });
            manager.SetActiveSave(manager.CurrentSessionId, manager.CurrentSaveId);
            Assert.That(manager.IsStorySequenceCompleted("INTRO"), Is.True);
            Assert.That(manager.ResetStorySequenceCompletion(" intro "), Is.True);
            Assert.That(manager.IsStorySequenceCompleted("intro"), Is.False);
            manager.ResetRuntimeStorySequenceState();
            Assert.That(manager.IsStorySequenceCompleted("intro"), Is.True);
            Assert.That(manager.ResetAllStorySequenceCompletions(), Is.EqualTo(1));
            manager.ResetRuntimeStorySequenceState();
            Assert.That(manager.IsStorySequenceCompleted("intro"), Is.True);
        });
    }

    [Test] public void NewManualSlotInheritsRuntimeStoriesWithoutChangingTheOldSlot()
    {
        WithStorySave(manager =>
        {
            string originalId = manager.CurrentSaveId;
            string originalPath = manager.GetActiveSaveFilePath("meta.json");
            byte[] original = File.ReadAllBytes(originalPath);
            manager.MarkStorySequenceCompleted("intro");
            var next = manager.CreateSave(manager.CurrentSessionId, "Manual");
            manager.SetActiveSave(manager.CurrentSessionId, next.saveId);
            Assert.That(manager.IsStorySequenceCompleted("intro"), Is.True);
            Assert.That(File.ReadAllBytes(originalPath), Is.EqualTo(original));
            manager.SetActiveSave(manager.CurrentSessionId, originalId);
            Assert.That(manager.IsStorySequenceCompleted("intro"), Is.False);
            var otherSession = manager.CreateSession("Other", SaveSessionType.Solo);
            var otherSave = manager.CreateSave(otherSession.sessionId, "Other");
            manager.SetActiveSave(otherSession.sessionId, otherSave.saveId);
            Assert.That(manager.IsStorySequenceCompleted("intro"), Is.False);
        });
    }

    [Test] public void RenamingActiveSessionUpdatesCatalogAndEveryExistingSaveMetadata()
    {
        WithStorySave(manager =>
        {
            SaveSlotInfo second = manager.CreateSave(manager.CurrentSessionId, "Deuxieme");
            Assert.That(manager.RenameActiveSession("Eowyn"), Is.True);
            Assert.That(manager.CurrentSessionName, Is.EqualTo("Eowyn"));
            Assert.That(manager.GetSession(manager.CurrentSessionId).sessionName, Is.EqualTo("Eowyn"));

            foreach (SaveSlotInfo slot in manager.GetSession(manager.CurrentSessionId).saves)
            {
                Assert.That(slot.sessionName, Is.EqualTo("Eowyn"));
                SaveMeta meta = UnityEngine.JsonUtility.FromJson<SaveMeta>(
                    File.ReadAllText(Path.Combine(slot.directoryPath, "meta.json")));
                Assert.That(meta.sessionName, Is.EqualTo("Eowyn"));
            }

            Assert.That(second, Is.Not.Null);
        });
    }

    [Test, Platform("Win")]
    public void FailedStorySaveKeepsTheCommittedCompletionState()
    {
        WithStorySave(manager =>
        {
            string path = manager.GetActiveSaveFilePath("meta.json");
            byte[] original = File.ReadAllBytes(path);
            manager.MarkStorySequenceCompleted("intro");
            manager.TryPrepareSaveMetadata("Maison_Core", out _, out byte[] bytes);
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert.Catch<Exception>(() => SaveMetadataWriter.WriteBatchAtomic(
                    new Dictionary<string, byte[]> { { path, bytes } }));
            SaveMetadataWriter.RecoverPendingBatch(Path.GetDirectoryName(path));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            Assert.That(manager.IsStorySequenceCompleted("intro"), Is.True);
            manager.SetActiveSave(manager.CurrentSessionId, manager.CurrentSaveId);
            Assert.That(manager.IsStorySequenceCompleted("intro"), Is.False);
        });
    }

    [Test] public void NoSaveStoryCompletionIsClearedBetweenGameplaySessions()
    {
        var sequence = UnityEngine.ScriptableObject.CreateInstance<Lit.Story.StorySequenceAsset>();
        sequence.sequenceId = "isolated-story-" + Guid.NewGuid().ToString("N");
        try
        {
            Assert.That(Lit.Story.StorySequenceCompletionStore.HasActiveSave, Is.False);
            Lit.Story.StorySequenceCompletionStore.MarkCompleted(sequence);
            Assert.That(Lit.Story.StorySequenceCompletionStore.IsCompleted(sequence), Is.True);
            Lit.Story.StorySequenceCompletionStore.ResetSessionCompletions();
            Assert.That(Lit.Story.StorySequenceCompletionStore.IsCompleted(sequence), Is.False);
        }
        finally
        {
            Lit.Story.StorySequenceCompletionStore.ResetSessionCompletions();
            UnityEngine.Object.DestroyImmediate(sequence);
        }
    }

    private static PrivateLobbyState Lobby() => new PrivateLobbyState
    { phase = PrivateSessionPhase.Lobby, characterIds = new[] { "a", "b", "c", "d" } };

    [Test] public void FourPlayersReceiveDistinctCharactersAndFifthIsRejected()
    {
        PrivateLobbyState lobby = Lobby();
        for (ulong i = 0; i < 4; i++) Assert.That(lobby.Add(i), Is.True);
        Assert.That(lobby.Add(4), Is.False);
        Assert.That(lobby.members.ConvertAll(m => m.characterId), Is.Unique);
    }
    [Test] public void ConcurrentReservationCannotStealACharacter()
    {
        PrivateLobbyState lobby = Lobby(); lobby.Add(0); lobby.Add(1);
        Assert.That(lobby.Reserve(0, "c"), Is.True);
        Assert.That(lobby.Reserve(1, "c"), Is.False);
        Assert.That(lobby.members[1].characterId, Is.EqualTo("b"));
    }
    [Test] public void CompositionAndCharacterChangesInvalidateReady()
    {
        PrivateLobbyState lobby = Lobby(); lobby.Add(0); lobby.members[0].ready = true;
        Assert.That(lobby.CanStart, Is.True);
        lobby.Add(1); Assert.That(lobby.CanStart, Is.False);
        foreach (PrivateLobbyMember member in lobby.members) member.ready = true;
        Assert.That(lobby.CanStart, Is.True);
        lobby.Reserve(1, "d"); Assert.That(lobby.CanStart, Is.False);
    }
    [Test] public void LaunchRequiresLobbyPhaseAndLateJoinUsesFreeCharacter()
    {
        PrivateLobbyState lobby = Lobby(); lobby.Add(0); lobby.members[0].ready = true;
        lobby.phase = PrivateSessionPhase.Playing;
        Assert.That(lobby.CanStart, Is.False);
        Assert.That(lobby.Reserve(0, "b"), Is.False);
        Assert.That(lobby.Add(1), Is.True);
        Assert.That(lobby.members[1].characterId, Is.EqualTo("b"));
    }
    [Test] public void RepeatedConnectedCallbackDoesNotAddAnotherMember()
    {
        PrivateLobbyState lobby = Lobby(); lobby.Add(0); lobby.members[0].ready = true;
        int revision = lobby.revision;
        Assert.That(lobby.Add(0), Is.True);
        Assert.That(lobby.members.Count, Is.EqualTo(1));
        Assert.That(lobby.revision, Is.EqualTo(revision));
    }
    [Test] public void MetadataReplacementIsCompleteAndLeavesNoTemporaryFiles()
    {
        string root = Path.Combine(Path.GetTempPath(), "LitMetadataTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "meta.json");
            SaveMetadataWriter.WriteAtomic(path, "{\"name\":\"avant\"}");
            SaveMetadataWriter.WriteAtomic(path, "{\"name\":\"après\"}");
            Assert.That(File.ReadAllText(path), Is.EqualTo("{\"name\":\"après\"}"));
            Assert.That(Directory.GetFiles(root).Length, Is.EqualTo(1));
            Assert.Throws<DirectoryNotFoundException>(() => SaveMetadataWriter.WriteAtomic(Path.Combine(root, "absent", "meta.json"), "{}"));
            Assert.That(File.ReadAllText(path), Is.EqualTo("{\"name\":\"après\"}"));
        }
        finally { Directory.Delete(root, true); }
    }
    [Test] public void InvalidDurationsCannotBreakTheSaveBrowser()
    {
        Assert.That(MainMenuSaveCatalog.SafePlaytime(float.NaN), Is.Zero);
        Assert.That(MainMenuSaveCatalog.SafePlaytime(float.PositiveInfinity), Is.Zero);
        Assert.That(MainMenuSaveCatalog.SafePlaytime(-1), Is.Zero);
    }

    [Test] public void BinarySaveReplacementPreservesOldSaveWhenTheDestinationIsLocked()
    {
        string root = Path.Combine(Path.GetTempPath(), "LitWorldSaveTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "WorldSnapshot.bin");
        byte[] original = { 1, 2, 3, 4 };
        byte[] replacement = { 5, 6, 7 };
        try
        {
            SaveMetadataWriter.WriteBytesAtomic(path, original);
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert.Throws<IOException>(() => SaveMetadataWriter.WriteBytesAtomic(path, replacement));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            Assert.That(Directory.GetFiles(root).Length, Is.EqualTo(1));
            SaveMetadataWriter.WriteBytesAtomic(path, replacement);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(replacement));
            Assert.That(Directory.GetFiles(root).Length, Is.EqualTo(1));
        }
        finally { Directory.Delete(root, true); }
    }

    [Test] public void EmptyAndUnsupportedWorldSavesAreRejected()
    {
        var serializer = new SnapshotSerializer();
        Assert.Throws<InvalidDataException>(() => serializer.Deserialize(Array.Empty<byte>()));
        var bytes = serializer.Serialize(new WorldSnapshot());
        Array.Copy(BitConverter.GetBytes(WorldSnapshot.CurrentSchemaVersion + 1), bytes, sizeof(int));
        Assert.Throws<InvalidDataException>(() => serializer.Deserialize(bytes));
    }

    [Test] public void WorldSaveRejectsTrailingDataInsteadOfRestoringAnAmbiguousPayload()
    {
        var serializer = new SnapshotSerializer();
        var bytes = serializer.Serialize(new WorldSnapshot { SceneName = "District_1_Core" });
        Assert.That(serializer.Deserialize(bytes).SceneName, Is.EqualTo("District_1_Core"));
        Array.Resize(ref bytes, bytes.Length + 1);
        Assert.Throws<InvalidDataException>(() => serializer.Deserialize(bytes));
    }
}

public sealed class SaveTransactionTests
{
    private string root;

    [SetUp] public void SetUp()
    {
        root = Path.Combine(Path.GetTempPath(), "LitSaveTransaction-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
    }

    [TearDown] public void TearDown() => Directory.Delete(root, true);

    private string Target(string name) => Path.Combine(root, name);

    [Test] public void BatchPublishesAllThreePayloadsAndCleansRecoveryFiles()
    {
        var payloads = new Dictionary<string, byte[]>();
        foreach (string name in new[] { "CharacterState.json", "WorldSnapshot.bin", "meta.json" })
        {
            File.WriteAllText(Target(name), "old");
            payloads.Add(Target(name), Encoding.UTF8.GetBytes("new-" + name));
        }
        SaveMetadataWriter.WriteBatchAtomic(payloads);
        foreach (var payload in payloads) Assert.That(File.ReadAllBytes(payload.Key), Is.EqualTo(payload.Value));
        Assert.That(Directory.GetFileSystemEntries(root).Length, Is.EqualTo(3));
        SaveMetadataWriter.RecoverPendingBatch(root);
        foreach (var payload in payloads) Assert.That(File.ReadAllBytes(payload.Key), Is.EqualTo(payload.Value));
    }

    [Test] public void FirstSavePublishesTheCompleteSet()
    {
        SaveMetadataWriter.WriteBatchAtomic(new Dictionary<string, byte[]>
        {
            { Target("CharacterState.json"), new byte[] { 1 } },
            { Target("WorldSnapshot.bin"), new byte[] { 2 } },
            { Target("meta.json"), new byte[] { 3 } }
        });
        Assert.That(File.ReadAllBytes(Target("CharacterState.json")), Is.EqualTo(new byte[] { 1 }));
        Assert.That(File.ReadAllBytes(Target("WorldSnapshot.bin")), Is.EqualTo(new byte[] { 2 }));
        Assert.That(File.ReadAllBytes(Target("meta.json")), Is.EqualTo(new byte[] { 3 }));
        Assert.That(Directory.GetFileSystemEntries(root).Length, Is.EqualTo(3));
    }

    [Test, Platform("Win")]
    public void LockedLaterFileCannotLeaveAnAcceptedMixedGeneration()
    {
        File.WriteAllText(Target("CharacterState.json"), "old-character");
        File.WriteAllText(Target("WorldSnapshot.bin"), "old-world");
        using (var locked = new FileStream(Target("WorldSnapshot.bin"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.Catch<Exception>(() => SaveMetadataWriter.WriteBatchAtomic(new Dictionary<string, byte[]>
            {
                { Target("CharacterState.json"), Encoding.UTF8.GetBytes("new-character") },
                { Target("WorldSnapshot.bin"), Encoding.UTF8.GetBytes("new-world") }
            }));
            Assert.That(File.ReadAllText(Target("CharacterState.json")), Is.EqualTo("old-character"));
            Assert.That(File.Exists(Target(".lit-save-journal")), Is.True);
            Assert.Throws<IOException>(() => SaveMetadataWriter.RecoverPendingBatch(root));
        }
        SaveMetadataWriter.RecoverPendingBatch(root);
        Assert.That(File.ReadAllText(Target("CharacterState.json")), Is.EqualTo("old-character"));
        Assert.That(File.ReadAllText(Target("WorldSnapshot.bin")), Is.EqualTo("old-world"));
        Assert.That(Directory.GetFileSystemEntries(root).Length, Is.EqualTo(2));
    }

    // Simulate a stopped process after the first target replacement, using
    // the actual on-disk journal format rather than a special runtime hook.
    private string InterruptedBatch(bool committed, bool characterExisted = true, string firstName = "CharacterState.json")
    {
        string id = Guid.NewGuid().ToString("N");
        string stage = Target(".lit-save-stage-" + id);
        Directory.CreateDirectory(stage);
        if (characterExisted) File.WriteAllText(Path.Combine(stage, "old-0"), "old-character");
        File.WriteAllText(Path.Combine(stage, "old-1"), "old-world");
        File.WriteAllText(Target(firstName), "new-character");
        File.WriteAllText(Target("WorldSnapshot.bin"), committed ? "new-world" : "old-world");
        using (var writer = new BinaryWriter(File.Create(Target(".lit-save-journal")), Encoding.UTF8))
        {
            writer.Write(0x4C495453);
            writer.Write(id);
            writer.Write(2);
            writer.Write(firstName);
            writer.Write(characterExisted);
            writer.Write("WorldSnapshot.bin");
            writer.Write(true);
        }
        if (committed) File.WriteAllBytes(Path.Combine(stage, "committed"), new byte[] { 1 });
        return stage;
    }

    [Test] public void InterruptedPublicationRestoresThePreviousGenerationIdempotently()
    {
        InterruptedBatch(false);
        SaveMetadataWriter.RecoverPendingBatch(root);
        SaveMetadataWriter.RecoverPendingBatch(root);
        Assert.That(File.ReadAllText(Target("CharacterState.json")), Is.EqualTo("old-character"));
        Assert.That(File.ReadAllText(Target("WorldSnapshot.bin")), Is.EqualTo("old-world"));
        Assert.That(Directory.GetFileSystemEntries(root).Length, Is.EqualTo(2));
    }

    [Test] public void InterruptedFirstPublicationRemovesFilesThatDidNotExistBefore()
    {
        InterruptedBatch(false, false);
        SaveMetadataWriter.RecoverPendingBatch(root);
        Assert.That(File.Exists(Target("CharacterState.json")), Is.False);
        Assert.That(File.ReadAllText(Target("WorldSnapshot.bin")), Is.EqualTo("old-world"));
        Assert.That(Directory.GetFileSystemEntries(root).Length, Is.EqualTo(1));
    }

    [Test] public void InterruptedCommittedCleanupNeverRollsNewFilesBack()
    {
        string stage = InterruptedBatch(true);
        File.Delete(Path.Combine(stage, "old-0"));
        SaveMetadataWriter.RecoverPendingBatch(root);
        Assert.That(File.ReadAllText(Target("CharacterState.json")), Is.EqualTo("new-character"));
        Assert.That(File.ReadAllText(Target("WorldSnapshot.bin")), Is.EqualTo("new-world"));
        Assert.That(Directory.GetFileSystemEntries(root).Length, Is.EqualTo(2));
    }

    [Test] public void MissingBackupRefusesRecoveryBeforeChangingAnyTarget()
    {
        string stage = InterruptedBatch(false);
        File.Delete(Path.Combine(stage, "old-1"));
        Assert.Throws<InvalidDataException>(() => SaveMetadataWriter.RecoverPendingBatch(root));
        Assert.That(File.ReadAllText(Target("CharacterState.json")), Is.EqualTo("new-character"));
        Assert.That(File.Exists(Target(".lit-save-journal")), Is.True);
    }

    [Test] public void MetadataReaderRecoversBeforeCheckingWhetherItsFileExists()
    {
        string stage = InterruptedBatch(false, true, "meta.json");
        File.WriteAllText(Path.Combine(stage, "old-0"), "{\"sceneName\":\"Maison_Core\"}");
        File.Delete(Target("meta.json"));
        var read = typeof(SaveSessionManager).GetMethod("ReadJson",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.That(read, Is.Not.Null);
        var meta = (SaveMeta)read.MakeGenericMethod(typeof(SaveMeta)).Invoke(null, new object[] { Target("meta.json") });
        Assert.That(meta, Is.Not.Null);
        Assert.That(meta.sceneName, Is.EqualTo("Maison_Core"));
        Assert.That(File.ReadAllText(Target("WorldSnapshot.bin")), Is.EqualTo("old-world"));
        Assert.That(File.Exists(Target(".lit-save-journal")), Is.False);
    }

    [Test] public void MetadataReaderRefusesPartiallyRecoveredSave()
    {
        string stage = InterruptedBatch(false, true, "meta.json");
        File.WriteAllText(Target("meta.json"), "{\"sceneName\":\"District_1_Core\"}");
        File.Delete(Path.Combine(stage, "old-1"));
        var read = typeof(SaveSessionManager).GetMethod("ReadJson",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var meta = read.MakeGenericMethod(typeof(SaveMeta)).Invoke(null, new object[] { Target("meta.json") });
        Assert.That(meta, Is.Null, "An unfinished recovery must not become a loadable catalog entry.");
        Assert.That(File.Exists(Target(".lit-save-journal")), Is.True);
    }

    [Test] public void PlayableCatalogRecoversThePreviousCharacterPayload()
    {
        string stage = InterruptedBatch(false);
        File.WriteAllText(Path.Combine(stage, "old-0"), "{\"characters\":[{\"characterId\":\"lucian\"}]}");
        File.Delete(Target("CharacterState.json"));
        Assert.That(MainMenuSaveCatalog.IsPlayable(new SaveSlotInfo { validMetadata = true, directoryPath = root }), Is.True);
        Assert.That(File.ReadAllText(Target("WorldSnapshot.bin")), Is.EqualTo("old-world"));
        Assert.That(File.Exists(Target(".lit-save-journal")), Is.False);
    }

    [Test] public void PlayableCatalogRefusesAReadableCharacterFromAnUnrecoverableBatch()
    {
        string stage = InterruptedBatch(false);
        File.WriteAllText(Target("CharacterState.json"), "{\"characters\":[{\"characterId\":\"lucian\"}]}");
        File.Delete(Path.Combine(stage, "old-1"));
        Assert.That(MainMenuSaveCatalog.IsPlayable(new SaveSlotInfo { validMetadata = true, directoryPath = root }), Is.False);
        Assert.That(File.Exists(Target(".lit-save-journal")), Is.True);
    }

    [Test] public void InvalidBatchIsRejectedBeforeTouchingExistingFiles()
    {
        File.WriteAllText(Target("CharacterState.json"), "old-character");
        Assert.Throws<ArgumentException>(() => SaveMetadataWriter.WriteBatchAtomic(new Dictionary<string, byte[]>
        {
            { Target("CharacterState.json"), new byte[] { 1 } },
            { Target(".lit-save-journal"), new byte[] { 2 } }
        }));
        Assert.That(File.ReadAllText(Target("CharacterState.json")), Is.EqualTo("old-character"));
        Assert.That(Directory.GetFileSystemEntries(root).Length, Is.EqualTo(1));
    }

    [TestCase(180), TestCase(200), Platform("Win")]
    public void DeepWindowsSlotPublishesAndReplacesWithoutLongTemporaryPaths(int pathLength)
    {
        WithDeepSlot(pathLength, () =>
        {
            var payloads = new Dictionary<string, byte[]>
            {
                { Target("CharacterState.json"), Encoding.UTF8.GetBytes("character") },
                { Target("WorldSnapshot.bin"), Encoding.UTF8.GetBytes("world") },
                { Target("meta.json"), Encoding.UTF8.GetBytes("metadata") }
            };
            SaveMetadataWriter.WriteBatchAtomic(payloads);
            SaveMetadataWriter.WriteBatchAtomic(payloads);
            foreach (var payload in payloads) Assert.That(File.ReadAllBytes(payload.Key), Is.EqualTo(payload.Value));
            Assert.That(Directory.GetFileSystemEntries(root).Length, Is.EqualTo(3));
        });
    }

    [TestCase(180), TestCase(200), Platform("Win")]
    public void DeepWindowsSlotRecoversInterruptedPublicationWithoutLongTemporaryPaths(int pathLength)
    {
        WithDeepSlot(pathLength, () =>
        {
            InterruptedBatch(false);
            SaveMetadataWriter.RecoverPendingBatch(root);
            SaveMetadataWriter.RecoverPendingBatch(root);
            Assert.That(File.ReadAllText(Target("CharacterState.json")), Is.EqualTo("old-character"));
            Assert.That(File.ReadAllText(Target("WorldSnapshot.bin")), Is.EqualTo("old-world"));
            Assert.That(Directory.GetFileSystemEntries(root).Length, Is.EqualTo(2));
        });
    }

    private void WithDeepSlot(int pathLength, Action test)
    {
        string original = root;
        root = Path.Combine(original, new string('s', pathLength - original.Length - 1));
        Directory.CreateDirectory(root);
        try { test(); }
        finally { root = original; }
    }
}
