using System.Globalization;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Health;

internal enum MovementKind
{
    None,
    Walking,
    Running,
    Swimming,
    Diving,
    Mounted,
    Flying,
}

internal sealed class HealthTracker : IDisposable
{
    private const long SampleIntervalMs = 100;
    private const long SaveIntervalMs = 60_000;
    private const long GoalCheckIntervalMs = 1000;
    private const long HeightCheckIntervalMs = 5000;
    private const int MaxStoredDays = 60;
    private const double Epsilon = 0.05;
    private const double WalkSpeedMax = 4.0;
    private const double MaxFootPerSec = 25.0;
    private const double MaxMountPerSec = 60.0;
    private const double MaxSwimPerSec = 20.0;

    private static readonly Vector4 Accent = AppAccents.For("health");

    private readonly CharacterWatch watch;
    private readonly NotificationService notifications;
    private readonly HealthStore store;
    private readonly FrameworkTicker ticker;

    private HealthProfile profile = new();
    private ulong contentId;
    private long lastTickMs;
    private long lastSaveMs = Environment.TickCount64;
    private bool dirty;
    private bool loggedError;

    private Vector3 lastPos;
    private uint lastTerritory, lastMap;
    private bool hasBaseline;
    private bool wasLoggedIn;

    private double sWalk, sRun, sSwim, sDive, sMount, sFly, sActive, sKcal, sDrinkMl;
    private int sTeleports, sDrinks;
    private double sTeleY;
    private double onFootSessionSeconds, swimSessionSeconds;

    private Vector3 tpOrigin;
    private uint tpOriginTerritory, tpOriginMap;
    private bool pendingTeleport;

    private long lastDrinkUnix;
    private long lastReminderMs;
    private long lastGoalCheckMs;
    private long lastHeightCheckMs;
    private string cachedDateKey = string.Empty;
    private long cachedDateKeyMs;
    private string pausedReason = string.Empty;

    public HealthTracker(IFramework framework, CharacterWatch watch, NotificationService notifications,
        DirectoryInfo configDirectory)
    {
        this.watch = watch;
        this.notifications = notifications;
        store = new HealthStore(new DirectoryInfo(Path.Combine(configDirectory.FullName, "Health")));
        SessionStartedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        lastReminderMs = Environment.TickCount64;
        ticker = new FrameworkTicker(framework, SampleIntervalMs, OnTick);
        Plugin.ClientState.Logout += OnLogout;
    }

    public HealthProfile Profile => profile;
    public bool IsTracking => contentId != 0;
    public ulong CharacterId => contentId;
    public long SessionStartedUnix { get; private set; }
    public MovementKind CurrentKind { get; private set; }
    public bool IsMoving { get; private set; }
    public HeightSource HeightSource { get; private set; } = HeightSource.Unavailable;
    public double HeightCm { get; private set; }

    public string TrackingStatus
    {
        get
        {
            if (!IsTracking)
            {
                return Loc.T(L.Health.StatusNotLoggedIn);
            }

            return CurrentKind switch
            {
                MovementKind.Swimming or MovementKind.Diving => Loc.T(L.Health.StatusTrackingSwimming),
                MovementKind.Walking or MovementKind.Running => Loc.T(L.Health.StatusTrackingOnFoot),
                _ => pausedReason.Length > 0 ? pausedReason : Loc.T(L.Health.StatusPaused),
            };
        }
    }

    public double SessionOnFootYalms => sWalk + sRun;
    public double SessionSwimYalms => sSwim + sDive;
    public double SessionSwimmingYalms => sSwim;
    public double SessionDivingYalms => sDive;
    public double SessionActiveSeconds => sActive;
    public double SessionCalories => sKcal;
    public int SessionTeleports => sTeleports;

    public void Dispose()
    {
        Plugin.ClientState.Logout -= OnLogout;
        ticker.Dispose();
        SaveNow();
    }

    public void SaveNow()
    {
        if (contentId == 0)
        {
            return;
        }

        store.Save(contentId, profile);
        dirty = false;
        lastSaveMs = Environment.TickCount64;
    }

    public void MarkDirty() => dirty = true;

    public void LogDrink(string kindKey, string customName, double millilitres)
    {
        if (!IsTracking || millilitres <= 0)
        {
            return;
        }

        var day = Today();
        day.Drinks.Add(new HydrationEntry
        {
            Unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            KindKey = kindKey,
            Kind = customName,
            Millilitres = millilitres,
        });
        profile.AllDrinks++;
        profile.AllDrinkMillilitres += millilitres;
        sDrinks++;
        sDrinkMl += millilitres;
        lastDrinkUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        EvaluateGoals(day);
        SaveNow();
    }

