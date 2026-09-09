using Aetherphone.Core.Aethernet;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Home;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Message;
using Aetherphone.Core.Net;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Report;
using Aetherphone.Core.Runtime;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Translation;
using Aetherphone.Core.Wallpapers;
using Dalamud.Interface.Windowing;

namespace Aetherphone.Apps.Message;

internal sealed class MessagePopoutServices
{
    public required AethernetSession Session { get; init; }
    public required AethernetApi Net { get; init; }
    public required NotificationService Notifications { get; init; }
    public required KeyVault Vault { get; init; }
    public required ConversationKeyStore ConversationKeys { get; init; }
    public required PeerKeyDirectory PeerKeys { get; init; }
    public required DecryptedHistoryStore ChatHistory { get; init; }
    public required PhoneVisibility Visibility { get; init; }
    public required RealtimeSignalBus Signals { get; init; }
    public required AppInstaller Installer { get; init; }
    public required RemoteImageCache Images { get; init; }
    public required LodestoneService Lodestone { get; init; }
    public required HttpService Http { get; init; }
    public required PhotoLibrary Library { get; init; }
    public required Configuration Configuration { get; init; }
    public required ConfirmService Confirm { get; init; }
    public required ReportService Report { get; init; }
    public required TranslationService Translation { get; init; }
    public required WallpaperImageCache WallpaperImages { get; init; }
    public required EncryptionHelpService EncryptionHelp { get; init; }
    public required ThemeProvider Themes { get; init; }

    public DirectMessagesStore CreateDetachedStore() =>
        new(Session, Net.Chats, Net.Safety, Net.Media, Notifications, Vault, ConversationKeys, PeerKeys, ChatHistory,
            Visibility, Signals, Installer, tracksInbox: false);
}

internal sealed class MessagePopouts : IMessagePopouts
{
    public const int MaxWindows = 4;

    private readonly MessagePopoutWindow[] windows;
    private readonly Configuration configuration;
    private readonly DirectMessagesStore inbox;
    private readonly AppGate installed;
    private readonly List<MessagePopoutState> restoreQueue = new(MaxWindows);

    public MessagePopouts(MessagePopoutServices services, DirectMessagesStore inbox)
    {
        configuration = services.Configuration;
        this.inbox = inbox;
        installed = services.Installer.Gate("message");
        windows = new MessagePopoutWindow[MaxWindows];
        for (var slot = 0; slot < MaxWindows; slot++)
        {
            windows[slot] = new MessagePopoutWindow(this, slot, services);
        }

        var saved = configuration.MessagePopouts;
        for (var index = 0; index < saved.Count && index < MaxWindows; index++)
        {
            if (saved[index].ConversationId.Length > 0)
            {
                restoreQueue.Add(saved[index]);
            }
        }

        Plugin.ClientState.Logout += OnLogout;
    }

    public IReadOnlyList<Window> Windows => windows;

    public IPhoneApp Owner { get; set; } = null!;

    public Action<string>? OpenInPhone { get; set; }

    public DirectMessagesStore Inbox => inbox;

    public void Restore()
    {
        if (!installed.Open)
        {
            restoreQueue.Clear();
            return;
        }

        for (var index = 0; index < restoreQueue.Count; index++)
        {
            var state = restoreQueue[index];
            var window = Free();
            if (window is null)
            {
                break;
            }

            window.Bind(state.ConversationId, state.Title, state);
        }

        restoreQueue.Clear();
    }

    public bool IsOpen(string conversationId) => Holder(conversationId) is not null;

    public bool Open(string conversationId, string title)
    {
        if (!installed.Open || conversationId.Length == 0)
        {
            return false;
        }

        if (Holder(conversationId) is { } existing)
        {
            existing.Focus();
            return true;
        }

        var window = Free();
        if (window is null)
        {
            return false;
        }

        window.Bind(conversationId, title, null);
        Persist();
        return true;
    }

    public void Close(string conversationId)
    {
        var window = Holder(conversationId);
        if (window is null)
        {
            return;
        }

        window.Unbind();
        Persist();
    }

    public void CloseAll()
    {
        for (var index = 0; index < windows.Length; index++)
        {
            windows[index].Unbind();
        }

        Persist();
    }

    public void Switch(MessagePopoutWindow window, string conversationId, string title)
    {
        if (Holder(conversationId) is { } other && !ReferenceEquals(other, window))
        {
            other.Focus();
            return;
        }

        window.Rebind(conversationId, title);
        Persist();
    }

    public void OnCollapseChanged() => Persist();

    public void OnWindowClosed(MessagePopoutWindow window)
    {
        window.Unbind();
        Persist();
    }

    public void Persist()
    {
        var states = configuration.MessagePopouts;
        states.Clear();
        for (var index = 0; index < windows.Length; index++)
        {
            if (windows[index].Bound)
            {
                states.Add(windows[index].Snapshot());
            }
        }

        configuration.Save();
    }

    public void Dispose()
    {
        Plugin.ClientState.Logout -= OnLogout;
        Persist();
        configuration.SaveNow();
        for (var index = 0; index < windows.Length; index++)
        {
            windows[index].Unbind();
        }
    }

    private void OnLogout(int type, int code) => CloseAll();

    private MessagePopoutWindow? Holder(string conversationId)
    {
        for (var index = 0; index < windows.Length; index++)
        {
            if (windows[index].Bound && windows[index].Holds(conversationId))
            {
                return windows[index];
            }
        }

        return null;
    }

    private MessagePopoutWindow? Free()
    {
        for (var index = 0; index < windows.Length; index++)
        {
            if (!windows[index].Bound)
            {
                return windows[index];
            }
        }

        return null;
    }
}
