using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.DealerHoldem;

internal sealed partial class DealerHoldemCabinet
{
    private const int TripsOff = 0;
    private const int TripsOn = 1;
    private const int MaxBetButtons = 2;
    private const int PassiveSlot = 0;
    private const string TripsKnobId = "casino.dealerholdem.trips";
    private const string WaitId = "casino.dealerholdem.wait";
    private const string BetId = "casino.dealerholdem.bet";

    private readonly int[] legalMultiples = new int[MaxBetButtons];
    private readonly string[] secondaryLabels = new string[DealerHoldemDecisions.MaxSecondary];
    private readonly int[] secondaryMultiples = new int[DealerHoldemDecisions.MaxSecondary];

    private void DrawComposer(CasinoStage stage, AppSkin ui, in CasinoStageFrame frame, long stack, bool blocked)
    {
        var ceiling = store.Ceiling.MaxBet;
        var share = trips ? 3 : 2;
        var busy = dealerStore.InFlight || playback.Busy || cards.Busy || chips.Busy;
        var enabled = !blocked;
        var model = new BetComposerModel(DealerHoldemRules.MinAnte, ceiling, stack, L.DealerHoldem.DealFor,
            enabled, Knob: true, Repeat: stage.RepeatPressed(), Busy: busy, Cost: composer.Amount * share);
        var action = composer.Draw(stage, ui, frame.Deck, model, frame.DeltaSeconds);
        DrawTripsKnob(ui, composer.KnobRect, enabled && !busy);
        if (action != BetComposerAction.Confirm)
        {
            return;
        }

        var ante = composer.Amount;
        var side = trips ? DealerHoldemRules.TripsFor(ante) : 0;
        if (!DealerHoldemRules.IsOpening(ante, side))
        {
            return;
        }

        stage.Celebration.Clear();
        noticeText = string.Empty;
        playback.Clear();
        BeginRound();
        dealing = true;
        dealerStore.Start(ante, side);
        CasinoSfx.Play(UiSound.ChipSlide);
    }

    private void DrawTripsKnob(AppSkin ui, Rect rect, bool enabled)
    {
        if (rect.Width <= 0f || rect.Height <= 0f)
        {
            return;
        }

        tripsOptions[TripsOff] = Loc.T(L.DealerHoldem.NoTrips);
        tripsOptions[TripsOn] = tripsLabel.Get(L.DealerHoldem.TripsFor,
            NumberText.Compact(DealerHoldemRules.TripsFor(composer.Amount)));
        var picked = KnobSegments.Draw(ImGui.GetWindowDrawList(), TripsKnobId, rect, tripsOptions,
            trips ? TripsOn : TripsOff, ui, enabled);
        if (!enabled)
        {
            return;
        }

        var next = picked == TripsOn;
        if (next == trips)
        {
            return;
        }

        trips = next;
        UiFeedback.Play(trips ? UiSound.ToggleOn : UiSound.ToggleOff);
    }

