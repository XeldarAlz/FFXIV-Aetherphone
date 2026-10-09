using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Race;

internal static class RaceHud
{
    public const float FadeInSeconds = 0.2f;
    public const float FadeOutSeconds = 0.4f;
    public const int Podium = 3;

    private const float DotShare = 0.42f;
    private const float LineThickness = 2f;
    private const float RingThickness = 2f;
    private const float FlagWidth = 3f;
    private const float EntryPad = 8f;

    private static readonly Vector4 LineInk = new(1f, 1f, 1f, 0.22f);
    private static readonly Vector4 DotShadow = new(0f, 0f, 0f, 0.55f);

    public static float CaptionAlpha(float age, float lineSeconds)
    {
        if (age < 0f || age >= lineSeconds)
        {
            return 0f;
        }

        var fadeIn = Math.Clamp(age / FadeInSeconds, 0f, 1f);
        var fadeOut = Math.Clamp((lineSeconds - age) / FadeOutSeconds, 0f, 1f);
        return MathF.Min(fadeIn, fadeOut);
    }

    public static void DrawProgress(ImDrawListPtr drawList, Rect rect, RaceRoundPlayback playback,
        CasinoRaceRunnerDto[]? runners, ReadOnlySpan<bool> mine, float scale)
    {
        if (rect.Width <= 0f || rect.Height <= 0f)
        {
            return;
        }

        StageText.Capsule(drawList, rect.Min, rect.Max);
        var radius = rect.Height * DotShare;
        var left = rect.Min.X + rect.Height * 0.5f;
        var right = rect.Max.X - rect.Height * 0.5f;
        var centerY = rect.Center.Y;
        drawList.AddLine(new Vector2(left, centerY), new Vector2(right, centerY), ImGui.GetColorU32(LineInk),
            MathF.Max(1f, LineThickness * scale));
        var flag = FlagWidth * scale;
        drawList.AddRectFilled(new Vector2(right - flag * 0.5f, rect.Min.Y + 3f * scale),
            new Vector2(right + flag * 0.5f, rect.Max.Y - 3f * scale), ImGui.GetColorU32(CasinoColors.MoneyHighlight));
        var ranking = playback.Ranking;
        var span = MathF.Max(0f, right - left);
        for (var place = RaceRules.FieldSize - 1; place >= 0; place--)
        {
            var slot = ranking[place];
            var progress = playback.HasOrder ? RaceTrackView.ProgressOf(playback, slot) : 0f;
            var center = new Vector2(left + span * progress, centerY);
            DrawDot(drawList, center, radius, slot, slot < mine.Length && mine[slot], scale);
        }
    }

    public static void DrawTopThree(ImDrawListPtr drawList, Rect rect, RaceRoundPlayback playback,
        ReadOnlySpan<bool> mine, float scale)
    {
        if (rect.Width <= 0f || rect.Height <= 0f || !playback.HasOrder)
        {
            return;
        }

        StageText.Capsule(drawList, rect.Min, rect.Max);
        var pad = EntryPad * scale;
        var inner = rect.Width - rect.Height;
        var entryWidth = inner / Podium;
        var radius = MathF.Min(rect.Height * 0.34f, entryWidth * 0.3f);
        var style = TextStyles.FootnoteEmphasized;
        var ranking = playback.Ranking;
        for (var place = 0; place < Podium; place++)
        {
            var slot = ranking[place];
            var entryLeft = rect.Min.X + rect.Height * 0.5f + place * entryWidth;
            var label = GameNumber.Label(place + 1);
            var labelSize = Typography.Measure(label, style);
            var ink = place == 0 ? CasinoColors.MoneyHighlight : CasinoColors.InkBody;
            Typography.Draw(drawList, new Vector2(entryLeft, rect.Center.Y - labelSize.Y * 0.5f), label, ink, style);
            var dotCenter = new Vector2(MathF.Min(entryLeft + labelSize.X + pad * 0.5f + radius,
                entryLeft + entryWidth - radius), rect.Center.Y);
            DrawDot(drawList, dotCenter, radius, slot, slot < mine.Length && mine[slot], scale);
        }
    }

    public static void DrawCaption(ImDrawListPtr drawList, Rect rect, string text, float alpha, bool live,
        float scale)
    {
        if (rect.Width <= 0f || alpha <= 0f || text.Length == 0)
        {
            return;
        }

        var style = TextStyles.SubheadlineEmphasized;
        var padX = rect.Height * 0.5f;
        var dot = live ? 4f * scale : 0f;
        var dotRoom = live ? dot * 4f : 0f;
        var fitted = Typography.FitText(text, MathF.Max(1f, rect.Width - padX * 2f - dotRoom), style);
        var size = Typography.Measure(fitted, style);
        var width = MathF.Min(rect.Width, size.X + padX * 2f + dotRoom);
        var min = new Vector2(rect.Center.X - width * 0.5f, rect.Min.Y);
        var max = new Vector2(rect.Center.X + width * 0.5f, rect.Max.Y);
        StageText.Capsule(drawList, min, max, alpha);
        var textLeft = min.X + padX + dotRoom;
        if (live)
        {
            var blink = 0.4f + 0.6f * Pulse.Wave(Pulse.Fast);
            drawList.AddCircleFilled(new Vector2(min.X + padX + dot, rect.Center.Y), dot,
                ImGui.GetColorU32(CasinoColors.LightA with { W = blink * alpha }), 12);
        }

        Typography.Draw(drawList, new Vector2(textLeft, rect.Center.Y - size.Y * 0.5f), fitted,
            CasinoColors.InkTitle with { W = alpha }, style);
    }

    private static void DrawDot(ImDrawListPtr drawList, Vector2 center, float radius, int slot, bool backed,
        float scale)
    {
        var cloth = RaceBirdArt.ClothOf(slot);
        drawList.AddCircleFilled(center + new Vector2(0f, MathF.Max(1f, scale)), radius,
            ImGui.GetColorU32(DotShadow), 18);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(cloth), 18);
        if (backed)
        {
            drawList.AddCircle(center, radius + RingThickness * scale * 0.5f, ImGui.GetColorU32(CasinoColors.Money), 18,
                MathF.Max(1f, RingThickness * scale));
        }

        Typography.DrawCentered(drawList, center, GameNumber.Label(slot + 1), RaceBirdArt.InkOn(cloth),
            TextStyles.FootnoteEmphasized);
    }
}
