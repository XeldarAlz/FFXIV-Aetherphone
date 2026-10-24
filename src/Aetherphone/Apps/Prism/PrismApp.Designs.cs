using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const string DesignSearchId = "##designSearch";
    private const string DesignListId = "prism.designs";
    private const int DesignQueryMaxLength = 64;
    private const float DesignCheckUnits = 22f;

    private readonly DesignPicker designPicker = new();
    private readonly List<(Guid Id, string Name)> pickedDesigns = new();
    private string designQuery = string.Empty;
    private bool designSearchFocus;
    private Spring designSearchEdge;
    private CachedText designCountText;

    private void OpenDesignPicker()
    {
        designQuery = string.Empty;
        designSearchFocus = true;
        designPicker.Load(DesignPicker.Build(glamour.Designs(), wardrobe.HasDesign));
        Push(PrismScreen.WardrobeDesigns);
    }

    private void DrawDesignPicker(ImDrawListPtr drawList, Rect content, float scale)
    {
        var ready = designPicker.PickedCount > 0 && !wardrobe.Importing && glamour.HasGlamourer;
        var top = DrawActionHeader(drawList, content, PhoneIcons.ChevronLeft, Loc.T(L.Prism.Back),
            Loc.T(L.Prism.ImportDesigns), PhoneIcons.Check, Loc.T(L.Prism.AddSelected), ready, string.Empty,
            UiSound.Tap, scale, out var add) + Metrics.Space.Sm * scale;
        if (add)
        {
            designPicker.CopyPicked(pickedDesigns);
            wardrobe.StartImport(pickedDesigns);
            GoBack();
            return;
        }

        top = DrawDesignSearch(drawList, content, top, scale);
        designPicker.Filter(Loc.Culture.CompareInfo, designQuery);
        top = DrawDesignCount(drawList, content, top, scale);
        LocString? notice = !glamour.HasGlamourer ? L.Prism.DesignsNeedGlamourer
            : designPicker.Count == 0 ? L.Prism.DesignsEmpty
            : designPicker.VisibleCount == 0 ? L.Prism.DesignsNoMatch
            : null;
        if (notice is { } empty)
        {
            Typography.DrawWrappedLeft(new Vector2(content.Min.X, top), Loc.T(empty), PrismArt.Muted, PrismType.Body,
                content.Width);
            return;
        }

        using var id = UiScope.PushId(DesignListId);
        DrawDesignRows(new Rect(new Vector2(content.Min.X, top), content.Max), scale);
    }

    private float DrawDesignSearch(ImDrawListPtr drawList, Rect content, float top, float scale)
    {
        var field = new Rect(new Vector2(content.Min.X, top),
            new Vector2(content.Max.X, top + Metrics.Size.TapTarget * scale));
        SearchBar.Surface(drawList, field, controls);
        var inset = Metrics.Space.Md * scale;
        var glyph = PrismArt.RowIconUnits * scale;
        PhoneIcon.Draw(drawList, new Vector2(field.Min.X + inset + glyph * 0.5f, field.Center.Y), PhoneIcons.Search,
            PrismArt.Muted, glyph);
        if (designSearchFocus)
        {
            ImGui.SetKeyboardFocusHere();
            designSearchFocus = false;
        }

        GlassField.Text(field, DesignSearchId, Loc.T(L.Prism.DesignSearchHint), ref designQuery, PrismArt.Fore,
            PrismArt.Muted, inset + glyph + Metrics.Space.Sm * scale, DesignQueryMaxLength);
        var focus = designSearchEdge.Step(ImGui.IsItemActive() ? 1f : 0f, Motion.Appear,
            MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds));
        PrismLight.PillEdge(drawList, SearchBar.Capsule(field), PrismGlow.Spectrum, PrismLight.FieldAlpha * focus,
            scale);
        return field.Max.Y + Metrics.Space.Sm * scale;
    }

    private float DrawDesignCount(ImDrawListPtr drawList, Rect content, float top, float scale)
    {
        var count = designPicker.PickedCount;
        var key = HashCode.Combine(count, Loc.Current.Code);
        var text = designCountText.IsCurrent(key)
            ? designCountText.Value
            : designCountText.Store(key, Loc.Plural(L.Photos.Selected, count));
        Typography.Draw(drawList, new Vector2(content.Min.X, top),
            Typography.FitText(text, content.Width, PrismType.Caption), count > 0 ? PrismArt.Fore : PrismArt.Muted,
            PrismType.Caption);
        return top + Typography.LineHeight(PrismType.Caption) + Metrics.Space.Sm * scale;
    }

    private void DrawDesignRows(Rect area, float scale)
    {
        using var surface = BeginBody(area);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var rowHeight = DesignRowHeight(scale);
        var windowTop = ImGui.GetWindowPos().Y;
        var windowBottom = windowTop + ImGui.GetWindowHeight();
        var rows = designPicker.VisibleCount;
        var first = Math.Clamp((int)MathF.Floor((windowTop - origin.Y) / rowHeight), 0, rows);
        var last = Math.Clamp((int)MathF.Ceiling((windowBottom - origin.Y) / rowHeight) + 1, first, rows);
        for (var row = first; row < last; row++)
        {
            DrawDesignRow(list, new Rect(new Vector2(origin.X, origin.Y + row * rowHeight),
                new Vector2(origin.X + width, origin.Y + (row + 1) * rowHeight)), row, scale);
        }

        ImGui.Dummy(new Vector2(width, rows * rowHeight + PrismArt.BottomMargin(scale)));
    }

    private static float DesignRowHeight(float scale) =>
        MathF.Max(Metrics.Size.TapTarget * scale,
            Typography.LineHeight(PrismType.Headline) + Typography.LineHeight(PrismType.Caption)) +
        Metrics.Space.Sm * scale;

    private void DrawDesignRow(ImDrawListPtr drawList, Rect row, int index, float scale)
    {
        ref readonly var choice = ref designPicker.Visible(index);
        var picked = designPicker.IsPicked(index);
        var hovered = !choice.Owned && HoverWash(drawList, row);
        var check = DesignCheckUnits * scale;
        var glyph = choice.Owned ? PhoneIcons.Check : picked ? PhoneIcons.CircleCheck : PhoneIcons.Circle;
        PhoneIcon.Draw(drawList, new Vector2(row.Min.X + check * 0.5f, row.Center.Y), glyph,
            picked ? PrismArt.Fore : PrismArt.Muted, check);
        var caption = choice.Owned ? Loc.T(L.Prism.InWardrobe) : choice.Folder;
        var nameHeight = Typography.LineHeight(PrismType.Headline);
        var captionHeight = caption.Length > 0 ? Typography.LineHeight(PrismType.Caption) : 0f;
        var textLeft = row.Min.X + check + Metrics.Space.Md * scale;
        var room = MathF.Max(1f, row.Max.X - textLeft);
        var textTop = row.Center.Y - (nameHeight + captionHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(choice.Name, room, PrismType.Headline), choice.Owned ? PrismArt.Muted : PrismArt.Fore,
            PrismType.Headline);
        if (caption.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(textLeft, textTop + nameHeight),
                Typography.FitText(caption, room, PrismType.Caption), PrismArt.Muted, PrismType.Caption);
        }

        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            designPicker.Toggle(index);
        }
    }
}
