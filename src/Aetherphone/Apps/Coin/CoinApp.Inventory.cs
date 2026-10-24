using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Coin;

internal sealed partial class CoinApp
{
    private const float LookHeight = 178f;
    private const float LookAvatar = 42f;
    private const float LookNameGap = 12f;
    private const float LookBadgeGap = 6f;
    private const int SwatchColumns = 3;
    private const float SwatchGap = 10f;
    private const float SwatchLabelGap = 6f;
    private const float SwatchAvatarFraction = 0.30f;
    private const float SwatchSelectedStroke = 2f;
    private const float SwatchCheck = 18f;
    private const float BadgeGlyph = 24f;
    private const float BadgePillWidth = 74f;
    private const float NameColorMarkerRadius = 15f;
    private const float NameColorGlyphScale = 0.55f;
    private const int NameColorSlot = 1;

    private readonly Dictionary<string, BadgeStyle> itemBadgeStyles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FrameStyle> itemFrameStyles = new(StringComparer.Ordinal);
    private InventorySectionDto[]? styledSections;
    private InventoryCounters slotsText;

    private struct InventoryCounters
    {
        public Windows.Widgets.CachedText Frames;
        public Windows.Widgets.CachedText Badges;
    }

    private void DrawInventory(in PhoneContext context)
    {
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (UiScope.PushId("coin.items"))
        using (var surface = AppSurface.Begin(navBar.Body))
        {
            inventory.EnsureFresh();
            frameCatalog.EnsureFresh();
            inventoryRefresh.Draw(navBar.Body, surface.Pull, surface.Dragging, inventory.Fetching, ui.MutedInk,
                RefreshInventory);
            if (!inventory.LoadedOnce)
            {
                LoadingPulse.Draw(navBar.Body.Center, 16f * UiScale.Current, ui.Palette.Accent, ui.MutedInk,
                    LoadingPulse.SafeLabel());
            }
            else
            {
                DrawInventoryBody();
            }
        }

        AppHeader.EndLargeTitle(in navBar, context, "coin.items.nav", TabTitle(CoinTab.Items), NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty);
    }

    private void DrawInventoryBody()
    {
        SyncItemStyles();
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var cursorY = DrawLook(drawList, origin, width, scale);
        cursorY = DrawFrameSection(drawList, new Vector2(origin.X, cursorY), width, scale);
        cursorY = DrawBadgeSection(drawList, new Vector2(origin.X, cursorY), width, scale);
        CoinArt.Reserve(origin, width, cursorY + CoinArt.BottomPad * scale);
    }

    private void SyncItemStyles()
    {
        var sections = inventory.Sections;
        if (ReferenceEquals(sections, styledSections))
        {
            return;
        }

        styledSections = sections;
        itemBadgeStyles.Clear();
        itemFrameStyles.Clear();
        for (var sectionIndex = 0; sectionIndex < sections.Length; sectionIndex++)
        {
            var items = sections[sectionIndex].Items;
            for (var itemIndex = 0; itemIndex < items.Length; itemIndex++)
            {
                var item = items[itemIndex];
                if (item.Badge is not null)
                {
                    itemBadgeStyles[item.Id] = BadgeStyle.From(item.Badge);
                }

                if (item.Frame is not null)
                {
                    itemFrameStyles[item.Id] = FrameStyle.From(item.Frame);
                }
            }
        }
    }

    private BadgeStyle? BadgeFor(InventoryItemDto item) =>
        itemBadgeStyles.TryGetValue(item.Id, out var style) ? style : null;

    private FrameStyle? FrameFor(InventoryItemDto item) =>
        itemFrameStyles.TryGetValue(item.Id, out var style) ? style : null;

