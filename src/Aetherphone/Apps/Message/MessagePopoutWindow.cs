using System.Runtime.InteropServices;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Message;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;

namespace Aetherphone.Apps.Message;

internal sealed class MessagePopoutWindow : Window
{
    public const float DefaultWidth = 340f;
    public const float DefaultHeight = 470f;

    private const ImGuiWindowFlags PopoutFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar |
                                                 ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoCollapse |
                                                 ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings |
                                                 ImGuiWindowFlags.NoFocusOnAppearing;

    private const int ScaledStyleVarCount = 7;
    private const int GripColorCount = 3;
    private const float MinWidth = 264f;
    private const float MinHeight = 220f;
    private const float MaxSide = 2400f;
    private const float TitleHeight = AppHeader.Height;
    private const float Rounding = 18f;
    private const float BodyInset = 4f;
    private const float AvatarRadius = 14f;
    private const float ButtonRadius = 14f;
    private const float ButtonPitch = 31f;
    private const float EdgeInset = 14f;
    private const float CaretGap = 6f;
    private const float StaggerStep = 28f;
    private const float ViewportMargin = 24f;
    private const float GripArm = 9f;
    private const float MinBodyHeight = 120f;
    private const int SwitchMenuLimit = 14;

    private static readonly Vector4 GripInk = new(1f, 1f, 1f, 0.22f);
    private static readonly TextStyle TitleStyle = TextStyles.Headline;
    private static readonly TextStyle SubtitleStyle = new(0.76f, FontWeight.Regular);

    private readonly MessagePopouts owner;
    private readonly int slot;
    private readonly MessagePopoutServices services;
    private readonly Configuration configuration;
    private readonly ThemeProvider themes;
    private readonly AppSkin ui = new(AppPalettes.Message);
    private readonly ConfirmService confirm;
    private readonly ConfirmOverlay confirmOverlay;
    private readonly int confirmHost = ConfirmHosts.Reserve();
    private readonly DropdownMenu switchMenu = new() { Detached = true };
    private readonly List<DropdownMenu.Item> switchItems = new(SwitchMenuLimit);
    private readonly List<string> switchIds = new(SwitchMenuLimit);
    private readonly List<string> switchTitles = new(SwitchMenuLimit);
    private readonly string switchMenuId;
    private readonly string closeButtonId;
    private readonly string phoneButtonId;
    private readonly string collapseButtonId;

    private DirectMessagesStore? store;
    private PopoutThreadView? view;
    private string conversationId = string.Empty;
    private string title = string.Empty;
    private PopoutScreen screen = PopoutScreen.Thread;
    private string overlayMessageId = string.Empty;
    private bool placePending;
    private bool collapsed;
    private bool focusedLastFrame;
    private Spring collapseSpring;
    private MessagePopoutState? savedPlacement;
    private Vector2 pendingPosition;
    private Vector2 pendingSize;
    private Vector2 expandedSize;
    private Rect titleAnchor;
    private Rect frame;

    public MessagePopoutWindow(MessagePopouts owner, int slot, MessagePopoutServices services)
        : base($"{AepConstants.Name}##MessagePopout{slot}", PopoutFlags)
    {
        this.owner = owner;
        this.slot = slot;
        this.services = services;
        configuration = services.Configuration;
        themes = services.Themes;
        confirm = services.Confirm;
        confirmOverlay = new ConfirmOverlay(confirm, confirmHost);
        var slotText = slot.ToString(Loc.Culture);
        switchMenuId = "message.popout.switch." + slotText;
        closeButtonId = "message.popout.close." + slotText;
        phoneButtonId = "message.popout.phone." + slotText;
        collapseButtonId = "message.popout.collapse." + slotText;
        RespectCloseHotkey = false;
    }

    private enum PopoutScreen : byte
    {
        Thread,
        Picker,
        Reactions,
        Encryption,
    }

    public bool Bound => conversationId.Length > 0;

    public PhoneTheme CurrentTheme => themes.ForApp(owner.Owner.WantsSystemTheme);

