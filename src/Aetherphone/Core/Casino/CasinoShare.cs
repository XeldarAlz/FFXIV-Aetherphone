namespace Aetherphone.Core.Casino;

internal static class CasinoShare
{
    public const int MaxIdLength = 64;

    private const string TokenPrefix = "[aep.casino.v1:";
    private const string TokenSuffix = "]";

    public static string Compose(string tableId)
    {
        var bare = TryParse(tableId, out var parsed) ? parsed : tableId;
        return string.Concat(TokenPrefix, bare, TokenSuffix);
    }

    public static bool IsToken(string? body)
    {
        return TryParse(body, out _);
    }

    public static bool TryParse(string? body, out string tableId)
    {
        tableId = string.Empty;
        if (string.IsNullOrEmpty(body))
        {
            return false;
        }

        var text = body.Trim();
        if (text.StartsWith(TokenPrefix, StringComparison.Ordinal)
            && text.EndsWith(TokenSuffix, StringComparison.Ordinal)
            && text.Length > TokenPrefix.Length + TokenSuffix.Length)
        {
            text = text.Substring(TokenPrefix.Length, text.Length - TokenPrefix.Length - TokenSuffix.Length);
        }

        if (text.Length == 0 || text.Length > MaxIdLength || !IsTableId(text))
        {
            return false;
        }

        tableId = text;
        return true;
    }

    private static bool IsTableId(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            var valid = character is >= '0' and <= '9' or >= 'a' and <= 'z' or >= 'A' and <= 'Z' or '-';
            if (!valid)
            {
                return false;
            }
        }

        return true;
    }
}
