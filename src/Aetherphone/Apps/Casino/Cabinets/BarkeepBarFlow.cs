using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Cabinets;

internal readonly record struct BarkeepPatronLook(int Tone, int Hair, int Hat, float Height, float Width);

internal sealed class BarkeepBarFlow
{
    public const float ServeX = 0.5f;
    public const float QueueStep = 0.17f;
    public const int VisibleQueue = 3;
    public const float EntryX = 1.2f;
    public const float ExitX = -0.25f;
    public const float SlideSmoothSeconds = 0.22f;
    public const float RailTolerance = 0.03f;
    public const int ToneCount = 6;
    public const int HairCount = 5;
    public const int HatCount = 4;

    private const float EdgeMargin = 0.02f;
    private const ulong FnvOffset = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    private readonly float[] positions = new float[BarkeepRules.MaxPatrons];
    private readonly float[] velocities = new float[BarkeepRules.MaxPatrons];
    private readonly bool[] present = new bool[BarkeepRules.MaxPatrons];
    private readonly BarkeepPatronLook[] looks = new BarkeepPatronLook[BarkeepRules.MaxPatrons];

    private int patronCount;

    public int PatronCount => patronCount;

    public static ulong SeedOf(string roundId)
    {
        var hash = FnvOffset;
        for (var index = 0; index < roundId.Length; index++)
        {
            hash ^= roundId[index];
            hash *= FnvPrime;
        }

        return hash;
    }

    public void Begin(ulong seed, int patrons)
    {
        patronCount = Math.Clamp(patrons, 0, BarkeepRules.MaxPatrons);
        var random = GameRandom.FromSeed(seed);
        for (var patronIndex = 0; patronIndex < BarkeepRules.MaxPatrons; patronIndex++)
        {
            looks[patronIndex] = new BarkeepPatronLook(random.Next(ToneCount), random.Next(HairCount),
                random.Next(HatCount), random.Range(0.88f, 1.08f), random.Range(0.9f, 1.12f));
            positions[patronIndex] = EntryX;
            velocities[patronIndex] = 0f;
            present[patronIndex] = false;
        }
    }

    public BarkeepPatronLook LookOf(int patronIndex) => looks[patronIndex];

    public float PositionOf(int patronIndex) => positions[patronIndex];

    public bool IsVisible(int patronIndex)
    {
        if (patronIndex < 0 || patronIndex >= patronCount || !present[patronIndex])
        {
            return false;
        }

        var position = positions[patronIndex];
        return position > ExitX + EdgeMargin && position < EntryX - EdgeMargin;
    }

    public bool AtRail(BarkeepShift shift, int patronIndex)
    {
        return patronIndex == shift.CompletedOrders && IsVisible(patronIndex)
            && MathF.Abs(positions[patronIndex] - ServeX) <= RailTolerance;
    }

    public static float TargetFor(int patronIndex, int completedOrders)
    {
        if (patronIndex < completedOrders)
        {
            return ExitX;
        }

        var slot = patronIndex - completedOrders;
        return slot <= VisibleQueue ? ServeX + slot * QueueStep : EntryX;
    }

    public void Update(BarkeepShift shift, double elapsedSeconds, float deltaSeconds)
    {
        var count = Math.Min(patronCount, shift.PatronCount);
        for (var patronIndex = 0; patronIndex < count; patronIndex++)
        {
            if (!present[patronIndex])
            {
                if (elapsedSeconds < shift.ArrivalOf(patronIndex))
                {
                    continue;
                }

                present[patronIndex] = true;
                positions[patronIndex] = EntryX;
                velocities[patronIndex] = 0f;
            }

            var target = TargetFor(patronIndex, shift.CompletedOrders);
            Smooth(ref positions[patronIndex], ref velocities[patronIndex], target, deltaSeconds);
        }
    }

    public void Snap(BarkeepShift shift, double elapsedSeconds)
    {
        var count = Math.Min(patronCount, shift.PatronCount);
        for (var patronIndex = 0; patronIndex < count; patronIndex++)
        {
            present[patronIndex] = elapsedSeconds >= shift.ArrivalOf(patronIndex)
                || patronIndex < shift.CompletedOrders;
            positions[patronIndex] = present[patronIndex]
                ? TargetFor(patronIndex, shift.CompletedOrders)
                : EntryX;
            velocities[patronIndex] = 0f;
        }
    }

    private static void Smooth(ref float position, ref float velocity, float target, float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        var omega = 2f / SlideSmoothSeconds;
        var step = omega * deltaSeconds;
        var decay = 1f / (1f + step + 0.48f * step * step + 0.235f * step * step * step);
        var change = position - target;
        var carry = (velocity + omega * change) * deltaSeconds;
        velocity = (velocity - omega * carry) * decay;
        position = target + (change + carry) * decay;
    }
}
