using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Net;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class PrivacyPage : ISettingsPage, IDisposable
{
    private static readonly SettingsEntry[] Searchable =
    {
        new(L.Settings.TellArchive),
        new(L.Settings.ReadReceipts),
        new(L.Settings.LastSeenOnline),
        new(L.Stage.ShowOnLeaderboards),
        new(L.PhotoTag.SettingsTitle),
        new(L.Social.BlockedUsers),
        new(L.Settings.ClearCache, L.Settings.Storage),
    };

    public string Title => Loc.T(L.Settings.Privacy);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.UserShield;
    public Vector4 Tint => new(0.42f, 0.56f, 0.86f, 1f);
    public ReadOnlySpan<SettingsEntry> Entries => Searchable;
    private readonly Configuration configuration;
    private readonly AethernetSession session;
    private readonly AccountClient client;
    private readonly ScoresClient scores;
    private readonly SafetyClient safety;
    private readonly ConfirmService confirm;
    private readonly ISettingsNavigator navigator;
    private readonly ISettingsPage tagsMentionsPage;
    private readonly CacheStorage cacheStorage;
    private readonly CancellationTokenSource cancellation = new();
    private static readonly TimeSpan BlockedListMaxAge = TimeSpan.FromSeconds(30);
    private const long BytesPerMegabyte = 1024L * 1024;
    private volatile bool chatPrivacyLoaded;
    private volatile bool chatPrivacyLoading;
    private volatile bool shareReadReceipts = true;
    private volatile bool sharePresence = true;
    private volatile bool showOnLeaderboards = true;
    private volatile UserDto[] blockedUsers = Array.Empty<UserDto>();
    private volatile bool blockedLoaded;
    private volatile bool blockedLoading;
    private DateTime blockedLoadedAtUtc = DateTime.MinValue;
    private string clearCacheLabel = string.Empty;
    private string clearCacheLabelFormat = string.Empty;
    private long clearCacheLabelMegabytes = -1;

    public PrivacyPage(Configuration configuration, AethernetSession session, AccountClient client,
        ScoresClient scores, SafetyClient safety, ConfirmService confirm, ISettingsNavigator navigator,
        ISettingsPage tagsMentionsPage, CacheStorage cacheStorage)
    {
        this.configuration = configuration;
        this.session = session;
        this.client = client;
        this.scores = scores;
        this.safety = safety;
        this.confirm = confirm;
        this.navigator = navigator;
        this.tagsMentionsPage = tagsMentionsPage;
        this.cacheStorage = cacheStorage;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = UiScale.Current;
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            DrawChatPrivacy(theme, scale);
            DrawBlockedUsers(theme, scale);
            DrawStorage(theme, scale);
        }
    }

    private void DrawStorage(PhoneTheme theme, float scale)
    {
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        SettingsSection.Header(Loc.T(L.Settings.Storage), theme, Loc.T(L.Settings.StorageHint));
        var card = GroupCard.Begin(theme, 1);
        var clearClicked = SettingsRow.Action(card.NextRow(), ClearCacheLabel(), theme.Danger, theme);
        card.End();
        if (clearClicked)
        {
            AskClearCache();
        }
    }

    private string ClearCacheLabel()
    {
        var sizeBytes = cacheStorage.SizeBytes();
        if (sizeBytes == CacheStorage.UnknownSize)
        {
            return Loc.T(L.Settings.ClearCache);
        }

        var megabytes = (sizeBytes + BytesPerMegabyte - 1) / BytesPerMegabyte;
        var format = Loc.T(L.Settings.ClearCacheSize);
        if (megabytes == clearCacheLabelMegabytes && ReferenceEquals(format, clearCacheLabelFormat))
        {
            return clearCacheLabel;
        }

        clearCacheLabelMegabytes = megabytes;
        clearCacheLabelFormat = format;
        clearCacheLabel = Loc.T(L.Settings.ClearCacheSize, megabytes);
        return clearCacheLabel;
    }

    private void AskClearCache()
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Settings.ClearCache),
            Message = Loc.T(L.Settings.ClearCacheBody),
            ConfirmLabel = Loc.T(L.Settings.ClearCacheAction),
            CancelLabel = Loc.T(L.Common.Cancel),
            Sheet = true,
            Confirm = () =>
            {
                _ = Task.Run(cacheStorage.Clear);
            },
        });
    }

    private void DrawBlockedUsers(PhoneTheme theme, float scale)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        EnsureBlockedLoaded();
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        SettingsSection.Header(Loc.T(L.Social.BlockedUsers), theme, Loc.T(L.Social.BlockedHint));
        var snapshot = blockedUsers;
        if (!blockedLoaded)
        {
            SettingsSection.Hint(Loc.T(L.Common.Loading), theme);
            return;
        }

        if (snapshot.Length == 0)
        {
            SettingsSection.Hint(Loc.T(L.Social.BlockedEmpty), theme);
            return;
        }

        var card = GroupCard.Begin(theme, snapshot.Length);
        for (var index = 0; index < snapshot.Length; index++)
        {
            var user = snapshot[index];
            var name = SocialIdentity.Name(user.DisplayName, user.Handle);
            if (SettingsRow.Action(card.NextRow(), name, theme.TextStrong, theme))
            {
                AskUnblock(user);
            }
        }

        card.End();
    }

    private void EnsureBlockedLoaded()
    {
        if (blockedLoading || DateTime.UtcNow - blockedLoadedAtUtc < BlockedListMaxAge)
        {
            return;
        }

        blockedLoading = true;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var page = await safety.BlockedUsersAsync(token).ConfigureAwait(false);
                if (page is not null)
                {
                    blockedUsers = page.Users;
                    blockedLoaded = true;
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Blocked list load failed");
            }
            finally
            {
                blockedLoadedAtUtc = DateTime.UtcNow;
                blockedLoading = false;
            }
        });
    }

    private void AskUnblock(UserDto user)
    {
        var name = SocialIdentity.Name(user.DisplayName, user.Handle);
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Social.UnblockConfirm, name),
            ConfirmLabel = Loc.T(L.Social.Unblock),
            CancelLabel = Loc.T(L.Common.Cancel),
            Confirm = () => Unblock(user.Id),
        });
    }

    private void Unblock(string userId)
    {
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                if (await safety.UnblockAsync(userId, token).ConfigureAwait(false))
                {
                    blockedUsers = CopyOnWrite.RemoveWhere(blockedUsers, user => user.Id == userId);
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Unblock failed");
            }
        });
    }

    private void DrawChatPrivacy(PhoneTheme theme, float scale)
    {
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        var archiveCard = GroupCard.Begin(theme, 1);
        var archive = SettingsRow.Bool(archiveCard.NextRow(), Loc.T(L.Settings.TellArchive),
            configuration.ArchiveTellsToDisk, theme, null, Loc.T(L.Settings.TellArchiveHint));
        archiveCard.End();
        if (archive != configuration.ArchiveTellsToDisk)
        {
            configuration.ArchiveTellsToDisk = archive;
            configuration.Save();
        }

        if (!session.IsSignedIn)
        {
            return;
        }

        EnsureLoaded();
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        if (!chatPrivacyLoaded)
        {
            SettingsSection.Hint(Loc.T(L.Common.Loading), theme);
            return;
        }

        var card = GroupCard.Begin(theme, 4);
        var readReceipts = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.ReadReceipts), shareReadReceipts, theme,
            null, Loc.T(L.Settings.ChatPrivacyHint));
        var lastSeen = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.LastSeenOnline), sharePresence, theme);
        var leaderboards = SettingsRow.Bool(card.NextRow(), Loc.T(L.Stage.ShowOnLeaderboards), showOnLeaderboards,
            theme);
        var tagsOpened = SettingsRow.Disclosure(card.NextRow(), Loc.T(L.PhotoTag.SettingsTitle), string.Empty, theme);
        card.End();
        if (readReceipts != shareReadReceipts || lastSeen != sharePresence)
        {
            shareReadReceipts = readReceipts;
            sharePresence = lastSeen;
            Push(readReceipts, lastSeen);
        }

        if (leaderboards != showOnLeaderboards)
        {
            showOnLeaderboards = leaderboards;
            PushLeaderboards(leaderboards);
        }

        if (tagsOpened)
        {
            navigator.Open(tagsMentionsPage);
        }
    }

    private void EnsureLoaded()
    {
        if (chatPrivacyLoaded || chatPrivacyLoading)
        {
            return;
        }

        chatPrivacyLoading = true;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var me = await client.MeAsync(token).ConfigureAwait(false);
                if (me is not null)
                {
                    shareReadReceipts = me.ShareReadReceipts;
                    sharePresence = me.SharePresence;
                    showOnLeaderboards = me.ShowOnLeaderboards;
                    chatPrivacyLoaded = true;
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Chat privacy load failed");
            }
            finally
            {
                chatPrivacyLoading = false;
            }
        });
    }

    private void Push(bool readReceipts, bool lastSeen)
    {
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var me = await client.UpdateChatPrivacyAsync(new UpdateChatPrivacyRequest(readReceipts, lastSeen),
                    token).ConfigureAwait(false);
                if (me is not null)
                {
                    shareReadReceipts = me.ShareReadReceipts;
                    sharePresence = me.SharePresence;
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Chat privacy update failed");
            }
        });
    }

    private void PushLeaderboards(bool show)
    {
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var me = await scores.SetShowOnLeaderboardsAsync(show, token).ConfigureAwait(false);
                if (me is null)
                {
                    return;
                }

                showOnLeaderboards = me.ShowOnLeaderboards;
                session.SetUser(me);
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Leaderboard privacy update failed");
            }
        });
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
