namespace Aetherphone.Core.Casino;

internal sealed class CasinoPreferences
{
    private readonly Configuration configuration;
    private readonly object gate = new();

    public CasinoPreferences(Configuration configuration)
    {
        this.configuration = configuration;
    }

    public long MachineBet(string machineId)
    {
        return configuration.CasinoMachineBets.TryGetValue(machineId, out var bet) ? bet : 0;
    }

    public void RememberMachineBet(string machineId, long bet)
    {
        if (machineId.Length == 0 || bet <= 0 || MachineBet(machineId) == bet)
        {
            return;
        }

        lock (gate)
        {
            var next = new Dictionary<string, long>(configuration.CasinoMachineBets, StringComparer.Ordinal)
            {
                [machineId] = bet,
            };
            configuration.CasinoMachineBets = next;
        }

        configuration.Save();
    }

    public int PlinkoRows => PlinkoRules.RowsIndex(configuration.CasinoPlinkoRows) >= 0
        ? configuration.CasinoPlinkoRows
        : PlinkoRules.DefaultRows;

    public int PlinkoRisk => configuration.CasinoPlinkoRisk is >= 0 and < PlinkoRules.RiskCount
        ? configuration.CasinoPlinkoRisk
        : PlinkoRules.DefaultRisk;

    public void RememberPlinko(int rows, int risk)
    {
        if (rows == configuration.CasinoPlinkoRows && risk == configuration.CasinoPlinkoRisk)
        {
            return;
        }

        configuration.CasinoPlinkoRows = rows;
        configuration.CasinoPlinkoRisk = risk;
        configuration.Save();
    }

    public bool Instant(string gameId) => configuration.CasinoInstantGames.Contains(gameId);

    public void RememberInstant(string gameId, bool instant)
    {
        if (gameId.Length == 0 || Instant(gameId) == instant)
        {
            return;
        }

        lock (gate)
        {
            var next = new HashSet<string>(configuration.CasinoInstantGames, StringComparer.Ordinal);
            if (instant)
            {
                next.Add(gameId);
            }
            else
            {
                next.Remove(gameId);
            }

            configuration.CasinoInstantGames = next;
        }

        configuration.Save();
    }

    public bool IntroSeen => configuration.CasinoStripIntroSeen;

    public void MarkIntroSeen()
    {
        if (configuration.CasinoStripIntroSeen)
        {
            return;
        }

        configuration.CasinoStripIntroSeen = true;
        configuration.Save();
    }
}
