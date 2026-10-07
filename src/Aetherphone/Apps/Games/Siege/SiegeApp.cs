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

namespace Aetherphone.Apps.Games.Siege;

internal sealed class SiegeApp : IMiniGame
{
    internal const string EndlessStatId = "siege.endless";
    private const string GameId = "siege";
    private const string SurfaceId = "siege.garden";
    private const int CampaignMode = 0;
    private const int EndlessMode = 1;
    private const int TipCapacity = 4;
    private const int FlightCapacity = 24;
    private const int NoCell = -1;
    private const ulong IdleSeed = 0x5349454745UL;
    private const float IdleSunlight = 600f;
    private const float TrayGap = 6f;
    private const float DragThreshold = 8f;
    private const float MoteTapRadius = 0.5f;
    private const float WaveBannerSeconds = 1.7f;
    private const float TipSeconds = 3.8f;
    private const float EndDelaySeconds = 1.6f;
    private const float FlightSeconds = 0.6f;
    private const float TrailStep = 0.05f;
    private const float TrailWidth = 0.08f;
    private const float ShotSoundGap = 0.09f;
    private const float HitSoundGap = 0.07f;
    private const float BiteSoundGap = 0.16f;
    private const float FenceFlashDecay = 2.2f;
    private const float LastStandShare = 0.4f;
    private const float LastStandFactor = 0.35f;
    private const float LastStandSeconds = 1.1f;
    private const float WorldGravity = 9f;
    private const float TipTop = 18f;
    private const float TipPadX = 14f;
    private const float TipHeight = 30f;
    private const float TipIconSize = 12f;
    private const float CaptionGap = 16f;
    private const float CapsulePadX = 10f;
    private const float CapsuleIconSize = 11f;
    private const float CapsuleIconGap = 5f;
    private const float BannerRow = 2.6f;
    private static readonly LocString[] Modes = { L.Siege.Campaign, L.Siege.Endless };
    private static readonly string[] ModeStatIds = { GameId, EndlessStatId };
    private static readonly bool[] LevelModes = { true, false };
    private static readonly GameSpec StageSpec = new(GameId, L.Siege.Title, GameGenre.Strategy, L.Siege.Hook,
        Backdrop.Meadow, HudStyle.Standard, ScoreKind.Level, Modes, ModeStatIds, clocked: true, keyboard: true,
        levelCount: SiegeLevels.Count, levelModes: LevelModes);
    private static readonly ImGuiKey[] SlotKeys =
    {
        ImGuiKey.Key1, ImGuiKey.Key2, ImGuiKey.Key3, ImGuiKey.Key4, ImGuiKey.Key5, ImGuiKey.Key6,
    };

    private static readonly TextStyle TipStyle = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle CapsuleStyle = TextStyles.FootnoteEmphasized;
    private static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Royal = new(0.66f, 0.40f, 0.86f, 1f);
    private static readonly Vector4 SmokeColor = new(0.36f, 0.34f, 0.32f, 0.7f);
    private static readonly Vector4 PotDark = new(0.52f, 0.28f, 0.16f, 1f);
    private static readonly Vector4 PetalPink = new(1f, 0.62f, 0.76f, 1f);
    private static readonly Vector4 Fuse = new(1f, 0.64f, 0.22f, 1f);
    private static readonly Vector4[] CelebrationPalette =
    {
        new(1f, 0.84f, 0.30f, 1f), new(0.46f, 0.86f, 0.40f, 1f), new(1f, 0.62f, 0.76f, 1f),
        new(0.58f, 0.84f, 1f, 1f), new(1f, 0.97f, 0.88f, 1f),
    };

    private static readonly ParticleSpec LeafBurst = new(SiegeArt.Leaf, SiegeArt.LeafDark, 0.09f, 3.4f, 0.75f,
        WorldGravity, 1.2f, 10f, shape: ParticleShape.Shard);
    private static readonly ParticleSpec HitSpark = new(SiegeArt.SeedGreen, White with { W = 0f }, 0.05f, 2.8f, 0.28f,
        0f, 2.4f, shape: ParticleShape.Spark, additive: true);
    private static readonly ParticleSpec FrostSpark = new(SiegeArt.FrostCore, SiegeArt.Frost with { W = 0f }, 0.06f,
        2.6f, 0.4f, 0f, 2.2f, 6f, shape: ParticleShape.Star, additive: true);
    private static readonly ParticleSpec DirtPuff = new(SiegeArt.Dirt, SiegeArt.Dirt with { W = 0f }, 0.07f, 2.4f,
        0.5f, WorldGravity, 1.6f, 6f, shape: ParticleShape.Square);
    private static readonly ParticleSpec Chips = new(SiegeArt.LeafDark, SiegeArt.Dirt with { W = 0f }, 0.05f, 2f,
        0.35f, WorldGravity, 1.6f, 8f, MathF.PI, -MathF.PI * 0.5f, ParticleShape.Shard);
    private static readonly ParticleSpec Embers = new(Fuse, SiegeArt.Cap with { W = 0f }, 0.09f, 5.5f, 0.65f, 3f,
        1.6f, 9f, shape: ParticleShape.Shard, additive: true);
    private static readonly ParticleSpec Smoke = new(SmokeColor, SmokeColor with { W = 0f }, 0.22f, 1.4f, 0.9f, -1.2f,
        1.4f, curve: SizeCurve.Grow);
    private static readonly ParticleSpec SunSparkle = new(SiegeArt.Sun, SiegeArt.SunCore with { W = 0f }, 0.06f, 1.8f,
        0.55f, -1f, 2f, 6f, shape: ParticleShape.Star, additive: true);
    private static readonly ParticleSpec PotShards = new(SiegeArt.Pot, PotDark, 0.08f, 3f, 0.7f, WorldGravity, 1f, 12f,
        MathF.PI, -MathF.PI * 0.5f, ParticleShape.Square);
    private static readonly ParticleSpec Petals = new(PetalPink, PetalPink with { W = 0f }, 0.07f, 3f, 0.9f, 5f, 1.4f,
        8f, MathF.PI, -MathF.PI * 0.5f, ParticleShape.Shard);
    private static readonly ParticleSpec MuzzlePuff = new(SiegeArt.SeedGreen with { W = 0.7f },
        SiegeArt.SeedGreen with { W = 0f }, 0.05f, 1.2f, 0.25f, 0f, 3f, 0f, 1.2f, -MathF.PI * 0.5f);

