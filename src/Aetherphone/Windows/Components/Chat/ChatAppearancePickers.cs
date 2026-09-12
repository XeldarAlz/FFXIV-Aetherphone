using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Wallpapers;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal readonly ref struct ChatThemePickerModel
{
    public required Rect Area { get; init; }
    public required string ThemeId { get; init; }
    public required string WallpaperId { get; init; }
    public required bool Pattern { get; init; }
    public required string Hint { get; init; }
    public required Action<string> Pick { get; init; }
}

internal readonly ref struct ChatWallpaperPickerModel
{
    public required Rect Area { get; init; }
    public required string ThemeId { get; init; }
    public required string WallpaperId { get; init; }
    public required bool Pattern { get; init; }
    public required bool Scoped { get; init; }
    public required bool HasOverride { get; init; }
    public required string Hint { get; init; }
    public required Action<string> Pick { get; init; }
    public required Action<bool> SetPattern { get; init; }
    public required Action ClearOverride { get; init; }
}

internal sealed class ChatAppearancePickers
{
    private const float SwatchRadius = 22f;
    private const float SwatchCellHeight = 82f;
    private const float SwatchRingGap = 4f;
    private const float SwatchCheckGlyph = 20f;
    private const int SwatchColumns = 4;
    private const float PreviewHeight = 150f;
    private const float PreviewPad = 12f;
    private const float PreviewBubblePadX = 10f;
    private const float PreviewBubblePadY = 7f;
    private const float PreviewBubbleGap = 8f;
    private const float PreviewTail = 7f;
    private const float WallpaperTileHeight = 64f;
    private const float WallpaperTileGap = 8f;
    private const float WallpaperTileRounding = 12f;
    private const int WallpaperColumns = 4;
    private const int PhotoColumns = 3;
    private const float PhotoTileGap = 6f;
    private const float SelectionRing = 2.5f;
    private const int ScreenResumeFrameGap = 3;

    private static readonly TextStyle SwatchLabelStyle = TextStyles.Caption1;
    private static readonly TextStyle PreviewTextStyle = TextStyles.Subheadline;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    private readonly ChatListChrome chrome;
    private readonly WallpaperImageCache images;
    private readonly PhotoLibrary library;
    private string[] wallpaperPhotos = Array.Empty<string>();
    private int lastWallpaperFrame = -100;

    public ChatAppearancePickers(ChatListChrome chrome, WallpaperImageCache images, PhotoLibrary library)
    {
        this.chrome = chrome;
        this.images = images;
        this.library = library;
    }

