using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Collections;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Collections;

internal sealed partial class CollectionsApp
{
    private const float DetailIconSize = 84f;
    private const float DetailIconRadius = 20f;
    private const float DetailPad = 16f;
    private const float DetailMetaGap = 10f;
    private const float DetailChipIcon = 22f;
    private const float DetailChipGap = 8f;
    private const float InfoRowHeight = 46f;
    private const float SourceRowPadY = 12f;
    private const float SourceActionSize = 34f;
    private const float SourceActionGap = 10f;
    private const float PillHeight = 44f;
    private const float StarsGap = 6f;
    private const float RarityBarHeight = 8f;
    private const string MarketAppId = "market";

    private readonly NavBarButton[] detailButtons = new NavBarButton[2];
    private CollectionItem? detailItem;
    private string detailLanguage = string.Empty;
    private int detailRevision = -1;
    private string detailRarityLine = string.Empty;
    private SourceLink[] detailLinks = Array.Empty<SourceLink>();
    private string[] detailNotes = Array.Empty<string>();

    private void SyncDetail(CollectionItem item)
    {
        var language = Loc.Current.Code;
        var revision = catalog.Revision;
        if (ReferenceEquals(detailItem, item) && detailRevision == revision &&
            string.Equals(detailLanguage, language, StringComparison.Ordinal))
        {
            return;
        }

        detailItem = item;
        detailRevision = revision;
        detailLanguage = language;
        detailRarityLine = item.HasRarity ? Loc.T(L.Collections.RarityLine, item.RarityText) : string.Empty;
        var count = item.Sources.Length;
        if (detailLinks.Length != count)
        {
            detailLinks = new SourceLink[count];
            detailNotes = new string[count];
        }

        for (var index = 0; index < count; index++)
        {
            detailLinks[index] = LinkFor(item.Sources[index]);
            detailNotes[index] = SourceDetail(detailLinks[index]);
        }
    }

