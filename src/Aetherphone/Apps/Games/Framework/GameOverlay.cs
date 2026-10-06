using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Framework;

internal enum ResultAction : byte
{
    None,
    Primary,
    Secondary,
    Leaderboard,
}

internal readonly struct StageResult
{
    public readonly string Title;
    public readonly Vector4 TitleColor;
    public readonly string PrimaryLabel;
    public readonly string PrimaryValue;
    public readonly bool NewBest;
    public readonly string ContinueLabel;
    public readonly string RankLine;
    public readonly string FriendsLine;
    public readonly bool Uploading;
    public readonly bool TopTen;
    public readonly GameOutcome Outcome;
    public readonly string SecondaryLabel;
    public readonly int Stars;

    public StageResult(string title, Vector4 titleColor, string primaryLabel, string primaryValue, bool newBest,
        string continueLabel, string rankLine, string friendsLine, bool uploading, bool topTen,
        in GameOutcome outcome, string secondaryLabel, int stars)
    {
        SecondaryLabel = secondaryLabel;
        Stars = stars;
        Title = title;
        TitleColor = titleColor;
        PrimaryLabel = primaryLabel;
        PrimaryValue = primaryValue;
        NewBest = newBest;
        ContinueLabel = continueLabel;
        RankLine = rankLine;
        FriendsLine = friendsLine;
        Uploading = uploading;
        TopTen = topTen;
        Outcome = outcome;
    }
}

internal static class GameOverlay
{
    private const float CountUpSeconds = 0.7f;
    private const float CardRadius = 26f;
    private const float MinCardWidth = 260f;
    private const float MaxCardWidth = 300f;
    private const float CardWidthFraction = 0.86f;
    private const float BadgeHeight = 24f;
    private const float ButtonHeight = 42f;
    private const float MinButtonWidth = 150f;
    private const float ButtonSidePadding = 52f;
    private const float MinFitFactor = 0.62f;
    private const float SpinnerRadius = 6f;
    private const float StarSize = 30f;
    private const float EmptyStarAlpha = 0.3f;
    private const float SecondaryTint = 0.18f;
    private const int TopTenRank = 10;

    private static readonly ParticleSystem Celebration = new(224);
    private static readonly Vector4[] ConfettiPalette =
    {
        new(0.98f, 0.62f, 0.28f, 1f), new(0.42f, 0.78f, 0.98f, 1f), new(0.62f, 0.90f, 0.46f, 1f),
        new(0.95f, 0.45f, 0.62f, 1f), new(0.80f, 0.62f, 0.98f, 1f), new(0.99f, 0.86f, 0.40f, 1f),
    };

    private static readonly Vector4[] GoldPalette =
    {
        new(1.00f, 0.84f, 0.30f, 1f), new(0.98f, 0.72f, 0.18f, 1f), new(1.00f, 0.93f, 0.62f, 1f),
        new(0.92f, 0.62f, 0.12f, 1f), new(1.00f, 0.98f, 0.86f, 1f), new(0.96f, 0.80f, 0.36f, 1f),
    };

    private static float lastProgress;
    private static double lastDrawTime;
    private static bool celebrated;
    private static float countShown;

    public static bool IsTopTen(in GameRank rank) => rank.IsRanked && rank.Rank <= TopTenRank;

    public static ResultAction DrawStage(Rect area, PhoneTheme theme, Vector4 accent, float progress,
        in StageResult result)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        var now = ImGui.GetTime();
        var clamped = Math.Clamp(progress, 0f, 1f);
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

        var padding = Metrics.Space.Xl * scale;
        var hasPrimary = result.PrimaryValue.Length > 0;
        var label = hasPrimary ? Loc.Upper(result.PrimaryLabel) : string.Empty;
        var buttonLabel = result.ContinueLabel;
        var leaderboardLabel = Loc.T(L.Stage.Leaderboard);
        var hasRank = result.RankLine.Length > 0;
        var hasFriends = result.FriendsLine.Length > 0;
        var hasSecondary = result.SecondaryLabel.Length > 0;
        var hasStars = result.Stars >= 0;
        var statCount = result.Outcome.StatCount;
        var statRows = (statCount + 1) / 2;
        var buttonWidth = MathF.Max(MinButtonWidth * scale,
            Typography.Measure(buttonLabel, TextStyles.Headline).X + ButtonSidePadding * scale);
        if (hasSecondary)
        {
            buttonWidth = MathF.Max(buttonWidth,
                Typography.Measure(result.SecondaryLabel, TextStyles.Headline).X + ButtonSidePadding * scale);
        }

