using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const float ActivityAvatarUnits = 40f;
    private const float ActivityThumbUnits = 40f;
    private const float ActivityDotUnits = 3f;
    private const long SecondsPerMinute = 60L;
    private const long WeekSeconds = 7L * 86400L;

    private static readonly LocString[] ActivityBuckets =
    {
        L.Prism.ActivityToday, L.Prism.ActivityThisWeek, L.Prism.ActivityEarlier,
    };

    private readonly Dictionary<string, ActivityText> activityText = new(StringComparer.Ordinal);
    private readonly PullToRefresh activityPull = new();
    private Action? refreshActivityNow;

    private readonly record struct ActivityText(string Language, int Fonts, float Width, string First, string Second,
        long Minute, string Time);

    private void OpenActivity()
    {
        social.MarkSeen(Id);
        social.RefreshNow();
        activityFeed.Invalidate();
        Push(PrismScreen.Activity);
    }

    private void OpenLink(SocialDeepLink link)
    {
        photoViewer.Close();
        OpenTab(PrismTab.Discover);
        if (link.Kind == SocialLinkKind.Post)
        {
            OpenGlam(new GlamDto(link.Id));
        }
        else if (link.Id.Length > 0)
        {
            OpenProfile(new GlamAuthorDto(link.Id));
        }
    }

    private void DrawActivity(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        content = AppArea(full, content);
        var top = DrawBackHeader(drawList, content, Loc.T(L.Prism.Activity), scale);
        activityFeed.EnsureFresh(social.Latest);
        var body = new Rect(new Vector2(content.Min.X, top), content.Max);
        using var surface = BeginBody(body);
        activityPull.Draw(body, surface.Pull, surface.Dragging, !activityFeed.Loaded, PrismArt.Muted,
            refreshActivityNow ??= RefreshActivity);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var inset = Metrics.Space.Lg * scale;
        var cursor = origin.Y;
        var items = activityFeed.Items;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var bucket = -1;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            if (item.App != Id || SocialActivity.IsModerationNotice(item.Type) ||
                ActivityLines(item, now, ActivityTextWidth(item, width, scale)).First.Length == 0)
            {
                continue;
            }

            var itemBucket = BucketFor(item.CreatedAtUnix, now);
            if (itemBucket != bucket)
            {
                cursor += (bucket < 0 ? Metrics.Space.Sm : Metrics.Space.Xl) * scale;
                Typography.Draw(list, new Vector2(origin.X + inset, cursor), Loc.T(ActivityBuckets[itemBucket]),
                    PrismArt.Fore, PrismType.Headline);
                cursor += Typography.LineHeight(PrismType.Headline) + Metrics.Space.Xs * scale;
                bucket = itemBucket;
            }

            cursor = DrawActivityRow(list, new Vector2(origin.X, cursor), width, item, now, scale);
        }

        if (bucket < 0 && activityFeed.Loaded)
        {
            var area = new Rect(new Vector2(origin.X, cursor + Metrics.Space.Xxl * scale),
                new Vector2(origin.X + width, cursor + Metrics.Space.Xxl * scale));
            cursor = DrawEmpty(list, area, PhoneIcons.Heart, Loc.T(L.Prism.ActivityEmptyTitle),
                Loc.T(L.Prism.ActivityEmpty), string.Empty, ButtonStyle.Solid, scale, out _);
        }
        else if (bucket < 0)
        {
            InfiniteScroll.DrawLoadingRow(origin.X + width * 0.5f, PrismArt.Muted);
            cursor = ImGui.GetCursorScreenPos().Y;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cursor - origin.Y + PrismArt.BottomMargin(scale)));
        if (InfiniteScroll.ReachedBottom())
        {
            activityFeed.LoadOlder();
        }
    }

    private void RefreshActivity()
    {
        social.RefreshNow();
        activityFeed.Invalidate();
    }

    private static int BucketFor(long createdAtUnix, long now)
    {
        if (TimeText.SameLocalDay(createdAtUnix, now))
        {
            return 0;
        }

        return now - createdAtUnix < WeekSeconds ? 1 : 2;
    }

    private float DrawActivityRow(ImDrawListPtr drawList, Vector2 topLeft, float width, NotificationDto item, long now,
        float scale)
    {
        var pad = Metrics.Space.Md * scale;
        var inset = Metrics.Space.Lg * scale;
        var avatar = ActivityAvatarUnits * scale;
        var thumb = new Vector2(ActivityThumbUnits * scale, ActivityThumbUnits * scale / GlamStore.PhotoRatio);
        var nameLine = Typography.LineHeight(PrismType.Headline);
        var bodyLine = Typography.LineHeight(PrismType.Body);
        var row = new Rect(topLeft,
            topLeft + new Vector2(width, MathF.Max(thumb.Y, nameLine + bodyLine * 2f) + pad * 2f));
        if (!ImGui.IsRectVisible(row.Min, row.Max))
        {
            return row.Max.Y;
        }

        var hovered = HoverWash(drawList, row);
        var middle = row.Center.Y;
        var placing = item.Type == SocialActivity.TypePlacing;
        var center = new Vector2(row.Min.X + inset + avatar * 0.5f, middle);
        if (placing)
        {
            PrismArt.Medal(drawList, center, avatar * 0.5f, item.Rank, 0f, scale);
            var numeral = NumberText.Group(item.Rank);
            var numeralPixels = avatar * 0.5f;
            var size = PrismType.Measure(numeral, PrismFace.Numerals, numeralPixels);
            PrismType.Draw(drawList, center - size * 0.5f, numeral, PrismArt.Fore, PrismFace.Numerals, numeralPixels);
        }
        else
        {
            DrawAuthorAvatar(drawList, center, avatar * 0.5f, SocialActivity.ActorLabel(item), item.ActorAvatarUrl,
                TextStyles.Subheadline.Scale);
        }

        var right = row.Max.X - inset;
        if (!string.IsNullOrEmpty(item.ImageUrl))
        {
            var frame = new Rect(new Vector2(right - thumb.X, middle - thumb.Y * 0.5f),
                new Vector2(right, middle + thumb.Y * 0.5f));
            DrawLook(drawList, frame, item.ImageUrl, false);
            right = frame.Min.X - pad;
        }

        var textLeft = row.Min.X + inset + avatar + pad;
        var text = ActivityLines(item, now, ActivityTextWidth(item, width, scale));
        var bodyLines = text.Second.Length > 0 ? 2 : text.First.Length > 0 ? 1 : 0;
        var textTop = middle - (nameLine + bodyLine * bodyLines) * 0.5f;
        var nameMiddle = textTop + nameLine * 0.5f;
        var timeSize = Typography.Measure(text.Time, PrismType.Caption);
        Typography.Draw(drawList, new Vector2(right - timeSize.X, nameMiddle - timeSize.Y * 0.5f), text.Time,
            PrismArt.Muted, PrismType.Caption);
        var timeWidth = timeSize.X;
        if (!item.Read)
        {
            timeWidth += ActivityDotUnits * scale * 2f + Metrics.Space.Sm * scale;
            drawList.AddCircleFilled(new Vector2(right - timeWidth + ActivityDotUnits * scale, nameMiddle),
                ActivityDotUnits * scale, ImGui.GetColorU32(PrismArt.Fore));
        }

        var name = placing ? Loc.T(L.Prism.ActivityResults) : SocialActivity.ActorLabel(item);
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(name, MathF.Max(1f, right - timeWidth - pad - textLeft), PrismType.Headline),
            PrismArt.Fore, PrismType.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + nameLine), text.First, PrismArt.Muted,
            PrismType.Body);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + nameLine + bodyLine), text.Second, PrismArt.Muted,
            PrismType.Body);
        FeedCell.Hairline(drawList, textLeft, row.Max.X, row.Max.Y, PrismArt.Faint);
        var tapped = UiInteract.Click(row.Min, row.Max, hovered);
        var reach = new Vector2(Metrics.Size.TapTarget * scale * 0.5f);
        var avatarHit = new Rect(center - reach, center + reach);
        var onAvatar = !placing && item.ActorId.Length > 0 && UiInteract.Click(avatarHit.Min, avatarHit.Max);
        if (!tapped && !onAvatar)
        {
            return row.Max.Y;
        }

        if (!onAvatar && SocialActivity.OpensPost(item))
        {
            OpenGlam(new GlamDto(item.PostId!));
        }
        else if (item.ActorId.Length > 0)
        {
            OpenProfile(new GlamAuthorDto(item.ActorId, item.ActorDisplayName, item.ActorHandle,
                item.ActorAvatarUrl ?? string.Empty));
        }

        return row.Max.Y;
    }

    private static float ActivityTextWidth(NotificationDto item, float width, float scale)
    {
        var thumb = string.IsNullOrEmpty(item.ImageUrl) ? 0f : (ActivityThumbUnits + Metrics.Space.Md) * scale;
        return width - Metrics.Space.Lg * 2f * scale - ActivityAvatarUnits * scale - Metrics.Space.Md * scale - thumb;
    }

    private ActivityText ActivityLines(NotificationDto item, long now, float width)
    {
        var minute = now / SecondsPerMinute;
        if (activityText.TryGetValue(item.Id, out var cached) && cached.Language == Loc.Current.Code &&
            cached.Fonts == Plugin.Fonts.Generation && cached.Width == width)
        {
            if (cached.Minute == minute)
            {
                return cached;
            }

            cached = cached with { Minute = minute, Time = TimeText.Short(item.CreatedAtUnix) };
        }
        else
        {
            var lines = Typography.WrapText(SocialActivity.Body(item), PrismType.Body, width);
            var second = lines.Length switch
            {
                < 2 => string.Empty,
                2 => Typography.FitText(lines[1], width, PrismType.Body),
                _ => Typography.FitText(string.Join(' ', lines, 1, lines.Length - 1), width, PrismType.Body),
            };
            cached = new ActivityText(Loc.Current.Code, Plugin.Fonts.Generation, width,
                lines.Length > 0 ? Typography.FitText(lines[0], width, PrismType.Body) : string.Empty, second, minute,
                TimeText.Short(item.CreatedAtUnix));
        }

        activityText[item.Id] = cached;
        return cached;
    }
}
