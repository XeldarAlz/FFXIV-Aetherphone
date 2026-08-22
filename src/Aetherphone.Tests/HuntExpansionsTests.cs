using Aetherphone.Core.Hunts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HuntExpansionsTests
{
    [Fact]
    public void IdsAndLabelsStayTheSameLength() =>
        Assert.Equal(HuntExpansions.Ids.Length, HuntExpansions.Labels.Length);

    [Fact]
    public void EveryRealCatalogMobHasAKnownExpansionId()
    {
        var source = new FileInfo(Path.Combine(AppContext.BaseDirectory, "Hunts", "HuntMob.json"));
        var catalog = new HuntMobCatalog(source);

        var mobs = catalog.ById.Values;
        Assert.NotEmpty(mobs);

        foreach (var mob in mobs)
        {
            Assert.Contains(mob.ExpansionId, HuntExpansions.Ids);
        }
    }
}
