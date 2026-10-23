using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const int MaxTracks = 3;

    private readonly CachedText[] trackTabTexts = new CachedText[MaxTracks];
    private readonly CachedText[] trackCountTexts = new CachedText[MaxTracks];
    private readonly CachedText[] trackActionTexts = new CachedText[MaxTracks];
    private readonly CachedText[] trackWinnerTexts = new CachedText[MaxTracks];
    private readonly Pager trackPager = new(Motion.PageSettle);
    private CachedText trackCaptionText;
    private float trackBlock;
    private int trackBlockKey;
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

    private void DrawTrackChoice(ImDrawListPtr drawList, Rect content, float scale)
    {
        var tracks = store.Tracks;
        if (tracks.Length < 2 || store.Week is not { } week)
        {
            Go(PrismScreen.Cover, -1);
            return;
        }

        var count = Math.Min(tracks.Length, MaxTracks);
        var full = FullFrom(content, scale);
        var button = ActionRect(content, scale);
        var gap = Metrics.Space.Lg * scale;
        var blockKey = HashCode.Combine(Loc.Current.Code, content.Width, Plugin.Fonts.Generation, count);
        if (blockKey != trackBlockKey)
        {
            trackBlockKey = blockKey;
            trackBlock = 0f;
            for (var index = 0; index < count; index++)
            {
                trackBlock = MathF.Max(trackBlock, TrackBlockHeight(tracks[index].Id, content.Width, scale));
            }
        }

        var top = PhotoTop(full, content);
        var height = MathF.Min(full.Width * CoverRatio, button.Min.Y - gap - trackBlock - gap - top);
        var photo = new Rect(new Vector2(full.Min.X, top), new Vector2(full.Max.X, top + height));
        var area = new Rect(photo.Min, new Vector2(photo.Max.X, button.Min.Y - gap));
        DriveTrackPager(area, count, scale);
        var scroll = trackPager.Value;
        var first = Math.Clamp((int)MathF.Floor(scroll), 0, count - 1);
        var last = Math.Clamp((int)MathF.Ceiling(scroll), 0, count - 1);
        drawList.PushClipRect(area.Min, area.Max, true);
        for (var index = first; index <= last; index++)
        {
            var offset = new Vector2((index - scroll) * full.Width, 0f);
            DrawTrackPage(drawList, new Rect(photo.Min + offset, photo.Max + offset), content.Min.X + offset.X,
                content.Width, index, tracks[index], week, scale);
        }

        drawList.PopClipRect();
        if (!PrismArt.Light)
        {
            PrismArt.TopFog(drawList, photo, scale);
        }

        var page = Math.Clamp((int)MathF.Round(scroll), 0, count - 1);
        DrawTrackDots(drawList, photo, count, page,
            CoverPlacing(store.LastWeekResults(tracks[page].Id)) is null ? PrismArt.Fore : PrismArt.PhotoFore, scale);
        DrawPhotoBack(drawList, photo, PhotoInset(full, content), scale);
        if (DrawButton(drawList, button, TrackAction(page, tracks[page]), ButtonStyle.Solid, scale,
                id: "prism.track.action"))
        {
            ChooseTrack(tracks[page].Id);
        }
    }

    private void DriveTrackPager(Rect area, int count, float scale)
    {
        var delta = ImGui.GetIO().DeltaTime;
        trackPager.Step(delta, count);
        var mouse = ImGui.GetMousePos();
        if (trackPager.Dragging)
        {
            trackPager.Drag(mouse.X, area.Width, count, delta);
            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                trackPager.Release(area.Width, count);
            }

            return;
        }

        if (UiInteract.Hover(area.Min, area.Max) && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            trackPressed = true;
            trackPress = mouse;
        }

        if (!trackPressed || !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            trackPressed = false;
            return;
        }

        var move = mouse - trackPress;
        if (MathF.Abs(move.X) <= PagerSlopUnits * scale || MathF.Abs(move.X) <= MathF.Abs(move.Y))
        {
            return;
        }

        trackPressed = false;
        UiInteract.CancelPendingTap();
        trackPager.Begin(trackPress.X);
    }

    private void DrawTrackDots(ImDrawListPtr drawList, Rect photo, int count, int page, Vector4 ink, float scale)
    {
        var tap = Metrics.Size.TapTarget * scale;
        var center = new Vector2(photo.Center.X, photo.Max.Y - tap * 0.5f);
        PhotoCarousel.DrawDots(drawList, center, count, page, photo.Width, ink);
        var hit = new Rect(new Vector2(center.X - tap * count * 0.5f, photo.Max.Y - tap),
            new Vector2(center.X + tap * count * 0.5f, photo.Max.Y));
        if (UiInteract.HoverClick(hit.Min, hit.Max))
        {
            trackPager.AnimateTo(ImGui.GetMousePos().X >= center.X ? page + 1 : page - 1, count);
        }
    }

    private float TrackBlockHeight(string id, float width, float scale) =>
        Typography.LineHeight(PrismType.Headline) + Metrics.Space.Xxs * scale +
        Typography.MeasureWrappedBlock(TrackRule(id), PrismType.Body, width).Y + Metrics.Space.Sm * scale +
        Typography.MeasureWrappedBlock(Loc.T(L.Prism.TrackMovedNote), PrismType.Caption, width).Y;

    private void DrawTrackPage(ImDrawListPtr drawList, Rect photo, float left, float width, int index,
        PrismTrackDto track, PrismWeekDto week, float scale)
    {
        var winner = CoverPlacing(store.LastWeekResults(track.Id));
        if (winner is null)
        {
            PrismArt.Placeholder(drawList, photo, false, scale);
        }
        else
        {
            DrawLook(drawList, photo, winner.ImageUrl, false, native: true);
        }

        var line = Typography.LineHeight(PrismType.Caption);
        var name = PrismTracks.Name(track.Id);
        var winnerHeight = winner is not null || week.PreviousWeekId.Length > 0 ? Metrics.Space.Xs * scale + line : 0f;
        var textHeight = line + Metrics.Space.Xxs * scale + PrismType.HeroHeight(name, width) + winnerHeight;
        var bottom = photo.Max.Y - Metrics.Size.TapTarget * scale;
        if (winner is not null)
        {
            PrismArt.Scrim(drawList, photo, photo.Max.Y - bottom + textHeight, scale);
        }

        var ink = winner is null ? PrismArt.Fore : PrismArt.PhotoFore;
        var muted = winner is null ? PrismArt.Muted : PrismArt.PhotoMuted;
        var y = bottom - textHeight;
        Typography.Draw(drawList, new Vector2(left, y),
            Typography.FitText(TrackCaption(week.Title), width, PrismType.Caption), muted, PrismType.Caption);
        y += line + Metrics.Space.Xxs * scale;
        y += PrismType.DrawHero(drawList, new Vector2(left, y), name, width, ink);
        if (winner is not null)
        {
            DrawGlyphCaption(drawList, new Vector2(left, y + Metrics.Space.Xs * scale), left + width,
                PhoneIcons.Crown, TrackWinner(index, winner), muted, winner, scale);
        }

        y = photo.Max.Y + Metrics.Space.Lg * scale;
        Typography.Draw(drawList, new Vector2(left, y),
            Typography.FitText(TrackCount(index, track.EntryCount), width, PrismType.Headline), PrismArt.Fore,
            PrismType.Headline);
        y += Typography.LineHeight(PrismType.Headline) + Metrics.Space.Xxs * scale;
        y += Typography.DrawWrappedLeft(new Vector2(left, y), TrackRule(track.Id), PrismArt.Muted, PrismType.Body,
            width) + Metrics.Space.Sm * scale;
        Typography.DrawWrappedLeft(new Vector2(left, y), Loc.T(L.Prism.TrackMovedNote), PrismArt.Muted,
            PrismType.Caption, width);
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

    private string TrackCount(int index, int count)
    {
        var key = HashCode.Combine(count, Loc.Current.Code);
        return trackCountTexts[index].IsCurrent(key)
            ? trackCountTexts[index].Value
            : trackCountTexts[index].Store(key, Loc.Plural(L.Prism.EnteredThisWeek, count));
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

    private string TrackWinner(int index, PrismPlacingDto winner)
    {
        var key = HashCode.Combine(winner.Handle, winner.DisplayName, Loc.Current.Code);
        return trackWinnerTexts[index].IsCurrent(key)
            ? trackWinnerTexts[index].Value
            : trackWinnerTexts[index].Store(key,
                Loc.T(L.Prism.LastWeekWinner, AtHandle(winner.Handle, winner.DisplayName)));
    }

    private string TrackCaption(string title) => FactLine(ref trackCaptionText, Loc.T(L.Prism.ChooseTrack), title);
}
