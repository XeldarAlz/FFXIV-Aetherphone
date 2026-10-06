using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Flow;

internal struct RatioLabel
{
    private const string Separator = "/";

    private int numerator;
    private int denominator;
    private string? text;

    public string Get(int wantedNumerator, int wantedDenominator)
    {
        if (text is not null && numerator == wantedNumerator && denominator == wantedDenominator)
        {
            return text;
        }

        numerator = wantedNumerator;
        denominator = wantedDenominator;
        text = string.Concat(GameNumber.Label(wantedNumerator), Separator, GameNumber.Label(wantedDenominator));
        return text;
    }
}
