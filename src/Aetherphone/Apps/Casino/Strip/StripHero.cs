using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Strip;

internal enum StripHeroAction : byte
{
    None,
    GetChips,
    Cashier,
    ClaimTimed,
    ClaimStreak,
    FreeSpin,
}

[Flags]
internal enum StripHeroClaims : byte
{
    None = 0,
    Timed = 1,
    Streak = 2,
    Spin = 4,
}

internal readonly record struct StripHeroModel(
    long Balance,
    bool Seated,
    int Level,
    float LevelProgress,
    long LevelCap,
    bool ShowsLevel,
    CasinoBonusDto? Timed,
    CasinoBonusDto? Streak,
    bool SpinReady,
    bool Claiming,
    long NowUnix,
    long Rate = CasinoChipLots.ChipPerCoin);

internal sealed class StripHero
{
    public const float Pad = 14f;
    public const float RowGap = 8f;
    public const float BonusRing = 30f;
    public const float RingThickness = 3.5f;
    public const float StreakDot = 23f;
    public const float StreakMinGap = 2f;
    public const float StreakColumnGap = 10f;
    public const float StreakColumnShare = 0.4f;
    public const float StreakSeparator = 11f;
    public const float TodayScale = 1.12f;
    public const float TodayRing = 2f;
    public const float SpinGlyph = 18f;
    public const int StreakDays = CasinoFaucets.StreakDays;
    public const long TimedPeriodSeconds = 3 * 3600;
    public const float LabelSlack = 4f;
    public const float CoinsGap = 2f;

    private const float SurfaceLuminance = 0.07f;
    private const float GlassOpacity = 0.94f;
    private const float SubAlpha = 0.86f;
    private const float IdleAlpha = 0.6f;
    private const float SeparatorAlpha = 0.12f;
    private const float DoneAlpha = 0.5f;
    private const float FutureRingAlpha = 0.28f;
    private const float FutureInkAlpha = 0.78f;
    private const float TodayHaloAlpha = 0.45f;
    private const float TodayHaloGrowth = 0.7f;
    private const float FitSlack = 0.5f;
    private const string GetChipsId = "casino.hero.get";
    private const string SpinId = "casino.hero.spin";
    private const string TimedId = "casino.hero.timed";

    private static readonly TextStyle AmountStyle = TextStyles.LargeTitle;
    private static readonly Vector4 SpinInk = new(0.651f, 0.961f, 1f, 1f);
    private static readonly Vector4 SpinHole = new(0.055f, 0.227f, 0.267f, 1f);

    private readonly CasinoTextCache texts = new();
    private readonly string[] dayLabels = new string[StreakDays];
    private readonly ChipValueText worth = new();
    private RollingAmount balance;
    private LevelCapsule level;
    private Vector2 timedCenter;
    private Vector2 streakCenter;
    private Rect spinSource;

    public Vector2 TimedCenter => timedCenter;

    public Vector2 StreakCenter => streakCenter;

    public Rect SpinSource => spinSource;

    public void Snap(long value) => balance.Snap(value);

    public static StripHeroClaims ClaimsOf(in StripHeroModel model)
    {
        var claims = StripHeroClaims.None;
        if (model.Timed is not null)
        {
            claims |= StripHeroClaims.Timed;
        }

        if (model.Streak is not null)
        {
            claims |= StripHeroClaims.Streak;
        }

        if (model.SpinReady)
        {
            claims |= StripHeroClaims.Spin;
        }

        return claims;
    }

    public static bool GetChipsInline(float rowWidth, float amountWidth, float getWidth, float gap) =>
        amountWidth + gap + getWidth <= rowWidth + FitSlack;

    public static float BalanceRowHeight(float amountLine, float getHeight) => MathF.Max(amountLine, getHeight);

    public static float BalanceBlockHeight(float amountLine, float footnoteLine, float coinsGap) =>
        amountLine + coinsGap + footnoteLine;

    public static float ClaimsHeight(StripHeroClaims claims, float buttonHeight) =>
        (claims & (StripHeroClaims.Timed | StripHeroClaims.Spin)) != 0 ? buttonHeight : 0f;

