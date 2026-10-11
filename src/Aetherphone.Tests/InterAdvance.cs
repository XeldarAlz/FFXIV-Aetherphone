using System.Buffers.Binary;

namespace Aetherphone.Tests;

internal sealed class InterAdvance
{
    public const float DalamudFontPixels = 17f;
    public const float ItemSpacingY = 4f;

    private static readonly Lazy<InterAdvance> SemiBoldFont = new(() => Load("Inter-SemiBold.ttf"));

    private readonly byte[] data;
    private readonly int unitsPerEm;
    private readonly int metricCount;
    private readonly int metricsOffset;
    private readonly int mapOffset;

    private InterAdvance(byte[] data)
    {
        this.data = data;
        unitsPerEm = U16(Table("head") + 18);
        metricCount = U16(Table("hhea") + 34);
        metricsOffset = Table("hmtx");
        mapOffset = FormatFourMap();
    }

    public static InterAdvance SemiBold => SemiBoldFont.Value;

    public static float LineHeight(float scale) => DalamudFontPixels * scale + ItemSpacingY;

    public float Measure(string text, float scale)
    {
        var pixels = DalamudFontPixels * scale;
        var total = 0f;
        for (var index = 0; index < text.Length; index++)
        {
            var glyph = GlyphOf(text[index]);
            total += glyph == 0 ? pixels : AdvanceOf(glyph) * pixels / unitsPerEm;
        }

        return total;
    }

    private static InterAdvance Load(string name)
    {
        var directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            var candidate = Path.Combine(directory, "src", "Aetherphone", "Fonts", name);
            if (File.Exists(candidate))
            {
                return new InterAdvance(File.ReadAllBytes(candidate));
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new FileNotFoundException(name);
    }

    private int AdvanceOf(int glyph)
    {
        var slot = Math.Min(glyph, metricCount - 1);
        return U16(metricsOffset + slot * 4);
    }

    private int GlyphOf(char character)
    {
        var segmentCount = U16(mapOffset + 6) / 2;
        var endCodes = mapOffset + 14;
        var startCodes = endCodes + segmentCount * 2 + 2;
        var deltas = startCodes + segmentCount * 2;
        var rangeOffsets = deltas + segmentCount * 2;
        for (var segment = 0; segment < segmentCount; segment++)
        {
            if (U16(endCodes + segment * 2) < character)
            {
                continue;
            }

            var start = U16(startCodes + segment * 2);
            if (start > character)
            {
                return 0;
            }

            var delta = (short)U16(deltas + segment * 2);
            var rangeOffset = U16(rangeOffsets + segment * 2);
            if (rangeOffset == 0)
            {
                return (character + delta) & 0xFFFF;
            }

            var glyph = U16(rangeOffsets + segment * 2 + rangeOffset + (character - start) * 2);
            return glyph == 0 ? 0 : (glyph + delta) & 0xFFFF;
        }

        return 0;
    }

    private int FormatFourMap()
    {
        var cmap = Table("cmap");
        var count = U16(cmap + 2);
        for (var index = 0; index < count; index++)
        {
            var record = cmap + 4 + index * 8;
            var offset = cmap + (int)U32(record + 4);
            if (U16(offset) == 4)
            {
                return offset;
            }
        }

        throw new InvalidDataException("no format 4 cmap");
    }

    private int Table(string tag)
    {
        var count = U16(4);
        for (var index = 0; index < count; index++)
        {
            var record = 12 + index * 16;
            if (data[record] == tag[0] && data[record + 1] == tag[1] && data[record + 2] == tag[2]
                && data[record + 3] == tag[3])
            {
                return (int)U32(record + 8);
            }
        }

        throw new InvalidDataException(tag);
    }

    private int U16(int offset) => BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset, 2));

    private uint U32(int offset) => BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4));
}
