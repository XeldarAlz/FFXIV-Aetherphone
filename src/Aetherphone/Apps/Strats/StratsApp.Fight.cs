using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Strats;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Strats;

internal sealed partial class StratsApp
{
    private const float ScrollAnchorShare = 0.45f;
    private const float MechanicSettledShare = 0.55f;
    private const float DisclosureRowHeight = 44f;
    private const float PagerPillHeight = 38f;
    private const float LinkPillHeight = 30f;
    private const float MaxImageHeight = 420f;
    private const float SpotBarWidth = 3f;
    private const float SpotFillAlpha = 0.12f;
    private const float CardGap = 12f;
    private const float CardPad = 16f;
    private const float BlockGap = 10f;
    private const float LabelGap = 2f;
    private const float HeaderGap = 2f;
    private const float ChapterTopPad = 22f;
    private const float ChapterBottomGap = 10f;
    private const float TimelineTimeWidth = 44f;
    private const float TimelineDotRadius = 4f;
    private const float TimelineNameGap = 12f;
    private const float LinkIconWidth = 18f;
    private const float LinkIconScale = 0.75f;
    private const float LinkTrailingScale = 0.6f;
    private const float ChevronScale = 0.62f;
    private const float BottomBreathing = 28f;
    private const string LinkRowIdPrefix = "strats.link:";

    private static readonly string VideoGlyph = IconGlyph.Of(FontAwesomeIcon.Video);
    private static readonly string BoardGlyph = IconGlyph.Of(FontAwesomeIcon.Map);
    private static readonly string DocumentGlyph = IconGlyph.Of(FontAwesomeIcon.FileAlt);
    private static readonly string LinkGlyph = IconGlyph.Of(FontAwesomeIcon.ExternalLinkAlt);
    private static readonly string ChevronDownGlyph = IconGlyph.Of(FontAwesomeIcon.ChevronDown);
    private static readonly string ChevronUpGlyph = IconGlyph.Of(FontAwesomeIcon.ChevronUp);

    private readonly NavBarButton[] fightButtons = new NavBarButton[1];
    private FightDoc? labelsDoc;
    private string[] tabLabels = Array.Empty<string>();
    private bool[] tabActive = Array.Empty<bool>();
    private bool mechanicAnchorTaken;
    private TimelineEntry[] timelineSource = Array.Empty<TimelineEntry>();
    private string[] timelineTimes = Array.Empty<string>();
    private GuideLink[] linksSource = Array.Empty<GuideLink>();
    private string[] linkGlyphs = Array.Empty<string>();
    private string[] linkRowIds = Array.Empty<string>();
    private string linksCountLabel = string.Empty;
    private float sectionsScrollY;

    private void DrawFight(Rect area, StratsView view)
    {
        if (!manifestStore.TryFind(view.FightKey, out var fight))
        {
            router.Pop();
            return;
        }

        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context);
        var entry = guideStore.Request(fight, false);
        var doc = entry.Doc;
        var current = doc is null ? null : ResolveCurrent(doc);
        if (doc is null || current is null)
        {
            DrawGuideState(navBar.Body, entry, fight);
        }
        else
        {
            DrawFightBody(navBar.Body, fight, doc, current);
        }

        var buttonCount = 0;
        if (current is not null && contents.Length > 0)
        {
            fightButtons[0] = new NavBarButton(PhoneIcons.Menu, Loc.T(L.Strats.Contents));
            buttonCount = 1;
            UiAnchors.Report("strats.contents", AppHeader.LargeTitleButtonRect(in navBar, 0, buttonCount));
        }

