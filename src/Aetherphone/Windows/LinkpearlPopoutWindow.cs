using System.Runtime.InteropServices;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Game;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;

namespace Aetherphone.Windows;

internal sealed class LinkpearlPopoutWindow : Window
{
    public const float DefaultWidth = 336f;
    public const float DefaultHeight = 430f;

    private const ImGuiWindowFlags PopoutFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar |
                                                 ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoCollapse |
                                                 ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings |
                                                 ImGuiWindowFlags.NoFocusOnAppearing;

    private const int ScaledStyleVarCount = 7;
    private const int GripColorCount = 3;
    private const float MinWidth = 250f;
    private const float MinHeight = 210f;
    private const float MaxSide = 2400f;
    private const float TitleHeight = 44f;
    private const float Rounding = 18f;
    private const float BodyInset = 4f;
    private const float AvatarRadius = 13f;
    private const float ButtonRadius = 14f;
    private const float ButtonPitch = 31f;
    private const float EdgeInset = 14f;
    private const float CaretGap = 6f;
    private const float StaggerStep = 28f;
    private const float ViewportMargin = 24f;
    private const float GripArm = 9f;
    private const float MinBodyHeight = 96f;
    private const float FlashStrength = 0.72f;
    private const float TabRailInset = 8f;
    private const float TabRailGap = 4f;
    private const float DragThreshold = 2f;
    private const float MinIdleOpacity = 0.15f;
    private const float DropHighlightFill = 0.16f;
    private const float DropHighlightStroke = 0.85f;
    private const int SwitchMenuLimit = 14;
    private const byte MenuActivateTab = 0;
    private const byte MenuAddTab = 1;
    private const byte MenuAddTarget = 2;
    private const byte MenuDetachTab = 3;
    private const byte MenuCloseTab = 4;
    private const byte MenuSwitchTo = 5;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 GripInk = new(1f, 1f, 1f, 0.22f);
    private static readonly Vector4 FlashInk = new(1f, 0.839f, 0.039f, 1f);

    private readonly LinkpearlPopouts owner;
    private readonly int slot;
    private readonly Configuration configuration;
    private readonly ChatInbox inbox;
    private readonly TabStore tabs;
    private readonly ThemeProvider themes;
    private readonly LodestoneService lodestone;
    private readonly NotificationService notifications;
    private readonly GameChatThread thread;
    private readonly GameChatMenu chatMenu;
    private readonly ConfirmService confirm;
    private readonly ConfirmOverlay confirmOverlay;
    private readonly int confirmHost = ConfirmHosts.Reserve();
    private readonly DropdownMenu switchMenu = new();
    private readonly List<DropdownMenu.Item> switchItems = new(SwitchMenuLimit);
    private readonly List<string> switchKeys = new(SwitchMenuLimit);
    private readonly List<byte> switchActions = new(SwitchMenuLimit);
    private readonly List<string> keys = new(PopoutTabs.MaxTabs);
    private readonly string[] tabLabels = new string[PopoutTabs.MaxTabs];
    private readonly string[] tabTitles = new string[PopoutTabs.MaxTabs];
    private readonly int[] tabUnread = new int[PopoutTabs.MaxTabs];
    private readonly bool[] tabSelected = new bool[PopoutTabs.MaxTabs];
    private readonly ChipRail tabRail = new();
    private readonly AppSkin railSkin = new(AppPalettes.Linkpearl(PhoneTheme.Default));
    private readonly string switchMenuId;
    private readonly string addMenuId;
    private readonly string closeButtonId;
    private readonly string phoneButtonId;
    private readonly string bellButtonId;
    private readonly string collapseButtonId;
    private int active;
    private string threadKey = string.Empty;
    private bool attended;
    private bool placePending;
    private bool collapsed;
    private bool suppressed;
    private bool positionForced;
    private bool dragging;
    private Spring collapseSpring;
    private Spring fadeSpring;
    private LinkpearlPopoutState? savedPlacement;
    private LinkpearlPopoutWindow? dropTarget;
    private Vector2 pendingPosition;
    private Vector2 pendingSize;
    private Vector2 expandedSize;
    private Vector2 lastPosition;
    private Rect titleAnchor;
    private Rect frame;

