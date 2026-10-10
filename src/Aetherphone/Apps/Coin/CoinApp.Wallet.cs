using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Conduct;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Coin;

internal sealed partial class CoinApp
{
    private const int RecentCount = 4;
    private const int HelpButton = 0;
    private const int RulesButton = 1;
    private const string CasinoAppId = "casino";
    private const float BalanceAspect = 0.56f;
    private const float BalancePad = 20f;
    private const float BalanceLuminance = 0.30f;
    private const float BalanceGlassOpacity = 0.94f;
    private const float BalanceIconSize = 30f;
    private const float BalanceIconGap = 10f;
    private const float BalanceLineGap = 4f;
    private const float BalanceSubAlpha = 0.82f;
    private const float BalanceIconWashAlpha = 0.22f;
    private const float WatermarkFraction = 0.92f;
    private const float WatermarkAlpha = 0.10f;
    private const float WatermarkInset = 0.20f;
    private const string CoinIconId = "coin";

    private RollingValue balanceRoll;
    private Vector2 checkInAnchor;
    private CachedText lifetimeText;

    private void PrimeWallet()
    {
        balanceRoll = default;
        PrimeToday();
        PrimeQuests();
    }

    private void DrawWallet(in PhoneContext context)
    {
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (ImRaii.PushId("coin.wallet"))
        using (var surface = AppSurface.Begin(navBar.Body))
        {
            var wallet = store.Wallet;
            walletRefresh.Draw(navBar.Body, surface.Pull, surface.Dragging, wallet is null, ui.MutedInk,
                store.RefreshNow);
            ConsumeCheckIn();
            ConsumeQuestClaim();
            if (wallet is null)
            {
                TourHolds.Hold(Id);
                LoadingPulse.Draw(navBar.Body.Center, 16f * UiScale.Current, ui.Palette.Accent, ui.MutedInk,
                    LoadingPulse.SafeLabel());
            }
            else
            {
                DrawWalletBody(wallet);
            }
        }

        var count = NavButton(0, PhoneIcons.InfoCircle, Loc.T(L.Coin.HelpTitle));
        count = NavButton(count, PhoneIcons.ShieldCheck, Loc.T(L.Conduct.Eyebrow));
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "coin.wallet.nav", DisplayName,
            NavBarStyle.From(ui), navButtons.AsSpan(0, count));
        if (pressed == HelpButton)
        {
            confirm.Alert(Loc.T(L.Coin.HelpTitle), Loc.T(L.Coin.HelpBody), Loc.T(L.Onboarding.GotIt));
        }
        else if (pressed == RulesButton)
        {
            conduct.ShowRules(ConductRules.Coin.AppId);
        }
    }

    private void ConsumeCheckIn()
    {
        var result = store.TakeCheckInResult();
        if (result is not { Granted: true, Amount: > 0 })
        {
            return;
        }

        UiFeedback.Play(UiSound.Payout);
        floats.Spawn(Loc.T(L.Coin.CheckInReward, NumberText.Group(result.Amount)), checkInAnchor);
    }

    private void DrawWalletBody(CoinWalletDto wallet)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var frozen = wallet.FrozenUntilUnix is not null;
        var cursorY = origin.Y;
        if (frozen)
        {
            cursorY = CoinArt.DrawPanel(ui, new Vector2(origin.X, cursorY), width, FontAwesomeIcon.Snowflake,
                ui.Accent, Loc.T(L.Coin.FrozenTitle), Loc.T(L.Coin.FrozenHint), scale) + Metrics.Space.Md * scale;
        }

        cursorY = DrawBalanceCard(drawList, new Vector2(origin.X, cursorY), width, wallet, scale);
        cursorY = DrawPurse(drawList, new Vector2(origin.X, cursorY), width, scale);
        cursorY = DrawCheckIn(new Vector2(origin.X, cursorY), width, wallet, frozen, scale);
        if (SeasonalTheme.Halloween)
        {
            cursorY = treatCard.Draw(ui, new Vector2(origin.X, cursorY + Metrics.Space.Md * scale), width,
                CharacterName());
        }

        if (wallet.Paused)
        {
            var panelTop = cursorY + Metrics.Space.Md * scale;
            cursorY = CoinArt.DrawPanel(ui, new Vector2(origin.X, panelTop), width, FontAwesomeIcon.Pause,
                ui.Accent, Loc.T(L.Coin.PausedTitle), Loc.T(L.Coin.PausedHint), scale);
            UiAnchors.Report("coin.today", new Rect(new Vector2(origin.X, panelTop), new Vector2(origin.X + width,
                cursorY)));
        }
        else
        {
            cursorY = DrawToday(drawList, new Vector2(origin.X, cursorY), width, wallet, scale);
        }

        cursorY = DrawQuests(drawList, new Vector2(origin.X, cursorY), width, scale);

        cursorY = DrawSavingGoal(drawList, new Vector2(origin.X, cursorY), width, wallet.Balance, scale);
        cursorY = DrawEarn(drawList, new Vector2(origin.X, cursorY), width, wallet, scale);
        cursorY = DrawRecent(drawList, new Vector2(origin.X, cursorY), width, scale);
        CoinArt.Reserve(origin, width, cursorY + CoinArt.BottomPad * scale);
    }

    private float DrawBalanceCard(ImDrawListPtr drawList, Vector2 origin, float width, CoinWalletDto wallet,
        float scale)
    {
        var pad = BalancePad * scale;
        var iconSize = BalanceIconSize * scale;
        var amountStyle = TextStyles.WidgetDisplay;
        var amountHeight = Typography.LineHeight(amountStyle);
        var summaryHeight = Typography.LineHeight(TextStyles.Subheadline);
        var natural = pad * 2f + iconSize + amountHeight + BalanceLineGap * scale + summaryHeight +
                      Metrics.Space.Lg * scale;
        var height = MathF.Max(width * BalanceAspect, natural);
        var restMax = new Vector2(origin.X + width, origin.Y + height);
        UiAnchors.Report("coin.balance", new Rect(origin, restMax));
        var hovered = UiInteract.Hover(origin, restMax);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale("coin.balance.card", pressed, Motion.PressScaleCard);
        var center = (origin + restMax) * 0.5f;
        var half = (restMax - origin) * 0.5f * press;
        var min = center - half;
        var max = center + half;
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var surface = Palette.ShadeToLuminance(ui.Accent with { W = 1f }, BalanceLuminance);
        var radius = Metrics.Radius.Grouped * scale;
        Material.AccentGlass(drawList, min, max, radius, scale, surface, BalanceGlassOpacity);
        DrawWatermark(drawList, min, max, height);

        var ink = CoinArt.White;
        var subInk = Palette.WithAlpha(ink, BalanceSubAlpha);
        var left = min.X + pad;
        var right = max.X - pad;
        var iconCenter = new Vector2(left + iconSize * 0.5f, min.Y + pad + iconSize * 0.5f);
        drawList.AddCircleFilled(iconCenter, iconSize * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(ink, BalanceIconWashAlpha)), 32);
        CurrencyGlyph.Draw(drawList, CurrencyKind.Coins, iconCenter, iconSize * 0.62f);
        var nameLeft = left + iconSize + BalanceIconGap * scale;
        var ownerWidth = 0f;
        var owner = CharacterName();
        if (owner.Length > 0)
        {
            var fittedOwner = Typography.FitText(owner, (right - nameLeft) * 0.5f, TextStyles.Footnote);
            var ownerSize = Typography.Measure(fittedOwner, TextStyles.Footnote);
            ownerWidth = ownerSize.X + BalanceIconGap * scale;
            Typography.Draw(drawList, new Vector2(right - ownerSize.X, iconCenter.Y - ownerSize.Y * 0.5f),
                fittedOwner, subInk, TextStyles.Footnote);
        }

        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(nameLeft, iconCenter.Y - nameHeight * 0.5f),
            Typography.FitText(Loc.T(L.Coin.Balance), MathF.Max(1f, right - ownerWidth - nameLeft),
                TextStyles.Headline), ink, TextStyles.Headline);

        var summaryTop = max.Y - pad - summaryHeight;
        Typography.Draw(drawList, new Vector2(left, summaryTop),
            Typography.FitText(LifetimeLine(wallet), right - left, TextStyles.Subheadline), subInk,
            TextStyles.Subheadline);
        var target = (int)Math.Clamp(wallet.Balance, 0, int.MaxValue);
        balanceRoll.Update(target, MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds));
        var amount = NumberText.Group(balanceRoll.Display);
        var fitted = WidgetText.FitStyle(amount, amountStyle, right - left, true);
        var fittedHeight = Typography.LineHeight(fitted);
        WidgetText.Tabular(drawList, new Vector2(left, summaryTop - BalanceLineGap * scale - fittedHeight), amount,
            ink, fitted);
        if (UiInteract.Click(origin, restMax, hovered))
        {
            UiFeedback.Play(UiSound.Tap);
            SelectTab(CoinTab.History);
        }

        return restMax.Y;
    }

    private static void DrawWatermark(ImDrawListPtr drawList, Vector2 min, Vector2 max, float height)
    {
        var size = height * WatermarkFraction;
        var center = new Vector2(max.X - height * WatermarkInset, min.Y + height * 0.58f);
        var tint = Palette.WithAlpha(CoinArt.White, WatermarkAlpha);
        drawList.PushClipRect(min, max, true);
        if (!AppIconTile.TryDrawGlyph(drawList, CoinIconId, center, size, tint))
        {
            ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.Coins, tint, size);
        }

        drawList.PopClipRect();
    }

    private string LifetimeLine(CoinWalletDto wallet)
    {
        var key = (wallet.LifetimeEarned << 1) ^ (wallet.LifetimeSpent << 33);
        if (lifetimeText.IsCurrent(key))
        {
            return lifetimeText.Value;
        }

        return lifetimeText.Store(key, wallet.LifetimeSpent > 0
            ? Loc.T(L.Coin.LifetimeSummary, NumberText.Group(wallet.LifetimeEarned),
                NumberText.Group(wallet.LifetimeSpent))
            : Loc.T(L.Coin.LifetimeEarnedOnly, NumberText.Group(wallet.LifetimeEarned)));
    }

    private string CharacterName()
    {
        var user = session.CurrentUser;
        if (user is null)
        {
            return string.Empty;
        }

        return user.DisplayName.Length > 0 ? user.DisplayName : user.Name;
    }

    private float DrawPurse(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var stack = casino.State?.Sitting?.Stack ?? 0;
        if (stack <= 0)
        {
            return origin.Y;
        }

        var top = origin.Y + Metrics.Space.Md * scale;
        var min = new Vector2(origin.X, top);
        var max = new Vector2(origin.X + width, top + CoinArt.RowHeight * scale);
        var row = new Rect(min, max);
        CoinArt.Card(drawList, ui, min, max, scale);
        var openable = navigation.IsAvailable(CasinoAppId);
        var hovered = openable && CoinArt.RowInteraction(drawList, ui, row, scale);
        var pad = Metrics.Space.Lg * scale;
        var tile = CoinArt.RowTile * scale;
        var tileCenter = new Vector2(min.X + pad + tile * 0.5f, row.Center.Y);
        IconTile.DrawApp(drawList, CasinoAppId, tileCenter, tile, IconTile.Surface(AppAccents.For(CasinoAppId)));
        var chips = NumberText.Group(stack);
        var chipSize = CurrencyGlyph.MeasureAmount(chips, TextStyles.Headline);
        CurrencyGlyph.DrawAmount(drawList, new Vector2(max.X - pad - chipSize.X, row.Center.Y - chipSize.Y * 0.5f),
            chips, CurrencyKind.Chips, ui.TitleInk, TextStyles.Headline);
        var textLeft = tileCenter.X + tile * 0.5f + CoinArt.TextGap * scale;
        CoinArt.Labels(drawList, textLeft, max.X - pad - chipSize.X - CoinArt.ValueGap * scale, row.Center.Y,
            Loc.T(L.Casino.PurseRow), Loc.T(L.Casino.PurseHint), ui.TitleInk, ui.MutedInk, scale);
        if (UiInteract.Click(min, max, hovered))
        {
            UiFeedback.Play(UiSound.Tap);
            navigation.Open(CasinoAppId);
        }

        return max.Y;
    }

    private float DrawCheckIn(Vector2 origin, float width, CoinWalletDto wallet, bool frozen, float scale)
    {
        var top = origin.Y + Metrics.Space.Md * scale;
        var available = wallet.CheckInAvailable && !wallet.Paused && !frozen && !store.CheckingIn;
        var bottom = streakCard.Draw(ui, new Vector2(origin.X, top), width, wallet, available, store.CheckingIn,
            out var buttonRect, out var pressed);
        checkInAnchor = new Vector2(buttonRect.Center.X, buttonRect.Min.Y - 6f * scale);
        UiAnchors.Report("coin.checkin", buttonRect);
        if (pressed)
        {
            UiFeedback.Play(UiSound.Tap);
            store.CheckIn();
        }

        return bottom;
    }

    private float DrawRecent(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        if (!store.LoadedOnce)
        {
            return origin.Y;
        }

        ledgerText.Sync(store.Entries);
        var entries = ledgerText.Entries;
        var headerTop = origin.Y + CoinArt.SectionGap * scale;
        var trailing = entries.Length > RecentCount ? Loc.T(L.Coin.SeeAll) : string.Empty;
        var cursorY = headerTop + CoinArt.SectionTitle(drawList, ui, new Vector2(origin.X, headerTop), width,
            Loc.T(L.Coin.LatestActivity), trailing, out var seeAll, scale);
        cursorY += CoinArt.HeaderGap * scale;
        if (seeAll)
        {
            UiFeedback.Play(UiSound.Tap);
            SelectTab(CoinTab.History);
        }

        if (entries.Length == 0)
        {
            return CoinArt.DrawPanel(ui, new Vector2(origin.X, cursorY), width, FontAwesomeIcon.Receipt, ui.Accent,
                Loc.T(L.Coin.HistoryEmptyTitle), Loc.T(L.Coin.HistoryEmptyHint), scale);
        }

        var count = Math.Min(RecentCount, entries.Length);
        var rowHeight = CoinArt.RowHeight * scale;
        var min = new Vector2(origin.X, cursorY);
        var max = new Vector2(origin.X + width, cursorY + count * rowHeight);
        CoinArt.Card(drawList, ui, min, max, scale);
        for (var index = 0; index < count; index++)
        {
            var top = cursorY + index * rowHeight;
            var row = new Rect(new Vector2(min.X, top), new Vector2(max.X, top + rowHeight));
            if (index > 0)
            {
                DrawRowHairline(drawList, row, scale);
            }

            if (DrawLedgerRow(drawList, row, index, scale))
            {
                OpenEntry(entries[index].Id);
            }
        }

        return max.Y;
    }
}
