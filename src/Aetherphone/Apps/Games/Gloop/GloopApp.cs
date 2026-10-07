using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Gloop;

internal sealed class GloopApp : IMiniGame
{
    internal const string VersusStatId = "gloop.versus";
    private const string GameId = "gloop";
    private const string SurfaceId = "gloop.well";
    private const string Separator = " · ";
    private const int VersusMode = 1;
    private const int HardRivalStreak = 3;
    private const float RivalScale = 0.42f;
    private const float SideGap = 0.35f;
    private const float EndlessSide = 1.5f;
    private const float IncomingBand = 0.6f;
    private const float MaxCell = 46f;
    private const int PiecesPerLevel = 18;
    private const int MaxLevel = 15;
    private const float EndlessGravity = 0.85f;
    private const float EndlessGravityStep = 0.42f;
    private const float VersusGravity = 1f;
    private const float VersusGravityPerSecond = 0.02f;
    private const float MaxGravity = 9f;
    private const float IdleGravity = 3f;
    private const int PunchChain = 3;
    private const int SlowMoChain = 4;
    private const int FlashChain = 5;
    private const int DangerHeight = 10;
    private const float FinishDelay = 1.2f;
    private const float CollapseSeconds = 1f;
    private const float OrbSeconds = 0.55f;
    private const int MaxOrbs = 6;
    private const float SmoothRate = 20f;
    private const float TapSeconds = 0.25f;
    private const float DeadZoneFraction = 0.35f;
    private const float FlickMinCells = 1.2f;
    private const float FlickCellsPerSecond = 9f;
    private const float BannerSeconds = 1.6f;
    private const ulong IdleSeed = 31;
    private const ulong GarbageSalt = 0x9E3779B97F4A7C15UL;
    private const ulong RivalGarbageSalt = 0xC2B2AE3D27D4EB4FUL;
    private const ulong BotSalt = 0x165667B19E3779F9UL;
    private static readonly LocString[] Modes = { L.Gloop.Endless, L.Gloop.Versus };
    private static readonly string[] ModeStatIds = { GameId, VersusStatId };
    private static readonly ScoreKind[] ModeKinds = { ScoreKind.Score, ScoreKind.Streak };
    private static readonly GameSpec StageSpec = new(GameId, L.Gloop.Title, GameGenre.Puzzle, L.Gloop.Hook,
        Backdrop.Cavern, HudStyle.Standard, ScoreKind.Score, Modes, ModeStatIds, clocked: true, countdown: true,
        keyboard: true, modeKinds: ModeKinds);
    private static readonly Vector4 Danger = new(0.95f, 0.32f, 0.34f, 1f);
    private static readonly Vector4 Gold = new(1f, 0.85f, 0.38f, 1f);
    private static readonly Vector4 Dust = new(0.82f, 0.78f, 0.88f, 0.6f);
    private static readonly Vector4 Muted = new(1f, 1f, 1f, 0.62f);
    private static readonly Vector4[] ClearPalette =
    {
        new(0.96f, 0.38f, 0.44f, 1f), new(0.42f, 0.86f, 0.46f, 1f), new(0.38f, 0.62f, 0.99f, 1f),
        new(0.99f, 0.82f, 0.30f, 1f),
    };

    private struct Wells
    {
        public Rect Player;
        public float Cell;
        public Rect Rival;
        public float RivalCell;
        public Rect Side;
        public Rect Plate;
    }

    private struct Orb
    {
        public bool Active;
        public bool ToRival;
        public int Count;
        public Vector2 From;
        public Vector2 To;
        public float Progress;
    }

