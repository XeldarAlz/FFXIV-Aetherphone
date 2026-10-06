namespace Aetherphone.Apps.Games.Framework.World;

internal readonly struct EnemyBudget
{
    public const int MaxKinds = 256;

    public readonly int BaseBudget;
    public readonly int BudgetPerWave;
    public readonly float WaveSeconds;
    private readonly int[] costs;
    private readonly int[] unlockWaves;

    public EnemyBudget(int[] costs, int[] unlockWaves, int baseBudget, int budgetPerWave, float waveSeconds)
    {
        ArgumentNullException.ThrowIfNull(costs);
        ArgumentNullException.ThrowIfNull(unlockWaves);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(costs.Length, MaxKinds);
        if (unlockWaves.Length != costs.Length)
        {
            throw new ArgumentException("Every enemy kind needs a cost and an unlock wave.", nameof(unlockWaves));
        }

        this.costs = costs;
        this.unlockWaves = unlockWaves;
        BaseBudget = baseBudget;
        BudgetPerWave = budgetPerWave;
        WaveSeconds = waveSeconds;
    }

    public int KindCount => costs?.Length ?? 0;

    public int Cost(int kind) => (uint)kind < (uint)KindCount ? costs[kind] : 0;

    public bool Available(int kind, int wave) => Cost(kind) > 0 && wave >= unlockWaves[kind];

    public int BudgetFor(int wave) => BaseBudget + BudgetPerWave * Math.Max(0, wave - 1);
}
