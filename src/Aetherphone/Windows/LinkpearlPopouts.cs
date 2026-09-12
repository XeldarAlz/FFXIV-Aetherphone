using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Game;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Runtime;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;

namespace Aetherphone.Windows;

internal sealed class LinkpearlPopouts : IDisposable
{
    public const int MaxWindows = 6;

    private const float ViewportMargin = 24f;
    private const float StaggerStep = 28f;

    private readonly LinkpearlPopoutWindow[] windows;
    private readonly Configuration configuration;
    private readonly ChatInbox inbox;
    private readonly ChatLog log;
    private readonly TabStore tabs;
    private readonly TellPreferences tellPreferences;
    private readonly LinkpearlNotificationGate gate;
    private readonly PhoneVisibility visibility;
    private readonly AppGate installed;
    private readonly List<LinkpearlPopoutState> restoreQueue = new(MaxWindows);

    public LinkpearlPopouts(Configuration configuration, ChatInbox inbox, ChatLog log, ChatSend send, TabStore tabs,
        TellPreferences tellPreferences, LinkpearlNotificationGate gate, PhoneVisibility visibility,
        AppGate installed, GameData gameData, ThemeProvider themes, LodestoneService lodestone,
        NotificationService notifications, ConfirmService confirm, WallpaperImageCache wallpaperImages)
    {
        this.configuration = configuration;
        this.inbox = inbox;
        this.log = log;
        this.tabs = tabs;
        this.tellPreferences = tellPreferences;
        this.gate = gate;
        this.visibility = visibility;
        this.installed = installed;
        windows = new LinkpearlPopoutWindow[MaxWindows];
        for (var slot = 0; slot < MaxWindows; slot++)
        {
            windows[slot] = new LinkpearlPopoutWindow(this, slot, configuration, inbox, tabs, log, send, gameData,
                themes, lodestone, notifications, confirm, wallpaperImages);
        }

        var saved = configuration.LinkpearlPopouts;
        for (var index = 0; index < saved.Count && index < MaxWindows; index++)
        {
            if (PopoutTabs.Migrate(saved[index]))
            {
                restoreQueue.Add(saved[index]);
            }
        }

        log.Appended += OnAppended;
        tabs.Changed += ReopenThreads;
        Plugin.ClientState.Logout += OnLogout;
    }

    public IReadOnlyList<LinkpearlPopoutWindow> Windows => windows;

    public Action<string>? OpenInPhone { get; set; }

    public Action<string, string>? LookUpInPhone { get; set; }

    public Action<uint>? OpenMarketInPhone { get; set; }

    public bool CanOpenMore => Free() is not null || Host() is not null;

    public bool CanDetach => Free() is not null;

