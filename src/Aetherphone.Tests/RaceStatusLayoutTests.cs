using System.Numerics;
using Aetherphone.Apps.Casino.Race;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RaceStatusLayoutTests
{
    private const float Tolerance = 0.01f;
    private const string WidestClock = "1:00";

    private static readonly float[] Scales = { 0.75f, 1f, 1.25f, 1.5f };

    private static readonly Vector2[] Phones =
    {
        new(390f, 844f), new(360f, 740f), new(430f, 932f), new(844f, 390f), new(740f, 360f),
    };

    private static readonly LocString[] Labels = { L.Race.StatusOpen, L.Race.StatusOpens };

    private static CasinoStageLayout Stage(Vector2 phone, float scale) =>
        RaceScreenLayoutTests.Stage(RaceScreenLayoutTests.Phone(phone, scale), RaceCabinet.DeckHeight, scale);

    private static float Clock(float scale, float zoom) =>
        RaceCopy.Measure(WidestClock, TextStyles.Headline, scale, zoom);

    private static Rect Capsule(in CasinoStageLayout stage, float width, float scale)
    {
        var half = new Vector2(width * 0.5f, CasinoStageLayout.CapsuleHeight * scale * 0.5f);
        return new Rect(stage.CapsuleCenter - half, stage.CapsuleCenter + half);
    }

    private static Rect Touch(Vector2 center, float radius) =>
        new(center - new Vector2(radius, radius), center + new Vector2(radius, radius));

    [Fact]
    public void TheStatusSitsUnderTheChromeBandAndAboveTheField()
    {
        for (var scaleIndex = 0; scaleIndex < Scales.Length; scaleIndex++)
        {
            var scale = Scales[scaleIndex];
            var clock = Clock(scale, RaceCopy.MaxTextZoom);
            for (var phone = 0; phone < Phones.Length; phone++)
            {
                var stage = Stage(Phones[phone], scale);
                var layout = RaceStatusLayout.Compute(stage, clock, scale);
                Assert.True(layout.Visible);
                Assert.True(layout.Area.Height <= RaceStatusLayout.MaxHeight * scale + Tolerance);
                Assert.True(layout.Area.Min.Y >= stage.Band.Max.Y - Tolerance);
                Assert.True(layout.Area.Max.Y <= stage.Safe.Min.Y + Tolerance);
                Assert.True(RaceScreenLayoutTests.Inside(layout.Capsule, layout.Area));
                Assert.True(RaceScreenLayoutTests.Inside(layout.Label, layout.Capsule));
                Assert.True(RaceScreenLayoutTests.Inside(layout.Clock, layout.Capsule));
                Assert.True(layout.Clock.Width >= clock - Tolerance);
                Assert.True(layout.Label.Max.X <= layout.Clock.Min.X + Tolerance);
                Assert.True(layout.Dot.X < layout.Label.Min.X);
            }
        }
    }

    [Fact]
    public void NoCapsuleWidthEverReachesTheStatus()
    {
        for (var scaleIndex = 0; scaleIndex < Scales.Length; scaleIndex++)
        {
            var scale = Scales[scaleIndex];
            for (var phone = 0; phone < Phones.Length; phone++)
            {
                var stage = Stage(Phones[phone], scale);
                var layout = RaceStatusLayout.Compute(stage, Clock(scale, 1f), scale);
                var back = Touch(stage.BackCenter, stage.TouchRadiusPixels);
                var info = Touch(stage.InfoCenter, stage.TouchRadiusPixels);
                for (var step = 0; step <= 10; step++)
                {
                    var capsule = Capsule(stage, stage.CapsuleMaxWidth * step / 10f, scale);
                    RaceScreenLayoutTests.AssertAllApart(layout.Area, capsule, back, info);
                }
            }
        }
    }

    [Fact]
    public void EveryLanguageFitsTheStatusLabelWhole()
    {
        var catalogs = RaceCopy.Catalogs;
        for (var scaleIndex = 0; scaleIndex < Scales.Length; scaleIndex++)
        {
            var scale = Scales[scaleIndex];
            for (var phone = 0; phone < Phones.Length; phone++)
            {
                var stage = Stage(Phones[phone], scale);
                var zoomed = RaceStatusLayout.Compute(stage, Clock(scale, RaceCopy.MaxTextZoom), scale);
                var regular = RaceStatusLayout.Compute(stage, Clock(scale, 1f), scale);
                for (var language = 0; language < catalogs.Length; language++)
                {
                    for (var label = 0; label < Labels.Length; label++)
                    {
                        var text = RaceCopy.Text(catalogs[language], Labels[label]);
                        Assert.True(
                            RaceCopy.Measure(text, TextStyles.SubheadlineEmphasized, scale, 1f) <= regular.Label.Width,
                            $"'{text}' at full size on {Phones[phone]} x{scale}");
                        Assert.True(
                            RaceCopy.Measure(text, TextStyles.FootnoteEmphasized, scale, RaceCopy.MaxTextZoom)
                            <= zoomed.Label.Width,
                            $"'{text}' at the largest text size on {Phones[phone]} x{scale}");
                    }
                }
            }
        }
    }

    [Fact]
    public void AnUnreservedRibbonDrawsNoStatus()
    {
        var stage = CasinoStageLayout.Compute(RaceScreenLayoutTests.Phone(Phones[0], 1f), false, false,
            RaceCabinet.DeckHeight, 1f);
        Assert.False(RaceStatusLayout.Compute(stage, 40f, 1f).Visible);
    }

    [Fact]
    public void TheClockRoundsUpToWholeSeconds()
    {
        Assert.Equal(0, RaceStatusLine.SecondsLeft(-500));
        Assert.Equal(0, RaceStatusLine.SecondsLeft(0));
        Assert.Equal(1, RaceStatusLine.SecondsLeft(1));
        Assert.Equal(1, RaceStatusLine.SecondsLeft(1000));
        Assert.Equal(60, RaceStatusLine.SecondsLeft(59_001));
    }
}
