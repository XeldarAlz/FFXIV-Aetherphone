using System.Text;
using System.Security.Cryptography;
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
    [Fact]
    public void ManagedP256KeysInteroperateWithPlatformEcdh()
    {
        using var identity = CryptoBox.TryGenerateIdentity();
        Assert.NotNull(identity);
        var publicKey = CryptoBox.ExportPublicKey(identity);
        var privateKey = CryptoBox.TryExportPrivateKey(identity);
        Assert.NotNull(privateKey);

        using var platformIdentity = ECDiffieHellman.Create();
        platformIdentity.ImportPkcs8PrivateKey(privateKey, out var privateBytesRead);
        Assert.Equal(privateKey.Length, privateBytesRead);
        Assert.Equal(publicKey, Convert.ToBase64String(platformIdentity.ExportSubjectPublicKeyInfo()));

        var cek = CryptoBox.GenerateCek();
        var managedWrap = CryptoBox.WrapCek(cek, publicKey);
        Assert.NotNull(managedWrap);
        Assert.Equal(cek, PlatformUnwrap(managedWrap, platformIdentity));

        var platformWrap = PlatformWrap(cek, publicKey);
        Assert.Equal(cek, CryptoBox.UnwrapCek(platformWrap, identity));
    }

    private static string PlatformWrap(byte[] cek, string recipientPublicKeyBase64)
    {
        using var recipient = ECDiffieHellman.Create();
        recipient.ImportSubjectPublicKeyInfo(Convert.FromBase64String(recipientPublicKeyBase64), out _);
        using var ephemeral = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var shared = ephemeral.DeriveRawSecretAgreement(recipient.PublicKey);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var wrapKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, CryptoBox.CekBytes, nonce,
            Encoding.UTF8.GetBytes("aethernet-cek-v1"));
        CryptographicOperations.ZeroMemory(shared);
        try
        {
            var ephemeralPublic = ephemeral.ExportSubjectPublicKeyInfo();
            var payload = new byte[1 + ephemeralPublic.Length + nonce.Length + cek.Length + 16];
            payload[0] = (byte)ephemeralPublic.Length;
            ephemeralPublic.CopyTo(payload.AsSpan(1));
            nonce.CopyTo(payload.AsSpan(1 + ephemeralPublic.Length));
            var cipherOffset = 1 + ephemeralPublic.Length + nonce.Length;
            using var aes = new AesGcm(wrapKey, 16);
            aes.Encrypt(nonce, cek, payload.AsSpan(cipherOffset, cek.Length),
                payload.AsSpan(cipherOffset + cek.Length, 16));
            return "EC1." + Convert.ToBase64String(payload);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(wrapKey);
        }
    }

    private static byte[] PlatformUnwrap(string wrappedKey, ECDiffieHellman privateKey)
    {
        var payload = Convert.FromBase64String(wrappedKey["EC1.".Length..]);
        var ephemeralLength = payload[0];
        using var ephemeral = ECDiffieHellman.Create();
        ephemeral.ImportSubjectPublicKeyInfo(payload.AsSpan(1, ephemeralLength), out _);
        var shared = privateKey.DeriveRawSecretAgreement(ephemeral.PublicKey);
        var nonce = payload.AsSpan(1 + ephemeralLength, 12).ToArray();
        var wrapKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, CryptoBox.CekBytes, nonce,
            Encoding.UTF8.GetBytes("aethernet-cek-v1"));
        CryptographicOperations.ZeroMemory(shared);
        try
        {
            var cipherOffset = 1 + ephemeralLength + nonce.Length;
            var cek = new byte[CryptoBox.CekBytes];
            using var aes = new AesGcm(wrapKey, 16);
            aes.Decrypt(nonce, payload.AsSpan(cipherOffset, cek.Length),
                payload.AsSpan(cipherOffset + cek.Length, 16), cek);
            return cek;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(wrapKey);
        }
    }
}
