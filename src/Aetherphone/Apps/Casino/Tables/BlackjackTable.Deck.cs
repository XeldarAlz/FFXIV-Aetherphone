using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Tables;

internal sealed partial class BlackjackTable
{
    private const int MainTarget = -1;
    private const float SideCaptionGap = 3f;

    private static readonly int[] MainActions =
    {
        BlackjackRules.ActionHit,
        BlackjackRules.ActionStand,
    };

    private static readonly int[] SideActions =
    {
        BlackjackRules.ActionDouble,
        BlackjackRules.ActionSplit,
        BlackjackRules.ActionSurrender,
    };

    private static readonly BlackjackSideBet[] SideBets = { BlackjackSideBet.PerfectPairs, BlackjackSideBet.TwentyOnePlusThree };

    private readonly BetComposer composer;
    private readonly ClassicBetComposer gilComposer;
    private readonly long[] pendingSides = new long[2];

    private int sideTarget = MainTarget;
    private long heldMainAmount;
    private string stateLabel = string.Empty;

    private void ResetDeck()
    {
        composer.Reset(BlackjackRules.MinBet);
        gilComposer.Reset(0);
        ClearPendingSides();
    }

    private void ClearPendingSides()
    {
        Array.Clear(pendingSides);
        if (sideTarget != MainTarget && heldMainAmount > 0)
        {
            composer.Reset(heldMainAmount);
        }

        sideTarget = MainTarget;
    }

    private bool CanStakeSides(CasinoBlackjackRoomStateDto board)
    {
        if (!BlackjackRules.IsSeat(mySeat) || board.Phase != BlackjackPhases.Betting
            || !BlackjackRecap.SideBetsOffered(board) || rooms.StakeInFlight || seatFlow.Waiting)
        {
            return false;
        }

        var seat = projection.SeatAt(mySeat);
        return seat is not null && seat.Committed == 0;
    }

    private void DrawBetSpots(ImDrawListPtr drawList, AppSkin ui, CasinoBlackjackRoomStateDto board, float scale)
    {
        var mainRadius = layout.BetSpotPixels;
        var ring = ImGui.GetColorU32(CasinoColors.Money with { W = sideTarget == MainTarget ? 0.7f : 0.35f });
        drawList.AddCircle(layout.BetSpot, mainRadius, ring, 40, MathF.Max(1.2f, 1.6f * scale));
        if (!BlackjackRecap.SideBetsOffered(board))
        {
            return;
        }

        var staking = CanStakeSides(board);
        var seat = projection.SeatAt(mySeat);
        var recap = recaps[mySeat];
        for (var index = 0; index < SideBets.Length; index++)
        {
            var bet = SideBets[index];
            var center = layout.SideSpot(bet);
            var placed = bet == BlackjackSideBet.PerfectPairs
                ? seat?.SideBets?.PerfectPairs ?? 0
                : seat?.SideBets?.TwentyOnePlusThree ?? 0;
            var amount = placed > 0 ? placed : staking ? pendingSides[index] : 0;
            var paid = bet == BlackjackSideBet.PerfectPairs ? recap.PairsPaid : recap.ThreePaid;
            if (DrawSideSpot(drawList, center, bet, amount, staking, sideTarget == index, paid, scale) && staking)
            {
                TapSideSpot(index);
            }
        }
    }

    private bool DrawSideSpot(ImDrawListPtr drawList, Vector2 center, BlackjackSideBet bet, long amount,
        bool interactive, bool targeted, bool paid, float scale)
    {
        var radius = layout.BetSpotPixels;
        var corner = new Vector2(radius, radius);
        var hovered = interactive && UiInteract.Hover(center - corner, center + corner);
        var lit = paid ? 1f : targeted ? 0.9f : hovered ? 0.7f : 0.4f;
        var fill = amount > 0 ? CasinoColors.LightA with { W = 0.20f + 0.2f * lit } : CasinoColors.LightB with { W = 0.06f + 0.1f * lit };
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(fill), 32);
        var ink = paid ? CasinoColors.Money : targeted ? CasinoColors.LightB : StageText.Strong;
        drawList.AddCircle(center, radius, ImGui.GetColorU32(ink with { W = 0.5f + 0.5f * lit }), 32,
            MathF.Max(1.2f, (targeted ? 2.2f : 1.4f) * scale));
        var style = TextStyles.FootnoteEmphasized;
        if (amount > 0)
        {
            var stake = NumberText.Compact(amount);
            var inner = radius * 1.8f;
            var fit = StageText.FitScale(stake, inner, style, StageTextRole.Label);
            Typography.DrawCentered(drawList, center, Typography.FitText(stake, inner, fit, style.Weight),
                CasinoColors.Money, fit, style.Weight);
        }
        else if (interactive)
        {
            var name = Loc.T(bet == BlackjackSideBet.PerfectPairs ? L.Blackjack.SidePairs : L.Blackjack.SideThree);
            var captionHeight = Typography.LineHeight(style);
            var captionCenter = new Vector2(center.X, center.Y - radius - SideCaptionGap * scale - captionHeight * 0.5f);
            Typography.DrawCentered(drawList, captionCenter,
                Typography.FitText(name, layout.SideCaptionWidth, style), StageText.Strong, style);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(center - corner, center + corner, hovered);
    }

