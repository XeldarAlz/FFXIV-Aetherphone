using System.Text;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Game;

namespace Aetherphone.Core.Social;

internal static class SocialRegion
{
    public static readonly string[] Codes = { "NA", "EU", "JP", "OCE", "CN" };

    public static readonly int AllMask = (1 << Codes.Length) - 1;

    public static bool IsValid(string code) => Array.IndexOf(Codes, code) >= 0;

    public static bool MaskShows(int mask, int regionIndex) =>
        mask == 0 || (mask & (1 << regionIndex)) != 0;

    public static int AllowedMask(int shown, int hidden) =>
        (shown == 0 ? AllMask : shown) & ~hidden & AllMask;

    public static int ToggleMask(int mask, int regionIndex)
    {
        var bits = (mask == 0 ? AllMask : mask) ^ (1 << regionIndex);
        if (bits == 0)
        {
            return mask;
        }

        return bits == AllMask ? 0 : bits;
    }

    public static string? FilterCsv(int mask)
    {
        if (mask == 0 || mask == AllMask)
        {
            return null;
        }

        var builder = new StringBuilder(16);
        for (var index = 0; index < Codes.Length; index++)
        {
            if ((mask & (1 << index)) == 0)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(',');
            }

            builder.Append(Codes[index]);
        }

        return builder.ToString();
    }

    public static string AutoCode(AethernetSession session, GameData gameData)
    {
        var accountCode = gameData.RegionCodeForWorld(session.AccountWorld);
        if (accountCode.Length > 0)
        {
            return accountCode;
        }

        var localCode = gameData.LocalRegionCode();
        return localCode.Length > 0 ? localCode : Codes[0];
    }

    public static string EffectiveCode(AethernetSession session, GameData gameData)
    {
        var manual = session.ManualRegion;
        return IsValid(manual) ? manual : AutoCode(session, gameData);
    }

    public static string Resolve(string? region, string? world, GameData gameData)
    {
        if (!string.IsNullOrEmpty(region))
        {
            return region;
        }

        return gameData.RegionCodeForWorld(world);
    }
}
