using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.News;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.News;

internal sealed partial class NewsApp
{
    private const float HeroGlassOpacity = 0.92f;
    private const float HeroTitleGap = 2f;
    private const float HeroSubGap = 2f;
    private const float HeroBarGap = 14f;
    private const float HeroColumnGap = 12f;
    private const float HeroSubAlpha = 0.82f;
    private const float HeroGlyphCircle = 40f;
    private const float HeroGlyphSize = 18f;
    private const float HeroGlyphAlpha = 0.22f;
    private const int StatusTitleLines = 2;
    private const float BannerHeight = 66f;
    private const float BannerGap = 16f;
    private const float BannerGlyphCircle = 36f;
    private const float BannerGlyphSize = 16f;
    private const float BannerTextGap = 12f;
    private const long BannerHorizonSeconds = 72L * 3600L;
    private const string HeroId = "news.hero";
    private const string BannerId = "news.banner";
    private const string StatusAnchor = "news.status";

    private static readonly MaintenanceStatus[] SectionOrder =
    {
        MaintenanceStatus.Active, MaintenanceStatus.Upcoming, MaintenanceStatus.Done,
    };

    private static readonly Vector4 HeroInk = new(1f, 1f, 1f, 1f);

    private readonly string?[] statusEyebrows = new string?[4];
    private CultureInfo? eyebrowCulture;
    private CachedText heroCountdown;
    private CachedText bannerCountdown;
    private CachedText articleCountdown;
    private CachedText lastMaintenance;

    private float DrawMaintenanceBanner(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var feed = feeds[(int)NewsCategory.Maintenance];
        if (!feed.HasStories)
        {
            return top;
        }

        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var pick = feed.Board.Pick(nowUnix);
        var window = feed.Board.WindowOf(pick.Index);
        var due = pick.Status == MaintenanceStatus.Active ||
                  (pick.Status == MaintenanceStatus.Upcoming && window.StartUnix - nowUnix <= BannerHorizonSeconds);
        if (!due)
        {
            return top;
        }

        var story = feed.Stories[pick.Index];
        var active = pick.Status == MaintenanceStatus.Active;
        var rest = new Rect(new Vector2(left, top), new Vector2(left + width, top + BannerHeight * scale));
        if (!ImGui.IsRectVisible(rest.Min, rest.Max))
        {
            return rest.Max.Y + BannerGap * scale;
        }

        var hovered = UiInteract.Hover(rest.Min, rest.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(BannerId, pressed, PressFx.CardPressedScale);
        var half = rest.Size * 0.5f * press;
        var card = new Rect(rest.Center - half, rest.Center + half);
        Material.AccentGlass(drawList, card.Min, card.Max, Metrics.Radius.Grouped * scale * press, scale,
            StatusColor(pick.Status), HeroGlassOpacity);
        var pad = Metrics.Space.Lg * scale;
        var subInk = Palette.WithAlpha(HeroInk, HeroSubAlpha);
        var glyphRadius = BannerGlyphCircle * scale * 0.5f;
        var glyphCenter = new Vector2(left + pad + glyphRadius, rest.Center.Y);
        drawList.AddCircleFilled(glyphCenter, glyphRadius, ImGui.GetColorU32(HeroInk with { W = HeroGlyphAlpha }), 32);
        ProgressRing.CenterIcon(drawList, glyphCenter, FontAwesomeIcon.Wrench, HeroInk, BannerGlyphSize * scale);

        var target = active ? window.EndUnix : window.StartUnix;
        var value = WidgetText.Countdown(ref bannerCountdown, TimeSpan.FromSeconds(target - nowUnix));
        var caption = Loc.T(active ? L.News.EndsIn : L.News.StartsIn);
        var columnWidth = MathF.Max(WidgetText.TabularWidth(value, TextStyles.Headline),
            Typography.Measure(caption, TextStyles.Footnote).X);
        var right = rest.Max.X - pad;
        var captionHeight = Typography.LineHeight(TextStyles.Footnote);
        var valueHeight = Typography.LineHeight(TextStyles.Headline);
        var columnTop = rest.Center.Y - (captionHeight + valueHeight) * 0.5f;
        WidgetText.DrawRight(drawList, right, columnTop, caption, subInk, TextStyles.Footnote);
        WidgetText.TabularRight(drawList, right, columnTop + captionHeight, value, HeroInk, TextStyles.Headline);

        var textLeft = glyphCenter.X + glyphRadius + BannerTextGap * scale;
        var textWidth = MathF.Max(1f, right - columnWidth - HeroColumnGap * scale - textLeft);
        var headline = Loc.T(active ? L.News.BannerActive : L.News.BannerUpcoming);
        var textTop = rest.Center.Y - (valueHeight + Typography.LineHeight(TextStyles.Subheadline)) * 0.5f;
        WidgetText.Draw(drawList, new Vector2(textLeft, textTop), headline, HeroInk, TextStyles.Headline, textWidth);
        WidgetText.Draw(drawList, new Vector2(textLeft, textTop + valueHeight), story.WindowText(window), subInk,
            TextStyles.Subheadline, textWidth);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(rest.Min, rest.Max, hovered))
        {
            SelectCategory(NewsCategory.Maintenance);
        }

        return rest.Max.Y + BannerGap * scale;
    }

