using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Camera;

internal sealed class CameraApp : IPhoneApp
{
    private const float TopBarHeight = 64f;
    private const float TrayHeight = 172f;
    private const float ShutterRadius = 34f;
    private const float FlashDuration = 0.42f;
    private const float ReticleDuration = 1.1f;
    private const float PressDuration = 0.18f;
    private const int SquareModeIndex = 0;
    private static readonly LocString[] Modes = { L.Camera.ModeSquare, L.Camera.ModePhoto, L.Camera.ModePano };
    private static readonly Vector4 SelectedMode = new(0.98f, 0.79f, 0.20f, 1f);
    private static readonly Vector4 ShutterRing = new(0.98f, 0.98f, 0.98f, 1f);
    private static readonly Vector4 BarTint = new(0f, 0f, 0f, 0.42f);
    private static readonly Vector4 TrayTint = new(0f, 0f, 0f, 0.88f);
    public string Id => "camera";
    public string DisplayName => Loc.T(L.Apps.Camera);
    public string Glyph => "O";
    public Vector4 Accent => new(0.34f, 0.35f, 0.41f, 1f);
    public int BadgeCount => 0;
    public bool WantsTransparentScreen => true;
    private readonly PhotoCaptureService capture;
    private readonly PhotoLibrary library;
    private int modeIndex = 1;
    private bool gridEnabled;
    private bool flashEnabled;
    private float shutterPress;
    private float flashAge = FlashDuration + 1f;
    private float reticleAge = ReticleDuration + 1f;
    private Vector2 reticlePos;
    private IDalamudTextureWrap? lastShot;

    public CameraApp(PhotoCaptureService capture, PhotoLibrary library)
    {
        this.capture = capture;
        this.library = library;
    }

    public void OnOpened()
    {
        flashAge = FlashDuration + 1f;
        reticleAge = ReticleDuration + 1f;
        shutterPress = 0f;
    }

    public void OnClosed()
    {
    }

