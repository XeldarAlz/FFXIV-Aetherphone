using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Online;

// The live Uno table. Opponents fan their backs along the top, the deck and the discard pile sit
// in the middle inside an orbit that shows the direction of play, and the hand fans out at the
// bottom. Every card position is a spring toward a layout the server dictates, so cards glide
// into the fan when dealt, fly to the pile when played, and the pile remembers the last few
// throws with a tilt. Nothing here decides a rule: a tap sends an intent and the next event
// repaints the truth.
internal sealed class OnlineUnoTable
{
    private const float HandCardWidth = 58f;
    private const float TableCardWidth = 62f;
    private const float SeatCardWidth = 22f;
    private const float PileSpread = 62f;
    private const float CenterFraction = 0.44f;
    private const float FanMaxAngle = 0.30f;
    private const float FanArcDrop = 16f;
    private const float FanStepFraction = 0.66f;
    private const float HoverLift = 26f;
    private const float PlayableLift = 8f;
    private const float NeighborPush = 18f;
    private const float FlightSeconds = 0.36f;
    private const float FlightArc = 24f;
    private const float DealStaggerSeconds = 0.08f;
    private const float BannerSeconds = 1.5f;
    private const float PositionSmoothing = 0.16f;
    private const float AngleSmoothing = 0.14f;
    private const float ScaleSmoothing = 0.10f;
    private const float OrbitRadiansPerSecond = 0.55f;
    private const int OrbitChevrons = 3;
    private const int OrbitSegments = 64;
    private const int PileDepth = 4;
    private const int SeatFanLimit = 8;
    private const long DepartureGraceMilliseconds = 1_500;

    private static readonly Vector4 DangerTint = new(0.85f, 0.35f, 0.32f, 1f);

    private struct HandSlot
    {
        public int Card;
        public Spring X;
        public Spring Y;
        public Spring Angle;
        public Spring Scale;
        public float Delay;
        public bool Claimed;
    }

    private struct Flight
    {
        public int Card;
        public Vector2 From;
        public Vector2 To;
        public float FromWidth;
        public float ToWidth;
        public float FromAngle;
        public float ToAngle;
        public float Delay;
        public float Progress;
        public bool FaceUp;
        public bool ToPile;
    }

    private struct PileEntry
    {
        public int Card;
        public float Angle;
        public Vector2 Offset;
    }

    private readonly GameRoomsStore store;
    private readonly ParticleSystem particles = new(256);
    private readonly PileEntry[] pile = new PileEntry[PileDepth];
    private readonly Spring[] swatchScales = { new(1f), new(1f), new(1f), new(1f) };

    private HandSlot[] slots = new HandSlot[16];
    private HandSlot[] scratch = new HandSlot[16];
    private int slotCount;
    private Flight[] flights = new Flight[12];
    private int flightCount;
    private int pileCount;
    private int pilePushes;
    private Vector2[] seatAnchors = Array.Empty<Vector2>();
    private Spring[] seatCounts = Array.Empty<Spring>();
    private int[] previousCounts = Array.Empty<int>();

    private int seenActionCount = -1;
    private long seenRound = -1;
    private int lastTurnSeat = -2;
    private int expectedDepartureCard = -1;
    private long expectedDepartureAtTick;
    private int ambientColor = int.MinValue;
    private Vector4 ambientFrom;
    private Vector4 ambientTo;
    private Spring ambientBlend = new(1f);
    private Spring statusPop = new(1f);
    private Spring deckLift = new(0f);
    private Spring passReveal = new(0f);
    private Spring pickerReveal = new(0f);
    private string bannerText = string.Empty;
    private Vector4 bannerAccent;
    private float bannerProgress = 1f;
    private int wildPendingCard = -1;
    private int wildOpenedFrame = -1;

    private Vector2 origin;
    private float uiScale = 1f;
    private Vector2 deckAnchor;
    private Vector2 discardAnchor;
    private Vector2 handAnchor;
    private float seatSlotWidth;

    public OnlineUnoTable(GameRoomsStore store)
    {
        this.store = store;
    }

    public void Reset()
    {
        slotCount = 0;
        flightCount = 0;
        pileCount = 0;
        seenActionCount = -1;
        seenRound = -1;
        lastTurnSeat = -2;
        expectedDepartureCard = -1;
        ambientColor = int.MinValue;
        bannerProgress = 1f;
        wildPendingCard = -1;
        particles.Clear();
        seatAnchors = Array.Empty<Vector2>();
        previousCounts = Array.Empty<int>();
        seatCounts = Array.Empty<Spring>();
    }

