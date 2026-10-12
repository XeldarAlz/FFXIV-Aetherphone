using System.Globalization;
using Aetherphone.Core.Game;
using Lumina.Excel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Aetherphone.Harness.Scenarios;

internal sealed class ScenarioLoader
{
    private const uint DefaultTerritoryId = 132;
    private const byte DefaultMaxLevel = 100;
    private const int DefaultWeeklyTomestoneLimit = 450;
    private const int DefaultCompanySealCap = 90000;
    private const int CustomizeLength = 26;
    private const int CustomizeRaceIndex = 0;
    private const int CustomizeGenderIndex = 1;
    private const int CustomizeTribeIndex = 4;
    private const int LinkshellSlots = 8;
    private const int SecondsPerMinute = 60;
    private const int SecondsPerHour = 3600;
    private const int HoursPerDay = 24;
    private const ulong ContentIdMarker = 0x0040_0000_0000_0000UL;
    private const ulong ContentIdMask = 0x0000_FFFF_FFFF_FFFFUL;
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    private static readonly string[] RaceNames =
    {
        string.Empty, "hyur", "elezen", "lalafell", "miqote", "roegadyn", "aura", "hrothgar", "viera",
    };

    private static readonly string[] GrandCompanyNames = { string.Empty, "maelstrom", "twinadder", "immortalflames" };

    private readonly ExcelModule excel;

    public ScenarioLoader(ExcelModule excel)
    {
        this.excel = excel;
    }

    public Scenario Load(string path)
    {
        var root = JObject.Parse(File.ReadAllText(path),
            new JsonLoadSettings { CommentHandling = CommentHandling.Ignore });
        var warnings = new List<string>();
        var sheets = new SheetLookup(excel, warnings);
        var inventory = new ScenarioInventory(sheets, warnings);
        var character = root["character"] as JObject ??
                        throw new InvalidDataException("the scenario has no character block");
        var name = Required(character, "name");
        var worldName = Required(character, "world");
        var homeWorldId = sheets.World(worldName);
        var currentWorldId = character["currentWorld"] is null ? homeWorldId : sheets.World(Text(character, "currentWorld"));
        var classJobId = sheets.ClassJob(Required(character, "job"));
        var contentId = (ulong?)character["contentId"] ?? StableId(name + "@" + worldName);
        var profile = new CharacterProfile(name, contentId, homeWorldId, currentWorldId, Text(character, "freeCompanyTag"),
            Customize(character));

        var location = root["location"] as JObject;
        var territoryId = location?.Value<uint?>("territory") ?? DefaultTerritoryId;
        var mapId = location?.Value<uint?>("map") ?? sheets.TerritoryMap(territoryId);
        var gearsets = Gearsets(root["gearsets"] as JArray, sheets);
        var (levels, experience) = Jobs(root["jobs"] as JObject, sheets, classJobId);
        var game = new SimulatedGame
        {
            ContentId = contentId,
            GrandCompany = GrandCompany(character["grandCompany"]),
            Gil = root.Value<long?>("gil") ?? 0,
            Containers = inventory.Local(root["inventory"] as JObject),
            Currencies = Currencies(root["currencies"] as JObject, sheets),
            WeeklyTomestones = root.Value<int?>("weeklyTomestones") ?? 0,
            WeeklyTomestoneLimit = root.Value<int?>("weeklyTomestoneLimit") ?? DefaultWeeklyTomestoneLimit,
            CompanySealCap = root.Value<int?>("companySealCap") ?? DefaultCompanySealCap,
            Retainers = Retainers(root["retainers"] as JArray, inventory),
            ActiveRetainer = root.Value<int?>("activeRetainer") ?? -1,
            FreeCompany = FreeCompany(root["freeCompany"] as JObject, inventory, Text(character, "freeCompanyTag")),
            Vessels = Vessels(root["vessels"] as JArray),
            MapAllowanceUnix = FromNow(root, "mapAllowanceMinutesLeft"),
            ClassJobLevels = levels,
            ClassJobExperience = experience,
            MaxLevel = root.Value<byte?>("maxLevel") ?? DefaultMaxLevel,
            CurrentClassJobId = classJobId,
            RestedExperience = root.Value<long?>("restedExperience") ?? 0,
            Gearsets = gearsets,
            ActiveGearsetId = root.Value<int?>("activeGearset") ?? FirstGearsetFor(gearsets, classJobId),
            Friends = Friends(root["friends"] as JArray, sheets),
            Linkshells = Names(root["linkshells"] as JArray),
            CrossWorldLinkshells = Names(root["crossWorldLinkshells"] as JArray),
            Dailies = Dailies(root["dailies"] as JObject),
            WeatherId = sheets.Weather(location?["weather"]),
            EorzeaMinuteOverride = MinuteOfDay(Text(location, "eorzeaTime")),
            Indoors = location?.Value<bool?>("indoors") ?? false,
            HouseDistrict = location?.Value<uint?>("district") ?? 0,
            Ward = location?.Value<short?>("ward") ?? 0,
            Plot = location?.Value<short?>("plot") ?? 0,
            Room = location?.Value<short?>("room") ?? 0,
        };
        var scenarioName = Text(root, "name");
        return new Scenario(scenarioName.Length > 0 ? scenarioName : Path.GetFileNameWithoutExtension(path), path,
            profile, game, territoryId, mapId, root.Value<bool?>("questsCompleted") ?? false,
            root.Value<double?>("collected") ?? 0d, warnings);
    }

