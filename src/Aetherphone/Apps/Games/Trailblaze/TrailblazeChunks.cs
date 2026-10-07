namespace Aetherphone.Apps.Games.Trailblaze;

internal enum TrailblazeCell : byte
{
    Open,
    Barrier,
    Gap,
    Cart,
}

internal enum TrailblazeLoot : byte
{
    None,
    Coins,
    Arc,
    Power,
}

internal static class TrailblazeChunks
{
    public const int Rows = 10;
    public const int Lanes = 3;
    public const int CellsPerChunk = Rows * Lanes;
    public const int TierCount = 3;
    private const int RowLength = Lanes * 2 + 1;
    private const char Separator = ' ';

    private static readonly string[][] Layouts =
    {
        new[]
        {
            "... .o.", "... .o.", "... .o.", "... ...", "... o..", "... o..", "... ...", "... ..o", "... ..o", "... ...",
        },
        new[]
        {
            "... ...", "... .o.", "... .o.", ".B. .o.", "... ...", "... o..", "B.. o..", "... ...", "... ..o", "... ...",
        },
        new[]
        {
            "... ...", "... .^.", ".G. .^.", "... .^.", "... ...", "... ...", "... ^..", "G.. ^..", "... ^..", "... ...",
        },
        new[]
        {
            "... ...", "... .o.", "C.. .o.", "C.. .o.", "... ...", "..C o..", "..C o..", "..C ...", "... .o.", "... ...",
        },
        new[]
        {
            "... ...", "... o.o", "... o.o", "... ...", "... .P.", "... ...", "B.B .o.", "... .o.", "... .o.", "... ...",
        },
        new[]
        {
            "... o..", "... o..", ".C. o..", ".C. ...", "... .o.", "... ..o", ".C. ..o", ".C. ..o", "... .o.", "... ...",
        },
        new[]
        {
            "... ...", "BB. ..o", "... ..o", "... ...", "... o..", ".BB o..", "... ...", "... .o.", "... .o.", "... ...",
        },
        new[]
        {
            "... ^..", "G.. ^..", "... ^..", "... ...", "... ..^", "..G ..^", "... ..^", "... ...", "... .o.", "... .o.",
        },
        new[]
        {
            "... ...", "... .o.", "C.C .o.", "C.C .o.", "C.C .o.", "... ...", ".B. o.o", "... o.o", "... ...", "... ...",
        },
        new[]
        {
            "... ...", "... .^.", "BG. .^.", "... .^.", "... ...", "..B ..o", ".G. ..o", "... ...", "C.. .o.", "... ...",
        },
        new[]
        {
            "... ...", "C.C .o.", "C.C .o.", "C.C ...", ".B. ...", "... .o.", "C.C .o.", "C.C .o.", "... ...", "... ...",
        },
        new[]
        {
            "... ^..", "G.. ^..", "... ^.^", "..G ..^", "... .^^", ".G. .^.", "... .^.", "... ...", "... o.o", "... o.o",
        },
        new[]
        {
            "... ...", "..C o..", "..C o..", "B.C ...", "... .o.", "C.. .o.", "C.B ...", "C.. ..o", "... ..o", "... ...",
        },
        new[]
        {
            "... ...", "C.C .o.", "C.C .P.", "C.C .o.", "... ^^^", "GGG ^^^", "... ^^^", "... ...", "... ...", "... ...",
        },
        new[]
        {
            "... ...", ".C. o..", ".C. o..", "... .o.", "C.. .o.", "C.C .o.", "..C .o.", ".C. o.o", "... o.o", "... ...",
        },
        new[]
        {
            "... ...", "BBB ...", "... .o.", "... .o.", "... .^.", ".G. .^.", "... .^.", "BBB ...", "... o.o", "... ...",
        },
        new[]
        {
            "... ...", "C.C .o.", "C.C .o.", "CBC ...", "C.C .^.", "CGC .^.", "C.C .^.", "... ...", "... o.o", "... o.o",
        },
        new[]
        {
            "... ...", "..C o..", ".CC o..", ".CC o..", "... .o.", "C.. ..o", "CC. ..o", "CC. ..o", "... .o.", "... ...",
        },
        new[]
        {
            "... ...", "... ^^^", "GGG ^^^", "... ^^^", "... ...", "BBB ...", "... .o.", ".C. o.o", ".C. o.o", "... ...",
        },
        new[]
        {
            "... ...", "C.. .o.", "C.C .o.", "..C o..", ".CC o..", ".C. ...", "... .o.", "B.B .^.", ".G. .^.", "... .^.",
        },
        new[]
        {
            "... ...", ".B. ^.^", "G.G ^.^", ".B. ^.^", "... ...", "C.. .^.", "CG. .^.", "C.. .^.", "..C ...", "... ...",
        },
        new[]
        {
            "... ...", "BBB ...", "... ^^^", "GGG ^^^", "... ^^^", "... ...", "C.C .o.", "CBC .o.", "C.C ...", "... ...",
        },
        new[]
        {
            "... ^..", "G.C ^..", "..C ^..", "C.. ..^", "C.G ..^", "... ..^", ".C. ^..", "GC. ^..", ".C. ^..", "... ...",
        },
        new[]
        {
            "... ...", "C.C .o.", "C.C .o.", "... .P.", ".B. ...", "B.B .o.", ".B. o.o", "C.C .o.", "C.C .o.", "... ...",
        },
    };

