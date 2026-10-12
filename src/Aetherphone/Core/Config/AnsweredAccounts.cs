namespace Aetherphone.Core.Config;

internal sealed class AnsweredAccounts
{
    private readonly Func<List<string>> accounts;
    private readonly Action save;
    private readonly object gate = new();

    public AnsweredAccounts(Func<List<string>> accounts, Action save)
    {
        this.accounts = accounts;
        this.save = save;
    }

    public bool Contains(string accountId)
    {
        if (accountId.Length == 0)
        {
            return false;
        }

        lock (gate)
        {
            return accounts().Contains(accountId);
        }
    }

    public bool Add(string accountId)
    {
        if (accountId.Length == 0)
        {
            return false;
        }

        lock (gate)
        {
            var answered = accounts();
            if (answered.Contains(accountId))
            {
                return false;
            }

            answered.Add(accountId);
        }

        save();
        return true;
    }
}
