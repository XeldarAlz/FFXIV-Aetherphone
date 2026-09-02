using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Apps.KindKupo;

internal sealed class KindKupoStore : IDisposable
{
    private readonly AethernetSession session;
    private readonly KupoClient client;
    private readonly StoreWork work = new("KindKupo");

    private volatile ConfessionDto[] confessions = Array.Empty<ConfessionDto>();
    private volatile ConfessionDto[] userConfessions = Array.Empty<ConfessionDto>();
    private volatile string? cursor;
    private volatile string? userCursor;
    private volatile string? activeUserId;
    private volatile bool loading;
    private volatile bool loadingMore;
    private volatile bool userLoading;

    public KindKupoStore(AethernetSession session, KupoClient client)
    {
        this.session = session;
        this.client = client;
    }

    public bool IsSignedIn => session.IsSignedIn;

    public ConfessionDto[] Confessions => confessions;

    public ConfessionDto[] UserConfessions => userConfessions;

    public bool Loading => loading;

    public bool UserLoading => userLoading;

    public bool HasMoreConfessions => cursor is not null;

    public bool HasMoreUserConfessions => userCursor is not null;

    public void Refresh()
    {
        if (loading)
        {
            return;
        }

        loading = true;
        work.Run("confessions refresh", async token =>
        {
            var page = await client.FeedAsync(null, token).ConfigureAwait(false);
            if (page is null)
            {
                return;
            }

            confessions = page.Items;
            cursor = page.NextCursor;
        }, () => loading = false);
    }

    public void LoadMore()
    {
        if (cursor is null || loadingMore || loading)
        {
            return;
        }

        loadingMore = true;
        work.Run("confessions page", async token =>
        {
            var page = await client.FeedAsync(cursor, token).ConfigureAwait(false);
            if (page is null)
            {
                return;
            }

            confessions = [..confessions, ..page.Items];
            cursor = page.NextCursor;
        }, () => loadingMore = false);
    }

    public void FetchUserConfessions(string userId)
    {
        if (userId.Length == 0)
        {
            return;
        }

        activeUserId = userId;
        userConfessions = Array.Empty<ConfessionDto>();
        userCursor = null;
        userLoading = true;

        work.Run("user confessions refresh", async token =>
        {
            var page = await client.MyConfessionsAsync(userId, null, token).ConfigureAwait(false);
            if (page is null || !string.Equals(activeUserId, userId, StringComparison.Ordinal))
            {
                return;
            }

            userConfessions = page.Items;
            userCursor = page.NextCursor;
        }, () =>
        {
            if (string.Equals(activeUserId, userId, StringComparison.Ordinal))
            {
                userLoading = false;
            }
        });
    }

    public void ComposeConfession(string text, long expiresAtUnix, Action<bool> onComplete)
    {
        work.Run("compose confession", async token =>
        {
            var created = await client.CreateConfessionAsync(text, expiresAtUnix, token).ConfigureAwait(false);
            if (created is null)
            {
                return false;
            }

            confessions = [created, ..confessions];
            userConfessions = [created, ..userConfessions];
            return true;
        }, onComplete);
    }

    public void SubmitResponse(string confessionId, string text, Action<bool> onComplete)
    {
        work.Run("submit response", async token =>
        {
            var created = await client.RespondAsync(confessionId, text, token).ConfigureAwait(false);
            if (created is null)
            {
                return false;
            }

            confessions = WithResponse(confessions, confessionId, created);
            userConfessions = WithResponse(userConfessions, confessionId, created);
            return true;
        }, onComplete);
    }

    public ConfessionDto? FindConfession(string confessionId)
    {
        var found = FindIn(confessions, confessionId);
        return found ?? FindIn(userConfessions, confessionId);
    }

    public void Dispose() => work.Dispose();

    private static ConfessionDto? FindIn(ConfessionDto[] source, string confessionId)
    {
        for (var index = 0; index < source.Length; index++)
        {
            if (string.Equals(source[index].Id, confessionId, StringComparison.Ordinal))
            {
                return source[index];
            }
        }

        return null;
    }

    private static ConfessionDto[] WithResponse(ConfessionDto[] source, string confessionId,
        ConfessionResponseDto response)
    {
        for (var index = 0; index < source.Length; index++)
        {
            var current = source[index];
            if (!string.Equals(current.Id, confessionId, StringComparison.Ordinal))
            {
                continue;
            }

            var responses = new ConfessionResponseDto[current.Responses.Length + 1];
            Array.Copy(current.Responses, responses, current.Responses.Length);
            responses[^1] = response;

            var updated = new ConfessionDto[source.Length];
            Array.Copy(source, updated, source.Length);
            updated[index] = current with
            {
                ResponseCount = current.ResponseCount + 1,
                Responses = responses,
            };
            return updated;
        }

        return source;
    }
}