    public IPhoneApp OwnerApp => owner.Owner;

    public bool Holds(string candidate) => string.Equals(conversationId, candidate, StringComparison.Ordinal);

    public void Bind(string candidateId, string candidateTitle, MessagePopoutState? saved)
    {
        if (candidateId.Length == 0)
        {
            return;
        }

        conversationId = candidateId;
        title = candidateTitle;
        screen = PopoutScreen.Thread;
        store ??= services.CreateDetachedStore();
        view ??= new PopoutThreadView(this, store);
        savedPlacement = saved;
        placePending = true;
        IsOpen = true;
        BringToFront();
    }

    public void Rebind(string candidateId, string candidateTitle)
    {
        if (!Bound || candidateId.Length == 0 || Holds(candidateId))
        {
            return;
        }

        conversationId = candidateId;
        title = candidateTitle;
        screen = PopoutScreen.Thread;
        CloseMenus();
    }

    public void Unbind()
    {
        if (view is { } openView)
        {
            openView.OnAppClosed();
            openView.Dispose();
        }

        view = null;
        store?.Dispose();
        store = null;
        conversationId = string.Empty;
        title = string.Empty;
        screen = PopoutScreen.Thread;
        switchMenu.Close();
        confirm.CancelHost(confirmHost);
        IsOpen = false;
    }

    public void Focus()
    {
        SetCollapsed(false);
        BringToFront();
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

        screen = PopoutScreen.Thread;
        CloseMenus();
        confirm.CancelHost(confirmHost);
        return true;
    }

    public MessagePopoutState Snapshot() =>
        new()
        {
            ConversationId = conversationId,
            Title = HeaderTitle(),
            X = frame.Min.X,
            Y = frame.Min.Y,
            Width = expandedSize.X,
            Height = expandedSize.Y,
            Collapsed = collapsed,
        };

    public override void OnClose() => owner.OnWindowClosed(this);

    public override void PreDraw()
    {
        var zoom = OwnZoom();
        UiScale.SetPhone(zoom);
        Plugin.Fonts.SetPhoneZoom(zoom);
        DragScrollHost.Enabled = false;
        if (placePending)
        {
            ResolvePlacement(zoom);
            Position = pendingPosition;
            PositionCondition = ImGuiCond.Always;
            Size = pendingSize;
            SizeCondition = ImGuiCond.Always;
            placePending = false;
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
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, style.Alpha);
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
        if (!Bound || view is null)
        {
            IsOpen = false;
            return;
        }

        var position = ImGui.GetWindowPos();
        frame = new Rect(position, position + ImGui.GetWindowSize());
        var hoveredWindow = ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows |
                                                  ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);
        var focusedWindow = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
        UiInteract.SetWindowHovered(hoveredWindow);
        UiInteract.SetWindowFocused(focusedWindow);
        if (focusedLastFrame && !focusedWindow)
        {
            CloseMenus();
        }

        focusedLastFrame = focusedWindow;
        view.GateMenus();
        switchMenu.Gate();
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var confirming = confirmOverlay.CapturesPointer;
        if (confirming && focusedWindow)
        {
            HandleConfirmEscape();
        }

        using (ConfirmHosts.Enter(confirmHost))
        using (Plugin.Fonts.Push(1f))
        {
            var theme = CurrentTheme;
            ui.Palette = MessageThemes.PaletteFor(configuration.MessageChatTheme);
            ui.Theme = theme;
            var scale = UiScale.Current;
            var barHeight = MathF.Min(TitleHeight * scale, frame.Height);
            var bodyOpen = frame.Height - barHeight >= MinBodyHeight * scale;
            if (!collapsed && collapseSpring.Value <= 0f)
            {
                expandedSize = frame.Size / UiScale.Global;
            }

            using (InputShield.Engage(confirming))
            {
                DrawSurface(theme, scale, hoveredWindow || focusedWindow, barHeight);
                var titleBar = new Rect(frame.Min, new Vector2(frame.Max.X, frame.Min.Y + barHeight));
                if (screen == PopoutScreen.Thread)
                {
                    DrawTitleBar(titleBar, theme, scale, delta);
                }

                if (bodyOpen)
                {
                    DrawBody(theme, scale);
                    DrawGrip(scale);
                }

                DrawSwitchMenu(theme);
            }

            ShellToast.DrawSecondary(frame, theme);
            confirmOverlay.Draw(frame, theme);
        }

