using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

internal sealed class BookLedgerAdmission
{
    internal sealed record Attempt(string TableId, string AccountId, CasinoTableActionDto? Answer = null)
    {
        public bool IsFor(string tableId, string accountId)
        {
            return string.Equals(TableId, tableId, StringComparison.Ordinal)
                && string.Equals(AccountId, accountId, StringComparison.Ordinal);
        }
    }

    private Attempt? current;

    public static bool Required(CasinoTableRowDto? card, bool owner)
    {
        return card is { Admitted: true } && !owner && CasinoBookRules.IsBook(card.GameKind);
    }

    public CasinoTableActionDto? For(string tableId, string accountId)
    {
        var held = Volatile.Read(ref current);
        return held is not null && held.IsFor(tableId, accountId) ? held.Answer : null;
    }

    public Attempt? Begin(string tableId, string accountId, bool retry)
    {
        if (tableId.Length == 0 || accountId.Length == 0)
        {
            return null;
        }

        var held = Volatile.Read(ref current);
        if (held is not null && held.IsFor(tableId, accountId) && (!retry || held.Answer is null))
        {
            return null;
        }

        var pending = new Attempt(tableId, accountId);
        Interlocked.Exchange(ref current, pending);
        return pending;
    }

    public bool Complete(Attempt attempt, CasinoTableActionDto? answer)
    {
        var completed = attempt with { Answer = answer ?? new CasinoTableActionDto(false, CasinoReasons.Unreachable) };
        return ReferenceEquals(Interlocked.CompareExchange(ref current, completed, attempt), attempt);
    }

    public void Reset()
    {
        Interlocked.Exchange(ref current, null);
    }
}
