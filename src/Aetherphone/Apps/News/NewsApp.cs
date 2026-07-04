using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Net;
using Aetherphone.Core.News;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.News;

internal sealed class NewsApp : IPhoneApp
{
    private const float RegionRowHeight = 24f;
    private const float SegmentRowHeight = 36f;
    private const float CardGap = 14f;
    private const float CardRounding = 20f;
    private const float CardPadding = 16f;
    private const float ImageAspectFallback = 0.56f;
    private const float ImageAspectMin = 0.22f;
    private const float ImageAspectMax = 0.75f;
    private const float TitleScale = 1.10f;
    private const float DescriptionScale = 0.88f;
    private const float MetaScale = 0.78f;
    private const float ImageFadeSeconds = 0.28f;
    private const int MaxItems = 40;
    private const int MaxTitleLines = 2;
    private const int MaxDescriptionLines = 3;
    private const float RowHeightNotices = 58f;
    private const float RowHeightMaintenance = 72f;
    private const float RowTitleScale = 0.95f;

    private static readonly Vector4 StatusUpcoming = new(0.95f, 0.62f, 0.22f, 1f);
    private static readonly Vector4 StatusActive = new(0.30f, 0.78f, 0.46f, 1f);

    public string Id => "news";

    public string DisplayName => Loc.T(L.Apps.News);

    public string Glyph => "Ne";

    public Vector4 Accent => new(0.96f, 0.44f, 0.27f, 1f);

    public int BadgeCount => 0;

    private readonly NewsService news;
    private readonly MediaCache media;
    private readonly HttpService http;
    private readonly GameData gameData;
    private readonly NewsUi ui = new();

    private readonly string[] categoryLabels = new string[NewsCategories.All.Length];
    private readonly List<string> titleLines = new();
    private readonly List<string> descriptionLines = new();
    private readonly Dictionary<string, float> imageFade = new();

    private PhoneTheme theme = PhoneTheme.Default;
    private string locale = "na";
    private int categoryIndex;
    private bool forceRefresh;
    private bool resetScroll;

    public NewsApp(NewsService news, MediaCache media, HttpService http, GameData gameData)
    {
        this.news = news;
        this.media = media;
        this.http = http;
        this.gameData = gameData;
    }

    public void OnOpened()
    {
        locale = gameData.LodestoneLocale();
        categoryIndex = 0;
        resetScroll = true;
    }

    public void OnClosed() => imageFade.Clear();

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        ui.Theme = theme;
        var area = context.Content;
        var scale = ImGuiHelpers.GlobalScale;

        var screen = SceneChrome.ScreenFrom(area, theme, scale);
        ui.Backdrop(screen);

        AppHeader.Draw(context, DisplayName);

        var top = area.Min.Y + AppHeader.Height * scale;
        DrawRegionRow(new Vector2(area.Min.X + 18f * scale, top + RegionRowHeight * scale * 0.5f));

        var segmentTop = top + RegionRowHeight * scale;
        var segmentRow = new Rect(new Vector2(area.Min.X + 16f * scale, segmentTop), new Vector2(area.Max.X - 16f * scale, segmentTop + SegmentRowHeight * scale));
        FillCategoryLabels();
        var selected = SegmentStrip.Draw("news.category", segmentRow, categoryLabels, categoryIndex, theme);
        if (selected != categoryIndex)
        {
            categoryIndex = selected;
            resetScroll = true;
            forceRefresh = false;
        }

        var category = NewsCategories.All[categoryIndex];
        var entry = news.Request(category, locale, forceRefresh);
        forceRefresh = false;

        DrawRefreshControl(new Vector2(area.Max.X - 20f * scale, area.Min.Y + AppHeader.Height * scale * 0.5f), entry.State, scale);

        var body = new Rect(new Vector2(area.Min.X, segmentRow.Max.Y), area.Max);
        var hasItems = entry.Items.Length > 0;

