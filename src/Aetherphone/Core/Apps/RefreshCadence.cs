namespace Aetherphone.Core.Apps;

internal struct RefreshCadence
{
    private float elapsedSeconds;

    public bool Advance(float deltaSeconds, float intervalSeconds)
    {
        elapsedSeconds += deltaSeconds;
        return elapsedSeconds >= intervalSeconds;
    }

    public void Reset() => elapsedSeconds = 0f;
}
