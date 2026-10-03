using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed partial class CoachmarkOverlay
{
    private const float StrandedSeconds = 0.6f;
    private static bool passthrough;
    private float targetMissing;
    private bool anchorPressed;

    private const float HeroScale = 1.5f;
    private const float HeroDropUnits = 108f;
    private const float BurstSeconds = 1.1f;
    private const int BurstSparks = 12;

    private CoachmarkAction DrawPage(ImDrawListPtr drawList, Rect screen, in GuideStep step, float alpha, bool live,
        int index, int count, float scale, string? appId)
    {
        var centerX = screen.Center.X;
        var heroCenter = new Vector2(centerX, screen.Min.Y + screen.Height * 0.32f);
        var heroReveal = Reveal(0);
        heroCenter.Y += Rise(heroReveal, scale) * 1.5f;
        if (step.Hero == HeroMotif.Finale)
        {
            DrawFinaleHero(drawList, heroCenter, heroReveal, alpha, scale);
        }
        else if (step.Hero == HeroMotif.AppIcon && appId is not null)
        {
            DrawAppHero(drawList, heroCenter, appId, heroReveal, alpha, scale);
        }
        else
        {
            OnboardingHero.Draw(drawList, heroCenter, step.Hero, BrandMark.Violet, scale * HeroScale, heroReveal,
                alpha);
        }

        var bodyWidth = MathF.Min(screen.Width - 56f * scale, 300f * scale);
        var titleReveal = Reveal(3);
        var titleHeight = LineHeight(TextStyles.Title1);
        var titleCenter = new Vector2(centerX, heroCenter.Y + HeroDropUnits * scale + titleHeight * 0.5f +
                                               Rise(titleReveal, scale));
        Typography.DrawCentered(drawList, titleCenter,
            Typography.FitText(Loc.T(step.Title), screen.Width - 40f * scale, TextStyles.Title1),
            Ink with { W = Ink.W * alpha * titleReveal }, TextStyles.Title1);
        var bodyReveal = Reveal(4);
        Typography.DrawWrappedCentered(drawList, Loc.T(step.Body), TextStyles.Body,
            InkMuted with { W = InkMuted.W * alpha * bodyReveal },
            new Vector2(centerX, titleCenter.Y - Rise(titleReveal, scale) + titleHeight * 0.5f + 12f * scale +
                                 Rise(bodyReveal, scale)), bodyWidth);
        var buttonWidth = MathF.Min(screen.Width - 48f * scale, PageButtonMaxUnits * scale);
        var buttonHeight = PageButtonHeightUnits * scale;
        var buttonBottom = screen.Max.Y - PageBottomUnits * scale;
        var button = new Rect(new Vector2(centerX - buttonWidth * 0.5f, buttonBottom - buttonHeight),
            new Vector2(centerX + buttonWidth * 0.5f, buttonBottom));
        var controlsAlpha = alpha * Reveal(6);
        if (count > 1)
        {
            var segmentsWidth = MathF.Min(buttonWidth * 0.6f, count * 20f * scale);
            DrawSegments(drawList, new Vector2(centerX - segmentsWidth * 0.5f, button.Min.Y - 30f * scale),
                segmentsWidth, index, count, controlsAlpha, scale);
        }

        var action = CoachmarkAction.None;
        if (BrandButton(drawList, button, Loc.T(step.ButtonLabel), controlsAlpha, live))
        {
            action = CoachmarkAction.Advance;
        }

        if (index < count - 1 && SkipLabel(drawList, new Vector2(centerX, button.Max.Y + 24f * scale), true,
                controlsAlpha, live))
        {
            action = CoachmarkAction.Skip;
        }

        return action;
    }

    private void DrawFinaleHero(ImDrawListPtr drawList, Vector2 center, float reveal, float alpha, float scale)
    {
        var size = FinaleMarkUnits * scale;
        var burst = (stepClock - 0.15f) / BurstSeconds;
        BrandMark.Shockwave(drawList, center, size, burst, alpha, scale);
        DrawBurst(drawList, center, size, burst, alpha, scale);
        var bob = MathF.Sin(Pulse.Phase(6200.0) * MathF.PI * 2f) * 3f * scale * reveal;
        BrandMark.TryDraw(drawList, center + new Vector2(0f, bob), size * (0.7f + 0.3f * reveal), alpha * reveal,
            scale);
    }

    private void DrawAppHero(ImDrawListPtr drawList, Vector2 center, string appId, float reveal, float alpha,
        float scale)
    {
        var size = FinaleMarkUnits * scale * (0.7f + 0.3f * reveal);
        var accent = AppAccents.For(appId);
        BrandMark.Shockwave(drawList, center, size, (stepClock - 0.15f) / BurstSeconds, alpha, scale);
        var glow = ImGui.GetColorU32(accent with { W = 0.05f * alpha * reveal });
        for (var layerIndex = 0; layerIndex < 6; layerIndex++)
        {
            var spread = size * (0.5f + 0.16f * (layerIndex + 1));
            Squircle.Fill(drawList, center - new Vector2(spread, spread), center + new Vector2(spread, spread),
                spread * 2f * BrandMark.CornerFraction * 1.4f, glow);
        }

        var bob = MathF.Sin(Pulse.Phase(6200.0) * MathF.PI * 2f) * 3f * scale * reveal;
        var half = new Vector2(size * 0.5f, size * 0.5f);
        var iconCenter = center + new Vector2(0f, bob);
        var radius = size * BrandMark.CornerFraction;
        if (AppIconTile.TryDraw(drawList, appId, accent, iconCenter - half, iconCenter + half, radius, alpha * reveal,
                true, scale))
        {
            Squircle.StrokeDirectional(drawList, iconCenter - half, iconCenter + half, radius,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.40f * alpha * reveal)), 1.3f * scale,
                new Vector2(-0.55f, -1f), 2.2f);
            return;
        }

        BrandMark.TryDraw(drawList, iconCenter, size, alpha * reveal, scale);
    }

    private static void DrawBurst(ImDrawListPtr drawList, Vector2 center, float size, float progress, float alpha,
        float scale)
    {
        if (progress <= 0f || progress >= 1f || alpha <= 0.001f)
        {
            return;
        }

        var fade = (1f - progress) * (1f - progress) * alpha;
        var reach = Spring.Settle(progress, 0.3f);
        for (var sparkIndex = 0; sparkIndex < BurstSparks; sparkIndex++)
        {
            var angle = sparkIndex * (MathF.PI * 2f / BurstSparks) - MathF.PI * 0.5f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var distance = size * (0.55f + (sparkIndex % 2 == 0 ? 0.75f : 0.55f) * reach);
            var spark = center + direction * distance;
            var ink = sparkIndex % 3 == 0 ? BrandMark.Vine : sparkIndex % 3 == 1 ? BrandMark.Lilac : Vector4.One;
            drawList.AddCircleFilled(spark, 5f * scale, ImGui.GetColorU32(ink with { W = 0.16f * fade }), 12);
            drawList.AddCircleFilled(spark, 2.2f * scale, ImGui.GetColorU32(ink with { W = fade }), 10);
        }
    }

    private CoachmarkAction DrawCoachmark(ImDrawListPtr drawList, Rect screen, in GuideStep step, Rect? hole,
        float alpha, float contentAlpha, float contentRise, float contentProgress, float blend, bool live, int index,
        int count, float scale, float delta)
    {
        targetMissing = hole.HasValue ? 0f : targetMissing + delta;
        var stranded = step.IsAction && targetMissing > StrandedSeconds;
        var isTap = (step.Advance == GuideAdvance.TapTarget && hole.HasValue) || (step.IsAction && !stranded);
        var size = MeasureCard(screen, step, isTap, scale);
        StepPose(CoachmarkTarget(screen, size, hole, scale), screen.Min, delta);
        var card = PoseRect(screen.Min, 0.94f + 0.06f * alpha);
        if (hole is { } spotlight && (card.Min.Y >= spotlight.Max.Y || card.Max.Y <= spotlight.Min.Y))
        {
            var below = card.Min.Y >= spotlight.Max.Y;
            var anchorX = ClampToRange(spotlight.Center.X, card.Min.X + 30f * scale, card.Max.X - 30f * scale);
            var from = new Vector2(anchorX, below ? card.Min.Y : card.Max.Y);
            var to = new Vector2(spotlight.Center.X,
                below ? spotlight.Max.Y + 5f * scale : spotlight.Min.Y - 5f * scale);
            Tether(drawList, from, to, alpha * blend * contentAlpha, contentProgress, scale);
        }

        if (hole is { } target && step.Gesture != GuideGesture.None)
        {
            DrawGesture(drawList, target.Center, step.Gesture, stepClock, alpha * blend * contentAlpha, scale);
        }

        var action = DrawCard(drawList, step, card, alpha, contentAlpha, contentRise, isTap, live, index, count,
            scale);
        if (step.IsAction && step.Condition == GuideCondition.TapAnchor && live && hole is { } actionHole)
        {
            var inside = UiInteract.HoverWindowOnly(actionHole.Min, actionHole.Max);
            if (inside && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                anchorPressed = true;
            }

            if (anchorPressed && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                anchorPressed = false;
                if (inside)
                {
                    action = CoachmarkAction.Advance;
                }
            }
        }

        if (!step.IsAction && isTap && live && hole is { } tapHole && UiInteract.Hover(tapHole.Min, tapHole.Max))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                action = CoachmarkAction.Advance;
            }
        }

        return action;
    }

    private CoachmarkAction DrawEdge(ImDrawListPtr drawList, Rect screen, in GuideStep step, Rect anchor, float alpha,
        float contentAlpha, float contentRise, float contentProgress, bool live, int index, int count, float scale,
        float delta)
    {
        var size = MeasureCard(screen, step, step.IsAction, scale);
        StepPose(EdgeCardTarget(screen, size, anchor, scale), screen.Min, delta);
        var card = PoseRect(screen.Min, 0.94f + 0.06f * alpha);
        var left = anchor.Center.X < screen.Center.X;
        var clipMin = new Vector2(MathF.Min(anchor.Min.X, card.Min.X) - 40f * scale, screen.Min.Y);
        var clipMax = new Vector2(MathF.Max(anchor.Max.X, card.Max.X) + 40f * scale, screen.Max.Y);
        drawList.PushClipRect(clipMin, clipMax, false);
        var tetherY = ClampToRange(anchor.Center.Y, card.Min.Y + 26f * scale, card.Max.Y - 26f * scale);
        var from = new Vector2(left ? card.Min.X : card.Max.X, tetherY);
        var to = new Vector2(left ? anchor.Max.X + 6f * scale : anchor.Min.X - 6f * scale, anchor.Center.Y);
        Tether(drawList, from, to, alpha * contentAlpha, contentProgress, scale);
        var ringRect = anchor.Inset(-3f * scale);
        Ring(drawList, ringRect, MathF.Min(ringRect.Width, ringRect.Height) * 0.45f, alpha * contentAlpha, scale);
        drawList.PopClipRect();
        return DrawCard(drawList, step, card, alpha, contentAlpha, contentRise, step.IsAction, live, index, count,
            scale);
    }

    private Vector2 MeasureCard(Rect screen, in GuideStep step, bool isTap, float scale)
    {
        var width = MathF.Min(screen.Width - 24f * scale, CardMaxWidthUnits * scale);
        var inner = width - 2f * CardPadUnits * scale;
        var bodyLines = Wrap(Loc.T(step.Body), inner).Length;
        var height = CardPadUnits * scale + LineHeight(TextStyles.FootnoteEmphasized) + 10f * scale +
                     LineHeight(TextStyles.Title2) + 6f * scale + bodyLines * LineHeight(TextStyles.Body) * 1.22f +
                     16f * scale + SegmentHeightUnits * scale + 18f * scale +
                     (isTap ? LineHeight(TextStyles.FootnoteEmphasized) + 6f * scale : ButtonHeightUnits * scale) +
                     CardPadUnits * scale;
        return new Vector2(width, height);
    }

    private static Rect CoachmarkTarget(Rect screen, Vector2 size, Rect? hole, float scale)
    {
        var margin = 14f * scale;
        var gap = 30f * scale;
        float centerX;
        float top;
        if (hole is { } spotlight)
        {
            var fitsBelow = spotlight.Max.Y + gap + size.Y + margin <= screen.Max.Y;
            var fitsAbove = spotlight.Min.Y - gap - size.Y - margin >= screen.Min.Y;
            if (fitsBelow)
            {
                top = spotlight.Max.Y + gap;
            }
            else if (fitsAbove)
            {
                top = spotlight.Min.Y - gap - size.Y;
            }
            else
            {
                top = screen.Max.Y - spotlight.Max.Y >= spotlight.Min.Y - screen.Min.Y
                    ? spotlight.Max.Y + gap
                    : spotlight.Min.Y - gap - size.Y;
            }

            centerX = ClampToRange(spotlight.Center.X, screen.Min.X + margin + size.X * 0.5f,
                screen.Max.X - margin - size.X * 0.5f);
        }
        else
        {
            centerX = screen.Center.X;
            top = screen.Center.Y - size.Y * 0.5f;
        }

        top = ClampToRange(top, screen.Min.Y + margin, screen.Max.Y - margin - size.Y);
        return new Rect(new Vector2(centerX - size.X * 0.5f, top), new Vector2(centerX + size.X * 0.5f, top + size.Y));
    }

    private static Rect EdgeCardTarget(Rect screen, Vector2 size, Rect anchor, float scale)
    {
        var margin = 16f * scale;
        var left = anchor.Center.X < screen.Center.X;
        var centerX = left ? screen.Min.X + margin + size.X * 0.5f : screen.Max.X - margin - size.X * 0.5f;
        var centerY = ClampToRange(anchor.Center.Y, screen.Min.Y + margin + size.Y * 0.5f,
            screen.Max.Y - margin - size.Y * 0.5f);
        var half = size * 0.5f;
        return new Rect(new Vector2(centerX - half.X, centerY - half.Y), new Vector2(centerX + half.X, centerY + half.Y));
    }

    private void StepPose(Rect target, Vector2 origin, float delta)
    {
        var centerX = target.Center.X - origin.X;
        var top = target.Min.Y - origin.Y;
        if (!hasPose)
        {
            cardCenterX.SnapTo(centerX);
            cardTop.SnapTo(top);
            cardWidth.SnapTo(target.Width);
            cardHeight.SnapTo(target.Height);
            hasPose = true;
            return;
        }

        cardCenterX.Step(centerX, Motion.Sheet, delta);
        cardTop.Step(top, Motion.Sheet, delta);
        cardWidth.Step(target.Width, Motion.Release, delta);
        cardHeight.Step(target.Height, Motion.Release, delta);
    }

    private Rect PoseRect(Vector2 origin, float pop)
    {
        var center = origin + new Vector2(cardCenterX.Value, cardTop.Value + cardHeight.Value * 0.5f);
        var half = new Vector2(cardWidth.Value, cardHeight.Value) * 0.5f * pop;
        return new Rect(center - half, center + half);
    }

    private CoachmarkAction DrawCard(ImDrawListPtr drawList, in GuideStep step, Rect card, float alpha,
        float contentAlpha, float contentRise, bool isTap, bool live, int index, int count, float scale)
    {
        var radius = CardRadiusUnits * scale;
        passthrough = step.IsAction;
        if (passthrough && live)
        {
            UiInteract.HoverOverlay(card);
        }

        Elevation.Floating(drawList, card.Min, card.Max, radius, scale, alpha);
        Material.LiquidGlass(drawList, card.Min, card.Max, radius, scale, GlassTone.Dark, 0f, alpha);
        Material.TopGlow(drawList, card.Min, card.Max, radius, BrandMark.Violet, 0.4f, 0.12f * alpha);
        var pad = CardPadUnits * scale;
        var left = card.Min.X + pad;
        var inner = card.Width - 2f * pad;
        var action = CoachmarkAction.None;
        drawList.PushClipRect(card.Min, card.Max, true);
        var y = card.Min.Y + pad - contentRise;
        var headerHeight = LineHeight(TextStyles.FootnoteEmphasized);
        if (count > 1)
        {
            Typography.Draw(drawList, new Vector2(left, y), Counter(index, count),
                BrandMark.Lilac with { W = contentAlpha }, TextStyles.FootnoteEmphasized);
        }

        if ((index < count - 1 || step.IsAction) &&
            SkipLabel(drawList, new Vector2(card.Max.X - pad, y + headerHeight * 0.5f), false,
                contentAlpha, live))
        {
            action = CoachmarkAction.Skip;
        }

        y += headerHeight + 10f * scale;
        Typography.Draw(drawList, new Vector2(left, y),
            Typography.FitText(Loc.T(step.Title), inner, TextStyles.Title2), Ink with { W = Ink.W * contentAlpha },
            TextStyles.Title2);
        y += LineHeight(TextStyles.Title2) + 6f * scale;
        var lines = Wrap(Loc.T(step.Body), inner);
        var lineHeight = LineHeight(TextStyles.Body) * 1.22f;
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            Typography.Draw(drawList, new Vector2(left, y + lineIndex * lineHeight), lines[lineIndex],
                InkMuted with { W = InkMuted.W * contentAlpha }, TextStyles.Body);
        }

        y += lines.Length * lineHeight + 16f * scale;
        if (count > 1)
        {
            DrawSegments(drawList, new Vector2(left, y), inner, index, count, contentAlpha, scale);
        }

        y += SegmentHeightUnits * scale + 18f * scale;
        if (isTap)
        {
            var breath = 0.65f + 0.35f * Pulse.Wave(Pulse.Calm);
            Typography.DrawCentered(drawList,
                new Vector2(card.Center.X, y + LineHeight(TextStyles.FootnoteEmphasized) * 0.5f),
                Loc.T(step.IsAction ? L.Onboarding.TryItNow : L.Onboarding.TapToContinue),
                BrandMark.Lilac with { W = contentAlpha * breath },
                TextStyles.FootnoteEmphasized);
        }

        drawList.PopClipRect();
        if (!isTap)
        {
            var button = new Rect(new Vector2(left, y), new Vector2(left + inner, y + ButtonHeightUnits * scale));
            if (BrandButton(drawList, button, Loc.T(step.ButtonLabel), contentAlpha, live))
            {
                action = CoachmarkAction.Advance;
            }
        }

        return action;
    }

    private void DrawSegments(ImDrawListPtr drawList, Vector2 origin, float width, int index, int count, float alpha,
        float scale)
    {
        if (alpha <= 0.001f || count <= 0)
        {
            return;
        }

        var gap = SegmentGapUnits * scale;
        var height = SegmentHeightUnits * scale;
        var segment = (width - gap * (count - 1)) / count;
        var radius = height * 0.5f;
        var fill = Math.Clamp(segmentFill.Value, 0f, 1f);
        for (var segmentIndex = 0; segmentIndex < count; segmentIndex++)
        {
            var min = new Vector2(origin.X + segmentIndex * (segment + gap), origin.Y);
            var max = new Vector2(min.X + segment, origin.Y + height);
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(Track with { W = Track.W * alpha }), radius);
            var amount = segmentIndex < index ? 1f : segmentIndex == index ? fill : 0f;
            if (amount <= 0.001f)
            {
                continue;
            }

            var fillMax = new Vector2(min.X + MathF.Max(height, segment * amount), max.Y);
            drawList.AddRectFilledMultiColor(min, fillMax,
                ImGui.GetColorU32(BrandMark.Violet with { W = alpha }),
                ImGui.GetColorU32(BrandMark.Lilac with { W = alpha }),
                ImGui.GetColorU32(BrandMark.Lilac with { W = alpha }),
                ImGui.GetColorU32(BrandMark.Violet with { W = alpha }));
        }
    }

    private static bool SkipLabel(ImDrawListPtr drawList, Vector2 anchor, bool centered, float alpha, bool live)
    {
        if (alpha <= 0.001f)
        {
            return false;
        }

        var scale = UiScale.Current;
        var label = Loc.T(L.Onboarding.SkipTour);
        var size = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var center = centered ? anchor : new Vector2(anchor.X - size.X * 0.5f, anchor.Y);
        var pad = new Vector2(8f * scale, 6f * scale);
        var min = center - size * 0.5f - pad;
        var max = center + size * 0.5f + pad;
        var hovered = live && (passthrough ? UiInteract.HoverWindowOnly(min, max) : UiInteract.Hover(min, max));
        var ink = hovered ? Ink : InkQuiet;
        Typography.DrawCentered(drawList, center, label, ink with { W = ink.W * alpha }, TextStyles.FootnoteEmphasized);
        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static bool BrandButton(ImDrawListPtr drawList, Rect rect, string label, float alpha, bool live)
    {
        if (alpha <= 0.001f)
        {
            return false;
        }

        var hovered = live && (passthrough ? UiInteract.HoverWindowOnly(rect.Min, rect.Max)
            : UiInteract.Hover(rect.Min, rect.Max));
        MotionButton.Brand(drawList, rect, label, label, alpha, hovered, true, DisabledFill, InkQuiet);
        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static void Tether(ImDrawListPtr drawList, Vector2 from, Vector2 to, float alpha, float progress,
        float scale)
    {
        if (alpha <= 0.001f || progress <= 0.001f)
        {
            return;
        }

        const int pieces = 10;
        var end = Vector2.Lerp(from, to, Spring.Settle(progress, 0.35f));
        var thickness = 1.6f * scale;
        for (var pieceIndex = 0; pieceIndex < pieces; pieceIndex++)
        {
            var start = Vector2.Lerp(from, end, pieceIndex / (float)pieces);
            var stop = Vector2.Lerp(from, end, (pieceIndex + 1f) / pieces);
            var weight = 0.25f + 0.75f * ((pieceIndex + 1f) / pieces);
            drawList.AddLine(start, stop, ImGui.GetColorU32(BrandMark.Lilac with { W = weight * 0.85f * alpha }),
                thickness);
        }

        var dot = TetherDotUnits * scale;
        drawList.AddCircleFilled(end, dot * 2.6f, ImGui.GetColorU32(BrandMark.Violet with { W = 0.25f * alpha }), 16);
        drawList.AddCircleFilled(end, dot, ImGui.GetColorU32(BrandMark.Lilac with { W = alpha }), 12);
    }
}
