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
    private float introDelay;

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
        var backdrop = cover?.ImageUrl ?? week.MyEntry?.ImageUrl ?? string.Empty;
        if (backdrop.Length > 0)
        {
            var zoom = 1f + CoverZoom * (1f - PrismMotion.At(screenAge, 0f, Motion.Sheet));
            DrawLook(drawList, full, backdrop, false, 1f, zoom);
            PrismArt.Shade(drawList, full, scale);
            onPhoto = true;
        }

        var below = DrawMasthead(drawList, content, cover is null ? string.Empty : CreditLine(cover), scale);
        var buttons = PrismMotion.Mark(drawList);
        DrawCoverButtons(drawList, content, below, week, scale);
        PrismMotion.Rise(drawList, buttons, PrismMotion.At(screenAge, introDelay + CreditsDelay, Motion.PageSettle),
            0f);
        var strips = HasPrimary(week) || store.Results is not null ? 1 : 0;
        PrismArt.OverPhoto(backdrop.Length > 0);
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

    private float DrawMasthead(ImDrawListPtr drawList, Rect content, string under, float scale)
    {
        var gutter = PrismArt.Gutter(scale);
        var top = content.Min.Y + HeaderGapUnits * scale;
        var pixels = MastheadUnits * scale;
        var labelPixels = PrismArt.LabelPixels();
        var mark = PrismMotion.Mark(drawList);
        PrismType.Draw(drawList, new Vector2(content.Min.X + gutter, top), DisplayName, PrismArt.Fore,
            PrismFace.Headline, pixels);
        var nameBottom = top + PrismType.Measure(DisplayName, PrismFace.Headline, pixels).Y;
        var guide = Loc.T(L.Prism.HowItWorks);
        var buttonTop = (top + nameBottom - PrismArt.ButtonUnits * scale) * 0.5f;
        if (PrismArt.Button(drawList, new Vector2(content.Max.X - gutter - PrismArt.ButtonWidth(guide, scale),
                buttonTop), guide, scale, out _))
        {
            Go(PrismScreen.Guide);
        }

        var rule = top + PrismType.LineHeight(PrismFace.Headline, pixels) + HeaderGapUnits * scale;
        var gap = rule - nameBottom;
        PrismMotion.Rise(drawList, mark, PrismMotion.At(screenAge, introDelay, Motion.PageSettle),
            PrismMotion.RiseUnits * scale);
        var draw = PrismMotion.At(screenAge, introDelay + RuleDelay, Motion.Sheet);
        var span = content.Width - gutter * 2f;
        PrismArt.SpectrumRule(drawList, content.Min.X + gutter, content.Min.X + gutter + span * draw, rule, scale);
        if (draw >= PrismMotion.Settled)
        {
            PrismArt.RuleGlow(drawList, content.Min.X + gutter, content.Max.X - gutter, rule, scale);
        }

        var ruleBottom = rule + MathF.Max(1f, 2f * scale);
        if (under.Length == 0)
        {
            return ruleBottom + HeaderGapUnits * scale;
        }

        var underTop = ruleBottom + gap;
        mark = PrismMotion.Mark(drawList);
        PrismType.DrawRight(drawList, new Vector2(content.Max.X - gutter, underTop), under, PrismArt.Muted,
            PrismFace.Label, labelPixels);
        PrismMotion.Rise(drawList, mark, PrismMotion.At(screenAge, introDelay + CreditsDelay, Motion.PageSettle), 0f);
        return underTop + PrismType.Measure(under, PrismFace.Label, labelPixels).Y + Metrics.Space.Md * scale;
    }

    private void DrawCoverButtons(ImDrawListPtr drawList, Rect content, float top, PrismWeekDto week, float scale)
    {
        if (week.MyEntry is not null && week.Phase != PrismPhases.Entering &&
            PrismArt.Button(drawList, new Vector2(content.Min.X + PrismArt.Gutter(scale), top), Loc.T(L.Prism.YourLook),
                scale, out _))
        {
            Go(PrismScreen.Entry);
        }
    }

    private int DrawCoverStory(ImDrawListPtr drawList, Rect content, PrismWeekDto week, int strips, float scale)
    {
        var gutter = PrismArt.Gutter(scale);
        var width = content.Width - gutter * 2f;
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var statPixels = StatUnits * scale;
        var statHeight = Metrics.Space.Md * scale + labelLine + PrismType.LineHeight(PrismFace.Headline, statPixels);
        var pixels = PrismType.Fit(week.Title, PrismFace.Headline, width, HeadlineMaxUnits * scale,
            HeadlineMinUnits * scale, HeadlineLines, out var lines);
        var headlineLine = PrismType.LineHeight(PrismFace.Headline, pixels);
        var eyebrowGap = Metrics.Space.Xs * scale;
        var bottom = (strips > 0 ? StripRect(content, scale, strips - 1).Min.Y : content.Max.Y - PrismArt.BottomMargin(scale)) -
                     Metrics.Space.Xl * scale;
        var top = bottom - statHeight - Metrics.Space.Xl * scale - lines.Length * headlineLine -
                  eyebrowGap - labelLine;
        var left = content.Min.X + gutter;
        var step = 0;
        var mark = PrismMotion.Mark(drawList);
        PrismType.Draw(drawList, new Vector2(left, top), Loc.T(L.Prism.ThemeLabel), PrismArt.Muted,
            PrismFace.Label, labelPixels);
        RiseStep(drawList, mark, step++, scale);
        top += labelLine + eyebrowGap;
        for (var index = 0; index < lines.Length; index++)
        {
            mark = PrismMotion.Mark(drawList);
            PrismType.Draw(drawList, new Vector2(left, top), lines[index], PrismArt.Fore, PrismFace.Headline,
                pixels);
            RiseStep(drawList, mark, step++, scale);
            top += headlineLine;
        }

        mark = PrismMotion.Mark(drawList);
        var statTop = bottom - statHeight;
        var column = (width - Metrics.Space.Lg * scale) * 0.5f;
        var totalLeft = left;
        if (week.Phase != PrismPhases.Results)
        {
            DrawStat(drawList, new Vector2(left, statTop), column, Loc.T(DeadlineLabel(week.Phase)),
                DeadlineValue(week), scale);
            totalLeft += column + Metrics.Space.Lg * scale;
        }

        DrawStat(drawList, new Vector2(totalLeft, statTop), column, Loc.T(L.Prism.LooksIn), LookTotal(week.EntryCount),
            scale);
        RiseStep(drawList, mark, step++, scale);
        return step;
    }

    private static void DrawStat(ImDrawListPtr drawList, Vector2 topLeft, float width, string label, string value,
        float scale)
    {
        PrismArt.Hairline(drawList, topLeft.X, topLeft.X + width, topLeft.Y, PrismArt.Faint, scale);
        var labelPixels = PrismArt.LabelPixels();
        var top = topLeft.Y + Metrics.Space.Md * scale;
        PrismType.Draw(drawList, new Vector2(topLeft.X, top), label, PrismArt.Muted, PrismFace.Label, labelPixels);
        PrismType.Draw(drawList, new Vector2(topLeft.X, top + PrismType.LineHeight(PrismFace.Label, labelPixels)),
            value, PrismArt.Fore, PrismFace.Headline, StatUnits * scale);
    }

    private static bool HasPrimary(PrismWeekDto week) =>
        week.Phase == PrismPhases.Entering || PrismPhases.Judgeable(week.Phase);

    private void DrawCoverStrip(ImDrawListPtr drawList, Rect content, PrismWeekDto week, float scale)
    {
        var row = StripRect(content, scale);
        var half = (row.Width - Metrics.Space.Md * scale) * 0.5f;
        var strip = store.Results is null ? row : new Rect(row.Min, new Vector2(row.Min.X + half, row.Max.Y));
        if (store.Results is { } results)
        {
            var seen = Revealed(results);
            var slot = HasPrimary(week) ? new Rect(new Vector2(row.Max.X - half, row.Min.Y), row.Max) : row;
            if (PrismArt.Strip(drawList, slot, Loc.T(seen ? L.Prism.LastResults : L.Prism.ResultsIn), "→", false))
            {
                OpenResults();
            }
        }

        if (week.Phase == PrismPhases.Entering)
        {
            if (week.MyEntry is not null)
            {
                if (PrismArt.Strip(drawList, strip, Loc.T(L.Prism.YourLook), "→", true))
                {
                    Go(PrismScreen.Entry);
                }

                return;
            }

            var left = Loc.T(store.Busy ? L.Prism.Uploading : L.Prism.Submit);
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
        var right = store.Results is not null ? "→"
            : HeatInProgress ? LookCounter(lookIndex + 1, stars.Length) : JudgedLine(week.BallotsCast);
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
            var size = PrismType.Measure(loading, PrismFace.Label, PrismArt.LabelPixels());
            PrismType.Draw(drawList, content.Center - size * 0.5f, loading, PrismArt.Muted, PrismFace.Label,
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

    private static LocString StatusName(string status) =>
        status == PrismPhases.StatusRejected ? L.Prism.StatusRejected : L.Prism.StatusLive;
}
