using System;
using System.Collections.Generic;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoReasonCoverageTests
{
    [Fact]
    public void EveryReasonTheServerCanSendHasItsOwnMessage()
    {
        for (var index = 0; index < CasinoReasons.All.Length; index++)
        {
            var reason = CasinoReasons.All[index];
            Assert.True(CasinoReasons.TryMessage(reason, out var message), reason);
            Assert.NotEqual(L.Casino.ReasonGeneric.Key, message.Key);
            Assert.False(string.IsNullOrWhiteSpace(message.Source), reason);
        }
    }

    [Fact]
    public void NoTwoReasonsShareOneMessage()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < CasinoReasons.All.Length; index++)
        {
            var message = CasinoReasons.MessageFor(CasinoReasons.All[index]);
            Assert.True(seen.Add(message.Key), CasinoReasons.All[index]);
        }
    }

    [Fact]
    public void EveryReasonAppearsExactlyOnceInTheList()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < CasinoReasons.All.Length; index++)
        {
            Assert.True(seen.Add(CasinoReasons.All[index]), CasinoReasons.All[index]);
        }
    }

    [Fact]
    public void TheEconomyV3RefusalsAreCovered()
    {
        Assert.Contains("ceiling", CasinoReasons.All);
        Assert.Contains("ladder", CasinoReasons.All);
        Assert.Equal(L.Strip.ReasonCeiling.Key, CasinoReasons.MessageFor("ceiling").Key);
        Assert.Equal(L.Strip.ReasonLadder.Key, CasinoReasons.MessageFor("ladder").Key);
    }

    [Fact]
    public void TheOriginalsRefusalIsCovered()
    {
        Assert.Contains("invalid_move", CasinoReasons.All);
        Assert.Equal(L.Originals.ReasonInvalidMove.Key, CasinoReasons.MessageFor("invalid_move").Key);
    }

    [Fact]
    public void TheMissionRefusalIsCovered()
    {
        Assert.Contains("mission_incomplete", CasinoReasons.All);
        Assert.Equal(L.Club.ReasonMissionIncomplete.Key, CasinoReasons.MessageFor("mission_incomplete").Key);
    }

    [Fact]
    public void TheBonusAndHostingRefusalsAreCovered()
    {
        var reasons = new[]
        {
            "bonus_not_ready", "club_insufficient", "config_invalid", "practice_only", "not_dealer", "rebuy_off",
            "tournament_live", "no_tournament", "nothing_to_deal", "no_spectators", "duel_live", "no_duel",
            "raffle_live", "no_raffle", "ticket_limit", "round_live", "bank_limit", "host_frozen", "not_party",
            "already_confirmed", "settled", "gil_only", "not_owner",
        };
        for (var index = 0; index < reasons.Length; index++)
        {
            Assert.Contains(reasons[index], CasinoReasons.All);
            Assert.True(CasinoReasons.TryMessage(reasons[index], out _), reasons[index]);
        }

        Assert.Equal(L.Tables.ReasonBankLimit.Key, CasinoReasons.MessageFor("bank_limit").Key);
        Assert.Equal(L.Strip.ReasonBonusNotReady.Key, CasinoReasons.MessageFor("bonus_not_ready").Key);
    }

    [Fact]
    public void EveryReasonTheCasinoRoutesSendIsInTheList()
    {
        var serverReasons = new[]
        {
            "already_claimed", "bonus_not_ready", "buyin_range", "cap_reached", "ceiling", "closed",
            "club_insufficient", "cooldown", "daily_cap", "rule_cap", "draining", "expired", "frozen",
            "insufficient", "invalid_move", "ladder", "loss_limit", "machine_unknown", "mission_incomplete",
            "pair_limited", "paused", "round_open", "sitting_open", "sold_out", "stake_range", "stakes_paused",
            "table_closed", "unavailable", "seat_taken", "already_seated", "seated_elsewhere", "not_seated",
            "not_your_turn", "stale_action", "invalid_action", "too_late", "stale_hand", "hand_over", "at_hand_end",
            "pvp_loss_cap", "pvp_win_cap", "in_hand", "title_required", "time_bank_empty", "holdem_closed",
            "config_invalid", "practice_only", "not_dealer", "rebuy_off", "tournament_live", "no_tournament",
            "nothing_to_deal", "no_spectators", "duel_live", "no_duel", "raffle_live", "no_raffle", "ticket_limit",
            "round_live", "bank_limit", "host_frozen", "not_party", "already_confirmed", "settled", "gil_only",
            "ended", "restarting", "full", "private", "denied", "knock_pending", "banned_from_table", "blocked",
            "not_member", "already_hosting", "not_owner", "kicked",
        };
        for (var index = 0; index < serverReasons.Length; index++)
        {
            Assert.Contains(serverReasons[index], CasinoReasons.All);
            Assert.NotEqual(L.Casino.ReasonGeneric.Key, CasinoReasons.MessageFor(serverReasons[index]).Key);
        }
    }

    [Fact]
    public void ACeilingRefusalNamesTheCapItCarried()
    {
        var text = CasinoReasons.Text(CasinoReasons.Ceiling, 25_000);

        Assert.Contains("25", text, StringComparison.Ordinal);
        Assert.Equal(Loc.T(L.Strip.ReasonCeiling), CasinoReasons.Text(CasinoReasons.Ceiling, 0));
        Assert.Equal(Loc.T(L.Strip.ReasonLadder), CasinoReasons.Text(CasinoReasons.Ladder, 25_000));
    }

    [Fact]
    public void TheTableVocabularyIsInTheList()
    {
        Assert.Contains(CasinoReasons.Full, CasinoReasons.All);
        Assert.Contains(CasinoReasons.InviteOnly, CasinoReasons.All);
        Assert.Contains(CasinoReasons.KnockPending, CasinoReasons.All);
        Assert.Contains(CasinoReasons.SeatTaken, CasinoReasons.All);
        Assert.Contains(CasinoReasons.AlreadySeated, CasinoReasons.All);
        Assert.Contains(CasinoReasons.SeatedElsewhere, CasinoReasons.All);
        Assert.Contains(CasinoReasons.BoundElsewhere, CasinoReasons.All);
        Assert.Contains(CasinoReasons.AtHandEnd, CasinoReasons.All);
        Assert.Contains(CasinoReasons.Kicked, CasinoReasons.All);
    }

    [Fact]
    public void TheRoomCodeRefusalIsCovered()
    {
        Assert.Contains("code_unknown", CasinoReasons.All);
        Assert.Equal(L.Tables.ReasonCodeUnknown.Key, CasinoReasons.MessageFor("code_unknown").Key);
    }

    [Fact]
    public void AnUnknownReasonStillSaysSomethingRatherThanNothing()
    {
        Assert.Equal(L.Casino.ReasonGeneric.Key, CasinoReasons.MessageFor("a_reason_from_the_future").Key);
        Assert.Equal(L.Casino.ReasonGeneric.Key, CasinoReasons.MessageFor(string.Empty).Key);
        Assert.False(CasinoReasons.TryMessage("a_reason_from_the_future", out _));
    }

    [Fact]
    public void AnEmptyReasonBecomesTheUnreachableOne()
    {
        Assert.Equal(CasinoReasons.Unreachable, CasinoTablesStore.Named(string.Empty));
        Assert.Equal(CasinoReasons.SeatTaken, CasinoTablesStore.Named(CasinoReasons.SeatTaken));
    }
}
