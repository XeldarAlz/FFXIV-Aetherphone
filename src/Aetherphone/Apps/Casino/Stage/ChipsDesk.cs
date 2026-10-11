using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Casino.Stage;

internal enum ChipsPress : byte
{
    None,
    Place,
}

internal readonly record struct ChipsPending(long Need, long StartedAt)
{
    public bool Active => StartedAt != 0;
}

internal sealed class ChipsDesk
{
    public const string GetChipsId = "casino.deck.getchips";

    private const long PendingTimeoutMilliseconds = 20_000;

    private readonly CasinoStore store;
    private readonly CoinStore coins;
    private readonly GetChipsSheet sheet;

    private long refusedAt;

    public ChipsDesk(CasinoStore store, CoinStore coins)
    {
        this.store = store;
        this.coins = coins;
        sheet = new GetChipsSheet(this);
    }

    public CashierPanel? Cashier { get; set; }

    public bool Buying => store.BuyingChips;

    public bool AutoTopUpOn => store.AutoTopUp;

    public bool SheetOpen => sheet.IsOpen;

    public long WalletCoins => Math.Max(0, coins.Wallet?.Balance ?? 0);

    public long Rate => store.Rate;

    public long Stack => store.State?.Sitting?.Stack ?? 0;

    public void SetAutoTopUp(bool enabled) => store.SetAutoTopUp(enabled);

    public void Request(long needChips, ChipsNeedKind kind)
    {
        coins.EnsureFresh();
        sheet.Open(needChips, kind);
    }

    public bool Buy(long coinAmount)
    {
        refusedAt = 0;
        return store.BuyChips(coinAmount);
    }

    public bool CanAutoCover(long needChips, long stackChips) =>
        !AutoTopUpRule.Resting(refusedAt, Environment.TickCount64)
        && AutoTopUpRule.CoinsFor(store.AutoTopUp, needChips, stackChips, WalletCoins, Rate) > 0;

    public bool TryAutoBuy(long needChips, long stackChips)
    {
        if (Buying || !CanAutoCover(needChips, stackChips))
        {
            return false;
        }

        return store.BuyChips(AutoTopUpRule.CoinsFor(true, needChips, stackChips, WalletCoins, Rate));
    }

    public ChipsPress Primary(Rect rect, string betLabel, long need, long stack, bool enabled, in ControlInk ink,
        string id, ref ChipsPending pending)
    {
        if (pending.Active)
        {
            if (Buying && Environment.TickCount64 - pending.StartedAt < PendingTimeoutMilliseconds)
            {
                Button.Draw(rect, betLabel, ink, ButtonStyle.Prominent, enabled: false, id: id);
                return ChipsPress.None;
            }

            var covered = stack >= pending.Need;
            pending = default;
            if (covered && enabled)
            {
                Button.Draw(rect, betLabel, ink, ButtonStyle.Prominent, enabled: false, id: id);
                return ChipsPress.Place;
            }
        }

        if (need <= stack)
        {
            return Button.Draw(rect, betLabel, ink, ButtonStyle.Prominent, enabled: enabled, id: id)
                ? ChipsPress.Place
                : ChipsPress.None;
        }

        if (CanAutoCover(need, stack))
        {
            if (Button.Draw(rect, betLabel, ink, ButtonStyle.Prominent, enabled: enabled && !Buying, id: id)
                && TryAutoBuy(need, stack))
            {
                pending = new ChipsPending(need, Environment.TickCount64);
            }

            return ChipsPress.None;
        }

        DrawGetChips(rect, need, ChipsNeedKind.Bet, ink);
        return ChipsPress.None;
    }

    public void DrawGetChips(Rect rect, long need, ChipsNeedKind kind, in ControlInk ink)
    {
        if (Button.Draw(rect, Loc.T(L.Strip.GetChips), ink, ButtonStyle.Prominent, enabled: !Buying, id: GetChipsId))
        {
            Request(need, kind);
        }
    }

    public void Update()
    {
        if (store.TakeBuyFailure())
        {
            refusedAt = Environment.TickCount64;
            Report(Loc.T(CasinoReasons.MessageFor(CasinoReasons.Unreachable)), false);
        }

        var result = store.TakeBuyResult();
        if (result is null)
        {
            return;
        }

        if (!result.Granted)
        {
            refusedAt = Environment.TickCount64;
            Report(CasinoReasons.Text(result.Reason.Length > 0 ? result.Reason : CasinoReasons.Unreachable,
                store.Ceiling.MaxBet), false);
            return;
        }

        refusedAt = 0;
        UiFeedback.Play(UiSound.CasinoChips);
        sheet.Close();
        Report(Loc.T(L.Chips.Bought, NumberText.Compact(result.Chips), NumberText.Group(result.Coins)), true);
    }

    public void Gate()
    {
        sheet.Gate();
    }

    public void Draw(Rect screen, AppSkin ui)
    {
        sheet.Draw(screen, ui);
    }

    public void Close()
    {
        sheet.Close();
    }

    private void Report(string message, bool good)
    {
        if (!good && sheet.IsOpen)
        {
            sheet.ShowNote(message);
            return;
        }

        if (Cashier is { Visible: true } panel)
        {
            panel.ShowNote(message, good);
            if (good)
            {
                panel.ClearBuy();
            }

            return;
        }

        ShellToast.Show(message);
    }
}
