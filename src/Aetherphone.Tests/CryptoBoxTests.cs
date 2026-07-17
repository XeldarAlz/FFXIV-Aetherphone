using System.Text;
using Aetherphone.Core.Crypto;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CryptoBoxTests
{
    [Fact]
    public void CekWrapUnwrapRoundTrips()
    {
        var identity = CryptoBox.TryGenerateIdentity();
        Assert.NotNull(identity);
        using var owned = identity;
        var publicKey = CryptoBox.ExportPublicKey(identity);
        var cek = CryptoBox.GenerateCek();
        var expected = (byte[])cek.Clone();

        var wrapped = CryptoBox.WrapCek(cek, publicKey);
        Assert.NotNull(wrapped);

        var unwrapped = CryptoBox.UnwrapCek(wrapped, identity);
        Assert.NotNull(unwrapped);
        Assert.Equal(expected, unwrapped);
    }

    [Fact]
    public void PrivateKeyExportImportRoundTrips()
    {
        var identity = CryptoBox.TryGenerateIdentity();
        Assert.NotNull(identity);
        using var owned = identity;
        var pkcs8 = CryptoBox.TryExportPrivateKey(identity);
        Assert.NotNull(pkcs8);

        using var imported = CryptoBox.ImportPrivateKey(pkcs8);
        Assert.NotNull(imported);
        Assert.Equal(CryptoBox.ExportPublicKey(identity), CryptoBox.ExportPublicKey(imported));
    }

    [Fact]
    public void OpenRejectsWrongAad()
    {
        var cek = CryptoBox.GenerateCek();
        var aad = Encoding.UTF8.GetBytes("scope|1|sender");
        var sealedBytes = CryptoBox.Seal(Encoding.UTF8.GetBytes("payload"), cek, aad);

        Assert.Null(CryptoBox.Open(sealedBytes, cek, Encoding.UTF8.GetBytes("scope|2|sender")));
        var opened = CryptoBox.Open(sealedBytes, cek, aad);
        Assert.NotNull(opened);
        Assert.Equal("payload", Encoding.UTF8.GetString(opened));
    }

    [Fact]
    public void WrappedCekIsFreshPerCall()
    {
        var identity = CryptoBox.TryGenerateIdentity();
        Assert.NotNull(identity);
        using var owned = identity;
        var publicKey = CryptoBox.ExportPublicKey(identity);
        var cek = CryptoBox.GenerateCek();

        var first = CryptoBox.WrapCek((byte[])cek.Clone(), publicKey);
        var second = CryptoBox.WrapCek((byte[])cek.Clone(), publicKey);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first, second);
    }
}
