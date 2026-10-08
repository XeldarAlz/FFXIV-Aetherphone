using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Coin;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp
{
    private const string CoinAppId = "coin";
    private const int EarnRowsShown = 4;
    private const float ExchangePad = 18f;
    private const float ExchangeDisc = 40f;
    private const float ExchangeArrow = 30f;
    private const float ExchangeButtonHeight = Button.LargeHeight;
    private const float ExchangeRowGap = 14f;
    private const float ExchangeArrowAlpha = 0.10f;
    private const float LevelCardPad = 16f;
    private const float CashOutCardPad = 16f;
    private const float NotePad = 12f;

    private LevelCapsule cashierLevel;

    private void DrawCashierTab(Rect body)
    {
        var scale = UiScale.Current;
        using (ImRaii.PushId("casino.cashier"))
        using (AppSurface.Begin(body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = DrawStakeNotice(origin, width, scale);
            cursorY = DrawExchange(drawList, new Vector2(origin.X, cursorY), width, scale);
            var state = casino.State;
            if (state?.Progress is not null)
            {
                cursorY = DrawLevelCard(drawList, new Vector2(origin.X, cursorY + CardGap * scale), width, scale);
            }

            if (state?.Sitting is { Stack: > 0 })
            {
                cursorY = DrawCashOutCard(drawList, new Vector2(origin.X, cursorY), width, scale);
            }

            if (casino.HasFeature(CasinoFeatures.Bonus) && casino.Bonuses.Length > 0)
            {
                cursorY = DrawBonusSection(drawList, new Vector2(origin.X, cursorY), width, scale);
            }

            if (casino.HasFeature(CasinoFeatures.Club) && casino.Club is { } club)
            {
                var clubTop = SectionTitle(drawList, new Vector2(origin.X, cursorY), width,
                    Loc.T(L.Strip.ClubHeading), scale);
                var clubMin = new Vector2(origin.X, clubTop);
                var clubMax = new Vector2(origin.X + width, clubTop + clubCard.Height(scale));
                var overClub = UiInteract.Hover(clubMin, clubMax);
                cursorY = clubCard.Draw(drawList, ui, club, origin.X, clubTop, width, scale);
                if (overClub)
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }

                if (UiInteract.Click(clubMin, clubMax, overClub))
                {
                    clubSheet.Open(club.Tier);
                }
            }

            if (state is not null)
            {
                var tonight = CasinoTonight.From(state);
                cursorY = tonight.Reached
                    ? DrawLimitReached(drawList, new Vector2(origin.X, cursorY + CardGap * scale), width, scale)
                    : DrawTonight(drawList, new Vector2(origin.X, cursorY + CardGap * scale), width, tonight, scale);
            }

            var wallet = coins.Wallet;
            if (wallet is { Rules.Length: > 0 })
            {
                var earnTop = SectionTitle(drawList, new Vector2(origin.X, cursorY), width,
                    Loc.T(L.Coin.EarnHeader), scale);
                CoinArt.Reserve(origin, width, earnTop);
                DrawEarnPreview(wallet);
                cursorY = ImGui.GetCursorScreenPos().Y;
            }

            if (navigation.IsAvailable(CoinAppId))
            {
                cursorY = DrawWalletLink(drawList, new Vector2(origin.X, cursorY + CardGap * scale), width, scale);
            }

            cursorY = DrawRecordsCard(drawList, new Vector2(origin.X, cursorY), width, scale);
            if (!cashier.IsOpen)
            {
                bonusShelf.DrawShower(drawList, scale);
            }

            CoinArt.Reserve(origin, width, cursorY + CoinArt.BottomPad * scale);
        }
    }

    private float DrawLevelCard(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var progress = casino.Progress!;
        var ceiling = casino.Ceiling;
        var pad = LevelCardPad * scale;
        var capsuleHeight = LevelCapsule.Height * scale;
        var lineHeight = Typography.LineHeight(TextStyles.Footnote);
        var height = pad * 2f + capsuleHeight + Metrics.Space.Sm * scale + lineHeight;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale);

        var balance = (casino.State?.Sitting?.Stack ?? 0) + (casino.State?.TableSitting?.Stack ?? 0);
        var title = StatusTitle.For(balance);
        var titleReserve = title == BalanceTitle.None ? 0f : StatusTitle.Width(title, width * 0.35f, scale);
        var capsuleRect = new Rect(new Vector2(min.X + pad, min.Y + pad),
            new Vector2(max.X - pad - (titleReserve > 0f ? titleReserve + Metrics.Space.Sm * scale : 0f),
                min.Y + pad + capsuleHeight));
        var span = progress.NextLevelXp - progress.LevelStartXp;
        var fraction = span <= 0 ? 1f : Math.Clamp((float)(progress.Xp - progress.LevelStartXp) / span, 0f, 1f);
        cashierLevel.Draw(drawList, capsuleRect, Math.Max(1, progress.Level), fraction, ceiling.LevelCap, ui.Accent,
            scale);
        if (titleReserve > 0f)
        {
            StatusTitle.Draw(drawList, new Vector2(max.X - pad - titleReserve * 0.5f, capsuleRect.Center.Y), title,
                width * 0.35f, scale);
        }

        var maxBet = texts.Compact(L.Strip.MaxBetLine, ceiling.MaxBet);
        Typography.Draw(drawList, new Vector2(min.X + pad, capsuleRect.Max.Y + Metrics.Space.Sm * scale),
            Typography.FitText(maxBet, width - pad * 2f, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        return max.Y;
    }

    private float DrawCashOutCard(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var top = SectionTitle(drawList, origin, width, Loc.T(L.Casino.CashOut), scale);
        var split = CashOutSplit.Of(casino.State);
        var pad = CashOutCardPad * scale;
        var inner = width - pad * 2f;
        var height = cashierCashOut.Height(split, inner, scale) + pad * 2f;
        var min = new Vector2(origin.X, top);
        var max = new Vector2(origin.X + width, top + height);
        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale);
        cashierCashOut.Draw(drawList, ui, split, min.X + pad, min.Y + pad, inner, scale, false,
            !casino.MovingMoney);
        return max.Y;
    }

    private float DrawBonusSection(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var top = SectionTitle(drawList, origin, width, Loc.T(L.Strip.FreeChips), scale);
        if (bonusShelf.Note.Length > 0 && !cashier.IsOpen)
        {
            var pad = NotePad * scale;
            var block = Typography.MeasureWrappedBlock(bonusShelf.Note, TextStyles.Footnote, width - pad * 2f);
            var noteMax = new Vector2(origin.X + width, top + block.Y + pad * 2f);
            var tint = bonusShelf.NoteIsGrant ? CasinoColors.Money : ui.Accent;
            Squircle.Fill(drawList, new Vector2(origin.X, top), noteMax, Metrics.Radius.Grouped * scale,
                ImGui.GetColorU32(Palette.WithAlpha(tint, 0.10f)));
            Typography.DrawWrappedLeft(new Vector2(origin.X + pad, top + pad), bonusShelf.Note, ui.TitleInk,
                TextStyles.Footnote, width - pad * 2f);
            top = noteMax.Y + Metrics.Space.Sm * scale;
        }

        return bonusShelf.Draw(drawList, ui, origin.X, top, width, scale, false, false, true);
    }

    private float DrawExchange(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var pad = ExchangePad * scale;
        var disc = ExchangeDisc * scale;
        var caption = Typography.LineHeight(TextStyles.Footnote);
        var value = Typography.LineHeight(TextStyles.Title3);
        var buttonHeight = ExchangeButtonHeight * scale;
        var rowGap = ExchangeRowGap * scale;
        var height = pad * 2f + disc + rowGap * 0.5f + caption + value + rowGap + caption + rowGap + buttonHeight;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale);

        var stack = casino.State?.Sitting?.Stack ?? 0;
        var balance = coins.Wallet?.Balance ?? 0;
        var columnWidth = (width - pad * 2f - ExchangeArrow * scale) * 0.5f;
        var leftCenter = min.X + pad + columnWidth * 0.5f;
        var rightCenter = max.X - pad - columnWidth * 0.5f;
        var discY = min.Y + pad + disc * 0.5f;
        DrawExchangeColumn(drawList, CurrencyKind.Coins, new Vector2(leftCenter, discY), columnWidth,
            Loc.T(L.Casino.WalletRow), NumberText.Group(balance), ui.TitleInk, scale);
        DrawExchangeColumn(drawList, CurrencyKind.Chips, new Vector2(rightCenter, discY), columnWidth,
            Loc.T(L.Casino.ChipsRow), NumberText.Group(stack), stack > 0 ? CasinoColors.Money : ui.MutedInk, scale);

        var arrowCenter = new Vector2(min.X + width * 0.5f, discY);
        var arrowRadius = ExchangeArrow * scale * 0.5f;
        drawList.AddCircleFilled(arrowCenter, arrowRadius,
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, ExchangeArrowAlpha)), 28);
        AppSkin.Icon(drawList, arrowCenter, IconGlyph.Of(FontAwesomeIcon.ExchangeAlt), ui.BodyInk, 0.7f);

        var rateTop = discY + disc * 0.5f + rowGap * 0.5f + caption + value + rowGap;
        DrawRateEquation(drawList, new Vector2(min.X + width * 0.5f, rateTop));

        var buttonTop = rateTop + caption + rowGap;
        var seated = stack > 0 && casino.State?.Sitting is not null;
        var busy = casino.MovingMoney;
        var gap = CardGap * scale;
        var buttonWidth = (width - pad * 2f - gap) * 0.5f;
        var buyRect = new Rect(new Vector2(min.X + pad, buttonTop),
            new Vector2(min.X + pad + buttonWidth, buttonTop + buttonHeight));
        if (Button.Draw(drawList, buyRect, Loc.T(L.Strip.GetChips), ui.Ink, ButtonStyle.Prominent, enabled: !busy,
                id: "casino.wallet.buy"))
        {
            cashier.Open();
        }

        var cashRect = new Rect(new Vector2(max.X - pad - buttonWidth, buttonTop),
            new Vector2(max.X - pad, buttonTop + buttonHeight));
        if (Button.Draw(drawList, cashRect, Loc.T(L.Casino.CashOut), ui.Ink, ButtonStyle.Tinted,
                enabled: seated && !busy, id: "casino.wallet.cash"))
        {
            cashierCashOut.Ask(CashOutSplit.Of(casino.State));
        }

        return max.Y;
    }

    private void DrawExchangeColumn(ImDrawListPtr drawList, CurrencyKind kind, Vector2 discCenter, float width,
        string label, string amount, Vector4 ink, float scale)
    {
        var disc = ExchangeDisc * scale;
        CurrencyGlyph.Draw(drawList, kind, discCenter, disc);
        var captionTop = discCenter.Y + disc * 0.5f + ExchangeRowGap * 0.5f * scale;
        Typography.DrawCentered(drawList,
            new Vector2(discCenter.X, captionTop + Typography.LineHeight(TextStyles.Footnote) * 0.5f),
            Typography.FitText(label, width, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        var fitted = Typography.FitText(amount, width, TextStyles.Title3);
        var size = Typography.Measure(fitted, TextStyles.Title3);
        Typography.Draw(drawList,
            new Vector2(discCenter.X - size.X * 0.5f, captionTop + Typography.LineHeight(TextStyles.Footnote)), fitted,
            ink, TextStyles.Title3);
    }

    private void DrawRateEquation(ImDrawListPtr drawList, Vector2 topCenter)
    {
        var chipsText = NumberText.Group(casino.Rate);
        var coinsText = NumberText.Group(1L);
        const string equalsText = " = ";
        var chipsSize = CurrencyGlyph.MeasureAmount(chipsText, TextStyles.Footnote);
        var equalsSize = Typography.Measure(equalsText, TextStyles.Footnote);
        var coinsSize = CurrencyGlyph.MeasureAmount(coinsText, TextStyles.Footnote);
        var x = topCenter.X - (chipsSize.X + equalsSize.X + coinsSize.X) * 0.5f;
        x += CurrencyGlyph.DrawAmount(drawList, new Vector2(x, topCenter.Y), chipsText, CurrencyKind.Chips,
            ui.MutedInk, TextStyles.Footnote).X;
        Typography.Draw(drawList, new Vector2(x, topCenter.Y), equalsText, ui.MutedInk, TextStyles.Footnote);
        x += equalsSize.X;
        CurrencyGlyph.DrawAmount(drawList, new Vector2(x, topCenter.Y), coinsText, CurrencyKind.Coins, ui.MutedInk,
            TextStyles.Footnote);
    }

    private void DrawEarnPreview(Core.Aethernet.Contracts.CoinWalletDto wallet)
    {
        var rules = wallet.Rules!;
        var shown = 0;
        for (var index = 0; index < rules.Length && shown < EarnRowsShown; index++)
        {
            if (CoinGoals.IsComplete(rules[index]))
            {
                continue;
            }

            CoinEarnRow.Draw(rules[index], ui.Palette);
            shown++;
        }

        for (var index = 0; index < rules.Length && shown == 0 && index < EarnRowsShown; index++)
        {
            CoinEarnRow.Draw(rules[index], ui.Palette);
        }
    }

    private float DrawWalletLink(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var rowHeight = RecordRowHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + rowHeight);
        var row = new Rect(origin, max);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var hovered = CoinArt.RowInteraction(drawList, ui, row, scale);
        var pad = Metrics.Space.Lg * scale;
        var tile = RecordTile * scale;
        var tileCenter = new Vector2(origin.X + pad + tile * 0.5f, row.Center.Y);
        IconTile.DrawApp(drawList, CoinAppId, tileCenter, tile, IconTile.Surface(AppAccents.For(CoinAppId)));
        var chevronCenter = new Vector2(max.X - pad - 4f * scale, row.Center.Y);
        CasinoArt.Chevron(drawList, chevronCenter, ui.MutedInk);
        CoinArt.Labels(drawList, tileCenter.X + tile * 0.5f + CoinArt.TextGap * scale,
            chevronCenter.X - CoinArt.ValueGap * scale, row.Center.Y, Loc.T(L.Casino.OpenWalletRow),
            Loc.T(L.Casino.OpenWalletRowHint), ui.TitleInk, ui.MutedInk, scale);
        if (UiInteract.Click(origin, max, hovered))
        {
            navigation.Open(CoinAppId);
        }

        return max.Y;
    }
}
