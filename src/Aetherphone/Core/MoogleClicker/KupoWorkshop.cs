namespace Aetherphone.Core.MoogleClicker;

internal readonly struct KupoAdvance
{
    public readonly double Seconds;
    public readonly double Kupo;
    public readonly bool Away;

    public KupoAdvance(double seconds, double kupo, bool away)
    {
        Seconds = seconds;
        Kupo = kupo;
        Away = away;
    }
}

internal sealed class KupoWorkshop
{
    public const double OfflineCapSeconds = 8d * 60d * 60d;
    public const double AwayThresholdSeconds = 60d;
    public const double KupoCap = 1e300;
    public const double FrenzyMultiplier = 7d;
    public const double FrenzySeconds = 77d;
    public const double TapFrenzyMultiplier = 25d;
    public const double TapFrenzySeconds = 15d;
    public const double LumpBankShare = 0.15d;
    public const double LumpProductionSeconds = 900d;
    public const double LumpFloor = 13d;
    private const double MillisecondsPerSecond = 1000d;

    private readonly int[] owned = new int[KupoBuildings.Count];
    private ulong upgrades;

    public double Kupo { get; private set; }

    public double LedgerKupo { get; private set; }

    public double LifetimeKupo { get; private set; }

    public double TapKupo { get; private set; }

    public long Taps { get; private set; }

    public double Stamps { get; private set; }

    public int LedgerPages { get; private set; }

    public int MinionsCaught { get; private set; }

    public KupoReward Buff { get; private set; }

    public long BuffEndsUnixMilliseconds { get; private set; }

    public long LastTickUnixMilliseconds { get; private set; }

    public double AwaySeconds { get; private set; }

    public double AwayKupo { get; private set; }

    public bool AwayCapped { get; private set; }

    public bool HasAwayReport => AwayKupo > 0d;

    public ReadOnlySpan<int> OwnedCounts => owned;

    public ulong UpgradeMask => upgrades;

    public int UpgradeCount => BitOperations.PopCount(upgrades);

    public double Multiplier => KupoLedger.Multiplier(Stamps);

    public double PendingStamps => Math.Max(0d, KupoLedger.StampsFor(LifetimeKupo) - Stamps);

    public int LedgerLevel => KupoLedger.Level(Stamps);

    public double NextStampKupo => KupoLedger.KupoForStamps(Math.Max(Stamps, KupoLedger.StampsFor(LifetimeKupo)) + 1d);

    public int BuildingCount
    {
        get
        {
            var total = 0;
            for (var building = 0; building < owned.Length; building++)
            {
                total += owned[building];
            }

            return total;
        }
    }

    public double BaseKupoPerSecond
    {
        get
        {
            var total = 0d;
            for (var building = 0; building < owned.Length; building++)
            {
                total += BuildingRate(building);
            }

            return total;
        }
    }

    public int Owned(int building) => owned[building];

    public bool HasUpgrade(int upgrade) => (upgrades & KupoUpgrades.Bit(upgrade)) != 0UL;

    public bool IsUnlocked(int upgrade) => KupoUpgrades.Unlocked(upgrade, owned, LedgerKupo);

    public double UnitRate(int building) =>
        KupoBuildings.BaseRate(building) * Math.Pow(2d, KupoUpgrades.TiersOwned(upgrades, building)) * Multiplier;

    public double BuildingRate(int building) => UnitRate(building) * owned[building];

    public bool BuffActive(long nowUnixMilliseconds) =>
        Buff != KupoReward.None && nowUnixMilliseconds < BuffEndsUnixMilliseconds;

    public bool FrenzyActive(long nowUnixMilliseconds) =>
        Buff == KupoReward.Frenzy && nowUnixMilliseconds < BuffEndsUnixMilliseconds;

    public bool TapFrenzyActive(long nowUnixMilliseconds) =>
        Buff == KupoReward.TapFrenzy && nowUnixMilliseconds < BuffEndsUnixMilliseconds;

    public double BuffSecondsLeft(long nowUnixMilliseconds) =>
        BuffActive(nowUnixMilliseconds)
            ? (BuffEndsUnixMilliseconds - nowUnixMilliseconds) / MillisecondsPerSecond
            : 0d;

    public double KupoPerSecond(long nowUnixMilliseconds) =>
        BaseKupoPerSecond * (FrenzyActive(nowUnixMilliseconds) ? FrenzyMultiplier : 1d);

