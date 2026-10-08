using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Race;

internal sealed class RaceTrackView
{
    public const float TrackLength = 1000f;
    public const float VisibleSpan = 210f;
    public const float ReplaySpan = 120f;
    public const float LeaderShare = 0.66f;
    public const float FarScale = 0.62f;
    public const float StrideUnits = 5f;
    public const float CoastUnits = 26f;
    public const float CoastTicks = 22f;

    private const float FollowSmoothSeconds = 0.32f;
    private const float LeadUnits = 6f;
    private const float StartShare = 0.36f;
    private const float GrandstandShare = 0.24f;
    private const float RailShare = 0.05f;
    private const float BirdLaneFactor = 1.75f;
    private const float DustSpacingUnits = 0.9f;
    private const float StandParallax = 0.3f;
    private const int CrowdLights = 64;
    private const int CrowdHeads = 36;
    private const float MarkerEvery = 100f;
    private const float PostEvery = 12f;
    private const float CheckerRows = 8f;
    private const float VerticalAnchorShare = 0.3f;
    private const float VerticalSpanShare = 0.9f;
    private const float RankSmoothing = 9f;

    private static readonly Vector4 DirtFar = new(0.27f, 0.19f, 0.13f, 1f);
    private static readonly Vector4 DirtNear = new(0.42f, 0.30f, 0.19f, 1f);
    private static readonly Vector4 RailInk = new(0.94f, 0.93f, 0.90f, 1f);
    private static readonly Vector4 StandInk = new(0.04f, 0.06f, 0.10f, 1f);
    private static readonly Vector4 DustInk = new(0.82f, 0.68f, 0.50f, 0.42f);
    private static readonly Vector4 Floodlight = new(0.85f, 0.93f, 1f, 1f);
    private static readonly Vector4 ReplayTint = new(0.95f, 0.82f, 0.55f, 0.12f);
    private static readonly Vector4 Black = new(0f, 0f, 0f, 1f);

    private static readonly float[] LightOffsets = BuildOffsets(CrowdLights, 0x5eed);
    private static readonly float[] LightRates = BuildOffsets(CrowdLights, 0xbead);
    private static readonly float[] HeadOffsets = BuildOffsets(CrowdHeads, 0xface);

    private readonly Ribbon[] dust = new Ribbon[RaceRules.FieldSize];
    private readonly float[] laneTop = new float[RaceRules.FieldSize];
    private readonly float[] laneHeight = new float[RaceRules.FieldSize];
    private readonly float[] laneScale = new float[RaceRules.FieldSize];
    private readonly float[] tickerRank = new float[RaceRules.FieldSize];

    private Camera2D camera = Camera2D.Create();
    private float crowdPhase;
    private float sweepPhase;
    private bool replaying;
    private bool tickerPlaced;
    private bool fresh = true;
    private bool vertical;

    public RaceTrackView()
    {
        for (var slot = 0; slot < dust.Length; slot++)
        {
            dust[slot] = new Ribbon();
        }
    }

    public Vector2 LeaderScreen { get; private set; }

    public Vector2 FinishScreen { get; private set; }

    public void Reset()
    {
        replaying = false;
        tickerPlaced = false;
        Recut();
    }

    private void Recut()
    {
        camera = Camera2D.Create();
        fresh = true;
        for (var slot = 0; slot < dust.Length; slot++)
        {
            dust[slot].Clear();
        }
    }

    private bool Prepare(RaceRoundPlayback playback, bool upright)
    {
        var replay = playback.Stage == RaceStage.Replay;
        if (replay != replaying || upright != vertical)
        {
            replaying = replay;
            vertical = upright;
            Recut();
        }

        return replay;
    }

    private void Aim(Vector2 target, Vector2 lead, float deltaSeconds)
    {
        if (fresh)
        {
            fresh = false;
            camera.Place(target + lead);
            return;
        }

        camera.Follow(target, lead, FollowSmoothSeconds, deltaSeconds);
    }

    public void Punch(float amount) => camera.Punch(amount);

    public void Shake(float amount) => camera.Shake(amount);

    public static float UnitsOf(int position) => position / (RaceScript.TrackUnits / TrackLength);