    public int OpenCount
    {
        get
        {
            var count = 0;
            for (var index = 0; index < windows.Length; index++)
            {
                if (windows[index].Bound)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public bool AnyExpanded
    {
        get
        {
            for (var index = 0; index < windows.Length; index++)
            {
                if (windows[index].Bound && !windows[index].IsCollapsed)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public void Restore()
    {
        for (var index = 0; index < restoreQueue.Count; index++)
        {
            var state = restoreQueue[index];
            var window = Free();
            if (window is null)
            {
                break;
            }

            window.Bind(state.Keys[state.Active], state);
        }

        restoreQueue.Clear();
    }

    public bool IsOpen(string key) => Holder(key) is not null;

    public bool Open(string key)
    {
        if (!installed.Open || key.Length == 0)
        {
            return false;
        }

        if (Holder(key) is { } existing)
        {
            existing.FocusTab(key);
            return true;
        }

        var host = Host();
        if (host is not null)
        {
            host.AddTab(key, true);
            host.Focus();
            Persist();
            return true;
        }

        return OpenInNewWindow(key);
    }

    public bool OpenInNewWindow(string key)
    {
        if (!installed.Open || key.Length == 0)
        {
            return false;
        }

        if (Holder(key) is { } existing)
        {
            existing.FocusTab(key);
            return true;
        }

        var window = Free();
        if (window is null)
        {
            return false;
        }

        window.Bind(key, null);
        Persist();
        return true;
    }

    public Vector2 DefaultPosition(Vector2 scaledSize)
    {
        var mode = (PopoutPlacement)Math.Clamp(configuration.LinkpearlPopoutPlacement, 0,
            (int)PopoutPlacement.BottomRight);
        Rect? phone = visibility.TryGetFrame(out var frame) ? frame : null;
        var margin = ViewportMargin * UiScale.Global;
        var stagger = StaggerStep * UiScale.Global * OpenCount;
        return PopoutPlacements.Resolve(mode, PhoneBounds.Viewport(), phone, scaledSize, margin, stagger);
    }

    public void Close(string key)
    {
        var window = Holder(key);
        if (window is null)
        {
            return;
        }

        window.RemoveTab(window.IndexOfTab(key));
        Persist();
    }

    public bool Toggle(string key)
    {
        if (IsOpen(key))
        {
            Close(key);
            return true;
        }

        return Open(key);
    }

    public void CloseAll()
    {
        for (var index = 0; index < windows.Length; index++)
        {
            windows[index].Unbind();
        }

        Persist();
    }

    public void SetAllCollapsed(bool collapsed)
    {
        var changed = false;
        for (var index = 0; index < windows.Length; index++)
        {
            changed |= windows[index].SetCollapsed(collapsed);
        }

        if (changed)
        {
            Persist();
        }
    }

    public void OnCollapseChanged() => Persist();

    public bool Suppressed { get; private set; }

    public void SetSuppressed(bool suppressed)
    {
        if (Suppressed == suppressed)
        {
            return;
        }

        Suppressed = suppressed;
        for (var index = 0; index < windows.Length; index++)
        {
            windows[index].SetSuppressed(suppressed);
        }
    }

    public void OpenTell(string name, string world)
    {
        if (name.Length == 0)
        {
            return;
        }

        var row = inbox.EnsureTell(name, world);
        Open(row.Key);
    }

    public void Switch(LinkpearlPopoutWindow window, string key)
    {
        if (Holder(key) is { } other && !ReferenceEquals(other, window))
        {
            other.FocusTab(key);
            return;
        }

        window.Rebind(key);
        Persist();
    }

    public bool AddTab(LinkpearlPopoutWindow window, string key)
    {
        if (Holder(key) is { } other && !ReferenceEquals(other, window))
        {
            other.FocusTab(key);
            return false;
        }

        if (!window.AddTab(key, true))
        {
            ShellToast.Show(Loc.T(L.Linkpearl.PopoutTabLimit, PopoutTabs.MaxTabs));
            return false;
        }

        Persist();
        return true;
    }

    public void CloseTab(LinkpearlPopoutWindow window, int index)
    {
        if (!window.RemoveTab(index))
        {
            return;
        }

        Persist();
    }

    public bool Detach(LinkpearlPopoutWindow window, int index)
    {
        if (window.TabCount <= 1)
        {
            return false;
        }

        var free = Free();
        if (free is null)
        {
            ShellToast.Show(Loc.T(L.Linkpearl.PopoutLimit, MaxWindows));
            return false;
        }

        var key = window.KeyAt(index);
        window.RemoveTab(index);
        free.Bind(key, null);
        Persist();
        return true;
    }

    public void Merge(LinkpearlPopoutWindow source, LinkpearlPopoutWindow target)
    {
        while (source.TabCount > 0 && target.AddTab(source.KeyAt(0), false))
        {
            source.RemoveTab(0);
        }

        if (source.Bound)
        {
            ShellToast.Show(Loc.T(L.Linkpearl.PopoutTabLimit, PopoutTabs.MaxTabs));
        }

        target.Focus();
        Persist();
    }

    public LinkpearlPopoutWindow? DropTargetAt(LinkpearlPopoutWindow source, Vector2 point)
    {
        for (var index = 0; index < windows.Length; index++)
        {
            var window = windows[index];
            if (ReferenceEquals(window, source) || !window.Bound || window.TabCount >= PopoutTabs.MaxTabs)
            {
                continue;
            }

            if (window.Frame.Contains(point))
            {
                return window;
            }
        }

        return null;
    }

    public void OnWindowClosed(LinkpearlPopoutWindow window)
    {
        window.Unbind();
        Persist();
    }

    public void Persist()
    {
        var states = configuration.LinkpearlPopouts;
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
        log.Appended -= OnAppended;
        tabs.Changed -= ReopenThreads;
        Plugin.ClientState.Logout -= OnLogout;
        Persist();
        configuration.SaveNow();
    }

    private void ReopenThreads()
    {
        for (var index = 0; index < windows.Length; index++)
        {
            if (windows[index].Bound)
            {
                windows[index].ReopenThread();
            }
        }
    }

    private void OnLogout(int type, int code)
    {
        if (!configuration.LinkpearlPopoutCloseOnLogout)
        {
            return;
        }

        CloseAll();
    }

    private LinkpearlPopoutWindow? Holder(string key)
    {
        for (var index = 0; index < windows.Length; index++)
        {
            if (windows[index].Bound && windows[index].Holds(key))
            {
                return windows[index];
            }
        }

        return null;
    }

    private LinkpearlPopoutWindow? Free()
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

    private LinkpearlPopoutWindow? Host()
    {
        if (!configuration.LinkpearlPopoutTabs)
        {
            return null;
        }

        LinkpearlPopoutWindow? best = null;
        for (var index = 0; index < windows.Length; index++)
        {
            var window = windows[index];
            if (!window.Bound || window.TabCount >= PopoutTabs.MaxTabs)
            {
                continue;
            }

            if (best is null || window.LastActiveTick > best.LastActiveTick)
            {
                best = window;
            }
        }

        return best;
    }

    private void OnAppended(ChatEntry entry)
    {
        if (!configuration.LinkpearlPopoutTells || !ChatStreams.IsTell(entry.StreamKey))
        {
            return;
        }

        if (entry.IsSelf && !configuration.LinkpearlPopoutOutgoingTells)
        {
            return;
        }

        if (!installed.Open || gate.Paused || configuration.DoNotDisturb || visibility.IsVisible)
        {
            return;
        }

        if (tellPreferences.IsMuted(entry.StreamKey) || IsOpen(entry.StreamKey))
        {
            return;
        }

        inbox.Sync();
        Open(entry.StreamKey);
    }
}
