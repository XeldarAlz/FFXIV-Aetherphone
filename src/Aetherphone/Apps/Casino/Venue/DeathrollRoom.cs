using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Venue;

internal sealed class DeathrollRoom
{
    private const float TileShare = 0.52f;
    private const float TileWidthShare = 0.78f;
    private const int TrailShown = 6;
    private const string TrailArrow = " > ";

    private readonly CasinoTextCache texts;
    private readonly RollLabels labels = new();
    private readonly DeathrollPlayback playback = new();
    private readonly VenueCheer cheer = new();
    private readonly Action<VenueActDraft> act;

    private int lastTumbleStep = -1;
    private long trailKey = -1;
    private string trail = string.Empty;
    private long matchupKey = -1;
    private string matchup = string.Empty;
    private LanguageInfo? matchupLanguage;
    private string matchupOpponent = string.Empty;

    public DeathrollRoom(CasinoTextCache texts, Action<VenueActDraft> act)
    {
        this.texts = texts;
        this.act = act;
    }

    public DeathrollPlayback Playback => playback;

    public void Reset()
    {
        playback.Reset();
        cheer.Clear();
        lastTumbleStep = -1;
        trailKey = -1;
        trail = string.Empty;
        matchupKey = -1;
    }

    public void Draw(in VenueRoomFrame room, Rect deck)
    {
        var board = room.View.Deathroll;
        playback.Update(board, room.Frame.DeltaSeconds, room.Frame.SnapToTruth || room.Frame.Instant);
        if (board is null)
        {
            return;
        }

        var scale = room.Scale;
        var drawList = room.DrawList;
        var world = room.World;
        var duel = playback.Duel;
        var top = VenueArt.DrawSign(drawList, CasinoSign.Deathroll, world, room.Frame.Phase, scale);
        var stateHeight = Typography.LineHeight(TextStyles.Title2);
        VenueArt.StateLine(drawList, StateText(room, duel), new Vector2(world.Center.X, top + stateHeight * 0.5f),
            world.Width, CasinoColors.InkTitle, scale);
        top += stateHeight + VenueArt.LineGap * scale;
        top = DrawMatchup(room, duel, board, top);
        var tileSide = MathF.Min(world.Width * TileWidthShare, (world.Max.Y - top) * TileShare);
        var tile = new Rect(new Vector2(world.Center.X - tileSide * 0.5f, top),
            new Vector2(world.Center.X + tileSide * 0.5f, top + tileSide));
        DrawCountdown(room, tile);
        var cursor = tile.Max.Y + VenueArt.SignGap * scale;
        DrawTrail(room, duel, board, cursor);
        Settle(room, board, tile.Center);
        cheer.Draw(drawList, room.Frame.Safe, room.Frame.DeltaSeconds);
        DrawDeck(room, board, duel, deck);
    }

    private string StateText(in VenueRoomFrame room, CasinoDeathrollDuelDto? duel)
    {
        if (duel is null)
        {
            return Loc.T(L.Venue.NoDuel);
        }

        if (duel.Phase == DuelPhases.Open)
        {
            return texts.Named(L.Venue.ChallengeOpen, duel.ChallengerName);
        }

        if (duel.Phase >= DuelPhases.Finished)
        {
            if (playback.Rolling)
            {
                return Loc.T(L.Venue.Rolling);
            }

            return room.IsMe(duel.LoserUserId)
                ? Loc.T(L.Venue.YouRolledOne)
                : texts.Named(L.Venue.DuelLost, NameOf(duel, duel.LoserUserId));
        }

        if (playback.Rolling)
        {
            return Loc.T(L.Venue.Rolling);
        }

        return room.IsMe(duel.TurnUserId)
            ? Loc.T(L.Venue.YourRoll)
            : texts.Named(L.Venue.WaitingTurn, NameOf(duel, duel.TurnUserId));
    }

    private float DrawMatchup(in VenueRoomFrame room, CasinoDeathrollDuelDto? duel, CasinoDeathrollStateDto board,
        float top)
    {
        var scale = room.Scale;
        var lineHeight = Typography.LineHeight(TextStyles.Subheadline);
        var stake = duel?.Stake ?? board.Stake;
        var stakeText = room.Gil
            ? texts.Number(L.Venue.StakeGil, stake)
            : texts.Number(L.Venue.StakePractice, stake);
        var matchup = duel is null || duel.OpponentName.Length == 0
            ? texts.Number(L.Venue.StartsAt, board.StartAt)
            : Matchup(duel);
        VenueArt.Status(room.DrawList, matchup, new Vector2(room.World.Center.X, top + lineHeight * 0.5f),
            room.World.Width);
        top += lineHeight + VenueArt.LineGap * scale;
        var amountHeight = Typography.LineHeight(TextStyles.Title3);
        VenueArt.Line(room.DrawList, stakeText, new Vector2(room.World.Center.X, top + amountHeight * 0.5f),
            room.World.Width, CasinoColors.Money, TextStyles.Title3);
        return top + amountHeight + VenueArt.SignGap * scale;
    }

