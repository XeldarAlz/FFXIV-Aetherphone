using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const float CreditGlyphShare = 0.8f;
    private const float TrackLabelShare = 0.88f;

    private static readonly string RewardsGlyph = IconGlyph.Of(FontAwesomeIcon.Gift);

    private CachedText deadlineText;
    private CachedText judgedText;

    private void DrawCover(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        var area = new Rect(content.Min, new Vector2(content.Max.X, content.Max.Y - bottomChrome));
        if (!store.IsSignedIn)
        {
            DrawEmpty(drawList, area, PhoneIcons.Crown, Loc.T(L.Prism.SignInTitle), Loc.T(L.Prism.SignInRequired),
                Loc.T(L.Prism.OpenSettings), ButtonStyle.Solid, scale, out var settings);
            if (settings)
            {
                navigation.Open(SettingsAppId);
            }

            return;
        }

        if (store.Week is not { } week)
        {
            DrawWeekMissing(drawList, area, scale);
            return;
        }

        DrawContest(drawList, full, content, week, scale);
    }

    private static float DrawGlyphCaption(ImDrawListPtr drawList, Vector2 topLeft, float right, string glyph,
        string text, Vector4 ink, float scale)
    {
        var line = Typography.LineHeight(PrismType.Caption);
        PhoneIcon.Draw(drawList, topLeft + new Vector2(line * 0.5f, line * 0.5f), glyph, ink, line * CreditGlyphShare);
        var left = topLeft.X + line + Metrics.Space.Xs * scale;
        var fitted = Typography.FitText(text, MathF.Max(1f, right - left), PrismType.Caption);
        Typography.Draw(drawList, new Vector2(left, topLeft.Y), fitted, ink, PrismType.Caption);
        return left + Typography.Measure(fitted, PrismType.Caption).X;
    }

    private static int DrawSegments(string id, Rect row, IReadOnlyList<string> labels, int selected)
    {
        var track = Surfaces.Fill(PrismArt.Fore, FillLevel.Tertiary);
        var under = Vector4.Lerp(PrismArt.Page, PrismArt.Fore, track.W);
        var thumb = Vector4.Lerp(under, PrismArt.Fore, Surfaces.Fill(PrismArt.Fore, FillLevel.Primary).W)
            with { W = 1f };
        return SegmentStrip.Draw(id, row, labels, selected, track, thumb, PrismArt.Muted, PrismArt.Fore,
            Metrics.Size.TapTarget, PrismType.Headline.Scale * TrackLabelShare);
    }

    private string EnterLabel() => Loc.T(store.Busy ? L.Prism.Uploading : L.Prism.EnterThisWeek);

    private static string FactLine(ref CachedText cache, string label, string detail)
    {
        var key = HashCode.Combine(label, detail);
        return cache.IsCurrent(key) ? cache.Value : cache.Store(key, Loc.T(L.Prism.WithTrack, label, detail));
    }

    private PrismResultsDto? ShownResults() =>
        store.Results is { } results && PrismStore.ShowsTrack(results, store.Track) ? results : null;

    private bool ShowingResults() => ShownResults() is not null;

    private void DrawWeekMissing(ImDrawListPtr drawList, Rect area, float scale)
    {
        if (!store.WeekFailed || store.Loading)
        {
            var loading = Loc.T(L.Common.Loading);
            Typography.Draw(drawList, area.Center - Typography.Measure(loading, PrismType.Caption) * 0.5f, loading,
                PrismArt.Muted, PrismType.Caption);
            return;
        }

        var reason = store.WeekFailureText;
        DrawEmpty(drawList, area, PhoneIcons.Refresh, Loc.T(L.Common.LoadFailed),
            reason.Length > 0 ? reason : Loc.T(L.Common.LoadFailedHint), Loc.T(L.Common.Retry), ButtonStyle.Solid,
            scale, out var retry);
        if (retry)
        {
            UiFeedback.Play(UiSound.Refresh);
            store.Refresh();
        }
    }

    private void OpenResults()
    {
        if (ShownResults() is not { } results)
        {
            return;
        }

        if (Revealed(results) || (results.FinalPodium?.Length ?? 0) == 0)
        {
            Go(PrismScreen.Final);
            return;
        }

        StartReveal();
    }

    private bool Revealed(PrismResultsDto results) =>
        configuration.PrismRevealedWeek == results.WeekId && configuration.PrismRevealedRevision == results.Revision;

    private static PrismPlacingDto? CoverPlacing(PrismResultsDto? results)
    {
        var podium = results?.FinalPodium;
        if (podium is null)
        {
            return null;
        }

        for (var index = 0; index < podium.Length; index++)
        {
            if (podium[index].Rank == 1)
            {
                return podium[index];
            }
        }

        return null;
    }

    private static LocString DeadlineLabel(string phase) => phase switch
    {
        PrismPhases.Entering => L.Prism.EntriesClose,
        PrismPhases.Judging => L.Prism.JudgingCloses,
        PrismPhases.Final => L.Prism.FinalCloses,
        PrismPhases.Results => L.Prism.NextTheme,
        _ => L.Prism.ResultsAt,
    };

    private string DeadlineValue(PrismWeekDto week)
    {
        var unix = week.Phase switch
        {
            PrismPhases.Entering => week.EntriesCloseAtUnix,
            PrismPhases.Judging => week.JudgingCloseAtUnix,
            PrismPhases.Final => week.FinalCloseAtUnix,
            PrismPhases.Results => week.NextWeekAtUnix,
            _ => week.ResultsAtUnix,
        };
        if (unix <= 0)
        {
            return string.Empty;
        }

        var minute = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
        var key = HashCode.Combine(week.Phase, unix, minute, Loc.Current.Code);
        return deadlineText.IsCurrent(key) ? deadlineText.Value : deadlineText.Store(key, TimeText.Until(unix));
    }

    private string JudgedLine(int count)
    {
        var key = HashCode.Combine(count, Loc.Current.Code);
        return judgedText.IsCurrent(key)
            ? judgedText.Value
            : judgedText.Store(key, Loc.Plural(L.Prism.HeatsJudged, count));
    }

    private static string AtHandle(string handle, string displayName) =>
        handle.Length > 0 ? string.Concat("@", handle) : displayName;

    private static LocString StatusName(string status) => status switch
    {
        PrismPhases.StatusRejected => L.Prism.StatusRejected,
        PrismPhases.StatusMoved => L.Prism.StatusMoved,
        _ => L.Prism.StatusLive,
    };
}
