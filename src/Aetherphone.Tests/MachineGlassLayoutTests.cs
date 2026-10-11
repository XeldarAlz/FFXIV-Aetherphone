using System.Numerics;
using Aetherphone.Apps.Casino.Machines;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MachineGlassLayoutTests
{
    private const float Tolerance = 0.01f;

    public static TheoryData<float, float, float> Screens() => new()
    {
        { 0.75f, 393f, 852f },
        { 1f, 393f, 852f },
        { 1.5f, 393f, 852f },
        { 1f, 280f, 640f },
        { 1f, 240f, 560f },
        { 1.5f, 320f, 560f },
    };

    [Theory]
    [MemberData(nameof(Screens))]
    public void TheSignTheJackpotAndTheRowNeverOverlap(float scale, float designWidth, float designHeight)
    {
        var glass = GlassFor(scale, designWidth, designHeight);
        var jackpot = new Vector2(122f, 40f) * scale;
        var layout = MachineGlassLayout.Compute(glass, jackpot, 17.6f * scale, scale);
        BetDeckLayoutTests.AssertDisjoint(new[] { layout.Hero, layout.Sign, layout.Jackpot, layout.Caption, layout.Content });
        AssertInside(glass, layout.Hero, layout.Sign, layout.Jackpot, layout.Caption, layout.Content);
        AssertInside(layout.Top, layout.Hero, layout.Sign, layout.Jackpot);
        Assert.True(layout.Sign.Width >= 0f);
        Assert.True(layout.Jackpot.Height <= layout.Top.Height + Tolerance);
        Assert.True(layout.Caption.Max.Y <= layout.Content.Min.Y + Tolerance);
        for (var rung = 0; rung < CrystalCascadeRules.TumbleLadder.Length; rung++)
        {
            var pill = MachineGlassLayout.Pill(layout.Content, rung, CrystalCascadeRules.TumbleLadder.Length, scale);
            AssertInside(layout.Content, pill);
            Assert.True(pill.Height <= MachineGlassLayout.PillMaxHeight * scale + Tolerance);
            if (rung > 0)
            {
                var previous = MachineGlassLayout.Pill(layout.Content, rung - 1, CrystalCascadeRules.TumbleLadder.Length,
                    scale);
                Assert.False(BetDeckLayoutTests.Overlaps(previous, pill));
            }
        }
    }

    [Fact]
    public void TheJackpotReadsWholeAsOneUnitInAllNineLanguages()
    {
        var glass = GlassFor(1f, 393f, 852f);
        var font = InterAdvance.SemiBold;
        var eyebrowScale = TextStyles.FootnoteEmphasized.Scale;
        foreach (var (code, catalog) in MachineLocales.All)
        {
            var eyebrow = font.Measure(MachineLocales.Text(catalog, L.Machines.FloorJackpot), eyebrowScale);
            var layout = MachineGlassLayout.Compute(glass, new Vector2(eyebrow, 40f), 0f, 1f);
            var room = layout.Jackpot.Width - MachineGlassLayout.JackpotPadX * 2f;
            Assert.True(eyebrow <= room + Tolerance, code + " needs " + eyebrow + " of " + room);
            Assert.True(layout.Sign.Width > layout.Hero.Width, code);
        }
    }

    [Fact]
    public void TheLadderCaptionFitsInAllNineLanguages()
    {
        var glass = GlassFor(1f, 393f, 852f);
        var layout = MachineGlassLayout.Compute(glass, new Vector2(122f, 40f), 17.6f, 1f);
        Assert.True(MachineGlassLayout.Pill(layout.Content, 0, 4, 1f).Height >= 24f);
        foreach (var (code, catalog) in MachineLocales.All)
        {
            var width = InterAdvance.SemiBold.Measure(MachineLocales.Text(catalog, L.Machines.TumbleLadder),
                TextStyles.FootnoteEmphasized.Scale);
            Assert.True(width <= layout.Caption.Width, code + " needs " + width);
        }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(5, 3)]
    [InlineData(8, 3)]
    public void TheLitRungFollowsTheTumbleMultiplier(int multiplier, int rung)
    {
        Assert.Equal(rung, MachineGlassLayout.ActiveRung(CrystalCascadeRules.TumbleLadder, multiplier));
    }

    private static void AssertInside(Rect outer, params Rect[] rects)
    {
        for (var index = 0; index < rects.Length; index++)
        {
            var rect = rects[index];
            Assert.True(rect.Min.X >= outer.Min.X - Tolerance && rect.Max.X <= outer.Max.X + Tolerance
                && rect.Min.Y >= outer.Min.Y - Tolerance && rect.Max.Y <= outer.Max.Y + Tolerance, index.ToString());
        }
    }

    private static Rect GlassFor(float scale, float designWidth, float designHeight)
    {
        var screen = new Rect(Vector2.Zero, new Vector2(designWidth * scale, designHeight * scale));
        var content = new Rect(new Vector2(16f * scale, 48f * scale),
            new Vector2((designWidth - 16f) * scale, (designHeight - 30f) * scale));
        var stage = CasinoStageLayout.Compute(screen, content, false, false, BetDeckLayout.DeckHeightFor(true, false),
            scale);
        return MachineStageLayout.Compute(stage.Full, stage.Safe, scale).Glass;
    }
}