    public LinkpearlPopoutWindow(LinkpearlPopouts owner, int slot, Configuration configuration, ChatInbox inbox,
        TabStore tabs, ChatLog log, ChatSend send, GameData gameData, ThemeProvider themes,
        LodestoneService lodestone, NotificationService notifications, ConfirmService confirm)
        : base($"{AepConstants.Name}##LinkpearlPopout{slot}", PopoutFlags)
    {
        this.owner = owner;
        this.slot = slot;
        this.configuration = configuration;
        this.inbox = inbox;
        this.tabs = tabs;
        this.themes = themes;
        this.lodestone = lodestone;
        this.notifications = notifications;
        this.confirm = confirm;
        confirmOverlay = new ConfirmOverlay(confirm, confirmHost);
        var slotText = slot.ToString(Loc.Culture);
        switchMenuId = "linkpearl.popout.switch." + slotText;
        addMenuId = "linkpearl.popout.add." + slotText;
        closeButtonId = "linkpearl.popout.close." + slotText;
        phoneButtonId = "linkpearl.popout.phone." + slotText;
        bellButtonId = "linkpearl.popout.bell." + slotText;
        collapseButtonId = "linkpearl.popout.collapse." + slotText;
        chatMenu = new GameChatMenu("linkpearl.popout.menu." + slotText)
        {
            SendTell = owner.OpenTell,
            LookUp = owner.LookUpInPhone,
            OpenMarket = owner.OpenMarketInPhone,
        };
        thread = new GameChatThread(log, send, gameData)
        {
            Context = chatMenu.Open,
            Link = chatMenu.OpenLink,
        };
        RespectCloseHotkey = false;
    }

    public string Key => active < keys.Count ? keys[active] : string.Empty;

    public bool Bound => keys.Count > 0;

    public bool IsCollapsed => collapsed;

    public int TabCount => keys.Count;

    public long LastActiveTick { get; private set; }

    public Rect Frame => frame;

    public string KeyAt(int index) => index >= 0 && index < keys.Count ? keys[index] : string.Empty;

    public int IndexOfTab(string conversationKey) => PopoutTabs.IndexOf(keys, conversationKey);

    public bool Holds(string conversationKey) => IndexOfTab(conversationKey) >= 0;

    public void Bind(string conversationKey, LinkpearlPopoutState? saved)
    {
        keys.Clear();
        active = 0;
        if (saved is not null)
        {
            for (var index = 0; index < saved.Keys.Count; index++)
            {
                PopoutTabs.Add(keys, saved.Keys[index]);
            }

            active = keys.Count == 0 ? 0 : Math.Clamp(saved.Active, 0, keys.Count - 1);
        }

        if (keys.Count == 0)
        {
            PopoutTabs.Add(keys, conversationKey);
        }

        threadKey = string.Empty;
        attended = false;
        savedPlacement = saved;
        placePending = true;
        fadeSpring.SnapTo(1f);
        Touch();
        IsOpen = !suppressed && Bound;
        BringToFront();
    }

    public bool AddTab(string conversationKey, bool activate)
    {
        if (!Bound || conversationKey.Length == 0)
        {
            return false;
        }

        var existing = PopoutTabs.IndexOf(keys, conversationKey);
        if (existing >= 0)
        {
            if (activate)
            {
                SetActive(existing);
            }

            return true;
        }

        if (!PopoutTabs.Add(keys, conversationKey))
        {
            return false;
        }

        if (activate)
        {
            SetActive(keys.Count - 1);
            return true;
        }

        Touch();
        return true;
    }

    public bool RemoveTab(int index)
    {
        if (index < 0 || index >= keys.Count)
        {
            return false;
        }

        inbox.SetAttended(keys[index], false);
        var wasActive = index == active;
        active = PopoutTabs.Remove(keys, active, index);
        if (keys.Count == 0)
        {
            Unbind();
            return true;
        }

        if (wasActive)
        {
            attended = false;
            threadKey = string.Empty;
            chatMenu.Close();
        }

        switchMenu.Close();
        Touch();
        return true;
    }

    public void FocusTab(string conversationKey)
    {
        SetActive(PopoutTabs.IndexOf(keys, conversationKey));
        Focus();
    }

    public void SetSuppressed(bool value)
    {
        if (suppressed == value)
        {
            return;
        }

        suppressed = value;
        if (value)
        {
            confirm.CancelHost(confirmHost);
        }

        if (!Bound)
        {
            return;
        }

        IsOpen = !value;
    }

    private void ResolvePlacement()
    {
        var zoom = OwnZoom();
        var saved = savedPlacement;
        savedPlacement = null;
        expandedSize = saved is { Width: > 0f, Height: > 0f }
            ? new Vector2(saved.Width, saved.Height)
            : new Vector2(DefaultWidth * zoom, DefaultHeight * zoom);
        collapsed = saved?.Collapsed ?? false;
        collapseSpring.SnapTo(collapsed ? 1f : 0f);
        pendingSize = new Vector2(expandedSize.X, HeightFor(zoom));
        pendingPosition = saved is not null
            ? new Vector2(saved.X, saved.Y)
            : DefaultPosition(pendingSize * UiScale.Global);
    }

    private float HeightFor(float zoom)
    {
        var collapsedHeight = TitleHeight * zoom;
        return expandedSize.Y + (collapsedHeight - expandedSize.Y) * collapseSpring.Value;
    }

