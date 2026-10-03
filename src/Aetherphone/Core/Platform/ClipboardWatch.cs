using System.Runtime.InteropServices;

namespace Aetherphone.Core.Platform;

internal static class ClipboardWatch
{
    internal const uint UnseenSequence = uint.MaxValue;

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();

    internal static bool HasChanged(ref uint seenSequence)
    {
        var sequence = ReadSequence();
        if (sequence == seenSequence)
        {
            return false;
        }

        seenSequence = sequence;
        return true;
    }

    private static uint ReadSequence()
    {
        try
        {
            return GetClipboardSequenceNumber();
        }
        catch (Exception)
        {
            return 0u;
        }
    }
}
