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
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.WaterSort;

internal sealed class WaterSortApp : IMiniGame
{
    private const string GameId = "watersort";
    private const float PourSeconds = 0.46f;
    private const float StreamFade = 0.9f;
    private const float EntranceSpeed = 1.1f;
    private const float SelectedLift = 10f;
    private const float LiftSmoothSeconds = 0.06f;
    private const float SortedGlowSpeed = 3f;
    private const float DropletRate = 36f;
    private const float CapsulePadX = 10f;
    private const float CapsuleIconSize = 11f;
    private const float CapsuleIconGap = 5f;
    private static readonly GameSpec StageSpec = new(GameId, L.Games.WaterSort, GameGenre.Puzzle, L.WaterSort.Hook,
        Backdrop.Cavern, HudStyle.Standard, ScoreKind.Level);
    private static readonly Vector4[] WinPalette =
    {
        Core.Theme.Accent.Mint, Core.Theme.Accent.Amber, Core.Theme.Accent.Pink, Core.Theme.Accent.Blue,
    };
    private static readonly Vector4 SparkleInk = new(1f, 0.95f, 0.7f, 1f);
    private static readonly TextStyle CapsuleStyle = TextStyles.FootnoteEmphasized;

    private readonly WaterSortBoard board = new();
    private readonly WaterSortRenderer renderer = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly float[] sortedGlow = new float[WaterSortBoard.MaxTubes];
    private readonly bool[] wasSorted = new bool[WaterSortBoard.MaxTubes];
    private readonly Vector4 accent = AppAccents.For(GameId);
    private Spring liftSpring = new(0f);
    private Emitter droplets;
    private WaterSortPour pour;
    private int level;
    private bool cleared;
    private bool boardReady;
    private bool finishPending;
    private bool finished;
    private float entrance;

    public GameSpec Spec => StageSpec;

    public void Start(in GameStart start)
    {
        if (cleared)
        {
            level++;
            cleared = false;
        }

        boardReady = false;
    }

    public void Close()
    {
        level = 0;
        cleared = false;
        boardReady = false;
    }

    public void Dispose()
    {
    }

    private void EnsureBoard(in GameContext context)
    {
        if (boardReady)
        {
            return;
        }

        if (level <= 0)
        {
            level = Math.Max(1, context.Session.Best + 1);
        }

        board.Reset(level, GameRandom.FromSeed(WaterSortBoard.SeedFor(context.Session.Seed, level)));
        particles.Clear();
        fx.Clear();
        liftSpring.SnapTo(0f);
        pour.Active = false;
        entrance = 0f;
        finishPending = false;
        finished = false;
        Array.Clear(sortedGlow);
        SnapshotSorted();
        boardReady = true;
    }

    private void SnapshotSorted()
    {
        for (var tube = 0; tube < board.TubeCount; tube++)
        {
            wasSorted[tube] = board.IsTubeSorted(tube);
        }
    }

    public void DrawIdle(in GameContext context)
    {
        EnsureBoard(context);
        renderer.Draw(board, context.Safe, UiScale.Current, context.Theme, 0f, 1f, in pour, sortedGlow);
    }

    public void Draw(in GameContext context)
    {
        EnsureBoard(context);
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var playing = context.Session.State == StageFlow.Playing;
        particles.Update(context.RawDeltaSeconds);
        fx.Update(context.RawDeltaSeconds);
        entrance = GameJuice.Advance(entrance, context.RawDeltaSeconds, EntranceSpeed);
        var area = Punched(context.Safe, context.Fx.PlateScale).Translate(fx.ShakeOffset(scale));
        var lift = liftSpring.Step(board.Selected >= 0 || pour.Active ? SelectedLift * scale : 0f, LiftSmoothSeconds,
            context.RawDeltaSeconds);
        AdvancePour(context.RawDeltaSeconds, area, scale);
        UpdateSortedGlow(context.RawDeltaSeconds);
        var interactive = playing && !finished && !finishPending && !pour.Active && entrance >= 1f;
        if (interactive)
        {
            HandleClick(area, scale);
        }

        renderer.Draw(board, area, scale, context.Theme, lift, entrance, in pour, sortedGlow);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        DrawHud(context, drawList, scale, interactive);
        if (finishPending && !pour.Active)
        {
            Celebrate(context, area, scale);
            Finish(context);
        }
    }

    private static Rect Punched(Rect area, float plateScale)
    {
        var half = area.Size * 0.5f * plateScale;
        return new Rect(area.Center - half, area.Center + half);
    }

    private void HandleClick(Rect area, float scale)
    {
        var hoveredTube = -1;
        for (var tube = 0; tube < board.TubeCount; tube++)
        {
            var tubeRect = WaterSortRenderer.TubeRect(area, tube, board.TubeCount, scale);
            if (UiInteract.Hover(tubeRect.Min, tubeRect.Max))
            {
                hoveredTube = tube;
                break;
            }
        }

        if (hoveredTube < 0)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        switch (board.ClickTube(hoveredTube))
        {
            case TubeAction.Selected:
                UiFeedback.Play(UiSound.GameHitSoft);
                return;
            case TubeAction.Poured:
                BeginPour();
                return;
            default:
                return;
        }
    }