    private void DrawDetail(Rect area, CollectionItem item)
    {
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context);
        UiAnchors.Report("collections.detail", navBar.Body);
        SyncDetail(item);
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = DrawDetailHero(drawList, new Vector2(origin.X, origin.Y), width, item, scale);
            cursorY = DrawDetailActions(new Vector2(origin.X, cursorY), width, item, scale);
            cursorY = DrawDetailRarity(drawList, origin.X, cursorY, width, item, scale);
            cursorY = DrawDetailSources(drawList, origin.X, cursorY, width, item, scale);
            cursorY = DrawDetailAbout(drawList, origin.X, cursorY, width, item, scale);
            cursorY = DrawDetailInfo(drawList, origin.X, cursorY, width, item, scale);
            ReserveTo(origin, width, cursorY + BottomBreathing * scale);
        }

        var count = 0;
        if (journal.IsTracking)
        {
            var pinned = journal.IsPinned(item.Category, item.Id);
            detailButtons[count++] = new NavBarButton(
                IconGlyph.Of(pinned ? FontAwesomeIcon.Check : FontAwesomeIcon.Plus),
                Loc.T(pinned ? L.Collections.RemoveFromWishlist : L.Collections.AddToWishlist));
        }

        var canLink = item.ItemId != 0;
        if (canLink)
        {
            detailButtons[count++] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.CommentDots),
                Loc.T(L.Collections.LinkInChat));
        }

        var backTitle = router.TryGetView(router.Depth - 2, out var previous) &&
                        previous.Kind == CollectionViewKind.Category
            ? Loc.T(CollectionText.Label(previous.Category))
            : DisplayName;
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "collections.detail.nav", item.Name,
            NavBarStyle.From(ui), detailButtons.AsSpan(0, count), backTitle, back);
        if (pressed < 0)
        {
            return;
        }

        var linkIndex = journal.IsTracking ? 1 : 0;
        if (pressed == linkIndex && canLink)
        {
            UiFeedback.Play(UiSound.Tap);
            GameLinkActions.LinkInChat(item.ItemId);
            return;
        }

        if (journal.IsTracking && pressed == 0)
        {
            var nowPinned = journal.TogglePin(item.Category, item.Id);
            digest.Invalidate();
            UiFeedback.Play(nowPinned ? UiSound.ToggleOn : UiSound.ToggleOff);
            if (nowPinned)
            {
                ShellToast.Show(Loc.T(L.Collections.AddedToWishlist));
            }
        }
    }

    private float DrawDetailHero(ImDrawListPtr drawList, Vector2 origin, float width, CollectionItem item,
        float scale)
    {
        var pad = DetailPad * scale;
        var iconSize = DetailIconSize * scale;
        var textLeft = origin.X + pad + iconSize + DetailPad * scale;
        var textWidth = MathF.Max(1f, origin.X + width - pad - textLeft);
        var nameFits = string.Equals(Typography.FitText(item.Name, width, TextStyles.LargeTitle), item.Name,
            StringComparison.Ordinal);
        var nameHeight = nameFits ? 0f
            : Typography.MeasureWrappedBlock(item.Name, TextStyles.Title3, textWidth).Y + DetailMetaGap * scale;
        var chipHeight = MathF.Max(DetailChipIcon * scale, Typography.LineHeight(TextStyles.Subheadline));
        var badge = BadgeFor(item);
        var pillHeight = badge == RowBadge.None ? 0f : 26f * scale + DetailMetaGap * scale;
        var starsHeight = item.Stars > 0 ? Typography.LineHeight(TextStyles.Subheadline) + StarsGap * scale : 0f;
        var contentHeight = nameHeight + chipHeight + pillHeight + starsHeight;
        var height = MathF.Max(iconSize, contentHeight) + pad * 2f;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var radius = Metrics.Radius.Widget * scale;
        ui.Card(drawList, origin, max, radius, true);
        Material.TopGlow(drawList, origin, max, radius, CollectionsArt.Tint(item.Category), HeroGlowCoverage,
            HeroGlowStrength);
        var iconMin = new Vector2(origin.X + pad, origin.Y + (height - iconSize) * 0.5f);
        DrawIcon(drawList, item, iconMin, iconMin + new Vector2(iconSize, iconSize), DetailIconRadius * scale);

        var top = origin.Y + (height - contentHeight) * 0.5f;
        if (!nameFits)
        {
            top += Typography.DrawWrappedLeft(new Vector2(textLeft, top), item.Name, ui.TitleInk, TextStyles.Title3,
                textWidth) + DetailMetaGap * scale;
        }

        var chipSize = DetailChipIcon * scale;
        var chipCenter = new Vector2(textLeft + chipSize * 0.5f, top + chipHeight * 0.5f);
        CollectionsArt.CategoryTile(drawList, chipCenter, chipSize, item.Category);
        var meta = Loc.T(CollectionText.Label(item.Category));
        var metaLeft = chipCenter.X + chipSize * 0.5f + DetailChipGap * scale;
        var metaFitted = Typography.FitText(meta, MathF.Max(1f, textLeft + textWidth - metaLeft),
            TextStyles.Subheadline);
        var metaHeight = Typography.Measure(metaFitted, TextStyles.Subheadline).Y;
        Typography.Draw(drawList, new Vector2(metaLeft, chipCenter.Y - metaHeight * 0.5f), metaFitted, ui.BodyInk,
            TextStyles.Subheadline);
        top += chipHeight + DetailMetaGap * scale;
        if (badge != RowBadge.None)
        {
            var owned = badge == RowBadge.Owned;
            CollectionsArt.StatusPill(drawList, new Vector2(textLeft, top),
                Loc.T(owned ? L.Collections.Owned : L.Collections.Missing),
                owned ? CollectionsArt.OwnedInk : ui.MutedInk, owned, scale);
            top += pillHeight;
        }

        if (item.Stars > 0)
        {
            var stars = StarsLabel(item.Stars);
            Typography.Draw(drawList, new Vector2(textLeft, top), stars, ui.Accent, TextStyles.Subheadline);
        }

        return max.Y;
    }

    private static readonly string[] StarLabels = { "★", "★★", "★★★", "★★★★", "★★★★★" };

    private static string StarsLabel(int stars) => StarLabels[Math.Clamp(stars, 1, StarLabels.Length) - 1];

    private float DrawDetailActions(Vector2 origin, float width, CollectionItem item, float scale)
    {
        if (item.ItemId == 0 || !item.Tradeable)
        {
            return origin.Y;
        }

        var top = origin.Y + ControlGap * scale;
        var rect = new Rect(new Vector2(origin.X, top), new Vector2(origin.X + width, top + PillHeight * scale));
        if (ui.ActionPill(rect, Loc.T(L.Collections.MarketPrices), true, TextStyles.Headline))
        {
            UiFeedback.Play(UiSound.Tap);
            marketLauncher.RequestItem(item.ItemId);
            navigation.Open(MarketAppId);
        }

        return rect.Max.Y;
    }

    private float DrawDetailRarity(ImDrawListPtr drawList, float left, float top, float width, CollectionItem item,
        float scale)
    {
        if (!item.HasRarity)
        {
            return top;
        }

        var cursorY = top + SectionGap * scale;
        cursorY += CollectionsArt.SectionHeader(drawList, new Vector2(left, cursorY), width,
            Loc.T(L.Collections.Rarity), ui.TitleInk);
        cursorY += HeaderGap * scale;
        var pad = DetailPad * scale;
        var innerWidth = width - pad * 2f;
        var line = detailRarityLine;
        var note = Loc.T(L.Collections.RarityNote);
        var lineHeight = Typography.MeasureWrappedBlock(line, TextStyles.BodyEmphasized, innerWidth).Y;
        var noteHeight = Typography.MeasureWrappedBlock(note, TextStyles.Footnote, innerWidth).Y;
        var height = pad * 2f + lineHeight + DetailMetaGap * scale + RarityBarHeight * scale +
                     DetailMetaGap * scale + noteHeight;
        var min = new Vector2(left, cursorY);
        var max = new Vector2(left + width, cursorY + height);
        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale, true);
        var y = min.Y + pad;
        y += Typography.DrawWrappedLeft(new Vector2(min.X + pad, y), line, ui.TitleInk, TextStyles.BodyEmphasized,
            innerWidth) + DetailMetaGap * scale;
        CollectionsArt.Bar(drawList, new Vector2(min.X + pad, y), new Vector2(max.X - pad, y + RarityBarHeight * scale),
            item.Rarity / 100f, Palette.WithAlpha(ui.TitleInk, ProgressTrackAlpha), ui.Accent);
        y += RarityBarHeight * scale + DetailMetaGap * scale;
        Typography.DrawWrappedLeft(new Vector2(min.X + pad, y), note, ui.MutedInk, TextStyles.Footnote, innerWidth);
        return max.Y;
    }

    private float DrawDetailSources(ImDrawListPtr drawList, float left, float top, float width, CollectionItem item,
        float scale)
    {
        if (item.Sources.Length == 0)
        {
            return top;
        }

        var cursorY = top + SectionGap * scale;
        cursorY += CollectionsArt.SectionHeader(drawList, new Vector2(left, cursorY), width,
            Loc.T(L.Collections.HowToObtain), ui.TitleInk);
        cursorY += HeaderGap * scale;
        var cardTop = cursorY;
        var height = 0f;
        for (var index = 0; index < item.Sources.Length; index++)
        {
            height += SourceRowHeight(item.Sources[index], index, width, scale);
        }

        var max = new Vector2(left + width, cardTop + height);
        ui.Card(drawList, new Vector2(left, cardTop), max, Metrics.Radius.Grouped * scale, true);
        var rowTop = cardTop;
        for (var index = 0; index < item.Sources.Length; index++)
        {
            var source = item.Sources[index];
            var rowHeight = SourceRowHeight(source, index, width, scale);
            var row = new Rect(new Vector2(left, rowTop), new Vector2(left + width, rowTop + rowHeight));
            if (index > 0)
            {
                drawList.AddLine(new Vector2(left + DetailPad * scale, rowTop),
                    new Vector2(left + width - DetailPad * scale, rowTop),
                    ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, HairlineAlpha)), 1f);
            }

            DrawSourceRow(drawList, row, source, index, scale);
            rowTop += rowHeight;
        }

        return max.Y;
    }

    private SourceLink LinkFor(CollectionSource source)
    {
        if (source.RelatedId is not { } relatedId || relatedId <= 0)
        {
            return default;
        }

        if (string.Equals(source.RelatedType, "Achievement", StringComparison.OrdinalIgnoreCase))
        {
            var achievement = catalog.RequestCatalog(CollectionCategory.Achievements).Find(relatedId);
            return achievement is null ? default : new SourceLink(achievement, null);
        }

        if (string.Equals(source.RelatedType, "Quest", StringComparison.OrdinalIgnoreCase))
        {
            return new SourceLink(null, catalog.Quest(relatedId));
        }

        return default;
    }

    private float SourceRowHeight(CollectionSource source, int index, float width, float scale)
    {
        var textWidth = SourceTextWidth(index, width, scale);
        var height = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        var text = source.Text ?? string.Empty;
        if (text.Length > 0)
        {
            height += Typography.MeasureWrappedBlock(text, TextStyles.Footnote, textWidth).Y;
        }

        if (detailNotes[index].Length > 0)
        {
            height += Typography.LineHeight(TextStyles.FootnoteEmphasized);
        }

        return MathF.Max(Metrics.Size.TapTarget * scale, height + SourceRowPadY * 2f * scale);
    }

    private float SourceTextWidth(int index, float width, float scale)
    {
        var reserve = detailLinks[index].HasAction ? (SourceActionSize + SourceActionGap) * scale : 0f;
        return MathF.Max(1f, width - DetailPad * 2f * scale - reserve);
    }

    private static string SourceDetail(SourceLink link)
    {
        if (link.Quest is not { } quest)
        {
            return string.Empty;
        }

        if (quest.Completed)
        {
            return Loc.T(L.Collections.QuestDone);
        }

        return quest.PlaceName.Length > 0 ? Loc.T(L.Collections.QuestGiver, quest.PlaceName) : string.Empty;
    }

    private void DrawSourceRow(ImDrawListPtr drawList, Rect row, CollectionSource source, int index, float scale)
    {
        var link = detailLinks[index];
        var pad = DetailPad * scale;
        var textWidth = SourceTextWidth(index, row.Width, scale);
        var hovered = link.Achievement is not null && UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            var inset = RowWashInset * scale;
            Squircle.Fill(drawList, row.Min + new Vector2(inset, inset), row.Max - new Vector2(inset, inset),
                RowWashRadius * scale, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var type = source.Type is { Length: > 0 } sourceType ? sourceType : Loc.T(L.Collections.Source);
        var y = row.Min.Y + SourceRowPadY * scale;
        var typeFitted = Typography.FitText(type, textWidth, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(row.Min.X + pad, y), typeFitted, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        y += Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        var text = source.Text ?? string.Empty;
        if (text.Length > 0)
        {
            y += Typography.DrawWrappedLeft(new Vector2(row.Min.X + pad, y), text, ui.MutedInk, TextStyles.Footnote,
                textWidth);
        }

        var detail = detailNotes[index];
        if (detail.Length > 0)
        {
            var done = link.Quest is { Completed: true };
            var fitted = Typography.FitText(detail, textWidth, TextStyles.FootnoteEmphasized);
            Typography.Draw(drawList, new Vector2(row.Min.X + pad, y), fitted,
                done ? CollectionsArt.OwnedInk : ui.BodyInk, TextStyles.FootnoteEmphasized);
        }

        var actionCenter = new Vector2(row.Max.X - pad - SourceActionSize * scale * 0.5f, row.Center.Y);
        if (link.Achievement is { } achievement)
        {
            CollectionsArt.Chevron(drawList, actionCenter, 5f * scale, ui.MutedInk, scale);
            if (UiInteract.Click(row.Min, row.Max, hovered))
            {
                OpenItem(achievement);
            }

            return;
        }

        if (link.Quest is { HasLocation: true } questLocation)
        {
            DrawMapButton(drawList, actionCenter, questLocation, scale);
        }
    }

    private void DrawMapButton(ImDrawListPtr drawList, Vector2 center, CollectionQuest quest, float scale)
    {
        var radius = SourceActionSize * scale * 0.5f;
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var hovered = UiInteract.Hover(min, max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(ImGui.GetID($"collections.map.{quest.TerritoryId}.{quest.RawX}"), pressed,
            PressFx.ControlPressedScale);
        var fill = Palette.WithAlpha(ui.Accent, hovered ? 0.32f : 0.20f);
        drawList.AddCircleFilled(center, radius * press, ImGui.GetColorU32(fill), 28);
        ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.MapMarkerAlt, ui.Accent, radius * 0.9f * press);
        HoverTooltip.Show(new Rect(min, max), Loc.T(L.Collections.ShowOnMap));
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!UiInteract.Click(min, max, hovered))
        {
            return;
        }

        UiFeedback.Play(UiSound.Tap);
        GameLinkActions.OpenMap(quest.TerritoryId, quest.MapId, quest.RawX, quest.RawY);
    }

    private float DrawDetailAbout(ImDrawListPtr drawList, float left, float top, float width, CollectionItem item,
        float scale)
    {
        if (item.Description.Length == 0)
        {
            return top;
        }

        var cursorY = top + SectionGap * scale;
        cursorY += CollectionsArt.SectionHeader(drawList, new Vector2(left, cursorY), width,
            Loc.T(L.Collections.About), ui.TitleInk);
        cursorY += HeaderGap * scale;
        var pad = DetailPad * scale;
        var innerWidth = width - pad * 2f;
        var textHeight = Typography.MeasureWrappedBlock(item.Description, TextStyles.Subheadline, innerWidth).Y;
        var max = new Vector2(left + width, cursorY + textHeight + pad * 2f);
        ui.Card(drawList, new Vector2(left, cursorY), max, Metrics.Radius.Grouped * scale, true);
        Typography.DrawWrappedLeft(new Vector2(left + pad, cursorY + pad), item.Description, ui.BodyInk,
            TextStyles.Subheadline, innerWidth);
        return max.Y;
    }

    private float DrawDetailInfo(ImDrawListPtr drawList, float left, float top, float width, CollectionItem item,
        float scale)
    {
        var showPoints = item.Category == CollectionCategory.Achievements && item.Points > 0;
        var showStats = item.Category == CollectionCategory.TriadCards && item.StatsText.Length > 0;
        var rows = (item.Patch.Length > 0 ? 1 : 0) + (showPoints ? 1 : 0) + (showStats ? 1 : 0) +
                   (item.HasTradeable ? 1 : 0);
        if (rows == 0)
        {
            return top;
        }

        var cursorY = top + SectionGap * scale;
        cursorY += CollectionsArt.SectionHeader(drawList, new Vector2(left, cursorY), width,
            Loc.T(L.Collections.Details), ui.TitleInk);
        cursorY += HeaderGap * scale;
        var rowHeight = InfoRowHeight * scale;
        var max = new Vector2(left + width, cursorY + rows * rowHeight);
        ui.Card(drawList, new Vector2(left, cursorY), max, Metrics.Radius.Grouped * scale, true);
        var rowIndex = 0;
        if (item.Patch.Length > 0)
        {
            DrawInfoRow(drawList, left, cursorY, width, rowIndex++, Loc.T(L.Collections.Patch), item.Patch, scale);
        }

        if (showPoints)
        {
            DrawInfoRow(drawList, left, cursorY, width, rowIndex++, Loc.T(L.Collections.Points), item.PointsText,
                scale);
        }

        if (showStats)
        {
            DrawInfoRow(drawList, left, cursorY, width, rowIndex++, Loc.T(L.Collections.CardStats), item.StatsText,
                scale);
        }

        if (item.HasTradeable)
        {
            DrawInfoRow(drawList, left, cursorY, width, rowIndex, Loc.T(L.Collections.Tradeable),
                Loc.T(item.Tradeable ? L.Collections.Yes : L.Collections.No), scale);
        }

        return max.Y;
    }

    private void DrawInfoRow(ImDrawListPtr drawList, float left, float cardTop, float width, int rowIndex,
        string label, string value, float scale)
    {
        var rowHeight = InfoRowHeight * scale;
        var top = cardTop + rowIndex * rowHeight;
        var pad = DetailPad * scale;
        if (rowIndex > 0)
        {
            drawList.AddLine(new Vector2(left + pad, top), new Vector2(left + width - pad, top),
                ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, HairlineAlpha)), 1f);
        }

        var centerY = top + rowHeight * 0.5f;
        var labelFitted = Typography.FitText(label, width * 0.45f, TextStyles.Body);
        var labelSize = Typography.Measure(labelFitted, TextStyles.Body);
        Typography.Draw(drawList, new Vector2(left + pad, centerY - labelSize.Y * 0.5f), labelFitted, ui.MutedInk,
            TextStyles.Body);
        var valueFitted = Typography.FitText(value, width * 0.5f - pad, TextStyles.BodyEmphasized);
        var valueSize = Typography.Measure(valueFitted, TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(left + width - pad - valueSize.X, centerY - valueSize.Y * 0.5f),
            valueFitted, ui.TitleInk, TextStyles.BodyEmphasized);
    }

    private readonly record struct SourceLink(CollectionItem? Achievement, CollectionQuest? Quest)
    {
        public bool HasAction => Achievement is not null || Quest is { HasLocation: true };
    }
}
