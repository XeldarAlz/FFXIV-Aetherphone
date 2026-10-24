using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed class PrismCountdown
{
    private const float TileUnits = 54f;
    private const float RadiusUnits = 12f;
    private const float CompactRadiusUnits = 10f;
    private const float LineUnits = 1.5f;
    private const float LargeTileUnits = 78f;
    private const float LargeRadiusUnits = 16f;
    private const float LargeLineUnits = 2f;

    private readonly CountdownTiles tiles = new();

    public static float Height(float scale) =>
        TileUnits * scale + Metrics.Space.Xs * scale + MathF.Max(1f, LineUnits * scale);

    public static float LargeHeight(float scale) =>
        LargeTileUnits * scale + Metrics.Space.Sm * scale + MathF.Max(1f, LargeLineUnits * scale);

    public static long PhaseDeadline(PrismWeekDto week) => week.Phase switch
    {
        PrismPhases.Entering => week.EntriesCloseAtUnix,
        PrismPhases.Judging => week.JudgingCloseAtUnix,
        PrismPhases.Final => week.FinalCloseAtUnix,
        PrismPhases.Tallying => week.ResultsAtUnix,
        PrismPhases.Results => week.NextWeekAtUnix,
        _ => 0L,
    };

    public static long Deadline(PrismWeekDto week) => week.Phase switch
    {
        PrismPhases.Entering when week.MyEntry is null => week.EntriesCloseAtUnix,
        PrismPhases.Judging => week.JudgingCloseAtUnix,
        PrismPhases.Final => week.FinalCloseAtUnix,
        PrismPhases.Tallying => week.ResultsAtUnix,
        _ => 0L,
    };

    public static long Remaining(long deadlineUnix) =>
        deadlineUnix <= 0 ? 0L : deadlineUnix - (long)Math.Floor(Now());

    public bool Draw(ImDrawListPtr drawList, Rect rect, long deadlineUnix, float scale)
    {
        var remaining = Remaining(deadlineUnix);
        if (remaining <= 0)
        {
            return false;
        }

        var line = MathF.Max(1f, LineUnits * scale);
        var row = new Rect(rect.Min, new Vector2(rect.Max.X, rect.Max.Y - Metrics.Space.Xs * scale - line));
        var lastDay = CountdownTiles.InLastDay(remaining);
        tiles.Draw(drawList, row, remaining, Now(), Look(lastDay, PrismType.Title, RadiusUnits, Metrics.Space.Sm),
            scale, true);
        if (!lastDay)
        {
            return true;
        }

        var top = rect.Max.Y - line;
        var reach = row.Min.X + row.Width * remaining / CountdownTiles.SecondsPerDay;
        drawList.AddRectFilled(new Vector2(reach, top), new Vector2(row.Max.X, top + line),
            ImGui.GetColorU32(PrismArt.Faint));
        PrismLight.Strip(drawList, row.Min.X, reach, top, line, PrismLight.TabAlpha);
        return true;
    }

    public bool DrawLarge(ImDrawListPtr drawList, Rect rect, long deadlineUnix, float scale)
    {
        var remaining = Remaining(deadlineUnix);
        if (remaining <= 0)
        {
            return false;
        }

        var line = MathF.Max(1f, LargeLineUnits * scale);
        var row = new Rect(rect.Min, new Vector2(rect.Max.X, rect.Min.Y + LargeTileUnits * scale));
        var lastDay = CountdownTiles.InLastDay(remaining);
        tiles.Draw(drawList, row, remaining, Now(),
            Look(lastDay, TextStyles.LargeTitle, LargeRadiusUnits, Metrics.Space.Sm), scale, true);
        var top = rect.Max.Y - line;
        var reach = lastDay ? row.Min.X + row.Width * remaining / CountdownTiles.SecondsPerDay : row.Max.X;
        drawList.AddRectFilled(new Vector2(reach, top), new Vector2(row.Max.X, top + line),
            ImGui.GetColorU32(PrismArt.Faint));
        PrismLight.Strip(drawList, row.Min.X, reach, top, line, lastDay ? PrismLight.TabAlpha : PrismLight.PrimaryAlpha);
        return true;
    }

    public bool DrawCompact(ImDrawListPtr drawList, Rect rect, long deadlineUnix, float scale)
    {
        var remaining = Remaining(deadlineUnix);
        if (remaining <= 0)
        {
            return false;
        }

        tiles.Draw(drawList, rect, remaining, Now(),
            Look(CountdownTiles.InLastDay(remaining), PrismType.Headline, CompactRadiusUnits, Metrics.Space.Xs),
            scale, true);
        return true;
    }

    private static double Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

    private static CountdownStyle Look(bool lastDay, in TextStyle digits, float radius, float gap)
    {
        var fill = Surfaces.Fill(PrismArt.Fore, lastDay ? FillLevel.Secondary : FillLevel.Tertiary);
        return new CountdownStyle(fill, fill, Vector4.Zero, PrismArt.Fore, PrismArt.Muted, digits,
            TextStyles.Caption2, radius, gap);
    }
}
