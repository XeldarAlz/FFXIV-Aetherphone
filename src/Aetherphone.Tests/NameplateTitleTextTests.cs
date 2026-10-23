using System.Numerics;
using Aetherphone.Core.Honorific;
using Xunit;

namespace Aetherphone.Tests;

public sealed class NameplateTitleTextTests
{
    private static readonly NameplateValues Song =
        NameplateValues.Empty with { Song = "Answers", Artist = "Susan Calloway" };

    [Fact]
    public void PlaceholdersFillIn()
    {
        var values = NameplateValues.Empty with { Code = "4KX 9QP", Name = "Lofi Night" };

        Assert.Equal("Jam · 4KX 9QP", NameplateTitleText.Render("Jam · [code]", values));
        Assert.Equal("Lofi Night (4KX 9QP)", NameplateTitleText.Render("[name] ([code])", values));
    }

    [Fact]
    public void GameAndChipPlaceholdersFillIn()
    {
        var values = NameplateValues.Empty with { Game = "Coil", Chips = "1,250" };

        Assert.Equal("Playing Coil", NameplateTitleText.Render("Playing [game]", values));
        Assert.Equal("Lost 1,250 chips :(", NameplateTitleText.Render("Lost [chips] chips :(", values));
    }

    [Fact]
    public void UserTextAroundPlaceholdersIsKept()
    {
        var values = NameplateValues.Empty with { Handle = "@mira" };

        Assert.Equal("My Velvet: mira", NameplateTitleText.Render("My Velvet: [handle]", values));
    }

    [Fact]
    public void EmptyValueTakesItsSeparatorWithIt()
    {
        Assert.Equal("On air", NameplateTitleText.Render("On air · [station]", NameplateValues.Empty));
    }

    [Fact]
    public void StationNameFillsTheRadioTemplate()
    {
        var values = NameplateValues.Empty with { Station = "Gridania FM" };

        Assert.Equal("On air · Gridania FM", NameplateTitleText.RenderFitted("On air · [station]", values));
    }

    [Fact]
    public void LongStationNameIsCutToTheTitleLimit()
    {
        var values = NameplateValues.Empty with { Station = "The Gold Saucer Midnight Request Hour" };

        var fitted = NameplateTitleText.RenderFitted("On air · [station]", values);

        Assert.Equal(NameplateTitleText.MaxLength, fitted.Length);
        Assert.StartsWith("On air · The Gold Saucer", fitted);
        Assert.EndsWith("…", fitted);
    }

    [Fact]
    public void UnknownPlaceholdersStayAsWritten()
    {
        Assert.Equal("Hi [there]", NameplateTitleText.Render("Hi [there]", NameplateValues.Empty));
    }

    [Fact]
    public void TextWithoutLettersOrDigitsIsEmpty()
    {
        Assert.Equal(string.Empty, NameplateTitleText.Render("@[handle]", NameplateValues.Empty));
        Assert.Equal(string.Empty, NameplateTitleText.Render(string.Empty, Song));
    }

    [Fact]
    public void LongSongDropsTheArtistBeforeCutting()
    {
        var fits = NameplateTitleText.RenderFitted("♪ [song] · [artist]", Song);
        var dropped = NameplateTitleText.RenderFitted("♪ [song] · [artist]",
            Song with { Song = "Flow (Feast of the Ancients)" });

        Assert.Equal("♪ Answers · Susan Calloway", fits);
        Assert.Equal("♪ Flow (Feast of the Ancients)", dropped);
    }

    [Fact]
    public void LongTextIsCutToHonorificLimit()
    {
        var fitted = NameplateTitleText.RenderFitted("Hosting · [type]",
            NameplateValues.Empty with { Type = "Moonlit Bards of Ul'dah Night Out" });

        Assert.Equal(NameplateTitleText.MaxLength, fitted.Length);
        Assert.EndsWith("…", fitted);
    }

    [Fact]
    public void EmptyLeadingValueTakesTheFollowingSeparator()
    {
        var noSong = Song with { Song = string.Empty };

        Assert.Equal("♪ Susan Calloway", NameplateTitleText.Render("♪ [song] · [artist]", noSong));
        Assert.Equal("On air · live", NameplateTitleText.Render("On air · [station] · live", NameplateValues.Empty));
    }

    [Fact]
    public void FittingSongIsASingleTurn()
    {
        Assert.Equal(new[] { "♪ Answers · Susan Calloway" }, NameplateTitleText.Turns("♪ [song] · [artist]", Song, false));
        Assert.Empty(NameplateTitleText.Turns("♪ [song] · [artist]", NameplateValues.Empty, false));
    }

