using System.Globalization;
using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Changelog;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

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

    public string Title => Loc.T(L.Settings.Changelog);

    public string Summary => Loc.T(L.Settings.ChangelogSummary);

    public string Glyph => "C";

    public Vector4 Tint => new(0.62f, 0.42f, 0.90f, 1f);

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = ImGuiHelpers.GlobalScale;
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
        Typography.Draw(drawList, new Vector2(origin.X, heroTop), Loc.T(L.Settings.ChangelogHero), theme.TextStrong, TextStyles.LargeTitle.Scale, TextStyles.LargeTitle.Weight);
        var heroHeight = Typography.Measure(Loc.T(L.Settings.ChangelogHero), TextStyles.LargeTitle).Y;

        var subtitleTop = heroTop + heroHeight + 2f * scale;
        Typography.Draw(drawList, new Vector2(origin.X, subtitleTop), AepConstants.Name, theme.TextMuted, TextStyles.Subheadline.Scale, TextStyles.Subheadline.Weight);
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
        var lineHeight = Typography.Measure("Ag", TextStyles.Callout).Y;
        var bulletGap = 8f * scale;

        WrapHighlights(entry.Highlights, textWidth);

        var highlightsHeight = 0f;
        for (var index = 0; index < highlightLineCounts.Count; index++)
        {
            highlightsHeight += highlightLineCounts[index] * lineHeight;
            if (index > 0)
            {
                highlightsHeight += bulletGap;
            }
        }

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
        Typography.Draw(drawList, new Vector2(innerLeft, versionTop), versionLabel, theme.Accent, TextStyles.Title3.Scale, TextStyles.Title3.Weight);

        if (isLatest)
        {
            DrawLatestPill(drawList, theme, scale, innerLeft + Typography.Measure(versionLabel, TextStyles.Title3).X + 8f * scale, versionTop, versionHeight);
        }

        var dateTop = versionTop + versionHeight + 4f * scale;
        Typography.Draw(drawList, new Vector2(innerLeft, dateTop), dateLabel, theme.TextMuted, TextStyles.Footnote.Scale, TextStyles.Footnote.Weight);

        var separatorY = dateTop + dateHeight + separatorGap;
        drawList.AddLine(new Vector2(innerLeft, separatorY), new Vector2(innerRight, separatorY), ImGui.GetColorU32(theme.Separator), 1f);

        var lineCursor = 0;
        var y = separatorY + separatorGap;
        for (var highlightIndex = 0; highlightIndex < highlightLineCounts.Count; highlightIndex++)
        {
            var bulletCenter = new Vector2(innerLeft + 3f * scale, y + lineHeight * 0.5f);
            drawList.AddCircleFilled(bulletCenter, 2.5f * scale, ImGui.GetColorU32(theme.Accent));

            var lineCount = highlightLineCounts[highlightIndex];
            for (var line = 0; line < lineCount; line++)
            {
                Typography.Draw(drawList, new Vector2(textLeft, y), wrappedLines[lineCursor], theme.TextStrong, TextStyles.Callout.Scale, TextStyles.Callout.Weight);
                lineCursor++;
                y += lineHeight;
            }

            y += bulletGap;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight));
    }

    private static void DrawLatestPill(ImDrawListPtr drawList, PhoneTheme theme, float scale, float x, float rowTop, float rowHeight)
    {
        var label = Loc.T(L.Settings.ChangelogLatest).ToUpperInvariant();
        var textSize = Typography.Measure(label, TextStyles.Caption2);
        var padX = 7f * scale;
        var padY = 3f * scale;
        var pillMin = new Vector2(x, rowTop + (rowHeight - textSize.Y - padY * 2f) * 0.5f);
        var pillMax = new Vector2(x + textSize.X + padX * 2f, pillMin.Y + textSize.Y + padY * 2f);

        Squircle.Fill(drawList, pillMin, pillMax, (pillMax.Y - pillMin.Y) * 0.5f, ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, 0.18f)));
        Typography.Draw(drawList, new Vector2(pillMin.X + padX, pillMin.Y + padY), label, theme.Accent, TextStyles.Caption2.Scale, TextStyles.Caption2.Weight);
    }

    private void WrapHighlights(IReadOnlyList<LocString> highlights, float maxWidth)
    {
        wrappedLines.Clear();
        highlightLineCounts.Clear();

        for (var index = 0; index < highlights.Count; index++)
        {
            var before = wrappedLines.Count;
            WrapLine(Loc.T(highlights[index]), maxWidth);
            highlightLineCounts.Add(wrappedLines.Count - before);
        }
    }

    private void WrapLine(string text, float maxWidth)
    {
        var words = text.Split(' ');
        var current = string.Empty;
        for (var index = 0; index < words.Length; index++)
        {
            var candidate = current.Length == 0 ? words[index] : string.Concat(current, " ", words[index]);
            if (current.Length > 0 && Typography.Measure(candidate, TextStyles.Callout).X > maxWidth)
            {
                wrappedLines.Add(current);
                current = words[index];
                continue;
            }

            current = candidate;
        }

        wrappedLines.Add(current);
    }

    private static string FormatDate(string isoDate)
    {
        if (DateTime.TryParse(isoDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed.ToString("d MMM yyyy", Loc.Culture);
        }

        return isoDate;
    }
}