    private void BeginPour()
    {
        UiFeedback.Play(UiSound.GameMatch);
        var info = board.LastPour;
        pour.Active = true;
        pour.FromTube = info.FromTube;
        pour.ToTube = info.ToTube;
        pour.Color = info.Color;
        pour.Count = info.Count;
        pour.Progress = 0f;
        pour.Splashed = false;
        var color = WaterSortRenderer.ColorOf(info.Color);
        var spec = new ParticleSpec(color, color with { W = 0f }, 2.2f, 90f, 0.5f, 420f, 1.2f, 0f, MathF.PI * 0.9f,
            -MathF.PI * 0.5f);
        droplets = new Emitter(in spec, DropletRate);
        if (board.IsSolved())
        {
            cleared = true;
            finishPending = true;
        }
    }

    private void AdvancePour(float deltaSeconds, Rect area, float scale)
    {
        if (!pour.Active)
        {
            return;
        }

        pour.Progress += deltaSeconds / PourSeconds;
        var target = WaterSortRenderer.TubeRect(area, pour.ToTube, board.TubeCount, scale);
        var surface = WaterSortRenderer.Surface(target, board.Count(pour.ToTube) - pour.Count);
        if (pour.Progress >= WaterSortRenderer.StreamArrive)
        {
            if (!pour.Splashed)
            {
                pour.Splashed = true;
                var color = WaterSortRenderer.ColorOf(pour.Color);
                particles.Burst(surface, 9, color, 130f * scale, 2.6f, 0.45f, 320f);
                fx.Shockwave(surface, 30f * scale, GamePalette.Lighten(color, 0.25f), 0.35f, 2.2f);
            }

            if (pour.Progress < StreamFade)
            {
                droplets.Advance(deltaSeconds, surface, particles);
            }
        }

        if (pour.Progress < 1f)
        {
            return;
        }

        pour.Active = false;
        RewardNewlySortedTubes(area, scale);
    }

    private void RewardNewlySortedTubes(Rect area, float scale)
    {
        for (var tube = 0; tube < board.TubeCount; tube++)
        {
            var sorted = board.IsTubeSorted(tube);
            if (sorted && !wasSorted[tube])
            {
                var rect = WaterSortRenderer.TubeRect(area, tube, board.TubeCount, scale);
                particles.Sparkle(rect.Center, 10, SparkleInk, 120f * scale, 2.4f, 0.7f);
                UiFeedback.Play(UiSound.GamePowerUp);
            }

            wasSorted[tube] = sorted;
        }
    }

    private void UpdateSortedGlow(float deltaSeconds)
    {
        for (var tube = 0; tube < board.TubeCount; tube++)
        {
            var target = board.IsTubeSorted(tube) ? 1f : 0f;
            var glow = sortedGlow[tube];
            sortedGlow[tube] = glow < target
                ? MathF.Min(target, glow + deltaSeconds * SortedGlowSpeed)
                : MathF.Max(target, glow - deltaSeconds * SortedGlowSpeed);
        }
    }

    private void DrawHud(in GameContext context, ImDrawListPtr drawList, float scale, bool interactive)
    {
        var hud = context.Hud;
        hud.Level(level);
        var label = GameNumber.Label(board.Moves);
        var width = CapsulePadX * 2f + CapsuleIconSize + CapsuleIconGap + Typography.Measure(label, CapsuleStyle).X / scale;
        hud.Custom(width);
        var rect = hud.CustomRect;
        if (rect.Width > 0f)
        {
            DrawUndoCapsule(drawList, rect, label, interactive, context.Theme, scale);
        }

        hud.Best(context.Session.Best);
        context.Session.Report(level - 1);
    }

    private void DrawUndoCapsule(ImDrawListPtr drawList, Rect rect, string label, bool interactive, PhoneTheme theme,
        float scale)
    {
        var canUndo = interactive && board.CanUndo;
        var hovered = canUndo && UiInteract.Hover(rect.Min, rect.Max);
        StageHud.Capsule(drawList, rect, scale, hovered ? 1f : 0.9f);
        var iconSize = CapsuleIconSize * scale;
        var left = rect.Min.X + CapsulePadX * scale;
        var centerY = rect.Center.Y;
        ProgressRing.CenterIcon(drawList, new Vector2(left + iconSize * 0.5f, centerY), FontAwesomeIcon.Undo,
            canUndo ? accent : theme.TextMuted with { W = 0.45f }, iconSize);
        Typography.Draw(drawList,
            new Vector2(left + iconSize + CapsuleIconGap * scale, centerY - Typography.LineHeight(CapsuleStyle) * 0.5f),
            label, theme.TextStrong, CapsuleStyle);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!canUndo || !UiInteract.HoverClick(rect.Min, rect.Max) || !board.Undo())
        {
            return;
        }

        UiFeedback.Play(UiSound.GameHitSoft);
        SnapshotSorted();
    }

    private void Celebrate(in GameContext context, Rect area, float scale)
    {
        GameSfx.LevelClear();
        context.Fx.Sweep();
        context.Fx.Punch(0.05f);
        fx.AddTrauma(0.25f);
        fx.Shockwave(area.Center, area.Width * 0.45f, GamePalette.Lighten(accent, 0.3f), 0.6f, 3f);
        particles.Confetti(new Vector2(area.Center.X, area.Min.Y), 64, WinPalette, 260f * scale, 4f, 1.3f);
        particles.Sparkle(area.Center, 18, SparkleInk, 200f * scale, 2.8f, 1f);
    }

    private void Finish(in GameContext context)
    {
        finishPending = false;
        finished = true;
        context.Session.Finish(new GameOutcome(level, ScoreKind.Level, GameId)
            .WithStat(L.Games.Moves, GameNumber.Label(board.Moves)));
    }
}
