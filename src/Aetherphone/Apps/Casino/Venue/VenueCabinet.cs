using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Venue;

internal sealed class VenueCabinet : ICabinetIdle
{
    private const float IdleTurnRate = 0.6f;

    private readonly CasinoVenueStore venue;
    private readonly Action leaveRoom;
    private readonly Action openGilLedger;
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

    public VenueCabinet(CasinoVenueStore venue, Action leaveRoom, Action openGilLedger, ConfirmService confirm)
    {
        this.venue = venue;
        this.leaveRoom = leaveRoom;
        this.openGilLedger = openGilLedger;
        Action<VenueActDraft> act = Act;
        dice = new DiceTableRoom(texts, act);
        deathroll = new DeathrollRoom(texts, act);
        raffle = new RaffleRoom(texts, confirm, act);
    }

    public Backdrop IdleBackdrop => Backdrop.Strip;

    public string RoomId => roomId;

    public VenueRoomKind Kind => kind;

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
        return new CasinoStageSpec(GameIdOf(kind), NameOf(kind), Backdrop.Strip, Room: true,
            DeckHeight: RaffleTicketPicker.DeckHeightFor(venue.ViewFor(roomId), Owning(), venue.AccountId),
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
            venue.Tables.ForgetKnock();
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

    private bool Owning()
    {
        var me = venue.AccountId;
        var card = venue.Tables.CardFor(roomId);
        return card is not null && me.Length > 0 && string.Equals(card.OwnerUserId, me, StringComparison.Ordinal);
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
        var knockHeight = Tables.HostKnockBanner.Reserve(venue.Tables, roomId, safe.Width, scale);
        var world = new Rect(new Vector2(safe.Min.X, safe.Min.Y + knockHeight), safe.Max);
        if (view.Gil)
        {
            world = DrawLedgerShortcut(drawList, ui, world, scale);
        }

        if (inlineReason.Length > 0)
        {
            var message = CasinoReasons.Text(inlineReason, 0);
            var height = CasinoNotice.Height(CasinoNoticeKind.Reason, string.Empty, message, safe.Width, scale);
            CasinoNotice.Draw(drawList, ui, CasinoNoticeKind.Reason, string.Empty, message, safe.Min.X,
                safe.Max.Y - height, safe.Width, scale);
            world = new Rect(world.Min, new Vector2(safe.Max.X, safe.Max.Y - height - VenueArt.LineGap * scale));
        }

        var room = new VenueRoomFrame(stage, frame, ui, view, drawList, world, venue.AccountId, Hosting(), Owning(),
            venue.ActInFlight || view.Paused, now, scale);
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

        Tables.HostKnockBanner.Draw(drawList, ui, venue.Tables, roomId, safe.Min, safe.Width, scale);
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

    private Rect DrawLedgerShortcut(ImDrawListPtr drawList, AppSkin ui, Rect world, float scale)
    {
        var label = Loc.T(L.Tables.MyLedgerTitle);
        var height = Button.RegularHeight * scale;
        var width = MathF.Min(world.Width, Button.WidthFor(label, ButtonSize.Regular));
        var rect = new Rect(new Vector2(world.Max.X - width, world.Min.Y),
            new Vector2(world.Max.X, world.Min.Y + height));
        if (Button.Draw(drawList, rect, label, ui.Ink, ButtonStyle.Tinted, id: "venue.gil.ledger"))
        {
            openGilLedger();
        }

        var top = MathF.Min(world.Max.Y, rect.Max.Y + Metrics.Space.Sm * scale);
        return new Rect(new Vector2(world.Min.X, top), world.Max);
    }

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
        if (view.Paused)
        {
            PhaseRibbon.Draw(drawList, frame.Layout.Ribbon, Loc.T(L.Tables.PausedByHost), 0, 0,
                view.Snapshot.Occupancy, CasinoColors.InkMuted, scale);
            return;
        }

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
        if (venue.Tables.TakeKnockAdmitted(roomId))
        {
            venue.Rooms.Enter(roomId);
            venue.Tables.RefreshCard(roomId);
            return;
        }

        var knock = venue.Tables.KnockFor(roomId, reason);
        venue.Tables.EnsureKnockFresh(roomId);
        var action = Tables.TableDoorCard.Draw(drawList, ui, reason, knock, L.Venue.RoomClosedTitle,
            L.Venue.RoomClosedHint, safe, scale, Currency == CasinoCurrencies.Gil);
        if (action == Tables.TableDoorAction.Knock)
        {
            venue.Tables.Knock(roomId);
            return;
        }

        if (action == Tables.TableDoorAction.GilLedger)
        {
            openGilLedger();
            return;
        }

        if (action == Tables.TableDoorAction.Leave)
        {
            leaveRoom();
        }
    }
}
