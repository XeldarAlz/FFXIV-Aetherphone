using System.Numerics;
using Aetherphone.Apps.Calendar;
using Aetherphone.Core;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Widgets;

internal sealed class CalendarWidget : IHomeWidget
{
    private const float RefreshIntervalSeconds = 20f;
    private const int LookaheadDays = 14;
    private const int MaxRows = 3;

    private readonly struct UpcomingEvent
    {
        public readonly string Name;
        public readonly DateTime Begin;
        public readonly Vector4 Color;

        public UpcomingEvent(string name, DateTime begin, Vector4 color)
        {
            Name = name;
            Begin = begin;
            Color = color;
        }
    }

    private readonly Configuration configuration;
    private readonly CalendarEvents events;
    private readonly List<UpcomingEvent> upcoming = new();
    private float sinceRefresh = RefreshIntervalSeconds;

    public CalendarWidget(Configuration configuration, CalendarEvents events)
    {
        this.configuration = configuration;
        this.events = events;
    }

    public string Id => "calendar.upcoming";
    public string DisplayName => Loc.T(L.Calendar.Title);
    public string AppId => "calendar";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium;

    public void Draw(in WidgetContext context)
    {
        events.Initialize();
        Advance(context.Delta, context.Theme.Accent);
        WidgetChrome.Card(context.DrawList, context.Bounds, context.Scale, context.Opacity);
        if (context.Size == WidgetSize.Small)
        {
            DrawSmall(context);
            return;
        }

        DrawMedium(context);
    }

    private void Advance(float delta, Vector4 accent)
    {
        sinceRefresh += delta;
        if (sinceRefresh < RefreshIntervalSeconds)
        {
            return;
        }

        sinceRefresh = 0f;
        upcoming.Clear();
        var merged = CalendarEventMerger.Merge(events.Events, configuration.CalendarCustomEvents, accent);
        var now = DateTime.Now;
        for (var day = 0; day < LookaheadDays; day++)
        {
            var key = now.Date.AddDays(day).Ticks;
            if (!merged.TryGetValue(key, out var dayEvents))
            {
                continue;
            }

            for (var index = 0; index < dayEvents.Length; index++)
            {
                var entry = dayEvents[index];
                if (entry.End >= now || entry.Begin >= now)
                {
                    upcoming.Add(new UpcomingEvent(entry.Name, entry.Begin, entry.Color));
                }
            }
        }

        upcoming.Sort(static (left, right) => left.Begin.CompareTo(right.Begin));
        if (upcoming.Count > MaxRows + 1)
        {
            upcoming.RemoveRange(MaxRows + 1, upcoming.Count - MaxRows - 1);
        }
    }

    private void DrawSmall(in WidgetContext context)
    {
        var bounds = context.Bounds;
        var scale = context.Scale;
        var opacity = context.Opacity;
        var drawList = context.DrawList;
        var theme = context.Theme;
        var pad = 13f * scale;
        var now = DateTime.Now;
        WidgetChrome.Eyebrow(drawList, new Vector2(bounds.Min.X + pad, bounds.Min.Y + pad), now.ToString("dddd"),
            theme.Accent, scale, opacity);
        Typography.Draw(drawList, new Vector2(bounds.Min.X + pad, bounds.Min.Y + pad + 13f * scale),
            now.Day.ToString(), Palette.WithAlpha(theme.TextStrong, opacity), TextStyles.LargeTitle);
        if (upcoming.Count == 0)
        {
            Typography.Draw(drawList, new Vector2(bounds.Min.X + pad, bounds.Max.Y - pad - 15f * scale),
                Typography.FitText(Loc.T(L.Home.NoEvents), bounds.Width - pad * 2f, TextStyles.Caption1),
                Palette.WithAlpha(theme.TextMuted, opacity), TextStyles.Caption1);
            return;
        }

        var first = upcoming[0];
        Typography.Draw(drawList, new Vector2(bounds.Min.X + pad, bounds.Max.Y - pad - 32f * scale),
            Typography.FitText(first.Name, bounds.Width - pad * 2f, TextStyles.FootnoteEmphasized),
            Palette.WithAlpha(theme.TextStrong, opacity), TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(bounds.Min.X + pad, bounds.Max.Y - pad - 15f * scale),
            WhenLabel(first.Begin), Palette.WithAlpha(theme.TextMuted, opacity), TextStyles.Caption1);
    }

