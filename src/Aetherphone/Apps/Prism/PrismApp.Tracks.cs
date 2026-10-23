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
    private const int MaxTracks = 3;

    private readonly CachedText[] trackTabTexts = new CachedText[MaxTracks];
    private readonly CachedText[] trackActionTexts = new CachedText[MaxTracks];
    private readonly CachedText[] trackWinnerTexts = new CachedText[MaxTracks];
    private readonly Pager trackPager = new(Motion.PageSettle);
    private CachedText trackCaptionText;
    private Vector2 trackPress;
    private bool trackPressed;
    private bool trackJudging;

    private bool HasTracks => store.Tracks.Length > 1;

    private bool VanillaEntry => HasTracks && store.Track == PrismTracks.Vanilla;

    private void StartTrackChoice(bool judging)
    {
        trackJudging = judging;
        store.LoadLastWeekResults();
        var tracks = store.Tracks;
        for (var index = 0; index < tracks.Length; index++)
        {
            if (tracks[index].Id == store.Track)
            {
                trackPager.SnapTo(index, Math.Min(tracks.Length, MaxTracks));
            }
        }

        Go(PrismScreen.Track);
    }

    private void ChooseTrack(string id)
    {
        store.SelectTrack(id);
        if (trackJudging)
        {
            StartJudging();
        }
        else if (store.Week?.MyEntry is not null)
        {
            Go(PrismScreen.Entry);
        }
        else
        {
            StartPick();
        }
    }

    private void DrawTrackChoice(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        var tracks = store.Tracks;
        if (tracks.Length < 2 || store.Week is not { } week)
        {
            Go(PrismScreen.Cover, -1);
            return;
        }

        var count = Math.Min(tracks.Length, MaxTracks);
        var area = new Rect(new Vector2(full.Min.X, PhotoTop(full, content)), full.Max);
        var dots = content.Max.Y - bottomChrome;
        var bottom = dots - Metrics.Size.TapTarget * scale;
        DrivePager(trackPager, area, count, ref trackPressed, ref trackPress, scale);
        var scroll = trackPager.Value;
        var first = Math.Clamp((int)MathF.Floor(scroll), 0, count - 1);
        var last = Math.Clamp((int)MathF.Ceiling(scroll), 0, count - 1);
        var page = Math.Clamp((int)MathF.Round(scroll), 0, count - 1);
        drawList.PushClipRect(area.Min, area.Max, true);
        for (var index = first; index <= last; index++)
        {
            var offset = new Vector2((index - scroll) * full.Width, 0f);
            DrawTrackPage(drawList, new Rect(area.Min + offset, area.Max + offset), content.Min.X + offset.X,
                content.Width, bottom, index, tracks[index], week, scale);
        }

        drawList.PopClipRect();
        for (var index = 0; index < count; index++)
        {
            onPhoto |= TrackWinner(tracks[index].Id) is not null;
        }

        PrismArt.OverPhoto(TrackWinner(tracks[page].Id) is not null);
        if (!PrismArt.Light)
        {
            PrismArt.TopFog(drawList, area, scale);
        }

        DrawPagerDots(drawList, trackPager, new Rect(area.Min, new Vector2(area.Max.X, dots)), count, page,
            PrismArt.Fore, scale);
        DrawPhotoBack(drawList, area, PhotoInset(full, content), scale);
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

    private PrismPlacingDto? TrackWinner(string id) => CoverPlacing(store.LastWeekResults(id));

    private void DrawTrackPage(ImDrawListPtr drawList, Rect page, float left, float width, float bottom, int index,
        PrismTrackDto track, PrismWeekDto week, float scale)
    {
        var winner = TrackWinner(track.Id);
        if (winner is null)
        {
            DrawTrackBackdrop(drawList, page);
        }
        else
        {
            DrawLook(drawList, page, winner.ImageUrl, false, native: true);
        }

        var tap = Metrics.Size.TapTarget * scale;
        var line = Typography.LineHeight(PrismType.Caption);
        var name = PrismTracks.Name(track.Id);
        var titleWidth = width - tap - Metrics.Space.Sm * scale;
        var titleHeight = MathF.Max(PrismType.HeroHeight(name, titleWidth), tap);
        var rule = TrackRule(track.Id);
        var note = Loc.T(L.Prism.TrackMovedNote);
        var creditHeight = winner is not null ? Metrics.Space.Xs * scale + line : 0f;
        var textHeight = line + Metrics.Space.Xxs * scale + titleHeight + creditHeight + Metrics.Space.Sm * scale +
                         Typography.MeasureWrappedBlock(rule, PrismType.Body, width).Y + Metrics.Space.Xs * scale +
                         Typography.MeasureWrappedBlock(note, PrismType.Caption, width).Y;
        var y = bottom - textHeight;
        if (winner is not null)
        {
            PrismArt.Scrim(drawList, page, page.Max.Y - y, scale);
        }

        PrismArt.OverPhoto(winner is not null);
        Typography.Draw(drawList, new Vector2(left, y),
            Typography.FitText(TrackCaption(week.Title), width, PrismType.Caption), PrismArt.Muted, PrismType.Caption);
        y += line + Metrics.Space.Xxs * scale;
        var heroHeight = PrismType.HeroHeight(name, titleWidth);
        PrismType.DrawHero(drawList, new Vector2(left, y + (titleHeight - heroHeight) * 0.5f), name, titleWidth,
            PrismArt.Fore);
        DrawTrackAction(drawList, new Vector2(left + width - tap * 0.5f, y + titleHeight * 0.5f), index, track,
            winner is not null, scale);
        y += titleHeight;
        if (winner is not null)
        {
            DrawGlyphCaption(drawList, new Vector2(left, y + Metrics.Space.Xs * scale), left + width,
                PhoneIcons.Crown, TrackWinnerLine(index, winner), PrismArt.Muted, winner, scale);
        }

        y += creditHeight + Metrics.Space.Sm * scale;
        y += Typography.DrawWrappedLeft(new Vector2(left, y), rule, PrismArt.Muted, PrismType.Body, width) +
             Metrics.Space.Xs * scale;
        Typography.DrawWrappedLeft(new Vector2(left, y), note, PrismArt.Muted, PrismType.Caption, width);
        PrismArt.OverPhoto(false);
    }

    private void DrawTrackAction(ImDrawListPtr drawList, Vector2 center, int index, PrismTrackDto track, bool photo,
        float scale)
    {
        var mine = !trackJudging && track.MyEntry is not null;
        var busy = store.Busy && !trackJudging && track.Id == store.Track ? Loc.T(L.Prism.Uploading) : string.Empty;
        var glyph = !mine ? PhoneIcons.ChevronRight
            : track.MyEntry!.Status is PrismPhases.StatusRejected or PrismPhases.StatusMoved
                ? StatusGlyph(track.MyEntry.Status)
                : PhoneIcons.Check;
        if (DrawHeaderAction(drawList, center, glyph, TrackAction(index, track), !store.Busy, busy, UiSound.Tap, scale, photo ? photoControls : controls,
                HoverLabelSide.Above))
        {
            ChooseTrack(track.Id);
        }
    }

    private static void DrawTrackBackdrop(ImDrawListPtr drawList, Rect page)
    {
        var fill = Surfaces.Fill(PrismArt.Fore, FillLevel.Tertiary);
        var top = ImGui.GetColorU32(fill);
        var bottom = ImGui.GetColorU32(fill with { W = 0f });
        drawList.AddRectFilledMultiColor(page.Min, page.Max, top, top, bottom, bottom);
    }

    private static string TrackRule(string id) => id switch
    {
        PrismTracks.Vanilla => Loc.T(L.Prism.TrackVanillaRule),
        PrismTracks.Modded => Loc.T(L.Prism.TrackModdedRule),
        _ => string.Empty,
    };

    private string TrackTab(int index, PrismTrackDto track)
    {
        var key = HashCode.Combine(track.Id, track.EntryCount, Loc.Current.Code);
        return trackTabTexts[index].IsCurrent(key)
            ? trackTabTexts[index].Value
            : trackTabTexts[index].Store(key,
                string.Concat(PrismTracks.Name(track.Id), " ", track.EntryCount.ToString("N0", Loc.Culture)));
    }

    private string TrackAction(int index, PrismTrackDto track)
    {
        var mine = !trackJudging && track.MyEntry is not null;
        var key = HashCode.Combine(track.Id, trackJudging, mine, Loc.Current.Code);
        if (trackActionTexts[index].IsCurrent(key))
        {
            return trackActionTexts[index].Value;
        }

        var name = PrismTracks.Name(track.Id);
        return trackActionTexts[index].Store(key, mine
            ? Loc.T(L.Prism.SeeYourGlam)
            : Loc.T(trackJudging ? L.Prism.JudgeTrack : L.Prism.EnterTrack, name));
    }

    private string TrackWinnerLine(int index, PrismPlacingDto winner)
    {
        var key = HashCode.Combine(winner.Handle, winner.DisplayName, Loc.Current.Code);
        return trackWinnerTexts[index].IsCurrent(key)
            ? trackWinnerTexts[index].Value
            : trackWinnerTexts[index].Store(key,
                Loc.T(L.Prism.LastWeekWinner, AtHandle(winner.Handle, winner.DisplayName)));
    }

    private string TrackCaption(string title) => FactLine(ref trackCaptionText, Loc.T(L.Prism.ChooseTrack), title);
}
