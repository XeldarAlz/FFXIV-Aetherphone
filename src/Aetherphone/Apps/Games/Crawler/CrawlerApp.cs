using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Crawler;

internal sealed class CrawlerApp : IMiniGame
{
    private const string GameId = "crawler";
    private const string FieldSurfaceId = "crawler.field";
    private const float PadInsetX = 12f;
    private const float PadInsetY = 6f;
    private const float PadOpacity = 0.92f;
    private const float WaveBannerSeconds = 1.5f;
    private const float PopDecay = 3.2f;
    private const float RegrowStagger = 0.035f;
    private const float ZoneHeatDecay = 0.8f;
    private const float BulletTrailWidth = 0.22f;
    private const ulong IdleSeed = 0x435241574CUL;
    private static readonly GameSpec StageSpec = new(GameId, L.Crawler.Title, GameGenre.Action, L.Crawler.Hook,
        Backdrop.Neon, HudStyle.Standard, ScoreKind.Score, clocked: true, countdown: true, keyboard: true);
    private static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Gold = new(1f, 0.84f, 0.36f, 1f);
    private static readonly Vector4 AccentColor = AppAccents.For(GameId);
    private static readonly ParticleSpec Sparks = new(White, AccentColor with { W = 0f }, 0.12f, 7f, 0.4f, 0f, 2f,
        shape: ParticleShape.Spark, additive: true);
    private static readonly ParticleSpec SegmentShards = new(NeonStroke.Core(CrawlerRenderer.BodyColor),
        CrawlerRenderer.BodyColor with { W = 0f }, 0.22f, 6f, 0.6f, 0f, 1.6f, 10f, shape: ParticleShape.Shard,
        additive: true);
    private static readonly ParticleSpec HeadShards = new(NeonStroke.Core(CrawlerRenderer.HeadColor),
        CrawlerRenderer.HeadColor with { W = 0f }, 0.26f, 8f, 0.7f, 0f, 1.4f, 12f, shape: ParticleShape.Shard,
        additive: true);
    private static readonly ParticleSpec SegmentRing = new(CrawlerRenderer.BodyColor,
        CrawlerRenderer.BodyColor with { W = 0f }, 0.4f, 0f, 0.35f, shape: ParticleShape.Ring, curve: SizeCurve.Grow,
        additive: true);
    private static readonly ParticleSpec MushroomChips = new(AccentColor, AccentColor with { W = 0f }, 0.1f, 5f, 0.35f,
        6f, 1.8f, 8f, shape: ParticleShape.Square, additive: true);
    private static readonly ParticleSpec SpiderShards = new(NeonStroke.Core(CrawlerRenderer.SpiderColor),
        CrawlerRenderer.SpiderColor with { W = 0f }, 0.3f, 9f, 0.8f, 0f, 1.3f, 12f, shape: ParticleShape.Shard,
        additive: true);
    private static readonly ParticleSpec FleaShards = new(NeonStroke.Core(CrawlerRenderer.FleaColor),
        CrawlerRenderer.FleaColor with { W = 0f }, 0.26f, 8f, 0.7f, 0f, 1.4f, 12f, shape: ParticleShape.Shard,
        additive: true);
    private static readonly ParticleSpec PlayerShards = new(NeonStroke.Core(AccentColor), AccentColor with { W = 0f },
        0.32f, 9f, 1f, 0f, 1.1f, 9f, shape: ParticleShape.Shard, additive: true);
    private static readonly ParticleSpec Blast = new(White, AccentColor with { W = 0f }, 1.4f, 0f, 0.5f,
        shape: ParticleShape.Ring, curve: SizeCurve.Grow, additive: true);
    private static readonly ParticleSpec Regrowth = new(AccentColor, White with { W = 0f }, 0.1f, 2.5f, 0.6f, -2f, 2f,
        shape: ParticleShape.Star, additive: true);

    private readonly CrawlerBoard board = new();
    private readonly CrawlerBoard idleBoard = new();
    private readonly ParticleSystem particles = new(640);
    private readonly FeedbackFx fx = new();
    private readonly Ribbon bulletTrail = new();
    private readonly float[] pops = new float[CrawlerBoard.CellCount];
    private readonly float[] idlePops = new float[CrawlerBoard.CellCount];
    private Camera2D camera = Camera2D.Create();
    private CrawlerControls controls;
    private LabelSlot waveLabel;
    private float zoneHeat;
    private float bannerProgress = 1f;
    private float time;
    private string bannerText = string.Empty;
    private bool dragging;
    private bool finished;
    private bool idleReady;

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AccentColor;