    [Fact]
    public void SongAndArtistTakeTurnsWhenTogetherTheyDoNotFit()
    {
        var turns = NameplateTitleText.Turns("♪ [song] · [artist]", Song with { Song = "Tomorrow and Tomorrow" }, false);

        Assert.Equal(new[] { "♪ Tomorrow and Tomorrow", "♪ Susan Calloway" }, turns);
    }

    [Fact]
    public void VeryLongSongIsPagedAtWordBreaksWithoutCutting()
    {
        const string song = "Footfalls (from FINAL FANTASY XIV: Endwalker Original Soundtrack)";

        var turns = NameplateTitleText.Turns("♪ [song] · [artist]", NameplateValues.Empty with { Song = song }, false);

        Assert.True(turns.Length > 1);
        var joined = string.Empty;
        for (var index = 0; index < turns.Length; index++)
        {
            Assert.True(turns[index].Length <= NameplateTitleText.MaxLength);
            Assert.DoesNotContain("…", turns[index]);
            Assert.StartsWith("♪ ", turns[index]);
            joined = index == 0 ? turns[index][2..] : joined + " " + turns[index][2..];
        }

        Assert.Equal(song, joined);
    }

    [Fact]
    public void EachLineIsItsOwnTurnAndEmptyLinesAreSkipped()
    {
        var turns = NameplateTitleText.Turns("I am listening to\n♪ [song]\n\n[artist]", Song, false);

        Assert.Equal(new[] { "I am listening to", "♪ Answers", "Susan Calloway" }, turns);
    }

    [Fact]
    public void LongPlainLineIsPagedInsteadOfCut()
    {
        var turns = NameplateTitleText.Turns("Vibing to the finest tunes in all of Eorzea tonight",
            NameplateValues.Empty, false);

        Assert.Equal(new[] { "Vibing to the finest tunes in", "all of Eorzea tonight" }, turns);
    }

    [Fact]
    public void ShortenKeepsOneTurnPerLine()
    {
        var turns = NameplateTitleText.Turns("Now playing\n♪ [song] · [artist]",
            Song with { Song = "Tomorrow and Tomorrow" }, true);

        Assert.Equal(new[] { "Now playing", "♪ Tomorrow and Tomorrow" }, turns);
    }

    [Fact]
    public void JsonRoundTripsThroughTheReader()
    {
        var look = new TitleLook(new Vector3(1f, 0.5f, 0f), new Vector3(0.1f, 0.2f, 0.3f), null, 12, 1);
        var title = new NameplateTitle(NameplateStatus.Jam, "Jam · 4KX 9QP", look, true);

        var json = NameplateTitleText.ToJson(title);
        var read = NameplateTitleText.TryReadTitle(json, out var text, out var readLook);

        Assert.True(read);
        Assert.Equal(title.Text, text);
        Assert.Equal(look, readLook);
        Assert.Contains("\"IsPrefix\":true", json);
        Assert.Contains("\"IsOriginal\":false", json);
        Assert.DoesNotContain("Color3", json);
    }

    [Fact]
    public void ReaderIgnoresVanillaTitles()
    {
        const string vanilla = "{\"Title\":\"the Insatiable\",\"IsPrefix\":false,\"IsOriginal\":true}";

        Assert.False(NameplateTitleText.TryReadTitle(vanilla, out _, out _));
        Assert.False(NameplateTitleText.TryReadTitle(string.Empty, out _, out _));
        Assert.False(NameplateTitleText.TryReadTitle("not json", out _, out _));
    }

    [Fact]
    public void ReaderAcceptsHonorificFormatting()
    {
        const string honorific =
            "{\"Title\":\"Warrior of Light\",\"IsPrefix\":false,\"IsOriginal\":false," +
            "\"Color\":{\"X\":1.0,\"Y\":0.8,\"Z\":0.2},\"Glow\":null,\"Color3\":null," +
            "\"GradientColourSet\":3,\"GradientAnimationStyle\":0}";

        var read = NameplateTitleText.TryReadTitle(honorific, out var text, out var look);

        Assert.True(read);
        Assert.Equal("Warrior of Light", text);
        Assert.Null(look.Glow);
        Assert.Equal(3, look.GradientColourSet);
        Assert.Equal(0, look.GradientAnimationStyle);
    }
}
