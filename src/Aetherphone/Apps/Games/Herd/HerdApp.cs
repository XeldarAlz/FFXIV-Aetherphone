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

namespace Aetherphone.Apps.Games.Herd;

internal sealed class HerdApp : IMiniGame
{
    private const string GameId = "herd";
    private const string SurfaceId = "herd.press";
    private const int SkillCount = HerdLevel.SkillCount;
    private const int ControlCount = 3;
    private const float TrayBottom = 12f;
    private const float SkillRowHeight = 56f;
    private const float ControlRowHeight = 34f;
    private const float RowGap = 6f;
    private const float TileGap = 6f;
    private const float ViewGap = 4f;
    private const float TileRadius = 14f;
    private const float IconSize = 22f;
    private const float BadgeRadius = 9f;
    private const float PickRadius = 0.8f;
    private const float FastFactor = 3f;
    private const float NukeArmSeconds = 2f;
    private const float UrgentSeconds = 20f;
    private const float BannerSeconds = 1.6f;
    private const float PopDecay = 4f;
    private const float PulseDecay = 2.4f;
    private const float DoorOpenSpeed = 1.6f;
    private const float PausePillHeight = 30f;
    private const ulong IdleSeed = 0x48455244UL;
    private static readonly GameSpec StageSpec = new(GameId, L.Herd.Title, GameGenre.Strategy, L.Herd.Hook,
        Backdrop.Meadow, HudStyle.Standard, ScoreKind.Level, clocked: true, keyboard: true,
        levelCount: HerdLevels.Count);
    private static readonly Rect WorldRect = new(Vector2.Zero, new Vector2(HerdBoard.WorldWidth, HerdBoard.WorldHeight));
    private static readonly HerdSkill[] Skills =
    {
        HerdSkill.Block, HerdSkill.Dig, HerdSkill.Bridge, HerdSkill.Climb, HerdSkill.Float, HerdSkill.Bash,
    };
    private static readonly LocString[] SkillNames =
    {
        L.Herd.Block, L.Herd.Dig, L.Herd.Bridge, L.Herd.Climb, L.Herd.Float, L.Herd.Bash,
    };
    private static readonly LocString[] SkillHints =
    {
        L.Herd.BlockHint, L.Herd.DigHint, L.Herd.BridgeHint, L.Herd.ClimbHint, L.Herd.FloatHint, L.Herd.BashHint,
    };
    private static readonly ImGuiKey[] SkillKeys =
    {
        ImGuiKey.Key1, ImGuiKey.Key2, ImGuiKey.Key3, ImGuiKey.Key4, ImGuiKey.Key5, ImGuiKey.Key6,
    };
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Gold = new(1f, 0.84f, 0.36f, 1f);
    private static readonly Vector4 Danger = new(0.95f, 0.3f, 0.3f, 1f);
    private static readonly Vector4 Success = new(0.44f, 0.86f, 0.52f, 1f);
    private static readonly Vector4 Muted = new(1f, 1f, 1f, 0.6f);
    private static readonly Vector4 Earth = new(0.55f, 0.4f, 0.27f, 1f);
    private static readonly Vector4 Dust = new(0.92f, 0.88f, 0.78f, 0.7f);
    private static readonly Vector4 Splash = new(0.5f, 0.78f, 1f, 1f);
    private static readonly Vector4[] ConfettiColors =
    {
        new(1f, 0.84f, 0.36f, 1f), new(0.98f, 0.4f, 0.52f, 1f), new(0.56f, 0.82f, 1f, 1f), new(0.6f, 0.92f, 0.56f, 1f),
    };
    private static readonly ParticleSpec Fluff = new(HerdArt.Fur, HerdArt.Fur with { W = 0f }, 0.09f, 3.2f, 0.6f, 9f,
        2f);
    private static readonly ParticleSpec PomBits = new(HerdArt.PomPom, HerdArt.PomPom with { W = 0f }, 0.07f, 3.8f,
        0.7f, 8f, 1.6f, 9f, shape: ParticleShape.Shard);
    private static readonly ParticleSpec SavedSparkle = new(Gold, White, 0.08f, 2.6f, 0.7f, 1f, 2.4f, 6f,
        shape: ParticleShape.Star, additive: true);
    private static readonly ParticleSpec SavedRing = new(Gold, Gold with { W = 0f }, 0.4f, 0f, 0.4f,
        shape: ParticleShape.Ring, curve: SizeCurve.Grow);
    private static readonly ParticleSpec Dirt = new(Earth, Earth with { W = 0f }, 0.05f, 2.2f, 0.45f, 9f, 1.4f, 8f,
        shape: ParticleShape.Square);
    private static readonly ParticleSpec DustPuff = new(Dust, Dust with { W = 0f }, 0.12f, 0.9f, 0.45f, -0.5f, 2.4f,
        curve: SizeCurve.Grow);
    private static readonly ParticleSpec SplashDrops = new(Splash, Splash with { W = 0f }, 0.07f, 3.2f, 0.6f, 9f, 1.2f);
    private static readonly ParticleSpec Bubbles = new(White with { W = 0.8f }, White with { W = 0f }, 0.05f, 0.6f,
        0.8f, -2f, 1f, shape: ParticleShape.Ring);
    private static readonly ParticleSpec PopPuff = new(White, White with { W = 0f }, 0.16f, 1.6f, 0.5f, -1f, 2.2f,
        shape: ParticleShape.GlowCircle, additive: true);

