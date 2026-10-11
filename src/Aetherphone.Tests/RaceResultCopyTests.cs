using System.Numerics;
using Aetherphone.Apps.Casino.Race;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RaceResultCopyTests
{
    private const float WrapSlack = 0.8f;
    private const string WidestAmount = "999.9M";

    private static readonly float[] Scales = { 0.75f, 1f, 1.5f };

    private static readonly Vector2 NarrowestPhone = new(360f, 740f);

    [Fact]
    public void EveryResultHeadlineFitsTwoLinesOfTheNarrowestCard()
    {
        var catalogs = RaceCopy.Catalogs;
        for (var scaleIndex = 0; scaleIndex < Scales.Length; scaleIndex++)
        {
            var scale = Scales[scaleIndex];
            var stage = RaceScreenLayoutTests.Stage(RaceScreenLayoutTests.Phone(NarrowestPhone, scale), 0f, scale);
            var width = RaceResultLayout.ContentWidthFor(stage.Safe, scale);
            var room = width * RaceResultLayout.HeadlineMaxLines * WrapSlack;
            for (var language = 0; language < catalogs.Length; language++)
            {
                var catalog = catalogs[language];
                var satOut = RaceCopy.Text(catalog, L.Race.SatOut);
                var noWin = RaceCopy.Text(catalog, L.Race.NoWin);
                var won = string.Format(RaceCopy.Text(catalog, L.Race.YouWon), WidestAmount);
                Assert.True(RaceCopy.Measure(satOut, TextStyles.Title2, scale, 1f) <= room, satOut);
                Assert.True(RaceCopy.Measure(noWin, TextStyles.Title2, scale, 1f) <= room, noWin);
                Assert.True(RaceCopy.Measure(won, TextStyles.Title1, scale, 1f) <= room, won);
            }
        }
    }

    [Fact]
    public void ASecondHeadlineLineGrowsTheCardInsteadOfOverlappingTheMeta()
    {
        var stage = RaceScreenLayoutTests.Stage(RaceScreenLayoutTests.Phone(NarrowestPhone, 1f), 0f, 1f);
        var single = RaceResultLayout.Compute(stage.Safe, 1, 0, 0, false, 1f);
        var wrapped = RaceResultLayout.Compute(stage.Safe, 2, 0, 0, false, 1f);
        var clamped = RaceResultLayout.Compute(stage.Safe, 9, 0, 0, false, 1f);
        Assert.Equal(single.ContentHeight + RaceResultLayout.HeadlineLineHeight, wrapped.ContentHeight, 2);
        Assert.Equal(wrapped.ContentHeight, clamped.ContentHeight, 2);
        Assert.True(wrapped.Meta.Min.Y >= wrapped.Headline.Max.Y);
    }
}
