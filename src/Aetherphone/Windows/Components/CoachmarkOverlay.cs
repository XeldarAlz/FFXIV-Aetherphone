using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal enum CoachmarkAction
{
    None,
    Advance,
    Skip,
}

internal static class CoachmarkOverlay
{
    private static readonly Vector4 Ink = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 CardTone = new(0.12f, 0.12f, 0.15f, 0.92f);

    private const float DimStrength = 0.78f;
    private const float ActivateThreshold = 0.55f;

    public static CoachmarkAction Draw(Rect screen, PhoneTheme theme, in GuideStep step, Rect? anchor, float progress, int index, int count)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetForegroundDrawList();
        var rounding = theme.ScreenRounding * scale;
        var alpha = MathF.Min(1f, progress * 1.6f);
        var grow = Easing.EaseOutBack(Math.Clamp(progress, 0f, 1f));
        var live = progress >= ActivateThreshold;

        dl.PushClipRect(screen.Min, screen.Max, true);

        var action = step.Surface == GuideSurface.FullCard
            ? DrawFullCard(screen, theme, step, alpha, grow, live, index, count, scale, dl)
            : DrawCoachmark(screen, theme, step, anchor, alpha, grow, live, index, count, scale, rounding, dl);

        dl.PopClipRect();
        return action;
    }

    private static CoachmarkAction DrawFullCard(Rect screen, PhoneTheme theme, in GuideStep step, float alpha, float grow, bool live, int index, int count, float scale, ImDrawListPtr dl)
    {
        Material.Veil(dl, screen.Min, screen.Max, 0.94f * alpha, theme.ScreenRounding * scale);

        var rise = (1f - grow) * 16f * scale;
        var emblemCenter = new Vector2(screen.Center.X, screen.Min.Y + screen.Height * 0.29f - rise);

        var buttonSize = new Vector2(MathF.Min(screen.Width - 48f * scale, 230f * scale), 46f * scale);
        var buttonCenter = new Vector2(screen.Center.X, screen.Max.Y - 68f * scale);

        var panelMin = new Vector2(screen.Min.X + 20f * scale, emblemCenter.Y - 92f * scale);
        var panelMax = new Vector2(screen.Max.X - 20f * scale, buttonCenter.Y + buttonSize.Y * 0.5f + 22f * scale);
        Material.Frosted(dl, panelMin, panelMax, 28f * scale, scale, alpha);

        Emblem(dl, emblemCenter, theme.Accent, scale, grow, alpha);

        var titleCenter = new Vector2(screen.Center.X, screen.Min.Y + screen.Height * 0.47f - rise);
        DrawCentered(dl, titleCenter, Loc.T(step.Title), theme.TextStrong with { W = alpha }, TextStyles.Title1);

        var bodyWidth = screen.Width * 0.80f;
        var bodyTop = titleCenter.Y + LineHeight(TextStyles.Title1) * 0.5f + 14f * scale;
        DrawWrapped(dl, Loc.T(step.Body), TextStyles.Body, theme.TextMuted with { W = alpha }, new Vector2(screen.Center.X, bodyTop), bodyWidth, scale, true);

        var action = CoachmarkAction.None;

        if (count > 1)
        {
            Dots(dl, new Vector2(screen.Center.X, screen.Max.Y - 112f * scale), index, count, theme.Accent, theme.TextMuted, alpha, scale);
        }

        if (Button(dl, buttonCenter, buttonSize, Loc.T(step.ButtonLabel), theme.Accent, alpha, live, scale))
        {
            action = CoachmarkAction.Advance;
        }

        return action;
    }

    private static CoachmarkAction DrawCoachmark(Rect screen, PhoneTheme theme, in GuideStep step, Rect? anchor, float alpha, float grow, bool live, int index, int count, float scale, float rounding, ImDrawListPtr dl)
    {
        var hole = anchor;
        if (hole.HasValue)
        {
            var padded = hole.Value.Inset(-7f * scale);
            if (!Within(screen, padded))
            {
                hole = null;
            }
            else
            {
                hole = padded;
            }
        }

        if (hole.HasValue)
        {
            Spotlight(dl, screen, hole.Value, rounding, DimStrength * alpha, scale);
            SpotlightRing(dl, hole.Value, theme.Accent, alpha, scale);
        }
        else
        {
            Material.Veil(dl, screen.Min, screen.Max, DimStrength * alpha, rounding);
        }

        var isTap = step.Advance == GuideAdvance.TapTarget && hole.HasValue;

        var cardWidth = MathF.Min(screen.Width - 24f * scale, 344f * scale);
        var innerWidth = cardWidth - 44f * scale;
        var titleLine = LineHeight(TextStyles.Title3);
        var bodyLine = LineHeight(TextStyles.Body);
        var bodyLines = CountWrapped(Loc.T(step.Body), TextStyles.Body, innerWidth);
        var bodyBlock = bodyLines * bodyLine * 1.25f;
        var actionHeight = isTap ? bodyLine : 50f * scale;
        var dotsHeight = count > 1 ? 16f * scale : 0f;
        var cardHeight = 22f * scale + titleLine + 10f * scale + bodyBlock + 18f * scale + dotsHeight + actionHeight + 22f * scale;

        var margin = 14f * scale;
        var arrowH = 9f * scale;
        var rise = (1f - grow) * 8f * scale;

        float cardCenterX;
        float cardTop;
        var arrowUp = true;
        var arrowX = screen.Center.X;

        if (hole.HasValue)
        {
            var h = hole.Value;
            arrowX = Math.Clamp(h.Center.X, screen.Min.X + margin + 24f * scale, screen.Max.X - margin - 24f * scale);
            var below = h.Max.Y + arrowH + cardHeight + margin <= screen.Max.Y;
            arrowUp = below;
            cardTop = below ? h.Max.Y + arrowH : h.Min.Y - arrowH - cardHeight;
            cardCenterX = Math.Clamp(h.Center.X, screen.Min.X + margin + cardWidth * 0.5f, screen.Max.X - margin - cardWidth * 0.5f);
        }
        else
        {
            cardCenterX = screen.Center.X;
            cardTop = screen.Center.Y - cardHeight * 0.5f;
        }

        cardTop += rise;
        var cardMin = new Vector2(cardCenterX - cardWidth * 0.5f, cardTop);
        var cardMax = new Vector2(cardCenterX + cardWidth * 0.5f, cardTop + cardHeight);
        var radius = 20f * scale;

        if (hole.HasValue)
        {
            Arrow(dl, arrowX, arrowUp ? cardMin.Y : cardMax.Y, arrowH, arrowUp, alpha);
        }

        Elevation.Floating(dl, cardMin, cardMax, radius, scale, alpha);
        Material.Frosted(dl, cardMin, cardMax, radius, scale, alpha);

        var cursorY = cardMin.Y + 22f * scale;
        DrawCentered(dl, new Vector2(cardCenterX, cursorY + titleLine * 0.5f), Loc.T(step.Title), theme.TextStrong with { W = alpha }, TextStyles.Title3);
        cursorY += titleLine + 10f * scale;

        cursorY = DrawWrapped(dl, Loc.T(step.Body), TextStyles.Body, theme.TextMuted with { W = alpha }, new Vector2(cardCenterX, cursorY), innerWidth, scale, true);
        cursorY += 18f * scale;

        if (count > 1)
        {
            Dots(dl, new Vector2(cardCenterX, cursorY + 4f * scale), index, count, theme.Accent, theme.TextMuted, alpha, scale);
            cursorY += dotsHeight;
        }

        var action = CoachmarkAction.None;

        if (isTap)
        {
            DrawCentered(dl, new Vector2(cardCenterX, cursorY + bodyLine * 0.5f), Loc.T(L.Onboarding.TapToContinue), theme.Accent with { W = alpha }, TextStyles.FootnoteEmphasized);

            var padded = hole!.Value;
            if (live && ImGui.IsMouseHoveringRect(padded.Min, padded.Max))
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    action = CoachmarkAction.Advance;
                }
            }
        }
        else
        {
            var buttonSize = new Vector2(cardWidth - 44f * scale, 46f * scale);
            var buttonCenter = new Vector2(cardCenterX, cursorY + buttonSize.Y * 0.5f);
            if (Button(dl, buttonCenter, buttonSize, Loc.T(step.ButtonLabel), theme.Accent, alpha, live, scale))
            {
                action = CoachmarkAction.Advance;
            }
        }

        return action;
    }

    private static void Spotlight(ImDrawListPtr dl, Rect screen, Rect hole, float rounding, float dim, float scale)
    {
        if (dim <= 0f)
        {
            return;
        }

        var color = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, dim));
        var top = Math.Clamp(hole.Min.Y, screen.Min.Y, screen.Max.Y);
        var bottom = Math.Clamp(hole.Max.Y, screen.Min.Y, screen.Max.Y);

        dl.AddRectFilled(screen.Min, new Vector2(screen.Max.X, top), color, rounding, ImDrawFlags.RoundCornersTop);
        dl.AddRectFilled(new Vector2(screen.Min.X, bottom), screen.Max, color, rounding, ImDrawFlags.RoundCornersBottom);
        dl.AddRectFilled(new Vector2(screen.Min.X, top), new Vector2(hole.Min.X, bottom), color, 0f, ImDrawFlags.None);
        dl.AddRectFilled(new Vector2(hole.Max.X, top), new Vector2(screen.Max.X, bottom), color, 0f, ImDrawFlags.None);
    }

    private static void SpotlightRing(ImDrawListPtr dl, Rect hole, Vector4 accent, float alpha, float scale)
    {
        var pulse = Styling.Pulse(Styling.PulseCalm);
        var ringRadius = MathF.Min(hole.Width, hole.Height) * 0.34f;
        var glow = accent with { W = (0.12f + 0.10f * pulse) * alpha };
        Squircle.Stroke(dl, hole.Min - new Vector2(4f * scale, 4f * scale), hole.Max + new Vector2(4f * scale, 4f * scale), ringRadius + 4f * scale, ImGui.GetColorU32(glow), 5f * scale);
        var ring = accent with { W = (0.65f + 0.35f * pulse) * alpha };
        Squircle.Stroke(dl, hole.Min, hole.Max, ringRadius, ImGui.GetColorU32(ring), 2.4f * scale);
    }

    private static void Emblem(ImDrawListPtr dl, Vector2 center, Vector4 accent, float scale, float grow, float alpha)
    {
        var radius = 34f * scale * (0.82f + 0.18f * grow);
        dl.AddCircleFilled(center, radius * 2.2f, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.06f * alpha)), 64);
        dl.AddCircleFilled(center, radius * 1.6f, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.10f * alpha)), 64);
        dl.AddCircleFilled(center, radius * 1.12f, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.14f * alpha)), 64);
        dl.AddCircle(center, radius, ImGui.GetColorU32(Palette.WithAlpha(accent, alpha)), 72, 3f * scale);
        var core = Palette.Mix(accent, Vector4.One, 0.7f);
        dl.AddCircleFilled(center, radius * 0.30f, ImGui.GetColorU32(Palette.WithAlpha(core, alpha)), 48);
    }

    private static void Arrow(ImDrawListPtr dl, float x, float y, float height, bool up, float alpha)
    {
        var half = height * 1.15f;
        var color = ImGui.GetColorU32(CardTone with { W = CardTone.W * alpha });
        if (up)
        {
            dl.AddTriangleFilled(new Vector2(x, y - height), new Vector2(x - half, y + 1f), new Vector2(x + half, y + 1f), color);
        }
        else
        {
            dl.AddTriangleFilled(new Vector2(x, y + height), new Vector2(x - half, y - 1f), new Vector2(x + half, y - 1f), color);
        }
    }

    private static void Dots(ImDrawListPtr dl, Vector2 center, int index, int count, Vector4 accent, Vector4 muted, float alpha, float scale)
    {
        var spacing = 13f * scale;
        var radius = 3f * scale;
        var startX = center.X - (count - 1) * spacing * 0.5f;
        for (var dot = 0; dot < count; dot++)
        {
            var color = dot == index ? accent with { W = alpha } : muted with { W = 0.5f * alpha };
            dl.AddCircleFilled(new Vector2(startX + dot * spacing, center.Y), radius, ImGui.GetColorU32(color), 16);
        }
    }

    private static bool Button(ImDrawListPtr dl, Vector2 center, Vector2 size, string label, Vector4 accent, float alpha, bool live, float scale)
    {
        var half = size * 0.5f;
        var min = center - half;
        var max = center + half;
        var radius = size.Y * 0.5f;
        var hovered = live && ImGui.IsMouseHoveringRect(min, max);
        var fill = hovered ? Palette.Mix(accent, Vector4.One, 0.14f) : accent;

        Squircle.Fill(dl, min, max, radius, ImGui.GetColorU32(fill with { W = fill.W * alpha }));
        DrawCentered(dl, center, label, Ink with { W = alpha }, TextStyles.Headline);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static float DrawWrapped(ImDrawListPtr dl, string text, in TextStyle style, Vector4 color, Vector2 topCenter, float maxWidth, float scale, bool spacious)
    {
        var lineHeight = LineHeight(style) * (spacious ? 1.25f : 1.2f);
        var y = topCenter.Y;
        var length = text.Length;
        var lineStart = 0;
        var lastSpace = -1;

        for (var index = 0; index <= length; index++)
        {
            var atEnd = index == length;
            if (!atEnd && text[index] != ' ')
            {
                continue;
            }

            var candidate = text.Substring(lineStart, index - lineStart);
            if (Typography.Measure(candidate, style).X > maxWidth && lastSpace > lineStart)
            {
                var line = text.Substring(lineStart, lastSpace - lineStart);
                DrawCentered(dl, new Vector2(topCenter.X, y + lineHeight * 0.5f), line, color, style);
                y += lineHeight;
                lineStart = lastSpace + 1;
            }

            lastSpace = index;

            if (atEnd)
            {
                var tail = text.Substring(lineStart);
                DrawCentered(dl, new Vector2(topCenter.X, y + lineHeight * 0.5f), tail, color, style);
                y += lineHeight;
            }
        }

        return y;
    }

    private static int CountWrapped(string text, in TextStyle style, float maxWidth)
    {
        var length = text.Length;
        var lines = 1;
        var lineStart = 0;
        var lastSpace = -1;

        for (var index = 0; index <= length; index++)
        {
            var atEnd = index == length;
            if (!atEnd && text[index] != ' ')
            {
                continue;
            }

            var candidate = text.Substring(lineStart, index - lineStart);
            if (Typography.Measure(candidate, style).X > maxWidth && lastSpace > lineStart)
            {
                lines++;
                lineStart = lastSpace + 1;
            }

            lastSpace = index;
        }

        return lines;
    }

    private static void DrawCentered(ImDrawListPtr dl, Vector2 center, string text, Vector4 color, in TextStyle style)
    {
        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            var size = ImGui.CalcTextSize(text);
            var font = ImGui.GetFont();
            dl.AddText(font, ImGui.GetFontSize(), center - size * 0.5f, ImGui.GetColorU32(color), text);
        }
    }

    private static float LineHeight(in TextStyle style) => Typography.Measure("Ay", style).Y;

    private static bool Within(Rect screen, Rect rect)
        => rect.Min.X >= screen.Min.X && rect.Min.Y >= screen.Min.Y && rect.Max.X <= screen.Max.X && rect.Max.Y <= screen.Max.Y;
}