    private readonly SiegeBoard board = new();
    private readonly SiegeBoard idleBoard = new();
    private readonly SiegeRenderer renderer = new();
    private readonly SiegeTray tray = new();
    private readonly ParticleSystem particles = new(640);
    private readonly FeedbackFx fx = new();
    private readonly Ribbon[] trails = new Ribbon[SiegeBoard.SeedCapacity];
    private readonly int[] trailOwners = new int[SiegeBoard.SeedCapacity];
    private readonly Flight[] flights = new Flight[FlightCapacity];
    private readonly float[] fenceFlash = new float[SiegeRules.Columns];
    private readonly LocString?[] tips = new LocString?[TipCapacity];
    private Camera2D camera = Camera2D.Create();
    private LabelPairSlot waveOfLabel;
    private LabelPairSlot bannerOfLabel;
    private LabelSlot bannerLabel;
    private LocString? currentTip;
    private string bannerText = string.Empty;
    private Vector2 pressStart;
    private int mode;
    private int selected = SiegeTray.NoSlot;
    private int pressSlot = SiegeTray.NoSlot;
    private int hoverColumn = NoCell;
    private int hoverRow = NoCell;
    private int tipHead;
    private int tipCount;
    private float time;
    private float entrance;
    private float bannerProgress = 1f;
    private float tipProgress = 1f;
    private float endTimer;
    private float shotSound;
    private float hitSound;
    private float biteSound;
    private bool dragging;
    private bool finished;
    private bool ending;
    private bool lastStand;
    private bool idleReady;

    public SiegeApp()
    {
        for (var index = 0; index < trails.Length; index++)
        {
            trails[index] = new Ribbon();
            trailOwners[index] = -1;
        }
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        mode = start.Mode == EndlessMode ? EndlessMode : CampaignMode;
        if (mode == EndlessMode)
        {
            board.StartEndless(start.Random);
        }
        else
        {
            board.StartCampaign(start.Random, Math.Max(1, start.Level));
        }

        particles.Clear();
        particles.Reseed(start.Seed);
        fx.Clear();
        tray.Reset();
        camera = Camera2D.Create();
        ClearTrails();
        Array.Clear(flights);
        Array.Clear(fenceFlash);
        Array.Clear(tips);
        tipHead = 0;
        tipCount = 0;
        currentTip = null;
        tipProgress = 1f;
        bannerProgress = 1f;
        selected = SiegeTray.NoSlot;
        pressSlot = SiegeTray.NoSlot;
        dragging = false;
        finished = false;
        ending = false;
        lastStand = false;
        endTimer = 0f;
        entrance = 0f;
        QueueOpeningTips();
    }

    public void Close()
    {
        particles.Clear();
        fx.Clear();
        ClearTrails();
        idleReady = false;
    }

    public void Dispose()
    {
    }

    public void OnQuit(GameSession session)
    {
        if (finished || mode != EndlessMode || board.WavesSurvived <= 0)
        {
            return;
        }

        finished = true;
        session.Finish(BuildOutcome());
    }

    public void DrawIdle(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var deltaSeconds = context.RawDeltaSeconds;
        time += deltaSeconds;
        if (!idleReady || idleBoard.Over)
        {
            BuildIdle();
        }

        Layout(context, scale, out var view);
        PlaceCamera(context, view, scale);
        entrance = 1f;
        idleBoard.BeginFrame();
        idleBoard.Step(deltaSeconds);
        CollectIdleMotes();
        ReactIdle();
        particles.Update(deltaSeconds);
        UpdateTrails(idleBoard);
        DrawScene(drawList, idleBoard, context, scale, false);
        tray.Draw(drawList, idleBoard, SiegeTray.NoSlot, time, scale, Accent);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rawSeconds = context.RawDeltaSeconds;
        var accent = Accent;
        time += rawSeconds;
        Layout(context, scale, out var view);
        PlaceCamera(context, view, scale);
        board.BeginFrame();
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        particles.Update(rawSeconds);
        fx.Update(rawSeconds);
        tray.Update(rawSeconds, selected);
        AdvanceCosmetics(rawSeconds, context.Session.State == StageFlow.Paused ? 0f : rawSeconds);
        entrance = GameJuice.Advance(entrance, rawSeconds);
        if (!finished)
        {
            if (context.Session.State == StageFlow.Playing && !board.Over)
            {
                HandleInput(context, scale);
            }
            else
            {
                CancelGesture();
            }

            board.Step(simDelta);
            React(context, scale);
            WatchLastStand(context);
            AdvanceEnd(context);
        }

        UpdateTrails(board);
        DrawScene(drawList, board, context, scale, true);
        DrawBanner(drawList, context);
        DrawTip(drawList, view, scale, accent);
        tray.Draw(drawList, board, selected, time, scale, accent);
        DrawSelectionCaption(drawList, scale);
        DrawDragGhost(drawList);
        DrawFlights(drawList, context, scale);
        FillHud(drawList, context, scale, accent);
        context.Session.Report(mode == EndlessMode ? board.WavesSurvived : board.Wave);
    }