    private static (short[] Levels, int[] Experience) Jobs(JObject? jobs, SheetLookup sheets, uint currentClassJobId)
    {
        var length = sheets.ExpArrayLength();
        var levels = new short[length];
        var experience = new int[length];
        if (jobs is not null)
        {
            foreach (var property in jobs.Properties())
            {
                var index = sheets.ExpArrayIndex(sheets.ClassJob(property.Name));
                if (index < 0 || index >= length)
                {
                    continue;
                }

                if (property.Value is JArray pair)
                {
                    levels[index] = pair.Count > 0 ? pair[0].Value<short>() : (short)0;
                    experience[index] = pair.Count > 1 ? pair[1].Value<int>() : 0;
                    continue;
                }

                levels[index] = property.Value.Value<short>();
            }
        }

        var currentIndex = sheets.ExpArrayIndex(currentClassJobId);
        if (currentIndex >= 0 && currentIndex < length && levels[currentIndex] == 0)
        {
            levels[currentIndex] = 1;
        }

        return (levels, experience);
    }

    private static SimulatedGearset[] Gearsets(JArray? entries, SheetLookup sheets)
    {
        if (entries is null)
        {
            return Array.Empty<SimulatedGearset>();
        }

        var gearsets = new List<SimulatedGearset>(entries.Count);
        for (var index = 0; index < entries.Count; index++)
        {
            if (entries[index] is not JObject entry)
            {
                continue;
            }

            var classJobId = sheets.ClassJob(Text(entry, "job"));
            if (classJobId == 0)
            {
                continue;
            }

            gearsets.Add(new SimulatedGearset(entry.Value<int?>("id") ?? index, classJobId, Text(entry, "name"),
                entry.Value<int?>("itemLevel") ?? 0));
        }

        return gearsets.ToArray();
    }

    private static int FirstGearsetFor(SimulatedGearset[] gearsets, uint classJobId)
    {
        for (var index = 0; index < gearsets.Length; index++)
        {
            if (gearsets[index].ClassJobId == classJobId)
            {
                return gearsets[index].Id;
            }
        }

        return -1;
    }

    private static SimulatedCurrency[] Currencies(JObject? currencies, SheetLookup sheets)
    {
        if (currencies is null)
        {
            return Array.Empty<SimulatedCurrency>();
        }

        var result = new List<SimulatedCurrency>();
        foreach (var property in currencies.Properties())
        {
            var key = uint.TryParse(property.Name, NumberStyles.None, CultureInfo.InvariantCulture, out var numeric)
                ? new JValue(numeric)
                : new JValue(property.Name);
            var itemId = sheets.Item(key);
            if (itemId != 0)
            {
                result.Add(new SimulatedCurrency(itemId, property.Value.Value<long>()));
            }
        }

        return result.ToArray();
    }

    private static SimulatedRetainer[] Retainers(JArray? entries, ScenarioInventory inventory)
    {
        if (entries is null)
        {
            return Array.Empty<SimulatedRetainer>();
        }

        var retainers = new List<SimulatedRetainer>(entries.Count);
        for (var index = 0; index < entries.Count; index++)
        {
            if (entries[index] is not JObject entry)
            {
                continue;
            }

            var name = Text(entry, "name");
            var venture = entry["venture"] as JObject;
            retainers.Add(new SimulatedRetainer(StableId("retainer:" + name), name, entry.Value<long?>("gil") ?? 0,
                entry.Value<int?>("selling") ?? 0, venture is null ? 0 : FromNow(venture, "minutesLeft"),
                (venture?.Value<int?>("hours") ?? 0) * SecondsPerHour, inventory.Retainer(entry)));
        }

        return retainers.ToArray();
    }

    private static SimulatedVessel[] Vessels(JArray? entries)
    {
        if (entries is null)
        {
            return Array.Empty<SimulatedVessel>();
        }

        var vessels = new List<SimulatedVessel>(entries.Count);
        for (var index = 0; index < entries.Count; index++)
        {
            if (entries[index] is not JObject entry)
            {
                continue;
            }

            var returnUnix = FromNow(entry, "minutesLeft");
            var registerUnix = returnUnix - (entry.Value<long?>("hours") ?? 0) * SecondsPerHour;
            vessels.Add(new SimulatedVessel(Text(entry, "name"), entry.Value<bool?>("airship") ?? false, registerUnix,
                returnUnix));
        }

        return vessels.ToArray();
    }

    private static long FromNow(JObject container, string minutesKey)
    {
        var minutes = container.Value<long?>(minutesKey);
        return minutes is { } value ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() + value * SecondsPerMinute : 0;
    }