    private void DrawCountdown(in VenueRoomFrame room, Rect tile)
    {
        var rolling = playback.Rolling;
        if (rolling)
        {
            var step = (int)(playback.RollProgress * 20f);
            if (step != lastTumbleStep)
            {
                lastTumbleStep = step;
                CasinoSfx.Pitched(UiSound.PegTick, 8 - Math.Min(8, step / 2));
            }
        }
        else if (lastTumbleStep >= 0)
        {
            lastTumbleStep = -1;
            CasinoSfx.Play(VenueRules.Loses(playback.Shown) ? UiSound.Bust : UiSound.ReelStop);
        }

        var shown = playback.Shown;
        var losing = !rolling && VenueRules.Loses(shown) && playback.Duel is not null;
        var number = shown <= 0 ? "?" : labels.Value(shown);
        VenueArt.DrawNumberTile(room.DrawList, tile, number, !rolling, losing, rolling ? 0f : 0.35f,
            room.Frame.Phase, room.Scale);
        if (playback.Bound <= 0 || rolling)
        {
            return;
        }

        var lineHeight = Typography.LineHeight(TextStyles.Subheadline);
        VenueArt.Status(room.DrawList, texts.Number(L.Venue.OutOf, playback.Bound),
            new Vector2(tile.Center.X, tile.Max.Y + VenueArt.LineGap * room.Scale + lineHeight * 0.5f),
            room.World.Width);
    }

    private void DrawTrail(in VenueRoomFrame room, CasinoDeathrollDuelDto? duel, CasinoDeathrollStateDto board,
        float top)
    {
        var lineHeight = Typography.LineHeight(TextStyles.Subheadline);
        top += lineHeight + VenueArt.SignGap * room.Scale;
        var text = duel is null ? RecentText(room, board) : TrailText(duel);
        if (text.Length == 0)
        {
            return;
        }

        VenueArt.Line(room.DrawList, text, new Vector2(room.World.Center.X, top + lineHeight * 0.5f),
            room.World.Width, CasinoColors.InkBody, TextStyles.Subheadline);
    }

    private string TrailText(CasinoDeathrollDuelDto duel)
    {
        var rolls = duel.Rolls;
        var key = duel.Seq * 1_000 + (rolls?.Length ?? 0);
        if (key == trailKey)
        {
            return trail;
        }

        trailKey = key;
        if (rolls is null || rolls.Length == 0)
        {
            trail = string.Empty;
            return trail;
        }

        var first = Math.Max(0, rolls.Length - TrailShown);
        var builder = new System.Text.StringBuilder();
        for (var index = first; index < rolls.Length; index++)
        {
            if (builder.Length > 0)
            {
                builder.Append(TrailArrow);
            }

            builder.Append(labels.Value(rolls[index].Value));
        }

        trail = builder.ToString();
        return trail;
    }

    private string RecentText(in VenueRoomFrame room, CasinoDeathrollStateDto board)
    {
        var recent = board.Recent;
        if (recent is null || recent.Length == 0)
        {
            return string.Empty;
        }

        var last = recent[0];
        if (last.Phase != DuelPhases.Finished || last.LoserUserId.Length == 0)
        {
            return string.Empty;
        }

        var loserIsChallenger = string.Equals(last.LoserUserId, last.ChallengerUserId, StringComparison.Ordinal);
        var winner = loserIsChallenger ? last.OpponentName : last.ChallengerName;
        return texts.Named(L.Venue.LastDuel, winner);
    }

    private void Settle(in VenueRoomFrame room, CasinoDeathrollStateDto board, Vector2 origin)
    {
        if (!playback.TakeFinish(out var loser))
        {
            return;
        }

        var duel = DeathrollPlayback.ActiveDuel(board, playback.Duel?.Seq ?? 0) ?? playback.Duel;
        if (duel is null || loser.Length == 0 || room.IsMe(loser))
        {
            return;
        }

        var won = room.IsMe(duel.ChallengerUserId) || room.IsMe(duel.OpponentUserId);
        if (!won)
        {
            return;
        }

        if (room.Gil)
        {
            cheer.Show(room.Stage, texts.Number(L.Venue.YouWinGil, duel.Stake), origin, room.Frame.Instant);
            return;
        }

        room.Stage.Celebration.Celebrate(duel.Stake, duel.Stake * 2, origin, room.Frame.Instant);
    }

