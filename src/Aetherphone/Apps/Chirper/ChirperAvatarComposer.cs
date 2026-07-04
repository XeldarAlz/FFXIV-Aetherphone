using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
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

namespace Aetherphone.Apps.Chirper;

internal sealed class ChirperAvatarComposer
{
    private const int GridColumns = 3;
    private const float CropSmoothTime = 0.10f;

    private readonly ChirperStore store;
    private readonly PhotoLibrary library;

    private PhoneTheme theme = PhoneTheme.Default;
    private Vector4 accent;

    private bool cropStage;
    private string sourcePath = string.Empty;
    private string[] pickerPaths = Array.Empty<string>();
    private string? pendingPickedPath;
    private volatile int outcome;
    private bool closeRequested;

    private Spring zoomSpring = new(1f);
    private Spring centerXSpring = new(0.5f);
    private Spring centerYSpring = new(0.5f);
    private float targetZoom = 1f;
    private float targetCenterX = 0.5f;
    private float targetCenterY = 0.5f;
    private bool cropDragging;
    private Vector2 cropLastDrag;

    public ChirperAvatarComposer(ChirperStore store, PhotoLibrary library)
    {
        this.store = store;
        this.library = library;
    }

    public void Open()
    {
        cropStage = false;
        sourcePath = string.Empty;
        pendingPickedPath = null;
        outcome = 0;
        closeRequested = false;
        pickerPaths = library.List();
    }

    public bool Draw(Rect area, in PhoneContext context, Vector4 accentColor)
    {
        theme = context.Theme;
        accent = accentColor;

        if (outcome == 1)
        {
            outcome = 0;
            return true;
        }

        if (outcome == 2)
        {
            outcome = 0;
        }

        if (closeRequested)
        {
            closeRequested = false;
            return true;
        }

        var picked = Interlocked.Exchange(ref pendingPickedPath, null);
        if (!string.IsNullOrEmpty(picked))
        {
            BeginCrop(picked);
        }

        if (cropStage)
        {
            DrawCrop(area, context);
        }
        else
        {
            DrawPick(area, context);
        }

        return false;
    }

