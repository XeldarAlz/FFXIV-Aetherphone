using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.Report;
using Aetherphone.Core.Wallpapers;

namespace Aetherphone.Apps.Prism;

internal static class PrismPhases
{
    public const string Entering = "entering";
    public const string Judging = "judging";
    public const string Final = "final";
    public const string Tallying = "tallying";
    public const string Results = "results";

    public const string StatusRejected = "rejected";
    public const string StatusMoved = "moved";

    public const string StageFinal = "final";
    public const string StageHeat = "heat";

    public static bool Judgeable(string phase) => phase is Judging or Final;
}

internal enum PrismAction : byte
{
    None,
    Enter,
    Withdraw,
    Ballot,
    Submit,
    Outfit,
}

internal sealed class PrismStore : IDisposable
{
    private const int EntryBaseSize = 1080;
    private const PostAspect EntryAspect = PostAspect.Portrait;

    private readonly AethernetSession session;
    private readonly PrismClient client;
    private readonly MediaClient media;
    private readonly SafetyClient safety;
    private readonly StoreWork work = new("Prism");
    private readonly FailureSlot weekFailure = new();
    private readonly FailureSlot actionFailure = new();

    private volatile PrismWeekDto? rawWeek;
    private volatile PrismWeekDto? week;
    private volatile string track = string.Empty;
    private volatile PrismResultsDto? results;
    private volatile PrismBallotDto? ballot;
    private volatile bool loading;
    private volatile bool loadedOnce;
    private volatile bool weekFailed;
    private volatile bool busy;
    private volatile PrismAction failedAction;
    private long lastBallotCoins;

    public PrismStore(AethernetSession session, AethernetApi api)
    {
        this.session = session;
        client = api.Prism;
        media = api.Media;
        safety = api.Safety;
    }

    public static float EntryRatio => PostAspects.Ratio(EntryAspect);

    public static bool EntryRevealsWholeImage => PostAspects.RevealsWholeImage(EntryAspect);

    public bool IsSignedIn => session.IsSignedIn;

    public UserDto? CurrentUser => session.CurrentUser;

    public PrismWeekDto? Week => week;

    public string Track => track;

    public PrismTrackDto[] Tracks => PrismTracks.Of(rawWeek);

    public PrismResultsDto? Results => results;

    public PrismBallotDto? Ballot => ballot;

    public bool Loading => loading;

    public bool LoadedOnce => loadedOnce;

    public bool WeekFailed => weekFailed;

    public string WeekFailureText => weekFailure.Text();

    public bool Busy => busy;

    public string ActionFailureText => actionFailure.Text();

    public long LastBallotCoins => Interlocked.Read(ref lastBallotCoins);

    public bool Failed(PrismAction action) => failedAction == action && action != PrismAction.None;

    public void Refresh()
    {
        if (!session.IsSignedIn || loading)
        {
            return;
        }

        loading = true;
        var lang = Loc.Current.Code;
        work.Run("week", async token =>
        {
            var failure = AepFailure.None;
            var fetched = await client.WeekAsync(lang, token, received => failure = received).ConfigureAwait(false);
            if (fetched is null)
            {
                weekFailure.Set(failure);
                weekFailed = true;
                return;
            }

            weekFailure.Clear();
            weekFailed = false;
            Publish(fetched);
            loadedOnce = true;
            await LoadResultsAsync(week!, lang, token).ConfigureAwait(false);
        }, () => loading = false);
    }

    public void SelectTrack(string id)
    {
        if (rawWeek is not { } raw || id == track || PrismTracks.Choose(raw, id) != id)
        {
            return;
        }

        track = id;
        var current = PrismTracks.Project(raw, id);
        week = current;
        ballot = null;
        results = null;
        var lang = Loc.Current.Code;
        work.Run("results", token => LoadResultsAsync(current, lang, token));
    }

    private void Publish(PrismWeekDto raw)
    {
        rawWeek = raw;
        track = PrismTracks.Choose(raw, track);
        week = PrismTracks.Project(raw, track);
    }

    private async Task LoadResultsAsync(PrismWeekDto current, string lang, CancellationToken token)
    {
        var showing = track;
        var fetched = ResultsOut(current)
            ? await client.ResultsAsync(current.Id, lang, showing, token).ConfigureAwait(false)
            : null;
        if (fetched is null && current.PreviousWeekId.Length > 0)
        {
            fetched = await client.ResultsAsync(current.PreviousWeekId, lang, showing, token).ConfigureAwait(false);
        }

        if (showing != track)
        {
            return;
        }

        if (fetched is not null)
        {
            results = fetched;
            return;
        }

        if (results is { } held && !StillCurrent(held, current))
        {
            results = null;
        }
    }

    internal static bool StillCurrent(PrismResultsDto held, PrismWeekDto current) =>
        (held.WeekId == current.PreviousWeekId || held.WeekId == current.Id) &&
        held.Revision == current.ResultsRevision;

    private static bool ResultsOut(PrismWeekDto week) =>
        week.Id.Length > 0 && (week.Phase == PrismPhases.Results || (week.ResultsAtUnix > 0 &&
            DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= week.ResultsAtUnix));

