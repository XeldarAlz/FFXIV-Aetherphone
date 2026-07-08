using System.Collections.Concurrent;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Analytics;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.DirectMessages;

internal sealed class DirectMessagesStore : IDisposable
{
    private const int DmImageMaxDimension = 1280;
    private static readonly TimeSpan InboxPollInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ViewingGrace = TimeSpan.FromSeconds(4);

    private readonly AethernetSession session;
    private readonly AethernetClient client;
    private readonly NotificationService notifications;
    private readonly StoreWork work = new("Messages");
    private readonly ConcurrentDictionary<string, string> dmMediaUrls = new();
    private readonly ConcurrentDictionary<string, byte> dmMediaLoading = new();
    private readonly Dictionary<string, long> inboxLastAt = new();

    private volatile ConversationDto[] conversations = Array.Empty<ConversationDto>();
    private volatile bool loadingConversations;
    private volatile bool conversationsLoaded;
    private volatile string? conversationId;
    private volatile ConversationDto? conversation;
    private volatile ConversationMemberDto[] members = Array.Empty<ConversationMemberDto>();
    private volatile ChatMessageDto[] messages = Array.Empty<ChatMessageDto>();
    private volatile bool loadingThread;
    private volatile bool sending;
    private volatile bool otherTyping;

    private volatile bool inboxPolling;
    private bool inboxPrimed;
    private DateTime lastInboxPollUtc = DateTime.MinValue;
    private volatile string? viewingConversationId;
    private DateTime lastViewingUtc = DateTime.MinValue;

    public DirectMessagesStore(AethernetSession session, AethernetClient client, NotificationService notifications)
    {
        this.session = session;
        this.client = client;
        this.notifications = notifications;
        Plugin.Framework.Update += OnFrameworkTick;
    }

    public bool IsSignedIn => session.IsSignedIn;
    public string MyUserId => session.CurrentUser?.Id ?? string.Empty;
    public ConversationDto[] Conversations => conversations;
    public bool LoadingConversations => loadingConversations;
    public bool ConversationsLoaded => conversationsLoaded;
    public string? ConversationId => conversationId;
    public ConversationDto? Conversation => conversation;
    public ConversationMemberDto[] Members => members;
    public ChatMessageDto[] Messages => messages;
    public bool LoadingThread => loadingThread;
    public bool Sending => sending;
    public bool OtherTyping => otherTyping;

    public int UnreadTotal
    {
        get
        {
            var snapshot = conversations;
            var total = 0;
            for (var index = 0; index < snapshot.Length; index++)
            {
                total += snapshot[index].UnreadCount;
            }

            return total;
        }
    }

    public void NoteConversationViewed(string id)
    {
        viewingConversationId = id;
        lastViewingUtc = DateTime.UtcNow;
        notifications.RemoveGroup(id);
    }

    private void OnFrameworkTick(IFramework framework)
    {
        if (!session.IsSignedIn)
        {
            inboxPrimed = false;
            return;
        }

        var now = DateTime.UtcNow;
        if (now - lastInboxPollUtc < InboxPollInterval)
        {
            return;
        }

        lastInboxPollUtc = now;
        PollInbox();
    }

    private void PollInbox()
    {
        if (inboxPolling)
        {
            return;
        }

        inboxPolling = true;
        work.Run("inbox poll", async token =>
        {
            var page = await client.ConversationsAsync(token).ConfigureAwait(false);
            if (page is not null)
            {
                conversations = page.Items;
                RaiseInboxNotifications(page.Items);
            }
        }, () => inboxPolling = false);
    }

    private void RaiseInboxNotifications(ConversationDto[] items)
    {
        var primed = inboxPrimed;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var previous = inboxLastAt.GetValueOrDefault(item.Id, 0L);
            inboxLastAt[item.Id] = item.LastMessageAtUnix;
            if (!primed || item.LastMessageAtUnix <= previous || item.UnreadCount <= 0)
            {
                continue;
            }

            if (viewingConversationId == item.Id && DateTime.UtcNow - lastViewingUtc < ViewingGrace)
            {
                continue;
            }

            notifications.Notify(new PhoneNotification("dm", DisplayTitle(item), PreviewText(item), DateTime.Now,
                AppPalettes.Messenger.Accent, item.Id));
        }

