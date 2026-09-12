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
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Aetherphone.Windows;

internal sealed partial class LinkpearlPopoutWindow : Window
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
    private const float ButtonRadius = 14f;
    private const float ButtonPitch = 31f;
    private const float ButtonGlyph = 18f;
    private const float EdgeInset = 8f;
    private const float ChipHeight = 30f;
    private const float ChipTileRadius = 10f;
    private const float ChipPadLeft = 5f;
    private const float ChipPadRight = 10f;
    private const float ChipGap = 4f;
    private const float ChipTextGap = 6f;
    private const float ChipMaxLabel = 110f;
    private const float PillHeight = 16f;
    private const float PillPadX = 5f;
    private const float PillGap = 6f;
    private const float StackedAvatarRadius = 12f;
    private const float StackedOverlap = 6f;
    private const int StackedAvatarLimit = 3;
    private const float NewPillHeight = 18f;
    private const float NewPillPadX = 7f;
    private const float StripAccentMix = 0.08f;
    private const float MinOpacity = 0.35f;
    private const float GripArm = 9f;
    private const float MinBodyHeight = 96f;
    private const float FlashStrength = 0.72f;
    private const float DragThreshold = 2f;
    private const float MinIdleOpacity = 0.15f;
    private const int SwitchMenuLimit = 14;
    private const byte MenuActivateTab = 0;
    private const byte MenuAddTab = 1;
    private const byte MenuAddTarget = 2;
    private const byte MenuDetachTab = 3;
    private const byte MenuCloseTab = 4;
    private const byte MenuSwitchTo = 5;
    private const byte MenuToggleMute = 6;
    private const string TitleSeparator = ", ";

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 GripInk = new(1f, 1f, 1f, 0.22f);
    private static readonly Vector4 FlashInk = new(1f, 0.839f, 0.039f, 1f);
    private static readonly TextStyle ChipStyle = TextStyles.Footnote;
    private static readonly TextStyle ChipActiveStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle PillStyle = new(0.62f, FontWeight.SemiBold);

    private readonly LinkpearlPopouts owner;
    private readonly int slot;
    private readonly Configuration configuration;
    private readonly ChatInbox inbox;
    private readonly TabStore tabs;
    private readonly ThemeProvider themes;
    private readonly LodestoneService lodestone;
    private readonly NotificationService notifications;
    private readonly WallpaperImageCache wallpaperImages;
    private readonly GameChatThread thread;
    private readonly GameChatMenu chatMenu;
    private readonly ConfirmService confirm;
    private readonly ConfirmOverlay confirmOverlay;
    private readonly int confirmHost = ConfirmHosts.Reserve();
    private readonly DropdownMenu switchMenu = new() { Detached = true };
    private readonly List<DropdownMenu.Item> switchItems = new(SwitchMenuLimit);
    private readonly List<string> switchKeys = new(SwitchMenuLimit);
    private readonly List<byte> switchActions = new(SwitchMenuLimit);
    private readonly List<string> keys = new(PopoutTabs.MaxTabs);
    private readonly string[] tabTitles = new string[PopoutTabs.MaxTabs];
    private readonly int[] tabUnread = new int[PopoutTabs.MaxTabs];
    private readonly string[] pillTexts = new string[PopoutTabs.MaxTabs];
    private readonly int[] pillCounts = new int[PopoutTabs.MaxTabs];
    private readonly float[] chipWidths = new float[PopoutTabs.MaxTabs];
    private readonly bool[] chipLabelled = new bool[PopoutTabs.MaxTabs];
    private readonly Action<Rect> paintBackdrop;
    private readonly string switchMenuId;
    private readonly string addMenuId;
    private SocialInk ink = ChatThemes.InkFor(ChatThemes.LinkpearlDefaultId);
    private ChatTheme chatTheme = ChatThemes.Resolve(ChatThemes.LinkpearlDefaultId);
    private string collapsedTitle = string.Empty;
    private int collapsedTitleRevision = -1;
    private int titlesRevision;
    private string newPillText = string.Empty;
    private int newPillCount = -1;
    private int active;
    private string threadKey = string.Empty;
    private bool attended;
    private bool placePending;
    private bool collapsed;
    private bool suppressed;
    private bool positionForced;
    private bool dragging;
    private bool focusedLastFrame;
    private Spring collapseSpring;
    private Spring fadeSpring;
    private LinkpearlPopoutState? savedPlacement;
    private LinkpearlPopoutWindow? dropTarget;
    private Vector2 pendingPosition;
    private Vector2 pendingSize;
    private Vector2 expandedSize;
    private Vector2 lastPosition;
    private Rect titleAnchor;
    private Rect chipsBand;
    private Rect frame;

    public LinkpearlPopoutWindow(LinkpearlPopouts owner, int slot, Configuration configuration, ChatInbox inbox,
        TabStore tabs, ChatLog log, ChatSend send, GameData gameData, ThemeProvider themes,
        LodestoneService lodestone, NotificationService notifications, ConfirmService confirm,
        WallpaperImageCache wallpaperImages)
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
        this.wallpaperImages = wallpaperImages;
        confirmOverlay = new ConfirmOverlay(confirm, confirmHost);
        var slotText = slot.ToString(Loc.Culture);
        switchMenuId = "linkpearl.popout.switch." + slotText;
        addMenuId = "linkpearl.popout.add." + slotText;
        textSizeMenuId = "linkpearl.popout.textSize." + slotText;
        settingIds = new PopoutSettingIds(slotText);
        chatMenu = new GameChatMenu("linkpearl.popout.menu." + slotText)
        {
            Detached = true,
            SendTell = owner.OpenTell,
            LookUp = owner.LookUpInPhone,
            OpenMarket = owner.OpenMarketInPhone,
        };
        paintBackdrop = PaintBackdrop;
        thread = new GameChatThread(log, send, gameData)
        {
            Context = chatMenu.Open,
            Link = chatMenu.OpenLink,
            Lodestone = lodestone,
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
        settingsOpen = false;
        savedPlacement = saved;
        placePending = true;
        fadeSpring.SnapTo(1f);
        InvalidateTitles();
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

        InvalidateTitles();
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
        InvalidateTitles();
        if (keys.Count == 0)
        {
            Unbind();
            return true;
        }

        if (wasActive)
        {
            attended = false;
            threadKey = string.Empty;
            settingsOpen = false;
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
            : owner.DefaultPosition(pendingSize * UiScale.Global);
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

        CloseMenus();
        settingsOpen = false;
        confirm.CancelHost(confirmHost);
        return true;
    }

    private void CloseMenus()
    {
        switchMenu.Close();
        chatMenu.Close();
        thread.CloseMenus();
    }

    private void InvalidateTitles()
    {
        Array.Clear(tabTitles);
        Array.Clear(pillTexts);
        titlesRevision++;
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
        settingsOpen = false;
        InvalidateTitles();
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
        settingsOpen = false;
        dragging = false;
        dropTarget = null;
        InvalidateTitles();
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

        UpdateAttention(row, !collapsed && lively);
        if (focusedLastFrame && !focusedWindow)
        {
            CloseMenus();
        }

        focusedLastFrame = focusedWindow;
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

        ResolveTheme();
        using (ConfirmHosts.Enter(confirmHost))
        using (Plugin.Fonts.Push(1f))
        {
            var theme = PhoneTheme.WithAccent(themes.ForApp(false), chatTheme.Accent);
            var scale = UiScale.Current;
            var barHeight = MathF.Min(TitleHeight * scale, frame.Height);
            var bodyOpen = frame.Height - barHeight >= MinBodyHeight * scale;
            if (!collapsed && collapseSpring.Value <= 0f)
            {
                expandedSize = frame.Size / UiScale.Global;
            }

            using (InputShield.Engage(confirming))
            {
                var flashing = configuration.LinkpearlPopoutFlash && TitleUnread(row) > 0;
                DrawSurface(scale, lively, barHeight, flashing);
                var strip = new Rect(frame.Min, new Vector2(frame.Max.X, frame.Min.Y + barHeight));
                DrawStrip(strip, row, scale, flashing);
                if (bodyOpen)
                {
                    var inset = BodyInset * scale;
                    var body = new Rect(new Vector2(frame.Min.X + inset, strip.Max.Y),
                        new Vector2(frame.Max.X - inset, frame.Max.Y - inset));
                    if (row is null)
                    {
                        Typography.DrawCentered(ImGui.GetWindowDrawList(), body.Center, Loc.T(L.Messages.Empty),
                            ink.MutedInk, TextStyles.Callout);
                    }
                    else if (settingsOpen)
                    {
                        DrawSettingsPanel(body, theme, row);
                    }
                    else
                    {
                        OpenThread(row);
                        thread.BubbleStyle = ChatThemes.BubbleStyleFor(chatTheme);
                        thread.Backdrop = paintBackdrop;
                        thread.Draw(body, theme);
                    }

                    DrawGrip(scale);
                }

                DrawSwitchMenu(theme);
                chatMenu.Draw(PhoneBounds.Viewport(), theme);
            }

            ShellToast.DrawSecondary(frame, theme);
            confirmOverlay.Draw(frame, theme);
            DrawDropHighlight(scale);
        }

        HoverTooltip.Flush();
    }

    private void ResolveTheme()
    {
        var id = configuration.LinkpearlChatTheme.Length > 0
            ? configuration.LinkpearlChatTheme
            : ChatThemes.LinkpearlDefaultId;
        chatTheme = ChatThemes.Resolve(id);
        ink = ChatThemes.InkFor(id);
    }

    private void PaintBackdrop(Rect listRect)
    {
        var id = ChatWallpapers.Effective(configuration.LinkpearlChatWallpapers, configuration.LinkpearlWallpaper,
            Key);
        if (id.Length == 0 && !configuration.LinkpearlWallpaperPattern)
        {
            return;
        }

        ChatWallpapers.Paint(ImGui.GetWindowDrawList(), listRect, id, configuration.LinkpearlWallpaperPattern,
            wallpaperImages, Opacity());
    }

    private float Opacity() => Math.Clamp(configuration.LinkpearlPopoutOpacity, MinOpacity, 1f);

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

    private void DrawDropHighlight(float scale)
    {
        if (dropTarget is not { Bound: true } target)
        {
            return;
        }

        var drawList = ImGui.GetForegroundDrawList();
        var rounding = Rounding * scale;
        Squircle.Fill(drawList, target.Frame.Min, target.Frame.Max, rounding, ImGui.GetColorU32(ink.AccentWash));
        Squircle.Stroke(drawList, target.Frame.Min, target.Frame.Max, rounding, ImGui.GetColorU32(ink.AccentLink),
            Metrics.Stroke.Ring * scale);
    }

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
        settingsOpen = false;
        chatMenu.Close();
        Touch();
    }

    private float OwnZoom()
    {
        var phoneZoom = PhoneSizeCatalog.ZoomFor(PhoneBounds.ClampWidth(configuration.PhoneWidth));
        return phoneZoom * Math.Clamp(configuration.LinkpearlPopoutTextScale, 0.6f, 1.8f);
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
        if (string.Equals(threadKey, row.Key, StringComparison.Ordinal) && thread.IsOpenFor(row.Key)
            && thread.Density == row.Density)
        {
            return;
        }

        threadKey = row.Key;
        thread.Open(GameChatTargets.For(row));
    }

    private void DrawSurface(float scale, bool lively, float stripHeight, bool flashing)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = Rounding * scale;
        var opacity = Opacity();
        Elevation.Floating(drawList, frame.Min, frame.Max, rounding, scale, lively ? 1f : 0.7f);
        var surface = ImGui.GetColorU32(Palette.WithAlpha(ChatThemes.Body, opacity));
        Squircle.FillVerticalGradient(drawList, frame.Min, frame.Max, rounding, surface, surface);
        var titleBottom = MathF.Min(frame.Min.Y + stripHeight, frame.Max.Y);
        var stripInk = Palette.Mix(ChatThemes.Body, chatTheme.Accent, StripAccentMix);
        if (flashing)
        {
            stripInk = Vector4.Lerp(stripInk, FlashInk, Pulse.Wave() * FlashStrength);
        }

        var strip = ImGui.GetColorU32(Palette.WithAlpha(stripInk, opacity));
        drawList.PushClipRect(frame.Min, new Vector2(frame.Max.X, titleBottom), true);
        Squircle.FillVerticalGradient(drawList, frame.Min, frame.Max, rounding, strip, strip);
        drawList.PopClipRect();
        if (titleBottom < frame.Max.Y)
        {
            drawList.AddLine(new Vector2(frame.Min.X, titleBottom), new Vector2(frame.Max.X, titleBottom),
                ImGui.GetColorU32(Palette.WithAlpha(ink.Hairline, ink.Hairline.W * opacity)), Metrics.Stroke.Hairline);
        }

        Squircle.Stroke(drawList, frame.Min, frame.Max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(ink.GlassStroke, ink.GlassStroke.W * (lively ? 1f : 0.7f))), 1f);
    }

    private void DrawStrip(Rect bar, InboxRow? row, float scale, bool flashing)
    {
        var drawList = ImGui.GetWindowDrawList();
        var centerY = bar.Center.Y;
        var radius = ButtonRadius * scale;
        var closeCenter = new Vector2(bar.Max.X - EdgeInset * scale - radius, centerY);
        var phoneCenter = new Vector2(closeCenter.X - ButtonPitch * scale, centerY);
        var collapseCenter = new Vector2(phoneCenter.X - ButtonPitch * scale, centerY);
        if (SocialChrome.DrawHeaderIcon(drawList, closeCenter, radius, PhoneIcons.X, ButtonGlyph, Loc.T(L.Common.Close),
                ink, ink.MutedInk))
        {
            owner.Close(Key);
            return;
        }

        if (SocialChrome.DrawHeaderIcon(drawList, phoneCenter, radius, PhoneIcons.DeviceMobile, ButtonGlyph,
                Loc.T(L.Linkpearl.OpenInPhone), ink, ink.MutedInk))
        {
            owner.OpenInPhone?.Invoke(Key);
        }

        if (SocialChrome.DrawHeaderIcon(drawList, collapseCenter, radius,
                collapsed ? PhoneIcons.ChevronDown : PhoneIcons.ChevronUp, ButtonGlyph,
                Loc.T(collapsed ? L.Linkpearl.Expand : L.Linkpearl.Collapse), ink, ink.MutedInk))
        {
            ToggleCollapsed(!collapsed);
        }

        var buttonsLeft = collapseCenter.X - radius - Metrics.Space.Sm * scale;
        if (!collapsed)
        {
            var settingsCenter = new Vector2(collapseCenter.X - ButtonPitch * scale, centerY);
            if (SocialChrome.DrawHeaderIcon(drawList, settingsCenter, radius, PhoneIcons.Settings, ButtonGlyph,
                    Loc.T(L.Linkpearl.ChatSettings), ink, ink.MutedInk, settingsOpen))
            {
                ToggleSettings();
            }

            buttonsLeft = settingsCenter.X - radius - Metrics.Space.Sm * scale;
        }

        BuildTabLabels();
        if (collapsed)
        {
            DrawCollapsedStrip(bar, buttonsLeft, scale, flashing);
        }
        else
        {
            DrawTabChips(bar, buttonsLeft, scale, flashing);
        }

        if (BarDoubleClicked(bar, chipsBand, buttonsLeft))
        {
            ToggleCollapsed(!collapsed);
        }
    }

    private void DrawTabChips(Rect bar, float buttonsLeft, float scale, bool flashing)
    {
        var drawList = ImGui.GetWindowDrawList();
        var centerY = bar.Center.Y;
        var left = bar.Min.X + EdgeInset * scale;
        var showPlus = configuration.LinkpearlPopoutTabs && keys.Count < PopoutTabs.MaxTabs && HasAddCandidates();
        var plusRadius = ButtonRadius * scale;
        var plusWidth = showPlus ? plusRadius * 2f + ChipGap * scale : 0f;
        var available = MathF.Max(0f, buttonsLeft - left - plusWidth);
        var gap = ChipGap * scale;
        var total = 0f;
        for (var index = 0; index < keys.Count; index++)
        {
            chipLabelled[index] = true;
            chipWidths[index] = ChipWidth(index, true, flashing, scale);
            total += chipWidths[index] + (index > 0 ? gap : 0f);
        }

        if (total > available)
        {
            var others = 0f;
            for (var index = 0; index < keys.Count; index++)
            {
                if (index == active)
                {
                    continue;
                }

                chipLabelled[index] = false;
                chipWidths[index] = ChipWidth(index, false, flashing, scale);
                others += chipWidths[index] + gap;
            }

            chipWidths[active] = MathF.Max(ChipWidth(active, false, flashing, scale),
                MathF.Min(chipWidths[active], available - others));
        }

        var x = left;
        var chipHeight = ChipHeight * scale;
        for (var index = 0; index < keys.Count; index++)
        {
            var rect = new Rect(new Vector2(x, centerY - chipHeight * 0.5f),
                new Vector2(x + chipWidths[index], centerY + chipHeight * 0.5f));
            DrawChip(drawList, index, rect, index == active, chipLabelled[index], flashing, scale);
            x = rect.Max.X + gap;
        }

        chipsBand = new Rect(new Vector2(left, bar.Min.Y), new Vector2(x, bar.Max.Y));
        if (!showPlus)
        {
            return;
        }

        var plusCenter = new Vector2(x + plusRadius, centerY);
        if (SocialChrome.DrawHeaderIcon(drawList, plusCenter, plusRadius, PhoneIcons.Plus, ButtonGlyph,
                Loc.T(L.Linkpearl.AddTab), ink, ink.MutedInk))
        {
            OpenAddMenu();
        }

        chipsBand = new Rect(chipsBand.Min, new Vector2(plusCenter.X + plusRadius, bar.Max.Y));
    }

    private float ChipWidth(int index, bool labelled, bool flashing, float scale)
    {
        var width = (ChipPadLeft + ChipTileRadius * 2f) * scale;
        if (labelled)
        {
            var style = index == active ? ChipActiveStyle : ChipStyle;
            width += ChipTextGap * scale + MathF.Min(ChipMaxLabel * scale, Typography.Measure(tabTitles[index], style).X);
        }

        var unread = ChipUnread(index, flashing);
        if (unread > 0)
        {
            width += PillGap * scale + PillWidth(PillLabel(index, unread), scale);
        }

        return width + (labelled ? ChipPadRight : ChipPadLeft) * scale;
    }

    private string PillLabel(int index, int unread)
    {
        if (pillCounts[index] == unread && pillTexts[index] is not null)
        {
            return pillTexts[index];
        }

        pillCounts[index] = unread;
        pillTexts[index] = CountText(unread);
        return pillTexts[index];
    }

    private static string CountText(int count) => count > 99 ? "99+" : count.ToString(Loc.Culture);

    private int ChipUnread(int index, bool flashing)
    {
        if (index == active)
        {
            return flashing ? 0 : tabUnread[index];
        }

        return tabUnread[index];
    }

    private static float PillWidth(string label, float scale)
    {
        var height = PillHeight * scale;
        return MathF.Max(Typography.Measure(label, PillStyle).X + PillPadX * 2f * scale, height);
    }

    private void DrawChip(ImDrawListPtr drawList, int index, Rect rect, bool isActive, bool labelled, bool flashing,
        float scale)
    {
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var rounding = rect.Height * 0.5f;
        if (isActive)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(ink.AccentWash));
        }
        else if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(ink.FieldFill));
        }

        var tileRadius = ChipTileRadius * scale;
        var tileCenter = new Vector2(rect.Min.X + ChipPadLeft * scale + tileRadius, rect.Center.Y);
        var row = inbox.Find(keys[index]);
        DrawTabAvatar(drawList, tileCenter, tileRadius, row);
        var cursor = tileCenter.X + tileRadius;
        var unread = ChipUnread(index, flashing);
        var pillLabel = unread > 0 ? PillLabel(index, unread) : string.Empty;
        var pillWidth = unread > 0 ? PillWidth(pillLabel, scale) : 0f;
        if (labelled)
        {
            var style = isActive ? ChipActiveStyle : ChipStyle;
            var labelLeft = cursor + ChipTextGap * scale;
            var labelRight = rect.Max.X - ChipPadRight * scale - (unread > 0 ? pillWidth + PillGap * scale : 0f);
            var label = Typography.FitText(tabTitles[index], MathF.Max(1f, labelRight - labelLeft), style);
            var labelSize = Typography.Measure(label, style);
            Typography.Draw(drawList, new Vector2(labelLeft, rect.Center.Y - labelSize.Y * 0.5f), label,
                isActive ? ink.TitleInk : ink.MutedInk, style);
            cursor = labelLeft + labelSize.X;
        }
        else
        {
            HoverTooltip.Show(rect, tabTitles[index], HoverLabelSide.Below);
        }

        if (unread > 0)
        {
            DrawPill(drawList, pillLabel, cursor + PillGap * scale, rect.Center.Y, pillWidth, scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (isActive)
        {
            titleAnchor = rect;
            HoverTooltip.Show(rect, Loc.T(L.Linkpearl.SwitchConversation), HoverLabelSide.Below);
        }

        if (!UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            return;
        }

        if (isActive)
        {
            OpenSwitchMenu(rect);
        }
        else
        {
            SetActive(index);
        }
    }

    private void DrawPill(ImDrawListPtr drawList, string label, float left, float centerY, float width, float scale)
    {
        var height = PillHeight * scale;
        var min = new Vector2(left, centerY - height * 0.5f);
        var max = new Vector2(left + width, centerY + height * 0.5f);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(chatTheme.Badge));
        Typography.DrawCentered(drawList, (min + max) * 0.5f, label, White, PillStyle);
    }

    private void DrawCollapsedStrip(Rect bar, float buttonsLeft, float scale, bool flashing)
    {
        var drawList = ImGui.GetWindowDrawList();
        var centerY = bar.Center.Y;
        var radius = StackedAvatarRadius * scale;
        var x = bar.Min.X + EdgeInset * scale + radius;
        var shown = Math.Min(keys.Count, StackedAvatarLimit);
        for (var index = 0; index < shown; index++)
        {
            var center = new Vector2(x + index * (radius * 2f - StackedOverlap * scale), centerY);
            drawList.AddCircleFilled(center, radius + 1.5f * scale, ImGui.GetColorU32(ChatThemes.Body), 24);
            DrawTabAvatar(drawList, center, radius, inbox.Find(keys[index]));
        }

        var textLeft = x + radius + (shown - 1) * (radius * 2f - StackedOverlap * scale) + ChipTextGap * scale;
        var unread = flashing ? 0 : GroupUnread();
        var pillWidth = 0f;
        if (unread > 0)
        {
            var pill = NewPill(unread);
            var pillSize = Typography.Measure(pill, PillStyle);
            pillWidth = pillSize.X + NewPillPadX * 2f * scale;
            var pillHeight = NewPillHeight * scale;
            var pillMin = new Vector2(buttonsLeft - pillWidth, centerY - pillHeight * 0.5f);
            var pillMax = new Vector2(buttonsLeft, centerY + pillHeight * 0.5f);
            Squircle.Fill(drawList, pillMin, pillMax, pillHeight * 0.5f, ImGui.GetColorU32(chatTheme.Badge));
            Typography.DrawCentered(drawList, (pillMin + pillMax) * 0.5f, pill, White, PillStyle);
            pillWidth += PillGap * scale;
        }

        var textRight = buttonsLeft - pillWidth;
        var title = CollapsedTitle();
        var titleTop = centerY - Typography.LineHeight(ChipActiveStyle) * 0.5f;
        var hovered = UiInteract.Hover(new Vector2(textLeft, bar.Min.Y), new Vector2(textRight, bar.Max.Y));
        Marquee.DrawLeft(drawList, new MarqueeId(switchMenuId, ".collapsed"), title, textLeft, titleTop,
            MathF.Max(1f, textRight - textLeft), ChipActiveStyle, ink.TitleInk, hovered);
        titleAnchor = new Rect(new Vector2(textLeft, bar.Min.Y), new Vector2(textRight, bar.Max.Y));
        chipsBand = titleAnchor;
        HoverTooltip.Show(titleAnchor, Loc.T(L.Linkpearl.Expand), HoverLabelSide.Below);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(titleAnchor.Min, titleAnchor.Max, hovered))
        {
            ToggleCollapsed(false);
        }
    }

    private string NewPill(int unread)
    {
        if (newPillCount == unread)
        {
            return newPillText;
        }

        newPillCount = unread;
        newPillText = Loc.T(L.Linkpearl.NewCount, CountText(unread));
        return newPillText;
    }

    private string CollapsedTitle()
    {
        if (collapsedTitleRevision == titlesRevision)
        {
            return collapsedTitle;
        }

        collapsedTitleRevision = titlesRevision;
        collapsedTitle = keys.Count switch
        {
            0 => string.Empty,
            1 => tabTitles[0],
            _ => string.Join(TitleSeparator, tabTitles, 0, keys.Count),
        };
        return collapsedTitle;
    }

    private void BuildTabLabels()
    {
        for (var index = 0; index < keys.Count; index++)
        {
            var row = inbox.Find(keys[index]);
            var title = row is not null
                ? row.IsTell ? NameMask.Display(row.Title) : row.Title
                : tabTitles[index] ?? FallbackTitleFor(keys[index]);
            var unread = row is { HasBadge: true } && (index != active || !attended) ? row.Unread : 0;
            if (!string.Equals(tabTitles[index], title, StringComparison.Ordinal))
            {
                tabTitles[index] = title;
                titlesRevision++;
            }

            tabUnread[index] = unread;
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

    private static bool BarDoubleClicked(in Rect bar, in Rect exclusion, float buttonsLeft)
    {
        if (!UiInteract.Hover(bar.Min, bar.Max) || !ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
        {
            return false;
        }

        var mouse = ImGui.GetMousePos();
        return mouse.X < buttonsLeft && !exclusion.Contains(mouse);
    }

    private void DrawTabAvatar(ImDrawListPtr drawList, Vector2 center, float radius, InboxRow? row)
    {
        if (row is null)
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ink.FieldFill), 24);
            return;
        }

        GameChatTiles.DrawAvatar(drawList, center, radius, row, lodestone, ink.Accent);
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

        if (inbox.Find(Key) is { } current)
        {
            AddMenuRow(MenuToggleMute, Key, Loc.T(current.Muted ? L.Linkpearl.Unmute : L.Linkpearl.Mute),
                current.Muted ? PhoneIcons.Bell : PhoneIcons.BellOff, false, false);
        }

        if (configuration.LinkpearlPopoutTabs && keys.Count < PopoutTabs.MaxTabs && HasAddCandidates())
        {
            AddMenuRow(MenuAddTab, string.Empty, Loc.T(L.Linkpearl.AddTab), PhoneIcons.Plus, false, false);
        }

        if (grouped)
        {
            if (owner.CanDetach)
            {
                AddMenuRow(MenuDetachTab, string.Empty, Loc.T(L.Linkpearl.MoveTabOut), PhoneIcons.ExternalLink, false,
                    false);
            }

            AddMenuRow(MenuCloseTab, string.Empty, Loc.T(L.Linkpearl.CloseTab), PhoneIcons.X, false, true);
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

    private static string RowGlyph(InboxRow? row) => row is null || row.IsTell ? PhoneIcons.User : PhoneIcons.Hash;

    private static string RowLabel(InboxRow row)
    {
        var title = row.IsTell ? NameMask.Display(row.Title) : row.Title;
        return row.HasBadge ? UnreadLabel(title, row.Unread) : title;
    }

    private static string UnreadLabel(string title, int unread) => string.Concat(title, " · ", CountText(unread));

    private string TabMenuLabel(int index, InboxRow? row)
    {
        var title = tabTitles[index] ?? FallbackTitleFor(keys[index]);
        return row is { HasBadge: true } ? UnreadLabel(title, row.Unread) : title;
    }

    private void DrawSwitchMenu(PhoneTheme theme)
    {
        if (switchMenu.IsOpenFor(textSizeMenuId))
        {
            DrawTextSizeMenu(theme);
            return;
        }

        if (!switchMenu.IsOpenFor(addMenuId) && !switchMenu.IsOpenFor(switchMenuId))
        {
            return;
        }

        var picked = switchMenu.Draw(PhoneBounds.Viewport(), theme, CollectionsMarshal.AsSpan(switchItems));
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
            case MenuToggleMute:
                if (inbox.Find(conversationKey) is { } row)
                {
                    inbox.ToggleMuted(row);
                }

                return;
        }
    }
}
