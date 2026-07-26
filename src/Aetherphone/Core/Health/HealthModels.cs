using Newtonsoft.Json;

namespace Aetherphone.Core.Health;

internal enum HealthUnits
{
    Eorzean,
    Metric,
    Imperial,
}

internal enum HeightSource
{
    Unavailable,
    Game,
    Manual,
}

internal enum HealthGoalType
{
    Steps,
    OnFootDistance,
    WalkDistance,
    RunDistance,
    SwimDistance,
    ActiveTime,
    HydrationCount,
    HydrationVolume,
    Teleports,
    TeleportDistance,
    Calories,
}

internal enum HealthGoalScope
{
    Daily,
    Weekly,
    Session,
    AllTime,
}

internal static class DrinkKeys
{
    public const string Water = "water";
    public const string Tea = "tea";
    public const string Coffee = "coffee";
    public const string Juice = "juice";

    public static readonly string[] All = { Water, Tea, Coffee, Juice };
}

internal static class GoalKeys
{
    public const string Walk1000 = "walk1000";
    public const string Walk5000 = "walk5000";
    public const string Walk10000 = "walk10000";
    public const string WalkMalm = "walkMalm";
    public const string Swim500 = "swim500";
    public const string Drinks4 = "drinks4";
    public const string Active30 = "active30";
    public const string New = "new";
}

internal sealed class HydrationEntry
{
    [JsonProperty("t")] public long Unix { get; set; }

    // Built-in kind key ("water", "tea", ...) resolved at draw time. Empty for custom drinks,
    // which fall back to the free-text Kind below.
    [JsonProperty("kk")] public string KindKey { get; set; } = string.Empty;

    [JsonProperty("k")] public string Kind { get; set; } = string.Empty;

    [JsonProperty("ml")] public double Millilitres { get; set; }
}

internal sealed class HealthGoal
{
    [JsonProperty("id")] public string Id { get; set; } = Guid.NewGuid().ToString("N");

    // Built-in goal key resolved at draw time. Empty once the user renames the goal, which
    // makes Name authoritative.
    [JsonProperty("nk")] public string NameKey { get; set; } = string.Empty;

    [JsonProperty("name")] public string Name { get; set; } = string.Empty;

    [JsonProperty("type")] public HealthGoalType Type { get; set; }

    [JsonProperty("scope")] public HealthGoalScope Scope { get; set; } = HealthGoalScope.Daily;

    [JsonProperty("target")] public double Target { get; set; } = 1;

    [JsonProperty("enabled")] public bool Enabled { get; set; } = true;

    // Scope key (e.g. the date) this goal was last completed for; prevents duplicate notifications.
    [JsonProperty("done")] public string CompletedKey { get; set; } = string.Empty;
}

internal sealed class HealthDay
{
    [JsonProperty("date")] public string Date { get; set; } = string.Empty;

    [JsonProperty("walk")] public double WalkYalms { get; set; }

    [JsonProperty("run")] public double RunYalms { get; set; }

    [JsonProperty("swim")] public double SwimYalms { get; set; }

    [JsonProperty("dive")] public double DiveYalms { get; set; }

    [JsonProperty("mount")] public double MountYalms { get; set; }

    [JsonProperty("fly")] public double FlyYalms { get; set; }

    [JsonProperty("active")] public double ActiveSeconds { get; set; }

    [JsonProperty("kcal")] public double Calories { get; set; }

    [JsonProperty("tp")] public int Teleports { get; set; }

    [JsonProperty("tpy")] public double TeleportYalms { get; set; }

    [JsonProperty("goals")] public int GoalsCompleted { get; set; }

    [JsonProperty("drinks")] public List<HydrationEntry> Drinks { get; set; } = new();

    [JsonIgnore] public double OnFootYalms => WalkYalms + RunYalms;

    [JsonIgnore] public double SwimTotalYalms => SwimYalms + DiveYalms;

    [JsonIgnore] public int DrinkCount => Drinks.Count;

    [JsonIgnore] public double DrinkMillilitres
    {
        get
        {
            var sum = 0d;
            for (var index = 0; index < Drinks.Count; index++)
            {
                sum += Drinks[index].Millilitres;
            }

            return sum;
        }
    }
}

