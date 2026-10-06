using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Swoop;

internal sealed class SwoopApp : ILegacyMiniGame
{
    private const string GameId = "swoop";
    private const float BannerSeconds = 1.7f;
    private const float TiltSmoothSeconds = 0.08f;
    private const float FeverSmoothSeconds = 0.25f;
    private const float FeverEmberInterval = 0.035f;
    private const float AirCalloutSeconds = 1f;
    private const float BirdDrawScale = 1.3f;
    private static readonly string[] PickupLabels = { "+" + SwoopBoard.CrystalPoints, "+" + SwoopBoard.CrystalPoints * 2 };
    private static readonly Vector4 DustColor = new(0.86f, 0.74f, 0.56f, 0.9f);
    private static readonly Vector4 SparkleColor = new(1f, 0.93f, 0.58f, 1f);
    private static readonly Vector4 CrystalSparkle = new(0.62f, 0.97f, 1f, 1f);
    private static readonly Vector4 FeverColor = new(0.80f, 1f, 0.40f, 1f);
    private static readonly Vector4 ThudColor = new(1f, 0.62f, 0.52f, 1f);
    private static readonly Vector4 NightTitle = new(0.74f, 0.70f, 1f, 1f);
    private static readonly Vector4 TextShadow = new(0f, 0f, 0f, 0.35f);
    private static readonly Vector4[] IslandConfetti =
    {
        new(1f, 0.84f, 0.32f, 1f), new(0.80f, 1f, 0.40f, 1f), new(0.62f, 0.97f, 1f, 1f),
        new(1f, 0.62f, 0.70f, 1f), new(1f, 1f, 1f, 1f),
    };

