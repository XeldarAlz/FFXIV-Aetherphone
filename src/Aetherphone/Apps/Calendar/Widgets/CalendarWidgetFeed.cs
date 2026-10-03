using System.Collections.Frozen;
using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Calendar;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Calendar.Widgets;

internal struct UpcomingEvent
{
    public string Name;
    public DateTime Begin;
    public DateTime End;
    public Vector4 Color;
    public bool Ongoing;
    public string When;
}

internal sealed class CalendarWidgetFeed
{
    public const int Capacity = 8;
    private const double RefreshSeconds = 20.0;
    private const int LookaheadDays = 21;
    private const int MaxDaysInMonth = 31;
    private const int SampleHour = 20;
    private const int LateSampleHour = 21;

    private readonly struct Candidate
    {
        public readonly ParsedEvent Event;
        public readonly int Rank;
        public readonly DateTime SortKey;

        public Candidate(ParsedEvent entry, int rank, DateTime sortKey)
        {
            Event = entry;
            Rank = rank;
            SortKey = sortKey;
        }
    }

    private readonly Configuration configuration;
    private readonly CalendarEvents events;
    private readonly UpcomingEvent[] upcoming = new UpcomingEvent[Capacity];
    private readonly UpcomingEvent[] samples = new UpcomingEvent[3];
    private readonly Vector4[] dayColors = new Vector4[MaxDaysInMonth + 1];
    private readonly List<Candidate> candidates = new();
    private int count;
    private uint dayMask;
    private double nextRefresh;
    private int seenRevision = -1;
    private long seenDay = -1;
    private Vector4 seenAccent;
    private FrozenDictionary<long, ParsedEvent[]>? seenRemote;
    private long samplesDay = -1;
    private CultureInfo? samplesCulture;
    private int samplesFormat = -1;

    public CalendarWidgetFeed(Configuration configuration, CalendarEvents events)
    {
        this.configuration = configuration;
        this.events = events;
    }

    public int Count => count;

    public bool IsLoading => count == 0 && events.IsLoading && !events.IsLoaded;

    public uint DayMask => dayMask;

    public ReadOnlySpan<UpcomingEvent> Upcoming => upcoming.AsSpan(0, count);

    public Vector4 DayColor(int day) => dayColors[Math.Clamp(day, 0, MaxDaysInMonth)];

    public void Update(Vector4 accent)
    {
        events.Initialize();
        var time = ImGui.GetTime();
        var today = DateTime.Today.Ticks;
        if (time < nextRefresh && seenRevision == events.CustomRevision &&
            ReferenceEquals(seenRemote, events.Events) && seenDay == today && seenAccent == accent)
        {
            return;
        }

        nextRefresh = time + RefreshSeconds;
        seenRevision = events.CustomRevision;
        seenRemote = events.Events;
        seenDay = today;
        seenAccent = accent;
        Rebuild(accent);
    }

    public ReadOnlySpan<UpcomingEvent> Samples(Vector4 accent)
    {
        var today = DateTime.Today;
        if (samplesDay == today.Ticks && ReferenceEquals(samplesCulture, Loc.Culture) &&
            samplesFormat == TimeText.FormatVersion)
        {
            return samples;
        }

        samplesDay = today.Ticks;
        samplesCulture = Loc.Culture;
        samplesFormat = TimeText.FormatVersion;
        samples[0] = Sample(Loc.T(WidgetSamples.Events[0]), today.AddHours(SampleHour), accent);
        samples[1] = Sample(Loc.T(WidgetSamples.Events[1]), today.AddDays(1).AddHours(SampleHour), Accent.Mint);
        samples[2] = Sample(Loc.T(L.WidgetsTime.SampleEvent), today.AddDays(2).AddHours(LateSampleHour), Accent.Amber);
        return samples;
    }

    public static uint SampleMask(DateTime today)
    {
        var mask = 1u << today.Day;
        if (today.Day + 1 <= DateTime.DaysInMonth(today.Year, today.Month))
        {
            mask |= 1u << (today.Day + 1);
        }

        if (today.Day + 2 <= DateTime.DaysInMonth(today.Year, today.Month))
        {
            mask |= 1u << (today.Day + 2);
        }

        return mask;
    }

