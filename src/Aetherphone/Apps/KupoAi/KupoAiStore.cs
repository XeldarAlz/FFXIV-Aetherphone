using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Apps.KupoAi;

internal sealed class KupoAiStore : IDisposable
{
    private const int MaxHistoryTurns = 8;
    public const int MaxQuestionChars = 500;
    private const int TitleMaxChars = 60;

    private readonly AethernetSession session;
    private readonly AssistantClient client;
    private readonly KupoAiArchive archive;
    private readonly StoreWork work = new StoreWork("KupoAI");
    private readonly object gate = new();

    private volatile List<KupoAiConversation> conversations = new();
    private volatile bool loaded;
    private volatile bool loading;
    private volatile bool asking;
    private volatile bool ready = true;
    private volatile int remainingToday = -1;
    private volatile int dailyLimit;
    private int version;

    public KupoAiStore(AethernetSession session, AssistantClient client, KupoAiArchive archive)
    {
        this.session = session;
        this.client = client;
        this.archive = archive;
    }

    public bool IsSignedIn => session.IsSignedIn;

    public bool Loaded => loaded;

    public bool Asking => asking;

    public bool Ready => ready;

    public int RemainingToday => remainingToday;

    public int DailyLimit => dailyLimit;

    public int Version => Volatile.Read(ref version);

    public IReadOnlyList<KupoAiConversation> Conversations => conversations;

    public void EnsureLoaded()
    {
        if (loaded || loading)
        {
            return;
        }

        loading = true;
        work.Run("load", token =>
        {
            var stored = archive.LoadAll();
            lock (gate)
            {
                conversations = stored;
            }

            loaded = true;
            Bump();
            return Task.CompletedTask;
        }, () => loading = false);
    }

    public void RefreshStatus()
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        work.Run("status", async token =>
        {
            var status = await client.StatusAsync(token).ConfigureAwait(false);
            if (status is null)
            {
                return;
            }

            ready = status.Ready;
            remainingToday = status.RemainingToday;
            dailyLimit = status.DailyLimit;
            Bump();
        });
    }

    public KupoAiConversation NewConversation()
    {
        var conversation = new KupoAiConversation
        {
            UpdatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };
        lock (gate)
        {
            var next = new List<KupoAiConversation>(conversations.Count + 1) { conversation };
            next.AddRange(conversations);
            conversations = next;
        }

        Bump();
        return conversation;
    }

    public void DeleteConversation(string conversationId)
    {
        lock (gate)
        {
            var next = new List<KupoAiConversation>(conversations.Count);
            for (var index = 0; index < conversations.Count; index++)
            {
                if (conversations[index].Id != conversationId)
                {
                    next.Add(conversations[index]);
                }
            }

            conversations = next;
        }

        archive.Delete(conversationId);
        Bump();
    }

    public KupoAiMessage[] SnapshotMessages(KupoAiConversation conversation)
    {
        lock (gate)
        {
            return conversation.Messages.ToArray();
        }
    }

    public void Ask(KupoAiConversation conversation, string question)
    {
        var trimmed = (question ?? string.Empty).Trim();
        if (trimmed.Length == 0 || asking || !session.IsSignedIn)
        {
            return;
        }

        if (trimmed.Length > MaxQuestionChars)
        {
            trimmed = trimmed[..MaxQuestionChars];
        }

        AssistantTurnDto[] history;
        lock (gate)
        {
            history = BuildHistory(conversation);
            conversation.Messages.Add(new KupoAiMessage
            {
                Role = KupoAiRoles.User,
                Text = trimmed,
                AtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            });
            if (conversation.Title.Length == 0)
            {
                conversation.Title = trimmed.Length > TitleMaxChars ? trimmed[..TitleMaxChars] : trimmed;
            }

            conversation.UpdatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        archive.Save(conversation);
        Bump();

        asking = true;
        var capturedStatus = 0;
        work.Run("ask", async token =>
        {
            var response = await client
                .AskAsync(new AssistantAskRequest(trimmed, history, conversation.Id), token, status => capturedStatus = status)
                .ConfigureAwait(false);
            CompleteAsk(conversation, response, capturedStatus);
        }, () => asking = false);
    }

    private void CompleteAsk(KupoAiConversation conversation, AssistantAskResponse? response, int httpStatus)
    {
        KupoAiMessage message;
        if (response is null)
        {
            message = new KupoAiMessage
            {
                Role = KupoAiRoles.System,
                Text = httpStatus == 429 ? KupoAiNotes.RateLimited : KupoAiNotes.Offline,
                AtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
        }
        else
        {
            remainingToday = response.RemainingToday;
            dailyLimit = response.DailyLimit;
            message = response.Status switch
            {
                "ok" => new KupoAiMessage
                {
                    Role = KupoAiRoles.Assistant,
                    Text = response.Answer ?? string.Empty,
                    SourceTitles = SourceTitles(response.Sources),
                    SourceUrls = SourceUrls(response.Sources),
                    AtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                },
                "no_match" => Note(KupoAiNotes.NoMatch),
                "quota" => Note(KupoAiNotes.Quota),
                "global_quota" => Note(KupoAiNotes.GlobalQuota),
                "indexing" => Note(KupoAiNotes.Indexing),
                _ => Note(KupoAiNotes.Error),
            };
        }

        lock (gate)
        {
            conversation.Messages.Add(message);
            conversation.UpdatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        archive.Save(conversation);
        Bump();
    }

    private static KupoAiMessage Note(string note)
    {
        return new KupoAiMessage
        {
            Role = KupoAiRoles.System,
            Text = note,
            AtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };
    }

    private static string[] SourceTitles(AssistantSourceDto[] sources)
    {
        var titles = new string[sources.Length];
        for (var index = 0; index < sources.Length; index++)
        {
            titles[index] = sources[index].Title;
        }

        return titles;
    }

    private static string[] SourceUrls(AssistantSourceDto[] sources)
    {
        var urls = new string[sources.Length];
        for (var index = 0; index < sources.Length; index++)
        {
            urls[index] = sources[index].Url;
        }

        return urls;
    }

    private static AssistantTurnDto[] BuildHistory(KupoAiConversation conversation)
    {
        var turns = new List<AssistantTurnDto>(MaxHistoryTurns);
        for (var index = conversation.Messages.Count - 1; index >= 0 && turns.Count < MaxHistoryTurns; index--)
        {
            var message = conversation.Messages[index];
            if (message.Role == KupoAiRoles.User)
            {
                turns.Add(new AssistantTurnDto("user", message.Text));
            }
            else if (message.Role == KupoAiRoles.Assistant)
            {
                turns.Add(new AssistantTurnDto("assistant", message.Text));
            }
        }

        turns.Reverse();
        return turns.ToArray();
    }

    private void Bump()
    {
        Interlocked.Increment(ref version);
    }

    public void Dispose()
    {
        work.Dispose();
    }
}
