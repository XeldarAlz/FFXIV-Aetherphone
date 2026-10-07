using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Framework;

internal sealed class StageHud
{
    private const float CapsulePadX = 10f;
    private const float IconSize = 11f;
    private const float IconGap = 5f;
    private const float BarHeight = 3f;
    private const float BarInset = 6f;
    private const float HeartGap = 2f;
    private const int EdgeGlowMultiplier = 3;
    private const float CompactSizeScale = StageLayout.CompactPillHeight / GameHud.PillHeight;
    private static readonly Vector4 Warm = new(1f, 0.72f, 0.30f, 1f);
    private static readonly Vector4 WhiteHot = new(1f, 0.96f, 0.90f, 1f);
    private static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    private static readonly TextStyle CapsuleStyle = TextStyles.FootnoteEmphasized;

    private RollingValue scoreRoll;
    private LabelSlot levelLabel;
    private LabelSlot comboLabel;
    private LabelSlot livesLabel;

    public void Reset()
    {
        scoreRoll.Snap(0);
        levelLabel.Reset();
        comboLabel.Reset();
        livesLabel.Reset();
    }

    public void Draw(ImDrawListPtr drawList, HudModel model, Rect full, HudStyle style, ScoreKind kind,
        Vector4 accent, PhoneTheme theme, float deltaSeconds, bool beatingBest, ScreenFx fx)
    {
        var scale = UiScale.Current;
        if (model.HasScore)
        {
            DrawScore(model, full, style, accent, theme, deltaSeconds, beatingBest, scale);
        }

        if (model.HasCombo && model.ComboValue.Multiplier >= EdgeGlowMultiplier)
        {
            fx.EdgeGlow(model.ComboValue.Heat);
        }

        var visible = model.Visible(style);
        if (visible.Length == 0)
        {
            return;
        }

        var height = StageLayout.SecondaryHeight * scale;
        if (style == HudStyle.Compact)
        {
            var center = StageLayout.CompactSecondaryCenter(full, scale);
            var width = CapsuleWidth(visible[0], model, kind, scale);
            var rect = new Rect(center - new Vector2(width * 0.5f, height * 0.5f),
                center + new Vector2(width * 0.5f, height * 0.5f));
            DrawCapsule(drawList, visible[0], model, kind, rect, accent, theme, scale);
            return;
        }

        var gap = StageLayout.SecondaryGap * scale;
        var total = -gap;
        for (var index = 0; index < visible.Length; index++)
        {
            total += CapsuleWidth(visible[index], model, kind, scale) + gap;
        }

        var rowY = StageLayout.SecondaryRowY(full, scale);
        var x = full.Center.X - total * 0.5f;
        for (var index = 0; index < visible.Length; index++)
        {
            var width = CapsuleWidth(visible[index], model, kind, scale);
            var rect = new Rect(new Vector2(x, rowY - height * 0.5f), new Vector2(x + width, rowY + height * 0.5f));
            DrawCapsule(drawList, visible[index], model, kind, rect, accent, theme, scale);
            x += width + gap;
        }
    }

    public static void Capsule(ImDrawListPtr drawList, Rect rect, float scale, float opacity = 0.9f)
    {
        Material.Frosted(drawList, rect.Min, rect.Max, rect.Height * 0.5f, scale, opacity);
    }

    public static void Bar(ImDrawListPtr drawList, Rect rect, float fraction, Vector4 color, float scale)
    {
        var inset = BarInset * scale;
        var height = BarHeight * scale;
        var bottom = rect.Max.Y - height * 0.8f;
        var left = rect.Min.X + inset;
        var right = rect.Max.X - inset;
        drawList.AddRectFilled(new Vector2(left, bottom - height), new Vector2(right, bottom),
            ImGui.GetColorU32(color with { W = 0.18f }), height * 0.5f);
        if (fraction <= 0f)
        {
            return;
        }

        drawList.AddRectFilled(new Vector2(left, bottom - height), new Vector2(left + (right - left) * fraction, bottom),
            ImGui.GetColorU32(color with { W = 0.9f }), height * 0.5f);
    }

    public static string ValueLabel(int value, ScoreKind kind) =>
        kind == ScoreKind.Time ? TimeText.MinutesSeconds(value) : GameNumber.Label(value);

    private void DrawScore(HudModel model, Rect full, HudStyle style, Vector4 accent, PhoneTheme theme,
        float deltaSeconds, bool beatingBest, float scale)
    {
        var label = Loc.T(model.ScoreLabel ?? L.Games.Score);
        var sizeScale = style == HudStyle.Compact ? CompactSizeScale : 1f;
        var width = GameHud.PillWidth(label, GameNumber.Label(model.ScoreValue), sizeScale);
        var maxWidth = StageLayout.PrimaryMaxWidth(full, scale);
        if (width > maxWidth && maxWidth > 0f)
        {
            sizeScale *= maxWidth / width;
        }

        var center = style == HudStyle.Compact
            ? StageLayout.CompactScoreCenter(full, scale)
            : StageLayout.PrimaryCenter(full, scale);
        GameHud.ScorePill(center, label, ref scoreRoll, model.ScoreValue, accent, theme, deltaSeconds, beatingBest,
            sizeScale);
    }