    public static float SharedClaimSplit(float rowWidth, float timedWidth, float spinWidth, float gap)
    {
        var half = (rowWidth - gap) * 0.5f;
        return MathF.Max(timedWidth, spinWidth) <= half ? half : MathF.Min(timedWidth, rowWidth - gap - spinWidth);
    }

    public static float StreakHeight(float labelBlock, float dot, float separator) =>
        separator + MathF.Max(labelBlock, dot * TodayScale);

    public static float StreakColumnWidth(float labelWidth, float dayWidth, float rowWidth) =>
        MathF.Min(MathF.Max(labelWidth, dayWidth), rowWidth * StreakColumnShare);

    public static float StreakDotSize(float region, float dot, float minGap, int days) =>
        days <= 1 ? MathF.Min(dot, region) : MathF.Max(0f, MathF.Min(dot, (region - minGap * (days - 1)) / days));

    public static float StreakPitch(float region, float dot, int days) =>
        days <= 1 ? 0f : (region - dot) / (days - 1);

    public float Height(in StripHeroModel model, float width, float scale)
    {
        var pad = Pad * scale;
        var gap = RowGap * scale;
        var getHeight = Button.RegularHeight * scale;
        var inline = ChipsInline(model.Balance, width - pad * 2f, scale);
        var block = BalanceBlockHeight(Typography.LineHeight(AmountStyle), Typography.LineHeight(TextStyles.Footnote),
            CoinsGap * scale);
        var height = pad * 2f + BalanceRowHeight(block, inline ? getHeight : 0f);
        if (!inline)
        {
            height += gap + getHeight;
        }

        if (model.ShowsLevel)
        {
            height += gap + LevelCardHeight(model.Level, scale);
        }

        var claims = ClaimsHeight(ClaimsOf(model), Button.LargeHeight * scale);
        if (claims > 0f)
        {
            height += gap + claims;
        }

        if (model.Streak is not null)
        {
            height += gap + StreakHeight(StreakLabelBlock(), StreakDot * scale, StreakSeparator * scale);
        }

        return height;
    }

    private static float LevelCardHeight(int level, float scale) =>
        MathF.Max(LevelCapsule.Ring * scale, LevelCapsule.TextBlock(StatusTitle.ForLevel(level), scale));

    private static float StreakLabelBlock() =>
        Typography.LineHeight(TextStyles.FootnoteEmphasized) + Typography.LineHeight(TextStyles.Footnote);

    private static bool ChipsInline(long balance, float rowWidth, float scale) =>
        GetChipsInline(rowWidth, MinAmountWidth(balance), GetChipsWidth(scale), Metrics.Space.Sm * scale);

    private static float MinAmountWidth(long balance) =>
        CurrencyGlyph.Reserve(Typography.LineHeight(AmountStyle))
        + Typography.Measure(NumberText.Compact(balance), TextStyles.Title2.Scale, AmountStyle.Weight).X;

    private static float GetChipsWidth(float scale) =>
        MathF.Max(CasinoStageLayout.TouchTarget * scale,
            Button.WidthFor(Loc.T(L.Strip.GetChips), ButtonSize.Regular) + LabelSlack * scale);

    private static float SpinWidth(float scale) =>
        MathF.Max(CasinoStageLayout.TouchTarget * scale,
            Button.LargeHeight * scale * 0.5f + SpinGlyph * 0.5f * scale + Metrics.Space.Xs * scale
            + Typography.Measure(Loc.T(L.Floor.FreeSpin), TextStyles.SubheadlineEmphasized).X
            + Metrics.Space.Sm * scale + LabelSlack * scale);

    private float TimedWidth(in StripHeroModel model, float scale)
    {
        if (model.Timed is not { } timed)
        {
            return 0f;
        }

        var style = TextStyles.SubheadlineEmphasized;
        var claim = Typography.Measure(texts.Compact(L.Strip.BonusClaim, timed.Amount), style).X;
        var wait = Typography.Measure(texts.Duration(L.Strip.BonusReadyIn, (int)TimedPeriodSeconds), style).X;
        var height = Button.LargeHeight * scale;
        return height * 0.5f + BonusRing * 0.5f * scale + Metrics.Space.Xs * scale + MathF.Max(claim, wait)
               + Metrics.Space.Sm * scale;
    }

