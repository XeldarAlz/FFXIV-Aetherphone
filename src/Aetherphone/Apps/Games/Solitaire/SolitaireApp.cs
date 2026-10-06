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

namespace Aetherphone.Apps.Games.Solitaire;

internal sealed class SolitaireApp : IMiniGame
{
    public const string VegasStatId = "solitaire.vegas";
    private const string GameId = "solitaire";
    private const int VegasMode = 1;
    private const int MaxFlights = 8;
    private const int MaxGrab = 13;
    private const float DragThreshold = 5f;
    private const float DealSpeed = 0.9f;
    private const float FlightSeconds = 0.32f;
    private const float FlightArc = 28f;
    private const float FlightPop = 0.22f;
    private const float FlightRibbonWidth = 7f;
    private const float AutoStepSeconds = 0.11f;
    private const float CapsulePadX = 10f;
    private const float CapsuleIconSize = 11f;
    private const float CapsuleIconGap = 5f;
    private static readonly LocString[] Modes = { L.Solitaire.Classic, L.Solitaire.Vegas };
    private static readonly string[] ModeStatIds = { GameId, VegasStatId };
    private static readonly GameSpec StageSpec = new(GameId, L.Games.Solitaire, GameGenre.Tabletop, L.Solitaire.Hook,
        Backdrop.Felt, HudStyle.Standard, ScoreKind.Time, Modes, ModeStatIds);
    private static readonly Vector4[] WinPalette =
    {
        Core.Theme.Accent.Mint, Core.Theme.Accent.Amber, Core.Theme.Accent.Rose, Core.Theme.Accent.Blue,
    };
    private static readonly Vector4 SparkleInk = new(1f, 0.95f, 0.7f, 1f);
    private static readonly TextStyle CapsuleStyle = TextStyles.FootnoteEmphasized;

    private struct CardFlight
    {
        public bool Active;
        public int Card;
        public Vector2 From;
        public Vector2 To;
        public float Progress;
    }

    private readonly SolitaireBoard board = new();
    private readonly SolitaireRenderer renderer = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly CardFlight[] flights = new CardFlight[MaxFlights];
    private readonly Ribbon[] trails = new Ribbon[MaxFlights];
    private readonly int[] inFlight = new int[SolitaireBoard.SuitCount];
    private readonly int[] grabbed = new int[MaxGrab];
    private readonly Vector4 accent = AppAccents.For(GameId);
    private SolitaireHit grabSource = SolitaireHit.None;
    private int grabCount;
    private Vector2 grabOffset;
    private Vector2 pressPosition;
    private bool dragMoved;
    private float elapsed;
    private float entrance;
    private float autoTimer;
    private bool autoPlaying;
    private bool vegas;
    private bool finishPending;
    private bool finished;
    private bool celebrated;
    private bool previewDealt;
    private bool bestLoaded;
    private int bestVegas;
    private int checkedMoves = -1;

    public SolitaireApp()
    {
        for (var index = 0; index < MaxFlights; index++)
        {
            trails[index] = new Ribbon();
        }
    }

    public GameSpec Spec => StageSpec;

    public void Start(in GameStart start)
    {
        vegas = start.Mode == VegasMode;
        board.Deal(start.Random, vegas);
        previewDealt = true;
        ResetRun();
    }

    public void Close()
    {
        previewDealt = false;
    }

    public void Dispose()
    {
    }

    private void ResetRun()
    {
        particles.Clear();
        fx.Clear();
        for (var index = 0; index < MaxFlights; index++)
        {
            flights[index].Active = false;
            trails[index].Clear();
        }

        Array.Clear(inFlight);
        grabSource = SolitaireHit.None;
        grabCount = 0;
        dragMoved = false;
        elapsed = 0f;
        entrance = 0f;
        autoTimer = 0f;
        autoPlaying = false;
        finishPending = false;
        finished = false;
        celebrated = false;
        bestLoaded = false;
        checkedMoves = -1;
    }

