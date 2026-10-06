namespace Aetherphone.Apps.Games.Mahjong;

internal readonly struct LayoutBlock
{
    public readonly int Layer;
    public readonly int X;
    public readonly int Y;
    public readonly int Columns;
    public readonly int Rows;

    public LayoutBlock(int layer, int x, int y, int columns, int rows)
    {
        Layer = layer;
        X = x;
        Y = y;
        Columns = columns;
        Rows = rows;
    }
}

internal sealed class MahjongLayout
{
    public const int MaxTiles = 144;
    public const int TileSpan = 2;

    private readonly byte[] tileX;
    private readonly byte[] tileY;
    private readonly byte[] tileLayer;
    private readonly short[] drawOrder;
    private readonly int[] aboveStart;
    private readonly short[] aboveItems;
    private readonly int[] belowStart;
    private readonly short[] belowItems;
    private readonly int[] leftStart;
    private readonly short[] leftItems;
    private readonly int[] rightStart;
    private readonly short[] rightItems;

    public MahjongLayout(ReadOnlySpan<LayoutBlock> blocks)
    {
        var count = 0;
        for (var blockIndex = 0; blockIndex < blocks.Length; blockIndex++)
        {
            count += blocks[blockIndex].Columns * blocks[blockIndex].Rows;
        }

        Count = Math.Min(count, MaxTiles);
        tileX = new byte[Count];
        tileY = new byte[Count];
        tileLayer = new byte[Count];
        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var tile = 0;
        for (var blockIndex = 0; blockIndex < blocks.Length && tile < Count; blockIndex++)
        {
            var block = blocks[blockIndex];
            for (var row = 0; row < block.Rows && tile < Count; row++)
            {
                for (var column = 0; column < block.Columns && tile < Count; column++)
                {
                    var x = block.X + column * TileSpan;
                    var y = block.Y + row * TileSpan;
                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    tileX[tile] = (byte)x;
                    tileY[tile] = (byte)y;
                    tileLayer[tile] = (byte)block.Layer;
                    tile++;
                }
            }
        }

        var maxX = 0;
        var maxY = 0;
        var maxLayer = 0;
        for (var index = 0; index < Count; index++)
        {
            tileX[index] = (byte)(tileX[index] - minX);
            tileY[index] = (byte)(tileY[index] - minY);
            maxX = Math.Max(maxX, tileX[index]);
            maxY = Math.Max(maxY, tileY[index]);
            maxLayer = Math.Max(maxLayer, tileLayer[index]);
        }

        Width = maxX + TileSpan;
        Height = maxY + TileSpan;
        Layers = maxLayer + 1;
        drawOrder = BuildDrawOrder();
        aboveStart = new int[Count + 1];
        belowStart = new int[Count + 1];
        leftStart = new int[Count + 1];
        rightStart = new int[Count + 1];
        aboveItems = Link(aboveStart, NeighbourKind.Above);
        belowItems = Link(belowStart, NeighbourKind.Below);
        leftItems = Link(leftStart, NeighbourKind.Left);
        rightItems = Link(rightStart, NeighbourKind.Right);
    }

    private enum NeighbourKind : byte
    {
        Above,
        Below,
        Left,
        Right,
    }

    public int Count { get; }

    public int Width { get; }

    public int Height { get; }

    public int Layers { get; }

    public int X(int tile) => tileX[tile];

    public int Y(int tile) => tileY[tile];

    public int Layer(int tile) => tileLayer[tile];

    public int DrawOrder(int index) => drawOrder[index];

    public ReadOnlySpan<short> Above(int tile) => Slice(aboveItems, aboveStart, tile);

    public ReadOnlySpan<short> Below(int tile) => Slice(belowItems, belowStart, tile);

    public ReadOnlySpan<short> Left(int tile) => Slice(leftItems, leftStart, tile);

    public ReadOnlySpan<short> Right(int tile) => Slice(rightItems, rightStart, tile);

    public bool Overlaps(int first, int second) =>
        Math.Abs(tileX[first] - tileX[second]) < TileSpan && Math.Abs(tileY[first] - tileY[second]) < TileSpan;

    private static ReadOnlySpan<short> Slice(short[] items, int[] start, int tile) =>
        items.AsSpan(start[tile], start[tile + 1] - start[tile]);

    private short[] BuildDrawOrder()
    {
        var order = new short[Count];
        for (var index = 0; index < Count; index++)
        {
            order[index] = (short)index;
        }

        for (var index = 1; index < Count; index++)
        {
            var current = order[index];
            var slot = index - 1;
            while (slot >= 0 && DrawsAfter(order[slot], current))
            {
                order[slot + 1] = order[slot];
                slot--;
            }

            order[slot + 1] = current;
        }

        return order;
    }

