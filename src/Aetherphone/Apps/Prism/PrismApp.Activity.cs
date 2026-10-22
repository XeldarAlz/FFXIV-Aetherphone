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
    private const long MonthSeconds = 30L * 86400L;

    private static readonly LocString[] ActivityBuckets =
    {
        L.Prism.ActivityToday, L.Prism.ActivityThisWeek, L.Prism.ActivityThisMonth, L.Prism.ActivityEarlier,
    };

    private readonly Dictionary<string, ActivityText> activityText = new(StringComparer.Ordinal);

    private readonly record struct ActivityText(string Language, float Width, string First, string Second, long Minute,
        string Time);

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

    private void DrawActivity(ImDrawListPtr drawList, Rect content, float scale)
    {
        var top = DrawHeader(drawList, content, string.Empty, string.Empty, true, scale);
        var gutter = PrismArt.Gutter(scale);
        var titlePixels = PrismType.TitleUnits * scale;
        PrismType.Draw(drawList, new Vector2(content.Min.X + gutter, top), Loc.T(L.Prism.Activity), PrismArt.Fore,
            PrismFace.Headline, titlePixels);
        top += PrismType.LineHeight(PrismFace.Headline, titlePixels) + Metrics.Space.Lg * scale;
        activityFeed.EnsureFresh(social.Latest);
        var body = new Rect(new Vector2(content.Min.X, top), content.Max);
        using var surface = BeginBody(body);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth() - gutter * 2f;
        var left = origin.X + gutter;
        var cursor = origin.Y;
        var items = activityFeed.Items;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var bucket = -1;
        var sections = 0;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            if (item.App != Id || SocialActivity.IsModerationNotice(item.Type))
            {
                continue;
            }

            var itemBucket = BucketFor(item.CreatedAtUnix, now);
            if (itemBucket != bucket)
            {
                cursor = DrawSectionLabel(list, left, cursor + (sections == 0 ? 0f : Metrics.Space.Lg * scale), width,
                    sections++, Loc.T(ActivityBuckets[itemBucket]), scale);
                bucket = itemBucket;
            }

            cursor = DrawActivityRow(list, left, cursor, width, item, now, scale);
        }

        if (sections == 0)
        {
            cursor += Typography.DrawWrappedLeft(new Vector2(left, cursor),
                Loc.T(activityFeed.Loaded ? L.Prism.ActivityEmpty : L.Common.Loading), PrismArt.Muted,
                TextStyles.Subheadline, width);
        }

        ImGui.Dummy(new Vector2(width, cursor - origin.Y + PrismArt.BottomMargin(scale)));
        if (InfiniteScroll.ReachedBottom())
        {
            activityFeed.LoadOlder();
        }
    }

    private static int BucketFor(long createdAtUnix, long now)
    {
        if (TimeText.SameLocalDay(createdAtUnix, now))
        {
            return 0;
        }

        var age = now - createdAtUnix;
        return age < WeekSeconds ? 1 : age < MonthSeconds ? 2 : 3;
    }

    private float DrawActivityRow(ImDrawListPtr drawList, float left, float top, float width, NotificationDto item,
        long now, float scale)
    {
        var pad = Metrics.Space.Md * scale;
        var avatar = ActivityAvatarUnits * scale;
        var thumb = new Vector2(ActivityThumbUnits * scale, ActivityThumbUnits * scale / GlamStore.PhotoRatio);
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var row = new Rect(new Vector2(left, top),
            new Vector2(left + width, top + MathF.Max(thumb.Y, labelLine * 3f) + pad * 2f));
        if (!ImGui.IsRectVisible(row.Min, row.Max))
        {
            return row.Max.Y;
        }

        var middle = row.Center.Y;
        var placing = item.Type == SocialActivity.TypePlacing;
        var center = new Vector2(left + avatar * 0.5f, middle);
        if (placing)
        {
            PrismArt.Medal(drawList, center, avatar * 0.5f, item.Rank, 0f, scale);
            var numeral = Plain[Math.Clamp(item.Rank, 0, Plain.Length - 1)];
            var numeralPixels = avatar * 0.5f;
            var size = PrismType.Measure(numeral, PrismFace.Numerals, numeralPixels);
            PrismType.Draw(drawList, center - size * 0.5f, numeral, PrismArt.Fore, PrismFace.Numerals, numeralPixels);
        }
        else
        {
            DrawAuthorAvatar(drawList, center, avatar * 0.5f, SocialActivity.ActorLabel(item), item.ActorAvatarUrl,
                TextStyles.Subheadline.Scale);
        }

        var right = row.Max.X;
        if (!string.IsNullOrEmpty(item.ImageUrl))
        {
            var frame = new Rect(new Vector2(right - thumb.X, middle - thumb.Y * 0.5f),
                new Vector2(right, middle + thumb.Y * 0.5f));
            DrawLook(drawList, frame, item.ImageUrl, false);
            right = frame.Min.X - pad;
        }

        var textLeft = left + avatar + pad;
        var text = ActivityLines(item, now, right - textLeft, labelPixels);
        var bodyLines = text.Second.Length > 0 ? 2 : text.First.Length > 0 ? 1 : 0;
        var textTop = middle - labelLine * (1 + bodyLines) * 0.5f;
        var timeWidth = PrismType.DrawRight(drawList, new Vector2(right, textTop), text.Time, PrismArt.Muted,
            PrismFace.Label, labelPixels);
        if (!item.Read)
        {
            timeWidth += ActivityDotUnits * scale * 2f + Metrics.Space.Sm * scale;
            drawList.AddCircleFilled(new Vector2(right - timeWidth + ActivityDotUnits * scale, textTop + labelLine * 0.5f),
                ActivityDotUnits * scale, ImGui.GetColorU32(PrismArt.Fore));
        }

        var name = placing ? Loc.T(L.Prism.ActivityResults) : SocialActivity.ActorLabel(item);
        PrismType.Draw(drawList, new Vector2(textLeft, textTop),
            PrismType.FitLabel(name, labelPixels, right - timeWidth - pad - textLeft), PrismArt.Fore, PrismFace.Label,
            labelPixels);
        PrismType.Draw(drawList, new Vector2(textLeft, textTop + labelLine), text.First, PrismArt.Muted,
            PrismFace.Label, labelPixels);
        PrismType.Draw(drawList, new Vector2(textLeft, textTop + labelLine * 2f), text.Second, PrismArt.Muted,
            PrismFace.Label, labelPixels);
        PrismArt.Hairline(drawList, textLeft, row.Max.X, row.Max.Y, PrismArt.Faint, scale);
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            if (SocialActivity.OpensPost(item))
            {
                OpenGlam(new GlamDto(item.PostId!));
            }
            else if (item.ActorId.Length > 0)
            {
                OpenProfile(new GlamAuthorDto(item.ActorId, item.ActorDisplayName, item.ActorHandle,
                    item.ActorAvatarUrl ?? string.Empty));
            }
        }

        return row.Max.Y;
    }

    private ActivityText ActivityLines(NotificationDto item, long now, float width, float labelPixels)
    {
        var minute = now / SecondsPerMinute;
        if (activityText.TryGetValue(item.Id, out var cached) && cached.Language == Loc.Current.Code &&
            cached.Width == width)
        {
            if (cached.Minute == minute)
            {
                return cached;
            }

            cached = cached with { Minute = minute, Time = TimeText.Short(item.CreatedAtUnix) };
        }
        else
        {
            var lines = PrismType.Wrap(SocialActivity.Body(item), PrismFace.Label, labelPixels, width);
            var second = lines.Length switch
            {
                < 2 => string.Empty,
                2 => PrismType.FitLabel(lines[1], labelPixels, width),
                _ => PrismType.FitLabel(string.Join(' ', lines, 1, lines.Length - 1), labelPixels, width),
            };
            cached = new ActivityText(Loc.Current.Code, width,
                lines.Length > 0 ? PrismType.FitLabel(lines[0], labelPixels, width) : string.Empty, second, minute,
                TimeText.Short(item.CreatedAtUnix));
        }

        activityText[item.Id] = cached;
        return cached;
    }
}
