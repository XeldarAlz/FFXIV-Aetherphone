using Aetherphone.Core.Emulation;
using Aetherphone.Core.Theme;
using Aetherphone.Apps.Games.GameBoy;
using System.Numerics;
using System.Text.Json;
using Xunit;

namespace Aetherphone.Tests;

public sealed class EmulatorSettingsTests
{
    [Fact]
    public void DefaultsMatchThePlayableKeyboardLayout()
    {
        var settings = new EmulatorSettings();

        Assert.Equal(0x26, settings.KeyFor(EmulatorButtons.Up));
        Assert.Equal(0x58, settings.KeyFor(EmulatorButtons.A));
        Assert.Equal(0x5A, settings.KeyFor(EmulatorButtons.B));
        Assert.Equal(0x43, settings.KeyFor(EmulatorButtons.X));
        Assert.Equal(0x56, settings.KeyFor(EmulatorButtons.Y));
        Assert.Equal(0x0D, settings.KeyFor(EmulatorButtons.Start));
        Assert.Equal(EmulatorVideoFilter.Pixel, settings.VideoFilter);
        Assert.Equal(EmulatorGameplayOrientation.Landscape, settings.GameplayOrientation);
        Assert.Equal(0.5f, settings.Layout.Screen.X);
        Assert.Equal(0.23f, settings.Layout.Screen.Y);
        Assert.Equal(0.82f, settings.Layout.A.X);
        Assert.Equal(0.75f, settings.Layout.X.X);
        Assert.True(settings.AutoSaveState);
        Assert.True(settings.AutoLoadState);
        Assert.Equal(2, settings.FastForwardSpeed);
        Assert.True(settings.FastForwardShortcut.IsEmpty);
        Assert.True(settings.SaveStateShortcut.IsEmpty);
        Assert.True(settings.LoadStateShortcut.IsEmpty);
        Assert.Equal(0.85f, settings.Layout.FastForward.X);
        Assert.Equal(0.5f, settings.LandscapeLayout.Screen.X);
        Assert.Equal(0.5f, settings.LandscapeLayout.Screen.Y);
        Assert.Equal(0.09f, settings.LandscapeLayout.Dpad.X);
        Assert.Equal(0.95f, settings.LandscapeLayout.A.X);
    }

    [Fact]
    public void VideoFilterValuesRemainBackwardCompatible()
    {
        Assert.Equal(0, (byte)EmulatorVideoFilter.Pixel);
        Assert.Equal(1, (byte)EmulatorVideoFilter.Smooth);
        Assert.Equal(2, (byte)EmulatorVideoFilter.Sharp);
        Assert.Equal(3, (byte)EmulatorVideoFilter.Balanced);
    }

    [Fact]
    public void LegacySettingsGainLandscapeWithoutLosingThePortraitLayout()
    {
        const string json = """
                            {"Layout":{"Screen":{"X":0.25,"Y":0.35,"Scale":1.1}}}
                            """;

        var restored = JsonSerializer.Deserialize<EmulatorSettings>(json)!;
        restored.Normalize();

        Assert.Equal(EmulatorGameplayOrientation.Landscape, restored.GameplayOrientation);
        Assert.Equal(0.25f, restored.Layout.Screen.X);
        Assert.Equal(0.35f, restored.Layout.Screen.Y);
        Assert.Equal(1.1f, restored.Layout.Screen.Scale);
        Assert.Equal(0.5f, restored.LandscapeLayout.Screen.X);
        Assert.Equal(0.5f, restored.LandscapeLayout.Screen.Y);
    }

    [Fact]
    public void RemappedKeysCanBeRestored()
    {
        var settings = new EmulatorSettings();
        settings.SetKey(EmulatorButtons.A, 0x43);
        settings.SetKey(EmulatorButtons.Start, 0x20);

        Assert.Equal(0x43, settings.KeyFor(EmulatorButtons.A));
        Assert.Equal(0x20, settings.KeyFor(EmulatorButtons.Start));

        settings.ResetKeys();

        Assert.Equal(0x58, settings.KeyFor(EmulatorButtons.A));
        Assert.Equal(0x0D, settings.KeyFor(EmulatorButtons.Start));
    }

