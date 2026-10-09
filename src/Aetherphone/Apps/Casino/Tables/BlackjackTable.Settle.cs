using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
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
    private const float AnnounceSeconds = 1.8f;
    private const int AnnounceCapacity = 3;

    private static readonly LocString[] PairNames =
    {
        default, L.Blackjack.PairMixed, L.Blackjack.PairColoured, L.Blackjack.PairPerfect,
    };

    private static readonly LocString[] ThreeNames =
    {
        default, L.Blackjack.ThreeFlush, L.Blackjack.ThreeStraight, L.Blackjack.ThreeTrips,
        L.Blackjack.ThreeStraightFlush, L.Blackjack.ThreeSuitedTrips,
    };

    private readonly string[] announcements = new string[AnnounceCapacity];

    private RollingAmount stackRoll;
    private string settledHandId = string.Empty;
    private string spokenHandId = string.Empty;
    private int spokenPhase = -1;
    private int spokenSeat = int.MinValue;
    private bool spokenInsurance;
    private int announceCount;
    private int announceIndex;
    private float announceClock;

    private void ResetSettlement()
    {
        settledHandId = string.Empty;
        spokenHandId = string.Empty;
        spokenPhase = -1;
        spokenSeat = int.MinValue;
        spokenInsurance = false;
        announceCount = 0;
        announceIndex = 0;
        announceClock = 0f;
        stackRoll.Snap(0);
        Array.Clear(recaps);
    }

    private bool Announcing => announceIndex < announceCount;

    private void UpdateAnnouncements(float delta)
    {
        if (!Announcing)
        {
            return;
        }

        announceClock += delta;
        if (announceClock < AnnounceSeconds)
        {
            return;
        }

        announceClock = 0f;
        announceIndex++;
    }

    private void Announce(string message)
    {
        if (announceCount >= AnnounceCapacity || message.Length == 0)
        {
            return;
        }

        announcements[announceCount] = message;
        announceCount++;
    }

    private bool DrawAnnouncement(ImDrawListPtr drawList, Vector2 center, float maxWidth, float scale)
    {
        if (!Announcing)
        {
            return false;
        }

        var fade = MathF.Min(1f, (AnnounceSeconds - announceClock) / 0.3f);
        StageText.Plate(drawList, center, announcements[announceIndex], maxWidth, CasinoColors.Money with { W = fade },
            TextStyles.Headline, scale);
        return true;
    }

    private void SpeakForPhase(CasinoBlackjackRoomStateDto board)
    {
        if (board.InsuranceOpen && !spokenInsurance)
        {
            spokenInsurance = true;
            dealer.Say(Loc.T(L.Blackjack.InsuranceQuestion));
            return;
        }

        if (board.Phase == spokenPhase && board.ActiveSeat == spokenSeat
            && string.Equals(spokenHandId, board.HandId, StringComparison.Ordinal))
        {
            return;
        }

        if (!string.Equals(spokenHandId, board.HandId, StringComparison.Ordinal))
        {
            spokenInsurance = false;
        }

        spokenPhase = board.Phase;
        spokenSeat = board.ActiveSeat;
        spokenHandId = board.HandId;
        if (board.Phase == BlackjackPhases.Betting)
        {
            dealer.Say(Loc.T(L.Casino.BlackjackWaitingForBets));
            return;
        }

        if (BlackjackPhases.Over(board.Phase) && board.DealerTotal > 0)
        {
            dealer.Say(board.DealerTotal > BlackjackRules.TargetTotal
                ? Loc.T(L.Blackjack.DealerBusts)
                : text.Count(L.Casino.BlackjackDealerHas, board.DealerTotal));
            return;
        }

        if (board.ActiveSeat >= 0 && board.ActiveSeat == mySeat)
        {
            dealer.Say(Loc.T(L.Casino.BlackjackYourTurn));
            CasinoSfx.Play(UiSound.TurnChime);
        }
    }

    private void SettleHand(CasinoStage stage, CasinoBlackjackRoomStateDto board, in CasinoStageFrame frame)
    {
        if (board.HandId.Length == 0 || string.Equals(settledHandId, board.HandId, StringComparison.Ordinal))
        {
            return;
        }

        if (!BlackjackRules.IsSeat(mySeat) || !BlackjackPhases.Over(board.Phase))
        {
            return;
        }

        var recap = recaps[mySeat];
        if (!recap.Settled || recap.Staked <= 0)
        {
            return;
        }

        settledHandId = board.HandId;
        announceCount = 0;
        announceIndex = 0;
        announceClock = 0f;
        var currency = CasinoCurrencies.Of(board);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (currency == CasinoCurrencies.Chips)
        {
            stage.Settle(new CasinoBetRecord(L.Casino.GameBlackjack, recap.Staked, recap.Returned, board.HandId, now));
        }

        if (!CasinoRoomIds.IsBlackjackHouse(roomId))
        {
            var name = board.Name.Length > 0 ? board.Name : Loc.T(L.Casino.TableUnnamed);
            history.RecordTableHand(roomId, board.HandIndex, name, currency, recap.Net, now);
        }

        var witnessed = motions[mySeat].SettleClock < SnappedClock && !frame.SnapToTruth;
        if (!witnessed)
        {
            return;
        }

        QueueSideAnnouncements(recap);
        if (recap.Net <= 0)
        {
            if (HeroBusted())
            {
                CasinoSfx.Play(UiSound.Bust);
            }

            return;
        }

        var origin = new Vector2(layout.Felt.Center.X, layout.HeroFanY);
        stage.Celebration.Celebrate(recap.Staked, recap.Returned, origin, frame.Instant);
    }

    private void QueueSideAnnouncements(in BlackjackSeatRecap recap)
    {
        if (recap.PairsPaid && recap.PairsKind < PairNames.Length)
        {
            Announce(Loc.T(L.Blackjack.SideWin, Loc.T(PairNames[recap.PairsKind]),
                GameNumber.Label(BlackjackSideBets.PaysFor(BlackjackSideBet.PerfectPairs, recap.PairsKind))));
        }

        if (recap.ThreePaid && recap.ThreeKind < ThreeNames.Length)
        {
            Announce(Loc.T(L.Blackjack.SideWin, Loc.T(ThreeNames[recap.ThreeKind]),
                GameNumber.Label(BlackjackSideBets.PaysFor(BlackjackSideBet.TwentyOnePlusThree,
                    recap.ThreeKind))));
        }

        if (recap.InsurancePaid)
        {
            Announce(Loc.T(L.Blackjack.InsurancePays));
        }

        if (announceCount > 0)
        {
            dealer.Say(announcements[0]);
        }
    }

    private bool HeroBusted()
    {
        var hands = projection.HandsAt(mySeat);
        for (var index = 0; index < hands.Length; index++)
        {
            if (hands[index].Outcome == BlackjackOutcomes.Bust)
            {
                return true;
            }
        }

        return false;
    }
}
