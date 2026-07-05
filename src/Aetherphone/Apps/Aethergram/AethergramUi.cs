using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Aethergram;

internal sealed class AethergramUi
{
    public static readonly Vector4 Accent = new(0.92f, 0.30f, 0.38f, 1f);
    public static readonly Vector4 Transparent = new(0f, 0f, 0f, 0f);
    public static readonly Vector4 TitleInk = new(0.99f, 0.95f, 0.97f, 1f);
    public static readonly Vector4 BodyInk = new(0.93f, 0.85f, 0.90f, 0.96f);
    public static readonly Vector4 MutedInk = new(0.78f, 0.66f, 0.76f, 0.85f);
    public static readonly Vector4 HeaderInk = new(0.99f, 0.72f, 0.82f, 0.95f);
    private static readonly Vector4 BackdropTop = new(0.20f, 0.08f, 0.32f, 1f);
    private static readonly Vector4 BackdropBottom = new(0.04f, 0.02f, 0.10f, 1f);
    private static readonly Vector4 BloomTop = new(0.92f, 0.30f, 0.38f, 0.22f);
    private static readonly Vector4 BloomBottom = new(0.45f, 0.16f, 0.55f, 0f);
    public static readonly Vector4 Surface = new(1f, 1f, 1f, 0.05f);
    public static readonly Vector4 SurfaceStroke = new(1f, 1f, 1f, 0.06f);
    public static readonly Vector4 FieldSurface = new(1f, 1f, 1f, 0.10f);
    public PhoneTheme Theme { get; set; } = PhoneTheme.Default;

    public void Backdrop(Rect screen)
    {
        var scale = ImGuiHelpers.GlobalScale;
        PaintGradient(ImGui.GetWindowDrawList(), screen, screen, Theme.ScreenRounding * scale);
    }

    public void Body(Rect area)
    {
        var frame = SceneChrome.ScreenFrom(area, Theme, ImGuiHelpers.GlobalScale);
        PaintGradient(ImGui.GetWindowDrawList(), area, frame, 0f);
    }

    public static void PaintGradient(ImDrawListPtr drawList, Rect target, Rect frame, float rounding)
    {
        var topFraction = frame.Height <= 0f ? 0f : (target.Min.Y - frame.Min.Y) / frame.Height;
        var bottomFraction = frame.Height <= 0f ? 1f : (target.Max.Y - frame.Min.Y) / frame.Height;
        Squircle.FillVerticalGradient(drawList, target.Min, target.Max, rounding,
            ImGui.GetColorU32(Vector4.Lerp(BackdropTop, BackdropBottom, topFraction)),
            ImGui.GetColorU32(Vector4.Lerp(BackdropTop, BackdropBottom, bottomFraction)));
        Squircle.FillVerticalGradient(drawList, target.Min, target.Max, rounding,
            ImGui.GetColorU32(Vector4.Lerp(BloomTop, BloomBottom, topFraction)),
            ImGui.GetColorU32(Vector4.Lerp(BloomTop, BloomBottom, bottomFraction)));
    }