    public void DrawIdle(in GameContext context)
    {
        if (!previewDealt)
        {
            board.Deal(GameRandom.FromSeed(context.Session.Seed), false);
            previewDealt = true;
            ResetRun();
        }

        var scale = UiScale.Current;
        var layout = SolitaireLayout.Compute(context.Safe, board, scale);
        renderer.Draw(board, layout, context.Theme, accent, scale, SolitaireHit.None, SolitaireHit.None, 1f, inFlight);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var session = context.Session;
        var playing = session.State == StageFlow.Playing;
        if (!bestLoaded)
        {
            bestVegas = session.Stats.Get(VegasStatId).BestScore;
            bestLoaded = true;
        }

        particles.Update(context.RawDeltaSeconds);
        fx.Update(context.RawDeltaSeconds);
        entrance = GameJuice.Advance(entrance, context.RawDeltaSeconds, DealSpeed);
        var running = !finished && !finishPending;
        if (running)
        {
            elapsed += context.DeltaSeconds;
        }

        var area = context.Safe.Translate(fx.ShakeOffset(scale));
        var layout = SolitaireLayout.Compute(area, board, scale);
        var interactive = playing && running && !autoPlaying && entrance >= 1f;
        var dropTarget = interactive ? HandleInput(layout, area, scale) : SolitaireHit.None;
        if (autoPlaying && playing && running)
        {
            AdvanceAuto(context.DeltaSeconds, layout);
        }

        AdvanceFlights(context.RawDeltaSeconds, layout, scale);
        CheckOutcome(context, layout, scale);
        renderer.Draw(board, layout, context.Theme, accent, scale, grabSource, dropTarget, entrance, inFlight);
        DrawFlights(drawList, layout, scale);
        if (grabCount > 0)
        {
            var topLeft = ImGui.GetMousePos() - grabOffset;
            renderer.DrawFloating(layout, new ReadOnlySpan<int>(grabbed, 0, grabCount), topLeft, scale);
        }

        fx.DrawFlash(drawList, context.Full, 0f);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        DrawHud(context, drawList, scale, playing && running);
        if (finishPending && !AnyFlightActive())
        {
            Finish(context);
        }
    }

    private void CheckOutcome(in GameContext context, in SolitaireLayout layout, float scale)
    {
        if (finished || finishPending)
        {
            return;
        }

        if (board.IsWon)
        {
            finishPending = true;
            Celebrate(context, layout, scale);
            return;
        }

        if (!vegas || board.Moves == checkedMoves)
        {
            return;
        }

        checkedMoves = board.Moves;
        if (!board.HasAnyMove())
        {
            finishPending = true;
            UiFeedback.Play(UiSound.GameWrong);
        }
    }

    private void Celebrate(in GameContext context, in SolitaireLayout layout, float scale)
    {
        if (celebrated)
        {
            return;
        }

        celebrated = true;
        UiFeedback.Play(UiSound.GameClear);
        context.Fx.Sweep();
        context.Fx.Flash(accent, 0.3f);
        fx.AddTrauma(0.3f);
        var top = new Vector2(layout.OriginX + layout.ColumnPitch * 3.5f, layout.TopRowY);
        particles.Confetti(top, 90, WinPalette, 280f * scale, 4.4f, 1.5f);
        particles.Sparkle(top + new Vector2(0f, 60f * scale), 18, SparkleInk, 210f * scale, 2.8f, 1f);
    }

    private void Finish(in GameContext context)
    {
        finishPending = false;
        finished = true;
        var seconds = Math.Max(1, (int)elapsed);
        if (vegas)
        {
            context.Session.Finish(new GameOutcome(board.Score, ScoreKind.Score, VegasStatId, board.IsWon)
                .WithStat(L.Games.Time, TimeText.MinutesSeconds(seconds))
                .WithStat(L.Games.Moves, GameNumber.Label(board.Moves)));
            return;
        }

        context.Session.Finish(new GameOutcome(seconds, ScoreKind.Time, GameId)
            .WithStat(L.Games.Moves, GameNumber.Label(board.Moves)));
    }

    private void DrawHud(in GameContext context, ImDrawListPtr drawList, float scale, bool playing)
    {
        var hud = context.Hud;
        hud.Timer(MathF.Floor(elapsed), 0f, false);
        if (vegas)
        {
            hud.Score(board.Score);
            hud.Best(bestVegas);
        }

        var autoReady = playing && !autoPlaying && entrance >= 1f && board.IsAutoCompletable;
        var label = autoReady ? Loc.T(L.Solitaire.Auto) : GameNumber.Label(board.Moves);
        var width = CapsulePadX * 2f + CapsuleIconSize + CapsuleIconGap + Typography.Measure(label, CapsuleStyle).X / scale;
        hud.Custom(width);
        var rect = hud.CustomRect(0);
        if (rect.Width > 0f)
        {
            DrawMovesCapsule(drawList, rect, label, autoReady, context.Theme, scale);
        }

        context.Session.Report(vegas ? board.Score : (int)elapsed);
    }

