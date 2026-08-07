namespace Aetherphone.Core.YellowPages;

internal enum AdCreateOutcome : byte
{
    Created,
    TooMany,
    Invalid,
    RateLimited,
    Failed,
}
