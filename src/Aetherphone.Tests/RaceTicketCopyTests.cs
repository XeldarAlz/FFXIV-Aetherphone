using Aetherphone.Apps.Casino.Race;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RaceTicketCopyTests
{
    private const float NarrowestRow = 336f;
    private const float StakeGlyph = 18f;
    private const string WidestAmount = "999.9M";
    private const string WidestRunner = "8";

    private static readonly float[] Scales = { 0.75f, 1f, 1.5f };

    private static readonly LocString[] Kinds =
    {
        L.Race.KindWin, L.Race.KindPlace, L.Race.KindForecast, L.Race.KindReverse,
    };

    [Fact]
    public void TicketLabelsUseTheBetSegmentNames()
    {
        var texts = new RaceTexts();
        var tickets = new[]
        {
            new CasinoRaceTicketDto(Kind: RaceRules.KindWin, Runner: 2, Amount: 100),
            new CasinoRaceTicketDto(Kind: RaceRules.KindReverse, Runner: 2, RunnerB: 4, Amount: 100),
        };
        Assert.Equal(Loc.T(L.Race.TicketSingle, Loc.T(L.Race.KindWin), "3"), texts.Ticket(tickets, 0));
        Assert.Equal(Loc.T(L.Race.TicketPair, Loc.T(L.Race.KindReverse), "3", "5"), texts.Ticket(tickets, 1));
        Assert.Equal(texts.KindLabels[RaceRules.KindReverse], Loc.T(L.Race.KindReverse));
    }

    [Fact]
    public void EveryTicketLabelFitsTheNarrowestTicketRow()
    {
        var catalogs = RaceCopy.Catalogs;
        for (var scaleIndex = 0; scaleIndex < Scales.Length; scaleIndex++)
        {
            var scale = Scales[scaleIndex];
            var row = NarrowestRow * scale;
            for (var language = 0; language < catalogs.Length; language++)
            {
                var catalog = catalogs[language];
                var pays = string.Format(RaceCopy.Text(catalog, L.Race.PaysUpTo), WidestAmount);
                var paysWidth = RaceCopy.Measure(pays, TextStyles.FootnoteEmphasized, scale, 1f);
                var stakeWidth = RaceCopy.Measure(WidestAmount, TextStyles.Footnote, scale, 1f) + StakeGlyph * scale;
                var room = RaceTicketStrip.LabelRoom(row, paysWidth, stakeWidth, scale);
                for (var kind = 0; kind < Kinds.Length; kind++)
                {
                    var name = RaceCopy.Text(catalog, Kinds[kind]);
                    var label = RaceRules.IsPair(kind)
                        ? string.Format(RaceCopy.Text(catalog, L.Race.TicketPair), name, WidestRunner, WidestRunner)
                        : string.Format(RaceCopy.Text(catalog, L.Race.TicketSingle), name, WidestRunner);
                    Assert.True(RaceCopy.Measure(label, TextStyles.Footnote, scale, 1f) <= room,
                        $"'{label}' beside '{pays}' x{scale}");
                }
            }
        }
    }

    [Fact]
    public void ThePayoutColumnNeverTakesMoreThanItsShare()
    {
        var wide = RaceTicketStrip.LabelRoom(NarrowestRow, NarrowestRow, 0f, 1f);
        var narrow = RaceTicketStrip.LabelRoom(NarrowestRow, NarrowestRow * RaceTicketStrip.PaysShare, 0f, 1f);
        Assert.Equal(narrow, wide, 2);
        Assert.True(wide > 0f);
    }
}
