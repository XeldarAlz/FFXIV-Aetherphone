using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Venue;

internal sealed class RaffleRoom
{
    private const float WheelShare = 0.62f;
    private const int SeedShown = 16;
    private const string StateMarquee = "venue.raffle.state";
    private const string EntrantsMarquee = "venue.raffle.entrants";
    private const string PrizeMarquee = "venue.raffle.prize";
    private const string SeedMarquee = "venue.raffle.seed";

    private readonly CasinoTextCache texts;
    private readonly RafflePlayback playback = new();
    private readonly VenueCheer cheer = new();
    private readonly RaffleComposer composer;
    private readonly Action<VenueActDraft> act;

    private int shownWinners = -1;
    private string seedText = string.Empty;
    private string seedSource = string.Empty;
    private LanguageInfo? seedLanguage;

    public RaffleRoom(CasinoTextCache texts, Action<VenueActDraft> act)
    {
        this.texts = texts;
        this.act = act;
        composer = new RaffleComposer(act);
    }

    public RafflePlayback Playback => playback;

    public void Reset()
    {
        playback.Reset();
        cheer.Clear();
        composer.Close();
        shownWinners = -1;
    }

    public void Gate()
    {
        composer.Gate();
    }

    public void DrawOverlay(Rect screen, AppSkin ui)
    {
        composer.Draw(screen, ui);
    }

    public void Draw(in VenueRoomFrame room, Rect deck)
    {
        var board = room.View.Raffle;
        playback.Update(board, room.Frame.DeltaSeconds, room.Frame.SnapToTruth || room.Frame.Instant);
        if (board is null)
        {
            return;
        }

        var scale = room.Scale;
        var drawList = room.DrawList;
        var world = room.World;
        var live = board.Raffle;
        var shown = live ?? playback.Replaying ?? board.Last;
        var top = VenueArt.DrawSign(drawList, CasinoSign.Raffle, world, room.Frame.Phase, scale);
        var stateHeight = Typography.LineHeight(TextStyles.Title2);
        StageText.State(drawList, new Vector2(world.Center.X, top + stateHeight * 0.5f), StateText(shown),
            world.Width, StateMarquee);
        top += stateHeight + VenueArt.LineGap * scale;
        top = DrawInfo(room, live, shown, top);
        var radius = MathF.Max(1f, MathF.Min(world.Width * 0.5f - 14f * scale, (world.Max.Y - top) * WheelShare * 0.5f));
        var center = new Vector2(world.Center.X, top + radius + 18f * scale);
        RaffleWheelArt.Draw(drawList, center, radius, playback,
            shown?.Entrants ?? Array.Empty<CasinoRaffleEntrantDto>(), room.Me, room.Frame.Phase, scale);
        TickLanding(room);
        DrawResults(room, shown, center.Y + radius + 22f * scale);
        Celebrate(room, center);
        cheer.Draw(drawList, room.Frame.Safe, room.Frame.DeltaSeconds);
        DrawDeck(room, board, live, deck);
    }

    private string StateText(CasinoRaffleDto? shown)
    {
        if (shown is null)
        {
            return Loc.T(L.Venue.NoRaffle);
        }

        if (!shown.Drawn)
        {
            return shown.Title;
        }

        return playback.Stage == RaffleStage.Spinning ? Loc.T(L.Venue.Drawing) : shown.Title;
    }