    public static float VisualDistance(RaceScriptPlan? plan, int slot, int position, long subTick)
    {
        var units = UnitsOf(position);
        if (plan is null || position < RaceScript.TrackUnits)
        {
            return units;
        }

        var past = (subTick - plan.FinishSubTicks[slot]) / (float)RaceScript.SubTicks;
        if (past <= 0f)
        {
            return units;
        }

        return units + CoastUnits * (1f - MathF.Exp(-past / CoastTicks));
    }

    public void Advance(float deltaSeconds, float stretch)
    {
        crowdPhase += deltaSeconds * (1f + 4f * stretch);
        sweepPhase += deltaSeconds;
    }

    public void DrawSide(ImDrawListPtr drawList, Rect area, RaceRoundPlayback playback,
        CasinoRaceRunnerDto[]? runners, float deltaSeconds, float stretch, float scale)
    {
        if (area.Width <= 1f || area.Height <= 1f)
        {
            return;
        }

        drawList.PushClipRect(area.Min, area.Max, true);
        var replay = Prepare(playback, false);
        var leaderUnits = LeaderUnits(playback);
        var span = replay ? ReplaySpan : VisibleSpan;
        var anchor = new Vector2(area.Min.X + area.Width * (replay ? 0.5f : LeaderShare), area.Center.Y);
        camera.Fit(area, span, 1f, FitMode.CoverWidth, anchor);
        if (replay)
        {
            camera.Place(new Vector2(TrackLength, 0f));
        }
        else
        {
            Aim(new Vector2(MathF.Max(leaderUnits, span * StartShare), 0f), new Vector2(LeadUnits, 0f), deltaSeconds);
        }

        camera.Update(deltaSeconds, scale);
        var standBottom = area.Min.Y + area.Height * GrandstandShare;
        DrawGrandstand(drawList, new Rect(area.Min, new Vector2(area.Max.X, standBottom)), stretch, scale);
        var farY = standBottom + area.Height * RailShare;
        var nearY = area.Max.Y - area.Height * RailShare;
        LayLanes(farY, nearY);
        DrawSurface(drawList, area, farY, nearY, scale);
        DrawFloodlights(drawList, area, scale);
        DrawBirdsSide(drawList, playback, runners, scale);
        if (replay)
        {
            drawList.AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(ReplayTint));
        }

