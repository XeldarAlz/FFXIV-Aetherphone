using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games.Framework;

internal readonly struct GameResult
{
    public readonly string Title;
    public readonly Vector4 TitleColor;
    public readonly string PrimaryLabel;
    public readonly string PrimaryValue;
    public readonly string? SecondaryLine;
    public readonly bool NewBest;
    public readonly string? ButtonLabel;

    public GameResult(string title, Vector4 titleColor, string primaryLabel, string primaryValue, string? secondaryLine,
        bool newBest, string? buttonLabel = null)
    {
        Title = title;
        TitleColor = titleColor;
        PrimaryLabel = primaryLabel;
        PrimaryValue = primaryValue;
        SecondaryLine = secondaryLine;
        NewBest = newBest;
        ButtonLabel = buttonLabel;
    }
}

internal static class GameOverlay
{
    private const float CountUpSeconds = 0.7f;

    private static readonly ParticleSystem Celebration = new(224);
    private static readonly Vector4[] ConfettiPalette =
    {
        new(0.98f, 0.62f, 0.28f, 1f), new(0.42f, 0.78f, 0.98f, 1f), new(0.62f, 0.90f, 0.46f, 1f),
        new(0.95f, 0.45f, 0.62f, 1f), new(0.80f, 0.62f, 0.98f, 1f), new(0.99f, 0.86f, 0.40f, 1f),
    };

    private static float lastProgress;
    private static double lastDrawTime;
    private static bool celebrated;
    private static float countShown;

    public static bool Draw(Rect area, PhoneTheme theme, Vector4 accent, float progress, in GameResult result)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        var now = ImGui.GetTime();
        var clamped = progress < 0f ? 0f :
            progress > 1f ? 1f : progress;
        if (now - lastDrawTime > 0.25 || clamped < lastProgress - 0.01f)
        {
            celebrated = false;
            countShown = 0f;
            Celebration.Clear();
        }

        lastDrawTime = now;
        lastProgress = clamped;
        var alpha = MathF.Min(1f, clamped * 1.5f);
        var grow = Easing.EaseOutBack(clamped);
        Material.Veil(drawList, area.Min, area.Max, 0.58f * alpha);
        var cardWidth = MathF.Min(area.Width * 0.84f, 272f * scale);
        var cardHeight = 218f * scale;
        var cardScale = 0.86f + 0.14f * grow;
        var half = new Vector2(cardWidth, cardHeight) * 0.5f * cardScale;
        var center = area.Center;
        var min = center - half;
        var max = center + half;
        var radius = 26f * scale;
        if (result.NewBest && !celebrated && clamped >= 0.4f)
        {
            celebrated = true;
            Celebration.Confetti(new Vector2(center.X, min.Y + 8f * scale), 110, ConfettiPalette, 300f * scale, 4.2f,
                1.5f);
            Celebration.Sparkle(center, 18, GamePalette.Lighten(accent, 0.4f), 130f * scale, 2.6f, 0.9f);
        }