    public void Draw(Rect body, PhoneTheme theme, float scale, GameRoomSnapshotDto snapshot,
        UnoRoomStateDto board, string notice)
    {
        using var surface = AppSurface.Begin(body, true);
        ImGui.Dummy(new Vector2(MathF.Max(1f, body.Width - 32f * scale), body.Height - 16f * scale));
        var drawList = ImGui.GetWindowDrawList();
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var accent = Core.Apps.AppAccents.For("games");
        var players = board.Players ?? Array.Empty<UnoPlayerDto>();
        var mySeat = SeatOf(players, store.AccountId);
        var mine = store.Room.Private?.Uno;
        var hand = mine?.Hand ?? Array.Empty<int>();
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var remaining = store.Room.RemainingMilliseconds(snapshot.PhaseEndsAtUnixMs, nowMs);
        var live = board.WinnerSeat < 0;
        var myTurn = live && mySeat >= 0 && board.TurnSeat == mySeat;
        var pending = myTurn && board.PendingDraw && mine is not null && mine.PendingDrawnPlayable;

        origin = body.Min;
        uiScale = scale;
        deckAnchor = new Vector2(body.Width * 0.5f - PileSpread * scale, body.Height * CenterFraction);
        discardAnchor = new Vector2(body.Width * 0.5f + PileSpread * scale, body.Height * CenterFraction);
        handAnchor = new Vector2(body.Width * 0.5f, body.Height - 14f * scale - HandCardWidth * UnoCardArt.Aspect * 0.5f * scale);

        EnsureSeatBuffers(players.Length);
        PlaceSeats(body, scale, players, mySeat);
        var ambient = StepAmbient(board.ActiveColor, delta);
        GameScene.Ambient(drawList, body, ambient);

        ObserveBoard(board, players, mySeat, myTurn, accent, scale);
        ReconcileHand(hand, scale);
        AdvanceFlights(delta);
        particles.Update(delta);
        bannerProgress = GameBanner.Advance(bannerProgress, delta, BannerSeconds);

        DrawSeats(drawList, theme, scale, board, players, mySeat, remaining, accent, delta);
        DrawCenter(drawList, theme, scale, board, myTurn, ambient, accent, delta);
        DrawStatus(drawList, body, theme, scale, board, players, hand, myTurn, pending, remaining, accent, notice,
            delta);
        DrawHand(drawList, body, theme, scale, board, mine, myTurn, pending, accent, delta);
        DrawFlights(drawList, scale);
        particles.Draw(drawList, scale);
        if (bannerProgress < 1f)
        {
            var bannerCenter = Absolute(new Vector2(body.Width * 0.5f,
                discardAnchor.Y - (TableCardWidth * UnoCardArt.Aspect * 0.5f + 44f) * scale));
            GameBanner.Draw(drawList, bannerCenter, bannerText, bannerAccent, theme, bannerProgress);
        }

        if (wildPendingCard >= 0)
        {
            DrawColorPicker(drawList, body, theme, scale, delta);
        }
    }

    private Vector2 Absolute(Vector2 relative)
    {
        return origin + relative;
    }

    private void EnsureSeatBuffers(int count)
    {
        if (seatAnchors.Length == count)
        {
            return;
        }

        seatAnchors = new Vector2[count];
        seatCounts = new Spring[count];
        previousCounts = new int[count];
        for (var seat = 0; seat < count; seat++)
        {
            seatCounts[seat] = new Spring(0f);
            previousCounts[seat] = -1;
        }
    }

    private void PlaceSeats(Rect body, float scale, UnoPlayerDto[] players, int mySeat)
    {
        var others = players.Length - (mySeat >= 0 ? 1 : 0);
        seatSlotWidth = others > 0 ? body.Width / others : body.Width;
        var slot = 0;
        for (var offset = 1; offset <= players.Length; offset++)
        {
            var seat = mySeat >= 0 ? (mySeat + offset) % players.Length : offset - 1;
            if (seat == mySeat)
            {
                seatAnchors[seat] = handAnchor;
                continue;
            }

            seatAnchors[seat] = new Vector2(seatSlotWidth * (slot + 0.5f), 42f * scale);
            slot++;
        }
    }

    private Vector4 StepAmbient(int activeColor, float delta)
    {
        var target = UnoCardArt.ColorFor(activeColor);
        if (activeColor != ambientColor)
        {
            ambientFrom = ambientColor == int.MinValue ? target : Vector4.Lerp(ambientFrom, ambientTo, ambientBlend.Value);
            ambientTo = target;
            ambientColor = activeColor;
            ambientBlend.SnapTo(0f);
        }

        var blend = ambientBlend.Step(1f, 0.35f, delta);
        return Vector4.Lerp(ambientFrom, ambientTo, blend);
    }

    private void ObserveBoard(UnoRoomStateDto board, UnoPlayerDto[] players, int mySeat, bool myTurn,
        Vector4 accent, float scale)
    {
        if (board.RoundIndex != seenRound)
        {
            seenRound = board.RoundIndex;
            pileCount = 0;
            flightCount = 0;
            slotCount = 0;
            seenActionCount = -1;
            lastTurnSeat = -2;
        }

        var first = seenActionCount < 0;
        if (board.ActionCount != seenActionCount)
        {
            seenActionCount = board.ActionCount;
            if (!first)
            {
                ReactToAction(board, players, mySeat, accent, scale);
            }
        }

        if (expectedDepartureCard >= 0 && Environment.TickCount64 - expectedDepartureAtTick > DepartureGraceMilliseconds)
        {
            expectedDepartureCard = -1;
        }

        SyncPile(board, scale);

        if (board.TurnSeat != lastTurnSeat)
        {
            var wasObserved = lastTurnSeat != -2;
            lastTurnSeat = board.TurnSeat;
            statusPop.SnapTo(1.24f);
            if (myTurn && wasObserved)
            {
                ShowBanner(Loc.T(L.Games.OnlineYourTurn), accent);
            }
        }

        for (var seat = 0; seat < players.Length; seat++)
        {
            var count = players[seat].CardCount;
            if (previousCounts[seat] > 1 && count == 1 && !first)
            {
                ShowBanner(Loc.T(L.Games.OnlineUnoCall), UnoCardArt.ColorFor(0));
                particles.Sparkle(Absolute(seatAnchors[seat]), 18, UnoCardArt.ColorFor(1), 140f * scale,
                    3f * scale, 0.8f);
            }

            previousCounts[seat] = count;
        }
    }

