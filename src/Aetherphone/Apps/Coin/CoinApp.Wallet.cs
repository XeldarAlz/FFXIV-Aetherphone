using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Coin;

internal sealed partial class CoinApp
{
    private Vector2 checkInAnchor;

    private void DrawWallet(Rect body)
    {
        var scale = UiScale.Current;
        using var surface = AppSurface.Begin(body);
        var wallet = store.Wallet;
        walletRefresh.Draw(body, surface.Pull, surface.Dragging, wallet is null, ui.MutedInk, store.RefreshNow);
        var checkInResult = store.TakeCheckInResult();
        if (checkInResult is { Granted: true, Amount: > 0 })
        {
            floats.Spawn(Loc.T(L.Coin.CheckInReward, NumberText.Group(checkInResult.Amount)),
                checkInAnchor);
        }

        if (wallet is null)
        {
            TourHolds.Hold(Id);
            LoadingPulse.Draw(body.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
            return;
        }

        var frozen = wallet.FrozenUntilUnix is not null;
        if (frozen)
        {
            NoticeCard(Loc.T(L.Coin.FrozenTitle), Loc.T(L.Coin.FrozenHint));
        }

        var heroOrigin = ImGui.GetCursorScreenPos();
        var heroWidth = ScrollLayout.StableContentWidth();
        CoinHero.Draw(wallet, ui.Palette);
        UiAnchors.Report("coin.balance", new Rect(heroOrigin,
            new Vector2(heroOrigin.X + heroWidth, ImGui.GetCursorScreenPos().Y)));
        ImGui.Dummy(new Vector2(0f, 8f * scale));

        if (wallet.Paused)
        {
            NoticeCard(Loc.T(L.Coin.PausedTitle), Loc.T(L.Coin.PausedHint));
        }
        else
        {
            CoinHero.DrawToday(wallet, ui.Palette);
        }

        DrawCasinoPurse(scale);
        ImGui.Dummy(new Vector2(0f, 12f * scale));
        DrawCheckIn(wallet, frozen, scale);
        ImGui.Dummy(new Vector2(0f, 12f * scale));
        DrawHowToEarn(wallet, body.Max.Y);
        ImGui.Dummy(new Vector2(0f, 16f * scale));
    }

    private void DrawCasinoPurse(float scale)
    {
        var stack = casino.State?.Sitting?.Stack ?? 0;
        if (stack <= 0)
        {
            return;
        }

        ImGui.Dummy(new Vector2(0f, 12f * scale));
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var height = 52f * scale;
        var drawList = ImGui.GetWindowDrawList();
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, min, max, Metrics.Radius.Card * scale);

        var pad = 14f * scale;
        Typography.Draw(drawList, new Vector2(min.X + pad, min.Y + 10f * scale),
            Loc.T(L.Casino.PurseRow), ui.BodyInk, TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(min.X + pad, min.Y + 28f * scale),
            Loc.T(L.Casino.PurseHint), ui.MutedInk, TextStyles.Caption1);

        var chipText = NumberText.Group(stack);
        var chipSize = CurrencyGlyph.MeasureAmount(chipText, TextStyles.SubheadlineEmphasized);
        CurrencyGlyph.DrawAmount(drawList, new Vector2(max.X - pad - chipSize.X, min.Y + 10f * scale), chipText,
            CurrencyKind.Chips, ui.Accent, TextStyles.SubheadlineEmphasized);
        var worth = Loc.T(L.Casino.LotCost,
            NumberText.Group(stack / Core.Casino.CasinoChipLots.ChipPerCoin));
        var worthSize = CurrencyGlyph.MeasureAmount(worth, TextStyles.Caption1);
        CurrencyGlyph.DrawAmount(drawList, new Vector2(max.X - pad - worthSize.X, min.Y + 28f * scale), worth,
            CurrencyKind.Coins, ui.MutedInk, TextStyles.Caption1);

        ImGui.Dummy(new Vector2(0f, height));
    }

    private void DrawCheckIn(CoinWalletDto wallet, bool frozen, float scale)
    {
        var available = wallet.CheckInAvailable && !wallet.Paused && !frozen && !store.CheckingIn;
        var pressed = CoinStreakCard.Draw(wallet, ui.Palette, theme, available, out var buttonRect);
        checkInAnchor = new Vector2(buttonRect.Center.X, buttonRect.Min.Y - 6f * scale);
        UiAnchors.Report("coin.checkin", buttonRect);
        if (pressed)
        {
            store.CheckIn();
        }
    }

    private void DrawHowToEarn(CoinWalletDto wallet, float visibleBottom)
    {
        if (wallet.Rules.Length == 0)
        {
            return;
        }

        var sectionOrigin = ImGui.GetCursorScreenPos();
        var sectionWidth = ScrollLayout.StableContentWidth();
        ui.SectionHeading(Loc.T(L.Coin.EarnHeader), 4f);
        var reported = DrawRules(wallet, false, sectionOrigin, sectionWidth, visibleBottom, false);
        DrawRules(wallet, true, sectionOrigin, sectionWidth, visibleBottom, reported);
    }

    private bool DrawRules(CoinWalletDto wallet, bool complete, Vector2 sectionOrigin, float sectionWidth,
        float visibleBottom, bool reported)
    {
        for (var index = 0; index < wallet.Rules.Length; index++)
        {
            var rule = wallet.Rules[index];
            if (CoinGoals.IsComplete(rule) != complete)
            {
                continue;
            }

            CoinEarnRow.Draw(rule, ui.Palette);
            if (reported)
            {
                continue;
            }

            reported = true;
            var bottom = MathF.Min(ImGui.GetCursorScreenPos().Y, visibleBottom);
            if (bottom > sectionOrigin.Y)
            {
                UiAnchors.Report("coin.earn", new Rect(sectionOrigin,
                    new Vector2(sectionOrigin.X + sectionWidth, bottom)));
            }
        }

        return reported;
    }

    private void NoticeCard(string title, string hint)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var inset = 14f * scale;
        var titleSize = Typography.Measure(title, TextStyles.FootnoteEmphasized);
        var hintBlock = Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, width - inset * 2f);
        var height = titleSize.Y + hintBlock.Y + 26f * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var rounding = 16f * scale;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(ui.Palette.CardFill));
        Material.EdgeSquircle(drawList, min, max, rounding, scale);
        Typography.Draw(drawList, new Vector2(min.X + inset, min.Y + 10f * scale), title,
            ui.Palette.Accent, TextStyles.FootnoteEmphasized);
        Typography.DrawWrappedLeft(new Vector2(min.X + inset, min.Y + titleSize.Y + 16f * scale), hint,
            ui.MutedInk, TextStyles.Footnote, width - inset * 2f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 8f * scale));
    }
}
