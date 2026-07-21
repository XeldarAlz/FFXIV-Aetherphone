using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class PrivacyPage : ISettingsPage, IDisposable
{
    public string Title => Loc.T(L.Settings.Privacy);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.UserShield;
    public Vector4 Tint => new(0.42f, 0.56f, 0.86f, 1f);
    private readonly Configuration configuration;
    private readonly AethernetSession session;
    private readonly AccountClient client;
    private readonly SafetyClient safety;
    private readonly ConfirmService confirm;
    private readonly CancellationTokenSource cancellation = new();
    private static readonly TimeSpan BlockedListMaxAge = TimeSpan.FromSeconds(30);
    private volatile bool chatPrivacyLoaded;
    private volatile bool chatPrivacyLoading;
    private volatile bool shareReadReceipts = true;
    private volatile bool sharePresence = true;
    private volatile UserDto[] blockedUsers = Array.Empty<UserDto>();
    private volatile bool blockedLoaded;
    private volatile bool blockedLoading;
    private DateTime blockedLoadedAtUtc = DateTime.MinValue;

    public PrivacyPage(Configuration configuration, AethernetSession session, AccountClient client, SafetyClient safety,
        ConfirmService confirm)
    {
        this.configuration = configuration;
        this.session = session;
        this.client = client;
        this.safety = safety;
        this.confirm = confirm;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            DrawTellArchive(theme, scale);
            DrawChatPrivacy(theme, scale);
            DrawBlockedUsers(theme, scale);
        }
    }

    private void DrawBlockedUsers(PhoneTheme theme, float scale)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        EnsureBlockedLoaded();
        ImGui.Dummy(new Vector2(0f, 12f * scale));
        SettingsSection.Header(Loc.T(L.Social.BlockedUsers), theme);
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
            var name = user.DisplayName.Length > 0 ? user.DisplayName : user.Name;
            if (SettingsRow.Action(card.NextRow(), name, theme.TextStrong, theme))
            {
                AskUnblock(user);
            }
        }

        card.End();
        ImGui.Dummy(new Vector2(0f, 8f * scale));
        SettingsSection.Hint(Loc.T(L.Social.BlockedHint), theme);
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
                AepLog.Warning($"Blocked list load failed: {exception.Message}");
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
        var name = user.DisplayName.Length > 0 ? user.DisplayName : user.Name;
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
                AepLog.Warning($"Unblock failed: {exception.Message}");
            }
        });
    }

    private void DrawTellArchive(PhoneTheme theme, float scale)
    {
        ImGui.Dummy(new Vector2(0f, 12f * scale));
        SettingsSection.Header(Loc.T(L.Settings.TellArchiveTitle), theme);
        var card = GroupCard.Begin(theme, 1);
        var archive = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.TellArchive),
            configuration.ArchiveTellsToDisk, theme);
        card.End();
        if (archive != configuration.ArchiveTellsToDisk)
        {
            configuration.ArchiveTellsToDisk = archive;
            configuration.Save();
        }

        ImGui.Dummy(new Vector2(0f, 8f * scale));
        SettingsSection.Hint(Loc.T(L.Settings.TellArchiveHint), theme);
    }

    private void DrawChatPrivacy(PhoneTheme theme, float scale)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        EnsureLoaded();
        ImGui.Dummy(new Vector2(0f, 12f * scale));
        SettingsSection.Header(Loc.T(L.Apps.Message), theme);
        if (!chatPrivacyLoaded)
        {
            SettingsSection.Hint(Loc.T(L.Common.Loading), theme);
            return;
        }

        var card = GroupCard.Begin(theme, 2);
        var readReceipts = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.ReadReceipts), shareReadReceipts, theme);
        var lastSeen = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.LastSeenOnline), sharePresence, theme);
        card.End();
        if (readReceipts != shareReadReceipts || lastSeen != sharePresence)
        {
            shareReadReceipts = readReceipts;
            sharePresence = lastSeen;
            Push(readReceipts, lastSeen);
        }

        ImGui.Dummy(new Vector2(0f, 8f * scale));
        SettingsSection.Hint(Loc.T(L.Settings.ChatPrivacyHint), theme);
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
                    chatPrivacyLoaded = true;
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Chat privacy load failed: {exception.Message}");
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
                AepLog.Warning($"Chat privacy update failed: {exception.Message}");
            }
        });
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
