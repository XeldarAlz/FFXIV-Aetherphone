using System.Collections.Concurrent;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Analytics;
using Aetherphone.Core.Media;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Social;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Velvet;

internal sealed class VelvetStore : IDisposable
{
    private const int AvatarSize = 512;
    private const int PostSize = 1080;
    private static readonly TimeSpan MeRetryCooldown = TimeSpan.FromSeconds(30);
    private const int DmImageMaxDimension = 1280;
    private static readonly TimeSpan InboxPollInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ViewingGrace = TimeSpan.FromSeconds(4);
    private readonly AethernetSession session;
    private readonly AethernetClient client;
    private readonly NotificationService notifications;
    private readonly Configuration configuration;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Dictionary<string, long> inboxLastAt = new();
    private readonly ConcurrentDictionary<string, string> dmMediaUrls = new();
    private readonly ConcurrentDictionary<string, byte> dmMediaLoading = new();
    private volatile bool inboxPolling;
    private bool inboxPrimed;
    private DateTime lastInboxPollUtc = DateTime.MinValue;
    private volatile string? viewingThreadUserId;
    private DateTime lastViewingUtc = DateTime.MinValue;
    private DateTime lastMeAttemptUtc = DateTime.MinValue;
    private volatile VelvetProfileDto? me;
    private volatile bool loadingMe;
    private volatile bool avatarBusy;
    private volatile VelvetProfileDto[] discoverResults = Array.Empty<VelvetProfileDto>();
    private volatile bool loadingDiscover;
    private volatile bool discoverLoaded;
    private volatile VelvetConnectionDto[] connections = Array.Empty<VelvetConnectionDto>();
    private volatile bool loadingConnections;
    private volatile bool connectionsLoaded;
    private volatile VelvetConnectionDto[] requests = Array.Empty<VelvetConnectionDto>();
    private volatile bool loadingRequests;
    private volatile bool requestsLoaded;
    private volatile string? profileUserId;
    private volatile VelvetProfileDto? profileUser;
    private volatile bool profileLoading;
    private volatile bool profileFailed;
    private volatile VelvetThreadDto[] threads = Array.Empty<VelvetThreadDto>();
    private volatile bool loadingThreads;
    private volatile bool threadsLoaded;
    private volatile string? threadId;
    private volatile VelvetMessageDto[] messages = Array.Empty<VelvetMessageDto>();
    private volatile bool loadingThread;
    private volatile bool sending;
    private volatile bool otherTyping;
    private volatile VelvetPostDto[] feed = Array.Empty<VelvetPostDto>();
    private volatile bool loadingFeed;
    private volatile string? detailPostId;
    private volatile VelvetCommentDto[] detailComments = Array.Empty<VelvetCommentDto>();
    private volatile bool loadingComments;
    private volatile bool commenting;
    private volatile bool feedLoaded;
    private volatile bool posting;

    public VelvetStore(AethernetSession session, AethernetClient client, NotificationService notifications,
        Configuration configuration)
    {
        this.session = session;
        this.client = client;
        this.notifications = notifications;
        this.configuration = configuration;
        Plugin.Framework.Update += OnFrameworkTick;
    }

    public void NoteThreadViewed(string userId)
    {
        viewingThreadUserId = userId;
        lastViewingUtc = DateTime.UtcNow;
    }

    private void OnFrameworkTick(IFramework framework)
    {
        if (!session.IsSignedIn || !configuration.VelvetOnboarded)
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
        RunGuarded("inbox poll", async token =>
        {
            var page = await client.VelvetThreadsAsync(null, token).ConfigureAwait(false);
            if (page is not null)
            {
                threads = page.Items;
                RaiseInboxNotifications(page.Items);
            }
        }, () => inboxPolling = false);
    }

    private void RaiseInboxNotifications(VelvetThreadDto[] items)
    {
        var primed = inboxPrimed;
        for (var index = 0; index < items.Length; index++)
        {
            var thread = items[index];
            var previous = inboxLastAt.GetValueOrDefault(thread.OtherUserId, 0L);
            inboxLastAt[thread.OtherUserId] = thread.LastMessageAtUnix;
            if (!primed || thread.LastMessageAtUnix <= previous || thread.UnreadCount <= 0)
            {
                continue;
            }

            if (viewingThreadUserId == thread.OtherUserId && DateTime.UtcNow - lastViewingUtc < ViewingGrace)
            {
                continue;
            }

            var name = string.IsNullOrEmpty(thread.OtherDisplayName) ? thread.OtherHandle : thread.OtherDisplayName;
            notifications.Notify(new PhoneNotification("velvet", name, thread.LastMessagePreview, DateTime.Now,
                AppPalettes.Velvet.Accent, thread.OtherUserId));
        }

        inboxPrimed = true;
    }

