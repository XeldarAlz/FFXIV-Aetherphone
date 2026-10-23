using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Notifications;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal readonly record struct UnderlineTabStyle(
    TextStyle Active,
    TextStyle Idle,
    Vector4 ActiveInk,
    Vector4 IdleInk,
    Vector4 UnderlineInk,
    float Underline,
    float Inset,
    float SmoothTime);

internal static class UnderlineTabs
{
    public const float LabelPadUnits = 16f;

    private const float HoverPadX = 14f;
    private const float HoverPadY = 6f;
    private const float LabelLift = 2f;

    public static int Draw(Rect row, string leftLabel, string rightLabel, bool rightActive, ref Spring slide,
        SocialInk ink, in UnderlineTabStyle style, UiSound tapSound = UiSound.Tap) =>
        Draw(row, [leftLabel, rightLabel], rightActive ? 1 : 0, ref slide, ink, style, tapSound);

    public static int Draw(Rect row, ReadOnlySpan<string> labels, int active, ref Spring slide, SocialInk ink,
        in UnderlineTabStyle style, UiSound tapSound = UiSound.Tap)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var count = labels.Length;
        if (count == 0)
        {
            return -1;
        }

        var slot = row.Width / count;
        var picked = -1;
        for (var index = 0; index < count; index++)
        {
            var cell = new Rect(new Vector2(row.Min.X + slot * index, row.Min.Y),
                new Vector2(row.Min.X + slot * (index + 1), row.Max.Y));
            var hovered = UiInteract.Hover(cell.Min, cell.Max);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            DrawLabel(drawList, cell, labels[index], index == active, hovered, ink, style);
            if (UiInteract.Click(cell.Min, cell.Max, hovered, tapSound))
            {
                picked = index;
            }
        }

        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        slide.Step(active, style.SmoothTime, delta);
        var inset = style.Inset * scale;
        var underlineLeft = row.Min.X + slide.Value * slot + inset * (1f - slide.Value / MathF.Max(1f, count - 1));
        var underlineTop = row.Max.Y - style.Underline * scale;
        Squircle.Fill(drawList, new Vector2(underlineLeft, underlineTop),
            new Vector2(underlineLeft + slot - inset, row.Max.Y), style.Underline * scale * 0.5f,
            ImGui.GetColorU32(style.UnderlineInk));
        FeedCell.Hairline(drawList, row.Min.X, row.Max.X, row.Max.Y, ink.Hairline);
        return picked;
    }

    public static int DrawIcons(Rect row, ReadOnlySpan<string> glyphs, ReadOnlySpan<string> labels, int active,
        ref Spring slide, SocialInk ink, float iconSize, float underline, float smoothTime)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var count = glyphs.Length;
        var slot = row.Width / count;
        var picked = -1;
        for (var index = 0; index < count; index++)
        {
            var cellMin = new Vector2(row.Min.X + slot * index, row.Min.Y);
            var cellMax = new Vector2(cellMin.X + slot, row.Max.Y);
            var hovered = UiInteract.Hover(cellMin, cellMax);
            var glyphInk = index == active ? ink.TitleInk : hovered ? ink.BodyInk : ink.MutedInk;
            PhoneIcon.Draw(drawList, new Vector2((cellMin.X + cellMax.X) * 0.5f, row.Center.Y), glyphs[index],
                glyphInk, iconSize * scale);
            HoverTooltip.Show(new Rect(cellMin, cellMax), labels[index], HoverLabelSide.Below);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(cellMin, cellMax, hovered))
            {
                picked = index;
            }
        }

        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        slide.Step(active, smoothTime, delta);
        var underlineLeft = row.Min.X + slide.Value * slot;
        var underlineTop = row.Max.Y - underline * scale;
        drawList.AddRectFilled(new Vector2(underlineLeft, underlineTop), new Vector2(underlineLeft + slot, row.Max.Y),
            ImGui.GetColorU32(ink.TitleInk));
        FeedCell.Hairline(drawList, row.Min.X, row.Max.X, row.Max.Y, ink.Hairline);
        return picked;
    }

    private static void DrawLabel(ImDrawListPtr drawList, Rect rect, string label, bool active, bool hovered,
        SocialInk ink, in UnderlineTabStyle style)
    {
        var scale = UiScale.Current;
        var textStyle = active ? style.Active : style.Idle;
        var color = active ? style.ActiveInk : hovered ? ink.TitleInk : style.IdleInk;
        var fitted = Typography.FitText(label, MathF.Max(1f, rect.Width - LabelPadUnits * scale), textStyle);
        var center = rect.Center - new Vector2(0f, LabelLift * scale);
        if (hovered && !active)
        {
            var size = Typography.Measure(fitted, textStyle);
            var half = new Vector2(size.X * 0.5f + HoverPadX * scale, size.Y * 0.5f + HoverPadY * scale);
            Squircle.Fill(drawList, center - half, center + half, half.Y,
                ImGui.GetColorU32(Surfaces.Fill(ink.TitleInk, FillLevel.Tertiary)));
        }

        Typography.DrawCentered(drawList, center, fitted, color, textStyle);
    }
}