    private void Layout(in GameContext context, float scale, out Rect view)
    {
        var safe = context.Safe;
        var trayTop = safe.Max.Y - SiegeTray.Height * scale;
        tray.Layout(new Rect(new Vector2(safe.Min.X, trayTop), safe.Max), scale);
        view = new Rect(safe.Min, new Vector2(safe.Max.X, MathF.Max(safe.Min.Y, trayTop - TrayGap * scale)));
    }

    private void PlaceCamera(in GameContext context, Rect view, float scale)
    {
        camera.Fit(view, SiegeRenderer.World, FitMode.Contain);
        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, scale);
        context.Backdrop.SetCamera(in camera);
    }

    private void BuildIdle()
    {
        idleBoard.StartEndless(GameRandom.FromSeed(IdleSeed));
        idleBoard.Grant(IdleSunlight);
        for (var column = 0; column < SiegeRules.Columns; column++)
        {
            idleBoard.Place(DefenderKind.Sunbloom, column, SiegeRules.Rows - 1);
            idleBoard.Place(column == 2 ? DefenderKind.Frostbud : DefenderKind.Sprout, column, SiegeRules.Rows - 2);
            idleBoard.Place(DefenderKind.Sprout, column, SiegeRules.Rows - 3);
        }

        idleBoard.Place(DefenderKind.Thornwall, 1, 2);
        idleBoard.Place(DefenderKind.Thornwall, 3, 3);
        idleReady = true;
    }

    private void CollectIdleMotes()
    {
        for (var index = 0; index < SiegeBoard.MoteCapacity; index++)
        {
            ref readonly var mote = ref idleBoard.Mote(index);
            if (mote.Alive && mote.Age > mote.Travel + 1.2f)
            {
                idleBoard.Collect(index);
            }
        }
    }

    private void ReactIdle()
    {
        var events = idleBoard.Events;
        for (var index = 0; index < events.Length; index++)
        {
            ref readonly var item = ref events[index];
            if (item.Kind == SiegeEventKind.EnemyKilled)
            {
                particles.Emit(LeafBurst, item.Position, 8);
            }
            else if (item.Kind == SiegeEventKind.MoteCollected)
            {
                particles.Emit(SunSparkle, item.Position, 6);
            }
        }
    }

    private void HandleInput(in GameContext context, float scale)
    {
        var full = context.Full;
        var area = new Rect(new Vector2(full.Min.X, context.Safe.Min.Y), full.Max);
        PressSurface.Claim(SurfaceId, area, out var activated);
        var mouse = ImGui.GetMousePos();
        var hovering = UiInteract.Hover(area.Min, area.Max) && !context.ChromeHit(mouse);
        if (!hovering || !CellUnder(mouse, out hoverColumn, out hoverRow))
        {
            hoverColumn = NoCell;
            hoverRow = NoCell;
        }

        for (var slot = 0; slot < SlotKeys.Length; slot++)
        {
            if (GameInput.Pressed(SlotKeys[slot]))
            {
                Toggle(slot, scale);
            }
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Right) && hovering)
        {
            selected = SiegeTray.NoSlot;
        }

        if (activated && hovering)
        {
            Press(mouse, scale);
        }

        if (pressSlot == SiegeTray.NoSlot)
        {
            return;
        }

        if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            if (!dragging && Vector2.Distance(mouse, pressStart) > DragThreshold * scale && Usable(pressSlot))
            {
                dragging = true;
                selected = pressSlot;
            }

            return;
        }

        if (dragging)
        {
            if (hoverColumn != NoCell)
            {
                Apply(pressSlot, hoverColumn, hoverRow, scale);
            }

            selected = SiegeTray.NoSlot;
        }
        else
        {
            Toggle(pressSlot, scale);
        }

        pressSlot = SiegeTray.NoSlot;
        dragging = false;
    }

    private void CancelGesture()
    {
        pressSlot = SiegeTray.NoSlot;
        dragging = false;
        hoverColumn = NoCell;
        hoverRow = NoCell;
    }

    private bool CellUnder(Vector2 mouse, out int column, out int row)
    {
        var world = camera.ToWorld(mouse);
        column = (int)MathF.Floor(world.X);
        row = (int)MathF.Floor(world.Y);
        return world.X >= 0f && world.Y >= 0f && column < SiegeRules.Columns && row < SiegeRules.Rows;
    }

    private void Press(Vector2 mouse, float scale)
    {
        var slot = tray.SlotAt(mouse);
        if (slot != SiegeTray.NoSlot)
        {
            pressSlot = slot;
            pressStart = mouse;
            dragging = false;
            return;
        }

        var mote = board.MoteAt(camera.ToWorld(mouse), MoteTapRadius);
        if (mote >= 0)
        {
            board.Collect(mote);
            return;
        }

        if (selected != SiegeTray.NoSlot && hoverColumn != NoCell)
        {
            Apply(selected, hoverColumn, hoverRow, scale);
            return;
        }

        selected = SiegeTray.NoSlot;
    }

    private bool Usable(int slot)
    {
        if (slot == SiegeTray.ShovelSlot)
        {
            return true;
        }

        var kind = SiegeTray.KindOf(slot);
        return board.Unlocked(kind) && board.Ready(kind) && board.Affordable(kind);
    }

    private void Toggle(int slot, float scale)
    {
        if (selected == slot)
        {
            selected = SiegeTray.NoSlot;
            return;
        }

        if (slot != SiegeTray.ShovelSlot)
        {
            var kind = SiegeTray.KindOf(slot);
            if (!board.Unlocked(kind))
            {
                Refuse(slot, L.Siege.Locked, scale);
                return;
            }

            if (!board.Ready(kind))
            {
                Refuse(slot, L.Siege.Recharging, scale);
                return;
            }

            if (!board.Affordable(kind))
            {
                Refuse(slot, L.Siege.NotEnough, scale);
                return;
            }
        }

        selected = slot;
        UiFeedback.Play(UiSound.GameCardFlip);
    }

    private void Apply(int slot, int column, int row, float scale)
    {
        if (slot == SiegeTray.ShovelSlot)
        {
            if (board.Dig(column, row))
            {
                selected = SiegeTray.NoSlot;
            }
            else
            {
                UiFeedback.Play(UiSound.GameWrong);
            }

            return;
        }

        var result = board.Plant(SiegeTray.KindOf(slot), column, row);
        switch (result)
        {
            case PlantResult.Planted:
                selected = SiegeTray.NoSlot;
                return;
            case PlantResult.Occupied:
                Refuse(slot, L.Siege.Occupied, scale);
                return;
            case PlantResult.CoolingDown:
                Refuse(slot, L.Siege.Recharging, scale);
                return;
            case PlantResult.Unaffordable:
                Refuse(slot, L.Siege.NotEnough, scale);
                return;
            case PlantResult.Locked:
                Refuse(slot, L.Siege.Locked, scale);
                return;
            default:
                return;
        }
    }

    private void Refuse(int slot, LocString reason, float scale)
    {
        tray.Shake(slot);
        UiFeedback.Play(UiSound.GameWrong);
        var card = tray.Slot(slot);
        fx.AddText(Loc.T(reason), new Vector2(card.Center.X, card.Min.Y - 10f * scale), Danger, 0.8f);
    }

    private void React(in GameContext context, float scale)
    {
        var events = board.Events;
        for (var index = 0; index < events.Length; index++)
        {
            ref readonly var item = ref events[index];
            switch (item.Kind)
            {
                case SiegeEventKind.WaveStarted:
                    OnWaveStarted(item, context);
                    break;
                case SiegeEventKind.WaveCleared:
                    UiFeedback.Play(UiSound.GameMatch);
                    fx.AddText(Loc.T(L.Siege.WaveCleared), camera.ToScreen(new Vector2(SiegeRules.Columns * 0.5f, BannerRow)),
                        SiegeArt.Sun, 1.1f);
                    break;
                case SiegeEventKind.EnemySpawned:
                    OnSpawned(item, context);
                    break;
                case SiegeEventKind.EnemyHit:
                    particles.Emit(item.Detail == 1 ? FrostSpark : HitSpark, item.Position, 5);
                    if (hitSound <= 0f)
                    {
                        hitSound = HitSoundGap;
                        UiFeedback.Play(UiSound.GameHitSoft);
                    }

                    break;
                case SiegeEventKind.EnemyKilled:
                    OnKilled(item, context);
                    break;
                case SiegeEventKind.PotLost:
                    particles.Emit(PotShards, item.Position - new Vector2(0f, 0.35f), 9);
                    fx.AddText(Loc.T(L.Siege.Clonk), camera.ToScreen(item.Position - new Vector2(0f, 0.6f)), SiegeArt.Pot, 0.9f);
                    UiFeedback.Play(UiSound.GameHitWood);
                    break;
                case SiegeEventKind.Surfaced:
                    particles.Emit(DirtPuff, item.Position + new Vector2(0f, SiegeRenderer.FeetOffset), 14);
                    camera.Shake(0.12f);
                    UiFeedback.Play(UiSound.GameBreak);
                    break;
                case SiegeEventKind.Summoned:
                    fx.Shockwave(camera.ToScreen(item.Position), camera.Px(1.2f), Royal, 0.5f, 3f);
                    particles.Emit(DirtPuff, new Vector2(item.Column + 0.5f, item.Position.Y + SiegeRenderer.FeetOffset), 8);
                    UiFeedback.Play(UiSound.GameMatch);
                    break;
                case SiegeEventKind.SeedFired:
                    particles.Emit(MuzzlePuff, item.Position, 3);
                    if (shotSound <= 0f)
                    {
                        shotSound = ShotSoundGap;
                        UiFeedback.Play(UiSound.GameShoot);
                    }

                    break;
                case SiegeEventKind.DefenderPlanted:
                    particles.Emit(DirtPuff, item.Position + new Vector2(0f, 0.3f), 10);
                    fx.Shockwave(camera.ToScreen(item.Position), camera.Px(0.6f), SiegeArt.Leaf, 0.35f, 2.2f);
                    UiFeedback.Play(UiSound.GameCardPlace);
                    break;
                case SiegeEventKind.DefenderBitten:
                    particles.Emit(Chips, item.Position - new Vector2(0f, 0.3f), 3);
                    if (biteSound <= 0f)
                    {
                        biteSound = BiteSoundGap;
                        UiFeedback.Play(UiSound.GameHitWood);
                    }

                    break;
                case SiegeEventKind.DefenderLost:
                    particles.Burst(item.Position, 14, SiegeArt.Tint((DefenderKind)item.Detail), 3.2f, 0.08f, 0.6f,
                        WorldGravity, MathF.PI * 2f, 0f, ParticleShape.Shard);
                    camera.Shake(0.15f);
                    UiFeedback.Play(UiSound.GameBreak);
                    break;
                case SiegeEventKind.DefenderRemoved:
                    particles.Emit(DirtPuff, item.Position + new Vector2(0f, 0.2f), 12);
                    UiFeedback.Play(UiSound.GameHitSoft);
                    break;
                case SiegeEventKind.BombExploded:
                    OnExploded(item, context);
                    break;
                case SiegeEventKind.SunProduced:
                    particles.Emit(SunSparkle, item.Position, 6);
                    break;
                case SiegeEventKind.MoteCollected:
                    OnCollected(item, scale);
                    break;
                case SiegeEventKind.MoteExpired:
                    particles.Emit(SunSparkle, item.Position, 4);
                    break;
                case SiegeEventKind.GardenHit:
                    OnGardenHit(item, context);
                    break;
                case SiegeEventKind.LevelWon:
                    OnWon(item, context);
                    break;
                case SiegeEventKind.LevelLost:
                    OnLost(context);
                    break;
                default:
                    break;
            }
        }
    }

    private void OnWaveStarted(in SiegeEvent item, in GameContext context)
    {
        if (item.Detail == 1)
        {
            ShowBanner(Loc.T(L.Siege.FinalWave));
            GameSfx.ComboTierUp();
        }
        else
        {
            ShowBanner(board.Endless
                ? bannerLabel.Get(L.Siege.WaveBanner, item.Value)
                : bannerOfLabel.Get(L.Siege.WaveOfBanner, item.Value, board.WaveCount));
            UiFeedback.Play(UiSound.GameTick);
        }

        context.Fx.Sweep();
    }

    private void OnSpawned(in SiegeEvent item, in GameContext context)
    {
        var kind = (EnemyKind)item.Detail;
        if (item.Value == 1 && !board.Endless && SiegeLevels.IntroducesEnemy(board.Level, kind))
        {
            QueueTip(EnemyTip(kind));
        }

        if (kind != EnemyKind.Boss)
        {
            return;
        }

        context.Fx.Vignette(Royal, 0.32f, 1.2f);
        camera.Shake(0.35f);
        ShowBanner(Loc.T(L.Siege.BossArrives));
        UiFeedback.Play(UiSound.GamePowerUp);
    }

    private void OnKilled(in SiegeEvent item, in GameContext context)
    {
        var kind = (EnemyKind)item.Detail;
        var screen = camera.ToScreen(item.Position);
        var size = SiegeArt.Size(kind);
        particles.Emit(LeafBurst, item.Position, kind == EnemyKind.Boss ? 30 : 10);
        particles.Burst(item.Position, kind == EnemyKind.Boss ? 30 : 9, SiegeArt.BodyColor(kind), 3.2f * size, 0.07f * size,
            0.55f, WorldGravity);
        fx.Shockwave(screen, camera.Px(0.6f * size), SiegeArt.Leaf, 0.35f, 2.4f);
        if (kind != EnemyKind.Boss)
        {
            camera.Shake(0.05f);
            UiFeedback.Play(UiSound.GamePop);
            return;
        }

        fx.Shockwave(screen, camera.Px(2.4f), Royal, 0.6f, 4f);
        fx.HitStop(0.08f);
        camera.Shake(0.5f);
        context.Fx.Punch(0.08f);
        context.Fx.Flash(White, 0.25f);
        context.Fx.Sweep();
        UiFeedback.Play(UiSound.GameExplosion);
    }

    private void OnExploded(in SiegeEvent item, in GameContext context)
    {
        var screen = camera.ToScreen(item.Position);
        particles.Emit(Embers, item.Position, 26);
        particles.Emit(Smoke, item.Position, 10);
        fx.Shockwave(screen, camera.Px(1.8f), Fuse, 0.5f, 4f);
        fx.Shockwave(screen, camera.Px(1.1f), White, 0.3f, 2f);
        fx.HitStop(0.05f);
        camera.Shake(0.5f);
        context.Fx.Punch(0.07f);
        context.Fx.Flash(Fuse, 0.28f);
        UiFeedback.Play(UiSound.GameExplosion);
    }

    private void OnCollected(in SiegeEvent item, float scale)
    {
        var screen = camera.ToScreen(item.Position);
        particles.Emit(SunSparkle, item.Position, 10);
        fx.AddText(GameNumber.Signed(item.Value), screen - new Vector2(0f, 14f * scale), SiegeArt.Sun, 1f);
        UiFeedback.Play(UiSound.GameCollect);
        for (var index = 0; index < FlightCapacity; index++)
        {
            if (flights[index].Active)
            {
                continue;
            }

            flights[index] = new Flight(screen, item.Value);
            return;
        }
    }

    private void OnGardenHit(in SiegeEvent item, in GameContext context)
    {
        if (item.Column < fenceFlash.Length)
        {
            fenceFlash[item.Column] = 1f;
        }

        particles.Emit(Petals, new Vector2(item.Column + 0.5f, SiegeRenderer.FenceTop + 0.1f), 14);
        fx.AddText(GameNumber.Signed(-item.Value), camera.ToScreen(item.Position - new Vector2(0f, 0.3f)), Danger, 1.2f);
        context.Fx.Vignette(Danger, 0.55f, 0.8f);
        context.Fx.Flash(Danger, 0.16f);
        context.Fx.Punch(0.05f);
        camera.Shake(0.45f);
        UiFeedback.Play(UiSound.GameWrong);
    }

    private void OnWon(in SiegeEvent item, in GameContext context)
    {
        if (!lastStand)
        {
            lastStand = true;
            context.Fx.SlowMo(LastStandFactor, LastStandSeconds);
        }

        particles.Confetti(item.Position, 60, CelebrationPalette, 6f, 0.08f, 1.4f, WorldGravity);
        fx.Shockwave(camera.ToScreen(item.Position), camera.Px(2f), SiegeArt.Sun, 0.6f, 3f);
        context.Fx.Sweep();
        context.Fx.Punch(0.06f);
        GameSfx.LevelClear();
    }

    private void OnLost(in GameContext context)
    {
        for (var column = 0; column < SiegeRules.Columns; column++)
        {
            fenceFlash[column] = 1f;
            particles.Emit(Petals, new Vector2(column + 0.5f, SiegeRenderer.FenceTop + 0.1f), 8);
        }

        context.Fx.SlowMo(0.5f, 0.6f);
        context.Fx.Vignette(Danger, 0.8f, 1.6f);
        context.Fx.Punch(0.08f);
        camera.Shake(0.7f);
        UiFeedback.Play(UiSound.GameBreak);
    }

    private void WatchLastStand(in GameContext context)
    {
        if (lastStand || board.Endless || !board.FinalWave || board.Phase != SiegePhase.Clearing ||
            board.AliveEnemies != 1)
        {
            return;
        }

        for (var index = 0; index < SiegeBoard.EnemyCapacity; index++)
        {
            ref readonly var enemy = ref board.Enemy(index);
            if (!enemy.Alive || enemy.HealthFraction > LastStandShare)
            {
                continue;
            }

            lastStand = true;
            context.Fx.SlowMo(LastStandFactor, LastStandSeconds);
            return;
        }
    }

    private void AdvanceEnd(in GameContext context)
    {
        if (!board.Over)
        {
            return;
        }

        if (!ending)
        {
            ending = true;
            endTimer = EndDelaySeconds;
            selected = SiegeTray.NoSlot;
            CancelGesture();
        }

        if (context.Session.State == StageFlow.Playing)
        {
            endTimer -= context.RawDeltaSeconds;
        }

        if (endTimer > 0f)
        {
            return;
        }

        finished = true;
        context.Session.Finish(BuildOutcome());
    }

    private GameOutcome BuildOutcome()
    {
        var defeated = GameNumber.Label(board.Defeated);
        var planted = GameNumber.Label(board.Planted);
        var gathered = GameNumber.Label(board.SunGathered);
        if (mode == EndlessMode)
        {
            return new GameOutcome(board.WavesSurvived, ScoreKind.Level, EndlessStatId, false)
                .WithStat(L.Siege.WavesSurvived, GameNumber.Label(board.WavesSurvived))
                .WithStat(L.Siege.Defeated, defeated)
                .WithStat(L.Siege.Planted, planted)
                .WithStat(L.Siege.Gathered, gathered);
        }

        var stars = board.Stars;
        return new GameOutcome(stars, ScoreKind.Level, GameId, board.Won)
            .WithStars(stars)
            .WithStat(L.Siege.Garden, Loc.T(L.Stage.StarsOf, GameNumber.Label(board.Health),
                GameNumber.Label(SiegeRules.GardenHealth)))
            .WithStat(L.Siege.Defeated, defeated)
            .WithStat(L.Siege.Planted, planted)
            .WithStat(L.Siege.Gathered, gathered);
    }

    private void AdvanceCosmetics(float deltaSeconds, float flowSeconds)
    {
        shotSound -= deltaSeconds;
        hitSound -= deltaSeconds;
        biteSound -= deltaSeconds;
        bannerProgress = GameBanner.Advance(bannerProgress, flowSeconds, WaveBannerSeconds);
        for (var column = 0; column < fenceFlash.Length; column++)
        {
            fenceFlash[column] = MathF.Max(0f, fenceFlash[column] - deltaSeconds * FenceFlashDecay);
        }

        for (var index = 0; index < FlightCapacity; index++)
        {
            if (!flights[index].Active)
            {
                continue;
            }

            flights[index].Age += deltaSeconds;
            if (flights[index].Age >= FlightSeconds)
            {
                flights[index].Active = false;
            }
        }

        if (tipProgress < 1f)
        {
            tipProgress = GameBanner.Advance(tipProgress, flowSeconds, TipSeconds);
            return;
        }

        if (tipCount == 0)
        {
            currentTip = null;
            return;
        }

        currentTip = tips[tipHead];
        tips[tipHead] = null;
        tipHead = (tipHead + 1) % TipCapacity;
        tipCount--;
        tipProgress = 0f;
    }

    private void QueueOpeningTips()
    {
        if (mode == EndlessMode)
        {
            QueueTip(L.Siege.TipEndless);
            return;
        }

        if (board.Level == 1)
        {
            QueueTip(L.Siege.TipPlant);
        }

        var introduced = SiegeLevels.DefenderIntroducedAt(board.Level);
        if (introduced != DefenderKind.None)
        {
            QueueTip(DefenderTip(introduced));
        }
    }

    private void QueueTip(LocString tip)
    {
        if (tipCount >= TipCapacity)
        {
            return;
        }

        tips[(tipHead + tipCount) % TipCapacity] = tip;
        tipCount++;
    }

    private void ShowBanner(string text)
    {
        bannerText = text;
        bannerProgress = 0f;
    }

    private void ClearTrails()
    {
        for (var index = 0; index < trails.Length; index++)
        {
            trails[index].Clear();
            trailOwners[index] = -1;
        }
    }

    private void UpdateTrails(SiegeBoard source)
    {
        for (var index = 0; index < SiegeBoard.SeedCapacity; index++)
        {
            ref readonly var seed = ref source.Seed(index);
            var trail = trails[index];
            if (!seed.Alive)
            {
                if (trail.Count > 0)
                {
                    trail.Clear();
                }

                trailOwners[index] = -1;
                continue;
            }

            if (trailOwners[index] != seed.Id)
            {
                trail.Clear();
                trailOwners[index] = seed.Id;
            }

            var head = new Vector2(seed.Column + 0.5f, seed.Y);
            if (trail.Count == 0 || Vector2.Distance(trail.Point(0), head) >= TrailStep)
            {
                trail.Push(head);
            }
        }
    }

    private void DrawScene(ImDrawListPtr drawList, SiegeBoard source, in GameContext context, float scale, bool live)
    {
        renderer.DrawGarden(drawList, in camera, entrance, scale, Accent, context.Backdrop.Ink, fenceFlash);
        if (live)
        {
            DrawPlacement(drawList, scale);
        }

        SiegeRenderer.DrawDefenders(drawList, in camera, source, time, 1f);
        var trailWidth = camera.Px(TrailWidth);
        for (var index = 0; index < SiegeBoard.SeedCapacity; index++)
        {
            ref readonly var seed = ref source.Seed(index);
            if (!seed.Alive)
            {
                continue;
            }

            var color = seed.Frost ? SiegeArt.Frost : SiegeArt.SeedGreen;
            trails[index].Draw(drawList, in camera, color with { W = 0.55f }, trailWidth, additive: true);
        }

        SiegeRenderer.DrawSeeds(drawList, in camera, source, 1f);
        renderer.DrawEnemies(drawList, in camera, source, time, 1f);
        particles.Draw(drawList, in camera);
        SiegeRenderer.DrawMotes(drawList, in camera, source, time, 1f);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
    }

    private void DrawPlacement(ImDrawListPtr drawList, float scale)
    {
        if (selected == SiegeTray.NoSlot || hoverColumn == NoCell)
        {
            return;
        }

        if (selected == SiegeTray.ShovelSlot)
        {
            SiegeRenderer.DrawPlacement(drawList, in camera, hoverColumn, hoverRow,
                board.DefenderAt(hoverColumn, hoverRow).Occupied, DefenderKind.None, time, scale);
            return;
        }

        var kind = SiegeTray.KindOf(selected);
        var valid = board.CanPlant(kind, hoverColumn, hoverRow) == PlantResult.Planted;
        SiegeRenderer.DrawPlacement(drawList, in camera, hoverColumn, hoverRow, valid, kind, time, scale);
    }

    private void DrawDragGhost(ImDrawListPtr drawList)
    {
        if (!dragging || pressSlot == SiegeTray.NoSlot)
        {
            return;
        }

        var mouse = ImGui.GetMousePos();
        var unit = camera.Px(1.05f);
        if (pressSlot == SiegeTray.ShovelSlot)
        {
            SiegeArt.Shovel(drawList, mouse, unit, 0.95f);
            return;
        }

        ProgressRing.Glow(mouse, unit * 0.45f, Accent, 0.5f);
        SiegeArt.Card(drawList, SiegeTray.KindOf(pressSlot), mouse, unit, time, 0.9f);
    }

    private void DrawSelectionCaption(ImDrawListPtr drawList, float scale)
    {
        if (selected == SiegeTray.NoSlot)
        {
            return;
        }

        var text = Loc.T(selected == SiegeTray.ShovelSlot ? L.Siege.ShovelHint : DefenderName(SiegeTray.KindOf(selected)));
        var area = tray.Area;
        var maxWidth = area.Width - TipPadX * 2f * scale;
        var fitted = Typography.FitText(text, maxWidth, CapsuleStyle);
        var size = Typography.Measure(fitted, CapsuleStyle);
        var center = new Vector2(area.Center.X, area.Min.Y - CaptionGap * scale);
        var half = new Vector2(size.X * 0.5f + TipPadX * scale, size.Y * 0.5f + 5f * scale);
        StageHud.Capsule(drawList, new Rect(center - half, center + half), scale);
        Typography.DrawCentered(drawList, center, fitted, GamePalette.InkLight, CapsuleStyle);
    }

    private void DrawBanner(ImDrawListPtr drawList, in GameContext context)
    {
        if (bannerProgress >= 1f)
        {
            return;
        }

        GameBanner.Draw(drawList, camera.ToScreen(new Vector2(SiegeRules.Columns * 0.5f, BannerRow)), bannerText, Accent,
            context.Theme, bannerProgress);
    }

    private void DrawTip(ImDrawListPtr drawList, Rect view, float scale, Vector4 accent)
    {
        if (currentTip is null || tipProgress >= 1f)
        {
            return;
        }

        var alpha = tipProgress < 0.1f ? tipProgress / 0.1f : tipProgress > 0.85f ? (1f - tipProgress) / 0.15f : 1f;
        var slide = (1f - Math.Clamp(tipProgress / 0.1f, 0f, 1f)) * 10f * scale;
        var iconSize = TipIconSize * scale;
        var padding = TipPadX * scale;
        var maxText = MathF.Max(1f, view.Width - padding * 3f - iconSize);
        var text = Loc.T(currentTip.Value);
        var block = Typography.MeasureWrappedBlock(text, TipStyle, maxText);
        var width = block.X + iconSize + padding * 2.5f;
        var baseHeight = TipHeight * scale;
        var height = MathF.Max(baseHeight, block.Y + 10f * scale);
        var radius = baseHeight * 0.5f;
        var center = new Vector2(view.Center.X, view.Min.Y + TipTop * scale + (height - baseHeight) * 0.5f - slide);
        var min = center - new Vector2(width * 0.5f, height * 0.5f);
        var max = center + new Vector2(width * 0.5f, height * 0.5f);
        Material.Frosted(drawList, min, max, radius, scale, 0.94f * alpha);
        Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(accent with { W = 0.7f * alpha }), 1.5f * scale);
        ProgressRing.CenterIcon(drawList, new Vector2(min.X + padding + iconSize * 0.5f, center.Y), FontAwesomeIcon.Leaf,
            SiegeArt.Leaf with { W = alpha }, iconSize);
        Typography.DrawWrappedCentered(drawList, new Vector2(min.X + padding * 1.5f + iconSize + block.X * 0.5f, center.Y),
            text, GamePalette.InkLight with { W = alpha }, TipStyle, maxText);
    }

    private void DrawFlights(ImDrawListPtr drawList, in GameContext context, float scale)
    {
        var target = StageLayout.PrimaryCenter(context.Full, scale);
        var radius = camera.Px(SiegeRenderer.MoteRadius);
        for (var index = 0; index < FlightCapacity; index++)
        {
            ref readonly var flight = ref flights[index];
            if (!flight.Active)
            {
                continue;
            }

            var progress = Math.Clamp(flight.Age / FlightSeconds, 0f, 1f);
            var eased = progress * progress * (3f - 2f * progress);
            var control = new Vector2(flight.From.X, target.Y + (flight.From.Y - target.Y) * 0.2f);
            var inverse = 1f - eased;
            var position = flight.From * inverse * inverse + control * 2f * inverse * eased + target * eased * eased;
            SiegeArt.Mote(drawList, position, radius * (1f - 0.45f * progress), time + index, 1f);
        }
    }

    private void FillHud(ImDrawListPtr drawList, in GameContext context, float scale, Vector4 accent)
    {
        var hud = context.Hud;
        hud.Score(DisplayedSunlight(), L.Siege.Sunlight);
        hud.Lives(board.Health, SiegeRules.GardenHealth);
        if (mode == CampaignMode)
        {
            hud.Level(board.Level);
        }
        else
        {
            hud.Best(context.Session.Best);
        }

        var label = WaveLabel();
        hud.Custom(CapsulePadX * 2f + CapsuleIconSize + CapsuleIconGap + Typography.Measure(label, CapsuleStyle).X / scale);
        if (board.Health == 1 && !board.Over)
        {
            context.Fx.Vignette(Danger, 0.1f + 0.1f * Pulse.Wave(Pulse.Fast), 0.3f);
        }

        if (!hud.CustomPlaced(0))
        {
            return;
        }

        var rect = hud.CustomRect(0);
        StageHud.Capsule(drawList, rect, scale);
        var iconSize = CapsuleIconSize * scale;
        var left = rect.Min.X + CapsulePadX * scale;
        var waiting = board.Phase is SiegePhase.Prelude or SiegePhase.Break;
        var barColor = waiting ? SiegeArt.Sun : accent;
        ProgressRing.CenterIcon(drawList, new Vector2(left + iconSize * 0.5f, rect.Center.Y), FontAwesomeIcon.Flag, barColor,
            iconSize);
        Typography.Draw(drawList,
            new Vector2(left + iconSize + CapsuleIconGap * scale, rect.Center.Y - Typography.LineHeight(CapsuleStyle) * 0.5f),
            label, context.Theme.TextStrong, CapsuleStyle);
        StageHud.Bar(drawList, rect, WaveBarFraction(), barColor, scale);
    }

    private string WaveLabel()
    {
        var current = board.Wave + 1;
        return board.Endless
            ? GameNumber.Label(current)
            : waveOfLabel.Get(L.Stage.StarsOf, current, board.WaveCount);
    }

    private float WaveBarFraction()
    {
        switch (board.Phase)
        {
            case SiegePhase.Prelude:
                return 1f - Math.Clamp(board.PhaseTimer / SiegeRules.PreludeSeconds, 0f, 1f);
            case SiegePhase.Break:
                return 1f - Math.Clamp(board.PhaseTimer / SiegeRules.BreakSeconds, 0f, 1f);
            case SiegePhase.Spawning:
                return board.WaveProgress;
            default:
                return 1f;
        }
    }

    private int DisplayedSunlight()
    {
        var landing = 0;
        for (var index = 0; index < FlightCapacity; index++)
        {
            if (flights[index].Active)
            {
                landing += flights[index].Value;
            }
        }

        return Math.Max(0, board.Sunlight - landing);
    }

    private static LocString DefenderName(DefenderKind kind) => kind switch
    {
        DefenderKind.Sunbloom => L.Siege.Sunbloom,
        DefenderKind.Thornwall => L.Siege.Thornwall,
        DefenderKind.Frostbud => L.Siege.Frostbud,
        DefenderKind.Bombcap => L.Siege.Bombcap,
        _ => L.Siege.Sprout,
    };

    private static LocString DefenderTip(DefenderKind kind) => kind switch
    {
        DefenderKind.Sunbloom => L.Siege.TipSunbloom,
        DefenderKind.Thornwall => L.Siege.TipThornwall,
        DefenderKind.Frostbud => L.Siege.TipFrostbud,
        DefenderKind.Bombcap => L.Siege.TipBombcap,
        _ => L.Siege.TipSprout,
    };

    private static LocString EnemyTip(EnemyKind kind) => kind switch
    {
        EnemyKind.Runner => L.Siege.TipRunner,
        EnemyKind.Armoured => L.Siege.TipArmoured,
        EnemyKind.Flyer => L.Siege.TipFlyer,
        EnemyKind.Digger => L.Siege.TipDigger,
        EnemyKind.Boss => L.Siege.TipBoss,
        _ => L.Siege.TipWalker,
    };

    private struct Flight
    {
        public Vector2 From;
        public float Age;
        public int Value;
        public bool Active;

        public Flight(Vector2 from, int value)
        {
            From = from;
            Age = 0f;
            Value = value;
            Active = true;
        }
    }
}
