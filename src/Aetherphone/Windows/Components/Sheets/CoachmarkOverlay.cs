using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal enum CoachmarkAction
{
    None,
    Advance,
    Skip,
}

internal sealed partial class CoachmarkOverlay
{
    private const float DimStrength = 0.74f;
    private const float StageStrength = 0.95f;
    private const float FeatherUnits = 20f;
    private const int FeatherRings = 7;
    private const float HolePadUnits = 7f;
    private const float HoleRadiusCapUnits = 24f;
    private const float IrisGrowth = 2.6f;
    private const double PingPeriodMs = 2200.0;
    private const float PingReachUnits = 18f;
    private const float CardPadUnits = 20f;
    private const float CardRadiusUnits = 26f;
    private const float CardMaxWidthUnits = 340f;
    private const float SegmentHeightUnits = 4f;
    private const float SegmentGapUnits = 4f;
    private const float ButtonHeightUnits = 46f;
    private const float PageButtonHeightUnits = 50f;
    private const float PageButtonMaxUnits = 320f;
    private const float PageBottomUnits = 64f;
    private const float TetherDotUnits = 2.8f;
    private const float FinaleMarkUnits = 108f;
    private const float RevealDelaySeconds = 0.10f;
    private const float RevealStaggerSeconds = 0.08f;
    private const float RevealRiseUnits = 14f;

    private static readonly Vector4 Ink = new(1f, 1f, 1f, 0.97f);
    private static readonly Vector4 InkMuted = new(0.90f, 0.88f, 1f, 0.70f);
    private static readonly Vector4 InkQuiet = new(0.90f, 0.88f, 1f, 0.55f);
    private static readonly Vector4 Track = new(1f, 1f, 1f, 0.14f);
    private static readonly Vector4 DimTone = new(0.014f, 0.010f, 0.040f, 1f);
    private static readonly Vector4 DisabledFill = new(1f, 1f, 1f, 0.14f);

    private Spring cardCenterX;
    private Spring cardTop;
    private Spring cardWidth;
    private Spring cardHeight;
    private Spring holeMinX;
    private Spring holeMinY;
    private Spring holeMaxX;
    private Spring holeMaxY;
    private Spring holeBlend;
    private Spring stageBlend;
    private Spring segmentFill;
    private bool hasPose;
    private bool hasHole;
    private bool lastFullCard;
    private int lastIndex = -1;
    private float stepClock;
    private string counterText = string.Empty;
    private int counterIndex = -1;
    private int counterCount = -1;
    private string counterLanguage = string.Empty;
    private string wrapSource = string.Empty;
    private float wrapWidth;
    private string[] wrapLines = Array.Empty<string>();

    public void Reset()
    {
        hasPose = false;
        hasHole = false;
        lastIndex = -1;
        holeBlend.SnapTo(0f);
        stageBlend.SnapTo(0f);
        segmentFill.SnapTo(0f);
    }

    public CoachmarkAction Draw(Rect screen, PhoneTheme theme, in GuideStep step, Rect? anchor, float presence,
        float textProgress, int index, int count, bool interactive, string? appId)
    {
        var scale = UiScale.Current;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var drawList = ImGui.GetForegroundDrawList();
        var rounding = theme.ScreenRounding * scale;
        var alpha = Math.Clamp(presence * 1.5f, 0f, 1f);
        if (index != lastIndex)
        {
            lastIndex = index;
            stepClock = 0f;
            anchorPressed = false;
            targetMissing = 0f;
            segmentFill.SnapTo(0f);
        }

        stepClock += delta;
        var contentProgress = Math.Clamp((textProgress - 0.12f) / 0.55f, 0f, 1f);
        var contentAlpha = contentProgress * alpha;
        var contentRise = (1f - contentProgress) * 8f * scale;
        var live = interactive && presence > 0.55f && textProgress > 0.45f;
        var fullCard = step.Surface == GuideSurface.FullCard;
        passthrough = false;
        if (fullCard != lastFullCard)
        {
            lastFullCard = fullCard;
            hasPose = false;
        }

        var edge = !fullCard && IsEdgeAnchor(screen, step, anchor);
        var targetHole = edge || fullCard ? null : PadHole(screen, anchor, step, scale);
        StepHole(targetHole, delta);
        holeBlend.Step(targetHole.HasValue ? 1f : 0f, Motion.Sheet, delta);
        stageBlend.Step(fullCard ? 1f : 0f, Motion.Sheet, delta);
        segmentFill.Step(1f, Motion.Sheet * 1.6f, delta);
        var blend = Math.Clamp(holeBlend.Value, 0f, 1f);
        var stage = Math.Clamp(stageBlend.Value, 0f, 1f);
        var hole = CurrentHole();
        drawList.PushClipRect(screen.Min, screen.Max, true);
        DrawDim(drawList, screen, rounding, alpha, blend, stage, hole, scale);
        CoachmarkAction action;
        if (fullCard)
        {
            action = DrawPage(drawList, screen, step, alpha, live, index, count, scale, appId);
        }
        else if (edge)
        {
            action = DrawEdge(drawList, screen, step, anchor!.Value, alpha, contentAlpha, contentRise,
                contentProgress, live, index, count, scale, delta);
        }
        else
        {
            action = DrawCoachmark(drawList, screen, step, targetHole.HasValue ? hole : null, alpha, contentAlpha,
                contentRise, contentProgress, blend, live, index, count, scale, delta);
        }

        drawList.PopClipRect();
        return action;
    }

