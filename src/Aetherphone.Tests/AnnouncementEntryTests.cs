using System.Text.Json;
using Aetherphone.Apps.Announcements;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class AnnouncementEntryTests
{
    private const string Cover = "https://media.example/announcement/cover.png";
    private const string Second = "https://media.example/announcement/second.png";
    private const string StoreUrl = "https://aetherphone.net/store";

    private static AnnouncementDto Announcement(string body, params AnnouncementBlockDto[]? blocks) =>
        new("a1", "Title", body, Array.Empty<AnnouncementTranslationDto>(), 1_760_000_000, blocks);

    private static AnnouncementBlockDto Text(string text, AnnouncementBlockTranslationDto[]? translations = null) =>
        new("text", text, string.Empty, string.Empty, 0, 0, translations);

    private static AnnouncementBlockDto Image(string url, int width = 1600, int height = 900) =>
        new("image", string.Empty, string.Empty, url, width, height);

    private static AnnouncementBlockDto Link(string label, string url) =>
        new("link", label, url, string.Empty, 0, 0);

    private static AnnouncementEntry Build(AnnouncementDto announcement)
    {
        var entry = new AnnouncementEntry();
        entry.Rebuild(announcement);
        return entry;
    }

    [Fact]
    public void LegacyBodySplitsIntoLinesAndOpensAParagraphAfterABlankLine()
    {
        var entry = Build(Announcement("First line\nSecond line\n\nNew paragraph", null));

        Assert.Equal(3, entry.Pieces.Length);
        Assert.All(entry.Pieces, piece => Assert.Equal(AnnouncementPieceKind.Line, piece.Kind));
        Assert.Equal(AnnouncementGap.None, entry.Pieces[0].Gap);
        Assert.Equal(AnnouncementGap.None, entry.Pieces[1].Gap);
        Assert.Equal(AnnouncementGap.Paragraph, entry.Pieces[2].Gap);
        Assert.Empty(entry.ImageUrls);
        Assert.False(entry.HasCover);
        Assert.Equal("First line Second line New paragraph", entry.Preview);
    }

    [Fact]
    public void EmptyBlockListFallsBackToTheBody()
    {
        var entry = Build(Announcement("Plain body", Array.Empty<AnnouncementBlockDto>()));

        Assert.Single(entry.Pieces);
        Assert.Equal("Plain body", entry.Body);
    }

    [Fact]
    public void BlocksKeepTheirOrderAndSeparateWithABlockGap()
    {
        var entry = Build(Announcement("ignored", Text("Intro"), Image(Cover), Link("Open the store", StoreUrl),
            Text("Outro")));

        Assert.Equal(4, entry.Pieces.Length);
        Assert.Equal(AnnouncementPieceKind.Line, entry.Pieces[0].Kind);
        Assert.Equal(AnnouncementPieceKind.Image, entry.Pieces[1].Kind);
        Assert.Equal(AnnouncementPieceKind.Link, entry.Pieces[2].Kind);
        Assert.Equal(AnnouncementPieceKind.Line, entry.Pieces[3].Kind);
        Assert.Equal(AnnouncementGap.None, entry.Pieces[0].Gap);
        Assert.Equal(AnnouncementGap.Block, entry.Pieces[1].Gap);
        Assert.Equal(AnnouncementGap.Block, entry.Pieces[2].Gap);
        Assert.Equal(AnnouncementGap.Block, entry.Pieces[3].Gap);
    }

    [Fact]
    public void BodyJoinsTextAndLinksWhilePreviewKeepsOnlyText()
    {
        var entry = Build(Announcement("ignored", Text("Intro"), Image(Cover), Link("Open the store", StoreUrl),
            Text("Outro")));

        Assert.Equal("Intro\n\nOpen the store: " + StoreUrl + "\n\nOutro", entry.Body);
        Assert.Equal("Intro Outro", entry.Preview);
        Assert.True(entry.Matches("store"));
    }

    [Fact]
    public void TextBlocksKeepTheirOwnParagraphs()
    {
        var entry = Build(Announcement("ignored", Text("One\n\nTwo"), Text("Three")));

        Assert.Equal(3, entry.Pieces.Length);
        Assert.Equal(AnnouncementGap.None, entry.Pieces[0].Gap);
        Assert.Equal(AnnouncementGap.Paragraph, entry.Pieces[1].Gap);
        Assert.Equal(AnnouncementGap.Block, entry.Pieces[2].Gap);
    }

    [Fact]
    public void ImagesCarryTheirAspectAndIndexAndTheFirstIsTheCover()
    {
        var entry = Build(Announcement("ignored", Image(Cover), Text("Between"), Image(Second, 900, 1800)));

        Assert.Equal(new[] { Cover, Second }, entry.ImageUrls);
        Assert.True(entry.HasCover);
        Assert.Equal(Cover, entry.CoverUrl);
        Assert.Equal(1600f / 900f, entry.CoverAspect, 4);
        Assert.Equal(0, entry.Pieces[0].ImageIndex);
        Assert.Equal(1600f / 900f, entry.Pieces[0].Aspect, 4);
        Assert.Equal(1, entry.Pieces[2].ImageIndex);
        Assert.Equal(0.5f, entry.Pieces[2].Aspect, 4);
    }

    [Fact]
    public void ImageWithoutASizeFallsBackToWidescreen()
    {
        var entry = Build(Announcement("ignored", Image(Cover, 0, 0)));

        Assert.Equal(16f / 9f, entry.Pieces[0].Aspect, 4);
    }

    [Fact]
    public void UnknownKindsAndEmptyBlocksAreSkipped()
    {
        var entry = Build(Announcement("ignored",
            new AnnouncementBlockDto("video", "clip", "https://example.com", string.Empty, 0, 0),
            Text("   "), Image(string.Empty), Link("Label", string.Empty)));

        Assert.Empty(entry.Pieces);
        Assert.Empty(entry.ImageUrls);
        Assert.Equal(string.Empty, entry.Body);
    }

    [Fact]
    public void LinkWithoutALabelShowsItsUrl()
    {
        var entry = Build(Announcement("ignored", Link("  ", StoreUrl)));

        Assert.Equal(StoreUrl, entry.Pieces[0].Text);
        Assert.Equal(StoreUrl, entry.Pieces[0].Url);
        Assert.Equal(StoreUrl, entry.Body);
    }

    [Fact]
    public void LinkKeysAreUniquePerBlock()
    {
        var entry = Build(Announcement("ignored", Link("One", StoreUrl), Link("Two", StoreUrl)));

        Assert.NotEqual(entry.Pieces[0].Key, entry.Pieces[1].Key);
    }

    [Fact]
    public void ServerPageWithBlocksDeserializes()
    {
        const string json = """
            {"items":[{"id":"a1","title":"T","body":"B","translations":[],"createdAtUnix":5,
            "blocks":[{"kind":"image","text":"","url":"","imageUrl":"https://cdn.example/a.png","width":1600,"height":900,
            "translations":null},{"kind":"link","text":"Go","url":"https://example.com","imageUrl":"","width":0,"height":0,
            "translations":[{"lang":"fr","text":"Aller"}]}]}],"nextCursor":null}
            """;

        var page = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.AnnouncementPage);

        var blocks = page!.Items[0].Blocks!;
        Assert.Equal("image", blocks[0].Kind);
        Assert.Equal("https://cdn.example/a.png", blocks[0].ImageUrl);
        Assert.Equal(1600, blocks[0].Width);
        Assert.Equal(900, blocks[0].Height);
        Assert.Equal("https://example.com", blocks[1].Url);
        Assert.Equal("Aller", blocks[1].Translations![0].Text);
    }

    [Fact]
    public void ServerPageWithoutBlocksStillDeserializes()
    {
        const string json = """
            {"items":[{"id":"a1","title":"T","body":"B","translations":[],"createdAtUnix":5}]}
            """;

        var page = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.AnnouncementPage);

        Assert.Null(page!.Items[0].Blocks);
    }

    [Fact]
    public void BlockTextFollowsTheCurrentLanguageAndFallsBackWhenEmpty()
    {
        var code = Loc.Current.Code;
        var translated = Text("English", new[] { new AnnouncementBlockTranslationDto(code, "Localized") });
        var blank = Text("English", new[] { new AnnouncementBlockTranslationDto(code, string.Empty) });

        Assert.Equal("Localized", AnnouncementText.For(translated));
        Assert.Equal("English", AnnouncementText.For(blank));
    }
}
