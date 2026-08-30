using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Emoji;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Translation;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal readonly record struct StoryViewers(StoryViewerDto[] Items, int Total, bool Loading,
    bool HasMore = false, bool LoadingMore = false);

internal readonly record struct StoryReplyPrompt(LocString Hint, Action<StoryDto, string> Send);

internal sealed class StoryViewerOverlay
{
    private const float SecondsPerStory = 5f;
    private const float SheetSmoothTime = 0.16f;
    private const float SheetHeightFraction = 0.58f;
    private const float SheetRowHeight = 46f;
    private const float RevealSmoothTime = 0.15f;
    private const float DismissDragDistance = 140f;
    private const float HoldPauseSeconds = 0.18f;
    private const float TapZoneFraction = 0.32f;
    private const float FooterInset = 16f;
    private const float FooterGap = 10f;
    private const float SeenPillHeight = 30f;
    private const float ScrimFadeHeight = 44f;
    private const float SeenHoverSmoothTime = 0.12f;
    private const float ReplyBarHeight = 42f;
    private const float ReplyBarInset = 12f;
    private const float ReplyRevealSmoothTime = 0.15f;
    private const float QuickEmojiSide = 30f;
    private const float QuickEmojiRowGap = 12f;
    private const int ReplyMaxLength = 500;

    private static readonly string[] QuickEmojiFiles =
        { "2764", "1f602", "1f62e", "1f62d", "1f525", "1f44f", "1f60d", "1f44d" };

    private static readonly string[] QuickEmojiShortcodes =
        { ":heart:", ":joy:", ":open_mouth:", ":sob:", ":fire:", ":clap:", ":heart_eyes:", ":thumbsup:" };

    private readonly RemoteImageCache images;
    private readonly LodestoneService lodestone;
    private readonly TranslationService translation;
    private readonly ConfirmService confirm;
    private readonly Action<string>? openProfile;
    private Spring reveal;
    private StoryDto[] stories = Array.Empty<StoryDto>();
    private string authorLabel = string.Empty;
    private string? authorAvatarUrl;
    private string authorFrameId = string.Empty;
    private bool canDelete;
    private int index;
    private float elapsed;
    private bool open;
    private bool holding;
    private double pressStartedAt;
    private Vector2 pressOrigin;
    private float dragOffset;
    private bool pressInChrome;
    private Rect seenPillBounds;
    private Action<StoryDto>? onSeen;
    private Action<StoryDto>? onDelete;
    private Action? onExhausted;
    private Func<StoryDto, StoryViewers>? viewersSource;
    private Action? viewersLoadMore;
    private Func<bool>? onNextGroup;
    private Func<bool>? onPreviousGroup;
    private bool awaitingGroup;
    private StoryReplyPrompt? replyPrompt;
    private string replyDraft = string.Empty;
    private bool replyFocused;
    private Spring replyReveal;
    private Spring sheetReveal;
    private Spring seenHover;
    private bool sheetOpen;

    public StoryViewerOverlay(RemoteImageCache images, LodestoneService lodestone, TranslationService translation,
        ConfirmService confirm, Action<string>? openProfile = null)
    {
        this.translation = translation;
        this.confirm = confirm;
        this.images = images;
        this.lodestone = lodestone;
        this.openProfile = openProfile;
    }

    public bool Active => open || reveal.Value > 0.01f;
    public StoryDto? Current => index >= 0 && index < stories.Length ? stories[index] : null;

