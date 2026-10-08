using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Stage;

internal enum StageTextRole : byte
{
    State,
    Status,
    Amount,
    Label,
}

internal readonly struct StageTextPlan
{
    public readonly TextStyle Style;
    public readonly float PadX;
    public readonly float PadY;
    public readonly float MinimumCapsuleHeight;
    public readonly float ShadowOffset;

    public StageTextPlan(TextStyle style, float padX, float padY, float minimumCapsuleHeight, float shadowOffset)
    {
        Style = style;
        PadX = padX;
        PadY = padY;
        MinimumCapsuleHeight = minimumCapsuleHeight;
        ShadowOffset = shadowOffset;
    }

    public float CapsuleHeight(float lineHeight) => MathF.Max(MinimumCapsuleHeight, lineHeight + PadY * 2f);
}

internal static class StageText
{
    public const float CapsulePadX = 12f;
    public const float CapsulePadY = 5f;
    public const float MinimumCapsuleHeight = 28f;
    public const float ShadowOffset = 1.5f;
    public const float ShadowAlpha = 0.6f;
    public const float FitSlack = 0.5f;

    public static readonly Vector4 Strong = CasinoColors.InkTitle;
    public static readonly Vector4 Muted = CasinoColors.InkMuted;
    public static readonly Vector4 CapsuleFill = new(0.035f, 0.03f, 0.06f, 0.8f);

    private static readonly Vector4 Shadow = new(0f, 0f, 0f, ShadowAlpha);
    private static readonly Vector4 CapsuleRim = new(1f, 1f, 1f, 0.08f);

    public static TextStyle Minimum(StageTextRole role) => role switch
    {
        StageTextRole.State => TextStyles.Title2,
        StageTextRole.Amount => TextStyles.Title3,
        _ => TextStyles.Footnote,
    };

    public static TextStyle Resolve(StageTextRole role, in TextStyle requested)
    {
        var minimum = Minimum(role);
        return requested.Scale >= minimum.Scale ? requested : minimum with { Weight = requested.Weight };
    }

    public static StageTextPlan Plan(StageTextRole role, in TextStyle requested, float scale) =>
        new(Resolve(role, requested), CapsulePadX * scale, CapsulePadY * scale, MinimumCapsuleHeight * scale,
            MathF.Max(1f, ShadowOffset * scale));

    public static bool Fits(float textWidth, float maxWidth) => textWidth <= maxWidth + FitSlack;

    public static Vector2 State(ImDrawListPtr drawList, Vector2 center, string text, float maxWidth, MarqueeId id) =>
        State(drawList, center, text, maxWidth, id, TextStyles.Title2, Strong);

    public static Vector2 State(ImDrawListPtr drawList, Vector2 center, string text, float maxWidth, MarqueeId id,
        in TextStyle requested, Vector4 ink)
    {
        var plan = Plan(StageTextRole.State, requested, UiScale.Current);
        var size = Typography.Measure(text, plan.Style);
        if (Fits(size.X, maxWidth))
        {
            var origin = new Vector2(center.X - size.X * 0.5f, center.Y - size.Y * 0.5f);
            Typography.Draw(drawList, origin + new Vector2(0f, plan.ShadowOffset), text, Shadow, plan.Style);
            Typography.Draw(drawList, origin, text, ink, plan.Style);
            return size;
        }

        return OnCapsule(drawList, center, text, maxWidth, id, plan, ink);
    }

    public static Vector2 Status(ImDrawListPtr drawList, Vector2 center, string text, float maxWidth, MarqueeId id,
        bool overWorld = true) =>
        Status(drawList, center, text, maxWidth, id, TextStyles.FootnoteEmphasized, overWorld);

    public static Vector2 Status(ImDrawListPtr drawList, Vector2 center, string text, float maxWidth, MarqueeId id,
        in TextStyle requested, bool overWorld)
    {
        var plan = Plan(StageTextRole.Status, requested, UiScale.Current);
        if (overWorld)
        {
            return OnCapsule(drawList, center, text, maxWidth, id, plan, Strong);
        }

        return Line(drawList, center, text, maxWidth, id, plan.Style, Strong);
    }

