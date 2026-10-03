using System.Buffers.Binary;

namespace Aetherphone.Core.Game;

internal static class LevelWeatherTable
{
    public const int Capacity = 32;
    private const int SceneOffset = 0x0C;
    private const int PrefixedSceneSkip = 0x14;
    private const int SettingsPointerOffset = 0x10;
    private const int ChunkHeaderSize = 0x08;
    private const int WeatherPointerOffset = 0x40;
    private const int PointerSize = 4;
    private const int EntrySize = 2;
    private const ushort LastWeatherId = 255;

    public static void Read(ReadOnlySpan<byte> level, List<byte> into)
    {
        into.Clear();
        var scene = IsSceneMagic(level, SceneOffset) ? SceneOffset : SceneOffset + PrefixedSceneSkip;
        if (!Fits(level, scene + SettingsPointerOffset, PointerSize))
        {
            return;
        }

        var settings = scene + ChunkHeaderSize + ReadPointer(level, scene + SettingsPointerOffset);
        if (!Fits(level, settings + WeatherPointerOffset, PointerSize))
        {
            return;
        }

        var table = settings + ReadPointer(level, settings + WeatherPointerOffset);
        if (!Fits(level, table, Capacity * EntrySize))
        {
            return;
        }

        for (var index = 0; index < Capacity; index++)
        {
            var id = BinaryPrimitives.ReadUInt16LittleEndian(level.Slice(table + index * EntrySize, EntrySize));
            if (id == 0 || id >= LastWeatherId || into.Contains((byte)id))
            {
                continue;
            }

            into.Add((byte)id);
        }
    }

    private static bool IsSceneMagic(ReadOnlySpan<byte> level, int offset) =>
        Fits(level, offset, 4) && level[offset] == 'S' && level[offset + 1] == 'C' && level[offset + 2] == 'N' &&
        level[offset + 3] == '1';

    private static int ReadPointer(ReadOnlySpan<byte> level, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(level.Slice(offset, PointerSize));

    private static bool Fits(ReadOnlySpan<byte> level, int offset, int length) =>
        offset >= 0 && length >= 0 && offset <= level.Length - length;
}
