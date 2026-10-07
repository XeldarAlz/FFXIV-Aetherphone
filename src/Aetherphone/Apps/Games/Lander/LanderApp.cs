using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.World;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Games.Lander;

internal sealed class LanderApp : IMiniGame
{
    private const string GameId = "lander";
    private const ulong IdleSeed = 0x4C414E444552UL;
    private const int MaskCells = 480;
    private const float MetresPerCell = LanderTerrain.Width / MaskCells;
    private const float NearWidth = 13f;
    private const float WidthPerAltitude = 1.5f;
    private const float WidthBase = 9f;
    private const float ZoomSmoothSeconds = 0.5f;
    private const float FollowSmoothSeconds = 0.22f;
    private const float LanderAbove = 0.15f;
    private const float GroundBelow = 0.35f;
    private const float TelemetryGap = 16f;
    private const float TelemetryHeight = 26f;
    private const float TelemetrySpacing = 8f;
    private const float CapsulePadX = 10f;
    private const float IconSize = 11f;
    private const float IconGap = 5f;
    private const float FuelBarWidth = 58f;
    private const float FuelBarHeight = 5f;
    private const float CraterRadius = 1.3f;
    private const float LightSeconds = 1.8f;
    private const float LightRadius = 9f;
    private const float BannerSeconds = 1.6f;
    private const float TallySeconds = 1.3f;
    private const float TallySteps = 40f;
    private const float FlameRise = 9f;
    private const float FlameFall = 12f;
    private const float DustAltitude = 6f;
    private const float GroundLightAltitude = 8f;
    private const float SafeGlowAltitude = 5f;
    private const float ArrowInset = 18f;
    private static readonly GameSpec StageSpec = new(GameId, L.Lander.Title, GameGenre.Arcade, L.Lander.Hook,
        Backdrop.Nebula, HudStyle.Standard, ScoreKind.Score, clocked: true, countdown: true, keyboard: true);
    private static readonly Vector4 Warn = new(1f, 0.74f, 0.30f, 1f);
    private static readonly Vector4 Safe = new(0.45f, 0.92f, 0.58f, 1f);
    private static readonly Vector4 SmokeGrey = new(0.62f, 0.62f, 0.68f, 0.42f);
    private static readonly Vector4 DustGrey = new(0.78f, 0.77f, 0.82f, 0.6f);
    private static readonly ParticleSpec FlameSpec = new(LanderRenderer.FlameCore,
        LanderRenderer.Flame with { W = 0f }, 0.18f, 7f, 0.18f, 0f, 2f, shape: ParticleShape.GlowCircle, additive: true);
    private static readonly ParticleSpec SmokeSpec = new(SmokeGrey, SmokeGrey with { W = 0f }, 0.35f, 3.5f, 1.4f,
        -0.4f, 1.2f, curve: SizeCurve.Grow);
    private static readonly ParticleSpec DustSpec = new(DustGrey, DustGrey with { W = 0f }, 0.22f, 5f, 0.8f, 1.6f,
        1.4f, curve: SizeCurve.Grow);
    private static readonly ParticleSpec RcsSpec = new(LanderRenderer.White with { W = 0.7f },
        LanderRenderer.White with { W = 0f }, 0.1f, 3f, 0.25f, curve: SizeCurve.Grow);
    private static readonly ParticleSpec HullDebris = new(LanderRenderer.Hull, LanderRenderer.Hull with { W = 0f },
        0.22f, 9f, 1.3f, 3.2f, 0.4f, 9f, shape: ParticleShape.Shard);
    private static readonly ParticleSpec FoilDebris = new(LanderRenderer.Gold, LanderRenderer.Gold with { W = 0f },
        0.2f, 8f, 1.3f, 3.2f, 0.4f, 9f, shape: ParticleShape.Shard);
    private static readonly ParticleSpec Fireball = new(LanderRenderer.FlameCore, LanderRenderer.Flame with { W = 0f },
        0.6f, 4f, 0.6f, 0f, 1.6f, shape: ParticleShape.GlowCircle, additive: true);
    private static readonly ParticleSpec CrashSparks = new(LanderRenderer.FlameCore, LanderRenderer.Flame with { W = 0f },
        0.08f, 12f, 0.6f, 3f, 1f, shape: ParticleShape.Spark, additive: true);
    private static readonly ParticleSpec CrashSmoke = new(SmokeGrey, SmokeGrey with { W = 0f }, 0.8f, 2.5f, 2.2f,
        -0.3f, 1.2f, curve: SizeCurve.Grow);
    private static readonly ParticleSpec LandingDust = new(DustGrey, DustGrey with { W = 0f }, 0.3f, 4f, 1f, 0.8f, 2.2f,
        curve: SizeCurve.Grow);
    private static readonly ParticleSpec PerfectSparkle = new(LanderRenderer.Gold, LanderRenderer.White with { W = 0f },
        0.18f, 5f, 0.9f, 0f, 2f, 6f, shape: ParticleShape.Star, additive: true);