    private void DrawDeck(in VenueRoomFrame room, CasinoDeathrollStateDto board, CasinoDeathrollDuelDto? duel,
        Rect deck)
    {
        var scale = room.Scale;
        var drawList = room.DrawList;
        var nowMs = room.NowUnixMs;
        if (duel is null || duel.Phase >= DuelPhases.Finished)
        {
            VenueArt.DeckCaption(drawList, deck, Loc.T(L.Venue.DuelHint), scale);
            var label = room.Gil
                ? texts.Number(L.Venue.ChallengeGil, board.Stake)
                : texts.Number(L.Venue.ChallengePractice, board.Stake);
            if (Button.Draw(drawList, VenueArt.DeckPrimary(deck, 0f, scale), label, room.Ui.Ink,
                    enabled: room.Enabled && !playback.Rolling, id: "venue.duel.open"))
            {
                CasinoSfx.Play(UiSound.ChipSlide);
                act(new VenueActDraft(VenueActions.DuelOpen));
            }

            return;
        }

        var seconds = (int)((Math.Max(0, duel.TurnEndsAtUnixMs - nowMs) + 999) / 1000);
        VenueArt.DeckCaption(drawList, deck, texts.Duration(L.Venue.TimeLeft, seconds), scale);
        if (duel.Phase == DuelPhases.Open)
        {
            DrawOpenDeck(room, duel, deck);
            return;
        }

        var myTurn = room.IsMe(duel.TurnUserId);
        var rollLabel = myTurn
            ? texts.Number(L.Venue.RollAction, duel.Current)
            : texts.Named(L.Venue.WaitingTurn, NameOf(duel, duel.TurnUserId));
        if ((Button.Draw(drawList, VenueArt.DeckPrimary(deck, 0f, scale), rollLabel, room.Ui.Ink,
                 enabled: room.Enabled && myTurn && !playback.Rolling, id: "venue.duel.roll")
             || (myTurn && room.Enabled && !playback.Rolling && room.Stage.RepeatPressed())) && myTurn)
        {
            act(new VenueActDraft(VenueActions.DuelRoll));
        }
    }

    private void DrawOpenDeck(in VenueRoomFrame room, CasinoDeathrollDuelDto duel, Rect deck)
    {
        var scale = room.Scale;
        var mine = room.IsMe(duel.ChallengerUserId);
        var named = duel.OpponentUserId.Length > 0;
        var canAccept = !mine && (!named || room.IsMe(duel.OpponentUserId));
        var canCancel = mine || room.Hosting;
        var cancelLabel = canCancel ? Loc.T(L.Venue.CancelChallenge) : string.Empty;
        var cancelWidth = VenueArt.SecondaryWidth(cancelLabel);
        var primaryLabel = canAccept ? Loc.T(L.Venue.AcceptDuel) : Loc.T(L.Venue.WaitingTaker);
        if (Button.Draw(room.DrawList, VenueArt.DeckPrimary(deck, cancelWidth, scale), primaryLabel, room.Ui.Ink,
                enabled: room.Enabled && canAccept, id: "venue.duel.accept") && canAccept)
        {
            CasinoSfx.Play(UiSound.ChipSlide);
            act(new VenueActDraft(VenueActions.DuelAccept));
        }

        if (canCancel && Button.Draw(room.DrawList, VenueArt.DeckSecondary(deck, cancelWidth, scale), cancelLabel,
                room.Ui.Ink, ButtonStyle.Gray, enabled: room.Enabled, id: "venue.duel.cancel"))
        {
            act(new VenueActDraft(VenueActions.DuelCancel));
        }
    }

    private string Matchup(CasinoDeathrollDuelDto duel)
    {
        if (duel.Seq == matchupKey && ReferenceEquals(matchupLanguage, Loc.Current)
            && string.Equals(matchupOpponent, duel.OpponentName, StringComparison.Ordinal))
        {
            return matchup;
        }

        matchupKey = duel.Seq;
        matchupOpponent = duel.OpponentName;
        matchupLanguage = Loc.Current;
        matchup = Loc.T(L.Venue.DuelVs, duel.ChallengerName, duel.OpponentName);
        return matchup;
    }

    private static string NameOf(CasinoDeathrollDuelDto duel, string userId)
    {
        return string.Equals(userId, duel.OpponentUserId, StringComparison.Ordinal)
            ? duel.OpponentName
            : duel.ChallengerName;
    }
}
