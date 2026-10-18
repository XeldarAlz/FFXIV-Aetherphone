using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

internal readonly record struct CasinoLaunchAnswer(string TableId, CasinoTableRowDto? Table, string Reason);

internal sealed class CasinoLaunchResolve
{
    private readonly object gate = new();
    private int ticket;
    private CasinoLaunchAnswer? answer;

    public int Begin()
    {
        lock (gate)
        {
            answer = null;
            ticket++;
            return ticket;
        }
    }

    public void Complete(int asked, CasinoLaunchAnswer value)
    {
        lock (gate)
        {
            if (asked != ticket)
            {
                return;
            }

            answer = value;
        }
    }

    public void Forget()
    {
        lock (gate)
        {
            answer = null;
            ticket++;
        }
    }

    public bool TryTake(out CasinoLaunchAnswer value)
    {
        lock (gate)
        {
            if (answer is not { } held)
            {
                value = default;
                return false;
            }

            answer = null;
            value = held;
            return true;
        }
    }
}
