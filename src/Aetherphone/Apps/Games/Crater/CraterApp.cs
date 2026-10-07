using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Games.Crater;

internal sealed partial class CraterApp : IMiniGame
{
    private const string GameId = "crater";
    private const int HardMode = 1;
    private const int VersusBotMask = 1 << 1;
    private const int DemoBotMask = (1 << CraterRules.MinTeams) - 1;
    private const ulong DemoSeed = 0xC4A7E5UL;
    private const float ViewWidth = 16f;
    private const float ViewHeight = 8f;
    private const float ChargeZoom = 1.15f;
    private const float FlightZoom = 1.08f;
    private const float ZoomSmoothSeconds = 0.4f;
    private const float WinBannerSeconds = 2.2f;
    private const float DemoRestartSeconds = 2.5f;
    private const int HintCapacity = 192;
    private const int HintStride = 3;
    private const float UrgentSeconds = 10f;

    private static readonly LocString[] Modes = { L.Games.Easy, L.Games.Hard };
    private static readonly GameSpec StageSpec = new(GameId, L.Crater.Title, GameGenre.Strategy, L.Crater.Hook,
        Backdrop.Sky, HudStyle.Compact, ScoreKind.Streak, Modes, landscape: true, keyboard: true,
        seats: CraterRules.MaxTeams);
    private static readonly float[] SkyMoments = { 0.12f, 0.2f, 0.26f, 0.32f, 0.46f };

    private readonly ITextureProvider textures;
    private readonly CraterBoard board = new();
    private readonly CraterLabels labels = new();
    private readonly CraterJuice juice = new();
    private readonly Vector2[] hintPath = new Vector2[HintCapacity];
    private CraterTerrain? terrainTexture;
    private Camera2D camera = Camera2D.Create();
    private Spring zoom = new(1f);
    private int demoRuns;
    private float time;
    private float skyProgress;
    private float overSeconds;
    private bool cameraPlaced;
    private bool demo;
    private bool hotSeat;
    private bool finished;

    public CraterApp(ITextureProvider textures)
    {
        this.textures = textures;
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        hotSeat = start.HotSeat;
        var teams = hotSeat ? Math.Clamp(start.Seats, CraterRules.MinTeams, CraterRules.MaxTeams) : CraterRules.MinTeams;
        var level = start.Mode == HardMode ? CraterLevel.Hard : CraterLevel.Easy;
        var setup = new CraterSetup(teams, hotSeat ? 0 : VersusBotMask, level);
        BeginMatch(start.Seed, setup, hotSeat ? teams : 1, false);
    }

    public void Close()
    {
        demo = false;
        finished = true;
        juice.Clear();
        ResetInput();
    }

    public void Dispose()
    {
        terrainTexture?.Dispose();
        terrainTexture = null;
    }

    public void DrawIdle(in GameContext context)
    {
        if (!demo || !board.Started)
        {
            BeginDemo();
        }

        var raw = context.RawDeltaSeconds;
        Advance(raw);
        PlaceCamera(context, raw);
        board.Step(raw);
        Drain(context, true);
        if (board.Over)
        {
            overSeconds += raw;
            if (overSeconds >= DemoRestartSeconds)
            {
                BeginDemo();
            }
        }

        DrawWorld(ImGui.GetWindowDrawList(), context, false, UiScale.Current);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var simDelta = juice.Fx.ScaleDelta(context.DeltaSeconds);
        Advance(context.RawDeltaSeconds);
        PlaceCamera(context, context.DeltaSeconds);
        board.Step(simDelta);
        Drain(context, false);
        var layout = CraterControls.Layout(context.Full, context.Safe, board.TeamCount, scale);
        DrawWorld(drawList, context, true, scale);
        juice.DrawBanners(drawList, context.Full, context.Theme);
        DrawControls(drawList, context, layout, scale);
        FillHud(drawList, context, layout, scale);
        CheckFinish(context);
    }

    private void BeginDemo()
    {
        hotSeat = false;
        var seed = DemoSeed + (ulong)demoRuns++;
        BeginMatch(seed, new CraterSetup(CraterRules.MinTeams, DemoBotMask, CraterLevel.Easy), 0, true);
    }

    private void BeginMatch(ulong seed, in CraterSetup setup, int humans, bool demoMatch)
    {
        demo = demoMatch;
        board.Start(seed, setup);
        labels.Configure(seed, hotSeat, humans);
        EnsureTerrain().Reset(CraterArt.Material(board.Style));
        juice.Reset(seed);
        ResetInput();
        finished = false;
        overSeconds = 0f;
        skyProgress = SkyMoments[(int)(seed % (ulong)SkyMoments.Length)];
        camera = Camera2D.Create();
        zoom.SnapTo(1f);
        cameraPlaced = false;
    }

    private CraterTerrain EnsureTerrain() => terrainTexture ??= new CraterTerrain(textures, board.Terrain);

    private void Advance(float rawSeconds)
    {
        time += rawSeconds;
        juice.Advance(rawSeconds);
    }

