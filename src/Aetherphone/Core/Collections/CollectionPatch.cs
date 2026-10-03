namespace Aetherphone.Core.Collections;

internal static class CollectionPatch
{
    private const int MajorWeight = 1000;
    private const int MinorDigits = 3;

    public static int Order(string patch)
    {
        if (string.IsNullOrEmpty(patch))
        {
            return 0;
        }

        var span = patch.AsSpan().Trim();
        var dot = span.IndexOf('.');
        var majorSpan = dot < 0 ? span : span[..dot];
        if (!int.TryParse(majorSpan, out var major) || major < 0)
        {
            return 0;
        }

        var minor = 0;
        if (dot >= 0)
        {
            var minorSpan = span[(dot + 1)..];
            var weight = MajorWeight / 10;
            for (var index = 0; index < minorSpan.Length && index < MinorDigits; index++)
            {
                var character = minorSpan[index];
                if (character < '0' || character > '9')
                {
                    break;
                }

                minor += (character - '0') * weight;
                weight /= 10;
            }
        }

        return major * MajorWeight + minor;
    }
}