    private void StepCollapse(float zoom, float delta)
    {
        var target = collapsed ? 1f : 0f;
        Position = null;
        if (collapseSpring.IsResting(target, TransitionTiming.RestPositionEpsilon,
                TransitionTiming.RestVelocityEpsilon))
        {
            var settling = collapseSpring.Value != target;
            collapseSpring.SnapTo(target);
            if (collapsed)
            {
                Size = new Vector2(expandedSize.X, TitleHeight * zoom);
                SizeCondition = ImGuiCond.Always;
                return;
            }

            if (settling)
            {
                Size = expandedSize;
                SizeCondition = ImGuiCond.Always;
                return;
            }

            Size = null;
            return;
        }

        collapseSpring.Step(target, TransitionTiming.PushSmoothTime, delta);
        Size = new Vector2(expandedSize.X, HeightFor(zoom));
        SizeCondition = ImGuiCond.Always;
    }

    public bool SetCollapsed(bool value)
    {
        if (collapsed == value || !Bound)
        {
            return false;
        }

        collapsed = value;
        if (!value)
        {
            return true;
        }

        switchMenu.Close();
        chatMenu.Close();
        thread.CloseMenus();
        confirm.CancelHost(confirmHost);
        return true;
    }

    private void ToggleCollapsed(bool value)
    {
        if (SetCollapsed(value))
        {
            owner.OnCollapseChanged();
        }
    }

    public void Rebind(string conversationKey)
    {
        if (!Bound || string.Equals(Key, conversationKey, StringComparison.Ordinal))
        {
            return;
        }

        var existing = PopoutTabs.IndexOf(keys, conversationKey);
        if (existing >= 0)
        {
            SetActive(existing);
            return;
        }

        inbox.SetAttended(Key, false);
        keys[active] = conversationKey;
        threadKey = string.Empty;
        attended = false;
        thread.Close();
        chatMenu.Close();
        Touch();
    }

    public void Unbind()
    {
        for (var index = 0; index < keys.Count; index++)
        {
            inbox.SetAttended(keys[index], false);
        }

        keys.Clear();
        active = 0;
        threadKey = string.Empty;
        attended = false;
        dragging = false;
        dropTarget = null;
        thread.Close();
        chatMenu.Close();
        switchMenu.Close();
        confirm.CancelHost(confirmHost);
        IsOpen = false;
    }

    public void Focus()
    {
        ToggleCollapsed(false);
        fadeSpring.SnapTo(1f);
        Touch();
        BringToFront();
    }

    public void ReopenThread() => threadKey = string.Empty;

    public LinkpearlPopoutState Snapshot()
    {
        var state = new LinkpearlPopoutState
        {
            Key = Key,
            Active = active,
            X = frame.Min.X,
            Y = frame.Min.Y,
            Width = expandedSize.X,
            Height = expandedSize.Y,
            Collapsed = collapsed,
        };
        for (var index = 0; index < keys.Count; index++)
        {
            state.Keys.Add(keys[index]);
        }

        return state;
    }

    public override void OnClose()
    {
        if (suppressed)
        {
            return;
        }

        owner.OnWindowClosed(this);
    }

    public override void PreDraw()
    {
        var zoom = OwnZoom();
        UiScale.SetPhone(zoom);
        Plugin.Fonts.SetPhoneZoom(zoom);
        DragScrollHost.Enabled = false;
        positionForced = false;
        if (placePending)
        {
            ResolvePlacement();
            Position = pendingPosition;
            PositionCondition = ImGuiCond.Always;
            Size = pendingSize;
            SizeCondition = ImGuiCond.Always;
            placePending = false;
            positionForced = true;
        }
        else
        {
            StepCollapse(zoom, MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds));
        }