    public void DrawThemePicker(in ChatThemePickerModel model)
    {
        var scale = UiScale.Current;
        var ink = chrome.Ink;
        var current = ChatThemes.Resolve(model.ThemeId);
        using (AppSurface.Begin(model.Area))
        {
            var drawList = ImGui.GetWindowDrawList();
            var width = ScrollLayout.StableContentWidth();
            DrawHintParagraph(model.Hint, width);
            ReservePreview(drawList, width, model.WallpaperId, model.Pattern, current, scale);

            var gridOrigin = ImGui.GetCursorScreenPos();
            var cellWidth = width / SwatchColumns;
            var themes = ChatThemes.All;
            var rows = (themes.Length + SwatchColumns - 1) / SwatchColumns;
            for (var index = 0; index < themes.Length; index++)
            {
                var column = index % SwatchColumns;
                var row = index / SwatchColumns;
                var cellMin = new Vector2(gridOrigin.X + column * cellWidth, gridOrigin.Y + row * SwatchCellHeight * scale);
                var cellMax = cellMin + new Vector2(cellWidth, SwatchCellHeight * scale);
                var swatchCenter = new Vector2((cellMin.X + cellMax.X) * 0.5f, cellMin.Y + SwatchRadius * scale + 6f * scale);
                var selected = string.Equals(themes[index].Id, current.Id, StringComparison.Ordinal);
                var hovered = UiInteract.Hover(cellMin, cellMax);
                var radius = SwatchRadius * scale * (hovered ? 1.06f : 1f);
                drawList.AddCircleFilled(swatchCenter, radius, ImGui.GetColorU32(themes[index].Accent), 40);
                if (selected)
                {
                    drawList.AddCircle(swatchCenter, radius + SwatchRingGap * scale, ImGui.GetColorU32(ink.TitleInk), 40,
                        SelectionRing * scale);
                    PhoneIcon.Draw(drawList, swatchCenter, PhoneIcons.Check, White, SwatchCheckGlyph * scale);
                }

                var label = Typography.FitText(Loc.T(themes[index].Name), cellWidth - 6f * scale, SwatchLabelStyle);
                Typography.DrawCentered(drawList, new Vector2(swatchCenter.X, swatchCenter.Y + SwatchRadius * scale + 14f * scale),
                    label, selected ? ink.TitleInk : ink.MutedInk, SwatchLabelStyle);
                if (hovered)
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }

                if (UiInteract.Click(cellMin, cellMax, hovered) && !selected)
                {
                    model.Pick(themes[index].Id);
                }
            }

            ImGui.SetCursorScreenPos(gridOrigin);
            ImGui.Dummy(new Vector2(width, rows * SwatchCellHeight * scale + 24f * scale));
        }
    }

    public void DrawWallpaperPicker(in ChatWallpaperPickerModel model)
    {
        var scale = UiScale.Current;
        var frame = ImGui.GetFrameCount();
        if (frame - lastWallpaperFrame > ScreenResumeFrameGap)
        {
            wallpaperPhotos = library.List();
        }

        lastWallpaperFrame = frame;
        var ink = chrome.Ink;
        var ui = chrome.Ui;
        var current = ChatThemes.Resolve(model.ThemeId);
        using (AppSurface.Begin(model.Area))
        {
            var drawList = ImGui.GetWindowDrawList();
            var width = ScrollLayout.StableContentWidth();
            DrawHintParagraph(model.Hint, width);
            ReservePreview(drawList, width, model.WallpaperId, model.Pattern, current, scale);

            var optionRows = model.Scoped ? 2 : 1;
            var optionCard = GroupCard.Begin(ui, optionRows, ChatListChrome.SettingRowHeight);
            var pattern = chrome.DrawCardSwitchRow(drawList, optionCard.NextRow(), PhoneIcons.Sparkles,
                ChatListChrome.TintViolet, Loc.T(L.Message.WallpaperPattern), model.Pattern, "chat.wallpaper.pattern");
            if (pattern != model.Pattern)
            {
                model.SetPattern(pattern);
            }

            if (model.Scoped && chrome.DrawCardRow(drawList, optionCard.NextRow(), PhoneIcons.Wallpaper,
                    ChatListChrome.TintTeal, Loc.T(L.Message.WallpaperUseDefault),
                    model.HasOverride ? string.Empty : Loc.T(L.Message.WallpaperDefault), chevron: false)
                && model.HasOverride)
            {
                model.ClearOverride();
            }

            optionCard.End();
            ChatListChrome.DrawCardGap();

            chrome.DrawInsetSectionLabel(Loc.T(L.Message.WallpaperColors));
            DrawColorGrid(drawList, width, model.WallpaperId, model.Pick, ink, scale);
            chrome.DrawInsetSectionLabel(Loc.T(L.Message.WallpaperPhotos));
            DrawPhotoGrid(drawList, width, model.WallpaperId, model.Pick, ink, ui, scale);
            ImGui.Dummy(new Vector2(0f, 30f * scale));
        }
    }

    public void DrawPreview(ImDrawListPtr drawList, Rect rect, string wallpaperId, bool pattern,
        in ChatTheme previewTheme)
    {
        var scale = UiScale.Current;
        ChatWallpapers.Paint(drawList, rect, wallpaperId, pattern, images);
        Squircle.Stroke(drawList, rect.Min, rect.Max, 0f, ImGui.GetColorU32(chrome.Ui.Palette.CardStroke), 1f);
        var pad = PreviewPad * scale;
        var maxBubbleWidth = rect.Width * 0.72f;
        var incoming = Loc.T(L.Message.PreviewIncoming);
        var outgoing = Loc.T(L.Message.PreviewOutgoing);
        var incomingTop = rect.Min.Y + pad;
        var incomingBottom = DrawPreviewBubble(drawList, new Vector2(rect.Min.X + pad, incomingTop), incoming,
            ChatThemes.IncomingBubble, ChatThemes.IncomingInk, maxBubbleWidth, false);
        var outgoingTop = incomingBottom + PreviewBubbleGap * scale;
        DrawPreviewBubble(drawList, new Vector2(rect.Max.X - pad, outgoingTop), outgoing,
            previewTheme.OutgoingBubble, ChatThemes.OutgoingInk, maxBubbleWidth, true);
    }

    private void ReservePreview(ImDrawListPtr drawList, float width, string wallpaperId, bool pattern,
        in ChatTheme theme, float scale)
    {
        var previewOrigin = ImGui.GetCursorScreenPos();
        var previewRect = new Rect(previewOrigin,
            new Vector2(previewOrigin.X + width, previewOrigin.Y + PreviewHeight * scale));
        DrawPreview(drawList, previewRect, wallpaperId, pattern, theme);
        ImGui.SetCursorScreenPos(previewOrigin);
        ImGui.Dummy(new Vector2(width, previewRect.Height + Metrics.Space.Lg * scale));
    }

    private void DrawHintParagraph(string text, float width)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var height = Typography.DrawWrappedLeft(new Vector2(origin.X + 4f * scale, origin.Y + 4f * scale), text,
            chrome.Ink.MutedInk, TextStyles.Footnote, width - 8f * scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Lg * scale));
    }

    private static float DrawPreviewBubble(ImDrawListPtr drawList, Vector2 anchor, string text, Vector4 fill,
        Vector4 textInk, float maxWidth, bool mine)
    {
        var scale = UiScale.Current;
        var padX = PreviewBubblePadX * scale;
        var padY = PreviewBubblePadY * scale;
        var textSize = Typography.MeasureWrappedBlock(text, PreviewTextStyle, maxWidth - padX * 2f);
        var width = textSize.X + padX * 2f;
        var height = textSize.Y + padY * 2f;
        var min = mine ? new Vector2(anchor.X - width, anchor.Y) : anchor;
        var max = min + new Vector2(width, height);
        var rounding = ChatThemes.BubbleRounding * scale;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(fill));
        var tail = PreviewTail * scale;
        if (mine)
        {
            drawList.AddTriangleFilled(new Vector2(max.X - rounding, min.Y), new Vector2(max.X + tail, min.Y),
                new Vector2(max.X, min.Y + rounding), ImGui.GetColorU32(fill));
        }
        else
        {
            drawList.AddTriangleFilled(new Vector2(min.X + rounding, min.Y), new Vector2(min.X - tail, min.Y),
                new Vector2(min.X, min.Y + rounding), ImGui.GetColorU32(fill));
        }

        Typography.DrawWrappedLeft(new Vector2(min.X + padX, min.Y + padY), text, textInk, PreviewTextStyle,
            maxWidth - padX * 2f);
        return max.Y;
    }

    private static void DrawColorGrid(ImDrawListPtr drawList, float width, string current, Action<string> pick,
        SocialInk ink, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var gap = WallpaperTileGap * scale;
        var tileWidth = (width - gap * (WallpaperColumns - 1)) / WallpaperColumns;
        var tileHeight = WallpaperTileHeight * scale;
        var colors = ChatWallpapers.Colors;
        var rows = (colors.Length + WallpaperColumns - 1) / WallpaperColumns;
        var selectedIsColor = !ChatWallpapers.IsPhoto(current);
        for (var index = 0; index < colors.Length; index++)
        {
            var column = index % WallpaperColumns;
            var row = index / WallpaperColumns;
            var min = new Vector2(origin.X + column * (tileWidth + gap), origin.Y + row * (tileHeight + gap));
            var max = min + new Vector2(tileWidth, tileHeight);
            var selected = selectedIsColor && (string.Equals(colors[index].Id, current, StringComparison.Ordinal)
                || (current.Length == 0 && index == 0));
            var hovered = UiInteract.Hover(min, max);
            Squircle.Fill(drawList, min, max, WallpaperTileRounding * scale, ImGui.GetColorU32(colors[index].Color));
            Squircle.Stroke(drawList, min, max, WallpaperTileRounding * scale,
                ImGui.GetColorU32(selected ? ink.TitleInk : hovered ? ink.ChipHover : ink.ChipStroke),
                selected ? SelectionRing * scale : 1f);
            if (selected)
            {
                PhoneIcon.Draw(drawList, (min + max) * 0.5f, PhoneIcons.Check, ink.TitleInk, SwatchCheckGlyph * scale);
            }

            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(min, max, hovered))
            {
                pick(index == 0 ? string.Empty : colors[index].Id);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rows * (tileHeight + gap) + Metrics.Space.Sm * scale));
    }

    private void DrawPhotoGrid(ImDrawListPtr drawList, float width, string current, Action<string> pick,
        SocialInk ink, AppSkin ui, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        if (wallpaperPhotos.Length == 0)
        {
            var height = Typography.DrawWrappedLeft(new Vector2(origin.X + 4f * scale, origin.Y), Loc.T(L.Common.NoPhotos),
                ink.MutedInk, TextStyles.Footnote, width - 8f * scale);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, height + Metrics.Space.Lg * scale));
            return;
        }

        var gap = PhotoTileGap * scale;
        var cell = (width - gap * (PhotoColumns - 1)) / PhotoColumns;
        var scrollY = ImGui.GetScrollY();
        var viewHeight = ImGui.GetWindowSize().Y;
        var windowTop = ImGui.GetWindowPos().Y;
        var selectedPath = ChatWallpapers.PhotoPath(current);
        for (var index = 0; index < wallpaperPhotos.Length; index++)
        {
            var column = index % PhotoColumns;
            var rowIndex = index / PhotoColumns;
            var min = new Vector2(origin.X + column * (cell + gap), origin.Y + rowIndex * (cell + gap));
            var max = min + new Vector2(cell, cell);
            var localTop = min.Y - windowTop + scrollY;
            if (localTop + cell < scrollY - cell || localTop > scrollY + viewHeight + cell)
            {
                continue;
            }

            var hovered = UiInteract.Hover(min, max);
            var texture = images.Get(wallpaperPhotos[index]);
            var rounding = WallpaperTileRounding * scale;
            if (texture is null)
            {
                Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(ui.FieldSurface));
            }
            else
            {
                var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
                drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding,
                    ImDrawFlags.RoundCornersAll);
            }

            var selected = string.Equals(wallpaperPhotos[index], selectedPath, StringComparison.Ordinal);
            if (selected)
            {
                Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(ink.TitleInk), SelectionRing * scale);
                drawList.AddCircleFilled((min + max) * 0.5f, SwatchCheckGlyph * 0.8f * scale,
                    ImGui.GetColorU32(ink.Accent), 24);
                PhoneIcon.Draw(drawList, (min + max) * 0.5f, PhoneIcons.Check, White, SwatchCheckGlyph * scale);
            }

            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(min, max, hovered))
            {
                pick(ChatWallpapers.PhotoId(wallpaperPhotos[index]));
            }
        }

        var rows = (wallpaperPhotos.Length + PhotoColumns - 1) / PhotoColumns;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rows * (cell + gap)));
    }
}
