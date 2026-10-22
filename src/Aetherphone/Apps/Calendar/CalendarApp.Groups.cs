using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Calendar;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Calendar;

internal sealed partial class CalendarApp
{
    private const int GroupNameMaxLength = 30;
    private const float GroupHeaderHeight = 64f;
    private const float GroupTileSize = 40f;
    private const float GroupGlyphSize = 18f;
    private const float TileGap = 10f;
    private const float SwatchRadius = 14f;
    private const float SwatchRowHeight = 64f;
    private const string GameAppKey = "calendar.game.app";
    private const string GameWidgetKey = "calendar.game.widget";
    private const string EditorAppKey = "calendar.groupEditor.app";
    private const string EditorWidgetKey = "calendar.groupEditor.widget";

    private static readonly Vector4 GameTint = new(0.345f, 0.337f, 0.839f, 1f);
    private static readonly string[] SwatchIds = BuildSwatchIds();

    private readonly NavBarButton[] groupsButtons = new NavBarButton[1];
    private readonly Dictionary<Guid, GroupKeys> groupKeys = new();
    private string groupsBackTitle = string.Empty;
    private Guid editGroupId;
    private bool editGroupIsNew;
    private string editGroupName = string.Empty;
    private int editGroupColor;
    private bool editGroupInApp;
    private bool editGroupInWidget;

    private sealed class GroupKeys
    {
        public readonly string App;
        public readonly string Widget;
        public CachedText Count;

        public GroupKeys(Guid groupId)
        {
            var stem = groupId.ToString("N");
            App = string.Concat("calendar.group.app.", stem);
            Widget = string.Concat("calendar.group.widget.", stem);
        }
    }

