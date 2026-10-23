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
    private const float MastheadUnits = PrismType.HeadingUnits;
    private const float HeadlineMaxUnits = PrismType.DisplayUnits;
    private const float HeadlineMinUnits = PrismType.HeadingUnits;
    private const int HeadlineLines = 1;
    private const float StatUnits = PrismType.HeadingUnits;
    private const float StatMinShare = 0.6f;
    private const float GlowLeadSeconds = 0.85f;
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
    private CachedText wonText;
    private CachedText countText;
    private CachedText totalText;
    private CachedText rewardsText;
    private float introDelay;
    private float coverHeadBottom;
    private bool mastheadDot;
    private double dotGlowAt = double.NegativeInfinity;

    private void DrawCover(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        if (!store.IsSignedIn)
        {
            DrawSignedOut(drawList, content, scale);
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
            if (store.Results is { } last &&
                PrismArt.Strip(drawList, StripRect(content, scale), Loc.T(ResultsLabel(last)), Arrow, true))
            {
                OpenResults();
            }

            return;
        }

        var cover = CoverPlacing(store.Results);
        var won = cover is not null && week.Phase == PrismPhases.Results && store.Results?.WeekId == week.Id;
        var backdrop = cover?.ImageUrl ?? week.MyEntry?.ImageUrl ?? string.Empty;
        if (backdrop.Length > 0)
        {
            var zoom = 1f + CoverZoom * (1f - PrismMotion.At(screenAge, 0f, Motion.Sheet));
            DrawLook(drawList, full, backdrop, false, 1f, zoom);
            PrismArt.Shade(drawList, full, scale, coverHeadBottom);
            onPhoto = true;
        }

        PrismArt.OverPhoto(backdrop.Length > 0);
        var below = DrawMasthead(drawList, content, cover is null || won ? string.Empty : CreditLine(cover), scale);
        var buttons = PrismMotion.Mark(drawList);
        coverHeadBottom = DrawCoverButtons(drawList, content, below, week, scale);
        PrismMotion.Rise(drawList, buttons, PrismMotion.At(screenAge, introDelay + CreditsDelay, Motion.PageSettle),
            0f);
        var strips = HasPrimary(week) || store.Results is not null ? 1 : 0;
        var steps = DrawCoverStory(drawList, content, week, won ? WonLine(cover!) : string.Empty, strips, scale);
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
        var below = DrawMasthead(drawList, content, DisplayName, Loc.T(L.Prism.HowItWorks), under, scale, out var guide);
        if (guide)
        {
            StartGuide(tab);
        }

        return below;
    }

    private float DrawMasthead(ImDrawListPtr drawList, Rect content, string title, string action, string under,
        float scale, out bool acted, string icon = "", bool dot = false)
    {
        var gutter = PrismArt.Gutter(scale);
        var top = content.Min.Y + HeaderGapUnits * scale;
        var pixels = MastheadUnits * scale;
        var labelPixels = PrismArt.LabelPixels();
        var mark = PrismMotion.Mark(drawList);
        PrismType.Draw(drawList, new Vector2(content.Min.X + gutter, top), title, PrismArt.Fore, PrismFace.Headline,
            pixels);
        var nameBottom = top + PrismType.Measure(title, PrismFace.Headline, pixels).Y;
        var buttonTop = (top + nameBottom - PrismArt.ButtonUnits * scale) * 0.5f;
        acted = action.Length > 0
            ? PrismArt.Button(drawList, new Vector2(content.Max.X - gutter - PrismArt.ButtonWidth(action, scale),
                buttonTop), action, scale, out _)
            : icon.Length > 0 && PrismArt.Icon(drawList,
                new Vector2(content.Max.X - gutter - PrismArt.IconUnits * scale * 0.5f, (top + nameBottom) * 0.5f),
                icon, dot, scale);

        var rule = top + PrismType.LineHeight(PrismFace.Headline, pixels) + HeaderGapUnits * scale;
        var gap = rule - nameBottom;
        PrismMotion.Rise(drawList, mark, PrismMotion.At(screenAge, introDelay, Motion.PageSettle),
            PrismMotion.RiseUnits * scale);
        var draw = PrismMotion.At(screenAge, introDelay + RuleDelay, Motion.Sheet);
        if (dot && !mastheadDot && draw >= PrismMotion.Settled)
        {
            dotGlowAt = ImGui.GetTime();
        }

        mastheadDot = dot;
        var span = content.Width - gutter * 2f;
        PrismArt.SpectrumRule(drawList, content.Min.X + gutter, content.Min.X + gutter + span * draw, rule, scale);
        if (draw >= PrismMotion.Settled)
        {
            var entry = screenAge - introDelay - RuleDelay - GlowLeadSeconds;
            var news = (float)(ImGui.GetTime() - dotGlowAt);
            PrismArt.RuleGlow(drawList, content.Min.X + gutter, content.Max.X - gutter, rule, scale,
                entry < 0f ? entry : MathF.Min(entry, news));
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

    private float DrawCoverButtons(ImDrawListPtr drawList, Rect content, float top, PrismWeekDto week, float scale)
    {
        var labelPixels = PrismArt.LabelPixels();
        var rewardsKey = Loc.Current.Code.GetHashCode(StringComparison.Ordinal);
        var rewards = rewardsText.IsCurrent(rewardsKey)
            ? rewardsText.Value
            : rewardsText.Store(rewardsKey, Loc.T(L.Prism.Rewards) + " " + Arrow);
        var rewardsLeft = content.Max.X - PrismArt.Gutter(scale) -
                          PrismType.Measure(rewards, PrismFace.Label, labelPixels).X;
        var tabsTop = top;
        top = DrawTrackTabs(drawList, content, top, scale, out var tabsRight);
        var rewardsTop = top > tabsTop && tabsRight + Metrics.Space.Xl * scale <= rewardsLeft
            ? tabsTop
            : top + (PrismArt.ButtonUnits * scale - labelPixels) * 0.5f;
        if (PrismArt.Link(drawList, new Vector2(rewardsLeft, rewardsTop), rewards, PrismArt.Fore, scale))
        {
            OpenRewards();
        }

        var bottom = MathF.Max(top - Metrics.Space.Lg * scale,
            rewardsTop + PrismType.LineHeight(PrismFace.Label, labelPixels));
        if (week.MyEntry is null || week.Phase == PrismPhases.Entering)
        {
            return bottom;
        }

        if (PrismArt.Button(drawList, new Vector2(content.Min.X + PrismArt.Gutter(scale), top), Loc.T(L.Prism.YourLook),
                scale, out _))
        {
            Go(PrismScreen.Entry);
        }

        return MathF.Max(bottom, top + PrismArt.ButtonUnits * scale);
    }

    private string EnterLabel() => Loc.T(store.Busy ? L.Prism.Uploading : L.Prism.Submit);

    private void StartEntry()
    {
        if (HasTracks)
        {
            StartTrackChoice(false);
        }
        else
        {
            StartPick();
        }
    }

    private int DrawCoverStory(ImDrawListPtr drawList, Rect content, PrismWeekDto week, string winner, int strips,
        float scale)
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
        var bottom = (strips > 0
                         ? StripRect(content, scale, strips - 1).Min.Y
                         : content.Max.Y - bottomChrome - PrismArt.BottomMargin(scale)) -
                     Metrics.Space.Xl * scale;
        var winnerHeight = winner.Length > 0 ? eyebrowGap + labelLine : 0f;
        var top = bottom - statHeight - Metrics.Space.Xl * scale - lines.Length * headlineLine - winnerHeight -
                  eyebrowGap - labelLine;
        var left = content.Min.X + gutter;
        var step = 0;
        var mark = PrismMotion.Mark(drawList);
        PrismType.Draw(drawList, new Vector2(left, top), Loc.T(Eyebrow(week.Phase)), PrismArt.Muted,
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

        if (winner.Length > 0)
        {
            mark = PrismMotion.Mark(drawList);
            PrismType.Draw(drawList, new Vector2(left, top + eyebrowGap), winner, PrismArt.Fore, PrismFace.Label,
                labelPixels);
            RiseStep(drawList, mark, step++, scale);
        }

        mark = PrismMotion.Mark(drawList);
        var statTop = bottom - statHeight;
        var column = (width - Metrics.Space.Lg * scale) * 0.5f;
        var totalLeft = left;
        var deadline = DeadlineValue(week);
        if (deadline.Length > 0)
        {
            DrawStat(drawList, new Vector2(left, statTop), column, Loc.T(DeadlineLabel(week.Phase)), deadline, scale);
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
        PrismType.Draw(drawList, new Vector2(topLeft.X, top), PrismType.FitLabel(label, labelPixels, width),
            PrismArt.Muted, PrismFace.Label, labelPixels);
        var pixels = PrismType.Fit(value, PrismFace.Headline, width, StatUnits * scale,
            StatUnits * scale * StatMinShare, 1, out var lines);
        if (lines.Length > 0)
        {
            PrismType.Draw(drawList, new Vector2(topLeft.X, top + PrismType.LineHeight(PrismFace.Label, labelPixels)),
                lines[0], PrismArt.Fore, PrismFace.Headline, pixels);
        }
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
            var slot = HasPrimary(week) ? new Rect(new Vector2(row.Max.X - half, row.Min.Y), row.Max) : row;
            if (PrismArt.Strip(drawList, slot, Loc.T(ResultsLabel(results)), Arrow, !HasPrimary(week)))
            {
                OpenResults();
            }
        }

        if (week.Phase == PrismPhases.Entering)
        {
            if (week.MyEntry is not null)
            {
                if (PrismArt.Strip(drawList, strip, Loc.T(L.Prism.YourLook), Arrow, true))
                {
                    Go(PrismScreen.Entry);
                }

                return;
            }

            if (PrismArt.Strip(drawList, strip, EnterLabel(), Arrow, true, !store.Busy))
            {
                StartEntry();
            }

            return;
        }

        if (!PrismPhases.Judgeable(week.Phase))
        {
            return;
        }

        var label = week.Phase == PrismPhases.Final ? L.Prism.JudgeFinal : L.Prism.JudgeHeat;
        var right = store.Results is not null ? Arrow
            : HeatInProgress ? LookCounter(lookIndex + 1, stars.Length) : JudgedLine(week.BallotsCast);
        if (PrismArt.Strip(drawList, strip, Loc.T(label), right, true, !store.Busy))
        {
            if (HasTracks && !HeatInProgress)
            {
                StartTrackChoice(true);
            }
            else
            {
                StartJudging();
            }
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
        if (PrismArt.Strip(drawList, StripRect(content, scale), Loc.T(L.Common.Retry), Arrow, true))
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
        PrismPhases.Results => L.Prism.NextTheme,
        _ => L.Prism.ResultsAt,
    };

    private static LocString Eyebrow(string phase) => phase switch
    {
        PrismPhases.Results => L.Prism.ResultsOut,
        PrismPhases.Tallying => L.Prism.JudgingClosed,
        _ => L.Prism.ThemeLabel,
    };

    private LocString ResultsLabel(PrismResultsDto results) =>
        Revealed(results) ? L.Prism.LastResults : L.Prism.ResultsIn;

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

    private string IssueLine(int issue)
    {
        if (issue <= 0)
        {
            return string.Empty;
        }

        var track = HasTracks ? store.Track : string.Empty;
        var key = HashCode.Combine(issue, track, Loc.Current.Code);
        if (issueText.IsCurrent(key))
        {
            return issueText.Value;
        }

        var line = Loc.T(L.Prism.Issue, issue);
        return issueText.Store(key, track.Length > 0 ? Loc.T(L.Prism.WithTrack, line, PrismTracks.Name(track)) : line);
    }

    private string CreditLine(PrismPlacingDto cover)
    {
        var key = HashCode.Combine(cover.DisplayName, Loc.Current.Code);
        return creditText.IsCurrent(key)
            ? creditText.Value
            : creditText.Store(key, Loc.T(L.Prism.CoverCredit, cover.DisplayName));
    }

    private string WonLine(PrismPlacingDto winner)
    {
        var key = HashCode.Combine(winner.DisplayName, Loc.Current.Code);
        return wonText.IsCurrent(key)
            ? wonText.Value
            : wonText.Store(key, Loc.T(L.Prism.WonBy, winner.DisplayName));
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

    private static LocString StatusName(string status) => status switch
    {
        PrismPhases.StatusRejected => L.Prism.StatusRejected,
        PrismPhases.StatusMoved => L.Prism.StatusMoved,
        _ => L.Prism.StatusLive,
    };
}
