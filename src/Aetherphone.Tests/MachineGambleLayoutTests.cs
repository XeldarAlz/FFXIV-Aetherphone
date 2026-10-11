using System.Numerics;
using Aetherphone.Apps.Casino.Machines;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MachineGambleLayoutTests
{
    private const float GambleInset = 10f;

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void RedAndBlackShareOneRowAboveCollect(float scale)
    {
        var inner = WindowFor(scale).Inset(GambleInset * scale);
        var buttons = MachineGambleLayout.Compute(inner, Button.LargeHeight * scale, scale);
        BetDeckLayoutTests.AssertDisjoint(new[] { buttons.Red, buttons.Black, buttons.Collect });
        Assert.Equal(buttons.Red.Width, buttons.Black.Width, 2);
        Assert.Equal(Button.LargeHeight * scale, buttons.Red.Height, 2);
        Assert.Equal(Button.LargeHeight * scale, buttons.Collect.Height, 2);
        Assert.Equal(inner.Width, buttons.Collect.Width, 2);
        Assert.True(buttons.Red.Max.Y <= buttons.Collect.Min.Y);
        Assert.Equal(inner.Max.Y, buttons.Collect.Max.Y, 2);
    }

    [Fact]
    public void EveryGambleButtonLabelFitsWholeInAllNineLanguages()
    {
        var inner = WindowFor(1f).Inset(GambleInset);
        var buttons = MachineGambleLayout.Compute(inner, Button.LargeHeight, 1f);
        var font = InterAdvance.SemiBold;
        var scale = TextStyles.Headline.Scale;
        foreach (var (code, catalog) in MachineLocales.All)
        {
            AssertRoom(code, font.Measure(MachineLocales.Text(catalog, L.Machines.Red), scale), buttons.Red);
            AssertRoom(code, font.Measure(MachineLocales.Text(catalog, L.Machines.Black), scale), buttons.Black);
            AssertRoom(code, font.Measure(MachineLocales.Text(catalog, L.Machines.Collect), scale), buttons.Collect);
        }
    }

    private static void AssertRoom(string code, float width, Rect button)
    {
        var room = button.Width - button.Height;
        Assert.True(width <= room, code + " needs " + width + " of " + room);
    }

    private static Rect WindowFor(float scale)
    {
        var screen = new Rect(Vector2.Zero, new Vector2(393f * scale, 852f * scale));
        var content = new Rect(new Vector2(16f * scale, 48f * scale), new Vector2(377f * scale, 822f * scale));
        var stage = CasinoStageLayout.Compute(screen, content, false, false, BetDeckLayout.DeckHeightFor(true, false),
            scale);
        return MachineStageLayout.Compute(stage.Full, stage.Safe, scale).Window;
    }
}