    private static bool IsEdgeAnchor(Rect screen, in GuideStep step, Rect? anchor)
    {
        if (step.AnchorKey is null || anchor is not { } rect)
        {
            return false;
        }

        return rect.Center.X < screen.Min.X || rect.Center.X > screen.Max.X;
    }

    private static Rect? PadHole(Rect screen, Rect? anchor, in GuideStep step, float scale)
    {
        if (step.AnchorKey is null || anchor is not { } rect)
        {
            return null;
        }

        var padded = Intersect(screen, rect.Inset(-HolePadUnits * scale));
        return padded.Width > 1f && padded.Height > 1f ? padded : null;
    }

    private static Rect Intersect(Rect a, Rect b) =>
        new(Vector2.Max(a.Min, b.Min), Vector2.Min(a.Max, b.Max));

    private void StepHole(Rect? target, float delta)
    {
        if (target is not { } rect)
        {
            return;
        }

        if (!hasHole)
        {
            var center = rect.Center;
            var half = rect.Size * 0.5f * IrisGrowth;
            holeMinX.SnapTo(center.X - half.X);
            holeMinY.SnapTo(center.Y - half.Y);
            holeMaxX.SnapTo(center.X + half.X);
            holeMaxY.SnapTo(center.Y + half.Y);
            hasHole = true;
        }

        holeMinX.Step(rect.Min.X, Motion.Sheet, delta);
        holeMinY.Step(rect.Min.Y, Motion.Sheet, delta);
        holeMaxX.Step(rect.Max.X, Motion.Sheet, delta);
        holeMaxY.Step(rect.Max.Y, Motion.Sheet, delta);
    }

    private Rect CurrentHole() =>
        new(new Vector2(holeMinX.Value, holeMinY.Value), new Vector2(holeMaxX.Value, holeMaxY.Value));

    private static float HoleRadius(Rect hole, float scale) =>
        MathF.Min(MathF.Min(hole.Width, hole.Height) * 0.32f, HoleRadiusCapUnits * scale);

    private void DrawDim(ImDrawListPtr drawList, Rect screen, float rounding, float alpha, float blend, float stage,
        Rect hole, float scale)
    {
        if (stage > 0.001f)
        {
            BrandMark.DrawStage(drawList, screen, rounding, StageStrength * alpha * stage, false, 1f);
        }

        var open = 1f - stage;
        var veil = DimStrength * alpha * (1f - blend) * open;
        if (veil > 0.001f)
        {
            Squircle.Fill(drawList, screen.Min, screen.Max, rounding, ImGui.GetColorU32(DimTone with { W = veil }));
        }

        if (!hasHole || blend <= 0.001f || open <= 0.001f)
        {
            return;
        }

        Spotlight(drawList, screen, hole, rounding, DimStrength * alpha * blend * open, scale);
        Ring(drawList, hole, HoleRadius(hole, scale), alpha * blend * open, scale);
    }

