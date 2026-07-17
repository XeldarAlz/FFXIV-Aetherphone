using System.Collections.Concurrent;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Media;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Dev;

internal sealed class DevStore : IDisposable
{
    public const int ColumnCount = 3;
    private const int ImageMaxDimension = 1280;
    private const long MediaUrlExpiryMarginSeconds = 120;
    private static readonly TimeSpan AccessProbeInterval = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan BackgroundChatInterval = TimeSpan.FromSeconds(30);

    private readonly AethernetSession session;
    private readonly DevClient client;
    private readonly AccountClient account;
    private readonly MediaClient media;
    private readonly Configuration configuration;
    private readonly StoreWork work = new StoreWork("Dev");
    private readonly object messagesLock = new();
    private readonly ConcurrentDictionary<string, DevMediaUrlDto> mediaUrls = new();
    private readonly ConcurrentDictionary<string, byte> mediaLoading = new();
    private readonly DevBoardCardDto[][] columns =
    {
        Array.Empty<DevBoardCardDto>(), Array.Empty<DevBoardCardDto>(), Array.Empty<DevBoardCardDto>(),
    };

    private volatile DevChatMessageDto[] messages = Array.Empty<DevChatMessageDto>();
    private volatile string? olderCursor;
    private volatile bool loadingOlder;
    private volatile bool hasMoreOlder;
    private volatile bool loadingBoard;
    private volatile bool boardLoaded;
    private volatile bool loadingChat;
    private volatile bool chatLoaded;
    private volatile bool sending;
    private volatile bool cardBusy;
    private volatile bool probing;
    private volatile bool accessSettled;
    private volatile bool pollingChat;
    private DateTime lastProbeUtc = DateTime.MinValue;
    private DateTime lastBackgroundChatUtc = DateTime.MinValue;

    public DevStore(AethernetSession session, DevClient client, AccountClient account, MediaClient media, Configuration configuration)
    {
        this.session = session;
        this.client = client;
        this.account = account;
        this.media = media;
        this.configuration = configuration;
        session.Changed += OnSessionChanged;
        Plugin.Framework.Update += OnFrameworkTick;
    }

    public DevChatMessageDto[] Messages => messages;
    public bool LoadingOlder => loadingOlder;
    public bool HasMoreOlder => hasMoreOlder;
    public bool LoadingBoard => loadingBoard;
    public bool BoardLoaded => boardLoaded;
    public bool LoadingChat => loadingChat;
    public bool ChatLoaded => chatLoaded;
    public bool Sending => sending;
    public bool CardBusy => cardBusy;

    public DevBoardCardDto[] Column(int status) =>
        status >= 0 && status < ColumnCount ? columns[status] : Array.Empty<DevBoardCardDto>();

    public DevBoardCardDto? FindCard(string cardId)
    {
        for (var status = 0; status < ColumnCount; status++)
        {
            var column = columns[status];
            for (var index = 0; index < column.Length; index++)
            {
                if (string.Equals(column[index].Id, cardId, StringComparison.Ordinal))
                {
                    return column[index];
                }
            }
        }

        return null;
    }

    public int UnreadCount
    {
        get
        {
            var snapshot = messages;
            var lastSeen = configuration.DevChatLastSeenUnix;
            var myId = session.CurrentUser?.Id;
            var total = 0;
            for (var index = 0; index < snapshot.Length; index++)
            {
                if (snapshot[index].CreatedAtUnix > lastSeen &&
                    !string.Equals(snapshot[index].SenderId, myId, StringComparison.Ordinal))
                {
                    total++;
                }
            }

            return total;
        }
    }

    private void OnSessionChanged()
    {
        accessSettled = false;
        lastProbeUtc = DateTime.MinValue;
    }

    private void OnFrameworkTick(IFramework framework)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (!session.HasDevAccess)
        {
            if (accessSettled || now - lastProbeUtc < AccessProbeInterval)
            {
                return;
            }

            lastProbeUtc = now;
            ProbeAccess();
            return;
        }

        if (now - lastBackgroundChatUtc < BackgroundChatInterval)
        {
            return;
        }

