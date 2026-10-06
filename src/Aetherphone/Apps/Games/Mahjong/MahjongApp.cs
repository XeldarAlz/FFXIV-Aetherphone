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

namespace Aetherphone.Apps.Games.Mahjong;

internal sealed class MahjongApp : IMiniGame
{
    private const string GameId = "mahjong";
    private const int HintPenaltySeconds = 15;
    private const float HintSeconds = 2.6f;
    private const float FlightSeconds = 0.36f;
    private const float FlightRise = 0.55f;
    private const float FlightRibbonWidth = 5f;
    private const float WinDelaySeconds = 1.3f;
    private const float BannerSeconds = 1.7f;
    private const float DealSpeed = 0.75f;
    private const float InteractiveEntrance = 0.85f;
    private const float ToolbarHeight = 76f;
    private const float ToolbarSpacing = 86f;
    private const float ToolRadius = 21f;
    private const float ComboWindowSeconds = 3.2f;
    private const int MaxFlights = 8;
    private const int PunchMultiplier = 3;
    private static readonly LocString[] Modes = { L.Games.Easy, L.Games.Medium, L.Games.Hard };
    private static readonly string[] ModeStatIds = { "mahjong.easy", "mahjong.medium", "mahjong.hard" };
    private static readonly LocString[] LayoutNames =
    {
        L.Mahjong.Moogle, L.Mahjong.Bridge, L.Mahjong.Tower, L.Mahjong.Crossroads, L.Mahjong.Fortress,
        L.Mahjong.Dragon,
    };
    private static readonly GameSpec StageSpec = new(GameId, L.Mahjong.Title, GameGenre.Puzzle, L.Mahjong.Hook,
        Backdrop.Felt, HudStyle.Standard, ScoreKind.Time, Modes, ModeStatIds, keyboard: true);
    private static readonly Vector4 Danger = new(0.95f, 0.34f, 0.32f, 1f);
    private static readonly Vector4 Gold = new(1f, 0.84f, 0.36f, 1f);
    private static readonly Vector4 Spark = new(1f, 0.96f, 0.78f, 1f);
    private static readonly Vector4[] WinPalette =
    {
        Core.Theme.Accent.Mint, Core.Theme.Accent.Amber, Core.Theme.Accent.Rose, Core.Theme.Accent.Blue,
    };
    private static readonly ParticleSpec Shards = new(new Vector4(1f, 1f, 1f, 1f), new Vector4(1f, 1f, 1f, 0f), 4.2f,
        260f, 0.7f, 420f, 1.4f, 9f, shape: ParticleShape.Shard);

    private struct Flight
    {
        public bool Active;
        public bool Leads;
        public int Face;
        public Vector2 From;
        public Vector2 To;
        public float Progress;
    }

    private readonly MahjongBoard board = new();
    private readonly MahjongRenderer renderer = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly Flight[] flights = new Flight[MaxFlights];
    private readonly Ribbon[] trails = new Ribbon[MaxFlights];
    private ComboMeter combo = new(ComboWindowSeconds);
    private LabelSlot penaltyLabel;
    private LabelSlot comboLabel;
    private MahjongGeometry geometry;
    private int layoutIndex;
    private int previewMode = -1;
    private ulong previewSeed;
    private int selected = -1;
    private int hintFirst = -1;
    private int hintSecond = -1;
    private float hintTimer;
    private int hints;
    private float elapsed;
    private float penalty;
    private float entrance;
    private float banner = 1f;
    private string bannerText = string.Empty;
    private Vector4 bannerTint;
    private int checkedVersion = -1;
    private bool stuck;
    private bool winPending;
    private float winTimer;
    private bool finished;

    public MahjongApp()
    {
        for (var index = 0; index < MaxFlights; index++)
        {
            trails[index] = new Ribbon();
        }
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        Deal(start.Mode, start.Seed);
        ResetRun();
        ShowBanner(Loc.T(LayoutNames[layoutIndex]), Accent);
    }

    public void Close()
    {
        previewMode = -1;
        particles.Clear();
        fx.Clear();
    }

    public void Dispose()
    {
    }

