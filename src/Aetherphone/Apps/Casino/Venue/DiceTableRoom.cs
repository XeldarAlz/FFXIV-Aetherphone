using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Venue;

internal sealed class DiceTableRoom
{
    private const float TileShare = 0.46f;
    private const float TileWidthShare = 0.62f;
    private const float LogTopGap = 14f;
    private const float LogCaptionGap = 4f;
    private const float LogFadeFloor = 0.75f;
    private const long MeasureMoment = 46_800_000;
    private const string Tumbling = "...";
    private const string StateMarquee = "venue.dice.state";
    private const string CaptionMarquee = "venue.dice.caption";
    private const string LogChildId = "##diceLog";

    private readonly CasinoTextCache texts;
    private readonly RollLabels labels = new();
    private readonly DiceTablePlayback playback = new();
    private readonly VenueCheer cheer = new();
    private readonly Action<VenueActDraft> act;

    private int lastTumbleStep = -1;
    private long landedSeq;
    private string roundLine = string.Empty;

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
        roundLine = string.Empty;
    }

    public void OnActAnswer(CasinoVenueActDto answer)
    {
        if (!answer.Granted)
        {
            playback.CancelMine();
            return;
        }

        playback.Confirm(answer.Seq);
    }

    public void Draw(in VenueRoomFrame room, Rect deck)
    {
        var board = room.View.Dice;
        playback.Update(board, room.Me, room.Frame.DeltaSeconds, room.Frame.SnapToTruth || room.Frame.Instant);
        if (board is null)
        {
            return;
        }

        var scale = room.Scale;
        var drawList = room.DrawList;
        var world = room.World;
        var top = VenueArt.DrawSign(drawList, CasinoSign.Dice, world, room.Frame.Phase, scale);
        var round = board.Round;
        var stateHeight = Typography.LineHeight(TextStyles.Title2);
        if (round is not null && !round.Closed)
        {
            StageText.State(drawList, new Vector2(world.Center.X, top + stateHeight * 0.5f),
                Loc.T(L.Venue.HighestRollWins), world.Width, StateMarquee);
        }

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
        StageText.Label(drawList,
            new Vector2(tile.Center.X, lineTop + Typography.LineHeight(TextStyles.Subheadline) * 0.5f), caption,
            room.World.Width, CaptionMarquee, TextStyles.Subheadline, false);
    }

    private float DrawRoundLine(in VenueRoomFrame room, CasinoDiceTableStateDto board, float top)
    {
        var lineHeight = Typography.LineHeight(TextStyles.Subheadline);
        top += lineHeight + VenueArt.LineGap * room.Scale;
        if (!playback.Tumbling)
        {
            roundLine = RoundText(board);
        }

        var text = roundLine;
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
        var captionHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        Typography.Draw(room.DrawList, area.Min, Typography.FitText(Loc.T(L.Venue.RecentRolls), area.Width,
            TextStyles.FootnoteEmphasized), StageText.Strong, TextStyles.FootnoteEmphasized);
        var list = new Rect(new Vector2(area.Min.X, area.Min.Y + captionHeight + LogCaptionGap * scale), area.Max);
        if (list.Height <= 1f)
        {
            return;
        }

        var rowHeight = MathF.Max(VenueArt.RowHeight * scale, Typography.LineHeight(TextStyles.Subheadline) + 6f * scale);
        var pageRows = Math.Max(1, (int)(list.Height / rowHeight));
        var timeWidth = Typography.Measure(labels.Time(-1, MeasureMoment), TextStyles.Footnote).X + 64f * scale;
        ImGui.SetCursorScreenPos(list.Min);
        using (ScrollLayout.PushScrollbarInk(CasinoColors.InkBody))
        using (ImRaii.Child(LogChildId, list.Size, false, ImGuiWindowFlags.NoBackground))
        {
            var drawList = ImGui.GetWindowDrawList();
            var width = ScrollLayout.NativeScrollContentWidth();
            var rowSize = new Vector2(width, rowHeight);
            for (var index = 0; index < rolls.Length; index++)
            {
                if (ImGui.IsRectVisible(rowSize))
                {
                    var origin = ImGui.GetCursorScreenPos();
                    var alpha = 1f - Math.Min(index, pageRows) * (1f - LogFadeFloor) / pageRows;
                    DrawLogRow(room, drawList, rolls[index], index == 0,
                        new Rect(origin, origin + rowSize), timeWidth, alpha);
                }

                ImGui.Dummy(rowSize);
            }
        }
    }

    private void DrawLogRow(in VenueRoomFrame room, ImDrawListPtr drawList, CasinoVenueRollDto roll, bool newest,
        Rect row, float timeWidth, float alpha)
    {
        var mine = room.IsMe(roll.UserId);
        var fresh = newest && playback.Tumbling && roll.Seq == playback.HeroSeq;
        var center = row.Center.Y;
        var time = labels.Time(roll.Seq, roll.AtUnixMs);
        Typography.Draw(drawList,
            new Vector2(row.Min.X, center - Typography.LineHeight(TextStyles.Footnote) * 0.5f), time,
            StageText.Strong with { W = alpha }, TextStyles.Footnote);
        var value = fresh ? Tumbling : labels.Value(roll.Value);
        var valueSize = Typography.Measure(value, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(row.Max.X - valueSize.X, center - valueSize.Y * 0.5f), value,
            (mine ? CasinoColors.Money : CasinoColors.InkTitle) with { W = alpha }, TextStyles.Headline);
        var nameLeft = row.Min.X + MathF.Min(timeWidth, row.Width * 0.3f);
        var nameWidth = row.Max.X - valueSize.X - 8f * room.Scale - nameLeft;
        VenueArt.LeftLine(drawList, roll.DisplayName,
            new Vector2(nameLeft, center - Typography.LineHeight(TextStyles.Subheadline) * 0.5f),
            MathF.Max(1f, nameWidth), (mine ? CasinoColors.MoneyHighlight : CasinoColors.InkTitle) with { W = alpha },
            TextStyles.Subheadline);
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
        var canOpenRound = room.Hosting && board.HighestWins && (round is null || round.Closed);
        if (canOpenRound && room.Stage.SecondaryAction(Loc.T(L.Venue.RoundOpen), room.Enabled, room.Ui.Ink))
        {
            act(new VenueActDraft(VenueActions.RoundOpen));
        }

        var rolledThisRound = DiceRounds.Rolled(round, room.Me);
        var ready = room.Enabled && !playback.AwaitingMine && !rolledThisRound;
        var label = rolledThisRound ? Loc.T(L.Venue.RolledThisRound) : texts.Number(L.Venue.RollAction, board.Sides);
        if (room.Stage.PrimaryAction(label, ready, room.Ui.Ink) || (ready && room.Stage.RepeatPressed()))
        {
            playback.BeginMine(board.Sides);
            CasinoSfx.Play(UiSound.ChipSlide);
            act(new VenueActDraft(VenueActions.Roll));
        }
    }
}

internal static class DiceRounds
{
    public static bool Rolled(CasinoDiceRoundDto? round, string userId)
    {
        return round is { Closed: false, Rollers: not null } && userId.Length > 0
            && Array.IndexOf(round.Rollers, userId) >= 0;
    }

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