    public StripHeroAction Draw(ImDrawListPtr drawList, AppSkin ui, in StripHeroModel model, Vector2 origin,
        float width, float deltaSeconds, float scale, out float bottom)
    {
        var height = Height(model, width, scale);
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        bottom = max.Y;
        UiAnchors.Report("casino.chipbar", new Rect(min, max));
        var surface = Palette.ShadeToLuminance(ui.Accent with { W = 1f }, SurfaceLuminance);
        Material.AccentGlass(drawList, min, max, Metrics.Radius.Widget * scale, scale, surface, GlassOpacity);

        var pad = Pad * scale;
        var gap = RowGap * scale;
        var left = min.X + pad;
        var right = max.X - pad;
        var action = StripHeroAction.None;
        var top = min.Y + pad;
        var buttonGap = Metrics.Space.Sm * scale;
        var getWidth = GetChipsWidth(scale);
        var getHeight = Button.RegularHeight * scale;
        var inline = ChipsInline(model.Balance, right - left, scale);
        var block = BalanceBlockHeight(Typography.LineHeight(AmountStyle), Typography.LineHeight(TextStyles.Footnote),
            CoinsGap * scale);
        var balanceRow = BalanceRowHeight(block, inline ? getHeight : 0f);
        if (DrawBalance(drawList, model, left, inline ? right - getWidth - buttonGap : right, top, balanceRow, block,
                deltaSeconds, scale))
        {
            action = StripHeroAction.Cashier;
        }

        if (inline)
        {
            var center = top + balanceRow * 0.5f;
            var getRect = new Rect(new Vector2(right - getWidth, center - getHeight * 0.5f),
                new Vector2(right, center + getHeight * 0.5f));
            if (DrawWhitePill(drawList, ui, getRect, Loc.T(L.Strip.GetChips), surface))
            {
                action = StripHeroAction.GetChips;
            }
        }

        top += balanceRow;
        if (!inline)
        {
            top += gap;
            var getRect = new Rect(new Vector2(left, top), new Vector2(right, top + getHeight));
            if (DrawWhitePill(drawList, ui, getRect, Loc.T(L.Strip.GetChips), surface))
            {
                action = StripHeroAction.GetChips;
            }

            top = getRect.Max.Y;
        }

        if (model.ShowsLevel)
        {
            top += gap;
            var capsule = new Rect(new Vector2(left, top), new Vector2(right, top + LevelCardHeight(model.Level, scale)));
            level.Draw(drawList, capsule, Math.Max(1, model.Level), model.LevelProgress, model.LevelCap,
                StatusTitle.ForLevel(model.Level), CasinoColors.Money, scale);
            top = capsule.Max.Y;
        }

        var claims = ClaimsOf(model);
        var claimsHeight = ClaimsHeight(claims, Button.LargeHeight * scale);
        if (claimsHeight > 0f)
        {
            top += gap;
            var claimAction = DrawClaims(drawList, ui, model, claims,
                new Rect(new Vector2(left, top), new Vector2(right, top + claimsHeight)), buttonGap, scale);
            if (claimAction != StripHeroAction.None)
            {
                action = claimAction;
            }

            top += claimsHeight;
        }

        if (model.Streak is { } streak)
        {
            top += gap;
            if (DrawStreak(drawList, streak, model.Claiming, new Vector2(left, top), right - left, scale))
            {
                action = StripHeroAction.ClaimStreak;
            }
        }

        return action;
    }

