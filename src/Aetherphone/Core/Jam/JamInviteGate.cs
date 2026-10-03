namespace Aetherphone.Core.Jam;

internal sealed class JamInviteGate
{
    internal const long RepeatMilliseconds = 60_000;

    private string lastCode = string.Empty;
    private long lastAtMilliseconds;

    internal bool Admit(string code, long nowMilliseconds)
    {
        if (string.Equals(code, lastCode, StringComparison.Ordinal)
            && nowMilliseconds - lastAtMilliseconds < RepeatMilliseconds)
        {
            return false;
        }

        lastCode = code;
        lastAtMilliseconds = nowMilliseconds;
        return true;
    }
}