    public static Vector2 Amount(ImDrawListPtr drawList, Vector2 center, string text, float maxWidth, MarqueeId id) =>
        Amount(drawList, center, text, maxWidth, id, TextStyles.Title3, CasinoColors.Money);

    public static Vector2 Amount(ImDrawListPtr drawList, Vector2 center, string text, float maxWidth, MarqueeId id,
        in TextStyle requested, Vector4 ink)
    {
        var plan = Plan(StageTextRole.Amount, requested, UiScale.Current);
        var size = Typography.Measure(text, plan.Style);
        if (Fits(size.X, maxWidth))
        {
            var origin = new Vector2(center.X - size.X * 0.5f, center.Y - size.Y * 0.5f);
            Typography.Draw(drawList, origin + new Vector2(0f, plan.ShadowOffset), text, Shadow, plan.Style);
            Typography.Draw(drawList, origin, text, ink, plan.Style);
            return size;
        }

        return OnCapsule(drawList, center, text, maxWidth, id, plan, ink);
    }

    public static Vector2 Label(ImDrawListPtr drawList, Vector2 center, string text, float maxWidth, MarqueeId id,
        bool muted = false) =>
        Label(drawList, center, text, maxWidth, id, TextStyles.FootnoteEmphasized, muted);

    public static Vector2 Label(ImDrawListPtr drawList, Vector2 center, string text, float maxWidth, MarqueeId id,
        in TextStyle requested, bool muted)
    {
        var plan = Plan(StageTextRole.Label, requested, UiScale.Current);
        if (muted)
        {
            return OnCapsule(drawList, center, text, maxWidth, id, plan, Muted);
        }

        return Line(drawList, center, text, maxWidth, id, plan.Style, Strong);
    }

    public static void Capsule(ImDrawListPtr drawList, Vector2 min, Vector2 max)
    {
        var radius = (max.Y - min.Y) * 0.5f;
        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(CapsuleFill));
        Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(CapsuleRim), MathF.Max(1f, UiScale.Current));
    }

    private static Vector2 Line(ImDrawListPtr drawList, Vector2 center, string text, float maxWidth, MarqueeId id,
        in TextStyle style, Vector4 ink)
    {
        var size = Typography.Measure(text, style);
        var top = center.Y - size.Y * 0.5f;
        var shadow = MathF.Max(1f, ShadowOffset * UiScale.Current);
        if (Fits(size.X, maxWidth))
        {
            var origin = new Vector2(center.X - size.X * 0.5f, top);
            Typography.Draw(drawList, origin + new Vector2(0f, shadow), text, Shadow, style);
            Typography.Draw(drawList, origin, text, ink, style);
            return size;
        }

        Marquee.DrawCenteredAuto(drawList, id, text, center.X, top, maxWidth, style, ink);
        return new Vector2(maxWidth, size.Y);
    }

    private static Vector2 OnCapsule(ImDrawListPtr drawList, Vector2 center, string text, float maxWidth, MarqueeId id,
        in StageTextPlan plan, Vector4 ink)
    {
        var size = Typography.Measure(text, plan.Style);
        var textWidth = MathF.Min(size.X, MathF.Max(0f, maxWidth - plan.PadX * 2f));
        var height = plan.CapsuleHeight(size.Y);
        var width = textWidth + plan.PadX * 2f;
        var min = new Vector2(center.X - width * 0.5f, center.Y - height * 0.5f);
        var max = new Vector2(center.X + width * 0.5f, center.Y + height * 0.5f);
        Capsule(drawList, min, max);
        var top = center.Y - size.Y * 0.5f;
        if (Fits(size.X, textWidth))
        {
            Typography.Draw(drawList, new Vector2(center.X - size.X * 0.5f, top), text, ink, plan.Style);
        }
        else
        {
            Marquee.DrawCenteredAuto(drawList, id, text, center.X, top, textWidth, plan.Style, ink);
        }

        return max - min;
    }
}