        var pressed = AppHeader.EndLargeTitle(in navBar, context, "strats.fight.nav", FightTitle(fight),
            NavBarStyle.From(ui), fightButtons.AsSpan(0, buttonCount), DisplayName, back);
        if (pressed == 0 && buttonCount > 0)
        {
            OpenContents();
        }
    }

    private static string FightTitle(ManifestFight fight) => fight.Abbrev.Length > 0 ? fight.Abbrev : fight.Title;

    private void DrawFightBody(Rect body, ManifestFight fight, FightDoc doc, ResolvedFight current)
    {
        var scale = UiScale.Current;
        EnsureLabels(doc, current);
        mechanicAnchorTaken = false;
        UiAnchors.Report("strats.scroll",
            new Rect(new Vector2(body.Min.X, body.Max.Y - body.Height * ScrollAnchorShare), body.Max));
        using (ImRaii.PushId(fight.Key))
        using (var surface = AppSurface.Begin(body))
        {
            DrawFightHeader(fight, scale);
            DrawSetup(doc, current, scale);
            DrawReferenceCard(current, scale);
            DrawTabs(doc, current, scale);
            DrawStratIntro(current, scale);
            DrawStratDifferences(current, scale);
            DrawPhases(current, scale);
            TrackReading(in surface, scale);
            DrawSectionPager(doc, current, in surface, scale);
            DrawResources(current, scale);
            DrawBackToTop(in surface, scale);
            ImGui.Dummy(new Vector2(0f, BottomBreathing * scale));
        }
    }

    private void DrawGuideState(Rect body, GuideEntry entry, ManifestFight fight)
    {
        var scale = UiScale.Current;
        if (entry.State == StratsState.Failed)
        {
            if (EmptyState.Draw(body, ui, FontAwesomeIcon.CloudDownloadAlt, Loc.T(L.Strats.GuideFailed),
                    Loc.T(L.Strats.GuideFailedHint), Loc.T(L.Strats.Retry)))
            {
                guideStore.Request(fight, true);
            }

            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var left = body.Min.X;
        var right = body.Max.X;
        var top = body.Min.Y + Metrics.Space.Sm * scale;
        Skeleton.Rows(drawList, new Rect(new Vector2(left, top), new Vector2(right, body.Max.Y - CardGap * scale)),
            72f, CardGap, scale);
    }

    private void EnsureLabels(FightDoc doc, ResolvedFight current)
    {
        if (!ReferenceEquals(labelsDoc, doc))
        {
            labelsDoc = doc;
            tabLabels = new string[doc.Tabs.Length];
            tabActive = new bool[doc.Tabs.Length];
            for (var index = 0; index < doc.Tabs.Length; index++)
            {
                tabLabels[index] = doc.Tabs[index].Label;
            }

            BuildSetupLabels(doc);
            tabRail.Reset();
            toggleRails.Clear();
        }

        EnsureRoleColumns(doc, current.Strat.JpRoles);
        if (!ReferenceEquals(timelineSource, current.Timeline))
        {
            timelineSource = current.Timeline;
            timelineTimes = new string[current.Timeline.Length];
            for (var index = 0; index < current.Timeline.Length; index++)
            {
                timelineTimes[index] = FormatDuration(current.Timeline[index].StartMs);
            }
        }

        if (!ReferenceEquals(linksSource, current.Strat.Links))
        {
            linksSource = current.Strat.Links;
            linkGlyphs = new string[linksSource.Length];
            linkRowIds = new string[linksSource.Length];
            for (var index = 0; index < linksSource.Length; index++)
            {
                linkGlyphs[index] = LinkGlyphFor(linksSource[index].Url);
                linkRowIds[index] = string.Concat(LinkRowIdPrefix, index.ToString());
            }

            linksCountLabel = linksSource.Length.ToString();
        }
    }

    private static string LinkGlyphFor(string url)
    {
        if (url.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("youtu.be", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("twitch.tv", StringComparison.OrdinalIgnoreCase))
        {
            return VideoGlyph;
        }

        if (url.Contains("raidplan.io", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("board.wtfdig", StringComparison.OrdinalIgnoreCase))
        {
            return BoardGlyph;
        }

        if (url.Contains("pastebin", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("docs.google", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("drive.google", StringComparison.OrdinalIgnoreCase))
        {
            return DocumentGlyph;
        }

        return LinkGlyph;
    }

    private static string FormatDuration(int milliseconds)
    {
        var totalSeconds = Math.Max(0, milliseconds / 1000);
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return string.Concat(minutes.ToString(), ":", seconds.ToString("00"));
    }

    private void DrawFightHeader(ManifestFight fight, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var titleHeight = Typography.DrawWrappedLeft(origin, fight.Title, ui.TitleInk, TextStyles.Headline, width);
        var total = titleHeight;
        if (fight.Subtitle.Length > 0)
        {
            var subtitleTop = origin.Y + titleHeight + HeaderGap * scale;
            Typography.Draw(drawList, new Vector2(origin.X, subtitleTop),
                Typography.FitText(fight.Subtitle, width, TextStyles.Subheadline), ui.MutedInk,
                TextStyles.Subheadline);
            total += HeaderGap * scale + Typography.LineHeight(TextStyles.Subheadline);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, total + CardGap * scale));
    }

    private void DrawReferenceCard(ResolvedFight current, float scale)
    {
        var timeline = current.Timeline;
        var links = linksSource;
        if (timeline.Length == 0 && links.Length == 0)
        {
            return;
        }

        var rowCount = 0;
        if (timeline.Length > 0)
        {
            rowCount += 1 + (timelineOpen ? timeline.Length : 0);
        }

        if (links.Length > 0)
        {
            rowCount += 1 + (linksOpen ? links.Length : 0);
        }

        var drawList = ImGui.GetWindowDrawList();
        var card = GroupCard.Begin(ui, rowCount, DisclosureRowHeight);
        if (timeline.Length > 0)
        {
            var value = timelineOpen ? Loc.T(L.Strats.HideTimeline) : Loc.T(L.Strats.ShowTimeline);
            if (DrawDisclosureRow(drawList, card.NextRow(), Loc.T(L.Strats.Timeline), value, timelineOpen))
            {
                timelineOpen = !timelineOpen;
            }

            if (timelineOpen)
            {
                for (var index = 0; index < timeline.Length; index++)
                {
                    DrawTimelineRow(drawList, card.NextRow(), timeline[index], index, scale);
                }
            }
        }

        if (links.Length > 0)
        {
            var value = linksOpen ? Loc.T(L.Strats.HideTimeline) : linksCountLabel;
            if (DrawDisclosureRow(drawList, card.NextRow(), Loc.T(L.Strats.Sources), value, linksOpen))
            {
                linksOpen = !linksOpen;
            }

            if (linksOpen)
            {
                for (var index = 0; index < links.Length; index++)
                {
                    DrawLinkRow(drawList, card.NextRow(), links[index], index, scale);
                }
            }
        }

        card.End();
        ImGui.Dummy(new Vector2(0f, CardGap * scale - ImGui.GetStyle().ItemSpacing.Y));
    }

    private bool DrawDisclosureRow(ImDrawListPtr drawList, Rect row, string title, string value, bool open)
    {
        var scale = UiScale.Current;
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            drawList.AddRectFilled(new Vector2(row.Min.X - Metrics.Space.Lg * scale, row.Min.Y),
                new Vector2(row.Max.X + Metrics.Space.Lg * scale, row.Max.Y), ImGui.GetColorU32(ui.HoverWash));
        }

        var chevronCenter = new Vector2(row.Max.X - Metrics.Space.Xs * scale, row.Center.Y);
        AppSkin.Icon(drawList, chevronCenter, open ? ChevronUpGlyph : ChevronDownGlyph, ui.MutedInk, ChevronScale);
        var valueRight = chevronCenter.X - Metrics.Space.Lg * scale;
        var valueWidth = Typography.Measure(value, TextStyles.Subheadline).X;
        var lineHeight = Typography.LineHeight(TextStyles.Body);
        Typography.Draw(drawList, new Vector2(valueRight - valueWidth,
            row.Center.Y - Typography.LineHeight(TextStyles.Subheadline) * 0.5f), value, ui.MutedInk,
            TextStyles.Subheadline);
        var titleWidth = MathF.Max(1f, valueRight - valueWidth - Metrics.Space.Md * scale - row.Min.X);
        Typography.Draw(drawList, new Vector2(row.Min.X, row.Center.Y - lineHeight * 0.5f),
            Typography.FitText(title, titleWidth, TextStyles.Body), ui.TitleInk, TextStyles.Body);
        return UiInteract.Click(row.Min, row.Max, hovered);
    }

    private void DrawTimelineRow(ImDrawListPtr drawList, Rect row, TimelineEntry item, int index, float scale)
    {
        Typography.Draw(drawList,
            new Vector2(row.Min.X, row.Center.Y - Typography.LineHeight(TextStyles.FootnoteEmphasized) * 0.5f),
            timelineTimes[index], ui.MutedInk, TextStyles.FootnoteEmphasized);
        var dotCenter = new Vector2(row.Min.X + TimelineTimeWidth * scale, row.Center.Y);
        drawList.AddCircleFilled(dotCenter, TimelineDotRadius * scale, ImGui.GetColorU32(TimelineColor(item.Type)), 12);
        var nameX = dotCenter.X + TimelineNameGap * scale;
        var name = Typography.FitText(item.Name, MathF.Max(1f, row.Max.X - nameX), TextStyles.Subheadline);
        Typography.Draw(drawList,
            new Vector2(nameX, row.Center.Y - Typography.LineHeight(TextStyles.Subheadline) * 0.5f), name, ui.BodyInk,
            TextStyles.Subheadline);
    }

    private void DrawLinkRow(ImDrawListPtr drawList, Rect row, GuideLink link, int index, float scale)
    {
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            drawList.AddRectFilled(new Vector2(row.Min.X - Metrics.Space.Lg * scale, row.Min.Y),
                new Vector2(row.Max.X + Metrics.Space.Lg * scale, row.Max.Y), ImGui.GetColorU32(ui.HoverWash));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var iconCenter = new Vector2(row.Min.X + LinkIconWidth * 0.5f * scale, row.Center.Y);
        AppSkin.Icon(drawList, iconCenter, linkGlyphs[index], ui.Accent, LinkIconScale);
        var labelX = row.Min.X + (LinkIconWidth + Metrics.Space.Sm) * scale;
        var trailingCenter = new Vector2(row.Max.X - Metrics.Space.Xs * scale, row.Center.Y);
        var labelMaxWidth = MathF.Max(1f, trailingCenter.X - Metrics.Space.Lg * scale - labelX);
        Marquee.DrawLeft(drawList, linkRowIds[index], link.Label, labelX,
            row.Center.Y - Typography.LineHeight(TextStyles.Subheadline) * 0.5f, labelMaxWidth, TextStyles.Subheadline,
            ui.BodyInk, hovered);
        AppSkin.Icon(drawList, trailingCenter, LinkGlyph, ui.MutedInk, LinkTrailingScale);
        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            UrlActions.AskThenOpen(link.Url);
        }
    }

    private Vector4 TimelineColor(string type) =>
        type switch
        {
            "Raidwide" => StratsInk.Resolve("orange", ui.BodyInk, ui.MutedInk),
            "Tankbuster" => StratsInk.Resolve("blue", ui.BodyInk, ui.MutedInk),
            "Enrage" => StratsInk.Resolve("red", ui.BodyInk, ui.MutedInk),
            "Mechanic" => ui.Accent,
            _ => ui.MutedInk,
        };

    private void DrawTabs(FightDoc doc, ResolvedFight current, float scale)
    {
        if (doc.Tabs.Length == 0)
        {
            return;
        }

        sectionsScrollY = ImGui.GetCursorPosY();
        for (var index = 0; index < tabActive.Length; index++)
        {
            tabActive[index] = index == current.TabIndex;
        }

        var tapped = tabRail.Draw(ui, tabLabels, tabActive, "strats.section");
        if (tapped >= 0 && tapped != current.TabIndex)
        {
            selection.Tab = tapped;
            TouchSelection();
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
    }

    private void DrawStratIntro(ResolvedFight current, float scale)
    {
        var strat = current.Strat;
        if (strat.Description.IsEmpty && strat.Notes.IsEmpty)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = CardPad * scale;
        var innerWidth = width - pad * 2f;
        var descriptionHeight = strat.Description.IsEmpty
            ? 0f
            : richText.Measure(strat.Description, innerWidth, TextStyles.Subheadline, scale);
        var notesHeight = strat.Notes.IsEmpty ? 0f : richText.Measure(strat.Notes, innerWidth, TextStyles.Footnote, scale);
        var between = descriptionHeight > 0f && notesHeight > 0f ? BlockGap * scale : 0f;
        var height = pad + descriptionHeight + between + notesHeight + pad;
        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var y = origin.Y + pad;
        if (descriptionHeight > 0f)
        {
            richText.Draw(drawList, new Vector2(origin.X + pad, y), strat.Description, innerWidth,
                TextStyles.Subheadline, ui.BodyInk, ui.MutedInk, scale, images);
            y += descriptionHeight + between;
        }

        if (notesHeight > 0f)
        {
            richText.Draw(drawList, new Vector2(origin.X + pad, y), strat.Notes, innerWidth, TextStyles.Footnote,
                ui.MutedInk, ui.MutedInk, scale, images);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + CardGap * scale));
    }

    private void DrawStratDifferences(ResolvedFight current, float scale)
    {
        var differences = current.Doc.StratDifferences;
        if (differences.Length == 0)
        {
            return;
        }

        DrawChapterHeading(Loc.T(L.Strats.StratDifferences), scale);
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = CardPad * scale;
        var innerWidth = width - pad * 2f;
        var labelHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        var height = pad;
        for (var index = 0; index < differences.Length; index++)
        {
            height += labelHeight + LabelGap * scale +
                      richText.Measure(differences[index].Text, innerWidth, TextStyles.Footnote, scale) +
                      (index < differences.Length - 1 ? BlockGap * scale : 0f);
        }

        height += pad;
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + height), Metrics.Radius.Grouped * scale);
        var y = origin.Y + pad;
        for (var index = 0; index < differences.Length; index++)
        {
            var difference = differences[index];
            Typography.Draw(drawList, new Vector2(origin.X + pad, y),
                Typography.FitText(difference.Label, innerWidth, TextStyles.SubheadlineEmphasized), ui.TitleInk,
                TextStyles.SubheadlineEmphasized);
            y += labelHeight + LabelGap * scale;
            y += richText.Draw(drawList, new Vector2(origin.X + pad, y), difference.Text, innerWidth,
                TextStyles.Footnote, ui.BodyInk, ui.MutedInk, scale, images);
            y += BlockGap * scale;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + CardGap * scale));
    }

    private void DrawChapterHeading(string title, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var titleHeight = Typography.DrawWrappedLeft(new Vector2(origin.X, origin.Y + ChapterTopPad * scale), title,
            ui.TitleInk, TextStyles.Title3, width);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, ChapterTopPad * scale + titleHeight + ChapterBottomGap * scale));
    }

    private void DrawPhases(ResolvedFight current, float scale)
    {
        for (var phaseIndex = 0; phaseIndex < current.Phases.Length; phaseIndex++)
        {
            var phase = current.Phases[phaseIndex];
            MarkEntry();
            DrawChapterHeading(phase.Name, scale);
            DrawPhaseIntro(phase, phaseIndex, scale);
            for (var mechIndex = 0; mechIndex < phase.Mechs.Length; mechIndex++)
            {
                MarkEntry();
                DrawMechanicCard(current, phase.Mechs[mechIndex], phaseIndex, mechIndex, scale);
            }
        }
    }

    private void DrawSectionPager(FightDoc doc, ResolvedFight current, in AppSurface.SurfaceScope surface,
        float scale)
    {
        if (doc.Tabs.Length <= 1)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        origin.Y += Metrics.Space.Sm * scale;
        var width = ImGui.GetContentRegionAvail().X;
        var gap = Metrics.Space.Sm * scale;
        var half = (width - gap) * 0.5f;
        var captionHeight = Typography.LineHeight(TextStyles.Footnote) + LabelGap * scale;
        var pillTop = origin.Y + captionHeight;
        var pillHeight = PagerPillHeight * scale;
        if (current.TabIndex > 0)
        {
            Typography.Draw(drawList, origin, Loc.T(L.Common.Previous), ui.MutedInk, TextStyles.Footnote);
            var rect = new Rect(new Vector2(origin.X, pillTop), new Vector2(origin.X + half, pillTop + pillHeight));
            var label = Typography.FitText(tabLabels[current.TabIndex - 1], half - pillHeight * 0.5f,
                TextStyles.Subheadline);
            if (ui.PillButton(rect, label, false, "strats.pager.previous"))
            {
                SwitchSection(current.TabIndex - 1, in surface, scale);
            }
        }

        if (current.TabIndex < doc.Tabs.Length - 1)
        {
            var left = origin.X + half + gap;
            var caption = Loc.T(L.Common.Next);
            var captionWidth = Typography.Measure(caption, TextStyles.Footnote).X;
            Typography.Draw(drawList, new Vector2(origin.X + width - captionWidth, origin.Y), caption, ui.MutedInk,
                TextStyles.Footnote);
            var rect = new Rect(new Vector2(left, pillTop), new Vector2(origin.X + width, pillTop + pillHeight));
            var label = Typography.FitText(tabLabels[current.TabIndex + 1], half - pillHeight * 0.5f,
                TextStyles.Subheadline);
            if (ui.PillButton(rect, label, true, "strats.pager.next"))
            {
                SwitchSection(current.TabIndex + 1, in surface, scale);
            }
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y - Metrics.Space.Sm * scale));
        ImGui.Dummy(new Vector2(width, Metrics.Space.Sm * scale + captionHeight + pillHeight + CardGap * scale));
    }

    private void SwitchSection(int tabIndex, in AppSurface.SurfaceScope surface, float scale)
    {
        selection.Tab = tabIndex;
        TouchSelection();
        surface.JumpTo(MathF.Max(0f, sectionsScrollY - ReadingLineOffset(scale)));
    }

    private void DrawBackToTop(in AppSurface.SurfaceScope surface, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var label = Loc.T(L.Strats.BackToTop);
        var pillHeight = LinkPillHeight * scale;
        var pillWidth = MathF.Min(width, AppSkin.PillWidthFor(label, pillHeight));
        var left = origin.X + (width - pillWidth) * 0.5f;
        var rect = new Rect(new Vector2(left, origin.Y), new Vector2(left + pillWidth, origin.Y + pillHeight));
        if (ui.GhostButton(rect, label))
        {
            gliding = false;
            surface.JumpToTop();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, pillHeight + Metrics.Space.Sm * scale));
    }

    private void DrawPhaseIntro(ResolvedPhase phase, int phaseIndex, float scale)
    {
        var hasText = phase.Description is not null;
        var hasImage = phase.Image is not null;
        var boardLabel = phase.BoardUrl.Length > 0 ? Loc.T(L.Strats.Board) : string.Empty;
        var hasLinks = phase.Links.Length > 0 || boardLabel.Length > 0;
        if (!hasText && !hasImage && !hasLinks)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var y = origin.Y;
        if (hasText)
        {
            y += richText.Draw(drawList, new Vector2(origin.X, y), phase.Description!, width, TextStyles.Body,
                ui.BodyInk, ui.MutedInk, scale, images);
            y += BlockGap * scale;
        }

        if (hasImage)
        {
            var frameHeight = MathF.Min(MaxImageHeight * scale, SpotlightImage.HeightFor(phase.Image!, string.Empty, width));
            var frame = new Rect(new Vector2(origin.X, y), new Vector2(origin.X + width, y + frameHeight));
            DrawImageFrame(drawList, frame, phase.Image!, phase.Spotlight, string.Empty, scale,
                new StratsView(StratsScreen.Viewer, selection.FightKey, phaseIndex));
            y = frame.Max.Y + BlockGap * scale;
        }

        if (hasLinks)
        {
            y += DrawLinkFlow(drawList, new Vector2(origin.X, y), width, phase.Links, boardLabel, phase.BoardUrl,
                scale);
            y += BlockGap * scale;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, y - origin.Y));
    }

    private float MeasureLinkFlow(float width, GuideLink[] links, string leadLabel, float scale)
    {
        var pillHeight = LinkPillHeight * scale;
        var flow = new PillFlow(0f, width, Metrics.Space.Sm * scale);
        if (leadLabel.Length > 0)
        {
            flow.Place(MathF.Min(width, AppSkin.PillWidthFor(leadLabel, pillHeight)));
        }

        for (var index = 0; index < links.Length; index++)
        {
            flow.Place(MathF.Min(width, AppSkin.PillWidthFor(links[index].Label, pillHeight)));
        }

        return flow.Height(pillHeight, Metrics.Space.Sm * scale);
    }

    private float DrawLinkFlow(ImDrawListPtr drawList, Vector2 origin, float width, GuideLink[] links,
        string leadLabel, string leadUrl, float scale)
    {
        var pillHeight = LinkPillHeight * scale;
        var rowGap = Metrics.Space.Sm * scale;
        var flow = new PillFlow(origin.X, width, Metrics.Space.Sm * scale);
        if (leadLabel.Length > 0)
        {
            var pillWidth = MathF.Min(width, AppSkin.PillWidthFor(leadLabel, pillHeight));
            var left = flow.Place(pillWidth);
            var top = origin.Y + flow.Row * (pillHeight + rowGap);
            if (ui.PillButton(new Rect(new Vector2(left, top), new Vector2(left + pillWidth, top + pillHeight)),
                    Typography.FitText(leadLabel, pillWidth - pillHeight * 0.5f, TextStyles.Subheadline), false,
                    "strats.board"))
            {
                UrlActions.AskThenOpen(leadUrl);
            }
        }

        for (var index = 0; index < links.Length; index++)
        {
            var link = links[index];
            var pillWidth = MathF.Min(width, AppSkin.PillWidthFor(link.Label, pillHeight));
            var left = flow.Place(pillWidth);
            var top = origin.Y + flow.Row * (pillHeight + rowGap);
            if (ui.GhostButton(new Rect(new Vector2(left, top), new Vector2(left + pillWidth, top + pillHeight)),
                    Typography.FitText(link.Label, pillWidth - pillHeight * 0.5f, TextStyles.Subheadline)))
            {
                UrlActions.AskThenOpen(link.Url);
            }
        }

        return flow.Height(pillHeight, rowGap);
    }

    private void DrawMechanicCard(ResolvedFight current, ResolvedMechanic mech, int phaseIndex, int mechIndex,
        float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = CardPad * scale;
        var innerWidth = width - pad * 2f;
        var gap = BlockGap * scale;
        var separate = current.Doc.SeparateDescriptionAction;

        var nameHeight = Typography.MeasureWrappedBlock(mech.Name, TextStyles.Headline, innerWidth).Y;
        var descriptionHeight = mech.Description is null
            ? 0f
            : richText.Measure(mech.Description, innerWidth, TextStyles.Subheadline, scale);
        var actionHeight = mech.Action is null ? 0f : richText.Measure(mech.Action, innerWidth, TextStyles.Subheadline, scale);
        var mechImageHeight = mech.Image is null
            ? 0f
            : MathF.Min(MaxImageHeight * scale, SpotlightImage.HeightFor(mech.Image, mech.Transform, innerWidth));
        var arenaHeight = mech.Arena is null ? 0f : innerWidth;
        var notesHeight = mech.Notes is null ? 0f : richText.Measure(mech.Notes, innerWidth, TextStyles.Footnote, scale);
        var labelHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var linksHeight = mech.Links.Length == 0 ? 0f : MeasureLinkFlow(innerWidth, mech.Links, string.Empty, scale);

        var spotPad = Metrics.Space.Md * scale;
        var spotInnerWidth = innerWidth - SpotBarWidth * scale - spotPad * 2f;
        var playerTextHeight = mech.PlayerText is null
            ? 0f
            : richText.Measure(mech.PlayerText, spotInnerWidth, TextStyles.BodyEmphasized, scale);
        var playerImageHeight = mech.PlayerImage is null
            ? 0f
            : MathF.Min(MaxImageHeight * scale,
                SpotlightImage.HeightFor(mech.PlayerImage, mech.PlayerTransform, spotInnerWidth));
        var spotHeight = playerTextHeight > 0f || playerImageHeight > 0f
            ? spotPad + labelHeight + LabelGap * scale + playerTextHeight +
              (playerTextHeight > 0f && playerImageHeight > 0f ? gap : 0f) + playerImageHeight + spotPad
            : 0f;

        var sectionLabel = separate ? labelHeight + LabelGap * scale : 0f;
        var height = pad + nameHeight;
        height += Block(descriptionHeight, sectionLabel, gap);
        height += Block(actionHeight, sectionLabel, gap);
        height += Block(spotHeight, 0f, gap);
        height += Block(mechImageHeight, 0f, gap);
        height += Block(arenaHeight, 0f, gap);
        height += Block(notesHeight, 0f, gap);
        height += Block(linksHeight, 0f, gap);
        height += pad;

        var max = new Vector2(origin.X + width, origin.Y + height);
        if (!mechanicAnchorTaken)
        {
            mechanicAnchorTaken = true;
            ReportMechanicAnchor(origin, max);
        }

        if (ImGui.IsRectVisible(origin, max))
        {
            ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
            var x = origin.X + pad;
            var y = origin.Y + pad;
            Typography.DrawWrappedLeft(new Vector2(x, y), mech.Name, ui.TitleInk, TextStyles.Headline, innerWidth);
            y += nameHeight;

            if (descriptionHeight > 0f)
            {
                y += gap;
                if (separate)
                {
                    Typography.Draw(drawList, new Vector2(x, y), Loc.T(L.Strats.WhatHappens), ui.MutedInk,
                        TextStyles.FootnoteEmphasized);
                    y += sectionLabel;
                }

                richText.Draw(drawList, new Vector2(x, y), mech.Description!, innerWidth, TextStyles.Subheadline,
                    ui.BodyInk, ui.MutedInk, scale, images);
                y += descriptionHeight;
            }

            if (actionHeight > 0f)
            {
                y += gap;
                if (separate)
                {
                    Typography.Draw(drawList, new Vector2(x, y), Loc.T(L.Strats.WhatToDo), ui.Accent,
                        TextStyles.FootnoteEmphasized);
                    y += sectionLabel;
                }

                richText.Draw(drawList, new Vector2(x, y), mech.Action!, innerWidth, TextStyles.Subheadline,
                    ui.BodyInk, ui.MutedInk, scale, images);
                y += actionHeight;
            }

            if (spotHeight > 0f)
            {
                y += gap;
                var panel = new Rect(new Vector2(x, y), new Vector2(x + innerWidth, y + spotHeight));
                DrawSpotPanel(drawList, panel, mech, spotPad, spotInnerWidth, labelHeight, playerTextHeight,
                    playerImageHeight, gap, scale, phaseIndex, mechIndex);
                y += spotHeight;
            }

            if (mechImageHeight > 0f)
            {
                y += gap;
                var frame = new Rect(new Vector2(x, y), new Vector2(x + innerWidth, y + mechImageHeight));
                DrawImageFrame(drawList, frame, mech.Image!, null, mech.Transform, scale,
                    new StratsView(StratsScreen.Viewer, selection.FightKey, phaseIndex, mechIndex));
                y += mechImageHeight;
            }

            if (arenaHeight > 0f)
            {
                y += gap;
                var stage = new Rect(new Vector2(x, y), new Vector2(x + innerWidth, y + arenaHeight));
                ArenaDiagramView.Draw(drawList, stage, mech.Arena!, scale, ui.BodyInk);
                if (mech.Arena!.FallbackUrl.Length > 0)
                {
                    DrawArenaFallback(stage, mech.Arena.FallbackUrl, scale);
                }

                y += arenaHeight;
            }

            if (notesHeight > 0f)
            {
                y += gap;
                richText.Draw(drawList, new Vector2(x, y), mech.Notes!, innerWidth, TextStyles.Footnote, ui.MutedInk,
                    ui.MutedInk, scale, images);
                y += notesHeight;
            }

            if (linksHeight > 0f)
            {
                y += gap;
                DrawLinkFlow(drawList, new Vector2(x, y), innerWidth, mech.Links, string.Empty, string.Empty, scale);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + CardGap * scale));
    }

    private static void ReportMechanicAnchor(Vector2 origin, Vector2 max)
    {
        if (!UiAnchors.Recording)
        {
            return;
        }

        var windowTop = ImGui.GetWindowPos().Y;
        var windowBottom = windowTop + ImGui.GetWindowHeight();
        var settledLine = windowTop + (windowBottom - windowTop) * MechanicSettledShare;
        if (origin.Y < windowTop || origin.Y > settledLine)
        {
            return;
        }

        UiAnchors.Report("strats.mechanic.first",
            new Rect(origin, new Vector2(max.X, MathF.Min(max.Y, windowBottom))));
    }

    private void DrawSpotPanel(ImDrawListPtr drawList, Rect panel, ResolvedMechanic mech, float spotPad,
        float spotInnerWidth, float labelHeight, float playerTextHeight, float playerImageHeight, float gap, float scale,
        int phaseIndex, int mechIndex)
    {
        var scaleRadius = Metrics.Radius.Md * scale;
        Squircle.Fill(drawList, panel.Min, panel.Max, scaleRadius,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, SpotFillAlpha)));
        var barWidth = SpotBarWidth * scale;
        Squircle.Fill(drawList, new Vector2(panel.Min.X, panel.Min.Y + spotPad),
            new Vector2(panel.Min.X + barWidth, panel.Max.Y - spotPad), barWidth * 0.5f, ImGui.GetColorU32(ui.Accent));
        var x = panel.Min.X + barWidth + spotPad;
        var y = panel.Min.Y + spotPad;
        Typography.Draw(drawList, new Vector2(x, y), roleSpotLabel, ui.Accent, TextStyles.FootnoteEmphasized);
        y += labelHeight + LabelGap * scale;
        if (playerTextHeight > 0f)
        {
            richText.Draw(drawList, new Vector2(x, y), mech.PlayerText!, spotInnerWidth, TextStyles.BodyEmphasized,
                ui.TitleInk, ui.MutedInk, scale, images);
            y += playerTextHeight + (playerImageHeight > 0f ? gap : 0f);
        }

        if (playerImageHeight > 0f)
        {
            var frame = new Rect(new Vector2(x, y), new Vector2(x + spotInnerWidth, y + playerImageHeight));
            DrawImageFrame(drawList, frame, mech.PlayerImage!, mech.PlayerSpotlight, mech.PlayerTransform, scale,
                new StratsView(StratsScreen.Viewer, selection.FightKey, phaseIndex, mechIndex, true));
        }
    }

    private static float Block(float contentHeight, float labelHeight, float gap) =>
        contentHeight > 0f ? gap + labelHeight + contentHeight : 0f;

    private void DrawImageFrame(ImDrawListPtr drawList, Rect frame, ImageRef image, SpotlightMask? mask,
        string transform, float scale, StratsView viewerRoute)
    {
        var texture = images.Sized(StratsContent.Url(image.Key), frame.Width);
        SpotlightImage.Draw(drawList, frame, texture, mask, transform, Metrics.Radius.Md * scale, scale,
            SpotlightImage.PlaceholderFor(theme), ui.Accent);
        var hovered = UiInteract.Hover(frame.Min, frame.Max);
        if (hovered && texture is not null)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            UiInteract.HoverHighlight(drawList, frame.Min, frame.Max, Metrics.Radius.Md * scale);
        }

        if (texture is not null && UiInteract.Click(frame.Min, frame.Max, hovered))
        {
            OpenViewer(viewerRoute);
        }
    }

    private void DrawArenaFallback(Rect stage, string url, float scale)
    {
        var label = Loc.T(L.Strats.OpenFullDiagram);
        var pillHeight = LinkPillHeight * scale;
        var inset = Metrics.Space.Sm * scale;
        var pillWidth = MathF.Min(stage.Width - inset * 2f, AppSkin.PillWidthFor(label, pillHeight));
        var rect = new Rect(new Vector2(stage.Max.X - pillWidth - inset, stage.Max.Y - pillHeight - inset),
            new Vector2(stage.Max.X - inset, stage.Max.Y - inset));
        if (ui.GhostButton(rect, Typography.FitText(label, pillWidth - pillHeight * 0.5f, TextStyles.Subheadline)))
        {
            UrlActions.AskThenOpen(url);
        }
    }

    private void DrawResources(ResolvedFight current, float scale)
    {
        var resources = current.Doc.Resources;
        if (resources is null)
        {
            return;
        }

        DrawChapterHeading(resources.Title.Length > 0 ? resources.Title : Loc.T(L.Strats.Resources), scale);
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = CardPad * scale;
        var innerWidth = width - pad * 2f;
        var textHeight = resources.Text.IsEmpty ? 0f : richText.Measure(resources.Text, innerWidth, TextStyles.Footnote, scale);
        var linksHeight = resources.Links.Length == 0
            ? 0f
            : MeasureLinkFlow(innerWidth, resources.Links, string.Empty, scale);
        var between = textHeight > 0f && linksHeight > 0f ? BlockGap * scale : 0f;
        var height = pad + textHeight + between + linksHeight + pad;
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + height), Metrics.Radius.Grouped * scale);
        var y = origin.Y + pad;
        if (textHeight > 0f)
        {
            richText.Draw(drawList, new Vector2(origin.X + pad, y), resources.Text, innerWidth, TextStyles.Footnote,
                ui.BodyInk, ui.MutedInk, scale, images);
            y += textHeight + between;
        }

        if (linksHeight > 0f)
        {
            DrawLinkFlow(drawList, new Vector2(origin.X + pad, y), innerWidth, resources.Links, string.Empty,
                string.Empty, scale);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + CardGap * scale));
    }
}