        lastBackgroundChatUtc = now;
        account.EnsureCurrentUser();
        PollChat();
    }

    private void ProbeAccess()
    {
        if (probing)
        {
            return;
        }

        probing = true;
        work.Run("access probe", async token =>
        {
            var granted = await client.AccessAsync(token).ConfigureAwait(false);
            if (granted is { } value)
            {
                accessSettled = true;
                session.SetDevAccess(value);
            }
        }, () => probing = false);
    }

    public void EnsureLoaded()
    {
        if (!boardLoaded && !loadingBoard)
        {
            RefreshBoard();
        }

        if (!chatLoaded && !loadingChat)
        {
            RefreshChat();
        }
    }

    public void RefreshBoard()
    {
        if (loadingBoard)
        {
            return;
        }

        loadingBoard = true;
        work.Run("board load", async token =>
        {
            var board = await client.BoardCardsAsync(token, OnDevStatus).ConfigureAwait(false);
            if (board is not null)
            {
                IngestBoard(board.Items);
                boardLoaded = true;
            }
        }, () => loadingBoard = false);
    }

    public void RefreshChat()
    {
        if (loadingChat)
        {
            return;
        }

        loadingChat = true;
        work.Run("chat load", async token =>
        {
            var page = await client.ChatMessagesAsync(0, null, token, OnDevStatus).ConfigureAwait(false);
            if (page is not null)
            {
                messages = page.Items;
                olderCursor = page.NextCursor;
                hasMoreOlder = page.NextCursor is not null;
                chatLoaded = true;
            }
        }, () => loadingChat = false);
    }

    public void LoadOlder()
    {
        if (loadingChat || loadingOlder || !hasMoreOlder)
        {
            return;
        }

        var cursor = olderCursor;
        if (cursor is null)
        {
            hasMoreOlder = false;
            return;
        }

        loadingOlder = true;
        work.Run("chat older", async token =>
        {
            var page = await client.ChatMessagesAsync(0, cursor, token, OnDevStatus).ConfigureAwait(false);
            if (page is not null)
            {
                MergeMessages(page.Items);
                olderCursor = page.NextCursor;
                hasMoreOlder = page.NextCursor is not null;
            }
        }, () => loadingOlder = false);
    }

    public void PollChat()
    {
        if (pollingChat)
        {
            return;
        }

        pollingChat = true;
        work.Run("chat poll", async token =>
        {
            var snapshot = messages;
            var after = configuration.DevChatLastSeenUnix;
            if (snapshot.Length > 0 && snapshot[^1].CreatedAtUnix > after)
            {
                after = snapshot[^1].CreatedAtUnix;
            }

            var page = await client.ChatMessagesAsync(after, null, token, OnDevStatus).ConfigureAwait(false);
            if (page is not null)
            {
                MergeMessages(page.Items);
            }
        }, () => pollingChat = false);
    }

    public void MarkChatSeen()
    {
        var snapshot = messages;
        if (snapshot.Length == 0)
        {
            return;
        }

        var newest = snapshot[^1].CreatedAtUnix;
        if (newest <= configuration.DevChatLastSeenUnix)
        {
            return;
        }

        configuration.DevChatLastSeenUnix = newest;
        configuration.Save();
    }

    public void CreateCard(string title, string body, Action<bool> onComplete)
    {
        if (cardBusy)
        {
            return;
        }

        cardBusy = true;
        RunCardOp("card create", onComplete, async token =>
        {
            var created = await client.CreateCardAsync(title, body, token).ConfigureAwait(false);
            if (created is not null)
            {
                ApplyCard(created);
            }

            return created is not null;
        });
    }

    public void UpdateCard(string cardId, string title, string body, Action<bool> onComplete)
    {
        if (cardBusy)
        {
            return;
        }

        cardBusy = true;
        RunCardOp("card update", onComplete, async token =>
        {
            var updated = await client.UpdateCardAsync(cardId, title, body, token).ConfigureAwait(false);
            if (updated is not null)
            {
                ApplyCard(updated);
            }

            return updated is not null;
        });
    }

    public void MoveCard(string cardId, int status, string? beforeId)
    {
        if (cardBusy)
        {
            return;
        }

        cardBusy = true;
        RunCardOp("card move", null, async token =>
        {
            var moved = await client.MoveCardAsync(cardId, status, beforeId, token).ConfigureAwait(false);
            if (moved is not null)
            {
                ApplyCard(moved);
            }

            return moved is not null;
        });
    }

    public void DeleteCard(string cardId, Action<bool> onComplete)
    {
        if (cardBusy)
        {
            return;
        }

        cardBusy = true;
        RunCardOp("card delete", onComplete, async token =>
        {
            var deleted = await client.DeleteCardAsync(cardId, token).ConfigureAwait(false);
            if (deleted)
            {
                RemoveCard(cardId);
            }

            return deleted;
        });
    }

    public void SendMessage(string body, Action<bool> onComplete)
    {
        var trimmed = body.Trim();
        if (trimmed.Length == 0 || sending)
        {
            return;
        }

        sending = true;
        work.Run("send", async token =>
        {
            var sent = await client.SendChatMessageAsync(trimmed, null, 0, 0, token).ConfigureAwait(false);
            if (sent is not null)
            {
                MergeMessages(new[] { sent });
            }

            onComplete(sent is not null);
        }, () => sending = false);
    }

    public void SendImageMessage(string sourcePath, Action<bool> onComplete)
    {
        if (sending)
        {
            return;
        }

        sending = true;
        work.Run("send image", async token =>
        {
            var baked = ImageProcessor.BakeJpeg(sourcePath, ImageMaxDimension);
            var upload = await media.UploadUrlAsync("image/jpeg", "dev", token).ConfigureAwait(false);
            if (upload is null)
            {
                onComplete(false);
                return;
            }

            var uploaded = await media.UploadImageAsync(upload.UploadUrl, baked.Bytes, "image/jpeg", token)
                .ConfigureAwait(false);
            if (!uploaded)
            {
                onComplete(false);
                return;
            }

            var sent = await client
                .SendChatMessageAsync(string.Empty, upload.Key, baked.Width, baked.Height, token)
                .ConfigureAwait(false);
            if (sent is not null)
            {
                MergeMessages(new[] { sent });
            }

            onComplete(sent is not null);
        }, () => sending = false);
    }

    public void DeleteMessage(string messageId, Action<bool> onComplete)
    {
        work.Run("message delete", async token =>
        {
            var deleted = await client.DeleteChatMessageAsync(messageId, token).ConfigureAwait(false);
            if (deleted)
            {
                messages = CopyOnWrite.RemoveById(messages, messageId);
            }

            onComplete(deleted);
        });
    }

    public string? MediaUrl(string messageId)
    {
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (mediaUrls.TryGetValue(messageId, out var entry))
        {
            if (entry.ExpiresAtUnix - MediaUrlExpiryMarginSeconds > nowUnix)
            {
                return entry.Url;
            }

            mediaUrls.TryRemove(messageId, out _);
        }

        if (!mediaLoading.TryAdd(messageId, 0))
        {
            return null;
        }

        work.Run("media url", async token =>
        {
            var result = await client.ChatMediaUrlAsync(messageId, token).ConfigureAwait(false);
            if (result is not null)
            {
                mediaUrls[messageId] = result;
            }
        }, () => mediaLoading.TryRemove(messageId, out _));
        return null;
    }

    private void OnDevStatus(int statusCode)
    {
        if (statusCode != 404)
        {
            return;
        }

        session.SetDevAccess(false);
        for (var status = 0; status < ColumnCount; status++)
        {
            columns[status] = Array.Empty<DevBoardCardDto>();
        }

        messages = Array.Empty<DevChatMessageDto>();
        olderCursor = null;
        hasMoreOlder = false;
        boardLoaded = false;
        chatLoaded = false;
        mediaUrls.Clear();
    }

    private void IngestBoard(DevBoardCardDto[] items)
    {
        var counts = new int[ColumnCount];
        for (var index = 0; index < items.Length; index++)
        {
            var status = items[index].Status;
            if (status >= 0 && status < ColumnCount)
            {
                counts[status]++;
            }
        }

        var fresh = new DevBoardCardDto[ColumnCount][];
        for (var status = 0; status < ColumnCount; status++)
        {
            fresh[status] = new DevBoardCardDto[counts[status]];
            counts[status] = 0;
        }

        for (var index = 0; index < items.Length; index++)
        {
            var status = items[index].Status;
            if (status >= 0 && status < ColumnCount)
            {
                fresh[status][counts[status]++] = items[index];
            }
        }

        for (var status = 0; status < ColumnCount; status++)
        {
            columns[status] = fresh[status];
        }
    }

    private void ApplyCard(DevBoardCardDto card)
    {
        RemoveCard(card.Id);
        if (card.Status < 0 || card.Status >= ColumnCount)
        {
            return;
        }

        var column = columns[card.Status];
        var insertAt = column.Length;
        for (var index = 0; index < column.Length; index++)
        {
            if (column[index].SortOrder > card.SortOrder)
            {
                insertAt = index;
                break;
            }
        }

        var result = new DevBoardCardDto[column.Length + 1];
        Array.Copy(column, 0, result, 0, insertAt);
        result[insertAt] = card;
        Array.Copy(column, insertAt, result, insertAt + 1, column.Length - insertAt);
        columns[card.Status] = result;
    }

    private void RemoveCard(string cardId)
    {
        for (var status = 0; status < ColumnCount; status++)
        {
            columns[status] = CopyOnWrite.RemoveById(columns[status], cardId);
        }
    }

    private void MergeMessages(DevChatMessageDto[] incoming)
    {
        if (incoming.Length == 0)
        {
            return;
        }

        lock (messagesLock)
        {
            var current = messages;
            var known = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < current.Length; index++)
            {
                known.Add(current[index].Id);
            }

            var merged = new List<DevChatMessageDto>(current.Length + incoming.Length);
            for (var index = 0; index < current.Length; index++)
            {
                merged.Add(current[index]);
            }

            for (var index = 0; index < incoming.Length; index++)
            {
                if (known.Add(incoming[index].Id))
                {
                    merged.Add(incoming[index]);
                }
            }

            merged.Sort(CompareMessages);
            messages = merged.ToArray();
        }
    }

    private static int CompareMessages(DevChatMessageDto left, DevChatMessageDto right)
    {
        var byTime = left.CreatedAtUnix.CompareTo(right.CreatedAtUnix);
        return byTime != 0 ? byTime : string.CompareOrdinal(left.Id, right.Id);
    }

    private void RunCardOp(string operation, Action<bool>? onComplete, Func<CancellationToken, Task<bool>> action)
    {
        work.Run(operation, async token =>
        {
            var succeeded = await action(token).ConfigureAwait(false);
            onComplete?.Invoke(succeeded);
        }, () => cardBusy = false);
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkTick;
        session.Changed -= OnSessionChanged;
        work.Dispose();
    }
}
