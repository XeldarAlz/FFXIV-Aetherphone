using Aetherphone.Core.Home;
using Aetherphone.Core.Theme;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HomeLookMirrorTests
{
    private const ulong MainContentId = 0x1100000000000001;
    private const ulong AltContentId = 0x1100000000000002;
    private static readonly Guid DefaultLook = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AltLook = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid DeletedLook = new("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void UnassignedCharacterResolvesToTheFirstLook()
    {
        var looks = Looks(DefaultLook, AltLook);
        var assignments = new Dictionary<ulong, Guid> { [AltContentId] = AltLook };

        Assert.Equal(DefaultLook, HomeLookMirror.Resolve(looks, assignments, MainContentId));
    }

    [Fact]
    public void AssignedCharacterResolvesToItsLook()
    {
        var looks = Looks(DefaultLook, AltLook);
        var assignments = new Dictionary<ulong, Guid> { [AltContentId] = AltLook };

        Assert.Equal(AltLook, HomeLookMirror.Resolve(looks, assignments, AltContentId));
    }

    [Fact]
    public void AssignmentToAMissingLookFallsBackToTheFirstLook()
    {
        var looks = Looks(DefaultLook, AltLook);
        var assignments = new Dictionary<ulong, Guid> { [AltContentId] = DeletedLook };

        Assert.Equal(DefaultLook, HomeLookMirror.Resolve(looks, assignments, AltContentId));
    }

    [Fact]
    public void NoLooksResolveToEmpty()
    {
        Assert.Equal(Guid.Empty,
            HomeLookMirror.Resolve(new List<HomeLook>(), new Dictionary<ulong, Guid>(), MainContentId));
    }

    [Fact]
    public void CaptureThenRestoreRoundTripsEveryField()
    {
        var configuration = Configured();
        var look = new HomeLook { Id = DefaultLook };
        HomeLookMirror.Capture(configuration, look);

        configuration.Home = new HomeLayout
        {
            Pages = new List<HomePage> { new() { Items = new List<HomeItem> { new() { AppId = "z" } } } },
            Dock = new List<string> { "z" },
            Installed = new List<string> { "kept" },
            Known = new List<string> { "kept" },
        };
        configuration.HomeGridRows = 8;
        configuration.ShowAppNames = false;
        configuration.ThemeMode = ThemeMode.Auto;
        configuration.AccentName = "#ff00ff";
        configuration.AccentCustomHex = "#ff00ff";
        configuration.PhoneCaseName = "Black";
        configuration.LightWallpaperId = "OtherLight";
        configuration.DarkWallpaperId = "OtherDark";

        HomeLookMirror.Restore(configuration, look);

        Assert.Equal(5, configuration.HomeGridRows);
        Assert.True(configuration.ShowAppNames);
        Assert.Equal(ThemeMode.Light, configuration.ThemeMode);
        Assert.Equal("Blue", configuration.AccentName);
        Assert.Equal(string.Empty, configuration.AccentCustomHex);
        Assert.Equal("Titanium", configuration.PhoneCaseName);
        Assert.Equal("DuskLight", configuration.LightWallpaperId);
        Assert.Equal("DuskDark", configuration.DarkWallpaperId);
        var home = configuration.Home!;
        Assert.Equal(new[] { "a", "b" }, home.Dock);
        Assert.Equal(2, home.Pages.Count);
        Assert.Equal("folder", home.Pages[0].Items[0].Kind);
        Assert.Equal("c", home.Pages[0].Items[0].Members[0].AppId);
        Assert.Equal(3, home.Pages[1].Items[0].Column);
    }

    [Fact]
    public void RestoreKeepsTheInstalledAndKnownSets()
    {
        var configuration = Configured();
        var look = new HomeLook { Id = DefaultLook };
        HomeLookMirror.Capture(configuration, look);
        configuration.Home!.Installed = new List<string> { "a", "b", "c", "later" };
        configuration.Home.Known = new List<string> { "a", "b", "c", "later" };

        HomeLookMirror.Restore(configuration, look);

        Assert.Equal(new[] { "a", "b", "c", "later" }, configuration.Home!.Installed);
        Assert.Equal(new[] { "a", "b", "c", "later" }, configuration.Home.Known);
    }

    [Fact]
    public void CaptureCopiesTheLayoutInsteadOfSharingIt()
    {
        var configuration = Configured();
        var look = new HomeLook { Id = DefaultLook };
        HomeLookMirror.Capture(configuration, look);

        configuration.Home!.Pages[0].Items[0].Members.Clear();
        configuration.Home.Dock!.Clear();

        Assert.Single(look.Pages[0].Items[0].Members);
        Assert.Equal(2, look.Dock!.Count);
    }

    [Fact]
    public void RestoringAnUnsavedLookOntoAnUnsavedPhoneLeavesTheLayoutUnsaved()
    {
        var configuration = new FakeLookConfiguration();
        var look = new HomeLook { Id = DefaultLook };
        HomeLookMirror.Capture(configuration, look);

        HomeLookMirror.Restore(configuration, look);

        Assert.Null(configuration.Home);
    }

    [Fact]
    public void RestoringAnUnsavedLookOntoASavedPhoneResetsTheLayout()
    {
        var configuration = Configured();
        var unsaved = new HomeLook { Id = AltLook };

        HomeLookMirror.Restore(configuration, unsaved);

        Assert.Null(configuration.Home);
    }

    private static List<HomeLook> Looks(params Guid[] ids)
    {
        var looks = new List<HomeLook>(ids.Length);
        for (var index = 0; index < ids.Length; index++)
        {
            looks.Add(new HomeLook { Id = ids[index] });
        }

        return looks;
    }

    private static FakeLookConfiguration Configured() =>
        new()
        {
            Home = new HomeLayout
            {
                Pages = new List<HomePage>
                {
                    new()
                    {
                        Items = new List<HomeItem>
                        {
                            new()
                            {
                                Kind = "folder",
                                FolderName = "Social",
                                Column = 0,
                                Row = 0,
                                Members = new List<HomeItem> { new() { AppId = "c", Column = 0, Row = 0 } },
                            },
                        },
                    },
                    new() { Items = new List<HomeItem> { new() { AppId = "a", Column = 3, Row = 1 } } },
                },
                Dock = new List<string> { "a", "b" },
                Installed = new List<string> { "a", "b", "c" },
                Known = new List<string> { "a", "b", "c" },
            },
            HomeGridRows = 5,
            ShowAppNames = true,
            ThemeMode = ThemeMode.Light,
            AccentName = "Blue",
            AccentCustomHex = string.Empty,
            PhoneCaseName = "Titanium",
            LightWallpaperId = "DuskLight",
            DarkWallpaperId = "DuskDark",
        };

    private sealed class FakeLookConfiguration : ILookConfiguration
    {
        public HomeLayout? Home { get; set; }
        public int HomeGridRows { get; set; }
        public bool ShowAppNames { get; set; }
        public ThemeMode ThemeMode { get; set; }
        public string AccentName { get; set; } = string.Empty;
        public string AccentCustomHex { get; set; } = string.Empty;
        public string PhoneCaseName { get; set; } = string.Empty;
        public string LightWallpaperId { get; set; } = string.Empty;
        public string DarkWallpaperId { get; set; } = string.Empty;
        public List<HomeLook> Looks { get; } = new();
        public Dictionary<ulong, Guid> LookByCharacter { get; } = new();
        public Guid ActiveLookId { get; set; }
        public void Save() { }
    }
}