        HoverTooltip.Flush();
    }

    private void DrawBody(PhoneTheme theme, float scale)
    {
        if (view is null || store is null)
        {
            return;
        }

        var inset = BodyInset * scale;
        var body = new Rect(new Vector2(frame.Min.X + inset, frame.Min.Y),
            new Vector2(frame.Max.X - inset, frame.Max.Y - inset));
        if (!store.IsSignedIn)
        {
            Typography.DrawCentered(ImGui.GetWindowDrawList(), body.Center, Loc.T(L.Message.PopoutSignedOut),
                theme.TextMuted, TextStyles.Callout);
            return;
        }

        switch (screen)
        {
            case PopoutScreen.Picker:
                view.DrawImagePicker(body, conversationId);
                return;
            case PopoutScreen.Reactions:
                view.DrawReactions(body, overlayMessageId);
                return;
            case PopoutScreen.Encryption:
                view.DrawEncryptionScreen(body);
                return;
            default:
                view.Draw(body, conversationId);
                return;
        }
    }

    public void ShowPicker()
    {
        screen = PopoutScreen.Picker;
        CloseMenus();
    }

    public void ShowReactions(string messageId)
    {
        overlayMessageId = messageId;
        screen = PopoutScreen.Reactions;
        CloseMenus();
    }

    public void ShowEncryption()
    {
        screen = PopoutScreen.Encryption;
        CloseMenus();
    }

    public void BackToThread() => screen = PopoutScreen.Thread;

    public void OpenInPhone() => owner.OpenInPhone?.Invoke(conversationId);

    private void HandleConfirmEscape()
    {
        ImGui.SetNextFrameWantCaptureKeyboard(true);
        if (!ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            return;
        }

        confirmOverlay.CancelActive();
    }

    private void CloseMenus()
    {
        switchMenu.Close();
        view?.CloseMenus();
    }

    private void ToggleCollapsed(bool value)
    {
        if (SetCollapsed(value))
        {
            owner.OnCollapseChanged();
        }
    }

    private float OwnZoom() => PhoneSizeCatalog.ZoomFor(PhoneBounds.ClampWidth(configuration.PhoneWidth));

    private void ResolvePlacement(float zoom)
    {
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

    private void DrawSurface(PhoneTheme theme, float scale, bool lively, float stripHeight)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = Rounding * scale;
        Elevation.Floating(drawList, frame.Min, frame.Max, rounding, scale, lively ? 1f : 0.7f);
        var surface = ImGui.GetColorU32(theme.AppBackground);
        Squircle.FillVerticalGradient(drawList, frame.Min, frame.Max, rounding, surface, surface);
        var titleBottom = MathF.Min(frame.Min.Y + stripHeight, frame.Max.Y);
        var strip = ImGui.GetColorU32(theme.GroupedCard);
        drawList.PushClipRect(frame.Min, new Vector2(frame.Max.X, titleBottom), true);
        Squircle.FillVerticalGradient(drawList, frame.Min, frame.Max, rounding, strip, strip);
        drawList.PopClipRect();
        if (titleBottom < frame.Max.Y)
        {
            drawList.AddLine(new Vector2(frame.Min.X, titleBottom), new Vector2(frame.Max.X, titleBottom),
                ImGui.GetColorU32(theme.Separator), Metrics.Stroke.Hairline);
        }

        Material.EdgeSquircle(drawList, frame.Min, frame.Max, rounding, scale, lively ? 1f : 0.6f);
    }

    private void DrawTitleBar(Rect bar, PhoneTheme theme, float scale, float delta)
    {
        var drawList = ImGui.GetWindowDrawList();
        var centerY = bar.Center.Y;
        var radius = ButtonRadius * scale;
        var closeCenter = new Vector2(bar.Max.X - EdgeInset * scale - radius * 0.5f, centerY);
        var phoneCenter = new Vector2(closeCenter.X - ButtonPitch * scale, centerY);
        var collapseCenter = new Vector2(phoneCenter.X - ButtonPitch * scale, centerY);
        if (HoverButton.Circle(drawList, closeButtonId, closeCenter, radius, FontAwesomeIcon.Times,
                AppSkin.Transparent, theme.TextMuted, delta, 1f, true, Loc.T(L.Common.Close)))
        {
            owner.Close(conversationId);
            return;
        }

        if (HoverButton.Circle(drawList, phoneButtonId, phoneCenter, radius, FontAwesomeIcon.MobileAlt,
                AppSkin.Transparent, theme.TextMuted, delta, 1f, true, Loc.T(L.Message.PopoutOpenInPhone)))
        {
            OpenInPhone();
        }

        if (HoverButton.Circle(drawList, collapseButtonId, collapseCenter, radius,
                collapsed ? FontAwesomeIcon.ChevronDown : FontAwesomeIcon.ChevronUp, AppSkin.Transparent,
                theme.TextMuted, delta, 1f, true,
                Loc.T(collapsed ? L.Message.PopoutExpand : L.Message.PopoutCollapse)))
        {
            ToggleCollapsed(!collapsed);
        }

        var avatarRadius = AvatarRadius * scale;
        var avatarCenter = new Vector2(bar.Min.X + EdgeInset * scale + avatarRadius, centerY);
        var conversation = store?.Conversation;
        if (conversation is not null && Holds(conversation.Id))
        {
            ConversationAvatar.Draw(drawList, conversation, avatarCenter, avatarRadius, theme, ui,
                services.Images, services.Lodestone);
        }
        else
        {
            drawList.AddCircleFilled(avatarCenter, avatarRadius, ImGui.GetColorU32(theme.SurfaceMuted), 24);
        }

        var textLeft = avatarCenter.X + avatarRadius + Metrics.Space.Sm * scale;
        var textLimit = collapseCenter.X - radius - Metrics.Space.Sm * scale;
        var caretWidth = 10f * scale;
        var headerTitle = HeaderTitle();
        var titleSize = Typography.Measure(headerTitle, TitleStyle);
        var titleWidth = MathF.Min(titleSize.X, MathF.Max(1f, textLimit - textLeft - caretWidth));
        var subtitle = Subtitle(conversation);
        var titleHeight = Typography.LineHeight(TitleStyle);
        var titleTop = subtitle.Length == 0
            ? centerY - titleSize.Y * 0.5f
            : centerY - (titleHeight + Typography.LineHeight(SubtitleStyle)) * 0.5f;
        var hitMin = new Vector2(textLeft - 4f * scale, bar.Min.Y + 4f * scale);
        var hitMax = new Vector2(textLeft + titleWidth + caretWidth + 4f * scale, bar.Max.Y - 4f * scale);
        var titleHovered = UiInteract.Hover(hitMin, hitMax);
        if (titleHovered)
        {
            Squircle.Fill(drawList, hitMin, hitMax, Metrics.Radius.Sm * scale,
                ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.06f)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Marquee.DrawLeft(drawList, new MarqueeId(switchMenuId, ".title"), headerTitle, textLeft, titleTop, titleWidth,
            TitleStyle, theme.TextStrong, titleHovered);
        AppSkin.Icon(drawList, new Vector2(textLeft + titleWidth + CaretGap * scale, titleTop + titleHeight * 0.5f),
            IconGlyph.Of(FontAwesomeIcon.ChevronDown), theme.TextMuted, 0.55f);
        if (subtitle.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(textLeft, titleTop + titleHeight),
                Typography.FitText(subtitle, MathF.Max(1f, textLimit - textLeft), SubtitleStyle), theme.TextMuted,
                SubtitleStyle);
        }

        titleAnchor = new Rect(hitMin, hitMax);
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

    private string HeaderTitle()
    {
        var conversation = store?.Conversation;
        if (conversation is not null && Holds(conversation.Id))
        {
            return DirectMessagesStore.DisplayTitle(conversation);
        }

        return title.Length > 0 ? title : Loc.T(L.Apps.Message);
    }

    private string Subtitle(ConversationDto? conversation)
    {
        if (conversation is null || !Holds(conversation.Id) || conversation.IsGroup)
        {
            return string.Empty;
        }

        return conversation.Presence == 1 ? Loc.T(L.Message.PresenceOnline) : string.Empty;
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
        switchIds.Clear();
        switchTitles.Clear();
        var conversations = owner.Inbox.Conversations;
        for (var index = 0; index < conversations.Length && switchItems.Count < SwitchMenuLimit; index++)
        {
            var item = conversations[index];
            var label = DirectMessagesStore.DisplayTitle(item);
            switchItems.Add(new DropdownMenu.Item(label,
                IconGlyph.Of(item.IsGroup ? FontAwesomeIcon.Users : FontAwesomeIcon.User), false, Holds(item.Id)));
            switchIds.Add(item.Id);
            switchTitles.Add(label);
        }

        if (switchItems.Count == 0)
        {
            return;
        }

        switchMenu.Header = Loc.T(L.Message.PopoutSwitch);
        switchMenu.Toggle(switchMenuId, anchor);
    }

    private void DrawSwitchMenu(PhoneTheme theme)
    {
        if (!switchMenu.IsOpenFor(switchMenuId))
        {
            return;
        }

        var picked = switchMenu.Draw(PhoneBounds.Viewport(), theme, CollectionsMarshal.AsSpan(switchItems));
        if (picked < 0)
        {
            return;
        }

        owner.Switch(this, switchIds[picked], switchTitles[picked]);
    }

    private sealed class PopoutThreadView : MessageThreadViewBase
    {
        private readonly MessagePopoutWindow window;
        private readonly Action back;

        public PopoutThreadView(MessagePopoutWindow window, DirectMessagesStore store)
            : base(store, window.ui, window.services.Images, window.services.Lodestone, window.services.Http,
                window.services.Library, window.services.Configuration, window.services.Confirm,
                window.services.Report, window.services.Translation, window.services.WallpaperImages,
                window.services.EncryptionHelp)
        {
            this.window = window;
            back = window.BackToThread;
        }

        public void CloseMenus()
        {
            menuController.Close();
            searchController.Close();
        }

        protected override PhoneTheme Theme => window.CurrentTheme;

        protected override IPhoneApp Owner => window.OwnerApp;

        protected override INavigator Navigation => PopoutNavigator.Instance;

        protected override Action BackAction => back;

        protected override MessageTheme ChatTheme => MessageThemes.Resolve(configuration.MessageChatTheme);

        protected override void DrawHeader(Rect area, string threadId)
        {
        }

        protected override ChatMenuModel BuildMenuModel()
        {
            return new ChatMenuModel
            {
                Ui = ui,
                ShowReactions = true,
                CanReply = true,
                CanCopy = true,
                CanEdit = true,
                CanDelete = true,
                CanTranslate = true,
                MyReactionTo = messages.MyReactionTo,
                OnReply = BeginReply,
                OnCopy = CopyMessage,
                OnEdit = BeginEdit,
                OnDelete = AskDeleteMessage,
                OnTranslate = TranslateMessage,
                OnReact = messages.SetReaction,
            };
        }

        protected override void OpenImageView(string messageId) =>
            Plugin.PhotoWindow.Open(() => ResolveThreadImage(messageId), Owner);

        protected override void OpenReactions(string messageId) => window.ShowReactions(messageId);

        protected override void PushImagePickerScreen(string threadId) => window.ShowPicker();

        protected override void PopScreen() => window.BackToThread();

        protected override void OpenEncryptionInfo(string threadId) => window.ShowEncryption();

    }
}