    private void ReactToAction(UnoRoomStateDto board, UnoPlayerDto[] players, int mySeat, Vector4 accent,
        float scale)
    {
        var seat = board.LastSeat;
        var seatKnown = seat >= 0 && seat < players.Length;
        switch (board.LastKind)
        {
            case GameRoomWire.UnoPlayEvent:
                if (seatKnown && seat != mySeat)
                {
                    AddFlight(board.LastCard, seatAnchors[seat], discardAnchor, SeatCardWidth * scale,
                        TableCardWidth * scale, 0f, NextPileAngle(), 0f, true, true);
                }
                else if (seatKnown && HoldsCard(board.LastCard))
                {
                    expectedDepartureCard = board.LastCard;
                    expectedDepartureAtTick = Environment.TickCount64;
                }

                ReactToCardEffect(board, players, mySeat, seat, accent, scale);
                break;
            case GameRoomWire.UnoDrawEvent:
                if (seatKnown && seat != mySeat)
                {
                    AddFlight(-1, deckAnchor, seatAnchors[seat], TableCardWidth * scale, SeatCardWidth * scale,
                        0f, 0f, 0f, false, false);
                }

                break;
            case GameRoomWire.UnoTimeoutEvent:
                if (seatKnown && seat != mySeat)
                {
                    AddFlight(-1, deckAnchor, seatAnchors[seat], TableCardWidth * scale, SeatCardWidth * scale,
                        0f, 0f, 0f, false, false);
                }

                ShowBanner(Loc.T(L.Games.OnlineTimedOut), DangerTint);
                break;
        }
    }

    private void ReactToCardEffect(UnoRoomStateDto board, UnoPlayerDto[] players, int mySeat, int seat,
        Vector4 accent, float scale)
    {
        var card = board.LastCard;
        var penalty = 0;
        if (card == GameRoomWire.WildDrawFourCard)
        {
            ShowBanner("+4", accent);
            penalty = 4;
        }
        else
        {
            switch (GameRoomWire.RankOf(card))
            {
                case GameRoomWire.RankDrawTwo:
                    ShowBanner("+2", accent);
                    penalty = 2;
                    break;
                case GameRoomWire.RankSkip:
                    ShowBanner(Loc.T(L.Games.OnlineSkipped), accent);
                    break;
                case GameRoomWire.RankReverse:
                    ShowBanner(Loc.T(L.Games.OnlineReversed), accent);
                    break;
            }
        }

        if (penalty == 0 || seat < 0 || players.Length < 2)
        {
            return;
        }

        var victim = board.Clockwise
            ? (seat + 1) % players.Length
            : (seat - 1 + players.Length) % players.Length;
        if (victim == mySeat)
        {
            return;
        }

        for (var index = 0; index < penalty; index++)
        {
            AddFlight(-1, deckAnchor, seatAnchors[victim], TableCardWidth * scale, SeatCardWidth * scale, 0f, 0f,
                FlightSeconds * 0.5f + index * DealStaggerSeconds, false, false);
        }
    }

    private bool HoldsCard(int card)
    {
        for (var index = 0; index < slotCount; index++)
        {
            if (slots[index].Card == card)
            {
                return true;
            }
        }

        return false;
    }

    private void ShowBanner(string text, Vector4 accent)
    {
        bannerText = text;
        bannerAccent = accent;
        bannerProgress = 0f;
    }

    private float NextPileAngle()
    {
        return ((pilePushes * 5) % 7 - 3) * 0.07f;
    }

    private void PushPile(int card, float angle, float scale)
    {
        if (pileCount == PileDepth)
        {
            Array.Copy(pile, 1, pile, 0, PileDepth - 1);
            pileCount--;
        }

        var jitter = ((pilePushes * 3) % 5 - 2) * 1.2f * scale;
        pile[pileCount] = new PileEntry
        {
            Card = card,
            Angle = angle,
            Offset = new Vector2(jitter, -jitter * 0.5f),
        };
        pileCount++;
        pilePushes++;
    }

    private void SyncPile(UnoRoomStateDto board, float scale)
    {
        if (board.DiscardTop < 0)
        {
            return;
        }

        if (pileCount > 0 && pile[pileCount - 1].Card == board.DiscardTop)
        {
            return;
        }

        if (expectedDepartureCard == board.DiscardTop)
        {
            return;
        }

        for (var index = 0; index < flightCount; index++)
        {
            if (flights[index].ToPile)
            {
                return;
            }
        }

        PushPile(board.DiscardTop, NextPileAngle(), scale);
    }