        Celebration.Update(deltaSeconds);
        Elevation.Floating(drawList, min, max, radius, scale, alpha);
        Material.Frosted(drawList, min, max, radius, scale, alpha);
        Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(accent with { W = 0.20f * alpha }), 1f * scale);
        var top = center.Y - cardHeight * 0.5f * cardScale;
        var titlePhase = Phase(clamped, 0.05f, 0.5f);
        DrawStaggered(new Vector2(center.X, top + 36f * scale), result.Title,
            result.TitleColor with { W = result.TitleColor.W * titlePhase }, TextStyles.Title1, titlePhase, scale);
        if (result.NewBest)
        {
            DrawBestBadge(drawList, new Vector2(center.X, top + 64f * scale), accent, Phase(clamped, 0.2f, 0.65f),
                scale);
        }

        var valuePhase = Phase(clamped, 0.15f, 0.65f);
        DrawStaggered(new Vector2(center.X, center.Y - 2f * scale),
            CountingValue(result.PrimaryValue, valuePhase > 0f ? deltaSeconds : 0f),
            theme.TextStrong with { W = valuePhase }, TextStyles.LargeTitle, valuePhase, scale);
        var labelPhase = Phase(clamped, 0.25f, 0.7f);
        DrawStaggered(new Vector2(center.X, center.Y + 26f * scale), Loc.Culture.TextInfo.ToUpper(result.PrimaryLabel),
            theme.TextMuted with { W = labelPhase }, TextStyles.Caption1, labelPhase, scale);
        if (!string.IsNullOrEmpty(result.SecondaryLine))
        {
            var secondaryPhase = Phase(clamped, 0.3f, 0.75f);
            DrawStaggered(new Vector2(center.X, center.Y + 48f * scale), result.SecondaryLine,
                theme.TextMuted with { W = secondaryPhase }, TextStyles.Footnote, secondaryPhase, scale);
        }

        Celebration.Draw(drawList, scale);
        var buttonPhase = Phase(clamped, 0.45f, 0.95f);
        if (buttonPhase <= 0.2f)
        {
            return false;
        }

        var buttonSize = new Vector2(140f * scale, 40f * scale) * (0.85f + 0.15f * Easing.EaseOutBack(buttonPhase));
        var buttonCenter = new Vector2(center.X, max.Y - 34f * scale + (1f - buttonPhase) * 8f * scale);
        return GameHud.Button(buttonCenter, buttonSize, result.ButtonLabel ?? Loc.T(L.Games.PlayAgain), accent, theme);
    }

    private static float Phase(float progress, float start, float end)
    {
        if (progress <= start)
        {
            return 0f;
        }

        if (progress >= end)
        {
            return 1f;
        }

        return Easing.EaseOutCubic((progress - start) / (end - start));
    }

    private static void DrawStaggered(Vector2 center, string text, Vector4 color, in TextStyle style, float phase,
        float scale)
    {
        if (phase <= 0f)
        {
            return;
        }

        var lift = (1f - phase) * 10f * scale;
        Typography.DrawCentered(center + new Vector2(0f, lift), text, color, style);
    }

    private static void DrawBestBadge(ImDrawListPtr drawList, Vector2 center, Vector4 accent, float phase, float scale)
    {
        if (phase <= 0f)
        {
            return;
        }

        var badge = Loc.T(L.Games.NewBest);
        var badgeSize = Typography.Measure(badge, TextStyles.FootnoteEmphasized);
        var badgeHalf = new Vector2(badgeSize.X * 0.5f + 12f * scale, 12f * scale) * Easing.EaseOutBack(phase);
        var min = center - badgeHalf;
        var max = center + badgeHalf;
        Squircle.Fill(drawList, min, max, badgeHalf.Y, ImGui.GetColorU32(accent with { W = 0.24f * phase }));
        Squircle.Stroke(drawList, min, max, badgeHalf.Y, ImGui.GetColorU32(accent with { W = 0.45f * phase }),
            1f * scale);
        var sweep = Pulse.Phase(2400.0);
        var sweepX = min.X + (max.X - min.X + 24f * scale) * sweep - 12f * scale;
        drawList.PushClipRect(min, max, true);
        drawList.AddQuadFilled(new Vector2(sweepX - 5f * scale, max.Y), new Vector2(sweepX + 1f * scale, min.Y),
            new Vector2(sweepX + 7f * scale, min.Y), new Vector2(sweepX + 1f * scale, max.Y),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.20f * phase)));
        drawList.PopClipRect();
        Typography.DrawCentered(center, badge, accent with { W = phase }, TextStyles.FootnoteEmphasized);
    }

    private static string CountingValue(string primaryValue, float deltaSeconds)
    {
        if (!int.TryParse(primaryValue, NumberStyles.None, CultureInfo.InvariantCulture, out var target) || target <= 0)
        {
            return primaryValue;
        }

        if (deltaSeconds <= 0f && countShown <= 0f)
        {
            return GameNumber.Label(0);
        }

        if (countShown >= target)
        {
            return primaryValue;
        }

        countShown = MathF.Min(target, countShown + target * deltaSeconds / CountUpSeconds);
        var display = (int)countShown;
        return display >= target ? primaryValue : GameNumber.Label(display);
    }
}
