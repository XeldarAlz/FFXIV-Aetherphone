using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Crypto;

internal interface IWrapRecipientGuard
{
    bool IsAuthorized(UserPublicKeyDto recipient);
}