    private void AddFlight(int card, Vector2 from, Vector2 to, float fromWidth, float toWidth, float fromAngle,
        float toAngle, float delay, bool faceUp, bool toPile)
    {
        if (flightCount == flights.Length)
        {
            Array.Resize(ref flights, flights.Length * 2);
        }

        flights[flightCount] = new Flight
        {
            Card = card,
            From = from,
            To = to,
            FromWidth = fromWidth,
            ToWidth = toWidth,
            FromAngle = fromAngle,
            ToAngle = toAngle,
            Delay = delay,
            Progress = 0f,
            FaceUp = faceUp,
            ToPile = toPile,
        };
        flightCount++;
    }

    private void AdvanceFlights(float delta)
    {
        for (var index = flightCount - 1; index >= 0; index--)
        {
            ref var flight = ref flights[index];
            if (flight.Delay > 0f)
            {
                flight.Delay -= delta;
                continue;
            }

            flight.Progress += delta / FlightSeconds;
            if (flight.Progress < 1f)
            {
                continue;
            }

            if (flight.ToPile)
            {
                PushPile(flight.Card, flight.ToAngle, uiScale);
            }

            flights[index] = flights[flightCount - 1];
            flightCount--;
        }
    }

    private void DrawFlights(ImDrawListPtr drawList, float scale)
    {
        for (var index = 0; index < flightCount; index++)
        {
            ref readonly var flight = ref flights[index];
            if (flight.Delay > 0f)
            {
                continue;
            }

            var eased = Easing.EaseOutCubic(flight.Progress);
            var position = Vector2.Lerp(flight.From, flight.To, eased);
            position.Y -= MathF.Sin(eased * MathF.PI) * FlightArc * scale;
            var width = Easing.Lerp(flight.FromWidth, flight.ToWidth, eased);
            var angle = Easing.Lerp(flight.FromAngle, flight.ToAngle, eased);
            var rect = UnoCardArt.RectAround(Absolute(position), width);
            if (flight.FaceUp)
            {
                UnoCardArt.DrawFace(drawList, rect, flight.Card, scale, false, 1f, angle);
            }
            else
            {
                UnoCardArt.DrawBack(drawList, rect, scale, 1f, angle);
            }
        }
    }

    // The hand is reconciled by card identity: a card that is still there keeps its springs, a
    // card that vanished was played and flies to the pile, a card that appeared was drawn and
    // starts on the deck. Only a changed hand pays for this walk.
    private void ReconcileHand(int[] hand, float scale)
    {
        if (HandUnchanged(hand))
        {
            return;
        }

        if (scratch.Length < hand.Length)
        {
            var capacity = Math.Max(hand.Length, scratch.Length * 2);
            scratch = new HandSlot[capacity];
            Array.Resize(ref slots, capacity);
        }

        var dealt = 0;
        for (var index = 0; index < hand.Length; index++)
        {
            var match = FindSlot(hand[index], index);
            if (match >= 0)
            {
                slots[match].Claimed = true;
                scratch[index] = slots[match];
                scratch[index].Claimed = false;
                continue;
            }

            scratch[index] = new HandSlot
            {
                Card = hand[index],
                X = new Spring(deckAnchor.X),
                Y = new Spring(deckAnchor.Y),
                Angle = new Spring(0f),
                Scale = new Spring(TableCardWidth / HandCardWidth),
                Delay = dealt * DealStaggerSeconds,
            };
            dealt++;
        }

        for (var index = 0; index < slotCount; index++)
        {
            ref readonly var slot = ref slots[index];
            if (slot.Claimed)
            {
                continue;
            }

            AddFlight(slot.Card, new Vector2(slot.X.Value, slot.Y.Value), discardAnchor,
                HandCardWidth * scale * slot.Scale.Value, TableCardWidth * scale, slot.Angle.Value,
                NextPileAngle(), 0f, true, true);
            if (slot.Card == expectedDepartureCard)
            {
                expectedDepartureCard = -1;
            }
        }

        (slots, scratch) = (scratch, slots);
        slotCount = hand.Length;
    }

    private bool HandUnchanged(int[] hand)
    {
        if (hand.Length != slotCount)
        {
            return false;
        }

        for (var index = 0; index < slotCount; index++)
        {
            if (slots[index].Card != hand[index])
            {
                return false;
            }
        }

        return true;
    }

    private int FindSlot(int card, int preferredIndex)
    {
        if (preferredIndex < slotCount && !slots[preferredIndex].Claimed && slots[preferredIndex].Card == card)
        {
            return preferredIndex;
        }

        for (var index = 0; index < slotCount; index++)
        {
            if (!slots[index].Claimed && slots[index].Card == card)
            {
                return index;
            }
        }

        return -1;
    }

