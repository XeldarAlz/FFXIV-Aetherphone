using Aetherphone.Core.Crypto;
using Xunit;

namespace Aetherphone.Tests;

public sealed class EnvelopeCodecTests
{
    private const string Scope = "chat:conversation-1";
    private const string Sender = "user-1";

    [Fact]
    public void RoundTripDecodesWithCommitmentVerified()
    {
        var cek = CryptoBox.GenerateCek();
        var encoded = EnvelopeCodec.Encode("hello moon", cek, 3, Scope, Sender);

        Assert.True(EnvelopeCodec.IsEnvelope(encoded.Envelope));
        Assert.True(EnvelopeCodec.TryParseGeneration(encoded.Envelope, out var generation));
        Assert.Equal(3, generation);

        var decoded = EnvelopeCodec.Decode(encoded.Envelope, cek, Scope, Sender, encoded.CommitmentTag);
        Assert.Equal(EnvelopeDecodeStatus.Success, decoded.Status);
        Assert.Equal("hello moon", decoded.Body);
        Assert.True(decoded.CommitmentVerified);
    }

    [Fact]
    public void WrongKeyFailsClosed()
    {
        var cek = CryptoBox.GenerateCek();
        var otherCek = CryptoBox.GenerateCek();
        var encoded = EnvelopeCodec.Encode("secret", cek, 1, Scope, Sender);

        var decoded = EnvelopeCodec.Decode(encoded.Envelope, otherCek, Scope, Sender, encoded.CommitmentTag);
        Assert.Equal(EnvelopeDecodeStatus.WrongKey, decoded.Status);
        Assert.Equal(string.Empty, decoded.Body);
    }

    [Fact]
    public void ScopeIsBoundIntoTheCiphertext()
    {
        var cek = CryptoBox.GenerateCek();
        var encoded = EnvelopeCodec.Encode("secret", cek, 1, Scope, Sender);

        var decoded = EnvelopeCodec.Decode(encoded.Envelope, cek, "chat:another-conversation", Sender,
            encoded.CommitmentTag);
        Assert.Equal(EnvelopeDecodeStatus.WrongKey, decoded.Status);
    }

    [Fact]
    public void SenderIsBoundIntoTheCiphertext()
    {
        var cek = CryptoBox.GenerateCek();
        var encoded = EnvelopeCodec.Encode("secret", cek, 1, Scope, Sender);

        var decoded = EnvelopeCodec.Decode(encoded.Envelope, cek, Scope, "user-2", encoded.CommitmentTag);
        Assert.Equal(EnvelopeDecodeStatus.WrongKey, decoded.Status);
    }

    [Fact]
    public void ForeignCommitmentTagDoesNotVerify()
    {
        var cek = CryptoBox.GenerateCek();
        var encoded = EnvelopeCodec.Encode("first message", cek, 1, Scope, Sender);
        var other = EnvelopeCodec.Encode("second message", cek, 1, Scope, Sender);

        var decoded = EnvelopeCodec.Decode(encoded.Envelope, cek, Scope, Sender, other.CommitmentTag);
        Assert.Equal(EnvelopeDecodeStatus.Success, decoded.Status);
        Assert.False(decoded.CommitmentVerified);
    }

    [Fact]
    public void GarbageIsMalformedNotCrashing()
    {
        var cek = CryptoBox.GenerateCek();
        var decoded = EnvelopeCodec.Decode("AE1.not-a-real-envelope", cek, Scope, Sender, null);
        Assert.Equal(EnvelopeDecodeStatus.Malformed, decoded.Status);
    }
}