    private void PlaceCamera(in GameContext context, float followSeconds)
    {
        var wanted = board.Charging ? ChargeZoom : board.Phase == CraterPhase.Flight ? FlightZoom : 1f;
        zoom.Step(wanted, ZoomSmoothSeconds, context.RawDeltaSeconds);
        camera.Fit(context.Full, ViewWidth * zoom.Value, ViewHeight * zoom.Value, FitMode.Contain);
        var focus = CraterFocus.Goal(board.Projectiles, board.Moogles, board.SinceBlast, board.LastBlast,
            board.ActiveMoogle, board.Phase == CraterPhase.Aiming, board.ActiveAim, out var smoothSeconds,
            out var flying);
        var goal = CraterFocus.KeepInView(in camera, focus, flying, board.WaterLevel);
        if (!cameraPlaced)
        {
            camera.Place(goal);
            cameraPlaced = true;
        }

        camera.Follow(goal, Vector2.Zero, smoothSeconds, followSeconds);
        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, UiScale.Current);
        context.Backdrop.SetSky(skyProgress);
        context.Backdrop.SetCamera(in camera);
    }

    private void DrawWorld(ImDrawListPtr drawList, in GameContext context, bool live, float scale)
    {
        EnsureTerrain().Draw(drawList, in camera, board.Craters);
        juice.DrawEmbers(drawList, in camera);
        var aiming = board.Phase is CraterPhase.Aiming or CraterPhase.TurnIntro;
        CraterRenderer.Bodies(drawList, in camera, board, juice.Hurt, aiming, time);
        juice.DrawTrails(drawList, in camera, board.Projectiles, CraterArt.Material(board.Style), scale);
        CraterRenderer.Projectiles(drawList, in camera, board, time, scale);
        if (live)
        {
            DrawAim(drawList, scale);
        }

        CraterRenderer.Water(drawList, in camera, board.Terrain, board.WaterLevel, time, scale);
        juice.Particles.Draw(drawList, in camera);
        juice.Fx.DrawRings(drawList, scale);
        juice.Fx.DrawText();
        CraterRenderer.Labels(drawList, in camera, board, labels, aiming, time, scale);
    }

    private void DrawAim(ImDrawListPtr drawList, float scale)
    {
        if (board.Phase != CraterPhase.Aiming || !board.Moogle(board.ActiveMoogle).Alive)
        {
            return;
        }

        ref readonly var active = ref board.Moogle(board.ActiveMoogle);
        var team = GameSeats.Color(active.Team);
        var direction = CraterRules.AimDirection(board.ActiveAim, active.Facing);
        if (board.Charging)
        {
            CraterRenderer.Power(drawList, in camera, active.Position, direction, board.Charge, scale);
        }

        if (!board.HumanTurn)
        {
            return;
        }

        if (board.Weapon == CraterWeapon.Teleport)
        {
            if (showTeleport)
            {
                CraterRenderer.Ghost(drawList, in camera, teleportSpot, teleportValid, team, time, scale);
            }

            return;
        }

        if (!CraterRules.Fires(board.Weapon))
        {
            return;
        }

        CraterRenderer.Reticle(drawList, in camera, active.Position, direction, team, time, scale);
        var power = board.Charging ? board.Charge : board.TeamPower(board.ActiveTeam);
        var shot = new CraterShot(board.Weapon, active.Facing, board.ActiveAim, power, board.Fuse);
        board.Predict(board.ActiveMoogle, shot, hintPath, HintStride, out var count, out _, out _);
        var shown = Math.Min(count, Math.Max(2, (int)MathF.Ceiling(count * CraterRenderer.HintShare)));
        CraterRenderer.Hint(drawList, in camera, new ReadOnlySpan<Vector2>(hintPath, 0, shown), team, scale);
    }

    private void FillHud(ImDrawListPtr drawList, in GameContext context, in CraterLayout layout, float scale)
    {
        var hud = context.Hud;
        hud.Score(board.Round, L.Crater.Round);
        hud.Timer(board.TurnLeft, CraterRules.TurnSeconds, board.HumanTurn && board.TurnLeft <= UrgentSeconds);
        CraterControls.Wind(drawList, layout.Wind, board.WindLevel, Accent, time, scale);
        context.Session.Report(board.DamageDealt(0));
    }

    private void CheckFinish(in GameContext context)
    {
        if (finished || !board.Over)
        {
            return;
        }

        if (context.Session.State == StageFlow.Playing)
        {
            overSeconds += context.RawDeltaSeconds;
        }

        if (overSeconds < WinBannerSeconds)
        {
            return;
        }

        finished = true;
        context.Session.Finish(Outcome(context.Session.StatId));
    }

    private GameOutcome Outcome(string statId)
    {
        var winner = board.Winner;
        if (hotSeat)
        {
            var unranked = winner == CraterBoard.NoTeam ? GameOutcome.Unranked(false) : GameOutcome.Unranked().WithWinner(winner);
            return unranked
                .WithStat(L.Crater.Rounds, GameNumber.Label(board.Round))
                .WithStat(L.Crater.Craters, GameNumber.Label(board.CraterCount));
        }

        var outcome = winner == CraterBoard.NoTeam
            ? GameOutcome.Drawn(statId)
            : new GameOutcome(0, ScoreKind.Streak, statId, winner == 0);
        return outcome
            .WithStat(L.Crater.Damage, GameNumber.Label(board.DamageDealt(0)))
            .WithStat(L.Crater.Knockouts, GameNumber.Label(board.Knockouts(0)))
            .WithStat(L.Crater.Rounds, GameNumber.Label(board.Round));
    }
}