    private void DrawSeats(ImDrawListPtr drawList, PhoneTheme theme, float scale, UnoRoomStateDto board,
        UnoPlayerDto[] players, int mySeat, long remaining, Vector4 accent, float delta)
    {
        var others = players.Length - (mySeat >= 0 ? 1 : 0);
        if (others <= 0)
        {
            return;
        }

        for (var seat = 0; seat < players.Length; seat++)
        {
            if (seat == mySeat)
            {
                continue;
            }

            var player = players[seat];
            var anchor = Absolute(seatAnchors[seat]);
            var dim = player.Away ? 0.35f : 1f;
            var onTurn = board.TurnSeat == seat && board.WinnerSeat < 0;
            if (onTurn)
            {
                ProgressRing.Glow(anchor, 40f * scale, accent, 0.35f + 0.25f * Pulse.Wave(Pulse.Calm));
            }

            var visible = seatCounts[seat].Step(MathF.Min(player.CardCount, SeatFanLimit), 0.22f, delta);
            DrawSeatFan(drawList, anchor, visible, scale, dim);

            var badgeCenter = anchor + new Vector2(24f, 12f) * scale;
            var badgeRadius = 11f * scale;
            drawList.AddCircleFilled(badgeCenter, badgeRadius,
                ImGui.GetColorU32(new Vector4(0.06f, 0.06f, 0.08f, 0.92f * dim)), 24);
            drawList.AddCircle(badgeCenter, badgeRadius,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.28f * dim)), 24, 1f * scale);
            Typography.DrawCentered(drawList, badgeCenter, player.CardCount.ToString(Loc.Culture),
                theme.TextStrong with { W = dim }, TextStyles.FootnoteEmphasized);
            if (onTurn)
            {
                TurnTimerRing.Draw(drawList, badgeCenter, badgeRadius + 4f * scale, remaining, board.TurnSeconds,
                    accent, scale);
            }