    public double TapValue(long nowUnixMilliseconds)
    {
        var doublings = 0;
        var share = 0d;
        for (var tapIndex = 0; tapIndex < KupoUpgrades.TapUpgrades; tapIndex++)
        {
            if (!HasUpgrade(KupoUpgrades.ForTap(tapIndex)))
            {
                continue;
            }

            if (tapIndex < KupoUpgrades.TapDoublings)
            {
                doublings++;
            }

            share += KupoUpgrades.TapShare(tapIndex);
        }

        var value = Math.Pow(2d, doublings) * Multiplier + KupoPerSecond(nowUnixMilliseconds) * share;
        return value * (TapFrenzyActive(nowUnixMilliseconds) ? TapFrenzyMultiplier : 1d);
    }

    public KupoAdvance Advance(long nowUnixMilliseconds)
    {
        if (LastTickUnixMilliseconds <= 0L)
        {
            LastTickUnixMilliseconds = nowUnixMilliseconds;
            return default;
        }

        var elapsedMilliseconds = nowUnixMilliseconds - LastTickUnixMilliseconds;
        if (elapsedMilliseconds <= 0L)
        {
            LastTickUnixMilliseconds = Math.Min(LastTickUnixMilliseconds, nowUnixMilliseconds);
            return default;
        }

        var elapsed = elapsedMilliseconds / MillisecondsPerSecond;
        var credited = Math.Min(elapsed, OfflineCapSeconds);
        var rate = BaseKupoPerSecond;
        var frenzySeconds = Buff == KupoReward.Frenzy
            ? Math.Clamp((BuffEndsUnixMilliseconds - LastTickUnixMilliseconds) / MillisecondsPerSecond, 0d, credited)
            : 0d;
        var gain = rate * credited + rate * (FrenzyMultiplier - 1d) * frenzySeconds;
        LastTickUnixMilliseconds = nowUnixMilliseconds;
        Earn(gain);
        ExpireBuff(nowUnixMilliseconds);
        var away = elapsed >= AwayThresholdSeconds;
        if (away && gain > 0d)
        {
            AwaySeconds += credited;
            AwayKupo = Math.Min(AwayKupo + gain, KupoCap);
            AwayCapped |= elapsed > OfflineCapSeconds;
        }

        return new KupoAdvance(credited, gain, away);
    }

    public double Tap(long nowUnixMilliseconds, double multiplier)
    {
        var gain = TapValue(nowUnixMilliseconds) * Math.Max(1d, multiplier);
        Taps++;
        TapKupo = Math.Min(TapKupo + gain, KupoCap);
        Earn(gain);
        return gain;
    }

    public int Buy(int building, int count)
    {
        if (building < 0 || building >= owned.Length || count <= 0 || owned[building] + count > KupoBuildings.MaxOwned)
        {
            return 0;
        }

        var cost = KupoBuildings.BulkCost(building, owned[building], count);
        if (!(cost <= Kupo))
        {
            return 0;
        }

        Kupo = Math.Max(0d, Kupo - cost);
        owned[building] += count;
        return count;
    }

    public int BuyMax(int building)
    {
        if (building < 0 || building >= owned.Length)
        {
            return 0;
        }

        var count = KupoBuildings.MaxAffordable(building, owned[building], Kupo);
        return count > 0 ? Buy(building, count) : 0;
    }

    public int Affordable(int building) => KupoBuildings.MaxAffordable(building, owned[building], Kupo);

    public bool CanBuyUpgrade(int upgrade) =>
        upgrade >= 0 && upgrade < KupoUpgrades.Count && !HasUpgrade(upgrade) && IsUnlocked(upgrade) &&
        KupoUpgrades.Cost(upgrade) <= Kupo;

    public bool BuyUpgrade(int upgrade)
    {
        if (!CanBuyUpgrade(upgrade))
        {
            return false;
        }

        Kupo = Math.Max(0d, Kupo - KupoUpgrades.Cost(upgrade));
        upgrades |= KupoUpgrades.Bit(upgrade);
        return true;
    }

    public int NextUpgrades(Span<int> output)
    {
        var count = 0;
        for (var upgrade = 0; upgrade < KupoUpgrades.Count; upgrade++)
        {
            if (HasUpgrade(upgrade) || !IsUnlocked(upgrade))
            {
                continue;
            }

            var cost = KupoUpgrades.Cost(upgrade);
            var slot = count < output.Length ? count : output.Length;
            while (slot > 0 && KupoUpgrades.Cost(output[slot - 1]) > cost)
            {
                if (slot < output.Length)
                {
                    output[slot] = output[slot - 1];
                }

                slot--;
            }

            if (slot >= output.Length)
            {
                continue;
            }

            output[slot] = upgrade;
            if (count < output.Length)
            {
                count++;
            }
        }

        return count;
    }