        FinishScreen = new Vector2(LaneX(TrackLength, laneScale[RaceRules.FieldSize - 1]), nearY);
        drawList.PopClipRect();
    }

    public void DrawVertical(ImDrawListPtr drawList, Rect area, RaceRoundPlayback playback,
        CasinoRaceRunnerDto[]? runners, float deltaSeconds, float stretch, float scale)
    {
        if (area.Width <= 1f || area.Height <= 1f)
        {
            return;
        }

        drawList.PushClipRect(area.Min, area.Max, true);
        var replay = Prepare(playback, true);
        var span = (replay ? ReplaySpan : VisibleSpan) * VerticalSpanShare;
        var anchor = new Vector2(area.Center.X, area.Min.Y + area.Height * (replay ? 0.5f : VerticalAnchorShare));
        camera.Fit(area, 1f, span, FitMode.CoverHeight, anchor);
        if (replay)
        {
            camera.Place(new Vector2(0f, -TrackLength));
        }
        else
        {
            Aim(new Vector2(0f, -LeaderUnits(playback)), new Vector2(0f, -LeadUnits), deltaSeconds);
        }

        camera.Update(deltaSeconds, scale);
        var standHeight = area.Height * 0.08f;
        DrawGrandstand(drawList, new Rect(area.Min, new Vector2(area.Max.X, area.Min.Y + standHeight)), stretch, scale);
        var track = new Rect(new Vector2(area.Min.X, area.Min.Y + standHeight), area.Max);
        drawList.AddRectFilledMultiColor(track.Min, track.Max, ImGui.GetColorU32(DirtFar), ImGui.GetColorU32(DirtFar),
            ImGui.GetColorU32(DirtNear), ImGui.GetColorU32(DirtNear));
        var laneWidth = track.Width / RaceRules.FieldSize;
        var hairline = ImGui.GetColorU32(RailInk with { W = 0.1f });
        for (var lane = 1; lane < RaceRules.FieldSize; lane++)
        {
            var x = track.Min.X + lane * laneWidth;
            drawList.AddLine(new Vector2(x, track.Min.Y), new Vector2(x, track.Max.Y), hairline, MathF.Max(1f, scale));
        }

        DrawVerticalMarkers(drawList, track, scale);
        DrawFloodlights(drawList, area, scale);
        DrawBirdsTop(drawList, playback, runners, track, laneWidth, scale);
        if (replay)
        {
            drawList.AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(ReplayTint));
        }

        FinishScreen = new Vector2(track.Center.X, camera.ToScreen(new Vector2(0f, -TrackLength)).Y);
        drawList.PopClipRect();
    }

    public float DrawTicker(ImDrawListPtr drawList, Rect row, RaceRoundPlayback playback,
        CasinoRaceRunnerDto[]? runners, ReadOnlySpan<bool> mine, float deltaSeconds, float scale)
    {
        if (runners is not { Length: RaceRules.FieldSize } || row.Width <= 0f)
        {
            return row.Min.Y;
        }

        var gap = 4f * scale;
        var columns = row.Width >= 520f * scale ? RaceRules.FieldSize : RaceRules.FieldSize / 2;
        var rows = RaceRules.FieldSize / columns;
        var cellWidth = (row.Width - gap * (columns - 1)) / columns;
        var cellHeight = (row.Height - gap * (rows - 1)) / rows;
        var ranking = playback.Ranking;
        var smoothing = MathF.Min(1f, deltaSeconds * RankSmoothing);
        for (var place = 0; place < RaceRules.FieldSize; place++)
        {
            var slot = ranking[place];
            tickerRank[slot] = tickerPlaced ? tickerRank[slot] + (place - tickerRank[slot]) * smoothing : place;
        }

        tickerPlaced = true;
        for (var place = 0; place < RaceRules.FieldSize; place++)
        {
            var slot = ranking[place];
            var shown = tickerRank[slot];
            var line = Math.Clamp((int)MathF.Round(shown) / columns, 0, rows - 1);
            var x = row.Min.X + (shown - line * columns) * (cellWidth + gap);
            var top = row.Min.Y + line * (cellHeight + gap);
            var cell = new Rect(new Vector2(x, top), new Vector2(x + cellWidth, top + cellHeight));
            DrawTickerCell(drawList, cell, place, runners[slot], playback.Crossed(slot),
                slot < mine.Length && mine[slot], scale);
        }

        return row.Max.Y;
    }

    private static void DrawTickerCell(ImDrawListPtr drawList, Rect cell, int place, CasinoRaceRunnerDto runner,
        bool home, bool backed, float scale)
    {
        var radius = cell.Height * 0.5f;
        var lead = place == 0;
        Squircle.Fill(drawList, cell.Min, cell.Max, radius,
            ImGui.GetColorU32(lead ? CasinoColors.Money with { W = 0.24f } : new Vector4(0f, 0f, 0f, 0.42f)));
        if (backed || home)
        {
            Squircle.Stroke(drawList, cell.Min, cell.Max, radius,
                ImGui.GetColorU32(backed ? CasinoColors.LightA : CasinoColors.Money with { W = 0.7f }),
                MathF.Max(1f, (backed ? 1.6f : 1f) * scale));
        }

        var disc = cell.Height * 0.34f;
        var discCenter = new Vector2(cell.Min.X + radius, cell.Center.Y);
        var cloth = RaceBirdArt.ClothOf(runner.Slot);
        drawList.AddCircleFilled(discCenter, disc, ImGui.GetColorU32(cloth), 16);
        Typography.DrawCentered(drawList, discCenter, GameNumber.Label(runner.Slot + 1), RaceBirdArt.InkOn(cloth),
            TextStyles.FootnoteEmphasized);
        var textLeft = discCenter.X + disc + 4f * scale;
        var available = cell.Max.X - radius * 0.6f - textLeft;
        if (available <= 4f * scale)
        {
            return;
        }

        var style = TextStyles.FootnoteEmphasized;
        var name = Typography.FitText(runner.Name, available, style);
        var size = Typography.Measure(name, style);
        Typography.Draw(drawList, new Vector2(textLeft, cell.Center.Y - size.Y * 0.5f), name,
            lead ? CasinoColors.MoneyHighlight : CasinoColors.InkTitle, style);
    }

    private float LeaderUnits(RaceRoundPlayback playback)
    {
        var leader = playback.Leader;
        if (!RaceRules.IsRunner(leader))
        {
            return 0f;
        }

        return VisualDistance(playback.Plan, leader, playback.Positions[leader], playback.DisplaySubTick);
    }

    private void LayLanes(float farY, float nearY)
    {
        var total = 0f;
        for (var lane = 0; lane < RaceRules.FieldSize; lane++)
        {
            var depth = lane / (float)(RaceRules.FieldSize - 1);
            laneScale[lane] = FarScale + (1f - FarScale) * depth;
            total += laneScale[lane];
        }

        var top = farY;
        var height = nearY - farY;
        for (var lane = 0; lane < RaceRules.FieldSize; lane++)
        {
            laneTop[lane] = top;
            laneHeight[lane] = height * laneScale[lane] / total;
            top += laneHeight[lane];
        }
    }

    private float LaneX(float units, float depthScale)
    {
        var screen = camera.ToScreen(new Vector2(units, 0f));
        return camera.Anchor.X + (screen.X - camera.Anchor.X) * depthScale;
    }

    private Camera2D LaneCamera(int lane, float groundY)
    {
        var copy = camera;
        copy.Zoom = camera.Zoom * laneScale[lane];
        copy.Anchor = new Vector2(camera.Anchor.X, groundY);
        copy.Origin = new Vector2(camera.Origin.X, 0f);
        return copy;
    }

    private void DrawGrandstand(ImDrawListPtr drawList, Rect stand, float stretch, float scale)
    {
        if (stand.Height <= 1f)
        {
            return;
        }

        drawList.AddRectFilledMultiColor(stand.Min, stand.Max, ImGui.GetColorU32(StandInk with { W = 0.2f }),
            ImGui.GetColorU32(StandInk with { W = 0.2f }), ImGui.GetColorU32(StandInk), ImGui.GetColorU32(StandInk));
        var shift = -camera.Origin.X * camera.Zoom * StandParallax;
        var width = stand.Width;
        var headRadius = width / CrowdHeads * 0.55f;
        var headColor = ImGui.GetColorU32(StandInk);
        for (var head = 0; head < CrowdHeads; head++)
        {
            var x = stand.Min.X + Wrap((head + HeadOffsets[head] * 0.6f) / CrowdHeads * width + shift, width);
            drawList.AddCircleFilled(new Vector2(x, stand.Min.Y + stand.Height * 0.35f), headRadius, headColor, 12);
        }

        var lightRadius = MathF.Max(1f, 1.4f * scale);
        for (var light = 0; light < CrowdLights; light++)
        {
            var x = stand.Min.X + Wrap(LightOffsets[light] * width + shift * 1.1f, width);
            var y = stand.Min.Y + stand.Height * (0.45f + 0.45f * LightRates[light]);
            var rate = 1.2f + 2.6f * LightRates[light];
            var glow = MathF.Max(0f, MathF.Sin(crowdPhase * rate * MathF.Tau + LightOffsets[light] * 40f));
            var tint = light % 3 == 0 ? CasinoColors.LightB : light % 3 == 1 ? CasinoColors.Money : Floodlight;
            var alpha = 0.18f + 0.72f * glow * (0.55f + 0.45f * stretch);
            drawList.AddCircleFilled(new Vector2(x, y), lightRadius * (1f + glow * stretch * 0.8f),
                ImGui.GetColorU32(tint with { W = alpha }), 6);
        }
    }

    private void DrawSurface(ImDrawListPtr drawList, Rect area, float farY, float nearY, float scale)
    {
        drawList.AddRectFilledMultiColor(new Vector2(area.Min.X, farY), new Vector2(area.Max.X, nearY),
            ImGui.GetColorU32(DirtFar), ImGui.GetColorU32(DirtFar), ImGui.GetColorU32(DirtNear),
            ImGui.GetColorU32(DirtNear));
        var hairline = ImGui.GetColorU32(RailInk with { W = 0.07f });
        for (var lane = 1; lane < RaceRules.FieldSize; lane++)
        {
            drawList.AddLine(new Vector2(area.Min.X, laneTop[lane]), new Vector2(area.Max.X, laneTop[lane]), hairline,
                MathF.Max(1f, scale));
        }

        var visible = camera.VisibleWorld;
        var markerInk = ImGui.GetColorU32(RailInk with { W = 0.22f });
        var first = MathF.Floor(visible.Min.X / MarkerEvery) * MarkerEvery;
        for (var marker = first; marker <= visible.Max.X + MarkerEvery; marker += MarkerEvery)
        {
            if (marker <= 0f || marker >= TrackLength)
            {
                continue;
            }

            drawList.AddLine(new Vector2(LaneX(marker, FarScale), farY), new Vector2(LaneX(marker, 1f), nearY),
                markerInk, MathF.Max(1f, 1.5f * scale));
        }

        DrawGate(drawList, farY, nearY, scale);
        DrawFinish(drawList, farY, nearY, scale);
        DrawRail(drawList, area, farY, FarScale, scale);
        DrawRail(drawList, area, nearY, 1f, scale);
    }

    private void DrawRail(ImDrawListPtr drawList, Rect area, float y, float depthScale, float scale)
    {
        var rail = ImGui.GetColorU32(RailInk with { W = 0.85f });
        var postHeight = 9f * scale * depthScale;
        drawList.AddLine(new Vector2(area.Min.X, y - postHeight), new Vector2(area.Max.X, y - postHeight), rail,
            MathF.Max(1f, 2f * scale * depthScale));
        var visible = camera.VisibleWorld;
        var span = visible.Max.X - visible.Min.X;
        var first = MathF.Floor((visible.Min.X - span) / PostEvery) * PostEvery;
        var last = visible.Max.X + span;
        for (var post = first; post <= last; post += PostEvery)
        {
            var x = LaneX(post, depthScale);
            if (x < area.Min.X - 4f || x > area.Max.X + 4f)
            {
                continue;
            }

            drawList.AddLine(new Vector2(x, y - postHeight), new Vector2(x, y), rail, MathF.Max(1f, scale * depthScale));
        }
    }

    private void DrawGate(ImDrawListPtr drawList, float farY, float nearY, float scale)
    {
        var bar = ImGui.GetColorU32(new Vector4(0.16f, 0.18f, 0.22f, 0.9f));
        for (var lane = 0; lane <= RaceRules.FieldSize; lane++)
        {
            var y = lane < RaceRules.FieldSize ? laneTop[lane] : nearY;
            var depth = lane < RaceRules.FieldSize ? laneScale[lane] : 1f;
            var x = LaneX(-3f, depth);
            drawList.AddLine(new Vector2(x, y - 14f * scale * depth), new Vector2(x, y), bar,
                MathF.Max(1f, 3f * scale * depth));
        }

        drawList.AddLine(new Vector2(LaneX(-3f, FarScale), farY - 14f * scale * FarScale),
            new Vector2(LaneX(-3f, 1f), nearY - 14f * scale), bar, MathF.Max(1f, 3f * scale));
    }

    private void DrawFinish(ImDrawListPtr drawList, float farY, float nearY, float scale)
    {
        var width = 3f;
        var dark = ImGui.GetColorU32(new Vector4(0.08f, 0.08f, 0.1f, 0.9f));
        var light = ImGui.GetColorU32(RailInk with { W = 0.9f });
        for (var row = 0; row < (int)CheckerRows; row++)
        {
            var fromShare = row / CheckerRows;
            var toShare = (row + 1) / CheckerRows;
            var topY = farY + (nearY - farY) * fromShare;
            var bottomY = farY + (nearY - farY) * toShare;
            var topDepth = FarScale + (1f - FarScale) * fromShare;
            var bottomDepth = FarScale + (1f - FarScale) * toShare;
            for (var column = 0; column < 2; column++)
            {
                var left = TrackLength + (column - 1) * width * 0.5f;
                var right = left + width * 0.5f;
                var color = (row + column) % 2 == 0 ? light : dark;
                drawList.AddQuadFilled(new Vector2(LaneX(left, topDepth), topY), new Vector2(LaneX(right, topDepth), topY),
                    new Vector2(LaneX(right, bottomDepth), bottomY), new Vector2(LaneX(left, bottomDepth), bottomY),
                    color);
            }
        }

        var postX = LaneX(TrackLength, 1f);
        var postTop = nearY - 64f * scale;
        drawList.AddLine(new Vector2(postX, nearY), new Vector2(postX, postTop), light, MathF.Max(1f, 3f * scale));
        drawList.AddCircleFilled(new Vector2(postX, postTop), 7f * scale, ImGui.GetColorU32(CasinoColors.Money), 16);
        drawList.AddCircleFilled(new Vector2(postX, postTop), 14f * scale,
            ImGui.GetColorU32(CasinoColors.Money with { W = 0.18f }), 20);
    }

    private void DrawFloodlights(ImDrawListPtr drawList, Rect area, float scale)
    {
        var swing = MathF.Sin(sweepPhase * 0.45f) * 0.35f;
        var length = area.Height * 1.4f;
        CasinoLights.Spotlight(drawList, area.Min + new Vector2(area.Width * 0.08f, 0f), MathF.PI * 0.32f + swing,
            length, 0.16f, Floodlight, 0.10f);
        CasinoLights.Spotlight(drawList, new Vector2(area.Max.X - area.Width * 0.08f, area.Min.Y),
            MathF.PI * 0.68f - swing, length, 0.16f, Floodlight, 0.10f);
        CasinoLights.Spotlight(drawList, new Vector2(area.Center.X, area.Min.Y), MathF.PI * 0.5f + swing * 1.4f,
            length * 0.8f, 0.10f, CasinoColors.LightA, 0.06f);
    }

    private void DrawBirdsSide(ImDrawListPtr drawList, RaceRoundPlayback playback, CasinoRaceRunnerDto[]? runners,
        float scale)
    {
        var positions = playback.Positions;
        var plan = playback.Plan;
        var leader = playback.Leader;
        for (var lane = 0; lane < RaceRules.FieldSize; lane++)
        {
            var slot = lane;
            var runner = RunnerAt(runners, slot);
            var units = VisualDistance(plan, slot, positions[slot], playback.DisplaySubTick);
            var groundY = laneTop[lane] + laneHeight[lane] * 0.82f;
            var height = laneHeight[lane] * BirdLaneFactor;
            var laneCamera = LaneCamera(lane, groundY);
            var foot = laneCamera.ToScreen(new Vector2(units, 0f));
            var ribbon = dust[slot];
            ribbon.PushSpaced(new Vector2(units - 0.06f * height / MathF.Max(0.001f, laneCamera.EffectiveZoom), 0f),
                DustSpacingUnits);
            ribbon.Draw(drawList, laneCamera, DustInk, MathF.Max(1f, height * 0.16f));
            if (slot == leader && playback.Stage is RaceStage.Running or RaceStage.Replay)
            {
                Shapes.FillEllipse(drawList, foot, new Vector2(height * 0.55f, height * 0.12f),
                    ImGui.GetColorU32(CasinoColors.Money with { W = 0.22f }), 20);
                LeaderScreen = foot - new Vector2(0f, height * 0.6f);
            }

            var frame = Moving(playback, slot) ? (int)(units / (StrideUnits * 0.5f)) & 1 : 0;
            RaceBirdArt.DrawSide(drawList, foot, height, runner?.Colour ?? slot, runner?.Silk ?? 0, slot, frame, 1f);
        }
    }

    private void DrawBirdsTop(ImDrawListPtr drawList, RaceRoundPlayback playback, CasinoRaceRunnerDto[]? runners,
        Rect track, float laneWidth, float scale)
    {
        var positions = playback.Positions;
        var plan = playback.Plan;
        var length = MathF.Min(laneWidth * 0.95f, track.Height * 0.12f);
        for (var slot = 0; slot < RaceRules.FieldSize; slot++)
        {
            var runner = RunnerAt(runners, slot);
            var units = VisualDistance(plan, slot, positions[slot], playback.DisplaySubTick);
            var centerX = track.Min.X + (slot + 0.5f) * laneWidth;
            var screenY = camera.ToScreen(new Vector2(0f, -units)).Y;
            var laneCamera = camera;
            laneCamera.Anchor = new Vector2(centerX, camera.Anchor.Y);
            laneCamera.Origin = new Vector2(0f, camera.Origin.Y);
            var ribbon = dust[slot];
            ribbon.PushSpaced(new Vector2(0f, -units + 0.3f * length / MathF.Max(0.001f, camera.EffectiveZoom)),
                DustSpacingUnits);
            ribbon.Draw(drawList, laneCamera, DustInk, MathF.Max(1f, length * 0.3f));
            if (slot == playback.Leader && playback.Stage is RaceStage.Running or RaceStage.Replay)
            {
                drawList.AddCircleFilled(new Vector2(centerX, screenY), length * 0.6f,
                    ImGui.GetColorU32(CasinoColors.Money with { W = 0.2f }), 20);
                LeaderScreen = new Vector2(centerX, screenY);
            }

            var frame = Moving(playback, slot) ? (int)(units / (StrideUnits * 0.5f)) & 1 : 0;
            RaceBirdArt.DrawTop(drawList, new Vector2(centerX, screenY), length, runner?.Colour ?? slot,
                runner?.Silk ?? 0, slot, frame, 1f);
        }
    }

    private void DrawVerticalMarkers(ImDrawListPtr drawList, Rect track, float scale)
    {
        var markerInk = ImGui.GetColorU32(RailInk with { W = 0.2f });
        var visible = camera.VisibleWorld;
        var nearest = -visible.Max.Y;
        var farthest = -visible.Min.Y;
        var first = MathF.Floor(nearest / MarkerEvery) * MarkerEvery;
        for (var marker = first; marker <= farthest + MarkerEvery; marker += MarkerEvery)
        {
            if (marker <= 0f || marker >= TrackLength)
            {
                continue;
            }

            var y = camera.ToScreen(new Vector2(0f, -marker)).Y;
            drawList.AddLine(new Vector2(track.Min.X, y), new Vector2(track.Max.X, y), markerInk, MathF.Max(1f, scale));
        }

        var finishY = camera.ToScreen(new Vector2(0f, -TrackLength)).Y;
        var cell = track.Width / 16f;
        var light = ImGui.GetColorU32(RailInk with { W = 0.9f });
        var dark = ImGui.GetColorU32(new Vector4(0.08f, 0.08f, 0.1f, 0.9f));
        for (var column = 0; column < 16; column++)
        {
            for (var row = 0; row < 2; row++)
            {
                var min = new Vector2(track.Min.X + column * cell, finishY - cell + row * cell * 0.5f);
                drawList.AddRectFilled(min, min + new Vector2(cell, cell * 0.5f), (column + row) % 2 == 0 ? light : dark);
            }
        }

        var startY = camera.ToScreen(new Vector2(0f, 3f)).Y;
        drawList.AddLine(new Vector2(track.Min.X, startY), new Vector2(track.Max.X, startY),
            ImGui.GetColorU32(Black with { W = 0.5f }), MathF.Max(1f, 3f * scale));
    }

    private static bool Moving(RaceRoundPlayback playback, int slot) =>
        playback.Stage is RaceStage.Running or RaceStage.Replay or RaceStage.Finished
        && playback.Positions[slot] > 0;

    private static CasinoRaceRunnerDto? RunnerAt(CasinoRaceRunnerDto[]? runners, int slot) =>
        runners is { Length: RaceRules.FieldSize } ? runners[slot] : null;

    private static float Wrap(float value, float width)
    {
        if (width <= 0f)
        {
            return 0f;
        }

        var wrapped = value % width;
        return wrapped < 0f ? wrapped + width : wrapped;
    }

    private static float[] BuildOffsets(int count, uint seed)
    {
        var random = GameRandom.FromSeed(seed);
        var values = new float[count];
        for (var index = 0; index < count; index++)
        {
            values[index] = random.NextFloat();
        }

        return values;
    }
}
