using Aetherphone.Core.Conduct;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ConductRulesTests
{
    [Fact]
    public void CoinConductGateShipsAllFiveSections()
    {
        var gate = ConductRules.For("coin");
        Assert.NotNull(gate);
        Assert.Equal(2, gate!.Version);
        Assert.Equal(5, gate.Sections.Length);
        Assert.Equal(ConductTone.Info, gate.Sections[0].Tone);
        Assert.Equal(ConductTone.Rule, gate.Sections[1].Tone);
        Assert.Equal(ConductTone.Info, gate.Sections[2].Tone);
        Assert.Equal(ConductTone.ZeroTolerance, gate.Sections[3].Tone);
        Assert.Equal(ConductTone.Info, gate.Sections[4].Tone);
    }

    [Fact]
    public void EveryGateResolvesByItsOwnAppId()
    {
        var gates = ConductRules.Gates;
        for (var index = 0; index < gates.Length; index++)
        {
            Assert.Same(gates[index], ConductRules.For(gates[index].AppId));
        }

        Assert.Null(ConductRules.For("calculator"));
    }

    [Fact]
    public void EveryGateCarriesTheSharedZeroToleranceLines()
    {
        var gates = ConductRules.Gates;
        for (var index = 0; index < gates.Length; index++)
        {
            var zero = Single(gates[index], ConductTone.ZeroTolerance);
            Assert.Equal(ConductPill.Suspended, zero.Pill);
            var shared = L.Conduct.ZeroItems;
            var tail = zero.Items.Length - shared.Length;
            Assert.True(tail >= 1, gates[index].AppId);
            for (var lineIndex = 0; lineIndex < shared.Length; lineIndex++)
            {
                Assert.Equal(shared[lineIndex].Key, zero.Items[tail + lineIndex].Key);
            }
        }
    }

    [Fact]
    public void EveryGateEndsWithTheModerationLadder()
    {
        var gates = ConductRules.Gates;
        for (var index = 0; index < gates.Length; index++)
        {
            var sections = gates[index].Sections;
            var ladder = sections[^1];
            Assert.Equal(L.Conduct.LadderTitle.Key, ladder.Heading.Key);
            var veils = gates[index].AppId is "chirper" or "aethergram";
            Assert.Equal(veils, ladder.Items[0].Key == L.Conduct.LadderVeil.Key);
            Assert.Equal(L.Conduct.LadderItems.Length + (veils ? 1 : 0), ladder.Items.Length);
        }
    }

    [Fact]
    public void OnlyTheSafeForWorkFeedsCarryAVeilList()
    {
        var gates = ConductRules.Gates;
        for (var index = 0; index < gates.Length; index++)
        {
            var veiled = 0;
            var sections = gates[index].Sections;
            for (var sectionIndex = 0; sectionIndex < sections.Length; sectionIndex++)
            {
                if (sections[sectionIndex].Tone == ConductTone.Veiled)
                {
                    veiled++;
                }
            }

            Assert.Equal(gates[index].AppId is "chirper" or "aethergram" ? 1 : 0, veiled);
        }
    }

    [Fact]
    public void MusicGatesOnItsChatsAndChocoChatGatesOnOpen()
    {
        Assert.False(ConductRules.Music.GatesOnOpen);
        Assert.True(ConductRules.Message.GatesOnOpen);
        Assert.Equal("message", ConductRules.Message.AppId);
    }

    private static ConductSection Single(ConductGate gate, ConductTone tone)
    {
        var found = -1;
        for (var index = 0; index < gate.Sections.Length; index++)
        {
            if (gate.Sections[index].Tone != tone)
            {
                continue;
            }

            Assert.Equal(-1, found);
            found = index;
        }

        Assert.NotEqual(-1, found);
        return gate.Sections[found];
    }
}
