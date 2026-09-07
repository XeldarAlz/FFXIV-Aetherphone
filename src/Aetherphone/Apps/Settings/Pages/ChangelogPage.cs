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
    private const float SeparatorGap = 13f;
    private const float HeaderGap = 4f;
    private readonly List<string> wrappedLines = new();
    private readonly List<int> highlightLineCounts = new();
    private readonly List<string> sectionTitles = new();
    private readonly List<int> sectionHighlightCounts = new();
    private readonly List<EntryLayout> entryLayouts = new();
    private readonly Configuration configuration;
    private BodyMetrics metrics;
    private LanguageInfo? layoutLanguage;
    private float layoutWidth = -1f;
    private float layoutScale;
    private int layoutFontGeneration = -1;
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
            var width = ImGui.GetContentRegionAvail().X;
            EnsureLayout(width, scale);
            var gap = CardGap * scale;
            for (var index = 0; index < entryLayouts.Count; index++)
            {
                var layout = entryLayouts[index];
                var size = new Vector2(width, layout.CardHeight);
                if (ImGui.IsRectVisible(size))
                {
                    DrawCard(theme, scale, layout, width, index == 0);
                }

                ImGui.Dummy(size);
                ImGui.Dummy(new Vector2(0f, gap));
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

    private void EnsureLayout(float width, float scale)
    {
        var fontGeneration = Plugin.Fonts.Generation;
        if (entryLayouts.Count == ChangelogData.Entries.Count
            && MathF.Abs(width - layoutWidth) < 0.5f
            && scale == layoutScale
            && fontGeneration == layoutFontGeneration
            && ReferenceEquals(layoutLanguage, Loc.Current))
        {
            return;
        }

        BuildLayout(width, scale, fontGeneration);
    }

    private void BuildLayout(float width, float scale, int fontGeneration)
    {
        wrappedLines.Clear();
        highlightLineCounts.Clear();
        sectionTitles.Clear();
        sectionHighlightCounts.Clear();
        entryLayouts.Clear();
        metrics = new BodyMetrics(
            Typography.Measure("Ag", TextStyles.Callout).Y,
            Typography.Measure("Ag", TextStyles.Title3).Y,
            8f * scale,
            16f * scale,
            6f * scale);
        var textWidth = width - CardPaddingX * scale * 2f - BulletColumn * scale;
        for (var index = 0; index < ChangelogData.Entries.Count; index++)
        {
            entryLayouts.Add(BuildEntry(ChangelogData.Entries[index], textWidth, scale));
        }

        layoutWidth = width;
        layoutScale = scale;
        layoutFontGeneration = fontGeneration;
        layoutLanguage = Loc.Current;
    }

    private EntryLayout BuildEntry(in ChangelogEntry entry, float textWidth, float scale)
    {
        var firstSection = sectionTitles.Count;
        var firstHighlight = highlightLineCounts.Count;
        var firstLine = wrappedLines.Count;
        if (entry.Sections.Count == 0)
        {
            sectionTitles.Add(string.Empty);
            sectionHighlightCounts.Add(entry.Highlights.Count);
            WrapHighlights(entry.Highlights, textWidth);
        }
        else
        {
            for (var index = 0; index < entry.Sections.Count; index++)
            {
                var section = entry.Sections[index];
                sectionTitles.Add(Loc.T(section.Title));
                sectionHighlightCounts.Add(section.Highlights.Count);
                WrapHighlights(section.Highlights, textWidth);
            }
        }

        var sectionCount = sectionTitles.Count - firstSection;
        var versionLabel = string.Concat(Loc.T(L.Settings.Version), " ", entry.Version);
        var dateLabel = FormatDate(entry.Date);
        var versionHeight = Typography.Measure(versionLabel, TextStyles.Title3).Y;
        var dateHeight = Typography.Measure(dateLabel, TextStyles.Footnote).Y;
        var bodyHeight = MeasureBody(firstSection, sectionCount, firstHighlight);
        var headerHeight = versionHeight + HeaderGap * scale + dateHeight;
        var cardHeight = CardPaddingY * scale * 2f + headerHeight + SeparatorGap * scale * 2f + 1f + bodyHeight;
        return new EntryLayout(firstSection, sectionCount, firstHighlight, firstLine, versionLabel, dateLabel,
            versionHeight, dateHeight, cardHeight);
    }

    private void DrawCard(PhoneTheme theme, float scale, in EntryLayout layout, float width, bool isLatest)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var paddingX = CardPaddingX * scale;
        var innerLeft = origin.X + paddingX;
        var right = origin.X + width;
        var innerRight = right - paddingX;
        var textLeft = innerLeft + BulletColumn * scale;
        var max = new Vector2(right, origin.Y + layout.CardHeight);
        var rounding = CardRounding * scale;
        Elevation.Card(drawList, origin, max, rounding, scale);
        Squircle.Fill(drawList, origin, max, rounding, ImGui.GetColorU32(theme.GroupedCard));
        Material.TopGlow(drawList, origin, max, rounding, theme.Accent, 0.5f, isLatest ? 0.14f : 0.09f);
        Material.EdgeSquircle(drawList, origin, max, rounding, scale);
        var versionTop = origin.Y + CardPaddingY * scale;
        Typography.Draw(drawList, new Vector2(innerLeft, versionTop), layout.VersionLabel, theme.Accent,
            TextStyles.Title3.Scale, TextStyles.Title3.Weight);
        if (isLatest)
        {
            DrawLatestPill(drawList, theme, scale,
                innerLeft + Typography.Measure(layout.VersionLabel, TextStyles.Title3).X + 8f * scale, versionTop,
                layout.VersionHeight);
        }

        var dateTop = versionTop + layout.VersionHeight + HeaderGap * scale;
        Typography.Draw(drawList, new Vector2(innerLeft, dateTop), layout.DateLabel, theme.TextMuted,
            TextStyles.Footnote.Scale, TextStyles.Footnote.Weight);
        var separatorY = dateTop + layout.DateHeight + SeparatorGap * scale;
        drawList.AddLine(new Vector2(innerLeft, separatorY), new Vector2(innerRight, separatorY),
            ImGui.GetColorU32(theme.Separator), 1f);
        DrawBody(drawList, theme, scale, layout, innerLeft, textLeft, separatorY + SeparatorGap * scale);
        ImGui.SetCursorScreenPos(origin);
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

    private float MeasureBody(int firstSection, int sectionCount, int firstHighlight)
    {
        var height = 0f;
        var highlightCursor = firstHighlight;
        for (var offset = 0; offset < sectionCount; offset++)
        {
            if (offset > 0)
            {
                height += metrics.SectionGap;
            }

            if (sectionTitles[firstSection + offset].Length > 0)
            {
                height += metrics.TitleHeight + metrics.TitleGap;
            }

            var highlightCount = sectionHighlightCounts[firstSection + offset];
            for (var bulletIndex = 0; bulletIndex < highlightCount; bulletIndex++)
            {
                if (bulletIndex > 0)
                {
                    height += metrics.BulletGap;
                }

                height += highlightLineCounts[highlightCursor] * metrics.LineHeight;
                highlightCursor++;
            }
        }

        return height;
    }

    private void DrawBody(ImDrawListPtr drawList, PhoneTheme theme, float scale, in EntryLayout layout,
        float innerLeft, float textLeft, float top)
    {
        var y = top;
        var highlightCursor = layout.FirstHighlight;
        var lineCursor = layout.FirstLine;
        for (var offset = 0; offset < layout.SectionCount; offset++)
        {
            if (offset > 0)
            {
                y += metrics.SectionGap;
            }

            var title = sectionTitles[layout.FirstSection + offset];
            if (title.Length > 0)
            {
                Typography.Draw(drawList, new Vector2(innerLeft, y), title, theme.TextStrong,
                    TextStyles.Title3.Scale, TextStyles.Title3.Weight);
                y += metrics.TitleHeight + metrics.TitleGap;
            }

            var highlightCount = sectionHighlightCounts[layout.FirstSection + offset];
            for (var bulletIndex = 0; bulletIndex < highlightCount; bulletIndex++)
            {
                if (bulletIndex > 0)
                {
                    y += metrics.BulletGap;
                }

                var bulletCenter = new Vector2(innerLeft + 3f * scale, y + metrics.LineHeight * 0.5f);
                drawList.AddCircleFilled(bulletCenter, 2.5f * scale, ImGui.GetColorU32(theme.Accent));
                var lineCount = highlightLineCounts[highlightCursor];
                for (var lineIndex = 0; lineIndex < lineCount; lineIndex++)
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

    private readonly struct EntryLayout
    {
        public readonly int FirstSection;
        public readonly int SectionCount;
        public readonly int FirstHighlight;
        public readonly int FirstLine;
        public readonly string VersionLabel;
        public readonly string DateLabel;
        public readonly float VersionHeight;
        public readonly float DateHeight;
        public readonly float CardHeight;

        public EntryLayout(int firstSection, int sectionCount, int firstHighlight, int firstLine, string versionLabel,
            string dateLabel, float versionHeight, float dateHeight, float cardHeight)
        {
            FirstSection = firstSection;
            SectionCount = sectionCount;
            FirstHighlight = firstHighlight;
            FirstLine = firstLine;
            VersionLabel = versionLabel;
            DateLabel = dateLabel;
            VersionHeight = versionHeight;
            DateHeight = dateHeight;
            CardHeight = cardHeight;
        }
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
