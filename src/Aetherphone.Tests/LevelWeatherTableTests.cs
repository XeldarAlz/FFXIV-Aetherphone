using System.Buffers.Binary;
using Aetherphone.Core.Game;
using Xunit;

namespace Aetherphone.Tests;

public sealed class LevelWeatherTableTests
{
    private const int SceneStart = 0x0C;
    private const int SettingsStart = 0x40;
    private const int TableStart = 0x100;

    [Fact]
    public void ReadsTheWeatherTableAndSkipsEmptyAndRepeatedSlots()
    {
        var level = Level(new ushort[] { 1, 2, 0, 2, 15, 255, 4 });
        var weathers = new List<byte>();

        LevelWeatherTable.Read(level, weathers);

        Assert.Equal(new byte[] { 1, 2, 15, 4 }, weathers);
    }

    [Fact]
    public void ReturnsNothingWhenTheFileIsTooShortForItsPointers()
    {
        var weathers = new List<byte> { 9 };

        LevelWeatherTable.Read(new byte[0x20], weathers);

        Assert.Empty(weathers);
    }

    [Fact]
    public void ReturnsNothingWhenThePointersRunPastTheEnd()
    {
        var level = Level(new ushort[] { 1 });
        BinaryPrimitives.WriteInt32LittleEndian(level.AsSpan(SettingsStart + 0x40), int.MaxValue - 8);
        var weathers = new List<byte>();

        LevelWeatherTable.Read(level, weathers);

        Assert.Empty(weathers);
    }

    private static byte[] Level(ushort[] ids)
    {
        var level = new byte[TableStart + LevelWeatherTable.Capacity * 2];
        level[SceneStart] = (byte)'S';
        level[SceneStart + 1] = (byte)'C';
        level[SceneStart + 2] = (byte)'N';
        level[SceneStart + 3] = (byte)'1';
        BinaryPrimitives.WriteInt32LittleEndian(level.AsSpan(SceneStart + 0x10), SettingsStart - SceneStart - 8);
        BinaryPrimitives.WriteInt32LittleEndian(level.AsSpan(SettingsStart + 0x40), TableStart - SettingsStart);
        for (var index = 0; index < ids.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(level.AsSpan(TableStart + index * 2), ids[index]);
        }

        return level;
    }
}
