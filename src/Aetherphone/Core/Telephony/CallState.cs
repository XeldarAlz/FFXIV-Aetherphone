namespace Aetherphone.Core.Telephony;

internal enum CallState : byte
{
    Idle,
    Dialing,
    Ringing,
    Connecting,
    Active,
    Ended,
}