    public void UndoLastDrink()
    {
        if (!IsTracking)
        {
            return;
        }

        var day = Today();
        if (day.Drinks.Count == 0)
        {
            return;
        }

        var removed = day.Drinks[day.Drinks.Count - 1];
        day.Drinks.RemoveAt(day.Drinks.Count - 1);
        if (profile.AllDrinks > 0)
        {
            profile.AllDrinks--;
            profile.AllDrinkMillilitres = Math.Max(0, profile.AllDrinkMillilitres - removed.Millilitres);
        }

        if (sDrinks > 0)
        {
            sDrinks--;
            sDrinkMl = Math.Max(0, sDrinkMl - removed.Millilitres);
        }

        SaveNow();
    }

    public void RefreshHeight() => ResolveHeight();

    public void ResetSession()
    {
        sWalk = sRun = sSwim = sDive = sMount = sFly = sActive = sKcal = sDrinkMl = sTeleY = 0;
        sTeleports = sDrinks = 0;
        onFootSessionSeconds = swimSessionSeconds = 0;
        SessionStartedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public void ResetToday()
    {
        var day = Today();
        day.WalkYalms = day.RunYalms = day.SwimYalms = day.DiveYalms = day.MountYalms = day.FlyYalms = 0;
        day.ActiveSeconds = day.Calories = day.TeleportYalms = 0;
        day.Teleports = day.GoalsCompleted = 0;
        day.Drinks.Clear();
        SaveNow();
    }

    public void ResetTodayHydration()
    {
        Today().Drinks.Clear();
        SaveNow();
    }

    public void ResetHistory()
    {
        var keep = Today();
        profile.Days.Clear();
        profile.Days.Add(keep);
        SaveNow();
    }

    public void ResetRecords()
    {
        profile.RecordStepsInDay = 0;
        profile.RecordOnFootYalmsInDay = 0;
        profile.RecordSwimYalmsInDay = 0;
        profile.RecordActiveSecondsInDay = 0;
        profile.LongestOnFootSessionSeconds = 0;
        profile.LongestSwimSessionSeconds = 0;
        profile.LongestTeleportYalms = 0;
        SaveNow();
    }

    public void ResetAll()
    {
        profile = new HealthProfile { SetupCompleted = profile.SetupCompleted, Units = profile.Units };
        profile.Goals = DefaultGoals(profile.DailySwimGoalYalms);
        ResetSession();
        hasBaseline = false;
        SaveNow();
    }

    public static List<HealthGoal> DefaultGoals() => DefaultGoals(500);

    public static List<HealthGoal> DefaultGoals(double swimTarget) => new()
    {
        new HealthGoal { NameKey = GoalKeys.Walk1000, Type = HealthGoalType.Steps, Target = 1000 },
        new HealthGoal { NameKey = GoalKeys.Walk5000, Type = HealthGoalType.Steps, Target = 5000 },
        new HealthGoal { NameKey = GoalKeys.Walk10000, Type = HealthGoalType.Steps, Target = 10000 },
        new HealthGoal
        {
            NameKey = GoalKeys.WalkMalm, Type = HealthGoalType.OnFootDistance, Target = HealthFormat.YalmsPerMalm,
        },
        new HealthGoal { NameKey = GoalKeys.Swim500, Type = HealthGoalType.SwimDistance, Target = swimTarget },
        new HealthGoal { NameKey = GoalKeys.Drinks4, Type = HealthGoalType.HydrationCount, Target = 4 },
        new HealthGoal { NameKey = GoalKeys.Active30, Type = HealthGoalType.ActiveTime, Target = 1800 },
    };

    private void OnTick()
    {
        var now = Environment.TickCount64;
        var delta = lastTickMs == 0 ? 0 : now - lastTickMs;
        lastTickMs = now;
        var dt = Math.Min(delta / 1000.0, 1.0);

        try
        {
            Sample(dt, now);
        }
        catch (Exception exception)
        {
            hasBaseline = false;
            if (!loggedError)
            {
                loggedError = true;
                AepLog.Warning($"Health tracking error: {exception.Message}");
            }
        }
    }

    private void Sample(double dt, long now)
    {
        var id = watch.CurrentContentId;
        if (id != contentId)
        {
            SaveNow();
            contentId = id;
            if (contentId != 0)
            {
                profile = store.Load(contentId);
                if (profile.Goals.Count == 0 && !profile.SetupCompleted)
                {
                    profile.Goals = DefaultGoals(profile.DailySwimGoalYalms);
                }
            }
            else
            {
                profile = new HealthProfile();
            }

            ResetSession();
            hasBaseline = false;
            pendingTeleport = false;
            ResolveHeight();
        }

        var loggedIn = Plugin.ClientState.IsLoggedIn && contentId != 0;
        if (wasLoggedIn && !loggedIn)
        {
            hasBaseline = false;
            pendingTeleport = false;
            SaveNow();
        }

        wasLoggedIn = loggedIn;
        if (!loggedIn)
        {
            CurrentKind = MovementKind.None;
            IsMoving = false;
            pausedReason = Loc.T(L.Health.StatusNotLoggedIn);
            return;
        }

        RollDayIfNeeded();
        HydrationReminderCheck(now);
        if (profile.AutoRefreshHeight && now - lastHeightCheckMs > HeightCheckIntervalMs)
        {
            lastHeightCheckMs = now;
            ResolveHeight();
        }

        var player = Plugin.ObjectTable.LocalPlayer;
        var loading = Plugin.Condition[ConditionFlag.BetweenAreas] || Plugin.Condition[ConditionFlag.BetweenAreas51];
        if (player is null || loading)
        {
            if (loading && hasBaseline && !pendingTeleport)
            {
                tpOrigin = lastPos;
                tpOriginTerritory = lastTerritory;
                tpOriginMap = lastMap;
                pendingTeleport = true;
            }

            hasBaseline = false;
            IsMoving = false;
            CurrentKind = MovementKind.None;
            pausedReason = Loc.T(player is null ? L.Health.StatusPlayerUnavailable : L.Health.StatusLoading);
            return;
        }

        var cur = player.Position;
        var curT = Plugin.ClientState.TerritoryType;
        var curM = Plugin.ClientState.MapId;

        if (pendingTeleport)
        {
            RegisterTeleport(cur, curT, curM);
            SetBaseline(cur, curT, curM);
            dirty = true;
            return;
        }

        if (!hasBaseline)
        {
            SetBaseline(cur, curT, curM);
            return;
        }

        if (curT != lastTerritory || curM != lastMap)
        {
            RegisterTeleport(cur, curT, curM, crossOnly: true);
            SetBaseline(cur, curT, curM);
            dirty = true;
            return;
        }

        var kind = Classify();
        var horiz = Horizontal(lastPos, cur);
        var maxStep = MaxSpeed(kind) * dt;
        if (double.IsFinite(horiz) && horiz > Epsilon && horiz <= maxStep && kind != MovementKind.None)
        {
            Accumulate(kind, horiz, dt);
            IsMoving = true;
        }
        else
        {
            IsMoving = false;
            onFootSessionSeconds = 0;
            swimSessionSeconds = 0;
        }

        CurrentKind = IsMoving ? kind : MovementKind.None;
        if (!IsMoving)
        {
            pausedReason = Loc.T(kind switch
            {
                MovementKind.Mounted => L.Health.StatusMounted,
                MovementKind.Flying => L.Health.StatusFlying,
                _ => L.Health.StatusIdle,
            });
        }

        SetBaseline(cur, curT, curM);

        if (dirty && now - lastSaveMs > SaveIntervalMs)
        {
            SaveNow();
        }
    }

    private MovementKind Classify()
    {
        var c = Plugin.Condition;
        if (c[ConditionFlag.Diving])
        {
            return MovementKind.Diving;
        }

        if (c[ConditionFlag.Swimming])
        {
            return MovementKind.Swimming;
        }

        if (c[ConditionFlag.InFlight])
        {
            return MovementKind.Flying;
        }

        if (c[ConditionFlag.Mounted] || c[ConditionFlag.RidingPillion])
        {
            return c[ConditionFlag.BeingMoved] ? MovementKind.None : MovementKind.Mounted;
        }

        if (c[ConditionFlag.BeingMoved] || c[ConditionFlag.Occupied38] || c[ConditionFlag.WatchingCutscene] ||
            c[ConditionFlag.WatchingCutscene78] || c[ConditionFlag.OccupiedInCutSceneEvent] ||
            c[ConditionFlag.Unconscious])
        {
            return MovementKind.None;
        }

        return MovementKind.Walking;
    }

    private static double MaxSpeed(MovementKind kind) => kind switch
    {
        MovementKind.Mounted => MaxMountPerSec,
        MovementKind.Flying => MaxMountPerSec,
        MovementKind.Swimming or MovementKind.Diving => MaxSwimPerSec,
        _ => MaxFootPerSec,
    };

    private void Accumulate(MovementKind kind, double horiz, double dt)
    {
        var day = Today();
        var speed = dt > 0 ? horiz / dt : 0;
        switch (kind)
        {
            case MovementKind.Walking:
            case MovementKind.Running:
                var running = speed > WalkSpeedMax;
                if (running)
                {
                    day.RunYalms += horiz;
                    sRun += horiz;
                    profile.AllRunYalms += horiz;
                }
                else
                {
                    day.WalkYalms += horiz;
                    sWalk += horiz;
                    profile.AllWalkYalms += horiz;
                }

                AddActive(day, dt, running ? MovementKind.Running : MovementKind.Walking);
                onFootSessionSeconds += dt;
                swimSessionSeconds = 0;
                if (onFootSessionSeconds > profile.LongestOnFootSessionSeconds)
                {
                    profile.LongestOnFootSessionSeconds = onFootSessionSeconds;
                }

                break;
            case MovementKind.Swimming:
                day.SwimYalms += horiz;
                sSwim += horiz;
                profile.AllSwimYalms += horiz;
                AddActive(day, dt, MovementKind.Swimming);
                swimSessionSeconds += dt;
                onFootSessionSeconds = 0;
                if (swimSessionSeconds > profile.LongestSwimSessionSeconds)
                {
                    profile.LongestSwimSessionSeconds = swimSessionSeconds;
                }

                break;
            case MovementKind.Diving:
                day.DiveYalms += horiz;
                sDive += horiz;
                profile.AllDiveYalms += horiz;
                AddActive(day, dt, MovementKind.Diving);
                swimSessionSeconds += dt;
                onFootSessionSeconds = 0;
                break;
            case MovementKind.Mounted:
                day.MountYalms += horiz;
                sMount += horiz;
                profile.AllMountYalms += horiz;
                break;
            case MovementKind.Flying:
                day.FlyYalms += horiz;
                sFly += horiz;
                profile.AllFlyYalms += horiz;
                break;
        }

        UpdateRecords(day);
        EvaluateGoals(day, force: false);
        dirty = true;
    }

    private void AddActive(HealthDay day, double dt, MovementKind kind)
    {
        day.ActiveSeconds += dt;
        sActive += dt;
        profile.AllActiveSeconds += dt;
        if (profile.CaloriesEnabled && profile.WeightKg is { } weight)
        {
            var kcal = HealthFormat.Calories(kind, dt, weight);
            day.Calories += kcal;
            sKcal += kcal;
            profile.AllCalories += kcal;
        }
    }

    private void UpdateRecords(HealthDay day)
    {
        var steps = HealthFormat.Steps(day.OnFootYalms, profile.StrideYalms);
        if (steps > profile.RecordStepsInDay)
        {
            profile.RecordStepsInDay = steps;
        }

        if (day.OnFootYalms > profile.RecordOnFootYalmsInDay)
        {
            profile.RecordOnFootYalmsInDay = day.OnFootYalms;
        }

        if (day.SwimTotalYalms > profile.RecordSwimYalmsInDay)
        {
            profile.RecordSwimYalmsInDay = day.SwimTotalYalms;
        }

        if (day.ActiveSeconds > profile.RecordActiveSecondsInDay)
        {
            profile.RecordActiveSecondsInDay = day.ActiveSeconds;
        }
    }

    private void RegisterTeleport(Vector3 cur, uint curT, uint curM, bool crossOnly = false)
    {
        var day = Today();
        day.Teleports++;
        profile.AllTeleports++;
        sTeleports++;
        pendingTeleport = false;
        if (crossOnly)
        {
            return;
        }

        if (tpOriginTerritory == curT && tpOriginMap == curM)
        {
            var d = Horizontal(tpOrigin, cur);
            if (double.IsFinite(d) && d > 0)
            {
                day.TeleportYalms += d;
                profile.AllTeleportYalms += d;
                sTeleY += d;
                if (d > profile.LongestTeleportYalms)
                {
                    profile.LongestTeleportYalms = d;
                }
            }
        }
    }

    private void SetBaseline(Vector3 pos, uint territory, uint map)
    {
        lastPos = pos;
        lastTerritory = territory;
        lastMap = map;
        hasBaseline = true;
    }

    private static string BuildDateKey() => DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private string DateKey()
    {
        var now = Environment.TickCount64;
        if (cachedDateKey.Length == 0 || now - cachedDateKeyMs >= 1000)
        {
            cachedDateKey = BuildDateKey();
            cachedDateKeyMs = now;
        }

        return cachedDateKey;
    }

    private HealthDay Today()
    {
        var key = DateKey();
        var days = profile.Days;
        if (days.Count > 0 && days[days.Count - 1].Date == key)
        {
            return days[days.Count - 1];
        }

        var day = new HealthDay { Date = key };
        days.Add(day);
        while (days.Count > MaxStoredDays)
        {
            days.RemoveAt(0);
        }

        dirty = true;
        return day;
    }

    private void RollDayIfNeeded()
    {
        var key = DateKey();
        var latest = profile.LatestDay;
        if (latest is null || latest.Date == key)
        {
            return;
        }

        var steps = HealthFormat.Steps(latest.OnFootYalms, profile.StrideYalms);
        if (steps >= profile.DailyStepGoal)
        {
            profile.StreakDays = profile.StreakLastDate == PreviousDateKey(latest.Date)
                ? profile.StreakDays + 1
                : 1;
            profile.StreakLastDate = latest.Date;
        }
        else if (profile.StreakLastDate != latest.Date)
        {
            profile.StreakDays = 0;
        }

        _ = Today();
        dirty = true;
    }

    private static string PreviousDateKey(string todayKey)
    {
        return DateTime.TryParseExact(todayKey, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
            out var parsed)
            ? parsed.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : string.Empty;
    }

    private void ResolveHeight()
    {
        if (profile.ManualHeightCm is { } manual && manual > 0)
        {
            HeightCm = manual;
            HeightSource = HeightSource.Manual;
            return;
        }

        var player = Plugin.ObjectTable.LocalPlayer;
        if (player is not null)
        {
            try
            {
                var customize = player.Customize;
                if (customize.Length >= 5)
                {
                    var female = customize[1] == 1;
                    var slider = customize[3];
                    var tribe = customize[4];
                    var cm = HealthFormat.HeightCm(tribe, female, slider);
                    if (cm > 0)
                    {
                        HeightCm = cm;
                        HeightSource = HeightSource.Game;
                        return;
                    }
                }
            }
            catch (Exception exception)
            {
                if (!loggedError)
                {
                    loggedError = true;
                    AepLog.Warning($"Health height read failed: {exception.Message}");
                }
            }
        }

        HeightCm = 0;
        HeightSource = HeightSource.Unavailable;
    }

    private void HydrationReminderCheck(long now)
    {
        if (!profile.HydrationRemindersEnabled)
        {
            return;
        }

        var intervalMs = profile.ReminderIntervalMinutes * 60_000L;
        if (now - lastReminderMs < intervalMs)
        {
            return;
        }

        var lastDrink = lastDrinkUnix;
        var latest = profile.LatestDay;
        if (latest is { Drinks.Count: > 0 })
        {
            lastDrink = Math.Max(lastDrink, latest.Drinks[latest.Drinks.Count - 1].Unix);
        }

        var sinceDrinkSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - lastDrink;
        if (lastDrink != 0 && sinceDrinkSeconds < profile.ReminderIntervalMinutes * 60L)
        {
            return;
        }

        if (InQuietHours(DateTime.Now) || RemindersSuppressed())
        {
            return;
        }

        lastReminderMs = now;
        notifications.Notify(new PhoneNotification("health", Loc.T(L.Health.NotifyHydrationTitle),
            Loc.T(L.Health.NotifyHydrationBody), DateTime.Now, Accent, "health.hydration"));
    }

    private bool InQuietHours(DateTime now)
    {
        var start = profile.QuietStartHour * 60 + profile.QuietStartMinute;
        var end = profile.QuietEndHour * 60 + profile.QuietEndMinute;
        if (start == end)
        {
            return false;
        }

        var current = now.Hour * 60 + now.Minute;
        return start < end ? current >= start && current < end : current >= start || current < end;
    }

    private bool RemindersSuppressed() => profile.ReminderPauseInDuties && PlayerBusy.Now;

    public (double Current, double Target) GoalProgress(HealthGoal goal)
    {
        return (GoalValue(goal.Type, goal.Scope), goal.Target);
    }

    private void EvaluateGoals(HealthDay day) => EvaluateGoals(day, force: true);

    private void EvaluateGoals(HealthDay day, bool force)
    {
        var now = Environment.TickCount64;
        if (!force && now - lastGoalCheckMs < GoalCheckIntervalMs)
        {
            return;
        }

        lastGoalCheckMs = now;
        for (var index = 0; index < profile.Goals.Count; index++)
        {
            var goal = profile.Goals[index];
            if (!goal.Enabled || goal.Target <= 0)
            {
                continue;
            }

            var current = GoalValue(goal.Type, goal.Scope);
            var key = ScopeKey(goal.Scope);
            if (current + 0.0001 < goal.Target)
            {
                continue;
            }

            if (goal.CompletedKey == key)
            {
                continue;
            }

            goal.CompletedKey = key;
            day.GoalsCompleted++;
            dirty = true;
            notifications.Notify(new PhoneNotification("health", Loc.T(L.Health.NotifyGoalTitle),
                Loc.T(L.Health.NotifyGoalBody, HealthFormat.GoalName(goal)), DateTime.Now, Accent, "health.goal." + goal.Id));
        }
    }

    private string ScopeKey(HealthGoalScope scope) => scope switch
    {
        HealthGoalScope.Daily => DateKey(),
        HealthGoalScope.Weekly => WeekStartKey(),
        HealthGoalScope.Session => SessionStartedUnix.ToString(CultureInfo.InvariantCulture),
        _ => "all",
    };

    private static string WeekStartKey()
    {
        var today = DateTime.Now.Date;
        var offset = (int)today.DayOfWeek;
        return today.AddDays(-offset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private double GoalValue(HealthGoalType type, HealthGoalScope scope) => type switch
    {
        HealthGoalType.Steps => HealthFormat.Steps(Scoped(scope, d => d.OnFootYalms, SessionOnFootYalms,
            profile.AllWalkYalms + profile.AllRunYalms), profile.StrideYalms),
        HealthGoalType.OnFootDistance => Scoped(scope, d => d.OnFootYalms, SessionOnFootYalms,
            profile.AllWalkYalms + profile.AllRunYalms),
        HealthGoalType.WalkDistance => Scoped(scope, d => d.WalkYalms, sWalk, profile.AllWalkYalms),
        HealthGoalType.RunDistance => Scoped(scope, d => d.RunYalms, sRun, profile.AllRunYalms),
        HealthGoalType.SwimDistance => Scoped(scope, d => d.SwimTotalYalms, SessionSwimYalms,
            profile.AllSwimYalms + profile.AllDiveYalms),
        HealthGoalType.ActiveTime => Scoped(scope, d => d.ActiveSeconds, sActive, profile.AllActiveSeconds),
        HealthGoalType.HydrationCount => Scoped(scope, d => d.DrinkCount, sDrinks, profile.AllDrinks),
        HealthGoalType.HydrationVolume => Scoped(scope, d => d.DrinkMillilitres, sDrinkMl, profile.AllDrinkMillilitres),
        HealthGoalType.Teleports => Scoped(scope, d => d.Teleports, sTeleports, profile.AllTeleports),
        HealthGoalType.TeleportDistance => Scoped(scope, d => d.TeleportYalms, sTeleY, profile.AllTeleportYalms),
        HealthGoalType.Calories => Scoped(scope, d => d.Calories, sKcal, profile.AllCalories),
        _ => 0d,
    };

    private double Scoped(HealthGoalScope scope, Func<HealthDay, double> perDay, double session, double allTime)
    {
        return scope switch
        {
            HealthGoalScope.Session => session,
            HealthGoalScope.AllTime => allTime,
            HealthGoalScope.Weekly => WeekSum(perDay),
            _ => profile.LatestDay is { } day ? perDay(day) : 0d,
        };
    }

    private double WeekSum(Func<HealthDay, double> perDay)
    {
        var cutoff = DateTime.Now.Date.AddDays(-6).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var sum = 0d;
        for (var index = profile.Days.Count - 1; index >= 0; index--)
        {
            var day = profile.Days[index];
            if (string.CompareOrdinal(day.Date, cutoff) < 0)
            {
                break;
            }

            sum += perDay(day);
        }

        return sum;
    }

    private void OnLogout(int type, int code)
    {
        SaveNow();
        hasBaseline = false;
        pendingTeleport = false;
    }

    private static double Horizontal(Vector3 a, Vector3 b)
    {
        double dx = a.X - b.X, dz = a.Z - b.Z;
        return Math.Sqrt(dx * dx + dz * dz);
    }
}
