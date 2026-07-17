using System.Text;
using Aetherphone.Core.Crypto;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MediaEnvelopeTests
{
    private const string Scope = "chat:conversation-1";
    private const string Sender = "user-1";
    private const int Image = 1;
    private const int Voice = 3;
    private static readonly byte[] Payload = Encoding.UTF8.GetBytes("pretend this is a JPEG");

    [Fact]
    public void RoundTripReturnsTheOriginalBytes()
    {
        var cek = CryptoBox.GenerateCek();
        var sealedBytes = MediaEnvelope.Seal(Payload, cek, Scope, 2, Sender, Image);

        var opened = MediaEnvelope.Open(sealedBytes, cek, Scope, 2, Sender, Image);
        Assert.NotNull(opened);
        Assert.Equal(Payload, opened);
    }

    [Fact]
    public void WrongKeyFailsClosed()
    {
        var cek = CryptoBox.GenerateCek();
        var sealedBytes = MediaEnvelope.Seal(Payload, cek, Scope, 1, Sender, Image);

        Assert.Null(MediaEnvelope.Open(sealedBytes, CryptoBox.GenerateCek(), Scope, 1, Sender, Image));
    }

    [Fact]
    public void MediaKindIsBoundSoImageCannotOpenAsVoice()
    {
        var cek = CryptoBox.GenerateCek();
        var sealedBytes = MediaEnvelope.Seal(Payload, cek, Scope, 1, Sender, Image);

        Assert.Null(MediaEnvelope.Open(sealedBytes, cek, Scope, 1, Sender, Voice));
    }

    [Fact]
    public void ScopeGenerationAndSenderAreBound()
    {
        var cek = CryptoBox.GenerateCek();
        var sealedBytes = MediaEnvelope.Seal(Payload, cek, Scope, 5, Sender, Image);

        Assert.Null(MediaEnvelope.Open(sealedBytes, cek, "chat:other", 5, Sender, Image));
        Assert.Null(MediaEnvelope.Open(sealedBytes, cek, Scope, 6, Sender, Image));
        Assert.Null(MediaEnvelope.Open(sealedBytes, cek, Scope, 5, "user-2", Image));
    }

    [Fact]
    public void TamperedCiphertextFailsClosed()
    {
        var cek = CryptoBox.GenerateCek();
        var sealedBytes = MediaEnvelope.Seal(Payload, cek, Scope, 1, Sender, Image);
        sealedBytes[^1] ^= 0xFF;

        Assert.Null(MediaEnvelope.Open(sealedBytes, cek, Scope, 1, Sender, Image));
    }
}