    private float DrawLook(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var max = new Vector2(origin.X + width, origin.Y + LookHeight * scale);
        CoinArt.Card(drawList, ui, origin, max, scale);
        var light = RoleInk.IsLight(theme);
        var avatarRadius = LookAvatar * scale;
        var worn = WornFrame();
        var frame = worn is null ? null : FrameFor(worn);
        var avatarCenter = new Vector2(origin.X + width * 0.5f,
            origin.Y + Metrics.Space.Lg * scale + avatarRadius * (frame?.Scale ?? 1f));
        DrawBloom(drawList, new Rect(origin, max), avatarCenter, width * 0.36f, avatarRadius * 1.6f, ui.Accent);
        var user = session.CurrentUser;
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, user?.Name ?? string.Empty,
            user?.World ?? string.Empty, user?.AvatarUrl, images, lodestone, 1.2f, 48, 1f, frame);

        var nameBadge = WornBadge(NameColorSlot);
        var secondBadge = WornBadge(NameColorSlot + 1);
        var nameStyle = TextStyles.Title3;
        var nameHeight = Typography.LineHeight(nameStyle);
        var glyph = nameHeight * GlyphFraction;
        var gap = LookBadgeGap * scale;
        var badgeCount = (nameBadge is null ? 0 : 1) + (secondBadge is null ? 0 : 1);
        var badgesWidth = badgeCount * (glyph + gap);
        var name = Typography.FitText(PreviewName(), MathF.Max(1f, width - Metrics.Space.Lg * scale * 2f - badgesWidth),
            nameStyle);
        var nameWidth = Typography.Measure(name, nameStyle).X;
        var nameTop = max.Y - Metrics.Space.Lg * scale - nameHeight;
        var left = origin.X + (width - nameWidth - badgesWidth) * 0.5f;
        var badgeStyle = nameBadge is null ? null : BadgeFor(nameBadge);
        var ink = badgeStyle is null ? ui.TitleInk : RoleInk.For(badgeStyle.Colors[0], light);
        if (badgeStyle is null)
        {
            Typography.Draw(drawList, new Vector2(left, nameTop), name, ink, nameStyle);
        }
        else
        {
            Typography.Draw(drawList, new Vector2(left, nameTop), name, ink, nameStyle, NameEffects.For(badgeStyle, light));
        }

        var badgeX = left + nameWidth + gap + glyph * 0.5f;
        var badgeY = nameTop + nameHeight * 0.5f;
        if (nameBadge is not null)
        {
            BadgeStrip.DrawOne(drawList, new Vector2(badgeX, badgeY), BadgeFor(nameBadge), images, light, glyph);
            badgeX += glyph + gap;
        }

        if (secondBadge is not null)
        {
            BadgeStrip.DrawOne(drawList, new Vector2(badgeX, badgeY), BadgeFor(secondBadge), images, light, glyph);
        }

