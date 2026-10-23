using System.Text.Json;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Glamour;
using Xunit;

namespace Aetherphone.Tests;

public sealed class GlamourCoreTests
{
    [Theory]
    [InlineData("chara/equipment/e6016/model/c0201e6016_top.mdl", (int)GearSlot.Body)]
    [InlineData("chara/equipment/e6016/material/v0001/mt_c0201e6016_met_a.mtrl", (int)GearSlot.Head)]
    [InlineData("chara/equipment/e0100/texture/v01_c0101e0100_sho_n.tex", (int)GearSlot.Feet)]
    [InlineData("chara/accessory/a0050/model/c0101a0050_ril.mdl", (int)GearSlot.RingLeft)]
    [InlineData("chara/accessory/a0050/model/c0101a0050_nek.mdl", (int)GearSlot.Neck)]
    [InlineData("chara/weapon/w2001/obj/body/b0001/model/w2001b0001.mdl", (int)GearSlot.MainHand)]
    [InlineData("chara/human/c0201/obj/body/b0001/model/c0201b0001_top.mdl", GearSlots.None)]
    [InlineData("chara/equipment/e6016/model/c0201e6016.mdl", GearSlots.None)]
    public void GamePathsMapToTheirSlot(string gamePath, int slot)
    {
        Assert.Equal(slot, GearSlots.FromGamePath(gamePath));
    }

    [Fact]
    public void GlamourerSlotsSkipTheWaistAndTheThirteenth()
    {
        Assert.Equal(1, GearSlots.GlamourerSlot(GearSlot.MainHand));
        Assert.Equal(7, GearSlots.GlamourerSlot(GearSlot.Legs));
        Assert.Equal(12, GearSlots.GlamourerSlot(GearSlot.RingRight));
        Assert.Equal(14, GearSlots.GlamourerSlot(GearSlot.RingLeft));
        Assert.False(GearSlots.IsGear(5));
        Assert.False(GearSlots.IsGear(GearSlots.None));
        Assert.True(GearSlots.IsGear((int)GearSlot.Feet));
    }

