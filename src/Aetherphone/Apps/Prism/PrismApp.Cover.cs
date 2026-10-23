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
    private const float CoverRatio = 4f / 3f;
    private const float CreditGlyphShare = 0.8f;
    private const float TrackLabelShare = 0.88f;
    private const string CoverPrimaryId = "prism.cover.primary";

    private static readonly string RewardsGlyph = IconGlyph.Of(FontAwesomeIcon.Gift);

    private CachedText deadlineText;
    private CachedText judgedText;
    private CachedText coverByText;
    private CachedText factText;
    private CachedText entryFactText;
    private CachedText clockText;
    private string[] trackLabels = Array.Empty<string>();
    private string coverShown = string.Empty;
    private string coverLeaving = string.Empty;
    private PrismPlacingDto? coverCredit;
    private Spring coverFade = new(1f);

    private void DrawCover(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        if (!store.IsSignedIn)
        {
            DrawCoverNotice(drawList, content, Loc.T(L.Prism.SignInTitle), Loc.T(L.Prism.SignInRequired), scale);
            if (DrawButton(drawList, ActionRect(content, scale), Loc.T(L.Prism.OpenSettings), ButtonStyle.Solid, scale))
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
        if (DrawPhotoButton(drawList, photo, inset, 1, PhoneIcons.HelpCircle, Loc.T(L.Prism.HowItWorks), scale))
        {
            StartGuide(tab);
        }

        if (DrawPhotoButton(drawList, photo, inset, 2, RewardsGlyph, Loc.T(L.Prism.Rewards), scale))
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
            var room = content.Width;
            if (EnteredJudging(week))
            {
                room -= FactLink(drawList, new Rect(new Vector2(content.Min.X, y), new Vector2(content.Max.X, y + fact)),
                    scale);
            }

            DrawFact(drawList, new Vector2(content.Min.X, y), room, FactCaption(week), FactValue(week));
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

    private static bool EnteredJudging(PrismWeekDto week) =>
        PrismPhases.Judgeable(week.Phase) && week.MyEntry is not null;

    private float FactLink(ImDrawListPtr drawList, Rect row, float scale)
    {
        var glyph = PrismArt.IconUnits * scale;
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            drawList.AddRectFilled(row.Min, row.Max, ImGui.GetColorU32(Surfaces.Fill(controls, FillLevel.Quaternary)),
                Metrics.Radius.Md * scale);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        PhoneIcon.Draw(drawList, new Vector2(row.Max.X - glyph * 0.5f, row.Center.Y), PhoneIcons.ChevronRight,
            PrismArt.Muted, glyph);
        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            Go(PrismScreen.Entry);
        }

        return glyph + Metrics.Space.Sm * scale;
    }

    private string FactCaption(PrismWeekDto week)
    {
        if (EnteredJudging(week))
        {
            return FactLine(ref entryFactText, Loc.T(L.Prism.YourLook), Loc.T(StatusName(week.MyEntry!.Status)));
        }

        if (week.Phase == PrismPhases.Entering && week.MyEntry is not null)
        {
            return Loc.T(L.Prism.YourLook);
        }

        if (week.Phase == PrismPhases.Results)
        {
            var next = DeadlineValue(week);
            return next.Length > 0 ? FactLine(ref factText, Loc.T(L.Prism.NextTheme), next) : string.Empty;
        }

        var label = Loc.T(DeadlineLabel(week.Phase));
        return PrismPhases.Judgeable(week.Phase) && week.BallotsCast > 0
            ? FactLine(ref factText, label, JudgedLine(week.BallotsCast))
            : label;
    }

    private string FactValue(PrismWeekDto week) => week.Phase switch
    {
        PrismPhases.Entering when week.MyEntry is { } entry => Loc.T(StatusName(entry.Status)),
        PrismPhases.Results => Loc.T(L.Prism.ResultsOut),
        PrismPhases.Tallying => ResultsClock(week.ResultsAtUnix),
        _ when EnteredJudging(week) => FactLine(ref factText, Loc.T(DeadlineLabel(week.Phase)), DeadlineValue(week)),
        _ => DeadlineValue(week),
    };

    private static string FactLine(ref CachedText cache, string label, string detail)
    {
        var key = HashCode.Combine(label, detail);
        return cache.IsCurrent(key) ? cache.Value : cache.Store(key, Loc.T(L.Prism.WithTrack, label, detail));
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

    private void DrawCoverNotice(ImDrawListPtr drawList, Rect content, string title, string body, float scale)
    {
        var width = content.Width;
        var bottom = ActionRect(content, scale).Min.Y - Metrics.Space.Xxl * scale;
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
        if (DrawButton(drawList, ActionRect(content, scale), Loc.T(L.Common.Retry), ButtonStyle.Solid, scale))
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

    private string CoverByLine(PrismPlacingDto cover)
    {
        var key = HashCode.Combine(cover.Handle, cover.DisplayName, Loc.Current.Code);
        return coverByText.IsCurrent(key)
            ? coverByText.Value
            : coverByText.Store(key, Loc.T(L.Prism.CoverBy, AtHandle(cover.Handle, cover.DisplayName)));
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