    private readonly LanderBoard board = new();
    private readonly TerrainMask mask = new(MaskCells, MaskCells, MetresPerCell);
    private readonly TerrainTexture texture;
    private readonly LanderPad pad = new();
    private readonly ParticleSystem particles = new(640);
    private readonly ParticleSystem smoke = new(320);
    private readonly FeedbackFx fx = new();
    private readonly Ribbon ribbon = new();
    private readonly LabelSlot[] padSlots = new LabelSlot[LanderTerrain.MaxPads];
    private readonly string[] padLabels = new string[LanderTerrain.MaxPads];
    private Camera2D camera = Camera2D.Create();
    private Spring viewWidth = new(LanderTerrain.Width);
    private LanderPilot pilot = LanderPilot.Create();
    private GameRandom effects = GameRandom.FromSeed(IdleSeed);
    private Emitter flameEmitter = new(FlameSpec, 90f);
    private Emitter smokeEmitter = new(SmokeSpec, 26f);
    private Emitter dustEmitter = new(DustSpec, 50f);
    private Emitter rcsEmitter = new(RcsSpec, 40f);
    private LabelSlot levelLabel;
    private LabelSlot landedLabel;
    private LabelSlot degreesLabel;
    private LanderPadInput padInput;
    private string bannerText = string.Empty;
    private Vector4 bannerColor;
    private Vector2 lightPosition;
    private Vector2 tallyWorld;
    private float bannerProgress = 1f;
    private float lightLeft;
    private float craterLeft;
    private float craterRight;
    private float tallyTime = TallySeconds;
    private float flamePower;
    private float flicker;
    private float time;
    private int tallyTarget;
    private int stampedVersion = -1;
    private bool craterActive;
    private bool snapCamera = true;
    private bool finished;

    public LanderApp(ITextureProvider textures)
    {
        texture = new TerrainTexture(textures, mask, TerrainMaterial.Lunar);
        BuildIdle();
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        board.Reset(start.Random);
        ResetEffects();
        particles.Reseed(start.Seed);
        smoke.Reseed(start.Seed ^ IdleSeed);
        effects = GameRandom.FromSeed(start.Seed ^ 0x5A5AUL);
        finished = false;
    }

    public void Close()
    {
        BuildIdle();
    }

    public void Dispose()
    {
        texture.Dispose();
    }

    public void OnQuit(GameSession session)
    {
        if (finished || board.Score <= 0)
        {
            return;
        }

        finished = true;
        session.Finish(Outcome());
    }

