using System.Text.Json;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Tests;

internal static class SlotsVectorFiles
{
    public static readonly string[] Names = { "slots-bird.json", "slots-cascade.json", "slots-moogle.json" };

    public static string PathOf(string name)
    {
        var directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            var candidate = Path.Combine(directory, "src", "Aetherphone.Tests", "Vectors", name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new FileNotFoundException(name);
    }

    public static JsonDocument Load(string name) => JsonDocument.Parse(File.ReadAllText(PathOf(name)));

    public static IEnumerable<(string Name, JsonElement Round)> Rounds()
    {
        foreach (var name in Names)
        {
            using var document = Load(name);
            foreach (var round in document.RootElement.GetProperty("rounds").EnumerateArray())
            {
                yield return (name, round.Clone());
            }
        }
    }

    public static string MachineOf(string name) => name switch
    {
        "slots-cascade.json" => "slots.cascade",
        "slots-moogle.json" => "slots.moogle",
        _ => "slots.bird",
    };

    public static CasinoSlotsSpinDto Spin(string name, JsonElement round)
    {
        var steps = new List<CasinoSlotsStepDto>();
        foreach (var step in round.GetProperty("steps").EnumerateArray())
        {
            steps.Add(JsonSerializer.Deserialize(step.GetRawText(), AethernetJsonContext.Default.CasinoSlotsStepDto)!);
        }

        return new CasinoSlotsSpinDto(
            Granted: true,
            RoundId: round.GetProperty("roundId").GetString()!,
            Stake: round.GetProperty("cost").GetInt64(),
            TotalWin: round.GetProperty("totalWin").GetInt64(),
            CapApplied: round.GetProperty("capApplied").GetBoolean(),
            MachineId: MachineOf(name),
            Bet: round.GetProperty("bet").GetInt64(),
            Mode: round.GetProperty("mode").GetString()!,
            Steps: steps.ToArray(),
            BonusTriggered: round.GetProperty("bonusTriggered").GetBoolean(),
            FreeSpinsPlayed: round.GetProperty("freeSpinsPlayed").GetInt32(),
            Expander: round.GetProperty("expander").GetInt32(),
            FeatureMultiplier: round.GetProperty("featureMultiplier").GetInt32());
    }
}
