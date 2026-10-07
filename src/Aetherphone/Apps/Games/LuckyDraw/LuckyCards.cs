namespace Aetherphone.Apps.Games.LuckyDraw;

internal static class LuckyCards
{
    public const byte MaxNumber = 12;
    public const byte Freeze = 13;
    public const byte FlipThree = 14;
    public const byte SecondChance = 15;
    public const byte PlusTwo = 16;
    public const byte PlusFour = 17;
    public const byte PlusSix = 18;
    public const byte PlusEight = 19;
    public const byte PlusTen = 20;
    public const byte Times = 21;
    public const int FaceCount = 22;
    public const int ActionCopies = 3;
    public const int NumberCards = 79;
    public const int DeckSize = 94;

    public static bool IsNumber(int face) => face >= 0 && face <= MaxNumber;

    public static bool IsTargeted(int face) => face is Freeze or FlipThree;

    public static bool IsPlus(int face) => face >= PlusTwo && face <= PlusTen;

    public static bool IsModifier(int face) => face >= PlusTwo && face <= Times;

    public static int PlusValue(int face) => IsPlus(face) ? (face - PlusTwo + 1) * 2 : 0;

    public static int CopiesOf(int face)
    {
        if (face == 0)
        {
            return 1;
        }

        if (IsNumber(face))
        {
            return face;
        }

        return face <= SecondChance ? ActionCopies : 1;
    }

    public static int Fill(Span<byte> deck)
    {
        var count = 0;
        for (var face = 0; face < FaceCount; face++)
        {
            var copies = CopiesOf(face);
            for (var copy = 0; copy < copies && count < deck.Length; copy++)
            {
                deck[count++] = (byte)face;
            }
        }

        return count;
    }
}