    public void Start(in GameStart start)
    {
        board.Reset(start.Random);
        particles.Clear();
        particles.Reseed(start.Seed);
        fx.Clear();
        bulletTrail.Clear();
        Array.Clear(pops);
        controls = default;
        zoneHeat = 0f;
        dragging = false;
        finished = false;
        ShowWaveBanner();
    }

    public void Close()
    {
        particles.Clear();
        bulletTrail.Clear();
        dragging = false;
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        if (!idleReady || idleBoard.GameOver)
        {
            idleBoard.Reset(GameRandom.FromSeed(IdleSeed));
            idleReady = true;
        }

        var scale = UiScale.Current;
        time += context.RawDeltaSeconds;
        PlaceCamera(context, scale);
        idleBoard.Step(context.RawDeltaSeconds, Autopilot(idleBoard));
        var drawList = ImGui.GetWindowDrawList();
        var field = CrawlerRenderer.FieldRect(in camera);
        CrawlerRenderer.DrawField(drawList, field, in camera, Accent, scale, 0f);
        drawList.PushClipRect(field.Min, field.Max, true);
        CrawlerRenderer.DrawMushrooms(drawList, idleBoard, in camera, Accent, scale, idlePops);
        CrawlerRenderer.DrawCrawler(drawList, idleBoard, in camera, scale, time);
        CrawlerRenderer.DrawSpider(drawList, idleBoard, in camera, scale, time);
        CrawlerRenderer.DrawFlea(drawList, idleBoard, in camera, scale, time);
        CrawlerRenderer.DrawBullet(drawList, idleBoard, in camera, Accent, scale);
        if (!idleBoard.Dying)
        {
            CrawlerRenderer.DrawPlayer(drawList, idleBoard.Player, in camera, Accent, scale, 1f);
        }

        drawList.PopClipRect();
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var accent = Accent;
        time += context.RawDeltaSeconds;
        PlaceCamera(context, scale);
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        if (!finished)
        {
            board.Step(simDelta, controls);
            TrailBullet(simDelta);
            ReactToEvents(context, accent);
        }

        DecayPops(context.RawDeltaSeconds);
        zoneHeat = MathF.Max(0f, zoneHeat - context.RawDeltaSeconds * ZoneHeatDecay);
        particles.Update(context.RawDeltaSeconds);
        fx.Update(context.RawDeltaSeconds);
        bannerProgress = GameBanner.Advance(bannerProgress, context.DeltaSeconds, WaveBannerSeconds);
        var field = CrawlerRenderer.FieldRect(in camera);
        CrawlerRenderer.DrawField(drawList, field, in camera, accent, scale, zoneHeat);
        drawList.PushClipRect(field.Min, field.Max, true);
        CrawlerRenderer.DrawMushrooms(drawList, board, in camera, accent, scale, pops);
        CrawlerRenderer.DrawCrawler(drawList, board, in camera, scale, time);
        CrawlerRenderer.DrawSpider(drawList, board, in camera, scale, time);
        CrawlerRenderer.DrawFlea(drawList, board, in camera, scale, time);
        bulletTrail.Draw(drawList, in camera, NeonStroke.Core(accent) with { W = 0.7f }, camera.Px(BulletTrailWidth),
            true);
        CrawlerRenderer.DrawBullet(drawList, board, in camera, accent, scale);
        if (!board.Dying && !board.GameOver)
        {
            CrawlerRenderer.DrawPlayer(drawList, board.Player, in camera, accent, scale, 1f);
        }

        particles.Draw(drawList, in camera);
        drawList.PopClipRect();
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        var pad = DrawPad(drawList, context, accent, scale);
        controls = context.Session.State == StageFlow.Playing && !finished
            ? ReadControls(in pad, field, context)
            : default;
        GameBanner.Draw(drawList, camera.ToScreen(new Vector2(CrawlerBoard.Columns * 0.5f, CrawlerBoard.Rows * 0.4f)),
            bannerText, accent, context.Theme, bannerProgress);
        context.Hud.Score(board.Score);
        context.Hud.Lives(board.Lives, Math.Max(CrawlerBoard.StartLives, board.Lives));
        context.Hud.Level(board.Wave);
        context.Hud.Combo(board.Combo);
        context.Hud.Best(context.Session.Best);
        context.Session.Report(board.Score);
        if (!board.GameOver || finished)
        {
            return;
        }

        finished = true;
        Finish(context);
    }