    private readonly HerdBoard board = new();
    private readonly TerrainTexture terrain;
    private readonly ParticleSystem particles = new(448);
    private readonly FeedbackFx fx = new();
    private readonly float[] tilePops = new float[SkillCount];
    private readonly float[] tileShakes = new float[SkillCount];
    private Camera2D camera = Camera2D.Create();
    private LabelSlot levelLabel;
    private LabelPairSlot savedLabel;
    private Rect view;
    private Rect skillRow;
    private Rect controlRow;
    private HerdSkill selected = HerdSkill.Dig;
    private string bannerText = string.Empty;
    private Vector4 bannerColor = White;
    private float bannerProgress = 1f;
    private float exitPulse;
    private float doorPulse;
    private float doorOpen;
    private float nukeArmed;
    private float time;
    private int hovered = -1;
    private bool hoveredEligible;
    private bool assignPause;
    private bool fast;
    private bool finished;
    private bool idleReady;

    public HerdApp(ITextureProvider textures)
    {
        board.Load(HerdLevels.Get(1), GameRandom.FromSeed(IdleSeed));
        terrain = new TerrainTexture(textures, board.Terrain, TerrainMaterial.Earth);
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        var level = Math.Clamp(start.Level, 1, HerdLevels.Count);
        board.Load(HerdLevels.Get(level), start.Random);
        particles.Clear();
        particles.Reseed(start.Seed);
        fx.Clear();
        camera = Camera2D.Create();
        Array.Clear(tilePops);
        Array.Clear(tileShakes);
        selected = FirstAvailableSkill();
        bannerText = levelLabel.Get(L.Stage.LevelNumber, level);
        bannerColor = Accent;
        bannerProgress = 0f;
        exitPulse = 0f;
        doorPulse = 0f;
        doorOpen = 0f;
        nukeArmed = 0f;
        hovered = -1;
        assignPause = false;
        fast = false;
        finished = false;
        idleReady = false;
    }

    public void Close()
    {
        particles.Clear();
        fx.Clear();
        idleReady = false;
    }

    public void Dispose()
    {
        terrain.Dispose();
    }

