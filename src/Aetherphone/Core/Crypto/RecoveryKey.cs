using System.Security.Cryptography;
using System.Text;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Crypto;

internal static class RecoveryKey
{
    private const int SaltBytes = 16;
    private const int NonceBytes = 12;
    private const int TagBytes = 16;
    private const int WrapKeyBytes = 32;
    private const int Iterations = 600_000;
    private const int CodeChars = 20;
    private const int GroupSize = 4;
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string GenerateCode()
    {
        var length = CodeChars + (CodeChars / GroupSize) - 1;
        var chars = new char[length];
        var cursor = 0;
        for (var position = 0; position < CodeChars; position++)
        {
            if (position > 0 && position % GroupSize == 0)
            {
                chars[cursor++] = '-';
            }

            chars[cursor++] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(chars);
    }

    public static string Canonicalize(string code)
    {
        var builder = new StringBuilder(CodeChars);
        for (var index = 0; index < code.Length; index++)
        {
            var upper = char.ToUpperInvariant(code[index]);
            switch (upper)
            {
                case 'O':
                    builder.Append('0');
                    break;
                case 'I':
                case 'L':
                    builder.Append('1');
                    break;
                case 'U':
                    builder.Append('V');
                    break;
                default:
                    if (Alphabet.IndexOf(upper) >= 0)
                    {
                        builder.Append(upper);
                    }

                    break;
            }
        }

        return builder.ToString();
    }

    public static WrappedPrivateKeyDto? Wrap(byte[] pkcs8, string code)
    {
        var canonical = Canonicalize(code);
        if (canonical.Length == 0)
        {
            return null;
        }

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var wrapKey = DeriveKey(canonical, salt, Iterations);
        var cipher = new byte[pkcs8.Length + TagBytes];
        try
        {
            using var aes = new AesGcm(wrapKey, TagBytes);
            aes.Encrypt(nonce, pkcs8, cipher.AsSpan(0, pkcs8.Length), cipher.AsSpan(pkcs8.Length, TagBytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(wrapKey);
        }

        return new WrappedPrivateKeyDto(
            Convert.ToBase64String(salt),
            Iterations,
            Convert.ToBase64String(nonce),
            Convert.ToBase64String(cipher));
    }

    public static byte[]? Unwrap(WrappedPrivateKeyDto escrow, string code)
    {
        var canonical = Canonicalize(code);
        if (canonical.Length == 0 || escrow.Iterations <= 0)
        {
            return null;
        }

        byte[] salt;
        byte[] nonce;
        byte[] cipher;
        try
        {
            salt = Convert.FromBase64String(escrow.Salt);
            nonce = Convert.FromBase64String(escrow.Nonce);
            cipher = Convert.FromBase64String(escrow.Ciphertext);
        }
        catch (FormatException)
        {
            return null;
        }

        if (nonce.Length != NonceBytes || cipher.Length < TagBytes)
        {
            return null;
        }

        var wrapKey = DeriveKey(canonical, salt, escrow.Iterations);
        var plaintext = new byte[cipher.Length - TagBytes];
        try
        {
            using var aes = new AesGcm(wrapKey, TagBytes);
            aes.Decrypt(nonce, cipher.AsSpan(0, plaintext.Length), cipher.AsSpan(plaintext.Length, TagBytes), plaintext);
            return plaintext;
        }
        catch (CryptographicException)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            return null;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(wrapKey);
        }
    }

    private static byte[] DeriveKey(string canonicalCode, byte[] salt, int iterations)
    {
        var password = Encoding.UTF8.GetBytes(canonicalCode);
        try
        {
            return Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, WrapKeyBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(password);
        }
    }
}
