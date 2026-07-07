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

internal sealed class CoachmarkOverlay
{
    private static readonly Vector4 Ink = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 CardTone = new(0.12f, 0.12f, 0.15f, 0.92f);
    private const float DimStrength = 0.78f;
    private const float PoseFrequency = 3.1f;
    private const float PoseDamping = 0.86f;
    private const float SizeSmoothTime = 0.16f;
    private const float BlendSmoothTime = 0.14f;
    private const float DotSmoothTime = 0.16f;

    private DampedSpring cardCenterX;
    private DampedSpring cardTop;
    private Spring cardWidth;
    private Spring cardHeight;
    private Spring arrowSlide;
    private Spring holeBlend;
    private Spring dotSlide;
    private bool hasPose;
    private Rect lastHole;
    private bool hasHole;

    public void Reset()
    {
        hasPose = false;
        hasHole = false;
        holeBlend.SnapTo(0f);
        dotSlide.SnapTo(0f);
    }

    public CoachmarkAction Draw(Rect screen, PhoneTheme theme, in GuideStep step, Rect? anchor, float presence,
        float textProgress, int index, int count, bool interactive)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var dl = ImGui.GetForegroundDrawList();
        var rounding = theme.ScreenRounding * scale;
        var alpha = Math.Clamp(presence * 1.5f, 0f, 1f);
        var contentProgress = Easing.SmoothStep(Math.Clamp((textProgress - 0.12f) / 0.55f, 0f, 1f));
        var contentAlpha = contentProgress * alpha;
        var contentRise = (1f - contentProgress) * 7f * scale;
        var live = interactive && presence > 0.55f && textProgress > 0.45f;
        var pop = 0.94f + 0.06f * Math.Clamp(presence, 0f, 1.15f);
        var hole = PadHole(screen, anchor, step, scale);
        if (hole is { } holeRect)
        {
            lastHole = holeRect;
            hasHole = true;
        }

