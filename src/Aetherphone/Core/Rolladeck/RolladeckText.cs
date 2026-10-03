namespace Aetherphone.Core.Rolladeck;

// Maps Unicode mathematical alphanumeric symbols (U+1D400–U+1D7FF) back to plain ASCII
// so ImGui can render DJ names and stream titles that use "bold" or "italic" Unicode fonts.
// Supplementary-plane characters that don't map to ASCII (emoji etc.) are stripped.
internal static class RolladeckText
{
    // Uppercase-start codepoint for each mathematical letter style (26 upper + 26 lower each).
    private static readonly int[] StyleBases =
    [
        0x1D400, // Mathematical Bold
        0x1D434, // Mathematical Italic
        0x1D468, // Mathematical Bold Italic
        0x1D49C, // Mathematical Script
        0x1D4D0, // Mathematical Bold Script
        0x1D504, // Mathematical Fraktur
        0x1D538, // Mathematical Double-Struck
        0x1D56C, // Mathematical Bold Fraktur
        0x1D5A0, // Mathematical Sans-Serif
        0x1D5D4, // Mathematical Sans-Serif Bold
        0x1D608, // Mathematical Sans-Serif Italic
        0x1D63C, // Mathematical Sans-Serif Bold Italic
        0x1D670, // Mathematical Monospace
    ];

    // Base codepoint for each mathematical digit style (10 digits 0–9 each).
    private static readonly int[] DigitBases =
    [
        0x1D7CE, // Bold
        0x1D7D8, // Double-Struck
        0x1D7E2, // Sans-Serif
        0x1D7EC, // Sans-Serif Bold
        0x1D7F6, // Monospace
    ];

    public static string? TeleportDestination(string? lifestream)
    {
        if (string.IsNullOrWhiteSpace(lifestream))
        {
            return null;
        }

        var trimmed = lifestream.Trim();
        return trimmed.StartsWith("/li ", StringComparison.OrdinalIgnoreCase) ? trimmed[4..].Trim() : trimmed;
    }

    public static string Normalize(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var hasSurrogate = false;
        for (var index = 0; index < input.Length; index++)
        {
            if (char.IsSurrogate(input[index]))
            {
                hasSurrogate = true;
                break;
            }
        }

        if (!hasSurrogate)
        {
            return input;
        }

        var builder = new System.Text.StringBuilder(input.Length);
        foreach (var rune in input.EnumerateRunes())
        {
            var codepoint = rune.Value;
            if (codepoint <= 0xFFFF)
            {
                builder.Append((char)codepoint);
            }
            else
            {
                var mapped = MapSupplementary(codepoint);
                if (mapped != '\0')
                {
                    builder.Append(mapped);
                }
            }
        }

        return builder.ToString().Trim();
    }

    private static char MapSupplementary(int codepoint)
    {
        for (var styleIndex = 0; styleIndex < StyleBases.Length; styleIndex++)
        {
            var offset = codepoint - StyleBases[styleIndex];
            if (offset >= 0 && offset < 26)
            {
                return (char)('A' + offset);
            }
            if (offset >= 26 && offset < 52)
            {
                return (char)('a' + offset - 26);
            }
        }

        for (var digitIndex = 0; digitIndex < DigitBases.Length; digitIndex++)
        {
            var offset = codepoint - DigitBases[digitIndex];
            if (offset >= 0 && offset < 10)
            {
                return (char)('0' + offset);
            }
        }

        return '\0';
    }
}
