namespace Aetherphone.Apps.Games.Framework.World;

internal sealed class TerrainPainter
{
    public const int BytesPerPixel = 4;
    public const int EdgeCells = 2;
    public const int TopsoilCells = 10;
    private const float GradientStart = 0.2f;
    private const int StrataPeriod = 23;
    private const int StrataThickness = 3;
    private const float StrataShade = 0.8f;
    private const float StrataWaveCells = 4f;
    private const float StrataWaveLength = 57f;
    private const float StrataRippleLength = 21f;
    private const float StrataRippleShare = 0.35f;
    private const float EdgeBlend = 0.45f;
    private const int TopsoilBoost = 4;
    private const int UndersideShade = 179;
    private const int GrainBase = 240;
    private const uint GrainMask = 31u;
    private const uint GrainSeed = 0x2545F491u;
    private const int ShadeShift = 8;
    private const byte Opaque = 255;
    private const byte Transparent = 0;

    private readonly TerrainMask mask;
    private readonly byte[] pixels;
    private readonly int[] strataOffsets;
    private readonly uint[] bodyColours;
    private readonly uint[] strataColours;
    private uint edgeColour;
    private uint edgeInnerColour;

    public TerrainPainter(TerrainMask mask, TerrainMaterial material)
    {
        this.mask = mask;
        pixels = new byte[mask.Width * mask.Height * BytesPerPixel];
        bodyColours = new uint[mask.Height];
        strataColours = new uint[mask.Height];
        strataOffsets = new int[mask.Width];
        for (var column = 0; column < mask.Width; column++)
        {
            var wave = MathF.Sin(column / StrataWaveLength * MathF.Tau);
            var ripple = MathF.Sin(column / StrataRippleLength * MathF.Tau) * StrataRippleShare;
            strataOffsets[column] = (int)MathF.Round((wave + ripple) * StrataWaveCells);
        }

        SetMaterial(material);
    }

    public TerrainMaterial Material { get; private set; }

    public ReadOnlySpan<byte> Pixels => pixels;

    public Span<byte> Canvas => pixels;

    public CellRegion Whole => new(0, 0, mask.Width - 1, mask.Height - 1);

    public CellRegion Coverage(in CellRegion region) => new(Math.Max(0, region.MinColumn),
        Math.Max(0, region.MinRow - 1), Math.Min(mask.Width - 1, region.MaxColumn),
        Math.Min(mask.Height - 1, region.MaxRow + TopsoilCells));

    public void SetMaterial(TerrainMaterial material)
    {
        Material = material;
        var lastRow = Math.Max(1, mask.Height - 1);
        for (var row = 0; row < mask.Height; row++)
        {
            var amount = Math.Clamp((row / (float)lastRow - GradientStart) / (1f - GradientStart), 0f, 1f);
            var body = Vector4.Lerp(material.Surface, material.Deep, amount);
            bodyColours[row] = Pack(body);
            strataColours[row] = Pack(body * StrataShade);
        }

        edgeColour = Pack(material.Edge);
        edgeInnerColour = Pack(Vector4.Lerp(material.Edge, material.Surface, EdgeBlend));
        PaintAll();
    }

    public void Paint(in CellRegion region)
    {
        var painted = Coverage(region);
        for (var column = painted.MinColumn; column <= painted.MaxColumn; column++)
        {
            var depth = DepthAbove(column, painted.MinRow);
            for (var row = painted.MinRow; row <= painted.MaxRow; row++)
            {
                var offset = (row * mask.Width + column) * BytesPerPixel;
                if (!mask.IsSolid(column, row))
                {
                    WriteAir(offset);
                    depth = 0;
                    continue;
                }

                WriteSolid(offset, column, row, depth);
                depth = Math.Min(depth + 1, TopsoilCells);
            }
        }
    }

    private void PaintAll() => Paint(Whole);

    private int DepthAbove(int column, int row)
    {
        var depth = 0;
        for (var above = row - 1; above >= 0 && depth < TopsoilCells; above--)
        {
            if (!mask.IsSolid(column, above))
            {
                return depth;
            }

            depth++;
        }

        return TopsoilCells;
    }

    private void WriteSolid(int offset, int column, int row, int depth)
    {
        var colour = ColourAt(column, row, depth);
        var shade = GrainBase + (int)(TerrainNoise.Hash(column, row, GrainSeed) & GrainMask);
        if (depth >= EdgeCells)
        {
            shade += (TopsoilCells - depth) * TopsoilBoost;
        }

        if (depth >= EdgeCells && row + 1 < mask.Height && !mask.IsSolid(column, row + 1))
        {
            shade = (shade * UndersideShade) >> ShadeShift;
        }

        pixels[offset] = Shade(colour, 0, shade);
        pixels[offset + 1] = Shade(colour, 8, shade);
        pixels[offset + 2] = Shade(colour, 16, shade);
        pixels[offset + 3] = Opaque;
    }

    private uint ColourAt(int column, int row, int depth)
    {
        if (depth == 0)
        {
            return edgeColour;
        }

        if (depth < EdgeCells)
        {
            return edgeInnerColour;
        }

        return InStratum(column, row) ? strataColours[row] : bodyColours[row];
    }

    // Air keeps the edge colour at zero alpha so bilinear sampling fades the rim instead of darkening it.
    private void WriteAir(int offset)
    {
        pixels[offset] = (byte)edgeColour;
        pixels[offset + 1] = (byte)(edgeColour >> 8);
        pixels[offset + 2] = (byte)(edgeColour >> 16);
        pixels[offset + 3] = Transparent;
    }

    private bool InStratum(int column, int row)
    {
        var phase = (row + strataOffsets[column]) % StrataPeriod;
        if (phase < 0)
        {
            phase += StrataPeriod;
        }

        return phase < StrataThickness;
    }

    private static byte Shade(uint colour, int shift, int shade) =>
        (byte)Math.Min(255, ((int)((colour >> shift) & 0xFFu) * shade) >> ShadeShift);

    private static uint Pack(Vector4 colour) =>
        ToByte(colour.X) | ((uint)ToByte(colour.Y) << 8) | ((uint)ToByte(colour.Z) << 16);

    private static byte ToByte(float channel) => (byte)Math.Clamp((int)MathF.Round(channel * 255f), 0, 255);
}