        holeBlend.Step(hole.HasValue ? 1f : 0f, BlendSmoothTime, delta);
        dotSlide.Step(index, DotSmoothTime, delta);
        var blend = Math.Clamp(holeBlend.Value, 0f, 1f);
        dl.PushClipRect(screen.Min, screen.Max, true);
        DrawDim(dl, screen, rounding, alpha, blend, scale, theme, step);
        var fullCard = step.Surface == GuideSurface.FullCard;
        var target = fullCard
            ? FullCardTarget(screen, scale)
            : CoachmarkTarget(screen, step, hole, scale, out _);
        StepPose(target, delta);
        var card = PoseRect(pop);
        var action = fullCard
            ? DrawFullCardContent(dl, screen, theme, step, card, alpha, contentAlpha, contentRise, live, index, count,
                scale)
            : DrawCoachmarkContent(dl, screen, theme, step, card, hole, alpha, contentAlpha, contentRise, blend, live,
                index, count, scale, delta);
        dl.PopClipRect();
        return action;
    }

    private static Rect? PadHole(Rect screen, Rect? anchor, in GuideStep step, float scale)
    {
        if (step.AnchorKey is null || anchor is not { } rect)
        {
            return null;
        }

        var padded = rect.Inset(-7f * scale);
        return Within(screen, padded) ? padded : null;
    }

    private void DrawDim(ImDrawListPtr dl, Rect screen, float rounding, float alpha, float blend, float scale,
        PhoneTheme theme, in GuideStep step)
    {
        var fullVeil = step.Surface == GuideSurface.FullCard ? 0.94f : DimStrength;
        if (blend < 0.999f)
        {
            Material.Veil(dl, screen.Min, screen.Max, fullVeil * alpha * (1f - blend), rounding);
        }

        if (blend > 0.001f && hasHole)
        {
            Spotlight(dl, screen, lastHole, rounding, DimStrength * alpha * blend);
            SpotlightRing(dl, lastHole, theme.Accent, alpha * blend, scale);
        }
    }

    private Rect FullCardTarget(Rect screen, float scale)
    {
        var buttonBottom = screen.Max.Y - 68f * scale + 23f * scale;
        var min = new Vector2(screen.Min.X + 20f * scale, screen.Min.Y + screen.Height * 0.29f - 92f * scale);
        var max = new Vector2(screen.Max.X - 20f * scale, buttonBottom + 22f * scale);
        return new Rect(min, max);
    }

    private Rect CoachmarkTarget(Rect screen, in GuideStep step, Rect? hole, float scale, out bool arrowUp)
    {
        var cardWidthTarget = MathF.Min(screen.Width - 24f * scale, 344f * scale);
        var innerWidth = cardWidthTarget - 44f * scale;
        var isTap = step.Advance == GuideAdvance.TapTarget && hole.HasValue;
        var titleLine = LineHeight(TextStyles.Title3);
        var bodyLine = LineHeight(TextStyles.Body);
        var bodyLines = CountWrapped(Loc.T(step.Body), TextStyles.Body, innerWidth);
        var bodyBlock = bodyLines * bodyLine * 1.25f;
        var actionHeight = isTap ? bodyLine : 50f * scale;
        var dotsHeight = 16f * scale;
        var cardHeightTarget = 22f * scale + titleLine + 10f * scale + bodyBlock + 18f * scale + dotsHeight +
                               actionHeight + 22f * scale;
        var margin = 14f * scale;
        var arrowH = 9f * scale;
        float centerX;
        float top;
        arrowUp = true;
        if (hole is { } h)
        {
            var below = h.Max.Y + arrowH + cardHeightTarget + margin <= screen.Max.Y;
            arrowUp = below;
            top = below ? h.Max.Y + arrowH : h.Min.Y - arrowH - cardHeightTarget;
            centerX = ClampToRange(h.Center.X, screen.Min.X + margin + cardWidthTarget * 0.5f,
                screen.Max.X - margin - cardWidthTarget * 0.5f);
        }
        else
        {
            centerX = screen.Center.X;
            top = screen.Center.Y - cardHeightTarget * 0.5f;
        }

        return new Rect(new Vector2(centerX - cardWidthTarget * 0.5f, top),
            new Vector2(centerX + cardWidthTarget * 0.5f, top + cardHeightTarget));
    }

    private void StepPose(Rect target, float delta)
    {
        var centerX = target.Center.X;
        var top = target.Min.Y;
        var width = target.Width;
        var height = target.Height;
        if (!hasPose)
        {
            cardCenterX.SnapTo(centerX);
            cardTop.SnapTo(top);
            cardWidth.SnapTo(width);
            cardHeight.SnapTo(height);
            hasPose = true;
            return;
        }

        cardCenterX.Step(centerX, PoseFrequency, PoseDamping, delta);
        cardTop.Step(top, PoseFrequency, PoseDamping, delta);
        cardWidth.Step(width, SizeSmoothTime, delta);
        cardHeight.Step(height, SizeSmoothTime, delta);
    }

    private Rect PoseRect(float pop)
    {
        var center = new Vector2(cardCenterX.Value, cardTop.Value + cardHeight.Value * 0.5f);
        var half = new Vector2(cardWidth.Value, cardHeight.Value) * 0.5f * pop;
        return new Rect(center - half, center + half);
    }

    private CoachmarkAction DrawFullCardContent(ImDrawListPtr dl, Rect screen, PhoneTheme theme, in GuideStep step,
        Rect card, float alpha, float contentAlpha, float contentRise, bool live, int index, int count, float scale)
    {
        Material.Frosted(dl, card.Min, card.Max, 28f * scale, scale, alpha);
        dl.PushClipRect(card.Min - new Vector2(0f, 90f * scale), card.Max + new Vector2(0f, 4f * scale), true);
        var emblemCenter = new Vector2(card.Center.X, card.Min.Y + 92f * scale - contentRise);
        Emblem(dl, emblemCenter, theme.Accent, scale, contentAlpha, contentAlpha);
        var titleCenter = new Vector2(card.Center.X, card.Min.Y + screen.Height * 0.18f + 92f * scale - contentRise);
        Typography.DrawCentered(dl, titleCenter, Loc.T(step.Title), theme.TextStrong with { W = contentAlpha },
            TextStyles.Title1);
        var bodyWidth = card.Width * 0.86f;
        var bodyTop = titleCenter.Y + LineHeight(TextStyles.Title1) * 0.5f + 14f * scale;
        DrawWrapped(dl, Loc.T(step.Body), TextStyles.Body, theme.TextMuted with { W = contentAlpha },
            new Vector2(card.Center.X, bodyTop), bodyWidth, scale);
        var buttonSize = new Vector2(MathF.Min(card.Width - 28f * scale, 230f * scale), 46f * scale);
        var buttonCenter = new Vector2(card.Center.X, card.Max.Y - 22f * scale - buttonSize.Y * 0.5f);
        if (count > 1)
        {
            DrawDots(dl, new Vector2(card.Center.X, buttonCenter.Y - buttonSize.Y * 0.5f - 22f * scale), count,
                theme.Accent, theme.TextMuted, contentAlpha, scale);
        }

        var action = CoachmarkAction.None;
        if (Button(dl, buttonCenter, buttonSize, Loc.T(step.ButtonLabel), theme.Accent, alpha, contentAlpha, live,
                scale))
        {
            action = CoachmarkAction.Advance;
        }

        dl.PopClipRect();
        return action;
    }

    private CoachmarkAction DrawCoachmarkContent(ImDrawListPtr dl, Rect screen, PhoneTheme theme, in GuideStep step,
        Rect card, Rect? hole, float alpha, float contentAlpha, float contentRise, float blend, bool live, int index,
        int count, float scale, float delta)
    {
        var radius = 20f * scale;
        var isTap = step.Advance == GuideAdvance.TapTarget && hole.HasValue;
        if (hole is { } h)
        {
            var arrowUp = card.Center.Y > h.Center.Y;
            var arrowX = ClampToRange(h.Center.X, card.Min.X + 24f * scale, card.Max.X - 24f * scale);
            if (blend < 0.05f)
            {
                arrowSlide.SnapTo(arrowX);
            }
            else
            {
                arrowSlide.Step(arrowX, SizeSmoothTime, delta);
            }

            Arrow(dl, arrowSlide.Value, arrowUp ? card.Min.Y : card.Max.Y, 9f * scale, arrowUp,
                alpha * blend * contentAlpha);
        }

        Elevation.Floating(dl, card.Min, card.Max, radius, scale, alpha);
        Material.Frosted(dl, card.Min, card.Max, radius, scale, alpha);
        dl.PushClipRect(card.Min, card.Max, true);
        var titleLine = LineHeight(TextStyles.Title3);
        var bodyLine = LineHeight(TextStyles.Body);
        var cursorY = card.Min.Y + 22f * scale - contentRise;
        Typography.DrawCentered(dl, new Vector2(card.Center.X, cursorY + titleLine * 0.5f), Loc.T(step.Title),
            theme.TextStrong with { W = contentAlpha }, TextStyles.Title3);
        cursorY += titleLine + 10f * scale;
        cursorY = DrawWrapped(dl, Loc.T(step.Body), TextStyles.Body, theme.TextMuted with { W = contentAlpha },
            new Vector2(card.Center.X, cursorY), card.Width - 44f * scale, scale);
        cursorY += 18f * scale;
        if (count > 1)
        {
            DrawDots(dl, new Vector2(card.Center.X, cursorY + 4f * scale), count, theme.Accent, theme.TextMuted,
                contentAlpha, scale);
            cursorY += 16f * scale;
        }

        var action = CoachmarkAction.None;
        if (isTap)
        {
            Typography.DrawCentered(dl, new Vector2(card.Center.X, cursorY + bodyLine * 0.5f), Loc.T(L.Onboarding.TapToContinue),
                theme.Accent with { W = contentAlpha }, TextStyles.FootnoteEmphasized);
        }
        else
        {
            var buttonSize = new Vector2(card.Width - 44f * scale, 46f * scale);
            var buttonCenter = new Vector2(card.Center.X, cursorY + buttonSize.Y * 0.5f);
            if (Button(dl, buttonCenter, buttonSize, Loc.T(step.ButtonLabel), theme.Accent, alpha, contentAlpha, live,
                    scale))
            {
                action = CoachmarkAction.Advance;
            }
        }

        dl.PopClipRect();
        if (isTap && live && hole is { } tapHole && ImGui.IsMouseHoveringRect(tapHole.Min, tapHole.Max))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                action = CoachmarkAction.Advance;
            }
        }

        return action;
    }

    private static float ClampToRange(float value, float min, float max)
    {
        if (min > max)
        {
            return (min + max) * 0.5f;
        }

        return Math.Clamp(value, min, max);
    }

    private static void Spotlight(ImDrawListPtr dl, Rect screen, Rect hole, float rounding, float dim)
    {
        if (dim <= 0f)
        {
            return;
        }

        var color = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, dim));
        var top = Math.Clamp(hole.Min.Y, screen.Min.Y, screen.Max.Y);
        var bottom = Math.Clamp(hole.Max.Y, screen.Min.Y, screen.Max.Y);
        dl.AddRectFilled(screen.Min, new Vector2(screen.Max.X, top), color, rounding, ImDrawFlags.RoundCornersTop);
        dl.AddRectFilled(new Vector2(screen.Min.X, bottom), screen.Max, color, rounding,
            ImDrawFlags.RoundCornersBottom);
        dl.AddRectFilled(new Vector2(screen.Min.X, top), new Vector2(hole.Min.X, bottom), color, 0f, ImDrawFlags.None);
        dl.AddRectFilled(new Vector2(hole.Max.X, top), new Vector2(screen.Max.X, bottom), color, 0f, ImDrawFlags.None);
    }

    private static void SpotlightRing(ImDrawListPtr dl, Rect hole, Vector4 accent, float alpha, float scale)
    {
        var pulse = Styling.Pulse(Styling.PulseCalm);
        var ringRadius = MathF.Min(hole.Width, hole.Height) * 0.34f;
        var glow = accent with { W = (0.12f + 0.10f * pulse) * alpha };
        Squircle.Stroke(dl, hole.Min - new Vector2(4f * scale, 4f * scale),
            hole.Max + new Vector2(4f * scale, 4f * scale), ringRadius + 4f * scale, ImGui.GetColorU32(glow),
            5f * scale);
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
            dl.AddTriangleFilled(new Vector2(x, y - height), new Vector2(x - half, y + 1f),
                new Vector2(x + half, y + 1f), color);
        }
        else
        {
            dl.AddTriangleFilled(new Vector2(x, y + height), new Vector2(x - half, y - 1f),
                new Vector2(x + half, y - 1f), color);
        }
    }

    private void DrawDots(ImDrawListPtr dl, Vector2 center, int count, Vector4 accent, Vector4 muted, float alpha,
        float scale)
    {
        var spacing = 13f * scale;
        var radius = 3f * scale;
        var startX = center.X - (count - 1) * spacing * 0.5f;
        for (var dot = 0; dot < count; dot++)
        {
            dl.AddCircleFilled(new Vector2(startX + dot * spacing, center.Y), radius,
                ImGui.GetColorU32(muted with { W = 0.5f * alpha }), 16);
        }

        var slide = Math.Clamp(dotSlide.Value, 0f, count - 1);
        dl.AddCircleFilled(new Vector2(startX + slide * spacing, center.Y), radius + 0.6f * scale,
            ImGui.GetColorU32(accent with { W = alpha }), 20);
    }

    private static bool Button(ImDrawListPtr dl, Vector2 center, Vector2 size, string label, Vector4 accent,
        float alpha, float contentAlpha, bool live, float scale)
    {
        var half = size * 0.5f;
        var min = center - half;
        var max = center + half;
        var radius = size.Y * 0.5f;
        var hovered = live && ImGui.IsMouseHoveringRect(min, max);
        var fill = hovered ? Palette.Mix(accent, Vector4.One, 0.14f) : accent;
        Squircle.Fill(dl, min, max, radius, ImGui.GetColorU32(fill with { W = fill.W * alpha }));
        Typography.DrawCentered(dl, center, label, Ink with { W = contentAlpha }, TextStyles.Headline);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static float DrawWrapped(ImDrawListPtr dl, string text, in TextStyle style, Vector4 color,
        Vector2 topCenter, float maxWidth, float scale)
    {
        var lineHeight = LineHeight(style) * 1.25f;
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
                Typography.DrawCentered(dl, new Vector2(topCenter.X, y + lineHeight * 0.5f), line, color, style);
                y += lineHeight;
                lineStart = lastSpace + 1;
            }

            lastSpace = index;
            if (atEnd)
            {
                var tail = text.Substring(lineStart);
                Typography.DrawCentered(dl, new Vector2(topCenter.X, y + lineHeight * 0.5f), tail, color, style);
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

    private static float LineHeight(in TextStyle style) => Typography.Measure("Ay", style).Y;

    private static bool Within(Rect screen, Rect rect) =>
        rect.Min.X >= screen.Min.X && rect.Min.Y >= screen.Min.Y && rect.Max.X <= screen.Max.X &&
        rect.Max.Y <= screen.Max.Y;
}
