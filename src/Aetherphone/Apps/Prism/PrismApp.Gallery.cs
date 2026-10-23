using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal readonly record struct WeekGallery(string WeekId, string Track, string Title);

internal sealed partial class PrismApp
{
    private const int FeatureCount = 3;

    private readonly string[] galleryTracks = new string[2];
    private readonly Dictionary<int, string> placeNumbers = new();
    private string? placeNumbersLanguage;
    private WeekGallery gallery;
    private CachedText weekLineText;

    private void OpenGallery(string weekId, string track, string title)
    {
        Push(PrismScreen.Gallery);
        gallery = new WeekGallery(weekId, track == PrismTracks.Vanilla ? PrismTracks.Vanilla : PrismTracks.Modded,
            title);
        glams.OpenWeek(gallery.WeekId, gallery.Track);
    }

    private void DrawGallery(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        if (gallery.WeekId.Length == 0)
        {
            GoBack();
            return;
        }

        var top = DrawBackHeader(drawList, content, gallery.Title.Length > 0 ? gallery.Title : WeekTitle(gallery.WeekId),
            scale);
        if (HasTracks)
        {
            var row = top + Metrics.Space.Xs * scale;
            top = DrawGalleryTracks(new Rect(new Vector2(content.Min.X, row),
                new Vector2(content.Max.X, row + Metrics.Size.TapTarget * scale)));
        }

        var body = new Rect(new Vector2(full.Min.X, top + Metrics.Space.Sm * scale),
            new Vector2(full.Max.X, content.Max.Y));
        using var surface = BeginBody(body);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var gap = Metrics.Space.Xxs * scale;
        var cursor = DrawGalleryGlams(list, origin.X + gap, origin.Y, width - gap * 2f, scale);
        ImGui.Dummy(new Vector2(width, cursor - origin.Y + PrismArt.BottomMargin(scale)));
        if (InfiniteScroll.ReachedBottom())
        {
            glams.LoadMoreList();
        }
    }

    private float DrawGalleryTracks(Rect row)
    {
        galleryTracks[0] = PrismTracks.Name(PrismTracks.Modded);
        galleryTracks[1] = PrismTracks.Name(PrismTracks.Vanilla);
        var selected = gallery.Track == PrismTracks.Vanilla ? 1 : 0;
        var picked = DrawSegments("prism.gallery.tracks", row, galleryTracks, selected);
        if (picked != selected)
        {
            gallery = gallery with { Track = picked == 1 ? PrismTracks.Vanilla : PrismTracks.Modded };
            pendingScroll = 0f;
            glams.OpenWeek(gallery.WeekId, gallery.Track);
        }

        return row.Max.Y;
    }

    private float DrawGalleryGlams(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var lane = glams.List;
        var items = lane.Items;
        if (items.Length == 0 && lane.Loading)
        {
            var shown = FeatureCount + ExploreColumns;
            for (var index = 0; index < shown; index++)
            {
                PrismArt.Placeholder(drawList, GalleryCell(left, top, width, index, true, scale), false, scale);
            }

            return top + GalleryHeight(width, shown, true, scale);
        }

        if (items.Length == 0)
        {
            return DrawGalleryEmpty(drawList, left, width, top, lane.Failed, scale);
        }

        var feature = items.Length >= FeatureCount && items[0].Placing == 1;
        for (var index = 0; index < items.Length; index++)
        {
            var photo = GalleryCell(left, top, width, index, feature, scale);
            if (ImGui.IsRectVisible(photo.Min, photo.Max) &&
                DrawGalleryTile(drawList, photo, items[index], feature && index < FeatureCount, index == 0, scale))
            {
                OpenGlam(items[index]);
            }
        }

        var bottom = top + GalleryHeight(width, items.Length, feature, scale);
        return lane.LoadingMore ? DrawLoadingRowBelow(left, width, bottom) : bottom;
    }

    private static Rect GalleryCell(float left, float top, float width, int index, bool feature, float scale)
    {
        if (!feature)
        {
            return GridCell(left, top, width, index, scale, ExploreColumns);
        }

        if (index >= FeatureCount)
        {
            return GridCell(left, top + GridPitch(width, scale, ExploreColumns) * 2f, width, index - FeatureCount,
                scale, ExploreColumns);
        }

        if (index > 0)
        {
            return GridCell(left, top, width, ExploreColumns - 1 + (index - 1) * ExploreColumns, scale,
                ExploreColumns);
        }

        return new Rect(new Vector2(left, top), GridCell(left, top, width, ExploreColumns + 1, scale, ExploreColumns).Max);
    }

