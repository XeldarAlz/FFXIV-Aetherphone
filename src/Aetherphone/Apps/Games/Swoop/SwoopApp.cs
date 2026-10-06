using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Swoop;

internal sealed class SwoopApp : IMiniGame
{
    private const string GameId = "swoop";
    private const float ViewWidthMeters = 34f;
    private const float MaxZoomOut = 2.8f;
    private const float AnchorXFraction = 0.4f;
    private const float AnchorYFraction = 0.72f;
    private const float LeadPerSpeed = 0.3f;
    private const float MinLead = 1f;
    private const float MaxLead = 8f;
    private const float LookBehind = 8f;
    private const float LookAhead = 40f;
    private const float SkyMargin = 7f;
    private const float GroundMargin = 3f;
    private const float FrameFill = 0.9f;
    private const float LeadSmoothSeconds = 0.6f;
    private const float FloorSmoothSeconds = 0.45f;
    private const float ZoomSmoothSeconds = 0.55f;
    private const float BannerSeconds = 1.7f;
    private const float TiltSmoothSeconds = 0.08f;
    private const float FeverSmoothSeconds = 0.25f;
    private const float FeverEmberRate = 28f;
    private const float AirCalloutSeconds = 1f;
    private const float BirdDrawScale = 1.3f;
    private const float UrgentClockSeconds = 10f;
    private const float CapsulePadX = 10f;
    private const float IconSize = 11f;
    private const float IconGap = 5f;
    private const float IslandBarWidth = 44f;
    private const float IslandBarHeight = 4f;
    private const float PipSpacing = 0.55f;
    private const ulong IdleSeed = 0x53574F4FUL;
    private static readonly GameSpec StageSpec = new(GameId, L.Swoop.Title, GameGenre.Action, L.Swoop.Hook,
        Backdrop.Sky, HudStyle.Standard, ScoreKind.Score, clocked: true, countdown: true, keyboard: true);
    private static readonly TextStyle CapsuleStyle = TextStyles.FootnoteEmphasized;
    private static readonly string[] PickupLabels =
    {
        string.Concat("+", GameNumber.Label(SwoopBoard.CrystalPoints)),
        string.Concat("+", GameNumber.Label(SwoopBoard.CrystalPoints * 2)),
    };

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 DustColor = new(0.86f, 0.74f, 0.56f, 0.9f);
    private static readonly Vector4 SparkleColor = new(1f, 0.93f, 0.58f, 1f);
    private static readonly Vector4 CrystalSparkle = new(0.62f, 0.97f, 1f, 1f);
    private static readonly Vector4 FeverColor = new(0.80f, 1f, 0.40f, 1f);
    private static readonly Vector4 ThudColor = new(1f, 0.62f, 0.52f, 1f);
    private static readonly Vector4 Sunlight = new(1f, 0.9f, 0.5f, 1f);
    private static readonly Vector4[] IslandConfetti =
    {
        new(1f, 0.84f, 0.32f, 1f), new(0.80f, 1f, 0.40f, 1f), new(0.62f, 0.97f, 1f, 1f),
        new(1f, 0.62f, 0.70f, 1f), new(1f, 1f, 1f, 1f),
    };

