namespace Aetherphone.Core.Photos;

internal sealed class CameraShutter
{
    private bool requested;

    public void Request() => requested = true;

    public bool TryConsume()
    {
        if (!requested)
        {
            return false;
        }

        requested = false;
        return true;
    }
}
