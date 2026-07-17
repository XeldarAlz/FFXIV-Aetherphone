using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Social;

internal sealed class MentionSuggestions
{
    private static readonly MentionSuggestDto[] NoResults = Array.Empty<MentionSuggestDto>();

    private readonly AccountClient client;
    private readonly StoreWork work;
    private long generation;

    public MentionSuggestions(AccountClient client, StoreWork work)
    {
        this.client = client;
        this.work = work;
    }

    public MentionSuggestDto[] Results { get; private set; } = NoResults;

    public bool Loading { get; private set; }

    public string Query { get; private set; } = string.Empty;

    public void Request(string query)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0)
        {
            Clear();
            return;
        }

        if (string.Equals(trimmed, Query, StringComparison.Ordinal))
        {
            return;
        }

        Query = trimmed;
        var ticket = Interlocked.Increment(ref generation);
        Loading = true;
        work.Run(
            "mention suggest",
            async token =>
            {
                var result = await client.MentionSuggestAsync(trimmed, token).ConfigureAwait(false);
                if (Interlocked.Read(ref generation) != ticket)
                {
                    return;
                }

                Results = result?.Users ?? NoResults;
            },
            () =>
            {
                if (Interlocked.Read(ref generation) == ticket)
                {
                    Loading = false;
                }
            });
    }

    public void Clear()
    {
        Interlocked.Increment(ref generation);
        Query = string.Empty;
        Results = NoResults;
        Loading = false;
    }
}
