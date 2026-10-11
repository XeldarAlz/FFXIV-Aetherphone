using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Venue;

internal sealed class VenueCabinet : ICabinetIdle
{
    private const float IdleTurnRate = 0.6f;

    private readonly CasinoVenueStore venue;
    private readonly Action leaveRoom;
    private readonly CasinoTextCache texts = new();
    private readonly DiceTableRoom dice;
    private readonly DeathrollRoom deathroll;
    private readonly RaffleRoom raffle;
    private readonly RollLabels idleLabels = new();

    private string roomId = string.Empty;
    private VenueRoomKind kind;
    private string inlineReason = string.Empty;
    private bool entered;
    private float idleClock;

    public VenueCabinet(CasinoVenueStore venue, Action leaveRoom)
    {
        this.venue = venue;
        this.leaveRoom = leaveRoom;
        Action<VenueActDraft> act = Act;
        dice = new DiceTableRoom(texts, act);
        deathroll = new DeathrollRoom(texts, act);
        raffle = new RaffleRoom(texts, act);
    }

    public Backdrop IdleBackdrop => Backdrop.Strip;

    public string RoomId => roomId;

    public VenueRoomKind Kind => kind;

    public static float DeckHeight => CasinoStageLayout.DeckHeight;

    public int Currency => venue.ViewFor(roomId)?.Currency
        ?? (venue.Tables.CardFor(roomId) is { } card ? CasinoCurrencies.Of(card) : CasinoCurrencies.Practice);

    public static string GameIdOf(VenueRoomKind kind) => kind switch
    {
        VenueRoomKind.Deathroll => CasinoGames.Deathroll,
        VenueRoomKind.Raffle => CasinoGames.Raffle,
        _ => CasinoGames.DiceTable,
    };

    public static LocString NameOf(VenueRoomKind kind) => kind switch
    {
        VenueRoomKind.Deathroll => L.Venue.GameDeathroll,
        VenueRoomKind.Raffle => L.Venue.GameRaffle,
        _ => L.Venue.GameDiceTable,
    };

    public CasinoStageSpec Spec()
    {
        return new CasinoStageSpec(GameIdOf(kind), NameOf(kind), Backdrop.Strip, Room: true, DeckHeight: DeckHeight,
            Practice: Currency == CasinoCurrencies.Practice, InstantAvailable: true, Extra: L.Venue.TableSheet,
            HouseBanked: false);
    }

    public void Enter(string tableId, VenueRoomKind roomKind)
    {
        if (entered)
        {
            venue.Rooms.Leave();
        }

        roomId = tableId;
        kind = roomKind == VenueRoomKind.None ? VenueRoomKind.Dice : roomKind;
        inlineReason = string.Empty;
        entered = true;
        dice.Reset();
        deathroll.Reset();
        raffle.Reset();
        venue.TakeActAnswer();
        venue.TakeActFailure();
        venue.Rooms.Enter(tableId);
        venue.Tables.RefreshCard(tableId);
    }

    public void Reset()
    {
        if (entered)
        {
            venue.Rooms.Leave();
        }

        entered = false;
        roomId = string.Empty;
        inlineReason = string.Empty;
        dice.Reset();
        deathroll.Reset();
        raffle.Reset();
    }

    public void Gate()
    {
        raffle.Gate();
    }

    public void DrawOverlay(Rect screen, AppSkin ui)
    {
        if (kind == VenueRoomKind.Raffle && entered)
        {
            raffle.DrawOverlay(screen, ui);
        }
    }

    public bool Hosting()
    {
        var me = venue.AccountId;
        var card = venue.Tables.CardFor(roomId);
        if (card is null || me.Length == 0)
        {
            return false;
        }

        return string.Equals(card.OwnerUserId, me, StringComparison.Ordinal)
            || (card.Config is not null && Tables.TableDoor.IsCoDealer(card.Config, me));
    }

    public void Draw(CasinoStage stage, in CasinoStageFrame frame, AppSkin ui)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        ConsumeAnswers();
        var session = venue.Rooms.Room;
        var safe = frame.Safe;
        if (session.ClosedReason.Length > 0)
        {
            DrawClosed(drawList, ui, session.ClosedReason, safe, scale);
            return;
        }

        var view = venue.ViewFor(roomId);
        if (view is null || !view.Ready)
        {
            LoadingPulse.Draw(safe.Center, 16f * scale, CasinoColors.LightA, CasinoColors.InkBody,
                LoadingPulse.SafeLabel());
            return;
        }

