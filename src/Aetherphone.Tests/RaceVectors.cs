using System.Text.Json;

namespace Aetherphone.Tests;

internal sealed record RaceVector(
    string Seed,
    string StreamBinding,
    int[] Birds,
    int[] Strengths,
    int[] OddsHundredths,
    int[] PlaceOddsHundredths,
    int[] Order,
    string DrawLog,
    long ForecastPayHundredths,
    long ReversePayHundredths,
    bool PhotoFinish,
    int[] FinishSubTicks,
    int[][] Surges,
    int[] FadeTick,
    int[] SampleTicks,
    int[][] Positions,
    int[] Leaders);

internal static class RaceVectors
{
    public const string FileHash = "9d4c13846c91d17faea9f3f9b69513a3a24b990f1083fe3df3db8f82905bcfbd";

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static string Path => System.IO.Path.Combine(AppContext.BaseDirectory, "Vectors", "race.json");

    public static RaceVector[] Load()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path));
        var races = document.RootElement.GetProperty("races");
        var vectors = new RaceVector[races.GetArrayLength()];
        for (var index = 0; index < vectors.Length; index++)
        {
            vectors[index] = races[index].Deserialize<RaceVector>(Options)!;
        }

        return vectors;
    }
}
