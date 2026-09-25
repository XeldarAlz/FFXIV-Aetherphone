using System.Collections.Frozen;
using Aetherphone.Core.Calendar;

namespace Aetherphone.Apps.Calendar;

internal static class CalendarEventMerger
{
    public static FrozenDictionary<long, ParsedEvent[]> Merge(FrozenDictionary<long, ParsedEvent[]> remote,
        IReadOnlyList<CalendarCustomEvent> custom, IReadOnlyList<CalendarEventGroup> groups, bool showGameEvents,
        CalendarSurface surface, Vector4 customColor)
    {
        var visibleRemote = showGameEvents ? remote : FrozenDictionary<long, ParsedEvent[]>.Empty;
        if (custom.Count == 0)
        {
            return visibleRemote;
        }

        var dimColor = customColor with { W = 0.42f };
        var builder = new Dictionary<long, List<ParsedEvent>>(visibleRemote.Count + custom.Count);
        foreach (var pair in visibleRemote)
        {
            builder[pair.Key] = new List<ParsedEvent>(pair.Value);
        }

        for (var index = 0; index < custom.Count; index++)
        {
            var item = custom[index];
            var group = FindGroup(groups, item.GroupId);
            if (group is not null && !group.ShowsOn(surface))
            {
                continue;
            }

            var key = item.When.Date.Ticks;
            if (!builder.TryGetValue(key, out var dayEvents))
            {
                dayEvents = new List<ParsedEvent>();
                builder[key] = dayEvents;
            }

            dayEvents.Add(new ParsedEvent
            {
                Name = item.Title,
                Begin = item.When,
                End = item.When,
                Url = string.Empty,
                Color = customColor,
                DimColor = dimColor,
                IsCustom = true,
                CustomId = item.Id,
                GroupName = group?.Name ?? string.Empty,
            });
        }

        var result = new Dictionary<long, ParsedEvent[]>(builder.Count);
        foreach (var pair in builder)
        {
            result[pair.Key] = pair.Value.ToArray();
        }

        return result.ToFrozenDictionary();
    }

    private static CalendarEventGroup? FindGroup(IReadOnlyList<CalendarEventGroup> groups, Guid groupId)
    {
        if (groupId == Guid.Empty)
        {
            return null;
        }

        for (var index = 0; index < groups.Count; index++)
        {
            if (groups[index].Id == groupId)
            {
                return groups[index];
            }
        }

        return null;
    }
}