    public bool IsSignedIn => session.IsSignedIn;
    public VelvetProfileDto? Me => me;
    public bool HasProfile => me is not null;
    public bool AvatarBusy => avatarBusy;
    public VelvetProfileDto[] DiscoverResults => discoverResults;
    public bool LoadingDiscover => loadingDiscover;
    public bool DiscoverLoaded => discoverLoaded;
    public VelvetConnectionDto[] Connections => connections;
    public bool LoadingConnections => loadingConnections;
    public bool ConnectionsLoaded => connectionsLoaded;
    public VelvetConnectionDto[] Requests => requests;
    public bool LoadingRequests => loadingRequests;
    public bool RequestsLoaded => requestsLoaded;
    public int RequestCount => requests.Length;
    public string? ProfileUserId => profileUserId;
    public VelvetProfileDto? ProfileUser => profileUser;
    public bool ProfileLoading => profileLoading;
    public bool ProfileFailed => profileFailed;
    public VelvetThreadDto[] Threads => threads;
    public bool LoadingThreads => loadingThreads;
    public bool ThreadsLoaded => threadsLoaded;
    public string? ThreadId => threadId;
    public VelvetMessageDto[] Messages => messages;
    public bool LoadingThread => loadingThread;
    public bool Sending => sending;
    public bool OtherTyping => otherTyping;
    public VelvetPostDto[] Feed => feed;
    public bool LoadingFeed => loadingFeed;
    public bool FeedLoaded => feedLoaded;
    public bool Posting => posting;

    public int UnreadCount
    {
        get
        {
            var snapshot = threads;
            var total = 0;
            for (var index = 0; index < snapshot.Length; index++)
            {
                total += snapshot[index].UnreadCount;
            }

            return total;
        }
    }

    public void EnsureMe()
    {
        if (!session.IsSignedIn || me is not null || loadingMe)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (now - lastMeAttemptUtc < MeRetryCooldown)
        {
            return;
        }

        lastMeAttemptUtc = now;
        loadingMe = true;
        RunGuarded("profile load", async token =>
        {
            var profile = await client.VelvetMeAsync(token).ConfigureAwait(false);
            if (profile is not null)
            {
                me = profile;
            }
        }, () => loadingMe = false);
    }

