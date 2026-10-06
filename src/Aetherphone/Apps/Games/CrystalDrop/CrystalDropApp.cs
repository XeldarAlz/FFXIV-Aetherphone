using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.CrystalDrop;

internal sealed class CrystalDropApp : IMiniGame
{
    private const string GameId = "crystaldrop";
    private const ulong IdleSeed = 11;
    private const int IdleDrops = 7;
    private const float IdleSettleSeconds = 0.55f;
    private const float IdleFrame = 1f / 60f;
    private const float WobbleKick = 0.8f;
    private const float WobbleSmoothSeconds = 0.22f;
    private const float MaxSkew = 0.05f;
    private const float ShardSize = 0.022f;
    private const float PreviewPad = 10f;
    private const float PreviewGap = 6f;
    private const float PreviewCrystalRadius = 9f;
    private const int MaxGain = CrystalDropBoard.ClearPoints * ComboMeter.MaxMultiplier;
    private static readonly GameSpec StageSpec = new(GameId, L.Games.CrystalDrop, GameGenre.Puzzle,
        L.CrystalDrop.Hook, Backdrop.Cavern, HudStyle.Standard, ScoreKind.Score, clocked: true, countdown: true);
    private static readonly string?[] GainLabels = new string?[MaxGain + 1];
    private static readonly string?[] MultiplierLabels = new string?[ComboMeter.MaxMultiplier + 1];
    private static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    private static readonly Vector4 Gold = new(1f, 0.92f, 0.6f, 1f);
    private static readonly Vector4 Dust = new(1f, 1f, 1f, 0.6f);
    private static readonly ParticleSpec[] MergeShards = BuildMergeShards();
    private static readonly ParticleSpec DropDust = new(Dust, Dust with { W = 0f }, 0.012f, 0.3f, 0.35f, 1.5f, 2f);
    private static readonly ParticleSpec ClearStreaks = new(CrystalDropRenderer.TierColor(CrystalDropBoard.MaxTier),
        GamePalette.Lighten(CrystalDropRenderer.TierColor(CrystalDropBoard.MaxTier), 0.5f) with { W = 0f }, 0.03f,
        2.2f, 0.6f, 2f, 1.2f, shape: ParticleShape.Streak, additive: true);
    private static readonly ParticleSpec OverflowShards = new(Danger, Danger with { W = 0f }, 0.02f, 1.4f, 0.7f, 3f,
        1.4f, 6f, shape: ParticleShape.Shard);
    private readonly CrystalDropBoard board = new();
    private readonly ParticleSystem particles = new(320);
    private readonly FeedbackFx fx = new();
    private Camera2D camera = Camera2D.Create();
    private Spring wobble;
    private float pointerX = 0.5f;
    private bool finished;

    public CrystalDropApp()
    {
        BuildIdle();
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        board.Reset(start.Random);
        particles.Clear();
        fx.Clear();
        camera = Camera2D.Create();
        wobble.SnapTo(0f);
        pointerX = 0.5f;
        finished = false;
    }

    public void Close()
    {
        BuildIdle();
        particles.Clear();
        fx.Clear();
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        wobble.Step(0f, WobbleSmoothSeconds, context.RawDeltaSeconds);
        PlaceCamera(context);
        board.Step(context.RawDeltaSeconds);
        DrawWorld(ImGui.GetWindowDrawList(), UiScale.Current);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        particles.Update(context.RawDeltaSeconds);
        fx.Update(context.RawDeltaSeconds);
        wobble.Step(0f, WobbleSmoothSeconds, context.RawDeltaSeconds);
        PlaceCamera(context);
        if (!finished)
        {
            Step(simDelta, context);
        }

        DrawWorld(drawList, scale);
        DrawNextPreview(drawList, context, scale);
        context.Hud.Score(board.Score);
        context.Hud.Combo(board.Combo);
        context.Hud.Best(context.Session.Best);
        context.Session.Report(board.Score);
    }

