using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal static class CoinHero
{
    private const float HeroHeight = 148f;
    private const float HeroRounding = 26f;
    private const float HeroInset = 20f;
    private const float HeroLuminance = 0.20f;
    private const float HeroTopLift = 0.16f;
    private const float HeroBottomDrop = 0.24f;
    private const float WatermarkFraction = 1.15f;
    private const float WatermarkInsetFraction = 0.24f;
    private const float WatermarkCenterFraction = 0.58f;
    private const float WatermarkAlpha = 0.12f;
    private const string CoinIconId = "coin";
    private const float BalanceMaxScale = 2.45f;
    private const float BalanceMinScale = 1.25f;

    private const float TodayHeight = 134f;
    private const float TodayRounding = 20f;
    private const float RingRadius = 40f;
    private const float RingThickness = 9f;
    private const float CapFillSmoothSeconds = 0.35f;
    private const float TitleRowHeight = 24f;
    private const float GoalRowHeight = 21f;
    private const float ResetRowHeight = 19f;
    private const float LegendDotRadius = 3.5f;

    private static readonly Vector4 Ink = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 CappedTint = new(0.98f, 0.80f, 0.36f, 1f);

    private static RollingValue balanceRoll;
    private static Spring capFill = new(0f);

    public static void Draw(CoinWalletDto wallet, in AppPalette palette)
    {
        var scale = UiScale.Current;
        var delta = ImGui.GetIO().DeltaTime;
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var height = HeroHeight * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var rounding = HeroRounding * scale;
        var surface = Palette.ShadeToLuminance(palette.Accent with { W = 1f }, HeroLuminance);

        Elevation.Card(drawList, min, max, rounding, scale, 0.9f);
        Squircle.FillVerticalGradient(drawList, min, max, rounding,
            ImGui.GetColorU32(Palette.Lighten(surface, HeroTopLift)),
            ImGui.GetColorU32(Palette.Darken(surface, HeroBottomDrop)));

        var watermark = height * WatermarkFraction;
        var watermarkCenter = new Vector2(max.X - height * WatermarkInsetFraction,
            min.Y + height * WatermarkCenterFraction);
        var watermarkTint = Palette.WithAlpha(Ink, WatermarkAlpha);
        drawList.PushClipRect(min, max, true);
        if (!AppIconTile.TryDrawGlyph(drawList, CoinIconId, watermarkCenter, watermark, watermarkTint))
        {
            ProgressRing.CenterIcon(drawList, watermarkCenter, FontAwesomeIcon.Coins, watermarkTint, watermark);
        }

        var inset = HeroInset * scale;
        var textLeft = min.X + inset;
        var available = width - inset * 2f - height * 0.34f;
        balanceRoll.Update((int)wallet.Balance, delta);
        var amountText = NumberText.Group(balanceRoll.Display);
        var tallestHeight = Typography.Measure(amountText, BalanceMaxScale, FontWeight.Bold).Y;
        var restScale = Typography.FitScale(amountText, available - CurrencyGlyph.Reserve(tallestHeight),
            BalanceMaxScale, BalanceMinScale, FontWeight.Bold);
        var restHeight = Typography.Measure(amountText, restScale, FontWeight.Bold).Y;
        var glyphSize = restHeight * CurrencyGlyph.GlyphFraction;
        var reserve = CurrencyGlyph.Reserve(restHeight);
        var poppedScale = restScale * balanceRoll.PopScale;
        var poppedSize = Typography.Measure(amountText, poppedScale, FontWeight.Bold);
        var amountBottom = min.Y + 28f * scale + restHeight;
        CurrencyGlyph.Draw(drawList, CurrencyKind.Coins,
            new Vector2(textLeft + glyphSize * 0.5f, amountBottom - restHeight * 0.5f), glyphSize);
        Typography.Draw(drawList, new Vector2(textLeft + reserve, amountBottom - poppedSize.Y), amountText, Ink,
            poppedScale, FontWeight.Bold);

        Typography.Draw(drawList, new Vector2(textLeft, amountBottom + 2f * scale),
            Typography.FitText(Loc.T(L.Coin.Balance), width - inset * 2f, TextStyles.Headline),
            Palette.WithAlpha(Ink, 0.82f), TextStyles.Headline);

        var statsText = Loc.T(L.Coin.EarnedLifetime) + " " + NumberText.Group(wallet.LifetimeEarned);
        if (wallet.LifetimeSpent > 0)
        {
            statsText += "  ·  " + Loc.T(L.Coin.SpentLifetime) + " "
                + NumberText.Group(wallet.LifetimeSpent);
        }

        Typography.Draw(drawList, new Vector2(textLeft, max.Y - 26f * scale),
            Typography.FitText(statsText, width - inset * 2f, TextStyles.Footnote),
            Palette.WithAlpha(Ink, 0.70f), TextStyles.Footnote);
        drawList.PopClipRect();
        Material.EdgeSquircle(drawList, min, max, rounding, scale);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 4f * scale));
    }

    public static void DrawToday(CoinWalletDto wallet, in AppPalette palette)
    {
        var scale = UiScale.Current;
        var delta = ImGui.GetIO().DeltaTime;
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var height = TodayHeight * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var rounding = TodayRounding * scale;
        var capped = wallet.DailyCap > 0 && wallet.EarnedToday >= wallet.DailyCap;
        var accent = capped ? CappedTint : palette.Accent;

        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(palette.CardFill));
        if (capped)
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.07f)));
            Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.40f)),
                1f * scale);
        }

        Material.EdgeSquircle(drawList, min, max, rounding, scale);

        var inset = 16f * scale;
        var radius = RingRadius * scale;
        var ringCenter = new Vector2(min.X + inset + radius, min.Y + height * 0.5f);
        var thickness = RingThickness * scale;
        ProgressRing.Glow(ringCenter, radius, accent, 0.26f);
        ProgressRing.Track(ringCenter, radius, thickness, Palette.WithAlpha(palette.TitleInk, 0.10f));
        var target = wallet.DailyCap > 0
            ? Math.Clamp((float)((double)wallet.EarnedToday / wallet.DailyCap), 0f, 1f)
            : 0f;
        var shown = Math.Clamp(capFill.Step(target, CapFillSmoothSeconds, delta), 0f, 1f);
        ProgressRing.Fill(ringCenter, radius, thickness, shown, accent);

        var earnedText = NumberText.Group(wallet.EarnedToday);
        var capText = "/ " + NumberText.Group(wallet.DailyCap);
        var earnedSize = Typography.Measure(earnedText, TextStyles.Title2);
        var capSize = Typography.Measure(capText, TextStyles.Caption1);
        var blockTop = ringCenter.Y - (earnedSize.Y + capSize.Y + 2f * scale) * 0.5f;
        Typography.Draw(drawList, new Vector2(ringCenter.X - earnedSize.X * 0.5f, blockTop), earnedText,
            capped ? accent : palette.TitleInk, TextStyles.Title2);
        Typography.Draw(drawList, new Vector2(ringCenter.X - capSize.X * 0.5f, blockTop + earnedSize.Y + 2f * scale),
            capText, palette.MutedInk, TextStyles.Caption1);

        var columnLeft = ringCenter.X + radius + 16f * scale;
        var columnRight = max.X - inset;
        CoinGoals.Count(wallet.Rules, false, out var dailyDone, out var dailyTotal);
        CoinGoals.Count(wallet.Rules, true, out var weeklyDone, out var weeklyTotal);
        var rows = (dailyTotal > 0 ? 1 : 0) + (weeklyTotal > 0 ? 1 : 0);
        var blockHeight = (TitleRowHeight + ResetRowHeight + rows * GoalRowHeight) * scale;
        var cursorY = min.Y + (height - blockHeight) * 0.5f;

        var title = capped ? Loc.T(L.Coin.CapReached) : Loc.T(L.Coin.EarnedToday);
        var titleInk = capped ? accent : palette.TitleInk;
        var titleLeft = columnLeft;
        if (capped)
        {
            var checkSize = 11f * scale;
            ProgressRing.CenterIcon(drawList,
                new Vector2(titleLeft + checkSize * 0.5f, cursorY + TitleRowHeight * scale * 0.42f),
                FontAwesomeIcon.CheckCircle, accent, checkSize);
            titleLeft += checkSize + 6f * scale;
        }

        Typography.Draw(drawList, new Vector2(titleLeft, cursorY),
            Typography.FitText(title, columnRight - titleLeft, TextStyles.SubheadlineEmphasized), titleInk,
            TextStyles.SubheadlineEmphasized);
        cursorY += TitleRowHeight * scale;

        if (dailyTotal > 0)
        {
            GoalRow(drawList, columnLeft, columnRight, cursorY + GoalRowHeight * scale * 0.5f, accent, palette,
                Loc.T(L.Coin.DailyGoals), dailyDone, dailyTotal, scale);
            cursorY += GoalRowHeight * scale;
        }

        if (weeklyTotal > 0)
        {
            GoalRow(drawList, columnLeft, columnRight, cursorY + GoalRowHeight * scale * 0.5f,
                Palette.Lighten(accent, 0.45f), palette, Loc.T(L.Coin.WeeklyGoals), weeklyDone, weeklyTotal, scale);
            cursorY += GoalRowHeight * scale;
        }

        var resetCenterY = cursorY + ResetRowHeight * scale * 0.5f;
        var glyphSize = 10f * scale;
        ProgressRing.CenterIcon(drawList, new Vector2(columnLeft + glyphSize * 0.5f, resetCenterY),
            FontAwesomeIcon.HourglassHalf, palette.MutedInk, glyphSize);
        var resetText = Loc.T(L.Coin.CapResets, TimeText.Until(wallet.ResetsAtUnix));
        var resetLeft = columnLeft + glyphSize + 7f * scale;
        var resetSize = Typography.Measure(resetText, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(resetLeft, resetCenterY - resetSize.Y * 0.5f),
            Typography.FitText(resetText, columnRight - resetLeft, TextStyles.Footnote), palette.MutedInk,
            TextStyles.Footnote);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 4f * scale));
    }

    private static void GoalRow(ImDrawListPtr drawList, float left, float right, float centerY, Vector4 dot,
        in AppPalette palette, string label, int done, int total, float scale)
    {
        var dotRadius = LegendDotRadius * scale;
        drawList.AddCircleFilled(new Vector2(left + dotRadius, centerY), dotRadius, ImGui.GetColorU32(dot), 16);

        var value = Loc.T(L.Coin.GoalsDone, done, total);
        var valueSize = Typography.Measure(value, TextStyles.FootnoteEmphasized);
        var labelLeft = left + dotRadius * 2f + 8f * scale;
        var labelWidth = MathF.Max(24f * scale, right - valueSize.X - 8f * scale - labelLeft);
        var fitted = Typography.FitText(label, labelWidth, TextStyles.Footnote);
        var labelSize = Typography.Measure(fitted, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(labelLeft, centerY - labelSize.Y * 0.5f), fitted, palette.MutedInk,
            TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(right - valueSize.X, centerY - valueSize.Y * 0.5f), value,
            done >= total ? dot : palette.TitleInk, TextStyles.FootnoteEmphasized);
    }
}
