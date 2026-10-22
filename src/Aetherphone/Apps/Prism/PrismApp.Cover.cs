using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const float MastheadUnits = PrismType.HeadingUnits;
    private const float StatUnits = PrismType.HeadingUnits;
    private const float StatMinShare = 0.6f;
    private const float GlowLeadSeconds = 0.85f;
    private const float RuleDelay = 0.05f;
    private const float CreditsDelay = 0.12f;
    private const float IntroSeconds = 0.9f;
    private const float CoverRatio = 4f / 3f;
    private const float CreditGlyphShare = 0.8f;
    private const float TrackLabelShare = 0.88f;
    private const string CoverPrimaryId = "prism.cover.primary";

    private static readonly string RewardsGlyph = IconGlyph.Of(FontAwesomeIcon.Gift);

    private CachedText deadlineText;
    private CachedText judgedText;
    private CachedText issueText;
    private CachedText coverByText;
    private CachedText countText;
    private CachedText factText;
    private CachedText clockText;
    private string[] trackLabels = Array.Empty<string>();
    private string coverShown = string.Empty;
    private string coverLeaving = string.Empty;
    private PrismPlacingDto? coverCredit;
    private Spring coverFade = new(1f);
    private float introDelay;
    private bool mastheadDot;
    private double dotGlowAt = double.NegativeInfinity;

    private void DrawCover(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        if (!store.IsSignedIn)
        {
            DrawCoverNotice(drawList, content, Loc.T(L.Prism.SignInTitle), Loc.T(L.Prism.SignInRequired), scale);
            if (DrawButton(drawList, ActionRow(content, scale), Loc.T(L.Prism.OpenSettings), ButtonStyle.Solid, scale))
            {
                navigation.Open("settings");
            }

            return;
        }

        if (store.Week is not { } week)
        {
            DrawWeekMissing(drawList, content, scale);
            return;
        }

        var open = week.Id.Length > 0 && week.Phase.Length > 0;
        var cover = CoverPlacing(store.Results);
        var url = cover?.ImageUrl ?? week.MyEntry?.ImageUrl ?? string.Empty;
        onPhoto = url.Length > 0 || coverShown.Length > 0;
        PrismArt.OverPhoto(onPhoto);
        var gap = Metrics.Space.Lg * scale;
        var tap = Metrics.Size.TapTarget * scale;
        var strip = open && HasTracks ? tap + gap : 0f;
        var hint = open ? string.Empty : Loc.T(L.Prism.NoWeekHint);
        var fact = open
            ? Typography.LineHeight(PrismType.Caption) + Typography.LineHeight(PrismType.Title)
            : Typography.MeasureWrappedBlock(hint, PrismType.Body, content.Width).Y;
        var top = PhotoTop(full, content);
        var bottom = content.Max.Y - bottomChrome - PrismArt.BottomMargin(scale);
        var height = MathF.Min(full.Width * CoverRatio,
            bottom - top - gap - strip - fact - gap - Button.LargeHeight * scale);
        var photo = new Rect(new Vector2(full.Min.X, top), new Vector2(full.Max.X, top + height));
        DrawCoverPhoto(drawList, photo, url, cover, scale);
        DrawCoverTitle(drawList, photo, content, open ? week.Title : Loc.T(L.Prism.NoWeekTitle), open,
            coverCredit, week.PreviousWeekId.Length > 0, scale);
        var inset = PhotoInset(full, content);
        var tips = PhotoTips(photo, inset, scale);
        if (DrawPhotoButton(drawList, photo, inset, 1, PhoneIcons.HelpCircle,
                tips ? Loc.T(L.Prism.HowItWorks) : string.Empty, scale))
        {
            StartGuide(tab);
        }

        if (DrawPhotoButton(drawList, photo, inset, 2, RewardsGlyph, tips ? Loc.T(L.Prism.Rewards) : string.Empty,
                scale))
        {
            OpenRewards();
        }

        var y = photo.Max.Y + gap;
        if (strip > 0f)
        {
            DrawTrackStrip(new Rect(new Vector2(content.Min.X, y), new Vector2(content.Max.X, y + tap)));
            y += strip;
        }

        if (open)
        {
            DrawFact(drawList, new Vector2(content.Min.X, y), content.Width, FactCaption(week), FactValue(week));
        }
        else
        {
            Typography.DrawWrappedLeft(new Vector2(content.Min.X, y), hint, PrismArt.Muted, PrismType.Body,
                content.Width);
        }

        y += fact + gap;
        DrawCoverActions(drawList,
            new Rect(new Vector2(content.Min.X, y), new Vector2(content.Max.X, y + Button.LargeHeight * scale)), week,
            open, scale);
    }

    private static bool PhotoTips(Rect photo, float inset, float scale) =>
        UiInteract.HoverWindowOnly(photo.Min,
            new Vector2(photo.Max.X, photo.Min.Y + inset + (Metrics.Size.TapTarget + Metrics.Space.Md) * scale));

    private void DrawCoverPhoto(ImDrawListPtr drawList, Rect photo, string url, PrismPlacingDto? credit, float scale)
    {
        if (url == coverShown)
        {
            coverCredit = credit;
        }
        else if (coverShown.Length == 0 || url.Length == 0 || images.Get(url) is not null || images.Failed(url))
        {
            coverLeaving = coverShown;
            coverShown = url;
            coverCredit = credit;
            coverFade = new Spring(coverLeaving.Length > 0 ? 0f : 1f);
        }

        var fade = coverFade.Step(1f, Motion.Sheet,
            MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds));
        if (coverLeaving.Length > 0 && fade < PrismMotion.Settled)
        {
            DrawLook(drawList, photo, coverLeaving, false, native: true);
        }
        else
        {
            coverLeaving = string.Empty;
        }

        if (coverShown.Length == 0)
        {
            PrismArt.Placeholder(drawList, photo, false, scale);
        }
        else
        {
            DrawLook(drawList, photo, coverShown, false, coverLeaving.Length > 0 ? fade : 1f, native: true);
        }

        if (!PrismArt.Light)
        {
            PrismArt.TopFog(drawList, photo, scale);
        }
    }

    private void DrawCoverTitle(ImDrawListPtr drawList, Rect photo, Rect content, string title, bool open,
        PrismPlacingDto? credit, bool creditSlot, float scale)
    {
        var line = Typography.LineHeight(PrismType.Caption);
        var captionHeight = open ? line + Metrics.Space.Xxs * scale : 0f;
        var creditHeight = credit is not null || creditSlot ? Metrics.Space.Xs * scale + line : 0f;
        var textHeight = captionHeight + PrismType.HeroHeight(title, content.Width) + creditHeight;
        if (onPhoto)
        {
            PrismArt.Scrim(drawList, photo, textHeight, scale);
        }

        var ink = onPhoto ? PrismArt.PhotoFore : PrismArt.Fore;
        var muted = onPhoto ? PrismArt.PhotoMuted : PrismArt.Muted;
        var left = content.Min.X;
        var y = photo.Max.Y - Metrics.Space.Lg * scale - textHeight;
        if (open)
        {
            Typography.Draw(drawList, new Vector2(left, y), Loc.T(L.Prism.ThemeLabel), muted, PrismType.Caption);
        }

        y += captionHeight;
        y += PrismType.DrawHero(drawList, new Vector2(left, y), title, content.Width, ink);
        if (credit is not null)
        {
            DrawGlyphCaption(drawList, new Vector2(left, y + Metrics.Space.Xs * scale), content.Max.X,
                PhoneIcons.Photo, CoverByLine(credit), muted, credit, scale);
        }
    }

    private void DrawGlyphCaption(ImDrawListPtr drawList, Vector2 topLeft, float right, string glyph, string text,
        Vector4 ink, PrismPlacingDto placing, float scale)
    {
        var line = Typography.LineHeight(PrismType.Caption);
        PhoneIcon.Draw(drawList, topLeft + new Vector2(line * 0.5f, line * 0.5f), glyph, ink, line * CreditGlyphShare);
        var left = topLeft.X + line + Metrics.Space.Xs * scale;
        var fitted = Typography.FitText(text, MathF.Max(1f, right - left), PrismType.Caption);
        Typography.Draw(drawList, new Vector2(left, topLeft.Y), fitted, ink, PrismType.Caption);
        TapForOutfit(new Vector2(topLeft.X, topLeft.Y + line - Metrics.Size.TapTarget * scale),
            new Vector2(left + Typography.Measure(fitted, PrismType.Caption).X, topLeft.Y + line), placing);
    }

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
                new Vector2(content.Max.X - gutter - PrismArt.HeaderIconUnits * scale * 0.5f,
                    (top + nameBottom) * 0.5f), icon, dot, scale);

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

    private void DrawTrackStrip(Rect row)
    {
        var tracks = store.Tracks;
        var count = Math.Min(tracks.Length, MaxTracks);
        if (trackLabels.Length != count)
        {
            trackLabels = new string[count];
        }

        var selected = 0;
        for (var index = 0; index < count; index++)
        {
            trackLabels[index] = TrackTab(index, tracks[index]);
            if (tracks[index].Id == store.Track)
            {
                selected = index;
            }
        }

        var track = Surfaces.Fill(PrismArt.Fore, FillLevel.Tertiary);
        var under = Vector4.Lerp(followedTheme!.AppBackground, PrismArt.Fore, track.W);
        var thumb = Vector4.Lerp(under, PrismArt.Fore, Surfaces.Fill(PrismArt.Fore, FillLevel.Primary).W)
            with { W = 1f };
        var picked = SegmentStrip.Draw("prism.tracks", row, trackLabels, selected, track, thumb, PrismArt.Muted,
            PrismArt.Fore, Metrics.Size.TapTarget, PrismType.Headline.Scale * TrackLabelShare);
        if (picked != selected)
        {
            store.SelectTrack(tracks[picked].Id);
        }
    }

    private static void DrawFact(ImDrawListPtr drawList, Vector2 topLeft, float width, string caption, string value)
    {
        Typography.Draw(drawList, topLeft, Typography.FitText(caption, width, PrismType.Caption), PrismArt.Muted,
            PrismType.Caption);
        Typography.Draw(drawList, topLeft + new Vector2(0f, Typography.LineHeight(PrismType.Caption)),
            Typography.FitText(value, width, PrismType.Title), PrismArt.Fore, PrismType.Title);
    }

    private string EnterLabel() => Loc.T(store.Busy ? L.Prism.Uploading : L.Prism.EnterThisWeek);

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

    private string FactCaption(PrismWeekDto week)
    {
        if (week.Phase == PrismPhases.Entering && week.MyEntry is not null)
        {
            return Loc.T(L.Prism.YourLook);
        }

        if (week.Phase == PrismPhases.Results)
        {
            var next = DeadlineValue(week);
            return next.Length > 0 ? FactLine(Loc.T(L.Prism.NextTheme), next) : string.Empty;
        }

        var label = Loc.T(DeadlineLabel(week.Phase));
        return PrismPhases.Judgeable(week.Phase) && week.BallotsCast > 0
            ? FactLine(label, JudgedLine(week.BallotsCast))
            : label;
    }

    private string FactValue(PrismWeekDto week) => week.Phase switch
    {
        PrismPhases.Entering when week.MyEntry is { } entry => Loc.T(StatusName(entry.Status)),
        PrismPhases.Results => Loc.T(L.Prism.ResultsOut),
        PrismPhases.Tallying => ResultsClock(week.ResultsAtUnix),
        _ => DeadlineValue(week),
    };

    private string FactLine(string label, string detail)
    {
        var key = HashCode.Combine(label, detail);
        return factText.IsCurrent(key)
            ? factText.Value
            : factText.Store(key, Loc.T(L.Prism.WithTrack, label, detail));
    }

    private string ResultsClock(long unix)
    {
        if (unix <= 0)
        {
            return string.Empty;
        }

        var key = HashCode.Combine(unix, Loc.Current.Code);
        return clockText.IsCurrent(key) ? clockText.Value : clockText.Store(key, TimeText.Clock(unix));
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

    private void DrawCoverActions(ImDrawListPtr drawList, Rect row, PrismWeekDto week, bool open, float scale)
    {
        var lastWeek = Loc.T(L.Prism.LastWeek);
        var primary = open ? PrimaryLabel(week) : string.Empty;
        var secondary = open
            ? week.Phase != PrismPhases.Results && week.PreviousWeekId.Length > 0
            : store.Results is not null;
        var gap = Metrics.Space.Sm * scale;
        var secondaryWidth = secondary
            ? ButtonWidth([lastWeek], primary.Length > 0 ? (row.Width - gap) * 0.5f : row.Width, scale)
            : 0f;
        if (secondary)
        {
            var left = primary.Length > 0 ? row.Max.X - secondaryWidth : row.Min.X;
            if (DrawButton(drawList,
                    new Rect(new Vector2(left, row.Min.Y), new Vector2(left + secondaryWidth, row.Max.Y)), lastWeek,
                    ButtonStyle.Tinted, scale, enabled: ShowingResults()))
            {
                OpenResults();
            }
        }

        if (primary.Length == 0)
        {
            return;
        }

        var right = secondary ? row.Max.X - secondaryWidth - gap : row.Max.X;
        var enabled = week.Phase == PrismPhases.Results ? ShowingResults() : !store.Busy;
        if (!DrawButton(drawList, new Rect(row.Min, new Vector2(right, row.Max.Y)), primary, ButtonStyle.Solid, scale,
                enabled: enabled, id: CoverPrimaryId))
        {
            return;
        }

        if (week.Phase == PrismPhases.Results)
        {
            OpenResults();
        }
        else if (week.Phase == PrismPhases.Entering && week.MyEntry is not null)
        {
            Go(PrismScreen.Entry);
        }
        else if (week.Phase == PrismPhases.Entering)
        {
            StartEntry();
        }
        else if (HasTracks && !HeatInProgress)
        {
            StartTrackChoice(true);
        }
        else
        {
            StartJudging();
        }
    }

    private string PrimaryLabel(PrismWeekDto week) => week.Phase switch
    {
        PrismPhases.Entering when week.MyEntry is not null => Loc.T(L.Prism.SeeYourGlam),
        PrismPhases.Entering => EnterLabel(),
        PrismPhases.Judging => Loc.T(L.Prism.JudgeHeat),
        PrismPhases.Final => Loc.T(L.Prism.JudgeFinal),
        PrismPhases.Results => Loc.T(L.Prism.SeeResults),
        _ => string.Empty,
    };

    private bool ShowingResults() => store.Results is { } results && PrismStore.ShowsTrack(results, store.Track);

    private Rect ActionRow(Rect content, float scale)
    {
        var bottom = content.Max.Y - bottomChrome - PrismArt.BottomMargin(scale);
        return new Rect(new Vector2(content.Min.X, bottom - Button.LargeHeight * scale),
            new Vector2(content.Max.X, bottom));
    }

    private void DrawCoverNotice(ImDrawListPtr drawList, Rect content, string title, string body, float scale)
    {
        var width = content.Width;
        var bottom = ActionRow(content, scale).Min.Y - Metrics.Space.Xxl * scale;
        var top = bottom - Typography.MeasureWrappedBlock(body, PrismType.Body, width).Y - Metrics.Space.Sm * scale -
                  PrismType.HeroHeight(title, width);
        top += PrismType.DrawHero(drawList, new Vector2(content.Min.X, top), title, width, PrismArt.Fore) +
               Metrics.Space.Sm * scale;
        Typography.DrawWrappedLeft(new Vector2(content.Min.X, top), body, PrismArt.Muted, PrismType.Body, width);
    }

    private void DrawWeekMissing(ImDrawListPtr drawList, Rect content, float scale)
    {
        if (!store.WeekFailed || store.Loading)
        {
            var loading = Loc.T(L.Common.Loading);
            Typography.Draw(drawList, content.Center - Typography.Measure(loading, PrismType.Caption) * 0.5f, loading,
                PrismArt.Muted, PrismType.Caption);
            return;
        }

        var reason = store.WeekFailureText;
        DrawCoverNotice(drawList, content, Loc.T(L.Common.LoadFailed),
            reason.Length > 0 ? reason : Loc.T(L.Common.LoadFailedHint), scale);
        if (DrawButton(drawList, ActionRow(content, scale), Loc.T(L.Common.Retry), ButtonStyle.Solid, scale))
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

    private string CoverByLine(PrismPlacingDto cover)
    {
        var key = HashCode.Combine(cover.Handle, cover.DisplayName, Loc.Current.Code);
        return coverByText.IsCurrent(key)
            ? coverByText.Value
            : coverByText.Store(key, Loc.T(L.Prism.CoverBy, AtHandle(cover.Handle, cover.DisplayName)));
    }

    private static string AtHandle(string handle, string displayName) =>
        handle.Length > 0 ? string.Concat("@", handle) : displayName;

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

    private static LocString StatusName(string status) => status switch
    {
        PrismPhases.StatusRejected => L.Prism.StatusRejected,
        PrismPhases.StatusMoved => L.Prism.StatusMoved,
        _ => L.Prism.StatusLive,
    };
}
