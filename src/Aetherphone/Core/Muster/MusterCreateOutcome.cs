namespace Aetherphone.Core.Muster;

internal enum MusterCreateOutcome
{
    Created,
    AlreadyHosting,
    Invalid,
    RateLimited,
    Failed,
}
