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

    private readonly int[] legalMultiples = new int[MaxBetButtons];

    private void DrawComposer(CasinoStage stage, AppSkin ui, in CasinoStageFrame frame, long stack, bool blocked)
    {
        var ceiling = store.Ceiling.MaxBet;
        var share = trips ? 3 : 2;
        var busy = dealerStore.InFlight || playback.Busy || cards.Busy || chips.Busy;
        var enabled = !blocked && stack >= DealerHoldemRules.StakeAtDeal(DealerHoldemRules.MinAnte, 0);
        var model = new BetComposerModel(DealerHoldemRules.MinAnte, ceiling, stack / share, L.DealerHoldem.DealFor,
            enabled, Knob: true, Repeat: stage.RepeatPressed(), Busy: busy);
        var action = composer.Draw(ui, frame.Deck, model, frame.DeltaSeconds);
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
        var picked = SegmentStrip.Draw("##dealerHoldemTrips", rect, tripsOptions, trips ? TripsOn : TripsOff,
            Surfaces.Fill(ui.TitleInk, FillLevel.Tertiary), ui.Accent, ui.MutedInk, CasinoColors.InkTitle);
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
        var row = DeckActions.Row(deck, scale);
        var round = playback.Round;
        var ready = round is not null && playback.Deciding && !dealerStore.InFlight && !blocked && !cards.Busy
                    && !dealing;
        if (round is null || !DealerHoldemRules.IsDecision(round.Phase) || playback.Finishing)
        {
            DeckActions.DrawPrimary(row, row.Min.X, StateText(), false, ui.Ink, id: "casino.dealerholdem.wait");
            return;
        }

        DrawStakeSummary(deck, row, round, scale);
        var multiples = LegalMultiples(round);
        var cursor = row.Min.X;
        var secondaries = 0;
        var folds = Has(round.Actions, DealerHoldemRules.Fold);
        var passive = folds ? DealerHoldemRules.Fold : DealerHoldemRules.Check;
        if (Has(round.Actions, passive) && DeckActions.DrawSecondary(row, ref cursor, ref secondaries,
                Loc.T(folds ? L.DealerHoldem.Fold : L.DealerHoldem.Check), ready, ui.Ink, scale))
        {
            dealerStore.Decide(round.RoundId, round.Step, passive, 0);
        }

        if (!Has(round.Actions, DealerHoldemRules.Bet) || multiples == 0)
        {
            return;
        }

        for (var index = 0; index < multiples - 1; index++)
        {
            var multiple = legalMultiples[index];
            if (DeckActions.DrawSecondary(row, ref cursor, ref secondaries, BetLabel(index, multiple, round.Ante),
                    ready && stack >= round.Ante * multiple, ui.Ink, scale))
            {
                Bet(round, multiple);
            }
        }

        var top = legalMultiples[multiples - 1];
        if (DeckActions.DrawPrimary(row, cursor, BetLabel(MaxBetButtons, top, round.Ante),
                ready && stack >= round.Ante * top, ui.Ink, id: "casino.dealerholdem.bet"))
        {
            Bet(round, top);
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

    private void DrawStakeSummary(Rect deck, Rect row, CasinoDealerHoldemDto round, float scale)
    {
        var top = deck.Min.Y + BetComposer.Pad * scale;
        var bottom = row.Min.Y - BetComposer.Gap * scale;
        if (bottom - top < Typography.LineHeight(TextStyles.Subheadline))
        {
            return;
        }

        var text = Loc.T(round.Phase switch
        {
            DealerHoldemRules.PhasePreFlop => L.DealerHoldem.HintPreFlop,
            DealerHoldemRules.PhaseFlop => L.DealerHoldem.HintFlop,
            _ => L.DealerHoldem.HintRiver,
        });
        var drawList = ImGui.GetWindowDrawList();
        Typography.DrawWrappedCentered(drawList, text, TextStyles.Subheadline, CasinoColors.InkBody,
            new Vector2(deck.Center.X, top), deck.Width - BetComposer.Pad * 2f * scale);
    }

    private void DrawSeatMissing(ImDrawListPtr drawList, AppSkin ui, Rect deck, float scale)
    {
        var inset = BetComposer.Pad * scale;
        var title = Loc.T(L.Casino.CabinetNoChipsTitle);
        Typography.Draw(drawList, new Vector2(deck.Min.X + inset, deck.Min.Y + inset),
            Typography.FitText(title, deck.Width - inset * 2f, TextStyles.SubheadlineEmphasized), ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        var row = DeckActions.Row(deck, scale);
        if (DeckActions.DrawPrimary(row, row.Min.X, Loc.T(L.Casino.Cashier), true, ui.Ink))
        {
            openCashier();
        }
    }
}
