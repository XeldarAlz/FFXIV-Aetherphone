using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Coil;

internal sealed class CoilApp : IMiniGame
{
    private const string GameId = "coil";
    private const float StageBannerSeconds = 2.4f;
    private const float ComboBannerSeconds = 1.2f;
    private const float ChainCapsuleSeconds = 1.6f;
    private const float ChainCapsuleWidth = 56f;
    private const float AimFollowRate = 22f;
    private const float RecoilDecay = 6f;
    private const float SwapDecay = 7f;
    private const float HintFadeRate = 2.5f;
    private const float DangerShake = 0.9f;
    private const float ChainPunch = 0.04f;
    private const int BigChain = 3;
    private const int IdleSeed = 7;
    private const int MaxChainLabel = 16;

    private static readonly LocString[] StageNames =
    {
        L.Coil.StageWhirlpool, L.Coil.StageMeander, L.Coil.StageTwinCoil, L.Coil.StageHeart, L.Coil.StageZigzag,
        L.Coil.StageStarburst, L.Coil.StageHourglass, L.Coil.StageClover, L.Coil.StageKeystone, L.Coil.StagePaperclip,
        L.Coil.StageFigureEight, L.Coil.StageRipple,
    };

    private static readonly CoilPower[] TimedPowers = { CoilPower.Freeze, CoilPower.Slow, CoilPower.Reverse, CoilPower.Guide };
    private static readonly GameSpec StageSpec = new(GameId, L.Coil.Title, GameGenre.Puzzle, L.Coil.Hook,
        Backdrop.Cavern, HudStyle.Standard, ScoreKind.Score, clocked: true, keyboard: true);
    private static readonly Vector4 Gold = new(1f, 0.88f, 0.45f, 1f);
    private static readonly Vector4 Cream = new(1f, 1f, 0.9f, 1f);
    private static readonly Vector4 ChainGold = new(1f, 0.86f, 0.42f, 1f);
    private static readonly Vector4 BigChainSpark = new(1f, 0.95f, 0.7f, 1f);
    private static readonly Vector4 StageSpark = new(1f, 0.95f, 0.8f, 1f);
    private static readonly Vector4 LandRing = new(1f, 1f, 1f, 0.5f);
    private static readonly Vector4 ChipTrack = new(1f, 1f, 1f, 0.14f);
    private static readonly ParticleSpec[] ClearBursts = BuildColourSpecs(0.0072f, 0.42f, 0.45f, 0.44f, 1.6f,
        ParticleShape.GlowCircle, 0f);
    private static readonly ParticleSpec[] ClearSparkles = BuildColourSpecs(0.0067f, 0.36f, 0.6f, 0.11f, 2.4f,
        ParticleShape.Star, 0.35f);
    private static readonly ParticleSpec[] SwallowBursts = BuildColourSpecs(0.006f, 0.33f, 0.5f, 0f, 1.6f,
        ParticleShape.Circle, 0f);
    private static readonly ParticleSpec[] ConfettiSpecs = BuildConfetti();
    private static readonly ParticleSpec[] PowerSparkles = BuildPowerSparkles();
    private static readonly ParticleSpec GapStreaks = new(Gold, Gold, 0.006f, 0.9f, 0.5f, 0.6f, 1.6f,
        shape: ParticleShape.Streak);
    private static readonly ParticleSpec GapSparkle = new(Cream, Cream, 0.0072f, 0.44f, 0.7f, 0.11f, 2.4f, 6f,
        shape: ParticleShape.Star);
    private static readonly ParticleSpec BigChainSparkle = new(BigChainSpark, BigChainSpark, 0.0083f, 0.6f, 0.8f, 0.11f,
        2.4f, 6f, shape: ParticleShape.Star, additive: true);
    private static readonly ParticleSpec StageSparkle = new(StageSpark, StageSpark, 0.0083f, 0.55f, 0.9f, 0.11f, 2.4f,
        6f, shape: ParticleShape.Star, additive: true);
    private static readonly string[] ChainLabels = BuildChainLabels();
    private static readonly Dictionary<int, string> PlusLabels = new();

