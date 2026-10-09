namespace Aetherphone.Core.Casino;

internal static class CasinoRoomCodes
{
    public const int Length = 6;

    public const int RawMaxLength = 16;

    public const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string Normalized(string? raw)
    {
        if (raw is null || raw.Length == 0 || raw.Length > RawMaxLength)
        {
            return string.Empty;
        }

        Span<char> code = stackalloc char[Length];
        var written = 0;
        for (var index = 0; index < raw.Length; index++)
        {
            var character = char.ToUpperInvariant(raw[index]);
            if (character is ' ' or '-')
            {
                continue;
            }

            if (written == Length || Alphabet.IndexOf(character) < 0)
            {
                return string.Empty;
            }

            code[written++] = character;
        }

        return written == Length ? new string(code) : string.Empty;
    }

    public static bool IsCode(string? raw)
    {
        if (raw is null || raw.Length == 0 || raw.Length > RawMaxLength)
        {
            return false;
        }

        var counted = 0;
        for (var index = 0; index < raw.Length; index++)
        {
            var character = char.ToUpperInvariant(raw[index]);
            if (character is ' ' or '-')
            {
                continue;
            }

            if (Alphabet.IndexOf(character) < 0)
            {
                return false;
            }

            counted++;
        }

        return counted == Length;
    }
}
