namespace Aetherphone.Apps.Casino.Plinko;

internal readonly record struct PlinkoResult(int Tenths, int Rows, int Risk, bool Edge);

internal sealed class PlinkoResultRail
{
    public const int Capacity = 24;

    private readonly PlinkoResult[] results = new PlinkoResult[Capacity];
    private int next;
    private int count;

    public int Count => count;

    public float Arrival { get; private set; }

    public void Push(in PlinkoResult result)
    {
        results[next] = result;
        next = (next + 1) % Capacity;
        count = Math.Min(Capacity, count + 1);
        Arrival = 0f;
    }

    public PlinkoResult Newest(int age)
    {
        var slot = next - 1 - age;
        if (slot < 0)
        {
            slot += Capacity;
        }

        return results[slot];
    }

    public void Advance(float deltaSeconds)
    {
        Arrival = MathF.Min(1f, Arrival + MathF.Max(0f, deltaSeconds) * 4f);
    }

    public void Clear()
    {
        Array.Clear(results);
        next = 0;
        count = 0;
        Arrival = 1f;
    }
}
