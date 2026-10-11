using Aetherphone.Apps.Casino;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Conduct;
using Aetherphone.Core.Theme;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoRegistrationTests
{
    [Fact]
    public void CasinoAccentIsTheRoseRingMember()
    {
        Assert.Equal(AccentRing.Rose, AppAccents.For("casino"));
        Assert.Equal(AccentRing.Ink, AppAccents.InkFor("casino"));
        Assert.False(AppAccents.IsBrandLocked("casino"));
    }

    [Fact]
    public void CasinoConductGateShipsAllFiveSections()
    {
        var gate = ConductRules.For("casino");
        Assert.NotNull(gate);
        Assert.Equal(1, gate!.Version);
        Assert.Equal(5, gate.Sections.Length);
        Assert.Equal(ConductTone.Neutral, gate.Sections[0].Tone);
        Assert.Equal(ConductTone.Prohibited, gate.Sections[1].Tone);
        Assert.Equal(ConductTone.Restricted, gate.Sections[2].Tone);
        Assert.Equal(ConductTone.Encouraged, gate.Sections[3].Tone);
        Assert.Equal(ConductTone.Neutral, gate.Sections[4].Tone);
    }

    [Fact]
    public void CasinoRouteDefaultsToTheFloor()
    {
        Assert.Equal(CasinoScreen.Floor, CasinoRoute.Floor.Screen);
        Assert.Equal(string.Empty, CasinoRoute.Floor.GameId);
    }
}