    public void AcceptGate(int gateVersion, Action<bool> onComplete)
    {
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var succeeded = false;
            try
            {
                var profile = await client.AcceptGateAsync(gateVersion, token).ConfigureAwait(false);
                if (profile is not null)
                {
                    me = profile;
                    succeeded = true;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Velvet] gate accept failed: {exception.Message}");
            }
            finally
            {
                onComplete(succeeded);
            }
        });
    }

    public void UpdateAvatar(string sourcePath, WallpaperCrop crop, Action<bool> onComplete)
    {
        if (avatarBusy)
        {
            return;
        }

        avatarBusy = true;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var succeeded = false;
            try
            {
                var baked = ImageProcessor.BakeSquareJpeg(sourcePath, crop, AvatarSize);
                var upload = await client.UploadUrlAsync("image/jpeg", "avatar", token).ConfigureAwait(false);
                if (upload is null)
                {
                    return;
                }

                var uploaded = await client.UploadImageAsync(upload.UploadUrl, baked.Bytes, "image/jpeg", token)
                    .ConfigureAwait(false);
                if (!uploaded)
                {
                    return;
                }

                var updated = await client
                    .UpdateProfileAsync(new UpdateProfileRequest(null, null, null, upload.PublicUrl), token)
                    .ConfigureAwait(false);
                if (updated is null)
                {
                    return;
                }

                var current = me;
                if (current is not null)
                {
                    me = current with { AvatarUrl = upload.PublicUrl };
                }

                succeeded = true;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Velvet] avatar update failed: {exception.Message}");
            }
            finally
            {
                avatarBusy = false;
                onComplete(succeeded);
            }
        });
    }

    public void UpdateIdentity(string displayName, string handle, Action<bool> onComplete)
    {
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var succeeded = false;
            try
            {
                var request = new UpdateProfileRequest(displayName.Length > 0 ? displayName : null,
                    handle.Length > 0 ? handle : null, null);
                var updated = await client.UpdateProfileAsync(request, token).ConfigureAwait(false);
                if (updated is not null)
                {
                    var current = me;
                    if (current is not null)
                    {
                        me = current with { DisplayName = updated.DisplayName, Handle = updated.Handle };
                    }

                    succeeded = true;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Velvet] identity update failed: {exception.Message}");
            }
            finally
            {
                onComplete(succeeded);
            }
        });
    }

    public void UpdateProfile(UpdateVelvetProfileRequest request, Action<bool> onComplete)
    {
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var succeeded = false;
            try
            {
                var updated = await client.UpdateVelvetProfileAsync(request, token).ConfigureAwait(false);
                if (updated is not null)
                {
                    me = updated;
                    succeeded = true;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Velvet] profile update failed: {exception.Message}");
            }
            finally
            {
                onComplete(succeeded);
            }
        });
    }

    public void RefreshDiscover(int lookingFor, string tags)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        loadingDiscover = true;
        RunGuarded("discover", async token =>
        {
            var page = await client.VelvetDiscoverAsync(lookingFor, tags, null, token).ConfigureAwait(false);
            if (page is not null)
            {
                discoverResults = page.Users;
            }
        }, () =>
        {
            loadingDiscover = false;
            discoverLoaded = true;
        });
    }

    public void RefreshConnections()
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        loadingConnections = true;
        RunGuarded("connections", async token =>
        {
            var page = await client.VelvetConnectionsAsync(null, token).ConfigureAwait(false);
            if (page is not null)
            {
                connections = page.Items;
            }
        }, () =>
        {
            loadingConnections = false;
            connectionsLoaded = true;
        });
    }

    public void Heartbeat()
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var offset = SocialTimeZone.EffectiveOffsetMinutes(configuration);
        RunGuarded("heartbeat", async token => await client.VelvetHeartbeatAsync(offset, token).ConfigureAwait(false));
    }

    public void RefreshRequests()
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        loadingRequests = true;
        RunGuarded("requests", async token =>
        {
            var page = await client.VelvetRequestsAsync(token).ConfigureAwait(false);
            if (page is not null)
            {
                requests = page.Items;
            }
        }, () =>
        {
            loadingRequests = false;
            requestsLoaded = true;
        });
    }

    public void AcceptRequest(string userId)
    {
        requests = RemoveConnection(requests, userId);
        connectionsLoaded = false;
        RunGuarded("accept", async token => await client.ConnectAsync(userId, token).ConfigureAwait(false));
    }

    public void DeclineRequest(string userId)
    {
        requests = RemoveConnection(requests, userId);
        RunGuarded("decline", async token => await client.DeclineRequestAsync(userId, token).ConfigureAwait(false));
    }

    public void RefreshFeed()
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        loadingFeed = true;
        RunGuarded("feed", async token =>
        {
            var page = await client.VelvetFeedAsync("explore", null, token).ConfigureAwait(false);
            if (page is not null)
            {
                feed = page.Items;
            }
        }, () =>
        {
            loadingFeed = false;
            feedLoaded = true;
        });
    }

    public void OpenProfile(string userId)
    {
        if (profileUserId == userId && (profileUser is not null || profileLoading))
        {
            return;
        }

        profileUserId = userId;
        profileUser = null;
        profileFailed = false;
        profileLoading = true;
        RunGuarded("profile open", async token =>
        {
            var user = await client.VelvetUserAsync(userId, token).ConfigureAwait(false);
            if (profileUserId != userId)
            {
                return;
            }

            if (user is null)
            {
                profileFailed = true;
            }
            else
            {
                profileUser = user;
            }
        }, () =>
        {
            if (profileUserId == userId)
            {
                profileLoading = false;
            }
        });
    }

    public void Connect(string userId)
    {
        SetConnectionStateEverywhere(userId, VelvetConnectionState.OutgoingRequest);
        RunGuarded("connect", async token => await client.ConnectAsync(userId, token).ConfigureAwait(false));
    }

    public void Disconnect(string userId)
    {
        SetConnectionStateEverywhere(userId, VelvetConnectionState.None);
        RunGuarded("disconnect", async token => await client.DisconnectAsync(userId, token).ConfigureAwait(false));
    }

    public void Block(string userId, Action<bool> onComplete)
    {
        SetConnectionStateEverywhere(userId, VelvetConnectionState.Blocked);
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var succeeded = false;
            try
            {
                succeeded = await client.BlockAsync(userId, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Velvet] block failed: {exception.Message}");
            }
            finally
            {
                onComplete(succeeded);
            }
        });
    }

    public void RefreshThreads()
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        loadingThreads = true;
        RunGuarded("threads", async token =>
        {
            var page = await client.VelvetThreadsAsync(null, token).ConfigureAwait(false);
            if (page is not null)
            {
                threads = page.Items;
            }
        }, () =>
        {
            loadingThreads = false;
            threadsLoaded = true;
        });
    }

    public void OpenThread(string id)
    {
        if (threadId == id && (messages.Length > 0 || loadingThread))
        {
            return;
        }

        threadId = id;
        messages = Array.Empty<VelvetMessageDto>();
        otherTyping = false;
        loadingThread = true;
        RunGuarded("thread open", async token =>
        {
            var page = await client.VelvetMessagesAsync(id, null, token).ConfigureAwait(false);
            if (threadId == id && page is not null)
            {
                messages = page.Items;
            }
        }, () =>
        {
            if (threadId == id)
            {
                loadingThread = false;
            }
        });
    }

    public void RefreshThread()
    {
        var current = threadId;
        if (current is null || loadingThread)
        {
            return;
        }

        RunGuarded("thread refresh", async token =>
        {
            var page = await client.VelvetMessagesAsync(current, null, token).ConfigureAwait(false);
            if (threadId == current && page is not null)
            {
                messages = page.Items;
            }
        });
    }

    public void SendTyping(string id)
    {
        RunGuarded("typing", async token => await client.SendVelvetTypingAsync(id, token).ConfigureAwait(false));
    }

    public void RefreshTyping(string id)
    {
        RunGuarded("typing state", async token =>
        {
            var result = await client.VelvetTypingAsync(id, token).ConfigureAwait(false);
            if (threadId == id && result is not null)
            {
                otherTyping = result.OtherTyping;
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
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var succeeded = false;
            try
            {
                var sent = await client.SendVelvetMessageAsync(id, trimmed, 0, null, token).ConfigureAwait(false);
                if (sent is not null)
                {
                    if (threadId == id)
                    {
                        messages = Append(messages, sent);
                    }

                    threadsLoaded = false;
                    succeeded = true;
                    Plugin.Analytics.Track(AnalyticsEvents.DmSent("velvet"));
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Velvet] send failed: {exception.Message}");
            }
            finally
            {
                sending = false;
                onComplete(succeeded);
            }
        });
    }

    public void SendImageMessage(string id, string sourcePath, string caption, Action<bool> onComplete)
    {
        if (sending)
        {
            return;
        }

        sending = true;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var succeeded = false;
            try
            {
                var baked = ImageProcessor.BakeJpeg(sourcePath, DmImageMaxDimension);
                var upload = await client.UploadUrlAsync("image/jpeg", "velvet-dm", token).ConfigureAwait(false);
                if (upload is null)
                {
                    return;
                }

                var uploaded = await client.UploadImageAsync(upload.UploadUrl, baked.Bytes, "image/jpeg", token)
                    .ConfigureAwait(false);
                if (!uploaded)
                {
                    return;
                }

                var sent = await client
                    .SendVelvetMessageAsync(id, caption.Trim(), 1, null, token, upload.Key, baked.Width, baked.Height)
                    .ConfigureAwait(false);
                if (sent is not null)
                {
                    if (threadId == id)
                    {
                        messages = Append(messages, sent);
                    }

                    threadsLoaded = false;
                    succeeded = true;
                    Plugin.Analytics.Track(AnalyticsEvents.DmSent("velvet"));
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Velvet] send image failed: {exception.Message}");
            }
            finally
            {
                sending = false;
                onComplete(succeeded);
            }
        });
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

        RunGuarded("dm media url", async token =>
        {
            var result = await client.VelvetDmMediaUrlAsync(messageId, token).ConfigureAwait(false);
            if (result is not null)
            {
                dmMediaUrls[messageId] = result.Url;
            }
        }, () => dmMediaLoading.TryRemove(messageId, out _));
        return null;
    }

    public void CreatePost(string sourcePath, WallpaperCrop crop, string caption, string[] tags, int visibility,
        Action<bool> onComplete)
    {
        if (posting)
        {
            return;
        }

        posting = true;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var succeeded = false;
            try
            {
                var baked = ImageProcessor.BakeSquareJpeg(sourcePath, crop, PostSize);
                var upload = await client.UploadUrlAsync("image/jpeg", "velvet", token).ConfigureAwait(false);
                if (upload is null)
                {
                    return;
                }

                var uploaded = await client.UploadImageAsync(upload.UploadUrl, baked.Bytes, "image/jpeg", token)
                    .ConfigureAwait(false);
                if (!uploaded)
                {
                    return;
                }

                var request =
                    new CreateVelvetPostRequest(upload.Key, baked.Width, baked.Height, caption, tags, visibility);
                var created = await client.CreateVelvetPostAsync(request, token).ConfigureAwait(false);
                succeeded = created is not null;
                if (succeeded)
                {
                    Plugin.Analytics.Track(AnalyticsEvents.PostCreated("velvet"));
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Velvet] create post failed: {exception.Message}");
            }
            finally
            {
                posting = false;
                onComplete(succeeded);
            }
        });
    }

    public VelvetCommentDto[] DetailComments => detailComments;
    public bool LoadingComments => loadingComments;
    public bool Commenting => commenting;

    public void OpenComments(string postId)
    {
        detailPostId = postId;
        detailComments = Array.Empty<VelvetCommentDto>();
        loadingComments = true;
        RunGuarded("comments", async token =>
        {
            var page = await client.VelvetCommentsAsync(postId, token).ConfigureAwait(false);
            if (detailPostId == postId && page is not null)
            {
                detailComments = page.Items;
            }
        }, () =>
        {
            if (detailPostId == postId)
            {
                loadingComments = false;
            }
        });
    }

    public void AddComment(string postId, string text, Action<bool> onComplete)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0 || commenting)
        {
            return;
        }

        commenting = true;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var succeeded = false;
            try
            {
                var created = await client.AddVelvetCommentAsync(postId, trimmed, token).ConfigureAwait(false);
                if (created is not null)
                {
                    if (detailPostId == postId)
                    {
                        detailComments = Append(detailComments, created);
                    }

                    succeeded = true;
                    Plugin.Analytics.Track(AnalyticsEvents.Comment("velvet"));
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Velvet] comment failed: {exception.Message}");
            }
            finally
            {
                commenting = false;
                onComplete(succeeded);
            }
        });
    }

    public void DeletePost(string postId)
    {
        var source = feed;
        var index = Array.FindIndex(source, item => item.Id == postId);
        if (index >= 0)
        {
            var result = new VelvetPostDto[source.Length - 1];
            Array.Copy(source, 0, result, 0, index);
            Array.Copy(source, index + 1, result, index, source.Length - index - 1);
            feed = result;
        }

        RunGuarded("delete post",
            async token => await client.DeleteVelvetPostAsync(postId, token).ConfigureAwait(false));
    }

    public void ToggleReaction(VelvetPostDto post, int kind)
    {
        var target = post.MyReaction == kind ? -1 : kind;
        if (target >= 0)
        {
            Plugin.Analytics.Track(AnalyticsEvents.Reaction("velvet"));
        }

        RunGuarded("reaction", async token =>
        {
            var result = target < 0
                ? await client.VelvetRemoveReactionAsync(post.Id, token).ConfigureAwait(false)
                : await client.VelvetReactAsync(post.Id, target, token).ConfigureAwait(false);
            if (result is not null)
            {
                feed = Replace(feed, result);
            }
        });
    }

    public void Report(string targetType, string targetId, string? reason, Action<bool> onComplete)
    {
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var succeeded = false;
            try
            {
                succeeded = await client.ReportAsync(targetType, targetId, reason, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Velvet] report failed: {exception.Message}");
            }
            finally
            {
                onComplete(succeeded);
            }
        });
    }

    public void DeletePost(string postId, Action<bool> onComplete)
    {
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var succeeded = false;
            try
            {
                succeeded = await client.DeleteVelvetPostAsync(postId, token).ConfigureAwait(false);
                if (succeeded)
                {
                    RemovePost(postId);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Velvet] delete post failed: {exception.Message}");
            }
            finally
            {
                onComplete(succeeded);
            }
        });
    }

    public void DeleteComment(string postId, string commentId)
    {
        if (detailPostId == postId)
        {
            detailComments = RemoveComment(detailComments, commentId);
        }

        RunGuarded("comment delete",
            async token => await client.DeleteVelvetCommentAsync(postId, commentId, token).ConfigureAwait(false));
    }

    public void DeleteComment(string postId, string commentId, Action<bool> onComplete)
    {
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var succeeded = false;
            try
            {
                succeeded = await client.DeleteVelvetCommentAsync(postId, commentId, token).ConfigureAwait(false);
                if (succeeded)
                {
                    if (detailPostId == postId)
                    {
                        detailComments = RemoveComment(detailComments, commentId);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Velvet] comment delete failed: {exception.Message}");
            }
            finally
            {
                onComplete(succeeded);
            }
        });
    }

    public void ClearDiscover() => discoverResults = Array.Empty<VelvetProfileDto>();

    public void InvalidateLists()
    {
        discoverLoaded = false;
        connectionsLoaded = false;
        threadsLoaded = false;
        requestsLoaded = false;
        feedLoaded = false;
    }

    private static VelvetConnectionDto[] RemoveConnection(VelvetConnectionDto[] source, string userId)
    {
        var index = Array.FindIndex(source, item => item.UserId == userId);
        if (index < 0)
        {
            return source;
        }

        var result = new VelvetConnectionDto[source.Length - 1];
        Array.Copy(source, 0, result, 0, index);
        Array.Copy(source, index + 1, result, index, source.Length - index - 1);
        return result;
    }

    private void SetConnectionStateEverywhere(string userId, int state)
    {
        if (profileUser is { } current && current.UserId == userId)
        {
            profileUser = current with { ConnectionState = state };
        }

        var discover = discoverResults;
        for (var index = 0; index < discover.Length; index++)
        {
            if (discover[index].UserId == userId && discover[index].ConnectionState != state)
            {
                var updated = (VelvetProfileDto[])discover.Clone();
                updated[index] = discover[index] with { ConnectionState = state };
                discoverResults = updated;
                break;
            }
        }
    }

    private void RunGuarded(string operation, Func<CancellationToken, Task> action, Action? cleanup = null)
    {
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await action(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Velvet] {operation} failed: {exception.Message}");
            }
            finally
            {
                cleanup?.Invoke();
            }
        });
    }

    private void RemovePost(string postId)
    {
        feed = RemoveById(feed, postId);
    }

    private static VelvetCommentDto[] RemoveComment(VelvetCommentDto[] source, string commentId)
    {
        var index = -1;
        for (var i = 0; i < source.Length; i++)
        {
            if (source[i].Id == commentId)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            return source;
        }

        var result = new VelvetCommentDto[source.Length - 1];
        Array.Copy(source, 0, result, 0, index);
        Array.Copy(source, index + 1, result, index, source.Length - index - 1);
        return result;
    }

    private static VelvetPostDto[] RemoveById(VelvetPostDto[] source, string postId)
    {
        var count = 0;
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index].Id != postId)
            {
                count++;
            }
        }

        if (count == source.Length)
        {
            return source;
        }

        var result = new VelvetPostDto[count];
        var resultIndex = 0;
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index].Id != postId)
            {
                result[resultIndex++] = source[index];
            }
        }

        return result;
    }

    private static VelvetPostDto[] Replace(VelvetPostDto[] source, VelvetPostDto updated)
    {
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index].Id != updated.Id)
            {
                continue;
            }

            var result = (VelvetPostDto[])source.Clone();
            result[index] = updated;
            return result;
        }

        return source;
    }

    private static VelvetMessageDto[] Append(VelvetMessageDto[] source, VelvetMessageDto message)
    {
        var result = new VelvetMessageDto[source.Length + 1];
        Array.Copy(source, result, source.Length);
        result[source.Length] = message;
        return result;
    }

    private static VelvetCommentDto[] Append(VelvetCommentDto[] source, VelvetCommentDto comment)
    {
        var result = new VelvetCommentDto[source.Length + 1];
        Array.Copy(source, result, source.Length);
        result[source.Length] = comment;
        return result;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkTick;
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
