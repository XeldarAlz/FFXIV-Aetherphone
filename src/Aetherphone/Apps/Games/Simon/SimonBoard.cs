namespace Aetherphone.Apps.Games.Simon;

internal sealed class SimonBoard
{
    public const int PadCount = 4;

    public const int MaxLength = 200;

    private readonly int[] sequence = new int[MaxLength];

    private readonly Random random = new();

    public int Length { get; private set; }

    public int PadAt(int index) => sequence[index];

    public void Reset()
    {
        Length = 0;
    }

    public void AddStep()
    {
        if (Length >= MaxLength)
        {
            return;
        }

        sequence[Length] = random.Next(PadCount);
        Length++;
    }

    public bool Matches(int index, int pad) => index < Length && sequence[index] == pad;
}
