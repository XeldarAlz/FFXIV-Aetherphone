using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Timers;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Timers.Widgets;

internal sealed class VenturesWidget : IHomeWidget
{
    private const int MediumRows = 4;
    private const float BarUnits = 3f;
    private const float RelevanceSoonMinutes = 10f;
    private const double QuickVentureHours = 1.0;
    private const double ExplorationVentureHours = 18.0;
    private const double SecondsPerHour = 3600.0;
    private static readonly Vector4 ReadyColor = AccentRing.Green;
    private static readonly int[] SampleMinutes = { 0, 42, 730, -1 };

    private enum VentureState : byte
    {
        Ready,
        Running,
        Idle,
    }

    private struct VentureRow
    {
        public string Name;
        public VentureState State;
        public DateTime CompleteUtc;
        public double LengthHours;
    }

    private readonly GameTimers timers;
    private VentureRow[] rows = new VentureRow[10];
    private int rowCount;
    private int readyCount;
    private int runningCount;
    private DateTime nextCompleteUtc;
    private bool known;
    private readonly CachedText[] countdowns = new CachedText[MediumRows];
    private CachedText heroCountdown;
    private CachedText readyText;
    private CachedText summary;
    private CachedText readySummary;

    public VenturesWidget(GameTimers timers)
    {
        this.timers = timers;
    }

    public string Id => "timers.ventures";
    public string DisplayName => Loc.T(L.WidgetsTime.Ventures);
    public string Description => Loc.T(L.WidgetsTime.VenturesDescription);
    public string AppId => "timers";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium;

    public float Relevance(string config)
    {
        Refresh(DateTime.UtcNow, false);
        if (!known)
        {
            return 0f;
        }

        if (readyCount > 0)
        {
            return 0.8f;
        }

        return runningCount > 0 && (nextCompleteUtc - DateTime.UtcNow).TotalMinutes <= RelevanceSoonMinutes
            ? 0.6f
            : 0f;
    }

    public void Draw(in WidgetContext context)
    {
        if (context.Opacity <= 0f)
        {
            return;
        }

        var utcNow = DateTime.UtcNow;
        Refresh(utcNow, context.Preview);
        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var content = WidgetMetrics.Content(context);
        var accent = AppAccents.For(AppId);
        var headerBottom = WidgetChrome.Header(context, ink, AppId, L.WidgetsTime.Ventures, accent);
        var body = new Rect(new Vector2(content.Min.X, headerBottom + WidgetMetrics.Gutter * context.Scale),
            content.Max);
        if (rowCount == 0)
        {
            WidgetChrome.Message(context, ink, body, Loc.T(L.WidgetsTime.VenturesUnavailable), string.Empty);
            return;
        }

        if (context.Size == WidgetSize.Small)
        {
            DrawSmall(context, ink, body, utcNow);
            return;
        }

        DrawMedium(context, ink, body, utcNow, accent);
    }

    private void Refresh(DateTime utcNow, bool preview)
    {
        known = Load(utcNow);
        if (known)
        {
            return;
        }

        if (preview)
        {
            LoadSamples(utcNow);
            return;
        }

        rowCount = 0;
        readyCount = 0;
        runningCount = 0;
    }

    private bool Load(DateTime utcNow)
    {
        var characters = timers.Characters;
        var count = 0;
        for (var characterIndex = 0; characterIndex < characters.Count; characterIndex++)
        {
            count += characters[characterIndex].Retainers.Count;
        }

        if (count == 0)
        {
            return false;
        }

        Ensure(count);
        var written = 0;
        for (var characterIndex = 0; characterIndex < characters.Count; characterIndex++)
        {
            var retainers = characters[characterIndex].Retainers;
            for (var index = 0; index < retainers.Count; index++)
            {
                var retainer = retainers[index];
                var completeUtc = retainer.CompleteUnix > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(retainer.CompleteUnix).UtcDateTime
                    : DateTime.MinValue;
                rows[written] = new VentureRow
                {
                    Name = retainer.Name,
                    State = retainer.CompleteUnix <= 0 ? VentureState.Idle :
                        completeUtc <= utcNow ? VentureState.Ready : VentureState.Running,
                    CompleteUtc = completeUtc,
                    LengthHours = retainer.DurationSeconds > 0
                        ? retainer.DurationSeconds / SecondsPerHour
                        : LengthGuess((completeUtc - utcNow).TotalHours),
                };
                written++;
            }
        }

        Finish(written);
        return true;
    }