    private void DrawGroups(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context);
        var scale = UiScale.Current;
        using (AppSurface.Begin(navBar.Body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = DrawGameGroupCard(drawList, origin, width, scale);
            var groups = configuration.CalendarGroups;
            for (var index = 0; index < groups.Count; index++)
            {
                cursorY = DrawGroupCard(drawList, new Vector2(origin.X, cursorY + CalendarArt.CardGap * scale), width,
                    groups[index], scale);
            }

            if (groups.Count == 0)
            {
                cursorY = DrawGroupsHint(drawList, new Vector2(origin.X, cursorY + CalendarArt.SectionGap * scale),
                    width, scale);
            }

            CalendarArt.Reserve(origin, width, cursorY + CalendarArt.BottomPad * scale);
        }

        groupsButtons[0] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.Plus), Loc.T(L.Calendar.NewGroup));
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "calendar.groups.nav", Loc.T(L.Calendar.Groups),
            NavBarStyle.From(ui), groupsButtons, groupsBackTitle, back);
        if (pressed == 0)
        {
            StartNewGroup();
        }
    }

    private float DrawGameGroupCard(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var max = new Vector2(origin.X + width, origin.Y + GroupCardHeight(scale));
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var header = new Rect(origin, new Vector2(max.X, origin.Y + GroupHeaderHeight * scale));
        DrawGroupHeader(drawList, header, GameTint, FontAwesomeIcon.Crown, Loc.T(L.Calendar.GameEvents),
            Loc.T(L.Calendar.GameEventsBody), false, scale);
        var showInApp = configuration.CalendarGameEventsInApp;
        var showInWidget = configuration.CalendarGameEventsInWidget;
        DrawVisibilityTiles(drawList, origin.X, header.Max.Y, width, GameAppKey, GameWidgetKey, ref showInApp, ref showInWidget,
            GameTint, scale);
        if (showInApp != configuration.CalendarGameEventsInApp ||
            showInWidget != configuration.CalendarGameEventsInWidget)
        {
            configuration.CalendarGameEventsInApp = showInApp;
            configuration.CalendarGameEventsInWidget = showInWidget;
            SaveCalendar();
        }

        return max.Y;
    }

    private float DrawGroupCard(ImDrawListPtr drawList, Vector2 origin, float width, CalendarEventGroup group,
        float scale)
    {
        var max = new Vector2(origin.X + width, origin.Y + GroupCardHeight(scale));
        if (!ImGui.IsRectVisible(origin, max))
        {
            return max.Y;
        }

        var keys = KeysFor(group.Id);
        var color = CalendarColors.Resolve(group.ColorIndex, ui.Accent);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var header = new Rect(origin, new Vector2(max.X, origin.Y + GroupHeaderHeight * scale));
        if (DrawGroupHeader(drawList, header, color, FontAwesomeIcon.CalendarAlt, group.Name,
                CountLabel(keys, group.Id), true, scale))
        {
            StartEditGroup(group);
        }

        var showInApp = group.ShowInApp;
        var showInWidget = group.ShowInWidget;
        DrawVisibilityTiles(drawList, origin.X, header.Max.Y, width, keys.App, keys.Widget, ref showInApp, ref showInWidget, color,
            scale);
        if (showInApp != group.ShowInApp || showInWidget != group.ShowInWidget)
        {
            group.ShowInApp = showInApp;
            group.ShowInWidget = showInWidget;
            SaveCalendar();
        }

        return max.Y;
    }

    private static float GroupCardHeight(float scale) =>
        (GroupHeaderHeight + CalendarArt.ToggleTileHeight + Metrics.Space.Lg) * scale;

    private bool DrawGroupHeader(ImDrawListPtr drawList, Rect header, Vector4 color, FontAwesomeIcon icon,
        string title, string subtitle, bool editable, float scale)
    {
        var hovered = editable && CalendarArt.RowWash(drawList, ui, header, scale);
        var pad = Metrics.Space.Lg * scale;
        var tileSize = GroupTileSize * scale;
        var tileMin = new Vector2(header.Min.X + pad, header.Center.Y - tileSize * 0.5f);
        var tileMax = tileMin + new Vector2(tileSize, tileSize);
        IconTile.FillShaded(drawList, tileMin, tileMax, tileSize * Metrics.Radius.TileFactor, IconTile.Surface(color));
        ProgressRing.CenterIcon(drawList, (tileMin + tileMax) * 0.5f, icon, AccentRing.Ink, GroupGlyphSize * scale);
        var trailing = editable
            ? CalendarArt.Trailing(drawList, ui, header.Max.X - pad, header.Center.Y, string.Empty, ui.MutedInk, true,
                scale)
            : 0f;
        CalendarArt.Labels(drawList, tileMax.X + CalendarArt.TextGap * scale,
            header.Max.X - pad - trailing - Metrics.Space.Sm * scale, header.Center.Y, title, subtitle, ui.TitleInk,
            ui.MutedInk, scale);
        return editable && UiInteract.Click(header.Min, header.Max, hovered);
    }

    private void DrawVisibilityTiles(ImDrawListPtr drawList, float left, float top, float width, string appKey,
        string widgetKey, ref bool showInApp, ref bool showInWidget, Vector4 tint, float scale)
    {
        var pad = Metrics.Space.Lg * scale;
        var gap = TileGap * scale;
        var tileWidth = (width - pad * 2f - gap) * 0.5f;
        var tileHeight = CalendarArt.ToggleTileHeight * scale;
        var appRect = new Rect(new Vector2(left + pad, top), new Vector2(left + pad + tileWidth, top + tileHeight));
        var widgetRect = new Rect(new Vector2(appRect.Max.X + gap, top),
            new Vector2(appRect.Max.X + gap + tileWidth, top + tileHeight));
        if (CalendarArt.ToggleTile(drawList, ui, appKey, appRect, FontAwesomeIcon.CalendarAlt,
                Loc.T(L.Calendar.ShowInApp), showInApp, tint, scale))
        {
            showInApp = !showInApp;
        }

        if (CalendarArt.ToggleTile(drawList, ui, widgetKey, widgetRect, FontAwesomeIcon.ThLarge,
                Loc.T(L.Calendar.ShowInWidget), showInWidget, tint, scale))
        {
            showInWidget = !showInWidget;
        }
    }

    private float DrawGroupsHint(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var title = Loc.T(L.Calendar.GroupsHintTitle);
        var body = Loc.T(L.Calendar.GroupsHintBody);
        var height = CalendarArt.PanelHeight(title, body, width, scale);
        CalendarArt.Panel(drawList, ui, origin, width, height, FontAwesomeIcon.LayerGroup, ui.Accent, title, body,
            scale);
        var pillTop = origin.Y + height + CalendarArt.SectionGap * scale;
        var pill = new Rect(new Vector2(origin.X, pillTop),
            new Vector2(origin.X + width, pillTop + CalendarArt.PillHeight * scale));
        if (ui.AccentPill(pill, Loc.T(L.Calendar.NewGroup), true, TextStyles.Headline))
        {
            StartNewGroup();
        }

        return pill.Max.Y;
    }

    private GroupKeys KeysFor(Guid groupId)
    {
        if (groupKeys.TryGetValue(groupId, out var keys))
        {
            return keys;
        }

        keys = new GroupKeys(groupId);
        groupKeys[groupId] = keys;
        return keys;
    }

    private string CountLabel(GroupKeys keys, Guid groupId)
    {
        var count = 0;
        var customEvents = configuration.CalendarCustomEvents;
        for (var index = 0; index < customEvents.Count; index++)
        {
            if (customEvents[index].GroupId == groupId)
            {
                count++;
            }
        }

        return keys.Count.IsCurrent(count) ? keys.Count.Value : keys.Count.Store(count, Loc.Plural(L.Calendar.EventCount, count));
    }

    private void StartNewGroup()
    {
        editGroupIsNew = true;
        editGroupId = Guid.Empty;
        editGroupName = string.Empty;
        editGroupColor = CalendarColors.NextFree(configuration.CalendarGroups);
        editGroupInApp = true;
        editGroupInWidget = true;
        Push(CalendarScreen.EditGroup);
    }

    private void StartEditGroup(CalendarEventGroup group)
    {
        editGroupIsNew = false;
        editGroupId = group.Id;
        editGroupName = group.Name;
        editGroupColor = group.ColorIndex;
        editGroupInApp = group.ShowInApp;
        editGroupInWidget = group.ShowInWidget;
        Push(CalendarScreen.EditGroup);
    }

    private void DrawGroupEditor(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context);
        var scale = UiScale.Current;
        using (AppSurface.Begin(navBar.Body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = DrawGroupNameCard(drawList, origin, width, scale);
            cursorY = DrawSwatchCard(drawList, new Vector2(origin.X, cursorY + CalendarArt.CardGap * scale), width,
                scale);
            var tilesTop = cursorY + CalendarArt.CardGap * scale;
            var tilesCard = new Vector2(origin.X + width,
                tilesTop + (CalendarArt.ToggleTileHeight + Metrics.Space.Lg * 2f) * scale);
            ui.Card(drawList, new Vector2(origin.X, tilesTop), tilesCard, Metrics.Radius.Grouped * scale);
            DrawVisibilityTiles(drawList, origin.X, tilesTop + Metrics.Space.Lg * scale, width, EditorAppKey, EditorWidgetKey,
                ref editGroupInApp, ref editGroupInWidget, CalendarColors.Resolve(editGroupColor, ui.Accent), scale);
            cursorY = tilesCard.Y;
            if (!editGroupIsNew)
            {
                var deleteTop = cursorY + CalendarArt.SectionGap * scale;
                var deleteRow = new Rect(new Vector2(origin.X, deleteTop),
                    new Vector2(origin.X + width, deleteTop + CalendarArt.FieldRowHeight * scale));
                ui.Card(drawList, deleteRow.Min, deleteRow.Max, Metrics.Radius.Grouped * scale);
                var hovered = CalendarArt.RowWash(drawList, ui, deleteRow, scale);
                Typography.DrawCentered(drawList, deleteRow.Center, Loc.T(L.Calendar.DeleteGroup), ui.Theme.Danger,
                    TextStyles.Body);
                if (UiInteract.Click(deleteRow.Min, deleteRow.Max, hovered))
                {
                    AskDeleteGroup(editGroupId);
                }

                cursorY = deleteRow.Max.Y;
            }

            var saveTop = cursorY + CalendarArt.SectionGap * scale;
            var saveRect = new Rect(new Vector2(origin.X, saveTop),
                new Vector2(origin.X + width, saveTop + CalendarArt.PillHeight * scale));
            var enabled = HasText(editGroupName);
            if (ui.AccentPill(saveRect, Loc.T(editGroupIsNew ? L.Calendar.AddGroup : L.Calendar.Save), enabled,
                    TextStyles.Headline))
            {
                CommitGroup();
            }

            CalendarArt.Reserve(origin, width, saveRect.Max.Y + CalendarArt.BottomPad * scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, "calendar.groupEditor.nav",
            Loc.T(editGroupIsNew ? L.Calendar.NewGroup : L.Calendar.EditGroup), NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty, Loc.T(L.Calendar.Groups), back);
    }

    private float DrawGroupNameCard(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var rowHeight = CalendarArt.FieldRowHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + rowHeight);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var pad = Metrics.Space.Lg * scale;
        var color = CalendarColors.Resolve(editGroupColor, ui.Accent);
        var dotRadius = DotRadius * scale * 1.4f;
        var dotCenter = new Vector2(origin.X + pad + dotRadius, origin.Y + rowHeight * 0.5f);
        drawList.AddCircleFilled(dotCenter, dotRadius, ImGui.GetColorU32(color), 24);
        var fieldLeft = dotCenter.X + dotRadius + Metrics.Space.Md * scale;
        ImGui.SetCursorScreenPos(new Vector2(fieldLeft, origin.Y + rowHeight * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(max.X - pad - fieldLeft);
        using (UiScope.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (UiScope.PushColor(ImGuiCol.FrameBgHovered, AppSkin.Transparent))
        using (UiScope.PushColor(ImGuiCol.FrameBgActive, AppSkin.Transparent))
        using (UiScope.PushColor(ImGuiCol.Text, ui.TitleInk))
        using (UiScope.PushColor(ImGuiCol.TextDisabled, ui.MutedInk))
        {
            var hint = Loc.T(L.Calendar.GroupNamePlaceholder);
            Plugin.Fonts.NoticeText(hint);
            Plugin.Fonts.NoticeText(editGroupName);
            ImGui.InputTextWithHint("##calendarGroupName", hint, ref editGroupName, GroupNameMaxLength);
        }

        return max.Y;
    }

    private float DrawSwatchCard(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var max = new Vector2(origin.X + width, origin.Y + SwatchRowHeight * scale);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var pad = Metrics.Space.Lg * scale;
        var count = CalendarColors.Count;
        var step = (width - pad * 2f) / count;
        var radius = MathF.Min(SwatchRadius * scale, step * 0.36f);
        var centerY = origin.Y + SwatchRowHeight * scale * 0.5f;
        using (UiScope.PushId("calendar.swatches"))
        {
            for (var index = 0; index < count; index++)
            {
                var center = new Vector2(origin.X + pad + step * (index + 0.5f), centerY);
                if (CalendarArt.Swatch(drawList, ui, SwatchIds[index], center, radius, CalendarColors.At(index),
                        editGroupColor == index, scale))
                {
                    editGroupColor = index;
                }
            }
        }

        return max.Y;
    }

    private static string[] BuildSwatchIds()
    {
        var ids = new string[CalendarColors.Count];
        for (var index = 0; index < ids.Length; index++)
        {
            ids[index] = string.Concat("swatch", index.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return ids;
    }

    private void CommitGroup()
    {
        var name = editGroupName.Trim();
        if (name.Length == 0)
        {
            return;
        }

        var group = editGroupIsNew ? new CalendarEventGroup() : FindGroup(editGroupId);
        if (group is null)
        {
            router.Pop();
            return;
        }

        group.Name = name;
        group.ColorIndex = editGroupColor;
        group.ShowInApp = editGroupInApp;
        group.ShowInWidget = editGroupInWidget;
        if (editGroupIsNew)
        {
            configuration.CalendarGroups.Add(group);
        }

        SaveCalendar();
        UiFeedback.Play(UiSound.Success);
        router.Pop();
    }

    private void AskDeleteGroup(Guid groupId)
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Calendar.DeleteGroupConfirmMessage),
            ConfirmLabel = Loc.T(L.Calendar.DeleteGroup),
            CancelLabel = Loc.T(L.Calendar.DeleteCancel),
            Sheet = true,
            Confirm = () => DeleteGroup(groupId),
        });
    }

    private void DeleteGroup(Guid groupId)
    {
        var groups = configuration.CalendarGroups;
        for (var index = groups.Count - 1; index >= 0; index--)
        {
            if (groups[index].Id == groupId)
            {
                groups.RemoveAt(index);
            }
        }

        var customEvents = configuration.CalendarCustomEvents;
        for (var index = 0; index < customEvents.Count; index++)
        {
            if (customEvents[index].GroupId == groupId)
            {
                customEvents[index].GroupId = Guid.Empty;
            }
        }

        groupKeys.Remove(groupId);
        SaveCalendar();
        if (router.Current == CalendarScreen.EditGroup)
        {
            router.Pop();
        }
    }
}
