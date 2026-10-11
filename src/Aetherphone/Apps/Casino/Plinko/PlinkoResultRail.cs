namespace Aetherphone.Apps.Casino.Plinko;

internal readonly record struct PlinkoResult(int Tenths, int Rows, int Risk, bool Edge);

internal sealed class PlinkoResultRail
{
    public const int Capacity = 24;
    public const float ChipWidth = 52f;
    public const float Gap = 4f;
    public const float CaptionGap = 8f;
    public const float CaptionShare = 0.3f;
    public const float FadePerAge = 0.07f;
    public const float MinimumAlpha = 0.45f;

    public static float CaptionRoom(float rowWidth) => rowWidth * CaptionShare;

    public static float ChipsLeft(float rowLeft, float captionWidth, float scale) =>
        captionWidth > 0f ? rowLeft + captionWidth + CaptionGap * scale : rowLeft;

    public static int Fit(float rowWidth, float captionWidth, float scale)
    {
        var room = rowWidth - ChipsLeft(0f, captionWidth, scale);
        var step = (ChipWidth + Gap) * scale;
        return Math.Max(0, (int)((room + Gap * scale) / step));
    }

    public static float ChipLeft(float rowLeft, float captionWidth, int age, float scale) =>
        ChipsLeft(rowLeft, captionWidth, scale) + age * (ChipWidth + Gap) * scale;

    public static float AlphaFor(int age) => MathF.Max(MinimumAlpha, 1f - age * FadePerAge);

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
