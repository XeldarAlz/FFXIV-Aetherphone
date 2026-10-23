using Aetherphone.Apps.Prism;
using Aetherphone.Core.Aethernet.Contracts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PostedModsTests
{
    private static readonly GlamModDto Coat = new("Starlit Coat", "Bea");
    private static readonly GlamModDto Body = new("Silhouette Body", "Cee");
    private static readonly GlamModDto Hair = new("Long Braid", "Rin");

    private static GlamGearDto[] Outfit() => new[]
    {
        new GlamGearDto(3, 45123, 12, 0, Mods: new[] { Coat }),
        new GlamGearDto(6, 45124),
        new GlamGearDto(-1, Mods: new[] { Body, Hair }),
    };

    [Fact]
    public void NothingLeftOutSendsTheCaptureUntouched()
    {
        var gear = Outfit();
        Assert.Same(gear, PostedMods.Without(gear, new HashSet<string>(StringComparer.Ordinal)));
    }

    [Fact]
    public void ALeftOutModNeverLeavesTheClient()
    {
        var dropped = new HashSet<string>(StringComparer.Ordinal) { PostedMods.Key(Body), PostedMods.Key(Coat) };
        var posted = PostedMods.Without(Outfit(), dropped);

        Assert.Equal(3, posted.Length);
        Assert.Equal(45123u, posted[0].ItemId);
        Assert.Null(posted[0].Mods);
        Assert.Equal(new[] { Hair }, posted[2].Mods);
    }

    [Fact]
    public void ACharacterSlotWithEveryModLeftOutIsDropped()
    {
        var dropped = new HashSet<string>(StringComparer.Ordinal) { PostedMods.Key(Body), PostedMods.Key(Hair) };
        var posted = PostedMods.Without(Outfit(), dropped);

        Assert.Equal(2, posted.Length);
        Assert.DoesNotContain(posted, piece => piece.Slot == -1);
    }

    [Fact]
    public void ALeftOutModLeavesEverySlotItCovers()
    {
        var gear = new[]
        {
            new GlamGearDto(3, 45123, Mods: new[] { Coat }),
            new GlamGearDto(4, 45125, Mods: new[] { Coat, Hair }),
        };
        var posted = PostedMods.Without(gear, new HashSet<string>(StringComparer.Ordinal) { PostedMods.Key(Coat) });

        Assert.Null(posted[0].Mods);
        Assert.Equal(new[] { Hair }, posted[1].Mods);
    }
}