    private static readonly ParticleSpec LaunchStreaks = new(White with { W = 0.7f }, White with { W = 0.7f }, 0.16f, 16f,
        0.35f, 22f, 1.6f, 12f, 0.8f, MathF.PI, ParticleShape.Streak);
    private static readonly ParticleSpec SmoothSparkle = new(SparkleColor, SparkleColor, 0.26f, 14f, 0.7f, 4f, 2.4f, 6f,
        shape: ParticleShape.Star);
    private static readonly ParticleSpec SmoothRing = new(SparkleColor with { W = 0.8f }, SparkleColor with { W = 0f },
        1.1f, 0f, 0.45f, shape: ParticleShape.Ring, curve: SizeCurve.Grow);
    private static readonly ParticleSpec ThudDust = new(DustColor, DustColor, 0.42f, 15f, 0.75f, 26f, 1.6f, 12f, MathF.PI,
        -MathF.PI * 0.5f);
    private static readonly ParticleSpec ThudPuff = new(DustColor with { W = 0.55f }, DustColor with { W = 0f }, 0.65f, 9f,
        1f, 4f, 1.6f, 12f, MathF.PI * 0.6f, -MathF.PI * 0.5f, curve: SizeCurve.Grow);
    private static readonly ParticleSpec ThudRing = new(DustColor, DustColor with { W = 0f }, 1.4f, 0f, 0.4f,
        shape: ParticleShape.Ring, curve: SizeCurve.Grow);
    private static readonly ParticleSpec FeverSparkle = new(FeverColor, FeverColor, 0.3f, 22f, 0.9f, 4f, 2.4f, 6f,
        shape: ParticleShape.Star, additive: true);
    private static readonly ParticleSpec FeverRing = new(FeverColor, FeverColor with { W = 0f }, 2.8f, 0f, 0.6f,
        shape: ParticleShape.Ring, curve: SizeCurve.Grow);
    private static readonly ParticleSpec PickupSparkle = new(CrystalSparkle, CrystalSparkle, 0.22f, 11f, 0.5f, 4f, 2.4f, 6f,
        shape: ParticleShape.Star);
    private static readonly ParticleSpec Ember = new(FeverColor with { W = 0.7f }, FeverColor with { W = 0f }, 0.32f, 3f,
        0.45f, shape: ParticleShape.GlowCircle);
    private static readonly ParticleSpec[] ConfettiSpecs = BuildConfetti();