    public void Open(StoryDto[] items, string label, string? avatarUrl, Action<StoryDto> seen, bool mine = false,
        Action<StoryDto>? delete = null, Func<StoryDto, StoryViewers>? viewers = null, Action? exhausted = null,
        Func<bool>? nextGroup = null, Func<bool>? previousGroup = null, bool startAtEnd = false,
        StoryReplyPrompt? reply = null, Action? loadMoreViewers = null)
    {
        stories = items;
        authorLabel = label;
        authorAvatarUrl = avatarUrl;
        authorFrameId = items.Length > 0 ? items[0].AuthorFrameId : string.Empty;
        canDelete = mine;
        onSeen = seen;
        onDelete = delete;
        viewersSource = viewers;
        viewersLoadMore = loadMoreViewers;
        onExhausted = exhausted;
        onNextGroup = nextGroup;
        onPreviousGroup = previousGroup;
        awaitingGroup = false;
        replyPrompt = reply;
        replyDraft = string.Empty;
        replyFocused = false;
        replyReveal = new Spring(0f);
        sheetOpen = false;
        sheetReveal = new Spring(0f);
        seenHover = new Spring(0f);
        index = startAtEnd && items.Length > 0 ? items.Length - 1 : FirstUnseen(items);
        elapsed = 0f;
        dragOffset = 0f;
        seenPillBounds = default;
        pressInChrome = false;
        holding = false;
        open = true;
        ReportSeen();
    }

    public void Replace(StoryDto[] items)
    {
        if (!open)
        {
            return;
        }

        stories = items;
        if (items.Length == 0)
        {
            Close();
            return;
        }

        index = Math.Clamp(index, 0, items.Length - 1);
        ReportSeen();
    }

    public void Close()
    {
        open = false;
        holding = false;
        sheetOpen = false;
        awaitingGroup = false;
        replyFocused = false;
        replyDraft = string.Empty;
    }

    public void CancelGroupWait()
    {
        if (!awaitingGroup)
        {
            return;
        }

        awaitingGroup = false;
        Close();
    }

    public void Reset()
    {
        Close();
        reveal = new Spring(0f);
        sheetReveal = new Spring(0f);
        seenHover = new Spring(0f);
        replyReveal = new Spring(0f);
        stories = Array.Empty<StoryDto>();
        onSeen = null;
        onDelete = null;
        onExhausted = null;
        onNextGroup = null;
        onPreviousGroup = null;
        replyPrompt = null;
        viewersSource = null;
        viewersLoadMore = null;
    }

