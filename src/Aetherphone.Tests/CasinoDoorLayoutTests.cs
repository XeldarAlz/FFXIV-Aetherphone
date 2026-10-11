using System.Numerics;
using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoDoorLayoutTests
{
    private const float PhoneWidth = 330f;

    public static TheoryData<float> Scales => new() { 0.75f, 0.9f, 1f, 1.25f, 1.5f };

    [Theory]
    [MemberData(nameof(Scales))]
    public void ControlButtonsAreLargeAndNeverOverlap(float scale)
    {
        var width = PhoneWidth * scale;
        var labelBlock = 36f * scale;
        for (var count = 2; count <= 5; count++)
        {
            for (var index = 0; index < count; index++)
            {
                var cell = DoorLayout.ControlCell(0f, 0f, width, index, count, scale, labelBlock);
                Assert.True(cell.Width >= DoorLayout.ControlButton * scale);
                Assert.True(cell.Max.X <= width + 0.01f);
                Assert.True(cell.Max.Y <= DoorLayout.ControlGridHeight(width, count, scale, labelBlock) + 0.01f);
                for (var other = 0; other < index; other++)
                {
                    var earlier = DoorLayout.ControlCell(0f, 0f, width, other, count, scale, labelBlock);
                    Assert.True(cell.Min.X >= earlier.Max.X - 0.01f || cell.Min.Y >= earlier.Max.Y - 0.01f);
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void KnockCardsKeepTwoTouchButtonsUnderTheName(float scale)
    {
        var name = 22f * scale;
        var line = 20f * scale;
        var height = DoorLayout.KnockHeight(scale, name, line);
        var card = new Rect(Vector2.Zero, new Vector2(PhoneWidth * scale, height));
        var approve = DoorLayout.KnockButton(card, 0, scale);
        var deny = DoorLayout.KnockButton(card, 1, scale);
        var avatar = DoorLayout.KnockAvatar(card, scale);

        Assert.True(approve.Height >= DoorLayout.Touch * scale - 0.01f);
        Assert.True(approve.Max.X < deny.Min.X);
        Assert.True(deny.Max.X <= card.Max.X);
        Assert.True(avatar.Y + DoorLayout.Avatar * 0.5f * scale <= approve.Min.Y);
        Assert.True(avatar.Y + (name + line) * 0.5f <= approve.Min.Y);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void PlayerRowsKeepTheRemoveButtonClearOfTheName(float scale)
    {
        var height = DoorLayout.PlayerHeight(scale, 22f * scale, 20f * scale);
        var row = new Rect(Vector2.Zero, new Vector2(PhoneWidth * scale, height));
        var button = DoorLayout.TrailingButton(row, 90f * scale, scale);
        var avatar = DoorLayout.AvatarCenter(row, scale);

        Assert.True(button.Height >= DoorLayout.Touch * scale - 0.01f);
        Assert.True(button.Min.Y >= row.Min.Y && button.Max.Y <= row.Max.Y);
        Assert.True(avatar.X + DoorLayout.Avatar * 0.5f * scale < DoorLayout.TextLeft(row, scale));
        Assert.True(DoorLayout.TextLeft(row, scale) < button.Min.X - DoorLayout.TextGap * scale);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void LedgerColumnsReadLeftToRightWithoutOverlap(float scale)
    {
        var width = PhoneWidth * scale;
        var number = DoorLayout.LedgerNumberWidth(width, scale);
        var nameRight = DoorLayout.LedgerNameRight(0f, width, scale);
        Assert.True(number > 40f * scale);
        Assert.True(nameRight > DoorLayout.Pad * scale);
        var previousRight = nameRight;
        for (var column = 0; column < 3; column++)
        {
            var right = DoorLayout.LedgerColumnRight(0f, width, column, scale);
            Assert.True(right - number >= previousRight - 0.01f);
            previousRight = right;
        }

        Assert.Equal(width - DoorLayout.Pad * scale, previousRight, 2);
    }

    [Fact]
    public void ChipTablesOfferRenameAndCloseOnly()
    {
        var controls = new DoorControl[5];
        Assert.Equal(2, TableDoor.Controls(false, false, controls));
        Assert.Equal(DoorControl.Rename, controls[0]);
        Assert.Equal(DoorControl.Close, controls[1]);
    }

    [Fact]
    public void HostDealtTablesOfferEveryControl()
    {
        var controls = new DoorControl[5];
        Assert.Equal(5, TableDoor.Controls(true, true, controls));
        Assert.Equal(DoorControl.Pause, controls[0]);
        Assert.Equal(DoorControl.Deal, controls[1]);
        Assert.Equal(DoorControl.CoDealers, controls[2]);
        Assert.Equal(4, TableDoor.Controls(true, false, controls));
    }

    [Fact]
    public void TheDoorPrefersItsOwnCodeOverTheCard()
    {
        var door = new CasinoTableDoorDto(RoomId: "t1", JoinCode: "ABCDEF");
        var card = new CasinoTableRowDto(TableId: "t1", JoinCode: "GHJKMN");
        Assert.Equal("ABCDEF", TableDoor.JoinCode(door, card));
        Assert.Equal("GHJKMN", TableDoor.JoinCode(new CasinoTableDoorDto(RoomId: "t1"), card));
        Assert.Equal(string.Empty, TableDoor.JoinCode(null, null));
    }

    [Fact]
    public void TheHostedAnswerShowsTheCodeBeforeTheDoorOrListingLoads()
    {
        var hosted = new CasinoTableRowDto(TableId: "t1", JoinCode: "ABCDEF");
        var nothingListed = Array.Empty<CasinoTableRowDto>();
        var card = CasinoTablesStore.CardIn(nothingListed, hosted, "t1");
        Assert.Same(hosted, card);
        Assert.Equal("ABCDEF", TableDoor.JoinCode(null, card));
        Assert.Null(CasinoTablesStore.CardIn(nothingListed, hosted, "t2"));
        Assert.Null(CasinoTablesStore.CardIn(nothingListed, hosted, string.Empty));
        var listed = new[] { new CasinoTableRowDto(TableId: "t1", SeatedCount: 1, JoinCode: "ABCDEF") };
        Assert.Same(listed[0], CasinoTablesStore.CardIn(listed, hosted, "t1"));
    }
}
