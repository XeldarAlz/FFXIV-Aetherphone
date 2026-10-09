using System.Numerics;
using System.Text.Json;
using Aetherphone.Apps.Casino;
using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoRoomCodeTests
{
    private const float PhoneWidth = 330f;

    public static TheoryData<float> Scales => new() { 0.75f, 0.9f, 1f, 1.25f, 1.5f };

    [Theory]
    [InlineData("ABCDEF", "ABCDEF")]
    [InlineData("abc-def", "ABCDEF")]
    [InlineData("ABC DEF", "ABCDEF")]
    [InlineData("q7m 4kd", "Q7M4KD")]
    [InlineData("  Q7M-4KD  ", "Q7M4KD")]
    public void CodesNormalizeLikeTheServer(string raw, string code)
    {
        Assert.Equal(code, CasinoRoomCodes.Normalized(raw));
        Assert.True(CasinoRoomCodes.IsCode(raw));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABCDE")]
    [InlineData("ABCDEFG")]
    [InlineData("ABCDE1")]
    [InlineData("ABCDEO")]
    [InlineData("ABC_DEF")]
    [InlineData("A B C D E F G H I")]
    [InlineData("[aep.casino.v1:table-442d]")]
    public void AnythingElseIsNotACode(string raw)
    {
        Assert.Equal(string.Empty, CasinoRoomCodes.Normalized(raw));
        Assert.False(CasinoRoomCodes.IsCode(raw));
    }

    [Fact]
    public void TheAlphabetMatchesTheGamesRooms()
    {
        Assert.Equal(6, CasinoRoomCodes.Length);
        Assert.Equal(16, CasinoRoomCodes.RawMaxLength);
        Assert.Equal("ABCDEFGHJKMNPQRSTUVWXYZ23456789", CasinoRoomCodes.Alphabet);
    }

    [Fact]
    public void TheJoinRouteAndBodyMatchTheServer()
    {
        Assert.Equal("/casino/tables/join", CasinoClient.JoinTablePath);
        Assert.Equal("roomcodes", CasinoFeatures.RoomCodes);
        var body = JsonSerializer.Serialize(new CasinoTableJoinRequest("Q7M4KD"),
            AethernetJsonContext.Default.CasinoTableJoinRequest);
        Assert.Equal("{\"code\":\"Q7M4KD\"}", body);
    }

    [Fact]
    public void TheJoinAnswerCarriesTheCardAndItsCode()
    {
        const string json = "{\"granted\":true,\"reason\":\"knock_pending\",\"tableId\":\"table-01J9\","
            + "\"table\":{\"tableId\":\"table-01J9\",\"gameKind\":\"blackjack\",\"kind\":1,\"listing\":1,"
            + "\"admitted\":false,\"reason\":\"knock_pending\",\"inviteToken\":\"[aep.casino.v1:table-01J9]\","
            + "\"joinCode\":\"Q7M4KD\"}}";
        var answer = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoTableJoinDto)!;
        Assert.True(answer.Granted);
        Assert.Equal(CasinoReasons.KnockPending, answer.Reason);
        Assert.Equal("table-01J9", answer.TableId);
        Assert.Equal("Q7M4KD", answer.Table!.JoinCode);
        Assert.False(answer.Table.Admitted);
    }

    [Fact]
    public void TheDoorCarriesTheCodeAndOldAnswersStillRead()
    {
        var door = JsonSerializer.Deserialize("{\"roomId\":\"t1\",\"owner\":true,\"joinCode\":\"ABCDEF\"}",
            AethernetJsonContext.Default.CasinoTableDoorDto)!;
        Assert.Equal("ABCDEF", door.JoinCode);
        var old = JsonSerializer.Deserialize("{\"roomId\":\"t1\",\"owner\":true}",
            AethernetJsonContext.Default.CasinoTableDoorDto)!;
        Assert.Equal(string.Empty, old.JoinCode);
        var card = JsonSerializer.Deserialize("{\"tableId\":\"t1\"}", AethernetJsonContext.Default.CasinoTableRowDto)!;
        Assert.Equal(string.Empty, card.JoinCode);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void TheCodeCardKeepsLabelCopyCellsAndHintApart(float scale)
    {
        CheckCodeCard(PhoneWidth * scale, scale);
        CheckCodeCard(280f * scale, scale);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void RoomCardsKeepArtTextPhaseAndChevronApart(float scale)
    {
        var headline = 22f * scale;
        var footnote = 18f * scale;
        var row = 22f * scale;
        var height = TableCardLayout.Height(scale, headline, footnote, row, 0f);
        var card = new Rect(Vector2.Zero, new Vector2(PhoneWidth * scale, height));
        var layout = TableCardLayout.Compute(card, scale, headline, footnote, row, 90f * scale, 22f * scale);

        Assert.True(layout.Art.Max.X < layout.TextLeft);
        Assert.True(layout.NameRight > layout.TextLeft);
        Assert.True(layout.NameRight <= layout.Phase.Min.X);
        Assert.True(layout.Phase.Max.X < layout.Chevron.X);
        Assert.True(layout.LineRight < layout.Chevron.X);
        Assert.True(layout.LineTop >= layout.NameTop + headline);
        Assert.True(layout.RowTop >= layout.LineTop + footnote);
        Assert.True(layout.RowTop + row <= card.Max.Y + 0.01f);
        Assert.True(layout.Art.Max.Y <= card.Max.Y);
        Assert.True(layout.Avatar.X + layout.AvatarRadius < layout.TextLeft);
        Assert.True(height >= TableCardLayout.MinHeight * scale);
    }

    private static void CheckCodeCard(float width, float scale)
    {
        var label = 22f * scale;
        var hint = 40f * scale;
        var copyWidth = 90f * scale;
        var layout = RoomCodeLayout.Compute(Vector2.Zero, width, scale, label, hint, copyWidth);

        Assert.True(layout.Copy.Height >= RoomCodeLayout.Touch * scale - 0.01f);
        Assert.True(layout.Copy.Max.X <= layout.Card.Max.X);
        Assert.True(layout.Label.Y + label <= layout.CellsTop);
        Assert.True(layout.Copy.Max.Y <= layout.CellsTop);
        for (var index = 0; index < CasinoRoomCodes.Length; index++)
        {
            var cell = layout.Cell(index);
            Assert.True(cell.Max.X <= layout.Card.Max.X - RoomCodeLayout.Pad * scale + 0.01f);
            Assert.True(cell.Max.Y <= layout.Hint.Y);
            Assert.True(cell.Width >= 24f * scale);
            if (index > 0)
            {
                Assert.True(cell.Min.X >= layout.Cell(index - 1).Max.X);
            }
        }

        Assert.True(layout.Hint.Y + hint <= layout.Card.Max.Y);
    }
}
