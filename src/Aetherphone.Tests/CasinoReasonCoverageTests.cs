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
