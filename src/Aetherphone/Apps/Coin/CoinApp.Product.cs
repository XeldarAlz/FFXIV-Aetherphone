using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Coin;

internal sealed partial class CoinApp
{
    private const float ProductStageHeight = 220f;
    private const float ProductStageInset = 10f;
    private const float ProductBarHeight = 6f;
    private const float ProductBarTrackAlpha = 0.14f;
    private const int FrameSlot = 1;

    private void DrawProduct(in PhoneContext context, CoinRoute route, int depth)
    {
        catalog.EnsureShelf(route.CategoryId);
        badgeCatalog.EnsureFresh();
        frameCatalog.EnsureFresh();
        inventory.EnsureFresh();
        var sku = FindSku(route.CategoryId, route.ItemId);
        var navBar = AppHeader.BeginLargeTitle(context);
        using (UiScope.PushId("coin.product"))
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            if (sku is not null)
            {
                DrawProductBody(sku, route.CategoryId, scale);
            }
            else if (!catalog.ShelfLoaded(route.CategoryId) && !catalog.ItemsComplete)
            {
                LoadingPulse.Draw(navBar.Body.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk,
                    LoadingPulse.SafeLabel());
            }
            else
            {
                ClearGoalFor(route.ItemId);
                CoinArt.StateScreen(ImGui.GetWindowDrawList(), ui, navBar.Body, FontAwesomeIcon.Store,
                    Loc.T(L.Coin.Unavailable), Loc.T(L.Coin.ProductGoneHint), string.Empty, 0u, scale);
            }
        }