    private bool DrawBalance(ImDrawListPtr drawList, in StripHeroModel model, float left, float right, float top,
        float rowHeight, float block, float deltaSeconds, float scale)
    {
        balance.Update(model.Balance, deltaSeconds);
        var amount = NumberText.Compact(balance.Display);
        var amountLine = Typography.LineHeight(AmountStyle);
        var reserve = CurrencyGlyph.Reserve(amountLine);
        var amountScale = Typography.FitScale(amount, MathF.Max(1f, right - left - reserve),
            AmountStyle.Scale * balance.PopScale, TextStyles.Title2.Scale, AmountStyle.Weight);
        var size = Typography.Measure(amount, amountScale, AmountStyle.Weight);
        var blockTop = top + (rowHeight - block) * 0.5f;
        var amountCenter = blockTop + amountLine * 0.5f;
        var glyph = size.Y * CurrencyGlyph.GlyphFraction;
        CurrencyGlyph.Draw(drawList, CurrencyKind.Chips, new Vector2(left + glyph * 0.5f, amountCenter), glyph);
        var textLeft = left + CurrencyGlyph.Reserve(size.Y);
        Typography.Draw(drawList, new Vector2(textLeft, amountCenter - size.Y * 0.5f), amount,
            model.Balance > 0 ? CasinoColors.Money : Palette.WithAlpha(CasinoArt.White, IdleAlpha), amountScale,
            AmountStyle.Weight);
        var coinsTop = blockTop + amountLine + CoinsGap * scale;
        var coinsWidth = MathF.Max(1f, right - textLeft);
        var coins = worth.Full(model.Balance, model.Rate);
        var coinsText = Typography.Measure(coins, TextStyles.Footnote).X <= coinsWidth
            ? coins
            : worth.Compact(model.Balance, model.Rate);
        Typography.Draw(drawList, new Vector2(textLeft, coinsTop),
            Typography.FitText(coinsText, coinsWidth, TextStyles.Footnote), Palette.WithAlpha(CasinoArt.White, SubAlpha),
            TextStyles.Footnote);
        var amountRect = new Rect(new Vector2(left, blockTop),
            new Vector2(MathF.Min(right, textLeft + MathF.Max(size.X, Typography.Measure(coinsText, TextStyles.Footnote).X)),
                blockTop + block));
        var overAmount = UiInteract.Hover(amountRect.Min, amountRect.Max);
        if (overAmount)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            HoverTooltip.Show(amountRect, NumberText.Group(model.Balance), HoverLabelSide.Below);
        }

