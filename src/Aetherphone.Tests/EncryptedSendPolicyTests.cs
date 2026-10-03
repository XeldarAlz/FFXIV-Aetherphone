using Aetherphone.Core.Crypto;
using Aetherphone.Core.Message;
using Xunit;

namespace Aetherphone.Tests;

public sealed class EncryptedSendPolicyTests
{
    private static readonly string[] NoMembersWithoutKeys = Array.Empty<string>();
    private static readonly string[] OneMemberWithoutKey = { "keyless-member" };

    [Fact]
    public void AnUnknownStatusHoldsPlaintextUntilTheServerAnswers()
    {
        Assert.True(EncryptedSendPolicy.MustHoldPlaintext(false, ChatKeyStatus.None));
    }

    [Fact]
    public void AFailedKeyFetchHoldsPlaintextEvenWithACachedGeneration()
    {
        var failedFetch = new ChatKeyStatus(true, false, 0, NoMembersWithoutKeys, false);

        Assert.True(EncryptedSendPolicy.MustHoldPlaintext(false, failedFetch));
    }

    [Fact]
    public void ALockedVaultStillSeesTheServerGenerationAndHoldsPlaintext()
    {
        var lockedButEncrypted = new ChatKeyStatus(false, false, 3, NoMembersWithoutKeys);

        Assert.True(EncryptedSendPolicy.IsEncryptedThread(lockedButEncrypted));
        Assert.True(EncryptedSendPolicy.MustHoldPlaintext(false, lockedButEncrypted));
    }

    [Fact]
    public void AThreadWithAKeylessMemberAcceptsPlaintext()
    {
        var waitingForKey = new ChatKeyStatus(true, false, 2, OneMemberWithoutKey);

        Assert.False(EncryptedSendPolicy.IsEncryptedThread(waitingForKey));
        Assert.False(EncryptedSendPolicy.MustHoldPlaintext(false, waitingForKey));
    }

    [Fact]
    public void AThreadWithoutAGenerationAcceptsPlaintext()
    {
        var neverKeyed = new ChatKeyStatus(true, false, 0, NoMembersWithoutKeys);

        Assert.False(EncryptedSendPolicy.MustHoldPlaintext(false, neverKeyed));
    }

    [Fact]
    public void AnEncryptedSendIsNeverHeld()
    {
        Assert.False(EncryptedSendPolicy.MustHoldPlaintext(true, ChatKeyStatus.None));
        Assert.False(EncryptedSendPolicy.MustHoldPlaintext(true,
            new ChatKeyStatus(true, true, 1, NoMembersWithoutKeys)));
    }
}