    public void DrawIdle(in GameContext context)
    {
        var scale = UiScale.Current;
        var deltaSeconds = context.RawDeltaSeconds;
        Animate(deltaSeconds, deltaSeconds);
        PlaceCamera(context, scale);
        pilot.Decide(board, out var rotate, out var thrust);
        board.Step(deltaSeconds, rotate, thrust);
        React(context, false, deltaSeconds);
        if (board.State == LanderState.Over)
        {
            BuildIdle();
        }

        if (snapCamera)
        {
            PlaceCamera(context, scale);
        }

        DrawWorld(ImGui.GetWindowDrawList(), context, scale);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        Animate(context.RawDeltaSeconds, simDelta);
        bannerProgress = GameBanner.Advance(bannerProgress, context.RawDeltaSeconds, BannerSeconds);
        PlaceCamera(context, scale);
        if (!finished)
        {
            ReadInput(context, out var rotate, out var thrust);
            board.Step(simDelta, rotate, thrust);
            React(context, true, simDelta);
            if (board.State == LanderState.Over)
            {
                finished = true;
                context.Session.Finish(Outcome());
            }
        }

        if (snapCamera)
        {
            PlaceCamera(context, scale);
        }

        DrawWorld(drawList, context, scale);
        DrawTelemetry(drawList, context, scale);
        DrawTally(drawList, scale);
        if (bannerText.Length > 0)
        {
            var focus = Focus(context, scale);
            GameBanner.Draw(drawList, new Vector2(focus.Center.X, focus.Min.Y + focus.Height * 0.22f), bannerText,
                bannerColor, context.Theme, bannerProgress);
        }

        var band = StageLayout.PadBand(context.Full, StageLayout.ShooterBand, scale);
        padInput = pad.Draw(drawList, band, Accent, context.Theme,
            context.Session.State == StageFlow.Playing && !finished, Loc.T(L.Lander.Thrust));
        context.Hud.Score(board.Score);
        context.Hud.Lives(board.Lives, LanderBoard.StartLives);
        context.Hud.Level(board.Level);
        context.Hud.Best(context.Session.Best);
        context.Hud.Custom(CapsulePadX * 2f + IconSize + IconGap + FuelBarWidth);
        if (context.Hud.CustomPlaced(0))
        {
            DrawFuel(drawList, context.Hud.CustomRect(0), scale);
        }

        context.Session.Report(board.Score);
    }

    private void BuildIdle()
    {
        board.Reset(GameRandom.FromSeed(IdleSeed));
        pilot = LanderPilot.Create();
        ResetEffects();
    }

    private void ResetEffects()
    {
        particles.Clear();
        smoke.Clear();
        fx.Clear();
        ribbon.Clear();
        camera = Camera2D.Create();
        flameEmitter.Reset();
        smokeEmitter.Reset();
        dustEmitter.Reset();
        rcsEmitter.Reset();
        padInput = default;
        bannerText = string.Empty;
        bannerProgress = 1f;
        tallyTime = TallySeconds;
        lightLeft = 0f;
        flamePower = 0f;
        snapCamera = true;
        if (craterActive)
        {
            board.Terrain.Restore(mask, craterLeft, craterRight);
            craterActive = false;
        }
    }

    private void Animate(float deltaSeconds, float simDelta)
    {
        time += deltaSeconds;
        particles.Update(deltaSeconds);
        smoke.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        lightLeft = MathF.Max(0f, lightLeft - deltaSeconds);
        tallyTime = MathF.Min(TallySeconds, tallyTime + deltaSeconds);
        if (simDelta <= 0f)
        {
            return;
        }

        var wanted = board.Thrusting ? 1f : 0f;
        flamePower = wanted > flamePower
            ? MathF.Min(wanted, flamePower + FlameRise * simDelta)
            : MathF.Max(wanted, flamePower - FlameFall * simDelta);
        flicker = effects.NextFloat();
    }

    private static Rect Focus(in GameContext context, float scale)
    {
        var band = StageLayout.PadBand(context.Full, StageLayout.ShooterBand, scale);
        var top = context.Safe.Min.Y + (TelemetryGap + TelemetryHeight) * scale;
        return new Rect(new Vector2(context.Full.Min.X, top), new Vector2(context.Full.Max.X, MathF.Max(top + 1f, band.Min.Y)));
    }