        if (!hasItems)
        {
            ui.Body(body);
            DrawState(body, entry.State, scale);
            return;
        }

        ui.Body(body);

        using (AppSurface.Begin(body))
        {
            if (resetScroll)
            {
                ImGui.SetScrollY(0f);
                resetScroll = false;
            }

            DrawFeed(entry.Items, Math.Min(entry.Items.Length, MaxItems), category, scale);
        }
    }

    private void DrawState(Rect body, NewsState state, float scale)
    {
        var center = body.Center;
        if (state == NewsState.Failed)
        {
            ProgressRing.CenterIcon(new Vector2(center.X, center.Y - 26f * scale), FontAwesomeIcon.CloudDownloadAlt, theme.TextMuted, 34f * scale);
            Typography.DrawCentered(new Vector2(center.X, center.Y + 18f * scale), Loc.T(L.News.CouldntReach), theme.TextMuted, 0.95f, FontWeight.Medium);
            if (DrawTextButton(new Vector2(center.X, center.Y + 48f * scale), Loc.T(L.News.TryAgain), Accent, scale))
            {
                forceRefresh = true;
            }

            return;
        }

        if (state == NewsState.Empty)
        {
            ProgressRing.CenterIcon(new Vector2(center.X, center.Y - 24f * scale), FontAwesomeIcon.Newspaper, theme.TextMuted, 34f * scale);
            Typography.DrawCentered(new Vector2(center.X, center.Y + 20f * scale), Loc.T(L.News.NoNews), theme.TextMuted, 0.95f, FontWeight.Medium);
            return;
        }

        DrawSpinner(new Vector2(center.X, center.Y - 6f * scale), 13f * scale, Accent);
        Typography.DrawCentered(new Vector2(center.X, center.Y + 24f * scale), Loc.T(L.Common.Loading), theme.TextMuted, 0.9f);
    }

    private void DrawFeed(LodestoneNewsItem[] items, int count, NewsCategory category, float scale)
    {
        ImGui.Dummy(new Vector2(0f, 4f * scale));

        if (category == NewsCategory.Topics)
        {
            for (var index = 0; index < count; index++)
            {
                var origin = ImGui.GetCursorScreenPos();
                var width = ImGui.GetContentRegionAvail().X;
                var height = DrawTopicCard(items[index], origin, width, scale);
                ImGui.SetCursorScreenPos(origin);
                ImGui.Dummy(new Vector2(width, height));
                ImGui.Dummy(new Vector2(0f, CardGap * scale));
            }

            return;
        }

        var rowHeight = category == NewsCategory.Maintenance ? RowHeightMaintenance : RowHeightNotices;
        var card = GroupCard.Begin(theme, count, rowHeight);
        for (var index = 0; index < count; index++)
        {
            var row = card.NextRow();
            var hovered = ImGui.IsMouseHoveringRect(row.Min, row.Max);
            if (category == NewsCategory.Maintenance)
            {
                DrawMaintenanceRow(row, items[index], scale, hovered);
            }
            else
            {
                DrawSimpleRow(row, items[index], scale, hovered);
            }

            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    UrlActions.OpenInBrowser(items[index].Url);
                }
            }
        }

        card.End();
    }

    private float DrawTopicCard(LodestoneNewsItem item, Vector2 origin, float width, float scale)
    {
        var hasImage = !string.IsNullOrEmpty(item.Image);
        var imageHeight = 0f;
        if (hasImage)
        {
            var probe = Thumb(item.Image!);
            if (probe.Texture is { } probeTexture && probeTexture.Size.X > 0f && probeTexture.Size.Y > 0f)
            {
                var naturalAspect = probeTexture.Size.Y / probeTexture.Size.X;
                imageHeight = width * Math.Clamp(naturalAspect, ImageAspectMin, ImageAspectMax);
            }
            else
            {
                imageHeight = width * ImageAspectFallback;
            }
        }

        var innerWidth = width - 2f * CardPadding * scale;

        WrapInto(titleLines, item.Title, innerWidth, TitleScale, FontWeight.SemiBold, MaxTitleLines);
        var hasDescription = !string.IsNullOrWhiteSpace(item.Description);
        if (hasDescription)
        {
            WrapInto(descriptionLines, item.Description!, innerWidth, DescriptionScale, FontWeight.Regular, MaxDescriptionLines);
        }
        else
        {
            descriptionLines.Clear();
        }

        var titleLineHeight = Typography.Measure("Ag", TitleScale, FontWeight.SemiBold).Y + 2f * scale;
        var descLineHeight = Typography.Measure("Ag", DescriptionScale, FontWeight.Regular).Y + 2f * scale;
        var metaHeight = Typography.Measure("Ag", MetaScale, FontWeight.Regular).Y;

        var contentHeight = titleLines.Count * titleLineHeight
            + (descriptionLines.Count > 0 ? 5f * scale + descriptionLines.Count * descLineHeight : 0f)
            + 8f * scale + metaHeight;
        var cardHeight = imageHeight + CardPadding * scale + contentHeight + CardPadding * scale;
        var cardMax = new Vector2(origin.X + width, origin.Y + cardHeight);
        var rounding = CardRounding * scale;

        var drawList = ImGui.GetWindowDrawList();
        Elevation.Card(drawList, origin, cardMax, rounding, scale, 0.7f);

        Squircle.Fill(drawList, origin, cardMax, rounding, ImGui.GetColorU32(theme.GroupedCard));

        if (hasImage)
        {
            var imageMax = new Vector2(cardMax.X, origin.Y + imageHeight);
            drawList.AddRectFilled(origin, imageMax, ImGui.GetColorU32(theme.SurfaceMuted), rounding, ImDrawFlags.RoundCornersTop);
            DrawCardImage(drawList, item.Image!, origin, imageMax, rounding, scale);
        }

        var textX = origin.X + CardPadding * scale;
        var cursorY = origin.Y + imageHeight + CardPadding * scale;
        for (var lineIndex = 0; lineIndex < titleLines.Count; lineIndex++)
        {
            Typography.Draw(new Vector2(textX, cursorY), titleLines[lineIndex], theme.TextStrong, TitleScale, FontWeight.SemiBold);
            cursorY += titleLineHeight;
        }

        if (descriptionLines.Count > 0)
        {
            cursorY += 5f * scale;
            for (var lineIndex = 0; lineIndex < descriptionLines.Count; lineIndex++)
            {
                Typography.Draw(new Vector2(textX, cursorY), descriptionLines[lineIndex], theme.TextMuted, DescriptionScale, FontWeight.Regular);
                cursorY += descLineHeight;
            }
        }

        cursorY += 8f * scale;
        Typography.Draw(new Vector2(textX, cursorY), NewsFormat.Ago(item.Time), theme.TextMuted, MetaScale, FontWeight.Medium);

        Material.EdgeSquircle(drawList, origin, cardMax, rounding, scale);

        InteractCard(new Rect(origin, cardMax), rounding, item.Url, drawList);
        return cardHeight;
    }

    private void DrawCardImage(ImDrawListPtr drawList, string url, Vector2 min, Vector2 max, float rounding, float scale)
    {
        var result = Thumb(url);
        if (result.Texture is { } texture)
        {
            var fade = StepFade(url, true);
            var tint = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, fade));
            drawList.AddImageRounded(texture.Handle, min, max, Vector2.Zero, Vector2.One, tint, rounding, ImDrawFlags.RoundCornersTop);
            if (fade < 1f)
            {
                DrawSpinner((min + max) * 0.5f, 11f * scale, Palette.WithAlpha(theme.TextMuted, 1f - fade));
            }

            return;
        }

        StepFade(url, false);
        if (result.Loading)
        {
            DrawSpinner((min + max) * 0.5f, 11f * scale, theme.TextMuted);
            return;
        }

        ProgressRing.CenterIcon((min + max) * 0.5f, FontAwesomeIcon.Image, Palette.WithAlpha(theme.TextMuted, 0.5f), 22f * scale);
    }

    private void DrawSimpleRow(Rect row, LodestoneNewsItem item, float scale, bool hovered)
    {
        var titleY = row.Min.Y + 10f * scale;
        var maxTitleWidth = row.Width - 24f * scale;
        var clippedTitle = PixelEllipsize(item.Title, maxTitleWidth, RowTitleScale, FontWeight.Medium);
        Typography.Draw(new Vector2(row.Min.X, titleY), clippedTitle, theme.TextStrong, RowTitleScale, FontWeight.Medium);
        Typography.Draw(new Vector2(row.Min.X, titleY + 23f * scale), NewsFormat.Ago(item.Time), theme.TextMuted, MetaScale, FontWeight.Regular);
        DrawChevronRight(new Vector2(row.Max.X, row.Center.Y), 6f * scale, 2.2f * scale, hovered ? theme.TextStrong : theme.TextMuted);
    }

    private void DrawMaintenanceRow(Rect row, LodestoneNewsItem item, float scale, bool hovered)
    {
        var titleY = row.Min.Y + 10f * scale;
        var subY = titleY + 25f * scale;
        var status = NewsFormat.Status(item.Start, item.End);

        var pillInfo = status != MaintenanceStatus.None
            ? MeasurePill(StatusLabel(status), MetaScale, FontWeight.SemiBold, scale)
            : default;

        var rightPadding = 8f * scale;
        var pillReserved = pillInfo.hasPill ? pillInfo.width + 12f * scale + rightPadding : rightPadding + 4f * scale;
        var maxTitleWidth = row.Width - pillReserved;
        var clippedTitle = PixelEllipsize(item.Title, maxTitleWidth, RowTitleScale, FontWeight.Medium);

        Typography.Draw(new Vector2(row.Min.X, titleY), clippedTitle, theme.TextStrong, RowTitleScale, FontWeight.Medium);

        var sub = item.Start is { } start && item.End is { } end ? NewsFormat.Window(start, end) : NewsFormat.Ago(item.Time);
        var subWidth = Typography.Measure(sub, MetaScale, FontWeight.Regular).X;
        var maxSubWidth = row.Width - rightPadding;
        if (subWidth > maxSubWidth)
        {
            sub = PixelEllipsize(sub, maxSubWidth, MetaScale, FontWeight.Regular);
        }

        Typography.Draw(new Vector2(row.Min.X, subY), sub, theme.TextMuted, MetaScale, FontWeight.Regular);

        if (pillInfo.hasPill)
        {
            var drawList = ImGui.GetWindowDrawList();
            var pillRight = row.Max.X - rightPadding;
            DrawStatusPill(drawList, pillRight, titleY + 2f * scale, status, scale);
        }

        DrawChevronRight(new Vector2(row.Max.X, row.Max.Y - 14f * scale), 5f * scale, 2f * scale, hovered ? theme.TextStrong : Palette.WithAlpha(theme.TextMuted, 0.6f));
    }

    private (bool hasPill, float width, float height) MeasurePill(string label, float labelScale, FontWeight labelWeight, float scale)
    {
        if (string.IsNullOrEmpty(label))
        {
            return (false, 0f, 0f);
        }

        var labelSize = Typography.Measure(label, labelScale, labelWeight);
        var padX = 8f * scale;
        var padY = 3f * scale;
        return (true, labelSize.X + 2f * padX, labelSize.Y + 2f * padY);
    }

    private void DrawStatusPill(ImDrawListPtr drawList, float right, float top, MaintenanceStatus status, float scale)
    {
        var label = StatusLabel(status);
        var color = StatusColor(status);
        var size = MeasurePill(label, MetaScale, FontWeight.SemiBold, scale);
        if (!size.hasPill)
        {
            return;
        }

        var pillMin = new Vector2(right - size.width, top);
        var pillMax = new Vector2(right, top + size.height);
        var pillRounding = size.height * 0.5f;

        drawList.AddRectFilled(pillMin, pillMax, ImGui.GetColorU32(Palette.WithAlpha(color, 0.18f)), pillRounding);
        var labelCenterY = (pillMin.Y + pillMax.Y) * 0.5f;
        Typography.DrawCentered(drawList, new Vector2((pillMin.X + pillMax.X) * 0.5f, labelCenterY), label, color, MetaScale, FontWeight.SemiBold);
    }

    private void DrawRefreshControl(Vector2 center, NewsState state, float scale)
    {
        if (state == NewsState.Loading)
        {
            DrawSpinner(center, 9f * scale, theme.TextMuted);
            return;
        }

        var box = 14f * scale;
        var hovered = ImGui.IsMouseHoveringRect(center - new Vector2(box, box), center + new Vector2(box, box));
        var glyph = FontAwesomeIcon.Sync.ToIconString();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(center - size * 0.5f);
            using (ImRaii.PushColor(ImGuiCol.Text, hovered ? theme.TextStrong : theme.TextMuted))
            {
                ImGui.TextUnformatted(glyph);
            }
        }

        if (!hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            forceRefresh = true;
        }
    }

    private void DrawRegionRow(Vector2 leftCenter)
    {
        var label = RegionLabel(locale);
        var size = Typography.Measure(label, 0.78f, FontWeight.Medium);
        Typography.Draw(new Vector2(leftCenter.X, leftCenter.Y - size.Y * 0.5f), label, theme.TextMuted, 0.78f, FontWeight.Medium);
    }

    private bool DrawTextButton(Vector2 center, string label, Vector4 color, float scale)
    {
        var size = Typography.Measure(label, 0.9f, FontWeight.SemiBold);
        var hitMin = new Vector2(center.X - size.X * 0.5f - 12f * scale, center.Y - size.Y * 0.5f - 6f * scale);
        var hitMax = new Vector2(center.X + size.X * 0.5f + 12f * scale, center.Y + size.Y * 0.5f + 6f * scale);
        var hovered = ImGui.IsMouseHoveringRect(hitMin, hitMax);

        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(hitMin, hitMax, ImGui.GetColorU32(Palette.WithAlpha(color, hovered ? 0.22f : 0.14f)), (hitMax.Y - hitMin.Y) * 0.5f);
        Typography.DrawCentered(center, label, color, 0.9f, FontWeight.SemiBold);

        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void InteractCard(Rect rect, float rounding, string url, ImDrawListPtr drawList)
    {
        if (!ImGui.IsMouseHoveringRect(rect.Min, rect.Max))
        {
            return;
        }

        var pressed = ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var wash = pressed ? new Vector4(0f, 0f, 0f, 0.08f) : new Vector4(1f, 1f, 1f, 0.05f);
        Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(wash));

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (!string.IsNullOrEmpty(url) && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            UrlActions.OpenInBrowser(url);
        }
    }

    private static void DrawSpinner(Vector2 center, float radius, Vector4 color) =>
        ProgressRing.Sweep(center, radius, 2.4f * ImGuiHelpers.GlobalScale, color, 900.0, 1.8f, 0.95f);

    private static void DrawChevronRight(Vector2 tip, float size, float thickness, Vector4 color)
    {
        var drawList = ImGui.GetWindowDrawList();
        var packed = ImGui.GetColorU32(color);
        drawList.AddLine(new Vector2(tip.X - size, tip.Y - size), tip, packed, thickness);
        drawList.AddLine(tip, new Vector2(tip.X - size, tip.Y + size), packed, thickness);
    }

    private float StepFade(string url, bool ready)
    {
        imageFade.TryGetValue(url, out var fade);
        var target = ready ? 1f : 0f;
        if (fade < target)
        {
            fade = Math.Min(target, fade + ImGui.GetIO().DeltaTime / ImageFadeSeconds);
        }

        imageFade[url] = fade;
        return fade;
    }

    private void FillCategoryLabels()
    {
        for (var index = 0; index < NewsCategories.All.Length; index++)
        {
            categoryLabels[index] = CategoryLabel(NewsCategories.All[index]);
        }
    }

    private MediaResult Thumb(string url) => media.GetOrRequest(url, token => http.GetBytesAsync(new Uri(url), token));


    private void WrapInto(List<string> output, string text, float maxWidth, float scale, FontWeight weight, int maxLines)
    {
        output.Clear();
        if (maxWidth <= 0f || maxLines <= 0)
        {
            return;
        }

        var words = Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return;
        }

        using (Plugin.Fonts.Push(scale, weight))
        {
            var current = string.Empty;
            for (var wordIndex = 0; wordIndex < words.Length; wordIndex++)
            {
                var word = words[wordIndex];
                var candidate = current.Length == 0 ? word : string.Concat(current, " ", word);
                if (current.Length == 0 || ImGui.CalcTextSize(candidate).X <= maxWidth)
                {
                    current = candidate;
                    continue;
                }

                if (output.Count == maxLines - 1)
                {
                    output.Add(Ellipsize(current, maxWidth));
                    return;
                }

                output.Add(current);
                current = word;
            }

            if (current.Length > 0)
            {
                output.Add(current);
            }
        }
    }

    private static string Ellipsize(string line, float maxWidth)
    {
        var trimmed = line;
        while (trimmed.Length > 1 && ImGui.CalcTextSize(string.Concat(trimmed, "…")).X > maxWidth)
        {
            trimmed = trimmed.Substring(0, trimmed.Length - 1).TrimEnd();
        }

        return string.Concat(trimmed, "…");
    }

    private string PixelEllipsize(string text, float maxWidth, float scale, FontWeight weight)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        using (Plugin.Fonts.Push(scale, weight))
        {
            var size = ImGui.CalcTextSize(text);
            if (size.X <= maxWidth)
            {
                return text;
            }

            var trimmed = text;
            while (trimmed.Length > 1 && ImGui.CalcTextSize(string.Concat(trimmed, "…")).X > maxWidth)
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 1).TrimEnd();
            }

            return string.Concat(trimmed, "…");
        }
    }

    private static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text.Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ');
    }

    private static string CategoryLabel(NewsCategory category) => category switch
    {
        NewsCategory.Notices => Loc.T(L.News.Notices),
        NewsCategory.Maintenance => Loc.T(L.News.Maintenance),
        NewsCategory.Updates => Loc.T(L.News.Updates),
        _ => Loc.T(L.News.Topics),
    };

    private static string StatusLabel(MaintenanceStatus status) => status switch
    {
        MaintenanceStatus.Upcoming => Loc.T(L.News.Upcoming),
        MaintenanceStatus.Active => Loc.T(L.News.Active),
        _ => Loc.T(L.News.Ended),
    };

    private Vector4 StatusColor(MaintenanceStatus status) => status switch
    {
        MaintenanceStatus.Upcoming => StatusUpcoming,
        MaintenanceStatus.Active => StatusActive,
        _ => theme.TextMuted,
    };

    private static string RegionLabel(string locale) => locale switch
    {
        "jp" => Loc.T(L.News.RegionJapan),
        "fr" => Loc.T(L.News.RegionFrance),
        "de" => Loc.T(L.News.RegionGermany),
        "eu" => Loc.T(L.News.RegionEurope),
        _ => Loc.T(L.News.RegionNorthAmerica),
    };

    public void Dispose()
    {
    }
}