    private readonly SwoopBoard board = new();
    private readonly SwoopRenderer renderer = new();
    private readonly ParticleSystem worldParticles = new(384);
    private readonly FeedbackFx fx = new();
    private readonly FeedbackFx worldFx = new();
    private SwoopCamera camera;
    private SwoopBirdPose pose;
    private RollingValue scoreRoll;
    private Spring tilt = new(0f);
    private Spring feverGlow = new(0f);
    private float squash;
    private float flapPhase;
    private float emberTimer;
    private bool started;
    private bool playing;
    private bool finished;
    private bool pendingSubmit;
    private bool newBest;
    private bool statsLoaded;
    private bool cameraReady;
    private bool mouseHeld;
    private int bestScore;
    private uint runCounter;
    private float resultAppear;
    private float bannerProgress = 1f;
    private string bannerText = string.Empty;
    private string resultLine = string.Empty;
    private string altitudeText = string.Empty;
    private int altitudeValue = -1;
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Swoop.Title);
    public GameGenre Genre => GameGenre.Action;
    public bool RunsOnAClock => true;

    public void Open()
    {
        started = false;
        statsLoaded = false;
    }

    public void Close()
    {
    }

    public void Dispose()
    {
    }

    private void StartNewRun()
    {
        runCounter++;
        board.Start((uint)Environment.TickCount ^ (runCounter * 2654435761u));
        renderer.ClearTrail();
        worldParticles.Clear();
        fx.Clear();
        worldFx.Clear();
        scoreRoll.Snap(0);
        tilt.SnapTo(0f);
        feverGlow.SnapTo(0f);
        squash = 0f;
        flapPhase = 0f;
        emberTimer = 0f;
        playing = false;
        finished = false;
        pendingSubmit = false;
        newBest = false;
        cameraReady = false;
        mouseHeld = false;
        resultAppear = 0f;
        bannerProgress = 1f;
        altitudeValue = -1;
        started = true;
    }

    public void Draw(in GameContext context)
    {
        var deltaSeconds = context.DeltaSeconds;
        var scale = UiScale.Current;
        var theme = context.Theme;
        var area = context.Body;
        if (!statsLoaded)
        {
            bestScore = context.Stats.Get(GameId).BestScore;
            statsLoaded = true;
        }

        if (!started)
        {
            StartNewRun();
        }

        if (pendingSubmit)
        {
            newBest = context.Stats.SubmitScore(GameId, board.Score);
            if (newBest)
            {
                bestScore = board.Score;
            }

            pendingSubmit = false;
        }

        var rowY = area.Min.Y + 30f * scale;
        var restartCenter = new Vector2(area.Max.X - 26f * scale, rowY);
        var restartRadius = 16f * scale;
        var holding = false;
        if (!finished)
        {
            holding = ReadInput(area, restartCenter, restartRadius, out var pressed);
            if (!playing && pressed)
            {
                playing = true;
                ShowBanner(Loc.T(L.Swoop.Island, GameNumber.Label(board.CurrentIsland + 1)));
            }
        }

        if (playing)
        {
            board.Tick(deltaSeconds, holding);
        }

        if (!cameraReady)
        {
            camera.Reset(board, area);
            cameraReady = true;
        }

        camera.Update(board, area, deltaSeconds);
        ReactToEvents(scale);
        UpdatePose(deltaSeconds);
        worldParticles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        worldFx.Update(deltaSeconds);
        bannerProgress = GameBanner.Advance(bannerProgress, deltaSeconds, BannerSeconds);
        if (board.GameOver && !finished)
        {
            finished = true;
            pendingSubmit = true;
            resultAppear = 0f;
            resultLine = Loc.T(L.Swoop.ResultLine, GameNumber.Label(board.Distance), GameNumber.Label(board.CurrentIsland + 1),
                board.BestAirSeconds.ToString("0.0", Loc.Culture));
        }

        var drawList = ImGui.GetWindowDrawList();
        var view = camera;
        view.Shift(fx.ShakeOffset(scale));
        var lighting = SwoopLighting.At(playing ? board.DayProgress : 0f);
        var time = (float)ImGui.GetTime();
        drawList.PushClipRect(area.Min, area.Max, true);
        SwoopSky.Draw(drawList, area, view, lighting, time, scale);
        renderer.PrepareColors(lighting);
        renderer.DrawTerrain(drawList, board, view, area, scale);
        renderer.DrawMarkers(drawList, board, view, area, lighting, time, scale);
        renderer.DrawCrystals(drawList, board, view, area, lighting, time, scale);
        SwoopRenderer.DrawDarkness(drawList, area, board, lighting, scale);
        DrawBird(drawList, view, lighting, time);
        DrawWorldEffects(drawList, view);
        if (playing && !finished)
        {
            SwoopRenderer.DrawSpeedLines(drawList, area, board.Speed, time, scale);
        }

        fx.DrawFlash(drawList, area, 0f);
        DrawAltitudeArrow(drawList, area, scale);
        drawList.PopClipRect();
        fx.DrawText();
        DrawHud(drawList, area, rowY, restartCenter, restartRadius, theme, deltaSeconds, scale);
        if (!playing)
        {
            DrawReadyHint(drawList, area, scale);
        }

        GameBanner.Draw(drawList, new Vector2(area.Center.X, area.Min.Y + area.Height * 0.3f), bannerText, Accent, theme, bannerProgress);
        if (finished)
        {
            DrawResult(area, theme, deltaSeconds);
        }
    }

    private bool ReadInput(Rect area, Vector2 restartCenter, float restartRadius, out bool pressed)
    {
        var overRestart = Vector2.DistanceSquared(ImGui.GetMousePos(), restartCenter) <= restartRadius * restartRadius;
        pressed = false;
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !overRestart && UiInteract.Hover(area.Min, area.Max))
        {
            mouseHeld = true;
            pressed = true;
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            mouseHeld = false;
        }

        if (GameInput.Pressed(ImGuiKey.Space) || GameInput.Pressed(ImGuiKey.W, ImGuiKey.UpArrow))
        {
            pressed = true;
        }

        var keys = GameInput.Held(ImGuiKey.Space) || GameInput.Held(ImGuiKey.W, ImGuiKey.UpArrow);
        return mouseHeld || keys;
    }

    private void ReactToEvents(float scale)
    {
        var birdWorld = SwoopCamera.WorldPixels(board.X, board.Y + SwoopBoard.BirdRadius * 0.4f);
        var birdScreen = camera.ToScreen(board.X, board.Y + SwoopBoard.BirdRadius);
        var calloutY = 40f * scale;
        if (board.LaunchedThisTick)
        {
            UiFeedback.Play(UiSound.GameJump);
            worldParticles.Streaks(birdWorld, 6, new Vector4(1f, 1f, 1f, 0.7f), 160f, 1.6f, 0.35f, 0.8f, MathF.PI);
        }

        if (board.SmoothThisTick)
        {
            UiFeedback.Play(UiSound.GamePop);
            squash = 0.22f;
            worldParticles.Sparkle(birdWorld, 14, SparkleColor, 140f, 2.6f, 0.7f);
            worldFx.Shockwave(birdWorld, 36f, SparkleColor with { W = 0.8f }, 0.45f, 2.6f);
            fx.AddText(Loc.T(L.Swoop.Smooth), birdScreen - new Vector2(0f, calloutY), SparkleColor, 1.15f);
        }

        if (board.ThudThisTick)
        {
            UiFeedback.Play(UiSound.GameHitSoft);
            squash = 0.42f;
            var impact = Math.Clamp(board.LastImpactSpeed / 25f, 0f, 1f);
            worldParticles.Burst(birdWorld, 18, DustColor, 150f, 4.2f, 0.75f, 260f, MathF.PI, -MathF.PI * 0.5f);
            worldParticles.Burst(birdWorld, 8, DustColor with { W = 0.55f }, 90f, 6.5f, 1f, 40f, MathF.PI * 0.6f, -MathF.PI * 0.5f);
            worldFx.Shockwave(birdWorld, 44f, DustColor, 0.4f, 3f);
            fx.AddTrauma(0.3f + 0.3f * impact);
            fx.AddText(Loc.T(L.Swoop.Thud), birdScreen - new Vector2(0f, calloutY), ThudColor, 1.05f);
        }

        if (board.AirCountedThisTick && board.LastAirSeconds >= AirCalloutSeconds)
        {
            fx.AddText(Loc.T(L.Swoop.AirTime, board.LastAirSeconds.ToString("0.0", Loc.Culture)),
                birdScreen - new Vector2(0f, calloutY * 1.8f), new Vector4(1f, 1f, 1f, 1f), 0.95f);
        }

        if (board.FeverStartedThisTick)
        {
            UiFeedback.Play(UiSound.GamePowerUp);
            fx.Flash(FeverColor, 0.22f);
            worldParticles.Sparkle(birdWorld, 24, FeverColor, 220f, 3f, 0.9f);
            worldFx.Shockwave(birdWorld, 90f, FeverColor, 0.6f, 4f);
            fx.AddText(Loc.T(L.Swoop.FeverStart), birdScreen - new Vector2(0f, calloutY * 2.6f), FeverColor, 1.3f);
        }

        if (board.PickupsThisTick > 0)
        {
            UiFeedback.Play(UiSound.GameCollect);
            var label = PickupLabels[board.Fever ? 1 : 0];
            for (var pickup = 0; pickup < board.PickupsThisTick; pickup++)
            {
                var crystalWorld = SwoopCamera.WorldPixels(board.PickupX(pickup), board.PickupY(pickup));
                worldParticles.Sparkle(crystalWorld, 7, CrystalSparkle, 110f, 2.2f, 0.5f);
                fx.AddText(label, camera.ToScreen(board.PickupX(pickup), board.PickupY(pickup)), CrystalSparkle, 0.8f, 60f);
            }
        }

        if (board.IslandReachedThisTick)
        {
            UiFeedback.Play(UiSound.GameClear);
            ShowBanner(Loc.T(L.Swoop.Island, GameNumber.Label(board.CurrentIsland + 1)));
            var flag = SwoopCamera.WorldPixels(board.Terrain.IslandStart(board.CurrentIsland), 9f);
            worldParticles.Confetti(flag, 50, IslandConfetti, 260f, 3.6f, 1.3f);
            fx.Flash(new Vector4(1f, 0.9f, 0.5f, 1f), 0.15f);
            if (board.LastBonusSeconds >= 0.5f)
            {
                fx.AddText(Loc.T(L.Swoop.TimeBonus, GameNumber.Label((int)MathF.Round(board.LastBonusSeconds))),
                    new Vector2(birdScreen.X, birdScreen.Y - calloutY * 3.2f), new Vector4(1f, 0.9f, 0.5f, 1f), 1.2f);
            }
        }

        if (board.NightFellThisTick)
        {
            UiFeedback.Play(UiSound.GameTick);
            ShowBanner(Loc.T(L.Swoop.Nightfall));
        }
    }

    private void ShowBanner(string text)
    {
        bannerText = text;
        bannerProgress = 0f;
    }

    private void UpdatePose(float deltaSeconds)
    {
        var slope = board.Grounded ? board.GroundSlope : (float)board.Terrain.Slope(board.X);
        var speed = MathF.Sqrt(board.VelocityX * board.VelocityX + board.VelocityY * board.VelocityY);
        var target = speed > 0.5f ? MathF.Atan2(-board.VelocityY, board.VelocityX) : MathF.Atan2(-slope, 1f);
        if (board.GameOver)
        {
            target = MathF.Atan2(-slope, 1f) * 0.5f;
        }

        if (deltaSeconds > 0f)
        {
            tilt.Step(Math.Clamp(target, -1.3f, 1.3f), TiltSmoothSeconds, deltaSeconds);
            feverGlow.Step(board.Fever ? 1f : 0f, FeverSmoothSeconds, deltaSeconds);
            squash *= MathF.Exp(-deltaSeconds * 7f);
        }

        var flapping = playing && !board.Grounded && !board.Holding && !board.GameOver;
        if (flapping)
        {
            flapPhase += deltaSeconds * 20f;
        }

        var radius = BirdDrawScale *
            MathF.Max(SwoopBoard.BirdRadius * camera.PixelsPerMeter, SwoopBoard.BirdRadius * camera.BasePixelsPerMeter * 0.72f);
        var altitude = MathF.Max(0f, board.Altitude);
        var norm = MathF.Sqrt(1f + slope * slope);
        var normal = new Vector2(-slope / norm, -1f / norm);
        var blend = Math.Clamp(altitude / (2f * SwoopBoard.BirdRadius), 0f, 1f);
        var direction = Vector2.Normalize(Vector2.Lerp(normal, new Vector2(0f, -1f), blend));
        pose.Radius = radius;
        pose.Altitude = altitude;
        pose.Tilt = tilt.Value;
        pose.Squash = squash;
        pose.Stretch = board.Grounded ? 0f : Math.Clamp((speed - 18f) / 30f, 0f, 0.2f);
        pose.FlapPhase = flapPhase;
        pose.Fever = feverGlow.Value;
        pose.Wing = board.Holding && playing ? SwoopWing.Tucked : flapping ? SwoopWing.Flapping : SwoopWing.Folded;
        pose.Sleep = board.GameOver ? 1f : board.Night ? 0.6f : 0f;
        pose.Contact = camera.ToScreen(board.X, (float)board.Terrain.Height(board.X));
        pose.Center = camera.ToScreen(board.X, board.Y) + direction * radius * (1f - squash * 0.9f);
        if (!playing)
        {
            pose.Center.Y -= MathF.Abs(MathF.Sin((float)ImGui.GetTime() * 3f)) * radius * 0.25f;
        }

        if (deltaSeconds <= 0f)
        {
            return;
        }

        renderer.RecordTrail(board.X, board.Y + SwoopBoard.BirdRadius, board.Fever && playing);
        if (!board.Fever)
        {
            return;
        }

        emberTimer -= deltaSeconds;
        if (emberTimer > 0f)
        {
            return;
        }

        emberTimer = FeverEmberInterval;
        worldParticles.Burst(SwoopCamera.WorldPixels(board.X, board.Y + SwoopBoard.BirdRadius), 1, FeverColor with { W = 0.7f }, 30f, 3.2f,
            0.45f, 0f, MathF.Tau, 0f, ParticleShape.GlowCircle);
    }

    private void DrawBird(ImDrawListPtr drawList, in SwoopCamera view, in SwoopLighting lighting, float time)
    {
        var offset = view.Anchor - camera.Anchor;
        var shaken = pose;
        shaken.Center += offset;
        shaken.Contact += offset;
        SwoopBirdArt.DrawShadow(drawList, shaken, 1f - lighting.Night * 0.6f);
        renderer.DrawTrail(drawList, view, shaken.Radius);
        SwoopBirdArt.Draw(drawList, shaken, lighting.Tint, time);
    }

    private void DrawWorldEffects(ImDrawListPtr drawList, in SwoopCamera view)
    {
        var firstVertex = drawList.VtxBuffer.Size;
        worldParticles.Draw(drawList, 1f);
        worldFx.DrawRings(drawList, 1f);
        var vertices = drawList.VtxBuffer.AsSpan();
        for (var vertexIndex = firstVertex; vertexIndex < vertices.Length; vertexIndex++)
        {
            ref var vertex = ref vertices[vertexIndex];
            vertex.Pos = view.WorldPixelsToScreen(vertex.Pos);
        }
    }

    private void DrawAltitudeArrow(ImDrawListPtr drawList, Rect area, float scale)
    {
        var ceiling = area.Min.Y + 70f * scale;
        if (pose.Center.Y - pose.Radius > ceiling || finished)
        {
            return;
        }

        var meters = (int)board.Altitude;
        if (meters != altitudeValue)
        {
            altitudeValue = meters;
            altitudeText = Loc.T(L.Swoop.Altitude, GameNumber.Label(meters));
        }

        var x = Math.Clamp(pose.Center.X, area.Min.X + 24f * scale, area.Max.X - 24f * scale);
        SwoopRenderer.DrawAltitudeArrow(drawList, new Vector2(x, ceiling + 10f * scale), altitudeText, Accent, scale);
    }

    private void DrawHud(ImDrawListPtr drawList, Rect area, float rowY, Vector2 restartCenter, float restartRadius, PhoneTheme theme,
        float deltaSeconds, float scale)
    {
        DrawClock(drawList, new Vector2(area.Min.X + 30f * scale, rowY), 19f * scale, scale);
        var beatingBest = bestScore > 0 && board.Score > bestScore;
        GameHud.ScorePill(new Vector2(area.Center.X, rowY), Loc.T(L.Games.Score), ref scoreRoll, board.Score, Accent, theme, deltaSeconds,
            beatingBest);
        if (GameHud.RestartButton(restartCenter, restartRadius, theme))
        {
            StartNewRun();
            return;
        }

        DrawIslandBar(drawList, area, rowY + 36f * scale, scale);
        DrawFeverMeter(drawList, new Vector2(area.Center.X, rowY + 54f * scale), theme, scale);
    }

    private void DrawClock(ImDrawListPtr drawList, Vector2 center, float radius, float scale)
    {
        var corner = new Vector2(radius, radius);
        Material.Frosted(drawList, center - corner, center + corner, radius, scale);
        var arcRadius = radius * 0.74f;
        var thickness = 3.4f * scale;
        drawList.PathClear();
        drawList.PathArcTo(center, arcRadius, 0f, MathF.Tau, 40);
        drawList.PathStroke(ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.16f)), ImDrawFlags.Closed, thickness);
        var fraction = playing ? board.ClockFraction : 1f;
        var lighting = SwoopLighting.At(playing ? board.DayProgress : 0f);
        var low = playing && !board.Night && board.Clock < 10f;
        var color = low ? lighting.Sun with { W = 0.55f + 0.45f * Pulse.Wave(Pulse.Fast) } : lighting.Sun;
        if (fraction > 0.005f)
        {
            var start = -MathF.PI * 0.5f;
            drawList.PathClear();
            drawList.PathArcTo(center, arcRadius, start, start + MathF.Tau * fraction, 40);
            drawList.PathStroke(ImGui.GetColorU32(color), ImDrawFlags.None, thickness);
            var tipAngle = start + MathF.Tau * fraction;
            var tip = center + new Vector2(MathF.Cos(tipAngle), MathF.Sin(tipAngle)) * arcRadius;
            SwoopShapes.Glow(drawList, tip, thickness * 2.4f, lighting.Sun, 1.2f);
            drawList.AddCircleFilled(tip, thickness * 0.8f, ImGui.GetColorU32(new Vector4(1f, 1f, 0.95f, 1f)), 12);
        }

        if (board.Night)
        {
            var moon = ImGui.GetColorU32(new Vector4(0.96f, 0.95f, 0.86f, 1f));
            drawList.AddCircleFilled(center, radius * 0.34f, moon, 20);
            drawList.AddCircleFilled(center + new Vector2(radius * 0.16f, -radius * 0.1f), radius * 0.3f,
                ImGui.GetColorU32(new Vector4(0.14f, 0.15f, 0.30f, 1f)), 20);
            return;
        }

        var seconds = (int)MathF.Ceiling(playing ? board.Clock : SwoopBoard.DaySeconds);
        Typography.DrawCentered(drawList, center, GameNumber.Label(seconds), new Vector4(1f, 1f, 1f, 0.95f), TextStyles.Caption1.Scale,
            FontWeight.Bold);
    }

    private void DrawIslandBar(ImDrawListPtr drawList, Rect area, float y, float scale)
    {
        var width = area.Width * 0.4f;
        var height = 6f * scale;
        var min = new Vector2(area.Center.X - width * 0.5f, y - height * 0.5f);
        var max = new Vector2(area.Center.X + width * 0.5f, y + height * 0.5f);
        drawList.AddRectFilled(min - new Vector2(1f, 1f) * scale, max + new Vector2(1f, 1f) * scale,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.22f)), height);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.22f)), height * 0.5f);
        var fillMax = new Vector2(min.X + width * board.IslandProgress, max.Y);
        if (fillMax.X > min.X + height)
        {
            drawList.AddRectFilled(min, fillMax, ImGui.GetColorU32(Accent), height * 0.5f);
            SwoopShapes.Glow(drawList, new Vector2(fillMax.X, y), height * 1.8f, Accent, 0.9f);
        }

        var labelColor = new Vector4(1f, 1f, 1f, 0.95f);
        Typography.DrawCentered(drawList, new Vector2(min.X - 12f * scale, y), GameNumber.Label(board.CurrentIsland + 1), labelColor,
            TextStyles.Caption1.Scale, FontWeight.Bold);
        var flagBase = new Vector2(max.X + 9f * scale, y + 6f * scale);
        var flagTop = flagBase - new Vector2(0f, 13f * scale);
        drawList.AddLine(flagBase, flagTop, ImGui.GetColorU32(labelColor), MathF.Max(1f, 1.4f * scale));
        drawList.AddTriangleFilled(flagTop, flagTop + new Vector2(9f * scale, 3f * scale), flagTop + new Vector2(0f, 6.5f * scale),
            ImGui.GetColorU32(Accent));
    }

    private void DrawFeverMeter(ImDrawListPtr drawList, Vector2 center, PhoneTheme theme, float scale)
    {
        if (board.Fever)
        {
            var label = Loc.Upper(Loc.T(L.Swoop.Fever));
            var size = Typography.Measure(label, TextStyles.Caption1.Scale, FontWeight.Bold);
            var half = new Vector2(size.X * 0.5f + 12f * scale, 9f * scale);
            var pulse = 0.7f + 0.3f * Pulse.Wave(Pulse.Fast);
            SwoopShapes.Glow(drawList, center, half.X * 1.5f, FeverColor, pulse);
            drawList.AddRectFilled(center - half, center + half, ImGui.GetColorU32(FeverColor with { W = 0.92f }), half.Y);
            Typography.DrawCentered(drawList, center, label, GamePalette.InkOn(FeverColor), TextStyles.Caption1.Scale, FontWeight.Bold);
            return;
        }

        var spacing = 13f * scale;
        var pipRadius = 4.2f * scale;
        for (var pip = 0; pip < SwoopBoard.FeverStreak; pip++)
        {
            var position = new Vector2(center.X + (pip - (SwoopBoard.FeverStreak - 1) * 0.5f) * spacing, center.Y);
            if (pip < board.SmoothStreak)
            {
                SwoopShapes.Glow(drawList, position, pipRadius * 2.6f, FeverColor, 0.9f);
                drawList.AddCircleFilled(position, pipRadius, ImGui.GetColorU32(FeverColor), 14);
                continue;
            }

            drawList.AddCircleFilled(position, pipRadius, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.2f)), 14);
            drawList.AddCircle(position, pipRadius, ImGui.GetColorU32(theme.TextStrong with { W = 0.55f }), 14, MathF.Max(1f, 1.2f * scale));
        }
    }

    private static void DrawReadyHint(ImDrawListPtr drawList, Rect area, float scale)
    {
        var pulse = 1f + 0.05f * Pulse.Wave(Pulse.Calm);
        var title = Loc.T(L.Games.TapToStart);
        var titleCenter = new Vector2(area.Center.X, area.Min.Y + area.Height * 0.42f);
        var shadowOffset = new Vector2(1.5f, 1.5f) * scale;
        Typography.DrawCentered(drawList, titleCenter + shadowOffset, title, TextShadow, TextStyles.Title2.Scale * pulse,
            TextStyles.Title2.Weight);
        Typography.DrawCentered(drawList, titleCenter, title, new Vector4(1f, 1f, 1f, 1f), TextStyles.Title2.Scale * pulse,
            TextStyles.Title2.Weight);
        var hint = Loc.T(L.Swoop.HowTo);
        var hintTop = new Vector2(area.Center.X, titleCenter.Y + 24f * scale);
        var maxWidth = area.Width * 0.78f;
        Typography.DrawWrappedCentered(drawList, hint, TextStyles.Footnote, TextShadow, hintTop + shadowOffset, maxWidth);
        Typography.DrawWrappedCentered(drawList, hint, TextStyles.Footnote, new Vector4(1f, 1f, 1f, 0.95f), hintTop, maxWidth);
    }

    private void DrawResult(Rect area, PhoneTheme theme, float deltaSeconds)
    {
        resultAppear = MathF.Min(1f, resultAppear + deltaSeconds * 3.4f);
        var result = new GameResult(Loc.T(L.Swoop.ResultTitle), NightTitle, Loc.T(L.Games.Score), GameNumber.Label(board.Score), resultLine,
            newBest);
        if (GameOverlay.Draw(area, theme, Accent, resultAppear, result))
        {
            StartNewRun();
        }
    }
}