internal sealed class HealthProfile
{
    [JsonProperty("version")] public int Version { get; set; } = 1;

    // Profile / setup
    [JsonProperty("setup")] public bool SetupCompleted { get; set; }
    [JsonProperty("units")] public HealthUnits Units { get; set; } = HealthUnits.Eorzean;
    [JsonProperty("manualHeightCm")] public double? ManualHeightCm { get; set; }
    [JsonProperty("weightKg")] public double? WeightKg { get; set; }
    [JsonProperty("calories")] public bool CaloriesEnabled { get; set; } = true;
    [JsonProperty("stride")] public double StrideYalms { get; set; } = 0.75;
    [JsonProperty("autoHeight")] public bool AutoRefreshHeight { get; set; } = true;

    // Daily targets used by the Overview rings and the default goals
    [JsonProperty("goalSteps")] public int DailyStepGoal { get; set; } = 10000;
    [JsonProperty("goalSwim")] public double DailySwimGoalYalms { get; set; } = 500;
    [JsonProperty("goalDrinks")] public int DailyHydrationGoal { get; set; } = 4;

    [JsonProperty("goalList")] public List<HealthGoal> Goals { get; set; } = new();

    // History (most recent last). Trimmed to a bounded window by the tracker.
    [JsonProperty("days")] public List<HealthDay> Days { get; set; } = new();

    // All-time totals (yalms / seconds / count)
    [JsonProperty("allWalk")] public double AllWalkYalms { get; set; }
    [JsonProperty("allRun")] public double AllRunYalms { get; set; }
    [JsonProperty("allSwim")] public double AllSwimYalms { get; set; }
    [JsonProperty("allDive")] public double AllDiveYalms { get; set; }
    [JsonProperty("allMount")] public double AllMountYalms { get; set; }
    [JsonProperty("allFly")] public double AllFlyYalms { get; set; }
    [JsonProperty("allActive")] public double AllActiveSeconds { get; set; }
    [JsonProperty("allKcal")] public double AllCalories { get; set; }
    [JsonProperty("allTp")] public int AllTeleports { get; set; }
    [JsonProperty("allTpY")] public double AllTeleportYalms { get; set; }
    [JsonProperty("allDrinks")] public int AllDrinks { get; set; }
    [JsonProperty("allDrinkMl")] public double AllDrinkMillilitres { get; set; }

    // Personal records
    [JsonProperty("recSteps")] public long RecordStepsInDay { get; set; }
    [JsonProperty("recOnFoot")] public double RecordOnFootYalmsInDay { get; set; }
    [JsonProperty("recSwim")] public double RecordSwimYalmsInDay { get; set; }
    [JsonProperty("recActive")] public double RecordActiveSecondsInDay { get; set; }
    [JsonProperty("recFootSession")] public double LongestOnFootSessionSeconds { get; set; }
    [JsonProperty("recSwimSession")] public double LongestSwimSessionSeconds { get; set; }
    [JsonProperty("recTeleport")] public double LongestTeleportYalms { get; set; }

    // Streak of consecutive days that met the daily step goal
    [JsonProperty("streak")] public int StreakDays { get; set; }
    [JsonProperty("streakDate")] public string StreakLastDate { get; set; } = string.Empty;

    // Hydration reminders
    [JsonProperty("remind")] public bool HydrationRemindersEnabled { get; set; }
    [JsonProperty("remindMins")] public int ReminderIntervalMinutes { get; set; } = 120;
    [JsonProperty("quietStart")] public int QuietStartHour { get; set; } = 22;
    [JsonProperty("quietStartMin")] public int QuietStartMinute { get; set; }
    [JsonProperty("quietEnd")] public int QuietEndHour { get; set; } = 8;
    [JsonProperty("quietEndMin")] public int QuietEndMinute { get; set; }
    [JsonProperty("remindPause")] public bool ReminderPauseInDuties { get; set; } = true;

    [JsonIgnore] public HealthDay? LatestDay => Days.Count > 0 ? Days[Days.Count - 1] : null;
}
