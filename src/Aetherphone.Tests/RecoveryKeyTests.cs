using System.Security.Cryptography;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Crypto;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RecoveryKeyTests
{
    [Fact]
    public void GeneratedCodeHasExpectedShape()
    {
        var code = RecoveryKey.GenerateCode();

        var groups = code.Split('-');
        Assert.Equal(5, groups.Length);
        for (var index = 0; index < groups.Length; index++)
        {
            Assert.Equal(4, groups[index].Length);
        }

        Assert.DoesNotContain('I', code);
        Assert.DoesNotContain('L', code);
        Assert.DoesNotContain('O', code);
        Assert.DoesNotContain('U', code);
    }

    [Fact]
    public void WrapUnwrapRoundTripsWithSameCode()
    {
        var identity = CryptoBox.TryGenerateIdentity();
        Assert.NotNull(identity);
        using var owned = identity;
        var pkcs8 = CryptoBox.TryExportPrivateKey(identity);
        Assert.NotNull(pkcs8);
        var code = RecoveryKey.GenerateCode();

        var escrow = RecoveryKey.Wrap(pkcs8, code);
        Assert.NotNull(escrow);

        var recovered = RecoveryKey.Unwrap(escrow, code);
        Assert.NotNull(recovered);
        Assert.Equal(pkcs8, recovered);

        using var imported = CryptoBox.ImportPrivateKey(recovered);
        Assert.NotNull(imported);
        Assert.Equal(CryptoBox.ExportPublicKey(identity), CryptoBox.ExportPublicKey(imported));
    }

    [Fact]
    public void WrongCodeFailsToUnwrap()
    {
        var pkcs8 = CryptoBox.TryExportPrivateKey(CryptoBox.TryGenerateIdentity()!);
        Assert.NotNull(pkcs8);

        var escrow = RecoveryKey.Wrap(pkcs8, RecoveryKey.GenerateCode());
        Assert.NotNull(escrow);

        Assert.Null(RecoveryKey.Unwrap(escrow, RecoveryKey.GenerateCode()));
    }

    [Fact]
    public void UnwrapToleratesFormattingAndAmbiguousCharacters()
    {
        var pkcs8 = CryptoBox.TryExportPrivateKey(CryptoBox.TryGenerateIdentity()!);
        Assert.NotNull(pkcs8);
        var escrow = RecoveryKey.Wrap(pkcs8, "ABCD-2345-6789-JKMN-PQRS");
        Assert.NotNull(escrow);

        var recovered = RecoveryKey.Unwrap(escrow, "abcd 2345 6789 jkmn pqrs");
        Assert.NotNull(recovered);
        Assert.Equal(pkcs8, recovered);
    }

    [Fact]
    public void CanonicalizeMapsAmbiguousCharacters()
    {
        Assert.Equal("0111VV", RecoveryKey.Canonicalize("oIlL-uV"));
    }

    [Fact]
    public void EscrowFieldsAreWellFormedForServerBounds()
    {
        var pkcs8 = CryptoBox.TryExportPrivateKey(CryptoBox.TryGenerateIdentity()!);
        Assert.NotNull(pkcs8);
        var escrow = RecoveryKey.Wrap(pkcs8, RecoveryKey.GenerateCode());
        Assert.NotNull(escrow);

        Assert.Equal(16, escrow.Nonce.Length);
        Assert.InRange(escrow.Salt.Length, 1, 88);
        Assert.InRange(escrow.Ciphertext.Length, 1, 4096);
        Assert.InRange(escrow.Iterations, 100_000, 2_000_000);
    }

    [Fact]
    public void CodeEscrowCarriesRecoveryCodeKind()
    {
        var pkcs8 = CryptoBox.TryExportPrivateKey(CryptoBox.TryGenerateIdentity()!);
        Assert.NotNull(pkcs8);

        var escrow = RecoveryKey.Wrap(pkcs8, RecoveryKey.GenerateCode());
        Assert.NotNull(escrow);
        Assert.Equal(EscrowKinds.RecoveryCode, escrow.Kind);
    }

    [Fact]
    public void PassphraseWrapUnwrapRoundTrips()
    {
        var identity = CryptoBox.TryGenerateIdentity();
        Assert.NotNull(identity);
        using var owned = identity;
        var pkcs8 = CryptoBox.TryExportPrivateKey(identity);
        Assert.NotNull(pkcs8);

        var escrow = RecoveryKey.WrapPassphrase(pkcs8, "correct horse battery staple");
        Assert.NotNull(escrow);
        Assert.Equal(EscrowKinds.Argon2Passphrase, escrow.Kind);

        var recovered = RecoveryKey.UnwrapPassphrase(escrow, "correct horse battery staple");
        Assert.NotNull(recovered);
        Assert.Equal(pkcs8, recovered);

        Assert.Null(RecoveryKey.UnwrapPassphrase(escrow, "wrong passphrase entirely"));
    }

    [Fact]
    public void PassphraseWrapUsesArgon2Params()
    {
        var pkcs8 = CryptoBox.TryExportPrivateKey(CryptoBox.TryGenerateIdentity()!);
        Assert.NotNull(pkcs8);

        var escrow = RecoveryKey.WrapPassphrase(pkcs8, "a memory hard passphrase");
        Assert.NotNull(escrow);
        Assert.Equal(EscrowKinds.Argon2Passphrase, escrow.Kind);
        Assert.Equal(65_536, escrow.MemoryKb);
        Assert.Equal(3, escrow.Iterations);
        Assert.Equal(1, escrow.Parallelism);
    }

    [Fact]
    public void LegacyPbkdf2PassphraseEscrowStillUnwraps()
    {
        var pkcs8 = CryptoBox.TryExportPrivateKey(CryptoBox.TryGenerateIdentity()!);
        Assert.NotNull(pkcs8);

        // Reproduce a pre-Argon2 escrow: Kind=Passphrase with PBKDF2 parameters, as older clients wrote.
        using var derived = new Rfc2898DeriveBytes("legacy passphrase here",
            Convert.FromBase64String("AAAAAAAAAAAAAAAAAAAAAA=="), 600_000, HashAlgorithmName.SHA256);
        var wrapKey = derived.GetBytes(32);
        var nonce = new byte[12];
        var cipher = new byte[pkcs8.Length + 16];
        using (var aes = new AesGcm(wrapKey, 16))
        {
            aes.Encrypt(nonce, pkcs8, cipher.AsSpan(0, pkcs8.Length), cipher.AsSpan(pkcs8.Length, 16));
        }

        var legacy = new WrappedPrivateKeyDto(
            "AAAAAAAAAAAAAAAAAAAAAA==", 600_000, Convert.ToBase64String(nonce),
            Convert.ToBase64String(cipher), EscrowKinds.Passphrase);

        var recovered = RecoveryKey.UnwrapPassphrase(legacy, "legacy passphrase here");
        Assert.NotNull(recovered);
        Assert.Equal(pkcs8, recovered);
    }

    [Fact]
    public void PassphraseTrimsButPreservesCase()
    {
        var pkcs8 = CryptoBox.TryExportPrivateKey(CryptoBox.TryGenerateIdentity()!);
        Assert.NotNull(pkcs8);

        var escrow = RecoveryKey.WrapPassphrase(pkcs8, "  Sarissa Dances  ");
        Assert.NotNull(escrow);

        Assert.NotNull(RecoveryKey.UnwrapPassphrase(escrow, "Sarissa Dances"));
        Assert.Null(RecoveryKey.UnwrapPassphrase(escrow, "sarissa dances"));
    }

    [Fact]
    public void PassphraseRejectsShortSecrets()
    {
        var pkcs8 = CryptoBox.TryExportPrivateKey(CryptoBox.TryGenerateIdentity()!);
        Assert.NotNull(pkcs8);

        Assert.Null(RecoveryKey.WrapPassphrase(pkcs8, "short"));
    }

    [Fact]
    public void KindMismatchRefusesToUnwrap()
    {
        var pkcs8 = CryptoBox.TryExportPrivateKey(CryptoBox.TryGenerateIdentity()!);
        Assert.NotNull(pkcs8);

        var codeEscrow = RecoveryKey.Wrap(pkcs8, "ABCD-2345-6789-JKMN-PQRS");
        Assert.NotNull(codeEscrow);
        Assert.Null(RecoveryKey.UnwrapPassphrase(codeEscrow, "ABCD-2345-6789-JKMN-PQRS"));

        var passphraseEscrow = RecoveryKey.WrapPassphrase(pkcs8, "a long enough passphrase");
        Assert.NotNull(passphraseEscrow);
        Assert.Null(RecoveryKey.Unwrap(passphraseEscrow, "a long enough passphrase"));
    }

    [Fact]
    public void LegacyEscrowWithoutKindUnwrapsAsRecoveryCode()
    {
        var pkcs8 = CryptoBox.TryExportPrivateKey(CryptoBox.TryGenerateIdentity()!);
        Assert.NotNull(pkcs8);
        var code = "ABCD-2345-6789-JKMN-PQRS";
        var written = RecoveryKey.Wrap(pkcs8, code);
        Assert.NotNull(written);

        var legacy = new Aetherphone.Core.Aethernet.Contracts.WrappedPrivateKeyDto(
            written.Salt, written.Iterations, written.Nonce, written.Ciphertext);
        Assert.Equal(EscrowKinds.RecoveryCode, legacy.Kind);
        Assert.NotNull(RecoveryKey.Unwrap(legacy, code));
    }
}