    public double Reward(KupoReward reward, long nowUnixMilliseconds)
    {
        switch (reward)
        {
            case KupoReward.Frenzy:
                MinionsCaught++;
                StartBuff(KupoReward.Frenzy, FrenzySeconds, nowUnixMilliseconds);
                return 0d;
            case KupoReward.TapFrenzy:
                MinionsCaught++;
                StartBuff(KupoReward.TapFrenzy, TapFrenzySeconds, nowUnixMilliseconds);
                return 0d;
            case KupoReward.Lump:
            {
                MinionsCaught++;
                var gain = Math.Min(Kupo * LumpBankShare, BaseKupoPerSecond * LumpProductionSeconds) + LumpFloor;
                Earn(gain);
                return gain;
            }
            default:
                return 0d;
        }
    }

    public double CloseLedger()
    {
        var gained = PendingStamps;
        if (gained < 1d)
        {
            return 0d;
        }

        Stamps += gained;
        LedgerPages++;
        Kupo = 0d;
        LedgerKupo = 0d;
        Array.Clear(owned);
        upgrades = 0UL;
        Buff = KupoReward.None;
        BuffEndsUnixMilliseconds = 0L;
        return gained;
    }

    public void DismissAway()
    {
        AwaySeconds = 0d;
        AwayKupo = 0d;
        AwayCapped = false;
    }

    public void Load(MoogleClickerSave save)
    {
        Kupo = Sanitize(save.Kupo);
        LedgerKupo = Sanitize(save.LedgerKupo);
        LifetimeKupo = Sanitize(save.LifetimeKupo);
        TapKupo = Sanitize(save.TapKupo);
        Taps = Math.Max(0L, save.Taps);
        Array.Clear(owned);
        var saved = save.Owned;
        if (saved is not null)
        {
            var length = Math.Min(saved.Length, owned.Length);
            for (var building = 0; building < length; building++)
            {
                owned[building] = Math.Clamp(saved[building], 0, KupoBuildings.MaxOwned);
            }
        }

        upgrades = unchecked((ulong)save.Upgrades);
        Stamps = Math.Floor(Sanitize(save.Stamps));
        LedgerPages = Math.Max(0, save.LedgerPages);
        MinionsCaught = Math.Max(0, save.MinionsCaught);
        Buff = save.Buff is KupoReward.Frenzy or KupoReward.TapFrenzy ? save.Buff : KupoReward.None;
        BuffEndsUnixMilliseconds = Buff == KupoReward.None ? 0L : save.BuffEndsUnixMilliseconds;
        LastTickUnixMilliseconds = Math.Max(0L, save.LastTickUnixMilliseconds);
        AwaySeconds = Sanitize(save.AwaySeconds);
        AwayKupo = Sanitize(save.AwayKupo);
        AwayCapped = save.AwayCapped;
    }

    public void Store(MoogleClickerSave save)
    {
        save.Kupo = Kupo;
        save.LedgerKupo = LedgerKupo;
        save.LifetimeKupo = LifetimeKupo;
        save.TapKupo = TapKupo;
        save.Taps = Taps;
        if (save.Owned is null || save.Owned.Length != owned.Length)
        {
            save.Owned = new int[owned.Length];
        }

        Array.Copy(owned, save.Owned, owned.Length);
        save.Upgrades = unchecked((long)upgrades);
        save.Stamps = Stamps;
        save.LedgerPages = LedgerPages;
        save.MinionsCaught = MinionsCaught;
        save.Buff = Buff;
        save.BuffEndsUnixMilliseconds = BuffEndsUnixMilliseconds;
        save.LastTickUnixMilliseconds = LastTickUnixMilliseconds;
        save.AwaySeconds = AwaySeconds;
        save.AwayKupo = AwayKupo;
        save.AwayCapped = AwayCapped;
    }

    private void Earn(double gain)
    {
        if (!(gain > 0d))
        {
            return;
        }

        Kupo = Math.Min(Kupo + gain, KupoCap);
        LedgerKupo = Math.Min(LedgerKupo + gain, KupoCap);
        LifetimeKupo = Math.Min(LifetimeKupo + gain, KupoCap);
    }

    private void StartBuff(KupoReward buff, double seconds, long nowUnixMilliseconds)
    {
        Buff = buff;
        BuffEndsUnixMilliseconds = nowUnixMilliseconds + (long)(seconds * MillisecondsPerSecond);
    }

    private void ExpireBuff(long nowUnixMilliseconds)
    {
        if (Buff == KupoReward.None || nowUnixMilliseconds < BuffEndsUnixMilliseconds)
        {
            return;
        }

        Buff = KupoReward.None;
        BuffEndsUnixMilliseconds = 0L;
    }

    private static double Sanitize(double value) =>
        double.IsFinite(value) ? Math.Clamp(value, 0d, KupoCap) : 0d;
}