    private void LoadSamples(DateTime utcNow)
    {
        Ensure(SampleMinutes.Length);
        for (var index = 0; index < SampleMinutes.Length; index++)
        {
            var minutes = SampleMinutes[index];
            rows[index] = new VentureRow
            {
                Name = WidgetSamples.Names[index % WidgetSamples.Names.Length],
                State = minutes < 0 ? VentureState.Idle : minutes == 0 ? VentureState.Ready : VentureState.Running,
                CompleteUtc = utcNow.AddMinutes(Math.Max(0, minutes)),
                LengthHours = LengthGuess(minutes / 60.0),
            };
        }

        Finish(SampleMinutes.Length);
    }

    private void Ensure(int count)
    {
        if (rows.Length < count)
        {
            rows = new VentureRow[Math.Max(count, rows.Length * 2)];
        }
    }

    private void Finish(int count)
    {
        rowCount = count;
        readyCount = 0;
        runningCount = 0;
        nextCompleteUtc = DateTime.MaxValue;
        for (var index = 0; index < count; index++)
        {
            var row = rows[index];
            if (row.State == VentureState.Ready)
            {
                readyCount++;
            }
            else if (row.State == VentureState.Running)
            {
                runningCount++;
                if (row.CompleteUtc < nextCompleteUtc)
                {
                    nextCompleteUtc = row.CompleteUtc;
                }
            }
        }

        for (var index = 1; index < count; index++)
        {
            var current = rows[index];
            var target = index - 1;
            while (target >= 0 && Before(current, rows[target]))
            {
                rows[target + 1] = rows[target];
                target--;
            }

            rows[target + 1] = current;
        }
    }

    private static bool Before(in VentureRow left, in VentureRow right)
    {
        if (left.State != right.State)
        {
            return left.State < right.State;
        }

        return left.State == VentureState.Running && left.CompleteUtc < right.CompleteUtc;
    }

    private void DrawSmall(in WidgetContext context, in WidgetInk ink, Rect body, DateTime utcNow)
    {
        var scale = context.Scale;
        var drawList = context.DrawList;
        var captionHeight = Typography.Measure("A", WidgetType.Caption).Y;
        var summaryTop = body.Max.Y - captionHeight;
        WidgetText.Draw(drawList, new Vector2(body.Min.X, summaryTop), Summary(), ink.Secondary, WidgetType.Caption,
            body.Width);
        var labelHeight = Typography.Measure("A", WidgetType.Headline).Y;
        var labelTop = summaryTop - WidgetMetrics.RowGap * scale - labelHeight;
        if (readyCount > 0)
        {
            var count = WidgetText.Integer(ref readyText, readyCount);
            var heroHeight = Typography.Measure(count, WidgetType.DisplayCompact).Y;
            WidgetText.Tabular(drawList, new Vector2(body.Min.X, labelTop - heroHeight), count, ink.Accent(ReadyColor),
                WidgetType.DisplayCompact);
            WidgetText.Draw(drawList, new Vector2(body.Min.X, labelTop), Loc.T(L.WidgetsTime.ReadyToCollect),
                ink.Primary, WidgetType.Headline, body.Width);
            return;
        }

        if (runningCount == 0)
        {
            WidgetText.Draw(drawList, new Vector2(body.Min.X, labelTop), Loc.T(L.WidgetsTime.NoVentures), ink.Secondary,
                WidgetType.Headline, body.Width);
            return;
        }

        var text = WidgetText.Countdown(ref heroCountdown, nextCompleteUtc - utcNow);
        var style = WidgetText.FitStyle(text, WidgetType.DisplayCompact, body.Width, true);
        var height = Typography.Measure(text, style).Y;
        WidgetText.Tabular(drawList, new Vector2(body.Min.X, labelTop - height), text, ink.Primary, style);
        WidgetText.Draw(drawList, new Vector2(body.Min.X, labelTop), Loc.T(L.WidgetsTime.NextVenture), ink.Primary,
            WidgetType.Headline, body.Width);
    }

