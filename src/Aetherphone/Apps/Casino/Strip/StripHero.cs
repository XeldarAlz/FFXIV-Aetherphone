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
    public const float StreakDot = 26f;
    public const float StreakGap = 6f;
    public const int StreakDays = CasinoFaucets.StreakDays;
    public const long TimedPeriodSeconds = 3 * 3600;
    public const float StreakLabelGap = 6f;
    public const float LabelSlack = 4f;

    private const float SurfaceLuminance = 0.07f;
    private const float GlassOpacity = 0.94f;
    private const float SubAlpha = 0.86f;
    private const float IdleAlpha = 0.6f;
    private const float FitSlack = 0.5f;
    private const float TitleShare = 0.4f;
    private const string GetChipsId = "casino.hero.get";
    private const string SpinId = "casino.hero.spin";
    private const string TimedId = "casino.hero.timed";

    private static readonly TextStyle AmountStyle = TextStyles.LargeTitle;

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

    public static float DetailRowHeight(float footnoteLine, float titleHeight) => MathF.Max(footnoteLine, titleHeight);

    public static float ClaimsHeight(StripHeroClaims claims, float buttonHeight) =>
        (claims & (StripHeroClaims.Timed | StripHeroClaims.Spin)) != 0 ? buttonHeight : 0f;

    public static float SharedClaimSplit(float rowWidth, float timedWidth, float spinWidth, float gap)
    {
        var half = (rowWidth - gap) * 0.5f;
        return MathF.Max(timedWidth, spinWidth) <= half ? half : MathF.Min(timedWidth, rowWidth - gap - spinWidth);
    }

    public static float StreakHeight(float labelHeight, float dot, float labelGap) => labelHeight + labelGap + dot;

    public float Height(in StripHeroModel model, float width, float scale)
    {
        var pad = Pad * scale;
        var gap = RowGap * scale;
        var getHeight = Button.RegularHeight * scale;
        var inline = ChipsInline(model.Balance, width - pad * 2f, scale);
        var height = pad * 2f + BalanceRowHeight(Typography.LineHeight(AmountStyle), inline ? getHeight : 0f)
                     + DetailRowHeight(Typography.LineHeight(TextStyles.Footnote),
                         StatusTitle.PillHeight(StatusTitle.For(model.Balance), scale));
        if (!inline)
        {
            height += gap + getHeight;
        }

        if (model.ShowsLevel)
        {
            height += gap + LevelCapsule.Height * scale;
        }

        var claims = ClaimsHeight(ClaimsOf(model), Button.LargeHeight * scale);
        if (claims > 0f)
        {
            height += gap + claims;
        }

        if (model.Streak is not null)
        {
            height += gap + StreakHeight(Typography.LineHeight(TextStyles.FootnoteEmphasized), StreakDot * scale,
                StreakLabelGap * scale);
        }

        return height;
    }

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
            Button.WidthFor(Loc.T(L.Floor.FreeSpin), ButtonSize.Large) + LabelSlack * scale);

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
        var balanceRow = BalanceRowHeight(Typography.LineHeight(AmountStyle), inline ? getHeight : 0f);
        if (DrawBalance(drawList, model, left, inline ? right - getWidth - buttonGap : right, top, balanceRow,
                deltaSeconds))
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

        top = DrawDetailRow(drawList, model, left, right, top + balanceRow, scale);
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
            var capsule = new Rect(new Vector2(left, top), new Vector2(right, top + LevelCapsule.Height * scale));
            level.Draw(drawList, capsule, Math.Max(1, model.Level), model.LevelProgress, model.LevelCap,
                CasinoColors.Money, scale);
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
        float rowHeight, float deltaSeconds)
    {
        balance.Update(model.Balance, deltaSeconds);
        var amount = NumberText.Compact(balance.Display);
        var reserve = CurrencyGlyph.Reserve(Typography.LineHeight(AmountStyle));
        var amountScale = Typography.FitScale(amount, MathF.Max(1f, right - left - reserve),
            AmountStyle.Scale * balance.PopScale, TextStyles.Title2.Scale, AmountStyle.Weight);
        var size = Typography.Measure(amount, amountScale, AmountStyle.Weight);
        var center = top + rowHeight * 0.5f;
        var glyph = size.Y * CurrencyGlyph.GlyphFraction;
        CurrencyGlyph.Draw(drawList, CurrencyKind.Chips, new Vector2(left + glyph * 0.5f, center), glyph);
        var textLeft = left + CurrencyGlyph.Reserve(size.Y);
        Typography.Draw(drawList, new Vector2(textLeft, center - size.Y * 0.5f), amount,
            model.Balance > 0 ? CasinoColors.Money : Palette.WithAlpha(CasinoArt.White, IdleAlpha), amountScale,
            AmountStyle.Weight);
        var amountRect = new Rect(new Vector2(left, top),
            new Vector2(MathF.Min(right, textLeft + size.X), top + rowHeight));
        var overAmount = UiInteract.Hover(amountRect.Min, amountRect.Max);
        if (overAmount)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            HoverTooltip.Show(amountRect, NumberText.Group(model.Balance), HoverLabelSide.Below);
        }

        return UiInteract.Click(amountRect.Min, amountRect.Max, overAmount);
    }

    private float DrawDetailRow(ImDrawListPtr drawList, in StripHeroModel model, float left, float right, float top,
        float scale)
    {
        var title = StatusTitle.For(model.Balance);
        var titleMax = (right - left) * TitleShare;
        var titleWidth = StatusTitle.Width(title, titleMax, scale);
        var footnote = Typography.LineHeight(TextStyles.Footnote);
        var rowHeight = DetailRowHeight(footnote, StatusTitle.PillHeight(title, scale));
        var center = top + rowHeight * 0.5f;
        if (titleWidth > 0f)
        {
            StatusTitle.Draw(drawList, new Vector2(right - titleWidth * 0.5f, center), title, titleMax, scale);
        }

        var coinsWidth = MathF.Max(1f, right - (titleWidth > 0f ? titleWidth + Metrics.Space.Sm * scale : 0f) - left);
        var coins = worth.Full(model.Balance, model.Rate);
        var coinsText = Typography.Measure(coins, TextStyles.Footnote).X <= coinsWidth
            ? coins
            : worth.Compact(model.Balance, model.Rate);
        Typography.Draw(drawList, new Vector2(left, center - footnote * 0.5f),
            Typography.FitText(coinsText, coinsWidth, TextStyles.Footnote), Palette.WithAlpha(CasinoArt.White, SubAlpha),
            TextStyles.Footnote);
        return top + rowHeight;
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

        if (hasSpin && DrawSpin(drawList, ui, spinRect))
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

    private bool DrawSpin(ImDrawListPtr drawList, AppSkin ui, Rect rect)
    {
        spinSource = rect;
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var face = Button.Surface(drawList, rect, ui.Ink.WithAccent(CasinoColors.Money), ButtonStyle.Tinted,
            ButtonRole.Normal, true, hovered, ImGui.GetID(SpinId));
        Button.DrawLabel(drawList, face with { LabelInk = CasinoColors.MoneyHighlight with { W = face.LabelInk.W } },
            Loc.T(L.Floor.FreeSpin), SpinId);
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
        var dot = StreakDot * scale;
        var gap = StreakGap * scale;
        var labelStyle = TextStyles.FootnoteEmphasized;
        var dotsWidth = StreakDays * dot + (StreakDays - 1) * gap;
        var label = Typography.FitText(Loc.T(L.Strip.BonusStreak), width, labelStyle);
        Typography.Draw(drawList, origin, label, Palette.WithAlpha(CasinoArt.White, SubAlpha), labelStyle);
        origin = new Vector2(origin.X, origin.Y + Typography.LineHeight(labelStyle) + StreakLabelGap * scale);
        var ready = streak.Ready && streak.Eligible;
        var paid = Math.Clamp(ready ? streak.StreakDay - 1 : streak.StreakDay, 0, StreakDays);
        var stripLeft = origin.X;
        var stripRect = new Rect(new Vector2(stripLeft, origin.Y),
            new Vector2(stripLeft + MathF.Min(dotsWidth, width), origin.Y + dot));
        streakCenter = new Vector2(stripLeft + paid * (dot + gap) + dot * 0.5f, origin.Y + dot * 0.5f);
        for (var day = 0; day < StreakDays; day++)
        {
            var center = new Vector2(stripLeft + day * (dot + gap) + dot * 0.5f, origin.Y + dot * 0.5f);
            var radius = dot * 0.5f;
            var dayLabel = DayLabel(day);
            if (day < paid)
            {
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(CasinoColors.Money), 20);
                Typography.DrawCentered(drawList, center, dayLabel, CasinoColors.InkOnLight, TextStyles.FootnoteEmphasized);
                continue;
            }

            if (day == paid && ready)
            {
                var glow = 0.55f + 0.45f * Pulse.Wave(Pulse.Breath);
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(CasinoColors.Money with { W = glow }), 20);
                Typography.DrawCentered(drawList, center, dayLabel, CasinoColors.InkOnLight, TextStyles.FootnoteEmphasized);
                continue;
            }

            drawList.AddCircle(center, radius - scale, ImGui.GetColorU32(Palette.WithAlpha(CasinoArt.White, 0.5f)),
                20, 1.5f * scale);
            Typography.DrawCentered(drawList, center, dayLabel, Palette.WithAlpha(CasinoArt.White, SubAlpha),
                TextStyles.FootnoteEmphasized);
        }

        var hovered = ready && !claiming && UiInteract.Hover(stripRect.Min, stripRect.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            HoverTooltip.Show(stripRect, texts.Compact(L.Strip.BonusClaim, streak.Amount), HoverLabelSide.Below);
        }

        return hovered && UiInteract.Click(stripRect.Min, stripRect.Max, hovered);
    }

    private string DayLabel(int day)
    {
        return dayLabels[day] ??= Games.Framework.GameNumber.Label(day + 1);
    }
}
