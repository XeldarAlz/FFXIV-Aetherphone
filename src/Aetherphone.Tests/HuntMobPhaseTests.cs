using Aetherphone.Core.Hunts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HuntMobPhaseTests
{
    private static FileInfo Source() => new(Path.Combine(AppContext.BaseDirectory, "Hunts", "HuntMob.json"));

    [Fact]
    public void BehemothHasTwoOwnPhasesInItsFirstWindow()
    {
        var catalog = new HuntMobCatalog(Source());

        var behemoth = catalog.Find("behemoth");

        Assert.NotNull(behemoth);
        var window = Assert.Single(behemoth!.Windows);
        Assert.Equal(2, window.Phases.Length);
        Assert.All(window.Phases, phase => Assert.Null(phase.MobId));
        Assert.Equal("Behold Now Behemoth", window.Phases[0].Name?["en"]);
        Assert.Equal("He Taketh It with His Eyes", window.Phases[1].Name?["en"]);
    }

    [Fact]
    public void ArchAethereaterOwnsBothPhasesOfItsOwnEncounter()
    {
        var catalog = new HuntMobCatalog(Source());

        var archAethereater = catalog.Find("arch_aethereater");

        Assert.NotNull(archAethereater);
        var window = Assert.Single(archAethereater!.Windows);
        Assert.Equal(2, window.Phases.Length);
        Assert.All(window.Phases, phase => Assert.Null(phase.MobId));
    }

    [Fact]
    public void GunittsSecondPhaseEntryBelongsToItsSSCompanionNotItself()
    {
        var catalog = new HuntMobCatalog(Source());

        var gunitt = catalog.Find("gunitt");

        Assert.NotNull(gunitt);
        var window = Assert.Single(gunitt!.Windows);
        Assert.Equal(3, window.Phases.Length);
        Assert.Null(window.Phases[0].MobId);
        Assert.Equal("forgiven_rebellion", window.Phases[1].MobId);
        Assert.Equal("forgiven_rebellion", window.Phases[2].MobId);
    }
}