    public void Enter(string sourcePath, WallpaperCrop crop, GlamGearDto[]? outfit, int race, int gender,
        Action<bool> onComplete)
    {
        if (rawWeek is not { } current || !BeginAction())
        {
            return;
        }

        var entering = track;

        work.Run("enter", async token =>
        {
            var (width, height) = PostAspects.Size(EntryAspect, EntryBaseSize);
            var baked = ImageProcessor.BakeCroppedUpload(sourcePath, crop, width, height, EntryRevealsWholeImage,
                PhotoEdit.None);
            var failure = AepFailure.None;
            var upload = await media.UploadUrlAsync(baked.ContentType, PrismClient.MediaScope, token,
                received => failure = received).ConfigureAwait(false);
            if (upload is null || !await media.UploadImageAsync(upload.UploadUrl, baked.Bytes, baked.ContentType,
                    token, received => failure = received).ConfigureAwait(false))
            {
                return Fail(PrismAction.Enter, failure);
            }

            var request = new PrismEntryRequest(upload.Key, baked.Width, baked.Height,
                entering.Length > 0 ? entering : null, outfit, race, gender);
            var entry = await client.EnterAsync(current.Id, request, token, received => failure = received)
                .ConfigureAwait(false);
            if (entry is null)
            {
                return Fail(PrismAction.Enter, failure);
            }

            Publish(PrismTracks.WithEntry(rawWeek ?? current, entering, entry));
            return true;
        }, onComplete, EndAction);
    }

    public void ShareOutfit(GlamGearDto[]? outfit)
    {
        if (week is not { MyEntry: not null } || rawWeek is not { } current || !BeginAction())
        {
            return;
        }

        var sharing = track;
        work.Run("outfit", async token =>
        {
            var failure = AepFailure.None;
            var entry = await client.ShareOutfitAsync(current.Id, sharing, new PrismOutfitRequest(outfit), token,
                received => failure = received).ConfigureAwait(false);
            if (entry is null)
            {
                return Fail(PrismAction.Outfit, failure);
            }

            Publish(PrismTracks.WithEntry(rawWeek ?? current, sharing, entry));
            return true;
        }, NoCompletion, EndAction);
    }

    public void Withdraw()
    {
        if (rawWeek is not { } current || !BeginAction())
        {
            return;
        }

        var leaving = track;
        work.Run("withdraw", async token =>
        {
            var failure = AepFailure.None;
            if (!await client.WithdrawAsync(current.Id, leaving, token, received => failure = received)
                    .ConfigureAwait(false))
            {
                return Fail(PrismAction.Withdraw, failure);
            }

            Publish(PrismTracks.WithEntry(rawWeek ?? current, leaving, null));
            return true;
        }, NoCompletion, EndAction);
    }

    public void RequestBallot()
    {
        if (week is not { } current || !BeginAction())
        {
            return;
        }

        work.Run("ballot", async token =>
        {
            var failure = AepFailure.None;
            var fetched = await client.BallotAsync(current.Id, track, token, received => failure = received)
                .ConfigureAwait(false);
            if (fetched is null)
            {
                return Fail(PrismAction.Ballot, failure);
            }

            ballot = fetched;
            return true;
        }, NoCompletion, EndAction);
    }

    public void SubmitBallot(PrismStarDto[] stars, Action<bool> onComplete)
    {
        if (ballot is not { } open || !BeginAction())
        {
            return;
        }

        Interlocked.Exchange(ref lastBallotCoins, 0);
        work.Run("submit ballot", async token =>
        {
            var failure = AepFailure.None;
            var updated = await client.SubmitBallotAsync(open.Id, new PrismBallotRequest(stars), token,
                received => failure = received).ConfigureAwait(false);
            if (updated is null)
            {
                return Fail(PrismAction.Submit, failure);
            }

            Interlocked.Exchange(ref lastBallotCoins, Math.Max(0, updated.BallotCoins));
            Publish(updated);
            ballot = null;
            return true;
        }, onComplete, EndAction);
    }

    public void ClearBallot()
    {
        ballot = null;
    }

    public void Report(string lookId, ReportReason reason, Action<bool> done)
    {
        work.Run("report", token => safety.ReportAsync(PrismClient.ReportTarget, lookId, reason, token), done);
    }

    private bool BeginAction()
    {
        if (busy || !session.IsSignedIn)
        {
            return false;
        }

        busy = true;
        failedAction = PrismAction.None;
        actionFailure.Clear();
        return true;
    }

    public void ClearFailure()
    {
        if (busy)
        {
            return;
        }

        failedAction = PrismAction.None;
        actionFailure.Clear();
    }

    private void EndAction()
    {
        busy = false;
    }

    private bool Fail(PrismAction action, AepFailure failure)
    {
        actionFailure.Set(failure.Failed ? failure : AepFailure.Transport(AepFailureKind.Offline));
        failedAction = action;
        return false;
    }

    private static void NoCompletion(bool succeeded)
    {
    }

    public void Dispose()
    {
        work.Dispose();
    }
}
