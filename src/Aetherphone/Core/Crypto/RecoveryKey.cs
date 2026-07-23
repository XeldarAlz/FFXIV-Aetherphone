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

    public const int MinPassphraseChars = 8;
    private const int Argon2MemoryKb = 65_536;
    private const int Argon2Iterations = 3;
    private const int Argon2Parallelism = 1;

    public static WrappedPrivateKeyDto? Wrap(byte[] pkcs8, string code)
    {
        var canonical = Canonicalize(code);
        if (canonical.Length == 0)
        {
            return null;
        }

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var wrapKey = DerivePbkdf2(canonical, salt, Iterations);
        return SealWrapKey(pkcs8, wrapKey, salt, EscrowKinds.RecoveryCode, Iterations, 0, 0);
    }

    public static WrappedPrivateKeyDto? WrapPassphrase(byte[] pkcs8, string passphrase)
    {
        var trimmed = passphrase.Trim();
        if (trimmed.Length < MinPassphraseChars)
        {
            return null;
        }

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var wrapKey = DeriveArgon2(trimmed, salt, Argon2MemoryKb, Argon2Iterations, Argon2Parallelism);
        return SealWrapKey(pkcs8, wrapKey, salt, EscrowKinds.Argon2Passphrase,
            Argon2Iterations, Argon2MemoryKb, Argon2Parallelism);
    }

    private static WrappedPrivateKeyDto SealWrapKey(byte[] pkcs8, byte[] wrapKey, byte[] salt, int kind,
        int iterations, int memoryKb, int parallelism)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
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
            iterations,
            Convert.ToBase64String(nonce),
            Convert.ToBase64String(cipher),
            kind,
            memoryKb,
            parallelism);
    }

    public static byte[]? Unwrap(WrappedPrivateKeyDto escrow, string code)
    {
        if (escrow.Kind != EscrowKinds.RecoveryCode)
        {
            return null;
        }

        var canonical = Canonicalize(code);
        return canonical.Length == 0 ? null : UnwrapSecret(escrow, canonical);
    }

    public static byte[]? UnwrapPassphrase(WrappedPrivateKeyDto escrow, string passphrase)
    {
        if (escrow.Kind is not (EscrowKinds.Passphrase or EscrowKinds.Argon2Passphrase))
        {
            return null;
        }

        var trimmed = passphrase.Trim();
        return trimmed.Length == 0 ? null : UnwrapSecret(escrow, trimmed);
    }

    private static byte[]? UnwrapSecret(WrappedPrivateKeyDto escrow, string secret)
    {
        if (escrow.Iterations <= 0)
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

        var wrapKey = escrow.Kind == EscrowKinds.Argon2Passphrase
            ? DeriveArgon2(secret, salt, escrow.MemoryKb, escrow.Iterations, escrow.Parallelism)
            : DerivePbkdf2(secret, salt, escrow.Iterations);
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

    private static byte[] DerivePbkdf2(string secret, byte[] salt, int iterations)
    {
        var password = Encoding.UTF8.GetBytes(secret);
        try
        {
            return Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, WrapKeyBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(password);
        }
    }

    private static byte[] DeriveArgon2(string secret, byte[] salt, int memoryKb, int iterations, int parallelism)
    {
        var password = Encoding.UTF8.GetBytes(secret);
        try
        {
            using var argon2 = new Konscious.Security.Cryptography.Argon2id(password)
            {
                Salt = salt,
                MemorySize = memoryKb,
                Iterations = iterations,
                DegreeOfParallelism = parallelism,
            };
            return argon2.GetBytes(WrapKeyBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(password);
        }
    }
}