    private readonly GloopBoard player = new();
    private readonly GloopBoard rival = new();
    private readonly GloopBot bot = new();
    private readonly GloopBoard idleBoard = new();
    private readonly GloopBot idleBot = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly Orb[] orbs = new Orb[MaxOrbs];
    private readonly Ribbon[] orbTrails = new Ribbon[MaxOrbs];
    private ComboMeter combo = ComboMeter.Create();
    private LabelSlot chainLabel;
    private LabelSlot levelLabel;
    private LanguageInfo? rivalLanguage;
    private BotSkill rivalLabelSkill;
    private string rivalLabel = string.Empty;
    private Wells wells;
    private GameRandom botRandom;
    private bool versus;
    private bool rivalPending;
    private int level = 1;
    private float matchSeconds;
    private float pieceColumn;
    private float pieceAngle;
    private int pieceSeen = -1;
    private float rivalColumn;
    private float rivalAngle;
    private int rivalSeen = -1;
    private float idleColumn;
    private float idleAngle;
    private int idleSeen = -1;
    private int idleRuns;
    private float playerCollapse;
    private float rivalCollapse;
    private bool finishPending;
    private bool playerWon;
    private float finishTimer;
    private bool finished;
    private float banner = 1f;
    private string bannerText = string.Empty;
    private Vector2 gestureStart;
    private float gestureLastY;
    private float gestureSeconds;
    private int dragColumns;
    private bool gestureActive;
    private bool gestureMoved;
    private bool gestureConsumed;
    private bool gestureSoft;

    public GloopApp()
    {
        for (var index = 0; index < MaxOrbs; index++)
        {
            orbTrails[index] = new Ribbon();
        }

        ResetIdle();
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        versus = start.Mode == VersusMode;
        var pieces = start.Random;
        player.Reset(pieces, GameRandom.FromSeed(start.Seed ^ GarbageSalt), versus);
        rival.Reset(pieces, GameRandom.FromSeed(start.Seed ^ RivalGarbageSalt), true);
        botRandom = GameRandom.FromSeed(start.Seed ^ BotSalt);
        rivalPending = versus;
        particles.Clear();
        fx.Clear();
        combo.Reset();
        for (var index = 0; index < MaxOrbs; index++)
        {
            orbs[index].Active = false;
            orbTrails[index].Clear();
        }

        level = 1;
        matchSeconds = 0f;
        pieceSeen = -1;
        rivalSeen = -1;
        playerCollapse = 0f;
        rivalCollapse = 0f;
        finishPending = false;
        playerWon = false;
        finishTimer = 0f;
        finished = false;
        banner = 1f;
        gestureActive = false;
        gestureSoft = false;
    }

    public void Close()
    {
        particles.Clear();
        fx.Clear();
        gestureActive = false;
    }

    public void Dispose()
    {
    }

    public void OnQuit(GameSession session)
    {
        if (finished)
        {
            return;
        }

        if (finishPending)
        {
            finished = true;
            finishPending = false;
            session.Finish(versus ? VersusOutcome(playerWon) : EndlessOutcome());
            return;
        }

        if (!versus && player.Score > 0)
        {
            finished = true;
            session.Finish(EndlessOutcome());
            return;
        }

        if (versus && player.Pieces > 0)
        {
            finished = true;
            session.Finish(VersusOutcome(false));
        }
    }

    public void DrawIdle(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var raw = context.RawDeltaSeconds;
        idleBoard.Gravity = IdleGravity;
        idleBot.Drive(idleBoard, raw);
        idleBoard.Step(raw);
        if (idleBoard.Over)
        {
            ResetIdle();
        }

        Layout(context.Safe, false, scale, out var layout);
        Smooth(idleBoard, ref idleColumn, ref idleAngle, ref idleSeen, raw);
        BoardPlate.Draw(drawList, BoardPlate.Around(layout.Plate, scale), BoardPlate.Radius * scale, scale, Accent,
            context.Backdrop.Ink);
        DrawWell(drawList, idleBoard, layout.Player, layout.Cell, idleColumn, idleAngle, 0f, true, scale);
        DrawNext(drawList, idleBoard, layout.Side, layout.Cell, scale);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var session = context.Session;
        var raw = context.RawDeltaSeconds;
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        particles.Update(raw);
        fx.Update(raw);
        combo.Update(simDelta);
        banner = GameBanner.Advance(banner, raw, BannerSeconds);
        if (rivalPending)
        {
            rivalPending = false;
            bot.Reset(botRandom, session.Best >= HardRivalStreak ? BotSkill.Hard : BotSkill.Easy);
        }

        var area = StageLayout.Punched(context.Safe, context.Fx.PlateScale).Translate(fx.ShakeOffset(scale));
        Layout(area, versus, scale, out wells);
        var playing = session.State == StageFlow.Playing && !finished && !finishPending;
        if (playing)
        {
            HandleInput(raw, scale);
        }
        else
        {
            player.SoftDrop(false);
            gestureActive = false;
        }

        if (!finished && !finishPending)
        {
            StepBoards(simDelta, context, scale);
        }

        AdvanceOrbs(raw, scale);
        if (finishPending && session.State == StageFlow.Playing)
        {
            AdvanceFinish(raw, context);
        }

        DrawScene(drawList, context, raw, scale);
        DrawHud(context);
    }