    private float DrawInfo(in VenueRoomFrame room, CasinoRaffleDto? live, CasinoRaffleDto? shown, float top)
    {
        var scale = room.Scale;
        var lineHeight = Typography.LineHeight(TextStyles.Subheadline);
        if (shown is null)
        {
            return top;
        }

        var entrants = shown.Entrants?.Length ?? 0;
        StageText.Label(room.DrawList, new Vector2(room.World.Center.X, top + lineHeight * 0.5f),
            texts.Counts(L.Venue.Entrants, entrants, shown.Tickets), room.World.Width, EntrantsMarquee,
            TextStyles.Subheadline, false);
        top += lineHeight + VenueArt.LineGap * scale;
        if (live is not null)
        {
            var seconds = (int)((Math.Max(0, live.EndsAtUnixMs - room.NowUnixMs) + 999) / 1000);
            VenueArt.Line(room.DrawList, texts.Duration(L.Venue.DrawsIn, seconds),
                new Vector2(room.World.Center.X, top + lineHeight * 0.5f), room.World.Width, CasinoColors.Money,
                TextStyles.SubheadlineEmphasized);
            top += lineHeight + VenueArt.LineGap * scale;
        }

        if (room.Gil && shown.Prize > 0)
        {
            var amountHeight = Typography.LineHeight(TextStyles.Title3);
            StageText.Amount(room.DrawList, new Vector2(room.World.Center.X, top + amountHeight * 0.5f),
                texts.Number(L.Venue.PrizeGil, shown.Prize), room.World.Width, PrizeMarquee);
            top += amountHeight + VenueArt.LineGap * scale;
        }

        return top;
    }

    private void TickLanding(in VenueRoomFrame room)
    {
        if (shownWinners == playback.WinnersShown)
        {
            return;
        }

        var landedOne = shownWinners >= 0 && playback.WinnersShown > shownWinners;
        shownWinners = playback.WinnersShown;
        if (landedOne && !room.Frame.Instant)
        {
            CasinoSfx.Play(UiSound.ReelStop);
        }
    }

    private void DrawResults(in VenueRoomFrame room, CasinoRaffleDto? shown, float top)
    {
        if (shown is null || !shown.Drawn)
        {
            DrawMyTickets(room, shown, top);
            return;
        }

        var entrants = shown.Entrants ?? Array.Empty<CasinoRaffleEntrantDto>();
        var lineHeight = Typography.LineHeight(TextStyles.Headline);
        var count = Math.Min(playback.WinnersShown, Math.Max(0, (int)((room.World.Max.Y - top) / lineHeight) - 1));
        for (var index = 0; index < count; index++)
        {
            var entrant = playback.WinnerEntrant(index);
            if (entrant < 0 || entrant >= entrants.Length)
            {
                continue;
            }

            var name = entrants[entrant].DisplayName;
            var mine = room.IsMe(entrants[entrant].UserId);
            VenueArt.Line(room.DrawList, texts.Named(L.Venue.WinnerLine, name),
                new Vector2(room.World.Center.X, top + lineHeight * (index + 0.5f)), room.World.Width,
                mine ? CasinoColors.MoneyHighlight : CasinoColors.Money, TextStyles.Headline);
        }

        if (playback.Stage != RaffleStage.Done || shown.Seed.Length == 0)
        {
            return;
        }

        var seedTop = top + lineHeight * count + VenueArt.LineGap * room.Scale;
        var footnote = Typography.LineHeight(TextStyles.Footnote);
        if (seedTop + footnote > room.World.Max.Y)
        {
            return;
        }

        StageText.Label(room.DrawList, new Vector2(room.World.Center.X, seedTop + footnote * 0.5f),
            SeedText(shown.Seed), room.World.Width, SeedMarquee, TextStyles.Footnote, false);
    }

    private void DrawMyTickets(in VenueRoomFrame room, CasinoRaffleDto? shown, float top)
    {
        if (shown is null)
        {
            return;
        }

        var held = MyTickets(shown, room.Me);
        var lineHeight = Typography.LineHeight(TextStyles.Headline);
        VenueArt.Line(room.DrawList, texts.Counts(L.Venue.TicketsHeld, held, shown.TicketsPerPerson),
            new Vector2(room.World.Center.X, top + lineHeight * 0.5f), room.World.Width,
            held > 0 ? CasinoColors.Money : CasinoColors.InkTitle, TextStyles.Headline);
    }

