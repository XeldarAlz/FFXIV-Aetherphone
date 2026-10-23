using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Prism;

internal static class PrismTracks
{
    public const string Vanilla = "vanilla";
    public const string Modded = "modded";

    public static PrismTrackDto[] Of(PrismWeekDto? week) => week?.Tracks ?? Array.Empty<PrismTrackDto>();

    public static string Name(string id) => id switch
    {
        Vanilla => Loc.T(L.Prism.TrackVanilla),
        Modded => Loc.T(L.Prism.TrackModded),
        _ => id,
    };

    public static string Choose(PrismWeekDto week, string current)
    {
        var tracks = Of(week);
        if (tracks.Length == 0)
        {
            return string.Empty;
        }

        if (Find(tracks, current) is not null)
        {
            return current;
        }

        for (var index = 0; index < tracks.Length; index++)
        {
            if (tracks[index].MyEntry is not null)
            {
                return tracks[index].Id;
            }
        }

        return Find(tracks, Modded) is not null ? Modded : tracks[0].Id;
    }

    public static PrismWeekDto Project(PrismWeekDto week, string track)
    {
        if (Find(Of(week), track) is not { } chosen)
        {
            return week;
        }

        return week with
        {
            EntryCount = chosen.EntryCount,
            MyEntry = chosen.MyEntry,
            BallotsCast = chosen.BallotsCast,
            FinalistCount = chosen.FinalistCount,
            ResultsRevision = chosen.ResultsRevision,
            Prizes = chosen.Prizes ?? week.Prizes,
        };
    }

    public static PrismWeekDto WithEntry(PrismWeekDto week, string track, PrismEntryDto? entry)
    {
        var tracks = Of(week);
        if (Find(tracks, track) is null)
        {
            return week with { MyEntry = entry };
        }

        var replaced = new PrismTrackDto[tracks.Length];
        for (var index = 0; index < tracks.Length; index++)
        {
            replaced[index] = tracks[index].Id == track ? tracks[index] with { MyEntry = entry } : tracks[index];
        }

        return week with { Tracks = replaced };
    }

    private static PrismTrackDto? Find(PrismTrackDto[] tracks, string id)
    {
        for (var index = 0; index < tracks.Length; index++)
        {
            if (tracks[index].Id == id)
            {
                return tracks[index];
            }
        }

        return null;
    }
}