    private void DrawMovesCapsule(ImDrawListPtr drawList, Rect rect, string label, bool autoReady, PhoneTheme theme,
        float scale)
    {
        var iconSize = CapsuleIconSize * scale;
        var left = rect.Min.X + CapsulePadX * scale;
        var centerY = rect.Center.Y;
        var textOrigin = new Vector2(left + iconSize + CapsuleIconGap * scale,
            centerY - Typography.LineHeight(CapsuleStyle) * 0.5f);
        if (!autoReady)
        {
            StageHud.Capsule(drawList, rect, scale);
            ProgressRing.CenterIcon(drawList, new Vector2(left + iconSize * 0.5f, centerY), FontAwesomeIcon.ShoePrints,
                accent, iconSize);
            Typography.Draw(drawList, textOrigin, label, theme.TextStrong, CapsuleStyle);
            return;
        }

        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var pulse = 0.4f + 0.6f * Pulse.Wave(Pulse.Medium);
        ProgressRing.Glow(rect.Center, rect.Height * 0.9f, accent, 0.5f * pulse);
        Squircle.Fill(drawList, rect.Min, rect.Max, rect.Height * 0.5f,
            ImGui.GetColorU32(GamePalette.Lighten(accent, hovered ? 0.2f : 0.08f)));
        Material.Sheen(drawList, rect.Min, rect.Max, rect.Height * 0.5f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.3f)), 1f * scale, 1f * scale);
        var ink = GamePalette.InkOn(accent);
        ProgressRing.CenterIcon(drawList, new Vector2(left + iconSize * 0.5f, centerY), FontAwesomeIcon.Forward, ink,
            iconSize);
        Typography.Draw(drawList, textOrigin, label, ink, CapsuleStyle);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!UiInteract.HoverClick(rect.Min, rect.Max))
        {
            return;
        }

        autoPlaying = true;
        autoTimer = 0f;
        grabSource = SolitaireHit.None;
        grabCount = 0;
        UiFeedback.Play(UiSound.GamePowerUp);
    }

    private void AdvanceAuto(float deltaSeconds, in SolitaireLayout layout)
    {
        autoTimer -= deltaSeconds;
        if (autoTimer > 0f)
        {
            return;
        }

        autoTimer = AutoStepSeconds;
        var move = board.PlanAutoStep(out var pile);
        switch (move)
        {
            case SolitaireAutoMove.WasteToFoundation:
            {
                var card = board.WasteTop();
                var origin = layout.WasteRect.Min;
                if (board.SendWasteToFoundation())
                {
                    LaunchFlight(card, origin, layout.FoundationRect(SolitaireBoard.Suit(card)).Min);
                }

                return;
            }
            case SolitaireAutoMove.TableauToFoundation:
            {
                var index = board.TableauCount(pile) - 1;
                var card = board.TableauCardAt(pile, index);
                var origin = layout.TableauCardRect(pile, index).Min;
                if (board.SendTableauToFoundation(pile))
                {
                    LaunchFlight(card, origin, layout.FoundationRect(SolitaireBoard.Suit(card)).Min);
                }

                return;
            }
            case SolitaireAutoMove.Draw:
                if (board.DrawStock())
                {
                    UiFeedback.Play(UiSound.GameCardFlip);
                }

                return;
            default:
                autoPlaying = false;
                return;
        }
    }

    private void LaunchFlight(int card, Vector2 fromMin, Vector2 toMin)
    {
        for (var index = 0; index < MaxFlights; index++)
        {
            ref var flight = ref flights[index];
            if (flight.Active)
            {
                continue;
            }

            flight.Active = true;
            flight.Card = card;
            flight.From = fromMin;
            flight.To = toMin;
            flight.Progress = 0f;
            trails[index].Clear();
            inFlight[SolitaireBoard.Suit(card)]++;
            UiFeedback.Play(UiSound.GameCardFlip);
            return;
        }

        UiFeedback.Play(UiSound.GameCardPlace);
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

    private void AdvanceFlights(float deltaSeconds, in SolitaireLayout layout, float scale)
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
            inFlight[SolitaireBoard.Suit(flight.Card)]--;
            LandCard(flight.To + layout.CardSize * 0.5f, scale);
        }
    }

    private void LandCard(Vector2 center, float scale)
    {
        UiFeedback.Play(UiSound.GameCardPlace);
        particles.Burst(center, 12, accent, 150f * scale, 3f, 0.5f, 220f);
        particles.Sparkle(center, 5, SparkleInk, 110f * scale, 2f, 0.6f);
        fx.Shockwave(center, 40f * scale, GamePalette.Lighten(accent, 0.3f), 0.4f, 2.4f);
        fx.AddTrauma(0.05f);
    }

    private void DrawFlights(ImDrawListPtr drawList, in SolitaireLayout layout, float scale)
    {
        var halfCard = layout.CardSize * 0.5f;
        for (var index = 0; index < MaxFlights; index++)
        {
            ref readonly var flight = ref flights[index];
            if (!flight.Active)
            {
                continue;
            }

            var eased = Easing.EaseOutCubic(flight.Progress);
            var center = Vector2.Lerp(flight.From, flight.To, eased) + halfCard;
            center.Y -= MathF.Sin(flight.Progress * MathF.PI) * FlightArc * scale;
            var trail = trails[index];
            trail.Push(center);
            trail.Draw(drawList, accent, FlightRibbonWidth * scale, true);
            var sizeFactor = 1f + (1f - Easing.EaseOutBack(flight.Progress)) * FlightPop;
            SolitaireRenderer.DrawFlightCard(drawList, layout, flight.Card, center, sizeFactor, scale);
        }
    }

    private SolitaireHit HandleInput(in SolitaireLayout layout, Rect area, float scale)
    {
        var mouse = ImGui.GetMousePos();
        if (grabCount == 0 && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && UiInteract.Hover(area.Min, area.Max))
        {
            var hit = layout.Hit(mouse);
            if (hit.Kind == SolitairePileKind.Stock)
            {
                var recycling = board.StockCount == 0;
                if (board.DrawStock())
                {
                    UiFeedback.Play(recycling ? UiSound.GameShuffle : UiSound.GameCardFlip);
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }
            }
            else
            {
                BeginGrab(hit, mouse, layout);
            }
        }

        if (grabCount == 0)
        {
            return SolitaireHit.None;
        }

        if (Vector2.Distance(mouse, pressPosition) > DragThreshold * scale)
        {
            dragMoved = true;
        }

        var dropTarget = ComputeDropTarget(layout, mouse);
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            ReleaseGrab(layout, mouse, dropTarget, scale);
            return SolitaireHit.None;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return dropTarget;
    }

    private void BeginGrab(in SolitaireHit hit, Vector2 mouse, in SolitaireLayout layout)
    {
        grabCount = 0;
        if (hit.Kind == SolitairePileKind.Waste)
        {
            var card = board.WasteTop();
            if (card < 0)
            {
                return;
            }

            grabbed[0] = card;
            grabCount = 1;
            grabOffset = mouse - layout.WasteRect.Min;
        }
        else if (hit.Kind == SolitairePileKind.Foundation)
        {
            var card = board.FoundationTop(hit.Pile);
            if (card < 0)
            {
                return;
            }

            grabbed[0] = card;
            grabCount = 1;
            grabOffset = mouse - layout.FoundationRect(hit.Pile).Min;
        }
        else if (hit.Kind == SolitairePileKind.Tableau)
        {
            if (hit.CardIndex < 0 || !board.IsTableauFaceUp(hit.Pile, hit.CardIndex) ||
                !board.IsRunStart(hit.Pile, hit.CardIndex))
            {
                return;
            }

            var count = board.TableauCount(hit.Pile);
            for (var index = hit.CardIndex; index < count && grabCount < MaxGrab; index++)
            {
                grabbed[grabCount++] = board.TableauCardAt(hit.Pile, index);
            }

            grabOffset = mouse - layout.TableauCardRect(hit.Pile, hit.CardIndex).Min;
        }

        if (grabCount > 0)
        {
            grabSource = hit;
            pressPosition = mouse;
            dragMoved = false;
        }
    }

    private SolitaireHit ComputeDropTarget(in SolitaireLayout layout, Vector2 mouse)
    {
        if (grabCount == 0)
        {
            return SolitaireHit.None;
        }

        var hit = layout.Hit(mouse);
        var first = grabbed[0];
        if (hit.Kind == SolitairePileKind.Foundation && grabCount == 1 &&
            grabSource.Kind != SolitairePileKind.Foundation && board.CanFoundation(first))
        {
            return new SolitaireHit(SolitairePileKind.Foundation, SolitaireBoard.Suit(first), -1);
        }

        if (hit.Kind == SolitairePileKind.Tableau)
        {
            if (grabSource.Kind == SolitairePileKind.Tableau && grabSource.Pile == hit.Pile)
            {
                return SolitaireHit.None;
            }

            if (board.CanTableau(first, hit.Pile))
            {
                return new SolitaireHit(SolitairePileKind.Tableau, hit.Pile, board.TableauCount(hit.Pile) - 1);
            }
        }

        return SolitaireHit.None;
    }

    private void ReleaseGrab(in SolitaireLayout layout, Vector2 mouse, in SolitaireHit dropTarget, float scale)
    {
        var card = grabbed[0];
        var origin = dragMoved ? mouse - grabOffset : GrabOrigin(layout);
        var toFoundation = false;
        var acted = false;
        if (!dragMoved)
        {
            acted = TapToFoundation();
            toFoundation = acted;
        }
        else if (dropTarget.Kind == SolitairePileKind.Foundation)
        {
            acted = DropToFoundation();
            toFoundation = acted;
        }
        else if (dropTarget.Kind == SolitairePileKind.Tableau)
        {
            acted = DropToTableau(dropTarget.Pile);
        }

        if (acted)
        {
            AfterMove(layout, card, toFoundation, origin, scale);
        }

        grabSource = SolitaireHit.None;
        grabCount = 0;
        dragMoved = false;
    }

    private Vector2 GrabOrigin(in SolitaireLayout layout) => grabSource.Kind switch
    {
        SolitairePileKind.Waste => layout.WasteRect.Min,
        SolitairePileKind.Foundation => layout.FoundationRect(grabSource.Pile).Min,
        _ => layout.TableauCardRect(grabSource.Pile, grabSource.CardIndex).Min,
    };

    private bool TapToFoundation()
    {
        if (grabSource.Kind == SolitairePileKind.Waste)
        {
            return board.SendWasteToFoundation();
        }

        if (grabSource.Kind == SolitairePileKind.Tableau && grabCount == 1)
        {
            return board.SendTableauToFoundation(grabSource.Pile);
        }

        return false;
    }

    private bool DropToFoundation()
    {
        if (grabSource.Kind == SolitairePileKind.Waste)
        {
            return board.SendWasteToFoundation();
        }

        if (grabSource.Kind == SolitairePileKind.Tableau)
        {
            return board.SendTableauToFoundation(grabSource.Pile);
        }

        return false;
    }

    private bool DropToTableau(int destPile)
    {
        if (grabSource.Kind == SolitairePileKind.Waste)
        {
            return board.MoveWasteToTableau(destPile);
        }

        if (grabSource.Kind == SolitairePileKind.Foundation)
        {
            return board.MoveFoundationToTableau(grabSource.Pile, destPile);
        }

        if (grabSource.Kind == SolitairePileKind.Tableau)
        {
            return board.MoveTableauToTableau(grabSource.Pile, grabSource.CardIndex, destPile);
        }

        return false;
    }

    private void AfterMove(in SolitaireLayout layout, int card, bool toFoundation, Vector2 origin, float scale)
    {
        if (toFoundation)
        {
            LaunchFlight(card, origin, layout.FoundationRect(SolitaireBoard.Suit(card)).Min);
        }
        else
        {
            UiFeedback.Play(UiSound.GameCardPlace);
        }

        if (board.LastFlippedPile < 0)
        {
            return;
        }

        UiFeedback.Play(UiSound.GameCardFlip);
        var pile = board.LastFlippedPile;
        var top = board.TableauCount(pile) - 1;
        if (top >= 0)
        {
            var center = layout.TableauCardRect(pile, top).Center;
            particles.Burst(center, 8, Core.Theme.Accent.Amber, 120f * scale, 2.6f, 0.45f, 220f);
        }
    }
}
