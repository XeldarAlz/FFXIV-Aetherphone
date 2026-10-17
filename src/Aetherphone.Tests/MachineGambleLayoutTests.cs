using System.Numerics;
using Aetherphone.Apps.Casino.Machines;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MachineGambleLayoutTests
{
    public static readonly TheoryData<bool, float> Phones = new()
    {
        { false, 0.75f },
        { false, 1f },
        { false, 1.5f },
        { true, 0.75f },
        { true, 1f },
        { true, 1.5f },
    };

    [Theory]
    [MemberData(nameof(Phones))]
    public void RedAndBlackShareOneRowAboveCollect(bool artCase, float scale)
    {
        var panel = PanelFor(KindOf(artCase), scale);
        var inner = panel.Inset(MachineGambleLayout.Inset * scale);
        var layout = LayoutFor(panel, scale);
        BetDeckLayoutTests.AssertDisjoint(new[] { layout.Red, layout.Black, layout.Collect });
        Assert.Equal(layout.Red.Width, layout.Black.Width, 2);
        Assert.Equal(Button.LargeHeight * scale, layout.Red.Height, 2);
        Assert.Equal(Button.LargeHeight * scale, layout.Collect.Height, 2);
        Assert.Equal(inner.Width, layout.Collect.Width, 2);
        Assert.True(layout.Red.Max.Y <= layout.Collect.Min.Y);
        Assert.Equal(inner.Max.Y, layout.Collect.Max.Y, 2);
    }

    [Theory]
    [MemberData(nameof(Phones))]
    public void TheCardFitsBetweenTheLadderAndTheButtonsOnEveryPhone(bool artCase, float scale)
    {
        var panel = PanelFor(KindOf(artCase), scale);
        var layout = LayoutFor(panel, scale);
        Assert.True(layout.HasCard, KindOf(artCase) + " at " + scale + " leaves " + layout.Card.Height + " for the card");
        BetDeckLayoutTests.AssertDisjoint(new[]
        {
            layout.Heading, layout.Amount, layout.Ladder, layout.Card, layout.Red, layout.Black, layout.Collect,
        });
        Assert.True(layout.Heading.Min.Y >= panel.Min.Y);
        Assert.True(layout.Collect.Max.Y <= panel.Max.Y);
    }

    [Fact]
    public void EveryGambleButtonLabelFitsWholeInAllNineLanguages()
    {
        var font = InterAdvance.SemiBold;
        var scale = TextStyles.Headline.Scale;
        foreach (var kind in new[] { PhoneCaseKind.Color, PhoneCaseKind.Art })
        {
            var layout = LayoutFor(PanelFor(kind, 1f), 1f);
            foreach (var (code, catalog) in MachineLocales.All)
            {
                AssertRoom(code, font.Measure(MachineLocales.Text(catalog, L.Machines.Red), scale), layout.Red);
                AssertRoom(code, font.Measure(MachineLocales.Text(catalog, L.Machines.Black), scale), layout.Black);
                AssertRoom(code, font.Measure(MachineLocales.Text(catalog, L.Machines.Collect), scale), layout.Collect);
            }
        }
    }

    private static void AssertRoom(string code, float width, Rect button)
    {
        var room = button.Width - button.Height;
        Assert.True(width <= room, code + " needs " + width + " of " + room);
    }

    private static PhoneCaseKind KindOf(bool artCase) => artCase ? PhoneCaseKind.Art : PhoneCaseKind.Color;

    private static MachineGambleLayout LayoutFor(Rect panel, float scale) =>
        MachineGambleLayout.Compute(panel, InterAdvance.LineHeight(TextStyles.Title3.Scale) * scale,
            InterAdvance.LineHeight(TextStyles.Title1.Scale) * scale,
            InterAdvance.LineHeight(TextStyles.Footnote.Scale) * scale, Button.LargeHeight * scale, scale);

    private static Rect PanelFor(PhoneCaseKind kind, float scale)
    {
        var width = BetDeckLayoutTests.ScreenWidth(kind);
        var height = PhoneSizeCatalog.DesignHeight - (PhoneSizeCatalog.DesignWidth - width);
        var screen = new Rect(Vector2.Zero, new Vector2(width * scale, height * scale));
        var content = new Rect(new Vector2(16f * scale, 48f * scale),
            new Vector2((width - 16f) * scale, (height - 30f) * scale));
        var stage = CasinoStageLayout.Compute(screen, content, false, false, BetDeckLayout.DeckHeightFor(true, false),
            scale);
        var machine = MachineStageLayout.Compute(stage.Full, stage.Safe, scale);
        return MachineGambleLayout.PanelOf(machine.Glass, machine.Window);
    }
}
