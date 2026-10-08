using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Venue;

internal sealed class DiceTableRoom
{
    private const float TileShare = 0.46f;
    private const float TileWidthShare = 0.62f;
    private const float LogTopGap = 14f;
    private const long MeasureMoment = 46_800_000;
    private const string Tumbling = "...";

    private readonly CasinoTextCache texts;
    private readonly RollLabels labels = new();
    private readonly DiceTablePlayback playback = new();
    private readonly VenueCheer cheer = new();
    private readonly Action<VenueActDraft> act;

    private int lastTumbleStep = -1;
    private long landedSeq;

    public DiceTableRoom(CasinoTextCache texts, Action<VenueActDraft> act)
    {
        this.texts = texts;
        this.act = act;
    }

    public DiceTablePlayback Playback => playback;

    public void Reset()
    {
        playback.Reset();
        cheer.Clear();
        lastTumbleStep = -1;
        landedSeq = 0;
    }

    public void OnActAnswer(CasinoVenueActDto answer)
    {
        if (!answer.Granted)
        {
            playback.CancelMine();
        }
    }

    public void Draw(in VenueRoomFrame room, Rect deck)
    {
        var board = room.View.Dice;
        playback.Update(board, room.Frame.DeltaSeconds, room.Frame.SnapToTruth || room.Frame.Instant);
        if (board is null)
        {
            return;
        }

        var scale = room.Scale;
        var drawList = room.DrawList;
        var world = room.World;
        var top = VenueArt.DrawSign(drawList, CasinoSign.Dice, world, room.Frame.Phase, scale);
        var round = board.Round;
        var remaining = round is null ? 0 : Math.Max(0, round.EndsAtUnixMs - room.NowUnixMs);
        var state = round is not null && !round.Closed
            ? texts.Duration(L.Venue.RoundEndsIn, (int)((remaining + 999) / 1000))
            : texts.Number(L.Venue.DiceRibbon, board.Sides);
        var stateHeight = Typography.LineHeight(TextStyles.Title2);
        VenueArt.StateLine(drawList, state, new Vector2(world.Center.X, top + stateHeight * 0.5f), world.Width,
            CasinoColors.InkTitle, scale);
        top += stateHeight + VenueArt.SignGap * scale;
        var tileSide = MathF.Min(world.Width * TileWidthShare, (world.Max.Y - top) * TileShare);
        var tile = new Rect(new Vector2(world.Center.X - tileSide * 0.5f, top),
            new Vector2(world.Center.X + tileSide * 0.5f, top + tileSide));
        DrawHero(room, tile);
        var cursor = tile.Max.Y + VenueArt.SignGap * scale;
        cursor = DrawRoundLine(room, board, cursor);
        DrawLog(room, board, new Rect(new Vector2(world.Min.X, cursor + LogTopGap * scale), world.Max));
        CelebrateRound(room, board, tile.Center);
        cheer.Draw(drawList, room.Frame.Safe, room.Frame.DeltaSeconds);
        DrawDeck(room, board, round, deck);
    }

    private void DrawHero(in VenueRoomFrame room, Rect tile)
    {
        var scale = room.Scale;
        var drawList = room.DrawList;
        var tumbling = playback.Tumbling;
        if (tumbling)
        {
            var step = VenueTumble.StepAt(playback.AwaitingMine ? room.Frame.Phase : playback.HeroSeconds);
            if (step != lastTumbleStep)
            {
                lastTumbleStep = step;
                CasinoSfx.Pitched(UiSound.PegTick, step % 8);
            }
        }
        else if (playback.HeroSeq > 0 && playback.HeroSeq != landedSeq)
        {
            if (landedSeq != 0)
            {
                CasinoSfx.Play(UiSound.ReelStop);
            }

            landedSeq = playback.HeroSeq;
        }

        var number = playback.HeroSeq == 0 && !tumbling ? "?" : labels.Value(playback.DisplayValue);
        var glow = tumbling ? 0f : MathF.Max(0f, 1f - playback.HeroSeconds / 3f);
        VenueArt.DrawNumberTile(drawList, tile, number, !tumbling, false, glow, room.Frame.Phase, scale);
        var caption = tumbling
            ? Loc.T(L.Venue.Rolling)
            : playback.HeroSeq == 0
                ? Loc.T(L.Venue.LogEmpty)
                : texts.NamedNumber(L.Venue.RolledOf, playback.HeroName, playback.HeroBound);
        var lineTop = tile.Max.Y + VenueArt.LineGap * scale;
        VenueArt.Status(drawList, caption,
            new Vector2(tile.Center.X, lineTop + Typography.LineHeight(TextStyles.Subheadline) * 0.5f),
            room.World.Width);
    }

    private float DrawRoundLine(in VenueRoomFrame room, CasinoDiceTableStateDto board, float top)
    {
        var lineHeight = Typography.LineHeight(TextStyles.Subheadline);
        top += lineHeight + VenueArt.LineGap * room.Scale;
        var text = RoundText(board);
        if (text.Length == 0)
        {
            return top;
        }

        VenueArt.Line(room.DrawList, text, new Vector2(room.World.Center.X, top + lineHeight * 0.5f),
            room.World.Width, CasinoColors.Money, TextStyles.SubheadlineEmphasized);
        return top + lineHeight;
    }

    private string RoundText(CasinoDiceTableStateDto board)
    {
        var round = board.Round;
        if (round is not null && !round.Closed)
        {
            var leader = DiceRounds.Leader(board.Rolls, round);
            return leader is null
                ? Loc.T(L.Venue.RoundNoRolls)
                : texts.NamedNumber(L.Venue.RoundLeader, leader.DisplayName, leader.Value);
        }

        var last = board.LastRound;
        if (last is null || !last.Closed)
        {
            return string.Empty;
        }

        return last.WinnerUserId.Length == 0
            ? Loc.T(L.Venue.RoundNoRolls)
            : texts.NamedNumber(L.Venue.RoundWinner, last.WinnerName, last.WinningValue);
    }

