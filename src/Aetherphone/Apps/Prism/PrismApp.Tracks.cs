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
    private const int MaxTracks = 3;
    private const float BackdropMarkUnits = 84f;
    private const float BackdropMarkShare = 0.3f;
    private const int PageTileUnitsShown = 3;
    private const float PageTileWidthUnits = 56f;
    private const float PageTileHeightUnits = 44f;

    private static readonly string ResultsGlyph = IconGlyph.Of(FontAwesomeIcon.Trophy);
    private static readonly string GalleryGlyph = IconGlyph.Of(FontAwesomeIcon.Th);

    private readonly CachedText[] trackActionTexts = new CachedText[MaxTracks];
    private readonly CachedText[] trackCreditTexts = new CachedText[MaxTracks];
    private readonly CachedText[] trackEntryTexts = new CachedText[MaxTracks];
    private readonly Pager trackPager = new(Motion.PageSettle);
    private readonly PrismCountdown pageCountdown = new();
    private CachedText phaseText;
    private CachedText phaseCaptionText;
    private PrismWeekDto? lastWeekAsked;
    private int trackPage = -1;
    private Vector2 trackPress;
    private bool trackPressed;

    private bool HasTracks => store.Tracks.Length > 1;

    private bool VanillaEntry => HasTracks && store.Track == PrismTracks.Vanilla;

    private void DrawContest(ImDrawListPtr drawList, Rect full, Rect content, PrismWeekDto week, float scale)
    {
        if (!ReferenceEquals(week, lastWeekAsked))
        {
            lastWeekAsked = week;
            store.LoadLastWeekResults();
        }

        var open = week.Id.Length > 0 && week.Phase.Length > 0;
        var tracks = store.Tracks;
        var count = open && tracks.Length > 1 ? Math.Min(tracks.Length, MaxTracks) : 1;
        var area = new Rect(new Vector2(full.Min.X, PhotoTop(full, content)), full.Max);
        var dots = content.Max.Y - bottomChrome;
        var bottom = dots - (count > 1 ? Metrics.Size.TapTarget : Metrics.Space.Lg) * scale;
        DrivePager(trackPager, area, count, ref trackPressed, ref trackPress, scale);
        if (count > 1)
        {
            FollowTrackPage(tracks, count);
        }

        var scroll = trackPager.Value;
        var first = Math.Clamp((int)MathF.Floor(scroll), 0, count - 1);
        var last = Math.Clamp((int)MathF.Ceiling(scroll), 0, count - 1);
        var page = Math.Clamp((int)MathF.Round(scroll), 0, count - 1);
        drawList.PushClipRect(area.Min, area.Max, true);
        for (var index = first; index <= last; index++)
        {
            var offset = new Vector2((index - scroll) * full.Width, 0f);
            DrawContestPage(drawList, new Rect(area.Min + offset, area.Max + offset), content.Min.X + offset.X,
                content.Width, bottom, index, count > 1 ? tracks[index] : null, week, open, scale);
        }

        drawList.PopClipRect();
        for (var index = 0; index < count; index++)
        {
            onPhoto |= PagePhoto(count > 1 ? tracks[index] : null, week, open, out _, out _).Length > 0;
        }

        PrismArt.OverPhoto(PagePhoto(count > 1 ? tracks[page] : null, week, open, out _, out _).Length > 0);
        if (!PrismArt.Light)
        {
            PrismArt.TopFog(drawList, area, scale);
        }

        if (count > 1)
        {
            DrawPagerDots(drawList, trackPager, new Rect(area.Min, new Vector2(area.Max.X, dots)), count, page,
                PrismArt.Fore, scale);
        }

        DrawContestButtons(drawList, area, PhotoInset(full, content), week, open, scale);
    }

    private void FollowTrackPage(PrismTrackDto[] tracks, int count)
    {
        if (trackPager.Dragging)
        {
            return;
        }

        var page = Math.Clamp((int)MathF.Round(trackPager.Value), 0, count - 1);
        if (trackPage >= 0 && page != trackPage)
        {
            trackPage = page;
            store.SelectTrack(tracks[page].Id);
            return;
        }

        var selected = 0;
        for (var index = 0; index < count; index++)
        {
            if (tracks[index].Id == store.Track)
            {
                selected = index;
            }
        }

        if (selected != trackPage)
        {
            trackPage = selected;
            trackPager.SnapTo(selected, count);
        }
    }

    private string PagePhoto(PrismTrackDto? track, PrismWeekDto week, bool open, out PrismResultsDto? results,
        out PrismPlacingDto? winner)
    {
        results = track is null || track.Id == store.Track ? ShownResults() : store.LastWeekResults(track.Id);
        winner = CoverPlacing(results) is { ImageUrl.Length: > 0 } placing ? placing : null;
        return winner?.ImageUrl ?? PageEntry(track, week, open)?.ImageUrl ?? string.Empty;
    }

    private static PrismEntryDto? PageEntry(PrismTrackDto? track, PrismWeekDto week, bool open) =>
        !open ? null : track is null ? week.MyEntry : track.MyEntry;

    private void DrawContestPage(ImDrawListPtr drawList, Rect page, float left, float width, float bottom, int index,
        PrismTrackDto? track, PrismWeekDto week, bool open, float scale)
    {
        var url = PagePhoto(track, week, open, out var results, out var winner);
        var photo = url.Length > 0;
        if (photo)
        {
            DrawLook(drawList, page, url, false, native: true);
        }
        else
        {
            DrawContestBackdrop(drawList, page, week.Id, scale);
        }

        var entry = PageEntry(track, week, open);
        var tap = Metrics.Size.TapTarget * scale;
        var line = Typography.LineHeight(PrismType.Caption);
        var rowGap = Metrics.Space.Xxs * scale;
        var title = open ? week.Title : Loc.T(L.Prism.NoWeekTitle);
        var action = open ? ContestAction(index, track, week.Phase, entry is not null) : string.Empty;
        var phase = open ? PhaseLine(week) : string.Empty;
        var mine = entry is not null ? EntryLine(index, entry) : string.Empty;
        var lastWeek = results?.WeekId == week.PreviousWeekId;
        var credit = winner is not null ? CreditLine(index, winner, lastWeek) : string.Empty;
        var copy = !open ? Loc.T(L.Prism.NoWeekHint) : track is null ? string.Empty : TrackRule(track.Id);
        var copyWidth = width - tap - Metrics.Space.Md * scale;
        var titleHeight = PrismType.HeroHeight(title, copyWidth);
        var labelHeight = track is not null ? line + Metrics.Space.Xs * scale : 0f;
        var phaseHeight = phase.Length > 0 ? Metrics.Space.Xs * scale + PhaseLineHeight(week, scale) : 0f;
        var copyGap = phase.Length > 0 ? rowGap : Metrics.Space.Xs * scale;
        var copyHeight = copy.Length > 0
            ? copyGap + Typography.MeasureWrappedBlock(copy, PrismType.Body, copyWidth).Y
            : 0f;
        var rows = (mine.Length > 0 ? 1 : 0) + (credit.Length > 0 ? 1 : 0);
        var rowsHeight = rows > 0 ? Metrics.Space.Md * scale + rows * line + (rows - 1) * rowGap : 0f;
        var y = bottom - labelHeight - titleHeight - phaseHeight - copyHeight - rowsHeight;
        if (photo)
        {
            PrismArt.Scrim(drawList, page, page.Max.Y - y + Metrics.Space.Xl * scale, scale);
        }

        PrismArt.OverPhoto(photo);
        if (track is not null)
        {
            var glyph = PrismArt.InlineIconUnits * scale;
            PrismArt.FactIcon(drawList, new Vector2(left + glyph * 0.5f, y + line * 0.5f), glyph,
                track.Id == PrismTracks.Vanilla ? PrismFact.Vanilla : PrismFact.Modded, PrismArt.Fore);
            var labelLeft = left + glyph + Metrics.Space.Xs * scale;
            Typography.Draw(drawList, new Vector2(labelLeft, y),
                Typography.FitText(PrismTracks.Name(track.Id), MathF.Max(1f, copyWidth - labelLeft + left),
                    PrismType.Caption), PrismArt.Fore, PrismType.Caption);
            y += labelHeight;
        }

        PrismType.DrawHero(drawList, new Vector2(left, y), title, copyWidth, PrismArt.Fore);
        if (action.Length > 0)
        {
            DrawContestAction(drawList, new Vector2(left + width - tap * 0.5f, y + titleHeight * 0.5f), action, track,
                week.Phase, entry, photo, scale);
        }

        y += titleHeight;
        if (phase.Length > 0)
        {
            y = DrawPhaseLine(drawList, new Vector2(left, y + Metrics.Space.Xs * scale), copyWidth, week, phase, scale);
        }

        if (copy.Length > 0)
        {
            Typography.DrawWrappedLeft(new Vector2(left, y + copyGap), copy, PrismArt.Muted, PrismType.Body,
                copyWidth);
            y += copyHeight;
        }

        y += rows > 0 ? Metrics.Space.Md * scale : 0f;
        if (mine.Length > 0)
        {
            var end = DrawGlyphCaption(drawList, new Vector2(left, y), left + copyWidth, StatusGlyph(entry!.Status),
                mine, PrismArt.Muted, scale);
            if (UiInteract.HoverClick(new Vector2(left, y + line - tap), new Vector2(end, y + line)))
            {
                SelectPage(track);
                Go(PrismScreen.Entry);
            }

            y += line + rowGap;
        }

        if (credit.Length > 0)
        {
            var end = DrawGlyphCaption(drawList, new Vector2(left, y), left + copyWidth,
                lastWeek ? PhoneIcons.Crown : PhoneIcons.Photo, credit, PrismArt.Muted, scale);
            TapForOutfit(new Vector2(left, y + line - tap), new Vector2(end, y + line), winner!);
        }

        PrismArt.OverPhoto(false);
    }

    private static void DrawContestBackdrop(ImDrawListPtr drawList, Rect page, string weekId, float scale) =>
        PrismLight.Week(drawList, page, weekId, PrismArt.Page, PrismArt.Fore, BackdropMarkUnits * scale,
            BackdropMarkShare);

    private static float PhaseLineHeight(PrismWeekDto week, float scale) =>
        PrismCountdown.Remaining(PrismCountdown.Deadline(week)) > 0
            ? Typography.LineHeight(PrismType.Caption) + Metrics.Space.Xs * scale + PageTileHeightUnits * scale
            : Typography.LineHeight(PrismType.Body);

    private float DrawPhaseLine(ImDrawListPtr drawList, Vector2 topLeft, float width, PrismWeekDto week,
        string phase, float scale)
    {
        var deadline = PrismCountdown.Deadline(week);
        if (PrismCountdown.Remaining(deadline) <= 0)
        {
            Typography.Draw(drawList, topLeft, Typography.FitText(phase, width, PrismType.Body), PrismArt.Muted,
                PrismType.Body);
            return topLeft.Y + Typography.LineHeight(PrismType.Body);
        }

        Typography.Draw(drawList, topLeft, Typography.FitText(PhaseCaption(week), width, PrismType.Caption),
            PrismArt.Muted, PrismType.Caption);
        var top = topLeft.Y + Typography.LineHeight(PrismType.Caption) + Metrics.Space.Xs * scale;
        var tilesWidth = MathF.Min(width,
            PageTileUnitsShown * PageTileWidthUnits * scale + (PageTileUnitsShown - 1) * Metrics.Space.Xs * scale);
        var tiles = new Rect(new Vector2(topLeft.X, top),
            new Vector2(topLeft.X + tilesWidth, top + PageTileHeightUnits * scale));
        pageCountdown.DrawCompact(drawList, tiles, deadline, scale);
        return tiles.Max.Y;
    }

    private string PhaseCaption(PrismWeekDto week)
    {
        var label = Loc.T(DeadlineLabel(week.Phase));
        if (!PrismPhases.Judgeable(week.Phase))
        {
            return label;
        }

        var judged = JudgedLine(week.BallotsCast);
        var key = HashCode.Combine(label, judged);
        return phaseCaptionText.IsCurrent(key)
            ? phaseCaptionText.Value
            : phaseCaptionText.Store(key, Loc.T(L.Prism.WithTrack, label, judged));
    }

    private void DrawContestAction(ImDrawListPtr drawList, Vector2 center, string label, PrismTrackDto? track,
        string phase, PrismEntryDto? entry, bool photo, float scale)
    {
        var entering = phase == PrismPhases.Entering;
        var busy = store.Busy && entering && entry is null && (track is null || track.Id == store.Track)
            ? Loc.T(L.Prism.Uploading)
            : string.Empty;
        var glyph = !entering || entry is null ? NextGlyph
            : entry.Status is PrismPhases.StatusRejected or PrismPhases.StatusMoved ? StatusGlyph(entry.Status)
            : DoneGlyph;
        var enabled = phase == PrismPhases.Results ? ShowingResults() : !store.Busy;
        if (!DrawRoundAction(drawList, center, glyph, label, enabled, busy, photo ? photoControls : controls, scale))
        {
            return;
        }

        SelectPage(track);
        if (phase == PrismPhases.Results)
        {
            OpenResults();
        }
        else if (entering && entry is not null)
        {
            Go(PrismScreen.Entry);
        }
        else if (entering)
        {
            StartPick();
        }
        else
        {
            StartJudging();
        }
    }

    private void SelectPage(PrismTrackDto? track)
    {
        if (track is not null)
        {
            store.SelectTrack(track.Id);
        }
    }

    private void DrawContestButtons(ImDrawListPtr drawList, Rect area, float inset, PrismWeekDto week, bool open,
        float scale)
    {
        if (DrawPhotoButton(drawList, area, inset, 1, PhoneIcons.HelpCircle, Loc.T(L.Prism.HowItWorks), scale))
        {
            StartGuide(tab);
        }

        if (DrawPhotoButton(drawList, area, inset, 2, RewardsGlyph, Loc.T(L.Prism.Rewards), scale))
        {
            OpenRewards();
        }

        if (ShownResults() is not { } results)
        {
            return;
        }

        if (open && week.Phase == PrismPhases.Results)
        {
            if (DrawPhotoButton(drawList, area, inset, 3, GalleryGlyph, Loc.T(L.Prism.AllGlams), scale))
            {
                OpenGallery(results.WeekId, store.Track, results.Title);
            }
        }
        else if (DrawPhotoButton(drawList, area, inset, 3, ResultsGlyph, Loc.T(L.Prism.LastWeekWinners), scale))
        {
            OpenResults();
        }
    }

    private static void DrivePager(Pager pager, Rect area, int count, ref bool pressed, ref Vector2 press,
        float scale)
    {
        var delta = ImGui.GetIO().DeltaTime;
        pager.Step(delta, count);
        var mouse = ImGui.GetMousePos();
        if (pager.Dragging)
        {
            pager.Drag(mouse.X, area.Width, count, delta);
            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                pager.Release(area.Width, count);
            }

            return;
        }

        if (UiInteract.Hover(area.Min, area.Max) && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            pressed = true;
            press = mouse;
        }

        if (!pressed || !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            pressed = false;
            return;
        }

        var move = mouse - press;
        if (MathF.Abs(move.X) <= PagerSlopUnits * scale || MathF.Abs(move.X) <= MathF.Abs(move.Y))
        {
            return;
        }

        pressed = false;
        UiInteract.CancelPendingTap();
        pager.Begin(press.X);
    }

    private static void DrawPagerDots(ImDrawListPtr drawList, Pager pager, Rect photo, int count, int page, Vector4 ink,
        float scale)
    {
        var tap = Metrics.Size.TapTarget * scale;
        var center = new Vector2(photo.Center.X, photo.Max.Y - tap * 0.5f);
        PhotoCarousel.DrawDots(drawList, center, count, page, photo.Width, ink);
        var hit = new Rect(new Vector2(center.X - tap * count * 0.5f, photo.Max.Y - tap),
            new Vector2(center.X + tap * count * 0.5f, photo.Max.Y));
        if (UiInteract.HoverClick(hit.Min, hit.Max))
        {
            pager.AnimateTo(ImGui.GetMousePos().X >= center.X ? page + 1 : page - 1, count);
        }
    }

    private static string TrackRule(string id) => id switch
    {
        PrismTracks.Vanilla => Loc.T(L.Prism.TrackVanillaRule),
        PrismTracks.Modded => Loc.T(L.Prism.TrackModdedRule),
        _ => string.Empty,
    };

    private string ContestAction(int index, PrismTrackDto? track, string phase, bool entered)
    {
        var key = HashCode.Combine(track?.Id, phase, entered, Loc.Current.Code);
        if (trackActionTexts[index].IsCurrent(key))
        {
            return trackActionTexts[index].Value;
        }

        var name = track is null ? string.Empty : PrismTracks.Name(track.Id);
        return trackActionTexts[index].Store(key, phase switch
        {
            PrismPhases.Entering when entered => Loc.T(L.Prism.SeeYourGlam),
            PrismPhases.Entering => track is null ? Loc.T(L.Prism.EnterThisWeek) : Loc.T(L.Prism.EnterTrack, name),
            PrismPhases.Judging when track is null => Loc.T(L.Prism.JudgeHeat),
            PrismPhases.Final when track is null => Loc.T(L.Prism.JudgeFinal),
            PrismPhases.Judging or PrismPhases.Final => Loc.T(L.Prism.JudgeTrack, name),
            PrismPhases.Results => Loc.T(L.Prism.SeeResults),
            _ => string.Empty,
        });
    }

    private string PhaseLine(PrismWeekDto week)
    {
        var label = Loc.T(DeadlineLabel(week.Phase));
        var value = week.Phase == PrismPhases.Tallying ? TimeText.Clock(week.ResultsAtUnix) : DeadlineValue(week);
        var judged = PrismPhases.Judgeable(week.Phase) ? JudgedLine(week.BallotsCast) : string.Empty;
        var key = HashCode.Combine(label, value, judged);
        if (phaseText.IsCurrent(key))
        {
            return phaseText.Value;
        }

        var line = value.Length > 0 ? Loc.T(L.Prism.WithTrack, label, value) : string.Empty;
        return phaseText.Store(key,
            judged.Length > 0 && line.Length > 0 ? Loc.T(L.Prism.WithTrack, line, judged) : line);
    }

    private string EntryLine(int index, PrismEntryDto entry)
    {
        var key = HashCode.Combine(entry.Status, Loc.Current.Code);
        return trackEntryTexts[index].IsCurrent(key)
            ? trackEntryTexts[index].Value
            : trackEntryTexts[index].Store(key,
                Loc.T(L.Prism.WithTrack, Loc.T(L.Prism.YourLook), Loc.T(StatusName(entry.Status))));
    }

    private string CreditLine(int index, PrismPlacingDto winner, bool lastWeek)
    {
        var key = HashCode.Combine(winner.Handle, winner.DisplayName, lastWeek, Loc.Current.Code);
        return trackCreditTexts[index].IsCurrent(key)
            ? trackCreditTexts[index].Value
            : trackCreditTexts[index].Store(key, Loc.T(lastWeek ? L.Prism.LastWeekWinner : L.Prism.CoverBy,
                AtHandle(winner.Handle, winner.DisplayName)));
    }

}
