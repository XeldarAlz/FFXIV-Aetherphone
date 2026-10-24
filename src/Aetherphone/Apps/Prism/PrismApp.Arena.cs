using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const int TimelineSteps = 4;
    private const int PodiumTiles = 3;
    private const float ArenaEmblemUnits = 38f;
    private const float ArenaEmblemInk = 0.9f;
    private const float ArenaBriefShare = 0.9f;
    private const float ArenaCountdownUnits = 340f;
    private const float TimelineNodeUnits = 6f;
    private const float TimelinePastShare = 0.7f;
    private const float TimelineLineUnits = 2f;
    private const float PodiumRunnerShare = 0.84f;
    private const float PodiumMedalUnits = 13f;

    private static readonly int[] PodiumOrder = { 2, 1, 3 };
    private static readonly string[] RankDigits = { string.Empty, "1", "2", "3" };
    private static readonly TextStyle TimelineCurrentStyle = new(PrismType.Caption.Scale, FontWeight.SemiBold);

    private readonly PrismCountdown arenaCountdown = new();
    private readonly string[] timelineLabels = new string[TimelineSteps];
    private readonly CachedText[] podiumHandles = new CachedText[MaxTracks * PodiumTiles];
    private CachedText arenaJudgeText;
    private float arenaBackdropBottom;

    private void DrawArenaBackdrop(ImDrawListPtr drawList, Rect full, float bodyTop, PrismWeekDto week, bool open,
        float scale)
    {
        var bottom = MathF.Min(full.Max.Y, MathF.Max(bodyTop, bodyTop + arenaBackdropBottom));
        PrismLight.Week(drawList, new Rect(full.Min, new Vector2(full.Max.X, bottom)), open ? week.Id : string.Empty,
            PrismArt.Page, PrismArt.Fore, 0f, 0f);
    }

    private float DrawArenaHero(ImDrawListPtr drawList, float left, float top, float width, PrismWeekDto week,
        bool open, float scale)
    {
        var center = left + width * 0.5f;
        var emblem = ArenaEmblemUnits * scale;
        var emblemTop = top + Metrics.Space.Sm * scale;
        PrismLight.Mark(drawList, new Vector2(center, emblemTop + emblem * 0.5f), emblem,
            PrismArt.Fore with { W = PrismArt.Fore.W * ArenaEmblemInk }, 1f);
        var y = DrawCenteredHero(drawList, center, emblemTop + emblem + Metrics.Space.Xs * scale, width,
            open ? week.Title : Loc.T(L.Prism.NoWeekTitle));
        var brief = open ? week.Brief : Loc.T(L.Prism.NoWeekHint);
        if (brief.Length > 0)
        {
            y = Typography.DrawWrappedCentered(drawList, brief, PrismType.Body, PrismArt.Muted,
                new Vector2(center, y + Metrics.Space.Sm * scale), width * ArenaBriefShare);
        }

        if (!open)
        {
            return y + Metrics.Space.Lg * scale;
        }

        var deadline = PrismCountdown.PhaseDeadline(week);
        if (PrismCountdown.Remaining(deadline) > 0)
        {
            y += Metrics.Space.Lg * scale;
            var labelLine = Typography.LineHeight(PrismType.Headline);
            Typography.DrawCentered(drawList, new Vector2(center, y + labelLine * 0.5f),
                Typography.FitText(Loc.T(DeadlineLabel(week.Phase)), width, PrismType.Headline), PrismArt.Fore,
                PrismType.Headline);
            y += labelLine + Metrics.Space.Sm * scale;
            var tilesWidth = MathF.Min(width, ArenaCountdownUnits * scale);
            var tiles = new Rect(new Vector2(center - tilesWidth * 0.5f, y),
                new Vector2(center + tilesWidth * 0.5f, y + PrismCountdown.LargeHeight(scale)));
            arenaCountdown.DrawLarge(drawList, tiles, deadline, scale);
            y = tiles.Max.Y;
        }

        if (PrismPhases.Judgeable(week.Phase))
        {
            var judged = JudgedLine(week.BallotsCast);
            var reward = RewardLine(week.JudgeReward);
            y = Typography.DrawWrappedCentered(drawList,
                reward.Length > 0 ? FactLine(ref arenaJudgeText, judged, reward) : judged, PrismType.Caption,
                PrismArt.Muted, new Vector2(center, y + Metrics.Space.Sm * scale), width);
        }

        return DrawPhaseTimeline(drawList, left, y + Metrics.Space.Lg * scale, width, week.Phase, scale) +
               Metrics.Space.Lg * scale;
    }

    private static float DrawCenteredHero(ImDrawListPtr drawList, float center, float top, float width, string title)
    {
        var pixels = PrismType.Hero(title, width, out var lines);
        var line = PrismType.LineHeight(PrismFace.Headline, pixels);
        for (var index = 0; index < lines.Length; index++)
        {
            var size = PrismType.Measure(lines[index], PrismFace.Headline, pixels);
            PrismType.Draw(drawList, new Vector2(center - size.X * 0.5f, top + index * line), lines[index],
                PrismArt.Fore, PrismFace.Headline, pixels);
        }

        return top + lines.Length * line;
    }

    private float DrawPhaseTimeline(ImDrawListPtr drawList, float left, float top, float width, string phase,
        float scale)
    {
        timelineLabels[0] = Loc.T(L.Prism.PhaseEntries);
        timelineLabels[1] = Loc.T(L.Prism.PhaseJudging);
        timelineLabels[2] = Loc.T(L.Prism.PhaseFinal);
        timelineLabels[3] = Loc.T(L.Prism.PhaseResults);
        var current = TimelineStep(phase);
        var step = width / TimelineSteps;
        var node = TimelineNodeUnits * scale;
        var ring = node + PrismLight.EdgeWidth(scale) * 2f + Metrics.Space.Xxs * scale;
        var lineY = top + ring;
        var thickness = MathF.Max(1f, TimelineLineUnits * scale);
        var firstX = left + step * 0.5f;
        var litX = firstX + step * current;
        drawList.AddRectFilled(new Vector2(litX, lineY - thickness * 0.5f),
            new Vector2(left + width - step * 0.5f, lineY + thickness * 0.5f), ImGui.GetColorU32(PrismArt.Faint));
        if (current > 0)
        {
            PrismLight.Strip(drawList, firstX, litX, lineY - thickness * 0.5f, thickness, PrismLight.TabAlpha);
        }

        var labelTop = lineY + ring + Metrics.Space.Xs * scale;
        var labelLine = Typography.LineHeight(PrismType.Caption);
        for (var index = 0; index < TimelineSteps; index++)
        {
            var center = new Vector2(firstX + step * index, lineY);
            DrawTimelineNode(drawList, center, node, index - current, scale);
            var style = index == current ? TimelineCurrentStyle : PrismType.Caption;
            Typography.DrawCentered(drawList, new Vector2(center.X, labelTop + labelLine * 0.5f),
                Typography.FitText(timelineLabels[index], step - Metrics.Space.Xs * scale, style),
                index <= current ? PrismArt.Fore : PrismArt.Muted, style);
        }

        return labelTop + labelLine;
    }

    private static void DrawTimelineNode(ImDrawListPtr drawList, Vector2 center, float radius, int offset,
        float scale)
    {
        if (offset < 0)
        {
            drawList.AddCircleFilled(center, radius * TimelinePastShare, ImGui.GetColorU32(PrismArt.Fore));
            return;
        }

        if (offset == 0)
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(PrismArt.Fore));
            PrismLight.CircleEdge(drawList, center, radius, PrismGlow.Spectrum, PrismLight.EdgeAlpha, scale);
            return;
        }

        drawList.AddCircleFilled(center, radius * TimelinePastShare, ImGui.GetColorU32(PrismArt.Page));
        drawList.AddCircle(center, radius * TimelinePastShare, ImGui.GetColorU32(PrismArt.Faint), 0,
            PrismArt.HairlineWidth(scale));
    }

    private static int TimelineStep(string phase) => phase switch
    {
        PrismPhases.Entering => 0,
        PrismPhases.Judging => 1,
        PrismPhases.Final => 2,
        _ => 3,
    };

    private static float DrawContestPrompt(ImDrawListPtr drawList, float left, float top, float width, string phase,
        float scale)
    {
        var prompt = phase switch
        {
            PrismPhases.Entering => Loc.T(L.Prism.PickContest),
            PrismPhases.Judging or PrismPhases.Final => Loc.T(L.Prism.PickJudging),
            PrismPhases.Results => Loc.T(L.Prism.SeeResults),
            _ => string.Empty,
        };
        if (prompt.Length == 0)
        {
            return top;
        }

        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(prompt, width, PrismType.Title),
            PrismArt.Fore, PrismType.Title);
        return top + Typography.LineHeight(PrismType.Title) + Metrics.Space.Sm * scale;
    }

    private float DrawLastWeekPodiums(ImDrawListPtr drawList, float left, float top, float width, PrismWeekDto week,
        float scale)
    {
        var tracks = store.Tracks;
        var rows = tracks.Length > 1 ? Math.Min(tracks.Length, MaxTracks) : 1;
        var headed = false;
        for (var row = 0; row < rows; row++)
        {
            var track = tracks.Length > 1 ? tracks[row] : null;
            if (PodiumResults(track, week)?.FinalPodium is not { Length: > 0 } podium)
            {
                continue;
            }

            if (!headed)
            {
                top += Metrics.Space.Xl * scale;
                Typography.Draw(drawList, new Vector2(left, top),
                    Typography.FitText(Loc.T(L.Prism.LastWeekWinners), width, PrismType.Title), PrismArt.Fore,
                    PrismType.Title);
                top += Typography.LineHeight(PrismType.Title) + Metrics.Space.Sm * scale;
                headed = true;
            }

            if (track is not null)
            {
                DrawTrackName(drawList, new Vector2(left, top), width, track.Id, scale);
                top += Typography.LineHeight(PrismType.Headline) + Metrics.Space.Xs * scale;
            }

            top = DrawPodiumRow(drawList, left, top, width, podium, row, scale) + Metrics.Space.Md * scale;
        }

        return top;
    }

    private PrismResultsDto? PodiumResults(PrismTrackDto? track, PrismWeekDto week)
    {
        if (track is not null)
        {
            return store.LastWeekResults(track.Id);
        }

        return ShownResults() is { } shown && shown.WeekId == week.PreviousWeekId ? shown : null;
    }

    private float DrawPodiumRow(ImDrawListPtr drawList, float left, float top, float width,
        PrismPlacingDto[] podium, int row, float scale)
    {
        var gap = Metrics.Space.Sm * scale;
        var tileWidth = (width - gap * (PodiumTiles - 1)) / PodiumTiles;
        var bottom = top + tileWidth * CardHeightPerWidth;
        var captionTop = bottom + Metrics.Space.Xs * scale;
        var shown = false;
        for (var slot = 0; slot < PodiumTiles; slot++)
        {
            var rank = PodiumOrder[slot];
            if (PlacingAt(podium, rank) is not { } placing)
            {
                continue;
            }

            shown = true;
            var height = (bottom - top) * (rank == 1 ? 1f : PodiumRunnerShare);
            var x = left + slot * (tileWidth + gap);
            DrawPodiumTile(drawList, new Rect(new Vector2(x, bottom - height), new Vector2(x + tileWidth, bottom)),
                placing, row * PodiumTiles + slot, captionTop, scale);
        }

        return shown ? captionTop + Typography.LineHeight(PrismType.Caption) : top;
    }

    private static PrismPlacingDto? PlacingAt(PrismPlacingDto[] podium, int rank)
    {
        for (var index = 0; index < podium.Length; index++)
        {
            if (podium[index].Rank == rank)
            {
                return podium[index];
            }
        }

        return null;
    }

    private void DrawPodiumTile(ImDrawListPtr drawList, Rect tile, PrismPlacingDto placing, int slot,
        float captionTop, float scale)
    {
        if (ImGui.IsRectVisible(tile.Min, tile.Max))
        {
            if (placing.ImageUrl.Length > 0)
            {
                DrawLook(drawList, tile, placing.ImageUrl, false);
            }
            else
            {
                PrismArt.Placeholder(drawList, tile, false, scale);
            }

            var first = placing.Rank == 1;
            PrismLight.Edge(drawList, tile, 0f, first ? PrismGlow.Spectrum : PrismGlow.Pearl,
                first ? PrismLight.EdgeAlpha : PlacedPearlAlpha, scale);
            var radius = PodiumMedalUnits * scale;
            var inset = Metrics.Space.Sm * scale + radius;
            var medal = new Vector2(tile.Min.X + inset, tile.Min.Y + inset);
            PrismArt.Medal(drawList, medal, radius, placing.Rank, 0f, scale);
            Typography.DrawCentered(drawList, medal, RankDigits[Math.Clamp(placing.Rank, 0, PodiumTiles)],
                PrismArt.Fore, PrismType.Headline);
        }

        var line = Typography.LineHeight(PrismType.Caption);
        Typography.DrawCentered(drawList, new Vector2(tile.Center.X, captionTop + line * 0.5f),
            Typography.FitText(PodiumHandle(slot, placing), tile.Width, PrismType.Caption), PrismArt.Muted,
            PrismType.Caption);
        TapForOutfit(tile.Min, new Vector2(tile.Max.X, captionTop + line), placing);
    }

    private string PodiumHandle(int slot, PrismPlacingDto placing)
    {
        var key = HashCode.Combine(placing.Handle, placing.DisplayName);
        return podiumHandles[slot].IsCurrent(key)
            ? podiumHandles[slot].Value
            : podiumHandles[slot].Store(key, AtHandle(placing.Handle, placing.DisplayName));
    }

    private float DrawRewardsLink(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        top += Metrics.Space.Lg * scale;
        var rect = new Rect(new Vector2(left, top), new Vector2(left + width, top + Button.LargeHeight * scale));
        PrismLight.Primary(drawList, rect, true, scale);
        if (DrawButton(drawList, rect, Loc.T(L.Prism.WhatWinnersGet), ButtonStyle.Gray, scale,
                id: "prism.arena.rewards"))
        {
            OpenRewards();
        }

        return rect.Max.Y;
    }
}