        var title = sku?.Name ?? Loc.T(L.Coin.TabShop);
        AppHeader.EndLargeTitle(in navBar, context, "coin.product.nav", title, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty, BackTitle(depth), back);
    }

    private void DrawProductBody(CoinSkuStyle sku, string categoryId, float scale)
    {
        SyncGoal(sku);
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var stageMax = new Vector2(origin.X + width, origin.Y + ProductStageHeight * scale);
        CoinArt.Card(drawList, ui, origin, stageMax, scale);
        var inset = ProductStageInset * scale;
        DrawItemStage(drawList, new Rect(origin + new Vector2(inset, inset), stageMax - new Vector2(inset, inset)),
            sku.Kind, sku.Payload, scale);

        var cursorY = stageMax.Y + Metrics.Space.Lg * scale;
        cursorY += Typography.DrawWrappedLeft(new Vector2(origin.X, cursorY), sku.Name, ui.TitleInk, TextStyles.Title2,
            width);
        cursorY += CoinArt.LineGap * scale;
        Typography.Draw(drawList, new Vector2(origin.X, cursorY),
            Typography.FitText(Loc.T(KindLabel(sku.Kind)), width, TextStyles.Subheadline), ui.MutedInk,
            TextStyles.Subheadline);
        cursorY += Typography.LineHeight(TextStyles.Subheadline);
        if (sku.AvailableUntilUnix is { } leaving && !sku.Owned)
        {
            cursorY += CoinArt.LineGap * scale;
            Typography.Draw(drawList, new Vector2(origin.X, cursorY),
                Typography.FitText(LeavingText(leaving), width, TextStyles.FootnoteEmphasized), ui.Accent,
                TextStyles.FootnoteEmphasized);
            cursorY += Typography.LineHeight(TextStyles.FootnoteEmphasized);
        }

        cursorY += Metrics.Space.Xl * scale;
        cursorY = sku.Owned
            ? DrawOwnedActions(drawList, new Vector2(origin.X, cursorY), width, sku, scale)
            : DrawBuyActions(drawList, new Vector2(origin.X, cursorY), width, sku, categoryId, scale);
        CoinArt.Reserve(origin, width, cursorY + CoinArt.BottomPad * scale);
    }

    private static LocString KindLabel(string kind)
    {
        if (string.Equals(kind, FrameKind, StringComparison.Ordinal))
        {
            return L.Coin.KindFrame;
        }

        return string.Equals(kind, FlairKind, StringComparison.Ordinal) ? L.Coin.KindFlair : L.Coin.KindOther;
    }

    private float DrawOwnedActions(ImDrawListPtr drawList, Vector2 origin, float width, CoinSkuStyle sku, float scale)
    {
        var height = CoinArt.LargeCapsuleHeight * scale;
        var rect = MotionButton.FitHaloWidth(new Rect(origin, new Vector2(origin.X + width, origin.Y + height)), scale);
        var item = FindOwnedItem(sku.Kind, sku.Payload);
        if (item is null)
        {
            CoinArt.Capsule(drawList, ui, ImGui.GetID("coin.product.owned"), rect, Loc.T(L.Coin.Owned),
                CapsuleTone.Quiet, false);
            return rect.Max.Y;
        }

        var worn = item.Slot > 0;
        var label = worn ? Loc.T(L.Coin.Wearing) : Loc.T(L.Loadout.Wear);
        var tone = worn ? CapsuleTone.Quiet : CapsuleTone.Filled;
        if (CoinArt.Capsule(drawList, ui, ImGui.GetID("coin.product.wear"), rect, label, tone,
                !worn && !inventory.Equipping))
        {
            UiFeedback.Play(UiSound.ToggleOn);
            var slot = string.Equals(item.Kind, LoadoutStore.FrameKind, StringComparison.Ordinal) ? FrameSlot : (int?)null;
            inventory.Equip(item.Kind, item.Id, slot);
        }

        return rect.Max.Y;
    }

    private float DrawBuyActions(ImDrawListPtr drawList, Vector2 origin, float width, CoinSkuStyle sku,
        string categoryId, float scale)
    {
        var wallet = store.Wallet;
        var balance = wallet?.Balance ?? 0L;
        var affordable = balance >= sku.Price;
        var frozen = wallet?.FrozenUntilUnix is not null;
        var height = CoinArt.LargeCapsuleHeight * scale;
        var rect = MotionButton.FitHaloWidth(new Rect(origin, new Vector2(origin.X + width, origin.Y + height)), scale);
        if (CoinArt.Capsule(drawList, ui, ImGui.GetID("coin.product.buy"), rect, texts.BuyFor(sku.Price),
                CapsuleTone.Filled, affordable && !frozen && !store.Purchasing && wallet is not null))
        {
            AskPurchase(sku);
        }

        var cursorY = rect.Max.Y;
        if (affordable || wallet is null)
        {
            return cursorY;
        }

        cursorY += Metrics.Space.Md * scale;
        var pad = Metrics.Space.Lg * scale;
        var lineHeight = Typography.LineHeight(TextStyles.Headline);
        var cardHeight = pad + lineHeight + Metrics.Space.Md * scale + ProductBarHeight * scale + pad;
        var cardMin = new Vector2(origin.X, cursorY);
        var cardMax = new Vector2(origin.X + width, cursorY + cardHeight);
        CoinArt.Card(drawList, ui, cardMin, cardMax, scale);
        var balanceText = BalanceText(balance);
        var balanceWidth = CoinArt.PriceWidth(balanceText, TextStyles.Headline);
        CoinArt.Price(drawList, new Vector2(cardMax.X - pad - balanceWidth, cardMin.Y + pad), balanceText, ui.MutedInk,
            TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(cardMin.X + pad, cardMin.Y + pad),
            Typography.FitText(texts.Remaining(CoinSavingGoal.Remaining(balance, sku.Price)),
                MathF.Max(1f, width - pad * 3f - balanceWidth), TextStyles.Headline), ui.TitleInk,
            TextStyles.Headline);
        var barTop = cardMin.Y + pad + lineHeight + Metrics.Space.Md * scale;
        CoinArt.Bar(drawList, new Vector2(cardMin.X + pad, barTop),
            new Vector2(cardMax.X - pad, barTop + ProductBarHeight * scale), CoinSavingGoal.Fraction(balance, sku.Price),
            Palette.WithAlpha(ui.TitleInk, ProductBarTrackAlpha), ui.Accent);
        cursorY = cardMax.Y + Metrics.Space.Md * scale;

        var saving = IsGoal(sku.Id);
        var saveRect = MotionButton.FitHaloWidth(
            new Rect(new Vector2(origin.X, cursorY), new Vector2(origin.X + width, cursorY + height)), scale);
        if (CoinArt.Capsule(drawList, ui, ImGui.GetID("coin.product.save"), saveRect,
                Loc.T(saving ? L.Coin.SavingForThis : L.Coin.SaveForThis), saving ? CapsuleTone.Quiet : CapsuleTone.Tinted,
                true))
        {
            if (saving)
            {
                UiFeedback.Play(UiSound.ToggleOff);
                ClearGoalFor(sku.Id);
            }
            else
            {
                UiFeedback.Play(UiSound.ToggleOn);
                SetGoal(sku, categoryId);
            }
        }

        return saveRect.Max.Y;
    }

    private InventoryItemDto? FindOwnedItem(string kind, string payload)
    {
        var section = inventory.Section(kind);
        if (section is null)
        {
            return null;
        }

        for (var index = 0; index < section.Items.Length; index++)
        {
            var item = section.Items[index];
            if (string.Equals(item.Id, payload, StringComparison.Ordinal) ||
                string.Equals(item.Frame?.Id, payload, StringComparison.Ordinal) ||
                string.Equals(item.Badge?.Id, payload, StringComparison.Ordinal))
            {
                return item;
            }
        }

        return null;
    }
}
