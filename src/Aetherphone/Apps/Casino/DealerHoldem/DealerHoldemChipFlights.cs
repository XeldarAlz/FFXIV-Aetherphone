using Aetherphone.Core.Animation;

namespace Aetherphone.Apps.Casino.DealerHoldem;

internal readonly record struct DealerHoldemChipLanding(int Tag, long Amount);

internal sealed class DealerHoldemChipFlights
{
    public const int Capacity = 8;
    public const float Seconds = 0.45f;
    public const float Arc = 0.18f;

    private readonly Flight[] flights = new Flight[Capacity];
    private readonly DealerHoldemChipLanding[] landed = new DealerHoldemChipLanding[Capacity];
    private int count;
    private int landedCount;

    private struct Flight
    {
        public Vector2 From;
        public Vector2 To;
        public long Amount;
        public float Elapsed;
        public int Tag;
        public bool Fading;
    }

    public int Count => count;

    public bool Busy => count > 0;

    public void Launch(Vector2 from, Vector2 to, long amount, int tag, bool fading)
    {
        if (amount <= 0)
        {
            return;
        }

        if (count == Capacity)
        {
            Land(0);
        }

        flights[count] = new Flight
        {
            From = from,
            To = to,
            Amount = amount,
            Elapsed = 0f,
            Tag = tag,
            Fading = fading,
        };
        count++;
    }

    public void Advance(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        var index = 0;
        while (index < count)
        {
            flights[index].Elapsed += deltaSeconds;
            if (flights[index].Elapsed >= Seconds)
            {
                Land(index);
                continue;
            }

            index++;
        }
    }

    public void Snap()
    {
        while (count > 0)
        {
            Land(0);
        }
    }

    public bool InFlight(int tag)
    {
        for (var index = 0; index < count; index++)
        {
            if (flights[index].Tag == tag)
            {
                return true;
            }
        }

        return false;
    }

    public long Amount(int index) => flights[index].Amount;

    public Vector2 Position(int index)
    {
        ref readonly var flight = ref flights[index];
        var progress = Easing.EaseOutCubic(Math.Clamp(flight.Elapsed / Seconds, 0f, 1f));
        var center = Vector2.Lerp(flight.From, flight.To, progress);
        center.Y -= MathF.Sin(progress * MathF.PI) * Vector2.Distance(flight.From, flight.To) * Arc;
        return center;
    }

    public float Alpha(int index)
    {
        ref readonly var flight = ref flights[index];
        return flight.Fading ? 1f - Math.Clamp(flight.Elapsed / Seconds, 0f, 1f) * 0.8f : 1f;
    }

    public bool TryTakeLanded(out DealerHoldemChipLanding landing)
    {
        if (landedCount == 0)
        {
            landing = default;
            return false;
        }

        landedCount--;
        landing = landed[landedCount];
        return true;
    }

    public void Clear()
    {
        count = 0;
        landedCount = 0;
    }

    private void Land(int index)
    {
        if (landedCount < landed.Length)
        {
            landed[landedCount] = new DealerHoldemChipLanding(flights[index].Tag, flights[index].Amount);
            landedCount++;
        }

        count--;
        flights[index] = flights[count];
    }
}
