namespace Aetherphone.Core.Apps;

internal readonly record struct ResumeStamp(long ClosedAtTicks, ulong CharacterId, ulong AccountId)
{
    public const long WindowMilliseconds = 10 * 60 * 1000;

    public bool BelongsTo(ulong characterId, ulong accountId) => CharacterId == characterId && AccountId == accountId;

    public bool Allows(long nowTicks, ulong characterId, ulong accountId) =>
        nowTicks - ClosedAtTicks <= WindowMilliseconds && BelongsTo(characterId, accountId);
}
