using System.Globalization;
using Aetherphone.Core.Game;
using Dalamud.Game.DutyState;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace Aetherphone.Core.Activity;

internal sealed unsafe class ActivityTracker : IDisposable
{
    private const long TickIntervalMilliseconds = 2000;
    private const long MaxCountedTickMilliseconds = 10000;
    private const int CollectionSampleEveryTicks = 30;
    private const int SaveEveryTicks = 30;
    private const int MaxCollectionGainPerSample = 8;
    private const int MaxStoredDays = 60;

    private readonly IClientState clientState;
    private readonly IDutyState dutyState;
    private readonly GameData gameData;
    private readonly ActivityStore store;
    private readonly FrameworkTicker ticker;
    private readonly ActivityDay session = new();
    private ActivityLedger ledger = new();
    private ActivityDay today = new();
    private ulong contentId;
    private long lastTickMilliseconds;
    private long playCarryMilliseconds;
    private int tickCounter;
    private bool dirty;
    private bool hasJobBaseline;
    private uint baselineJobId;
    private int baselineLevel;
    private long baselineExp;
    private long baselineNeededExp;
    private long baselineGil = -1;
    private int baselineMounts = -1;
    private int baselineMinions = -1;

    public ActivityTracker(IFramework framework, IClientState clientState, IDutyState dutyState, GameData gameData,
        DirectoryInfo configDirectory)
    {
        this.clientState = clientState;
        this.dutyState = dutyState;
        this.gameData = gameData;
        store = new ActivityStore(new DirectoryInfo(Path.Combine(configDirectory.FullName, "Activity")));
        ticker = new FrameworkTicker(framework, TickIntervalMilliseconds, OnTick);
        dutyState.DutyCompleted += OnDutyCompleted;
        clientState.Logout += OnLogout;
        SessionStartedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public ActivityDay Today => today;
    public ActivityDay Session => session;
    public IReadOnlyList<ActivityDay> Days => ledger.Days;
    public long SessionStartedUnix { get; private set; }
    public bool IsTracking => contentId != 0;
    public int RetainerCount { get; private set; }
    public int VenturesReady { get; private set; }
    public int VenturesActive { get; private set; }

    public void MarkDirty() => dirty = true;

    public void Dispose()
    {
        clientState.Logout -= OnLogout;
        dutyState.DutyCompleted -= OnDutyCompleted;
        ticker.Dispose();
        SaveIfDirty();
    }

    private void OnDutyCompleted(IDutyStateEventArgs args)
    {
        if (contentId == 0)
        {
            return;
        }

        today.DutiesCompleted++;
        session.DutiesCompleted++;
        dirty = true;
    }

    private void OnLogout(int type, int code)
    {
        SaveIfDirty();
        contentId = 0;
        ResetSession();
        ResetBaselines();
        RetainerCount = 0;
        VenturesReady = 0;
        VenturesActive = 0;
    }

    private void OnTick()
    {
        var nowMilliseconds = Environment.TickCount64;
        var elapsed = lastTickMilliseconds == 0 ? 0 : nowMilliseconds - lastTickMilliseconds;
        lastTickMilliseconds = nowMilliseconds;
        var playerState = PlayerState.Instance();
        var player = gameData.LocalPlayer;
        if (playerState is null || player is null || playerState->ContentId == 0)
        {
            ResetBaselines();
            return;
        }

        if (playerState->ContentId != contentId)
        {
            SaveIfDirty();
            contentId = playerState->ContentId;
            ledger = store.Load(contentId);
            today = DayFor(LocalDateKey());
            ResetSession();
            ResetBaselines();
        }

        RollDayIfNeeded();
        AddPlayTime(elapsed);
        SampleJob(playerState, player.ClassJob.RowId);
        SampleGil();
        SampleRetainers();
        if (tickCounter % CollectionSampleEveryTicks == 0)
        {
            SampleCollection(playerState);
        }

        tickCounter++;
        if (dirty && tickCounter % SaveEveryTicks == 0)
        {
            SaveIfDirty();
        }
    }

    private static string LocalDateKey() => DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private ActivityDay DayFor(string dateKey)
    {
        var days = ledger.Days;
        if (days.Count > 0 && days[days.Count - 1].Date == dateKey)
        {
            return days[days.Count - 1];
        }

        var day = new ActivityDay { Date = dateKey };
        days.Add(day);
        while (days.Count > MaxStoredDays)
        {
            days.RemoveAt(0);
        }

        return day;
    }

    private void RollDayIfNeeded()
    {
        var dateKey = LocalDateKey();
        if (today.Date == dateKey)
        {
            return;
        }

        today = DayFor(dateKey);
        dirty = true;
    }

    private void AddPlayTime(long elapsedMilliseconds)
    {
        if (elapsedMilliseconds <= 0)
        {
            return;
        }

        if (elapsedMilliseconds > MaxCountedTickMilliseconds)
        {
            elapsedMilliseconds = MaxCountedTickMilliseconds;
        }

        playCarryMilliseconds += elapsedMilliseconds;
        var seconds = playCarryMilliseconds / 1000;
        if (seconds <= 0)
        {
            return;
        }

        playCarryMilliseconds -= seconds * 1000;
        today.PlaySeconds += seconds;
        session.PlaySeconds += seconds;
        dirty = true;
    }

    private void SampleJob(PlayerState* playerState, uint jobId)
    {
        var expArrayIndex = gameData.JobExpArrayIndex(jobId);
        var levels = playerState->ClassJobLevels;
        var level = expArrayIndex >= 0 && expArrayIndex < levels.Length ? (int)levels[expArrayIndex] : 0;
        if (level <= 0)
        {
            hasJobBaseline = false;
            return;
        }

        var maxLevel = playerState->MaxLevel > 0 && level >= playerState->MaxLevel;
        long exp = maxLevel ? 0 : playerState->GetCurrentClassJobExp();
        long neededExp = maxLevel ? 0 : playerState->GetCurrentClassJobNeededExp();
        if (hasJobBaseline && jobId == baselineJobId && level == baselineLevel)
        {
            var delta = exp - baselineExp;
            if (delta > 0)
            {
                AddExperience(delta, neededExp > 0 ? delta / (float)neededExp : 0f, 0);
            }
        }
        else if (hasJobBaseline && jobId == baselineJobId && level > baselineLevel)
        {
            var gained = baselineNeededExp > baselineExp ? baselineNeededExp - baselineExp : 0;
            var units = baselineNeededExp > 0 ? (baselineNeededExp - baselineExp) / (float)baselineNeededExp : 0f;
            for (var midLevel = baselineLevel + 1; midLevel < level; midLevel++)
            {
                gained += gameData.ExpToNextLevel(midLevel);
                units += 1f;
            }

            if (exp > 0)
            {
                gained += exp;
                units += neededExp > 0 ? exp / (float)neededExp : 0f;
            }

            AddExperience(gained, units, level - baselineLevel);
        }

        hasJobBaseline = true;
        baselineJobId = jobId;
        baselineLevel = level;
        baselineExp = exp;
        baselineNeededExp = neededExp;
    }

    private void AddExperience(long expGained, float levelUnits, int levelsGained)
    {
        if (expGained <= 0 && levelsGained <= 0)
        {
            return;
        }

        today.ExpGained += expGained;
        session.ExpGained += expGained;
        today.LevelUnitsGained += levelUnits;
        session.LevelUnitsGained += levelUnits;
        today.LevelsGained += levelsGained;
        session.LevelsGained += levelsGained;
        dirty = true;
    }

    private void SampleGil()
    {
        var manager = InventoryManager.Instance();
        if (manager is null)
        {
            return;
        }

        var gil = (long)manager->GetGil();
        if (baselineGil >= 0 && gil > baselineGil)
        {
            today.GilEarned += gil - baselineGil;
            session.GilEarned += gil - baselineGil;
            dirty = true;
        }

        baselineGil = gil;
    }

    private void SampleRetainers()
    {
        RetainerCount = 0;
        VenturesReady = 0;
        VenturesActive = 0;
        var manager = RetainerManager.Instance();
        if (manager is null)
        {
            return;
        }

        var retainerCount = manager->GetRetainerCount();
        if (retainerCount == 0)
        {
            return;
        }

        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var count = 0;
        var ready = 0;
        var active = 0;
        for (var index = 0u; index < retainerCount; index++)
        {
            var retainer = manager->GetRetainerBySortedIndex(index);
            if (retainer is null)
            {
                continue;
            }

            count++;
            if (retainer->VentureId == 0)
            {
                continue;
            }

            if (retainer->VentureComplete <= nowUnix)
            {
                ready++;
            }
            else
            {
                active++;
            }
        }

        RetainerCount = count;
        VenturesReady = ready;
        VenturesActive = active;
    }

    private void SampleCollection(PlayerState* playerState)
    {
        var mounts = 0;
        var mountIds = gameData.CollectableMountIds();
        for (var index = 0; index < mountIds.Length; index++)
        {
            if (playerState->IsMountUnlocked(mountIds[index]))
            {
                mounts++;
            }
        }

        if (baselineMounts >= 0)
        {
            var gained = mounts - baselineMounts;
            if (gained > 0 && gained <= MaxCollectionGainPerSample)
            {
                today.MountsGained += gained;
                session.MountsGained += gained;
                dirty = true;
            }
        }

        baselineMounts = mounts;
        var uiState = UIState.Instance();
        if (uiState is null)
        {
            return;
        }

        var minions = 0;
        var minionIds = gameData.CollectableMinionIds();
        for (var index = 0; index < minionIds.Length; index++)
        {
            if (uiState->IsCompanionUnlocked(minionIds[index]))
            {
                minions++;
            }
        }

        if (baselineMinions >= 0)
        {
            var gained = minions - baselineMinions;
            if (gained > 0 && gained <= MaxCollectionGainPerSample)
            {
                today.MinionsGained += gained;
                session.MinionsGained += gained;
                dirty = true;
            }
        }

        baselineMinions = minions;
    }

    private void ResetSession()
    {
        session.Clear();
        SessionStartedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private void ResetBaselines()
    {
        hasJobBaseline = false;
        baselineGil = -1;
        baselineMounts = -1;
        baselineMinions = -1;
    }

    private void SaveIfDirty()
    {
        if (!dirty || contentId == 0)
        {
            return;
        }

        store.Save(contentId, ledger);
        dirty = false;
    }
}
