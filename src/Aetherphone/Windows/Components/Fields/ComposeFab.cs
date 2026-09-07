using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal static class ComposeFab
{
    private const float DefaultRadius = 26f;
    private const float HoverGrow = 0.12f;
    private const float HoverSmoothTime = 0.11f;
    private const float HoverTopLift = 0.18f;
    private const float HoverBottomLift = 0.10f;
    private const float HoverRimAlpha = 0.30f;
    private const float PressShrink = 0.94f;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Dictionary<uint, Spring> HoverSprings = new();

    public static bool Draw(Rect area, string childId, Vector4 accent, string glyph, string tooltip,
        string? anchorKey = null, Vector4? gradientBottom = null, float radiusUnscaled = DefaultRadius,
        bool phoneGlyph = false)
    {
        var scale = UiScale.Current;
        var radius = radiusUnscaled * scale;
        var glowPad = gradientBottom is null ? 0f : AccentGloss.GlowReach * scale;
        var scrollbarInset = DragScrollHost.Enabled ? 0f : ImGui.GetStyle().ScrollbarSize;
        var box = ComputeBoxRect(area, radiusUnscaled, scale, glowPad, scrollbarInset);
        var boxSize = box.Width;
        ImGui.SetCursorScreenPos(box.Min);
        using var overlay = ImRaii.Child(childId, new Vector2(boxSize, boxSize), false,
            ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        var center = box.Center;
        var fabRect = new Rect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        if (anchorKey is not null)
        {
            UiAnchors.Report(anchorKey, fabRect);
        }

        var drawList = ImGui.GetWindowDrawList();
        var hovered = !InputShield.Active && UiInteract.HoverOverlay(fabRect);
        var eased = HoverEase(childId, hovered);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(childId, pressed, PressShrink);
        var drawRadius = radius * (1f + HoverGrow * eased) * press;
        if (gradientBottom is { } deep)
        {
            AccentGloss.Circle(drawList, center, drawRadius, Palette.Mix(accent, White, HoverTopLift * eased),
                Palette.Mix(deep, White, HoverBottomLift * eased), scale, eased);
        }
        else
        {
            drawList.AddCircleFilled(center + new Vector2(0f, 2f * scale), drawRadius,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.30f)), 32);
            drawList.AddCircleFilled(center, drawRadius,
                ImGui.GetColorU32(Palette.Mix(accent, White, HoverTopLift * eased)), 32);
        }

        if (eased > 0.001f)
        {
            drawList.AddCircle(center, drawRadius,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, HoverRimAlpha * eased)), 48, 1.5f * scale);
        }

        if (phoneGlyph)
        {
            PhoneIcon.Draw(drawList, center, glyph, White, drawRadius * 0.82f);
        }
        else
        {
            AppSkin.Icon(center, glyph, White, 1.1f * drawRadius / radius);
        }

        HoverTooltip.Show(fabRect, tooltip, HoverLabelSide.Above);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(fabRect.Min, fabRect.Max, hovered);
    }

    internal static Rect ComputeBoxRect(Rect area, float radiusUnscaled, float scale, float glowPad,
        float scrollbarInset)
    {
        var radius = radiusUnscaled * scale;
        var margin = 18f * scale;
        var boxSize = radius * 2f + margin + glowPad;
        var boxMin = new Vector2(area.Max.X - boxSize - scrollbarInset, area.Max.Y - boxSize);
        return new Rect(boxMin, boxMin + new Vector2(boxSize, boxSize));
    }

    private static float HoverEase(string id, bool hovered)
    {
        var key = ImGui.GetID(id);
        if (!HoverSprings.TryGetValue(key, out var spring))
        {
            spring = default;
        }

        spring.Step(hovered ? 1f : 0f, HoverSmoothTime,
            MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds));
        HoverSprings[key] = spring;
        return Math.Clamp(spring.Value, 0f, 1f);
    }
}