    private float DrawMaintenancePage(ImDrawListPtr drawList, float left, float top, float width, NewsFeed feed,
        float scale)
    {
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var pick = feed.Board.Pick(nowUnix);
        var cursorY = top;
        if (pick.Status is MaintenanceStatus.Active or MaintenanceStatus.Upcoming)
        {
            cursorY = DrawStatusHero(drawList, left, top, width, feed.Stories[pick.Index],
                feed.Board.WindowOf(pick.Index), pick.Status, ref heroCountdown, true, nowUnix, scale);
        }
        else if (pick.Exists)
        {
            cursorY = DrawAllClear(drawList, left, top, width, feed, pick.Index, scale);
        }

        if (cursorY > top)
        {
            UiAnchors.Report(StatusAnchor, new Rect(new Vector2(left, top), new Vector2(left + width, cursorY)));
        }

        var anchorTaken = false;
        for (var sectionIndex = 0; sectionIndex < SectionOrder.Length; sectionIndex++)
        {
            var status = SectionOrder[sectionIndex];
            var count = CollectStatus(feed.Board, status, nowUnix);
            if (count == 0)
            {
                continue;
            }

            if (cursorY > top)
            {
                cursorY += NewsArt.SectionTopGap * scale;
            }

            cursorY += CardSectionHeader.Draw(drawList, new Vector2(left, cursorY), width, StatusLabel(status),
                ui.TitleInk) + NewsArt.SectionHeaderGap * scale;
            cursorY = DrawRowGroup(drawList, left, cursorY, width, feed, rowBuffer.AsSpan(0, count), ref anchorTaken,
                scale);
        }

        return cursorY;
    }

    private int CollectStatus(MaintenanceBoard board, MaintenanceStatus status, long nowUnix)
    {
        var count = 0;
        for (var index = 0; index < board.Count && count < rowBuffer.Length; index++)
        {
            var window = board.WindowOf(index);
            var own = window.StatusAt(nowUnix);
            var bucket = own == MaintenanceStatus.None ? MaintenanceStatus.Done : own;
            if (bucket != status)
            {
                continue;
            }

            var slot = count;
            if (status == MaintenanceStatus.Upcoming)
            {
                while (slot > 0 && board.WindowOf(rowBuffer[slot - 1]).StartUnix > window.StartUnix)
                {
                    rowBuffer[slot] = rowBuffer[slot - 1];
                    slot--;
                }
            }

            rowBuffer[slot] = index;
            count++;
        }

        return count;
    }

