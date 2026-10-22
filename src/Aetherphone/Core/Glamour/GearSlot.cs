namespace Aetherphone.Core.Glamour;

internal enum GearSlot : byte
{
    MainHand = 0,
    OffHand = 1,
    Head = 2,
    Body = 3,
    Hands = 4,
    Legs = 6,
    Feet = 7,
    Ears = 8,
    Neck = 9,
    Wrists = 10,
    RingRight = 11,
    RingLeft = 12,
}

internal static class GearSlots
{
    public const int None = -1;

    private const int RetiredWaistSlot = 5;
    private const byte GlamourerLeftRing = 14;

    private static readonly (string Token, GearSlot Slot)[] PathTokens =
    {
        ("_met", GearSlot.Head), ("_top", GearSlot.Body), ("_glv", GearSlot.Hands), ("_dwn", GearSlot.Legs),
        ("_sho", GearSlot.Feet), ("_ear", GearSlot.Ears), ("_nek", GearSlot.Neck), ("_wrs", GearSlot.Wrists),
        ("_rir", GearSlot.RingRight), ("_ril", GearSlot.RingLeft),
    };

    public static readonly GearSlot[] All =
    {
        GearSlot.MainHand, GearSlot.OffHand, GearSlot.Head, GearSlot.Body, GearSlot.Hands, GearSlot.Legs,
        GearSlot.Feet, GearSlot.Ears, GearSlot.Neck, GearSlot.Wrists, GearSlot.RingRight, GearSlot.RingLeft,
    };

    public static bool IsGear(int slot) => slot is >= 0 and <= (int)GearSlot.RingLeft && slot != RetiredWaistSlot;

    public static byte GlamourerSlot(GearSlot slot) =>
        slot == GearSlot.RingLeft ? GlamourerLeftRing : (byte)((byte)slot + 1);

    public static int FromGamePath(string gamePath)
    {
        var path = gamePath.AsSpan();
        if (path.StartsWith("chara/weapon/", StringComparison.OrdinalIgnoreCase))
        {
            return (int)GearSlot.MainHand;
        }

        if (!path.StartsWith("chara/equipment/", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith("chara/accessory/", StringComparison.OrdinalIgnoreCase))
        {
            return None;
        }

        var file = path[(path.LastIndexOf('/') + 1)..];
        for (var index = 0; index < PathTokens.Length; index++)
        {
            if (file.Contains(PathTokens[index].Token, StringComparison.OrdinalIgnoreCase))
            {
                return (int)PathTokens[index].Slot;
            }
        }

        return None;
    }
}
