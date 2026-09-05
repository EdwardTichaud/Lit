using System;
using System.IO;
using NUnit.Framework;

public sealed class MainMenuSessionTests
{
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
}