    private static SimulatedFreeCompany? FreeCompany(JObject? entry, ScenarioInventory inventory, string tag)
    {
        if (entry is null)
        {
            return null;
        }

        var name = Text(entry, "name");
        return new SimulatedFreeCompany(StableId("free company:" + name + tag), name, inventory.FreeCompany(entry));
    }

    private static SimulatedFriend[] Friends(JArray? entries, SheetLookup sheets)
    {
        if (entries is null)
        {
            return Array.Empty<SimulatedFriend>();
        }

        var friends = new List<SimulatedFriend>(entries.Count);
        for (var index = 0; index < entries.Count; index++)
        {
            if (entries[index] is not JObject entry)
            {
                continue;
            }

            var name = Text(entry, "name");
            var worldName = Text(entry, "world");
            var homeWorldId = (ushort)sheets.World(worldName);
            var currentWorldId = entry["currentWorld"] is null
                ? homeWorldId
                : (ushort)sheets.World(Text(entry, "currentWorld"));
            var online = entry.Value<bool?>("online") ?? true;
            var classJobId = entry["job"] is null ? 0u : sheets.ClassJob(Text(entry, "job"));
            friends.Add(new SimulatedFriend(StableId(name + "@" + worldName), name, homeWorldId, currentWorldId,
                Text(entry, "freeCompany"), (byte)classJobId, entry.Value<ushort?>("territory") ?? 0, online));
        }

        return friends.ToArray();
    }

    private static SimulatedDailies Dailies(JObject? dailies)
    {
        if (dailies is null)
        {
            return new SimulatedDailies();
        }

        return new SimulatedDailies
        {
            BeastTribeAllowances = dailies.Value<int?>("beastTribeAllowances") ?? 0,
            CustomDeliveryAllowances = dailies.Value<int?>("customDeliveries") ?? 0,
            LeveAllowances = dailies.Value<int?>("leveAllowances") ?? 0,
            WondrousTailsStickers = dailies.Value<int?>("wondrousTailsStickers") ?? 0,
            WondrousTailsExpired = dailies.Value<bool?>("wondrousTailsExpired") ?? false,
            DomanDonated = dailies.Value<int?>("domanDonated") ?? 0,
            DomanAllowance = dailies.Value<int?>("domanAllowance") ?? 0,
            DutyRouletteComplete = dailies.Value<bool?>("dutyRouletteComplete") ?? false,
            HuntBillsUnlocked = dailies.Value<int?>("huntBillsUnlocked") ?? 0,
            HuntBillsComplete = dailies.Value<int?>("huntBillsComplete") ?? 0,
        };
    }

    private static byte[] Customize(JObject character)
    {
        var customize = new byte[CustomizeLength];
        var race = Indexed(character["race"], RaceNames);
        var gender = Text(character, "gender");
        customize[CustomizeRaceIndex] = race;
        customize[CustomizeGenderIndex] = string.Equals(gender, "female", StringComparison.OrdinalIgnoreCase) ? (byte)1 : (byte)0;
        customize[CustomizeTribeIndex] = character.Value<byte?>("tribe") ?? (byte)(race == 0 ? 0 : race * 2 - 1);
        return customize;
    }

    private static byte GrandCompany(JToken? token) => Indexed(token, GrandCompanyNames);

    private static byte Indexed(JToken? token, string[] names)
    {
        if (token is null)
        {
            return 0;
        }

        if (token.Type == JTokenType.Integer)
        {
            return token.Value<byte>();
        }

        var key = Letters(token.Value<string>() ?? string.Empty);
        for (var index = 1; index < names.Length; index++)
        {
            if (string.Equals(names[index], key, StringComparison.Ordinal))
            {
                return (byte)index;
            }
        }

        return 0;
    }

    private static string[] Names(JArray? entries)
    {
        var names = new string[LinkshellSlots];
        for (var index = 0; index < names.Length; index++)
        {
            names[index] = entries is not null && index < entries.Count ? entries[index].Value<string>() ?? string.Empty : string.Empty;
        }

        return names;
    }

    private static int MinuteOfDay(string text)
    {
        var separator = text.IndexOf(':');
        if (separator <= 0 ||
            !int.TryParse(text.AsSpan(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out var hour) ||
            !int.TryParse(text.AsSpan(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var minute) ||
            hour >= HoursPerDay || minute >= SecondsPerMinute)
        {
            return -1;
        }

        return hour * SecondsPerMinute + minute;
    }

    private static string Required(JObject container, string key)
    {
        var value = Text(container, key);
        return value.Length > 0 ? value : throw new InvalidDataException($"the scenario needs character.{key}");
    }

    private static string Text(JObject? container, string key) =>
        container?[key] is JValue { Type: JTokenType.String } value ? (string)value! : string.Empty;

    private static string Letters(string text)
    {
        var builder = new System.Text.StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            if (char.IsLetter(text[index]))
            {
                builder.Append(char.ToLowerInvariant(text[index]));
            }
        }

        return builder.ToString();
    }

    private static ulong StableId(string text)
    {
        var hash = FnvOffsetBasis;
        for (var index = 0; index < text.Length; index++)
        {
            hash ^= text[index];
            hash *= FnvPrime;
        }

        return (hash & ContentIdMask) | ContentIdMarker;
    }
}
