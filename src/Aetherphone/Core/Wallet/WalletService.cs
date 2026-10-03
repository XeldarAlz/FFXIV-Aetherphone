using Aetherphone.Core.Game;
using Aetherphone.Core.Home;
using Aetherphone.Core.Runtime;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Wallet;

internal sealed class WalletService : IDisposable
{
    public const string AppId = "wallet";

    private const long TickMilliseconds = 1000;
    private const long SaveIntervalSeconds = 20;

    private readonly GameData gameData;
    private readonly CharacterWatch characterWatch;
    private readonly IClientState clientState;
    private readonly WalletHistoryStore store;
    private readonly FrameworkTicker ticker;
    private readonly Dictionary<uint, WalletEntry> byItem = new();
    private WalletReading[] readings = Array.Empty<WalletReading>();
    private WalletReading[] pending = Array.Empty<WalletReading>();
    private bool pendingFilled;
    private ulong owner;
    private uint builtSeal;
    private bool dirty;
    private long lastSaveUnix;

    public WalletService(IFramework framework, GameData gameData, CharacterWatch characterWatch,
        IClientState clientState, DirectoryInfo configDirectory, AppGate gate)
    {
        this.gameData = gameData;
        this.characterWatch = characterWatch;
        this.clientState = clientState;
        store = new WalletHistoryStore(new DirectoryInfo(Path.Combine(configDirectory.FullName, "Wallet")));
        ticker = new FrameworkTicker(framework, TickMilliseconds, OnTick, gate);
    }

    public WalletEntry? Gil { get; private set; }
    public WalletSection[] Sections { get; private set; } = Array.Empty<WalletSection>();
    public WalletEntry[] Entries { get; private set; } = Array.Empty<WalletEntry>();
    public WalletHistory History { get; private set; } = new();
    public string CharacterName { get; private set; } = string.Empty;
    public int HistoryRevision { get; private set; }
    public int FullCount { get; private set; }
    public bool Ready => Gil is not null;

    public bool TryGetEntry(uint itemId, out WalletEntry entry) => byItem.TryGetValue(itemId, out entry!);

    public void Dispose()
    {
        ticker.Dispose();
        Flush();
    }

    private void OnTick()
    {
        var contentId = characterWatch.CurrentContentId;
        if (contentId == 0)
        {
            Release();
            return;
        }

        if (gameData.LocalPlayer is null)
        {
            return;
        }

        if (contentId != owner)
        {
            Flush();
            owner = contentId;
            History = store.Load(contentId);
            Build();
        }
        else if (WalletReader.GrandCompanySealItemId() != builtSeal)
        {
            Build();
        }

        if (!WalletReader.IsCurrencyLoaded())
        {
            return;
        }

        WalletReader.RefreshAmounts(Gil!, Entries);
        Capture();
        FullCount = CountFull();
        Journal();
        SaveIfDue();
    }

    private void Release()
    {
        if (owner == 0)
        {
            return;
        }

        Flush();
        owner = 0;
        Gil = null;
        Sections = Array.Empty<WalletSection>();
        Entries = Array.Empty<WalletEntry>();
        History = new WalletHistory();
        CharacterName = string.Empty;
        FullCount = 0;
        byItem.Clear();
        pendingFilled = false;
        HistoryRevision++;
    }

    private void Build()
    {
        builtSeal = WalletReader.GrandCompanySealItemId();
        Gil = WalletReader.BuildGil(gameData);
        Sections = WalletReader.BuildSections(gameData, builtSeal);
        var total = 0;
        for (var index = 0; index < Sections.Length; index++)
        {
            total += Sections[index].Entries.Length;
        }

        var flat = new WalletEntry[total];
        var cursor = 0;
        byItem.Clear();
        byItem[Gil.ItemId] = Gil;
        for (var sectionIndex = 0; sectionIndex < Sections.Length; sectionIndex++)
        {
            var entries = Sections[sectionIndex].Entries;
            for (var entryIndex = 0; entryIndex < entries.Length; entryIndex++)
            {
                flat[cursor++] = entries[entryIndex];
                byItem[entries[entryIndex].ItemId] = entries[entryIndex];
            }
        }

        Entries = flat;
        readings = new WalletReading[total + 1];
        pending = new WalletReading[total + 1];
        pendingFilled = false;
        CharacterName = gameData.LocalPlayer?.Name.TextValue ?? string.Empty;
        HistoryRevision++;
    }

    private void Capture()
    {
        readings[0] = new WalletReading(Gil!.ItemId, Gil.Amount);
        for (var index = 0; index < Entries.Length; index++)
        {
            readings[index + 1] = new WalletReading(Entries[index].ItemId, Entries[index].Amount);
        }
    }

    private void Journal()
    {
        var stable = pendingFilled && readings.AsSpan().SequenceEqual(pending);
        readings.AsSpan().CopyTo(pending);
        pendingFilled = true;
        if (!stable || LooksUnloaded())
        {
            return;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var day = DateOnly.FromDateTime(DateTime.Now).DayNumber;
        if (!WalletJournal.Apply(History, readings, now, day, clientState.TerritoryType))
        {
            return;
        }

        dirty = true;
        HistoryRevision++;
    }

    private bool LooksUnloaded()
    {
        for (var index = 0; index < readings.Length; index++)
        {
            if (readings[index].Amount != 0)
            {
                return false;
            }
        }

        return History.Last.Count > 0;
    }

    private int CountFull()
    {
        var count = 0;
        for (var index = 0; index < Entries.Length; index++)
        {
            if (Entries[index].Level == CapLevel.Full)
            {
                count++;
            }
        }

        return count;
    }

    private void SaveIfDue()
    {
        if (!dirty)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (now - lastSaveUnix < SaveIntervalSeconds)
        {
            return;
        }

        lastSaveUnix = now;
        Flush();
    }

    private void Flush()
    {
        if (!dirty || owner == 0)
        {
            return;
        }

        dirty = false;
        store.Save(owner, History);
    }
}
