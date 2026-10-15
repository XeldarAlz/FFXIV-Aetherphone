using System.Text;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

internal static class CasinoBookRules
{
    private const char PrivateUseFirst = (char)0xE000;
    private const char PrivateUseLast = (char)0xF8FF;

    public static bool IsBook(string? gameKind)
    {
        return string.Equals(gameKind, CasinoWire.BookKind, StringComparison.Ordinal);
    }

    public static string Check(CasinoTableConfigDto config)
    {
        if (config.Currency != CasinoCurrencies.Gil)
        {
            return CasinoReasons.GilOnly;
        }

        if (config.Bank < CasinoHostingRules.MinGil || config.Bank > CasinoHostingRules.MaxGil
            || config.MaxPayout < 0 || config.MaxPayout > config.Bank
            || config.Name.Trim().Length > CasinoHostingRules.NameMaxLength
            || config.Listing < CasinoListings.Private || config.Listing > CasinoListings.Open
            || (config.CoDealers?.Length ?? 0) > 0)
        {
            return CasinoReasons.ConfigInvalid;
        }

        if (!CasinoHostingRules.IsTurnSeconds(config.TurnSeconds)
            || (config.TimeBankUses != CasinoHostingRules.TimeBankOff
                && config.TimeBankUses != CasinoHostingRules.TimeBankUses)
            || config.PracticeStack < CasinoHostingRules.MinPracticeStack
            || config.PracticeStack > CasinoHostingRules.MaxPracticeStack)
        {
            return CasinoReasons.ConfigInvalid;
        }

        return string.Empty;
    }

    public static string NormalizeGuestName(string raw)
    {
        var builder = new StringBuilder(raw.Length);
        var pendingSpace = false;
        for (var index = 0; index < raw.Length; index++)
        {
            var character = raw[index];
            if (character is >= PrivateUseFirst and <= PrivateUseLast)
            {
                break;
            }

            if (char.IsControl(character))
            {
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    public static bool IsGuestName(string name)
    {
        return name.Length > 0 && name.Length <= CasinoHostingRules.GuestNameMaxLength;
    }

    public static bool HasGuestName(string raw)
    {
        for (var index = 0; index < raw.Length; index++)
        {
            var character = raw[index];
            if (!char.IsControl(character) && !char.IsWhiteSpace(character))
            {
                return true;
            }
        }

        return false;
    }
}