        return max.Y;
    }

    private InventoryItemDto? WornFrame()
    {
        var section = inventory.Section(LoadoutStore.FrameKind);
        if (section is null)
        {
            return null;
        }

        for (var index = 0; index < section.Items.Length; index++)
        {
            if (section.Items[index].Slot > 0)
            {
                return section.Items[index];
            }
        }

        return null;
    }

    private InventoryItemDto? WornBadge(int slot)
    {
        var section = inventory.Section(LoadoutStore.BadgeKind);
        if (section is null)
        {
            return null;
        }

        for (var index = 0; index < section.Items.Length; index++)
        {
            if (section.Items[index].Slot == slot)
            {
                return section.Items[index];
            }
        }

        return null;
    }

    private float DrawInventoryHeader(ImDrawListPtr drawList, Vector2 origin, float width, string title,
        ref Windows.Widgets.CachedText counterCache, int worn, int slots, float scale)
    {
        var key = ((long)worn << 16) | (uint)slots;
        var counter = counterCache.IsCurrent(key)
            ? counterCache.Value
            : counterCache.Store(key, Loc.T(L.Loadout.SlotsUsed, worn, slots));
        var counterSize = Typography.Measure(counter, TextStyles.Subheadline);
        var height = CoinArt.SectionHeader(drawList, origin, width, title, ui.TitleInk,
            counterSize.X + CoinArt.ValueGap * scale, scale);
        if (slots > 0)
        {
            Typography.Draw(drawList, new Vector2(origin.X + width - counterSize.X, origin.Y + (height - counterSize.Y) * 0.5f),
                counter, ui.MutedInk, TextStyles.Subheadline);
        }

        return origin.Y + height + CoinArt.HeaderGap * scale;
    }

    private float DrawFrameSection(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var section = inventory.Section(LoadoutStore.FrameKind);
        var items = section?.Items ?? Array.Empty<InventoryItemDto>();
        var cursorY = DrawInventoryHeader(drawList, new Vector2(origin.X, origin.Y + CoinArt.SectionGap * scale),
            width, Loc.T(L.Loadout.FramesTitle), ref slotsText.Frames, WornCount(items), section?.Slots ?? 0, scale);
        if (items.Length == 0)
        {
            return DrawInventoryEmpty(new Vector2(origin.X, cursorY), width, FontAwesomeIcon.UserCircle,
                Loc.T(L.Loadout.FramesEmpty), Loc.T(L.Loadout.FramesEmptyHint), "coin.items.frames.shop", scale);
        }

        var gap = SwatchGap * scale;
        var cellWidth = (width - gap * (SwatchColumns - 1)) / SwatchColumns;
        var labelHeight = Typography.LineHeight(TextStyles.Footnote);
        var cellHeight = cellWidth + SwatchLabelGap * scale + labelHeight;
        var total = items.Length + 1;
        var wornId = WornFrame()?.Id ?? string.Empty;
        for (var index = 0; index < total; index++)
        {
            var column = index % SwatchColumns;
            var row = index / SwatchColumns;
            var min = new Vector2(origin.X + column * (cellWidth + gap), cursorY + row * (cellHeight + gap));
            if (index == 0)
            {
                DrawFrameSwatch(drawList, min, cellWidth, null, wornId.Length == 0, wornId, scale);
                continue;
            }

            var item = items[index - 1];
            DrawFrameSwatch(drawList, min, cellWidth, item, item.Slot > 0, wornId, scale);
        }

        var rows = (total + SwatchColumns - 1) / SwatchColumns;
        return cursorY + rows * (cellHeight + gap) - gap;
    }

    private void DrawFrameSwatch(ImDrawListPtr drawList, Vector2 min, float size, InventoryItemDto? item,
        bool selected, string wornId, float scale)
    {
        var max = min + new Vector2(size, size);
        var hovered = !selected && !inventory.Equipping && UiInteract.Hover(min, max);
        var press = PressFx.Scale(ImGui.GetID(item?.Id ?? "coin.frame.none"),
            hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left), Core.Animation.Motion.PressScaleControl);
        var center = (min + max) * 0.5f;
        var half = new Vector2(size, size) * 0.5f * press;
        var drawMin = center - half;
        var drawMax = center + half;
        var radius = Metrics.Radius.Grouped * scale;
        CoinArt.Card(drawList, ui, drawMin, drawMax, scale);
        if (hovered)
        {
            Squircle.Fill(drawList, drawMin, drawMax, radius, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (item is null)
        {
            drawList.AddCircleFilled(center, size * SwatchAvatarFraction, ImGui.GetColorU32(ui.Palette.FieldSurface), 48);
            ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.Ban, ui.MutedInk, size * SwatchAvatarFraction);
        }
        else
        {
            var style = FrameFor(item);
            var avatarRadius = size * SwatchAvatarFraction * press;
            var user = session.CurrentUser;
            AvatarView.DrawRemote(drawList, center, avatarRadius, theme, user?.Name ?? string.Empty,
                user?.World ?? string.Empty, user?.AvatarUrl, images, lodestone, 0.8f, 40, 1f, style);
        }

        if (selected)
        {
            Squircle.Stroke(drawList, drawMin, drawMax, radius, ImGui.GetColorU32(ui.Accent),
                SwatchSelectedStroke * scale);
            var check = SwatchCheck * scale;
            var checkCenter = new Vector2(drawMax.X - check * 0.5f - Metrics.Space.Xs * scale,
                drawMin.Y + check * 0.5f + Metrics.Space.Xs * scale);
            drawList.AddCircleFilled(checkCenter, check * 0.5f, ImGui.GetColorU32(ui.Accent), 24);
            ProgressRing.CenterIcon(drawList, checkCenter, FontAwesomeIcon.Check, CoinArt.White, check * 0.5f);
        }

        var label = item is null ? Loc.T(L.Loadout.NoneOption) : FrameFor(item)?.Name ?? string.Empty;
        if (label.Length > 0)
        {
            var fitted = Typography.FitText(label, size, TextStyles.Footnote);
            var labelWidth = Typography.Measure(fitted, TextStyles.Footnote).X;
            Typography.Draw(drawList, new Vector2(min.X + (size - labelWidth) * 0.5f, max.Y + SwatchLabelGap * scale),
                fitted, selected ? ui.TitleInk : ui.MutedInk, TextStyles.Footnote);
        }

        if (!UiInteract.Click(min, max, hovered))
        {
            return;
        }

        UiFeedback.Play(UiSound.ToggleOn);
        if (item is null)
        {
            inventory.Equip(LoadoutStore.FrameKind, wornId, 0);
            return;
        }

        inventory.Equip(LoadoutStore.FrameKind, item.Id, FrameSlot);
    }

    private float DrawBadgeSection(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var section = inventory.Section(LoadoutStore.BadgeKind);
        var items = section?.Items ?? Array.Empty<InventoryItemDto>();
        var cursorY = DrawInventoryHeader(drawList, new Vector2(origin.X, origin.Y + CoinArt.SectionGap * scale),
            width, Loc.T(L.Loadout.BadgesTitle), ref slotsText.Badges, WornCount(items), section?.Slots ?? 0, scale);
        if (items.Length == 0)
        {
            return DrawInventoryEmpty(new Vector2(origin.X, cursorY), width, FontAwesomeIcon.Star,
                Loc.T(L.Loadout.BadgesEmpty), Loc.T(L.Loadout.BadgesEmptyHint), "coin.items.badges.shop", scale);
        }

        cursorY += Typography.DrawWrappedLeft(new Vector2(origin.X, cursorY), Loc.T(L.Loadout.BadgesHint), ui.MutedInk,
            TextStyles.Footnote, width);
        cursorY += Metrics.Space.Sm * scale;
        var rowHeight = CoinArt.RowHeight * scale;
        var min = new Vector2(origin.X, cursorY);
        var max = new Vector2(origin.X + width, cursorY + rowHeight * items.Length);
        CoinArt.Card(drawList, ui, min, max, scale);
        for (var index = 0; index < items.Length; index++)
        {
            var top = cursorY + index * rowHeight;
            var row = new Rect(new Vector2(min.X, top), new Vector2(max.X, top + rowHeight));
            if (index > 0)
            {
                CoinArt.Hairline(drawList, ui, row.Min.X + (Metrics.Space.Lg + BadgeGlyph + CoinArt.TextGap) * scale,
                    row.Max.X - Metrics.Space.Lg * scale, top);
            }

            DrawBadgeRow(drawList, items[index], row, index, scale);
        }

        return max.Y;
    }

    private void DrawBadgeRow(ImDrawListPtr drawList, InventoryItemDto item, Rect row, int index, float scale)
    {
        var light = RoleInk.IsLight(theme);
        var worn = item.Slot > 0;
        var pad = Metrics.Space.Lg * scale;
        var glyph = BadgeGlyph * scale;
        var center = new Vector2(row.Min.X + pad + glyph * 0.5f, row.Center.Y);
        var style = BadgeFor(item);
        BadgeStrip.DrawOne(drawList, center, style, images, light, glyph);

        var pillWidth = BadgePillWidth * scale;
        var pillHeight = CoinArt.CapsuleHeight * scale;
        var pill = new Rect(new Vector2(row.Max.X - pad - pillWidth, row.Center.Y - pillHeight * 0.5f),
            new Vector2(row.Max.X - pad, row.Center.Y + pillHeight * 0.5f));
        var labelRight = worn ? DrawNameColorMarker(item, pill, index, scale) : pill.Min.X;
        var labelLeft = center.X + glyph * 0.5f + CoinArt.TextGap * scale;
        var labelWidth = MathF.Max(1f, labelRight - CoinArt.ValueGap * scale - labelLeft);
        var name = style?.Name ?? string.Empty;
        var labelHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        Marquee.DrawLeftAuto(new MarqueeId("inventory.badge.", index), name, labelLeft,
            row.Center.Y - labelHeight * 0.5f, labelWidth, TextStyles.BodyEmphasized, ui.TitleInk);

        var label = worn ? NumberText.Group(item.Slot) : Loc.T(L.Loadout.Wear);
        if (CoinArt.Capsule(drawList, ui, ImGui.GetID(item.Id), pill, label,
                worn ? CapsuleTone.Filled : CapsuleTone.Tinted, !inventory.Equipping))
        {
            UiFeedback.Play(worn ? UiSound.ToggleOff : UiSound.ToggleOn);
            inventory.Equip(LoadoutStore.BadgeKind, item.Id, worn ? 0 : null);
        }
    }

    private float DrawNameColorMarker(InventoryItemDto item, Rect pill, int index, float scale)
    {
        var radius = NameColorMarkerRadius * scale;
        var center = new Vector2(pill.Min.X - Metrics.Space.Sm * scale - radius, pill.Center.Y);
        var glyph = IconGlyph.Of(FontAwesomeIcon.Palette);
        if (item.Slot == NameColorSlot)
        {
            AppSkin.Icon(center, glyph, ui.Palette.Accent, NameColorGlyphScale);
            HoverTooltip.Show(new Rect(center - new Vector2(radius, radius),
                center + new Vector2(radius, radius)), Loc.T(L.Loadout.ColorsName));
            return center.X - radius;
        }

        if (ui.IconButton(center, radius, glyph, ui.MutedInk, ui.Palette.FieldSurface, NameColorGlyphScale,
                Loc.T(L.Loadout.UseForNameColor)))
        {
            UiFeedback.Play(UiSound.ToggleOn);
            inventory.Equip(LoadoutStore.BadgeKind, item.Id, NameColorSlot);
        }

        return center.X - radius;
    }

    private float DrawInventoryEmpty(Vector2 origin, float width, FontAwesomeIcon icon, string title, string hint,
        string actionId, float scale)
    {
        var bottom = CoinArt.DrawPanel(ui, origin, width, icon, ui.Accent, title, hint, scale);
        var height = CoinArt.LargeCapsuleHeight * scale;
        var top = bottom + Metrics.Space.Md * scale;
        var rect = new Rect(new Vector2(origin.X, top), new Vector2(origin.X + width, top + height));
        if (CoinArt.Capsule(ImGui.GetWindowDrawList(), ui, ImGui.GetID(actionId), rect, Loc.T(L.Coin.BrowseShop),
                CapsuleTone.Tinted, true))
        {
            UiFeedback.Play(UiSound.Tap);
            SelectTab(CoinTab.Shop);
        }

        return rect.Max.Y;
    }

    private static int WornCount(InventoryItemDto[] items)
    {
        var worn = 0;
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].Slot > 0)
            {
                worn++;
            }
        }

        return worn;
    }
}