    [Fact]
    public void ModFolderIsTheFirstSegmentUnderTheRoot()
    {
        Assert.Equal("Cute Coat", ModAttribution.ModFolder(@"D:\Penumbra", @"D:\Penumbra\Cute Coat\files\top.mdl"));
        Assert.Equal("Cute Coat", ModAttribution.ModFolder(@"D:\Penumbra\", @"d:\penumbra\Cute Coat\top.mdl"));
        Assert.Equal(string.Empty, ModAttribution.ModFolder(@"D:\Penumbra", @"D:\PenumbraOld\Coat\top.mdl"));
        Assert.Equal(string.Empty, ModAttribution.ModFolder(@"D:\Penumbra", "chara/equipment/e0001/model/x.mdl"));
        Assert.Equal(string.Empty, ModAttribution.ModFolder(string.Empty, @"D:\Penumbra\Coat\top.mdl"));
    }

    [Fact]
    public void CollectGroupsModsBySlotAndKeepsBodyModsApart()
    {
        var paths = new Dictionary<string, HashSet<string>>
        {
            [@"D:\Penumbra\Coat\top.mdl"] = new() { "chara/equipment/e6016/model/c0201e6016_top.mdl" },
            [@"D:\Penumbra\Coat\top_n.tex"] = new() { "chara/equipment/e6016/texture/v01_c0201e6016_top_n.tex" },
            [@"D:\Penumbra\Body\skin.tex"] = new() { "chara/human/c0201/obj/body/b0001/texture/skin.tex" },
            ["chara/equipment/e0001/model/c0201e0001_met.mdl"] = new() { "chara/equipment/e0001/model/c0201e0001_met.mdl" },
        };
        var bySlot = new Dictionary<int, HashSet<string>>();
        ModAttribution.Collect(paths, @"D:\Penumbra", bySlot);
        Assert.Equal(2, bySlot.Count);
        Assert.Contains("Coat", bySlot[(int)GearSlot.Body]);
        Assert.Contains("Body", bySlot[GearSlots.None]);
    }

    [Fact]
    public void ModMetaIsReadOncePerFolderUntilPenumbraRenamesItOrReloads()
    {
        var root = Path.Combine(Path.GetTempPath(), "aetherphone-mods-" + Guid.NewGuid().ToString("N"));
        var meta = Path.Combine(root, "Coat", "meta.json");
        Directory.CreateDirectory(Path.GetDirectoryName(meta)!);
        try
        {
            var cache = new ModMetaCache();
            File.WriteAllText(meta, "{\"Name\":\"Cute Coat\",\"Author\":\"Ada\",\"Version\":\"1.0\"}");
            Assert.Equal(new GlamModDto("Cute Coat", "Ada", "1.0"), cache.Describe(root, "Coat", "Cute Coat"));

            File.WriteAllText(meta, "{\"Name\":\"Cuter Coat\",\"Author\":\"Ada\",\"Version\":\"2.0\"}");
            Assert.Equal("1.0", cache.Describe(root, "coat", "Cute Coat").Version);
            Assert.Equal(new GlamModDto("Cuter Coat", "Ada", "2.0"), cache.Describe(root, "Coat", "Cuter Coat"));

            File.WriteAllText(meta, "{\"Name\":\"Cuter Coat\",\"Author\":\"Ada\",\"Version\":\"3.0\"}");
            cache.Clear();
            Assert.Equal("3.0", cache.Describe(root, "Coat", "Cuter Coat").Version);
            Assert.Equal(new GlamModDto("Boots"), cache.Describe(root, "Boots", "Boots"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void OptionsRoundTripIntoPenumbraSettings()
    {
        var options = ModAttribution.Options(new Dictionary<string, List<string>>
        {
            ["Colour"] = new() { "Red" },
            ["Extras"] = new() { "Bow", "Lace" },
            ["Empty"] = new(),
        });
        Assert.NotNull(options);
        Assert.Equal(2, options!.Length);
        var settings = ModAttribution.Settings(options);
        Assert.Equal(new[] { "Bow", "Lace" }, settings["Extras"]);
        Assert.Null(ModAttribution.Options(null));
        Assert.Empty(ModAttribution.Settings(null));
    }

    [Fact]
    public void MatchingFindsAnInstalledModByName()
    {
        var installed = new Dictionary<string, string> { ["coat-v2"] = "Cute Coat", ["boots"] = "Boots" };
        Assert.Equal("coat-v2", ModAttribution.MatchFolder(installed, new GlamModDto("cute coat")));
        Assert.Equal(string.Empty, ModAttribution.MatchFolder(installed, new GlamModDto("Hat")));
    }

    [Theory]
    [InlineData("https://www.xivmodarchive.com/modid/1234", true)]
    [InlineData("https://heliosphere.app/mod/abc", true)]
    [InlineData("https://someone.carrd.co", true)]
    [InlineData("https://www.patreon.com/creator", true)]
    [InlineData("http://www.xivmodarchive.com/modid/1234", false)]
    [InlineData("https://evil-xivmodarchive.com/mod", false)]
    [InlineData("https://example.com", false)]
    [InlineData("not a link", false)]
    [InlineData("", false)]
    public void OnlyAllowlistedHttpsLinksAreClickable(string link, bool allowed)
    {
        Assert.Equal(allowed, ModLinks.IsAllowed(link));
    }

    [Fact]
    public void VanillaMeansNoModsOnAnySlot()
    {
        Assert.True(GlamourService.IsVanilla(new[] { new GlamGearDto(3, 100) }));
        Assert.False(GlamourService.IsVanilla(new[]
        {
            new GlamGearDto(3, 100), new GlamGearDto(GearSlots.None, Mods: new[] { new GlamModDto("Body") }),
        }));
    }

    [Fact]
    public void GlamourerDesignsGiveGearAndBody()
    {
        var gear = GlamourerDesign.Read("{\"Customize\":{\"Race\":{\"Value\":8},\"Gender\":{\"Value\":1}},"
            + "\"Equipment\":{\"Head\":{\"ItemId\":0},\"Body\":{\"ItemId\":45123,\"Stain\":12,\"Stain2\":3},"
            + "\"Hands\":{\"ItemId\":4294967162},\"Feet\":{\"ItemId\":32802,\"Apply\":false},"
            + "\"LFinger\":{\"ItemId\":8000}}}", out var race, out var gender);
        Assert.Equal(2, gear.Length);
        Assert.Equal(new GlamGearDto((int)GearSlot.Body, 45123, 12, 3), gear[0]);
        Assert.Equal(new GlamGearDto((int)GearSlot.RingLeft, 8000), gear[1]);
        Assert.Equal((8, 1), (race, gender));
        Assert.Empty(GlamourerDesign.Read(string.Empty, out _, out _));
        Assert.Empty(GlamourerDesign.Read("{\"Customize\":{}}", out race, out _));
        Assert.Equal(0, race);
    }

    [Fact]
    public void GearLeavesDefaultsOffTheWire()
    {
        var plain = JsonSerializer.Serialize(new GlamGearDto(2, 45123, 1, 101), AethernetJsonContext.Default.GlamGearDto);
        Assert.Equal("{\"slot\":2,\"itemId\":45123,\"stain0\":1,\"stain1\":101}", plain);
        var modded = JsonSerializer.Serialize(new GlamGearDto(3, 1, Optional: true, Mods: new[]
        {
            new GlamModDto("Coat", "Ada", "1.2", "https://heliosphere.app/mod/x",
                new[] { new GlamOptionDto("Colour", new[] { "Red" }) }),
        }), AethernetJsonContext.Default.GlamGearDto);
        Assert.Contains("\"optional\":true", modded);
        Assert.Contains("\"mods\":[{\"name\":\"Coat\",\"author\":\"Ada\",\"version\":\"1.2\"", modded);
        Assert.Contains("\"options\":[{\"group\":\"Colour\",\"picked\":[\"Red\"]}]", modded);
    }
}