    private string SeedText(string seed)
    {
        if (string.Equals(seed, seedSource, StringComparison.Ordinal) && ReferenceEquals(seedLanguage, Loc.Current))
        {
            return seedText;
        }

        seedSource = seed;
        seedLanguage = Loc.Current;
        seedText = Loc.T(L.Venue.SeedLine, seed.Length > SeedShown ? seed[..SeedShown] : seed);
        return seedText;
    }

    private void Celebrate(in VenueRoomFrame room, Vector2 origin)
    {
        if (!playback.TakeFinish(out var finished) || finished is null)
        {
            return;
        }

        var winners = finished.WinnersDrawn;
        if (winners is null)
        {
            return;
        }

        for (var index = 0; index < winners.Length; index++)
        {
            if (!room.IsMe(winners[index].UserId))
            {
                continue;
            }

            var message = room.Gil && finished.Prize > 0
                ? texts.Number(L.Venue.YouWinGil, finished.Prize)
                : Loc.T(L.Venue.YouWonRaffle);
            cheer.Show(room.Stage, message, origin, room.Frame.Instant);
            return;
        }
    }

    internal static int MyTickets(CasinoRaffleDto raffle, string me)
    {
        var entrants = raffle.Entrants;
        if (entrants is null || me.Length == 0)
        {
            return 0;
        }

        var index = RafflePlayback.EntrantIndexOf(entrants, me);
        return index < 0 ? 0 : entrants[index].Tickets;
    }

    private void DrawDeck(in VenueRoomFrame room, CasinoRaffleStateDto board, CasinoRaffleDto? live, Rect deck)
    {
        var scale = room.Scale;
        var drawList = room.DrawList;
        if (live is null)
        {
            DrawIdleDeck(room, board, deck);
            return;
        }

        var held = MyTickets(live, room.Me);
        var maxed = held >= live.TicketsPerPerson;
        var caption = room.Gil && live.TicketPrice > 0
            ? texts.Number(L.Venue.TicketPriceGil, live.TicketPrice)
            : Loc.T(L.Venue.RaffleFree);
        VenueArt.DeckCaption(drawList, deck, caption, scale);
        if (room.Hosting && room.Stage.SecondaryAction(Loc.T(L.Venue.DrawNow), room.Enabled, room.Ui.Ink))
        {
            act(new VenueActDraft(VenueActions.RaffleDraw));
        }

        var label = maxed ? Loc.T(L.Venue.TicketsMaxed) : Loc.T(L.Venue.TakeTicket);
        if (room.Stage.PrimaryAction(label, room.Enabled && !maxed, room.Ui.Ink) && !maxed)
        {
            CasinoSfx.Play(UiSound.Daub);
            act(new VenueActDraft(VenueActions.RaffleTicket, 1));
        }
    }

    private void DrawIdleDeck(in VenueRoomFrame room, CasinoRaffleStateDto board, Rect deck)
    {
        var scale = room.Scale;
        var drawList = room.DrawList;
        var last = board.Last;
        var canReplay = last is not null && playback.CanReplay(board);
        var replayLabel = canReplay ? Loc.T(L.Venue.ReplayDraw) : string.Empty;
        VenueArt.DeckCaption(drawList, deck, Loc.T(room.Hosting ? L.Venue.RaffleHostHint : L.Venue.RaffleWaitHint),
            scale);
        if (room.Hosting)
        {
            if (canReplay && room.Stage.SecondaryAction(replayLabel, true, room.Ui.Ink))
            {
                playback.Replay(last!);
            }

            if (room.Stage.PrimaryAction(Loc.T(L.Venue.RaffleOpenAction), room.Enabled, room.Ui.Ink))
            {
                composer.Open(room.Gil);
            }

            return;
        }

        var primaryLabel = canReplay ? replayLabel : Loc.T(L.Venue.WaitingHost);
        if (room.Stage.PrimaryAction(primaryLabel, canReplay, room.Ui.Ink) && canReplay)
        {
            playback.Replay(last!);
        }
    }
}