    private static readonly byte[] Tiers =
    {
        0, 0, 0, 0, 0, 0, 0, 0,
        1, 1, 1, 1, 1, 1, 1, 1,
        2, 2, 2, 2, 2, 2, 2, 2,
    };

    private static readonly TrailblazeCell[] CellTable = BuildCells();
    private static readonly TrailblazeLoot[] LootTable = BuildLoot();

    public static int Count => Layouts.Length;

    public static int Tier(int chunk) => Tiers[chunk];

    public static TrailblazeCell Cell(int chunk, int row, int lane) => CellTable[Index(chunk, row, lane)];

    public static TrailblazeLoot Loot(int chunk, int row, int lane) => LootTable[Index(chunk, row, lane)];

    public static bool IsWellFormed(int chunk)
    {
        var rows = Layouts[chunk];
        if (rows.Length != Rows)
        {
            return false;
        }

        for (var row = 0; row < rows.Length; row++)
        {
            var text = rows[row];
            if (text.Length != RowLength || text[Lanes] != Separator)
            {
                return false;
            }

            for (var lane = 0; lane < Lanes; lane++)
            {
                if (!TryCell(text[lane], out _) || !TryLoot(text[Lanes + 1 + lane], out _))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static int Index(int chunk, int row, int lane) => chunk * CellsPerChunk + row * Lanes + lane;

    private static TrailblazeCell[] BuildCells()
    {
        var cells = new TrailblazeCell[Layouts.Length * CellsPerChunk];
        for (var chunk = 0; chunk < Layouts.Length; chunk++)
        {
            var rows = Layouts[chunk];
            for (var row = 0; row < Rows && row < rows.Length; row++)
            {
                for (var lane = 0; lane < Lanes && lane < rows[row].Length; lane++)
                {
                    TryCell(rows[row][lane], out var cell);
                    cells[Index(chunk, row, lane)] = cell;
                }
            }
        }

        return cells;
    }

    private static TrailblazeLoot[] BuildLoot()
    {
        var loot = new TrailblazeLoot[Layouts.Length * CellsPerChunk];
        for (var chunk = 0; chunk < Layouts.Length; chunk++)
        {
            var rows = Layouts[chunk];
            for (var row = 0; row < Rows && row < rows.Length; row++)
            {
                for (var lane = 0; lane < Lanes && Lanes + 1 + lane < rows[row].Length; lane++)
                {
                    TryLoot(rows[row][Lanes + 1 + lane], out var item);
                    loot[Index(chunk, row, lane)] = item;
                }
            }
        }

        return loot;
    }

    private static bool TryCell(char symbol, out TrailblazeCell cell)
    {
        cell = symbol switch
        {
            'B' => TrailblazeCell.Barrier,
            'G' => TrailblazeCell.Gap,
            'C' => TrailblazeCell.Cart,
            _ => TrailblazeCell.Open,
        };
        return symbol is '.' or 'B' or 'G' or 'C';
    }

    private static bool TryLoot(char symbol, out TrailblazeLoot loot)
    {
        loot = symbol switch
        {
            'o' => TrailblazeLoot.Coins,
            '^' => TrailblazeLoot.Arc,
            'P' => TrailblazeLoot.Power,
            _ => TrailblazeLoot.None,
        };
        return symbol is '.' or 'o' or '^' or 'P';
    }
}
