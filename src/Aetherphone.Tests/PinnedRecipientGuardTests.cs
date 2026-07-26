using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Crypto;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PinnedRecipientGuardTests
{
    [Fact]
    public void AllowsUnknownRecipientWhenNoRosterConfigured()
    {
        var guard = new PinnedRecipientGuard();
        Assert.True(guard.IsAuthorized(new UserPublicKeyDto("bob", "key-a", 1)));
    }

    [Fact]
    public void RejectsRecipientNotOnConfiguredRoster()
    {
        var guard = new PinnedRecipientGuard();
        guard.SetAuthorizedMembers("alice");
        Assert.False(guard.IsAuthorized(new UserPublicKeyDto("bob", "key-a", 1)));
    }

    [Fact]
    public void RejectsSameVersionKeySubstitution()
    {
        var guard = new PinnedRecipientGuard();
        guard.Pin("bob", 1, "honest-key");
        Assert.False(guard.IsAuthorized(new UserPublicKeyDto("bob", "attacker-key", 1)));
    }

    [Fact]
    public void AllowsHigherVersionKeyRotation()
    {
        var guard = new PinnedRecipientGuard();
        guard.Pin("bob", 1, "honest-key");
        Assert.True(guard.IsAuthorized(new UserPublicKeyDto("bob", "rotated-key", 2)));
    }

    [Fact]
    public void AcceptsMatchingPinnedKey()
    {
        var guard = new PinnedRecipientGuard();
        var recipient = new UserPublicKeyDto("bob", "key-a", 1);
        Assert.True(guard.IsAuthorized(recipient));
        Assert.True(guard.IsAuthorized(recipient));
    }
}
