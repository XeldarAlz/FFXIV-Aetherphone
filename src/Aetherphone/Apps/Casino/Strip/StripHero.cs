using Aetherphone.Apps.Casino.Stage;
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
    bool Claiming,
    long NowUnix);

internal sealed class StripHero
{
    public const float Pad = 18f;
    public const float GlyphSize = 30f;
    public const float RowGap = 12f;
    public const float BonusRing = 30f;
    public const float RingThickness = 3.5f;
    public const float StreakDot = 26f;
    public const float StreakGap = 6f;
    public const int StreakDays = CasinoFaucets.StreakDays;
    public const long TimedPeriodSeconds = 3 * 3600;

    private const float SurfaceLuminance = 0.22f;
    private const float GlassOpacity = 0.94f;
    private const float SubAlpha = 0.86f;
    private const float IdleAlpha = 0.6f;

    private static readonly TextStyle AmountStyle = TextStyles.LargeTitle;

    private readonly CasinoTextCache texts = new();
    private readonly string[] dayLabels = new string[StreakDays];
    private RollingAmount balance;
    private LevelCapsule level;
    private Vector2 timedCenter;
    private Vector2 streakCenter;

    public Vector2 TimedCenter => timedCenter;

    public Vector2 StreakCenter => streakCenter;

    public void Snap(long value) => balance.Snap(value);

    public static float Height(in StripHeroModel model, float scale)
    {
        var pad = Pad * scale;
        var gap = RowGap * scale;
        var height = pad * 2f + MathF.Max(GlyphSize * scale, Typography.LineHeight(TextStyles.SubheadlineEmphasized))
                     + Typography.LineHeight(AmountStyle) + gap + Button.LargeHeight * scale;
        if (model.ShowsLevel)
        {
            height += gap + LevelCapsule.Height * scale;
        }

        if (model.Streak is not null)
        {
            height += gap + StreakDot * scale;
        }

        return height;
    }

    public StripHeroAction Draw(ImDrawListPtr drawList, AppSkin ui, in StripHeroModel model, Vector2 origin,
        float width, float deltaSeconds, float scale, out float bottom)
    {
        var height = Height(model, scale);
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
        var headRow = MathF.Max(GlyphSize * scale, Typography.LineHeight(TextStyles.SubheadlineEmphasized));
        var glyphCenter = new Vector2(left + GlyphSize * scale * 0.5f, top + headRow * 0.5f);
        CurrencyGlyph.Draw(drawList, CurrencyKind.Chips, glyphCenter, GlyphSize * scale);
        var title = StatusTitle.For(model.Balance);
        var titleWidth = StatusTitle.Width(title, width * 0.4f, scale);
        if (titleWidth > 0f)
        {
            StatusTitle.Draw(drawList, new Vector2(right - titleWidth * 0.5f, glyphCenter.Y), title, width * 0.4f,
                scale);
        }

        var labelLeft = glyphCenter.X + GlyphSize * scale * 0.5f + Metrics.Space.Sm * scale;
        var labelRight = right - (titleWidth > 0f ? titleWidth + Metrics.Space.Sm * scale : 0f);
        var label = Typography.FitText(Loc.T(L.Strip.YourChips), MathF.Max(1f, labelRight - labelLeft),
            TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList,
            new Vector2(labelLeft, glyphCenter.Y - Typography.LineHeight(TextStyles.SubheadlineEmphasized) * 0.5f),
            label, Palette.WithAlpha(CasinoArt.White, SubAlpha), TextStyles.SubheadlineEmphasized);
        top += headRow;

        balance.Update(model.Balance, deltaSeconds);
        var amount = NumberText.Compact(balance.Display);
        var amountHeight = Typography.LineHeight(AmountStyle);
        var amountScale = Typography.FitScale(amount, right - left, AmountStyle.Scale * balance.PopScale,
            TextStyles.Title2.Scale, AmountStyle.Weight);
        var amountSize = Typography.Measure(amount, amountScale, AmountStyle.Weight);
        var amountRect = new Rect(new Vector2(left, top), new Vector2(right, top + amountHeight));
        Typography.Draw(drawList, new Vector2(left, top + (amountHeight - amountSize.Y) * 0.5f), amount,
            model.Balance > 0 ? CasinoColors.Money : Palette.WithAlpha(CasinoArt.White, IdleAlpha), amountScale,
            AmountStyle.Weight);
        var overAmount = UiInteract.Hover(amountRect.Min, amountRect.Max);
        if (overAmount)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            HoverTooltip.Show(amountRect, NumberText.Group(model.Balance), HoverLabelSide.Below);
        }

