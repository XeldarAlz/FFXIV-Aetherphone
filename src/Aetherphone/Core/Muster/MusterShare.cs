namespace Aetherphone.Core.Muster;

/// <summary>The muster chat invite token. Like location shares, only a message whose entire body is the
/// token renders as a card; mixed into other text it stays plain, and it rides the normal text-message
/// wire format so the backend never learns about it.</summary>
internal static class MusterShare
{
    private const string TokenPrefix = "[aep.muster.v1:";
    private const string TokenSuffix = "]";
    private const int MaxIdLength = 64;

    public static string Compose(string musterId)
    {
        return $"{TokenPrefix}{musterId}{TokenSuffix}";
    }

    public static bool IsToken(string? body)
    {
        return TryParse(body, out _);
    }

    public static bool TryParse(string? body, out string musterId)
    {
        musterId = string.Empty;
        if (string.IsNullOrEmpty(body))
        {
            return false;
        }

        var text = body.Trim();
        if (text.Length <= TokenPrefix.Length + TokenSuffix.Length
            || !text.StartsWith(TokenPrefix, StringComparison.Ordinal)
            || !text.EndsWith(TokenSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        var inner = text.Substring(TokenPrefix.Length, text.Length - TokenPrefix.Length - TokenSuffix.Length);
        if (inner.Length > MaxIdLength)
        {
            return false;
        }

        for (var index = 0; index < inner.Length; index++)
        {
            var character = inner[index];
            var valid = character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F' or '-';
            if (!valid)
            {
                return false;
            }
        }

        musterId = inner;
        return true;
    }
}
