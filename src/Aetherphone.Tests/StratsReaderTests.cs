using Aetherphone.Apps.Strats;
using Aetherphone.Core.Strats;
using Xunit;

namespace Aetherphone.Tests;

public sealed class StratsReaderTests
{
    private static ManifestFight Fight(string key, string title, string abbrev, string subtitle,
        params uint[] territories) =>
        new() { Key = key, Title = title, Abbrev = abbrev, Subtitle = subtitle, TerritoryIds = territories };

    private static StratsManifest Manifest(params ManifestFight[] fights) =>
        new() { Groups = new[] { new ManifestGroup { Id = "savage", Title = "Savage", Fights = fights } } };

    [Fact]
    public void TerritoryLookupFindsTheFightThatOwnsTheZone()
    {
        var manifest = Manifest(Fight("m9s", "Vamp Fatale", "M9S", "Savage", 1300, 1301),
            Fight("fru", "Futures Rewritten", "FRU", "Ultimate", 1238));
        Assert.True(StratsLibrary.TryFindByTerritory(manifest, 1301, out var found));
        Assert.Equal("m9s", found.Key);
        Assert.True(StratsLibrary.TryFindByTerritory(manifest, 1238, out found));
        Assert.Equal("fru", found.Key);
    }

    [Fact]
    public void TerritoryLookupIgnoresUnknownAndEmptyZones()
    {
        var manifest = Manifest(Fight("m9s", "Vamp Fatale", "M9S", "Savage", 1300));
        Assert.False(StratsLibrary.TryFindByTerritory(manifest, 0, out _));
        Assert.False(StratsLibrary.TryFindByTerritory(manifest, 129, out _));
        Assert.False(StratsLibrary.TryFindByTerritory(null, 1300, out _));
    }

    [Fact]
    public void SearchMatchesEveryWordAcrossTitleShortNameAndSubtitle()
    {
        var fight = Fight("m9s", "AAC Heavyweight M1", "M9S", "Savage");
        Assert.True(StratsLibrary.Matches(fight, string.Empty));
        Assert.True(StratsLibrary.Matches(fight, "  "));
        Assert.True(StratsLibrary.Matches(fight, "m9s"));
        Assert.True(StratsLibrary.Matches(fight, "heavyweight  savage"));
        Assert.False(StratsLibrary.Matches(fight, "heavyweight ultimate"));
    }

    [Fact]
    public void MostRecentPicksTheLatestOpenedFightInTheManifest()
    {
        var manifest = Manifest(Fight("a", "A", "A", string.Empty), Fight("b", "B", "B", string.Empty));
        var snapshot = new StratsSnapshot();
        Assert.False(StratsLibrary.TryMostRecent(manifest, snapshot, out _, out _));
        snapshot.Fights["a"] = new StratsFightSelection { OpenedUnix = 100 };
        snapshot.Fights["b"] = new StratsFightSelection { OpenedUnix = 200 };
        snapshot.Fights["gone"] = new StratsFightSelection { OpenedUnix = 900 };
        Assert.True(StratsLibrary.TryMostRecent(manifest, snapshot, out var fight, out var selection));
        Assert.Equal("b", fight.Key);
        Assert.Equal(200, selection.OpenedUnix);
    }

    [Fact]
    public void ContentsListPhasesFollowedByTheirMechanics()
    {
        var fight = new ResolvedFight
        {
            Phases = new[]
            {
                new ResolvedPhase
                {
                    Name = "Phase one",
                    Mechs = new[]
                    {
                        new ResolvedMechanic { Name = "Raidwide" },
                        new ResolvedMechanic
                        {
                            Name = "Spread",
                            PlayerText = new GuideText { Paras = new[] { new GuidePara() } },
                        },
                    },
                },
                new ResolvedPhase { Name = "Phase two" },
            },
        };

        var entries = StratsContents.Build(fight);
        Assert.Equal(4, entries.Length);
        Assert.True(entries[0].IsPhase);
        Assert.Equal("Raidwide", entries[1].Label);
        Assert.False(entries[1].ForYou);
        Assert.True(entries[2].ForYou);
        Assert.Equal(1, entries[2].MechIndex);
        Assert.True(entries[3].IsPhase);
        Assert.Equal(1, entries[3].PhaseIndex);
        Assert.Equal(1, StratsContents.CountForYou(entries));
    }

    [Fact]
    public void ReadingIndexIsTheLastEntryAboveTheLine()
    {
        var tops = new[] { 100f, 300f, 600f, 900f };
        Assert.Equal(-1, StratsContents.IndexAt(ReadOnlySpan<float>.Empty, 50f));
        Assert.Equal(0, StratsContents.IndexAt(tops, 50f));
        Assert.Equal(0, StratsContents.IndexAt(tops, 299f));
        Assert.Equal(1, StratsContents.IndexAt(tops, 300f));
        Assert.Equal(2, StratsContents.IndexAt(tops, 899f));
        Assert.Equal(3, StratsContents.IndexAt(tops, 5000f));
    }

    [Fact]
    public void ProgressIsClampedAndSafeWithoutScrollRange()
    {
        Assert.Equal(0f, StratsContents.Progress(40f, 0f));
        Assert.Equal(0.5f, StratsContents.Progress(50f, 100f));
        Assert.Equal(1f, StratsContents.Progress(150f, 100f));
    }

    [Fact]
    public void SelectionRoundTripsTheReadingPlace()
    {
        var selection = new StratsSelection();
        selection.Load("m9s", null, 3, 1234);
        Assert.True(selection.Fresh);
        Assert.Equal(-1, selection.ReadingEntry);
        Assert.True(selection.MarkReading(4, "Spread", 0.4f));
        Assert.False(selection.MarkReading(4, "Spread", 0.403f));
        var saved = selection.Capture();
        Assert.Equal(1234, saved.OpenedUnix);

        var reopened = new StratsSelection();
        reopened.Load("m9s", saved, 0, 5678);
        Assert.False(reopened.Fresh);
        Assert.Equal(3, reopened.Slot);
        Assert.Equal(4, reopened.ReadingEntry);
        Assert.Equal("Spread", reopened.ReadingLabel);
        Assert.Equal(5678, reopened.OpenedUnix);
    }

    [Fact]
    public void PillFlowWrapsInsteadOfDroppingPills()
    {
        var flow = new PillFlow(10f, 100f, 8f);
        Assert.Equal(0f, flow.Height(30f, 8f));
        Assert.Equal(10f, flow.Place(60f));
        Assert.Equal(0, flow.Row);
        Assert.Equal(10f, flow.Place(60f));
        Assert.Equal(1, flow.Row);
        Assert.Equal(78f, flow.Place(20f));
        Assert.Equal(1, flow.Row);
        Assert.Equal(10f, flow.Place(140f));
        Assert.Equal(3, flow.Rows);
        Assert.Equal(30f * 3f + 8f * 2f, flow.Height(30f, 8f));
    }
}
