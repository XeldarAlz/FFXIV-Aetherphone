using Aetherphone.Core.Aethernet.Contracts;
using Newtonsoft.Json.Linq;

namespace Aetherphone.Core.Glamour;

internal static class GlamourerDesign
{
    // Glamourer stores empty slots as reserved ids just under uint.MaxValue (ItemManager.NothingId and SmallclothesId).
    private const uint ReservedIds = 1024;

    private static readonly (string Key, GearSlot Slot)[] Slots =
    {
        ("MainHand", GearSlot.MainHand), ("OffHand", GearSlot.OffHand), ("Head", GearSlot.Head),
        ("Body", GearSlot.Body), ("Hands", GearSlot.Hands), ("Legs", GearSlot.Legs), ("Feet", GearSlot.Feet),
        ("Ears", GearSlot.Ears), ("Neck", GearSlot.Neck), ("Wrists", GearSlot.Wrists),
        ("RFinger", GearSlot.RingRight), ("LFinger", GearSlot.RingLeft),
    };

    public static GlamGearDto[] Read(string json, out int race, out int gender)
    {
        race = 0;
        gender = 0;
        if (json.Length == 0)
        {
            return Array.Empty<GlamGearDto>();
        }

        try
        {
            var design = JObject.Parse(json);
            if (design["Customize"] is JObject customize)
            {
                race = customize["Race"]?["Value"]?.Value<int>() ?? 0;
                gender = customize["Gender"]?["Value"]?.Value<int>() ?? 0;
            }

            if (design["Equipment"] is not JObject equipment)
            {
                return Array.Empty<GlamGearDto>();
            }

            var gear = new List<GlamGearDto>(Slots.Length);
            for (var index = 0; index < Slots.Length; index++)
            {
                if (equipment[Slots[index].Key] is not JObject piece || piece["Apply"]?.Value<bool>() == false ||
                    piece["ItemId"]?.Value<ulong>() is not { } itemId || itemId == 0 ||
                    itemId >= uint.MaxValue - ReservedIds)
                {
                    continue;
                }

                gear.Add(new GlamGearDto((int)Slots[index].Slot, (uint)itemId, piece["Stain"]?.Value<byte>() ?? 0,
                    piece["Stain2"]?.Value<byte>() ?? 0));
            }

            return gear.ToArray();
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Could not read a Glamourer design: {exception.Message}");
            return Array.Empty<GlamGearDto>();
        }
    }
}