    private readonly SwoopBoard board = new();
    private readonly SwoopBoard idleBoard = new();
    private readonly SwoopRenderer renderer = new();
    private readonly ParticleSystem particles = new(384);
    private readonly FeedbackFx fx = new();
    private readonly Ribbon ribbon = new();
    private readonly Dictionary<int, string> airLabels = new();
    private Camera2D camera = Camera2D.Create();
    private Emitter embers = new(Ember, FeverEmberRate);
    private SwoopBirdPose pose;
    private Spring lead = new(MinLead);
    private Spring floor = new(0f);
    private Spring zoom = new(1f);
    private Spring tilt = new(0f);
    private Spring feverGlow = new(0f);
    private LabelSlot islandLabel;
    private LabelSlot bonusLabel;
    private LabelSlot altitudeLabel;
    private LabelSlot distanceLabel;
    private LanguageInfo? airLanguage;
    private float basePixelsPerMeter = 1f;
    private float squash;
    private float flapPhase;
    private float bannerProgress = 1f;
    private float time;
    private float feverSeconds;
    private int crystalsCollected;
    private string bannerText = string.Empty;
    private bool finished;
    private bool mouseHeld;
    private bool cameraReady;
    private bool idleReady;

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        board.Start(SeedOf(start.Seed));
        particles.Clear();
        particles.Reseed(start.Seed);
        fx.Clear();
        ribbon.Clear();
        embers.Reset();
        tilt.SnapTo(0f);
        feverGlow.SnapTo(0f);
        squash = 0f;
        flapPhase = 0f;
        feverSeconds = 0f;
        crystalsCollected = 0;
        finished = false;
        mouseHeld = false;
        cameraReady = false;
        bannerProgress = 1f;
        bannerText = string.Empty;
    }

    public void Close()
    {
        particles.Clear();
        fx.Clear();
        ribbon.Clear();
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        if (!idleReady || idleBoard.Night)
        {
            idleBoard.Start(SeedOf(IdleSeed));
            idleReady = true;
            cameraReady = false;
        }

        var raw = context.RawDeltaSeconds;
        time += raw;
        idleBoard.Tick(raw, false);
        PlaceCamera(context, idleBoard, raw);
        UpdatePose(idleBoard, raw, false);
        var lighting = SwoopLighting.At(idleBoard.DayProgress);
        context.Backdrop.SetSky(SkyProgress(idleBoard.DayProgress));
        DrawWorld(ImGui.GetWindowDrawList(), context, idleBoard, lighting, false);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var raw = context.RawDeltaSeconds;
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        time += raw;
        var holding = ReadInput(context);
        board.Tick(simDelta, holding);
        if (board.Fever && simDelta > 0f)
        {
            feverSeconds += simDelta;
        }

        PlaceCamera(context, board, context.DeltaSeconds);
        ReactToEvents(context, scale);
        UpdatePose(board, context.DeltaSeconds, true);
        if (simDelta > 0f && !board.GameOver)
        {
            ribbon.Push(World(board.X, board.Y + SwoopBoard.BirdRadius));
            if (board.Fever)
            {
                embers.Advance(simDelta, World(board.X, board.Y + SwoopBoard.BirdRadius), particles);
            }
        }

        particles.Update(raw);
        fx.Update(raw);
        bannerProgress = GameBanner.Advance(bannerProgress, raw, BannerSeconds);
        if (board.GameOver && !finished)
        {
            finished = true;
            Finish(context);
        }

        var lighting = SwoopLighting.At(board.DayProgress);
        context.Backdrop.SetSky(SkyProgress(board.DayProgress));
        DrawWorld(drawList, context, board, lighting, true);
        GameBanner.Draw(drawList, new Vector2(context.Full.Center.X, context.Full.Min.Y + context.Full.Height * 0.3f),
            bannerText, Accent, context.Theme, bannerProgress);
        DrawIslandCapsule(drawList, context, scale);
        context.Hud.Score(board.Score);
        context.Hud.Timer(board.Clock, SwoopBoard.DaySeconds, !board.Night && board.Clock < UrgentClockSeconds);
        context.Hud.Custom(IslandCapsuleWidth(scale));
        context.Hud.Best(context.Session.Best);
        context.Session.Report(board.Score);
    }

    private static uint SeedOf(ulong seed) => unchecked((uint)(seed ^ (seed >> 32)));

    private static float SkyProgress(float dayProgress) => 0.25f + 0.75f * dayProgress * dayProgress * dayProgress;

    private static Vector2 World(double x, float y) => new((float)x, -y);

    private Vector2 ToScreen(double x, float y) => camera.ToScreen(World(x, y));

    private void PlaceCamera(in GameContext context, SwoopBoard target, float deltaSeconds)
    {
        var full = context.Full;
        basePixelsPerMeter = full.Width / ViewWidthMeters;
        Targets(target, full, out var leadTarget, out var floorTarget, out var zoomTarget);
        if (!cameraReady)
        {
            lead.SnapTo(leadTarget);
            floor.SnapTo(floorTarget);
            zoom.SnapTo(zoomTarget);
            cameraReady = true;
        }
        else if (deltaSeconds > 0f)
        {
            lead.Step(leadTarget, LeadSmoothSeconds, deltaSeconds);
            floor.Step(floorTarget, FloorSmoothSeconds, deltaSeconds);
            zoom.Step(zoomTarget, ZoomSmoothSeconds, deltaSeconds);
        }

        camera.Fit(full, ViewWidthMeters * MathF.Max(1f, zoom.Value), 1f, FitMode.CoverWidth);
        camera.Anchor = new Vector2(full.Min.X + full.Width * AnchorXFraction, full.Min.Y + full.Height * AnchorYFraction);
        camera.Place(World(target.X + lead.Value, floor.Value));
        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, UiScale.Current);
        context.Backdrop.SetCamera(in camera);
    }

    private static void Targets(SwoopBoard target, Rect area, out float leadTarget, out float floorTarget,
        out float zoomTarget)
    {
        leadTarget = Math.Clamp(target.Speed * LeadPerSpeed, MinLead, MaxLead);
        var firstSample = Math.Max(target.FirstSample, (int)Math.Floor((target.X - LookBehind) / SwoopBoard.SampleSpacing));
        var endSample = Math.Min(target.EndSample, (int)Math.Ceiling((target.X + LookAhead) / SwoopBoard.SampleSpacing));
        var lowest = target.Y;
        var highest = target.Y;
        for (var sample = firstSample; sample < endSample; sample++)
        {
            var height = target.SampleHeight(sample);
            lowest = MathF.Min(lowest, height);
            highest = MathF.Max(highest, height);
        }

        var top = MathF.Max(highest + SkyMargin, target.Y + SwoopBoard.BirdRadius * 2f + SkyMargin * 0.6f);
        var bottom = lowest - GroundMargin;
        var baseHeight = ViewWidthMeters * area.Height / MathF.Max(1f, area.Width);
        var needed = (top - bottom) / (AnchorYFraction * FrameFill);
        zoomTarget = Math.Clamp(needed / MathF.Max(1f, baseHeight), 1f, MaxZoomOut);
        floorTarget = bottom;
    }

    private bool ReadInput(in GameContext context)
    {
        if (context.Session.State != StageFlow.Playing || finished)
        {
            mouseHeld = false;
            return false;
        }

        var full = context.Full;
        var hitMin = new Vector2(full.Min.X, full.Min.Y + StageLayout.ChromeBand * UiScale.Current);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && UiInteract.Hover(hitMin, full.Max))
        {
            mouseHeld = true;
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            mouseHeld = false;
        }

        var keys = GameInput.Held(ImGuiKey.Space) || GameInput.Held(ImGuiKey.W, ImGuiKey.UpArrow);
        return mouseHeld || keys;
    }

    private void ReactToEvents(in GameContext context, float scale)
    {
        var birdWorld = World(board.X, board.Y + SwoopBoard.BirdRadius * 0.4f);
        var birdScreen = ToScreen(board.X, board.Y + SwoopBoard.BirdRadius);
        var calloutY = 40f * scale;
        if (board.LaunchedThisTick)
        {
            UiFeedback.Play(UiSound.GameJump);
            particles.Emit(LaunchStreaks, birdWorld, 6);
        }

        if (board.SmoothThisTick)
        {
            UiFeedback.Play(UiSound.GamePop);
            squash = 0.22f;
            particles.Emit(SmoothSparkle, birdWorld, 14);
            particles.Emit(SmoothRing, birdWorld, 1);
            fx.AddText(Loc.T(L.Swoop.Smooth), birdScreen - new Vector2(0f, calloutY), SparkleColor, 1.15f);
            context.Fx.Punch(0.03f);
        }

        if (board.ThudThisTick)
        {
            UiFeedback.Play(UiSound.GameHitSoft);
            squash = 0.42f;
            var impact = Math.Clamp(board.LastImpactSpeed / 25f, 0f, 1f);
            particles.Emit(ThudDust, birdWorld, 18);
            particles.Emit(ThudPuff, birdWorld, 8);
            particles.Emit(ThudRing, birdWorld, 1);
            camera.Shake(0.3f + 0.3f * impact);
            fx.AddText(Loc.T(L.Swoop.Thud), birdScreen - new Vector2(0f, calloutY), ThudColor, 1.05f);
        }

        if (board.AirCountedThisTick && board.LastAirSeconds >= AirCalloutSeconds)
        {
            fx.AddText(AirLabel(board.LastAirSeconds), birdScreen - new Vector2(0f, calloutY * 1.8f), White, 0.95f);
        }

        if (board.FeverStartedThisTick)
        {
            UiFeedback.Play(UiSound.GamePowerUp);
            context.Fx.Flash(FeverColor, 0.22f);
            context.Fx.Punch(0.06f);
            particles.Emit(FeverSparkle, birdWorld, 24);
            particles.Emit(FeverRing, birdWorld, 1);
            fx.AddText(Loc.T(L.Swoop.FeverStart), birdScreen - new Vector2(0f, calloutY * 2.6f), FeverColor, 1.3f);
        }

        if (board.PickupsThisTick > 0)
        {
            UiFeedback.Play(UiSound.GameCollect);
            crystalsCollected += board.PickupsThisTick;
            var label = PickupLabels[board.Fever ? 1 : 0];
            for (var pickup = 0; pickup < board.PickupsThisTick; pickup++)
            {
                particles.Emit(PickupSparkle, World(board.PickupX(pickup), board.PickupY(pickup)), 7);
                fx.AddText(label, ToScreen(board.PickupX(pickup), board.PickupY(pickup)), CrystalSparkle, 0.8f, 60f);
            }
        }

        if (board.IslandReachedThisTick)
        {
            UiFeedback.Play(UiSound.GameClear);
            ShowBanner(islandLabel.Get(L.Swoop.Island, board.CurrentIsland + 1));
            EmitConfetti(World(board.Terrain.IslandStart(board.CurrentIsland), 9f));
            context.Fx.Flash(Sunlight, 0.15f);
            context.Fx.Sweep();
            if (board.LastBonusSeconds >= 0.5f)
            {
                fx.AddText(bonusLabel.Get(L.Swoop.TimeBonus, (int)MathF.Round(board.LastBonusSeconds)),
                    new Vector2(birdScreen.X, birdScreen.Y - calloutY * 3.2f), Sunlight, 1.2f);
            }
        }

        if (board.NightFellThisTick)
        {
            UiFeedback.Play(UiSound.GameTick);
            ShowBanner(Loc.T(L.Swoop.Nightfall));
        }

        if (board.EndedThisTick)
        {
            context.Fx.SlowMo(0.6f, 0.4f);
        }
    }

    private void ShowBanner(string text)
    {
        bannerText = text;
        bannerProgress = 0f;
    }

    private string AirLabel(float seconds)
    {
        if (!ReferenceEquals(airLanguage, Loc.Current))
        {
            airLabels.Clear();
            airLanguage = Loc.Current;
        }

        var tenths = (int)MathF.Round(seconds * 10f);
        if (airLabels.TryGetValue(tenths, out var cached))
        {
            return cached;
        }

        var label = Loc.T(L.Swoop.AirTime, (tenths / 10f).ToString("0.0", Loc.Culture));
        airLabels[tenths] = label;
        return label;
    }

    private void EmitConfetti(Vector2 origin)
    {
        for (var index = 0; index < ConfettiSpecs.Length; index++)
        {
            particles.Emit(in ConfettiSpecs[index], origin, 10);
        }
    }

    private void UpdatePose(SwoopBoard target, float deltaSeconds, bool playing)
    {
        var slope = target.Grounded ? target.GroundSlope : (float)target.Terrain.Slope(target.X);
        var speed = MathF.Sqrt(target.VelocityX * target.VelocityX + target.VelocityY * target.VelocityY);
        var targetTilt = speed > 0.5f ? MathF.Atan2(-target.VelocityY, target.VelocityX) : MathF.Atan2(-slope, 1f);
        if (target.GameOver)
        {
            targetTilt = MathF.Atan2(-slope, 1f) * 0.5f;
        }

        if (deltaSeconds > 0f)
        {
            tilt.Step(Math.Clamp(targetTilt, -1.3f, 1.3f), TiltSmoothSeconds, deltaSeconds);
            feverGlow.Step(target.Fever ? 1f : 0f, FeverSmoothSeconds, deltaSeconds);
            squash *= MathF.Exp(-deltaSeconds * 7f);
        }

        var flapping = playing && !target.Grounded && !target.Holding && !target.GameOver;
        if (flapping)
        {
            flapPhase += deltaSeconds * 20f;
        }

        var pixels = camera.Px(1f);
        var radius = BirdDrawScale * MathF.Max(SwoopBoard.BirdRadius * pixels, SwoopBoard.BirdRadius * basePixelsPerMeter * 0.72f);
        var altitude = MathF.Max(0f, target.Altitude);
        var norm = MathF.Sqrt(1f + slope * slope);
        var normal = new Vector2(-slope / norm, -1f / norm);
        var blend = Math.Clamp(altitude / (2f * SwoopBoard.BirdRadius), 0f, 1f);
        var direction = Vector2.Normalize(Vector2.Lerp(normal, new Vector2(0f, -1f), blend));
        pose.Radius = radius;
        pose.Altitude = altitude;
        pose.Tilt = tilt.Value;
        pose.Squash = squash;
        pose.Stretch = target.Grounded ? 0f : Math.Clamp((speed - 18f) / 30f, 0f, 0.2f);
        pose.FlapPhase = flapPhase;
        pose.Fever = feverGlow.Value;
        pose.Wing = target.Holding && playing ? SwoopWing.Tucked : flapping ? SwoopWing.Flapping : SwoopWing.Folded;
        pose.Sleep = target.GameOver ? 1f : target.Night ? 0.6f : 0f;
        pose.Contact = ToScreen(target.X, (float)target.Terrain.Height(target.X));
        pose.Center = ToScreen(target.X, target.Y) + direction * radius * (1f - squash * 0.9f);
    }

    private void Finish(in GameContext context)
    {
        context.Session.Finish(new GameOutcome(board.Score, ScoreKind.Score, GameId)
            .WithStat(L.Swoop.Distance, distanceLabel.Get(L.Swoop.Altitude, board.Distance))
            .WithStat(L.Swoop.Islands, GameNumber.Label(board.IslandsReached))
            .WithStat(L.Swoop.Crystals, GameNumber.Label(crystalsCollected))
            .WithStat(L.Swoop.FeverTime, TimeText.MinutesSeconds((int)feverSeconds)));
    }

    private void DrawWorld(ImDrawListPtr drawList, in GameContext context, SwoopBoard target, in SwoopLighting lighting,
        bool live)
    {
        var scale = UiScale.Current;
        var full = context.Full;
        drawList.PushClipRect(full.Min, full.Max, true);
        SwoopSky.DrawHills(drawList, full, camera.Origin, basePixelsPerMeter, MathF.Max(1f, zoom.Value), lighting);
        renderer.PrepareColors(lighting);
        renderer.DrawTerrain(drawList, target, in camera, full, scale);
        renderer.DrawMarkers(drawList, target, in camera, full, lighting, zoom.Value, time, scale);
        renderer.DrawCrystals(drawList, target, in camera, full, lighting, time, scale);
        SwoopRenderer.DrawDarkness(drawList, full, target, lighting, time, scale);
        DrawBird(drawList, target, lighting, live);
        if (live)
        {
            particles.Draw(drawList, in camera);
            fx.DrawRings(drawList, scale);
            if (!finished)
            {
                SwoopRenderer.DrawSpeedLines(drawList, full, target.Speed, time, scale);
            }

            DrawAltitudeArrow(drawList, context, scale);
        }

        drawList.PopClipRect();
        if (live)
        {
            fx.DrawText();
        }
    }

    private void DrawBird(ImDrawListPtr drawList, SwoopBoard target, in SwoopLighting lighting, bool live)
    {
        SwoopBirdArt.DrawShadow(drawList, pose, 1f - lighting.Night * 0.6f);
        if (live && feverGlow.Value > 0.01f)
        {
            ribbon.Draw(drawList, in camera, SwoopRenderer.TrailCore with { W = 0.85f * feverGlow.Value },
                camera.Px(SwoopBoard.BirdRadius * 0.7f), additive: true);
        }

        SwoopBirdArt.Draw(drawList, pose, lighting.Tint, time);
        if (!live || target.Fever || target.SmoothStreak <= 0 || target.GameOver)
        {
            return;
        }

        var spacing = pose.Radius * PipSpacing;
        var pipRadius = pose.Radius * 0.14f;
        var center = pose.Center - new Vector2(0f, pose.Radius * 1.7f);
        for (var pip = 0; pip < SwoopBoard.FeverStreak; pip++)
        {
            var position = new Vector2(center.X + (pip - (SwoopBoard.FeverStreak - 1) * 0.5f) * spacing, center.Y);
            if (pip < target.SmoothStreak)
            {
                SwoopShapes.Glow(drawList, position, pipRadius * 2.6f, FeverColor, 0.9f);
                drawList.AddCircleFilled(position, pipRadius, ImGui.GetColorU32(FeverColor), 12);
                continue;
            }

            drawList.AddCircleFilled(position, pipRadius, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.2f)), 12);
            drawList.AddCircle(position, pipRadius, ImGui.GetColorU32(White with { W = 0.55f }), 12, 1f);
        }
    }

    private void DrawAltitudeArrow(ImDrawListPtr drawList, in GameContext context, float scale)
    {
        var ceiling = context.Safe.Min.Y;
        if (pose.Center.Y - pose.Radius > ceiling || finished)
        {
            return;
        }

        var full = context.Full;
        var x = Math.Clamp(pose.Center.X, full.Min.X + 24f * scale, full.Max.X - 24f * scale);
        SwoopRenderer.DrawAltitudeArrow(drawList, new Vector2(x, ceiling + 10f * scale),
            altitudeLabel.Get(L.Swoop.Altitude, (int)board.Altitude), Accent, time, scale);
    }

    private float IslandCapsuleWidth(float scale)
    {
        var text = Typography.Measure(GameNumber.Label(board.CurrentIsland + 1), CapsuleStyle).X / scale;
        return CapsulePadX * 2f + IconSize + IconGap + text + IconGap + IslandBarWidth;
    }

    private void DrawIslandCapsule(ImDrawListPtr drawList, in GameContext context, float scale)
    {
        var rect = context.Hud.CustomRect(0);
        if (rect.Width <= 0f)
        {
            return;
        }

        StageHud.Capsule(drawList, rect, scale);
        var accent = Accent;
        var iconSize = IconSize * scale;
        var left = rect.Min.X + CapsulePadX * scale;
        ProgressRing.CenterIcon(drawList, new Vector2(left + iconSize * 0.5f, rect.Center.Y), FontAwesomeIcon.Flag, accent,
            iconSize);
        left += iconSize + IconGap * scale;
        var label = GameNumber.Label(board.CurrentIsland + 1);
        Typography.Draw(drawList, new Vector2(left, rect.Center.Y - Typography.LineHeight(CapsuleStyle) * 0.5f), label,
            context.Theme.TextStrong, CapsuleStyle);
        left += Typography.Measure(label, CapsuleStyle).X + IconGap * scale;
        var barHeight = IslandBarHeight * scale;
        var barMin = new Vector2(left, rect.Center.Y - barHeight * 0.5f);
        var barMax = new Vector2(left + IslandBarWidth * scale, rect.Center.Y + barHeight * 0.5f);
        drawList.AddRectFilled(barMin, barMax, ImGui.GetColorU32(accent with { W = 0.18f }), barHeight * 0.5f);
        var fillRight = barMin.X + (barMax.X - barMin.X) * board.IslandProgress;
        if (fillRight > barMin.X + barHeight)
        {
            drawList.AddRectFilled(barMin, new Vector2(fillRight, barMax.Y), ImGui.GetColorU32(accent), barHeight * 0.5f);
        }
    }

    private static ParticleSpec[] BuildConfetti()
    {
        var specs = new ParticleSpec[IslandConfetti.Length];
        for (var index = 0; index < specs.Length; index++)
        {
            specs[index] = new ParticleSpec(IslandConfetti[index], IslandConfetti[index], 0.36f, 26f, 1.3f, 54f, 0.7f,
                16f, 1.4f, -MathF.PI * 0.5f, ParticleShape.Square);
        }

        return specs;
    }
}