    private void PlaceCamera(in GameContext context, float scale)
    {
        var full = context.Full;
        var band = StageLayout.PadBand(full, StageLayout.ShooterBand, scale);
        var view = new Rect(new Vector2(full.Min.X + StageLayout.SafeSide * scale, context.Safe.Min.Y),
            new Vector2(full.Max.X - StageLayout.SafeSide * scale, band.Min.Y));
        camera.Fit(view, CrawlerBoard.Columns, CrawlerBoard.Rows, FitMode.Contain);
        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, scale);
        context.Backdrop.SetCamera(in camera);
    }

    private static ShooterPadInput DrawPad(ImDrawListPtr drawList, in GameContext context, Vector4 accent, float scale)
    {
        var band = StageLayout.PadBand(context.Full, StageLayout.ShooterBand, scale);
        var panel = new Rect(new Vector2(band.Min.X + PadInsetX * scale, band.Min.Y + PadInsetY * scale),
            new Vector2(band.Max.X - PadInsetX * scale, band.Max.Y - PadInsetY * scale));
        Material.Frosted(drawList, panel.Min, panel.Max, Metrics.Radius.Lg * scale, scale, PadOpacity);
        return GamePad.Shooter(panel, accent, context.Theme);
    }

    private CrawlerControls ReadControls(in ShooterPadInput pad, Rect field, in GameContext context)
    {
        PressSurface.Claim(FieldSurfaceId, field, out var activated);
        var mouse = ImGui.GetMousePos();
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            dragging = false;
        }
        else if (activated && !context.ChromeHit(mouse))
        {
            dragging = true;
        }

        var left = pad.Left || GameInput.Held(ImGuiKey.A, ImGuiKey.LeftArrow);
        var right = pad.Right || GameInput.Held(ImGuiKey.D, ImGuiKey.RightArrow);
        var up = GameInput.Held(ImGuiKey.UpArrow);
        var down = GameInput.Held(ImGuiKey.DownArrow);
        var fire = pad.Fire || dragging || GameInput.Held(ImGuiKey.Space, ImGuiKey.W);
        return new CrawlerControls((right ? 1f : 0f) - (left ? 1f : 0f), (down ? 1f : 0f) - (up ? 1f : 0f), fire,
            dragging, dragging ? camera.ToWorld(mouse) : Vector2.Zero);
    }

    private static CrawlerControls Autopilot(CrawlerBoard target)
    {
        var aimX = target.Player.X;
        var nearest = float.MaxValue;
        for (var index = 0; index < target.SegmentCapacity; index++)
        {
            if (!target.SegmentAt(index).Alive)
            {
                continue;
            }

            var position = target.SegmentPosition(index);
            if (position.X < 0f || position.X > CrawlerBoard.Columns)
            {
                continue;
            }

            var distance = CrawlerBoard.Rows - position.Y;
            if (distance >= nearest)
            {
                continue;
            }

            nearest = distance;
            aimX = position.X;
        }

        if (target.SpiderActive && target.Spider.X > 0f && target.Spider.X < CrawlerBoard.Columns)
        {
            aimX = target.Spider.X;
        }

        var goal = new Vector2(aimX, CrawlerBoard.Rows - 0.6f);
        return new CrawlerControls(0f, 0f, true, true, goal);
    }

    private void TrailBullet(float deltaSeconds)
    {
        if (board.ShotFiredThisFrame)
        {
            bulletTrail.Clear();
        }

        if (!board.HasBullet)
        {
            bulletTrail.Clear();
            return;
        }

        if (deltaSeconds > 0f)
        {
            bulletTrail.Push(board.Bullet);
        }
    }

    private void DecayPops(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        for (var index = 0; index < pops.Length; index++)
        {
            if (pops[index] > 0f)
            {
                pops[index] = MathF.Max(0f, pops[index] - deltaSeconds * PopDecay);
            }
        }
    }

    private void Pop(Vector2 position)
    {
        var column = (int)MathF.Floor(position.X);
        var row = (int)MathF.Floor(position.Y);
        if (!CrawlerBoard.InField(column, row))
        {
            return;
        }

        pops[CrawlerBoard.CellIndex(column, row)] = 1f;
    }

    private void ReactToEvents(in GameContext context, Vector4 accent)
    {
        if (board.ShotFiredThisFrame)
        {
            UiFeedback.Play(UiSound.GameShoot);
            particles.Emit(Sparks.WithDirection(-MathF.PI * 0.5f, 0.7f), board.Bullet, 3);
        }

        for (var index = 0; index < board.HitCount; index++)
        {
            OnSegmentHit(board.HitAt(index), context);
        }

        for (var index = 0; index < board.ChipCount; index++)
        {
            var chip = board.ChipAt(index);
            particles.Emit(MushroomChips, chip, 5);
            var gone = board.MushroomAt((int)chip.X, (int)chip.Y) == 0;
            UiFeedback.Play(gone ? UiSound.GameBreak : UiSound.GameHitWood);
            if (gone)
            {
                particles.Emit(Sparks, chip, 6);
            }
        }

        if (board.ComboRaisedThisFrame)
        {
            GameSfx.ComboTierUp();
        }

        if (board.ZoneBreachedThisFrame)
        {
            UiFeedback.Play(UiSound.GameTick);
            zoneHeat = 1f;
            context.Fx.Vignette(CrawlerRenderer.BodyColor, 0.25f, 0.8f);
            fx.AddText(Loc.T(L.Crawler.Incoming),
                camera.ToScreen(new Vector2(CrawlerBoard.Columns * 0.5f, CrawlerBoard.ZoneTop - 0.6f)),
                CrawlerRenderer.BodyColor, 1.1f);
        }

        if (board.SpiderArrivedThisFrame)
        {
            UiFeedback.Play(UiSound.GameTick);
            zoneHeat = MathF.Max(zoneHeat, 0.6f);
        }

        if (board.SpiderShotThisFrame)
        {
            OnSpiderShot(context);
        }

        if (board.FleaWoundedThisFrame)
        {
            UiFeedback.Play(UiSound.GameHitSoft);
            particles.Emit(Sparks, board.Flea, 8);
        }

        if (board.FleaShotThisFrame)
        {
            UiFeedback.Play(UiSound.GameExplosion);
            particles.Emit(FleaShards, board.FleaShotPosition, 12);
            particles.Emit(Blast, board.FleaShotPosition, 1);
            fx.AddText(GameNumber.Signed(CrawlerBoard.FleaPoints), camera.ToScreen(board.FleaShotPosition),
                CrawlerRenderer.FleaColor, 1.1f);
            camera.Shake(0.15f);
        }

        if (board.FleaDroppedThisFrame)
        {
            Pop(board.Flea);
        }

        if (board.PlayerLostThisFrame)
        {
            OnPlayerLost(context);
        }

        if (board.RegrowThisFrame)
        {
            OnRegrow();
        }

        if (board.ExtraLifeThisFrame)
        {
            UiFeedback.Play(UiSound.GamePowerUp);
            fx.AddText(Loc.T(L.Crawler.ExtraLife), camera.ToScreen(board.Player), Gold, 1.2f);
            context.Fx.Flash(Gold, 0.14f);
        }

        if (board.WaveClearedThisFrame)
        {
            GameSfx.LevelClear();
            context.Fx.Sweep();
            context.Fx.SlowMo(0.5f, 0.35f);
            context.Fx.Flash(GamePalette.Lighten(accent, 0.4f), 0.12f);
            fx.AddText(Loc.T(L.Crawler.WaveClear),
                camera.ToScreen(new Vector2(CrawlerBoard.Columns * 0.5f, CrawlerBoard.Rows * 0.45f)), accent, 1.4f);
        }

        if (board.WaveStartedThisFrame)
        {
            ShowWaveBanner();
        }
    }

    private void OnSegmentHit(in SegmentHit hit, in GameContext context)
    {
        var color = hit.Head ? CrawlerRenderer.HeadColor : CrawlerRenderer.BodyColor;
        particles.Emit(hit.Head ? HeadShards : SegmentShards, hit.Position, hit.Head ? 12 : 8);
        particles.Emit(Sparks, hit.Position, 5);
        particles.Emit(SegmentRing, hit.Position, 1);
        Pop(hit.Position);
        var screen = camera.ToScreen(hit.Position);
        fx.AddText(GameNumber.Signed(hit.Points), screen, color, hit.Head ? 1.15f : 0.85f);
        if (!hit.Head)
        {
            UiFeedback.Play(UiSound.GameHitSoft);
            camera.Shake(0.05f);
            return;
        }

        UiFeedback.Play(UiSound.GamePop);
        fx.Shockwave(screen, camera.Px(1.6f), color with { W = 0.6f }, 0.3f, 2f);
        camera.Shake(0.12f);
        fx.HitStop(0.03f);
        context.Fx.Punch(0.03f);
    }

    private void OnSpiderShot(in GameContext context)
    {
        var position = board.SpiderShotPosition;
        var big = board.SpiderPoints >= 900;
        UiFeedback.Play(big ? UiSound.GamePowerUp : UiSound.GameExplosion);
        particles.Emit(SpiderShards, position, 16);
        particles.Emit(Sparks, position, 10);
        particles.Emit(Blast, position, 1);
        var screen = camera.ToScreen(position);
        fx.Shockwave(screen, camera.Px(2.4f), CrawlerRenderer.SpiderColor with { W = 0.7f }, 0.4f, 2.6f);
        fx.AddText(GameNumber.Signed(board.SpiderPoints), screen, big ? Gold : CrawlerRenderer.SpiderColor,
            big ? 1.4f : 1.15f);
        camera.Shake(0.25f);
        fx.HitStop(0.05f);
        context.Fx.Punch(big ? 0.06f : 0.04f);
        if (big)
        {
            context.Fx.Flash(Gold, 0.14f);
        }
    }

    private void OnPlayerLost(in GameContext context)
    {
        UiFeedback.Play(UiSound.GameExplosion);
        var position = board.Player;
        particles.Emit(PlayerShards, position, 18);
        particles.Emit(Sparks, position, 22);
        particles.Emit(Blast, position, 2);
        fx.Shockwave(camera.ToScreen(position), camera.Px(5f), Danger with { W = 0.7f }, 0.6f, 3.4f);
        camera.Shake(0.75f);
        fx.HitStop(0.08f);
        context.Fx.Punch(0.06f);
        context.Fx.Flash(Danger, 0.38f);
        context.Fx.SlowMo(0.4f, 0.6f);
        bulletTrail.Clear();
        if (board.Lives == 1)
        {
            context.Fx.Vignette(Danger, 0.5f, 1.4f);
        }
        else if (board.Lives <= 0)
        {
            context.Fx.Vignette(Danger, 0.6f, 1.6f);
        }
    }

    private void OnRegrow()
    {
        if (board.RegrownCount == 0)
        {
            return;
        }

        UiFeedback.Play(UiSound.GameCollect);
        for (var index = 0; index < board.RegrownCount; index++)
        {
            var cell = board.RegrownCell(index);
            var center = CrawlerBoard.CellCenter(cell % CrawlerBoard.Columns, cell / CrawlerBoard.Columns);
            pops[cell] = 1f + index * RegrowStagger;
            particles.Emit(Regrowth, center, 2);
        }
    }

    private void ShowWaveBanner()
    {
        bannerText = waveLabel.Get(L.Crawler.WaveNumber, board.Wave);
        bannerProgress = 0f;
    }

    private void Finish(in GameContext context)
    {
        var outcome = new GameOutcome(board.Score, ScoreKind.Score, GameId)
            .WithStat(L.Crawler.WavesCleared, GameNumber.Label(board.WavesCleared))
            .WithStat(L.Crawler.Segments, GameNumber.Label(board.SegmentsShot))
            .WithStat(L.Crawler.Spiders, GameNumber.Label(board.SpidersShot));
        if (board.ShotsFired > 0)
        {
            var percent = board.ShotsHit * 100 / board.ShotsFired;
            outcome = outcome.WithStat(L.Crawler.Accuracy, Loc.T(L.Crawler.Percent, GameNumber.Label(percent)));
        }

        context.Session.Finish(outcome);
    }
}
