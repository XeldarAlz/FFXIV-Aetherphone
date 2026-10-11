using System.Numerics;
using Aetherphone.Apps.Casino.Machines;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MachineExtrasLayoutTests
{
    private const float Tolerance = 0.01f;
    private const string LongestBuyPrice = "500M";

    [Fact]
    public void EveryPillKeepsItsMeasuredWidthAndNoPillsOverlap()
    {
        var row = new Rect(new Vector2(28f, 600f), new Vector2(365f, 636f));
        var widths = new[] { 90f, 110f, 120f };
        Assert.True(MachineExtrasLayout.Fits(row, widths, 1f));
        var rects = new Rect[widths.Length];
        MachineExtrasLayout.Place(row, widths, 0f, 1f, rects);
        for (var index = 0; index < rects.Length; index++)
        {
            Assert.Equal(widths[index], rects[index].Width, 3);
            Assert.Equal(row.Height, rects[index].Height, 3);
            Assert.True(rects[index].Min.X >= row.Min.X - Tolerance && rects[index].Max.X <= row.Max.X + Tolerance);
        }

        BetDeckLayoutTests.AssertDisjoint(rects);
        Assert.Equal(MachineExtrasLayout.Gap, rects[1].Min.X - rects[0].Max.X, 3);
    }

    [Fact]
    public void ARowThatCannotHoldItsPillsPansInsteadOfShrinkingThem()
    {
        var row = new Rect(new Vector2(0f, 0f), new Vector2(200f, 36f));
        var widths = new[] { 90f, 110f, 150f };
        Assert.False(MachineExtrasLayout.Fits(row, widths, 1f));
        var content = MachineExtrasLayout.ContentWidth(widths, 1f);
        var rects = new Rect[widths.Length];
        MachineExtrasLayout.Place(row, widths, content - row.Width, 1f, rects);
        Assert.Equal(row.Max.X, rects[^1].Max.X, 3);
        for (var index = 0; index < rects.Length; index++)
        {
            Assert.Equal(widths[index], rects[index].Width, 3);
        }

        BetDeckLayoutTests.AssertDisjoint(rects);
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void APillIsAlwaysATouchTargetAndHoldsItsWholeLabel(float scale)
    {
        Assert.Equal(MachineExtrasLayout.MinimumWidth * scale, MachineExtrasLayout.PillWidth(0f, 0f, scale), 3);
        var label = 140f * scale;
        var icon = 17f * scale;
        var width = MachineExtrasLayout.PillWidth(label, icon, scale);
        Assert.True(width >= label + icon + (MachineExtrasLayout.IconGap + MachineExtrasLayout.PadX * 2f) * scale);
    }

    [Fact]
    public void EveryMachinePillFitsTheDefaultDeckInAllNineLanguages()
    {
        var font = InterAdvance.SemiBold;
        var style = MachineExtrasLayout.LabelStyle;
        Assert.InRange(font.Measure("Turbo", style.Scale), 40f, 46f);
        Assert.Equal(InterAdvance.DalamudFontPixels * style.Scale * 2f, font.Measure("加速", style.Scale), 3);
        var icon = InterAdvance.LineHeight(style.Scale) * MachineExtrasLayout.IconShare;
        var row = new Rect(Vector2.Zero, new Vector2(MachineLocales.DefaultDeckRowWidth, 36f));
        Span<MachineExtra> kinds = stackalloc MachineExtra[MachineExtrasLayout.MaxPills];
        Span<float> widths = stackalloc float[MachineExtrasLayout.MaxPills];
        foreach (var (code, catalog) in MachineLocales.All)
        {
            for (var state = 0; state < 4; state++)
            {
                var count = MachineExtrasLayout.Pick(kinds, state == 0, state == 1, state == 2);
                for (var index = 0; index < count; index++)
                {
                    var label = LabelOf(catalog, kinds[index]);
                    widths[index] = MachineExtrasLayout.PillWidth(font.Measure(label, style.Scale),
                        MachineExtrasLayout.HasIcon(kinds[index]) ? icon : 0f, 1f);
                }

                var used = MachineExtrasLayout.ContentWidth(widths[..count], 1f);
                Assert.True(used <= row.Width, code + " state " + state + " needs " + used + " of " + row.Width);
            }
        }
    }

    [Fact]
    public void TheDeckOffersTheMachinePillsAgainOnceEveryRoundEnds()
    {
        Span<MachineExtra> kinds = stackalloc MachineExtra[MachineExtrasLayout.MaxPills];
        var playback = new MachineRoundPlayback();
        var skippedSomewhere = false;
        foreach (var (name, round) in SlotsVectorFiles.Rounds())
        {
            var spin = SlotsVectorFiles.Spin(name, round);
            Assert.True(playback.Begin(spin));
            var guard = 0;
            while (!playback.Finished && guard < 200_000)
            {
                skippedSomewhere |= MachineExtrasLayout.Skippable(playback);
                playback.Update(1f / 30f);
                guard++;
            }

            Assert.True(playback.Finished, spin.RoundId);
            Assert.False(MachineExtrasLayout.Skippable(playback), spin.RoundId);
            var cascade = spin.MachineId == SlotsRules.CascadeId;
            var count = MachineExtrasLayout.Pick(kinds, cascade, false, false);
            Assert.Equal(MachineExtra.Turbo, kinds[0]);
            Assert.Equal(cascade ? 3 : 1, count);
        }

        Assert.True(skippedSomewhere);
        Assert.Equal(2, MachineExtrasLayout.Pick(kinds, true, true, false));
        Assert.Equal(MachineExtra.Skip, kinds[1]);
        Assert.Equal(2, MachineExtrasLayout.Pick(kinds, false, false, true));
        Assert.Equal(MachineExtra.Gamble, kinds[1]);
    }

    private static string LabelOf(StringCatalog catalog, MachineExtra kind) => kind switch
    {
        MachineExtra.Ante => MachineLocales.Text(catalog, L.Machines.AnteBoost),
        MachineExtra.Buy => MachineLocales.Text(catalog, L.Machines.BuyBonus, LongestBuyPrice),
        MachineExtra.Skip => MachineLocales.Text(catalog, L.Machines.Skip),
        MachineExtra.Gamble => MachineLocales.Text(catalog, L.Machines.Gamble),
        _ => MachineLocales.Text(catalog, L.Machines.Turbo),
    };
}
