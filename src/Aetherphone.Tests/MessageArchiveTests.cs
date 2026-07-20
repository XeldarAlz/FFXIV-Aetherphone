using Aetherphone.Core.Linkpearl;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MessageArchiveTests
{
    private const ulong CharacterId = 0x0123456789abcdef;

    [Fact]
    public void SaveThenLoadRoundTrips()
    {
        var root = TempRoot();
        try
        {
            var archive = new MessageArchive(root);
            archive.SetCharacter(CharacterId);
            var lines = new List<ChatLine>
            {
                new(MessageDirection.Incoming, "hey", DateTime.Now),
                new(MessageDirection.Outgoing, "hi there", DateTime.Now.AddSeconds(5)),
            };
            archive.Save("Aiko Braveheart", "Aiko Braveheart@Odin", lines);

            var reopened = new MessageArchive(root);
            reopened.SetCharacter(CharacterId);
            var loaded = reopened.LoadAll();
            Assert.Single(loaded);
            Assert.Equal("Aiko Braveheart@Odin", loaded[0].SendTarget);
            Assert.Equal("Aiko Braveheart", loaded[0].Contact);
            Assert.Equal(2, loaded[0].Lines.Count);
            Assert.Equal("hi there", loaded[0].Lines[1].Text);
            Assert.Equal(MessageDirection.Outgoing, loaded[0].Lines[1].Direction);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void DeleteRemovesTheConversation()
    {
        var root = TempRoot();
        try
        {
            var archive = new MessageArchive(root);
            archive.SetCharacter(CharacterId);
            archive.Save("Aiko", "Aiko@Odin", new List<ChatLine> { new(MessageDirection.Incoming, "hey", DateTime.Now) });
            archive.Delete("Aiko@Odin");

            var reopened = new MessageArchive(root);
            reopened.SetCharacter(CharacterId);
            Assert.Empty(reopened.LoadAll());
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void SaveKeepsOnlyTheNewestFiveHundredLines()
    {
        var root = TempRoot();
        try
        {
            var archive = new MessageArchive(root);
            archive.SetCharacter(CharacterId);
            var lines = new List<ChatLine>(520);
            for (var index = 0; index < 520; index++)
            {
                lines.Add(new ChatLine(MessageDirection.Incoming, $"line {index}", DateTime.Now.AddSeconds(index)));
            }

            archive.Save("Aiko", "Aiko@Odin", lines);

            var reopened = new MessageArchive(root);
            reopened.SetCharacter(CharacterId);
            var loaded = reopened.LoadAll();
            Assert.Single(loaded);
            Assert.Equal(500, loaded[0].Lines.Count);
            Assert.Equal("line 20", loaded[0].Lines[0].Text);
            Assert.Equal("line 519", loaded[0].Lines[^1].Text);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void FileNamesDoNotLeakContactNames()
    {
        var root = TempRoot();
        try
        {
            var archive = new MessageArchive(root);
            archive.SetCharacter(CharacterId);
            archive.Save("Aiko", "Aiko@Odin", new List<ChatLine> { new(MessageDirection.Incoming, "hey", DateTime.Now) });

            var characterFolder = new DirectoryInfo(Path.Combine(root.FullName, CharacterId.ToString("x16")));
            var files = characterFolder.GetFiles("*.json");
            Assert.Single(files);
            Assert.DoesNotContain("aiko", files[0].Name, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void CharactersDoNotSeeEachOthersMessages()
    {
        var root = TempRoot();
        try
        {
            var archive = new MessageArchive(root);
            archive.SetCharacter(0x111);
            archive.Save("Main Friend", "Main Friend@Odin",
                new List<ChatLine> { new(MessageDirection.Incoming, "main tell", DateTime.Now) });

            archive.SetCharacter(0x222);
            Assert.Empty(archive.LoadAll());
            archive.Save("Alt Friend", "Alt Friend@Odin",
                new List<ChatLine> { new(MessageDirection.Incoming, "alt tell", DateTime.Now) });

            var altLoaded = archive.LoadAll();
            Assert.Single(altLoaded);
            Assert.Equal("Alt Friend@Odin", altLoaded[0].SendTarget);

            archive.SetCharacter(0x111);
            var mainLoaded = archive.LoadAll();
            Assert.Single(mainLoaded);
            Assert.Equal("Main Friend@Odin", mainLoaded[0].SendTarget);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void MigrateLegacyMovesRootFilesIntoCharacterFolder()
    {
        var root = TempRoot();
        try
        {
            root.Create();
            const string legacyJson =
                "{\"contact\":\"Old Friend\",\"target\":\"Old Friend@Odin\",\"lines\":[{\"d\":0,\"t\":\"hello\",\"u\":1000}]}";
            File.WriteAllText(Path.Combine(root.FullName, "legacy.json"), legacyJson);

            var archive = new MessageArchive(root);
            archive.MigrateLegacyTo(CharacterId);
            archive.SetCharacter(CharacterId);

            var loaded = archive.LoadAll();
            Assert.Single(loaded);
            Assert.Equal("Old Friend@Odin", loaded[0].SendTarget);
            Assert.Empty(root.GetFiles("*.json"));
        }
        finally
        {
            root.Delete(true);
        }
    }

    private static DirectoryInfo TempRoot()
    {
        return new DirectoryInfo(Path.Combine(Path.GetTempPath(), "aetherphone-tests-" + Guid.NewGuid().ToString("N")));
    }
}