    private void ResetIdle()
    {
        idleRuns++;
        idleBoard.Reset(GameRandom.FromSeed(IdleSeed + (ulong)idleRuns), GameRandom.FromSeed(IdleSeed), false);
        idleBot.Reset(GameRandom.FromSeed(IdleSeed ^ BotSalt), BotSkill.Hard);
        idleSeen = -1;
    }

    private static void Layout(Rect area, bool versusLayout, float scale, out Wells layout)
    {
        var inner = area.Inset(BoardPlate.Padding * scale);
        var sideUnits = versusLayout ? GloopRules.Columns * RivalScale : EndlessSide;
        var band = versusLayout ? IncomingBand : 0f;
        var widthUnits = GloopRules.Columns + SideGap + sideUnits;
        var heightUnits = GloopRules.VisibleRows + band;
        var cell = MathF.Min(MathF.Min(inner.Width / widthUnits, inner.Height / heightUnits), MaxCell * scale);
        cell = MathF.Max(1f, cell);
        var size = new Vector2(widthUnits, heightUnits) * cell;
        var topLeft = inner.Center - size * 0.5f;
        var wellTop = topLeft.Y + band * cell;
        layout.Cell = cell;
        layout.Player = new Rect(new Vector2(topLeft.X, wellTop),
            new Vector2(topLeft.X + GloopRules.Columns * cell, wellTop + GloopRules.VisibleRows * cell));
        var sideLeft = layout.Player.Max.X + SideGap * cell;
        layout.Plate = new Rect(topLeft, topLeft + size);
        layout.RivalCell = cell * RivalScale;
        var rivalSize = new Vector2(GloopRules.Columns, GloopRules.VisibleRows) * layout.RivalCell;
        var rivalMin = new Vector2(sideLeft, wellTop);
        layout.Rival = versusLayout ? new Rect(rivalMin, rivalMin + rivalSize) : default;
        var sideTop = versusLayout ? layout.Rival.Max.Y + cell * 0.9f : wellTop;
        layout.Side = new Rect(new Vector2(sideLeft, sideTop),
            new Vector2(sideLeft + sideUnits * cell, layout.Player.Max.Y));
    }

    private void HandleInput(float rawDelta, float scale)
    {
        if (GameInput.Pressed(ImGuiKey.A, ImGuiKey.LeftArrow, true))
        {
            player.Shift(-1);
        }

        if (GameInput.Pressed(ImGuiKey.D, ImGuiKey.RightArrow, true))
        {
            player.Shift(1);
        }

        if (GameInput.Pressed(ImGuiKey.W, ImGuiKey.UpArrow) || GameInput.Pressed(ImGuiKey.X))
        {
            Rotate(1);
        }

        if (GameInput.Pressed(ImGuiKey.Z, ImGuiKey.Q))
        {
            Rotate(-1);
        }

        if (GameInput.Pressed(ImGuiKey.Space))
        {
            player.HardDrop();
        }

        HandleGestures(rawDelta, scale);
        player.SoftDrop(GameInput.Held(ImGuiKey.S, ImGuiKey.DownArrow) || gestureSoft);
    }

    private void Rotate(int direction)
    {
        if (player.Rotate(direction))
        {
            UiFeedback.Play(UiSound.GamePiece);
        }
    }

    private void HandleGestures(float rawDelta, float scale)
    {
        var hovered = PressSurface.Claim(SurfaceId, wells.Player, out var activated);
        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            Rotate(-1);
        }

