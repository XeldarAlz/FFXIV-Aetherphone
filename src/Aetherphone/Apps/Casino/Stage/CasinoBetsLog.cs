using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino.Stage;

internal readonly record struct CasinoBetRecord(
    LocString Game,
    long Stake,
    long Payout,
    string RoundId,
    long SettledAtUnixMs,
    bool Capped = false)
{
    public int MultipleHundredths => Stake <= 0 ? 0 : (int)Math.Min(int.MaxValue, Payout * 100 / Stake);

    public bool Won => Payout > Stake;
}

internal sealed class CasinoBetsLog
{
    public const int Capacity = 50;

    private readonly CasinoBetRecord[] records = new CasinoBetRecord[Capacity];
    private int next;
    private int count;

    public int Count => count;

    public void Record(in CasinoBetRecord record)
    {
        if (record.Stake <= 0)
        {
            return;
        }

        if (record.RoundId.Length > 0 && Contains(record.RoundId))
        {
            return;
        }

        records[next] = record;
        next = (next + 1) % Capacity;
        if (count < Capacity)
        {
            count++;
        }
    }

    public CasinoBetRecord Newest(int index)
    {
        var slot = next - 1 - index;
        if (slot < 0)
        {
            slot += Capacity;
        }

        return records[slot];
    }

    public void Clear()
    {
        Array.Clear(records);
        next = 0;
        count = 0;
    }

    public bool Contains(string roundId)
    {
        for (var index = 0; index < count; index++)
        {
            if (string.Equals(Newest(index).RoundId, roundId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
