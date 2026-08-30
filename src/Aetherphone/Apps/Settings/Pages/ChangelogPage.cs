using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Changelog;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class ChangelogPage : ISettingsPage
{
    private const float CardRounding = 22f;
    private const float CardGap = 14f;
    private const float CardPaddingX = 18f;
    private const float CardPaddingY = 16f;
    private const float BulletColumn = 20f;
    private readonly List<string> wrappedLines = new();
    private readonly List<int> highlightLineCounts = new();
    private readonly List<string> sectionTitles = new();
    private readonly List<int> sectionHighlightCounts = new();
    private readonly Configuration configuration;
    public string Title => Loc.T(L.Settings.Changelog);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.Gift;
    public Vector4 Tint => new(0.62f, 0.42f, 0.90f, 1f);
    public bool ShowsBadge => configuration.HasUnseenChangelog;

    public ChangelogPage(Configuration configuration)
    {
        this.configuration = configuration;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = UiScale.Current;
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            DrawHero(theme, scale);
            for (var index = 0; index < ChangelogData.Entries.Count; index++)
            {
                DrawCard(theme, scale, ChangelogData.Entries[index], index == 0);
                ImGui.Dummy(new Vector2(0f, CardGap * scale));
            }
        }
    }

    private static void DrawHero(PhoneTheme theme, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var heroTop = origin.Y + 6f * scale;
        Typography.Draw(drawList, new Vector2(origin.X, heroTop), Loc.T(L.Settings.ChangelogHero), theme.TextStrong,
            TextStyles.LargeTitle.Scale, TextStyles.LargeTitle.Weight);
        var heroHeight = Typography.Measure(Loc.T(L.Settings.ChangelogHero), TextStyles.LargeTitle).Y;
        var subtitleTop = heroTop + heroHeight + 2f * scale;
        Typography.Draw(drawList, new Vector2(origin.X, subtitleTop), AepConstants.Name, theme.TextMuted,
            TextStyles.Subheadline.Scale, TextStyles.Subheadline.Weight);
        var subtitleHeight = Typography.Measure(AepConstants.Name, TextStyles.Subheadline).Y;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, subtitleTop - origin.Y + subtitleHeight + 16f * scale));
    }

    private void DrawCard(PhoneTheme theme, float scale, in ChangelogEntry entry, bool isLatest)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var left = origin.X;
        var right = left + width;
        var paddingX = CardPaddingX * scale;
        var paddingY = CardPaddingY * scale;
        var innerLeft = left + paddingX;
        var innerRight = right - paddingX;
        var textLeft = innerLeft + BulletColumn * scale;
        var textWidth = innerRight - textLeft;
        var versionLabel = string.Concat(Loc.T(L.Settings.Version), " ", entry.Version);
        var versionHeight = Typography.Measure(versionLabel, TextStyles.Title3).Y;
        var dateLabel = FormatDate(entry.Date);
        var dateHeight = Typography.Measure(dateLabel, TextStyles.Footnote).Y;
        var metrics = new BodyMetrics(
            Typography.Measure("Ag", TextStyles.Callout).Y,
            Typography.Measure("Ag", TextStyles.Headline).Y,
            8f * scale,
            16f * scale,
            6f * scale);
        WrapEntry(entry, textWidth);
        var highlightsHeight = MeasureBody(metrics);
        var headerHeight = versionHeight + 4f * scale + dateHeight;
        var separatorGap = 13f * scale;
        var cardHeight = paddingY + headerHeight + separatorGap + 1f + separatorGap + highlightsHeight + paddingY;
        var min = origin;
        var max = new Vector2(right, origin.Y + cardHeight);
        var rounding = CardRounding * scale;
        Elevation.Card(drawList, min, max, rounding, scale);
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(theme.GroupedCard));
        Material.TopGlow(drawList, min, max, rounding, theme.Accent, 0.5f, isLatest ? 0.14f : 0.09f);
        Material.EdgeSquircle(drawList, min, max, rounding, scale);
        var versionTop = min.Y + paddingY;
        Typography.Draw(drawList, new Vector2(innerLeft, versionTop), versionLabel, theme.Accent,
            TextStyles.Title3.Scale, TextStyles.Title3.Weight);
        if (isLatest)
        {
            DrawLatestPill(drawList, theme, scale,
                innerLeft + Typography.Measure(versionLabel, TextStyles.Title3).X + 8f * scale, versionTop,
                versionHeight);
        }

        var dateTop = versionTop + versionHeight + 4f * scale;
        Typography.Draw(drawList, new Vector2(innerLeft, dateTop), dateLabel, theme.TextMuted,
            TextStyles.Footnote.Scale, TextStyles.Footnote.Weight);
        var separatorY = dateTop + dateHeight + separatorGap;
        drawList.AddLine(new Vector2(innerLeft, separatorY), new Vector2(innerRight, separatorY),
            ImGui.GetColorU32(theme.Separator), 1f);
        DrawBody(drawList, theme, scale, metrics, innerLeft, textLeft, separatorY + separatorGap);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight));
    }

    private static void DrawLatestPill(ImDrawListPtr drawList, PhoneTheme theme, float scale, float x, float rowTop,
        float rowHeight)
    {
        var label = Loc.Culture.TextInfo.ToUpper(Loc.T(L.Settings.ChangelogLatest));
        var textSize = Typography.Measure(label, TextStyles.Caption2);
        var padX = 7f * scale;
        var padY = 3f * scale;
        var pillMin = new Vector2(x, rowTop + (rowHeight - textSize.Y - padY * 2f) * 0.5f);
        var pillMax = new Vector2(x + textSize.X + padX * 2f, pillMin.Y + textSize.Y + padY * 2f);
        Squircle.Fill(drawList, pillMin, pillMax, (pillMax.Y - pillMin.Y) * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, 0.18f)));
        Typography.Draw(drawList, new Vector2(pillMin.X + padX, pillMin.Y + padY), label, theme.Accent,
            TextStyles.Caption2.Scale, TextStyles.Caption2.Weight);
    }

    private float MeasureBody(in BodyMetrics metrics)
    {
        var height = 0f;
        var highlightCursor = 0;
        for (var sectionIndex = 0; sectionIndex < sectionTitles.Count; sectionIndex++)
        {
            if (sectionIndex > 0)
            {
                height += metrics.SectionGap;
            }

            if (sectionTitles[sectionIndex].Length > 0)
            {
                height += metrics.TitleHeight + metrics.TitleGap;
            }

            var highlightCount = sectionHighlightCounts[sectionIndex];
            for (var offset = 0; offset < highlightCount; offset++)
            {
                if (offset > 0)
                {
                    height += metrics.BulletGap;
                }

                height += highlightLineCounts[highlightCursor] * metrics.LineHeight;
                highlightCursor++;
            }
        }

        return height;
    }

    private void DrawBody(ImDrawListPtr drawList, PhoneTheme theme, float scale, in BodyMetrics metrics,
        float innerLeft, float textLeft, float top)
    {
        var y = top;
        var highlightCursor = 0;
        var lineCursor = 0;
        for (var sectionIndex = 0; sectionIndex < sectionTitles.Count; sectionIndex++)
        {
            if (sectionIndex > 0)
            {
                y += metrics.SectionGap;
            }

            var title = sectionTitles[sectionIndex];
            if (title.Length > 0)
            {
                Typography.Draw(drawList, new Vector2(innerLeft, y), title, theme.TextStrong,
                    TextStyles.Headline.Scale, TextStyles.Headline.Weight);
                y += metrics.TitleHeight + metrics.TitleGap;
            }

            var highlightCount = sectionHighlightCounts[sectionIndex];
            for (var offset = 0; offset < highlightCount; offset++)
            {
                if (offset > 0)
                {
                    y += metrics.BulletGap;
                }

                var bulletCenter = new Vector2(innerLeft + 3f * scale, y + metrics.LineHeight * 0.5f);
                drawList.AddCircleFilled(bulletCenter, 2.5f * scale, ImGui.GetColorU32(theme.Accent));
                var lineCount = highlightLineCounts[highlightCursor];
                for (var line = 0; line < lineCount; line++)
                {
                    Typography.Draw(drawList, new Vector2(textLeft, y), wrappedLines[lineCursor], theme.TextStrong,
                        TextStyles.Callout.Scale, TextStyles.Callout.Weight);
                    lineCursor++;
                    y += metrics.LineHeight;
                }

                highlightCursor++;
            }
        }
    }

    private void WrapEntry(in ChangelogEntry entry, float maxWidth)
    {
        wrappedLines.Clear();
        highlightLineCounts.Clear();
        sectionTitles.Clear();
        sectionHighlightCounts.Clear();
        if (entry.Sections.Count == 0)
        {
            sectionTitles.Add(string.Empty);
            sectionHighlightCounts.Add(entry.Highlights.Count);
            WrapHighlights(entry.Highlights, maxWidth);
            return;
        }

        for (var index = 0; index < entry.Sections.Count; index++)
        {
            var section = entry.Sections[index];
            sectionTitles.Add(Loc.T(section.Title));
            sectionHighlightCounts.Add(section.Highlights.Count);
            WrapHighlights(section.Highlights, maxWidth);
        }
    }

    private void WrapHighlights(IReadOnlyList<LocString> highlights, float maxWidth)
    {
        for (var index = 0; index < highlights.Count; index++)
        {
            var before = wrappedLines.Count;
            WrapLine(Loc.T(highlights[index]), maxWidth);
            highlightLineCounts.Add(wrappedLines.Count - before);
        }
    }

    private void WrapLine(string text, float maxWidth)
    {
        var lines = Typography.WrapText(text, TextStyles.Callout, maxWidth);
        for (var index = 0; index < lines.Length; index++)
        {
            wrappedLines.Add(lines[index]);
        }
    }

    private static string FormatDate(string isoDate)
    {
        if (DateTime.TryParse(isoDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed.ToString("d MMM yyyy", Loc.Culture);
        }

        return isoDate;
    }

    private readonly struct BodyMetrics
    {
        public readonly float LineHeight;
        public readonly float TitleHeight;
        public readonly float BulletGap;
        public readonly float SectionGap;
        public readonly float TitleGap;

        public BodyMetrics(float lineHeight, float titleHeight, float bulletGap, float sectionGap, float titleGap)
        {
            LineHeight = lineHeight;
            TitleHeight = titleHeight;
            BulletGap = bulletGap;
            SectionGap = sectionGap;
            TitleGap = titleGap;
        }
    }
}
