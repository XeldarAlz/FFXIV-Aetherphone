using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Hunts;

internal sealed partial class HuntsApp
{
    private static readonly (LocString Title, LocString Body, bool IsGlossary, string Glyph)[] GuideTopics =
    {
        (L.Hunts.GuideAffiliationTitle, L.Hunts.GuideAffiliationBody, false, PhoneIcons.InfoCircle),
        (L.Hunts.GuideUnlockTitle, L.Hunts.GuideUnlockBody, false, PhoneIcons.LockOpen),
        (L.Hunts.GuideHowItWorksTitle, L.Hunts.GuideHowItWorksBody, false, PhoneIcons.Clock),
        (L.Hunts.GuideRanksTitle, L.Hunts.GuideRanksBody, false, PhoneIcons.Crown),
        (L.Hunts.GuideWindowStatusTitle, L.Hunts.GuideWindowStatusBody, false, PhoneIcons.Circle),
        (L.Hunts.GuideSSMarksTitle, L.Hunts.GuideSSMarksBody, false, PhoneIcons.Flame),
        (L.Hunts.GuideContributeTitle, L.Hunts.GuideContributeBody, false, PhoneIcons.Users),
        (L.Hunts.GuideScheduledTitle, L.Hunts.GuideScheduledBody, false, PhoneIcons.Calendar),
        (L.Hunts.GuideMaintenanceTitle, L.Hunts.GuideMaintenanceBody, false, PhoneIcons.Refresh),
        (L.Hunts.GuideSpawnConditionsTitle, L.Hunts.GuideSpawnConditionsBody, false, PhoneIcons.Sparkles),
        (L.Hunts.GuideGlossaryTitle, L.Hunts.GuideGlossaryBody, true, PhoneIcons.FileText),
    };

    private const float GuideChevronReserve = 20f;
    private const float GuideHeaderBottomPad = 8f;
    private const float GuideGlossaryTermGap = 2f;
    private const float GuideGlossaryEntryGap = 8f;
    private const float GuideTileSize = 30f;

    private readonly bool[] guideTopicExpanded = new bool[GuideTopics.Length];
    private (string Term, string Definition)[] glossaryCache = Array.Empty<(string, string)>();
    private string glossaryCacheSource = string.Empty;

