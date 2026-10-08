using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino.Race;

internal sealed class RaceCommentary
{
    public const float LineSeconds = 2.4f;

    private const int SeedHexDigits = 16;

    private static readonly LocString[] OffLines = { L.Race.LineOff1, L.Race.LineOff2 };

    private static readonly LocString[] SurgeLines = { L.Race.LineSurge1, L.Race.LineSurge2, L.Race.LineSurge3 };

    private static readonly LocString[] LeadLines = { L.Race.LineLead1, L.Race.LineLead2 };

    private static readonly LocString[] FadeLines = { L.Race.LineFade1, L.Race.LineFade2 };

    private static readonly LocString[] StretchLines = { L.Race.LineStretch1, L.Race.LineStretch2 };

    private static readonly LocString[] WinnerLines = { L.Race.LineWinner1, L.Race.LineWinner2 };

    private static readonly LocString[] PhotoLines = { L.Race.PhotoFinish };

    private static readonly LocString[] NoLines = Array.Empty<LocString>();

    private GameRandom random = GameRandom.FromSeed(1);
    private string line = string.Empty;
    private float age = LineSeconds;

    public string Line => line;

    public float Age => age;

    public bool Showing => line.Length > 0 && age < LineSeconds;

    public void Begin(string seedHex)
    {
        random = GameRandom.FromSeed(SeedOf(seedHex));
        line = string.Empty;
        age = LineSeconds;
    }

    public void Clear()
    {
        line = string.Empty;
        age = LineSeconds;
    }

    public void Advance(float deltaSeconds)
    {
        age += deltaSeconds;
    }

    public bool Speak(RaceCue cue, string name)
    {
        var picked = Pick(cue);
        if (picked.Key is null)
        {
            return false;
        }

        line = Loc.T(picked, name);
        age = 0f;
        return true;
    }

    public LocString Pick(RaceCue cue)
    {
        var lines = LinesFor(cue);
        return lines.Length == 0 ? default : lines[random.Next(lines.Length)];
    }

    public static LocString[] LinesFor(RaceCue cue) => cue switch
    {
        RaceCue.Off => OffLines,
        RaceCue.Surge => SurgeLines,
        RaceCue.LeadChange => LeadLines,
        RaceCue.Fade => FadeLines,
        RaceCue.FinalStretch => StretchLines,
        RaceCue.PhotoFinish => PhotoLines,
        RaceCue.Winner => WinnerLines,
        _ => NoLines,
    };

    internal static ulong SeedOf(string seedHex)
    {
        var seed = 0UL;
        var digits = Math.Min(seedHex.Length, SeedHexDigits);
        for (var index = 0; index < digits; index++)
        {
            var digit = seedHex[index];
            var nibble = digit switch
            {
                >= '0' and <= '9' => (ulong)(digit - '0'),
                >= 'a' and <= 'f' => (ulong)(digit - 'a' + 10),
                >= 'A' and <= 'F' => (ulong)(digit - 'A' + 10),
                _ => 0UL,
            };
            seed = (seed << 4) | nibble;
        }

        return ~seed;
    }
}