    private void TapSideSpot(int index)
    {
        inlineReason = string.Empty;
        CasinoSfx.Play(UiSound.ChipSlide);
        if (sideTarget == index)
        {
            pendingSides[index] = 0;
            sideTarget = MainTarget;
            composer.Reset(heldMainAmount);
            return;
        }

        if (sideTarget == MainTarget)
        {
            heldMainAmount = composer.Amount;
        }

        sideTarget = index;
        var start = pendingSides[index] > 0 ? pendingSides[index] : BlackjackSideBets.SideBetMin;
        composer.Reset(start);
    }

    private void DrawDeck(CasinoStage stage, in CasinoStageFrame frame, AppSkin ui, CasinoStateDto state,
        CasinoBlackjackRoomStateDto board, CasinoRoomSnapshotDto snapshot, long deadlineRemaining, bool veiled)
    {
        var scale = UiScale.Current;
        var deck = frame.Deck;
        var row = DeckActions.Row(deck, scale);
        var draining = snapshot.State != CasinoRoomStates.Live;
        var banked = BlackjackHosting.SeatBanked(board);
        var sitting = CasinoWire.SittingFor(state, CasinoWire.BlackjackKind);
        var bought = banked || sitting is not null;
        var onBoard = BlackjackRules.IsSeat(mySeat);
        if (!bought || (!onBoard && !CasinoSeatMachine.Holds(seatFlow.Stage)))
        {
            DrawSitAction(stage, ui, state, board, bought, row);
            return;
        }

        var stakeBlocked = draining || (!banked && (state.Draining || state.StakesPaused));
        var seatStack = banked ? projection.SeatAt(mySeat)?.Chips ?? 0 : SeatStackOf(sitting!);
        if (onBoard && banked && DrawSeatBankedAction(ui, board, seatStack, row))
        {
            return;
        }

        var mask = projection.ActionsMask;
        if (onBoard && BlackjackRecap.InsuranceOffered(board, mask))
        {
            DrawInsurance(ui, row, scale);
            return;
        }

        var seat = projection.SeatAt(mySeat);
        var floorPaused = banked ? board.Paused : state.StakesPaused || state.Draining;
        var canBet = onBoard && CasinoJoinGate.CanPlaceBet(board.Phase, true, seatFlow.Waiting, draining, floorPaused);
        if (canBet && (seat?.Committed ?? 0) == 0)
        {
            DrawComposer(stage, frame, ui, board, seatStack, veiled);
            return;
        }

        if (onBoard && CasinoJoinGate.CanAct(true, seatFlow.Waiting, mask != 0))
        {
            DrawActionBar(ui, seatStack, row, scale);
            return;
        }

        if (canBet || (onBoard && board.InsuranceOpen))
        {
            var status = board.InsuranceOpen && seat is { Insurance: > 0 }
                ? text.Compact(L.Blackjack.Insured, seat.Insurance)
                : Loc.T(board.InsuranceOpen ? L.Blackjack.InsuranceWaiting : L.Blackjack.BetIn);
            StageText.Status(ImGui.GetWindowDrawList(), BlackjackDeckLayout.UpperRow(row, scale).Center, status,
                row.Width, scale);
        }

        if (seatFlow.StandQueued)
        {
            return;
        }

        var standLabel = stakeBlocked ? Loc.T(L.Casino.CashOut) : Loc.T(L.Casino.StandAction);
        if (stage.SecondaryAction(standLabel, !seatFlow.Busy, ui.Ink))
        {
            inlineReason = string.Empty;
            seatFlow.Stand(roomId);
        }
    }

