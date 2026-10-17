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
    private const float StatUnits = 24f;
    private const float CoverZoom = 0.06f;
    private const float RuleDelay = 0.05f;
    private const float CreditsDelay = 0.12f;
    private const float StoryDelay = 0.08f;
    private const float IntroSeconds = 0.9f;
    private const float IntroStoryShare = 1.35f;

    private CachedText deadlineText;
    private CachedText judgedText;
    private CachedText issueText;
    private CachedText creditText;
    private CachedText countText;
    private CachedText totalText;
    private CachedText statusText;
    private float introDelay;

    private void DrawCover(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        if (!store.IsSignedIn)
        {
            DrawMasthead(drawList, content, string.Empty, string.Empty, scale);
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
            DrawMasthead(drawList, content, string.Empty, string.Empty, scale);
            DrawWeekMissing(drawList, content, scale);
            return;
        }

        if (week.Id.Length == 0 || week.Phase.Length == 0)
        {
            DrawMasthead(drawList, content, string.Empty, string.Empty, scale);
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
        var below = DrawMasthead(drawList, content, IssueLine(week.Issue),
            cover is null ? string.Empty : CreditLine(cover), scale);
        var pills = PrismMotion.Mark(drawList);
        DrawCoverPills(drawList, content, below, week, scale);
        PrismMotion.Rise(drawList, pills, PrismMotion.At(screenAge, introDelay + CreditsDelay, Motion.PageSettle),
            0f);
        var primary = week.Phase == PrismPhases.Entering || PrismPhases.Judgeable(week.Phase);
        var strips = (primary ? 1 : 0) + (store.Results is null ? 0 : 1);
        var steps = DrawCoverStory(drawList, content, week, strips, scale);
        var strip = PrismMotion.Mark(drawList);
        DrawCoverStrip(drawList, content, week, scale);
        RiseStep(drawList, strip, steps, scale);
    }

    private void RiseStep(ImDrawListPtr drawList, int mark, int step, float scale) =>
        PrismMotion.Rise(drawList, mark,
            PrismMotion.At(screenAge, introDelay * IntroStoryShare + StoryDelay + step * PrismMotion.Stagger,
                Motion.PageSettle),
            PrismMotion.RiseUnits * scale);

    private float DrawMasthead(ImDrawListPtr drawList, Rect content, string right, string under, float scale)
    {
        var gutter = PrismArt.Gutter(scale);
        var top = content.Min.Y + HeaderGapUnits * scale;
        var pixels = MastheadUnits * scale;
        var labelPixels = PrismArt.LabelPixels();
        var mark = PrismMotion.Mark(drawList);
        PrismType.Draw(drawList, new Vector2(content.Min.X + gutter, top), DisplayName, PrismArt.White,
            EditorialFace.Headline, pixels);
        var rule = top + PrismType.LineHeight(EditorialFace.Headline, pixels) + HeaderGapUnits * scale;
        var gap = rule;
        if (right.Length > 0)
        {
            var rightTop = top + pixels - labelPixels;
            PrismType.DrawRight(drawList, new Vector2(content.Max.X - gutter, rightTop), right, PrismArt.White,
                EditorialFace.Label, labelPixels);
            gap = rule - rightTop - PrismType.Measure(right, EditorialFace.Label, labelPixels).Y;
        }

        PrismMotion.Rise(drawList, mark, PrismMotion.At(screenAge, introDelay, Motion.PageSettle),
            PrismMotion.RiseUnits * scale);
        var draw = PrismMotion.At(screenAge, introDelay + RuleDelay, Motion.Sheet);
        var span = content.Width - gutter * 2f;
        PrismArt.SpectrumRule(drawList, content.Min.X + gutter, content.Min.X + gutter + span * draw, rule, scale);
        var ruleBottom = rule + MathF.Max(1f, 2f * scale);
        if (under.Length == 0 || right.Length == 0)
        {
            return ruleBottom + HeaderGapUnits * scale;
        }

        var underTop = ruleBottom + gap;
        mark = PrismMotion.Mark(drawList);
        PrismType.DrawRight(drawList, new Vector2(content.Max.X - gutter, underTop), under, PrismArt.Muted,
            EditorialFace.Label, labelPixels);
        PrismMotion.Rise(drawList, mark, PrismMotion.At(screenAge, introDelay + CreditsDelay, Motion.PageSettle), 0f);
        return underTop + PrismType.Measure(under, EditorialFace.Label, labelPixels).Y + Metrics.Space.Md * scale;
    }

    private void DrawCoverPills(ImDrawListPtr drawList, Rect content, float top, PrismWeekDto week, float scale)
    {
        var left = content.Min.X + PrismArt.Gutter(scale);
        if (week.MyEntry is not null && week.Phase != PrismPhases.Entering)
        {
            if (PrismArt.Pill(drawList, new Vector2(left, top), Loc.T(L.Prism.YourLook), scale, out var width))
            {
                Go(PrismScreen.Entry);
            }

            left += width + Metrics.Space.Sm * scale;
        }

        if (PrismArt.Pill(drawList, new Vector2(left, top), Loc.T(L.Prism.HowItWorks), scale, out _))
        {
            Go(PrismScreen.Guide);
        }
    }

    private int DrawCoverStory(ImDrawListPtr drawList, Rect content, PrismWeekDto week, int strips, float scale)
    {
        var gutter = PrismArt.Gutter(scale);
        var width = content.Width - gutter * 2f;
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(EditorialFace.Label, labelPixels);
        var statPixels = StatUnits * scale;
        var statHeight = Metrics.Space.Md * scale + labelLine + PrismType.LineHeight(EditorialFace.Headline, statPixels);
        var pixels = PrismType.Fit(PrismType.Title(week.Title), EditorialFace.Headline, width, HeadlineMaxUnits * scale,
            HeadlineMinUnits * scale, HeadlineLines, out var lines);
        var headlineLine = PrismType.LineHeight(EditorialFace.Headline, pixels);
        var eyebrowGap = Metrics.Space.Xs * scale;
        var bottom = (strips > 0 ? StripRect(content, scale, strips - 1).Min.Y : content.Max.Y - gutter) -
                     Metrics.Space.Xl * scale;
        var top = bottom - statHeight - Metrics.Space.Xl * scale - lines.Length * headlineLine -
                  eyebrowGap - labelPixels;
        var left = content.Min.X + gutter;
        var step = 0;
        var mark = PrismMotion.Mark(drawList);
        PrismType.Draw(drawList, new Vector2(left, top), Loc.T(L.Prism.ThemeLabel), PrismArt.Muted,
            EditorialFace.Label, labelPixels);
        RiseStep(drawList, mark, step++, scale);
        top += labelPixels + eyebrowGap;
        for (var index = 0; index < lines.Length; index++)
        {
            mark = PrismMotion.Mark(drawList);
            PrismType.Draw(drawList, new Vector2(left, top), lines[index], PrismArt.White, EditorialFace.Headline,
                pixels);
            RiseStep(drawList, mark, step++, scale);
            top += headlineLine;
        }

        mark = PrismMotion.Mark(drawList);
        var statTop = bottom - statHeight;
        var column = (width - Metrics.Space.Lg * scale) * 0.5f;
        DrawStat(drawList, new Vector2(left, statTop), column, Loc.T(DeadlineLabel(week.Phase)), DeadlineValue(week),
            scale);
        DrawStat(drawList, new Vector2(left + column + Metrics.Space.Lg * scale, statTop), column,
            Loc.T(L.Prism.LooksIn), LookTotal(week.EntryCount), scale);
        RiseStep(drawList, mark, step++, scale);
        return step;
    }

    private static void DrawStat(ImDrawListPtr drawList, Vector2 topLeft, float width, string label, string value,
        float scale)
    {
        PrismArt.Hairline(drawList, topLeft.X, topLeft.X + width, topLeft.Y, PrismArt.Faint, scale);
        var labelPixels = PrismArt.LabelPixels();
        var top = topLeft.Y + Metrics.Space.Md * scale;
        PrismType.Draw(drawList, new Vector2(topLeft.X, top), label, PrismArt.Muted, EditorialFace.Label, labelPixels);
        PrismType.Draw(drawList, new Vector2(topLeft.X, top + PrismType.LineHeight(EditorialFace.Label, labelPixels)),
            value, PrismArt.White, EditorialFace.Headline, StatUnits * scale);
    }

    private void DrawCoverStrip(ImDrawListPtr drawList, Rect content, PrismWeekDto week, float scale)
    {
        var strip = StripRect(content, scale, store.Results is null ? 0 : 1);
        if (store.Results is { } results)
        {
            var seen = configuration.PrismRevealedWeek == results.WeekId;
            if (PrismArt.Strip(drawList, StripRect(content, scale), Loc.T(seen ? L.Prism.LastResults : L.Prism.ResultsIn),
                    "→", false))
            {
                OpenResults();
            }
        }

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

        if (!PrismPhases.Judgeable(week.Phase))
        {
            return;
        }

        var label = week.Phase == PrismPhases.Final ? L.Prism.JudgeFinal : L.Prism.JudgeHeat;
        var right = HeatInProgress ? LookCounter(lookIndex + 1, stars.Length) : JudgedLine(week.BallotsCast);
        if (PrismArt.Strip(drawList, strip, Loc.T(label), right, true, !store.Busy))
        {
            StartJudging();
        }
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

    private static LocString DeadlineLabel(string phase) => phase switch
    {
        PrismPhases.Entering => L.Prism.EntriesClose,
        PrismPhases.Judging => L.Prism.JudgingCloses,
        PrismPhases.Final => L.Prism.FinalCloses,
        _ => L.Prism.ResultsAt,
    };

    private string DeadlineValue(PrismWeekDto week)
    {
        var unix = week.Phase switch
        {
            PrismPhases.Entering => week.EntriesCloseAtUnix,
            PrismPhases.Judging => week.JudgingCloseAtUnix,
            PrismPhases.Final => week.FinalCloseAtUnix,
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

    private string LookTotal(int count)
    {
        var key = HashCode.Combine(count, Loc.Current.Code);
        return totalText.IsCurrent(key) ? totalText.Value : totalText.Store(key, count.ToString("N0", Loc.Culture));
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
