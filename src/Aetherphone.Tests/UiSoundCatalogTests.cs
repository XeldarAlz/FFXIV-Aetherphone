using Aetherphone.Core.Notifications;
using Xunit;

namespace Aetherphone.Tests;

public sealed class UiSoundCatalogTests
{
    [Fact]
    public void EveryUiSoundHasACatalogEntry()
    {
        var sounds = Enum.GetValues<UiSound>();
        Assert.Equal(sounds.Length, UiSoundCatalog.Entries.Length);
    }

    [Fact]
    public void EveryEntryHasFilesAndSaneLimits()
    {
        for (var entryIndex = 0; entryIndex < UiSoundCatalog.Entries.Length; entryIndex++)
        {
            var entry = UiSoundCatalog.Entries[entryIndex];
            Assert.NotEmpty(entry.Files);
            Assert.InRange(entry.Gain, 0f, 1f);
            Assert.InRange(entry.MinimumIntervalMilliseconds, 1, 10000);
            for (var fileIndex = 0; fileIndex < entry.Files.Length; fileIndex++)
            {
                Assert.EndsWith(".wav", entry.Files[fileIndex], StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void EveryCatalogFileShipsWithThePlugin()
    {
        var root = Path.Combine(FindProjectRoot(), "src", "Aetherphone", "Sounds");
        var files = UiSoundCatalog.Files();
        for (var index = 0; index < files.Count; index++)
        {
            Assert.True(File.Exists(Path.Combine(root, files[index])), $"missing bundled clip {files[index]}");
        }
    }

    [Fact]
    public void ChannelsMatchTheEventMap()
    {
        Assert.Equal(UiSoundChannel.Event, UiSoundCatalog.Entries[(int)UiSound.Sleep].Channel);
        Assert.Equal(UiSoundChannel.Event, UiSoundCatalog.Entries[(int)UiSound.Shutter].Channel);
        Assert.Equal(UiSoundChannel.Transition, UiSoundCatalog.Entries[(int)UiSound.AppOpen].Channel);
        Assert.Equal(UiSoundChannel.Transition, UiSoundCatalog.Entries[(int)UiSound.AppClose].Channel);
        Assert.Equal(UiSoundChannel.Tap, UiSoundCatalog.Entries[(int)UiSound.Tap].Channel);
        Assert.Equal(UiSoundChannel.Toggle, UiSoundCatalog.Entries[(int)UiSound.ToggleOn].Channel);
        Assert.Equal(UiSoundChannel.Toggle, UiSoundCatalog.Entries[(int)UiSound.ToggleOff].Channel);
        Assert.Equal(UiSoundChannel.Keyboard, UiSoundCatalog.Entries[(int)UiSound.Keystroke].Channel);
        Assert.Equal(UiSoundChannel.Event, UiSoundCatalog.Entries[(int)UiSound.GameWin].Channel);
        Assert.Equal(UiSoundChannel.Game, UiSoundCatalog.Entries[(int)UiSound.GameHitSoft].Channel);
        Assert.Equal(UiSoundChannel.Game, UiSoundCatalog.Entries[(int)UiSound.SimonTone4].Channel);
    }

    [Fact]
    public void CasinoCuesAreGameChannelAndBigWinsThrottle()
    {
        Assert.True(UiSoundCatalog.Entries[(int)UiSound.WinBig].MinimumIntervalMilliseconds >= 10000);
        Assert.True(UiSoundCatalog.Entries[(int)UiSound.WinEpic].MinimumIntervalMilliseconds >= 10000);
        Assert.True(UiSoundCatalog.Entries[(int)UiSound.Fanfare].MinimumIntervalMilliseconds >= 10000);
        for (var sound = (int)UiSound.ReelTick; sound <= (int)UiSound.LevelUp; sound++)
        {
            Assert.Equal(UiSoundChannel.Game, UiSoundCatalog.Entries[sound].Channel);
        }
    }

    [Fact]
    public void SimonTonesAreContiguousSingleFiles()
    {
        Assert.Equal((int)UiSound.SimonTone1 + 1, (int)UiSound.SimonTone2);
        Assert.Equal((int)UiSound.SimonTone1 + 2, (int)UiSound.SimonTone3);
        Assert.Equal((int)UiSound.SimonTone1 + 3, (int)UiSound.SimonTone4);
        for (var pad = 0; pad < 4; pad++)
        {
            Assert.Single(UiSoundCatalog.Entries[(int)UiSound.SimonTone1 + pad].Files);
        }
    }

    private static string FindProjectRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Aetherphone.sln")))
        {
            current = current.Parent;
        }

        Assert.NotNull(current);
        return current.FullName;
    }
}
