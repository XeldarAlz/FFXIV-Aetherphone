using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.Photos;

internal static class PhotosChrome
{
    private const float ThumbRounding = 7f;

    public static void Thumbnail(ImDrawListPtr drawList, IDalamudTextureWrap? texture, Vector2 min, Vector2 max,
        bool hovered, Vector4 placeholder)
    {
        var scale = UiScale.Current;
        var rounding = ThumbRounding * scale;
        if (texture is null)
        {
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(placeholder), rounding, ImDrawFlags.RoundCornersAll);
            Material.Edge(drawList, min, max, rounding, scale, 0.4f);
            return;
        }

        var (uv0, uv1) = CenterCrop(texture.Size, 1f);
        drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding, ImDrawFlags.RoundCornersAll);
        Material.Edge(drawList, min, max, rounding, scale, 0.45f);
        if (hovered)
        {
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.12f)), rounding,
                ImDrawFlags.RoundCornersAll);
        }
    }

    public static void TopScrim(ImDrawListPtr drawList, Vector2 min, Vector2 max, float height)
    {
        var solid = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.52f));
        var clear = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0f));
        drawList.AddRectFilledMultiColor(min, new Vector2(max.X, min.Y + height), solid, solid, clear, clear);
    }

    public static void BottomScrim(ImDrawListPtr drawList, Vector2 min, Vector2 max, float height)
    {
        var solid = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.52f));
        var clear = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0f));
        drawList.AddRectFilledMultiColor(new Vector2(min.X, max.Y - height), max, clear, clear, solid, solid);
    }

    public static bool Arrow(Vector2 center, Vector4 color, bool pointsLeft, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var radius = 18f * scale;
        var bounds = new Rect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        var hovered = UiInteract.Hover(bounds.Min, bounds.Max);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, hovered ? 0.5f : 0.34f)), 28);
        HoverTooltip.Show(bounds, Loc.T(pointsLeft ? L.Common.Previous : L.Common.Next));
        return Chevron(center, color, pointsLeft, scale) || Tapped(hovered);
    }

    public static bool Chevron(Vector2 center, Vector4 color, bool pointsLeft, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var radius = 16f * scale;
        var hovered =
            UiInteract.Hover(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        var ink = ImGui.GetColorU32(hovered ? color : color with { W = 0.85f });
        var size = Metrics.Space.Xs * scale;
        var direction = pointsLeft ? -1f : 1f;
        var tip = new Vector2(center.X + direction * size * 0.4f, center.Y);
        drawList.AddLine(new Vector2(tip.X - direction * size, tip.Y - size), tip, ink, 2.4f * scale);
        drawList.AddLine(tip, new Vector2(tip.X - direction * size, tip.Y + size), ink, 2.4f * scale);
        return Tapped(hovered);
    }

    public static bool Trash(Vector2 center, PhoneTheme theme, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var radius = 17f * scale;
        var hovered =
            UiInteract.Hover(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, hovered ? 0.5f : 0.32f)), 28);
        var color = theme.Danger;
        var ink = ImGui.GetColorU32(hovered ? color : color with { W = 0.9f });
        var extent = 7f * scale;
        var bodyMin = new Vector2(center.X - extent * 0.7f, center.Y - extent * 0.4f);
        var bodyMax = new Vector2(center.X + extent * 0.7f, center.Y + extent);
        drawList.AddRect(bodyMin, bodyMax, ink, 2f * scale, ImDrawFlags.RoundCornersBottom, Metrics.Stroke.Thin * scale);
        drawList.AddLine(new Vector2(center.X - extent, center.Y - extent * 0.4f),
            new Vector2(center.X + extent, center.Y - extent * 0.4f), ink, Metrics.Stroke.Thin * scale);
        drawList.AddLine(new Vector2(center.X - extent * 0.4f, center.Y - extent),
            new Vector2(center.X + extent * 0.4f, center.Y - extent), ink, Metrics.Stroke.Thin * scale);
        HoverTooltip.Show(new Rect(center - new Vector2(radius, radius), center + new Vector2(radius, radius)),
            Loc.T(L.Photos.Delete));
        return Tapped(hovered);
    }

    public static bool AddToAlbum(Vector2 center, Vector4 color, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var radius = 17f * scale;
        var bounds = new Rect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        var hovered = UiInteract.Hover(bounds.Min, bounds.Max);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, hovered ? 0.5f : 0.32f)), 28);
        var ink = ImGui.GetColorU32(hovered ? color : color with { W = 0.9f });
        var extent = 6.5f * scale;
        var thickness = Metrics.Stroke.Thin * scale;
        var boxMin = new Vector2(center.X - extent, center.Y - extent);
        var boxMax = new Vector2(center.X + extent * 0.35f, center.Y + extent * 0.35f);
        drawList.AddRect(boxMin, boxMax, ink, 2f * scale, ImDrawFlags.RoundCornersAll, thickness);
        var plusCenter = new Vector2(center.X + extent * 0.55f, center.Y + extent * 0.55f);
        var arm = extent * 0.5f;
        drawList.AddLine(new Vector2(plusCenter.X - arm, plusCenter.Y), new Vector2(plusCenter.X + arm, plusCenter.Y),
            ink, thickness);
        drawList.AddLine(new Vector2(plusCenter.X, plusCenter.Y - arm), new Vector2(plusCenter.X, plusCenter.Y + arm),
            ink, thickness);
        HoverTooltip.Show(bounds, Loc.T(L.Photos.AddToAlbum));
        return Tapped(hovered);
    }

    public static bool Share(Vector2 center, Vector4 color, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var radius = 17f * scale;
        var hovered =
            UiInteract.Hover(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, hovered ? 0.5f : 0.32f)), 28);
        var ink = ImGui.GetColorU32(hovered ? color : color with { W = 0.9f });
        var extent = 7f * scale;
        var thickness = Metrics.Stroke.Thin * scale;
        var trayTop = center.Y - extent * 0.15f;
        var trayLeft = center.X - extent * 0.85f;
        var trayRight = center.X + extent * 0.85f;
        var trayBottom = center.Y + extent;
        drawList.AddLine(new Vector2(trayLeft, trayTop), new Vector2(trayLeft, trayBottom), ink, thickness);
        drawList.AddLine(new Vector2(trayRight, trayTop), new Vector2(trayRight, trayBottom), ink, thickness);
        drawList.AddLine(new Vector2(trayLeft, trayBottom), new Vector2(trayRight, trayBottom), ink, thickness);
        var arrowTop = center.Y - extent;
        drawList.AddLine(new Vector2(center.X, arrowTop), new Vector2(center.X, center.Y + extent * 0.32f), ink,
            thickness);
        drawList.AddLine(new Vector2(center.X - extent * 0.45f, arrowTop + extent * 0.48f),
            new Vector2(center.X, arrowTop), ink, thickness);
        drawList.AddLine(new Vector2(center.X + extent * 0.45f, arrowTop + extent * 0.48f),
            new Vector2(center.X, arrowTop), ink, thickness);
        HoverTooltip.Show(new Rect(center - new Vector2(radius, radius), center + new Vector2(radius, radius)),
            Loc.T(L.Share.Action));
        return Tapped(hovered);
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
