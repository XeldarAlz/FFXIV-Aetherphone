using Aetherphone.Core.Crypto;
using Xunit;

namespace Aetherphone.Tests;

public sealed class AdInquiryCryptoTests
{
    private const string Owner = "owner-1";
    private const string Reader = "reader-2";

    private static string PairScope(string first, string second) =>
        ConversationKeyStore.AdScope(ConversationKeyStore.Pair(first, second));

    [Fact]
    public void BothSidesDeriveTheSameScope()
    {
        Assert.Equal(PairScope(Owner, Reader), PairScope(Reader, Owner));
        Assert.StartsWith("ads:", PairScope(Owner, Reader));
    }

    [Fact]
    public void ReaderSealsAndOwnerOpens()
    {
        var cek = CryptoBox.GenerateCek();
        var scope = PairScope(Reader, Owner);
        var encoded = EnvelopeCodec.Encode("can you meld my gear?", cek, 1, scope, Reader);

        Assert.True(EnvelopeCodec.IsEnvelope(encoded.Envelope));
        Assert.DoesNotContain("meld", encoded.Envelope, System.StringComparison.OrdinalIgnoreCase);

        var opened = EnvelopeCodec.Decode(encoded.Envelope, cek, PairScope(Owner, Reader), Reader,
            encoded.CommitmentTag);
        Assert.Equal(EnvelopeDecodeStatus.Success, opened.Status);
        Assert.Equal("can you meld my gear?", opened.Body);
        Assert.True(opened.CommitmentVerified);
    }

    [Fact]
    public void AnotherPairCannotOpenTheEnvelope()
    {
        var cek = CryptoBox.GenerateCek();
        var encoded = EnvelopeCodec.Encode("private", cek, 1, PairScope(Reader, Owner), Reader);

        var wrongScope = EnvelopeCodec.Decode(encoded.Envelope, cek, PairScope(Reader, "stranger-3"), Reader,
            encoded.CommitmentTag);
        Assert.Equal(EnvelopeDecodeStatus.WrongKey, wrongScope.Status);

        var wrongKey = EnvelopeCodec.Decode(encoded.Envelope, CryptoBox.GenerateCek(), PairScope(Reader, Owner),
            Reader, encoded.CommitmentTag);
        Assert.Equal(EnvelopeDecodeStatus.WrongKey, wrongKey.Status);
    }

    [Fact]
    public void GramAndAdScopesStayApartForTheSamePair()
    {
        var cek = CryptoBox.GenerateCek();
        var pair = ConversationKeyStore.Pair(Owner, Reader);
        var encoded = EnvelopeCodec.Encode("inquiry body", cek, 1, ConversationKeyStore.AdScope(pair), Reader);

        var asGram = EnvelopeCodec.Decode(encoded.Envelope, cek, ConversationKeyStore.GramScope(pair), Reader,
            encoded.CommitmentTag);
        Assert.Equal(EnvelopeDecodeStatus.WrongKey, asGram.Status);
    }

    [Fact]
    public void RotatingTheGenerationDoesNotOpenOlderMessages()
    {
        var scope = PairScope(Reader, Owner);
        var firstCek = CryptoBox.GenerateCek();
        var secondCek = CryptoBox.GenerateCek();
        var encoded = EnvelopeCodec.Encode("older message", firstCek, 1, scope, Reader);

        Assert.True(EnvelopeCodec.TryParseGeneration(encoded.Envelope, out var generation));
        Assert.Equal(1, generation);

        var withNewCek = EnvelopeCodec.Decode(encoded.Envelope, secondCek, scope, Reader, encoded.CommitmentTag);
        Assert.Equal(EnvelopeDecodeStatus.WrongKey, withNewCek.Status);

        var withOldCek = EnvelopeCodec.Decode(encoded.Envelope, firstCek, scope, Reader, encoded.CommitmentTag);
        Assert.Equal(EnvelopeDecodeStatus.Success, withOldCek.Status);
    }
}
