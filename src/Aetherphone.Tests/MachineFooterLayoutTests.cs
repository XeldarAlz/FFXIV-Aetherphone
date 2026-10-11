using System.Numerics;
using Aetherphone.Apps.Casino.Machines;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MachineFooterLayoutTests
{
    private const float Tolerance = 0.01f;

    [Theory]
    [InlineData(0.75f, 852f)]
    [InlineData(1f, 852f)]
    [InlineData(1.5f, 852f)]
    [InlineData(1f, 640f)]
    [InlineData(1.5f, 560f)]
    public void BetAndWinSitSideBySideInsideTheStrip(float scale, float designHeight)
    {
        var strip = StripFor(scale, designHeight);
        var footer = MachineFooterLayout.Compute(strip, scale);
        BetDeckLayoutTests.AssertDisjoint(new[] { footer.Left, footer.Right });
        Assert.True(footer.Left.Width > 0f && footer.Right.Width > footer.Left.Width);
        Assert.Equal(footer.Left.Min.Y, footer.Right.Min.Y, 3);
        Assert.Equal(footer.Left.Max.Y, footer.Right.Max.Y, 3);
        Assert.True(footer.Left.Min.X >= strip.Min.X - Tolerance && footer.Right.Max.X <= strip.Max.X + Tolerance);
        Assert.True(footer.Left.Min.Y >= strip.Min.Y - Tolerance && footer.Left.Max.Y <= strip.Max.Y + Tolerance);
    }

    [Fact]
    public void TheEyebrowAndAWholeTitleValueFitTheDefaultStrip()
    {
        var footer = MachineFooterLayout.Compute(StripFor(1f, 852f), 1f);
        var needed = InterAdvance.LineHeight(TextStyles.FootnoteEmphasized.Scale)
            + InterAdvance.LineHeight(TextStyles.Title2.Scale);
        Assert.True(needed <= footer.Left.Height, needed + " of " + footer.Left.Height);
    }

    [Fact]
    public void EveryEyebrowFitsItsColumnInAllNineLanguages()
    {
        var footer = MachineFooterLayout.Compute(StripFor(1f, 852f), 1f);
        var font = InterAdvance.SemiBold;
        var scale = TextStyles.FootnoteEmphasized.Scale;
        foreach (var (code, catalog) in MachineLocales.All)
        {
            AssertFits(code, font.Measure(MachineLocales.Text(catalog, L.Machines.BetLabel), scale), footer.Left);
            AssertFits(code, font.Measure(MachineLocales.Text(catalog, L.Machines.FreeSpinsLabel), scale), footer.Left);
            AssertFits(code, font.Measure(MachineLocales.Text(catalog, L.Machines.FreeGamesLabel), scale), footer.Left);
            AssertFits(code, font.Measure(MachineLocales.Text(catalog, L.Machines.RespinsLabel), scale), footer.Left);
            AssertFits(code, font.Measure(MachineLocales.Text(catalog, L.Machines.Win), scale), footer.Right);
        }
    }

    private static void AssertFits(string code, float width, Rect column)
    {
        Assert.True(width <= column.Width, code + " needs " + width + " of " + column.Width);
    }

    private static Rect StripFor(float scale, float designHeight)
    {
        var screen = new Rect(Vector2.Zero, new Vector2(393f * scale, designHeight * scale));
        var content = new Rect(new Vector2(16f * scale, 48f * scale),
            new Vector2(377f * scale, (designHeight - 30f) * scale));
        var stage = CasinoStageLayout.Compute(screen, content, false, false, BetDeckLayout.DeckHeightFor(true, false),
            scale);
        return MachineStageLayout.Compute(stage.Full, stage.Safe, scale).Strip;
    }
}
