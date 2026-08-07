using Newtonsoft.Json;

namespace Aetherphone.Core.Health;

internal sealed class HealthStore
{
    private readonly DirectoryInfo root;

    public HealthStore(DirectoryInfo root)
    {
        this.root = root;
        if (!root.Exists)
        {
            root.Create();
        }
    }

    public HealthProfile Load(ulong contentId)
    {
        var path = PathFor(contentId);
        if (!File.Exists(path))
        {
            return new HealthProfile();
        }

        try
        {
            var loaded = JsonConvert.DeserializeObject<HealthProfile>(File.ReadAllText(path));
            return loaded is null ? new HealthProfile() : Sanitize(loaded);
        }
        catch (Exception exception)
        {
            AepLog.Warning($"HealthStore load failed for {contentId:X16}: {exception.Message}");
            return new HealthProfile();
        }
    }

    public void Save(ulong contentId, HealthProfile profile)
    {
        if (contentId == 0)
        {
            return;
        }

        try
        {
            var path = PathFor(contentId);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonConvert.SerializeObject(profile));
            File.Move(temp, path, true);
        }
        catch (Exception exception)
        {
            AepLog.Warning($"HealthStore write failed for {contentId:X16}: {exception.Message}");
        }
    }

    private static HealthProfile Sanitize(HealthProfile profile)
    {
        profile.StrideYalms = profile.StrideYalms is > 0.05 and < 10 ? profile.StrideYalms : 0.75;
        if (profile.WeightKg is { } weight && (!double.IsFinite(weight) || weight <= 0))
        {
            profile.WeightKg = null;
        }

        if (profile.ManualHeightCm is { } height && (!double.IsFinite(height) || height <= 0))
        {
            profile.ManualHeightCm = null;
        }

        profile.DailyStepGoal = Math.Clamp(profile.DailyStepGoal, 1, 1_000_000);
        profile.DailyHydrationGoal = Math.Clamp(profile.DailyHydrationGoal, 1, 100);
        profile.ReminderIntervalMinutes = Math.Clamp(profile.ReminderIntervalMinutes, 1, 720);
        profile.QuietStartHour = Math.Clamp(profile.QuietStartHour, 0, 23);
        profile.QuietEndHour = Math.Clamp(profile.QuietEndHour, 0, 23);
        profile.QuietStartMinute = Math.Clamp(profile.QuietStartMinute, 0, 59);
        profile.QuietEndMinute = Math.Clamp(profile.QuietEndMinute, 0, 59);
        profile.AllWalkYalms = Clean(profile.AllWalkYalms);
        profile.AllRunYalms = Clean(profile.AllRunYalms);
        profile.AllSwimYalms = Clean(profile.AllSwimYalms);
        profile.AllDiveYalms = Clean(profile.AllDiveYalms);
        profile.AllMountYalms = Clean(profile.AllMountYalms);
        profile.AllFlyYalms = Clean(profile.AllFlyYalms);
        profile.AllActiveSeconds = Clean(profile.AllActiveSeconds);
        profile.AllCalories = Clean(profile.AllCalories);
        profile.AllTeleportYalms = Clean(profile.AllTeleportYalms);
        profile.AllDrinkMillilitres = Clean(profile.AllDrinkMillilitres);
        profile.AllDrinks = Math.Max(0, profile.AllDrinks);
        profile.AllTeleports = Math.Max(0, profile.AllTeleports);
        profile.RecordStepsInDay = Math.Max(0, profile.RecordStepsInDay);
        profile.RecordOnFootYalmsInDay = Clean(profile.RecordOnFootYalmsInDay);
        profile.RecordSwimYalmsInDay = Clean(profile.RecordSwimYalmsInDay);
        profile.RecordActiveSecondsInDay = Clean(profile.RecordActiveSecondsInDay);
        profile.LongestOnFootSessionSeconds = Clean(profile.LongestOnFootSessionSeconds);
        profile.LongestSwimSessionSeconds = Clean(profile.LongestSwimSessionSeconds);
        profile.LongestTeleportYalms = Clean(profile.LongestTeleportYalms);
        profile.StreakDays = Math.Max(0, profile.StreakDays);
        profile.Goals ??= new List<HealthGoal>();
        profile.Days ??= new List<HealthDay>();
        profile.Days.RemoveAll(day => day is null || string.IsNullOrWhiteSpace(day.Date));
        for (var index = 0; index < profile.Days.Count; index++)
        {
            var day = profile.Days[index];
            day.WalkYalms = Clean(day.WalkYalms);
            day.RunYalms = Clean(day.RunYalms);
            day.SwimYalms = Clean(day.SwimYalms);
            day.DiveYalms = Clean(day.DiveYalms);
            day.MountYalms = Clean(day.MountYalms);
            day.FlyYalms = Clean(day.FlyYalms);
            day.ActiveSeconds = Clean(day.ActiveSeconds);
            day.Calories = Clean(day.Calories);
            day.TeleportYalms = Clean(day.TeleportYalms);
            day.Teleports = Math.Max(0, day.Teleports);
            day.GoalsCompleted = Math.Max(0, day.GoalsCompleted);
            day.Drinks ??= new List<HydrationEntry>();
            day.Drinks.RemoveAll(drink => drink is null || !double.IsFinite(drink.Millilitres) ||
                                          drink.Millilitres <= 0);
        }

        profile.Goals.RemoveAll(goal => goal is null || goal.Target <= 0);
        for (var index = 0; index < profile.Goals.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(profile.Goals[index].Id))
            {
                profile.Goals[index].Id = Guid.NewGuid().ToString("N");
            }
        }

        return profile;
    }

    private static double Clean(double value) => double.IsFinite(value) && value > 0 ? value : 0d;

    private string PathFor(ulong contentId) => Path.Combine(root.FullName, contentId.ToString("X16") + ".json");
}
