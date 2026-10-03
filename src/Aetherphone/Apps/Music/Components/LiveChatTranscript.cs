using Aetherphone.Apps.Music.Radio.Live;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music.Components;

internal sealed class LiveChatTranscript
{
    public const float ComposerBaseHeight = 54f;
    public const float ComposerFieldInsetY = 9f;
    public const int ComposerMaxLines = 3;
    public const float ComposerButtonRadius = 17f;

    public static readonly TextStyle BodyStyle = TextStyles.Subheadline;

    private const float AvatarRadius = 13f;
    private const float RowPadY = 6f;
    private const float RowGap = 9f;
    private const float BodyGap = 2f;
    private const float FollowTolerance = 6f;
    private const float JumpScroll = 10_000_000f;
    private const double LongPressSeconds = 0.45;
    private const float LongPressSlop = 6f;
    private const float JumpPillHeight = 30f;
    private const float BadgePadX = 5f;
    private const float OwnRowAlpha = 0.10f;
    private const int CounterWarning = 10;

    private static readonly TextStyle NameStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle BadgeStyle = new(0.66f, FontWeight.Bold);

    private readonly AppSkin ui;
    private readonly RemoteImageCache images;
    private readonly LodestoneService lodestone;
    private readonly Func<RadioChatEntry, string> badgeFor;
    private readonly string scopeId;
    private readonly RadioChatFollow follow = new();
    private readonly Dictionary<long, RadioChatRow> rowsById = new();
    private readonly RadioChatRow?[] rows = new RadioChatRow?[RadioChatRing.Capacity];
    private readonly List<long> staleRows = new();
    private readonly RadioCountLabel newMessagesLabel = new();
    private readonly RadioFittedText jumpPillFit = new();
    private int rowCount;
    private int rowsVersion = -1;
    private int rowStamp;
    private long pressedMessageId = -1;
    private double pressedAt;
    private Vector2 pressedOrigin;

    public LiveChatTranscript(string scopeId, AppSkin ui, RemoteImageCache images, LodestoneService lodestone,
        Func<RadioChatEntry, string> badgeFor)
    {
        this.scopeId = scopeId;
        this.ui = ui;
        this.images = images;
        this.lodestone = lodestone;
        this.badgeFor = badgeFor;
    }

    public int Count => rowCount;

    public void Reset()
    {
        follow.Reset();
        pressedMessageId = -1;
    }

    public void RequestJump()
    {
        follow.RequestJump();
    }

    public void Sync(ILiveChatFeed feed)
    {
        if (rowsVersion == feed.Version)
        {
            return;
        }

        rowsVersion = feed.Version;
        rowStamp++;
        rowCount = Math.Min(feed.Count, rows.Length);
        for (var index = 0; index < rowCount; index++)
        {
            var entry = feed.At(index);
            if (!rowsById.TryGetValue(entry.MessageId, out var row))
            {
                row = new RadioChatRow();
                rowsById[entry.MessageId] = row;
            }

            if (!ReferenceEquals(row.Entry, entry))
            {
                row.Assign(entry, NameInkFor(entry));
            }

            row.Stamp = rowStamp;
            rows[index] = row;
        }

        for (var index = rowCount; index < rows.Length; index++)
        {
            rows[index] = null;
        }

        if (rowsById.Count <= rowCount)
        {
            return;
        }

        staleRows.Clear();
        foreach (var pair in rowsById)
        {
            if (pair.Value.Stamp != rowStamp)
            {
                staleRows.Add(pair.Key);
            }
        }

        for (var index = 0; index < staleRows.Count; index++)
        {
            rowsById.Remove(staleRows[index]);
        }
    }

