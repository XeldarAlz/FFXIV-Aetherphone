using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Apps.Casino.Venue;

internal enum TournamentEvent : byte
{
    None,
    Eliminated,
    Won,
}

internal sealed class TournamentPlayback
{
    public const float NoticeSeconds = 2.4f;

    private readonly HashSet<string> eliminated = new(StringComparer.Ordinal);

    private bool primed;
    private bool live;
    private string winnerUserId = string.Empty;
    private string noticeName = string.Empty;
    private float noticeSeconds = NoticeSeconds;
    private TournamentEvent pending;

    public bool Live => live;

    public string WinnerUserId => winnerUserId;

    public string NoticeName => noticeName;

    public bool NoticeShowing => noticeSeconds < NoticeSeconds && noticeName.Length > 0;

    public float NoticeProgress => Math.Clamp(noticeSeconds / NoticeSeconds, 0f, 1f);

    public void Reset()
    {
        eliminated.Clear();
        primed = false;
        live = false;
        winnerUserId = string.Empty;
        noticeName = string.Empty;
        noticeSeconds = NoticeSeconds;
        pending = TournamentEvent.None;
    }

    public void Update(CasinoBlackjackTournamentDto? tournament, float deltaSeconds, bool snap)
    {
        noticeSeconds += deltaSeconds;
        if (tournament is null)
        {
            live = false;
            return;
        }

        var standings = tournament.Standings ?? Array.Empty<CasinoBlackjackStandingDto>();
        if (!primed || (tournament.Live && !live && tournament.HandsPlayed == 0))
        {
            primed = true;
            eliminated.Clear();
            for (var index = 0; index < standings.Length; index++)
            {
                if (standings[index].Eliminated)
                {
                    eliminated.Add(standings[index].UserId);
                }
            }

            live = tournament.Live;
            winnerUserId = tournament.WinnerUserId;
            return;
        }

        for (var index = 0; index < standings.Length; index++)
        {
            var standing = standings[index];
            if (!standing.Eliminated || !eliminated.Add(standing.UserId) || snap)
            {
                continue;
            }

            noticeName = standing.DisplayName;
            noticeSeconds = 0f;
            pending = TournamentEvent.Eliminated;
        }

        if (tournament.WinnerUserId.Length > 0
            && !string.Equals(tournament.WinnerUserId, winnerUserId, StringComparison.Ordinal))
        {
            winnerUserId = tournament.WinnerUserId;
            if (!snap)
            {
                noticeName = tournament.WinnerName;
                noticeSeconds = 0f;
                pending = TournamentEvent.Won;
            }
        }

        live = tournament.Live;
    }

    public TournamentEvent TakeEvent()
    {
        var taken = pending;
        pending = TournamentEvent.None;
        return taken;
    }
}
