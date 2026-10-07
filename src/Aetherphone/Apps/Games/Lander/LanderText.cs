using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Games.Lander;

internal static class LanderText
{
    private const int MaxTenths = 999;
    private static readonly string?[] Tenths = new string?[MaxTenths + 1];
    private static LanguageInfo? language;

    public static string TenthsOf(float value)
    {
        if (!ReferenceEquals(language, Loc.Current))
        {
            Array.Clear(Tenths);
            language = Loc.Current;
        }

        var index = Math.Clamp((int)MathF.Round(MathF.Abs(value) * 10f), 0, MaxTenths);
        return Tenths[index] ??= (index / 10f).ToString("0.0", Loc.Culture);
    }
}
