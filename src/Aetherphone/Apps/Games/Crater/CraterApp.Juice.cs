using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;

namespace Aetherphone.Apps.Games.Crater;

internal sealed partial class CraterApp
{
    private void Drain(in GameContext context, bool quiet)
    {
        while (board.TryTakeEvent(out var entry))
        {
            switch (entry.Kind)
            {
                case CraterEventKind.TurnStarted:
                    OnTurnStarted(context, entry, quiet);
                    break;
                case CraterEventKind.MatchOver:
                    OnMatchOver(context, entry, quiet);
                    break;
                default:
                    if (entry.Kind == CraterEventKind.Launched && !board.IsBot(entry.Team))
                    {
                        firstShotTaken = true;
                    }

                    juice.Play(entry, ref camera, context.Fx, CraterArt.Material(board.Style), quiet);
                    break;
            }
        }
    }

    private void OnTurnStarted(in GameContext context, in CraterEvent entry, bool quiet)
    {
        if (quiet)
        {
            return;
        }

        juice.QueueBanner(labels.TurnLine(entry.Team), GameSeats.Color(entry.Team), CraterJuice.BannerSeconds);
        UiFeedback.Play(UiSound.GamePiece);
        if (hotSeat)
        {
            context.Session.Handoff(entry.Team);
        }
    }

    private void OnMatchOver(in GameContext context, in CraterEvent entry, bool quiet)
    {
        if (quiet)
        {
            return;
        }

        var winner = entry.Team;
        context.Fx.SlowMo(0.35f, 0.7f);
        context.Fx.Sweep();
        if (winner == CraterBoard.NoTeam)
        {
            juice.QueueBanner(Loc.T(L.Games.Draw), CraterJuice.Flash, CraterJuice.WinSeconds);
            UiFeedback.Play(UiSound.GameWrong);
            return;
        }

        var color = GameSeats.Color(winner);
        juice.QueueBanner(labels.WinLine(winner), color, CraterJuice.WinSeconds);
        juice.Celebrate(board.Moogles, winner, color);
        var won = hotSeat || winner == 0;
        UiFeedback.Play(won ? UiSound.GameClear : UiSound.GameWrong);
    }
}