    private void DrawPick(Rect area, in PhoneContext context)
    {
        AppHeader.Draw(context, Loc.T(L.Chirper.ChangePhoto), () => closeRequested = true);

        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var importHeight = 46f * scale;
        var importRect = new Rect(new Vector2(area.Min.X + 16f * scale, top + 8f * scale), new Vector2(area.Max.X - 16f * scale, top + 8f * scale + importHeight));
        if (PillButton(importRect, Loc.T(L.Chirper.ImportFromPc), true))
        {
            LaunchFileDialog();
        }

        var gridRect = new Rect(new Vector2(area.Min.X, importRect.Max.Y + 12f * scale), area.Max);
        using (AppSurface.Begin(gridRect))
        {
            if (pickerPaths.Length == 0)
            {
                Typography.DrawCentered(new Vector2(gridRect.Center.X, gridRect.Min.Y + 60f * scale), Loc.T(L.Photos.NoPhotos), theme.TextMuted);
                return;
            }

            var gap = 6f * scale;
            var cell = (ImGui.GetContentRegionAvail().X - gap * (GridColumns - 1)) / GridColumns;
            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(gap, gap)))
            {
                for (var index = 0; index < pickerPaths.Length; index++)
                {
                    using (ImRaii.PushId(index))
                    {
                        var clicked = ImGui.InvisibleButton("pick", new Vector2(cell, cell));
                        DrawLocalThumbnail(pickerPaths[index], ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), scale);
                        if (clicked)
                        {
                            BeginCrop(pickerPaths[index]);
                        }
                    }

                    if (index % GridColumns != GridColumns - 1)
                    {
                        ImGui.SameLine();
                    }
                }
            }
        }
    }

    private void DrawLocalThumbnail(string path, Vector2 min, Vector2 max, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 10f * scale;
        var texture = Plugin.WallpaperImages.Get(path);
        if (texture is null)
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(theme.SurfaceMuted));
            return;
        }

        var (uv0, uv1) = CenterCropSquare(texture.Size);
        drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding, ImDrawFlags.RoundCornersAll);
        if (ImGui.IsItemHovered())
        {
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.1f)), rounding);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    private void BeginCrop(string path)
    {
        sourcePath = path;
        targetZoom = 1f;
        targetCenterX = 0.5f;
        targetCenterY = 0.5f;
        zoomSpring.SnapTo(1f);
        centerXSpring.SnapTo(0.5f);
        centerYSpring.SnapTo(0.5f);
        cropDragging = false;
        cropStage = true;
    }

    private void DrawCrop(Rect area, in PhoneContext context)
    {
        AppHeader.Draw(context, Loc.T(L.Chirper.MoveAndScale), () => cropStage = false);

        var canUse = !store.AvatarBusy;
        if (HeaderAction(area, store.AvatarBusy ? Loc.T(L.Chirper.Saving) : Loc.T(L.Chirper.Use), canUse))
        {
            Commit();
        }

        var scale = ImGuiHelpers.GlobalScale;
        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        var drawList = ImGui.GetWindowDrawList();
        var top = area.Min.Y + AppHeader.Height * scale;

        var stage = new Rect(new Vector2(area.Min.X + 16f * scale, top + 12f * scale), new Vector2(area.Max.X - 16f * scale, area.Max.Y - 96f * scale));
        var side = MathF.Min(stage.Width, stage.Height);
        var preview = new Rect(new Vector2(stage.Center.X - side * 0.5f, stage.Center.Y - side * 0.5f), new Vector2(stage.Center.X + side * 0.5f, stage.Center.Y + side * 0.5f));
        var rounding = side * 0.5f;

        var texture = Plugin.WallpaperImages.Get(sourcePath);
        if (texture is null)
        {
            Squircle.Fill(drawList, preview.Min, preview.Max, rounding, ImGui.GetColorU32(theme.SurfaceMuted));
            Typography.DrawCentered(preview.Center, Loc.T(L.Common.Loading), theme.TextMuted);
            return;
        }

        var size = texture.Size;
        var zoom = zoomSpring.Step(targetZoom, CropSmoothTime, deltaSeconds);
        var centerX = centerXSpring.Step(targetCenterX, CropSmoothTime, deltaSeconds);
        var centerY = centerYSpring.Step(targetCenterY, CropSmoothTime, deltaSeconds);
        var crop = new WallpaperCrop(zoom, centerX, centerY).Clamped(size, 1f);
        var (uv0, uv1) = crop.ComputeUv(size, 1f);

        drawList.AddImageRounded(texture.Handle, preview.Min, preview.Max, uv0, uv1, 0xFFFFFFFFu, rounding, ImDrawFlags.RoundCornersAll);
        HandleCropGestures(preview, size, uv1 - uv0);

        Typography.DrawCentered(new Vector2(area.Center.X, area.Max.Y - 70f * scale), Loc.T(L.Chirper.GestureHint), theme.TextMuted, 0.78f);
        var trackWidth = area.Width * 0.62f;
        var track = new Rect(new Vector2(area.Center.X - trackWidth * 0.5f, area.Max.Y - 48f * scale), new Vector2(area.Center.X + trackWidth * 0.5f, area.Max.Y - 44f * scale));
        var zoomNormalized = (targetZoom - WallpaperCrop.MinZoom) / (WallpaperCrop.MaxZoom - WallpaperCrop.MinZoom);
        var updated = Scrubber.Draw(track, zoomNormalized, accent, theme.SurfaceMuted, 1f);
        targetZoom = WallpaperCrop.MinZoom + updated * (WallpaperCrop.MaxZoom - WallpaperCrop.MinZoom);
    }

    private void HandleCropGestures(Rect preview, Vector2 size, Vector2 visible)
    {
        var hovering = ImGui.IsMouseHoveringRect(preview.Min, preview.Max);
        if (hovering)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            var wheel = ImGui.GetIO().MouseWheel;
            if (wheel != 0f)
            {
                targetZoom = Math.Clamp(targetZoom * (1f + wheel * 0.12f), WallpaperCrop.MinZoom, WallpaperCrop.MaxZoom);
            }
        }

        if (hovering && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            cropDragging = true;
            cropLastDrag = ImGui.GetMousePos();
        }

        if (cropDragging)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                var position = ImGui.GetMousePos();
                var delta = position - cropLastDrag;
                cropLastDrag = position;
                if (preview.Width > 0f && preview.Height > 0f)
                {
                    targetCenterX -= delta.X * visible.X / preview.Width;
                    targetCenterY -= delta.Y * visible.Y / preview.Height;
                }
            }
            else
            {
                cropDragging = false;
            }
        }

        var clamped = new WallpaperCrop(targetZoom, targetCenterX, targetCenterY).Clamped(size, 1f);
        targetZoom = clamped.Zoom;
        targetCenterX = clamped.CenterX;
        targetCenterY = clamped.CenterY;
    }

    private void Commit()
    {
        if (sourcePath.Length == 0 || store.AvatarBusy)
        {
            return;
        }

        var crop = new WallpaperCrop(targetZoom, targetCenterX, targetCenterY);
        store.UpdateAvatar(sourcePath, crop, ok => outcome = ok ? 1 : 2);
    }

    private void LaunchFileDialog()
    {
        _ = NativeFileDialog.OpenImageAsync(Loc.T(L.Chirper.ChangePhoto)).ContinueWith(task =>
        {
            if (task.Status == TaskStatus.RanToCompletion && !string.IsNullOrEmpty(task.Result))
            {
                Interlocked.Exchange(ref pendingPickedPath, task.Result);
            }
        });
    }

    private bool HeaderAction(Rect area, string label, bool enabled)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var height = 28f * scale;
        var width = Typography.Measure(label, 0.9f, FontWeight.SemiBold).X + 26f * scale;
        var max = new Vector2(area.Max.X - 12f * scale, area.Min.Y + AppHeader.Height * scale * 0.5f + height * 0.5f);
        var min = new Vector2(max.X - width, max.Y - height);
        return PillButton(new Rect(min, max), label, enabled) && enabled;
    }

    private bool PillButton(Rect rect, string label, bool filled)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var radius = rect.Height * 0.5f;

        var fill = filled
            ? (hovered ? Palette.Mix(accent, theme.TextStrong, 0.12f) : accent)
            : (hovered ? Palette.Mix(theme.GroupedCard, theme.TextStrong, 0.08f) : theme.GroupedCard);
        var ink = filled ? new Vector4(1f, 1f, 1f, 1f) : theme.TextStrong;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(fill));
        if (!filled)
        {
            Squircle.Stroke(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(theme.Separator), 1f);
        }

        var textSize = Typography.Measure(label, 0.9f, FontWeight.SemiBold);
        Typography.Draw(rect.Center - textSize * 0.5f, label, ink, 0.9f, FontWeight.SemiBold);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static (Vector2 Uv0, Vector2 Uv1) CenterCropSquare(Vector2 size)
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
