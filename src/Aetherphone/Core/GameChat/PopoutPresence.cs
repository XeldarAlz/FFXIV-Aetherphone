using Aetherphone.Core.Game;
using Aetherphone.Windows;
using Dalamud.Game.ClientState.Conditions;
using Lumina.Excel.Sheets;

namespace Aetherphone.Core.GameChat;

internal sealed class PopoutPresence : IDisposable
{
    private const float SuppressDelaySeconds = 0.35f;
    private const float RestoreDelaySeconds = 1f;
    private const uint EurekaIntendedUse = 41;
    private const uint BozjaIntendedUse = 48;
    private const uint OccultCrescentIntendedUse = 61;

    private readonly Configuration configuration;
    private readonly LinkpearlPopouts popouts;
    private readonly ChatLog log;
    private readonly ChatInbox inbox;
    private PresenceDebounce debounce;
    private string pendingKey = string.Empty;
    private uint knownTerritory = uint.MaxValue;
    private bool knownFieldOperation;

    public PopoutPresence(Configuration configuration, LinkpearlPopouts popouts, ChatLog log, ChatInbox inbox)
    {
        this.configuration = configuration;
        this.popouts = popouts;
        this.log = log;
        this.inbox = inbox;
        log.Appended += OnAppended;
    }

    public void Tick(float deltaSeconds)
    {
        var settings = new PresenceSettings(configuration.LinkpearlPopoutHideInCombat,
            configuration.LinkpearlPopoutHideInDuty, configuration.LinkpearlPopoutFieldOperationsExempt);
        var target = PopoutPresenceGate.ShouldSuppress(ReadState(), settings);
        if (!debounce.Step(target, deltaSeconds, DelayFor(target)))
        {
            return;
        }

        Apply(debounce.Value);
    }

    public void Dispose()
    {
        log.Appended -= OnAppended;
        popouts.SetSuppressed(false);
    }

    private float DelayFor(bool target)
    {
        if (target)
        {
            return SuppressDelaySeconds;
        }

        return WantsImmediateReturn ? 0f : RestoreDelaySeconds;
    }

    private bool WantsImmediateReturn => pendingKey.Length > 0 && configuration.LinkpearlPopoutReopenAfterCombat;

    private void Apply(bool suppressed)
    {
        popouts.SetSuppressed(suppressed);
        if (suppressed)
        {
            return;
        }

        if (WantsImmediateReturn)
        {
            popouts.Open(pendingKey);
        }

        pendingKey = string.Empty;
    }

    private PresenceState ReadState()
    {
        var condition = Plugin.Condition;
        var boundByDuty = condition[ConditionFlag.BoundByDuty] || condition[ConditionFlag.BoundByDuty56] ||
                          condition[ConditionFlag.BoundByDuty95];
        return new PresenceState(condition[ConditionFlag.InCombat], boundByDuty, InFieldOperation());
    }

    private bool InFieldOperation()
    {
        var territory = Plugin.ClientState.TerritoryType;
        if (territory == knownTerritory)
        {
            return knownFieldOperation;
        }

        knownTerritory = territory;
        knownFieldOperation = IsFieldOperation(territory);
        return knownFieldOperation;
    }

    private static bool IsFieldOperation(uint territoryId)
    {
        if (!GameSheets.Available)
        {
            return false;
        }

        if (territoryId == 0 ||
            !Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territory))
        {
            return false;
        }

        var intendedUse = territory.TerritoryIntendedUse.RowId;
        return intendedUse is EurekaIntendedUse or BozjaIntendedUse or OccultCrescentIntendedUse;
    }

    private void OnAppended(ChatEntry entry)
    {
        if (!popouts.Suppressed || entry.IsSelf)
        {
            return;
        }

        var windows = popouts.Windows;
        for (var index = 0; index < windows.Count; index++)
        {
            var window = windows[index];
            if (window.Bound && Carries(window.Key, entry))
            {
                pendingKey = window.Key;
                return;
            }
        }
    }

    private bool Carries(string conversationKey, ChatEntry entry)
    {
        if (ChatStreams.IsTell(entry.StreamKey))
        {
            return string.Equals(conversationKey, entry.StreamKey, StringComparison.Ordinal);
        }

        return inbox.Find(conversationKey)?.Tab is { } tab && tab.Includes(entry.ChannelKey);
    }
}
