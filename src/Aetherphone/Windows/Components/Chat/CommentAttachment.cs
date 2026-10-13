using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal sealed class CommentAttachment
{
    private const float PanelHeightUnits = 232f;
    private const float StripHeightUnits = 64f;
    private const int PickerColumns = 4;

    private readonly ImageIntake intake = new();
    private string? path;
    private bool panelOpen;
    private string[] pickerPaths = Array.Empty<string>();
    private string notice = string.Empty;
    private string? pendingImport;

    public string? Path => path;

    public Action<ImDrawListPtr, Vector2, float, Vector4>? IconPainter { get; set; }

    public void Clear()
    {
        path = null;
        panelOpen = false;
        notice = string.Empty;
        intake.Discard();
    }

    public void Restore(string restoredPath)
    {
        path = restoredPath;
    }

    public void ClosePanel()
    {
        panelOpen = false;
    }

    public float StripHeight(float scale)
    {
        return path is null ? 0f : StripHeightUnits * scale;
    }

    public void ConsumePendingImport()
    {
        var picked = Interlocked.Exchange(ref pendingImport, null);
        if (!string.IsNullOrEmpty(picked))
        {
            Take(picked);
        }

        if (intake.TryTake(out var taken))
        {
            Take(taken);
            intake.Discard();
        }
    }

    public void AcceptPasteAndDrop(Rect area, PhotoLibrary library, bool fieldActive, Vector4 highlight)
    {
        if (fieldActive && ImageIntake.PasteRequested())
        {
            intake.Paste(library);
        }

        intake.AcceptDrop(area, library, highlight);
    }

    public void DrawToggle(in AppSkin ui, Vector2 center, float radius, Vector4 activeColor, Vector4 idleColor,
        string tooltip, PhotoLibrary library, EmojiComposer emoji)
    {
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var hovered = UiInteract.Hover(min, max);
        var color = panelOpen || path is not null ? activeColor : hovered ? ui.Theme.TextStrong : idleColor;
        if (IconPainter is { } painter)
        {
            painter(ImGui.GetWindowDrawList(), center, radius, color);
        }
        else
        {
            AppSkin.Icon(center, IconGlyph.Of(FontAwesomeIcon.Image), color, 0.95f);
        }

        HoverTooltip.Show(new Rect(min, max), tooltip, HoverLabelSide.Above);
        if (!hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        panelOpen = !panelOpen;
        if (panelOpen)
        {
            pickerPaths = library.List();
            notice = string.Empty;
            emoji.Close();
        }
    }

    public void DrawStrip(Rect strip, PhoneTheme theme, WallpaperImageCache wallpaperImages)
    {
        if (path is null)
        {
            return;
        }

        UiInteract.HoverOverlay(strip);
        var scale = UiScale.Current;
        ImGui.SetCursorScreenPos(strip.Min);
        using var host = ImRaii.Child("##commentAttachmentStrip", strip.Size, false,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoBackground);
        if (!host)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var background = theme.AppBackground;
        drawList.AddRectFilled(strip.Min, strip.Max,
            ImGui.GetColorU32(new Vector4(background.X, background.Y, background.Z, 1f)));
        drawList.AddLine(strip.Min, new Vector2(strip.Max.X, strip.Min.Y), ImGui.GetColorU32(theme.Separator), 1f);
        var pad = 6f * scale;
        var tile = strip.Height - pad * 2f;
        var min = new Vector2(strip.Min.X + 12f * scale, strip.Min.Y + pad);
        var max = min + new Vector2(tile, tile);
        var rounding = 8f * scale;
        var texture = wallpaperImages.Get(path);
        if (texture is null)
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(theme.SurfaceMuted));
        }
        else
        {
            var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
            drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding,
                ImDrawFlags.RoundCornersAll);
        }

        if (GifMedia.IsGif(path))
        {
            var label = Typography.Measure("GIF", TextStyles.FootnoteEmphasized);
            Typography.Draw(drawList, new Vector2(max.X + 8f * scale, (min.Y + max.Y - label.Y) * 0.5f), "GIF",
                theme.TextMuted, TextStyles.FootnoteEmphasized);
        }

        var badgeRadius = 8.5f * scale;
        var badgeCenter = new Vector2(max.X - badgeRadius - 2f * scale, min.Y + badgeRadius + 2f * scale);
        var badgeMin = badgeCenter - new Vector2(badgeRadius, badgeRadius);
        var badgeMax = badgeCenter + new Vector2(badgeRadius, badgeRadius);
        var badgeHovered = !UiInteract.InputBlocked && UiInteract.HoverWindowOnly(badgeMin, badgeMax);
        drawList.AddCircleFilled(badgeCenter, badgeRadius,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, badgeHovered ? 0.9f : 0.62f)), 20);
        AppSkin.Icon(badgeCenter, IconGlyph.Of(FontAwesomeIcon.Times), new Vector4(1f, 1f, 1f, 1f), 0.6f);
        if (badgeHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(badgeMin, badgeMax, badgeHovered))
        {
            path = null;
        }
    }

    public void DrawPanel(Rect panel, in AppSkin ui, PhoneTheme theme, WallpaperImageCache wallpaperImages,
        PhotoLibrary library)
    {
        if (!panelOpen)
        {
            return;
        }

        UiInteract.HoverOverlay(panel);
        var scale = UiScale.Current;
        ImGui.SetCursorScreenPos(panel.Min);
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero))
        using (var host = ImRaii.Child("##commentPhotoPanel", panel.Size, false,
                   ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoBackground))
        {
            if (!host)
            {
                return;
            }

            var drawList = ImGui.GetWindowDrawList();
            var background = theme.AppBackground;
            drawList.AddRectFilled(panel.Min, panel.Max,
                ImGui.GetColorU32(new Vector4(background.X, background.Y, background.Z, 1f)));
            drawList.AddLine(panel.Min, new Vector2(panel.Max.X, panel.Min.Y), ImGui.GetColorU32(theme.Separator), 1f);

            var pad = 10f * scale;
            var importHeight = 34f * scale;
            var importRect = new Rect(new Vector2(panel.Min.X + pad, panel.Min.Y + pad),
                new Vector2(panel.Max.X - pad, panel.Min.Y + pad + importHeight));
            var canPaste = intake.OfferPaste(ref importRect, out var pasteRect);
            if (DrawPill(drawList, importRect, Loc.T(L.Common.ImportFromPc), ui, theme))
            {
                FilePicker.PickImage(Loc.T(L.Common.AddPhoto),
                    picked => Interlocked.Exchange(ref pendingImport, picked));
            }

            if (canPaste && DrawPill(drawList, pasteRect, Loc.T(L.Common.PasteImage), ui, theme))
            {
                intake.Paste(library);
            }

            var noticeHeight = notice.Length > 0 ? 18f * scale : 0f;
            if (noticeHeight > 0f)
            {
                Typography.DrawCentered(new Vector2(panel.Center.X, importRect.Max.Y + 5f * scale), notice,
                    theme.TextMuted, TextStyles.Footnote);
            }

            var gridTop = importRect.Max.Y + 8f * scale + noticeHeight;
            var gridRect = new Rect(new Vector2(panel.Min.X, gridTop), panel.Max);
            ImGui.SetCursorScreenPos(gridRect.Min);
            using var grid = ImRaii.Child("##commentPhotoGrid", gridRect.Size, false);
            if (!grid)
            {
                return;
            }

            if (pickerPaths.Length == 0)
            {
                Typography.DrawCentered(new Vector2(gridRect.Center.X, gridRect.Min.Y + 40f * scale),
                    Loc.T(L.Common.NoPhotos), theme.TextMuted);
                return;
            }

            var gridDrawList = ImGui.GetWindowDrawList();
            var gap = 5f * scale;
            var available = gridRect.Width - pad * 2f;
            var cell = (available - gap * (PickerColumns - 1)) / PickerColumns;
            var origin = new Vector2(gridRect.Min.X + pad, ImGui.GetCursorScreenPos().Y);
            var scrollY = ImGui.GetScrollY();
            var viewHeight = gridRect.Height;
            var cullMargin = cell + 40f * scale;
            for (var index = 0; index < pickerPaths.Length; index++)
            {
                var column = index % PickerColumns;
                var rowIndex = index / PickerColumns;
                var rowTop = rowIndex * (cell + gap);
                if (rowTop + cell < scrollY - cullMargin || rowTop > scrollY + viewHeight + cullMargin)
                {
                    continue;
                }

                var min = new Vector2(origin.X + column * (cell + gap), origin.Y + rowTop);
                var max = new Vector2(min.X + cell, min.Y + cell);
                var hovered = !UiInteract.InputBlocked && UiInteract.HoverWindowOnly(min, max);
                DrawPickerThumb(gridDrawList, pickerPaths[index], min, max, scale, theme, hovered);
                if (UiInteract.Click(min, max, hovered))
                {
                    Take(pickerPaths[index]);
                }
            }

            var rows = (pickerPaths.Length + PickerColumns - 1) / PickerColumns;
            var totalHeight = rows * (cell + gap);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(available, totalHeight));

            void DrawPickerThumb(ImDrawListPtr thumbList, string thumbPath, Vector2 min, Vector2 max, float thumbScale,
                PhoneTheme thumbTheme, bool thumbHovered)
            {
                var rounding = 8f * thumbScale;
                var texture = wallpaperImages.Get(thumbPath);
                if (texture is null)
                {
                    Squircle.Fill(thumbList, min, max, rounding, ImGui.GetColorU32(thumbTheme.SurfaceMuted));
                    return;
                }

                var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
                thumbList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding,
                    ImDrawFlags.RoundCornersAll);
                if (thumbHovered)
                {
                    thumbList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.1f)), rounding);
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }
            }
        }
    }

    public float PanelHeight(float scale)
    {
        return panelOpen ? PanelHeightUnits * scale : 0f;
    }

    private static bool DrawPill(ImDrawListPtr drawList, Rect rect, string text, AppSkin ui, PhoneTheme theme)
    {
        var hovered = !UiInteract.InputBlocked && UiInteract.HoverWindowOnly(rect.Min, rect.Max);
        var fill = hovered ? Palette.Mix(ui.Accent, theme.TextStrong, 0.12f) : ui.Accent;
        Squircle.Fill(drawList, rect.Min, rect.Max, rect.Height * 0.5f, ImGui.GetColorU32(fill));
        var label = Typography.FitText(text, MathF.Max(1f, rect.Width - rect.Height), 0.95f, FontWeight.SemiBold);
        var labelSize = Typography.Measure(label, 0.95f, FontWeight.SemiBold);
        Typography.Draw(drawList, rect.Center - labelSize * 0.5f, label, new Vector4(1f, 1f, 1f, 1f), 0.95f,
            FontWeight.SemiBold);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private void Take(string picked)
    {
        if (GifMedia.IsGif(picked) && !GifMedia.FitsSizeCap(picked))
        {
            notice = Loc.T(L.Common.GifTooLarge);
            if (!panelOpen)
            {
                ShellToast.Show(notice);
            }

            return;
        }

        path = picked;
        panelOpen = false;
        notice = string.Empty;
    }
}