    private void DrawInsurance(AppSkin ui, Rect row, float scale)
    {
        var bet = ActiveBetOf();
        var cost = BlackjackRules.InsuranceFor(bet > 0 ? bet : projection.SeatAt(mySeat)?.Committed ?? 0);
        var enabled = !rooms.StakeInFlight;
        var insureLabel = text.Compact(L.Blackjack.InsurePill, cost);
        var declineLabel = Loc.T(L.Blackjack.NoInsurePill);
        var style = Button.LabelStyle(row.Height);
        BlackjackDeckLayout.Split(row, BlackjackDeckLayout.UpperRow(row, scale),
            Typography.Measure(declineLabel, style).X, Typography.Measure(insureLabel, style).X, scale,
            out var declineRect, out var insureRect);
        var drawList = ImGui.GetWindowDrawList();
        if (Button.Draw(drawList, insureRect, insureLabel, ui.Ink, ButtonStyle.Prominent, enabled: enabled,
                id: "blackjack.insure"))
        {
            inlineReason = string.Empty;
            rooms.SendBlackjackAction(BlackjackRules.ActionInsurance);
            CasinoSfx.Play(UiSound.ChipSlide);
        }

        if (Button.Draw(drawList, declineRect, declineLabel, ui.Ink, ButtonStyle.Gray, enabled: enabled,
                id: "blackjack.noInsure"))
        {
            inlineReason = string.Empty;
            rooms.SendBlackjackAction(BlackjackRules.ActionDeclineInsurance);
        }
    }

    private void DrawComposer(CasinoStage stage, in CasinoStageFrame frame, AppSkin ui,
        CasinoBlackjackRoomStateDto board, long seatStack, bool veiled)
    {
        var scale = UiScale.Current;
        var currency = CasinoCurrencies.Of(board);
        var minimum = board.MinBet > 0 ? board.MinBet : BlackjackRules.MinBet;
        var tableMax = board.MaxBet > 0 ? board.MaxBet : BlackjackRules.MaxBet;
        var blocked = veiled || board.Paused || rooms.StakeInFlight || frame.Blocked;
        if (currency == CasinoCurrencies.Gil)
        {
            var inset = BetComposer.Pad * scale;
            var bounds = new Rect(new Vector2(frame.Deck.Min.X + inset, frame.Deck.Min.Y + inset),
                new Vector2(frame.Deck.Max.X - inset, frame.Deck.Max.Y - inset));
            var label = text.Named(L.Strip.BetFor, hostedText.Gil(gilComposer.Amount));
            if (gilComposer.Draw(ui, bounds, minimum, tableMax, seatStack, !blocked, label, frame.DeltaSeconds))
            {
                inlineReason = string.Empty;
                rooms.PlaceBlackjackBet(gilComposer.Amount);
            }

            return;
        }

        var maximum = currency == CasinoCurrencies.Chips ? Math.Min(tableMax, chips.Ceiling.MaxBet) : tableMax;
        if (sideTarget != MainTarget)
        {
            DrawSideComposer(stage, frame, ui, seatStack, blocked);
            return;
        }

        composer.Prefill(minimum);
        var sides = pendingSides[0] + pendingSides[1];
        var model = new BetComposerModel(minimum, maximum, Math.Max(0, seatStack - sides), L.Strip.BetFor,
            !blocked, Repeat: stage.RepeatPressed(), Busy: rooms.StakeInFlight, Rack: true);
        var action = composer.Draw(stage, ui, frame.Deck, model, frame.DeltaSeconds);
        ClampSidesTo(composer.Amount);
        if (action != BetComposerAction.Confirm)
        {
            return;
        }

        inlineReason = string.Empty;
        rooms.PlaceBlackjackBet(composer.Amount, pendingSides[0], pendingSides[1]);
    }

    private void DrawSideComposer(CasinoStage stage, in CasinoStageFrame frame, AppSkin ui, long seatStack,
        bool blocked)
    {
        var other = pendingSides[sideTarget == 0 ? 1 : 0];
        var model = new BetComposerModel(BlackjackSideBets.SideBetMin, heldMainAmount,
            Math.Max(0, seatStack - heldMainAmount - other), L.Blackjack.SideStake, !blocked,
            Busy: rooms.StakeInFlight, Rack: true);
        if (composer.Draw(stage, ui, frame.Deck, model, frame.DeltaSeconds) != BetComposerAction.Confirm)
        {
            return;
        }

        pendingSides[sideTarget] = BlackjackSideBets.Clamp(composer.Amount, heldMainAmount);
        sideTarget = MainTarget;
        composer.Reset(heldMainAmount);
        CasinoSfx.Play(UiSound.ChipSlide);
    }

