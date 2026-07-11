using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Aethernet;

internal static class SignInFailureText
{
    public static (string Title, string Message) Resolve(string? reason, GameData gameData)
    {
        switch (reason)
        {
            case VerifyFailure.CharacterNotFound:
                var player = gameData.LocalPlayer;
                var name = player?.Name.TextValue ?? string.Empty;
                var world = gameData.WorldName(gameData.LocalHomeWorldId);
                return (Loc.T(L.Account.FailCharacterNotFoundTitle),
                    Loc.T(L.Account.FailCharacterNotFoundBody, name, world));
            case VerifyFailure.CodeNotFound:
                return (Loc.T(L.Account.FailCodeNotFoundTitle), Loc.T(L.Account.FailCodeNotFoundBody));
            case VerifyFailure.Timeout:
                return (Loc.T(L.Account.FailTimeoutTitle), Loc.T(L.Account.FailTimeoutBody));
            case VerifyFailure.ChallengeExpired:
                return (Loc.T(L.Account.FailChallengeExpiredTitle), Loc.T(L.Account.FailChallengeExpiredBody));
            case VerifyFailure.Banned:
                return (Loc.T(L.Account.FailBannedTitle), Loc.T(L.Account.FailBannedBody));
            case VerifyFailure.RateLimited:
                return (Loc.T(L.Account.FailRateLimitedTitle), Loc.T(L.Account.FailRateLimitedBody));
            case VerifyFailure.Network:
                return (Loc.T(L.Account.FailNetworkTitle), Loc.T(L.Account.FailNetworkBody));
            case VerifyFailure.AccessDenied:
                return (Loc.T(L.Account.FailAccessDeniedTitle), Loc.T(L.Account.FailAccessDeniedBody));
            case VerifyFailure.XivAuthUnavailable:
                return (Loc.T(L.Account.FailXivUnavailableTitle), Loc.T(L.Account.FailXivUnavailableBody));
            case VerifyFailure.XivCharacterNotVerified:
                var xivPlayer = gameData.LocalPlayer;
                var xivName = xivPlayer?.Name.TextValue ?? string.Empty;
                var xivWorld = gameData.WorldName(gameData.LocalHomeWorldId);
                return (Loc.T(L.Account.FailXivCharacterTitle),
                    Loc.T(L.Account.FailXivCharacterBody, xivName, xivWorld));
            default:
                return (Loc.T(L.Account.FailLodestoneUnavailableTitle), Loc.T(L.Account.FailLodestoneUnavailableBody));
        }
    }
}
