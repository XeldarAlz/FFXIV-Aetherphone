using System.Collections.Frozen;
using Aetherphone.Core.Calendar;
using System.Numerics;

namespace Aetherphone.Apps.Calendar;

internal static class CalendarEventMerger
{
    public static FrozenDictionary<long, ParsedEvent[]> Merge(FrozenDictionary<long, ParsedEvent[]> remote,
        IReadOnlyList<CalendarCustomEvent> custom, Vector4 customColor)
    {
        if (custom.Count == 0)
        {
            return remote;
        }

        var dimColor = customColor with { W = 0.42f };
        var builder = new Dictionary<long, List<ParsedEvent>>(remote.Count + custom.Count);
        foreach (var pair in remote)
        {
            builder[pair.Key] = new List<ParsedEvent>(pair.Value);
        }

        for (var index = 0; index < custom.Count; index++)
        {
            var item = custom[index];
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
            });
        }

        var result = new Dictionary<long, ParsedEvent[]>(builder.Count);
        foreach (var pair in builder)
        {
            result[pair.Key] = pair.Value.ToArray();
        }

        return result.ToFrozenDictionary();
    }
}