    private void DrawMedium(in WidgetContext context, in WidgetInk ink, Rect body, DateTime utcNow, Vector4 accent)
    {
        var scale = context.Scale;
        var drawList = context.DrawList;
        var content = WidgetMetrics.Content(context);
        if (readyCount > 0)
        {
            var text = ReadySummary();
            var width = MathF.Min(content.Width * 0.5f, Typography.Measure(text, WidgetType.Caption).X);
            WidgetText.Draw(drawList, new Vector2(content.Max.X - width, content.Min.Y), text, ink.Accent(ReadyColor),
                WidgetType.Caption, width);
        }

        var visible = Math.Min(MediumRows, rowCount);
        var rowHeight = body.Height / MediumRows;
        for (var index = 0; index < visible; index++)
        {
            var top = body.Min.Y + index * rowHeight;
            DrawRow(context, ink, new Rect(new Vector2(body.Min.X, top), new Vector2(body.Max.X, top + rowHeight)),
                index, utcNow, accent);
        }
    }

    private void DrawRow(in WidgetContext context, in WidgetInk ink, Rect row, int index, DateTime utcNow,
        Vector4 accent)
    {
        var scale = context.Scale;
        var drawList = context.DrawList;
        var entry = rows[index];
        var barHeight = BarUnits * scale;
        var nameHeight = Typography.Measure("A", WidgetType.Headline).Y;
        var blockHeight = nameHeight + WidgetMetrics.RowGap * scale + barHeight;
        var top = row.Center.Y - blockHeight * 0.5f;

        string value;
        Vector4 valueColor;
        switch (entry.State)
        {
            case VentureState.Ready:
                value = Loc.T(L.Timers.Ready);
                valueColor = ink.Accent(ReadyColor);
                break;
            case VentureState.Running:
                value = WidgetText.Countdown(ref countdowns[index], entry.CompleteUtc - utcNow);
                valueColor = ink.Primary;
                break;
            default:
                value = Loc.T(L.Timers.NoVenture);
                valueColor = ink.Tertiary;
                break;
        }

        var tabular = entry.State == VentureState.Running;
        var valueWidth = tabular
            ? WidgetText.TabularWidth(value, WidgetType.Headline)
            : Typography.Measure(value, WidgetType.Headline).X;
        var valueLeft = row.Max.X - valueWidth;
        if (tabular)
        {
            WidgetText.Tabular(drawList, new Vector2(valueLeft, top), value, valueColor, WidgetType.Headline);
        }
        else
        {
            Typography.Draw(drawList, new Vector2(valueLeft, top), value, valueColor, WidgetType.Headline);
        }

        var nameWidth = MathF.Max(1f, valueLeft - WidgetMetrics.Gutter * scale - row.Min.X);
        Marquee.DrawLeftAuto(drawList, new MarqueeId(context.InstanceKey, index), entry.Name, row.Min.X, top, nameWidth,
            WidgetType.Headline, entry.State == VentureState.Idle ? ink.Secondary : ink.Primary);

        var barTop = top + nameHeight + WidgetMetrics.RowGap * scale;
        var track = new Rect(new Vector2(row.Min.X, barTop), new Vector2(row.Max.X, barTop + barHeight));
        var fill = entry.State == VentureState.Ready ? ReadyColor : accent;
        WidgetChrome.Bar(drawList, track, Progress(entry, utcNow), ink.Fill, ink.Accent(fill));
    }

    private static float Progress(in VentureRow row, DateTime utcNow)
    {
        switch (row.State)
        {
            case VentureState.Ready:
                return 1f;
            case VentureState.Idle:
                return 0f;
        }

        var remaining = (row.CompleteUtc - utcNow).TotalHours;
        return Math.Clamp(1f - (float)(remaining / row.LengthHours), 0f, 1f);
    }

    private static double LengthGuess(double remainingHours) =>
        remainingHours > QuickVentureHours ? ExplorationVentureHours : QuickVentureHours;

    private string Summary()
    {
        var outCount = readyCount + runningCount;
        var key = outCount * 1000L + rowCount;
        return summary.IsCurrent(key)
            ? summary.Value
            : summary.Store(key, Loc.T(L.WidgetsTime.VenturesOut, outCount, rowCount));
    }

    private string ReadySummary() =>
        readySummary.IsCurrent(readyCount)
            ? readySummary.Value
            : readySummary.Store(readyCount, Loc.T(L.WidgetsTime.VenturesReady, readyCount));

    public void Dispose()
    {
    }
}
