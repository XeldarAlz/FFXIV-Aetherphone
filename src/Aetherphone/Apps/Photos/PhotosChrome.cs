using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.Photos;

/// <summary>
/// Pure rendering for the Photos surface: thumbnails, viewer image, navigation glyphs and the empty
/// state. Texture loading, routing and delete logic stay in <see cref="PhotosApp"/>; interactive
/// glyphs return whether they were tapped.
/// </summary>
internal static class PhotosChrome
{
    private const float ThumbRounding = 10f;

    public static void Thumbnail(IDalamudTextureWrap? texture, Vector2 min, Vector2 max, bool hovered, float scale)
    {
        var dl = ImGui.GetWindowDrawList();
        var rounding = ThumbRounding * scale;
        if (texture is null)
        {
            dl.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0.14f, 0.15f, 0.18f, 1f)), rounding);
            dl.AddRect(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.06f)), rounding, ImDrawFlags.RoundCornersAll,
                Metrics.Stroke.Hairline);
            return;
        }

        var (uv0, uv1) = CenterCrop(texture.Size, 1f);
        dl.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding, ImDrawFlags.RoundCornersAll);
        if (hovered)
        {
            dl.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)), rounding);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }


    public static void Empty(Rect content, PhoneTheme theme, float scale)
    {
        var center = content.Center;
        var glyphCenter = center - new Vector2(0f, 18f * scale);
        AppIconArt.TryDraw("photos", glyphCenter, 64f * scale, theme.TextMuted, theme.AppBackground);
        Typography.DrawCentered(center + new Vector2(0f, 34f * scale), Loc.T(L.Photos.NoPhotos), theme.TextMuted, 1.1f);
        Typography.DrawCentered(center + new Vector2(0f, 58f * scale), Loc.T(L.Photos.UseCameraHint),
            theme.TextMuted with { W = 0.7f }, 0.8f);
    }

    public static bool Arrow(Vector2 center, Vector4 color, bool pointsLeft, float scale)
    {
        var dl = ImGui.GetWindowDrawList();
        var radius = 18f * scale;
        var hovered =
            ImGui.IsMouseHoveringRect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        dl.AddCircleFilled(center, radius, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, hovered ? 0.5f : 0.32f)), 28);
        return Chevron(center, color, pointsLeft, scale) || Tapped(hovered);
    }

    public static bool Chevron(Vector2 center, Vector4 color, bool pointsLeft, float scale)
    {
        var dl = ImGui.GetWindowDrawList();
        var radius = 16f * scale;
        var hovered =
            ImGui.IsMouseHoveringRect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        var ink = ImGui.GetColorU32(hovered ? color : color with { W = 0.85f });
        var size = Metrics.Space.Xs * scale;
        var direction = pointsLeft ? -1f : 1f;
        var tip = new Vector2(center.X - direction * size * 0.4f, center.Y);
        dl.AddLine(new Vector2(tip.X + direction * size, tip.Y - size), tip, ink, 2.4f * scale);
        dl.AddLine(tip, new Vector2(tip.X + direction * size, tip.Y + size), ink, 2.4f * scale);
        return Tapped(hovered);
    }

    public static bool Trash(Vector2 center, PhoneTheme theme, float scale)
    {
        var dl = ImGui.GetWindowDrawList();
        var radius = 16f * scale;
        var hovered =
            ImGui.IsMouseHoveringRect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        var color = theme.Danger;
        var ink = ImGui.GetColorU32(hovered ? color : color with { W = 0.85f });
        var extent = 7f * scale;
        var bodyMin = new Vector2(center.X - extent * 0.7f, center.Y - extent * 0.4f);
        var bodyMax = new Vector2(center.X + extent * 0.7f, center.Y + extent);
        dl.AddRect(bodyMin, bodyMax, ink, 2f * scale, ImDrawFlags.RoundCornersBottom, Metrics.Stroke.Thin * scale);
        dl.AddLine(new Vector2(center.X - extent, center.Y - extent * 0.4f),
            new Vector2(center.X + extent, center.Y - extent * 0.4f), ink, Metrics.Stroke.Thin * scale);
        dl.AddLine(new Vector2(center.X - extent * 0.4f, center.Y - extent),
            new Vector2(center.X + extent * 0.4f, center.Y - extent), ink, Metrics.Stroke.Thin * scale);
        if (hovered)
        {
            Tooltip(center, radius, Loc.T(L.Photos.Delete), theme, scale);
        }

        return Tapped(hovered);
    }

    private static void Tooltip(Vector2 iconCenter, float hitRadius, string text, PhoneTheme theme, float scale)
    {
        var dl = ImGui.GetWindowDrawList();
        var textSize = Typography.Measure(text, 0.78f, FontWeight.Medium);
        var padX = 9f * scale;
        var padY = 5f * scale;
        var bubbleSize = new Vector2(textSize.X + padX * 2f, textSize.Y + padY * 2f);
        var gap = 9f * scale;
        var windowMin = ImGui.GetWindowPos();
        var windowMax = windowMin + ImGui.GetWindowSize();
        var minX = Math.Clamp(iconCenter.X - bubbleSize.X * 0.5f, windowMin.X + 4f * scale,
            windowMax.X - bubbleSize.X - 4f * scale);
        var minY = iconCenter.Y - hitRadius - gap - bubbleSize.Y;
        if (minY < windowMin.Y + 4f * scale)
        {
            minY = iconCenter.Y + hitRadius + gap;
        }

        var min = new Vector2(minX, minY);
        var max = min + bubbleSize;
        var bubble = Palette.WithAlpha(Palette.Mix(theme.AppBackground, theme.TextStrong, 0.9f), 0.97f);
        Squircle.Fill(dl, min, max, bubbleSize.Y * 0.5f, ImGui.GetColorU32(bubble));
        Typography.Draw(dl, new Vector2(min.X + padX, min.Y + padY), text, theme.AppBackground, 0.78f,
            FontWeight.Medium);
    }

    private static bool Tapped(bool hovered)
    {
        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static (Vector2 Uv0, Vector2 Uv1) CenterCrop(Vector2 size, float targetAspect)
    {
        if (size.X <= 0f || size.Y <= 0f)
        {
            return (Vector2.Zero, Vector2.One);
        }

        var aspect = size.X / size.Y / targetAspect;
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
