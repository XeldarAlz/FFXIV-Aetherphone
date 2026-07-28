using Aetherphone.Core.Emulation;
using Xunit;

namespace Aetherphone.Tests;

public sealed class EmulatorStateStoreTests
{
    [Fact]
    public void ManualAndAutomaticStatesUseSeparateFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "AetherphoneStateTests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new EmulatorStateStore(root, Path.Combine(root, "Pokemon.gba"));
            store.WriteSlot(1, new byte[] { 1, 2, 3 });
            store.WriteAuto(new byte[] { 4, 5, 6 });

            Assert.True(store.HasSlot(1));
            Assert.True(store.HasAuto);
            Assert.Equal(new byte[] { 1, 2, 3 }, store.ReadSlot(1));
            Assert.Equal(new byte[] { 4, 5, 6 }, store.ReadAuto());
            Assert.NotEqual(store.SlotPath(1), store.AutoPath);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void RejectsSlotsOutsideTheVisibleRange(int slot)
    {
        var root = Path.Combine(Path.GetTempPath(), "AetherphoneStateTests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new EmulatorStateStore(root, Path.Combine(root, "game.gba"));
            Assert.Throws<ArgumentOutOfRangeException>(() => store.SlotPath(slot));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }


    [Fact]
    public void StatesFromDifferentCoresDoNotCollide()
    {
        var root = Path.Combine(Path.GetTempPath(), "AetherphoneStateTests", Guid.NewGuid().ToString("N"));
        try
        {
            var rom = Path.Combine(root, "Pokemon.gba");
            var gpsp = new EmulatorStateStore(root, rom, "gpsp_libretro");
            var other = new EmulatorStateStore(root, rom, "other_core");

            Assert.NotEqual(gpsp.AutoPath, other.AutoPath);
            Assert.NotEqual(gpsp.SlotPath(1), other.SlotPath(1));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
