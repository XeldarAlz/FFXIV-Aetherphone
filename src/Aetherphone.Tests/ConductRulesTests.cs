using Aetherphone.Core.Conduct;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ConductRulesTests
{
    [Fact]
    public void CoinConductGateShipsAllFourSections()
    {
        var gate = ConductRules.For("coin");
        Assert.NotNull(gate);
        Assert.Equal(1, gate!.Version);
        Assert.Equal(4, gate.Sections.Length);
        Assert.Equal(ConductTone.Neutral, gate.Sections[0].Tone);
        Assert.Equal(ConductTone.Prohibited, gate.Sections[1].Tone);
        Assert.Equal(ConductTone.Restricted, gate.Sections[2].Tone);
        Assert.Equal(ConductTone.Restricted, gate.Sections[3].Tone);
    }

    [Fact]
    public void EveryGateResolvesByItsOwnAppId()
    {
        Assert.Same(ConductRules.Coin, ConductRules.For(ConductRules.Coin.AppId));
        Assert.Same(ConductRules.Casino, ConductRules.For(ConductRules.Casino.AppId));
        Assert.Same(ConductRules.KindKupo, ConductRules.For(ConductRules.KindKupo.AppId));
        Assert.Null(ConductRules.For("calculator"));
    }
}
