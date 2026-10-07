namespace Aetherphone.Apps.Games.Tempo;

internal enum TempoItem : byte
{
    None,
    Spike,
    CeilingSpike,
    Pad,
    CeilingPad,
    GravityUp,
    GravityDown,
}

internal sealed class TempoLevel
{
    public const int RowsPerSegment = 4;
    public const int CoinCount = 3;
    public const sbyte Pit = -1;
    public const sbyte Open = -1;

    private readonly sbyte[] ground;
    private readonly sbyte[] ceiling;
    private readonly TempoItem[] items;
    private readonly sbyte[] coins;
    private readonly int[] coinColumns = new int[CoinCount];

    public TempoLevel(int bpm, float speed, params string[] segments)
    {
        Bpm = bpm;
        Speed = speed;
        var length = 0;
        for (var segment = 0; segment + RowsPerSegment <= segments.Length; segment += RowsPerSegment)
        {
            length += segments[segment].Length;
        }

        Length = length;
        ground = new sbyte[length];
        ceiling = new sbyte[length];
        items = new TempoItem[length];
        coins = new sbyte[length];
        var column = 0;
        for (var segment = 0; segment + RowsPerSegment <= segments.Length; segment += RowsPerSegment)
        {
            var groundRow = segments[segment];
            var ceilingRow = segments[segment + 1];
            var itemRow = segments[segment + 2];
            var coinRow = segments[segment + 3];
            if (ceilingRow.Length != groundRow.Length || itemRow.Length != groundRow.Length ||
                coinRow.Length != groundRow.Length)
            {
                Malformed = true;
            }

            for (var offset = 0; offset < groundRow.Length; offset++)
            {
                var index = column + offset;
                var ceilingCell = offset < ceilingRow.Length ? ceilingRow[offset] : '.';
                var itemCell = offset < itemRow.Length ? itemRow[offset] : '.';
                var coinCell = offset < coinRow.Length ? coinRow[offset] : '.';
                ground[index] = groundRow[offset] == '_' ? Pit : Height(groundRow[offset]);
                ceiling[index] = Height(ceilingCell);
                items[index] = ItemFor(itemCell);
                coins[index] = Height(coinCell);
                if ((ground[index] == Pit && groundRow[offset] != '_') || (ceiling[index] == Open && ceilingCell != '.') ||
                    (items[index] == TempoItem.None && itemCell != '.') || (coins[index] == Open && coinCell != '.'))
                {
                    Malformed = true;
                }

                if (coins[index] >= 0)
                {
                    if (CoinTotal < CoinCount)
                    {
                        coinColumns[CoinTotal] = index;
                    }

                    CoinTotal++;
                }
            }

            column += groundRow.Length;
        }
    }

    public int Bpm { get; }

    public float Speed { get; }

    public int Length { get; }

    public int CoinTotal { get; }

    public bool Malformed { get; }

    public float TilesPerBeat => Speed * 60f / Bpm;

    public float Seconds => Length / Speed;

    public sbyte Ground(int column) => column >= 0 && column < Length ? ground[column] : (sbyte)(column < 0 ? ground[0] : ground[Length - 1]);

    public sbyte Ceiling(int column) => column >= 0 && column < Length ? ceiling[column] : (sbyte)(column < 0 ? ceiling[0] : ceiling[Length - 1]);

    public TempoItem Item(int column) => column >= 0 && column < Length ? items[column] : TempoItem.None;

    public sbyte Coin(int column) => column >= 0 && column < Length ? coins[column] : Open;

    public int CoinColumn(int index) => coinColumns[index];

    private static sbyte Height(char cell)
    {
        if (cell >= '0' && cell <= '9')
        {
            return (sbyte)(cell - '0');
        }

        if (cell >= 'a' && cell <= 'f')
        {
            return (sbyte)(cell - 'a' + 10);
        }

        return Open;
    }

    private static TempoItem ItemFor(char cell) => cell switch
    {
        '^' => TempoItem.Spike,
        'v' => TempoItem.CeilingSpike,
        'p' => TempoItem.Pad,
        'q' => TempoItem.CeilingPad,
        'u' => TempoItem.GravityUp,
        'd' => TempoItem.GravityDown,
        _ => TempoItem.None,
    };
}
