using System.Security.Cryptography;
using System.Text;

namespace Aetherphone.Core.Crypto;

internal static class CryptoBox
{
    public const int CekBytes = 32;
    private const int NonceBytes = 12;
    private const int TagBytes = 16;
    private const string WrapPrefix = "EC1.";
    private static readonly byte[] WrapInfo = Encoding.UTF8.GetBytes("aethernet-cek-v1");

    public static ECDiffieHellman GenerateIdentity()
    {
        return ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
    }

    public static string ExportPublicKey(ECDiffieHellman key)
    {
        return Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
    }

    public static ECDiffieHellman? ImportPublicKey(string publicKeyBase64)
    {
        try
        {
            var key = ECDiffieHellman.Create();
            key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
            return key;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static byte[] ExportPrivateKey(ECDiffieHellman key)
    {
        return key.ExportPkcs8PrivateKey();
    }

    public static ECDiffieHellman? ImportPrivateKey(byte[] pkcs8)
    {
        try
        {
            var key = ECDiffieHellman.Create();
            key.ImportPkcs8PrivateKey(pkcs8, out _);
            return key;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static byte[] GenerateCek()
    {
        return RandomNumberGenerator.GetBytes(CekBytes);
    }

    public static string? WrapCek(byte[] cek, string recipientPublicKeyBase64)
    {
        using var recipient = ImportPublicKey(recipientPublicKeyBase64);
        if (recipient is null)
        {
            return null;
        }

        using var ephemeral = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var shared = ephemeral.DeriveRawSecretAgreement(recipient.PublicKey);
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var wrapKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, CekBytes, nonce, WrapInfo);
        CryptographicOperations.ZeroMemory(shared);

        var ephemeralPublic = ephemeral.ExportSubjectPublicKeyInfo();
        var payload = new byte[1 + ephemeralPublic.Length + NonceBytes + cek.Length + TagBytes];
        payload[0] = (byte)ephemeralPublic.Length;
        ephemeralPublic.CopyTo(payload.AsSpan(1));
        nonce.CopyTo(payload.AsSpan(1 + ephemeralPublic.Length));
        var cipherOffset = 1 + ephemeralPublic.Length + NonceBytes;
        using (var aes = new AesGcm(wrapKey, TagBytes))
        {
            aes.Encrypt(nonce, cek, payload.AsSpan(cipherOffset, cek.Length), payload.AsSpan(cipherOffset + cek.Length, TagBytes));
        }

        CryptographicOperations.ZeroMemory(wrapKey);
        return WrapPrefix + Convert.ToBase64String(payload);
    }

    public static byte[]? UnwrapCek(string wrappedKey, ECDiffieHellman privateKey)
    {
        if (!wrappedKey.StartsWith(WrapPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(wrappedKey[WrapPrefix.Length..]);
        }
        catch (FormatException)
        {
            return null;
        }

        if (payload.Length < 2)
        {
            return null;
        }

        int ephemeralLength = payload[0];
        var cipherOffset = 1 + ephemeralLength + NonceBytes;
        var cekLength = payload.Length - cipherOffset - TagBytes;
        if (ephemeralLength == 0 || cekLength != CekBytes)
        {
            return null;
        }

        try
        {
            using var ephemeral = ECDiffieHellman.Create();
            ephemeral.ImportSubjectPublicKeyInfo(payload.AsSpan(1, ephemeralLength), out _);
            var shared = privateKey.DeriveRawSecretAgreement(ephemeral.PublicKey);
            var nonce = payload.AsSpan(1 + ephemeralLength, NonceBytes).ToArray();
            var wrapKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, CekBytes, nonce, WrapInfo);
            CryptographicOperations.ZeroMemory(shared);

            var cek = new byte[CekBytes];
            try
            {
                using var aes = new AesGcm(wrapKey, TagBytes);
                aes.Decrypt(nonce, payload.AsSpan(cipherOffset, CekBytes), payload.AsSpan(cipherOffset + CekBytes, TagBytes), cek);
                return cek;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(wrapKey);
            }
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public static byte[] Seal(byte[] plaintext, byte[] cek, byte[] aad)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var output = new byte[NonceBytes + plaintext.Length + TagBytes];
        nonce.CopyTo(output.AsSpan(0, NonceBytes));
        using var aes = new AesGcm(cek, TagBytes);
        aes.Encrypt(nonce, plaintext, output.AsSpan(NonceBytes, plaintext.Length),
            output.AsSpan(NonceBytes + plaintext.Length, TagBytes), aad);
        return output;
    }

    public static byte[]? Open(byte[] sealedBytes, byte[] cek, byte[] aad)
    {
        if (sealedBytes.Length < NonceBytes + TagBytes)
        {
            return null;
        }

        try
        {
            var plaintext = new byte[sealedBytes.Length - NonceBytes - TagBytes];
            using var aes = new AesGcm(cek, TagBytes);
            aes.Decrypt(sealedBytes.AsSpan(0, NonceBytes), sealedBytes.AsSpan(NonceBytes, plaintext.Length),
                sealedBytes.AsSpan(NonceBytes + plaintext.Length, TagBytes), plaintext, aad);
            return plaintext;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public static byte[] Hmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data)
    {
        return HMACSHA256.HashData(key, data);
    }
}