    private void DrawDecisions(AppSkin ui, Rect deck, long stack, bool blocked, float scale)
    {
        var primary = DealerHoldemDecisions.Primary(deck, scale);
        var round = playback.Round;
        var ready = round is not null && playback.Deciding && !dealerStore.InFlight && !blocked && !cards.Busy
                    && !dealing;
        if (round is null || !DealerHoldemRules.IsDecision(round.Phase) || playback.Finishing)
        {
            Button.Draw(primary, StateText(), ui.Ink, ButtonStyle.Prominent, enabled: false, id: WaitId);
            return;
        }

        var multiples = LegalMultiples(round);
        var folds = Has(round.Actions, DealerHoldemRules.Fold);
        var passive = folds ? DealerHoldemRules.Fold : DealerHoldemRules.Check;
        var passiveLabel = Loc.T(folds ? L.DealerHoldem.Fold : L.DealerHoldem.Check);
        var canPass = Has(round.Actions, passive);
        if (!Has(round.Actions, DealerHoldemRules.Bet) || multiples == 0)
        {
            if (canPass && Button.Draw(primary, passiveLabel, ui.Ink, ButtonStyle.Prominent, enabled: ready, id: BetId))
            {
                dealerStore.Decide(round.RoundId, round.Step, passive, 0);
            }

            return;
        }

        var count = 0;
        if (canPass)
        {
            secondaryLabels[count] = passiveLabel;
            secondaryMultiples[count] = PassiveSlot;
            count++;
        }

        for (var index = 0; index < multiples - 1 && count < DealerHoldemDecisions.MaxSecondary; index++)
        {
            secondaryLabels[count] = BetLabel(index, legalMultiples[index], round.Ante);
            secondaryMultiples[count] = legalMultiples[index];
            count++;
        }

        DrawSecondaries(ui, deck, round, passive, count, stack, ready, scale);
        var top = legalMultiples[multiples - 1];
        if (Button.Draw(primary, BetLabel(MaxBetButtons, top, round.Ante), ui.Ink, ButtonStyle.Prominent,
                enabled: ready && stack >= round.Ante * top, id: BetId))
        {
            Bet(round, top);
        }
    }

    private void DrawSecondaries(AppSkin ui, Rect deck, CasinoDealerHoldemDto round, string passive, int count,
        long stack, bool ready, float scale)
    {
        if (count == 0)
        {
            return;
        }

        var row = DealerHoldemDecisions.SecondaryRow(deck, scale);
        var style = Button.LabelStyle(row.Height);
        Span<float> widths = stackalloc float[DealerHoldemDecisions.MaxSecondary];
        Span<Rect> rects = stackalloc Rect[DealerHoldemDecisions.MaxSecondary];
        for (var index = 0; index < count; index++)
        {
            widths[index] = Typography.Measure(secondaryLabels[index], style).X;
        }

        DealerHoldemDecisions.Secondaries(row, widths[..count], scale, rects);
        for (var index = 0; index < count; index++)
        {
            var multiple = secondaryMultiples[index];
            var enabled = ready && (multiple == PassiveSlot || stack >= round.Ante * multiple);
            if (!Button.Draw(rects[index], secondaryLabels[index], ui.Ink, ButtonStyle.Gray, enabled: enabled))
            {
                continue;
            }

            if (multiple == PassiveSlot)
            {
                dealerStore.Decide(round.RoundId, round.Step, passive, 0);
                continue;
            }

            Bet(round, multiple);
        }
    }

    private void Bet(CasinoDealerHoldemDto round, int multiple)
    {
        dealerStore.Decide(round.RoundId, round.Step, DealerHoldemRules.Bet, multiple);
        CasinoSfx.Play(UiSound.ChipSlide);
    }

    private int LegalMultiples(CasinoDealerHoldemDto round)
    {
        var source = round.Multiples;
        var count = 0;
        if (source is null)
        {
            return 0;
        }

        for (var index = 0; index < source.Length && count < MaxBetButtons; index++)
        {
            if (source[index] <= 0 || source[index] > DealerHoldemRules.MaxPlayMultiple)
            {
                continue;
            }

            legalMultiples[count] = source[index];
            count++;
        }

        if (count == MaxBetButtons && legalMultiples[0] > legalMultiples[1])
        {
            (legalMultiples[0], legalMultiples[1]) = (legalMultiples[1], legalMultiples[0]);
        }

        return count;
    }

    private string BetLabel(int slot, int multiple, long ante) =>
        betLabels[Math.Min(slot, betLabels.Length - 1)].Get(L.DealerHoldem.BetFor, GameNumber.Label(multiple),
            NumberText.Compact(ante * multiple));

    private static bool Has(string[]? actions, string action)
    {
        if (actions is null)
        {
            return false;
        }

        for (var index = 0; index < actions.Length; index++)
        {
            if (string.Equals(actions[index], action, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