    private void DrawMedium(in WidgetContext context)
    {
        var bounds = context.Bounds;
        var scale = context.Scale;
        var opacity = context.Opacity;
        var drawList = context.DrawList;
        var theme = context.Theme;
        var pad = 16f * scale;
        var now = DateTime.Now;
        var left = bounds.Min.X + pad;
        WidgetChrome.Eyebrow(drawList, new Vector2(left, bounds.Min.Y + pad), now.ToString("dddd"), theme.Accent,
            scale, opacity);
        Typography.Draw(drawList, new Vector2(left, bounds.Min.Y + pad + 14f * scale), now.Day.ToString(),
            Palette.WithAlpha(theme.TextStrong, opacity), TextStyles.LargeTitle);
        Typography.Draw(drawList, new Vector2(left, bounds.Max.Y - pad - 16f * scale), now.ToString("MMMM"),
            Palette.WithAlpha(theme.TextMuted, opacity), TextStyles.Caption1);
        var columnX = bounds.Min.X + bounds.Width * 0.36f;
        drawList.AddLine(new Vector2(columnX, bounds.Min.Y + pad), new Vector2(columnX, bounds.Max.Y - pad),
            ImGui.GetColorU32(Palette.WithAlpha(theme.Separator, opacity)), 1f * scale);
        var listLeft = columnX + pad;
        if (upcoming.Count == 0)
        {
            Typography.Draw(drawList,
                new Vector2(listLeft, bounds.Center.Y - Typography.Measure(Loc.T(L.Home.NoEvents)).Y * 0.5f),
                Typography.FitText(Loc.T(L.Home.NoEvents), bounds.Max.X - pad - listLeft, TextStyles.Footnote),
                Palette.WithAlpha(theme.TextMuted, opacity), TextStyles.Footnote);
            return;
        }

        var rows = Math.Min(MaxRows, upcoming.Count);
        var rowHeight = (bounds.Height - pad * 2f) / MaxRows;
        for (var index = 0; index < rows; index++)
        {
            var entry = upcoming[index];
            var rowTop = bounds.Min.Y + pad + index * rowHeight;
            var barRect = new Rect(new Vector2(listLeft, rowTop + 3f * scale),
                new Vector2(listLeft + 3f * scale, rowTop + rowHeight - 5f * scale));
            drawList.AddRectFilled(barRect.Min, barRect.Max,
                ImGui.GetColorU32(Palette.WithAlpha(entry.Color, opacity)), 1.5f * scale);
            var textLeft = listLeft + 10f * scale;
            var maxWidth = bounds.Max.X - pad - textLeft;
            Typography.Draw(drawList, new Vector2(textLeft, rowTop + 2f * scale),
                Typography.FitText(entry.Name, maxWidth, TextStyles.FootnoteEmphasized),
                Palette.WithAlpha(context.Theme.TextStrong, opacity), TextStyles.FootnoteEmphasized);
            Typography.Draw(drawList, new Vector2(textLeft, rowTop + rowHeight * 0.5f + 1f * scale),
                WhenLabel(entry.Begin), Palette.WithAlpha(theme.TextMuted, opacity), TextStyles.Caption1);
        }
    }

    private static string WhenLabel(DateTime begin)
    {
        if (begin.Date == DateTime.Today)
        {
            return begin.ToString("HH:mm");
        }

        return string.Concat(begin.ToString("ddd"), " ", begin.ToString("HH:mm"));
    }

    public void Dispose()
    {
    }
}