    private float CapsuleWidth(HudSlot slot, HudModel model, ScoreKind kind, float scale)
    {
        var pad = CapsulePadX * scale * 2f;
        var icon = (IconSize + IconGap) * scale;
        switch (slot)
        {
            case HudSlot.Timer:
                return pad + icon + Typography.Measure(TimerLabel(model), CapsuleStyle).X;
            case HudSlot.Lives:
                if (model.LivesMax > HudModel.MaxHearts)
                {
                    return pad + icon + Typography.Measure(livesLabel.Get(L.Stage.Times, model.LivesLeft), CapsuleStyle).X;
                }

                return pad + model.LivesMax * IconSize * scale + (model.LivesMax - 1) * HeartGap * scale;
            case HudSlot.Level:
                return pad + Typography.Measure(levelLabel.Get(L.Stage.LevelShort, model.LevelValue), CapsuleStyle).X;
            case HudSlot.Combo:
                return pad + Typography.Measure(comboLabel.Get(L.Stage.Times, model.ComboValue.Multiplier), CapsuleStyle).X +
                       icon;
            case HudSlot.Best:
                return pad + icon + Typography.Measure(ValueLabel(model.BestValue, kind), CapsuleStyle).X;
            case HudSlot.Custom:
            case HudSlot.SecondCustom:
                return model.CustomWidth(HudModel.CustomIndex(slot)) * scale;
            default:
                return 0f;
        }
    }

    private void DrawCapsule(ImDrawListPtr drawList, HudSlot slot, HudModel model, ScoreKind kind, Rect rect,
        Vector4 accent, PhoneTheme theme, float scale)
    {
        model.PlaceSlot(slot, rect);
        if (slot is HudSlot.Custom or HudSlot.SecondCustom)
        {
            return;
        }

        Capsule(drawList, rect, scale);
        var pad = CapsulePadX * scale;
        var iconSize = IconSize * scale;
        var left = rect.Min.X + pad;
        var centerY = rect.Center.Y;
        switch (slot)
        {
            case HudSlot.Timer:
            {
                var urgent = model.TimerUrgent;
                var pulse = urgent ? 0.5f + 0.5f * Pulse.Wave(Pulse.Fast) : 0f;
                var ink = urgent ? Vector4.Lerp(StageInks.Strong, Danger, pulse) : StageInks.Strong;
                ProgressRing.CenterIcon(drawList, new Vector2(left + iconSize * 0.5f, centerY), FontAwesomeIcon.Clock,
                    urgent ? Danger : accent, iconSize);
                Typography.Draw(drawList, TextOrigin(left + iconSize + IconGap * scale, centerY), TimerLabel(model), ink,
                    CapsuleStyle);
                if (model.TimerTotal > 0f)
                {
                    Bar(drawList, rect, Math.Clamp(model.TimerLeft / model.TimerTotal, 0f, 1f), urgent ? Danger : accent,
                        scale);
                }

                return;
            }
            case HudSlot.Lives:
            {
                if (model.LivesMax > HudModel.MaxHearts)
                {
                    ProgressRing.CenterIcon(drawList, new Vector2(left + iconSize * 0.5f, centerY), FontAwesomeIcon.Heart,
                        accent, iconSize);
                    Typography.Draw(drawList, TextOrigin(left + iconSize + IconGap * scale, centerY),
                        livesLabel.Get(L.Stage.Times, model.LivesLeft), StageInks.Strong, CapsuleStyle);
                    return;
                }

                var dim = StageInks.Muted with { W = 0.35f };
                for (var heart = 0; heart < model.LivesMax; heart++)
                {
                    var x = left + heart * (IconSize + HeartGap) * scale + iconSize * 0.5f;
                    ProgressRing.CenterIcon(drawList, new Vector2(x, centerY), FontAwesomeIcon.Heart,
                        heart < model.LivesLeft ? accent : dim, iconSize);
                }

                return;
            }
            case HudSlot.Level:
                Typography.Draw(drawList, TextOrigin(left, centerY), levelLabel.Get(L.Stage.LevelShort, model.LevelValue),
                    StageInks.Strong, CapsuleStyle);
                return;
            case HudSlot.Combo:
            {
                var meter = model.ComboValue;
                var heat = meter.Heat;
                var color = heat < 0.5f
                    ? Vector4.Lerp(accent, Warm, heat * 2f)
                    : Vector4.Lerp(Warm, WhiteHot, (heat - 0.5f) * 2f);
                ProgressRing.CenterIcon(drawList, new Vector2(left + iconSize * 0.5f, centerY), FontAwesomeIcon.Fire,
                    color, iconSize);
                Typography.Draw(drawList, TextOrigin(left + iconSize + IconGap * scale, centerY),
                    comboLabel.Get(L.Stage.Times, meter.Multiplier), color, CapsuleStyle);
                if (meter.Timed)
                {
                    Bar(drawList, rect, meter.WindowFraction, color, scale);
                }

                return;
            }
            case HudSlot.Best:
                ProgressRing.CenterIcon(drawList, new Vector2(left + iconSize * 0.5f, centerY), FontAwesomeIcon.Trophy,
                    accent, iconSize);
                Typography.Draw(drawList, TextOrigin(left + iconSize + IconGap * scale, centerY),
                    ValueLabel(model.BestValue, kind), StageInks.Strong, CapsuleStyle);
                return;
            default:
                return;
        }
    }

    private static Vector2 TextOrigin(float left, float centerY) =>
        new(left, centerY - Typography.LineHeight(CapsuleStyle) * 0.5f);

    private static string TimerLabel(HudModel model) =>
        TimeText.MinutesSeconds(model.TimerElapsed ? (int)model.TimerLeft : (int)MathF.Ceiling(model.TimerLeft));
}
