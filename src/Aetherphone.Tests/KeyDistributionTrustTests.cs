using Aetherphone.Core.Crypto;
using Xunit;

namespace Aetherphone.Tests;

public sealed class KeyDistributionTrustTests
{
    private const string Scope = "chat:7";
    private const int Generation = 1;

    [Fact]
    public void WrapMintedFromOnlyAPublicKeyUnwrapsSuccessfully()
    {
        var victim = CryptoBox.TryGenerateIdentity()!;
        var victimPublicKey = CryptoBox.ExportPublicKey(victim);
        var keyChosenByWhoeverServesTheWrap = CryptoBox.GenerateCek();

        var wrap = CryptoBox.WrapCek(keyChosenByWhoeverServesTheWrap, victimPublicKey);

        Assert.NotNull(wrap);
        Assert.Equal(keyChosenByWhoeverServesTheWrap, CryptoBox.UnwrapCek(wrap!, victim));
    }

    [Fact]
    public void SubstitutedConversationKeyProducesAnEnvelopeThatDecodesAsValid()
    {
        var honestKey = CryptoBox.GenerateCek();
        var substitutedKey = CryptoBox.GenerateCek();

        var envelope = EnvelopeCodec.Encode("meet me at the Goblet", substitutedKey, Generation, Scope, "victim");

        var underSubstitutedKey = EnvelopeCodec.Decode(envelope.Envelope, substitutedKey, Scope, "victim", null);
        Assert.Equal(EnvelopeDecodeStatus.Success, underSubstitutedKey.Status);
        Assert.Equal("meet me at the Goblet", underSubstitutedKey.Body);

        var underHonestKey = EnvelopeCodec.Decode(envelope.Envelope, honestKey, Scope, "victim", null);
        Assert.Equal(EnvelopeDecodeStatus.WrongKey, underHonestKey.Status);
    }

    [Fact]
    public void CommitmentTagVerifiesForAnyHolderOfTheConversationKey()
    {
        var conversationKey = CryptoBox.GenerateCek();

        var forged = EnvelopeCodec.Encode("wire me 5 million gil", conversationKey, Generation, Scope, "peer");
        var asTheRecipientSeesIt = EnvelopeCodec.Decode(
            forged.Envelope, conversationKey, Scope, "peer", forged.CommitmentTag);

        Assert.Equal(EnvelopeDecodeStatus.Success, asTheRecipientSeesIt.Status);
        Assert.Equal("wire me 5 million gil", asTheRecipientSeesIt.Body);
        Assert.True(asTheRecipientSeesIt.CommitmentVerified);
    }
}