            var nameWidth = MathF.Max(40f * scale, seatSlotWidth - 12f * scale);
            var name = Typography.FitText(player.DisplayName, nameWidth, TextStyles.Caption1);
            Typography.DrawCentered(drawList, new Vector2(anchor.X, anchor.Y + 40f * scale), name,
                (onTurn ? theme.TextStrong : theme.TextMuted) with { W = dim }, TextStyles.Caption1);
        }
    }

    private static void DrawSeatFan(ImDrawListPtr drawList, Vector2 anchor, float visible, float scale, float dim)
    {
        var count = (int)MathF.Ceiling(visible - 0.001f);
        if (count <= 0)
        {
            return;
        }

        var width = SeatCardWidth * scale;
        var spread = MathF.Min(count * 4.5f, 30f) * scale;
        var fanAngle = MathF.Min(0.55f, count * 0.1f);
        for (var index = 0; index < count; index++)
        {
            var t = count > 1 ? index / (float)(count - 1) * 2f - 1f : 0f;
            var center = anchor + new Vector2(t * spread, t * t * 4f * scale);
            var alpha = index == count - 1 ? MathF.Min(1f, visible - (count - 1)) : 1f;
            UnoCardArt.DrawBack(drawList, UnoCardArt.RectAround(center, width), scale, dim * alpha, t * fanAngle);
        }
    }

    private void DrawCenter(ImDrawListPtr drawList, PhoneTheme theme, float scale, UnoRoomStateDto board,
        bool myTurn, Vector4 ambient, Vector4 accent, float delta)
    {
        var deckCenter = Absolute(deckAnchor);
        var discardCenter = Absolute(discardAnchor);
        var cardWidth = TableCardWidth * scale;

        ProgressRing.Glow(discardCenter, cardWidth * 1.1f, ambient, 0.6f);
        DrawOrbit(drawList, theme, scale, board.Clockwise, (deckCenter + discardCenter) * 0.5f);

        var canDraw = myTurn && !board.PendingDraw && !store.ActInFlight && board.WinnerSeat < 0;
        var deckRect = UnoCardArt.RectAround(deckCenter, cardWidth);
        var deckHovered = canDraw && UiInteract.Hover(deckRect.Min, deckRect.Max);
        var lift = deckLift.Step(deckHovered ? -6f * scale : 0f, 0.12f, delta);
        for (var layer = 2; layer >= 1; layer--)
        {
            var layerRect = UnoCardArt.RectAround(deckCenter + new Vector2(0f, layer * 2.5f * scale), cardWidth);
            UnoCardArt.DrawBack(drawList, layerRect, scale, 0.55f);
        }

        var topRect = UnoCardArt.RectAround(deckCenter + new Vector2(0f, lift), cardWidth);
        if (canDraw)
        {
            ProgressRing.Glow(topRect.Center, cardWidth * 0.9f, accent, 0.35f + 0.3f * Pulse.Wave(Pulse.Calm));
        }

        UnoCardArt.DrawBack(drawList, topRect, scale, canDraw ? 1f : 0.8f);
        if (canDraw)
        {
            Squircle.Stroke(drawList, topRect.Min, topRect.Max, cardWidth * 0.18f,
                ImGui.GetColorU32(Palette.WithAlpha(accent, deckHovered ? 1f : 0.55f + 0.3f * Pulse.Wave(Pulse.Calm))),
                2f * scale);
            if (deckHovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(deckRect.Min, deckRect.Max, deckHovered))
            {
                store.SendDraw();
            }
        }

        var countLabel = board.DrawPileCount.ToString(Loc.Culture);
        var pillSize = new Vector2(Typography.Measure(countLabel, TextStyles.Caption1).X + 16f * scale, 18f * scale);
        var pillCenter = new Vector2(deckCenter.X, deckRect.Max.Y + 9f * scale);
        Material.Frosted(drawList, pillCenter - pillSize * 0.5f, pillCenter + pillSize * 0.5f, pillSize.Y * 0.5f,
            scale, 0.95f);
        Typography.DrawCentered(drawList, pillCenter, countLabel, theme.TextStrong, TextStyles.Caption1);
        Typography.DrawCentered(drawList, new Vector2(deckCenter.X, pillCenter.Y + 18f * scale),
            Loc.T(L.Games.OnlineDeck), theme.TextMuted, TextStyles.Caption2);

        for (var index = 0; index < pileCount; index++)
        {
            ref readonly var entry = ref pile[index];
            var depth = pileCount - 1 - index;
            var rect = UnoCardArt.RectAround(discardCenter + entry.Offset, cardWidth);
            UnoCardArt.DrawFace(drawList, rect, entry.Card, scale, false, depth == 0 ? 1f : 0.85f, entry.Angle);
        }

        var ringRadius = cardWidth * UnoCardArt.Aspect * 0.66f;
        drawList.AddCircle(discardCenter, ringRadius,
            ImGui.GetColorU32(Palette.WithAlpha(ambient, 0.75f + 0.2f * Pulse.Wave(Pulse.Breath))), 48, 3f * scale);
    }

    private static void DrawOrbit(ImDrawListPtr drawList, PhoneTheme theme, float scale, bool clockwise,
        Vector2 center)
    {
        var radiusX = (PileSpread + TableCardWidth * 0.5f + 30f) * scale;
        var radiusY = 76f * scale;
        var direction = clockwise ? 1f : -1f;
        var spin = (float)(ImGui.GetTime() * OrbitRadiansPerSecond) * direction;
        var track = ImGui.GetColorU32(theme.TextMuted with { W = 0.10f });
        drawList.PathClear();
        for (var segment = 0; segment < OrbitSegments; segment++)
        {
            var angle = segment * (MathF.PI * 2f / OrbitSegments);
            drawList.PathLineTo(center + new Vector2(MathF.Cos(angle) * radiusX, MathF.Sin(angle) * radiusY));
        }

        drawList.PathStroke(track, ImDrawFlags.Closed, 1f * scale);
        for (var chevron = 0; chevron < OrbitChevrons; chevron++)
        {
            var head = spin + chevron * (MathF.PI * 2f / OrbitChevrons);
            for (var tail = 3; tail >= 0; tail--)
            {
                var angle = head - direction * tail * 0.09f;
                var alpha = tail == 0 ? 0.7f : 0.32f - tail * 0.09f;
                var size = tail == 0 ? 6f : 4.5f - tail * 0.8f;
                DrawChevron(drawList, center, radiusX, radiusY, angle, direction, size * scale,
                    ImGui.GetColorU32(theme.TextMuted with { W = alpha }));
            }
        }
    }

    private static void DrawChevron(ImDrawListPtr drawList, Vector2 center, float radiusX, float radiusY,
        float angle, float direction, float size, uint color)
    {
        var at = center + new Vector2(MathF.Cos(angle) * radiusX, MathF.Sin(angle) * radiusY);
        var tangent = Vector2.Normalize(new Vector2(-radiusX * MathF.Sin(angle), radiusY * MathF.Cos(angle)))
            * direction;
        var normal = new Vector2(-tangent.Y, tangent.X);
        drawList.AddTriangleFilled(at + tangent * size, at - tangent * size * 0.5f + normal * size * 0.7f,
            at - tangent * size * 0.5f - normal * size * 0.7f, color);
    }

    private void DrawStatus(ImDrawListPtr drawList, Rect body, PhoneTheme theme, float scale,
        UnoRoomStateDto board, UnoPlayerDto[] players, int[] hand, bool myTurn, bool pending, long remaining,
        Vector4 accent, string notice, float delta)
    {
        var centerX = body.Min.X + body.Width * 0.5f;
        var baseY = origin.Y + discardAnchor.Y;
        string status;
        if (myTurn)
        {
            status = Loc.T(L.Games.OnlineYourTurn);
        }
        else if (board.TurnSeat >= 0 && board.TurnSeat < players.Length)
        {
            status = Loc.T(L.Games.OnlineTheirTurn, players[board.TurnSeat].DisplayName);
        }
        else
        {
            status = string.Empty;
        }

        if (!store.Room.Attached)
        {
            status = status.Length == 0
                ? Loc.T(L.Games.OnlineReconnecting)
                : status + " · " + Loc.T(L.Games.OnlineReconnecting);
        }

        if (notice.Length > 0)
        {
            status = notice;
        }

        if (myTurn && board.WinnerSeat < 0)
        {
            TurnTimerRing.Bar(drawList, new Vector2(centerX, baseY + 88f * scale), 150f * scale, 5f * scale,
                remaining, board.TurnSeconds, accent);
        }

        var pop = statusPop.Step(1f, 0.16f, delta);
        if (status.Length > 0)
        {
            var style = TextStyles.SubheadlineEmphasized;
            Typography.DrawCentered(drawList, new Vector2(centerX, baseY + 106f * scale),
                Typography.FitText(status, body.Width - 32f * scale, style),
                myTurn ? theme.TextStrong : theme.TextMuted, style.Scale * pop, style.Weight);
        }

        if (myTurn && !pending && board.WinnerSeat < 0 && !AnyPlayable(hand, board))
        {
            Typography.DrawCentered(drawList, new Vector2(centerX, baseY + 126f * scale),
                Typography.FitText(Loc.T(L.Games.OnlineNoPlayable), body.Width - 32f * scale, TextStyles.Footnote),
                theme.TextMuted, TextStyles.Footnote);
        }
    }

    private static bool AnyPlayable(int[] hand, UnoRoomStateDto board)
    {
        for (var index = 0; index < hand.Length; index++)
        {
            if (GameRoomWire.IsPlayable(hand[index], board.ActiveColor, board.DiscardTop))
            {
                return true;
            }
        }

        return false;
    }

    private void DrawHand(ImDrawListPtr drawList, Rect body, PhoneTheme theme, float scale,
        UnoRoomStateDto board, UnoYouDto? mine, bool myTurn, bool pending, Vector4 accent, float delta)
    {
        var cardWidth = HandCardWidth * scale;
        var cardHeight = cardWidth * UnoCardArt.Aspect;
        var handTop = origin.Y + handAnchor.Y - cardHeight * 0.5f;

        var reveal = passReveal.Step(pending ? 1f : 0f, 0.14f, delta);
        if (reveal > 0.02f)
        {
            var passCenter = new Vector2(body.Min.X + body.Width * 0.5f, handTop - (HoverLift + 22f) * scale);
            var passSize = new Vector2(110f * scale, 32f * scale) * (0.6f + 0.4f * reveal);
            if (GameHud.Button(passCenter, passSize, Loc.T(L.Games.OnlinePass), accent, theme)
                && pending && !store.ActInFlight)
            {
                store.SendPass();
            }
        }

        if (slotCount == 0)
        {
            return;
        }

        var count = slotCount;
        var available = body.Width - 24f * scale;
        var step = count <= 1
            ? 0f
            : MathF.Min(cardWidth * FanStepFraction, (available - cardWidth) / (count - 1));
        var halfSpan = step * (count - 1) * 0.5f;
        var fanAngle = FanMaxAngle * MathF.Min(1f, count / 7f);

        var hovered = -1;
        var interactive = wildPendingCard < 0 && board.WinnerSeat < 0;
        var bandMin = new Vector2(body.Min.X, handTop - HoverLift * scale - 8f * scale);
        var bandMax = new Vector2(body.Max.X, body.Max.Y);
        if (interactive && UiInteract.Hover(bandMin, bandMax))
        {
            var mouseX = ImGui.GetMousePos().X;
            var left = origin.X + handAnchor.X - halfSpan - cardWidth * 0.5f;
            for (var index = count - 1; index >= 0; index--)
            {
                var cardLeft = left + step * index;
                var cardRight = index == count - 1 ? cardLeft + cardWidth : cardLeft + step;
                if (mouseX >= cardLeft && mouseX < cardRight)
                {
                    hovered = index;
                    break;
                }
            }
        }

        var hoveredPlayable = false;
        for (var index = 0; index < count; index++)
        {
            ref var slot = ref slots[index];
            var playable = myTurn && board.WinnerSeat < 0
                && GameRoomWire.IsPlayable(slot.Card, board.ActiveColor, board.DiscardTop)
                && (!pending || slot.Card == mine!.PendingDrawnCard);
            var lifted = index == hovered && playable;
            if (lifted)
            {
                hoveredPlayable = true;
            }

            var t = halfSpan > 0f ? (step * index - halfSpan) / halfSpan : 0f;
            var targetX = handAnchor.X - halfSpan + step * index;
            var targetY = handAnchor.Y + t * t * FanArcDrop * scale;
            if (hovered >= 0 && index != hovered)
            {
                var distance = index - hovered;
                var push = NeighborPush * scale * MathF.Max(0f, 1f - (MathF.Abs(distance) - 1) / 3f);
                targetX += MathF.Sign(distance) * push;
            }

            if (lifted)
            {
                targetY -= HoverLift * scale;
            }
            else if (playable)
            {
                targetY -= PlayableLift * scale;
            }

            if (slot.Delay > 0f)
            {
                slot.Delay -= delta;
                continue;
            }

            slot.X.Step(targetX, PositionSmoothing, delta);
            slot.Y.Step(targetY, PositionSmoothing, delta);
            slot.Angle.Step(lifted ? 0f : t * fanAngle, AngleSmoothing, delta);
            slot.Scale.Step(lifted ? 1.1f : 1f, ScaleSmoothing, delta);
        }

        for (var index = 0; index < count; index++)
        {
            if (index == hovered)
            {
                continue;
            }

            DrawHandCard(drawList, index, cardWidth, scale, board, mine, myTurn, pending, false, accent);
        }

        if (hovered >= 0)
        {
            DrawHandCard(drawList, hovered, cardWidth, scale, board, mine, myTurn, pending, hoveredPlayable, accent);
        }

        if (!hoveredPlayable)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (store.ActInFlight || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        ref var picked = ref slots[hovered];
        picked.Scale.SnapTo(0.94f);
        if (GameRoomWire.IsWild(picked.Card))
        {
            wildPendingCard = picked.Card;
            wildOpenedFrame = ImGui.GetFrameCount();
            pickerReveal.SnapTo(0f);
            for (var swatch = 0; swatch < swatchScales.Length; swatch++)
            {
                swatchScales[swatch].SnapTo(0.6f);
            }
        }
        else
        {
            store.SendPlay(picked.Card, -1);
        }
    }

    private void DrawHandCard(ImDrawListPtr drawList, int index, float cardWidth, float scale, UnoRoomStateDto board,
        UnoYouDto? mine, bool myTurn, bool pending, bool lifted, Vector4 accent)
    {
        ref readonly var slot = ref slots[index];
        if (slot.Delay > 0f)
        {
            return;
        }

        var playable = myTurn && board.WinnerSeat < 0
            && GameRoomWire.IsPlayable(slot.Card, board.ActiveColor, board.DiscardTop)
            && (!pending || slot.Card == mine!.PendingDrawnCard);
        var alpha = !myTurn ? 0.88f : playable ? 1f : 0.5f;
        var center = Absolute(new Vector2(slot.X.Value, slot.Y.Value));
        var rect = UnoCardArt.RectAround(center, cardWidth * slot.Scale.Value);
        if (playable)
        {
            ProgressRing.Glow(center, cardWidth * (lifted ? 0.9f : 0.6f), accent, lifted ? 0.7f : 0.28f);
        }

        UnoCardArt.DrawFace(drawList, rect, slot.Card, scale, lifted, alpha, slot.Angle.Value);
    }

    private void DrawColorPicker(ImDrawListPtr drawList, Rect body, PhoneTheme theme, float scale, float delta)
    {
        var reveal = pickerReveal.Step(1f, 0.16f, delta);
        drawList.AddRectFilled(body.Min, body.Max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.62f * reveal)));
        var panelHalf = new Vector2(132f, 96f) * scale * (0.92f + 0.08f * reveal);
        var panel = new Rect(body.Center - panelHalf, body.Center + panelHalf);
        UiInteract.HoverOverlay(panel);
        Elevation.Floating(drawList, panel.Min, panel.Max, 20f * scale, scale, reveal);
        Squircle.Fill(drawList, panel.Min, panel.Max, 20f * scale,
            ImGui.GetColorU32(theme.AppBackground with { W = reveal }));
        Squircle.Stroke(drawList, panel.Min, panel.Max, 20f * scale,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.18f * reveal)), 1f * scale);

        var previewWidth = 34f * scale;
        var previewCenter = new Vector2(panel.Center.X, panel.Min.Y + 34f * scale);
        UnoCardArt.DrawFace(drawList, UnoCardArt.RectAround(previewCenter, previewWidth), wildPendingCard, scale,
            false, reveal);
        Typography.DrawCentered(drawList, new Vector2(panel.Center.X, panel.Min.Y + 70f * scale),
            Loc.T(L.Games.OnlinePickColor), theme.TextStrong with { W = reveal }, TextStyles.SubheadlineEmphasized);

        var swatch = 42f * scale;
        var gap = 12f * scale;
        var rowWidth = swatch * 4f + gap * 3f;
        var startX = panel.Center.X - rowWidth * 0.5f;
        var top = panel.Min.Y + 90f * scale;
        var chosen = -1;
        for (var color = 0; color < 4; color++)
        {
            var min = new Vector2(startX + color * (swatch + gap), top);
            var max = min + new Vector2(swatch, swatch);
            var hovered = UiInteract.HoverWindowOnly(min, max);
            var grow = swatchScales[color].Step(hovered ? 1.12f : 1f, 0.10f, delta) * reveal;
            var center = (min + max) * 0.5f;
            var half = new Vector2(swatch, swatch) * 0.5f * grow;
            var tint = UnoCardArt.ColorFor(color);
            if (hovered)
            {
                ProgressRing.Glow(center, swatch * 0.8f, tint, 0.6f);
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            Squircle.Fill(drawList, center - half, center + half, 12f * scale * grow,
                ImGui.GetColorU32(tint with { W = reveal }));
            Squircle.Stroke(drawList, center - half, center + half, 12f * scale * grow,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, (hovered ? 0.9f : 0.3f) * reveal)),
                (hovered ? 2f : 1f) * scale);
            if (UiInteract.Click(min, max, hovered))
            {
                chosen = color;
            }
        }

        if (chosen >= 0)
        {
            store.SendPlay(wildPendingCard, chosen);
            wildPendingCard = -1;
            return;
        }

        if (ImGui.GetFrameCount() != wildOpenedFrame && UiInteract.ClickedOutside(panel.Min, panel.Max))
        {
            wildPendingCard = -1;
        }
    }

    private static int SeatOf(UnoPlayerDto[] players, string userId)
    {
        for (var index = 0; index < players.Length; index++)
        {
            if (string.Equals(players[index].UserId, userId, StringComparison.Ordinal))
            {
                return players[index].Seat;
            }
        }

        return -1;
    }
}
