using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Feedback;

internal sealed partial class FeedbackApp
{
    private const int PickerColumns = 3;
    private const float PickerGap = 4f;
    private const float PickerRadius = 6f;
    private const float PickerBadgeRadius = 11f;
    private const float PickerBadgeInset = 6f;
    private const float PickerRingThickness = 3f;
    private const float PickerFullAlpha = 0.4f;
    private const float PickerCullMargin = 60f;

    private static readonly string[] SelectionNumbers = { "1", "2", "3", "4", "5" };
    private static readonly Vector4 PickerBadgeIdle = new(0f, 0f, 0f, 0.35f);
    private static readonly Vector4 PickerBadgeStroke = new(1f, 1f, 1f, 0.9f);
    private static readonly Vector4 PickerShade = new(0f, 0f, 0f, 1f);

    private string[] pickerPaths = Array.Empty<string>();
    private string? pendingPickedPath;

    private void DrawPhotos(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context);
        var body = navBar.Body;
        using (AppSurface.Begin(body))
        {
            var scale = UiScale.Current;
            var width = ScrollLayout.StableContentWidth();
            if (pickerPaths.Length == 0)
            {
                DrawEmptyGallery(body, width, scale);
            }
            else
            {
                DrawPickerGrid(width, scale);
            }
        }

        photosButtons[0] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.FolderOpen), Loc.T(L.Common.ImportFromPc));
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "feedback.photos.nav", Loc.T(L.Feedback.AddPhotos),
            NavBarStyle.From(ui), photosButtons, Loc.T(L.Feedback.NewSection), back);
        if (pressed == 0)
        {
            LaunchFileDialog();
        }
    }

    private void DrawEmptyGallery(Rect body, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var title = Loc.T(L.Common.NoPhotos);
        var hint = Loc.T(L.Feedback.NoPhotosHint);
        var height = FeedbackArt.StatePanelHeight(title, hint, true, width, scale);
        var top = MathF.Max(origin.Y + Metrics.Space.Xxl * scale, body.Center.Y - height * 0.6f);
        if (FeedbackArt.StatePanel(ui, top, origin.X + width * 0.5f, width, FontAwesomeIcon.Images, ui.Accent, title,
                hint, Loc.T(L.Common.ImportFromPc), "feedback.photos.import"))
        {
            LaunchFileDialog();
        }

        ReserveTo(origin, width, top + height);
    }

    private void DrawPickerGrid(float width, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var gap = PickerGap * scale;
        var cell = (width - gap * (PickerColumns - 1)) / PickerColumns;
        var origin = ImGui.GetCursorScreenPos();
        var margin = PickerCullMargin * scale;
        var visibleTop = ImGui.GetWindowPos().Y - margin;
        var visibleBottom = ImGui.GetWindowPos().Y + ImGui.GetWindowSize().Y + margin;
        var radius = PickerRadius * scale;
        for (var index = 0; index < pickerPaths.Length; index++)
        {
            var column = index % PickerColumns;
            var row = index / PickerColumns;
            var rowTop = origin.Y + row * (cell + gap);
            if (rowTop + cell < visibleTop || rowTop > visibleBottom)
            {
                continue;
            }

            var min = new Vector2(origin.X + column * (cell + gap), rowTop);
            var max = new Vector2(min.X + cell, min.Y + cell);
            DrawPickerCell(drawList, pickerPaths[index], min, max, radius, scale);
        }

        var rows = (pickerPaths.Length + PickerColumns - 1) / PickerColumns;
        ReserveTo(origin, width, origin.Y + rows * (cell + gap) + BottomBreathing * scale);
    }

    private void DrawPickerCell(ImDrawListPtr drawList, string path, Vector2 min, Vector2 max, float radius,
        float scale)
    {
        var selectedIndex = draft.IndexOf(path);
        var selected = selectedIndex >= 0;
        var blocked = !selected && draft.IsFull;
        var hovered = UiInteract.Hover(min, max);
        var texture = wallpaperImages.Get(path);
        if (texture is null)
        {
            Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(ui.FieldSurface));
        }
        else
        {
            var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
            Squircle.FillImage(drawList, min, max, radius, texture.Handle, 0xFFFFFFFFu, uv0, uv1);
        }

        if (blocked)
        {
            Squircle.Fill(drawList, min, max, radius,
                ImGui.GetColorU32(PickerShade with { W = 1f - PickerFullAlpha }));
        }
        else if (hovered)
        {
            Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(ui.HoverTint));
        }

        var badgeRadius = PickerBadgeRadius * scale;
        var badgeCenter = new Vector2(max.X - badgeRadius - PickerBadgeInset * scale,
            min.Y + badgeRadius + PickerBadgeInset * scale);
        if (selected)
        {
            Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(ui.Accent), PickerRingThickness * scale);
            drawList.AddCircleFilled(badgeCenter, badgeRadius, ImGui.GetColorU32(ui.Accent), 24);
            Typography.DrawCentered(drawList, badgeCenter, SelectionNumbers[selectedIndex], AccentRing.Ink,
                TextStyles.FootnoteEmphasized);
        }
        else if (!blocked)
        {
            drawList.AddCircleFilled(badgeCenter, badgeRadius, ImGui.GetColorU32(PickerBadgeIdle), 24);
            drawList.AddCircle(badgeCenter, badgeRadius, ImGui.GetColorU32(PickerBadgeStroke), 24,
                Metrics.Stroke.Thin * scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!UiInteract.Click(min, max, hovered))
        {
            return;
        }

        if (selected)
        {
            draft.RemoveAt(selectedIndex);
            attachmentCountCache = -1;
            return;
        }

        if (blocked)
        {
            ShellToast.Show(Loc.T(L.Common.PhotoLimit, FeedbackDraft.MaxAttachments));
            return;
        }

        draft.Add(path);
        attachmentCountCache = -1;
    }

    private void LaunchFileDialog()
    {
        FilePicker.PickImage(Loc.T(L.Feedback.AddPhotos), path => Interlocked.Exchange(ref pendingPickedPath, path));
    }

    private void ConsumePickedFile()
    {
        var picked = Interlocked.Exchange(ref pendingPickedPath, null);
        if (string.IsNullOrEmpty(picked))
        {
            return;
        }

        if (draft.IsFull)
        {
            ShellToast.Show(Loc.T(L.Common.PhotoLimit, FeedbackDraft.MaxAttachments));
            return;
        }

        draft.Add(picked);
        attachmentCountCache = -1;
        if (router.Current.Screen == FeedbackScreen.Photos)
        {
            router.Pop();
        }
    }
}