        inboxPrimed = true;
    }

    public static string DisplayTitle(ConversationDto item)
    {
        if (item.IsGroup)
        {
            return item.Title.Length > 0 ? item.Title : Loc.T(L.DirectMessages.GroupFallback);
        }

        return item.OtherDisplayName.Length > 0 ? item.OtherDisplayName : item.OtherHandle;
    }

    private static string PreviewText(ConversationDto item)
    {
        if (item.LastMessagePreview.Length > 0)
        {
            return item.LastMessagePreview;
        }

        return item.LastMessageKind == 1 ? Loc.T(L.DirectMessages.PhotoPreview) : string.Empty;
    }

    public void RefreshConversations()
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        loadingConversations = true;
        work.Run("conversations", async token =>
        {
            var page = await client.ConversationsAsync(token).ConfigureAwait(false);
            if (page is not null)
            {
                conversations = page.Items;
            }
        }, () =>
        {
            loadingConversations = false;
            conversationsLoaded = true;
        });
    }

    public void OpenConversation(string id)
    {
        if (conversationId == id && (messages.Length > 0 || loadingThread))
        {
            return;
        }

        conversationId = id;
        conversation = FindConversation(id);
        messages = Array.Empty<ChatMessageDto>();
        otherTyping = false;
        loadingThread = true;
        work.Run("thread open", async token =>
        {
            var detail = await client.ConversationAsync(id, token).ConfigureAwait(false);
            if (conversationId == id && detail is not null)
            {
                conversation = detail.Conversation;
                members = detail.Members;
            }

            var page = await client.ChatMessagesAsync(id, token).ConfigureAwait(false);
            if (conversationId == id && page is not null)
            {
                messages = page.Items;
            }
        }, () =>
        {
            if (conversationId == id)
            {
                loadingThread = false;
            }
        });
    }

    public void RefreshThread()
    {
        var current = conversationId;
        if (current is null || loadingThread)
        {
            return;
        }

        work.Run("thread refresh", async token =>
        {
            var page = await client.ChatMessagesAsync(current, token).ConfigureAwait(false);
            if (conversationId == current && page is not null)
            {
                messages = page.Items;
            }
        });
    }

    public void RefreshDetail()
    {
        var current = conversationId;
        if (current is null)
        {
            return;
        }

        work.Run("thread detail", async token =>
        {
            var detail = await client.ConversationAsync(current, token).ConfigureAwait(false);
            if (conversationId == current && detail is not null)
            {
                conversation = detail.Conversation;
                members = detail.Members;
            }
        });
    }

    public void SendTyping(string id)
    {
        work.Run("typing", async token => await client.SendChatTypingAsync(id, token).ConfigureAwait(false));
    }

    public void RefreshTyping(string id)
    {
        work.Run("typing state", async token =>
        {
            var result = await client.ChatTypingAsync(id, token).ConfigureAwait(false);
            if (conversationId == id && result is not null)
            {
                otherTyping = result.TypingUserIds.Length > 0;
            }
        });
    }

    public void SendMessage(string id, string body, Action<bool> onComplete)
    {
        var trimmed = body.Trim();
        if (trimmed.Length == 0 || sending)
        {
            return;
        }

        sending = true;
        work.Run("send", async token =>
        {
            var sent = await client.SendChatMessageAsync(id, trimmed, 0, token).ConfigureAwait(false);
            if (sent is null)
            {
                return false;
            }

            if (conversationId == id)
            {
                messages = CopyOnWrite.Append(messages, sent);
            }

            conversationsLoaded = false;
            Plugin.Analytics.Track(AnalyticsEvents.DmSent("dm"));
            return true;
        }, onComplete, () => sending = false);
    }

    public void SendImageMessage(string id, string sourcePath, string caption, Action<bool> onComplete)
    {
        if (sending)
        {
            return;
        }

        sending = true;
        work.Run("send image", async token =>
        {
            var baked = ImageProcessor.BakeJpeg(sourcePath, DmImageMaxDimension);
            var upload = await client.UploadUrlAsync("image/jpeg", "chat-dm", token).ConfigureAwait(false);
            if (upload is null)
            {
                return false;
            }

            var uploaded = await client.UploadImageAsync(upload.UploadUrl, baked.Bytes, "image/jpeg", token)
                .ConfigureAwait(false);
            if (!uploaded)
            {
                return false;
            }

            var sent = await client
                .SendChatMessageAsync(id, caption.Trim(), 1, token, upload.Key, baked.Width, baked.Height)
                .ConfigureAwait(false);
            if (sent is null)
            {
                return false;
            }

            if (conversationId == id)
            {
                messages = CopyOnWrite.Append(messages, sent);
            }

            conversationsLoaded = false;
            Plugin.Analytics.Track(AnalyticsEvents.DmSent("dm"));
            return true;
        }, onComplete, () => sending = false);
    }

    public string? DmMediaUrl(string messageId)
    {
        if (dmMediaUrls.TryGetValue(messageId, out var url))
        {
            return url;
        }

        if (!dmMediaLoading.TryAdd(messageId, 0))
        {
            return null;
        }

        work.Run("dm media url", async token =>
        {
            var result = await client.ChatDmMediaUrlAsync(messageId, token).ConfigureAwait(false);
            if (result is not null)
            {
                dmMediaUrls[messageId] = result.Url;
            }
        }, () => dmMediaLoading.TryRemove(messageId, out _));
        return null;
    }

    public void CreateDirect(string userId, Action<string?> onResult)
    {
        work.Run("create direct", async token =>
        {
            var detail = await client.CreateConversationAsync(new CreateConversationRequest(userId, null, null), token)
                .ConfigureAwait(false);
            onResult(detail?.Conversation.Id);
            if (detail is not null)
            {
                conversationsLoaded = false;
            }
        });
    }

    public void CreateGroup(string title, string[] memberIds, Action<string?> onResult)
    {
        work.Run("create group", async token =>
        {
            var detail = await client
                .CreateConversationAsync(new CreateConversationRequest(null, title, memberIds), token)
                .ConfigureAwait(false);
            onResult(detail?.Conversation.Id);
            if (detail is not null)
            {
                conversationsLoaded = false;
            }
        });
    }

    public void AddMembers(string id, string[] memberIds, Action<bool> onComplete)
    {
        work.Run("add members", async token =>
        {
            var detail = await client.AddChatMembersAsync(id, memberIds, token).ConfigureAwait(false);
            if (detail is null)
            {
                return false;
            }

            if (conversationId == id)
            {
                conversation = detail.Conversation;
                members = detail.Members;
            }

            conversationsLoaded = false;
            return true;
        }, onComplete);
    }

    public void RemoveMember(string id, string userId, Action<bool> onComplete)
    {
        work.Run("remove member", async token =>
        {
            var ok = await client.RemoveChatMemberAsync(id, userId, token).ConfigureAwait(false);
            if (ok)
            {
                conversationsLoaded = false;
            }

            return ok;
        }, onComplete);
    }

    public void Rename(string id, string title, Action<bool> onComplete)
    {
        work.Run("rename", async token =>
        {
            var detail = await client.RenameConversationAsync(id, title, token).ConfigureAwait(false);
            if (detail is null)
            {
                return false;
            }

            if (conversationId == id)
            {
                conversation = detail.Conversation;
                members = detail.Members;
            }

            conversationsLoaded = false;
            return true;
        }, onComplete);
    }

    private ConversationDto? FindConversation(string id)
    {
        var snapshot = conversations;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].Id == id)
            {
                return snapshot[index];
            }
        }

        return null;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkTick;
        work.Dispose();
    }
}