    public float Relevance()
    {
        if (count == 0)
        {
            return 0f;
        }

        var first = upcoming[0];
        if (first.Ongoing)
        {
            return first.End - first.Begin < TimeSpan.FromDays(1) ? 0.4f : 0f;
        }

        var minutes = (first.Begin - DateTime.Now).TotalMinutes;
        if (minutes <= 15)
        {
            return 0.9f;
        }

        return minutes <= 60 ? 0.6f : 0f;
    }

    private void Rebuild(Vector4 accent)
    {
        var merged = CalendarEventMerger.Merge(events.Events, configuration.CalendarCustomEvents,
            configuration.CalendarGroups, configuration.CalendarGameEventsInWidget, CalendarSurface.Widget, accent);
        var now = DateTime.Now;
        CollectUpcoming(merged, now);
        CollectMonth(merged, now.Date);
    }

    private void CollectUpcoming(FrozenDictionary<long, ParsedEvent[]> merged, DateTime now)
    {
        candidates.Clear();
        for (var day = 0; day < LookaheadDays; day++)
        {
            if (!merged.TryGetValue(now.Date.AddDays(day).Ticks, out var dayEvents))
            {
                continue;
            }

            for (var index = 0; index < dayEvents.Length; index++)
            {
                var entry = dayEvents[index];
                if (entry.End < now && entry.Begin < now)
                {
                    continue;
                }

                if (Contains(entry))
                {
                    continue;
                }

                candidates.Add(Classify(entry, now));
            }
        }

        candidates.Sort(static (left, right) =>
            left.Rank != right.Rank ? left.Rank.CompareTo(right.Rank) : left.SortKey.CompareTo(right.SortKey));
        count = Math.Min(Capacity, candidates.Count);
        for (var index = 0; index < count; index++)
        {
            var entry = candidates[index].Event;
            var ongoing = candidates[index].Rank != 1;
            upcoming[index] = new UpcomingEvent
            {
                Name = entry.Name,
                Begin = entry.Begin,
                End = entry.End,
                Color = entry.Color,
                Ongoing = ongoing,
                When = ongoing ? UntilLabel(entry.End) : WhenLabel(entry.Begin, entry.End),
            };
        }
    }

    private void CollectMonth(FrozenDictionary<long, ParsedEvent[]> merged, DateTime today)
    {
        dayMask = 0;
        var days = DateTime.DaysInMonth(today.Year, today.Month);
        var first = new DateTime(today.Year, today.Month, 1);
        for (var day = 1; day <= days; day++)
        {
            if (!merged.TryGetValue(first.AddDays(day - 1).Ticks, out var dayEvents) || dayEvents.Length == 0)
            {
                continue;
            }

            dayMask |= 1u << day;
            dayColors[day] = dayEvents[0].Color;
        }
    }

    private bool Contains(in ParsedEvent entry)
    {
        for (var index = 0; index < candidates.Count; index++)
        {
            var existing = candidates[index].Event;
            if (existing.IsCustom != entry.IsCustom)
            {
                continue;
            }

            if (entry.IsCustom ? existing.CustomId == entry.CustomId : existing.Id == entry.Id)
            {
                return true;
            }
        }

        return false;
    }

    private static Candidate Classify(in ParsedEvent entry, DateTime now)
    {
        if (entry.Begin >= now)
        {
            return new Candidate(entry, 1, entry.Begin);
        }

        var shortEvent = entry.End - entry.Begin < TimeSpan.FromDays(1);
        return new Candidate(entry, shortEvent ? 0 : 2, entry.End);
    }

    private static UpcomingEvent Sample(string name, DateTime begin, Vector4 color) => new()
    {
        Name = name,
        Begin = begin,
        End = begin,
        Color = color,
        Ongoing = false,
        When = WhenLabel(begin, begin),
    };

    private static string WhenLabel(DateTime begin, DateTime end)
    {
        var start = TimeWidgetParts.DayAndClock(begin);
        if (end <= begin || end.Date != begin.Date)
        {
            return start;
        }

        return string.Concat(start, " - ", TimeText.Clock(end));
    }

    private static string UntilLabel(DateTime end)
    {
        var moment = end.Date == DateTime.Today
            ? TimeText.Clock(end)
            : end.ToString(Loc.Culture.DateTimeFormat.MonthDayPattern, Loc.Culture);
        return Loc.T(L.WidgetsTime.UntilMoment, moment);
    }
}
