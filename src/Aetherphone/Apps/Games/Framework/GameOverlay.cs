using System.Numerics;
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

    public GameResult(string title, Vector4 titleColor, string primaryLabel, string primaryValue, string? secondaryLine, bool newBest, string? buttonLabel = null)
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
    public static bool Draw(Rect area, PhoneTheme theme, Vector4 accent, float progress, in GameResult result)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();

        var clamped = progress < 0f ? 0f : progress > 1f ? 1f : progress;
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

        Elevation.Floating(drawList, min, max, radius, scale, alpha);
        Material.Frosted(drawList, min, max, radius, scale, alpha);

        var textAlpha = alpha;
        var top = center.Y - cardHeight * 0.5f * cardScale;

        Typography.DrawCentered(new Vector2(center.X, top + 36f * scale), result.Title, result.TitleColor with { W = result.TitleColor.W * textAlpha }, TextStyles.Title1);

        if (result.NewBest)
        {
            var badge = Loc.T(L.Games.NewBest);
            var badgeSize = Typography.Measure(badge, TextStyles.FootnoteEmphasized);
            var badgeCenter = new Vector2(center.X, top + 64f * scale);
            var badgeHalf = new Vector2(badgeSize.X * 0.5f + 12f * scale, 12f * scale);
            Squircle.Fill(drawList, badgeCenter - badgeHalf, badgeCenter + badgeHalf, badgeHalf.Y, ImGui.GetColorU32(accent with { W = 0.22f * textAlpha }));
            Typography.DrawCentered(badgeCenter, badge, accent with { W = textAlpha }, TextStyles.FootnoteEmphasized);
        }

        Typography.DrawCentered(new Vector2(center.X, center.Y - 2f * scale), result.PrimaryValue, theme.TextStrong with { W = textAlpha }, TextStyles.LargeTitle);
        Typography.DrawCentered(new Vector2(center.X, center.Y + 26f * scale), result.PrimaryLabel.ToUpperInvariant(), theme.TextMuted with { W = textAlpha }, TextStyles.Caption1);

        if (!string.IsNullOrEmpty(result.SecondaryLine))
        {
            Typography.DrawCentered(new Vector2(center.X, center.Y + 48f * scale), result.SecondaryLine, theme.TextMuted with { W = textAlpha }, TextStyles.Footnote);
        }

        var buttonSize = new Vector2(140f * scale, 40f * scale);
        var buttonCenter = new Vector2(center.X, max.Y - 34f * scale);
        if (clamped < 0.85f)
        {
            return false;
        }

        return GameHud.Button(buttonCenter, buttonSize, result.ButtonLabel ?? Loc.T(L.Games.PlayAgain), accent, theme);
    }
}
