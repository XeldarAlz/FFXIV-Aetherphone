using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.MoogleClicker;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.MoogleClicker;

internal enum SheetKind : byte
{
    None,
    Stats,
    Ledger,
    Confirm,
}

internal enum SheetAction : byte
{
    None,
    CloseLedger,
    CollectAway,
}

internal sealed class MoogleClickerSheets
{
    private const string SurfaceId = "moogleclicker.sheet";
    private const string LedgerButtonId = "moogleclicker.ledger.close";
    private const string ConfirmButtonId = "moogleclicker.ledger.confirm";
    private const string AwayButtonId = "moogleclicker.away.collect";
    private const string StatsButtonId = "moogleclicker.stats.close";
    private const float AppearSpeed = 3.6f;
    private const float VeilAlpha = 0.62f;
    private const float CardMaxWidth = 320f;
    private const float CardMargin = 16f;
    private const float CardRadius = 22f;
    private const float EmblemRadius = 28f;
    private const float BarHeight = 8f;
    private const float SlideDistance = 28f;
    private const float StatRowGap = 6f;
    private const float LabelShare = 0.6f;
    private const int StatCount = 13;
    private static readonly Vector4 Gold = new(1f, 0.84f, 0.36f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    private static readonly LocString[] StatLabels =
    {
        L.MoogleClicker.StatBank, L.MoogleClicker.StatLedger, L.MoogleClicker.StatLifetime,
        L.MoogleClicker.StatPerSecond, L.MoogleClicker.StatPerTap, L.MoogleClicker.StatTaps,
        L.MoogleClicker.StatTapKupo, L.MoogleClicker.StatBuildings, L.MoogleClicker.StatUpgrades,
        L.MoogleClicker.StatMinions, L.MoogleClicker.StatLedgerLevel, L.MoogleClicker.StatPages,
        L.MoogleClicker.StatBonus,
    };

    private readonly string[] statValues = new string[StatCount];
    private TextSlot upgradesValue;
    private TextSlot bonusValue;
    private TextSlot ledgerLevel;
    private TextSlot ledgerBonus;
    private TextSlot ledgerWaiting;
    private TextSlot ledgerNext;
    private TextSlot ledgerExplain;
    private TextSlot confirmBody;
    private TextSlot awayBody;
    private TextSlot awayCapped;
    private float appear;

    public SheetKind Kind { get; private set; }

    public bool Blocking(KupoWorkshop workshop) => Kind != SheetKind.None || workshop.HasAwayReport;

    public void Open(SheetKind kind)
    {
        if (Kind == SheetKind.None)
        {
            appear = 0f;
        }

        Kind = kind;
    }

    public void Close()
    {
        Kind = SheetKind.None;
    }

    public SheetAction Draw(ImDrawListPtr drawList, Rect full, KupoWorkshop workshop, long now, Vector4 accent,
        PhoneTheme theme, bool interactive, float deltaSeconds, float scale)
    {
        var away = workshop.HasAwayReport;
        if (!away && Kind == SheetKind.None)
        {
            appear = 0f;
            return SheetAction.None;
        }

        appear = GameJuice.Advance(appear, deltaSeconds, AppearSpeed);
        var alpha = MathF.Min(1f, appear * 1.5f);
        var slide = (1f - Easing.EaseOutCubic(appear)) * SlideDistance * scale;
        Material.Veil(drawList, full.Min, full.Max, VeilAlpha * alpha);
        if (interactive)
        {
            PressSurface.Claim(SurfaceId, full, out _);
        }

        if (away)
        {
            return DrawAway(drawList, full, workshop, accent, theme, interactive, alpha, slide, scale);
        }

        return Kind switch
        {
            SheetKind.Stats => DrawStats(drawList, full, workshop, now, accent, theme, interactive, alpha, slide, scale),
            SheetKind.Ledger => DrawLedger(drawList, full, workshop, accent, theme, interactive, alpha, slide, scale),
            _ => DrawConfirm(drawList, full, workshop, accent, theme, interactive, alpha, slide, scale),
        };
    }

    private SheetAction DrawStats(ImDrawListPtr drawList, Rect full, KupoWorkshop workshop, long now, Vector4 accent,
        PhoneTheme theme, bool interactive, float alpha, float slide, float scale)
    {
        FillStats(workshop, now);
        var padding = Metrics.Space.Xl * scale;
        var titleHeight = Typography.LineHeight(TextStyles.Title2);
        var rowHeight = Typography.LineHeight(TextStyles.Subheadline) + StatRowGap * scale;
        var buttonHeight = Button.Height(ButtonSize.Regular) * scale;
        var fixedHeight = titleHeight + Metrics.Space.Md * scale + Metrics.Space.Lg * scale + buttonHeight;
        var available = full.Height - StageLayout.ChromeBand * scale - CardMargin * 2f * scale - padding * 2f;
        rowHeight = MathF.Min(rowHeight, MathF.Max(1f, (available - fixedHeight) / StatCount));
        var card = Card(full, fixedHeight + rowHeight * StatCount, slide, scale, out var inner);
        DrawCardBack(drawList, card, accent, alpha, scale);
        var y = inner.Min.Y;
        Typography.DrawCentered(drawList, new Vector2(inner.Center.X, y + titleHeight * 0.5f),
            Typography.FitText(Loc.T(L.MoogleClicker.StatsTitle), inner.Width, TextStyles.Title2),
            StageInks.Strong with { W = alpha }, TextStyles.Title2);
        y += titleHeight + Metrics.Space.Md * scale;
        var labelWidth = inner.Width * LabelShare;
        var lineHeight = Typography.LineHeight(TextStyles.Subheadline);
        for (var stat = 0; stat < StatCount; stat++)
        {
            var textY = y + (rowHeight - lineHeight) * 0.5f;
            Typography.Draw(drawList, new Vector2(inner.Min.X, textY),
                Typography.FitText(Loc.T(StatLabels[stat]), labelWidth, TextStyles.Subheadline),
                StageInks.Muted with { W = alpha }, TextStyles.Subheadline);
            var value = statValues[stat];
            var valueSize = Typography.Measure(value, TextStyles.SubheadlineEmphasized);
            Typography.Draw(drawList, new Vector2(inner.Max.X - valueSize.X, textY), value,
                StageInks.Strong with { W = alpha }, TextStyles.SubheadlineEmphasized);
            if (stat > 0)
            {
                drawList.AddLine(new Vector2(inner.Min.X, y), new Vector2(inner.Max.X, y),
                    ImGui.GetColorU32(StageInks.Strong with { W = 0.07f * alpha }), Metrics.Stroke.Hairline * scale);
            }

            y += rowHeight;
        }

        y += Metrics.Space.Lg * scale;
        var button = Centered(inner.Center.X, y, inner.Width * 0.6f, buttonHeight);
        var ink = MoogleClickerText.Controls(accent, theme);
        if (Button.Draw(drawList, button, Loc.T(L.Common.Close), ink, ButtonStyle.Tinted, enabled: interactive,
                id: StatsButtonId) || DismissedOutside(card, interactive))
        {
            Close();
        }

        return SheetAction.None;
    }

    private SheetAction DrawLedger(ImDrawListPtr drawList, Rect full, KupoWorkshop workshop, Vector4 accent,
        PhoneTheme theme, bool interactive, float alpha, float slide, float scale)
    {
        var pending = workshop.PendingStamps;
        var current = Math.Max(workshop.Stamps, KupoLedger.StampsFor(workshop.LifetimeKupo));
        var low = KupoLedger.KupoForStamps(current);
        var high = KupoLedger.KupoForStamps(current + 1d);
        var fraction = high > low ? (float)Math.Clamp((workshop.LifetimeKupo - low) / (high - low), 0d, 1d) : 0f;
        var level = ledgerLevel.Get(L.MoogleClicker.LedgerLevel, GameNumber.Label(workshop.LedgerLevel));
        var bonus = ledgerBonus.Get(L.MoogleClicker.LedgerBonus,
            GameNumber.Label(KupoLedger.BonusPercent(workshop.Stamps)));
        var waiting = ledgerWaiting.Get(L.MoogleClicker.LedgerWaiting, KupoFormat.Amount(pending));
        var next = ledgerNext.Get(L.MoogleClicker.LedgerNext, KupoFormat.Amount(high));
        var explain = ledgerExplain.Get(L.MoogleClicker.LedgerExplain,
            GameNumber.Label((int)Math.Round(KupoLedger.BonusPerStamp * 100d)));
        var padding = Metrics.Space.Xl * scale;
        var width = CardWidth(full, scale) - padding * 2f;
        var titleHeight = Typography.LineHeight(TextStyles.Title2);
        var emblem = EmblemRadius * scale * 2f;
        var levelHeight = Typography.LineHeight(TextStyles.Title3);
        var bonusHeight = Typography.LineHeight(TextStyles.Subheadline);
        var waitingHeight = Typography.LineHeight(TextStyles.Headline);
        var nextHeight = Typography.LineHeight(TextStyles.Caption1);
        var explainHeight = Typography.MeasureWrappedBlock(explain, TextStyles.Footnote, width).Y;
        var buttonHeight = Button.Height(ButtonSize.Large) * scale;
        var closeHeight = Button.Height(ButtonSize.Small) * scale;
        var barHeight = BarHeight * scale;
        var contentHeight = titleHeight + Metrics.Space.Md * scale + emblem + Metrics.Space.Sm * scale + levelHeight +
                            bonusHeight + Metrics.Space.Lg * scale + waitingHeight + Metrics.Space.Xs * scale +
                            barHeight + Metrics.Space.Xs * scale + nextHeight + Metrics.Space.Md * scale +
                            explainHeight + Metrics.Space.Lg * scale + buttonHeight + Metrics.Space.Sm * scale +
                            closeHeight;
        var card = Card(full, contentHeight, slide, scale, out var inner);
        DrawCardBack(drawList, card, accent, alpha, scale);
        var centerX = inner.Center.X;
        var y = inner.Min.Y;
        Typography.DrawCentered(drawList, new Vector2(centerX, y + titleHeight * 0.5f),
            Typography.FitText(Loc.T(L.MoogleClicker.LedgerTitle), inner.Width, TextStyles.Title2),
            StageInks.Strong with { W = alpha }, TextStyles.Title2);
        y += titleHeight + Metrics.Space.Md * scale;
        DrawEmblem(drawList, new Vector2(centerX, y + emblem * 0.5f), FontAwesomeIcon.BookOpen,
            pending >= 1d ? Gold : accent, pending >= 1d, alpha, scale);
        y += emblem + Metrics.Space.Sm * scale;
        Typography.DrawCentered(drawList, new Vector2(centerX, y + levelHeight * 0.5f), level,
            StageInks.Strong with { W = alpha }, TextStyles.Title3);
        y += levelHeight;
        Typography.DrawCentered(drawList, new Vector2(centerX, y + bonusHeight * 0.5f),
            Typography.FitText(bonus, inner.Width, TextStyles.Subheadline), StageInks.Muted with { W = alpha },
            TextStyles.Subheadline);
        y += bonusHeight + Metrics.Space.Lg * scale;
        Typography.DrawCentered(drawList, new Vector2(centerX, y + waitingHeight * 0.5f),
            Typography.FitText(waiting, inner.Width, TextStyles.Headline),
            (pending >= 1d ? Gold : StageInks.Strong) with { W = alpha }, TextStyles.Headline);
        y += waitingHeight + Metrics.Space.Xs * scale;
        var bar = new Rect(new Vector2(inner.Min.X, y), new Vector2(inner.Max.X, y + barHeight));
        Squircle.Fill(drawList, bar.Min, bar.Max, barHeight * 0.5f, ImGui.GetColorU32(White with { W = 0.12f * alpha }));
        if (fraction > 0f)
        {
            Squircle.Fill(drawList, bar.Min, new Vector2(bar.Min.X + MathF.Max(barHeight, bar.Width * fraction), bar.Max.Y),
                barHeight * 0.5f, ImGui.GetColorU32(Gold with { W = alpha }));
        }

        y += barHeight + Metrics.Space.Xs * scale;
        Typography.DrawCentered(drawList, new Vector2(centerX, y + nextHeight * 0.5f),
            Typography.FitText(next, inner.Width, TextStyles.Caption1), StageInks.Muted with { W = alpha },
            TextStyles.Caption1);
        y += nextHeight + Metrics.Space.Md * scale;
        Typography.DrawWrappedCentered(drawList, new Vector2(centerX, y + explainHeight * 0.5f), explain,
            StageInks.Muted with { W = alpha }, TextStyles.Footnote, width);
        y += explainHeight + Metrics.Space.Lg * scale;
        var ink = MoogleClickerText.Controls(accent, theme);
        var button = Centered(centerX, y, inner.Width, buttonHeight);
        if (Button.Draw(drawList, button, Loc.T(L.MoogleClicker.LedgerClose), ink, ButtonStyle.Prominent,
                enabled: interactive && pending >= 1d, id: LedgerButtonId))
        {
            Kind = SheetKind.Confirm;
            return SheetAction.None;
        }

        y += buttonHeight + Metrics.Space.Sm * scale;
        var closeClicked = TextButton.Draw(new Vector2(centerX, y + closeHeight * 0.5f), Loc.T(L.Common.Close),
            StageInks.Muted, scale);
        if ((closeClicked && interactive) || DismissedOutside(card, interactive))
        {
            Close();
        }

        return SheetAction.None;
    }

    private SheetAction DrawConfirm(ImDrawListPtr drawList, Rect full, KupoWorkshop workshop, Vector4 accent,
        PhoneTheme theme, bool interactive, float alpha, float slide, float scale)
    {
        var body = confirmBody.Get(L.MoogleClicker.ConfirmBody, KupoFormat.Amount(workshop.PendingStamps));
        var padding = Metrics.Space.Xl * scale;
        var width = CardWidth(full, scale) - padding * 2f;
        var emblem = EmblemRadius * scale * 2f;
        var titleHeight = Typography.LineHeight(TextStyles.Title2);
        var bodyHeight = Typography.MeasureWrappedBlock(body, TextStyles.Subheadline, width).Y;
        var buttonHeight = Button.Height(ButtonSize.Large) * scale;
        var cancelHeight = Button.Height(ButtonSize.Small) * scale;
        var contentHeight = emblem + Metrics.Space.Md * scale + titleHeight + Metrics.Space.Sm * scale + bodyHeight +
                            Metrics.Space.Xl * scale + buttonHeight + Metrics.Space.Sm * scale + cancelHeight;
        var card = Card(full, contentHeight, slide, scale, out var inner);
        DrawCardBack(drawList, card, Gold, alpha, scale);
        var centerX = inner.Center.X;
        var y = inner.Min.Y;
        DrawEmblem(drawList, new Vector2(centerX, y + emblem * 0.5f), FontAwesomeIcon.BookOpen, Gold, true, alpha,
            scale);
        y += emblem + Metrics.Space.Md * scale;
        Typography.DrawCentered(drawList, new Vector2(centerX, y + titleHeight * 0.5f),
            Typography.FitText(Loc.T(L.MoogleClicker.ConfirmTitle), inner.Width, TextStyles.Title2),
            StageInks.Strong with { W = alpha }, TextStyles.Title2);
        y += titleHeight + Metrics.Space.Sm * scale;
        Typography.DrawWrappedCentered(drawList, new Vector2(centerX, y + bodyHeight * 0.5f), body,
            StageInks.Muted with { W = alpha }, TextStyles.Subheadline, width);
        y += bodyHeight + Metrics.Space.Xl * scale;
        var ink = MoogleClickerText.Controls(accent, theme);
        var button = Centered(centerX, y, inner.Width, buttonHeight);
        if (Button.Draw(drawList, button, Loc.T(L.MoogleClicker.ConfirmYes), ink, ButtonStyle.Prominent,
                enabled: interactive && workshop.PendingStamps >= 1d, id: ConfirmButtonId))
        {
            Close();
            return SheetAction.CloseLedger;
        }

        y += buttonHeight + Metrics.Space.Sm * scale;
        var cancelClicked = TextButton.Draw(new Vector2(centerX, y + cancelHeight * 0.5f),
            Loc.T(L.MoogleClicker.ConfirmNo), StageInks.Muted, scale);
        if ((cancelClicked && interactive) || DismissedOutside(card, interactive))
        {
            Kind = SheetKind.Ledger;
        }

        return SheetAction.None;
    }

    private SheetAction DrawAway(ImDrawListPtr drawList, Rect full, KupoWorkshop workshop, Vector4 accent,
        PhoneTheme theme, bool interactive, float alpha, float slide, float scale)
    {
        var amount = KupoFormat.Amount(workshop.AwayKupo);
        var body = awayBody.Get(L.MoogleClicker.AwayBody, amount, TimeText.Duration((int)workshop.AwaySeconds));
        var capped = workshop.AwayCapped
            ? awayCapped.Get(L.MoogleClicker.AwayCapped,
                GameNumber.Label((int)(KupoWorkshop.OfflineCapSeconds / 3600d)))
            : string.Empty;
        var padding = Metrics.Space.Xl * scale;
        var width = CardWidth(full, scale) - padding * 2f;
        var emblem = EmblemRadius * scale * 2f;
        var titleHeight = Typography.LineHeight(TextStyles.Title2);
        var amountHeight = Typography.LineHeight(TextStyles.Title1);
        var bodyHeight = Typography.MeasureWrappedBlock(body, TextStyles.Subheadline, width).Y;
        var cappedHeight = capped.Length > 0 ? Typography.MeasureWrappedBlock(capped, TextStyles.Footnote, width).Y : 0f;
        var buttonHeight = Button.Height(ButtonSize.Large) * scale;
        var contentHeight = emblem + Metrics.Space.Md * scale + titleHeight + Metrics.Space.Sm * scale + amountHeight +
                            Metrics.Space.Sm * scale + bodyHeight + Metrics.Space.Xl * scale + buttonHeight;
        if (cappedHeight > 0f)
        {
            contentHeight += Metrics.Space.Xs * scale + cappedHeight;
        }

        var card = Card(full, contentHeight, slide, scale, out var inner);
        DrawCardBack(drawList, card, accent, alpha, scale);
        var centerX = inner.Center.X;
        var y = inner.Min.Y;
        DrawEmblem(drawList, new Vector2(centerX, y + emblem * 0.5f), FontAwesomeIcon.Moon, accent, false, alpha, scale);
        y += emblem + Metrics.Space.Md * scale;
        Typography.DrawCentered(drawList, new Vector2(centerX, y + titleHeight * 0.5f),
            Typography.FitText(Loc.T(L.MoogleClicker.AwayTitle), inner.Width, TextStyles.Title2),
            StageInks.Strong with { W = alpha }, TextStyles.Title2);
        y += titleHeight + Metrics.Space.Sm * scale;
        Typography.DrawCentered(drawList, new Vector2(centerX, y + amountHeight * 0.5f), amount, Gold with { W = alpha },
            TextStyles.Title1);
        y += amountHeight + Metrics.Space.Sm * scale;
        Typography.DrawWrappedCentered(drawList, new Vector2(centerX, y + bodyHeight * 0.5f), body,
            StageInks.Muted with { W = alpha }, TextStyles.Subheadline, width);
        y += bodyHeight;
        if (cappedHeight > 0f)
        {
            y += Metrics.Space.Xs * scale;
            Typography.DrawWrappedCentered(drawList, new Vector2(centerX, y + cappedHeight * 0.5f), capped,
                StageInks.Muted with { W = 0.75f * alpha }, TextStyles.Footnote, width);
            y += cappedHeight;
        }

        y += Metrics.Space.Xl * scale;
        var ink = MoogleClickerText.Controls(accent, theme);
        var button = Centered(centerX, y, inner.Width * 0.7f, buttonHeight);
        return Button.Draw(drawList, button, Loc.T(L.MoogleClicker.AwayCollect), ink, ButtonStyle.Prominent,
            enabled: interactive, id: AwayButtonId)
            ? SheetAction.CollectAway
            : SheetAction.None;
    }

    private void FillStats(KupoWorkshop workshop, long now)
    {
        statValues[0] = KupoFormat.Amount(workshop.Kupo);
        statValues[1] = KupoFormat.Amount(workshop.LedgerKupo);
        statValues[2] = KupoFormat.Amount(workshop.LifetimeKupo);
        statValues[3] = KupoFormat.Rate(workshop.KupoPerSecond(now));
        statValues[4] = KupoFormat.Rate(workshop.TapValue(now));
        statValues[5] = KupoFormat.Amount(workshop.Taps);
        statValues[6] = KupoFormat.Amount(workshop.TapKupo);
        statValues[7] = GameNumber.Label(workshop.BuildingCount);
        statValues[8] = upgradesValue.Get(L.MoogleClicker.CountOf, GameNumber.Label(workshop.UpgradeCount),
            GameNumber.Label(KupoUpgrades.Count));
        statValues[9] = GameNumber.Label(workshop.MinionsCaught);
        statValues[10] = GameNumber.Label(workshop.LedgerLevel);
        statValues[11] = GameNumber.Label(workshop.LedgerPages);
        statValues[12] = bonusValue.Get(L.MoogleClicker.BonusPercent,
            GameNumber.Label(KupoLedger.BonusPercent(workshop.Stamps)));
    }

    private static float CardWidth(Rect full, float scale) =>
        MathF.Max(1f, MathF.Min(full.Width - CardMargin * 2f * scale, CardMaxWidth * scale));

    private static Rect Card(Rect full, float contentHeight, float slide, float scale, out Rect inner)
    {
        var padding = Metrics.Space.Xl * scale;
        var width = CardWidth(full, scale);
        var height = contentHeight + padding * 2f;
        var top = full.Min.Y + StageLayout.ChromeBand * scale;
        var center = new Vector2(full.Center.X, MathF.Max(top + height * 0.5f, full.Center.Y) + slide);
        var card = new Rect(center - new Vector2(width, height) * 0.5f, center + new Vector2(width, height) * 0.5f);
        inner = new Rect(card.Min + new Vector2(padding, padding), card.Max - new Vector2(padding, padding));
        return card;
    }

    private static void DrawCardBack(ImDrawListPtr drawList, Rect card, Vector4 accent, float alpha, float scale)
    {
        var radius = CardRadius * scale;
        Elevation.Floating(drawList, card.Min, card.Max, radius, scale, alpha);
        Material.Frosted(drawList, card.Min, card.Max, radius, scale, alpha);
        Squircle.Stroke(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(accent with { W = 0.28f * alpha }),
            1f * scale);
    }

    private static void DrawEmblem(ImDrawListPtr drawList, Vector2 center, FontAwesomeIcon icon, Vector4 tint,
        bool pulse, float alpha, float scale)
    {
        var radius = EmblemRadius * scale;
        var glow = pulse ? 0.55f + 0.35f * Pulse.Wave(Pulse.Medium) : 0.45f;
        ProgressRing.Glow(center, radius * 1.5f, tint, glow * alpha);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(tint with { W = alpha }), 36);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.3f, radius * 0.34f), radius * 0.42f,
            ImGui.GetColorU32(White with { W = 0.18f * alpha }), 20);
        ProgressRing.CenterIcon(drawList, center, icon, GamePalette.InkOn(tint) with { W = alpha }, radius * 0.9f);
    }

    private static Rect Centered(float centerX, float top, float width, float height) =>
        new(new Vector2(centerX - width * 0.5f, top), new Vector2(centerX + width * 0.5f, top + height));

    private static bool DismissedOutside(Rect card, bool interactive) =>
        interactive && UiInteract.ClickedOutside(card.Min, card.Max);
}
