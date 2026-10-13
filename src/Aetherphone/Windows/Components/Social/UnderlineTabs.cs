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
    private const float HoverPadX = 14f;
    private const float HoverPadY = 6f;
    private const float LabelLift = 2f;

    public static int Draw(Rect row, string leftLabel, string rightLabel, bool rightActive, ref Spring slide,
        SocialInk ink, in UnderlineTabStyle style, UiSound tapSound = UiSound.Tap)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var half = row.Width * 0.5f;
        var leftRect = new Rect(row.Min, new Vector2(row.Min.X + half, row.Max.Y));
        var rightRect = new Rect(new Vector2(row.Min.X + half, row.Min.Y), row.Max);
        var leftHovered = UiInteract.Hover(leftRect.Min, leftRect.Max);
        var rightHovered = UiInteract.Hover(rightRect.Min, rightRect.Max);
        if (leftHovered || rightHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        DrawLabel(drawList, leftRect, leftLabel, !rightActive, leftHovered, ink, style);
        DrawLabel(drawList, rightRect, rightLabel, rightActive, rightHovered, ink, style);
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        slide.Step(rightActive ? 1f : 0f, style.SmoothTime, delta);
        var inset = style.Inset * scale;
        var underlineWidth = half - inset;
        var underlineLeft = row.Min.X + inset + slide.Value * (half - inset);
        var underlineTop = row.Max.Y - style.Underline * scale;
        Squircle.Fill(drawList, new Vector2(underlineLeft, underlineTop),
            new Vector2(underlineLeft + underlineWidth, row.Max.Y), style.Underline * scale * 0.5f,
            ImGui.GetColorU32(style.UnderlineInk));
        FeedCell.Hairline(drawList, row.Min.X, row.Max.X, row.Max.Y, ink.Hairline);
        if (UiInteract.Click(leftRect.Min, leftRect.Max, leftHovered, tapSound))
        {
            return 0;
        }

        return UiInteract.Click(rightRect.Min, rightRect.Max, rightHovered, tapSound) ? 1 : -1;
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
        var fitted = Typography.FitText(label, MathF.Max(1f, rect.Width - 16f * scale), textStyle);
        var center = rect.Center - new Vector2(0f, LabelLift * scale);
        if (hovered)
        {
            var size = Typography.Measure(fitted, textStyle);
            var half = new Vector2(size.X * 0.5f + HoverPadX * scale, size.Y * 0.5f + HoverPadY * scale);
            Squircle.Fill(drawList, center - half, center + half, half.Y,
                ImGui.GetColorU32(Surfaces.Fill(ink.TitleInk, FillLevel.Tertiary)));
        }

        Typography.DrawCentered(drawList, center, fitted, color, textStyle);
    }
}