    public void DrawIdle(in GameContext context)
    {
        var level = HerdLevels.Get(Math.Max(1, context.Session.Level));
        if (!idleReady || !ReferenceEquals(board.Level, level) || board.Over)
        {
            board.Load(level, GameRandom.FromSeed(IdleSeed));
            doorOpen = 0f;
            idleReady = true;
        }

        var raw = context.RawDeltaSeconds;
        time += raw;
        doorOpen = MathF.Min(1f, doorOpen + raw * DoorOpenSpeed);
        Layout(context);
        PlaceCamera(context);
        board.Step(raw);
        DrawWorld(ImGui.GetWindowDrawList(), UiScale.Current, false);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var raw = context.RawDeltaSeconds;
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        time += raw;
        particles.Update(raw);
        fx.Update(raw);
        bannerProgress = GameBanner.Advance(bannerProgress, raw, BannerSeconds);
        exitPulse = MathF.Max(0f, exitPulse - raw * PulseDecay);
        doorPulse = MathF.Max(0f, doorPulse - raw * PulseDecay);
        doorOpen = MathF.Min(1f, doorOpen + raw * DoorOpenSpeed);
        nukeArmed = MathF.Max(0f, nukeArmed - raw);
        for (var skill = 0; skill < SkillCount; skill++)
        {
            tilePops[skill] = MathF.Max(0f, tilePops[skill] - raw * PopDecay);
            tileShakes[skill] = MathF.Max(0f, tileShakes[skill] - raw * PopDecay);
        }

        Layout(context);
        PlaceCamera(context);
        if (!finished)
        {
            HandleInput(context, scale);
            var speed = assignPause ? 0f : fast ? FastFactor : 1f;
            board.Step(simDelta * speed);
            React(context, scale);
            if (board.Over)
            {
                FinishRun(context);
            }
        }

        DrawWorld(drawList, scale, true);
        DrawTray(drawList, context, scale);
        DrawPausePill(drawList, context, scale);
        GameBanner.Draw(drawList, new Vector2(view.Center.X, view.Min.Y + view.Height * 0.3f), bannerText, bannerColor,
            context.Theme, bannerProgress);
        if (!finished && board.SecondsLeft <= UrgentSeconds && context.Session.State == StageFlow.Playing)
        {
            context.Fx.Vignette(Danger, 0.08f + 0.1f * Pulse.Wave(Pulse.Fast), 0.3f);
        }

        context.Hud.Score(board.SavedCount, L.Herd.Saved);
        context.Hud.Timer(board.SecondsLeft, board.TotalSeconds, board.SecondsLeft <= UrgentSeconds);
        context.Hud.Level(context.Session.Level);
        context.Hud.Custom(StatCapsule.Width(GameNumber.Label(board.Required), scale));
        DrawGoalCapsule(drawList, context, scale);
        context.Session.Report(board.SavedCount);
    }

    private HerdSkill FirstAvailableSkill()
    {
        for (var skill = 0; skill < SkillCount; skill++)
        {
            if (board.SkillsLeft(Skills[skill]) > 0)
            {
                return Skills[skill];
            }
        }

        return HerdSkill.Dig;
    }

    private void Layout(in GameContext context)
    {
        var scale = UiScale.Current;
        var safe = context.Safe;
        var bottom = context.Full.Max.Y - TrayBottom * scale;
        skillRow = new Rect(new Vector2(safe.Min.X, bottom - SkillRowHeight * scale), new Vector2(safe.Max.X, bottom));
        var controlBottom = skillRow.Min.Y - RowGap * scale;
        controlRow = new Rect(new Vector2(safe.Min.X, controlBottom - ControlRowHeight * scale),
            new Vector2(safe.Max.X, controlBottom));
        var viewBottom = MathF.Max(safe.Min.Y + 1f, controlRow.Min.Y - ViewGap * scale);
        view = new Rect(safe.Min, new Vector2(safe.Max.X, viewBottom));
    }

