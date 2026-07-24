using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Music;

/// <summary>
/// Pure drawing for the Music surface: cover art with a name-derived gradient fallback, the round
/// play/pause button, the seek/volume slider and the pull-down chevron. State stays in
/// <see cref="MusicApp"/>.
/// </summary>
internal static class MusicRenderer
{
    private const float PressSmoothTime = 0.09f;
    private const float KnobSmoothTime = 0.10f;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Dictionary<string, Spring> Springs = new(StringComparer.Ordinal);
    private static string activeSliderId = string.Empty;
    private static float dragFraction;

    public readonly struct SliderState
    {
        public readonly float Value;
        public readonly bool Dragging;
        public readonly bool Released;

        public SliderState(float value, bool dragging, bool released)
        {
            Value = value;
            Dragging = dragging;
            Released = released;
        }
    }

    public static void Cover(ImDrawListPtr drawList, Vector2 min, Vector2 max, IDalamudTextureWrap? texture,
        string fallbackName, float rounding)
    {
        if (texture is not null)
        {
            drawList.AddImageRounded(texture.Handle, min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, rounding,
                ImDrawFlags.RoundCornersAll);
        }
        else
        {
            var swatch = ArtGradient.FromName(fallbackName);
            Squircle.FillVerticalGradient(drawList, min, max, rounding, ImGui.GetColorU32(swatch.Top),
                ImGui.GetColorU32(swatch.Bottom));
        }

        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.06f)), 1f);
    }

    public static bool PlayButton(string id, Vector2 center, float radius, Vector4 fill, Vector4 ink, bool playing,
        float alpha = 1f)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hit = new Vector2(radius, radius);
        var hovered = alpha > 0.6f && UiInteract.Hover(center - hit, center + hit);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var spring = Springs.TryGetValue(id, out var stored) ? stored : new Spring(1f);
        var grow = spring.Step(pressed ? 0.90f : hovered ? 1.05f : 1f, PressSmoothTime,
            MathF.Min(ImGui.GetIO().DeltaTime, 0.1f));
        Springs[id] = spring;
        var drawnRadius = radius * grow;
        drawList.AddCircleFilled(center + new Vector2(0f, 1.5f * ImGuiHelpers.GlobalScale), drawnRadius,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.30f * alpha)), 48);
        drawList.AddCircleFilled(center, drawnRadius, ImGui.GetColorU32(Palette.WithAlpha(fill, alpha)), 48);
        var glyphInk = ImGui.GetColorU32(Palette.WithAlpha(ink, alpha));
        var glyphSize = drawnRadius * 0.42f;
        if (playing)
        {
            MediaGlyph.Pause(drawList, center, glyphSize, glyphInk);
        }
        else
        {
            MediaGlyph.Play(drawList, center + new Vector2(glyphSize * 0.14f, 0f), glyphSize, glyphInk);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(center - hit, center + hit, hovered);
    }

    public static SliderState Slider(string id, Rect track, float fraction, Vector4 fillIdle, Vector4 fillActive,
        Vector4 rail)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var midY = track.Center.Y;
        var left = track.Min.X;
        var width = MathF.Max(track.Width, 1f);
        var hitMin = new Vector2(left - 6f * scale, midY - 12f * scale);
        var hitMax = new Vector2(track.Max.X + 6f * scale, midY + 12f * scale);
        var hovered = UiInteract.Hover(hitMin, hitMax);
        var dragging = string.Equals(activeSliderId, id, StringComparison.Ordinal);
        var released = false;
        if (hovered && !dragging && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            activeSliderId = id;
            dragging = true;
        }

        if (dragging)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                dragFraction = Math.Clamp((ImGui.GetMousePos().X - left) / width, 0f, 1f);
            }
            else
            {
                activeSliderId = string.Empty;
                dragging = false;
                released = true;
            }
        }

        var value = dragging || released ? dragFraction : Math.Clamp(fraction, 0f, 1f);
        var engaged = hovered || dragging;
        var knobSpring = Springs.TryGetValue(id, out var stored) ? stored : new Spring(0f);
        var knob = knobSpring.Step(engaged ? 1f : 0f, KnobSmoothTime, MathF.Min(ImGui.GetIO().DeltaTime, 0.1f));
        Springs[id] = knobSpring;
        var thickness = track.Height;
        var railMin = new Vector2(left, midY - thickness * 0.5f);
        var railMax = new Vector2(track.Max.X, midY + thickness * 0.5f);
        drawList.AddRectFilled(railMin, railMax, ImGui.GetColorU32(rail), thickness * 0.5f);
        var fill = Vector4.Lerp(fillIdle, fillActive, Math.Clamp(knob, 0f, 1f));
        var knobX = left + width * value;
        drawList.AddRectFilled(railMin, new Vector2(knobX, railMax.Y), ImGui.GetColorU32(fill), thickness * 0.5f);
        if (knob > 0.02f)
        {
            drawList.AddCircleFilled(new Vector2(knobX, midY), (thickness * 0.5f + 4f * scale) * knob,
                ImGui.GetColorU32(White), 24);
        }

        if (hovered || dragging)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return new SliderState(value, dragging, released);
    }

    public static bool ChevronDown(string id, Vector2 center, float radius, Vector4 ink, float scale)
    {
        var hit = new Vector2(radius + 6f * scale, radius + 6f * scale);
        var hovered = UiInteract.Hover(center - hit, center + hit);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var spring = Springs.TryGetValue(id, out var stored) ? stored : new Spring(1f);
        var grow = spring.Step(pressed ? 0.82f : 1f, PressSmoothTime, MathF.Min(ImGui.GetIO().DeltaTime, 0.1f));
        Springs[id] = spring;
        var drawList = ImGui.GetWindowDrawList();
        var reach = radius * 0.62f * grow;
        var thickness = 2.4f * scale;
        var tip = new Vector2(center.X, center.Y + reach * 0.5f);
        var leftArm = new Vector2(center.X - reach, tip.Y - reach * 0.72f);
        var rightArm = new Vector2(center.X + reach, tip.Y - reach * 0.72f);
        var packed = ImGui.GetColorU32(hovered ? ink : ink with { W = ink.W * 0.88f });
        drawList.AddLine(leftArm, tip, packed, thickness);
        drawList.AddLine(tip, rightArm, packed, thickness);
        var cap = thickness * 0.5f;
        drawList.AddCircleFilled(leftArm, cap, packed, 8);
        drawList.AddCircleFilled(tip, cap, packed, 8);
        drawList.AddCircleFilled(rightArm, cap, packed, 8);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}