    public void Draw(in PhoneContext context)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var delta = ImGui.GetIO().DeltaTime;
        AdvanceTimers(delta);
        var screen = ScreenFrom(context.Content, theme, scale);
        var viewfinder = new Rect(new Vector2(screen.Min.X, screen.Min.Y + TopBarHeight * scale),
            new Vector2(screen.Max.X, screen.Max.Y - TrayHeight * scale));
        var captureRect = CaptureRect(viewfinder);
        var consumed = DrawTopBar(screen, scale);
        DrawViewfinder(viewfinder, captureRect, scale);
        consumed |= DrawTray(screen, captureRect, context.Navigation, scale);
        HandleFocusTap(viewfinder, consumed);
        DrawFlash(screen, scale);
    }

    private void AdvanceTimers(float delta)
    {
        if (shutterPress > 0f)
        {
            shutterPress = MathF.Max(0f, shutterPress - delta / PressDuration);
        }

        if (flashAge <= FlashDuration)
        {
            flashAge += delta;
        }

        if (reticleAge <= ReticleDuration)
        {
            reticleAge += delta;
        }
    }

    private bool DrawTopBar(Rect screen, float scale)
    {
        var dl = ImGui.GetWindowDrawList();
        var barMax = new Vector2(screen.Max.X, screen.Min.Y + TopBarHeight * scale);
        dl.AddRectFilled(screen.Min, barMax, ImGui.GetColorU32(BarTint));
        var rowCenterY = barMax.Y - 16f * scale;
        var consumed = DrawFlashToggle(new Vector2(screen.Min.X + 28f * scale, rowCenterY), scale);
        DrawLiveBadge(new Vector2(screen.Max.X - 34f * scale, rowCenterY), scale);
        return consumed;
    }

    private bool DrawFlashToggle(Vector2 center, float scale)
    {
        var dl = ImGui.GetWindowDrawList();
        var radius = 15f * scale;
        var hovered =
            ImGui.IsMouseHoveringRect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        var tint = flashEnabled ? SelectedMode : new Vector4(0.92f, 0.92f, 0.94f, 0.9f);
        if (hovered)
        {
            dl.AddCircleFilled(center, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.12f)), 24);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        DrawBolt(dl, center, 9f * scale, ImGui.GetColorU32(tint));
        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            flashEnabled = !flashEnabled;
            return true;
        }

        return false;
    }

    private static void DrawBolt(ImDrawListPtr dl, Vector2 center, float extent, uint color)
    {
        Span<Vector2> bolt = stackalloc Vector2[6]
        {
            new Vector2(center.X + extent * 0.35f, center.Y - extent),
            new Vector2(center.X - extent * 0.55f, center.Y + extent * 0.2f),
            new Vector2(center.X - extent * 0.02f, center.Y + extent * 0.2f),
            new Vector2(center.X - extent * 0.35f, center.Y + extent),
            new Vector2(center.X + extent * 0.55f, center.Y - extent * 0.2f),
            new Vector2(center.X + extent * 0.02f, center.Y - extent * 0.2f),
        };
        dl.PathClear();
        for (var index = 0; index < bolt.Length; index++)
        {
            dl.PathLineTo(bolt[index]);
        }

        dl.PathFillConvex(color);
    }

    private static void DrawLiveBadge(Vector2 center, float scale)
    {
        var dl = ImGui.GetWindowDrawList();
        var radius = 14f * scale;
        dl.AddCircle(center, radius, ImGui.GetColorU32(new Vector4(0.92f, 0.92f, 0.94f, 0.55f)), 28, 1.4f * scale);
        Typography.DrawCentered(center, Loc.T(L.Common.Live), new Vector4(0.92f, 0.92f, 0.94f, 0.85f), 0.55f);
    }

    private void DrawViewfinder(Rect viewfinder, Rect captureRect, float scale)
    {
        var dl = ImGui.GetWindowDrawList();
        if (captureRect.Min.Y > viewfinder.Min.Y + 0.5f)
        {
            var crop = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.78f));
            dl.AddRectFilled(viewfinder.Min, new Vector2(viewfinder.Max.X, captureRect.Min.Y), crop);
            dl.AddRectFilled(new Vector2(viewfinder.Min.X, captureRect.Max.Y), viewfinder.Max, crop);
        }

        if (gridEnabled)
        {
            DrawGrid(dl, captureRect, scale);
        }

        DrawReticle(dl, scale);
    }

    private static void DrawGrid(ImDrawListPtr dl, Rect area, float scale)
    {
        var line = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.28f));
        var thickness = 1f * scale;
        var thirdX = area.Width / 3f;
        var thirdY = area.Height / 3f;
        for (var step = 1; step <= 2; step++)
        {
            var x = area.Min.X + thirdX * step;
            dl.AddLine(new Vector2(x, area.Min.Y), new Vector2(x, area.Max.Y), line, thickness);
            var y = area.Min.Y + thirdY * step;
            dl.AddLine(new Vector2(area.Min.X, y), new Vector2(area.Max.X, y), line, thickness);
        }
    }

    private void DrawReticle(ImDrawListPtr dl, float scale)
    {
        if (reticleAge > ReticleDuration)
        {
            return;
        }

        var grow = Math.Clamp(reticleAge / 0.22f, 0f, 1f);
        var fade = reticleAge < 0.7f ? 1f : MathF.Max(0f, 1f - (reticleAge - 0.7f) / (ReticleDuration - 0.7f));
        var half = (40f - 8f * grow) * scale;
        var color = ImGui.GetColorU32(new Vector4(0.98f, 0.79f, 0.20f, 0.9f * fade));
        var min = reticlePos - new Vector2(half, half);
        var max = reticlePos + new Vector2(half, half);
        dl.AddRect(min, max, color, 2f * scale, ImDrawFlags.RoundCornersAll, 1.6f * scale);
        var tick = 6f * scale;
        dl.AddLine(new Vector2(reticlePos.X, min.Y), new Vector2(reticlePos.X, min.Y + tick), color, 1.6f * scale);
        dl.AddLine(new Vector2(reticlePos.X, max.Y), new Vector2(reticlePos.X, max.Y - tick), color, 1.6f * scale);
        dl.AddLine(new Vector2(min.X, reticlePos.Y), new Vector2(min.X + tick, reticlePos.Y), color, 1.6f * scale);
        dl.AddLine(new Vector2(max.X, reticlePos.Y), new Vector2(max.X - tick, reticlePos.Y), color, 1.6f * scale);
    }

    private bool DrawTray(Rect screen, Rect captureRect, INavigator navigation, float scale)
    {
        var dl = ImGui.GetWindowDrawList();
        var trayTop = screen.Max.Y - TrayHeight * scale;
        dl.AddRectFilled(new Vector2(screen.Min.X, trayTop), screen.Max, ImGui.GetColorU32(TrayTint));
        var consumed = DrawModeCarousel(screen, trayTop + 22f * scale, scale);
        var shutterCenter = new Vector2(screen.Center.X, trayTop + 92f * scale);
        consumed |= DrawShutter(shutterCenter, captureRect, scale);
        consumed |= DrawThumbnailWell(new Vector2(screen.Min.X + 44f * scale, shutterCenter.Y), navigation, scale);
        consumed |= DrawGridToggle(new Vector2(screen.Max.X - 44f * scale, shutterCenter.Y), scale);
        return consumed;
    }

    private bool DrawModeCarousel(Rect screen, float rowCenterY, float scale)
    {
        var gap = 26f * scale;
        Span<float> widths = stackalloc float[Modes.Length];
        var total = 0f;
        for (var index = 0; index < Modes.Length; index++)
        {
            var modeScale = index == modeIndex ? 0.78f : 0.72f;
            widths[index] = Typography.Measure(Loc.T(Modes[index]), modeScale).X;
            total += widths[index];
            if (index > 0)
            {
                total += gap;
            }
        }

        var cursorX = screen.Center.X - total * 0.5f;
        var consumed = false;
        for (var index = 0; index < Modes.Length; index++)
        {
            var selected = index == modeIndex;
            var modeScale = selected ? 0.78f : 0.72f;
            var color = selected ? SelectedMode : new Vector4(0.82f, 0.82f, 0.85f, 0.75f);
            var labelCenter = new Vector2(cursorX + widths[index] * 0.5f, rowCenterY);
            Typography.DrawCentered(labelCenter, Loc.T(Modes[index]), color, modeScale);
            var hitMin = new Vector2(cursorX - gap * 0.4f, rowCenterY - 14f * scale);
            var hitMax = new Vector2(cursorX + widths[index] + gap * 0.4f, rowCenterY + 14f * scale);
            if (!selected && ImGui.IsMouseHoveringRect(hitMin, hitMax))
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    modeIndex = index;
                    consumed = true;
                }
            }

            cursorX += widths[index] + gap;
        }

        return consumed;
    }

    private bool DrawShutter(Vector2 center, Rect captureRect, float scale)
    {
        var dl = ImGui.GetWindowDrawList();
        var outerRadius = ShutterRadius * scale;
        var hovered = ImGui.IsMouseHoveringRect(center - new Vector2(outerRadius, outerRadius),
            center + new Vector2(outerRadius, outerRadius));
        dl.AddCircle(center, outerRadius, ImGui.GetColorU32(ShutterRing), 48, 3f * scale);
        var innerRadius = (outerRadius - 6f * scale) * (1f - 0.16f * shutterPress);
        var innerTint = hovered ? new Vector4(0.86f, 0.86f, 0.88f, 1f) : ShutterRing;
        dl.AddCircleFilled(center, innerRadius, ImGui.GetColorU32(innerTint), 48);
        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            Shoot(captureRect);
            return true;
        }

        return false;
    }

    private void Shoot(Rect captureRect)
    {
        shutterPress = 1f;
        flashAge = 0f;
        if (!capture.TryCapture(captureRect, out var pixels, out var width, out var height))
        {
            return;
        }

        lastShot?.Dispose();
        lastShot = Plugin.TextureProvider.CreateFromRaw(RawImageSpecification.Rgba32(width, height), pixels,
            "Aetherphone.Photo.Last");
        library.Save(pixels, width, height);
    }

    private bool DrawThumbnailWell(Vector2 center, INavigator navigation, float scale)
    {
        var dl = ImGui.GetWindowDrawList();
        var half = 22f * scale;
        var min = center - new Vector2(half, half);
        var max = center + new Vector2(half, half);
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        var rounding = 8f * scale;
        if (lastShot is { } shot)
        {
            dl.AddImageRounded(shot.Handle, min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, rounding,
                ImDrawFlags.RoundCornersAll);
        }
        else
        {
            dl.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0.18f, 0.19f, 0.23f, 1f)), rounding);
            dl.AddRectFilled(min, new Vector2(max.X, center.Y), ImGui.GetColorU32(new Vector4(0.30f, 0.33f, 0.40f, 1f)),
                rounding, ImDrawFlags.RoundCornersTop);
        }

        dl.AddRect(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.18f)), rounding, ImDrawFlags.RoundCornersAll,
            1f * scale);
        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            navigation.Open("photos");
            return true;
        }

        return false;
    }

    private bool DrawGridToggle(Vector2 center, float scale)
    {
        var dl = ImGui.GetWindowDrawList();
        var radius = 19f * scale;
        var hovered =
            ImGui.IsMouseHoveringRect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        if (gridEnabled || hovered)
        {
            var bg = gridEnabled ? new Vector4(1f, 1f, 1f, 0.16f) : new Vector4(1f, 1f, 1f, 0.08f);
            dl.AddCircleFilled(center, radius, ImGui.GetColorU32(bg), 28);
        }

        var color = ImGui.GetColorU32(gridEnabled ? SelectedMode : new Vector4(0.9f, 0.9f, 0.92f, 0.85f));
        var extent = 9f * scale;
        var third = extent * 2f / 3f;
        for (var step = 1; step <= 2; step++)
        {
            var offset = -extent + third * step;
            dl.AddLine(new Vector2(center.X + offset, center.Y - extent),
                new Vector2(center.X + offset, center.Y + extent), color, 1.3f * scale);
            dl.AddLine(new Vector2(center.X - extent, center.Y + offset),
                new Vector2(center.X + extent, center.Y + offset), color, 1.3f * scale);
        }

        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            gridEnabled = !gridEnabled;
            return true;
        }

        return false;
    }

    private void HandleFocusTap(Rect viewfinder, bool consumed)
    {
        if (consumed || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        var mouse = ImGui.GetMousePos();
        if (!viewfinder.Contains(mouse))
        {
            return;
        }

        reticlePos = mouse;
        reticleAge = 0f;
    }

    private void DrawFlash(Rect screen, float scale)
    {
        if (flashAge > FlashDuration)
        {
            return;
        }

        var alpha = 0.85f * (1f - flashAge / FlashDuration);
        ImGui.GetWindowDrawList()
            .AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)));
    }

    private Rect CaptureRect(Rect viewfinder)
    {
        if (modeIndex != SquareModeIndex)
        {
            return viewfinder;
        }

        var side = MathF.Min(viewfinder.Width, viewfinder.Height);
        var center = viewfinder.Center;
        var half = new Vector2(side * 0.5f, side * 0.5f);
        return new Rect(center - half, center + half);
    }

    private static Rect ScreenFrom(Rect content, PhoneTheme theme, float scale)
    {
        var min = new Vector2(content.Min.X - theme.SidePadding * scale, content.Min.Y - theme.TopZoneHeight * scale);
        var max = new Vector2(content.Max.X + theme.SidePadding * scale,
            content.Max.Y + theme.BottomZoneHeight * scale);
        return new Rect(min, max);
    }

    public void Dispose()
    {
        lastShot?.Dispose();
        lastShot = null;
    }
}