    private void PlaceCamera(in GameContext context)
    {
        camera.Fit(view, WorldRect, FitMode.Contain);
        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, UiScale.Current);
        context.Backdrop.SetCamera(in camera);
    }

    private Rect TileRect(int index)
    {
        var scale = UiScale.Current;
        var gap = TileGap * scale;
        var width = (skillRow.Width - gap * (SkillCount - 1)) / SkillCount;
        var left = skillRow.Min.X + index * (width + gap);
        return new Rect(new Vector2(left, skillRow.Min.Y), new Vector2(left + width, skillRow.Max.Y));
    }

    private Vector2 ControlCenter(int index)
    {
        var radius = controlRow.Height * 0.5f;
        var gap = TileGap * UiScale.Current;
        var x = controlRow.Max.X - radius - (ControlCount - 1 - index) * (radius * 2f + gap);
        return new Vector2(x, controlRow.Center.Y);
    }

    private void HandleInput(in GameContext context, float scale)
    {
        hovered = -1;
        if (context.Session.State != StageFlow.Playing || board.Over)
        {
            return;
        }

        for (var skill = 0; skill < SkillCount; skill++)
        {
            if (GameInput.Pressed(SkillKeys[skill]))
            {
                Select(skill);
            }
        }

        if (GameInput.Pressed(ImGuiKey.Space))
        {
            TogglePause();
        }

        if (GameInput.Pressed(ImGuiKey.F))
        {
            ToggleFast();
        }

        var full = context.Full;
        var pressArea = new Rect(new Vector2(full.Min.X, full.Min.Y + StageLayout.ChromeBand * scale), full.Max);
        PressSurface.Claim(SurfaceId, pressArea, out var activated);
        var mouse = ImGui.GetMousePos();
        var overView = view.Contains(mouse) && UiInteract.Hover(view.Min, view.Max) && !context.ChromeHit(mouse);
        if (overView)
        {
            var world = camera.ToWorld(mouse);
            hovered = board.Pick(world, PickRadius, selected, true);
            hoveredEligible = hovered >= 0;
            if (hovered < 0)
            {
                hovered = board.Pick(world, PickRadius, selected, false);
            }
        }

        if (!activated || context.ChromeHit(mouse))
        {
            return;
        }

        for (var skill = 0; skill < SkillCount; skill++)
        {
            if (TileRect(skill).Contains(mouse))
            {
                Select(skill);
                return;
            }
        }

        var radius = controlRow.Height * 0.5f;
        for (var control = 0; control < ControlCount; control++)
        {
            if (Vector2.DistanceSquared(mouse, ControlCenter(control)) > radius * radius)
            {
                continue;
            }

            switch (control)
            {
                case 0:
                    TogglePause();
                    break;
                case 1:
                    ToggleFast();
                    break;
                default:
                    PressNuke(context);
                    break;
            }

            return;
        }

        if (!overView)
        {
            return;
        }

        TryAssign();
    }

    private void Select(int skill)
    {
        if (selected == Skills[skill])
        {
            tilePops[skill] = 0.6f;
            return;
        }

        selected = Skills[skill];
        tilePops[skill] = 1f;
        UiFeedback.Play(UiSound.GameTick);
    }

    private void TogglePause()
    {
        assignPause = !assignPause;
        UiFeedback.Play(UiSound.GamePop);
    }

    private void ToggleFast()
    {
        fast = !fast;
        UiFeedback.Play(UiSound.GamePop);
    }

    private void PressNuke(in GameContext context)
    {
        if (board.Nuking)
        {
            return;
        }

        if (nukeArmed <= 0f)
        {
            nukeArmed = NukeArmSeconds;
            UiFeedback.Play(UiSound.GameWrong);
            ShowBanner(Loc.T(L.Herd.NukeArmed), Danger);
            return;
        }

        nukeArmed = 0f;
        board.Nuke();
        assignPause = false;
        UiFeedback.Play(UiSound.GameExplosion);
        context.Fx.Flash(Danger, 0.25f);
        camera.Shake(0.3f);
    }

    private void TryAssign()
    {
        var skillIndex = (int)selected;
        if (hovered < 0)
        {
            return;
        }

        if (!hoveredEligible || !board.Assign(hovered, selected))
        {
            UiFeedback.Play(UiSound.GameWrong);
            tileShakes[skillIndex] = 1f;
            return;
        }

        tilePops[skillIndex] = 1f;
        var center = board.BodyCenter(hovered, board.Alpha);
        var screen = camera.ToScreen(center);
        UiFeedback.Play(UiSound.GamePiece);
        fx.Shockwave(screen, camera.Px(0.9f), Accent, 0.35f, 2.4f);
        particles.Emit(SavedSparkle, center, 5);
        camera.Punch(0.012f);
    }

    private void React(in GameContext context, float scale)
    {
        for (var index = 0; index < board.EventCount; index++)
        {
            ref readonly var entry = ref board.Event(index);
            var world = HerdBoard.CellPoint(entry.Column, entry.Row);
            switch (entry.Kind)
            {
                case HerdEventKind.Spawned:
                    doorPulse = 1f;
                    particles.Emit(DustPuff, world + new Vector2(0f, -0.3f), 4);
                    break;
                case HerdEventKind.Saved:
                    OnSaved(world, scale);
                    break;
                case HerdEventKind.GoalReached:
                    OnGoal(context, world);
                    break;
                case HerdEventKind.Splat:
                    UiFeedback.Play(UiSound.GameHitSoft);
                    particles.Emit(Fluff, world + new Vector2(0f, -0.2f), 12);
                    particles.Emit(PomBits, world + new Vector2(0f, -0.5f), 5);
                    fx.Shockwave(camera.ToScreen(world), camera.Px(0.8f), Danger, 0.35f, 2f);
                    camera.Shake(0.18f);
                    break;
                case HerdEventKind.Drowned:
                    UiFeedback.Play(UiSound.GamePop);
                    particles.Emit(SplashDrops, world, 12);
                    particles.Emit(Bubbles, world + new Vector2(0f, 0.2f), 5);
                    break;
                case HerdEventKind.Popped:
                    UiFeedback.Play(UiSound.GamePop);
                    particles.Emit(PopPuff, world + new Vector2(0f, -0.45f), 5);
                    particles.Emit(Fluff, world + new Vector2(0f, -0.45f), 8);
                    particles.Emit(PomBits, world + new Vector2(0f, -0.8f), 4);
                    camera.Shake(0.06f);
                    break;
                case HerdEventKind.Brick:
                    particles.Emit(DustPuff, world, 2);
                    if (board.Moogle(entry.Moogle).Bricks % 3 == 0)
                    {
                        UiFeedback.Play(UiSound.GameHitWood);
                    }

                    break;
                case HerdEventKind.BridgeDone:
                    particles.Emit(DustPuff, world, 5);
                    particles.Emit(SavedSparkle, world + new Vector2(0f, -1f), 3);
                    break;
                case HerdEventKind.Dug:
                    particles.Emit(Dirt, world + new Vector2(0f, -0.05f), 3);
                    break;
                case HerdEventKind.Bashed:
                    particles.Emit(Dirt, world, 4);
                    if (board.Moogle(entry.Moogle).ActionTicks % 4 == 0)
                    {
                        UiFeedback.Play(UiSound.GameHitWood);
                        camera.Shake(0.04f);
                    }

                    break;
                case HerdEventKind.Landed:
                    particles.Emit(DustPuff, world, 3);
                    break;
                default:
                    break;
            }
        }
    }

    private void OnSaved(Vector2 world, float scale)
    {
        UiFeedback.Play(UiSound.GameCollect);
        exitPulse = 1f;
        var screen = camera.ToScreen(world + new Vector2(0f, -0.5f));
        particles.Emit(SavedSparkle, world + new Vector2(0f, -0.5f), 9);
        particles.Emit(SavedRing, world + new Vector2(0f, -0.5f), 1);
        fx.Shockwave(screen, camera.Px(1.2f), Gold, 0.4f, 2.6f);
        fx.AddText(GameNumber.Signed(1), screen - new Vector2(0f, 20f * scale), Gold, 1.05f);
        camera.Punch(0.015f);
    }

    private void OnGoal(in GameContext context, Vector2 world)
    {
        UiFeedback.Play(UiSound.GamePowerUp);
        ShowBanner(Loc.T(L.Herd.GoalReached), Success);
        particles.Confetti(world + new Vector2(0f, -1f), 30, ConfettiColors, 6f, 0.08f, 1.1f, 9f);
        context.Fx.Sweep();
        context.Fx.Flash(Gold, 0.15f);
        context.Fx.Punch(0.05f);
    }

    private void FinishRun(in GameContext context)
    {
        finished = true;
        hovered = -1;
        var stars = HerdBoard.Stars(board.SavedCount, board.Required, board.Count);
        if (stars > 0)
        {
            GameSfx.LevelClear();
            context.Fx.Sweep();
            particles.Confetti(HerdBoard.CellPoint(board.ExitColumn, board.ExitRow - 8), 40, ConfettiColors, 7f,
                0.08f, 1.2f, 9f);
        }
        else
        {
            UiFeedback.Play(UiSound.GameBreak);
            context.Fx.Vignette(Danger, 0.3f, 0.6f);
        }

        context.Session.Finish(new GameOutcome(stars, ScoreKind.Level, GameId, stars > 0)
            .WithStars(stars)
            .WithStat(L.Herd.Saved, savedLabel.Get(L.Herd.SavedOf, board.SavedCount, board.Count))
            .WithStat(L.Herd.Needed, GameNumber.Label(board.Required))
            .WithStat(L.Herd.SkillsUsed, GameNumber.Label(board.SkillsUsed))
            .WithStat(L.Games.Time, TimeText.MinutesSeconds((int)(board.Ticks * HerdBoard.TickSeconds))));
    }

    private void ShowBanner(string text, Vector4 color)
    {
        bannerText = text;
        bannerColor = color;
        bannerProgress = 0f;
    }

    private void DrawWorld(ImDrawListPtr drawList, float scale, bool live)
    {
        drawList.PushClipRect(new Vector2(view.Min.X - 4f * scale, camera.View.Min.Y - 40f * scale),
            new Vector2(view.Max.X + 4f * scale, view.Max.Y), true);
        HerdRenderer.DrawBounds(drawList, in camera);
        terrain.Draw(drawList, in camera);
        HerdRenderer.DrawWater(drawList, in camera, board, time);
        HerdRenderer.DrawDoor(drawList, in camera, board, doorOpen, doorPulse);
        HerdRenderer.DrawExit(drawList, in camera, board, time, exitPulse, Accent);
        HerdRenderer.DrawMoogles(drawList, in camera, board, board.Alpha, time);
        if (live && hovered >= 0 && hovered < board.MoogleCount)
        {
            var center = camera.ToScreen(board.BodyCenter(hovered, board.Alpha));
            var color = hoveredEligible ? Accent : Muted;
            HerdRenderer.DrawTarget(drawList, center, camera.Px(0.62f), color, time);
            HerdArt.DrawSkillIcon(drawList, selected, center - new Vector2(0f, camera.Px(1.05f)), camera.Px(0.42f),
                ImGui.GetColorU32(color));
        }

        particles.Draw(drawList, in camera);
        drawList.PopClipRect();
        if (!live)
        {
            return;
        }

        fx.DrawRings(drawList, scale);
        fx.DrawText();
    }

    private void DrawTray(ImDrawListPtr drawList, in GameContext context, float scale)
    {
        var interactive = context.Session.State == StageFlow.Playing && !finished;
        var mouse = ImGui.GetMousePos();
        for (var index = 0; index < SkillCount; index++)
        {
            DrawTile(drawList, index, interactive, mouse, scale);
        }

        DrawLabel(drawList, scale);
        DrawControls(drawList, interactive, mouse, scale);
    }

    private void DrawTile(ImDrawListPtr drawList, int index, bool interactive, Vector2 mouse, float scale)
    {
        var skill = Skills[index];
        var count = board.SkillsLeft(skill);
        var isSelected = skill == selected;
        var rect = TileRect(index);
        var shake = tileShakes[index] > 0f ? MathF.Sin(time * 60f) * 3f * scale * tileShakes[index] : 0f;
        var pop = 1f + 0.12f * MathF.Sin(tilePops[index] * MathF.PI);
        var lift = isSelected ? 4f * scale : 0f;
        var half = rect.Size * 0.5f * pop;
        var center = rect.Center + new Vector2(shake, -lift);
        var min = center - half;
        var max = center + half;
        var radius = TileRadius * scale;
        var hoveredTile = interactive && UiInteract.Hover(rect.Min, rect.Max);
        if (isSelected)
        {
            ProgressRing.Glow(center, half.X * 1.1f, Accent, 0.35f);
        }

        Material.Frosted(drawList, min, max, radius, scale, isSelected ? 1f : hoveredTile ? 0.95f : 0.82f);
        if (isSelected)
        {
            Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(Accent with { W = 0.34f }));
            Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(Accent with { W = 0.95f }), 1.5f * scale);
        }
        else if (hoveredTile)
        {
            Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(Accent with { W = 0.45f }), 1f * scale);
        }

        if (hoveredTile)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var inkAlpha = count > 0 ? 1f : 0.35f;
        HerdArt.DrawSkillIcon(drawList, skill, center + new Vector2(0f, 2f * scale), IconSize * scale,
            ImGui.GetColorU32(White with { W = inkAlpha }));
        var badgeCenter = new Vector2(max.X - BadgeRadius * scale * 0.9f, min.Y + BadgeRadius * scale * 0.9f);
        var badgeFill = count > 0 ? (isSelected ? Accent : new Vector4(0.2f, 0.22f, 0.28f, 0.95f)) : Danger with { W = 0.6f };
        drawList.AddCircleFilled(badgeCenter, BadgeRadius * scale, ImGui.GetColorU32(badgeFill), 16);
        Typography.DrawCentered(drawList, badgeCenter, GameNumber.Label(count), GamePalette.InkOn(badgeFill),
            TextStyles.Caption2);
    }

    private void DrawLabel(ImDrawListPtr drawList, float scale)
    {
        var skillIndex = (int)selected;
        var radius = controlRow.Height * 0.5f;
        var right = ControlCenter(0).X - radius - TileGap * scale;
        var width = right - controlRow.Min.X - 4f * scale;
        if (width <= 0f)
        {
            return;
        }

        var pillMin = controlRow.Min;
        var pillMax = new Vector2(right, controlRow.Max.Y);
        Material.Frosted(drawList, pillMin, pillMax, controlRow.Height * 0.5f, scale, 0.82f);
        var inner = width - 20f * scale;
        var name = Typography.FitText(Loc.T(SkillNames[skillIndex]), inner, TextStyles.SubheadlineEmphasized);
        var nameWidth = Typography.Measure(name, TextStyles.SubheadlineEmphasized).X;
        var left = pillMin.X + 12f * scale;
        var centerY = controlRow.Center.Y;
        Typography.Draw(drawList, new Vector2(left, centerY - Typography.LineHeight(TextStyles.SubheadlineEmphasized) * 0.5f),
            name, Accent, TextStyles.SubheadlineEmphasized);
        var hintWidth = inner - nameWidth - 8f * scale;
        if (hintWidth <= 24f * scale)
        {
            return;
        }

        var hint = Typography.FitText(Loc.T(SkillHints[skillIndex]), hintWidth, TextStyles.Caption1);
        Typography.Draw(drawList,
            new Vector2(left + nameWidth + 8f * scale, centerY - Typography.LineHeight(TextStyles.Caption1) * 0.5f), hint,
            Muted, TextStyles.Caption1);
    }

    private void DrawControls(ImDrawListPtr drawList, bool interactive, Vector2 mouse, float scale)
    {
        var radius = controlRow.Height * 0.5f;
        for (var control = 0; control < ControlCount; control++)
        {
            var center = ControlCenter(control);
            var corner = new Vector2(radius, radius);
            var hoveredControl = interactive && UiInteract.Hover(center - corner, center + corner) &&
                                 Vector2.DistanceSquared(mouse, center) <= radius * radius;
            var active = control switch
            {
                0 => assignPause,
                1 => fast,
                _ => nukeArmed > 0f || board.Nuking,
            };
            var tint = control == 2 ? Danger : Accent;
            if (active)
            {
                ProgressRing.Glow(center, radius * 1.3f, tint, 0.4f + (control == 2 ? 0.3f * Pulse.Wave(Pulse.Fast) : 0f));
            }

            Material.Frosted(drawList, center - corner, center + corner, radius, scale, hoveredControl || active ? 1f : 0.82f);
            if (active)
            {
                Squircle.Fill(drawList, center - corner, center + corner, radius, ImGui.GetColorU32(tint with { W = 0.4f }));
            }

            if (hoveredControl)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            var ink = ImGui.GetColorU32(White);
            var size = radius * 0.95f;
            switch (control)
            {
                case 0:
                    if (assignPause)
                    {
                        HerdArt.DrawPlay(drawList, center, size, ink);
                    }
                    else
                    {
                        HerdArt.DrawPause(drawList, center, size, ink);
                    }

                    break;
                case 1:
                    HerdArt.DrawFast(drawList, center, size, ink);
                    break;
                default:
                    HerdArt.DrawBomb(drawList, center, size, ink, ImGui.GetColorU32(Gold));
                    break;
            }
        }
    }

    private void DrawPausePill(ImDrawListPtr drawList, in GameContext context, float scale)
    {
        if (!assignPause || finished || context.Session.State != StageFlow.Playing)
        {
            return;
        }

        var text = Loc.T(L.Herd.Paused);
        var style = TextStyles.FootnoteEmphasized;
        var maxWidth = view.Width - 24f * scale;
        var fitted = Typography.FitText(text, maxWidth - 24f * scale, style);
        var width = Typography.Measure(fitted, style).X + 24f * scale;
        var height = PausePillHeight * scale;
        var center = new Vector2(view.Center.X, view.Min.Y + height * 0.5f + 4f * scale);
        var min = center - new Vector2(width * 0.5f, height * 0.5f);
        var max = center + new Vector2(width * 0.5f, height * 0.5f);
        var glow = 0.6f + 0.4f * Pulse.Wave(Pulse.Calm);
        Material.Frosted(drawList, min, max, height * 0.5f, scale, 0.95f);
        Squircle.Stroke(drawList, min, max, height * 0.5f, ImGui.GetColorU32(Accent with { W = glow }), 1.5f * scale);
        Typography.DrawCentered(drawList, center, fitted, White, style);
    }

    private void DrawGoalCapsule(ImDrawListPtr drawList, in GameContext context, float scale)
    {
        if (!context.Hud.CustomPlaced(0))
        {
            return;
        }

        var rect = context.Hud.CustomRect(0);
        var reached = board.GoalReached;
        StatCapsule.Draw(drawList, rect, FontAwesomeIcon.Flag, GameNumber.Label(board.Required),
            reached ? Success : Accent, scale);
        if (reached)
        {
            Squircle.Stroke(drawList, rect.Min, rect.Max, rect.Height * 0.5f,
                ImGui.GetColorU32(Success with { W = 0.7f }), 1.2f * scale);
        }
    }
}