    private readonly CoilBoard board = new(IdleSeed);
    private readonly ParticleSystem particles = new(640);
    private readonly FeedbackFx fx = new();
    private Camera2D camera = Camera2D.Create();
    private LabelSlot chainBannerLabel;
    private float recoil;
    private float swapPulse;
    private float turretAngle = -MathF.PI * 0.5f;
    private float stageBanner = 1f;
    private float comboBanner = 1f;
    private float chainCapsule;
    private float hintAlpha;
    private int chainShown;
    private int maxChain;
    private int powerUpsUsed;
    private bool chainCapsulePlaced;
    private bool fired;
    private bool finished;
    private string comboText = string.Empty;
    private string stageLabel = string.Empty;
    private string? stageLabelPrefix;
    private int stageLabelNumber = -1;

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        var random = start.Random;
        board.Reset((int)random.NextUInt());
        board.Begin();
        particles.Clear();
        fx.Clear();
        camera = Camera2D.Create();
        recoil = 0f;
        swapPulse = 0f;
        stageBanner = 0f;
        comboBanner = 1f;
        chainCapsule = 0f;
        chainCapsulePlaced = false;
        chainShown = 0;
        maxChain = 0;
        powerUpsUsed = 0;
        hintAlpha = 1f;
        fired = false;
        finished = false;
    }

    public void Close()
    {
        particles.Clear();
        fx.Clear();
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        PlaceCamera(context);
        var time = (float)ImGui.GetTime();
        DrawWorld(ImGui.GetWindowDrawList(), CoilShapes.Polar(turretAngle), UiScale.Current, time);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var theme = context.Theme;
        var time = (float)ImGui.GetTime();
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        particles.Update(context.RawDeltaSeconds);
        fx.Update(context.RawDeltaSeconds);
        PlaceCamera(context);
        board.BeginFrame();
        var aim = AimAt();
        if (!finished && context.Session.State == StageFlow.Playing)
        {
            HandleInput(context.Safe, aim);
        }

        board.Tick(simDelta);
        ReactToEvents(context, theme);
        Animate(aim, context);
        var turret = CoilShapes.Polar(turretAngle);
        DrawWorld(drawList, turret, scale, time);
        var field = CoilRenderer.FieldRect(in camera);
        DrawPowerChips(drawList, field, scale);
        DrawBanners(drawList, field, theme, scale);
        DrawHint(drawList, field, theme, scale);
        DrawChainCapsule(drawList, context, scale);
        context.Hud.Score(board.Score);
        context.Hud.Lives(board.Lives, CoilBoard.StartLives);
        context.Hud.Level(board.Stage);
        context.Hud.Best(context.Session.Best);
        context.Session.Report(board.Score);
    }

    private void PlaceCamera(in GameContext context)
    {
        camera.Fit(context.Safe, CoilBoard.FieldWidth, CoilBoard.FieldHeight, FitMode.Contain);
        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, UiScale.Current);
        context.Backdrop.SetCamera(in camera);
    }

    private Vector2 AimAt()
    {
        var direction = camera.ToWorld(ImGui.GetMousePos()) - board.Launcher;
        return direction.LengthSquared() < 0.000001f ? CoilShapes.Polar(turretAngle) : Vector2.Normalize(direction);
    }

    private void HandleInput(Rect area, Vector2 aim)
    {
        if (board.State != CoilState.Playing)
        {
            return;
        }

        var hovered = UiInteract.Hover(area.Min, area.Max);
        var swapKey = GameInput.Pressed(ImGuiKey.Space);
        if ((hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right)) || swapKey)
        {
            if (board.Swap())
            {
                swapPulse = 1f;
                UiFeedback.Play(UiSound.GameTick);
            }

            return;
        }

        if (!hovered || !ImGui.IsMouseClicked(ImGuiMouseButton.Left) || !board.Fire(aim))
        {
            return;
        }

        recoil = 1f;
        fired = true;
        UiFeedback.Play(UiSound.GameShoot);
    }

    private void Animate(Vector2 aim, in GameContext context)
    {
        var raw = context.RawDeltaSeconds;
        var target = MathF.Atan2(aim.Y, aim.X);
        var difference = MathF.IEEERemainder(target - turretAngle, MathF.Tau);
        turretAngle += difference * MathF.Min(1f, context.DeltaSeconds * AimFollowRate);
        recoil = MathF.Max(0f, recoil - raw * RecoilDecay);
        swapPulse = MathF.Max(0f, swapPulse - raw * SwapDecay);
        stageBanner = GameBanner.Advance(stageBanner, raw, StageBannerSeconds);
        comboBanner = GameBanner.Advance(comboBanner, raw, ComboBannerSeconds);
        chainCapsule = MathF.Max(0f, chainCapsule - raw / ChainCapsuleSeconds);
        if (fired)
        {
            hintAlpha = MathF.Max(0f, hintAlpha - raw * HintFadeRate);
        }

        if (board.State == CoilState.Draining)
        {
            context.Fx.Vignette(CoilRenderer.Danger, 0.32f * (0.65f + 0.35f * Pulse.Wave(Pulse.Fast)), 0.3f);
            return;
        }

        if (board.State != CoilState.Playing || board.Danger <= 0f)
        {
            return;
        }

        camera.Shake(context.DeltaSeconds * DangerShake * board.Danger);
        context.Fx.Vignette(CoilRenderer.Danger, 0.32f * board.Danger * (0.65f + 0.35f * Pulse.Wave(Pulse.Fast)), 0.3f);
    }

    private void ReactToEvents(in GameContext context, PhoneTheme theme)
    {
        var radius = camera.Px(CoilBoard.MarbleRadius);
        ReactToClears(context, radius);
        ReactToGaps(radius);
        ReactToPowers(radius);
        var swallowed = board.Swallowed;
        for (var index = 0; index < swallowed.Length; index++)
        {
            particles.Emit(SwallowBursts[swallowed[index].Colour % CoilArt.MarbleColours.Length],
                swallowed[index].Position, 4);
        }

        if (board.LandedThisFrame && board.Clears.Length == 0)
        {
            fx.Shockwave(camera.ToScreen(board.LandPosition), radius * 2.2f, LandRing, 0.22f, 1.6f, radius);
            UiFeedback.Play(UiSound.GamePiece);
        }

        if (board.DrainStartedThisFrame)
        {
            UiFeedback.Play(UiSound.GameWrong);
            context.Fx.Flash(theme.Danger, 0.35f);
            context.Fx.SlowMo(0.6f, 0.4f);
            camera.Shake(0.55f);
        }

        if (board.LifeLostThisFrame)
        {
            UiFeedback.Play(UiSound.GameHitSoft);
            camera.Shake(0.3f);
        }

        if (board.StageClearedThisFrame)
        {
            OnStageCleared(context);
        }

        if (board.StageStartedThisFrame)
        {
            stageBanner = 0f;
        }

        if (!board.GameOverThisFrame || finished)
        {
            return;
        }

        finished = true;
        context.Session.Finish(new GameOutcome(board.Score, ScoreKind.Score, GameId)
            .WithStat(L.Games.Stage, GameNumber.Label(board.Stage))
            .WithStat(L.Coil.MaxChain, GameNumber.Label(maxChain))
            .WithStat(L.Coil.PowerUps, GameNumber.Label(powerUpsUsed)));
    }

    private void OnStageCleared(in GameContext context)
    {
        GameSfx.LevelClear();
        context.Fx.Sweep();
        context.Fx.Flash(GamePalette.Lighten(Accent, 0.4f), 0.22f);
        var top = new Vector2(CoilBoard.FieldWidth * 0.5f, CoilBoard.FieldHeight * 0.12f);
        for (var colour = 0; colour < ConfettiSpecs.Length; colour++)
        {
            particles.Emit(ConfettiSpecs[colour], top, 15);
        }

        particles.Emit(StageSparkle, board.Track.End, 24);
    }

    private void ReactToClears(in GameContext context, float radius)
    {
        var bursts = board.Bursts;
        for (var index = 0; index < bursts.Length; index++)
        {
            var colour = bursts[index].Colour % CoilArt.MarbleColours.Length;
            particles.Emit(ClearSparkles[colour], bursts[index].Position, 5);
            particles.Emit(ClearBursts[colour], bursts[index].Position, 6);
        }

        var clears = board.Clears;
        var deepest = 0;
        for (var index = 0; index < clears.Length; index++)
        {
            ref readonly var clear = ref clears[index];
            var center = camera.ToScreen(clear.Center);
            var colour = CoilArt.ColourOf(clear.Colour);
            fx.Shockwave(center, radius * (3.2f + clear.Count * 0.5f), GamePalette.Lighten(colour, 0.3f), 0.45f, 3f,
                radius);
            fx.AddText(PlusLabel(clear.Points), center, GamePalette.Lighten(colour, 0.4f), 1.1f);
            camera.Shake(MathF.Min(0.3f, 0.04f * clear.Count * clear.Multiplier));
            deepest = Math.Max(deepest, clear.Multiplier);
        }

        if (clears.Length == 0)
        {
            return;
        }

        maxChain = Math.Max(maxChain, deepest);
        UiFeedback.Play(deepest >= 2 ? UiSound.GameMatch : UiSound.GamePop);
        if (deepest < 2)
        {
            return;
        }

        comboText = chainBannerLabel.Get(L.Coil.Chain, deepest);
        comboBanner = 0f;
        chainShown = deepest;
        chainCapsule = 1f;
        context.Fx.Punch(ChainPunch);
        if (deepest < BigChain)
        {
            return;
        }

        fx.HitStop(MathF.Min(0.1f, 0.04f + 0.015f * (deepest - BigChain)));
        context.Fx.Flash(GamePalette.Lighten(Accent, 0.5f), 0.18f);
        particles.Emit(BigChainSparkle, new Vector2(CoilBoard.FieldWidth * 0.5f, CoilBoard.FieldHeight * 0.5f), 18);
    }

    private void ReactToGaps(float radius)
    {
        var gaps = board.Gaps;
        for (var index = 0; index < gaps.Length; index++)
        {
            var position = gaps[index].Position;
            var center = camera.ToScreen(position);
            particles.Emit(GapStreaks, position, 12);
            particles.Emit(GapSparkle, position, 10);
            fx.Shockwave(center, radius * 4.5f, Gold, 0.5f, 3.2f, radius);
            fx.AddText(Loc.T(L.Coil.GapShot), center - new Vector2(0f, radius * 3.2f), Gold, 1.05f);
            fx.AddText(PlusLabel(gaps[index].Points), center - new Vector2(0f, radius * 1.4f), Gold, 1f);
            UiFeedback.Play(UiSound.GameCollect);
        }
    }

    private void ReactToPowers(float radius)
    {
        var triggers = board.Triggers;
        for (var index = 0; index < triggers.Length; index++)
        {
            var power = triggers[index].Power;
            var position = triggers[index].Position;
            var center = camera.ToScreen(position);
            var color = CoilArt.PowerColour(power);
            powerUpsUsed++;
            fx.Shockwave(center, radius * 7f, color, 0.6f, 3.6f, radius);
            fx.AddText(Loc.T(PowerName(power)), center - new Vector2(0f, radius * 2.6f), color, 1.2f);
            particles.Emit(PowerSparkles[(int)power], position, 16);
            UiFeedback.Play(UiSound.GamePowerUp);
        }
    }

    private static LocString PowerName(CoilPower power) => power switch
    {
        CoilPower.Freeze => L.Coil.PowerFreeze,
        CoilPower.Slow => L.Coil.PowerSlow,
        CoilPower.Reverse => L.Coil.PowerReverse,
        CoilPower.Blast => L.Coil.PowerBlast,
        CoilPower.Prism => L.Coil.PowerPrism,
        _ => L.Coil.PowerGuide,
    };

    private string StageLabel()
    {
        var prefix = Loc.T(L.Games.Stage);
        if (ReferenceEquals(prefix, stageLabelPrefix) && stageLabelNumber == board.Stage)
        {
            return stageLabel;
        }

        stageLabelPrefix = prefix;
        stageLabelNumber = board.Stage;
        stageLabel = string.Concat(prefix, " ", GameNumber.Label(board.Stage));
        return stageLabel;
    }

    private static string PlusLabel(int points)
    {
        if (PlusLabels.TryGetValue(points, out var label))
        {
            return label;
        }

        label = string.Concat("+", GameNumber.Label(points));
        PlusLabels[points] = label;
        return label;
    }

    private void DrawWorld(ImDrawListPtr drawList, Vector2 turret, float scale, float time)
    {
        CoilRenderer.DrawTrack(drawList, board, in camera, Accent, scale, time);
        CoilRenderer.DrawVortex(drawList, board, in camera, Accent, time);
        CoilRenderer.DrawMarbles(drawList, board, in camera, time);
        CoilRenderer.DrawShots(drawList, board, in camera, time);
        if (board.State == CoilState.Playing)
        {
            CoilRenderer.DrawAim(drawList, board, in camera, turret, scale);
        }

        CoilRenderer.DrawLauncher(drawList, board, in camera, turret, Accent, recoil, swapPulse, time);
        particles.Draw(drawList, in camera);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
    }

    private void DrawPowerChips(ImDrawListPtr drawList, Rect field, float scale)
    {
        var chipSize = new Vector2(50f * scale, 22f * scale);
        var cursor = field.Min + new Vector2(8f * scale, 8f * scale);
        for (var index = 0; index < TimedPowers.Length; index++)
        {
            var power = TimedPowers[index];
            var left = TimeLeft(power);
            if (left <= 0f)
            {
                continue;
            }

            DrawChip(drawList, cursor, chipSize, power, left / Duration(power), false, scale);
            cursor.X += chipSize.X + 6f * scale;
        }

        if (board.ArmedPower != CoilPower.None)
        {
            DrawChip(drawList, cursor, chipSize, board.ArmedPower, 1f, true, scale);
        }
    }

    private static void DrawChip(ImDrawListPtr drawList, Vector2 min, Vector2 size, CoilPower power, float fraction,
        bool armed, float scale)
    {
        var max = min + size;
        var color = CoilArt.PowerColour(power);
        var radius = size.Y * 0.5f;
        Material.Frosted(drawList, min, max, radius, scale, 0.9f);
        var pulse = armed ? 0.5f + 0.5f * MathF.Sin((float)ImGui.GetTime() * 6f) : 0f;
        Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(color with { W = 0.45f + pulse * 0.45f }),
            1.2f * scale);
        var glyphCenter = new Vector2(min.X + radius, min.Y + radius);
        drawList.AddCircleFilled(glyphCenter, radius * 0.72f, ImGui.GetColorU32(GamePalette.Darken(color, 0.55f)));
        CoilArt.PowerGlyph(drawList, glyphCenter, radius * 0.48f, power, ImGui.GetColorU32(color), 1.4f * scale);
        var barMin = new Vector2(min.X + radius * 2f + 2f * scale, min.Y + radius - 2f * scale);
        var barMax = new Vector2(max.X - 8f * scale, barMin.Y + 4f * scale);
        drawList.AddRectFilled(barMin, barMax, ImGui.GetColorU32(ChipTrack), 2f * scale);
        var fillMax = new Vector2(barMin.X + (barMax.X - barMin.X) * Math.Clamp(fraction, 0f, 1f), barMax.Y);
        drawList.AddRectFilled(barMin, fillMax, ImGui.GetColorU32(color with { W = armed ? 0.5f + pulse * 0.5f : 1f }),
            2f * scale);
    }

    private float TimeLeft(CoilPower power) => power switch
    {
        CoilPower.Freeze => board.FreezeLeft,
        CoilPower.Slow => board.SlowLeft,
        CoilPower.Reverse => board.ReverseLeft,
        _ => board.GuideLeft,
    };

    private static float Duration(CoilPower power) => power switch
    {
        CoilPower.Freeze => CoilBoard.FreezeSeconds,
        CoilPower.Slow => CoilBoard.SlowSeconds,
        CoilPower.Reverse => CoilBoard.ReverseSeconds,
        _ => CoilBoard.GuideSeconds,
    };

    private void DrawBanners(ImDrawListPtr drawList, Rect field, PhoneTheme theme, float scale)
    {
        var upper = new Vector2(field.Center.X, field.Min.Y + field.Height * 0.2f);
        GameBanner.Draw(drawList, upper, StageLabel(), Accent, theme, stageBanner);
        GameBanner.Draw(drawList, upper + new Vector2(0f, 44f * scale), Loc.T(StageNames[CoilBoard.ShapeFor(board.Stage)]),
            Accent, theme, stageBanner, TextStyles.Headline);
        GameBanner.Draw(drawList, new Vector2(field.Center.X, field.Min.Y + field.Height * 0.36f), comboText, ChainGold,
            theme, comboBanner, TextStyles.Title1);
        if (board.State != CoilState.StageClear)
        {
            return;
        }

        var progress = board.StageClearProgress;
        var center = new Vector2(field.Center.X, field.Min.Y + field.Height * 0.42f);
        GameBanner.Draw(drawList, center, Loc.T(L.Coil.StageClear), Accent, theme, progress, TextStyles.Title1);
        var alpha = progress < 0.15f ? progress / 0.15f : progress > 0.8f ? (1f - progress) / 0.2f : 1f;
        var counted = (int)(board.ClearBonus * Math.Clamp((progress - 0.12f) * 2.4f, 0f, 1f));
        var bonusCenter = center + new Vector2(0f, 52f * scale);
        Typography.DrawCentered(drawList, bonusCenter, Loc.Upper(Loc.T(L.Coil.Bonus)), theme.TextMuted with { W = alpha },
            TextStyles.Caption1.Scale, TextStyles.Caption1.Weight);
        Typography.DrawCentered(drawList, bonusCenter + new Vector2(0f, 22f * scale), GameNumber.Label(counted),
            GamePalette.Lighten(Accent, 0.45f) with { W = alpha }, TextStyles.Title2.Scale, TextStyles.Title2.Weight);
    }

    private void DrawHint(ImDrawListPtr drawList, Rect field, PhoneTheme theme, float scale)
    {
        if (hintAlpha <= 0f)
        {
            return;
        }

        var width = field.Width * 0.86f;
        var text = Loc.T(L.Coil.HowTo);
        var height = Typography.MeasureWrappedBlock(text, TextStyles.Footnote, width).Y;
        var center = new Vector2(field.Center.X, field.Max.Y - height * 0.5f - 6f * scale);
        Typography.DrawWrappedCentered(drawList, center, text, theme.TextMuted with { W = hintAlpha }, TextStyles.Footnote,
            width);
    }

    private void DrawChainCapsule(ImDrawListPtr drawList, in GameContext context, float scale)
    {
        if (chainCapsule <= 0f || chainShown < 2)
        {
            chainCapsulePlaced = false;
            return;
        }

        context.Hud.Custom(ChainCapsuleWidth);
        var rect = context.Hud.CustomRect(0);
        var placed = chainCapsulePlaced;
        chainCapsulePlaced = true;
        if (!placed || rect.Width <= 0f)
        {
            return;
        }

        StageHud.Capsule(drawList, rect, scale);
        var alpha = MathF.Min(1f, chainCapsule * 4f);
        Typography.DrawCentered(drawList, rect.Center, ChainLabels[Math.Min(chainShown, MaxChainLabel)],
            ChainGold with { W = alpha }, TextStyles.Headline);
    }

    private static ParticleSpec[] BuildColourSpecs(float size, float speed, float life, float gravity, float drag,
        ParticleShape shape, float lighten)
    {
        var specs = new ParticleSpec[CoilArt.MarbleColours.Length];
        for (var colour = 0; colour < specs.Length; colour++)
        {
            var tint = lighten > 0f ? GamePalette.Lighten(CoilArt.MarbleColours[colour], lighten) : CoilArt.MarbleColours[colour];
            specs[colour] = new ParticleSpec(tint, tint, size, speed, life, gravity, drag, 6f, shape: shape);
        }

        return specs;
    }

    private static ParticleSpec[] BuildConfetti()
    {
        var specs = new ParticleSpec[CoilArt.MarbleColours.Length];
        for (var colour = 0; colour < specs.Length; colour++)
        {
            var tint = CoilArt.MarbleColours[colour];
            specs[colour] = new ParticleSpec(tint, tint, 0.011f, 0.83f, 1.6f, 1.5f, 0.7f, 16f, 1.4f, -MathF.PI * 0.5f,
                ParticleShape.Square);
        }

        return specs;
    }

    private static ParticleSpec[] BuildPowerSparkles()
    {
        var specs = new ParticleSpec[(int)CoilPower.Guide + 1];
        for (var power = 0; power < specs.Length; power++)
        {
            var tint = CoilArt.PowerColour((CoilPower)power);
            specs[power] = new ParticleSpec(tint, tint, 0.0083f, 0.55f, 0.8f, 0.11f, 2.4f, 6f, shape: ParticleShape.Star,
                additive: true);
        }

        return specs;
    }

    private static string[] BuildChainLabels()
    {
        var labels = new string[MaxChainLabel + 1];
        for (var chain = 0; chain < labels.Length; chain++)
        {
            labels[chain] = string.Concat("x", GameNumber.Label(chain));
        }

        return labels;
    }
}