        var widest = MathF.Max(buttonWidth, Typography.Measure(result.Title, TextStyles.Title1).X);
        if (hasPrimary)
        {
            widest = MathF.Max(widest, Typography.Measure(result.PrimaryValue, TextStyles.LargeTitle).X);
            widest = MathF.Max(widest, Typography.Measure(label, TextStyles.Caption1).X);
        }

        if (hasRank)
        {
            widest = MathF.Max(widest, Typography.Measure(result.RankLine, TextStyles.Footnote).X + SpinnerRadius * 3f * scale);
        }

        var widthLimit = MathF.Max(MinCardWidth * scale,
            MathF.Min(area.Width * CardWidthFraction, MaxCardWidth * scale));
        var cardWidth = Math.Clamp(widest + padding * 2f, MinCardWidth * scale, widthLimit);
        var contentWidth = cardWidth - padding * 2f;
        var titleScale = Typography.FitScale(result.Title, contentWidth, TextStyles.Title1.Scale,
            TextStyles.Title1.Scale * MinFitFactor, TextStyles.Title1.Weight);
        var valueScale = Typography.FitScale(result.PrimaryValue, contentWidth, TextStyles.LargeTitle.Scale,
            TextStyles.LargeTitle.Scale * MinFitFactor, TextStyles.LargeTitle.Weight);
        var titleHeight = Typography.Measure(result.Title, titleScale, TextStyles.Title1.Weight).Y;
        var labelHeight = hasPrimary ? Typography.Measure(label, TextStyles.Caption1).Y : 0f;
        var valueHeight = hasPrimary
            ? Typography.Measure(result.PrimaryValue, valueScale, TextStyles.LargeTitle.Weight).Y
            : 0f;
        var primaryHeight = hasPrimary ? labelHeight + Metrics.Space.Xxs * scale + valueHeight : 0f;
        var lineHeight = Typography.LineHeight(TextStyles.Footnote);
        var statLabelHeight = Typography.LineHeight(TextStyles.Caption1);
        var statValueHeight = Typography.LineHeight(TextStyles.Headline);
        var statCellHeight = statLabelHeight + Metrics.Space.Xxs * scale + statValueHeight;
        var buttonHeight = ButtonHeight * scale;
        var leaderboardHeight = Button.Height(ButtonSize.Small) * scale;
        var cardHeight = padding * 2f + titleHeight + Metrics.Space.Lg * scale + primaryHeight +
            Metrics.Space.Xl * scale + buttonHeight + Metrics.Space.Sm * scale + leaderboardHeight;
        if (result.NewBest)
        {
            cardHeight += BadgeHeight * scale + Metrics.Space.Md * scale;
        }

        if (hasStars)
        {
            cardHeight += StarSize * scale + Metrics.Space.Md * scale;
        }

        if (hasSecondary)
        {
            cardHeight += Metrics.Space.Sm * scale + buttonHeight;
        }

        if (hasRank)
        {
            cardHeight += Metrics.Space.Sm * scale + lineHeight;
        }

        if (hasFriends)
        {
            cardHeight += Metrics.Space.Xxs * scale + lineHeight;
        }

        if (statRows > 0)
        {
            cardHeight += Metrics.Space.Lg * scale + statRows * statCellHeight + (statRows - 1) * Metrics.Space.Sm * scale;
        }

        var cardScale = 0.86f + 0.14f * grow;
        var center = area.Center;
        var half = new Vector2(cardWidth, cardHeight) * 0.5f * cardScale;
        var min = center - half;
        var max = center + half;
        var radius = CardRadius * scale;
        if (result.NewBest && !celebrated && clamped >= 0.4f)
        {
            celebrated = true;
            Celebration.Confetti(new Vector2(center.X, min.Y + 8f * scale), 110,
                result.TopTen ? GoldPalette : ConfettiPalette, 300f * scale, 4.2f, 1.5f);
            Celebration.Sparkle(center, 18, GamePalette.Lighten(accent, 0.4f), 130f * scale, 2.6f, 0.9f);
            GameSfx.NewBest();
        }

