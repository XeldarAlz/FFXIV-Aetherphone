using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Coin;

internal sealed partial class CoinApp
{
    private const float EntryTile = 64f;
    private const float EntryHeroGap = 10f;
    private const float DetailRowHeight = 46f;

    private void DrawEntry(in PhoneContext context, CoinRoute route, int depth)
    {
        var navBar = AppHeader.BeginLargeTitle(context);
        ledgerText.Sync(store.Entries);
        var index = ledgerText.Find(route.ItemId);
        var title = index >= 0 ? ledgerText.Title(index) : Loc.T(L.Coin.TabHistory);
        using (UiScope.PushId("coin.entry"))
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            if (index < 0)
            {
                CoinArt.StateScreen(ImGui.GetWindowDrawList(), ui, navBar.Body, FontAwesomeIcon.Receipt,
                    Loc.T(L.Coin.EntryMissingTitle), Loc.T(L.Coin.EntryMissingHint), string.Empty, 0u,
                    scale);
            }
            else
            {
                DrawEntryBody(ledgerText.Entries[index], index, scale);
            }
        }

        AppHeader.EndLargeTitle(in navBar, context, "coin.entry.nav", title, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty, BackTitle(depth), back);
    }

    private void DrawEntryBody(CoinLedgerEntryDto entry, int index, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = Metrics.Space.Lg * scale;
        var tile = EntryTile * scale;
        var amountStyle = TextStyles.WidgetDisplay;
        var amountHeight = Typography.LineHeight(amountStyle);
        var titleHeight = Typography.LineHeight(TextStyles.Title3);
        var stampHeight = Typography.LineHeight(TextStyles.Subheadline);
        var gap = EntryHeroGap * scale;
        var heroHeight = pad + tile + gap + amountHeight + gap + titleHeight + CoinArt.LineGap * scale + stampHeight +
                         pad;
        var heroMax = new Vector2(origin.X + width, origin.Y + heroHeight);
        CoinArt.Card(drawList, ui, origin, heroMax, scale);
        var centerX = origin.X + width * 0.5f;
        var appId = entry.App.Length == 0 ? Id : entry.App;
        IconTile.DrawApp(drawList, appId, new Vector2(centerX, origin.Y + pad + tile * 0.5f), tile,
            IconTile.Surface(AppAccents.For(appId)));
        var cursorY = origin.Y + pad + tile + gap;
        var amount = ledgerText.Amount(index);
        var fitted = WidgetText.FitStyle(amount, amountStyle, width - pad * 2f, true);
        var amountWidth = CoinArt.PriceWidth(amount, fitted);
        CoinArt.Price(drawList, new Vector2(centerX - amountWidth * 0.5f, cursorY), amount,
            entry.Amount > 0 ? CoinArt.GainInk : ui.TitleInk, fitted);
        cursorY += amountHeight + gap;
        var label = Typography.FitText(ledgerText.Title(index), width - pad * 2f, TextStyles.Title3);
        Typography.DrawCentered(drawList, new Vector2(centerX, cursorY + titleHeight * 0.5f), label, ui.TitleInk,
            TextStyles.Title3);
        cursorY += titleHeight + CoinArt.LineGap * scale;
        var stamp = Typography.FitText(ledgerText.Stamp(index), width - pad * 2f, TextStyles.Subheadline);
        Typography.DrawCentered(drawList, new Vector2(centerX, cursorY + stampHeight * 0.5f), stamp, ui.MutedInk,
            TextStyles.Subheadline);

        cursorY = heroMax.Y + Metrics.Space.Md * scale;
        var rowHeight = DetailRowHeight * scale;
        var detailsMin = new Vector2(origin.X, cursorY);
        var detailsMax = new Vector2(origin.X + width, cursorY + rowHeight * 2f);
        CoinArt.Card(drawList, ui, detailsMin, detailsMax, scale);
        DetailRow(drawList, detailsMin.Y, origin.X, width, Loc.T(L.Coin.DetailType),
            Loc.T(entry.Amount >= 0 ? L.Coin.FilterEarned : L.Coin.FilterSpent), false, scale);
        CoinArt.Hairline(drawList, ui, origin.X + pad, origin.X + width - pad, detailsMin.Y + rowHeight);
        DetailRow(drawList, detailsMin.Y + rowHeight, origin.X, width, Loc.T(L.Coin.BalanceAfter),
            NumberText.Group(entry.BalanceAfter), true, scale);
        cursorY = detailsMax.Y;

        if (CoinRuleLabels.TryHint(entry.RuleId, out var hint))
        {
            cursorY += CoinArt.SectionGap * scale;
            cursorY += CoinArt.SectionHeader(drawList, new Vector2(origin.X, cursorY), width,
                Loc.T(L.Coin.HowItWorks), ui.TitleInk, 0f, scale) + CoinArt.HeaderGap * scale;
            var text = Loc.T(hint);
            var textHeight = Typography.MeasureWrappedBlock(text, TextStyles.Body, width - pad * 2f).Y;
            var cardMax = new Vector2(origin.X + width, cursorY + textHeight + pad * 2f);
            CoinArt.Card(drawList, ui, new Vector2(origin.X, cursorY), cardMax, scale);
            Typography.DrawWrappedLeft(new Vector2(origin.X + pad, cursorY + pad), text, ui.TitleInk,
                TextStyles.Body, width - pad * 2f);
            cursorY = cardMax.Y;
        }

        if (!string.Equals(appId, Id, StringComparison.Ordinal) && navigation.IsAvailable(appId))
        {
            cursorY += Metrics.Space.Xl * scale;
            var buttonHeight = CoinArt.LargeCapsuleHeight * scale;
            var rect = new Rect(new Vector2(origin.X, cursorY), new Vector2(origin.X + width, cursorY + buttonHeight));
            if (CoinArt.Capsule(drawList, ui, ImGui.GetID("coin.entry.open"), rect, Loc.T(L.Coin.OpenSource), CapsuleTone.Tinted,
                    true))
            {
                UiFeedback.Play(UiSound.Tap);
                navigation.Open(appId);
            }

            cursorY = rect.Max.Y;
        }

        CoinArt.Reserve(origin, width, cursorY + CoinArt.BottomPad * scale);
    }

    private void DetailRow(ImDrawListPtr drawList, float top, float left, float width, string label, string value,
        bool coin, float scale)
    {
        var pad = Metrics.Space.Lg * scale;
        var height = DetailRowHeight * scale;
        var lineHeight = Typography.LineHeight(TextStyles.Body);
        var textTop = top + (height - lineHeight) * 0.5f;
        var valueWidth = coin ? CoinArt.PriceWidth(value, TextStyles.Body) : Typography.Measure(value, TextStyles.Body).X;
        var valueLeft = left + width - pad - valueWidth;
        if (coin)
        {
            CoinArt.Price(drawList, new Vector2(valueLeft, textTop), value, ui.MutedInk, TextStyles.Body);
        }
        else
        {
            Typography.Draw(drawList, new Vector2(valueLeft, textTop), value, ui.MutedInk, TextStyles.Body);
        }

        Typography.Draw(drawList, new Vector2(left + pad, textTop),
            Typography.FitText(label, MathF.Max(1f, valueLeft - CoinArt.ValueGap * scale - left - pad), TextStyles.Body),
            ui.TitleInk, TextStyles.Body);
    }
}