    private static float GalleryHeight(float width, int count, bool feature, float scale) =>
        feature
            ? GridPitch(width, scale, ExploreColumns) * 2f +
              GridHeight(width, count - FeatureCount, scale, ExploreColumns)
            : GridHeight(width, count, scale, ExploreColumns);

    private bool DrawGalleryTile(ImDrawListPtr drawList, Rect photo, GlamDto glam, bool podium, bool winner,
        float scale)
    {
        var tapped = DrawGridTile(drawList, photo, Cover(glam), string.Empty, string.Empty, string.Empty, scale);
        var mark = PrismMotion.Mark(drawList);
        if (podium)
        {
            DrawPodiumBand(drawList, photo, glam, winner, scale);
        }
        else
        {
            DrawPlaceBand(drawList, photo, glam, scale);
        }

        if (photo.Min == pressedTile)
        {
            PrismMotion.Grow(drawList, mark, photo.Center, tileScale);
        }

        return tapped;
    }

    private void DrawPodiumBand(ImDrawListPtr drawList, Rect photo, GlamDto glam, bool winner, float scale)
    {
        var inset = (winner ? Metrics.Space.Lg : Metrics.Space.Sm) * scale;
        var radius = CoverMedalUnits * scale;
        var line = MathF.Max(radius * 2f, Typography.LineHeight(PrismType.Headline));
        PrismArt.Scrim(drawList, photo, line + inset, scale);
        var middle = photo.Max.Y - inset - line * 0.5f;
        var left = photo.Min.X + inset;
        DrawPlacingMedal(drawList, new Vector2(left + radius, middle), radius, glam.Placing, true, scale);
        left += radius * 2f + Metrics.Space.Xs * scale;
        var handle = AuthorLine(glam.Author);
        if (winner)
        {
            DrawCountByline(drawList, left, photo.Max.X - inset, middle, handle, PhoneIcons.HeartFilled,
                glam.Likes > 0 ? NumberText.Compact(glam.Likes) : string.Empty, PrismArt.PhotoFore,
                PrismArt.PhotoFore, scale);
            return;
        }

        var fitted = Typography.FitText(handle, MathF.Max(1f, photo.Max.X - inset - left), PrismType.Caption);
        Typography.Draw(drawList, new Vector2(left, middle - Typography.LineHeight(PrismType.Caption) * 0.5f), fitted,
            PrismArt.PhotoFore, PrismType.Caption);
    }

    private void DrawPlaceBand(ImDrawListPtr drawList, Rect photo, GlamDto glam, float scale)
    {
        var place = glam.Placing > 0 ? PlaceNumber(glam.Placing) : string.Empty;
        var likes = glam.Likes > 0 ? NumberText.Compact(glam.Likes) : string.Empty;
        if (place.Length == 0 && likes.Length == 0)
        {
            return;
        }

        var line = Typography.LineHeight(PrismType.Caption);
        var inset = Metrics.Space.Sm * scale;
        PrismArt.Scrim(drawList, photo, line + inset, scale);
        var top = photo.Max.Y - inset - line;
        var right = photo.Max.X - inset;
        if (likes.Length > 0)
        {
            right -= Typography.Measure(likes, PrismType.Caption).X;
            Typography.Draw(drawList, new Vector2(right, top), likes, PrismArt.PhotoFore, PrismType.Caption);
            var glyph = PrismArt.InlineIconUnits * scale;
            right -= Metrics.Space.Xxs * scale + glyph;
            PhoneIcon.Draw(drawList, new Vector2(right + glyph * 0.5f, top + line * 0.5f), PhoneIcons.HeartFilled,
                PrismArt.PhotoFore, glyph);
            right -= Metrics.Space.Xs * scale;
        }

        var left = photo.Min.X + inset;
        Typography.Draw(drawList, new Vector2(left, top),
            Typography.FitText(place, MathF.Max(1f, right - left), PrismType.Caption), PrismArt.PhotoFore,
            PrismType.Caption);
    }