        Celebration.Update(deltaSeconds);
        Elevation.Floating(drawList, min, max, radius, scale, alpha);
        Material.Frosted(drawList, min, max, radius, scale, alpha);
        Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(accent with { W = 0.20f * alpha }), 1f * scale);

        var offset = padding - cardHeight * 0.5f;
        var titlePhase = Phase(clamped, 0.05f, 0.5f);
        DrawStaggered(drawList, Place(center, offset + titleHeight * 0.5f, cardScale), result.Title,
            result.TitleColor with { W = result.TitleColor.W * titlePhase }, titleScale, TextStyles.Title1.Weight,
            titlePhase, scale);
        offset += titleHeight + Metrics.Space.Lg * scale;
        if (hasStars)
        {
            var starsPhase = Phase(clamped, 0.15f, 0.85f);
            StarRow.Draw(drawList, Place(center, offset + StarSize * 0.5f * scale, cardScale), StarSize * scale * cardScale,
                result.Stars, theme.TextMuted with { W = EmptyStarAlpha }, alpha, starsPhase);
            offset += StarSize * scale + Metrics.Space.Md * scale;
        }

        if (result.NewBest)
        {
            DrawBestBadge(drawList, Place(center, offset + BadgeHeight * 0.5f * scale, cardScale), accent,
                Phase(clamped, 0.2f, 0.65f), scale);
            offset += BadgeHeight * scale + Metrics.Space.Md * scale;
        }

        if (hasPrimary)
        {
            var labelPhase = Phase(clamped, 0.2f, 0.62f);
            DrawStaggered(drawList, Place(center, offset + labelHeight * 0.5f, cardScale), label,
                theme.TextMuted with { W = labelPhase }, TextStyles.Caption1.Scale, TextStyles.Caption1.Weight,
                labelPhase, scale);
            offset += labelHeight + Metrics.Space.Xxs * scale;
            var valuePhase = Phase(clamped, 0.25f, 0.7f);
            DrawStaggered(drawList, Place(center, offset + valueHeight * 0.5f, cardScale),
                CountingValue(result.PrimaryValue, valuePhase > 0f ? deltaSeconds : 0f),
                theme.TextStrong with { W = valuePhase }, valueScale, TextStyles.LargeTitle.Weight, valuePhase, scale);
            offset += valueHeight;
        }

        if (hasRank)
        {
            offset += Metrics.Space.Sm * scale;
            var rankPhase = Phase(clamped, 0.32f, 0.78f);
            var rankCenter = Place(center, offset + lineHeight * 0.5f, cardScale);
            if (result.Uploading)
            {
                var textWidth = Typography.Measure(result.RankLine, TextStyles.Footnote).X;
                var spinner = SpinnerRadius * scale;
                LoadingPulse.Spinner(new Vector2(rankCenter.X - textWidth * 0.5f - spinner * 1.5f, rankCenter.Y), spinner,
                    accent, rankPhase, drawList);
                rankCenter.X += spinner;
            }

            DrawStaggered(drawList, rankCenter, result.RankLine, theme.TextMuted with { W = rankPhase },
                TextStyles.Footnote.Scale, TextStyles.Footnote.Weight, rankPhase, scale);
            offset += lineHeight;
        }

        if (hasFriends)
        {
            offset += Metrics.Space.Xxs * scale;
            var friendsPhase = Phase(clamped, 0.36f, 0.8f);
            DrawStaggered(drawList, Place(center, offset + lineHeight * 0.5f, cardScale), result.FriendsLine,
                theme.TextMuted with { W = friendsPhase }, TextStyles.Footnote.Scale, TextStyles.Footnote.Weight,
                friendsPhase, scale);
            offset += lineHeight;
        }

        if (statRows > 0)
        {
            offset += Metrics.Space.Lg * scale;
            var cellWidth = contentWidth * 0.5f;
            for (var statIndex = 0; statIndex < statCount; statIndex++)
            {
                var stat = result.Outcome.Stat(statIndex);
                var row = statIndex / 2;
                var column = statIndex % 2;
                var columnCenterX = center.X + (column == 0 ? -cellWidth * 0.5f : cellWidth * 0.5f) * cardScale;
                var cellTop = offset + row * (statCellHeight + Metrics.Space.Sm * scale);
                var statPhase = Phase(clamped, 0.38f + statIndex * 0.04f, 0.82f + statIndex * 0.04f);
                DrawStaggered(drawList, Place(new Vector2(columnCenterX, center.Y), cellTop + statLabelHeight * 0.5f, cardScale),
                    Loc.Upper(Loc.T(stat.Label)), theme.TextMuted with { W = statPhase }, TextStyles.Caption1.Scale,
                    TextStyles.Caption1.Weight, statPhase, scale);
                DrawStaggered(drawList,
                    Place(new Vector2(columnCenterX, center.Y),
                        cellTop + statLabelHeight + Metrics.Space.Xxs * scale + statValueHeight * 0.5f, cardScale),
                    Typography.FitText(stat.Value, cellWidth - Metrics.Space.Sm * scale, TextStyles.Headline),
                    theme.TextStrong with { W = statPhase }, TextStyles.Headline.Scale, TextStyles.Headline.Weight,
                    statPhase, scale);
            }

            offset += statRows * statCellHeight + (statRows - 1) * Metrics.Space.Sm * scale;
        }

        offset += Metrics.Space.Xl * scale;
        Celebration.Draw(drawList, scale);
        var buttonPhase = Phase(clamped, 0.45f, 0.95f);
        if (buttonPhase <= 0.2f)
        {
            return ResultAction.None;
        }

        var pop = 0.85f + 0.15f * Easing.EaseOutBack(buttonPhase);
        var buttonSize = new Vector2(MathF.Min(buttonWidth, contentWidth), buttonHeight) * pop;
        var buttonLift = (1f - buttonPhase) * 8f * scale;
        var action = ResultAction.None;
        if (GameHud.Button(Place(center, offset + buttonHeight * 0.5f, cardScale) + new Vector2(0f, buttonLift),
                buttonSize, buttonLabel, accent, theme))
        {
            action = ResultAction.Primary;
        }

        offset += buttonHeight + Metrics.Space.Sm * scale;
        if (hasSecondary)
        {
            if (GameHud.Button(Place(center, offset + buttonHeight * 0.5f, cardScale) + new Vector2(0f, buttonLift),
                    buttonSize, result.SecondaryLabel, Palette.Mix(theme.SurfaceMuted, accent, SecondaryTint), theme))
            {
                action = ResultAction.Secondary;
            }

            offset += buttonHeight + Metrics.Space.Sm * scale;
        }

        if (buttonPhase > 0.8f &&
            TextButton.Draw(Place(center, offset + leaderboardHeight * 0.5f, cardScale), leaderboardLabel, theme.TextMuted,
                scale))
        {
            action = ResultAction.Leaderboard;
        }

        return action;
    }

    private static Vector2 Place(Vector2 center, float offsetY, float cardScale) =>
        new(center.X, center.Y + offsetY * cardScale);

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

    private static void DrawStaggered(ImDrawListPtr drawList, Vector2 center, string text, Vector4 color,
        float textScale, FontWeight weight, float phase, float scale)
    {
        if (phase <= 0f)
        {
            return;
        }

        var lift = (1f - phase) * 10f * scale;
        Typography.DrawCentered(drawList, center + new Vector2(0f, lift), text, color, textScale, weight);
    }

    private static void DrawBestBadge(ImDrawListPtr drawList, Vector2 center, Vector4 accent, float phase, float scale)
    {
        if (phase <= 0f)
        {
            return;
        }

        var badge = Loc.T(L.Games.NewBest);
        var badgeSize = Typography.Measure(badge, TextStyles.FootnoteEmphasized);
        var badgeHalf = new Vector2(badgeSize.X * 0.5f + 12f * scale, BadgeHeight * 0.5f * scale) *
            Easing.EaseOutBack(phase);
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
        Typography.DrawCentered(drawList, center, badge, accent with { W = phase }, TextStyles.FootnoteEmphasized);
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
