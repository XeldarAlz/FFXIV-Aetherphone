using System.Numerics;
using Aetherphone.Apps.Casino.Plinko;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PlinkoStageLayoutTests
{
    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void TheSignBoardAndPlateNeverCollide(float scale)
    {
        var screen = new Rect(Vector2.Zero, new Vector2(393f * scale, 852f * scale));
        var content = new Rect(new Vector2(16f * scale, 48f * scale), new Vector2(377f * scale, 822f * scale));
        var stage = CasinoStageLayout.Compute(screen, content, false, false, BetDeckLayout.DeckHeightFor(true, false),
            scale);
        var layout = PlinkoStageLayout.Compute(stage.Safe, scale);
        BetDeckLayoutTests.AssertDisjoint(new[] { layout.Sign, layout.Board, layout.Plate });
        BetDeckLayoutTests.AssertDisjoint(new[] { layout.Rail, layout.Stats });
        Assert.True(layout.Board.Height > 0f);
        Assert.True(layout.Rail.Min.Y >= layout.Plate.Min.Y && layout.Stats.Max.Y <= layout.Plate.Max.Y + 0.01f);
        Assert.True(layout.Rail.Min.Y > layout.Sign.Max.Y);
        Assert.True(layout.Plate.Max.Y <= stage.Deck.Min.Y);
    }
}
