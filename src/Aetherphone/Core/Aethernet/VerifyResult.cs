using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Aethernet;

internal static class VerifyFailure
{
    public const string CharacterNotFound = "character_not_found";
    public const string CodeNotFound = "code_not_found";
    public const string LodestoneUnavailable = "lodestone_unavailable";
    public const string Timeout = "timeout";
    public const string ChallengeExpired = "challenge_expired";
    public const string Banned = "banned";
    public const string RateLimited = "rate_limited";
    public const string Network = "network";
    public const string Pending = "authorization_pending";
    public const string AccessDenied = "access_denied";
    public const string XivAuthUnavailable = "xivauth_unavailable";
    public const string XivCharacterNotVerified = "xiv_character_not_verified";
}

internal readonly record struct VerifyResult(AuthResponse? Auth, string? FailureReason)
{
    public static VerifyResult Success(AuthResponse auth)
    {
        return new VerifyResult(auth, null);
    }

    public static VerifyResult Failure(string reason)
    {
        return new VerifyResult(null, reason);
    }
}