    private void DrawLog(in VenueRoomFrame room, CasinoDiceTableStateDto board, Rect area)
    {
        var rolls = board.Rolls;
        if (rolls is null || rolls.Length == 0 || area.Height <= 0f)
        {
            return;
        }

        var scale = room.Scale;
        var rowHeight = MathF.Max(VenueArt.RowHeight * scale, Typography.LineHeight(TextStyles.Subheadline) + 6f * scale);
        var count = Math.Min(rolls.Length, (int)(area.Height / rowHeight));
        var drawList = room.DrawList;
        var timeWidth = Typography.Measure(labels.Time(-1, MeasureMoment), TextStyles.Footnote).X + 64f * scale;
        for (var index = 0; index < count; index++)
        {
            var roll = rolls[index];
            var rowTop = area.Min.Y + index * rowHeight;
            var mine = room.IsMe(roll.UserId);
            var fresh = index == 0 && playback.Tumbling && roll.Seq == playback.HeroSeq;
            var alpha = 1f - index * 0.6f / Math.Max(1, count);
            var center = rowTop + rowHeight * 0.5f;
            var time = labels.Time(roll.Seq, roll.AtUnixMs);
            Typography.Draw(drawList,
                new Vector2(area.Min.X, center - Typography.LineHeight(TextStyles.Footnote) * 0.5f), time,
                CasinoColors.InkBody with { W = alpha }, TextStyles.Footnote);
            var value = fresh ? Tumbling : labels.Value(roll.Value);
            var valueSize = Typography.Measure(value, TextStyles.Headline);
            Typography.Draw(drawList, new Vector2(area.Max.X - valueSize.X, center - valueSize.Y * 0.5f), value,
                (mine ? CasinoColors.Money : CasinoColors.InkTitle) with { W = alpha }, TextStyles.Headline);
            var nameLeft = area.Min.X + MathF.Min(timeWidth, area.Width * 0.3f);
            var nameWidth = area.Max.X - valueSize.X - 8f * scale - nameLeft;
            VenueArt.LeftLine(drawList, roll.DisplayName,
                new Vector2(nameLeft, center - Typography.LineHeight(TextStyles.Subheadline) * 0.5f),
                MathF.Max(1f, nameWidth), (mine ? CasinoColors.MoneyHighlight : CasinoColors.InkTitle) with { W = alpha },
                TextStyles.Subheadline);
        }
    }

    private void CelebrateRound(in VenueRoomFrame room, CasinoDiceTableStateDto board, Vector2 origin)
    {
        if (!playback.TakeRoundClose(out _))
        {
            return;
        }

        var last = board.LastRound;
        if (last is null || !room.IsMe(last.WinnerUserId))
        {
            return;
        }

        cheer.Show(room.Stage, Loc.T(L.Venue.YouWonRound), origin, room.Frame.Instant);
    }

    private void DrawDeck(in VenueRoomFrame room, CasinoDiceTableStateDto board, CasinoDiceRoundDto? round,
        Rect deck)
    {
        var scale = room.Scale;
        var canOpenRound = room.Hosting && board.HighestWins && (round is null || round.Closed);
        var secondaryLabel = canOpenRound ? Loc.T(L.Venue.RoundOpen) : string.Empty;
        var secondaryWidth = VenueArt.SecondaryWidth(secondaryLabel);
        var caption = round is not null && !round.Closed
            ? Loc.T(L.Venue.RoundFirstRollCounts)
            : Loc.T(L.Venue.DiceHint);
        VenueArt.DeckCaption(room.DrawList, deck, caption, scale);
        var primary = VenueArt.DeckPrimary(deck, secondaryWidth, scale);
        var label = texts.Number(L.Venue.RollAction, board.Sides);
        if (Button.Draw(room.DrawList, primary, label, room.Ui.Ink, ButtonStyle.Prominent,
                enabled: room.Enabled && !playback.AwaitingMine, id: "venue.dice.roll")
            || (room.Enabled && !playback.AwaitingMine && room.Stage.RepeatPressed()))
        {
            playback.BeginMine(board.Sides);
            CasinoSfx.Play(UiSound.ChipSlide);
            act(new VenueActDraft(VenueActions.Roll));
        }

        if (!canOpenRound)
        {
            return;
        }

        if (Button.Draw(room.DrawList, VenueArt.DeckSecondary(deck, secondaryWidth, scale), secondaryLabel,
                room.Ui.Ink, ButtonStyle.Tinted, enabled: room.Enabled, id: "venue.dice.round"))
        {
            act(new VenueActDraft(VenueActions.RoundOpen));
        }
    }
}

internal static class DiceRounds
{
    public static CasinoVenueRollDto? Leader(CasinoVenueRollDto[]? rolls, CasinoDiceRoundDto round)
    {
        if (rolls is null)
        {
            return null;
        }

        CasinoVenueRollDto? best = null;
        for (var index = rolls.Length - 1; index >= 0; index--)
        {
            var roll = rolls[index];
            if (roll.Seq <= round.OpenedSeq || !FirstFor(rolls, index, round.OpenedSeq))
            {
                continue;
            }

            if (best is null || roll.Value > best.Value)
            {
                best = roll;
            }
        }

        return best;
    }

    private static bool FirstFor(CasinoVenueRollDto[] rolls, int index, long openedSeq)
    {
        var roll = rolls[index];
        for (var older = index + 1; older < rolls.Length; older++)
        {
            if (rolls[older].Seq > openedSeq
                && string.Equals(rolls[older].UserId, roll.UserId, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