    private void BuildIdle()
    {
        board.Reset(GameRandom.FromSeed(IdleSeed));
        var idleRandom = GameRandom.FromSeed(IdleSeed);
        for (var drop = 0; drop < IdleDrops; drop++)
        {
            board.Drop(idleRandom.Range(0.2f, 0.8f));
            for (var elapsed = 0f; elapsed < IdleSettleSeconds; elapsed += IdleFrame)
            {
                board.Step(IdleFrame);
            }
        }

        board.ClearMerges();
    }

    private void PlaceCamera(in GameContext context)
    {
        camera.Fit(context.Safe, 1f, CrystalDropRenderer.WorldHeight, FitMode.Contain);
        camera.Place(CrystalDropRenderer.WorldCenter);
        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, UiScale.Current);
        context.Backdrop.SetCamera(in camera);
    }

    private void Step(float deltaSeconds, in GameContext context)
    {
        HandleInput(context);
        var multiplierBefore = board.Combo.Multiplier;
        board.Step(deltaSeconds);
        ConsumeMerges(context);
        if (board.Combo.Multiplier > multiplierBefore)
        {
            GameSfx.ComboTierUp();
        }

        if (board.OverflowSeconds > 0f && !board.GameOver)
        {
            var pulse = 0.6f + 0.4f * Pulse.Wave(Pulse.Fast);
            context.Fx.Vignette(Danger, (0.08f + 0.24f * board.OverflowFraction) * pulse, 0.3f);
        }

        if (!board.GameOver)
        {
            return;
        }

        OnGameOver(context);
        finished = true;
        context.Session.Finish(new GameOutcome(board.Score, ScoreKind.Score, GameId)
            .WithStat(L.Games.Combo, GameNumber.Label(board.BestCombo))
            .WithStat(L.CrystalDrop.Merges, GameNumber.Label(board.Merges))
            .WithStat(L.Games.Time, TimeText.MinutesSeconds((int)context.Session.PlaySeconds)));
    }

    private void HandleInput(in GameContext context)
    {
        var state = context.Session.State;
        if (state is not (StageFlow.Playing or StageFlow.Countdown))
        {
            return;
        }

        var safe = context.Safe;
        if (!UiInteract.Hover(safe.Min, safe.Max))
        {
            return;
        }

        pointerX = camera.ToWorld(ImGui.GetMousePos()).X;
        if (state != StageFlow.Playing)
        {
            return;
        }

        if (board.CanDrop)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        var tier = board.HeldTier;
        var dropX = board.ClampDropX(pointerX);
        if (!board.Drop(pointerX))
        {
            return;
        }

        OnDrop(tier, dropX);
    }

    private void OnDrop(int tier, float dropX)
    {
        UiFeedback.Play(UiSound.GamePop);
        var radius = CrystalDropBoard.RadiusOf(tier);
        particles.Emit(DropDust, new Vector2(dropX, -radius * 0.4f), 5);
        var side = dropX < 0.5f ? -1f : 1f;
        wobble.Launch(wobble.Value, Math.Clamp(wobble.Velocity + side * WobbleKick, -WobbleKick, WobbleKick));
    }

    private void ConsumeMerges(in GameContext context)
    {
        if (board.MergeCount > 0)
        {
            UiFeedback.Play(UiSound.GameMatch);
        }

        var scale = UiScale.Current;
        for (var index = 0; index < board.MergeCount; index++)
        {
            var merge = board.Merge(index);
            var tier = merge.Tier;
            var screen = CrystalDropRenderer.Project(in camera, merge.Position, SkewPixels());
            var color = CrystalDropRenderer.TierColor(tier);
            particles.Emit(MergeShards[tier], merge.Position, 8 + tier * 2);
            fx.Shockwave(screen, camera.Px(0.10f + tier * 0.035f), GamePalette.Lighten(color, 0.4f), 0.45f, 2.8f);
            fx.AddText(GainLabel(merge.Points), screen, GamePalette.Lighten(color, 0.45f), 1f + tier * 0.045f);
            if (merge.Multiplier > 1)
            {
                fx.AddText(MultiplierLabel(merge.Multiplier), screen + new Vector2(0f, -22f * scale), Gold, 1.05f);
            }

            camera.Shake(MathF.Min(0.5f, 0.04f + tier * 0.03f));
            context.Fx.Punch(MathF.Min(0.1f, 0.02f + tier * 0.007f));
            if (!merge.Cleared)
            {
                continue;
            }

            UiFeedback.Play(UiSound.GameBreak);
            context.Fx.Flash(GamePalette.Lighten(color, 0.4f), 0.4f);
            context.Fx.Sweep();
            fx.HitStop(0.06f);
            particles.Emit(ClearStreaks, merge.Position, 16);
        }

        board.ClearMerges();
    }

    private void OnGameOver(in GameContext context)
    {
        UiFeedback.Play(UiSound.GameHitSoft);
        context.Fx.Flash(Danger, 0.45f);
        camera.Shake(0.55f);
        particles.Emit(OverflowShards, new Vector2(0.5f, CrystalDropBoard.DangerLine), 24);
    }

    private float SkewPixels() => camera.Px(Math.Clamp(wobble.Value, -MaxSkew, MaxSkew));

    private void DrawWorld(ImDrawListPtr drawList, float scale)
    {
        var skew = SkewPixels();
        CrystalDropRenderer.DrawJar(drawList, in camera, skew, Accent, scale);
        CrystalDropRenderer.DrawDangerLine(drawList, in camera, board, skew, scale);
        if (!board.GameOver)
        {
            CrystalDropRenderer.DrawHeld(drawList, in camera, board, pointerX, skew, scale);
        }

        CrystalDropRenderer.DrawCrystals(drawList, in camera, board, skew, scale);
        particles.Draw(drawList, in camera);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
    }

    private void DrawNextPreview(ImDrawListPtr drawList, in GameContext context, float scale)
    {
        var label = Loc.T(L.Games.Next);
        var labelWidth = Typography.Measure(label, TextStyles.FootnoteEmphasized).X;
        var width = PreviewPad * 2f * scale + labelWidth + PreviewGap * scale + PreviewCrystalRadius * 2f * scale;
        context.Hud.Custom(width / scale);
        var slot = context.Hud.CustomRect(0);
        if (slot.Width <= 0f)
        {
            return;
        }

        StageHud.Capsule(drawList, slot, scale);
        var textLeft = slot.Min.X + PreviewPad * scale;
        Typography.DrawCentered(drawList, new Vector2(textLeft + labelWidth * 0.5f, slot.Center.Y), label,
            context.Theme.TextMuted, TextStyles.FootnoteEmphasized);
        var crystalCenter = new Vector2(slot.Max.X - PreviewPad * scale - PreviewCrystalRadius * scale, slot.Center.Y);
        CrystalDropRenderer.DrawCrystal(drawList, crystalCenter, PreviewCrystalRadius * scale, board.NextTier, scale);
    }

    private static string GainLabel(int points)
    {
        var index = Math.Clamp(points, 0, MaxGain);
        return GainLabels[index] ??= string.Concat("+", GameNumber.Label(index));
    }

    private static string MultiplierLabel(int multiplier)
    {
        var index = Math.Clamp(multiplier, 0, ComboMeter.MaxMultiplier);
        return MultiplierLabels[index] ??= string.Concat("x", GameNumber.Label(index));
    }

    private static ParticleSpec[] BuildMergeShards()
    {
        var specs = new ParticleSpec[CrystalDropBoard.MaxTier + 1];
        for (var tier = 0; tier <= CrystalDropBoard.MaxTier; tier++)
        {
            var color = CrystalDropRenderer.TierColor(tier);
            specs[tier] = new ParticleSpec(GamePalette.Lighten(color, 0.25f), color with { W = 0.2f },
                ShardSize * (1f + tier * 0.08f), 0.9f + tier * 0.05f, 0.55f, 3.5f, 1.4f, 10f,
                shape: ParticleShape.Shard, additive: true);
        }

        return specs;
    }
}