    private void ClampSidesTo(long mainAmount)
    {
        for (var index = 0; index < pendingSides.Length; index++)
        {
            if (pendingSides[index] <= mainAmount)
            {
                continue;
            }

            var floored = CasinoLadder.FloorToRung(mainAmount);
            pendingSides[index] = floored >= BlackjackSideBets.SideBetMin ? floored : 0;
        }
    }

    private bool DrawSeatBankedAction(AppSkin ui, CasinoBlackjackRoomStateDto board, long seatStack, Rect row)
    {
        if (BlackjackHosting.CanRebuy(board, seatStack))
        {
            if (DeckActions.DrawPrimary(row, hostedText.Rebuy(board.PracticeStack), ui.Ink, !tables.IntentInFlight,
                    "blackjack.rebuy"))
            {
                inlineReason = string.Empty;
                tables.Rebuy(roomId);
            }

            return true;
        }

        if (CasinoCurrencies.Of(board) != CasinoCurrencies.Gil || seatStack > 0
            || board.Phase != BlackjackPhases.Betting)
        {
            return false;
        }

        if (DeckActions.DrawPrimary(row, Loc.T(L.Tables.OpenLedger), ui.Ink, true, "blackjack.ledger"))
        {
            openLedger(roomId);
        }

        return true;
    }

    private long SeatStackOf(CasinoSittingDto sitting)
    {
        var seat = projection.SeatAt(mySeat);
        return seat is not null && seat.Chips > 0 ? seat.Chips : sitting.Stack;
    }

    private void DrawActionBar(AppSkin ui, long seatStack, Rect row, float scale)
    {
        var mask = projection.ActionsMask;
        var mainCount = OfferedCount(mask, MainActions);
        var sideCount = OfferedCount(mask, SideActions);
        if (mainCount + sideCount == 0)
        {
            return;
        }

        var cost = ActiveBetOf();
        var affordable = seatStack >= cost;
        var drawList = ImGui.GetWindowDrawList();
        DrawActionRow(drawList, ui, mask, MainActions, mainCount, row, cost, affordable, scale);
        var sideRow = mainCount > 0 ? BlackjackDeckLayout.UpperRow(row, scale) : row;
        DrawActionRow(drawList, ui, mask, SideActions, sideCount, sideRow, cost, affordable, scale);
    }

    private static int OfferedCount(int mask, int[] actions)
    {
        var count = 0;
        for (var index = 0; index < actions.Length; index++)
        {
            if (BlackjackRules.Allows(mask, actions[index]))
            {
                count++;
            }
        }

        return count;
    }

    private void DrawActionRow(ImDrawListPtr drawList, AppSkin ui, int mask, int[] actions, int count, Rect row,
        long cost, bool affordable, float scale)
    {
        var drawn = 0;
        for (var index = 0; index < actions.Length; index++)
        {
            var bit = actions[index];
            if (!BlackjackRules.Allows(mask, bit))
            {
                continue;
            }

            var rect = DeckActions.Slice(row, drawn, count, scale);
            drawn++;
            var wagered = bit == BlackjackRules.ActionDouble || bit == BlackjackRules.ActionSplit;
            var legal = !rooms.StakeInFlight && (!wagered || affordable);
            var costLine = wagered ? NumberText.Compact(cost) : string.Empty;
            if (!TableButton.Draw(drawList, rect, LabelFor(bit), costLine, ui.Ink, StyleFor(bit), legal, IdFor(bit)))
            {
                continue;
            }

            inlineReason = string.Empty;
            rooms.SendBlackjackAction(bit);
            CasinoSfx.Play(wagered || bit == BlackjackRules.ActionSurrender ? UiSound.ChipSlide : UiSound.CardSnap);
        }
    }

    private static ButtonStyle StyleFor(int action) => action switch
    {
        BlackjackRules.ActionHit => ButtonStyle.Prominent,
        BlackjackRules.ActionStand => ButtonStyle.Tinted,
        _ => ButtonStyle.Gray,
    };