    public void Draw(Rect area, PhoneTheme theme, bool suspended = false)
    {
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        reveal.Step(open ? 1f : 0f, RevealSmoothTime, delta);
        var eased = Math.Clamp(reveal.Value, 0f, 1f);
        if (eased <= 0.01f)
        {
            if (!open)
            {
                stories = Array.Empty<StoryDto>();
                onSeen = null;
                onDelete = null;
                onExhausted = null;
                onNextGroup = null;
                onPreviousGroup = null;
                replyPrompt = null;
            }

            return;
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var dim = 0.97f * eased * (1f - Math.Clamp(dragOffset / (DismissDragDistance * 2f), 0f, 0.45f));
        drawList.AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, dim)));

        var story = Current;
        if (story is null)
        {
            Typography.DrawCentered(area.Center, Loc.T(L.Common.Loading), new Vector4(1f, 1f, 1f, 0.7f));
            return;
        }

        sheetReveal.Step(sheetOpen ? 1f : 0f, SheetSmoothTime, delta);
        replyReveal.Step(replyFocused ? 1f : 0f, ReplyRevealSmoothTime, delta);
        var contentTop = area.Min.Y + theme.TopZoneHeight * scale;
        var baseStage = new Rect(new Vector2(area.Min.X, contentTop + 44f * scale),
            new Vector2(area.Max.X, area.Max.Y - 16f * scale));
        if (open && !suspended && !sheetOpen)
        {
            HandleInput(baseStage, delta, images.Get(story.MediaUrl) is not null, ReplyZone(baseStage, scale));
        }

        var shift = new Vector2(0f, dragOffset * scale);
        var stage = new Rect(baseStage.Min + shift, baseStage.Max + shift);
        DrawImage(drawList, stage, story, scale);
        var footerInset = replyPrompt is null ? 0f : (ReplyBarHeight + FooterGap) * scale;
        DrawFooter(drawList, stage, story, scale, delta, footerInset);
        if (replyPrompt is not null && !suspended)
        {
            DrawReplyBar(drawList, stage, story, scale);
        }

        DrawProgress(drawList, new Rect(new Vector2(area.Min.X + 12f * scale, contentTop + 8f * scale) + shift,
            new Vector2(area.Max.X - 12f * scale, contentTop + 11f * scale) + shift), scale);
        DrawHeader(new Rect(new Vector2(area.Min.X + 12f * scale, contentTop + 18f * scale) + shift,
            new Vector2(area.Max.X - 12f * scale, contentTop + 42f * scale) + shift), theme, story, scale);
        DrawViewersSheet(area, theme, story, scale);
    }

    private Rect ReplyZone(Rect stage, float scale)
    {
        if (replyPrompt is null)
        {
            return new Rect(stage.Max, stage.Max);
        }

        var barTop = stage.Max.Y - (ReplyBarInset + ReplyBarHeight) * scale;
        var rowRise = Math.Clamp(replyReveal.Value, 0f, 1f) * (QuickEmojiSide + QuickEmojiRowGap) * scale;
        return new Rect(new Vector2(stage.Min.X, barTop - rowRise), stage.Max);
    }

    private void DrawFooter(ImDrawListPtr drawList, Rect stage, StoryDto story, float scale, float delta,
        float bottomInset)
    {
        var showSeen = ShowSeenPill;
        var hasCaption = story.Caption.Length > 0;
        if (!showSeen && !hasCaption)
        {
            return;
        }

        var inset = FooterInset * scale;
        var captionWidth = stage.Width - inset * 2f;
        var storyKey = new TranslationKey(TranslationSurface.Story, story.Id);
        var captionText = translation.View(storyKey, story.Caption, story.Lang).Text;
        var captionLayout = hasCaption ? LinkText.LayoutFor(captionText, captionWidth) : null;
        var captionHeight = hasCaption
            ? captionLayout?.Size.Y ?? Typography.MeasureWrappedBlock(captionText, TextStyles.Body, captionWidth).Y
            : 0f;
        var linkHeight = hasCaption ? TranslateLink.Height(translation, storyKey, story.Lang, scale) : 0f;
        var seenHeight = showSeen ? SeenPillHeight * scale : 0f;
        var gap = hasCaption && showSeen ? FooterGap * scale : 0f;
        var bottom = stage.Max.Y - inset - bottomInset;
        var top = MathF.Max(stage.Min.Y + inset, bottom - captionHeight - linkHeight - gap - seenHeight);
        var scrimTop = MathF.Max(stage.Min.Y, top - ScrimFadeHeight * scale);
        Squircle.FillVerticalGradient(drawList, new Vector2(stage.Min.X, scrimTop), stage.Max,
            Metrics.Radius.Md * scale, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0f)),
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.78f)));
        if (hasCaption)
        {
            var captionInk = new Vector4(1f, 1f, 1f, 0.96f);
            if (captionLayout is not null)
            {
                LinkText.Draw(drawList, captionLayout,
                    new Vector2(stage.Center.X - captionLayout.Size.X * 0.5f, top), 1f, captionInk, captionInk, 1f,
                    true);
            }
            else
            {
                Typography.DrawWrappedCentered(new Vector2(stage.Center.X, top), captionText, captionInk,
                    TextStyles.Body, captionWidth);
            }

            if (linkHeight > 0f)
            {
                TranslateLink.Draw(translation, confirm, storyKey, story.Lang, story.Caption,
                    new Vector2(stage.Min.X + inset, top + captionHeight), captionWidth, new Vector4(1f, 1f, 1f, 0.7f),
                    new Vector4(1f, 1f, 1f, 1f), scale);
            }
        }

        if (showSeen)
        {
            DrawSeenPill(drawList, new Vector2(stage.Min.X + inset, bottom - seenHeight), story, seenHeight, scale,
                delta);
        }
    }

    private void DrawReplyBar(ImDrawListPtr drawList, Rect stage, StoryDto story, float scale)
    {
        if (replyPrompt is not { } prompt)
        {
            return;
        }

        var inset = ReplyBarInset * scale;
        var barHeight = ReplyBarHeight * scale;
        var barMin = new Vector2(stage.Min.X + inset, stage.Max.Y - inset - barHeight);
        var barMax = new Vector2(stage.Max.X - inset, stage.Max.Y - inset);
        var radius = barHeight * 0.5f;
        Squircle.Fill(drawList, barMin, barMax, radius, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.45f)));
        Squircle.Stroke(drawList, barMin, barMax, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.35f)),
            1.2f * scale);
        var centerY = (barMin.Y + barMax.Y) * 0.5f;
        var hint = Loc.T(prompt.Hint, authorLabel);
        var hasDraft = replyDraft.Length > 0;
        var sendCenter = new Vector2(barMax.X - 20f * scale, centerY);
        var inputLeft = barMin.X + 16f * scale;
        var inputRight = hasDraft ? sendCenter.X - 16f * scale : barMax.X - 16f * scale;
        ImGui.SetCursorScreenPos(new Vector2(inputLeft, centerY - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(inputRight - inputLeft);
        Plugin.Fonts.NoticeText(hint);
        Plugin.Fonts.NoticeText(replyDraft);
        bool submitted;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 0.96f)))
        using (ImRaii.PushColor(ImGuiCol.TextDisabled, new Vector4(1f, 1f, 1f, 0.55f)))
        {
            submitted = ImGui.InputTextWithHint("##storyReply", hint, ref replyDraft, ReplyMaxLength,
                ImGuiInputTextFlags.EnterReturnsTrue);
        }

        replyFocused = ImGui.IsItemActive();
        if (hasDraft)
        {
            AppSkin.Icon(sendCenter, IconGlyph.Of(FontAwesomeIcon.PaperPlane), new Vector4(1f, 1f, 1f, 0.95f), 1f);
            var hit = new Vector2(14f * scale, 14f * scale);
            if (UiInteract.Hover(sendCenter - hit, sendCenter + hit))
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    submitted = true;
                }
            }
        }

        if (submitted)
        {
            SendReply(prompt, story, replyDraft);
        }

        DrawQuickEmojiRow(drawList, prompt, story, barMin, barMax, scale);
    }

    private void DrawQuickEmojiRow(ImDrawListPtr drawList, in StoryReplyPrompt prompt, StoryDto story,
        Vector2 barMin, Vector2 barMax, float scale)
    {
        var revealValue = Math.Clamp(replyReveal.Value, 0f, 1f);
        if (revealValue <= 0.01f)
        {
            return;
        }

        var eased = Easing.EaseOutQuint(revealValue);
        var side = QuickEmojiSide * scale;
        var rise = (1f - eased) * 8f * scale;
        var rowBottom = barMin.Y - QuickEmojiRowGap * scale + rise;
        var rowTop = rowBottom - side;
        var slot = (barMax.X - barMin.X) / QuickEmojiFiles.Length;
        var backdropPad = 8f * scale;
        Squircle.Fill(drawList, new Vector2(barMin.X, rowTop - backdropPad),
            new Vector2(barMax.X, rowBottom + backdropPad), (side + backdropPad * 2f) * 0.5f,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.35f * eased)));
        var tint = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, eased));
        var rowCenterY = (rowTop + rowBottom) * 0.5f;
        for (var emojiIndex = 0; emojiIndex < QuickEmojiFiles.Length; emojiIndex++)
        {
            var center = new Vector2(barMin.X + slot * (emojiIndex + 0.5f), rowCenterY);
            var half = side * 0.5f;
            var hovered = eased > 0.5f
                && UiInteract.Hover(center - new Vector2(half, half), center + new Vector2(half, half));
            var drawHalf = hovered ? half * 1.15f : half;
            EmojiImages.TryDraw(drawList, QuickEmojiFiles[emojiIndex], center - new Vector2(drawHalf, drawHalf),
                center + new Vector2(drawHalf, drawHalf), tint);
            if (!hovered)
            {
                continue;
            }

            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                SendReply(prompt, story, QuickEmojiShortcodes[emojiIndex]);
            }
        }
    }

    private void SendReply(in StoryReplyPrompt prompt, StoryDto story, string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        replyDraft = string.Empty;
        replyFocused = false;
        prompt.Send(story, trimmed);
    }

    private bool ShowSeenPill => canDelete && viewersSource is not null;

    private void DrawSeenPill(ImDrawListPtr drawList, Vector2 origin, StoryDto story, float height, float scale,
        float delta)
    {
        var label = Loc.Plural(L.Story.SeenBy, story.ViewCount);
        var size = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var padding = 12f * scale;
        var iconWidth = 11f * scale;
        var iconGap = 7f * scale;
        var max = new Vector2(origin.X + padding * 2f + iconWidth + iconGap + size.X, origin.Y + height);
        seenPillBounds = new Rect(origin, max);
        var radius = height * 0.5f;
        var centerY = origin.Y + height * 0.5f;
        var hovered = !sheetOpen && UiInteract.Hover(origin, max);
        seenHover.Step(hovered ? 1f : 0f, SeenHoverSmoothTime, delta);
        var hover = Math.Clamp(seenHover.Value, 0f, 1f);
        var press = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left) ? 0.1f : 0f;
        Squircle.Fill(drawList, origin, max, radius,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.45f + 0.25f * hover + press)));
        if (hover > 0.001f)
        {
            Squircle.Stroke(drawList, origin, max, radius,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.3f * hover)), 1f * scale);
        }

        var ink = new Vector4(1f, 1f, 1f, 0.9f + 0.1f * hover);
        AppSkin.Icon(new Vector2(origin.X + padding + iconWidth * 0.5f, centerY), IconGlyph.Of(FontAwesomeIcon.Eye),
            ink, 0.8f);
        Typography.Draw(new Vector2(origin.X + padding + iconWidth + iconGap, centerY - size.Y * 0.5f), label, ink,
            TextStyles.FootnoteEmphasized);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(origin, max, hovered))
        {
            sheetOpen = true;
        }
    }

    private void DrawViewersSheet(Rect area, PhoneTheme theme, StoryDto story, float scale)
    {
        var reveal = Math.Clamp(sheetReveal.Value, 0f, 1f);
        if (reveal <= 0.01f || viewersSource is null)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.5f * reveal)));
        var height = area.Height * SheetHeightFraction;
        var top = area.Max.Y - height * Easing.EaseOutQuint(reveal);
        var panel = new Rect(new Vector2(area.Min.X, top), area.Max);
        if (UiInteract.ClickedOutside(panel.Min, panel.Max, false))
        {
            sheetOpen = false;
        }

        var rounding = Metrics.Radius.Lg * scale;
        Squircle.Fill(drawList, panel.Min, new Vector2(panel.Max.X, panel.Max.Y + rounding), rounding,
            ImGui.GetColorU32(theme.Surface));
        drawList.AddRectFilled(new Vector2(panel.Center.X - 18f * scale, panel.Min.Y + 8f * scale),
            new Vector2(panel.Center.X + 18f * scale, panel.Min.Y + 11f * scale),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.25f)), 2f * scale);

        var viewers = viewersSource(story);
        var headerY = panel.Min.Y + 26f * scale;
        Typography.DrawCentered(new Vector2(panel.Center.X, headerY),
            Loc.Plural(L.Story.SeenBy, story.ViewCount), theme.TextStrong, TextStyles.Headline);
        var listRect = new Rect(new Vector2(panel.Min.X, headerY + 18f * scale), panel.Max);
        if (viewers.Items.Length == 0)
        {
            Typography.DrawCentered(new Vector2(panel.Center.X, listRect.Min.Y + 40f * scale),
                Loc.T(viewers.Loading ? L.Common.Loading : L.Story.NoViewers), theme.TextMuted,
                TextStyles.Subheadline);
            return;
        }

        using (AppSurface.Begin(listRect))
        {
            for (var index = 0; index < viewers.Items.Length; index++)
            {
                DrawViewerRow(viewers.Items[index], theme, scale);
            }

            if (viewers.LoadingMore)
            {
                InfiniteScroll.DrawLoadingRow(listRect.Center.X, theme.TextMuted);
            }
            else if (viewers.HasMore && viewersLoadMore is not null && InfiniteScroll.ReachedBottom())
            {
                viewersLoadMore();
            }

            if (viewers.HasMore && viewers.Items.Length < viewers.Total)
            {
                Typography.DrawCentered(
                    new Vector2(listRect.Center.X, ImGui.GetCursorScreenPos().Y + 14f * scale),
                    Loc.T(L.Story.ViewersTrimmed, viewers.Items.Length, viewers.Total), theme.TextMuted,
                    TextStyles.Caption1);
                ImGui.Dummy(new Vector2(0f, 30f * scale));
            }
        }
    }

    private void DrawViewerRow(StoryViewerDto viewer, PhoneTheme theme, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = SheetRowHeight * scale;
        var drawList = ImGui.GetWindowDrawList();
        var radius = 16f * scale;
        var center = new Vector2(origin.X + radius + 4f * scale, origin.Y + height * 0.5f);
        var name = SocialIdentity.Name(viewer.DisplayName, viewer.Handle);
        AvatarView.DrawRemote(drawList, center, radius, theme, name, string.Empty, viewer.AvatarUrl, images, lodestone,
            0.8f, 28, 1f, Frames.Of(viewer.FrameId));
        var left = center.X + radius + 10f * scale;
        var stamp = TimeText.Short(viewer.ViewedAtUnix);
        var stampSize = Typography.Measure(stamp, TextStyles.Caption1);
        var nameMaxWidth = MathF.Max(1f, origin.X + width - stampSize.X - 16f * scale - left);
        var rowHovering = UiInteract.Hover(origin, new Vector2(origin.X + width, origin.Y + height));
        var nameSize = Typography.Measure(name, TextStyles.Subheadline);
        UserName.Draw("storyviewer.name." + viewer.Handle, name, viewer.Badges, viewer.BadgeIds, left,
            center.Y - nameSize.Y * 0.5f, nameMaxWidth, TextStyles.Subheadline, theme.TextStrong, rowHovering, theme);
        Typography.Draw(new Vector2(origin.X + width - stampSize.X - 6f * scale, center.Y - stampSize.Y * 0.5f), stamp,
            theme.TextMuted, TextStyles.Caption1);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void HandleInput(Rect area, float delta, bool imageReady, Rect replyZone)
    {
        var hovering = UiInteract.Hover(area.Min, area.Max);
        if (hovering && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            pressStartedAt = ImGui.GetTime();
            pressOrigin = ImGui.GetIO().MousePos;
            pressInChrome = (replyPrompt is not null && UiInteract.Hover(replyZone.Min, replyZone.Max))
                || (ShowSeenPill && UiInteract.Hover(seenPillBounds.Min, seenPillBounds.Max));
        }

        var down = ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var heldFor = ImGui.GetTime() - pressStartedAt;
        if (down && hovering && !pressInChrome)
        {
            var travel = ImGui.GetIO().MousePos.Y - pressOrigin.Y;
            dragOffset = MathF.Max(0f, travel / UiScale.Current);
            holding = heldFor >= HoldPauseSeconds && dragOffset < 8f;
        }
        else
        {
            holding = false;
        }

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            var wasDrag = !pressInChrome && dragOffset >= DismissDragDistance;
            var wasTap = !pressInChrome && heldFor < HoldPauseSeconds && dragOffset < 8f;
            dragOffset = 0f;
            pressInChrome = false;
            if (wasDrag)
            {
                Close();
                return;
            }

            if (wasTap && hovering)
            {
                var x = ImGui.GetIO().MousePos.X;
                if (x <= area.Min.X + area.Width * TapZoneFraction)
                {
                    Step(-1);
                }
                else if (x >= area.Max.X - area.Width * TapZoneFraction)
                {
                    Step(1);
                }
            }
        }

        if (ImGui.IsKeyPressed(ImGuiKey.Escape) && !replyFocused)
        {
            Close();
            return;
        }

        if (holding || !imageReady || awaitingGroup || replyFocused)
        {
            return;
        }

        elapsed += delta;
        if (elapsed >= SecondsPerStory)
        {
            Step(1);
        }
    }

    private void Step(int direction)
    {
        if (awaitingGroup)
        {
            return;
        }

        var next = index + direction;
        if (next < 0)
        {
            if (onPreviousGroup?.Invoke() == true)
            {
                awaitingGroup = true;
            }

            elapsed = 0f;
            return;
        }

        if (next >= stories.Length)
        {
            if (onNextGroup?.Invoke() == true)
            {
                awaitingGroup = true;
                elapsed = 0f;
                return;
            }

            Close();
            onExhausted?.Invoke();
            return;
        }

        index = next;
        elapsed = 0f;
        ReportSeen();
    }

    private void ReportSeen()
    {
        if (Current is { } story)
        {
            onSeen?.Invoke(story);
        }
    }

    private void DrawImage(ImDrawListPtr drawList, Rect stage, StoryDto story, float scale)
    {
        var rounding = Metrics.Radius.Md * scale;
        var texture = images.Get(story.MediaUrl);
        if (texture is null)
        {
            Squircle.Fill(drawList, stage.Min, stage.Max, rounding, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.06f)));
            Typography.DrawCentered(stage.Center,
                Loc.T(images.Failed(story.MediaUrl) ? L.Common.ImageFailed : L.Common.Loading),
                new Vector4(1f, 1f, 1f, 0.7f), TextStyles.Subheadline);
            return;
        }

        var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, stage.Width, stage.Height);
        drawList.AddImageRounded(texture.Handle, stage.Min, stage.Max, uv0, uv1, 0xFFFFFFFFu, rounding,
            ImDrawFlags.RoundCornersAll);
        if (!ContentModeration.IsInReview(story.ScanStatus))
        {
            return;
        }

        Squircle.Fill(drawList, stage.Min, stage.Max, rounding, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.55f)));
        var center = stage.Center;
        AppSkin.Icon(drawList, new Vector2(center.X, center.Y - 26f * scale), IconGlyph.Of(FontAwesomeIcon.Hourglass),
            new Vector4(1f, 1f, 1f, 0.92f), 1.6f);
        Typography.DrawCentered(drawList, center, Loc.T(L.Moderation.InReview), new Vector4(1f, 1f, 1f, 0.95f),
            TextStyles.Headline);
        Typography.DrawCentered(drawList, new Vector2(center.X, center.Y + 22f * scale),
            Loc.T(L.Moderation.InReviewHint), new Vector4(1f, 1f, 1f, 0.75f), TextStyles.Footnote);
    }

    private void DrawProgress(ImDrawListPtr drawList, Rect bar, float scale)
    {
        if (stories.Length == 0)
        {
            return;
        }

        var gap = 3f * scale;
        var slot = (bar.Width - gap * (stories.Length - 1)) / stories.Length;
        var rounding = bar.Height * 0.5f;
        for (var slotIndex = 0; slotIndex < stories.Length; slotIndex++)
        {
            var left = bar.Min.X + (slot + gap) * slotIndex;
            var right = left + slot;
            drawList.AddRectFilled(new Vector2(left, bar.Min.Y), new Vector2(right, bar.Max.Y),
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.28f)), rounding);
            var fill = slotIndex < index ? 1f
                : slotIndex > index ? 0f
                : Math.Clamp(elapsed / SecondsPerStory, 0f, 1f);
            if (fill <= 0f)
            {
                continue;
            }

            drawList.AddRectFilled(new Vector2(left, bar.Min.Y),
                new Vector2(left + slot * fill, bar.Max.Y), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.95f)),
                rounding);
        }
    }

    private void DrawHeader(Rect row, PhoneTheme theme, StoryDto story, float scale)
    {
        var radius = 12f * scale;
        var center = new Vector2(row.Min.X + radius, row.Center.Y);
        AvatarView.DrawRemote(ImGui.GetWindowDrawList(), center, radius, theme, authorLabel, string.Empty,
            authorAvatarUrl, images, lodestone, 0.8f, 24, 1f, Frames.Of(authorFrameId));
        var left = center.X + radius + 9f * scale;
        var closeReserve = 34f * scale;
        var stamp = TimeText.Short(story.CreatedAtUnix);
        var stampWidth = Typography.Measure(stamp, TextStyles.Footnote).X;
        var nameMaxWidth = MathF.Max(1f, row.Max.X - closeReserve - stampWidth - 8f * scale - left);
        var nameSize = Typography.Measure(authorLabel, TextStyles.SubheadlineEmphasized);
        var authorMin = new Vector2(row.Min.X, row.Min.Y);
        var authorMax = new Vector2(left + MathF.Min(nameSize.X, nameMaxWidth), row.Max.Y);
        var headerHovering = UiInteract.Hover(authorMin, authorMax);
        var nameWidth = UserName.Draw("storyviewer.header.author." + authorLabel, authorLabel, story.AuthorBadges, story.AuthorBadgeIds,
            left, row.Center.Y - nameSize.Y * 0.5f, nameMaxWidth, TextStyles.SubheadlineEmphasized,
            new Vector4(1f, 1f, 1f, 0.98f), headerHovering, false);
        Typography.Draw(new Vector2(left + nameWidth + 8f * scale, row.Center.Y - nameSize.Y * 0.5f + 1f * scale),
            stamp, new Vector4(1f, 1f, 1f, 0.6f), TextStyles.Footnote);
        if (openProfile is not null && UiInteract.HoverClick(authorMin, authorMax))
        {
            openProfile(story.AuthorId);
            return;
        }

        var hit = new Vector2(14f * scale, 14f * scale);
        var closeCenter = new Vector2(row.Max.X - 10f * scale, row.Center.Y);
        AppSkin.Icon(closeCenter, IconGlyph.Of(FontAwesomeIcon.Times), new Vector4(1f, 1f, 1f, 0.9f), 1.1f);
        if (UiInteract.HoverClick(closeCenter - hit, closeCenter + hit))
        {
            Close();
        }

        if (!canDelete)
        {
            return;
        }

        var deleteCenter = new Vector2(closeCenter.X - 32f * scale, row.Center.Y);
        AppSkin.Icon(deleteCenter, IconGlyph.Of(FontAwesomeIcon.Trash), new Vector4(1f, 1f, 1f, 0.82f), 1f);
        if (UiInteract.HoverClick(deleteCenter - hit, deleteCenter + hit))
        {
            onDelete?.Invoke(story);
        }
    }

    private static int FirstUnseen(StoryDto[] items)
    {
        for (var itemIndex = 0; itemIndex < items.Length; itemIndex++)
        {
            if (!items[itemIndex].Seen)
            {
                return itemIndex;
            }
        }

        return 0;
    }
}