    private static void Spotlight(ImDrawListPtr drawList, Rect screen, Rect hole, float rounding, float dim,
        float scale)
    {
        if (dim <= 0.001f)
        {
            return;
        }

        var radius = HoleRadius(hole, scale);
        var feather = FeatherUnits * scale;
        var outer = hole.Inset(-feather);
        var color = ImGui.GetColorU32(DimTone with { W = dim });
        var top = Math.Clamp(outer.Min.Y, screen.Min.Y, screen.Max.Y);
        var bottom = Math.Clamp(outer.Max.Y, screen.Min.Y, screen.Max.Y);
        if (top > screen.Min.Y)
        {
            drawList.AddRectFilled(screen.Min, new Vector2(screen.Max.X, top), color, rounding,
                ImDrawFlags.RoundCornersTop);
        }

        if (bottom < screen.Max.Y)
        {
            drawList.AddRectFilled(new Vector2(screen.Min.X, bottom), screen.Max, color, rounding,
                ImDrawFlags.RoundCornersBottom);
        }

        drawList.AddRectFilled(new Vector2(screen.Min.X, top), new Vector2(MathF.Max(screen.Min.X, outer.Min.X), bottom),
            color);
        drawList.AddRectFilled(new Vector2(MathF.Min(screen.Max.X, outer.Max.X), top), new Vector2(screen.Max.X, bottom),
            color);
        Squircle.FillOutsideCorners(drawList, outer.Min, outer.Max, radius + feather, color, 0f);
        var band = feather / FeatherRings;
        for (var ringIndex = 0; ringIndex < FeatherRings; ringIndex++)
        {
            var position = (ringIndex + 0.5f) / FeatherRings;
            var inset = feather * position;
            var weight = position * position * (3f - 2f * position);
            Squircle.Stroke(drawList, hole.Min - new Vector2(inset, inset), hole.Max + new Vector2(inset, inset),
                radius + inset, ImGui.GetColorU32(DimTone with { W = dim * weight }), band + 0.6f * scale);
        }
    }

    private static void Ring(ImDrawListPtr drawList, Rect hole, float radius, float alpha, float scale)
    {
        if (alpha <= 0.001f)
        {
            return;
        }

        var pulse = Pulse.Wave(Pulse.Calm);
        var glowPad = 4f * scale;
        Squircle.Stroke(drawList, hole.Min - new Vector2(glowPad, glowPad), hole.Max + new Vector2(glowPad, glowPad),
            radius + glowPad, ImGui.GetColorU32(BrandMark.Violet with { W = (0.16f + 0.10f * pulse) * alpha }),
            6f * scale);
        Squircle.Stroke(drawList, hole.Min, hole.Max, radius,
            ImGui.GetColorU32(BrandMark.Lilac with { W = (0.62f + 0.30f * pulse) * alpha }), 2f * scale);
        var ping = Pulse.Phase(PingPeriodMs);
        var reach = PingReachUnits * scale * Spring.Settle(ping, 0.3f);
        var fade = (1f - ping) * (1f - ping) * 0.5f * alpha;
        Squircle.Stroke(drawList, hole.Min - new Vector2(reach, reach), hole.Max + new Vector2(reach, reach),
            radius + reach, ImGui.GetColorU32(BrandMark.Lilac with { W = fade }), 1.4f * scale);
    }

    private float Reveal(int order) =>
        Spring.Settle(stepClock - RevealDelaySeconds - order * RevealStaggerSeconds, Motion.Sheet);

    private static float Rise(float reveal, float scale) => (1f - reveal) * RevealRiseUnits * scale;

    private string Counter(int index, int count)
    {
        var language = Loc.Current.Code;
        if (index == counterIndex && count == counterCount && string.Equals(language, counterLanguage,
                StringComparison.Ordinal))
        {
            return counterText;
        }

        counterIndex = index;
        counterCount = count;
        counterLanguage = language;
        counterText = Loc.T(L.Onboarding.StepCounter, index + 1, count);
        return counterText;
    }

    private string[] Wrap(string text, float width)
    {
        if (string.Equals(text, wrapSource, StringComparison.Ordinal) && MathF.Abs(width - wrapWidth) < 0.5f)
        {
            return wrapLines;
        }

        wrapSource = text;
        wrapWidth = width;
        wrapLines = Typography.WrapText(text, TextStyles.Body, width);
        return wrapLines;
    }

    private static float LineHeight(in TextStyle style) => Typography.Measure("Ay", style).Y;

    private static float ClampToRange(float value, float min, float max)
    {
        if (min > max)
        {
            return (min + max) * 0.5f;
        }

        return Math.Clamp(value, min, max);
    }
}