        var mouse = ImGui.GetMousePos();
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            ReleaseGesture();
            return;
        }

        if (!gestureActive)
        {
            if (!activated)
            {
                return;
            }

            gestureActive = true;
            gestureStart = mouse;
            gestureLastY = mouse.Y;
            gestureSeconds = 0f;
            dragColumns = 0;
            gestureMoved = false;
            gestureConsumed = false;
            gestureSoft = false;
            return;
        }

        gestureSeconds += rawDelta;
        var verticalSpeed = (mouse.Y - gestureLastY) / MathF.Max(rawDelta, 0.0001f);
        gestureLastY = mouse.Y;
        if (gestureConsumed)
        {
            return;
        }

        var delta = mouse - gestureStart;
        var pitch = wells.Cell;
        var deadZone = pitch * DeadZoneFraction;
        var targetColumns = StepsFor(delta.X, pitch, deadZone);
        while (dragColumns != targetColumns)
        {
            var step = Math.Sign(targetColumns - dragColumns);
            if (!player.Shift(step))
            {
                dragColumns = targetColumns;
                break;
            }

            dragColumns += step;
            gestureMoved = true;
        }

        var flicked = delta.Y > FlickMinCells * pitch && delta.Y > MathF.Abs(delta.X) &&
                      verticalSpeed > FlickCellsPerSecond * pitch;
        if (flicked)
        {
            player.HardDrop();
            gestureMoved = true;
            gestureConsumed = true;
            gestureSoft = false;
            return;
        }

        gestureSoft = delta.Y > deadZone && delta.Y > MathF.Abs(delta.X);
        if (gestureSoft)
        {
            gestureMoved = true;
        }
    }

    private void ReleaseGesture()
    {
        gestureSoft = false;
        if (!gestureActive)
        {
            return;
        }

        gestureActive = false;
        if (gestureConsumed || gestureMoved || gestureSeconds >= TapSeconds)
        {
            return;
        }

        Rotate(1);
    }

    private static int StepsFor(float distance, float pitch, float deadZone)
    {
        var magnitude = MathF.Abs(distance);
        if (magnitude <= deadZone)
        {
            return 0;
        }

        var steps = (int)((magnitude - deadZone) / pitch) + 1;
        return distance < 0f ? -steps : steps;
    }

    private void StepBoards(float deltaSeconds, in GameContext context, float scale)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        matchSeconds += deltaSeconds;
        if (versus)
        {
            var gravity = MathF.Min(MaxGravity, VersusGravity + matchSeconds * VersusGravityPerSecond);
            player.Gravity = gravity;
            rival.Gravity = gravity;
        }
        else
        {
            level = Math.Min(MaxLevel, 1 + player.Pieces / PiecesPerLevel);
            player.Gravity = MathF.Min(MaxGravity, EndlessGravity + EndlessGravityStep * (level - 1));
        }

        var levelBefore = level;
        player.Step(deltaSeconds);
        OnPlayerEvents(context, scale);
        if (!versus)
        {
            level = Math.Min(MaxLevel, 1 + player.Pieces / PiecesPerLevel);
            if (level > levelBefore)
            {
                OnLevelUp(context, scale);
            }
        }
        else
        {
            bot.Drive(rival, deltaSeconds);
            rival.Step(deltaSeconds);
            OnRivalEvents(scale);
            Exchange();
        }

        if (player.DiedNow)
        {
            OnPlayerDied(context, scale);
            return;
        }

        if (versus && rival.DiedNow)
        {
            OnRivalDied(context, scale);
        }
    }

    private void Exchange()
    {
        var toRival = player.TakeOutgoing();
        if (toRival > 0)
        {
            rival.AddIncoming(toRival);
            LaunchOrb(true, toRival);
        }

        var toPlayer = rival.TakeOutgoing();
        if (toPlayer <= 0)
        {
            return;
        }

        player.AddIncoming(toPlayer);
        LaunchOrb(false, toPlayer);
    }

    private void LaunchOrb(bool toRival, int count)
    {
        var playerTop = new Vector2(wells.Player.Center.X, wells.Player.Min.Y + wells.Cell * 2f);
        var rivalTop = new Vector2(wells.Rival.Center.X, wells.Rival.Min.Y + wells.RivalCell * 2f);
        var playerEdge = new Vector2(wells.Player.Center.X, wells.Player.Min.Y);
        var rivalEdge = new Vector2(wells.Rival.Center.X, wells.Rival.Min.Y);
        for (var index = 0; index < MaxOrbs; index++)
        {
            ref var orb = ref orbs[index];
            if (orb.Active)
            {
                continue;
            }

            orb.Active = true;
            orb.ToRival = toRival;
            orb.Count = count;
            orb.From = toRival ? playerTop : rivalTop;
            orb.To = toRival ? rivalEdge : playerEdge;
            orb.Progress = 0f;
            orbTrails[index].Clear();
            UiFeedback.Play(UiSound.GameShoot);
            return;
        }
    }

    private void AdvanceOrbs(float deltaSeconds, float scale)
    {
        for (var index = 0; index < MaxOrbs; index++)
        {
            ref var orb = ref orbs[index];
            if (!orb.Active)
            {
                continue;
            }

            orb.Progress += deltaSeconds / OrbSeconds;
            if (orb.Progress < 1f)
            {
                continue;
            }

            orb.Active = false;
            orbTrails[index].Clear();
            fx.Shockwave(orb.To, (orb.ToRival ? 20f : 34f) * scale, Danger, 0.4f, 2.6f);
            particles.Burst(orb.To, orb.ToRival ? 6 : 12, Dust, 140f * scale, 2.6f, 0.45f, 200f);
            if (!orb.ToRival)
            {
                fx.AddTrauma(0.12f);
                UiFeedback.Play(UiSound.GameHitWood);
            }
        }
    }

    private void OnPlayerEvents(in GameContext context, float scale)
    {
        var cell = wells.Cell;
        var well = wells.Player;
        if (player.LockedNow)
        {
            UiFeedback.Play(UiSound.GameHitSoft);
            fx.AddTrauma(0.02f);
        }

        if (player.LandedCount > 0)
        {
            for (var index = 0; index < player.LandedCount; index++)
            {
                var center = GloopRenderer.CenterOf(well, cell, player.LandedCell(index));
                particles.Burst(center + new Vector2(0f, cell * 0.4f), 2, Dust, 60f * scale, 2.2f, 0.3f, 120f,
                    MathF.PI * 0.8f, -MathF.PI * 0.5f);
            }

            UiFeedback.Play(UiSound.GameHitSoft);
        }

        if (player.PoppedCount > 0)
        {
            OnPlayerPop(context, scale);
        }

        if (player.ClearedCount > 0)
        {
            OnPlayerCleared(scale);
        }

        if (player.ChainEnded >= SlowMoChain)
        {
            context.Fx.Sweep();
        }

        if (player.AllClearNow)
        {
            UiFeedback.Play(UiSound.GamePowerUp);
            GameSfx.LevelClear();
            particles.Confetti(well.Center, 90, ClearPalette, 300f * scale, 4.2f, 1.5f);
            context.Fx.Sweep();
            context.Fx.Flash(Gold, 0.25f);
            ShowBanner(Loc.T(L.Gloop.AllClear));
        }

        if (player.GarbageDropped > 0)
        {
            UiFeedback.Play(UiSound.GameHitWood);
            fx.AddTrauma(MathF.Min(0.5f, 0.15f + player.GarbageDropped / 60f));
            context.Fx.Punch(0.03f);
        }

        if (GloopRules.Height(player.Grid, GloopRules.SpawnColumn) >= DangerHeight)
        {
            context.Fx.Vignette(Danger, 0.10f + 0.12f * Pulse.Wave(Pulse.Fast), 0.3f);
        }
    }

    private void OnPlayerCleared(float scale)
    {
        var cell = wells.Cell;
        for (var index = 0; index < player.ClearedCount; index++)
        {
            var center = GloopRenderer.CenterOf(wells.Player, cell, player.PoppedCell(index));
            var color = GloopRenderer.ColorOf(player.PoppedColor(index));
            particles.Emit(new ParticleSpec(GamePalette.Lighten(color, 0.25f), color with { W = 0f }, 4f * scale,
                230f * scale, 0.6f, 380f * scale, 1.4f, 10f, shape: ParticleShape.Shard), center, 6);
            particles.Burst(center, 3, color, 120f * scale, 3f, 0.4f, 260f);
        }

        UiFeedback.Play(UiSound.GamePop);
        fx.AddTrauma(0.08f);
    }

    private void OnPlayerPop(in GameContext context, float scale)
    {
        var cell = wells.Cell;
        var well = wells.Player;
        var centroid = Vector2.Zero;
        for (var index = 0; index < player.PoppedCount; index++)
        {
            var center = GloopRenderer.CenterOf(well, cell, player.PoppedCell(index));
            centroid += center;
            particles.Sparkle(center, 1, GamePalette.Lighten(GloopRenderer.ColorOf(player.PoppedColor(index)), 0.5f),
                90f * scale, 2.2f, 0.5f);
        }

        centroid /= player.PoppedCount;
        var chain = player.PopChain;
        var tierBefore = combo.Multiplier;
        combo.Hit();
        UiFeedback.Play(UiSound.GameMatch);
        if (combo.Multiplier > tierBefore)
        {
            GameSfx.ComboTierUp();
        }

        fx.Shockwave(centroid, cell * (1.4f + 0.3f * chain), GamePalette.Lighten(Accent, 0.3f), 0.5f, 3f);
        fx.AddText(GameNumber.Signed(player.PopGain), centroid, Gold, 1.05f);
        fx.AddTrauma(MathF.Min(0.5f, 0.06f + 0.04f * chain));
        if (chain >= 2)
        {
            fx.AddText(chainLabel.Get(L.Gloop.Chain, chain), centroid - new Vector2(0f, cell * 0.8f),
                GamePalette.Lighten(Accent, 0.4f), 1.2f + 0.08f * MathF.Min(chain, 6));
        }

        if (chain >= PunchChain)
        {
            context.Fx.Punch(0.02f + 0.01f * chain);
        }

        if (chain >= SlowMoChain)
        {
            context.Fx.SlowMo(0.6f, 0.3f);
        }

        if (chain >= FlashChain)
        {
            context.Fx.Flash(Gold, 0.18f);
        }
    }

    private void OnRivalEvents(float scale)
    {
        if (rival.ClearedCount <= 0)
        {
            return;
        }

        var cell = wells.RivalCell;
        for (var index = 0; index < rival.ClearedCount; index++)
        {
            var center = GloopRenderer.CenterOf(wells.Rival, cell, rival.PoppedCell(index));
            particles.Burst(center, 3, GloopRenderer.ColorOf(rival.PoppedColor(index)), 80f * scale, 2f, 0.35f, 200f);
        }
    }

    private void OnLevelUp(in GameContext context, float scale)
    {
        GameSfx.LevelClear();
        context.Fx.Sweep();
        fx.AddText(levelLabel.Get(L.Stage.LevelShort, level), wells.Player.Center, Gold, 1.4f);
        particles.Sparkle(wells.Player.Center, 16, Gold, 200f * scale, 2.8f, 0.8f);
    }

    private void OnPlayerDied(in GameContext context, float scale)
    {
        UiFeedback.Play(UiSound.GameBreak);
        BurstWell(player, wells.Player, wells.Cell, scale);
        fx.AddTrauma(0.6f);
        context.Fx.Flash(Danger, 0.35f);
        context.Fx.Vignette(Danger, 0.4f, 0.9f);
        context.Fx.SlowMo(0.5f, 0.4f);
        playerCollapse = 0.0001f;
        BeginFinish(false);
    }

    private void OnRivalDied(in GameContext context, float scale)
    {
        UiFeedback.Play(UiSound.GameExplosion);
        BurstWell(rival, wells.Rival, wells.RivalCell, scale);
        particles.Confetti(wells.Rival.Center, 70, ClearPalette, 260f * scale, 4f, 1.4f);
        context.Fx.Sweep();
        context.Fx.Punch(0.06f);
        fx.AddTrauma(0.3f);
        rivalCollapse = 0.0001f;
        BeginFinish(true);
    }

    private void BurstWell(GloopBoard board, Rect well, float cell, float scale)
    {
        for (var index = 0; index < GloopRules.Cells; index += 2)
        {
            var value = board.Cell(index);
            if (value == GloopRules.Empty)
            {
                continue;
            }

            var center = GloopRenderer.CenterOf(well, cell, index);
            particles.Burst(center, 2, GloopRenderer.ColorOf(value), 180f * scale, 3f, 0.6f, 320f, MathF.PI * 2f, 0f,
                ParticleShape.Shard);
        }
    }

    private void BeginFinish(bool won)
    {
        finishPending = true;
        playerWon = won;
        finishTimer = 0f;
        player.SoftDrop(false);
    }

    private void AdvanceFinish(float deltaSeconds, in GameContext context)
    {
        finishTimer += deltaSeconds;
        if (finishTimer < FinishDelay || finished)
        {
            return;
        }

        finished = true;
        finishPending = false;
        context.Session.Finish(versus ? VersusOutcome(playerWon) : EndlessOutcome());
    }

    private GameOutcome EndlessOutcome() =>
        new GameOutcome(player.Score, ScoreKind.Score, GameId)
            .WithStat(L.Gloop.BestChain, GameNumber.Label(player.BestChain))
            .WithStat(L.Gloop.Popped, GameNumber.Label(player.Popped))
            .WithStat(L.Games.Level, GameNumber.Label(level));

    private GameOutcome VersusOutcome(bool won) =>
        new GameOutcome(won ? 1 : 0, ScoreKind.Streak, VersusStatId, won)
            .WithStat(L.Games.Score, GameNumber.Label(player.Score))
            .WithStat(L.Gloop.BestChain, GameNumber.Label(player.BestChain))
            .WithStat(L.Gloop.Sent, GameNumber.Label(player.Sent));

    private void ShowBanner(string text)
    {
        bannerText = text;
        banner = 0f;
    }

    private static void Smooth(GloopBoard board, ref float column, ref float angle, ref int seen, float deltaSeconds)
    {
        var targetAngle = OrientationAngle(board.PieceOrientation);
        if (seen != board.Spawns)
        {
            seen = board.Spawns;
            column = board.PieceColumn;
            angle = targetAngle;
            return;
        }

        var blend = MathF.Min(1f, deltaSeconds * SmoothRate);
        column += (board.PieceColumn - column) * blend;
        var turn = targetAngle - angle;
        while (turn > MathF.PI)
        {
            turn -= MathF.PI * 2f;
        }

        while (turn < -MathF.PI)
        {
            turn += MathF.PI * 2f;
        }

        angle += turn * blend;
    }

    private static float OrientationAngle(int orientation) => orientation switch
    {
        1 => 0f,
        2 => MathF.PI * 0.5f,
        3 => MathF.PI,
        _ => -MathF.PI * 0.5f,
    };

    private void DrawScene(ImDrawListPtr drawList, in GameContext context, float raw, float scale)
    {
        if (playerCollapse > 0f)
        {
            playerCollapse = MathF.Min(1f, playerCollapse + raw / CollapseSeconds);
        }

        if (rivalCollapse > 0f)
        {
            rivalCollapse = MathF.Min(1f, rivalCollapse + raw / CollapseSeconds);
        }

        Smooth(player, ref pieceColumn, ref pieceAngle, ref pieceSeen, raw);
        BoardPlate.Draw(drawList, BoardPlate.Around(wells.Plate, scale), BoardPlate.Radius * scale, scale, Accent,
            context.Backdrop.Ink);
        DrawWell(drawList, player, wells.Player, wells.Cell, pieceColumn, pieceAngle, playerCollapse, true, scale);
        if (versus)
        {
            Smooth(rival, ref rivalColumn, ref rivalAngle, ref rivalSeen, raw);
            DrawWell(drawList, rival, wells.Rival, wells.RivalCell, rivalColumn, rivalAngle, rivalCollapse, false,
                scale);
            DrawRivalLabel(drawList, scale);
            var band = IncomingBand * wells.Cell * 0.5f;
            GloopRenderer.DrawIncoming(drawList, new Vector2(wells.Player.Min.X, wells.Player.Min.Y - band),
                band * 1.4f, player.Incoming, scale);
            GloopRenderer.DrawIncoming(drawList, new Vector2(wells.Rival.Min.X, wells.Rival.Min.Y - band),
                band * 1.1f, rival.Incoming, scale);
        }

        DrawNext(drawList, player, wells.Side, wells.Cell, scale);
        DrawOrbs(drawList, scale);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        GameBanner.Draw(drawList, wells.Player.Center, bannerText, Gold, context.Theme, banner);
    }

    private static void DrawWell(ImDrawListPtr drawList, GloopBoard board, Rect well, float cell, float column,
        float angle, float collapse, bool ghost, float scale)
    {
        GloopRenderer.DrawWell(drawList, well, cell, scale);
        GloopRenderer.DrawBlobs(drawList, board, well, cell, board.PopProgress, collapse, scale);
        if (!board.Falling || collapse > 0f)
        {
            return;
        }

        if (ghost)
        {
            GloopRenderer.DrawGhost(drawList, board, well, cell, scale);
        }

        GloopRenderer.DrawPiece(drawList, board, well, cell, column, angle, scale);
    }

    private static void DrawNext(ImDrawListPtr drawList, GloopBoard board, Rect side, float cell, float scale)
    {
        var caption = Loc.T(L.Games.Next);
        var captionHeight = Typography.LineHeight(TextStyles.Caption2);
        Typography.DrawCentered(drawList, new Vector2(side.Center.X, side.Min.Y + captionHeight * 0.5f), caption, Muted,
            TextStyles.Caption2);
        var first = cell * 0.82f;
        var second = cell * 0.62f;
        var top = side.Min.Y + captionHeight + 4f * scale;
        var wide = side.Width >= first + second + 6f * scale;
        var firstCenter = wide
            ? new Vector2(side.Center.X - second * 0.6f, top + first)
            : new Vector2(side.Center.X, top + first);
        var secondCenter = wide
            ? new Vector2(side.Center.X + first * 0.6f, top + first)
            : new Vector2(side.Center.X, top + first * 2f + second * 1.4f);
        GloopRenderer.DrawPair(drawList, firstCenter, first, board.NextPivot(0), board.NextSatellite(0), 1f, scale);
        GloopRenderer.DrawPair(drawList, secondCenter, second, board.NextPivot(1), board.NextSatellite(1), 0.7f, scale);
    }

    private void DrawRivalLabel(ImDrawListPtr drawList, float scale)
    {
        if (!ReferenceEquals(rivalLanguage, Loc.Current) || rivalLabelSkill != bot.Skill || rivalLabel.Length == 0)
        {
            rivalLanguage = Loc.Current;
            rivalLabelSkill = bot.Skill;
            rivalLabel = string.Concat(Loc.T(L.Gloop.Rival), Separator,
                Loc.T(bot.Skill == BotSkill.Hard ? L.Games.Hard : L.Games.Easy));
        }

        var labelY = wells.Rival.Max.Y + Typography.LineHeight(TextStyles.Caption2) * 0.5f + 3f * scale;
        Typography.DrawCentered(drawList, new Vector2(wells.Rival.Center.X, labelY), rivalLabel, Muted,
            TextStyles.Caption2);
    }

    private void DrawOrbs(ImDrawListPtr drawList, float scale)
    {
        for (var index = 0; index < MaxOrbs; index++)
        {
            ref readonly var orb = ref orbs[index];
            if (!orb.Active)
            {
                continue;
            }

            var eased = Easing.EaseInOutCubic(orb.Progress);
            var position = Vector2.Lerp(orb.From, orb.To, eased);
            position.Y -= MathF.Sin(orb.Progress * MathF.PI) * 40f * scale;
            var trail = orbTrails[index];
            trail.Push(position);
            trail.Draw(drawList, Danger, 6f * scale, true);
            var radius = (5f + MathF.Min(orb.Count, 30) * 0.25f) * scale;
            ProgressRing.Glow(position, radius * 2.2f, Danger, 0.7f);
            GloopRenderer.DrawBlob(drawList, position, radius, GloopRules.Rock, 0f, 0f, 1f, false);
        }
    }

    private void DrawHud(in GameContext context)
    {
        var hud = context.Hud;
        hud.Score(player.Score);
        if (!versus)
        {
            hud.Level(level);
        }

        hud.Combo(combo);
        hud.Best(context.Session.Best);
        context.Session.Report(player.Score);
    }
}
