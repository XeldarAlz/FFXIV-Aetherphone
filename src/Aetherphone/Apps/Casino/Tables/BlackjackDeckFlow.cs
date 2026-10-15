namespace Aetherphone.Apps.Casino.Tables;

internal enum BlackjackDeck : byte
{
    Sit,
    Dealer,
    Bank,
    Rebuy,
    Broke,
    Paused,
    Insurance,
    Bet,
    Act,
    Wait,
}

internal readonly record struct BlackjackDeckFacts(
    bool Dealing,
    bool Banking,
    bool Bought,
    bool OnBoard,
    bool HoldsSeat,
    bool CanRebuy,
    bool Broke,
    bool Paused,
    bool InsuranceOffered,
    bool CanBet,
    bool Committed,
    bool CanAct);

internal static class BlackjackDeckFlow
{
    public static BlackjackDeck Pick(in BlackjackDeckFacts facts)
    {
        if (facts.Dealing)
        {
            return BlackjackDeck.Dealer;
        }

        if (facts.Banking)
        {
            return BlackjackDeck.Bank;
        }

        if (!facts.Bought || (!facts.OnBoard && !facts.HoldsSeat))
        {
            return BlackjackDeck.Sit;
        }

        if (facts.OnBoard && facts.CanRebuy)
        {
            return BlackjackDeck.Rebuy;
        }

        if (facts.OnBoard && facts.Broke)
        {
            return BlackjackDeck.Broke;
        }

        if (facts.OnBoard && facts.Paused)
        {
            return BlackjackDeck.Paused;
        }

        if (facts.OnBoard && facts.InsuranceOffered)
        {
            return BlackjackDeck.Insurance;
        }

        if (facts.CanBet && !facts.Committed)
        {
            return BlackjackDeck.Bet;
        }

        return facts.OnBoard && facts.CanAct ? BlackjackDeck.Act : BlackjackDeck.Wait;
    }
}