    public void OnQuit(GameSession session)
    {
        if (!winPending || finished)
        {
            return;
        }

        FinishWin(session);
    }

    public void DrawIdle(in GameContext context)
    {
        var session = context.Session;
        if (previewMode != session.Mode || previewSeed != session.Seed)
        {
            Deal(session.Mode, session.Seed);
            ResetRun();
            entrance = 1f;
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        Layout(context.Safe, scale, out var boardArea, out _);
        geometry = MahjongGeometry.Fit(boardArea, board.Layout, scale);
        BoardPlate.Draw(drawList, BoardPlate.Around(geometry.Content, scale), BoardPlate.Radius * scale, scale, Accent,
            context.Backdrop.Ink);
        renderer.DrawBoard(drawList, board, geometry, 1f, -1, -1, -1, 0f, Accent, scale);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var session = context.Session;
        var raw = context.RawDeltaSeconds;
        particles.Update(raw);
        fx.Update(raw);
        renderer.Update(raw);
        combo.Update(context.DeltaSeconds);
        entrance = GameJuice.Advance(entrance, raw, DealSpeed);
        banner = GameBanner.Advance(banner, raw, BannerSeconds);
        hintTimer = MathF.Max(0f, hintTimer - context.DeltaSeconds);
        var playing = session.State == StageFlow.Playing && !finished;
        if (playing && !winPending)
        {
            elapsed += context.DeltaSeconds;
        }

        var area = StageLayout.Punched(context.Safe, context.Fx.PlateScale).Translate(fx.ShakeOffset(scale));
        Layout(area, scale, out var boardArea, out var toolbar);
        geometry = MahjongGeometry.Fit(boardArea, board.Layout, scale);
        var interactive = playing && !winPending && entrance >= InteractiveEntrance;
        if (interactive)
        {
            HandleInput(context);
        }

        AdvanceFlights(winPending || board.Cleared ? context.DeltaSeconds : raw, context, scale);
        CheckState(context);
        BoardPlate.Draw(drawList, BoardPlate.Around(geometry.Content, scale), BoardPlate.Radius * scale, scale, Accent,
            context.Backdrop.Ink);
        renderer.DrawBoard(drawList, board, geometry, entrance, selected, hintFirst, hintSecond,
            MathF.Min(1f, hintTimer), Accent, scale);
        DrawFlights(drawList, scale);
        DrawToolbar(drawList, toolbar, context, interactive, scale);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        GameBanner.Draw(drawList, geometry.Content.Center, bannerText, bannerTint, context.Theme, banner);
        if (winPending && session.State == StageFlow.Playing)
        {
            AdvanceWin(raw, context);
        }

        DrawHud(context, drawList, scale);
    }

    private void Deal(int mode, ulong seed)
    {
        var random = GameRandom.FromSeed(seed);
        layoutIndex = MahjongLayouts.IndexFor(mode, random.Next(MahjongLayouts.PerTier));
        board.Deal(MahjongLayouts.All[layoutIndex], random);
        previewMode = mode;
        previewSeed = seed;
    }

    private void ResetRun()
    {
        particles.Clear();
        fx.Clear();
        renderer.Clear();
        combo.Reset();
        for (var index = 0; index < MaxFlights; index++)
        {
            flights[index].Active = false;
            trails[index].Clear();
        }

        selected = -1;
        hintFirst = -1;
        hintSecond = -1;
        hintTimer = 0f;
        hints = 0;
        elapsed = 0f;
        penalty = 0f;
        entrance = 0f;
        banner = 1f;
        checkedVersion = -1;
        stuck = false;
        winPending = false;
        winTimer = 0f;
        finished = false;
    }

    private static void Layout(Rect area, float scale, out Rect boardArea, out Rect toolbar)
    {
        var band = ToolbarHeight * scale;
        var inset = BoardPlate.Padding * scale;
        boardArea = new Rect(area.Min + new Vector2(inset, inset),
            new Vector2(area.Max.X - inset, MathF.Max(area.Min.Y + inset, area.Max.Y - band - inset)));
        toolbar = new Rect(new Vector2(area.Min.X, area.Max.Y - band), area.Max);
    }

    private void HandleInput(in GameContext context)
    {
        if (GameInput.Pressed(ImGuiKey.H))
        {
            Hint(context);
        }

        if (GameInput.Pressed(ImGuiKey.Z) || GameInput.Pressed(ImGuiKey.U))
        {
            Undo();
        }

        if (stuck && GameInput.Pressed(ImGuiKey.S))
        {
            Shuffle(context);
        }

        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left) || context.ChromeHit(ImGui.GetMousePos()))
        {
            return;
        }

