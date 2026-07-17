using Aetherphone.Core.Linkpearl;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MessageArchiveTests
{
    [Fact]
    public void SaveThenLoadRoundTrips()
    {
        var root = TempRoot();
        try
        {
            var archive = new MessageArchive(root);
            var lines = new List<ChatLine>
            {
                new(MessageDirection.Incoming, "hey", DateTime.Now),
                new(MessageDirection.Outgoing, "hi there", DateTime.Now.AddSeconds(5)),
            };
            archive.Save("Aiko Braveheart", "Aiko Braveheart@Odin", lines);

            var loaded = new MessageArchive(root).LoadAll();
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
            archive.Save("Aiko", "Aiko@Odin", new List<ChatLine> { new(MessageDirection.Incoming, "hey", DateTime.Now) });
            archive.Delete("Aiko@Odin");

            Assert.Empty(new MessageArchive(root).LoadAll());
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
            var lines = new List<ChatLine>(520);
            for (var index = 0; index < 520; index++)
            {
                lines.Add(new ChatLine(MessageDirection.Incoming, $"line {index}", DateTime.Now.AddSeconds(index)));
            }

            archive.Save("Aiko", "Aiko@Odin", lines);

            var loaded = new MessageArchive(root).LoadAll();
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
            archive.Save("Aiko", "Aiko@Odin", new List<ChatLine> { new(MessageDirection.Incoming, "hey", DateTime.Now) });

            var files = root.GetFiles("*.json");
            Assert.Single(files);
            Assert.DoesNotContain("aiko", files[0].Name, StringComparison.OrdinalIgnoreCase);
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
