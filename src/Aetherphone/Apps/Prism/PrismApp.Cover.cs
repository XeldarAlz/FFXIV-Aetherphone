using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const float MastheadUnits = 24f;
    private const float HeadlineMaxUnits = 48f;
    private const float HeadlineMinUnits = 26f;
    private const int HeadlineLines = 1;
    private const float CoverZoom = 0.06f;
    private const float RuleDelay = 0.05f;
    private const float CreditsDelay = 0.12f;
    private const float StoryDelay = 0.08f;

    private CachedText deadlineText;
    private CachedText judgedText;
    private CachedText issueText;
    private CachedText creditText;
    private CachedText countText;
    private CachedText statusText;

    private void DrawCover(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        if (!store.IsSignedIn)
        {
            DrawMasthead(drawList, content, string.Empty, scale);
            DrawNotice(drawList, content, Loc.T(L.Prism.SignInTitle), Loc.T(L.Prism.SignInRequired), scale);
            if (PrismArt.Strip(drawList, StripRect(content, scale), Loc.T(L.Prism.OpenSettings), "→",
                    true))
            {
                navigation.Open("settings");
            }

            return;
        }

        if (store.Week is not { } week)
        {
            DrawMasthead(drawList, content, string.Empty, scale);
            DrawWeekMissing(drawList, content, scale);
            return;
        }

        if (week.Id.Length == 0 || week.Phase.Length == 0)
        {
            DrawMasthead(drawList, content, string.Empty, scale);
            DrawNotice(drawList, content, Loc.T(L.Prism.NoWeekTitle), Loc.T(L.Prism.NoWeekHint), scale);
            return;
        }

        var cover = CoverPlacing(store.Results);
        if (cover is not null)
        {
            var zoom = 1f + CoverZoom * (1f - PrismMotion.At(screenAge, 0f, Motion.Sheet));
            DrawLook(drawList, full, cover.ImageUrl, false, 1f, zoom);
        }

        PrismArt.Shade(drawList, full, scale);
        var below = DrawMasthead(drawList, content, IssueLine(week.Issue), scale);
        var credits = PrismMotion.Mark(drawList);
        DrawCoverCredits(drawList, content, below, cover, week, scale);
        PrismMotion.Rise(drawList, credits, PrismMotion.At(screenAge, CreditsDelay, Motion.PageSettle), 0f);
        var steps = DrawCoverStory(drawList, content, week, scale);
        var strip = PrismMotion.Mark(drawList);
        DrawCoverStrip(drawList, content, week, scale);
        RiseStep(drawList, strip, steps, scale);
    }

    private void RiseStep(ImDrawListPtr drawList, int mark, int step, float scale) =>
        PrismMotion.Rise(drawList, mark,
            PrismMotion.At(screenAge, StoryDelay + step * PrismMotion.Stagger, Motion.PageSettle),
            PrismMotion.RiseUnits * scale);

    private float DrawMasthead(ImDrawListPtr drawList, Rect content, string right, float scale)
    {
        var gutter = PrismArt.Gutter(scale);
        var top = content.Min.Y + HeaderGapUnits * scale;
        var pixels = MastheadUnits * scale;
        PrismType.Draw(drawList, new Vector2(content.Min.X + gutter, top), DisplayName, PrismArt.White,
            EditorialFace.Headline, pixels);
        if (right.Length > 0)
        {
            var labelPixels = PrismArt.LabelPixels();
            PrismType.DrawRight(drawList, new Vector2(content.Max.X - gutter, top + pixels - labelPixels), right,
                PrismArt.White, EditorialFace.Label, labelPixels);
        }

        var rule = top + PrismType.LineHeight(EditorialFace.Headline, pixels) + HeaderGapUnits * scale;
        var draw = PrismMotion.At(screenAge, RuleDelay, Motion.Sheet);
        var span = content.Width - gutter * 2f;
        PrismArt.SpectrumRule(drawList, content.Min.X + gutter, content.Min.X + gutter + span * draw, rule, scale);
        return rule + HeaderGapUnits * scale;
    }

    private void DrawCoverCredits(ImDrawListPtr drawList, Rect content, float top, PrismPlacingDto? cover,
        PrismWeekDto week, float scale)
    {
        var gutter = PrismArt.Gutter(scale);
        var labelPixels = PrismArt.LabelPixels();
        if (cover is not null)
        {
            PrismType.DrawRight(drawList, new Vector2(content.Max.X - gutter, top), CreditLine(cover), PrismArt.Muted,
                EditorialFace.Label, labelPixels);
            PrismType.DrawRight(drawList, new Vector2(content.Max.X - gutter, top + PrismType.LineHeight(
                EditorialFace.Label, labelPixels)), cover.World, PrismArt.Muted, EditorialFace.Label, labelPixels);
        }

        var left = new Vector2(content.Min.X + gutter, top);
        if (store.Results is { } results)
        {
            var seen = configuration.PrismRevealedWeek == results.WeekId;
            var label = Loc.T(seen ? L.Prism.LastResults : L.Prism.ResultsIn);
            if (PrismArt.Link(drawList, left, label, seen ? PrismArt.Muted : PrismArt.White, scale))
            {
                OpenResults();
            }

            left.Y += Metrics.Size.TapTarget * scale;
        }

        if (week.MyEntry is not null && week.Phase != PrismPhases.Entering &&
            PrismArt.Link(drawList, left, Loc.T(L.Prism.YourLook), PrismArt.Muted, scale))
        {
            Go(PrismScreen.Entry);
        }
    }

    private int DrawCoverStory(ImDrawListPtr drawList, Rect content, PrismWeekDto week, float scale)
    {
        var gutter = PrismArt.Gutter(scale);
        var width = content.Width - gutter * 2f;
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(EditorialFace.Label, labelPixels);
        var briefHeight = week.Brief.Length > 0
            ? Typography.MeasureWrappedBlock(week.Brief, TextStyles.Subheadline, width).Y + Metrics.Space.Sm * scale
            : 0f;
        var count = CountLine(week.EntryCount);
        var deadline = week.Phase == PrismPhases.Tallying ? string.Empty : DeadlineLine(week);
        var pixels = PrismType.Fit(PrismType.Title(week.Title), EditorialFace.Headline, width, HeadlineMaxUnits * scale,
            HeadlineMinUnits * scale, HeadlineLines, out var lines);
        var headline = lines.Length * PrismType.LineHeight(EditorialFace.Headline, pixels);
        var gap = Metrics.Space.Md * scale;
        var eyebrowGap = Metrics.Space.Xs * scale;
        var bottom = StripRect(content, scale).Min.Y - Metrics.Space.Lg * scale;
        var meta = briefHeight + labelLine * ((count.Length > 0 ? 1 : 0) + (deadline.Length > 0 ? 1 : 0));
        var top = bottom - meta - gap - headline - eyebrowGap - labelLine;
        var left = content.Min.X + gutter;
        var step = 0;
        var mark = PrismMotion.Mark(drawList);
        PrismType.Draw(drawList, new Vector2(left, top), Loc.T(L.Prism.ThemeLabel), PrismArt.Muted,
            EditorialFace.Label, labelPixels);
        RiseStep(drawList, mark, step++, scale);
        top += labelLine + eyebrowGap;
        var headlineLine = PrismType.LineHeight(EditorialFace.Headline, pixels);
        for (var index = 0; index < lines.Length; index++)
        {
            mark = PrismMotion.Mark(drawList);
            PrismType.Draw(drawList, new Vector2(left, top), lines[index], PrismArt.White, EditorialFace.Headline,
                pixels);
            RiseStep(drawList, mark, step++, scale);
            top += headlineLine;
        }

        top += gap;
        mark = PrismMotion.Mark(drawList);
        if (briefHeight > 0f)
        {
            top += Typography.DrawWrappedLeft(new Vector2(left, top), week.Brief, PrismArt.White,
                TextStyles.Subheadline, width) + Metrics.Space.Sm * scale;
        }

        if (deadline.Length > 0)
        {
            PrismType.Draw(drawList, new Vector2(left, top), deadline, PrismArt.White, EditorialFace.Label, labelPixels);
            top += labelLine;
        }

        if (count.Length > 0)
        {
            PrismType.Draw(drawList, new Vector2(left, top), count, PrismArt.Muted, EditorialFace.Label, labelPixels);
        }

        RiseStep(drawList, mark, step++, scale);
        return step;
    }

    private void DrawCoverStrip(ImDrawListPtr drawList, Rect content, PrismWeekDto week, float scale)
    {
        var strip = StripRect(content, scale);
        if (week.Phase == PrismPhases.Entering)
        {
            if (week.MyEntry is { } entry)
            {
                if (PrismArt.Strip(drawList, strip, StatusLine(entry), "→", true))
                {
                    Go(PrismScreen.Entry);
                }

                return;
            }

            var left = Loc.T(store.Busy ? L.Prism.Uploading : L.Prism.EnterLook);
            if (PrismArt.Strip(drawList, strip, left, "→", true, !store.Busy))
            {
                StartPick();
            }

            return;
        }

        if (PrismPhases.Judgeable(week.Phase))
        {
            var label = week.Phase == PrismPhases.Final ? L.Prism.JudgeFinal : L.Prism.JudgeHeat;
            if (PrismArt.Strip(drawList, strip, Loc.T(label), JudgedLine(week.BallotsCast), true,
                    !store.Busy))
            {
                StartJudging();
            }

            return;
        }

        PrismArt.Strip(drawList, strip, DeadlineLine(week), string.Empty, false, false);
    }

    private void DrawWeekMissing(ImDrawListPtr drawList, Rect content, float scale)
    {
        if (!store.WeekFailed || store.Loading)
        {
            var loading = Loc.T(L.Common.Loading);
            var size = PrismType.Measure(loading, EditorialFace.Label, PrismArt.LabelPixels());
            PrismType.Draw(drawList, content.Center - size * 0.5f, loading, PrismArt.Muted, EditorialFace.Label,
                PrismArt.LabelPixels());
            return;
        }

        var reason = store.WeekFailureText;
        DrawNotice(drawList, content, Loc.T(L.Common.LoadFailed), reason.Length > 0 ? reason : Loc.T(L.Common.LoadFailedHint),
            scale);
        if (PrismArt.Strip(drawList, StripRect(content, scale), Loc.T(L.Common.Retry), "→", true))
        {
            UiFeedback.Play(UiSound.Refresh);
            store.Refresh();
        }
    }

    private void OpenResults()
    {
        if (store.Results is not { } results)
        {
            return;
        }

        if (configuration.PrismRevealedWeek == results.WeekId || (results.FinalPodium?.Length ?? 0) == 0)
        {
            Go(PrismScreen.Final);
            return;
        }

        StartReveal();
    }

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

    private string DeadlineLine(PrismWeekDto week)
    {
        var (format, unix) = week.Phase switch
        {
            PrismPhases.Entering => (L.Prism.EntriesClose, week.EntriesCloseAtUnix),
            PrismPhases.Judging => (L.Prism.JudgingCloses, week.JudgingCloseAtUnix),
            PrismPhases.Final => (L.Prism.FinalCloses, week.FinalCloseAtUnix),
            _ => (L.Prism.ResultsAt, week.ResultsAtUnix),
        };
        if (unix <= 0)
        {
            return string.Empty;
        }

        var minute = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
        var key = HashCode.Combine(week.Phase, unix, minute, Loc.Current.Code);
        return deadlineText.IsCurrent(key)
            ? deadlineText.Value
            : deadlineText.Store(key, Loc.T(format, TimeText.Until(unix)));
    }

    private string JudgedLine(int count)
    {
        var key = HashCode.Combine(count, Loc.Current.Code);
        return judgedText.IsCurrent(key)
            ? judgedText.Value
            : judgedText.Store(key, Loc.Plural(L.Prism.HeatsJudged, count));
    }

    private string IssueLine(int issue)
    {
        if (issue <= 0)
        {
            return string.Empty;
        }

        var key = HashCode.Combine(issue, Loc.Current.Code);
        return issueText.IsCurrent(key) ? issueText.Value : issueText.Store(key, Loc.T(L.Prism.Issue, issue));
    }

    private string CreditLine(PrismPlacingDto cover)
    {
        var key = HashCode.Combine(cover.DisplayName, Loc.Current.Code);
        return creditText.IsCurrent(key)
            ? creditText.Value
            : creditText.Store(key, Loc.T(L.Prism.CoverCredit, cover.DisplayName));
    }

    private string CountLine(int count)
    {
        if (count <= 0)
        {
            return string.Empty;
        }

        var key = HashCode.Combine(count, Loc.Current.Code);
        return countText.IsCurrent(key)
            ? countText.Value
            : countText.Store(key, Loc.Plural(L.Prism.LooksThisIssue, count));
    }

    private string StatusLine(PrismEntryDto entry)
    {
        var key = HashCode.Combine(entry.Status, Loc.Current.Code);
        return statusText.IsCurrent(key)
            ? statusText.Value
            : statusText.Store(key, Loc.T(L.Prism.YourLookStatus, Loc.T(StatusName(entry.Status))));
    }

    private static LocString StatusName(string status) => status switch
    {
        PrismPhases.StatusLive => L.Prism.StatusLive,
        PrismPhases.StatusRejected => L.Prism.StatusRejected,
        _ => L.Prism.StatusReview,
    };
}