    private void DrawGuide(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (ImRaii.PushId("hunts.guide"))
        using (AppSurface.Begin(navBar.Body))
        {
            var width = ImGui.GetContentRegionAvail().X;
            for (var index = 0; index < GuideTopics.Length; index++)
            {
                var origin = ImGui.GetCursorScreenPos();
                var topic = GuideTopics[index];
                var height = DrawGuideTopic(origin, width, topic.Title, topic.Body, topic.IsGlossary, topic.Glyph,
                    ref guideTopicExpanded[index], scale);
                ImGui.SetCursorScreenPos(origin);
                ImGui.Dummy(new Vector2(width, height + HuntsArt.TileGap * scale));
            }

            BottomSpacer(scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, "hunts.guide.nav", Loc.T(L.Hunts.GuideTab), NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty);
    }

    private float DrawGuideTopic(Vector2 origin, float width, LocString title, LocString body, bool isGlossary,
        string glyph, ref bool expanded, float scale)
    {
        var inset = HuntsArt.CardPadding * scale;
        var tile = GuideTileSize * scale;
        var bodyWidth = width - inset * 2f;
        var titleLeft = origin.X + inset + tile + HuntsArt.RowGap * scale;
        var titleWidth = origin.X + width - inset - GuideChevronReserve * scale - titleLeft;
        var drawList = ImGui.GetWindowDrawList();

        var titleStyle = TextStyles.BodyEmphasized;
        var titleText = Loc.T(title);
        var titleHeight = Typography.MeasureWrappedBlock(titleText, titleStyle, titleWidth).Y;

        var bodyText = expanded ? Loc.T(body) : string.Empty;
        RichTextLayout? bodyLayout = null;
        if (expanded && !isGlossary)
        {
            using (Plugin.Fonts.Push(TextStyles.Footnote.Scale, TextStyles.Footnote.Weight))
            {
                bodyLayout = LinkText.LayoutFor(bodyText, bodyWidth);
            }
        }

        var bodyGap = expanded ? 6f * scale : 0f;
        var bodyHeight = 0f;
        if (expanded)
        {
            bodyHeight = isGlossary
                ? MeasureGlossaryHeight(bodyText, bodyWidth, scale)
                : bodyLayout?.Size.Y ?? Typography.MeasureWrappedBlock(bodyText, TextStyles.Footnote, bodyWidth).Y;
        }

        var headerHeight = MathF.Max(titleHeight, tile);
        var height = inset + headerHeight + bodyGap + bodyHeight + inset;

        var titleTop = origin.Y + inset;
        var headerMax = new Vector2(origin.X + width, titleTop + headerHeight + GuideHeaderBottomPad * scale);
        var headerHovered = UiInteract.Hover(origin, headerMax);

        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, origin, max, HuntsArt.CardRadius * scale);
        if (headerHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var tileMin = new Vector2(origin.X + inset, titleTop + (headerHeight - tile) * 0.5f);
        IconTile.FillShaded(drawList, tileMin, tileMin + new Vector2(tile, tile), tile * Metrics.Radius.TileFactor,
            IconTile.Surface(ui.Accent));
        PhoneIcon.Draw(drawList, tileMin + new Vector2(tile, tile) * 0.5f, glyph, AccentRing.Ink, tile * 0.55f);
        Typography.DrawWrappedLeft(new Vector2(titleLeft, titleTop + (headerHeight - titleHeight) * 0.5f), titleText,
            ui.TitleInk, titleStyle, titleWidth);

        var chevron = expanded ? FontAwesomeIcon.ChevronUp : FontAwesomeIcon.ChevronDown;
        var chevronCenter = new Vector2(max.X - inset - 6f * scale,
            titleTop + headerHeight * 0.5f);
        AppSkin.Icon(drawList, chevronCenter, IconGlyph.Of(chevron), ui.MutedInk, 0.55f);

        if (expanded)
        {
            var bodyTop = titleTop + headerHeight + bodyGap;
            var bodyOrigin = new Vector2(origin.X + inset, bodyTop);
            if (isGlossary)
            {
                DrawGlossary(drawList, bodyOrigin, bodyText, bodyWidth, scale);
            }
            else if (bodyLayout is null)
            {
                Typography.DrawWrappedLeft(bodyOrigin, bodyText, ui.MutedInk, TextStyles.Footnote, bodyWidth);
            }
            else
            {
                using (Plugin.Fonts.Push(TextStyles.Footnote.Scale, TextStyles.Footnote.Weight))
                {
                    LinkText.Draw(drawList, bodyLayout, bodyOrigin, 1f, ui.MutedInk, ui.Accent, 1f, true);
                }
            }
        }

        if (UiInteract.Click(origin, headerMax, headerHovered))
        {
            expanded = !expanded;
            UiFeedback.Play(UiSound.Tap);
        }

        return height;
    }

    private float MeasureGlossaryHeight(string bodyText, float bodyWidth, float scale)
    {
        var entries = ResolveGlossaryEntries(bodyText);
        var termLine = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var height = 0f;
        for (var index = 0; index < entries.Length; index++)
        {
            var definitionHeight = Typography.MeasureWrappedBlock(entries[index].Definition, TextStyles.Footnote,
                bodyWidth).Y;
            height += termLine + GuideGlossaryTermGap * scale + definitionHeight;
            if (index < entries.Length - 1)
            {
                height += GuideGlossaryEntryGap * scale;
            }
        }

        return height;
    }

    private void DrawGlossary(ImDrawListPtr drawList, Vector2 origin, string bodyText, float bodyWidth, float scale)
    {
        var entries = ResolveGlossaryEntries(bodyText);
        var termStyle = TextStyles.FootnoteEmphasized;
        var termLine = Typography.LineHeight(termStyle);
        var cursorY = origin.Y;
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            Typography.Draw(drawList, new Vector2(origin.X, cursorY), entry.Term, ui.TitleInk, termStyle);
            cursorY += termLine + GuideGlossaryTermGap * scale;
            cursorY += Typography.DrawWrappedLeft(new Vector2(origin.X, cursorY), entry.Definition, ui.MutedInk,
                TextStyles.Footnote, bodyWidth);
            if (index < entries.Length - 1)
            {
                cursorY += GuideGlossaryEntryGap * scale;
            }
        }
    }

    private (string Term, string Definition)[] ResolveGlossaryEntries(string bodyText)
    {
        if (string.Equals(glossaryCacheSource, bodyText, StringComparison.Ordinal))
        {
            return glossaryCache;
        }

        var lines = bodyText.Split('\n');
        var entries = new (string Term, string Definition)[lines.Length];
        for (var index = 0; index < lines.Length; index++)
        {
            entries[index] = SplitGlossaryLine(lines[index]);
        }

        glossaryCacheSource = bodyText;
        glossaryCache = entries;
        return entries;
    }

    private static (string Term, string Definition) SplitGlossaryLine(string line)
    {
        var separatorIndex = line.IndexOf(": ", StringComparison.Ordinal);
        return separatorIndex < 0 ? (line, string.Empty) : (line[..separatorIndex], line[(separatorIndex + 2)..]);
    }
}