    private bool DrawsAfter(int first, int second)
    {
        if (tileLayer[first] != tileLayer[second])
        {
            return tileLayer[first] > tileLayer[second];
        }

        if (tileY[first] != tileY[second])
        {
            return tileY[first] > tileY[second];
        }

        return tileX[first] > tileX[second];
    }

    private short[] Link(int[] start, NeighbourKind kind)
    {
        var total = 0;
        for (var tile = 0; tile < Count; tile++)
        {
            start[tile] = total;
            for (var other = 0; other < Count; other++)
            {
                if (IsNeighbour(tile, other, kind))
                {
                    total++;
                }
            }
        }

        start[Count] = total;
        var items = new short[total];
        var cursor = 0;
        for (var tile = 0; tile < Count; tile++)
        {
            for (var other = 0; other < Count; other++)
            {
                if (IsNeighbour(tile, other, kind))
                {
                    items[cursor++] = (short)other;
                }
            }
        }

        return items;
    }

    private bool IsNeighbour(int tile, int other, NeighbourKind kind)
    {
        if (tile == other)
        {
            return false;
        }

        var sameRowBand = Math.Abs(tileY[tile] - tileY[other]) < TileSpan;
        return kind switch
        {
            NeighbourKind.Above => tileLayer[other] > tileLayer[tile] && Overlaps(tile, other),
            NeighbourKind.Below => tileLayer[other] < tileLayer[tile] && Overlaps(tile, other),
            NeighbourKind.Left => tileLayer[other] == tileLayer[tile] && sameRowBand &&
                                  tileX[other] == tileX[tile] - TileSpan,
            _ => tileLayer[other] == tileLayer[tile] && sameRowBand && tileX[other] == tileX[tile] + TileSpan,
        };
    }
}

internal static class MahjongLayouts
{
    public const int PerTier = 2;

    public static readonly MahjongLayout Moogle = new(new LayoutBlock[]
    {
        new(0, 6, 0, 2, 1), new(0, 4, 2, 4, 1), new(0, 0, 4, 8, 3), new(0, 2, 10, 6, 1), new(0, 4, 12, 4, 1),
        new(1, 2, 4, 6, 4), new(2, 4, 6, 4, 2),
    });

    public static readonly MahjongLayout Bridge = new(new LayoutBlock[]
    {
        new(0, 0, 0, 2, 7), new(0, 12, 0, 2, 7), new(0, 4, 4, 4, 3), new(1, 0, 2, 2, 5), new(1, 12, 2, 2, 5),
        new(1, 4, 6, 4, 1), new(2, 1, 3, 1, 4), new(2, 13, 3, 1, 4),
    });

    public static readonly MahjongLayout Tower = new(new LayoutBlock[]
    {
        new(0, 0, 0, 6, 8), new(1, 1, 1, 5, 7), new(2, 2, 2, 4, 6), new(3, 5, 7, 1, 1),
    });

    public static readonly MahjongLayout Crossroads = new(new LayoutBlock[]
    {
        new(0, 0, 6, 9, 3), new(0, 6, 0, 3, 3), new(0, 6, 12, 3, 3), new(1, 2, 6, 7, 3), new(1, 6, 2, 3, 2),
        new(1, 6, 12, 3, 2), new(2, 4, 6, 5, 3), new(2, 6, 4, 3, 1), new(2, 6, 12, 3, 1), new(3, 6, 6, 3, 3),
    });

    public static readonly MahjongLayout Fortress = new(new LayoutBlock[]
    {
        new(0, 0, 0, 8, 8), new(1, 0, 0, 8, 1), new(1, 0, 14, 8, 1), new(1, 0, 2, 1, 6), new(1, 14, 2, 1, 6),
        new(1, 4, 4, 4, 4), new(2, 4, 4, 4, 4), new(2, 0, 0, 2, 1), new(2, 6, 0, 2, 1), new(2, 12, 0, 2, 1),
        new(2, 0, 14, 2, 1), new(2, 6, 14, 2, 1), new(2, 12, 14, 2, 1), new(2, 0, 2, 1, 1), new(2, 0, 6, 1, 2),
        new(2, 0, 12, 1, 1), new(2, 14, 2, 1, 1), new(2, 14, 6, 1, 2), new(2, 14, 12, 1, 1),
    });

    public static readonly MahjongLayout Dragon = new(new LayoutBlock[]
    {
        new(0, 0, 2, 9, 6), new(0, 6, 0, 3, 1), new(0, 6, 14, 3, 1), new(1, 1, 3, 8, 5), new(2, 2, 4, 7, 4),
        new(3, 5, 5, 4, 3), new(4, 7, 6, 2, 2),
    });

    public static readonly MahjongLayout[] All = { Moogle, Bridge, Tower, Crossroads, Fortress, Dragon };

    public static int IndexFor(int mode, int variant) =>
        Math.Clamp(mode, 0, All.Length / PerTier - 1) * PerTier + Math.Clamp(variant, 0, PerTier - 1);
}
