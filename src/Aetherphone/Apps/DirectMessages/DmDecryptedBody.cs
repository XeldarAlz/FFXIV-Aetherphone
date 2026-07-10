namespace Aetherphone.Apps.DirectMessages;

internal enum DmBodyState : byte
{
    Plain = 0,
    Decrypted = 1,
    Locked = 2,
    NoKey = 3,
    Malformed = 4,
}

internal readonly record struct DmDecryptedBody(DmBodyState State, string Text, string? FrankingKey, bool Verified)
{
    public bool IsPlaceholder => State is DmBodyState.Locked or DmBodyState.NoKey or DmBodyState.Malformed;
}
