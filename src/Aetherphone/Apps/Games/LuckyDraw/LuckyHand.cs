namespace Aetherphone.Apps.Games.LuckyDraw;

internal static class LuckyHand
{
    public static int Score(ReadOnlySpan<byte> row, bool busted, bool seven)
    {
        if (busted)
        {
            return 0;
        }

        var numbers = 0;
        var plus = 0;
        var doubled = false;
        for (var slot = 0; slot < row.Length; slot++)
        {
            var face = row[slot];
            if (LuckyCards.IsNumber(face))
            {
                numbers += face;
            }
            else if (face == LuckyCards.Times)
            {
                doubled = true;
            }
            else
            {
                plus += LuckyCards.PlusValue(face);
            }
        }

        var score = (doubled ? numbers * 2 : numbers) + plus;
        return seven ? score + LuckyDrawBoard.SevenBonus : score;
    }

    public static int UniqueNumbers(ReadOnlySpan<byte> row)
    {
        var mask = 0;
        for (var slot = 0; slot < row.Length; slot++)
        {
            var face = row[slot];
            if (LuckyCards.IsNumber(face))
            {
                mask |= 1 << face;
            }
        }

        return BitOperations.PopCount((uint)mask);
    }
}