        var now = session.ServerNowUnixMs(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        DrawRibbon(drawList, frame, view, now, session.Attached, scale);
        var world = safe;
        if (inlineReason.Length > 0)
        {
            var message = CasinoReasons.Text(inlineReason, 0);
            var height = CasinoNotice.Height(CasinoNoticeKind.Reason, string.Empty, message, safe.Width, scale);
            CasinoNotice.Draw(drawList, ui, CasinoNoticeKind.Reason, string.Empty, message, safe.Min.X,
                safe.Max.Y - height, safe.Width, scale);
            world = new Rect(safe.Min, new Vector2(safe.Max.X, safe.Max.Y - height - VenueArt.LineGap * scale));
        }

        var room = new VenueRoomFrame(stage, frame, ui, view, drawList, world, venue.AccountId, Hosting(),
            venue.ActInFlight, now, scale);
        switch (view.Kind)
        {
            case VenueRoomKind.Deathroll:
                deathroll.Draw(room, frame.Deck);
                break;
            case VenueRoomKind.Raffle:
                raffle.Draw(room, frame.Deck);
                break;
            default:
                dice.Draw(room, frame.Deck);
                break;
        }
    }

    public void DrawIdle(ImDrawListPtr drawList, Rect rect, float deltaSeconds)
    {
        var scale = UiScale.Current;
        idleClock += deltaSeconds * IdleTurnRate;
        var side = MathF.Min(rect.Width, rect.Height) * 0.5f;
        var tile = new Rect(rect.Center - new Vector2(side * 0.5f), rect.Center + new Vector2(side * 0.5f));
        var step = VenueTumble.StepAt(idleClock);
        var value = VenueTumble.Value(1, step / 6, VenueRules.DefaultSides);
        VenueArt.DrawNumberTile(drawList, tile, idleLabels.Value(value), true, false, 0.6f, idleClock * 4f, scale);
    }

    public VenueRoomView? View => venue.ViewFor(roomId);

    private void Act(VenueActDraft draft)
    {
        inlineReason = string.Empty;
        venue.Act(roomId, draft);
    }

    private void ConsumeAnswers()
    {
        if (venue.TakeActFailure())
        {
            inlineReason = CasinoReasons.Unreachable;
            dice.Playback.CancelMine();
        }

        var answer = venue.TakeActAnswer();
        if (answer is null)
        {
            return;
        }

        inlineReason = answer.Granted ? string.Empty : answer.Reason;
        dice.OnActAnswer(answer);
    }

    private void DrawRibbon(ImDrawListPtr drawList, in CasinoStageFrame frame, VenueRoomView view, long now,
        bool attached, float scale)
    {
        var name = view.Name.Length > 0 ? view.Name : Loc.T(NameOf(view.Kind));
        var remaining = 0L;
        var window = 0;
        switch (view.Kind)
        {
            case VenueRoomKind.Dice when view.Dice?.Round is { Closed: false } round:
                remaining = Math.Max(0, round.EndsAtUnixMs - now);
                window = view.Dice.RoundSeconds;
                break;
            case VenueRoomKind.Deathroll when view.Deathroll?.Duel is { } duel && duel.TurnEndsAtUnixMs > 0:
                remaining = Math.Max(0, duel.TurnEndsAtUnixMs - now);
                window = duel.Phase == DuelPhases.Open ? VenueRules.ChallengeLapseSeconds : VenueRules.DuelTurnSeconds;
                break;
        }

        PhaseRibbon.Draw(drawList, frame.Layout.Ribbon, name, remaining, remaining > 0 ? window : 0,
            view.Snapshot.Occupancy, attached ? CasinoColors.LightA : CasinoColors.InkMuted, scale);
    }

    private void DrawClosed(ImDrawListPtr drawList, AppSkin ui, string reason, Rect safe, float scale)
    {
        var title = Loc.T(L.Venue.RoomClosedTitle);
        var hint = Loc.T(CasinoReasons.TryMessage(reason, out var known) ? known : L.Venue.RoomClosedHint);
        CasinoNotice.DrawWithAction(drawList, ui, CasinoNoticeKind.Card, title, hint, Loc.T(L.Casino.WheelBackToFloor),
            safe.Min.X, safe.Min.Y + Metrics.Space.Lg * scale, safe.Width, scale, out var pressed);
        if (pressed)
        {
            leaveRoom();
        }
    }
}
