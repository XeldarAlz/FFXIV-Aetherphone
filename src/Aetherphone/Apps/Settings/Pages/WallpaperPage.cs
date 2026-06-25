using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class WallpaperPage : ISettingsPage
{
    private const int Columns = 3;

    private const int PhotoColumns = 3;

    private enum Overlay
    {
        None,
        Sheet,
        Photos,
    }

    public string Title => Loc.T(L.Wallpaper.Title);

    public string Summary => string.Empty;

    public string Glyph => "W";

    public Vector4 Tint => new(0.55f, 0.45f, 0.95f, 1f);

    private readonly Configuration configuration;
    private readonly ThemeProvider themes;
    private readonly ISettingsNavigator navigator;
    private readonly PhotoLibrary photos;
    private readonly Action<string> assign;

    private Overlay overlay = Overlay.None;
    private string[] photoPaths = Array.Empty<string>();
    private string? pendingFilePath;
    private bool editingDark;

    public WallpaperPage(Configuration configuration, ThemeProvider themes, ISettingsNavigator navigator, PhotoLibrary photos)
    {
        this.configuration = configuration;
        this.themes = themes;
        this.navigator = navigator;
        this.photos = photos;
        assign = Assign;
        editingDark = Plugin.Wallpapers.Darkness >= 0.5f;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        Plugin.Wallpapers.StepDayNight(MathF.Min(ImGui.GetIO().DeltaTime, 0.1f));

        var picked = Interlocked.Exchange(ref pendingFilePath, null);
        if (!string.IsNullOrEmpty(picked))
        {
            overlay = Overlay.None;
            navigator.Open(new WallpaperCropPage(picked, navigator, assign));
            return;
        }

        var theme = context.Theme;
        var overlayAtFrameStart = overlay;
        var interactive = overlay == Overlay.None;
        var scale = ImGuiHelpers.GlobalScale;

        var previewHeight = 198f * scale;
        var previewRegion = new Rect(body.Min, new Vector2(body.Max.X, body.Min.Y + previewHeight));
        var gridRegion = new Rect(new Vector2(body.Min.X, body.Min.Y + previewHeight), body.Max);

        DrawAppearancePreviews(previewRegion, theme, interactive);
        DrawGrid(gridRegion, theme, interactive);

        if (overlay == Overlay.None)
        {
            return;
        }

        ImGui.SetCursorScreenPos(body.Min);
        using (ImRaii.Child("##wallpaperOverlay", body.Size, false, ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            switch (overlay)
            {
                case Overlay.Sheet:
                    DrawSheet(body, theme, overlayAtFrameStart == Overlay.Sheet);
                    break;
                case Overlay.Photos:
                    DrawPhotoPicker(body, theme);
                    break;
            }
        }
    }

    private void DrawAppearancePreviews(Rect region, PhoneTheme theme, bool interactive)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var aspect = TileAspect();
        var cardHeight = 148f * scale;
        var cardWidth = cardHeight * aspect;
        var gap = 30f * scale;
        var totalWidth = cardWidth * 2f + gap;
        var startX = region.Center.X - totalWidth * 0.5f;
        var top = region.Min.Y + 12f * scale;

        var lightRect = new Rect(new Vector2(startX, top), new Vector2(startX + cardWidth, top + cardHeight));
        var darkRect = new Rect(new Vector2(startX + cardWidth + gap, top), new Vector2(startX + totalWidth, top + cardHeight));

        DrawAppearanceCard(lightRect, false, configuration.LightWallpaperId, Loc.T(L.Wallpaper.Light), theme, interactive);
        DrawAppearanceCard(darkRect, true, configuration.DarkWallpaperId, Loc.T(L.Wallpaper.Dark), theme, interactive);
    }

    private void DrawAppearanceCard(Rect rect, bool isDark, string selectedId, string label, PhoneTheme theme, bool interactive)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var rounding = 22f * scale;
        var active = editingDark == isDark;
        var entry = Plugin.Wallpapers.Resolve(selectedId);

        if (active)
        {
            Elevation.Floating(dl, rect.Min, rect.Max, rounding, scale, 0.7f);
        }

        WallpaperRenderer.DrawSingle(dl, rect, rounding, entry, TileAspect(), 1f, theme.SurfaceMuted);
        Squircle.Stroke(dl, rect.Min, rect.Max, rounding, ImGui.GetColorU32(active ? theme.Accent : theme.Separator), (active ? 2.5f : 1f) * scale);

        var labelColor = active ? theme.Accent : theme.TextMuted;
        Typography.DrawCentered(new Vector2(rect.Center.X, rect.Max.Y + 14f * scale), label, labelColor, 0.95f, active ? FontWeight.SemiBold : FontWeight.Regular);

        if (!interactive)
        {
            return;
        }

        if (ImGui.IsMouseHoveringRect(rect.Min, new Vector2(rect.Max.X, rect.Max.Y + 24f * scale)))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                editingDark = isDark;
            }
        }
    }

    private void DrawGrid(Rect region, PhoneTheme theme, bool interactive)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var aspect = TileAspect();
        var entries = Plugin.Wallpapers.Entries;
        var gap = 10f * scale;

        using (AppSurface.Begin(region))
        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(gap, gap)))
        {
            var cellWidth = (ImGui.GetContentRegionAvail().X - gap * (Columns - 1)) / Columns;
            var cellHeight = aspect > 0f ? cellWidth / aspect : cellWidth;
            var tileCount = entries.Count + 1;

            for (var index = 0; index < tileCount; index++)
            {
                using (ImRaii.PushId(index))
                {
                    var clicked = ImGui.InvisibleButton("tile", new Vector2(cellWidth, cellHeight)) && interactive;
                    var min = ImGui.GetItemRectMin();
                    var max = ImGui.GetItemRectMax();

                    if (index < entries.Count)
                    {
                        DrawWallpaperTile(entries[index], min, max, theme, interactive, clicked);
                    }
                    else
                    {
                        DrawAddTile(min, max, theme, interactive && clicked);
                    }
                }

                if (index % Columns != Columns - 1)
                {
                    ImGui.SameLine();
                }
            }
        }
    }

    private void DrawWallpaperTile(WallpaperEntry entry, Vector2 min, Vector2 max, PhoneTheme theme, bool interactive, bool clicked)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var rounding = 16f * scale;
        var rect = new Rect(min, max);
        var selected = entry.Id == ActiveSelectedId();

        WallpaperRenderer.DrawSingle(dl, rect, rounding, entry, TileAspect(), 1f, theme.SurfaceMuted);
        dl.AddRect(min, max, ImGui.GetColorU32(selected ? theme.Accent : theme.Separator), rounding, ImDrawFlags.RoundCornersAll, selected ? 2.5f * scale : 1f);

        if (entry.Kind == WallpaperKind.Custom && interactive && DrawDeleteBadge(new Vector2(max.X - 14f * scale, min.Y + 14f * scale), theme))
        {
            RemoveCustom(entry.Id);
            return;
        }

        if (clicked && !selected)
        {
            Assign(entry.Id);
        }
    }

    private void DrawAddTile(Vector2 min, Vector2 max, PhoneTheme theme, bool clicked)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var rounding = 16f * scale;
        var hovered = ImGui.IsItemHovered();

        Squircle.Fill(dl, min, max, rounding, ImGui.GetColorU32(theme.SurfaceMuted with { W = hovered ? 0.55f : 0.4f }));
        Squircle.Stroke(dl, min, max, rounding, ImGui.GetColorU32(theme.Separator), 1.4f * scale);

        var center = (min + max) * 0.5f;
        var arm = 13f * scale;
        var ink = ImGui.GetColorU32(theme.Accent);
        dl.AddLine(new Vector2(center.X - arm, center.Y), new Vector2(center.X + arm, center.Y), ink, 2.4f * scale);
        dl.AddLine(new Vector2(center.X, center.Y - arm), new Vector2(center.X, center.Y + arm), ink, 2.4f * scale);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (clicked)
        {
            overlay = Overlay.Sheet;
        }
    }

    private static bool DrawDeleteBadge(Vector2 center, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var radius = 11f * scale;
        var hovered = ImGui.IsMouseHoveringRect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));

        dl.AddCircleFilled(center, radius, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, hovered ? 0.72f : 0.55f)), 24);
        var arm = 4f * scale;
        var ink = ImGui.GetColorU32(hovered ? theme.Danger : new Vector4(1f, 1f, 1f, 0.9f));
        dl.AddLine(new Vector2(center.X - arm, center.Y - arm), new Vector2(center.X + arm, center.Y + arm), ink, 1.8f * scale);
        dl.AddLine(new Vector2(center.X - arm, center.Y + arm), new Vector2(center.X + arm, center.Y - arm), ink, 1.8f * scale);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void DrawSheet(Rect body, PhoneTheme theme, bool canDismiss)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(body.Min, body.Max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.5f)), 0f);

        var rowHeight = 52f * scale;
        var pad = 14f * scale;
        var width = body.Width - pad * 2f;
        var left = body.Min.X + pad;
        var right = left + width;
        var cancelTop = body.Max.Y - pad - rowHeight;
        var optionsBottom = cancelTop - 10f * scale;
        var optionsTop = optionsBottom - rowHeight * 2f;

        var cardMin = new Vector2(left, optionsTop);
        var cardMax = new Vector2(right, cancelTop + rowHeight);
        var overCard = ImGui.IsMouseHoveringRect(cardMin, cardMax);
        if (canDismiss && !overCard && ImGui.IsMouseHoveringRect(body.Min, body.Max) && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            overlay = Overlay.None;
            return;
        }

        Elevation.Floating(dl, new Vector2(left, optionsTop), new Vector2(right, optionsBottom), 16f * scale, scale, 0.9f);
        Squircle.Fill(dl, new Vector2(left, optionsTop), new Vector2(right, optionsBottom), 16f * scale, ImGui.GetColorU32(theme.GroupedCard));
        Material.EdgeSquircle(dl, new Vector2(left, optionsTop), new Vector2(right, optionsBottom), 16f * scale, scale);

        var photosRow = new Rect(new Vector2(left, optionsTop), new Vector2(right, optionsTop + rowHeight));
        var filesRow = new Rect(new Vector2(left, optionsTop + rowHeight), new Vector2(right, optionsBottom));
        dl.AddLine(new Vector2(left + 16f * scale, optionsTop + rowHeight), new Vector2(right - 4f * scale, optionsTop + rowHeight), ImGui.GetColorU32(theme.Separator), 1f);

        if (DrawSheetRow(photosRow, Loc.T(L.Wallpaper.FromPhotos), theme.Accent))
        {
            photoPaths = photos.List();
            overlay = Overlay.Photos;
        }

        if (DrawSheetRow(filesRow, Loc.T(L.Wallpaper.FromFiles), theme.Accent))
        {
            overlay = Overlay.None;
            LaunchFileDialog();
        }

        var cancelRect = new Rect(new Vector2(left, cancelTop), new Vector2(right, cancelTop + rowHeight));
        Squircle.Fill(dl, cancelRect.Min, cancelRect.Max, 16f * scale, ImGui.GetColorU32(theme.GroupedCard));
        Material.EdgeSquircle(dl, cancelRect.Min, cancelRect.Max, 16f * scale, scale);
        if (DrawSheetRow(cancelRect, Loc.T(L.Common.Cancel), theme.TextMuted))
        {
            overlay = Overlay.None;
        }
    }

    private static bool DrawSheetRow(Rect rect, string label, Vector4 color)
    {
        var hovered = ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        if (hovered)
        {
            ImGui.GetWindowDrawList().AddRectFilled(rect.Min, rect.Max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.06f)), 16f * ImGuiHelpers.GlobalScale);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Typography.DrawCentered(rect.Center, label, color, 1.0f, FontWeight.Medium);
        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void DrawPhotoPicker(Rect body, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(body.Min, body.Max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.5f)), 0f);

        var pad = 10f * scale;
        var panel = body.Inset(pad);
        Squircle.Fill(dl, panel.Min, panel.Max, 18f * scale, ImGui.GetColorU32(theme.AppBackground));
        Material.EdgeSquircle(dl, panel.Min, panel.Max, 18f * scale, scale);

        var headerHeight = 38f * scale;
        Typography.DrawCentered(new Vector2(panel.Center.X, panel.Min.Y + headerHeight * 0.5f), Loc.T(L.Wallpaper.FromPhotos), theme.TextStrong, 1.05f, FontWeight.SemiBold);
        if (DrawCloseBadge(new Vector2(panel.Max.X - 18f * scale, panel.Min.Y + headerHeight * 0.5f), theme))
        {
            overlay = Overlay.None;
            return;
        }

        var grid = new Rect(new Vector2(panel.Min.X + pad, panel.Min.Y + headerHeight), new Vector2(panel.Max.X - pad, panel.Max.Y - pad));
        if (photoPaths.Length == 0)
        {
            Typography.DrawCentered(grid.Center, Loc.T(L.Photos.NoPhotos), theme.TextMuted, 1.0f);
            return;
        }

        ImGui.SetCursorScreenPos(grid.Min);
        var gap = 6f * scale;
        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(gap, gap)))
        using (var child = ImRaii.Child("##wallpaperPhotos", grid.Size, false, ImGuiWindowFlags.NoBackground))
        {
            if (!child)
            {
                return;
            }

            var cell = (ImGui.GetContentRegionAvail().X - gap * (PhotoColumns - 1)) / PhotoColumns;
            for (var index = 0; index < photoPaths.Length; index++)
            {
                using (ImRaii.PushId(index))
                {
                    var clicked = ImGui.InvisibleButton("photo", new Vector2(cell, cell));
                    DrawPhotoThumb(photoPaths[index], ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), theme);
                    if (clicked)
                    {
                        overlay = Overlay.None;
                        navigator.Open(new WallpaperCropPage(photoPaths[index], navigator, assign));
                        return;
                    }
                }

                if (index % PhotoColumns != PhotoColumns - 1)
                {
                    ImGui.SameLine();
                }
            }
        }
    }

    private static void DrawPhotoThumb(string path, Vector2 min, Vector2 max, PhoneTheme theme)
    {
        var dl = ImGui.GetWindowDrawList();
        var rounding = 10f * ImGuiHelpers.GlobalScale;
        var texture = Plugin.WallpaperImages.Get(path);
        if (texture is null)
        {
            dl.AddRectFilled(min, max, ImGui.GetColorU32(theme.SurfaceMuted), rounding);
            return;
        }

        var (uv0, uv1) = CenterCrop(texture.Size);
        dl.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding, ImDrawFlags.RoundCornersAll);
        if (ImGui.IsItemHovered())
        {
            dl.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.1f)), rounding);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    private static bool DrawCloseBadge(Vector2 center, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var radius = 12f * scale;
        var hovered = ImGui.IsMouseHoveringRect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        var arm = 5f * scale;
        var ink = ImGui.GetColorU32(hovered ? theme.TextStrong : theme.TextMuted);
        dl.AddLine(new Vector2(center.X - arm, center.Y - arm), new Vector2(center.X + arm, center.Y + arm), ink, 2f * scale);
        dl.AddLine(new Vector2(center.X - arm, center.Y + arm), new Vector2(center.X + arm, center.Y - arm), ink, 2f * scale);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void LaunchFileDialog()
    {
        _ = NativeFileDialog.OpenImageAsync(Loc.T(L.Wallpaper.Add)).ContinueWith(task =>
        {
            if (task.Status == TaskStatus.RanToCompletion && !string.IsNullOrEmpty(task.Result))
            {
                Interlocked.Exchange(ref pendingFilePath, task.Result);
            }
        });
    }

    private string ActiveSelectedId() => editingDark ? configuration.DarkWallpaperId : configuration.LightWallpaperId;

    private void Assign(string id)
    {
        if (editingDark)
        {
            configuration.DarkWallpaperId = id;
        }
        else
        {
            configuration.LightWallpaperId = id;
        }

        themes.Apply(configuration);
        configuration.Save();
    }

    private void RemoveCustom(string id)
    {
        Plugin.Wallpapers.RemoveCustom(id);
        var entries = Plugin.Wallpapers.Entries;
        var fallback = entries.Count > 0 ? entries[0].Id : string.Empty;

        var changed = false;
        if (configuration.LightWallpaperId == id)
        {
            configuration.LightWallpaperId = fallback;
            changed = true;
        }

        if (configuration.DarkWallpaperId == id)
        {
            configuration.DarkWallpaperId = fallback;
            changed = true;
        }

        if (changed)
        {
            themes.Apply(configuration);
            configuration.Save();
        }
    }

    private static float TileAspect()
    {
        var aspect = Plugin.Wallpapers.CurrentTargetAspect;
        return aspect > 0.1f ? aspect : 0.5f;
    }

    private static (Vector2 Uv0, Vector2 Uv1) CenterCrop(Vector2 size)
    {
        if (size.X <= 0f || size.Y <= 0f)
        {
            return (Vector2.Zero, Vector2.One);
        }

        var aspect = size.X / size.Y;
        if (aspect > 1f)
        {
            var inset = (1f - 1f / aspect) * 0.5f;
            return (new Vector2(inset, 0f), new Vector2(1f - inset, 1f));
        }

        if (aspect < 1f)
        {
            var inset = (1f - aspect) * 0.5f;
            return (new Vector2(0f, inset), new Vector2(1f, 1f - inset));
        }

        return (Vector2.Zero, Vector2.One);
    }
}
