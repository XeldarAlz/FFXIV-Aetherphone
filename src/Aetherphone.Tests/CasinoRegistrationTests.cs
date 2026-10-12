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
    public void CasinoConductGateShipsAllSevenSections()
    {
        var gate = ConductRules.For("casino");
        Assert.NotNull(gate);
        Assert.Equal(2, gate!.Version);
        Assert.Equal(7, gate.Sections.Length);
        Assert.Equal(ConductTone.Info, gate.Sections[0].Tone);
        Assert.Equal(ConductTone.Allowed, gate.Sections[1].Tone);
        Assert.Equal(ConductTone.Rule, gate.Sections[2].Tone);
        Assert.Equal(ConductTone.Rule, gate.Sections[3].Tone);
        Assert.Equal(ConductTone.ZeroTolerance, gate.Sections[4].Tone);
        Assert.Equal(ConductTone.Info, gate.Sections[5].Tone);
        Assert.Equal(ConductTone.Info, gate.Sections[6].Tone);
    }

    [Fact]
    public void CasinoRouteDefaultsToTheFloor()
    {
        Assert.Equal(CasinoScreen.Floor, CasinoRoute.Floor.Screen);
        Assert.Equal(string.Empty, CasinoRoute.Floor.GameId);
    }
}