    [Fact]
    public void SettingsSurviveAJsonRoundTrip()
    {
        var settings = new EmulatorSettings
        {
            VideoFilter = EmulatorVideoFilter.Smooth,
            GameplayOrientation = EmulatorGameplayOrientation.Portrait,
        };
        settings.Layout.Screen.Y = 0.74f;
        settings.Layout.A.X = 0.31f;
        settings.Layout.A.Scale = 1.4f;
        settings.LandscapeLayout.Screen.Scale = 0.95f;
        settings.SetKey(EmulatorButtons.B, 0x43);
        settings.AutoLoadState = false;
        settings.FastForwardSpeed = 4;
        settings.FastForwardShortcut.Set(new[] { 0x11, 0x46 }, 0x0200);
        settings.SaveStateShortcut.Set(new[] { 0x74 }, 0);
        settings.RomFolders.Add(@"C:\Games\Game Boy");

        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<EmulatorSettings>(json)!;

        Assert.Equal(EmulatorVideoFilter.Smooth, restored.VideoFilter);
        Assert.Equal(EmulatorGameplayOrientation.Portrait, restored.GameplayOrientation);
        Assert.Equal(0.74f, restored.Layout.Screen.Y);
        Assert.Equal(0.31f, restored.Layout.A.X);
        Assert.Equal(1.4f, restored.Layout.A.Scale);
        Assert.Equal(0.95f, restored.LandscapeLayout.Screen.Scale);
        Assert.Equal(0x43, restored.KeyFor(EmulatorButtons.B));
        Assert.False(restored.AutoLoadState);
        Assert.Equal(4, restored.FastForwardSpeed);
        Assert.Equal(new[] { 0x11, 0x46 }, restored.FastForwardShortcut.Keys);
        Assert.Equal((ushort)0x0200, restored.FastForwardShortcut.GamepadButtons);
        Assert.Equal(new[] { 0x74 }, restored.SaveStateShortcut.Keys);
        Assert.True(restored.LoadStateShortcut.IsEmpty);
        Assert.Equal(@"C:\Games\Game Boy", Assert.Single(restored.RomFolders));
    }

    [Theory]
    [InlineData(0.1f, 0.5f)]
    [InlineData(1.0f, 1.0f)]
    [InlineData(3.0f, 1.0f)]
    public void ElementScalesAreClamped(float configured, float expected)
    {
        var element = new EmulatorElementLayout { Scale = configured };

        Assert.Equal(expected, element.SafeScale);
    }

    [Fact]
    public void CoreSettingsAndLibrariesAreIndependent()
    {
        var root = new EmulatorSettings();
        root.MigrateToPerCoreSettings(EmulatorSystemCatalog.All);
        var gba = root.ForCore(EmulatorSystemCatalog.GameBoyAdvance);
        var n64 = root.ForCore(EmulatorSystemCatalog.Nintendo64);

        gba.VideoFilter = EmulatorVideoFilter.Smooth;
        gba.GameplayOrientation = EmulatorGameplayOrientation.Portrait;
        gba.RomFolders.Add(@"C:\Games\GBA");
        n64.CoreOptions["mupen64plus-pak1"] = "rumble";

        Assert.Equal(EmulatorVideoFilter.Pixel, n64.VideoFilter);
        Assert.Equal(EmulatorGameplayOrientation.Landscape, n64.GameplayOrientation);
        Assert.Empty(n64.RomFolders);
        Assert.Empty(gba.CoreOptions);
        Assert.Equal("rumble", n64.CoreOptions["mupen64plus-pak1"]);
    }

    [Fact]
    public void LandscapePhoneSizeIsTheProportionalPortraitRotation()
    {
        var portrait = PhoneSizeCatalog.SizeFor(PhoneSizeCatalog.DefaultScale);
        var landscape = PhoneSizeCatalog.LandscapeSizeFor(PhoneSizeCatalog.DefaultScale);

        Assert.Equal(portrait.Y, landscape.X);
        Assert.Equal(portrait.X, landscape.Y);
    }

    [Fact]
    public void EmulatorScreenFitPreservesAspectRatioAtTheViewportEdge()
    {
        var fitted = GameBoyApp.FitSizeWithin(new Vector2(600f, 500f), new Vector2(900f, 340f));

        Assert.Equal(408f, fitted.X, 3);
        Assert.Equal(340f, fitted.Y, 3);
        Assert.Equal(1.2f, fitted.X / fitted.Y, 3);
    }

    [Fact]
    public void RecentGamesMoveToTheFrontWithoutDuplicates()
    {
        var settings = new EmulatorSettings();
        settings.AddRecent(EmulatorSystemCatalog.GameBoy, @"C:\Games\one.gb");
        settings.AddRecent(EmulatorSystemCatalog.GameBoyAdvance, @"C:\Games\two.gba");
        settings.AddRecent(EmulatorSystemCatalog.GameBoy, @"C:\Games\one.gb");

        Assert.Equal(2, settings.RecentGames.Count);
        Assert.Equal("gb", settings.RecentGames[0].SystemId);
        Assert.Equal(@"C:\Games\one.gb", settings.RecentGames[0].Path);
    }
}
