using Aetherphone.Core.Hunts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HuntSpawnConditionResolverTests
{
    private static HuntSpawnConditionRule CroakadileMoonRule() => new()
    {
        Type = "moon",
        Phase = "full",
        Periods =
        [
            new HuntConditionPeriod { From = 18000, To = 54000 },
            new HuntConditionPeriod { From = 104400, To = 140400 },
            new HuntConditionPeriod { From = 190800, To = 226800 },
            new HuntConditionPeriod { From = 277200, To = 313200 },
        ],
    };

    private static HuntSpawnConditionRule MindflayerMoonRule() => new()
    {
        Type = "moon",
        Phase = "new",
        Periods =
        [
            new HuntConditionPeriod { From = 43200, To = 54000 },
            new HuntConditionPeriod { From = 104400, To = 140400 },
            new HuntConditionPeriod { From = 190800, To = 226800 },
            new HuntConditionPeriod { From = 277200, To = 313200 },
        ],
    };

    private static HuntSpawnConditionRule ZonaSeekerWeatherRule() => new()
    {
        Type = "weather",
        MatchingWeather = ["clear_skies", "fair_skies"],
        Probabilities =
        [
            new HuntWeatherProbability { Chance = 40, Condition = "clear_skies" },
            new HuntWeatherProbability { Chance = 20, Condition = "fair_skies" },
            new HuntWeatherProbability { Chance = 25, Condition = "clouds" },
            new HuntWeatherProbability { Chance = 10, Condition = "fog" },
            new HuntWeatherProbability { Chance = 5, Condition = "rain" },
        ],
    };

    private static HuntSpawnConditionRule OffsetWeatherRule() => new()
    {
        Type = "weather",
        MatchingWeather = ["fog", "clear_skies", "fair_skies", "clouds"],
        Probabilities =
        [
            new HuntWeatherProbability { Chance = 5, Condition = "fog" },
            new HuntWeatherProbability { Chance = 45, Condition = "clear_skies" },
            new HuntWeatherProbability { Chance = 30, Condition = "fair_skies" },
            new HuntWeatherProbability { Chance = 10, Condition = "clouds" },
            new HuntWeatherProbability { Chance = 5, Condition = "rain" },
            new HuntWeatherProbability { Chance = 5, Condition = "showers" },
        ],
        Offset = 12000,
    };

    private static HuntSpawnConditionRule TimeRule() => new()
    {
        Type = "time",
        Hours = [17],
        Duration = 14400,
    };

    private static readonly Dictionary<string, Func<HuntSpawnConditionRule>> SingleRulesByName = new()
    {
        ["croakadile moon"] = CroakadileMoonRule,
        ["mindflayer moon"] = MindflayerMoonRule,
        ["zona seeker weather"] = ZonaSeekerWeatherRule,
        ["offset weather"] = OffsetWeatherRule,
        ["time"] = TimeRule,
    };

    public static TheoryData<string> AllSingleRuleNames()
    {
        var data = new TheoryData<string>();
        foreach (var name in SingleRulesByName.Keys)
        {
            data.Add(name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllSingleRuleNames))]
    public void ResolvedWindowStartsBeforeItEnds(string ruleName)
    {
        var rule = SingleRulesByName[ruleName]();
        var window = HuntSpawnConditionResolver.Resolve([rule], DateTimeOffset.UtcNow);
        Assert.NotNull(window);
        Assert.True(window!.Value.Start < window.Value.End);
    }

    [Theory]
    [MemberData(nameof(AllSingleRuleNames))]
    public void QueryingAtItsOwnStartReturnsTheSameWindow(string ruleName)
    {
        var rule = SingleRulesByName[ruleName]();
        var window = HuntSpawnConditionResolver.Resolve([rule], DateTimeOffset.UtcNow)!.Value;
        var atStart = HuntSpawnConditionResolver.Resolve([rule], window.Start);
        Assert.NotNull(atStart);
        Assert.Equal(window.Start, atStart!.Value.Start);
        Assert.Equal(window.End, atStart.Value.End);
        Assert.True(window.ActiveAt(window.Start));
    }

    [Theory]
    [MemberData(nameof(AllSingleRuleNames))]
    public void QueryingAtItsOwnEndMovesToTheNextOccurrence(string ruleName)
    {
        var rule = SingleRulesByName[ruleName]();
        var window = HuntSpawnConditionResolver.Resolve([rule], DateTimeOffset.UtcNow)!.Value;
        Assert.False(window.ActiveAt(window.End));

        var atEnd = HuntSpawnConditionResolver.Resolve([rule], window.End);
        Assert.NotNull(atEnd);
        Assert.True(atEnd!.Value.Start >= window.End);
    }

    [Fact]
    public void FullMoonPhaseHasFourNamedSubPeriodsWithinIt()
    {
        var rule = CroakadileMoonRule();
        var cursor = DateTimeOffset.UtcNow;
        var seen = new List<HuntConditionWindow>();
        for (var index = 0; index < 4; index++)
        {
            var window = HuntSpawnConditionResolver.Resolve([rule], cursor)!.Value;
            seen.Add(window);
            cursor = window.End;
        }

        var fullPhaseRealSeconds = (seen[3].End - seen[0].Start).TotalSeconds;
        var wholeCycleRealSeconds = 32d * 86400d * 7d / 144d;
        Assert.True(fullPhaseRealSeconds < wholeCycleRealSeconds);
    }

    [Fact]
    public void MoonPhaseRecursOnA32EorzeaDayCycle()
    {
        var rule = MindflayerMoonRule();
        var first = HuntSpawnConditionResolver.Resolve([rule], DateTimeOffset.UtcNow)!.Value;

        var cursor = first.End;
        HuntConditionWindow window;
        do
        {
            window = HuntSpawnConditionResolver.Resolve([rule], cursor)!.Value;
            cursor = window.End;
        } while ((window.Start - first.Start).TotalSeconds < 32d * 86400d * 7d / 144d - 1d);

        var expectedCycleRealSeconds = 32d * 86400d * 7d / 144d;
        var actualGapRealSeconds = (window.Start - first.Start).TotalSeconds;
        Assert.Equal(expectedCycleRealSeconds, actualGapRealSeconds, 1d);
    }

    [Fact]
    public void TimeRuleWindowLastsExactlyItsEorzeaDuration()
    {
        var rule = TimeRule();
        var window = HuntSpawnConditionResolver.Resolve([rule], DateTimeOffset.UtcNow)!.Value;
        var expectedRealSeconds = rule.Duration!.Value * 7d / 144d;
        Assert.Equal(expectedRealSeconds, (window.End - window.Start).TotalSeconds, 1d);
    }

    [Fact]
    public void OffsetWeatherRuleWindowIsNoShorterThanZeroOffset()
    {
        var withOffset = OffsetWeatherRule();
        var withoutOffset = OffsetWeatherRule();
        withoutOffset.Offset = 0;

        var at = DateTimeOffset.UtcNow;
        var offsetWindow = HuntSpawnConditionResolver.Resolve([withOffset], at);
        var plainWindow = HuntSpawnConditionResolver.Resolve([withoutOffset], at);
        Assert.NotNull(offsetWindow);
        Assert.NotNull(plainWindow);

        Assert.True(offsetWindow!.Value.Start >= plainWindow!.Value.Start);
    }

    [Fact]
    public void MultipleRulesResolveToTheirOverlap()
    {
        var rules = new HuntSpawnConditionRule[]
        {
            new()
            {
                Type = "time",
                Hours = [9],
                Duration = 28800,
            },
            new()
            {
                Type = "weather",
                MatchingWeather = ["clear_skies", "fair_skies"],
                Probabilities =
                [
                    new HuntWeatherProbability { Chance = 15, Condition = "clear_skies" },
                    new HuntWeatherProbability { Chance = 45, Condition = "fair_skies" },
                    new HuntWeatherProbability { Chance = 25, Condition = "clouds" },
                    new HuntWeatherProbability { Chance = 15, Condition = "rain" },
                ],
            },
        };

        var window = HuntSpawnConditionResolver.Resolve(rules, DateTimeOffset.UtcNow);
        Assert.NotNull(window);
        Assert.True(window!.Value.Start < window.Value.End);

        var timeWindow = HuntSpawnConditionResolver.Resolve([rules[0]], window.Value.Start)!.Value;
        var weatherWindow = HuntSpawnConditionResolver.Resolve([rules[1]], window.Value.Start)!.Value;
        Assert.True(timeWindow.ActiveAt(window.Value.Start));
        Assert.True(weatherWindow.ActiveAt(window.Value.Start));
    }

    [Fact]
    public void EmptyRuleSetResolvesToNull() =>
        Assert.Null(HuntSpawnConditionResolver.Resolve(Array.Empty<HuntSpawnConditionRule>(), DateTimeOffset.UtcNow));

    [Fact]
    public void UnrecognizedRuleTypeResolvesToNull()
    {
        var rule = new HuntSpawnConditionRule { Type = "eclipse" };
        Assert.Null(HuntSpawnConditionResolver.Resolve([rule], DateTimeOffset.UtcNow));
    }

    [Fact]
    public void RealCatalogDataParsesConditionsForKnownConditionMarks()
    {
        var source = new FileInfo(Path.Combine(AppContext.BaseDirectory, "Hunts", "HuntMob.json"));
        var catalog = new HuntMobCatalog(source);

        var mindflayer = catalog.Find("mindflayer");
        Assert.NotNull(mindflayer);
        var mindflayerRule = Assert.Single(mindflayer!.Conditions!.Automatic!).RuleSet;
        Assert.Single(mindflayerRule);
        Assert.Equal("moon", mindflayerRule[0].Type);
        Assert.Equal("new", mindflayerRule[0].Phase);
        Assert.Equal(4, mindflayerRule[0].Periods!.Length);

        var croakadile = catalog.Find("croakadile");
        Assert.NotNull(croakadile);
        var croakadileRule = Assert.Single(croakadile!.Conditions!.Automatic!).RuleSet;
        Assert.Single(croakadileRule);
        Assert.Equal("moon", croakadileRule[0].Type);
        Assert.Equal("full", croakadileRule[0].Phase);

        var mindflayerWindow = HuntSpawnConditionResolver.Resolve(mindflayerRule, DateTimeOffset.UtcNow);
        var croakadileWindow = HuntSpawnConditionResolver.Resolve(croakadileRule, DateTimeOffset.UtcNow);
        Assert.NotNull(mindflayerWindow);
        Assert.NotNull(croakadileWindow);
    }

    [Fact]
    public void ResolveGateOfEmptyAutomaticListResolvesToNull() =>
        Assert.Null(HuntSpawnConditionResolver.ResolveGate(Array.Empty<HuntMobConditionWindow>(),
            DateTimeOffset.UtcNow));

    [Fact]
    public void ResolveGateMatchesWhicheverSingleEntryIsActiveOrOtherwiseEarliest()
    {
        var mindflayerRule = MindflayerMoonRule();
        var croakadileRule = CroakadileMoonRule();
        var at = DateTimeOffset.UtcNow;

        var mindflayerWindow = HuntSpawnConditionResolver.Resolve([mindflayerRule], at)!.Value;
        var croakadileWindow = HuntSpawnConditionResolver.Resolve([croakadileRule], at)!.Value;

        var entries = new[]
        {
            new HuntMobConditionWindow { Rule = mindflayerRule },
            new HuntMobConditionWindow { Rule = croakadileRule },
        };

        var gate = HuntSpawnConditionResolver.ResolveGate(entries, at);
        Assert.NotNull(gate);

        if (mindflayerWindow.ActiveAt(at))
        {
            Assert.Equal(mindflayerWindow, gate!.Value);
        }
        else if (croakadileWindow.ActiveAt(at))
        {
            Assert.Equal(croakadileWindow, gate!.Value);
        }
        else
        {
            var expected = mindflayerWindow.Start <= croakadileWindow.Start ? mindflayerWindow : croakadileWindow;
            Assert.Equal(expected, gate!.Value);
        }
    }

    [Fact]
    public void RealCatalogDataResolvesKirlirgerConditionGate()
    {
        var source = new FileInfo(Path.Combine(AppContext.BaseDirectory, "Hunts", "HuntMob.json"));
        var catalog = new HuntMobCatalog(source);

        var kirlirger = catalog.Find("kirlirger_the_abhorrent");
        Assert.NotNull(kirlirger);
        var automatic = kirlirger!.Conditions!.Automatic!;
        Assert.Single(automatic);
        Assert.Equal(2, automatic[0].RuleSet.Count);

        var gate = HuntSpawnConditionResolver.ResolveGate(automatic, DateTimeOffset.UtcNow);
        Assert.NotNull(gate);
        Assert.True(gate!.Value.Start < gate.Value.End);
    }
}