    private void PlaceCamera(in GameContext context, float scale)
    {
        var full = context.Full;
        var focus = Focus(context, scale);
        var position = board.Position;
        var ground = board.Terrain.GroundY(position.X);
        var altitude = MathF.Max(0f, ground - (position.Y + LanderBoard.FootDrop));
        var wanted = Math.Clamp(altitude * WidthPerAltitude + WidthBase, NearWidth, LanderTerrain.Width);
        if (snapCamera)
        {
            viewWidth.SnapTo(wanted);
        }
        else
        {
            viewWidth.Step(wanted, ZoomSmoothSeconds, context.RawDeltaSeconds);
        }

        var width = MathF.Max(1f, viewWidth.Value);
        var zoom = full.Width / width;
        camera.Fit(full, 1f, full.Height / zoom, FitMode.CoverHeight);
        var focusHeight = focus.Height / zoom;
        var centerY = MathF.Max(position.Y + LanderAbove * focusHeight, ground - GroundBelow * focusHeight);
        var half = width * 0.5f;
        var centerX = width >= LanderTerrain.Width
            ? LanderTerrain.Width * 0.5f
            : Math.Clamp(position.X, half, LanderTerrain.Width - half);
        var target = new Vector2(centerX, centerY) + (full.Center - focus.Center) / zoom;
        if (snapCamera)
        {
            camera.Place(target);
            snapCamera = false;
        }
        else
        {
            camera.Follow(target, Vector2.Zero, FollowSmoothSeconds, context.RawDeltaSeconds);
        }

        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, scale);
        context.Backdrop.SetCamera(in camera);
    }

    private void ReadInput(in GameContext context, out int rotate, out bool thrust)
    {
        rotate = 0;
        thrust = false;
        if (context.Session.State != StageFlow.Playing)
        {
            return;
        }

        var left = GameInput.Held(ImGuiKey.A, ImGuiKey.LeftArrow);
        var right = GameInput.Held(ImGuiKey.D, ImGuiKey.RightArrow);
        rotate = (right ? 1 : 0) - (left ? 1 : 0);
        if (rotate == 0)
        {
            rotate = padInput.Rotate;
        }

        thrust = GameInput.Held(ImGuiKey.W, ImGuiKey.UpArrow) || GameInput.Held(ImGuiKey.Space) || padInput.Thrust;
    }

    private void React(in GameContext context, bool live, float deltaSeconds)
    {
        if (board.LevelStartedThisStep)
        {
            craterActive = false;
            snapCamera = true;
            ribbon.Clear();
            smoke.Clear();
            if (live)
            {
                ShowBanner(levelLabel.Get(L.Lander.LevelBanner, board.Level), Accent);
                UiFeedback.Play(UiSound.GameTick);
            }
        }
        else if (board.RespawnedThisStep)
        {
            if (craterActive)
            {
                board.Terrain.Restore(mask, craterLeft, craterRight);
                craterActive = false;
            }

            snapCamera = true;
            ribbon.Clear();
        }

        if (board.LandedThisStep)
        {
            OnLanded(context, live);
        }

        if (board.CrashThisStep != LanderCrash.None)
        {
            OnCrash(context, live);
        }

        if (live && board.FuelLowThisStep)
        {
            UiFeedback.Play(UiSound.GameWrong);
            ShowBanner(Loc.T(L.Lander.LowFuel), Warn);
            context.Fx.Vignette(Warn, 0.3f, 0.6f);
        }

        if (live && board.FuelEmptyThisStep)
        {
            UiFeedback.Play(UiSound.GameWrong);
            ShowBanner(Loc.T(L.Lander.NoFuel), LanderRenderer.Danger);
        }

        if (board.State != LanderState.Flying || deltaSeconds <= 0f)
        {
            return;
        }

        ribbon.Push(board.Position);
        if (board.Thrusting)
        {
            EmitExhaust(deltaSeconds);
            if (live && board.FuelFraction <= LanderBoard.LowFuelFraction)
            {
                context.Fx.Vignette(Warn, 0.14f + 0.1f * Pulse.Wave(Pulse.Fast), 0.2f);
            }
        }

        if (board.Rotating == 0)
        {
            return;
        }

        var side = board.Rotating > 0 ? -1f : 1f;
        var nozzle = board.ToWorld(new Vector2(0.44f * side, -0.42f));
        var outward = board.ToWorld(new Vector2(side, -0.42f)) - nozzle;
        rcsEmitter.Spec = RcsSpec.WithDirection(MathF.Atan2(outward.Y, outward.X), 0.5f);
        rcsEmitter.Advance(deltaSeconds, nozzle, particles);
    }

    private void EmitExhaust(float deltaSeconds)
    {
        var nozzle = board.ToWorld(new Vector2(0f, LanderBoard.NozzleDrop + 0.1f));
        var down = -board.Up;
        var heading = MathF.Atan2(down.Y, down.X);
        flameEmitter.Spec = FlameSpec.WithDirection(heading, 0.5f);
        flameEmitter.Advance(deltaSeconds, nozzle, particles);
        smokeEmitter.Spec = SmokeSpec.WithDirection(heading, 0.7f);
        smokeEmitter.Advance(deltaSeconds, nozzle + down * 0.4f, smoke);
        var altitude = board.Altitude;
        if (altitude > DustAltitude || down.Y <= 0.2f)
        {
            return;
        }

        var reach = (altitude + 0.5f) / down.Y;
        var hit = nozzle + down * reach;
        var groundPoint = new Vector2(hit.X, board.Terrain.GroundY(hit.X) - 0.1f);
        var strength = 1f - altitude / DustAltitude;
        dustEmitter.Rate = 30f + 60f * strength;
        dustEmitter.Spec = DustSpec.WithDirection(effects.Chance(0.5f) ? -0.25f : MathF.PI + 0.25f, 0.6f);
        dustEmitter.Advance(deltaSeconds, groundPoint, smoke);
    }

    private void OnLanded(in GameContext context, bool live)
    {
        var position = board.Position;
        var feetY = position.Y + LanderBoard.FootDrop;
        particles.Emit(LandingDust.WithDirection(-0.12f, 0.5f), new Vector2(position.X + 0.6f, feetY - 0.1f), 10);
        particles.Emit(LandingDust.WithDirection(MathF.PI + 0.12f, 0.5f), new Vector2(position.X - 0.6f, feetY - 0.1f),
            10);
        tallyTarget = board.LastLandingPoints;
        tallyTime = 0f;
        tallyWorld = position - new Vector2(0f, 2.4f);
        camera.Punch(0.03f);
        if (board.PerfectThisStep)
        {
            particles.Emit(PerfectSparkle, position, 18);
        }

        if (!live)
        {
            return;
        }

        GameSfx.LevelClear();
        context.Fx.Sweep();
        var spot = board.Terrain.Pads[board.PadIndex];
        if (board.PerfectThisStep)
        {
            UiFeedback.Play(UiSound.GamePowerUp);
            ShowBanner(Loc.T(L.Lander.Perfect), LanderRenderer.Gold);
            context.Fx.Flash(LanderRenderer.Gold, 0.12f);
            return;
        }

        ShowBanner(landedLabel.Get(L.Lander.Landed, spot.Multiplier), LanderRenderer.MultiplierColor(spot.Multiplier, Accent));
    }

    private void OnCrash(in GameContext context, bool live)
    {
        var point = board.CrashPoint;
        var center = board.Position;
        particles.Emit(HullDebris, center, 16);
        particles.Emit(FoilDebris, center, 14);
        particles.Emit(Fireball, point, 12);
        particles.Emit(CrashSparks, point, 26);
        smoke.Emit(CrashSmoke, point, 14);
        mask.Carve(point, CraterRadius);
        craterActive = true;
        craterLeft = point.X - CraterRadius - MetresPerCell * 2f;
        craterRight = point.X + CraterRadius + MetresPerCell * 2f;
        lightPosition = point;
        lightLeft = LightSeconds;
        fx.Shockwave(camera.ToScreen(point), camera.Px(7f), LanderRenderer.Flame, 0.6f, 3.4f);
        camera.Shake(0.8f);
        ribbon.Clear();
        if (!live)
        {
            return;
        }

        UiFeedback.Play(UiSound.GameExplosion);
        context.Fx.Flash(LanderRenderer.Flame, 0.4f);
        context.Fx.SlowMo(0.4f, 0.45f);
        var reason = board.CrashThisStep switch
        {
            LanderCrash.TooFast => L.Lander.TooFast,
            LanderCrash.TooSteep => L.Lander.TooSteep,
            LanderCrash.Hull => L.Lander.HullHit,
            _ => L.Lander.MissedPad,
        };
        ShowBanner(Loc.T(reason), LanderRenderer.Danger);
        if (board.Lives == 1)
        {
            context.Fx.Vignette(LanderRenderer.Danger, 0.45f, 1.4f);
        }
    }

    private void ShowBanner(string text, Vector4 color)
    {
        bannerText = text;
        bannerColor = color;
        bannerProgress = 0f;
    }

    private void DrawWorld(ImDrawListPtr drawList, in GameContext context, float scale)
    {
        var accent = Accent;
        var full = context.Full;
        if (board.Terrain.Version != stampedVersion)
        {
            board.Terrain.Stamp(mask);
            stampedVersion = board.Terrain.Version;
            craterActive = false;
        }

        drawList.PushClipRect(full.Min, full.Max, true);
        var floor = camera.ToScreen(new Vector2(0f, LanderTerrain.Height)).Y;
        if (floor < full.Max.Y)
        {
            drawList.AddRectFilled(new Vector2(full.Min.X, MathF.Max(full.Min.Y, floor - 1f)), full.Max,
                ImGui.GetColorU32(TerrainMaterial.Lunar.Deep));
        }

        texture.Draw(drawList, in camera);
        var lightStrength = lightLeft / LightSeconds;
        if (lightStrength > 0f)
        {
            LanderRenderer.DrawLight(drawList, camera.ToScreen(lightPosition),
                camera.Px(LightRadius) * (0.7f + 0.3f * lightStrength), LanderRenderer.Flame,
                lightStrength * (0.85f + 0.15f * flicker));
        }

        DrawGroundLight(drawList);
        LanderRenderer.DrawRim(drawList, board.Terrain, in camera, accent, lightPosition, lightStrength,
            craterActive ? craterLeft : float.MaxValue, craterActive ? craterRight : float.MinValue, scale);
        RefreshPadLabels();
        var celebrating = board.State == LanderState.Landed ? board.PadIndex : -1;
        LanderRenderer.DrawPads(drawList, board.Terrain, in camera, accent, time, celebrating,
            padLabels.AsSpan(0, board.Terrain.Pads.Length), scale);
        smoke.Draw(drawList, in camera);
        ribbon.Draw(drawList, in camera, GamePalette.Lighten(accent, 0.3f) with { W = 0.28f },
            MathF.Max(1f, camera.Px(0.08f)));
        if (board.State is LanderState.Flying or LanderState.Landed)
        {
            LanderRenderer.DrawFlame(drawList, board, in camera, flamePower, flicker);
            LanderRenderer.DrawLander(drawList, board, in camera, accent, 1f);
        }

        particles.Draw(drawList, in camera);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        DrawPadArrows(drawList, context, accent, scale);
        drawList.PopClipRect();
    }

    private void DrawGroundLight(ImDrawListPtr drawList)
    {
        if (flamePower <= 0.05f || board.State != LanderState.Flying)
        {
            return;
        }

        var altitude = board.Altitude;
        if (altitude > GroundLightAltitude)
        {
            return;
        }

        var landerX = board.Position.X;
        var ground = new Vector2(landerX, board.Terrain.GroundY(landerX));
        var strength = (1f - altitude / GroundLightAltitude) * flamePower * (0.75f + 0.25f * flicker);
        LanderRenderer.DrawLight(drawList, camera.ToScreen(ground), camera.Px(3.5f), LanderRenderer.Flame,
            strength * 0.8f);
    }

    private void RefreshPadLabels()
    {
        var pads = board.Terrain.Pads;
        for (var index = 0; index < pads.Length; index++)
        {
            padLabels[index] = padSlots[index].Get(L.Stage.Times, pads[index].Multiplier);
        }
    }

    private void DrawPadArrows(ImDrawListPtr drawList, in GameContext context, Vector4 accent, float scale)
    {
        if (board.State != LanderState.Flying)
        {
            return;
        }

        var focus = Focus(context, scale);
        var visible = camera.VisibleWorld;
        var pads = board.Terrain.Pads;
        var inset = ArrowInset * scale;
        for (var index = 0; index < pads.Length; index++)
        {
            ref readonly var spot = ref pads[index];
            var onLeft = spot.Right < visible.Min.X;
            if (!onLeft && spot.Left <= visible.Max.X)
            {
                continue;
            }

            var screenY = Math.Clamp(camera.ToScreen(new Vector2(spot.Center, spot.Y)).Y, focus.Min.Y + inset * 2f,
                focus.Max.Y - inset * 2f);
            var screenX = onLeft ? context.Full.Min.X + inset : context.Full.Max.X - inset;
            var color = LanderRenderer.MultiplierColor(spot.Multiplier, accent);
            var center = new Vector2(screenX, screenY);
            ProgressRing.Glow(center, inset, color, 0.5f);
            ProgressRing.CenterIcon(drawList, center, onLeft ? FontAwesomeIcon.ChevronLeft : FontAwesomeIcon.ChevronRight,
                color, inset * 0.8f);
            Typography.DrawCentered(drawList, center + new Vector2(0f, inset * 1.1f), padLabels[index], color,
                TextStyles.Caption2.Scale, FontWeight.Bold);
        }
    }

    private void DrawTelemetry(ImDrawListPtr drawList, in GameContext context, float scale)
    {
        if (board.State != LanderState.Flying || finished)
        {
            return;
        }

        var style = TextStyles.FootnoteEmphasized;
        var descent = board.Descent;
        var drift = board.Velocity.X;
        var tiltDegrees = (int)MathF.Round(board.Tilt * 180f / MathF.PI);
        var descentText = LanderText.TenthsOf(descent);
        var driftText = LanderText.TenthsOf(drift);
        var tiltText = degreesLabel.Get(L.Lander.Degrees, tiltDegrees);
        var descentStatus = Status(descent, LanderBoard.SafeDescent);
        var driftStatus = Status(MathF.Abs(drift), LanderBoard.SafeDrift);
        var tiltStatus = Status(board.Tilt, LanderBoard.SafeTilt);
        var first = CapsuleWidth(descentText, style, scale);
        var second = CapsuleWidth(driftText, style, scale);
        var third = CapsuleWidth(tiltText, style, scale);
        var spacing = TelemetrySpacing * scale;
        var total = first + second + third + spacing * 2f;
        var height = TelemetryHeight * scale;
        var centerY = context.Safe.Min.Y + TelemetryGap * scale * 0.5f + height * 0.5f;
        var cursor = context.Full.Center.X - total * 0.5f;
        var safe = descentStatus == 0 && driftStatus == 0 && tiltStatus == 0 && board.Altitude < SafeGlowAltitude;
        if (safe)
        {
            ProgressRing.Glow(new Vector2(context.Full.Center.X, centerY), total * 0.42f, Safe,
                0.35f + 0.25f * Pulse.Wave(Pulse.Fast));
        }

        cursor = DrawReading(drawList, cursor, centerY, first, height,
            descent >= 0f ? FontAwesomeIcon.ArrowDown : FontAwesomeIcon.ArrowUp, descentText, descentStatus, safe,
            context, scale) + spacing;
        cursor = DrawReading(drawList, cursor, centerY, second, height,
            drift >= 0f ? FontAwesomeIcon.ArrowRight : FontAwesomeIcon.ArrowLeft, driftText, driftStatus, safe, context,
            scale) + spacing;
        DrawReading(drawList, cursor, centerY, third, height,
            board.Angle >= 0f ? FontAwesomeIcon.RedoAlt : FontAwesomeIcon.UndoAlt, tiltText, tiltStatus, safe, context,
            scale);
    }

    private static int Status(float value, float limit)
    {
        if (value > limit)
        {
            return 2;
        }

        return value > limit * 0.75f ? 1 : 0;
    }

    private static float CapsuleWidth(string text, in TextStyle style, float scale) =>
        (CapsulePadX * 2f + IconSize + IconGap) * scale + Typography.Measure(text, style).X;

    private float DrawReading(ImDrawListPtr drawList, float left, float centerY, float width, float height,
        FontAwesomeIcon icon, string text, int status, bool safe, in GameContext context, float scale)
    {
        var rect = new Rect(new Vector2(left, centerY - height * 0.5f), new Vector2(left + width, centerY + height * 0.5f));
        StageHud.Capsule(drawList, rect, scale);
        var color = status switch
        {
            2 => LanderRenderer.Danger,
            1 => Warn,
            _ => safe ? Safe : StageInks.Strong,
        };
        if (status == 2)
        {
            var pulse = 0.5f + 0.5f * Pulse.Wave(Pulse.Fast);
            Squircle.Stroke(drawList, rect.Min, rect.Max, height * 0.5f,
                ImGui.GetColorU32(LanderRenderer.Danger with { W = 0.5f + 0.4f * pulse }), 1.5f * scale);
        }

        var iconSize = IconSize * scale;
        var cursor = rect.Min.X + CapsulePadX * scale;
        ProgressRing.CenterIcon(drawList, new Vector2(cursor + iconSize * 0.5f, centerY), icon,
            status == 0 && !safe ? Accent : color, iconSize);
        var style = TextStyles.FootnoteEmphasized;
        Typography.Draw(drawList,
            new Vector2(cursor + iconSize + IconGap * scale, centerY - Typography.LineHeight(style) * 0.5f), text, color,
            style);
        return rect.Max.X;
    }

    private void DrawTally(ImDrawListPtr drawList, float scale)
    {
        if (tallyTime >= TallySeconds && board.State != LanderState.Landed)
        {
            return;
        }

        var progress = Math.Clamp(tallyTime / TallySeconds, 0f, 1f);
        var eased = Easing.EaseOutCubic(progress);
        var step = MathF.Max(1f, tallyTarget / TallySteps);
        var shown = progress >= 1f ? tallyTarget : (int)(MathF.Floor(tallyTarget * eased / step) * step);
        var center = camera.ToScreen(tallyWorld) - new Vector2(0f, 10f * scale * eased);
        var pop = progress >= 1f ? 1.12f : 1f;
        ProgressRing.Glow(center, 34f * scale, LanderRenderer.Gold, 0.5f);
        Typography.DrawCentered(drawList, center, GameNumber.Signed(shown), LanderRenderer.Gold,
            TextStyles.Title2.Scale * pop, FontWeight.Bold);
    }

    private void DrawFuel(ImDrawListPtr drawList, Rect rect, float scale)
    {
        if (rect.Width <= 0f)
        {
            return;
        }

        var fraction = Math.Clamp(board.FuelFraction, 0f, 1f);
        var low = fraction <= LanderBoard.LowFuelFraction;
        var pulse = low ? 0.5f + 0.5f * Pulse.Wave(Pulse.Fast) : 0f;
        var color = fraction > 0.35f ? Accent : low ? Vector4.Lerp(Warn, LanderRenderer.Danger, pulse) : Warn;
        StageHud.Capsule(drawList, rect, scale);
        var iconSize = IconSize * scale;
        var left = rect.Min.X + CapsulePadX * scale;
        ProgressRing.CenterIcon(drawList, new Vector2(left + iconSize * 0.5f, rect.Center.Y), FontAwesomeIcon.Tint, color,
            iconSize);
        var barLeft = left + iconSize + IconGap * scale;
        var barRight = rect.Max.X - CapsulePadX * scale;
        var half = FuelBarHeight * scale * 0.5f;
        var barMin = new Vector2(barLeft, rect.Center.Y - half);
        var barMax = new Vector2(barRight, rect.Center.Y + half);
        drawList.AddRectFilled(barMin, barMax, ImGui.GetColorU32(color with { W = 0.2f }), half);
        if (fraction <= 0f)
        {
            return;
        }

        var fillRight = barLeft + (barRight - barLeft) * fraction;
        drawList.AddRectFilled(barMin, new Vector2(fillRight, barMax.Y), ImGui.GetColorU32(color), half);
        if (board.Thrusting)
        {
            ProgressRing.Glow(new Vector2(fillRight, rect.Center.Y), half * 3f, LanderRenderer.Flame, 0.8f);
        }
    }

    private GameOutcome Outcome() =>
        new GameOutcome(board.Score, ScoreKind.Score, GameId)
            .WithStat(L.Lander.Landings, GameNumber.Label(board.Landings))
            .WithStat(L.Games.Level, GameNumber.Label(board.Level))
            .WithStat(L.Lander.BestLanding, GameNumber.Label(board.BestLanding))
            .WithStat(L.Lander.Perfects, GameNumber.Label(board.Perfects));
}