    private static string IdFor(int action) => action switch
    {
        BlackjackRules.ActionHit => "blackjack.action.hit",
        BlackjackRules.ActionStand => "blackjack.action.stand",
        BlackjackRules.ActionDouble => "blackjack.action.double",
        BlackjackRules.ActionSplit => "blackjack.action.split",
        _ => "blackjack.action.surrender",
    };

    private long ActiveBetOf()
    {
        var hands = projection.HandsAt(mySeat);
        var active = projection.ActiveHand;
        if (active >= 0 && active < hands.Length)
        {
            return hands[active].Bet;
        }

        return hands.Length > 0 ? hands[0].Bet : 0;
    }

    private static string LabelFor(int action)
    {
        return action switch
        {
            BlackjackRules.ActionHit => Loc.T(L.Casino.BlackjackActionHit),
            BlackjackRules.ActionStand => Loc.T(L.Casino.BlackjackActionStand),
            BlackjackRules.ActionDouble => Loc.T(L.Casino.BlackjackActionDouble),
            BlackjackRules.ActionSplit => Loc.T(L.Casino.BlackjackActionSplit),
            _ => Loc.T(L.Blackjack.SurrenderPill),
        };
    }

    private void DrawSitAction(CasinoStage stage, AppSkin ui, CasinoStateDto state,
        CasinoBlackjackRoomStateDto board, bool bought, Rect row)
    {
        var banked = BlackjackHosting.SeatBanked(board);
        var buyIn = banked
            ? 0
            : bought
                ? CasinoWire.SittingFor(state, CasinoWire.BlackjackKind)?.Stack ?? 0
                : RackFor(state, board);
        if (!banked && !bought && buyIn <= 0)
        {
            stage.Chips?.DrawGetChips(row, RackNeed(state, board), ChipsNeedKind.BuyIn, ui.Ink);
            return;
        }

        var seatIndex = FirstOpenSeat(board);
        var label = seatIndex < 0 ? Loc.T(L.Casino.TableFullBadge) : Loc.T(L.Casino.SitDownAction);
        if (DeckActions.DrawPrimary(row, label, ui.Ink, seatIndex >= 0 && !seatFlow.Busy, "blackjack.sit"))
        {
            inlineReason = string.Empty;
            seatFlow.Sit(roomId, seatIndex, buyIn, board.Phase);
        }
    }

    private void TapEmptySeat(CasinoStage stage, int seatIndex, CasinoStateDto state,
        CasinoBlackjackRoomStateDto board)
    {
        CasinoSfx.Play(UiSound.ChipSlide);
        if (BlackjackHosting.SeatBanked(board))
        {
            inlineReason = string.Empty;
            seatFlow.Sit(roomId, seatIndex, 0, board.Phase);
            return;
        }

        var rack = CasinoWire.SittingFor(state, CasinoWire.BlackjackKind);
        if (rack is not null)
        {
            inlineReason = string.Empty;
            seatFlow.Sit(roomId, seatIndex, rack.Stack, board.Phase);
            return;
        }

        var buyIn = RackFor(state, board);
        if (buyIn <= 0)
        {
            stage.Chips?.Request(RackNeed(state, board), ChipsNeedKind.BuyIn);
            return;
        }

        inlineReason = string.Empty;
        seatFlow.Sit(roomId, seatIndex, buyIn, board.Phase);
    }

    private static long RackNeed(CasinoStateDto state, CasinoBlackjackRoomStateDto board) =>
        BlackjackRules.RackFor(CasinoLadder.CeilingFor(state).MaxBet, board.MinBet, board.MaxBet, RackMinimum(board),
            RackMaximum(board), long.MaxValue);

    private static long RackMinimum(CasinoBlackjackRoomStateDto board) =>
        board.MinBuyIn > 0 ? board.MinBuyIn : CasinoHostingRules.ChipMinBuyIn;

    private static long RackMaximum(CasinoBlackjackRoomStateDto board) =>
        board.MaxBuyIn > 0 ? board.MaxBuyIn : CasinoHostingRules.ChipMaxBuyIn;

    private static long RackFor(CasinoStateDto state, CasinoBlackjackRoomStateDto board)
    {
        return BlackjackRules.RackFor(CasinoLadder.CeilingFor(state).MaxBet, board.MinBet, board.MaxBet,
            RackMinimum(board), RackMaximum(board), state.Sitting?.Stack ?? 0);
    }
}