        if (UiInteract.Click(amountRect.Min, amountRect.Max, overAmount))
        {
            action = StripHeroAction.Cashier;
        }

        top += amountHeight;
        if (model.ShowsLevel)
        {
            top += gap;
            var capsule = new Rect(new Vector2(left, top), new Vector2(right, top + LevelCapsule.Height * scale));
            level.Draw(drawList, capsule, Math.Max(1, model.Level), model.LevelProgress, model.LevelCap,
                CasinoColors.Money, scale);
            top = capsule.Max.Y;
        }

        top += gap;
        var buttonHeight = Button.LargeHeight * scale;
        var buttonGap = Metrics.Space.Sm * scale;
        var hasTimed = model.Timed is not null;
        var getWidth = hasTimed ? (right - left - buttonGap) * 0.5f : right - left;
        var getRect = new Rect(new Vector2(left, top), new Vector2(left + getWidth, top + buttonHeight));
        if (DrawWhitePill(drawList, ui, getRect, Loc.T(L.Strip.GetChips), surface))
        {
            action = StripHeroAction.GetChips;
        }

        if (hasTimed)
        {
            var bonusRect = new Rect(new Vector2(getRect.Max.X + buttonGap, top), new Vector2(right, top + buttonHeight));
            UiAnchors.Report("casino.bonus", bonusRect);
            if (DrawTimed(drawList, ui, bonusRect, model.Timed!, model.Claiming, model.NowUnix, scale))
            {
                action = StripHeroAction.ClaimTimed;
            }
        }

        top += buttonHeight;
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

    private static bool DrawWhitePill(ImDrawListPtr drawList, AppSkin ui, Rect rect, string label, Vector4 surface)
    {
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var face = Button.Surface(drawList, rect, ui.Ink.WithAccent(CasinoArt.White), ButtonStyle.Prominent,
            ButtonRole.Normal, true, hovered, ImGui.GetID("casino.hero.get"));
        Button.DrawLabel(drawList, face with { LabelInk = surface with { W = face.LabelInk.W } }, label);
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
        var face = Button.Surface(drawList, rect, ui.Ink, ready ? ButtonStyle.Tinted : ButtonStyle.Gray,
            ButtonRole.Normal, ready && !claiming, hovered, ImGui.GetID("casino.hero.timed"));
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
        var shown = Typography.FitText(label, MathF.Max(1f, rect.Max.X - Metrics.Space.Sm * scale - textLeft), style);
        var size = Typography.Measure(shown, style);
        Typography.Draw(drawList, new Vector2(textLeft, rect.Center.Y - size.Y * 0.5f), shown,
            ready ? face.LabelInk : CasinoColors.InkBody, style);
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
        var labelWidth = MathF.Max(0f, width - dotsWidth - Metrics.Space.Md * scale);
        var label = Typography.FitText(Loc.T(L.Strip.BonusStreak), labelWidth, labelStyle);
        Typography.Draw(drawList, new Vector2(origin.X, origin.Y + (dot - Typography.LineHeight(labelStyle)) * 0.5f),
            label, Palette.WithAlpha(CasinoArt.White, SubAlpha), labelStyle);
        var ready = streak.Ready && streak.Eligible;
        var paid = Math.Clamp(ready ? streak.StreakDay - 1 : streak.StreakDay, 0, StreakDays);
        var stripLeft = origin.X + width - dotsWidth;
        var stripRect = new Rect(new Vector2(stripLeft, origin.Y), new Vector2(origin.X + width, origin.Y + dot));
        streakCenter = new Vector2(stripLeft + paid * (dot + gap) + dot * 0.5f, origin.Y + dot * 0.5f);
        for (var day = 0; day < StreakDays; day++)
        {
            var center = new Vector2(stripLeft + day * (dot + gap) + dot * 0.5f, origin.Y + dot * 0.5f);
            var radius = dot * 0.5f;
            var dayLabel = DayLabel(day);
            if (day < paid)
            {
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(CasinoColors.Money), 20);
                Typography.DrawCentered(drawList, center, dayLabel, CasinoColors.FeltBottom, TextStyles.FootnoteEmphasized);
                continue;
            }

            if (day == paid && ready)
            {
                var glow = 0.55f + 0.45f * Pulse.Wave(Pulse.Breath);
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(CasinoColors.Money with { W = glow }), 20);
                Typography.DrawCentered(drawList, center, dayLabel, CasinoColors.FeltBottom, TextStyles.FootnoteEmphasized);
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
