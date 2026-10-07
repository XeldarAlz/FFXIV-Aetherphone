namespace Aetherphone.Apps.Games.LuckyDraw;

internal interface ILuckyScoreboard
{
    int SevenSeat { get; }

    LuckySeatState State(int seat);

    int Total(int seat);

    int LastRoundScore(int seat);
}