    public RadioChatEntry? DrawTranscript(Rect list, float scale)
    {
        if (rowCount == 0)
        {
            return null;
        }

        RadioChatEntry? menu = null;
        ImGui.PushID(scopeId);
        using (AppSurface.ReserveBottom(0f))
        {
            using var surface = AppSurface.Begin(list);
            var tailId = rows[rowCount - 1]!.Entry.MessageId;
            var newer = CountNewerThan(follow.SeenTailId);
            if (follow.Update(ImGui.GetScrollY(), ImGui.GetScrollMaxY(), FollowTolerance * scale, list.Height,
                    tailId, newer))
            {
                surface.JumpTo(JumpScroll);
            }

            var width = ScrollLayout.StableContentWidth();
            for (var index = 0; index < rowCount; index++)
            {
                if (DrawRow(rows[index]!, width, scale) is { } picked)
                {
                    menu = picked;
                }
            }
        }

        ImGui.PopID();
        return menu;
    }

    public void DrawJumpPill(Rect list, float scale)
    {
        if (follow.Pinned || rowCount == 0)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var height = JumpPillHeight * scale;
        var bottom = list.Max.Y - Metrics.Space.Sm * scale;
        Vector2 min;
        Vector2 max;
        if (follow.Unseen > 0)
        {
            var label = newMessagesLabel.Plural(L.Music.Live.NewMessages, follow.Unseen);
            var width = MathF.Min(list.Width - Metrics.Space.Xxl * scale,
                Typography.Measure(label, TextStyles.FootnoteEmphasized).X + height + Metrics.Space.Lg * scale);
            min = new Vector2(list.Center.X - width * 0.5f, bottom - height);
            max = new Vector2(list.Center.X + width * 0.5f, bottom);
            Material.ThemedGlass(drawList, min, max, height * 0.5f, scale, ui.Theme);
            AppSkin.Icon(drawList, new Vector2(min.X + height * 0.5f + Metrics.Space.Xxs * scale, min.Y + height * 0.5f),
                IconGlyph.Of(FontAwesomeIcon.ArrowDown), ui.Accent, 0.65f);
            var fitted = jumpPillFit.Fit(label, width - height - Metrics.Space.Sm * scale,
                TextStyles.FootnoteEmphasized);
            var size = Typography.Measure(fitted, TextStyles.FootnoteEmphasized);
            Typography.Draw(drawList, new Vector2(min.X + height, min.Y + (height - size.Y) * 0.5f), fitted, ui.Accent,
                TextStyles.FootnoteEmphasized);
        }
        else
        {
            min = new Vector2(list.Center.X - height * 0.5f, bottom - height);
            max = new Vector2(list.Center.X + height * 0.5f, bottom);
            Material.ThemedGlass(drawList, min, max, height * 0.5f, scale, ui.Theme);
            AppSkin.Icon(drawList, (min + max) * 0.5f, IconGlyph.Of(FontAwesomeIcon.ArrowDown), ui.Accent, 0.65f);
        }

        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(min, max, hovered))
        {
            follow.RequestJump();
        }
    }

    public static bool DrawComposerField(AppSkin ui, Rect bar, float fieldLeft, string imguiId, string hint,
        SoftWrapEditor editor, int maxLength, bool canSend, RadioCountLabel counter, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var inset = Metrics.Space.Md * scale;
        var buttonRadius = ComposerButtonRadius * scale;
        var rowCenterY = bar.Max.Y - ComposerBaseHeight * scale * 0.5f;
        var sendCenter = new Vector2(bar.Max.X - inset - buttonRadius, rowCenterY);
        var field = new Rect(new Vector2(fieldLeft, bar.Min.Y + ComposerFieldInsetY * scale),
            new Vector2(sendCenter.X - buttonRadius - Metrics.Space.Sm * scale, bar.Max.Y - ComposerFieldInsetY * scale));
        var submitted = SubmitField.Multiline(field, imguiId, hint, editor, ui.Theme, maxLength, ComposerMaxLines,
            FontAwesomeIcon.Comment);
        var ready = editor.HasContent && canSend;
        drawList.AddCircleFilled(sendCenter, buttonRadius,
            ImGui.GetColorU32(ready ? ui.Accent : Palette.WithAlpha(ui.FieldSurface, 0.9f)), 32);
        var sendTapped = ui.IconButton(sendCenter, buttonRadius, IconGlyph.Of(FontAwesomeIcon.PaperPlane),
            ready ? ui.Palette.BackdropBottom : ui.MutedInk, AppSkin.Transparent, 0.8f, Loc.T(L.Music.Live.Send));
        DrawCounter(ui, drawList, field, editor.Text.Length, maxLength, counter, scale);
        return (submitted || sendTapped) && ready;
    }

    private static void DrawCounter(AppSkin ui, ImDrawListPtr drawList, Rect field, int length, int maxLength,
        RadioCountLabel counter, float scale)
    {
        if (!RadioLiveRules.ShowsCounter(length, maxLength))
        {
            return;
        }

        var remaining = maxLength - length;
        var label = counter.Number(remaining);
        var size = Typography.Measure(label, TextStyles.Caption2);
        var ink = remaining <= CounterWarning ? ui.Theme.Danger : ui.MutedInk;
        Typography.Draw(drawList,
            new Vector2(field.Max.X - size.X - Metrics.Space.Md * scale, field.Min.Y - size.Y - Metrics.Space.Xxs * scale),
            label, ink, TextStyles.Caption2);
    }

    private Vector4 NameInkFor(RadioChatEntry entry)
    {
        if (entry.IsDj)
        {
            return ui.Accent;
        }

        return entry.IsMine ? ui.TitleInk : SenderTint.Of(entry.DisplayName);
    }

    private int CountNewerThan(long messageId)
    {
        var count = 0;
        for (var index = rowCount - 1; index >= 0; index--)
        {
            if (rows[index]!.Entry.MessageId <= messageId)
            {
                break;
            }

            count++;
        }

        return count;
    }

    private RadioChatEntry? DrawRow(RadioChatRow row, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var avatarRadius = AvatarRadius * scale;
        var padY = RowPadY * scale;
        var sidePad = Metrics.Space.Xs * scale;
        var textLeft = origin.X + sidePad + avatarRadius * 2f + RowGap * scale;
        var right = origin.X + width - sidePad;
        row.EnsureBody(MathF.Max(1f, right - textLeft), BodyStyle);
        var headerHeight = Typography.LineHeight(NameStyle);
        var height = MathF.Max(avatarRadius * 2f, headerHeight + BodyGap * scale + row.BodyHeight) + padY * 2f;
        var size = new Vector2(width, height);
        if (!ImGui.IsRectVisible(size))
        {
            ImGui.Dummy(size);
            return null;
        }

        var entry = row.Entry;
        var min = origin;
        var max = origin + size;
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(min, max);
        if (entry.IsMine)
        {
            Squircle.Fill(drawList, min, max, Metrics.Radius.Md * scale,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, OwnRowAlpha)));
        }
        else if (hovered)
        {
            Squircle.Fill(drawList, min, max, Metrics.Radius.Md * scale, ImGui.GetColorU32(ui.HoverWash));
        }

        var avatarCenter = new Vector2(min.X + sidePad + avatarRadius, min.Y + padY + avatarRadius);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, ui.Theme, entry.DisplayName, string.Empty,
            entry.AvatarUrl, images, lodestone, 0.8f, 24);

        var headerY = min.Y + padY;
        var menuRadius = 10f * scale;
        var menuCenter = new Vector2(right - menuRadius, headerY + headerHeight * 0.5f);
        var timeWidth = Typography.Measure(entry.TimeLabel, TextStyles.Caption2).X;
        var badge = badgeFor(entry);
        var badgeWidth = badge.Length > 0 ? Typography.Measure(badge, BadgeStyle).X + BadgePadX * 2f * scale : 0f;
        var badgeGap = badge.Length > 0 ? Metrics.Space.Xs * scale : 0f;
        var nameRoom = right - MathF.Max(timeWidth, menuRadius * 2f) - Metrics.Space.Sm * scale - badgeWidth
                       - badgeGap - textLeft;
        var name = row.NameFor(nameRoom, NameStyle);
        Typography.Draw(drawList, new Vector2(textLeft, headerY), name, row.NameInk, NameStyle);
        if (badge.Length > 0)
        {
            var badgeMin = new Vector2(textLeft + Typography.Measure(name, NameStyle).X + badgeGap,
                headerY + headerHeight * 0.12f);
            DrawBadge(drawList, badgeMin, badgeWidth, headerHeight * 0.76f, badge, entry.IsDj, scale);
        }

        var menuTapped = false;
        if (hovered)
        {
            menuTapped = ui.IconButton(menuCenter, menuRadius, IconGlyph.Of(FontAwesomeIcon.EllipsisH), ui.MutedInk,
                AppSkin.Transparent, 0.7f, Loc.T(L.Music.Live.MessageOptions));
        }
        else
        {
            Typography.Draw(drawList,
                new Vector2(right - timeWidth,
                    headerY + (headerHeight - Typography.LineHeight(TextStyles.Caption2)) * 0.5f), entry.TimeLabel,
                ui.MutedInk, TextStyles.Caption2);
        }

        DrawBody(drawList, row, new Vector2(textLeft, headerY + headerHeight + BodyGap * scale));
        var overMenu = hovered && Vector2.DistanceSquared(ImGui.GetMousePos(), menuCenter) <= menuRadius * menuRadius;
        var opened = menuTapped || TrackPress(entry, hovered && !overMenu, scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(size);
        return opened ? entry : null;
    }

    private void DrawBadge(ImDrawListPtr drawList, Vector2 min, float width, float height, string label, bool primary,
        float scale)
    {
        var fill = primary ? ui.Accent : ui.Theme.ToggleOn;
        var max = new Vector2(min.X + width, min.Y + height);
        Squircle.Fill(drawList, min, max, height * 0.35f, ImGui.GetColorU32(fill));
        var size = Typography.Measure(label, BadgeStyle);
        Typography.Draw(drawList, new Vector2(min.X + BadgePadX * scale, min.Y + (height - size.Y) * 0.5f), label,
            ui.Palette.BackdropBottom, BadgeStyle);
    }

    private void DrawBody(ImDrawListPtr drawList, RadioChatRow row, Vector2 origin)
    {
        using (Plugin.Fonts.Push(BodyStyle.Scale, BodyStyle.Weight))
        {
            if (row.Rich is { } rich)
            {
                RichText.Draw(drawList, rich, origin, new RichTextInk(ui.BodyInk, ui.Accent, ui.Accent), out var hit);
                if (hit.Kind == RichTextRunKind.Link && hit.Clicked)
                {
                    UrlActions.AskThenOpen(rich.Urls[hit.TargetIndex]);
                }

                return;
            }

            var font = ImGui.GetFont();
            var fontSize = ImGui.GetFontSize();
            var ink = ImGui.GetColorU32(ui.BodyInk);
            var lines = row.Lines;
            for (var index = 0; index < lines.Length; index++)
            {
                drawList.AddText(font, fontSize, new Vector2(origin.X, origin.Y + index * row.LineHeight), ink,
                    lines[index]);
            }
        }
    }

    private bool TrackPress(RadioChatEntry entry, bool hovered, float scale)
    {
        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            return true;
        }

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            pressedMessageId = entry.MessageId;
            pressedAt = ImGui.GetTime();
            pressedOrigin = ImGui.GetMousePos();
            return false;
        }

        if (pressedMessageId != entry.MessageId)
        {
            return false;
        }

        var slop = LongPressSlop * scale;
        var drifted = Vector2.DistanceSquared(ImGui.GetMousePos(), pressedOrigin) > slop * slop;
        if (!hovered || drifted || !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            pressedMessageId = -1;
            return false;
        }

        if (ImGui.GetTime() - pressedAt < LongPressSeconds)
        {
            return false;
        }

        pressedMessageId = -1;
        UiInteract.CancelPendingTap();
        return true;
    }
}
