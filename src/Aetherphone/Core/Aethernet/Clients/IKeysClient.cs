using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Aethernet.Clients;

internal interface IKeysClient
{
    Task<MyKeysDto?> PutMyKeysAsync(PutMyKeysRequest request, CancellationToken token);

    Task<(MyKeysDto? Keys, int Status)> MyKeysAsync(CancellationToken token);

    Task<PublicKeysDto?> PublicKeysAsync(string[] userIds, CancellationToken token);

    Task<MyConversationKeysDto?> MyConversationKeysAsync(CancellationToken token);

    Task<ConversationKeysDto?> ConversationKeysAsync(string conversationId, CancellationToken token);

    Task<(bool Ok, int Status)> CreateConversationGenerationAsync(string conversationId, CreateGenerationRequest request, CancellationToken token);

    Task<bool> AddConversationWrapsAsync(string conversationId, AddWrapsRequest request, CancellationToken token);

    Task<MyConversationKeysDto?> VelvetKeysAsync(CancellationToken token);

    Task<ConversationKeysDto?> VelvetThreadKeysAsync(string otherId, CancellationToken token);

    Task<(bool Ok, int Status)> CreateVelvetGenerationAsync(string otherId, CreateGenerationRequest request, CancellationToken token);

    Task<bool> AddVelvetWrapsAsync(string otherId, AddWrapsRequest request, CancellationToken token);

    Task<MyConversationKeysDto?> GramKeysAsync(CancellationToken token);

    Task<ConversationKeysDto?> GramThreadKeysAsync(string otherId, CancellationToken token);

    Task<(bool Ok, int Status)> CreateGramGenerationAsync(string otherId, CreateGenerationRequest request, CancellationToken token);

    Task<bool> AddGramWrapsAsync(string otherId, AddWrapsRequest request, CancellationToken token);
}
