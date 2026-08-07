using System.Globalization;

namespace Aetherphone.Core.Shortcuts;

internal static class ShortcutCommandText
{
    private const string WaitOpen = "<wait.";
    private const string WaitCommand = "/wait";

    public static bool IsWebUrl(string text) => TryParseWebUrl(text, out _);

    public static bool TryParseWebUrl(string text, out Uri parsed)
    {
        parsed = null!;
        var trimmed = text.Trim();
        if (trimmed.Length == 0 || !Uri.TryCreate(trimmed, UriKind.Absolute, out var candidate))
        {
            return false;
        }

        if (candidate.Scheme != Uri.UriSchemeHttp && candidate.Scheme != Uri.UriSchemeHttps ||
            candidate.Host.Length == 0)
        {
            return false;
        }

        parsed = candidate;
        return true;
    }

    public static string HostOf(string text) => TryParseWebUrl(text, out var parsed) ? parsed.Host : text.Trim();

    public static string Split(string text, out float wait)
    {
        wait = 0f;
        var line = text.Trim();
        if (line.Length == 0)
        {
            return string.Empty;
        }

        line = StripInlineWait(line, ref wait);
        if (!line.StartsWith(WaitCommand, StringComparison.OrdinalIgnoreCase))
        {
            return line;
        }

        var argument = line.Substring(WaitCommand.Length).Trim();
        if (argument.Length == 0)
        {
            wait += 1f;
            return string.Empty;
        }

        if (float.TryParse(argument, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            wait += seconds;
            return string.Empty;
        }

        return line;
    }

    private static string StripInlineWait(string line, ref float wait)
    {
        if (!line.EndsWith(">", StringComparison.Ordinal))
        {
            return line;
        }

        var open = line.LastIndexOf(WaitOpen, StringComparison.OrdinalIgnoreCase);
        if (open < 0)
        {
            return line;
        }

        var digits = line.Substring(open + WaitOpen.Length, line.Length - open - WaitOpen.Length - 1);
        if (!float.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            return line;
        }

        wait += seconds;
        return line.Substring(0, open).TrimEnd();
    }
}
