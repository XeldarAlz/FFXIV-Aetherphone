using System.Security.Cryptography;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Crypto;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ConversationKeyStoreSecurityTests
{
    [Fact]
    public async Task DmMemberRestrictionDropsInjectedThirdParty()
    {
        using var victim = CryptoBox.TryGenerateIdentity()!;
        using var bob = CryptoBox.TryGenerateIdentity()!;
        using var attacker = CryptoBox.TryGenerateIdentity()!;
        var cek = CryptoBox.GenerateCek();

        var response = new ConversationKeysDto(
            "velvet-thread",
            1,
            new[] { new KeyWrapDto(1, CryptoBox.WrapCek(cek, CryptoBox.ExportPublicKey(victim))!, "victim", 1, 0) },
            new[]
            {
                new UserPublicKeyDto("victim", CryptoBox.ExportPublicKey(victim), 1),
                new UserPublicKeyDto("bob", CryptoBox.ExportPublicKey(bob), 1),
                new UserPublicKeyDto("evil", CryptoBox.ExportPublicKey(attacker), 1),
            },
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { "evil" },
            false);

        var client = new FakeKeysClient { VelvetResponse = response };
        var store = new ConversationKeyStore(client, new FakeVault(victim));
        await store.EnsureVelvetKeysAsync("bob", "victim", CancellationToken.None);

        Assert.DoesNotContain(client.VelvetWraps, wrap => wrap.RecipientUserId == "evil");
    }

    [Fact]
    public async Task GuardRosterRejectsInjectedRecipientInGroupChat()
    {
        using var victim = CryptoBox.TryGenerateIdentity()!;
        using var attacker = CryptoBox.TryGenerateIdentity()!;
        var cek = CryptoBox.GenerateCek();

        var response = new ConversationKeysDto(
            "42",
            1,
            new[] { new KeyWrapDto(1, CryptoBox.WrapCek(cek, CryptoBox.ExportPublicKey(victim))!, "victim", 1, 0) },
            new[] { new UserPublicKeyDto("evil", CryptoBox.ExportPublicKey(attacker), 1) },
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { "evil" },
            false);

        var guard = new PinnedRecipientGuard();
        guard.SetAuthorizedMembers("victim");
        var client = new FakeKeysClient { ChatResponse = response };
        var store = new ConversationKeyStore(client, new FakeVault(victim), guard);
        await store.EnsureChatKeysAsync("42", CancellationToken.None);

        Assert.Empty(client.ChatWraps);
    }
}

internal sealed class FakeVault : IKeyVault
{
    private readonly ECDiffieHellman privateKey;

    public FakeVault(ECDiffieHellman privateKey) => this.privateKey = privateKey;

    public event Action? Changed
    {
        add { }
        remove { }
    }

    public KeyVaultState State => KeyVaultState.Unlocked;

    public byte[]? UnwrapCek(string wrappedKey) => CryptoBox.UnwrapCek(wrappedKey, privateKey);
}

internal sealed class FakeKeysClient : IKeysClient
{
    public ConversationKeysDto? ChatResponse { get; set; }

    public ConversationKeysDto? VelvetResponse { get; set; }

    public List<NewWrapDto> ChatWraps { get; } = new();

    public List<NewWrapDto> VelvetWraps { get; } = new();

    public Task<ConversationKeysDto?> ConversationKeysAsync(string conversationId, CancellationToken token)
        => Task.FromResult(ChatResponse);

    public Task<bool> AddConversationWrapsAsync(string conversationId, AddWrapsRequest request, CancellationToken token)
    {
        ChatWraps.AddRange(request.Wraps);
        return Task.FromResult(true);
    }

    public Task<ConversationKeysDto?> VelvetThreadKeysAsync(string otherId, CancellationToken token)
        => Task.FromResult(VelvetResponse);

    public Task<bool> AddVelvetWrapsAsync(string otherId, AddWrapsRequest request, CancellationToken token)
    {
        VelvetWraps.AddRange(request.Wraps);
        return Task.FromResult(true);
    }

    public Task<MyKeysDto?> PutMyKeysAsync(PutMyKeysRequest request, CancellationToken token) => Task.FromResult<MyKeysDto?>(null);

    public Task<(MyKeysDto? Keys, int Status)> MyKeysAsync(CancellationToken token) => Task.FromResult<(MyKeysDto?, int)>((null, 0));

    public Task<PublicKeysDto?> PublicKeysAsync(string[] userIds, CancellationToken token) => Task.FromResult<PublicKeysDto?>(null);

    public Task<MyConversationKeysDto?> MyConversationKeysAsync(CancellationToken token) => Task.FromResult<MyConversationKeysDto?>(null);

    public Task<(bool Ok, int Status)> CreateConversationGenerationAsync(string conversationId, CreateGenerationRequest request, CancellationToken token) => Task.FromResult((false, 0));

    public Task<MyConversationKeysDto?> VelvetKeysAsync(CancellationToken token) => Task.FromResult<MyConversationKeysDto?>(null);

    public Task<(bool Ok, int Status)> CreateVelvetGenerationAsync(string otherId, CreateGenerationRequest request, CancellationToken token) => Task.FromResult((false, 0));

    public Task<MyConversationKeysDto?> GramKeysAsync(CancellationToken token) => Task.FromResult<MyConversationKeysDto?>(null);

    public Task<ConversationKeysDto?> GramThreadKeysAsync(string otherId, CancellationToken token) => Task.FromResult<ConversationKeysDto?>(null);

    public Task<(bool Ok, int Status)> CreateGramGenerationAsync(string otherId, CreateGenerationRequest request, CancellationToken token) => Task.FromResult((false, 0));

    public Task<bool> AddGramWrapsAsync(string otherId, AddWrapsRequest request, CancellationToken token) => Task.FromResult(true);
}