        var resizable = !collapsed && collapseSpring.Value <= 0f;
        Flags = resizable ? PopoutFlags : PopoutFlags | ImGuiWindowFlags.NoResize;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(MinWidth, resizable ? MinHeight : MathF.Min(MinHeight, HeightFor(zoom))),
            MaximumSize = new Vector2(MaxSide, MaxSide),
        };
        var style = ImGui.GetStyle();
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, style.Alpha * IdleAlpha());
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, style.FramePadding * zoom);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, style.ItemSpacing * zoom);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing, style.ItemInnerSpacing * zoom);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, style.ScrollbarSize * zoom);
        ImGui.PushStyleVar(ImGuiStyleVar.GrabMinSize, style.GrabMinSize * zoom);
        ImGui.PushStyleColor(ImGuiCol.ResizeGrip, AppSkin.Transparent);
        ImGui.PushStyleColor(ImGuiCol.ResizeGripHovered, AppSkin.Transparent);
        ImGui.PushStyleColor(ImGuiCol.ResizeGripActive, AppSkin.Transparent);
    }

    public override void PostDraw()
    {
        ImGui.PopStyleColor(GripColorCount);
        ImGui.PopStyleVar(ScaledStyleVarCount);
    }

    public override void Draw()
    {
        if (!Bound)
        {
            IsOpen = false;
            return;
        }

        var position = ImGui.GetWindowPos();
        frame = new Rect(position, position + ImGui.GetWindowSize());
        var hoveredWindow = ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows |
                                                  ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);
        var focusedWindow = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
        if (TryFinishDrag(position, focusedWindow))
        {
            return;
        }

        UiInteract.SetWindowHovered(hoveredWindow);
        UiInteract.SetWindowFocused(focusedWindow);
        inbox.Sync();
        var row = inbox.Find(Key);
        var lively = hoveredWindow || focusedWindow;
        if (focusedWindow)
        {
            Touch();
        }
        else
        {
            switchMenu.Close();
        }

        UpdateAttention(row, !collapsed && lively);
        chatMenu.Gate();
        switchMenu.Gate();
        thread.Gate();
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        fadeSpring.Step(lively || !configuration.LinkpearlPopoutFade ? 1f : 0f,
            TransitionTiming.PresentSmoothTime, delta);
        var confirming = confirmOverlay.CapturesPointer;
        if (confirming && focusedWindow)
        {
            HandleConfirmEscape();
        }

        using (ConfirmHosts.Enter(confirmHost))
        using (Plugin.Fonts.Push(1f))
        {
            var theme = themes.ForApp(true);
            var scale = UiScale.Current;
            var barHeight = MathF.Min(TitleHeight * scale, frame.Height);
            var railHeight = TabbedNow ? (ChipRail.RowHeight + TabRailGap * 2f) * scale : 0f;
            var bodyOpen = frame.Height - barHeight - railHeight >= MinBodyHeight * scale;
            if (!collapsed && collapseSpring.Value <= 0f)
            {
                expandedSize = frame.Size / UiScale.Global;
            }

            using (InputShield.Engage(confirming))
            {
                var flashing = configuration.LinkpearlPopoutFlash && TitleUnread(row) > 0;
                DrawSurface(theme, scale, lively, barHeight + (bodyOpen ? railHeight : 0f), flashing);
                var titleBar = new Rect(frame.Min, new Vector2(frame.Max.X, frame.Min.Y + barHeight));
                DrawTitleBar(titleBar, row, theme, scale, delta, flashing);
                if (bodyOpen)
                {
                    var bodyTop = DrawTabRail(titleBar.Max.Y, theme, scale);
                    var inset = BodyInset * scale;
                    var body = new Rect(new Vector2(frame.Min.X + inset, bodyTop),
                        new Vector2(frame.Max.X - inset, frame.Max.Y - inset));
                    if (row is null)
                    {
                        Typography.DrawCentered(ImGui.GetWindowDrawList(), body.Center, Loc.T(L.Messages.Empty),
                            theme.TextMuted, TextStyles.Callout);
                    }
                    else
                    {
                        OpenThread(row);
                        thread.Draw(body, theme);
                    }

                    DrawGrip(scale);
                }

                DrawSwitchMenu(theme);
                chatMenu.Draw(frame, theme);
            }

            ShellToast.DrawSecondary(frame, theme);
            confirmOverlay.Draw(frame, theme);
            DrawDropHighlight(theme, scale);
        }

        HoverTooltip.Flush();
    }

    private void HandleConfirmEscape()
    {
        ImGui.SetNextFrameWantCaptureKeyboard(true);
        if (!ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            return;
        }

        confirmOverlay.CancelActive();
    }

    private bool TabbedNow => keys.Count > 1;

    private void Touch() => LastActiveTick = Environment.TickCount64;

    private float IdleAlpha()
    {
        if (!configuration.LinkpearlPopoutFade)
        {
            return 1f;
        }

        var idle = Math.Clamp(configuration.LinkpearlPopoutIdleOpacity, MinIdleOpacity, 1f);
        return idle + (1f - idle) * Math.Clamp(fadeSpring.Value, 0f, 1f);
    }

    private bool TryFinishDrag(Vector2 position, bool focusedWindow)
    {
        var travel = MathF.Abs(position.X - lastPosition.X) + MathF.Abs(position.Y - lastPosition.Y);
        lastPosition = position;
        var target = dropTarget;
        if (dragging && target is { Bound: true } && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            dragging = false;
            dropTarget = null;
            owner.Merge(this, target);
            return true;
        }

        if (positionForced || !focusedWindow || !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            dragging = false;
            dropTarget = null;
            return false;
        }

        dragging = dragging || travel > DragThreshold * UiScale.Global;
        dropTarget = dragging ? owner.DropTargetAt(this, ImGui.GetMousePos()) : null;
        return false;
    }

    private void DrawDropHighlight(PhoneTheme theme, float scale)
    {
        if (dropTarget is not { Bound: true } target)
        {
            return;
        }

        var drawList = ImGui.GetForegroundDrawList();
        var rounding = Rounding * scale;
        Squircle.Fill(drawList, target.Frame.Min, target.Frame.Max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, DropHighlightFill)));
        Squircle.Stroke(drawList, target.Frame.Min, target.Frame.Max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, DropHighlightStroke)), Metrics.Stroke.Ring * scale);
    }

    private float DrawTabRail(float top, PhoneTheme theme, float scale)
    {
        if (!TabbedNow)
        {
            return top;
        }

        BuildTabLabels();
        railSkin.Palette = AppPalettes.Linkpearl(theme);
        railSkin.Theme = theme;
        var inset = TabRailInset * scale;
        var rail = new Rect(new Vector2(frame.Min.X + inset, top + TabRailGap * scale),
            new Vector2(frame.Max.X - inset, top + (TabRailGap + ChipRail.RowHeight) * scale));
        var tapped = tabRail.Draw(rail, railSkin, tabLabels.AsSpan(0, keys.Count),
            tabSelected.AsSpan(0, keys.Count), false, null, ChipRail.CompactLabelPadding);
        if (tapped >= 0)
        {
            SetActive(tapped);
        }

        return rail.Max.Y + TabRailGap * scale;
    }

    private void BuildTabLabels()
    {
        for (var index = 0; index < keys.Count; index++)
        {
            var row = inbox.Find(keys[index]);
            var title = row?.Title ?? FallbackTitleFor(keys[index]);
            var unread = row is { HasBadge: true } && index != active ? row.Unread : 0;
            if (!string.Equals(tabTitles[index], title, StringComparison.Ordinal) || tabUnread[index] != unread)
            {
                tabTitles[index] = title;
                tabUnread[index] = unread;
                tabLabels[index] = unread > 0 ? UnreadLabel(title, unread) : title;
            }

            tabSelected[index] = index == active;
        }
    }

    private static string UnreadLabel(string title, int unread) =>
        string.Concat(title, " · ", unread > 99 ? "99+" : unread.ToString(Loc.Culture));

    private void SetActive(int index)
    {
        if (index < 0 || index >= keys.Count || index == active)
        {
            return;
        }

        inbox.SetAttended(Key, false);
        attended = false;
        active = index;
        threadKey = string.Empty;
        chatMenu.Close();
        Touch();
    }

    private float OwnZoom()
    {
        var phoneZoom = PhoneSizeCatalog.ZoomFor(PhoneBounds.ClampWidth(configuration.PhoneWidth));
        return phoneZoom * Math.Clamp(configuration.LinkpearlPopoutTextScale, 0.6f, 1.8f);
    }

    private Vector2 DefaultPosition(Vector2 scaledSize)
    {
        var viewport = ImGui.GetMainViewport();
        var margin = ViewportMargin * UiScale.Global;
        var stagger = StaggerStep * UiScale.Global * slot;
        var target = viewport.Pos + viewport.Size - scaledSize - new Vector2(margin + stagger, margin + stagger);
        target.X = MathF.Max(viewport.Pos.X, target.X);
        target.Y = MathF.Max(viewport.Pos.Y, target.Y);
        return target;
    }

    private void UpdateAttention(InboxRow? row, bool attending)
    {
        if (attending == attended)
        {
            if (attending && row is { Unread: > 0 })
            {
                inbox.MarkRead(row);
            }

            return;
        }

        attended = attending;
        inbox.SetAttended(Key, attending);
        if (!attending)
        {
            inbox.FlushSeen();
            return;
        }

        if (row is not null)
        {
            inbox.MarkRead(row);
        }

        notifications.RemoveGroup(Key);
    }

    private void OpenThread(InboxRow row)
    {
        if (string.Equals(threadKey, row.Key, StringComparison.Ordinal) && thread.IsOpenFor(row.Key))
        {
            return;
        }

        threadKey = row.Key;
        thread.Open(GameChatTargets.For(row));
    }

    private void DrawSurface(PhoneTheme theme, float scale, bool lively, float stripHeight, bool flashing)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = Rounding * scale;
        var opacity = Math.Clamp(configuration.LinkpearlPopoutOpacity, 0.35f, 1f);
        Elevation.Floating(drawList, frame.Min, frame.Max, rounding, scale, lively ? 1f : 0.7f);
        var surface = ImGui.GetColorU32(Palette.WithAlpha(theme.AppBackground, opacity));
        Squircle.FillVerticalGradient(drawList, frame.Min, frame.Max, rounding, surface, surface);
        var titleBottom = MathF.Min(frame.Min.Y + stripHeight, frame.Max.Y);
        var stripInk = flashing
            ? Vector4.Lerp(theme.GroupedCard, FlashInk, Pulse.Wave() * FlashStrength)
            : theme.GroupedCard;
        var strip = ImGui.GetColorU32(Palette.WithAlpha(stripInk, opacity));
        drawList.PushClipRect(frame.Min, new Vector2(frame.Max.X, titleBottom), true);
        Squircle.FillVerticalGradient(drawList, frame.Min, frame.Max, rounding, strip, strip);
        drawList.PopClipRect();
        if (titleBottom < frame.Max.Y)
        {
            drawList.AddLine(new Vector2(frame.Min.X, titleBottom), new Vector2(frame.Max.X, titleBottom),
                ImGui.GetColorU32(Palette.WithAlpha(theme.Separator, theme.Separator.W * opacity)),
                Metrics.Stroke.Hairline);
        }

        Material.EdgeSquircle(drawList, frame.Min, frame.Max, rounding, scale, lively ? 1f : 0.6f);
    }

    private void DrawTitleBar(Rect bar, InboxRow? row, PhoneTheme theme, float scale, float delta, bool flashing)
    {
        var drawList = ImGui.GetWindowDrawList();
        var centerY = bar.Center.Y;
        var radius = ButtonRadius * scale;
        var closeCenter = new Vector2(bar.Max.X - EdgeInset * scale - radius * 0.5f, centerY);
        var phoneCenter = new Vector2(closeCenter.X - ButtonPitch * scale, centerY);
        var bellCenter = new Vector2(phoneCenter.X - ButtonPitch * scale, centerY);
        var collapseCenter = new Vector2(bellCenter.X - ButtonPitch * scale, centerY);
        var muted = row?.Muted ?? false;
        if (HoverButton.Circle(drawList, closeButtonId, closeCenter, radius, FontAwesomeIcon.Times,
                AppSkin.Transparent, theme.TextMuted, delta, 1f, true, Loc.T(L.Common.Close)))
        {
            owner.Close(Key);
            return;
        }

        if (HoverButton.Circle(drawList, phoneButtonId, phoneCenter, radius, FontAwesomeIcon.MobileAlt,
                AppSkin.Transparent, theme.TextMuted, delta, 1f, true, Loc.T(L.Linkpearl.OpenInPhone)))
        {
            owner.OpenInPhone?.Invoke(Key);
        }

        if (row is not null && HoverButton.Circle(drawList, bellButtonId, bellCenter, radius,
                muted ? FontAwesomeIcon.BellSlash : FontAwesomeIcon.Bell, AppSkin.Transparent,
                muted ? theme.Accent : theme.TextMuted, delta, 1f, true,
                Loc.T(muted ? L.Linkpearl.Unmute : L.Linkpearl.Mute)))
        {
            inbox.ToggleMuted(row);
        }

        if (HoverButton.Circle(drawList, collapseButtonId, collapseCenter, radius,
                collapsed ? FontAwesomeIcon.ChevronDown : FontAwesomeIcon.ChevronUp, AppSkin.Transparent,
                theme.TextMuted, delta, 1f, true, Loc.T(collapsed ? L.Linkpearl.Expand : L.Linkpearl.Collapse)))
        {
            ToggleCollapsed(!collapsed);
        }

        var avatarRadius = AvatarRadius * scale;
        var avatarCenter = new Vector2(bar.Min.X + EdgeInset * scale + avatarRadius, centerY);
        DrawAvatar(drawList, avatarCenter, avatarRadius, row, theme);
        var textLeft = avatarCenter.X + avatarRadius + Metrics.Space.Sm * scale;
        var textLimit = collapseCenter.X - radius - Metrics.Space.Sm * scale;
        var title = row?.Title ?? FallbackTitleFor(Key);
        var unread = flashing ? 0 : TitleUnread(row);
        var badgeWidth = 0f;
        if (unread > 0)
        {
            badgeWidth = BadgeWidth(unread, scale) + Metrics.Space.Xs * scale;
        }

        var caretWidth = 10f * scale;
        var titleStyle = TextStyles.Headline;
        var titleSize = Typography.Measure(title, titleStyle);
        var titleWidth = MathF.Min(titleSize.X, MathF.Max(1f, textLimit - textLeft - caretWidth - badgeWidth));
        var titleTop = centerY - titleSize.Y * 0.5f;
        var hitMin = new Vector2(textLeft - 4f * scale, bar.Min.Y + 6f * scale);
        var hitMax = new Vector2(textLeft + titleWidth + caretWidth + 4f * scale, bar.Max.Y - 6f * scale);
        var titleHovered = UiInteract.Hover(hitMin, hitMax);
        if (titleHovered)
        {
            Squircle.Fill(drawList, hitMin, hitMax, Metrics.Radius.Sm * scale,
                ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.06f)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Marquee.DrawLeft(drawList, new MarqueeId(switchMenuId, ".title"), title, textLeft, titleTop, titleWidth, titleStyle,
            theme.TextStrong, titleHovered);
        AppSkin.Icon(drawList, new Vector2(textLeft + titleWidth + CaretGap * scale, centerY + 1f * scale),
            IconGlyph.Of(FontAwesomeIcon.ChevronDown), theme.TextMuted, 0.55f);
        if (unread > 0)
        {
            DrawBadge(drawList, unread, textLeft + titleWidth + caretWidth + Metrics.Space.Xs * scale, centerY,
                theme, scale);
        }

        titleAnchor = new Rect(hitMin, hitMax);
        HoverTooltip.Show(titleAnchor, Loc.T(collapsed ? L.Linkpearl.Expand : L.Linkpearl.SwitchConversation),
            HoverLabelSide.Below);
        if (UiInteract.Click(hitMin, hitMax, titleHovered))
        {
            if (collapsed)
            {
                ToggleCollapsed(false);
            }
            else
            {
                OpenSwitchMenu(titleAnchor);
            }
        }

        if (BarDoubleClicked(bar, titleAnchor, collapseCenter.X - radius))
        {
            ToggleCollapsed(!collapsed);
        }
    }

    private int TitleUnread(InboxRow? row)
    {
        if (collapsed)
        {
            return GroupUnread();
        }

        return row is { HasBadge: true } && !attended ? row.Unread : 0;
    }

    private int GroupUnread()
    {
        var total = 0;
        for (var index = 0; index < keys.Count; index++)
        {
            var row = inbox.Find(keys[index]);
            if (row is { HasBadge: true })
            {
                total += row.Unread;
            }
        }

        return total;
    }

    private static bool BarDoubleClicked(in Rect bar, in Rect titleHit, float buttonsLeft)
    {
        if (!UiInteract.Hover(bar.Min, bar.Max) || !ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
        {
            return false;
        }

        var mouse = ImGui.GetMousePos();
        return mouse.X < buttonsLeft && !titleHit.Contains(mouse);
    }

    private void DrawAvatar(ImDrawListPtr drawList, Vector2 center, float radius, InboxRow? row, PhoneTheme theme)
    {
        if (row is null)
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(theme.SurfaceMuted), 24);
            return;
        }

        if (row.IsTell)
        {
            AvatarView.Draw(drawList, center, radius, theme.Accent, Initials.Of(row.Title), 0.7f,
                lodestone.Avatar(row.Title, row.World, radius * 2f), 24);
            return;
        }

        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        Squircle.Fill(drawList, min, max, radius * 0.62f, ImGui.GetColorU32(Palette.WithAlpha(row.Tint, 0.24f)));
        Typography.DrawCentered(drawList, center, Initials.Of(row.Title), row.Tint, TextStyles.Caption2);
    }

    private string FallbackTitleFor(string conversationKey)
    {
        if (conversationKey.StartsWith("tab:", StringComparison.Ordinal))
        {
            return tabs.Find(conversationKey["tab:".Length..])?.Name ?? Loc.T(L.Apps.Linkpearl);
        }

        var target = ChatStreams.TellTarget(conversationKey);
        var at = target.IndexOf('@');
        var name = at >= 0 ? target[..at] : target;
        return name.Length > 0 ? Loc.Culture.TextInfo.ToTitleCase(name) : Loc.T(L.Apps.Linkpearl);
    }

    private static float BadgeWidth(int unread, float scale)
    {
        var label = unread > 99 ? "99+" : unread.ToString(Loc.Culture);
        var height = 16f * scale;
        return MathF.Max(Typography.Measure(label, TextStyles.Caption2).X + 10f * scale, height);
    }

    private static void DrawBadge(ImDrawListPtr drawList, int unread, float left, float centerY, PhoneTheme theme,
        float scale)
    {
        var label = unread > 99 ? "99+" : unread.ToString(Loc.Culture);
        var height = 16f * scale;
        var width = BadgeWidth(unread, scale);
        var min = new Vector2(left, centerY - height * 0.5f);
        var max = new Vector2(left + width, centerY + height * 0.5f);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(theme.Accent));
        Typography.DrawCentered(drawList, (min + max) * 0.5f, label, White, TextStyles.Caption2);
    }

    private void DrawGrip(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var corner = frame.Max - new Vector2(7f * scale, 7f * scale);
        var arm = GripArm * scale;
        var color = ImGui.GetColorU32(GripInk);
        drawList.AddLine(new Vector2(corner.X - arm, corner.Y), new Vector2(corner.X, corner.Y - arm), color,
            1.4f * scale);
        drawList.AddLine(new Vector2(corner.X - arm * 0.45f, corner.Y), new Vector2(corner.X, corner.Y - arm * 0.45f),
            color, 1.4f * scale);
    }

    private void OpenSwitchMenu(Rect anchor)
    {
        switchItems.Clear();
        switchKeys.Clear();
        switchActions.Clear();
        var grouped = TabbedNow;
        if (grouped)
        {
            for (var index = 0; index < keys.Count; index++)
            {
                var row = inbox.Find(keys[index]);
                AddMenuRow(MenuActivateTab, keys[index], TabMenuLabel(index, row), RowGlyph(row), index == active,
                    false);
            }
        }

        if (configuration.LinkpearlPopoutTabs && keys.Count < PopoutTabs.MaxTabs && HasAddCandidates())
        {
            AddMenuRow(MenuAddTab, string.Empty, Loc.T(L.Linkpearl.AddTab), IconGlyph.Of(FontAwesomeIcon.Plus), false,
                false);
        }

        if (grouped)
        {
            if (owner.CanDetach)
            {
                AddMenuRow(MenuDetachTab, string.Empty, Loc.T(L.Linkpearl.MoveTabOut),
                    IconGlyph.Of(FontAwesomeIcon.ExternalLinkAlt), false, false);
            }

            AddMenuRow(MenuCloseTab, string.Empty, Loc.T(L.Linkpearl.CloseTab), IconGlyph.Of(FontAwesomeIcon.Times),
                false, true);
        }
        else
        {
            AddSwitchRows(inbox.Pinned);
            AddSwitchRows(inbox.Rows);
        }

        if (switchItems.Count == 0)
        {
            return;
        }

        switchMenu.Header = Loc.T(grouped ? L.Linkpearl.WindowTabs : L.Linkpearl.SwitchConversation);
        switchMenu.Toggle(switchMenuId, anchor);
    }

    private void OpenAddMenu()
    {
        switchItems.Clear();
        switchKeys.Clear();
        switchActions.Clear();
        AddCandidateRows(inbox.Pinned);
        AddCandidateRows(inbox.Rows);
        if (switchItems.Count == 0)
        {
            return;
        }

        switchMenu.Header = Loc.T(L.Linkpearl.AddTab);
        switchMenu.Toggle(addMenuId, titleAnchor);
    }

    private void AddSwitchRows(IReadOnlyList<InboxRow> rows)
    {
        for (var index = 0; index < rows.Count && switchItems.Count < SwitchMenuLimit; index++)
        {
            var row = rows[index];
            AddMenuRow(MenuSwitchTo, row.Key, RowLabel(row), RowGlyph(row),
                string.Equals(row.Key, Key, StringComparison.Ordinal), false);
        }
    }

    private bool HasAddCandidates() => HasCandidateIn(inbox.Pinned) || HasCandidateIn(inbox.Rows);

    private bool HasCandidateIn(IReadOnlyList<InboxRow> rows)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            if (!Holds(rows[index].Key))
            {
                return true;
            }
        }

        return false;
    }

    private void AddCandidateRows(IReadOnlyList<InboxRow> rows)
    {
        for (var index = 0; index < rows.Count && switchItems.Count < SwitchMenuLimit; index++)
        {
            var row = rows[index];
            if (Holds(row.Key))
            {
                continue;
            }

            AddMenuRow(MenuAddTarget, row.Key, RowLabel(row), RowGlyph(row), false, false);
        }
    }

    private void AddMenuRow(byte action, string conversationKey, string label, string glyph, bool selected,
        bool danger)
    {
        switchItems.Add(new DropdownMenu.Item(label, glyph, danger, selected));
        switchKeys.Add(conversationKey);
        switchActions.Add(action);
    }

    private static string RowGlyph(InboxRow? row) =>
        row is null || row.IsTell ? IconGlyph.Of(FontAwesomeIcon.User) : IconGlyph.Of(FontAwesomeIcon.Hashtag);

    private static string RowLabel(InboxRow row) => row.HasBadge ? UnreadLabel(row.Title, row.Unread) : row.Title;

    private string TabMenuLabel(int index, InboxRow? row)
    {
        var title = row?.Title ?? FallbackTitleFor(keys[index]);
        return row is { HasBadge: true } ? UnreadLabel(title, row.Unread) : title;
    }

    private void DrawSwitchMenu(PhoneTheme theme)
    {
        if (!switchMenu.IsOpenFor(addMenuId) && !switchMenu.IsOpenFor(switchMenuId))
        {
            return;
        }

        var picked = switchMenu.Draw(frame, theme, CollectionsMarshal.AsSpan(switchItems));
        if (picked < 0)
        {
            return;
        }

        RunMenuAction(switchActions[picked], switchKeys[picked]);
    }

    private void RunMenuAction(byte action, string conversationKey)
    {
        switch (action)
        {
            case MenuActivateTab:
                SetActive(PopoutTabs.IndexOf(keys, conversationKey));
                return;
            case MenuAddTab:
                OpenAddMenu();
                return;
            case MenuAddTarget:
                owner.AddTab(this, conversationKey);
                return;
            case MenuDetachTab:
                owner.Detach(this, active);
                return;
            case MenuCloseTab:
                owner.CloseTab(this, active);
                return;
            case MenuSwitchTo:
                owner.Switch(this, conversationKey);
                return;
        }
    }
}
