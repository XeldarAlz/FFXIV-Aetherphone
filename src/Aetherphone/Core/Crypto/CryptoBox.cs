using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

namespace Aetherphone.Core.Crypto;

internal sealed class EcPrivateKey : IDisposable
{
    internal EcPrivateKey(ECPrivateKeyParameters privateKey, ECPublicKeyParameters publicKey)
    {
        PrivateKey = privateKey;
        PublicKey = publicKey;
    }

    internal ECPrivateKeyParameters PrivateKey { get; private set; }
    internal ECPublicKeyParameters PublicKey { get; private set; }

    public void Dispose()
    {
        PrivateKey = null!;
        PublicKey = null!;
    }
}

internal sealed class EcPublicKey : IDisposable
{
    internal EcPublicKey(ECPublicKeyParameters publicKey) => PublicKey = publicKey;

    internal ECPublicKeyParameters PublicKey { get; private set; }

    public void Dispose() => PublicKey = null!;
}

internal static class CryptoBox
{
    public const int CekBytes = 32;
    private const int NonceBytes = 12;
    private const int TagBytes = 16;
    private const string WrapPrefix = "EC1.";
    private static readonly byte[] WrapInfo = Encoding.UTF8.GetBytes("aethernet-cek-v1");

    private static readonly ECNamedDomainParameters P256 = CreateP256Domain();

    public static EcPrivateKey? TryGenerateIdentity()
    {
        try
        {
            var generator = new ECKeyPairGenerator();
            generator.Init(new ECKeyGenerationParameters(P256, new SecureRandom()));
            var pair = generator.GenerateKeyPair();
            return new EcPrivateKey((ECPrivateKeyParameters)pair.Private,
                (ECPublicKeyParameters)pair.Public);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static string ExportPublicKey(EcPrivateKey key)
    {
        return Convert.ToBase64String(
            SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(key.PublicKey).GetDerEncoded());
    }

    public static string? TryExportPublicKey(EcPrivateKey key)
    {
        try
        {
            return ExportPublicKey(key);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static EcPublicKey? ImportPublicKey(string publicKeyBase64)
    {
        try
        {
            var parsed = PublicKeyFactory.CreateKey(Convert.FromBase64String(publicKeyBase64));
            return parsed is ECPublicKeyParameters publicKey && IsP256(publicKey.Parameters)
                ? new EcPublicKey(publicKey)
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static byte[]? TryExportPrivateKey(EcPrivateKey key)
    {
        try
        {
            return PrivateKeyInfoFactory.CreatePrivateKeyInfo(key.PrivateKey).GetDerEncoded();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static EcPrivateKey? ImportPrivateKey(byte[] pkcs8)
    {
        try
        {
            var parsed = PrivateKeyFactory.CreateKey(pkcs8);
            if (parsed is not ECPrivateKeyParameters privateKey || !IsP256(privateKey.Parameters))
            {
                return null;
            }

            var publicPoint = privateKey.Parameters.G.Multiply(privateKey.D).Normalize();
            var publicKey = new ECPublicKeyParameters("ECDH", publicPoint, privateKey.Parameters);
            return new EcPrivateKey(privateKey, publicKey);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static ECNamedDomainParameters CreateP256Domain()
    {
        var parameters = SecNamedCurves.GetByOid(SecObjectIdentifiers.SecP256r1);
        return new ECNamedDomainParameters(SecObjectIdentifiers.SecP256r1, parameters.Curve, parameters.G,
            parameters.N, parameters.H, parameters.GetSeed());
    }

    private static bool IsP256(ECDomainParameters? parameters)
    {
        return parameters is not null
               && parameters.Curve.FieldSize == P256.Curve.FieldSize
               && parameters.N.Equals(P256.N)
               && parameters.H.Equals(P256.H)
               && parameters.G.Normalize().Equals(P256.G.Normalize());
    }

    private static byte[] DeriveRawSecret(EcPrivateKey privateKey, EcPublicKey publicKey)
    {
        var agreement = new ECDHBasicAgreement();
        agreement.Init(privateKey.PrivateKey);
        var raw = agreement.CalculateAgreement(publicKey.PublicKey).ToByteArrayUnsigned();
        var secret = new byte[agreement.GetFieldSize()];
        if (raw.Length > secret.Length)
        {
            throw new CryptographicException("ECDH agreement exceeded the P-256 field size.");
        }

        raw.CopyTo(secret, secret.Length - raw.Length);
        return secret;
    }

    public static byte[] GenerateCek()
    {
        return RandomNumberGenerator.GetBytes(CekBytes);
    }

    public static string? WrapCek(byte[] cek, string recipientPublicKeyBase64)
    {
        using var recipient = ImportPublicKey(recipientPublicKeyBase64);
        using var ephemeral = TryGenerateIdentity();
        if (recipient is null || ephemeral is null)
        {
            return null;
        }

        var shared = DeriveRawSecret(ephemeral, recipient);
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var wrapKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, CekBytes, nonce, WrapInfo);
        CryptographicOperations.ZeroMemory(shared);

        var ephemeralPublic = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(ephemeral.PublicKey)
            .GetDerEncoded();
        var payload = new byte[1 + ephemeralPublic.Length + NonceBytes + cek.Length + TagBytes];
        payload[0] = (byte)ephemeralPublic.Length;
        ephemeralPublic.CopyTo(payload.AsSpan(1));
        nonce.CopyTo(payload.AsSpan(1 + ephemeralPublic.Length));
        var cipherOffset = 1 + ephemeralPublic.Length + NonceBytes;
        using (var aes = new AesGcm(wrapKey, TagBytes))
        {
            aes.Encrypt(nonce, cek, payload.AsSpan(cipherOffset, cek.Length),
                payload.AsSpan(cipherOffset + cek.Length, TagBytes));
        }

        CryptographicOperations.ZeroMemory(wrapKey);
        return WrapPrefix + Convert.ToBase64String(payload);
    }

    public static byte[]? UnwrapCek(string wrappedKey, EcPrivateKey privateKey)
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
            var parsed = PublicKeyFactory.CreateKey(payload.AsSpan(1, ephemeralLength).ToArray());
            if (parsed is not ECPublicKeyParameters publicKey || !IsP256(publicKey.Parameters))
            {
                return null;
            }

            using var ephemeral = new EcPublicKey(publicKey);
            var shared = DeriveRawSecret(privateKey, ephemeral);
            var nonce = payload.AsSpan(1 + ephemeralLength, NonceBytes).ToArray();
            var wrapKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, CekBytes, nonce, WrapInfo);
            CryptographicOperations.ZeroMemory(shared);

            var cek = new byte[CekBytes];
            try
            {
                using var aes = new AesGcm(wrapKey, TagBytes);
                aes.Decrypt(nonce, payload.AsSpan(cipherOffset, CekBytes),
                    payload.AsSpan(cipherOffset + CekBytes, TagBytes), cek);
                return cek;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(wrapKey);
            }
        }
        catch (Exception)
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
