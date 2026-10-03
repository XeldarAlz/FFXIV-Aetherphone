using Aetherphone.Core;
using Aetherphone.Core.Calendar;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Calendar;

internal sealed partial class CalendarApp
{
    private const int GroupNameMaxLength = 30;
    private const int GroupCardRows = 3;
    private const float GroupIconRadius = 14f;
    private const float GroupIconGlyphScale = 0.8f;
    private const int GroupNameKey = 0;
    private const int GroupAppToggleKey = 1;
    private const int GroupWidgetToggleKey = 2;
    private const string GameEventsKey = "game";

    private readonly Dictionary<Guid, string[]> groupKeys = new();
    private Guid editGroupId;
    private bool editGroupIsNew;
    private string editGroupName = string.Empty;

    private void DrawGroups(Rect content, float scale)
    {
        var context = new PhoneContext(content, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Calendar.Groups), back);
        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + AppHeader.Height * scale), content.Max);
        using (AppSurface.Begin(body))
        {
            DrawGameEventsCard();
            var groups = configuration.CalendarGroups;
            for (var index = 0; index < groups.Count; index++)
            {
                ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
                DrawGroupCard(groups[index], scale);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            var origin = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;
            var pillHeight = Metrics.Size.Row * scale;
            var pillRect = new Rect(origin, new Vector2(origin.X + width, origin.Y + pillHeight));
            if (ui.AccentPill(pillRect, Loc.T(L.Calendar.NewGroup), true, TextStyles.Headline))
            {
                StartNewGroup();
            }

            ImGui.Dummy(new Vector2(width, pillHeight + Metrics.Space.Lg * scale));
        }
    }

    private void DrawGameEventsCard()
    {
        var card = GroupCard.Begin(ui, GroupCardRows);
        var heading = card.NextRow();
        DrawGroupName(heading, Loc.T(L.Calendar.GameEvents), GameEventsKey, heading.Width);

        var showInApp = SettingsRow.Bool(card.NextRow(), Loc.T(L.Calendar.ShowInApp),
            configuration.CalendarGameEventsInApp, theme, "calendar.group.app.game");
        if (showInApp != configuration.CalendarGameEventsInApp)
        {
            configuration.CalendarGameEventsInApp = showInApp;
            SaveCalendar();
        }

        var showInWidget = SettingsRow.Bool(card.NextRow(), Loc.T(L.Calendar.ShowInWidget),
            configuration.CalendarGameEventsInWidget, theme, "calendar.group.widget.game");
        if (showInWidget != configuration.CalendarGameEventsInWidget)
        {
            configuration.CalendarGameEventsInWidget = showInWidget;
            SaveCalendar();
        }

        card.End();
    }

    private void DrawGroupCard(CalendarEventGroup group, float scale)
    {
        var keys = KeysFor(group.Id);
        var card = GroupCard.Begin(ui, GroupCardRows);
        var heading = card.NextRow();
        var radius = GroupIconRadius * scale;
        var deleteCenter = new Vector2(heading.Max.X - radius, heading.Center.Y);
        var renameCenter = new Vector2(deleteCenter.X - radius * 2f - Metrics.Space.Sm * scale, heading.Center.Y);
        var nameMaxWidth = MathF.Max(1f, renameCenter.X - radius - Metrics.Space.Md * scale - heading.Min.X);
        DrawGroupName(heading, group.Name, keys[GroupNameKey], nameMaxWidth);

        if (ui.IconButton(renameCenter, radius, IconGlyph.Of(FontAwesomeIcon.Pen), ui.MutedInk, AppSkin.Transparent,
                GroupIconGlyphScale, Loc.T(L.Calendar.RenameGroup)))
        {
            StartRenameGroup(group);
        }

        if (ui.IconButton(deleteCenter, radius, IconGlyph.Of(FontAwesomeIcon.Trash), ui.MutedInk, AppSkin.Transparent,
                GroupIconGlyphScale, Loc.T(L.Calendar.DeleteGroup)))
        {
            AskDeleteGroup(group.Id);
        }

        var showInApp = SettingsRow.Bool(card.NextRow(), Loc.T(L.Calendar.ShowInApp), group.ShowInApp, theme,
            keys[GroupAppToggleKey]);
        if (showInApp != group.ShowInApp)
        {
            group.ShowInApp = showInApp;
            SaveCalendar();
        }

        var showInWidget = SettingsRow.Bool(card.NextRow(), Loc.T(L.Calendar.ShowInWidget), group.ShowInWidget, theme,
            keys[GroupWidgetToggleKey]);
        if (showInWidget != group.ShowInWidget)
        {
            group.ShowInWidget = showInWidget;
            SaveCalendar();
        }

        card.End();
    }

    private void DrawGroupName(Rect row, string name, string key, float maxWidth)
    {
        var size = Typography.Measure(name, TextStyles.BodyEmphasized);
        Marquee.DrawLeftAuto(new MarqueeId("calendar.group.", key), name, row.Min.X, row.Center.Y - size.Y * 0.5f,
            maxWidth, TextStyles.BodyEmphasized, ui.TitleInk);
    }

    private string[] KeysFor(Guid groupId)
    {
        if (groupKeys.TryGetValue(groupId, out var keys))
        {
            return keys;
        }

        var stem = groupId.ToString("N");
        keys = new[]
        {
            stem,
            string.Concat("calendar.group.app.", stem),
            string.Concat("calendar.group.widget.", stem),
        };
        groupKeys[groupId] = keys;
        return keys;
    }

    private void StartNewGroup()
    {
        editGroupIsNew = true;
        editGroupId = Guid.Empty;
        editGroupName = string.Empty;
        router.Push(CalendarScreen.EditGroup);
    }

    private void StartRenameGroup(CalendarEventGroup group)
    {
        editGroupIsNew = false;
        editGroupId = group.Id;
        editGroupName = group.Name;
        router.Push(CalendarScreen.EditGroup);
    }

    private void DrawGroupEditor(Rect content, float scale)
    {
        var context = new PhoneContext(content, theme, navigation);
        AppHeader.Draw(context, Loc.T(editGroupIsNew ? L.Calendar.NewGroup : L.Calendar.RenameGroup), back);
        var margin = Metrics.Space.Lg * scale;
        var left = content.Min.X + margin;
        var right = content.Max.X - margin;
        var top = content.Min.Y + AppHeader.Height * scale + margin;
        var fieldHeight = EditorFieldHeight * scale;

        var nameRect = new Rect(new Vector2(left, top), new Vector2(right, top + fieldHeight));
        DrawTextField(nameRect, scale, "##calGroupName", Loc.T(L.Calendar.GroupNamePlaceholder), ref editGroupName,
            GroupNameMaxLength);

        var saveRect = new Rect(new Vector2(left, content.Max.Y - margin - fieldHeight),
            new Vector2(right, content.Max.Y - margin));
        var enabled = HasText(editGroupName);
        if (ui.AccentPill(saveRect, Loc.T(L.Calendar.Save), enabled, TextStyles.Headline) && enabled)
        {
            CommitGroup();
        }
    }

    private void CommitGroup()
    {
        var name = editGroupName.Trim();
        if (name.Length == 0)
        {
            return;
        }

        if (editGroupIsNew)
        {
            configuration.CalendarGroups.Add(new CalendarEventGroup { Name = name });
        }
        else
        {
            var group = FindGroup(editGroupId);
            if (group is not null)
            {
                group.Name = name;
            }
        }

        SaveCalendar();
        router.Pop();
    }

    private CalendarEventGroup? FindGroup(Guid groupId)
    {
        var groups = configuration.CalendarGroups;
        for (var index = 0; index < groups.Count; index++)
        {
            if (groups[index].Id == groupId)
            {
                return groups[index];
            }
        }

        return null;
    }

    private void AskDeleteGroup(Guid groupId)
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Calendar.DeleteGroupConfirmMessage),
            ConfirmLabel = Loc.T(L.Calendar.DeleteConfirm),
            CancelLabel = Loc.T(L.Calendar.DeleteCancel),
            Sheet = true,
            Confirm = () => DeleteGroup(groupId),
        });
    }

    private void DeleteGroup(Guid groupId)
    {
        configuration.CalendarGroups.RemoveAll(group => group.Id == groupId);
        var customEvents = configuration.CalendarCustomEvents;
        for (var index = 0; index < customEvents.Count; index++)
        {
            if (customEvents[index].GroupId == groupId)
            {
                customEvents[index].GroupId = Guid.Empty;
            }
        }

        SaveCalendar();
    }
}