    private string PlaceNumber(int placing)
    {
        if (!ReferenceEquals(placeNumbersLanguage, Loc.Current.Code))
        {
            placeNumbersLanguage = Loc.Current.Code;
            placeNumbers.Clear();
        }

        if (!placeNumbers.TryGetValue(placing, out var label))
        {
            label = Loc.T(L.Prism.PlaceNumber, NumberText.Group(placing));
            placeNumbers[placing] = label;
        }

        return label;
    }

    private float DrawGalleryEmpty(ImDrawListPtr drawList, float left, float width, float top, bool failed,
        float scale)
    {
        var pending = !failed && store.Week is { } week && week.Id == gallery.WeekId &&
                      week.Phase != PrismPhases.Results;
        var bottom = DrawEmpty(drawList, EmptyArea(left, width, top, scale),
            failed ? PhoneIcons.Refresh : pending ? PhoneIcons.Crown : PhoneIcons.Photo,
            Loc.T(failed ? L.Common.LoadFailed : pending ? L.Prism.ResultsPendingTitle : L.Prism.EmptyFeedTitle),
            Loc.T(failed ? L.Common.LoadFailedHint : pending ? L.Prism.ResultsPendingHint : L.Prism.GalleryEmptyHint),
            failed ? Loc.T(L.Common.Retry) : string.Empty, ButtonStyle.Solid, scale, out var retry);
        if (retry)
        {
            glams.OpenWeek(gallery.WeekId, gallery.Track);
        }

        return bottom;
    }

    private float DrawWeekLine(ImDrawListPtr drawList, float left, float right, float top, GlamDto glam, float scale)
    {
        var middle = top + Metrics.Size.TapTarget * scale * 0.5f;
        var radius = CoverMedalUnits * scale;
        DrawPlacingMedal(drawList, new Vector2(left + radius, middle), radius, glam.Placing, false, scale);
        var textLeft = left + radius * 2f + Metrics.Space.Sm * scale;
        var glyph = PrismArt.IconUnits * scale;
        var label = Typography.FitText(WeekLine(glam), MathF.Max(1f, right - textLeft - glyph), PrismType.Body);
        var size = Typography.Measure(label, PrismType.Body);
        var hit = new Rect(new Vector2(left, top),
            new Vector2(textLeft + size.X + glyph, top + Metrics.Size.TapTarget * scale));
        var hovered = UiInteract.Hover(hit.Min, hit.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Typography.Draw(drawList, new Vector2(textLeft, middle - size.Y * 0.5f), label,
            hovered ? PrismArt.Muted : PrismArt.Fore, PrismType.Body);
        PhoneIcon.Draw(drawList, new Vector2(textLeft + size.X + glyph * 0.5f, middle), PhoneIcons.ChevronRight,
            PrismArt.Muted, glyph);
        if (UiInteract.Click(hit.Min, hit.Max, hovered))
        {
            OpenGallery(glam.WeekId, glam.Vanilla ? PrismTracks.Vanilla : PrismTracks.Modded, string.Empty);
        }

        return hit.Max.Y;
    }

    private string WeekLine(GlamDto glam)
    {
        var title = WeekTitle(glam.WeekId);
        if (title.Length == 0)
        {
            title = PrismTracks.Name(glam.Vanilla ? PrismTracks.Vanilla : PrismTracks.Modded);
        }

        var key = HashCode.Combine(glam.Placing, title, Loc.Current.Code);
        return weekLineText.IsCurrent(key)
            ? weekLineText.Value
            : weekLineText.Store(key, Loc.T(L.Prism.PlacingLine, glam.Placing, title));
    }

    private string WeekTitle(string weekId)
    {
        if (store.Week is { } week && week.Id == weekId)
        {
            return week.Title;
        }

        if (store.Results is { } results && results.WeekId == weekId)
        {
            return results.Title;
        }

        var placings = glams.Profile?.Placings ?? Array.Empty<GlamPlacingDto>();
        for (var index = 0; index < placings.Length; index++)
        {
            if (placings[index].WeekId == weekId && placings[index].Title.Length > 0)
            {
                return placings[index].Title;
            }
        }

        return store.WeekTitle(weekId);
    }
}