    public void Card(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding)
    {
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(Surface));
        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(SurfaceStroke), 1f);
    }

    public bool PillButton(Rect rect, string label, bool filled)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var radius = rect.Height * 0.5f;
        var fill = filled
            ? (hovered ? Palette.Mix(Accent, Theme.TextStrong, 0.12f) : Accent)
            : (hovered ? new Vector4(1f, 1f, 1f, 0.16f) : FieldSurface);
        var ink = filled ? new Vector4(1f, 1f, 1f, 1f) : TitleInk;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(fill));
        var textSize = Typography.Measure(label, 0.9f, FontWeight.SemiBold);
        Typography.Draw(rect.Center - textSize * 0.5f, label, ink, 0.9f, FontWeight.SemiBold);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    public bool DangerPillButton(Rect rect, string label)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var radius = rect.Height * 0.5f;
        var fill = hovered ? Palette.Mix(Theme.Danger, Theme.TextStrong, 0.12f) : Theme.Danger;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(fill));
        var textSize = Typography.Measure(label, 0.9f, FontWeight.SemiBold);
        Typography.Draw(rect.Center - textSize * 0.5f, label, new Vector4(1f, 1f, 1f, 1f), 0.9f, FontWeight.SemiBold);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    public bool GhostButton(Rect rect, string label)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var radius = rect.Height * 0.5f;
        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f)));
        }

        Squircle.Stroke(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.28f)), 1f);
        var textSize = Typography.Measure(label, 0.9f, FontWeight.SemiBold);
        Typography.Draw(rect.Center - textSize * 0.5f, label, TitleInk, 0.9f, FontWeight.SemiBold);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    public bool IconButton(Vector2 center, float hitRadius, string glyph, Vector4 color, Vector4 background,
        float glyphScale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(center - new Vector2(hitRadius, hitRadius),
            center + new Vector2(hitRadius, hitRadius));
        if (background.W > 0f)
        {
            drawList.AddCircleFilled(center, hitRadius,
                ImGui.GetColorU32(hovered ? Palette.Mix(background, Theme.TextStrong, 0.08f) : background), 24);
        }

        Icon(center, glyph, hovered ? Palette.Mix(color, Theme.TextStrong, 0.2f) : color, glyphScale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    public bool Chip(Rect rect, string label, bool active)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var radius = rect.Height * 0.5f;
        var fill = active ? Palette.WithAlpha(Accent, 0.28f) : new Vector4(1f, 1f, 1f, 0.08f);
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(fill));
        if (active)
        {
            Squircle.Stroke(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(Accent), 1.4f);
        }

        var ink = active ? new Vector4(0.99f, 0.85f, 0.91f, 1f) : BodyInk;
        Typography.DrawCentered(rect.Center, label, ink, 0.85f, FontWeight.Medium);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    public bool HeaderAction(Rect area, string label, bool enabled)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var height = 28f * scale;
        var width = Typography.Measure(label, 0.9f, FontWeight.SemiBold).X + 26f * scale;
        var max = new Vector2(area.Max.X - 12f * scale, area.Min.Y + AppHeader.Height * scale * 0.5f + height * 0.5f);
        var min = new Vector2(max.X - width, max.Y - height);
        var rect = new Rect(min, max);
        return PillButton(rect, label, enabled) && enabled;
    }

    public static void SectionLabel(string label)
    {
        var scale = ImGuiHelpers.GlobalScale;
        using (Plugin.Fonts.Push(0.78f, FontWeight.SemiBold))
        using (ImRaii.PushColor(ImGuiCol.Text, HeaderInk))
        {
            ImGui.TextUnformatted(label.ToUpperInvariant());
        }

        ImGui.Dummy(new Vector2(0f, 4f * scale));
    }

    public void SectionHeading(string label)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var barWidth = 3f * scale;
        var barHeight = 14f * scale;
        Squircle.Fill(drawList, new Vector2(origin.X, origin.Y + 2f * scale),
            new Vector2(origin.X + barWidth, origin.Y + 2f * scale + barHeight), barWidth * 0.5f,
            ImGui.GetColorU32(Accent));
        Typography.Draw(new Vector2(origin.X + barWidth + 9f * scale, origin.Y), label,
            new Vector4(0.99f, 0.90f, 0.94f, 1f), 0.95f, FontWeight.SemiBold);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, barHeight + 10f * scale));
    }

    public void Field(string label, string id, ref string value, int maxLength, bool multiline)
    {
        var scale = ImGuiHelpers.GlobalScale;
        using (ImRaii.PushColor(ImGuiCol.Text, MutedInk))
        {
            ImGui.TextUnformatted(label);
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = (multiline ? 88f : 34f) * scale;
        Squircle.Fill(ImGui.GetWindowDrawList(), origin, new Vector2(origin.X + width, origin.Y + height), 9f * scale,
            ImGui.GetColorU32(FieldSurface));
        ImGui.SetCursorScreenPos(new Vector2(origin.X + 12f * scale,
            origin.Y + (multiline ? 8f * scale : height * 0.5f - ImGui.GetFrameHeight() * 0.5f)));
        ImGui.SetNextItemWidth(width - 24f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, TitleInk))
        {
            if (multiline)
            {
                ImGui.InputTextMultiline(id, ref value, maxLength,
                    new Vector2(width - 24f * scale, height - 16f * scale), ImGuiInputTextFlags.None);
            }
            else
            {
                ImGui.InputText(id, ref value, maxLength, ImGuiInputTextFlags.None);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    public static void Icon(Vector2 center, string glyph, Vector4 color, float scale)
    {
        float fontSize;
        Vector2 size;
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            fontSize = ImGui.GetFontSize() * scale;
            size = ImGui.CalcTextSize(glyph) * scale;
        }

        ImGui.GetWindowDrawList().AddText(UiBuilder.IconFont, fontSize, center - size * 0.5f, ImGui.GetColorU32(color),
            glyph, 0f);
    }

    public static bool HoverClick(Vector2 min, Vector2 max)
    {
        if (!ImGui.IsMouseHoveringRect(min, max))
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}