        return UiInteract.Click(amountRect.Min, amountRect.Max, overAmount);
    }

    private StripHeroAction DrawClaims(ImDrawListPtr drawList, AppSkin ui, in StripHeroModel model,
        StripHeroClaims claims, Rect row, float gap, float scale)
    {
        var hasTimed = (claims & StripHeroClaims.Timed) != 0;
        var hasSpin = (claims & StripHeroClaims.Spin) != 0;
        var timedRect = row;
        var spinRect = row;
        if (hasTimed && hasSpin)
        {
            var split = row.Min.X + SharedClaimSplit(row.Width, TimedWidth(model, scale), SpinWidth(scale), gap);
            timedRect = new Rect(row.Min, new Vector2(split, row.Max.Y));
            spinRect = new Rect(new Vector2(split + gap, row.Min.Y), row.Max);
        }

        var action = StripHeroAction.None;
        if (hasTimed)
        {
            UiAnchors.Report("casino.bonus", timedRect);
            if (DrawTimed(drawList, ui, timedRect, model.Timed!, model.Claiming, model.NowUnix, scale))
            {
                action = StripHeroAction.ClaimTimed;
            }
        }

        if (hasSpin && DrawSpin(drawList, ui, spinRect, scale))
        {
            action = StripHeroAction.FreeSpin;
        }

        return action;
    }

    private static bool DrawWhitePill(ImDrawListPtr drawList, AppSkin ui, Rect rect, string label, Vector4 surface)
    {
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var face = Button.Surface(drawList, rect, ui.Ink.WithAccent(CasinoArt.White), ButtonStyle.Prominent,
            ButtonRole.Normal, true, hovered, ImGui.GetID(GetChipsId));
        Button.DrawLabel(drawList, face with { LabelInk = surface with { W = face.LabelInk.W } }, label, GetChipsId);
        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private bool DrawSpin(ImDrawListPtr drawList, AppSkin ui, Rect rect, float scale)
    {
        spinSource = rect;
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var face = Button.Surface(drawList, rect, ui.Ink.WithAccent(CasinoColors.LightB), ButtonStyle.Tinted,
            ButtonRole.Normal, true, hovered, ImGui.GetID(SpinId));
        var shown = face.Face;
        var ink = SpinInk with { W = face.LabelInk.W };
        var style = TextStyles.SubheadlineEmphasized;
        var label = Loc.T(L.Floor.FreeSpin);
        var glyphRadius = SpinGlyph * 0.5f * scale;
        var content = glyphRadius * 2f + Metrics.Space.Xs * scale + Typography.Measure(label, style).X;
        var glyphCenter = new Vector2(
            MathF.Max(shown.Min.X + shown.Height * 0.5f, shown.Center.X - content * 0.5f + glyphRadius),
            shown.Center.Y);
        var breath = Pulse.Wave(Pulse.Breath);
        CasinoGlyphs.Draw(drawList, CasinoGames.DailySpin, glyphCenter, glyphRadius * (0.92f + 0.08f * breath),
            ImGui.GetColorU32(ink), ImGui.GetColorU32(SpinHole));
        var textLeft = glyphCenter.X + glyphRadius + Metrics.Space.Xs * scale;
        Marquee.DrawLeftAuto(drawList, SpinId, label, textLeft,
            shown.Center.Y - Typography.LineHeight(style) * 0.5f,
            MathF.Max(1f, shown.Max.X - Metrics.Space.Sm * scale - textLeft), style, ink);
        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private bool DrawTimed(ImDrawListPtr drawList, AppSkin ui, Rect rect, CasinoBonusDto timed, bool claiming,
        long nowUnix, float scale)
    {
        timedCenter = rect.Center;
        var ready = timed.Ready && timed.Eligible;
        var remaining = CashierBonusShelf.SecondsUntil(timed, nowUnix);
        var label = ready
            ? texts.Compact(L.Strip.BonusClaim, timed.Amount)
            : texts.Duration(L.Strip.BonusReadyIn, (int)Math.Min(remaining, int.MaxValue));
        var hovered = ready && !claiming && UiInteract.Hover(rect.Min, rect.Max);
        var face = Button.Surface(drawList, rect, ui.Ink.WithAccent(CasinoColors.Money),
            ready ? ButtonStyle.Tinted : ButtonStyle.Gray, ButtonRole.Normal, ready && !claiming, hovered,
            ImGui.GetID(TimedId));
        var ringRadius = BonusRing * 0.5f * scale;
        var ringCenter = new Vector2(rect.Min.X + rect.Height * 0.5f, rect.Center.Y);
        var thickness = RingThickness * scale;
        ProgressRing.Track(drawList, ringCenter, ringRadius - thickness, thickness,
            Palette.WithAlpha(CasinoColors.InkMuted, 0.4f));
        var fraction = ready ? 1f : 1f - Math.Clamp((float)remaining / TimedPeriodSeconds, 0f, 1f);
        if (fraction > 0f)
        {
            ProgressRing.Fill(drawList, ringCenter, ringRadius - thickness, thickness, fraction, CasinoColors.Money);
        }

        if (ready)
        {
            var breath = Pulse.Wave(Pulse.Breath);
            drawList.AddCircleFilled(ringCenter, (ringRadius - thickness * 2f) * (0.55f + 0.2f * breath),
                ImGui.GetColorU32(CasinoColors.Money), 20);
        }

        var textLeft = ringCenter.X + ringRadius + Metrics.Space.Xs * scale;
        var style = TextStyles.SubheadlineEmphasized;
        Marquee.DrawLeftAuto(drawList, TimedId, label, textLeft, rect.Center.Y - Typography.LineHeight(style) * 0.5f,
            MathF.Max(1f, rect.Max.X - Metrics.Space.Sm * scale - textLeft), style,
            ready ? CasinoColors.MoneyHighlight with { W = face.LabelInk.W } : CasinoColors.InkBody);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return ready && !claiming && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private bool DrawStreak(ImDrawListPtr drawList, CasinoBonusDto streak, bool claiming, Vector2 origin, float width,
        float scale)
    {
        var view = StreakView.Of(streak, StreakDays);
        drawList.AddLine(origin, new Vector2(origin.X + width, origin.Y),
            ImGui.GetColorU32(CasinoArt.White with { W = SeparatorAlpha }), MathF.Max(1f, scale));
        var labelBlock = StreakLabelBlock();
        var rowTop = origin.Y + StreakSeparator * scale;
        var rowHeight = MathF.Max(labelBlock, StreakDot * scale * TodayScale);
        var centerY = rowTop + rowHeight * 0.5f;
        var labelStyle = TextStyles.FootnoteEmphasized;
        var dayStyle = TextStyles.Footnote;
        var label = Loc.T(L.Strip.BonusStreak);
        var day = texts.Counts(L.FloorPage.StreakDay, view.DayNumber, StreakDays);
        var column = StreakColumnWidth(Typography.Measure(label, labelStyle).X, Typography.Measure(day, dayStyle).X,
            width);
        var labelTop = centerY - labelBlock * 0.5f;
        Typography.Draw(drawList, new Vector2(origin.X, labelTop), Typography.FitText(label, column, labelStyle),
            Palette.WithAlpha(CasinoArt.White, SubAlpha), labelStyle);
        Typography.Draw(drawList, new Vector2(origin.X, labelTop + Typography.LineHeight(labelStyle)),
            Typography.FitText(day, column, dayStyle), CasinoColors.MoneyHighlight, dayStyle);
        var regionLeft = origin.X + column + StreakColumnGap * scale;
        var region = MathF.Max(0f, origin.X + width - regionLeft);
        var dot = StreakDotSize(region, StreakDot * scale, StreakMinGap * scale, StreakDays);
        var pitch = StreakPitch(region, dot, StreakDays);
        var rowRect = new Rect(new Vector2(origin.X, rowTop), new Vector2(origin.X + width, rowTop + rowHeight));
        streakCenter = new Vector2(regionLeft + Math.Max(0, view.TodayIndex) * pitch + dot * 0.5f, centerY);
        for (var dayIndex = 0; dayIndex < StreakDays; dayIndex++)
        {
            var center = new Vector2(regionLeft + dayIndex * pitch + dot * 0.5f, centerY);
            DrawStreakDay(drawList, view.StateOf(dayIndex), view.Claimable, center, dot * 0.5f, DayLabel(dayIndex),
                scale);
        }

        var hovered = view.Claimable && !claiming && UiInteract.Hover(rowRect.Min, rowRect.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            HoverTooltip.Show(rowRect, texts.Compact(L.Strip.BonusClaim, streak.Amount), HoverLabelSide.Below);
        }

        return hovered && UiInteract.Click(rowRect.Min, rowRect.Max, hovered);
    }

    private static void DrawStreakDay(ImDrawListPtr drawList, StreakDayState state, bool claimable, Vector2 center,
        float radius, string label, float scale)
    {
        var style = TextStyles.FootnoteEmphasized;
        var size = Typography.Measure(label, style);
        var labelAt = center - size * 0.5f;
        switch (state)
        {
            case StreakDayState.Done:
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(CasinoColors.Money with { W = DoneAlpha }),
                    20);
                Typography.Draw(drawList, labelAt, label, CasinoColors.InkOnLight, style);
                break;
            case StreakDayState.Today:
                var today = radius * TodayScale;
                var breath = claimable ? Pulse.Wave(Pulse.Breath) : 0f;
                var halo = TodayHaloAlpha * (claimable ? 1f - breath * 0.6f : 0.5f);
                drawList.AddCircleFilled(center, today * (1.3f + TodayHaloGrowth * breath),
                    ImGui.GetColorU32(CasinoColors.Money with { W = halo }), 24);
                drawList.AddCircleFilled(center, today + TodayRing * scale, ImGui.GetColorU32(CasinoArt.White), 24);
                drawList.AddCircleFilled(center, today, ImGui.GetColorU32(CasinoColors.Money), 24);
                Typography.Draw(drawList, labelAt, label, CasinoColors.InkOnLight, style);
                break;
            default:
                drawList.AddCircle(center, radius - 0.75f * scale,
                    ImGui.GetColorU32(CasinoArt.White with { W = FutureRingAlpha }), 20, 1.5f * scale);
                Typography.Draw(drawList, labelAt, label, Palette.WithAlpha(CasinoArt.White, FutureInkAlpha), style);
                break;
        }
    }

    private string DayLabel(int day)
    {
        return dayLabels[day] ??= Games.Framework.GameNumber.Label(day + 1);
    }
}