        var tile = MahjongRenderer.HitTest(board, geometry);
        if (tile < 0)
        {
            selected = -1;
            return;
        }

        if (!board.IsFree(tile))
        {
            renderer.Wiggle(tile);
            UiFeedback.Play(UiSound.GameWrong);
            fx.AddTrauma(0.04f);
            return;
        }

        if (selected < 0 || selected == tile)
        {
            selected = selected == tile ? -1 : tile;
            UiFeedback.Play(UiSound.GameCardPlace);
            return;
        }

        var first = selected;
        if (board.TryMatch(first, tile))
        {
            selected = -1;
            OnMatch(first, tile, context);
            return;
        }

        renderer.Wiggle(first);
        selected = tile;
        UiFeedback.Play(UiSound.GameTick);
    }

    private void OnMatch(int first, int second, in GameContext context)
    {
        if (first == hintFirst || first == hintSecond || second == hintFirst || second == hintSecond)
        {
            hintTimer = 0f;
        }

        var firstFace = geometry.Face(board.Layout, first);
        var secondFace = geometry.Face(board.Layout, second);
        var meet = (firstFace.Center + secondFace.Center) * 0.5f - new Vector2(0f, geometry.TileHeight * FlightRise);
        var tierBefore = combo.Multiplier;
        combo.Hit();
        if (combo.Multiplier > tierBefore)
        {
            GameSfx.ComboTierUp();
        }

        UiFeedback.Play(UiSound.GameCardFlip);
        var launched = Launch(board.Face(first), firstFace.Center, meet, true);
        launched &= Launch(board.Face(second), secondFace.Center, meet, false);
        if (!launched)
        {
            Burst(board.Face(first), meet, context, UiScale.Current);
        }
    }

    private bool Launch(int face, Vector2 from, Vector2 to, bool leads)
    {
        for (var index = 0; index < MaxFlights; index++)
        {
            ref var flight = ref flights[index];
            if (flight.Active)
            {
                continue;
            }

            flight.Active = true;
            flight.Leads = leads;
            flight.Face = face;
            flight.From = from;
            flight.To = to;
            flight.Progress = 0f;
            trails[index].Clear();
            return true;
        }

        return false;
    }

    private void AdvanceFlights(float deltaSeconds, in GameContext context, float scale)
    {
        for (var index = 0; index < MaxFlights; index++)
        {
            ref var flight = ref flights[index];
            if (!flight.Active)
            {
                continue;
            }

            flight.Progress += deltaSeconds / FlightSeconds;
            if (flight.Progress < 1f)
            {
                continue;
            }

            flight.Active = false;
            trails[index].Clear();
            if (flight.Leads)
            {
                Burst(flight.Face, flight.To, context, scale);
            }
        }
    }

    private void Burst(int face, Vector2 at, in GameContext context, float scale)
    {
        var tint = MahjongArt.Tint(face);
        UiFeedback.Play(UiSound.GameMatch);
        particles.Emit(new ParticleSpec(GamePalette.Lighten(tint, 0.2f), tint with { W = 0f }, Shards.Size * scale,
            Shards.Speed * scale, Shards.Life, Shards.Gravity * scale, Shards.Drag, Shards.Spin,
            shape: ParticleShape.Shard), at, 16);
        particles.Sparkle(at, 8, Spark, 150f * scale, 2.6f, 0.7f);
        fx.Shockwave(at, geometry.TileWidth * 1.3f, GamePalette.Lighten(tint, 0.35f), 0.45f, 2.6f);
        fx.AddTrauma(0.07f);
        if (combo.Count >= 2)
        {
            fx.AddText(comboLabel.Get(L.Stage.Times, combo.Count), at - new Vector2(0f, 14f * scale), Gold, 1.15f);
        }

        if (combo.Multiplier >= PunchMultiplier)
        {
            context.Fx.Punch(0.03f);
        }

        if (!board.Cleared)
        {
            return;
        }

        particles.Confetti(at, 110, WinPalette, 320f * scale, 4.4f, 1.6f);
        particles.Sparkle(geometry.Content.Center, 26, Gold, 260f * scale, 3.2f, 1.1f);
        context.Fx.Flash(Gold, 0.28f);
        context.Fx.Sweep();
        context.Fx.Punch(0.06f);
        fx.AddTrauma(0.3f);
        GameSfx.LevelClear();
        ShowBanner(Loc.T(L.Mahjong.TableClear), Gold);
    }

    private void DrawFlights(ImDrawListPtr drawList, float scale)
    {
        var tileSize = new Vector2(geometry.TileWidth, geometry.TileHeight) * (1f - MahjongGeometry.GapFraction);
        for (var index = 0; index < MaxFlights; index++)
        {
            ref readonly var flight = ref flights[index];
            if (!flight.Active)
            {
                continue;
            }

            var eased = Easing.EaseInCubic(flight.Progress);
            var center = Vector2.Lerp(flight.From, flight.To, eased);
            center.Y -= MathF.Sin(flight.Progress * MathF.PI) * geometry.TileHeight * 0.25f;
            var trail = trails[index];
            trail.Push(center);
            trail.Draw(drawList, MahjongArt.Tint(flight.Face), FlightRibbonWidth * scale, true);
            var grow = 1f + 0.18f * MathF.Sin(flight.Progress * MathF.PI);
            var half = tileSize * 0.5f * grow;
            var face = new Rect(center - half, center + half);
            MahjongRenderer.DrawFace(drawList, face, flight.Face, Accent, geometry.Radius, 1f - 0.3f * eased, scale,
                geometry);
        }
    }

    private void DrawToolbar(ImDrawListPtr drawList, Rect toolbar, in GameContext context, bool interactive,
        float scale)
    {
        var center = new Vector2(toolbar.Center.X, toolbar.Min.Y + (ToolRadius + 8f) * scale);
        var spacing = ToolbarSpacing * scale;
        var radius = ToolRadius * scale;
        var theme = context.Theme;
        var canHint = interactive && !stuck && board.Remaining > 0;
        if (MahjongRenderer.ToolButton(drawList, center - new Vector2(spacing, 0f), radius, FontAwesomeIcon.Lightbulb,
                Loc.T(L.Games.Hint), penaltyLabel.Get(L.Mahjong.Penalty, HintPenaltySeconds), Accent, theme, canHint,
                false, scale))
        {
            Hint(context);
        }

        if (MahjongRenderer.ToolButton(drawList, center, radius, FontAwesomeIcon.Undo, Loc.T(L.Games.Undo),
                string.Empty, Accent, theme, interactive && board.CanUndo, false, scale))
        {
            Undo();
        }

        if (MahjongRenderer.ToolButton(drawList, center + new Vector2(spacing, 0f), radius, FontAwesomeIcon.Random,
                Loc.T(L.Mahjong.Shuffle), string.Empty, Accent, theme, interactive && stuck, stuck, scale))
        {
            Shuffle(context);
        }
    }

    private void Hint(in GameContext context)
    {
        if (stuck || !board.FindHint(out var first, out var second))
        {
            return;
        }

        hintFirst = first;
        hintSecond = second;
        hintTimer = HintSeconds;
        hints++;
        penalty += HintPenaltySeconds;
        selected = -1;
        UiFeedback.Play(UiSound.GameTick);
        var timer = context.Hud.SlotRect(HudSlot.Timer);
        var anchor = timer.Width > 0f
            ? new Vector2(timer.Center.X, timer.Max.Y + 10f * UiScale.Current)
            : geometry.Content.Center;
        fx.AddText(penaltyLabel.Get(L.Mahjong.Penalty, HintPenaltySeconds), anchor, Danger, 1.05f, 18f);
    }

    private void Undo()
    {
        if (!board.Undo(out var first, out var second))
        {
            return;
        }

        renderer.Appear(first);
        renderer.Appear(second);
        selected = -1;
        hintTimer = 0f;
        combo.Reset();
        UiFeedback.Play(UiSound.GameCardFlip);
    }

    private void Shuffle(in GameContext context)
    {
        if (!board.Shuffle())
        {
            UiFeedback.Play(UiSound.GameWrong);
            ShowBanner(Loc.T(L.Mahjong.UndoToContinue), Danger);
            return;
        }

        renderer.Flip();
        selected = -1;
        hintTimer = 0f;
        UiFeedback.Play(UiSound.GameShuffle);
        context.Fx.Sweep();
        ShowBanner(Loc.T(L.Mahjong.Shuffled), Accent);
    }

    private void CheckState(in GameContext context)
    {
        if (board.Version == checkedVersion || finished)
        {
            return;
        }

        checkedVersion = board.Version;
        if (board.Cleared)
        {
            if (!winPending)
            {
                winPending = true;
                winTimer = 0f;
                selected = -1;
                context.Fx.SlowMo(0.5f, 0.45f);
            }

            stuck = false;
            return;
        }

        var wasStuck = stuck;
        stuck = !board.HasMoves;
        if (!stuck || wasStuck)
        {
            return;
        }

        UiFeedback.Play(UiSound.GameWrong);
        context.Fx.Vignette(Danger, 0.3f, 0.8f);
        ShowBanner(Loc.T(L.Mahjong.NoMoves), Danger);
    }

    private void AdvanceWin(float deltaSeconds, in GameContext context)
    {
        winTimer += deltaSeconds;
        if (winTimer < WinDelaySeconds || finished || AnyFlightActive())
        {
            return;
        }

        FinishWin(context.Session);
    }

    private void FinishWin(GameSession session)
    {
        finished = true;
        var seconds = Math.Max(1, (int)(elapsed + penalty));
        session.Finish(new GameOutcome(seconds, ScoreKind.Time, session.StatId)
            .WithStat(L.Mahjong.Layout, Loc.T(LayoutNames[layoutIndex]))
            .WithStat(L.Mahjong.Hints, GameNumber.Label(hints))
            .WithStat(L.Mahjong.Shuffles, GameNumber.Label(board.Shuffles))
            .WithStat(L.Mahjong.Undos, GameNumber.Label(board.Undos)));
    }

    private bool AnyFlightActive()
    {
        for (var index = 0; index < MaxFlights; index++)
        {
            if (flights[index].Active)
            {
                return true;
            }
        }

        return false;
    }

    private void ShowBanner(string text, Vector4 tint)
    {
        bannerText = text;
        bannerTint = tint;
        banner = 0f;
    }

    private void DrawHud(in GameContext context, ImDrawListPtr drawList, float scale)
    {
        var hud = context.Hud;
        var shownSeconds = elapsed + penalty;
        hud.Clock(shownSeconds);
        hud.Combo(combo);
        hud.Best(context.Session.Best);
        var tilesLabel = GameNumber.Label(board.Remaining);
        var pairsLabel = GameNumber.Label(board.FreePairs);
        hud.Custom(StatCapsule.Width(tilesLabel, scale));
        hud.Custom(StatCapsule.Width(pairsLabel, scale));
        if (hud.CustomPlaced(0))
        {
            StatCapsule.Draw(drawList, hud.CustomRect(0), FontAwesomeIcon.LayerGroup, tilesLabel, Accent, scale);
        }

        if (hud.CustomPlaced(1))
        {
            StatCapsule.Draw(drawList, hud.CustomRect(1), FontAwesomeIcon.Clone, pairsLabel,
                stuck ? Danger : Gold, scale);
        }

        if (stuck && context.Session.State == StageFlow.Playing)
        {
            context.Fx.Vignette(Danger, 0.08f + 0.08f * Pulse.Wave(Pulse.Fast), 0.3f);
        }

        context.Session.Report((int)shownSeconds);
    }
}