    private float DrawStatusHero(ImDrawListPtr drawList, float left, float top, float width, NewsStory story,
        MaintenanceWindow window, MaintenanceStatus status, ref CachedText countdown, bool interactive, long nowUnix,
        float scale)
    {
        var pad = Metrics.Space.Lg * scale;
        var right = left + width - pad;
        var textLeft = left + pad;
        var counting = status is MaintenanceStatus.Active or MaintenanceStatus.Upcoming;
        var value = string.Empty;
        var caption = string.Empty;
        var columnWidth = 0f;
        if (counting)
        {
            var target = status == MaintenanceStatus.Active ? window.EndUnix : window.StartUnix;
            value = WidgetText.Countdown(ref countdown, TimeSpan.FromSeconds(target - nowUnix));
            caption = Loc.T(status == MaintenanceStatus.Active ? L.News.EndsIn : L.News.StartsIn);
            columnWidth = MathF.Max(WidgetText.TabularWidth(value, TextStyles.WidgetDisplayCompact),
                Typography.Measure(caption, TextStyles.Footnote).X) + HeroColumnGap * scale;
        }

        var textWidth = MathF.Max(1f, right - columnWidth - textLeft);
        var eyebrowHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var titleLineHeight = Typography.LineHeight(TextStyles.Title3);
        var subHeight = Typography.LineHeight(TextStyles.Subheadline);
        var titleLines = story.HeroTitle.Get(story.Item.Title, TextStyles.Title3, textWidth, StatusTitleLines, fontKey);
        var bar = status == MaintenanceStatus.Active;
        var height = pad + eyebrowHeight + HeroTitleGap * scale + Math.Max(1, titleLines.Length) * titleLineHeight +
                     HeroSubGap * scale + subHeight + (bar ? (HeroBarGap + NewsArt.BarHeight) * scale : 0f) + pad;
        var rest = new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
        if (!ImGui.IsRectVisible(rest.Min, rest.Max))
        {
            return rest.Max.Y;
        }

        var hovered = interactive && UiInteract.Hover(rest.Min, rest.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = interactive ? PressFx.Scale(HeroId, pressed, PressFx.CardPressedScale) : 1f;
        var half = rest.Size * 0.5f * press;
        var card = new Rect(rest.Center - half, rest.Center + half);
        Material.AccentGlass(drawList, card.Min, card.Max, Metrics.Radius.Grouped * scale * press, scale,
            StatusColor(status), HeroGlassOpacity);
        var subInk = Palette.WithAlpha(HeroInk, HeroSubAlpha);
        var cursorY = top + pad;
        Typography.Draw(drawList, new Vector2(textLeft, cursorY),
            Typography.FitText(StatusEyebrow(status), textWidth, TextStyles.FootnoteEmphasized), subInk,
            TextStyles.FootnoteEmphasized);
        cursorY += eyebrowHeight + HeroTitleGap * scale;
        var titleTop = cursorY;
        for (var lineIndex = 0; lineIndex < titleLines.Length; lineIndex++)
        {
            Typography.Draw(drawList, new Vector2(textLeft, cursorY), titleLines[lineIndex], HeroInk,
                TextStyles.Title3);
            cursorY += titleLineHeight;
        }

        cursorY = titleTop + Math.Max(1, titleLines.Length) * titleLineHeight + HeroSubGap * scale;
        var windowText = window.IsEmpty ? story.LongDate : story.WindowText(window);
        Typography.Draw(drawList, new Vector2(textLeft, cursorY),
            Typography.FitText(windowText, textWidth, TextStyles.Subheadline), subInk, TextStyles.Subheadline);
        if (counting)
        {
            var captionHeight = Typography.LineHeight(TextStyles.Footnote);
            WidgetText.DrawRight(drawList, right, titleTop, caption, subInk, TextStyles.Footnote);
            WidgetText.TabularRight(drawList, right, titleTop + captionHeight, value, HeroInk,
                TextStyles.WidgetDisplayCompact);
        }

        if (bar)
        {
            var barTop = rest.Max.Y - pad - NewsArt.BarHeight * scale;
            NewsArt.Bar(drawList, new Vector2(textLeft, barTop), new Vector2(right, barTop + NewsArt.BarHeight * scale),
                window.Progress(nowUnix), HeroInk);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (interactive && UiInteract.Click(rest.Min, rest.Max, hovered))
        {
            OpenStory(NewsCategory.Maintenance, story);
        }

        return rest.Max.Y;
    }

    private float DrawAllClear(ImDrawListPtr drawList, float left, float top, float width, NewsFeed feed,
        int lastIndex, float scale)
    {
        var pad = Metrics.Space.Lg * scale;
        var glyphRadius = HeroGlyphCircle * scale * 0.5f;
        var textLeft = left + pad + glyphRadius * 2f + BannerTextGap * scale;
        var textWidth = MathF.Max(1f, left + width - pad - textLeft);
        var subtitle = LastMaintenanceText(feed, lastIndex);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var subtitleHeight = Typography.LineHeight(TextStyles.Subheadline);
        var height = pad * 2f + MathF.Max(glyphRadius * 2f, titleHeight + subtitleHeight);
        var min = new Vector2(left, top);
        var max = new Vector2(left + width, top + height);
        Material.AccentGlass(drawList, min, max, Metrics.Radius.Grouped * scale, scale, AccentRing.Green,
            HeroGlassOpacity);
        var center = new Vector2(left + pad + glyphRadius, (min.Y + max.Y) * 0.5f);
        drawList.AddCircleFilled(center, glyphRadius, ImGui.GetColorU32(HeroInk with { W = HeroGlyphAlpha }), 32);
        ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.Check, HeroInk, HeroGlyphSize * scale);
        var textTop = center.Y - (titleHeight + subtitleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(Loc.T(L.News.AllClear), textWidth, TextStyles.Headline), HeroInk, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + titleHeight),
            Typography.FitText(subtitle, textWidth, TextStyles.Subheadline), Palette.WithAlpha(HeroInk, HeroSubAlpha),
            TextStyles.Subheadline);
        return max.Y;
    }

    private string LastMaintenanceText(NewsFeed feed, int index)
    {
        var window = feed.Board.WindowOf(index);
        var key = window.StartUnix ^ (window.EndUnix << 1);
        return lastMaintenance.IsCurrent(key)
            ? lastMaintenance.Value
            : lastMaintenance.Store(key, Loc.T(L.News.LastMaintenance, NewsFormat.Window(window)));
    }

    private string StatusEyebrow(MaintenanceStatus status)
    {
        if (!ReferenceEquals(eyebrowCulture, Loc.Culture))
        {
            Array.Clear(statusEyebrows);
            eyebrowCulture = Loc.Culture;
        }

        ref var label = ref statusEyebrows[(int)status];
        label ??= Loc.Upper(StatusLabel(status));
        return label;
    }

    private static string StatusLabel(MaintenanceStatus status) =>
        status switch
        {
            MaintenanceStatus.Upcoming => Loc.T(L.News.Upcoming),
            MaintenanceStatus.Active => Loc.T(L.News.Active),
            _ => Loc.T(L.News.Ended),
        };

    private Vector4 StatusColor(MaintenanceStatus status) =>
        status switch
        {
            MaintenanceStatus.Upcoming => AccentRing.Azure,
            MaintenanceStatus.Active => AccentRing.Orange,
            _ => ui.Accent,
        };
}
