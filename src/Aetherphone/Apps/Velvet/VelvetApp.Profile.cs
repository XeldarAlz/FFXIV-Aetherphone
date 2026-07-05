using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Messaging;
using Aetherphone.Core.Net;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Velvet;

// The "me" profile screen, settings, the edit-profile form (with chip pickers, suggestion chips
// and the avatar editor) and tag rendering. Split from the main hub/timeline for readability.
internal sealed partial class VelvetApp
{
    private void DrawMe(Rect area)
    {
        var me = store.Me;
        if (me is null)
        {
            store.EnsureMe();
            Typography.DrawCentered(area.Center, Loc.T(L.Common.Loading), AppPalettes.Velvet.MutedInk);
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        using (AppSurface.Begin(area))
        {
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;
            var centerX = origin.X + width * 0.5f;
            var avatarRadius = 66f * scale;
            var avatarCenter = new Vector2(centerX, origin.Y + 18f * scale + avatarRadius);
            drawList.AddCircleFilled(avatarCenter, avatarRadius + 3f * scale, ImGui.GetColorU32(theme.AppBackground),
                72);
            AvatarView.Draw(drawList, avatarCenter, avatarRadius, Accent, MonogramFor(me), 2.2f, AvatarFor(me), 72);
            var y = avatarCenter.Y + avatarRadius + 16f * scale;
            var displayName = string.IsNullOrEmpty(me.DisplayName) ? me.Handle : me.DisplayName;
            y += DrawCenteredLine(drawList, centerX, y, displayName, theme.TextStrong, 1.45f, FontWeight.SemiBold) +
                 3f * scale;
            var meta = me.Handle.Length > 0 ? $"@{me.Handle}" : me.World;
            if (meta.Length > 0)
            {
                y += DrawCenteredLine(drawList, centerX, y, meta, AppPalettes.Velvet.MutedInk, 0.92f, FontWeight.Regular) +
                     2f * scale;
            }

            var lookingLine = VelvetLookingFor.Label(me.LookingFor);
            if (me.RelationshipStatus != VelvetRelationship.NotSaying)
            {
                lookingLine += $"  ·  {VelvetRelationship.Label(me.RelationshipStatus)}";
            }

            y += DrawCenteredLine(drawList, centerX, y, lookingLine, Palette.Mix(Accent, theme.TextStrong, 0.35f),
                0.92f, FontWeight.Medium);
            var contentWidth = width - 24f * scale;
            if (me.Intro.Length > 0 || me.Dynamic.Length > 0 || me.Tags.Length > 0)
            {
                y += 20f * scale;
                drawList.AddLine(new Vector2(origin.X, y), new Vector2(origin.X + width, y),
                    ImGui.GetColorU32(theme.Separator), 1f);
                y += 20f * scale;
            }

            if (me.Intro.Length > 0)
            {
                y += UiText.WrappedCentered(centerX, y, me.Intro, contentWidth, AppPalettes.Velvet.BodyInk, scale, 1.02f) +
                     14f * scale;
            }

            if (me.Dynamic.Length > 0 || me.Tags.Length > 0)
            {
                y += DrawCenteredChips(centerX, y, contentWidth, me.Dynamic, me.Tags) + 6f * scale;
            }

            y += 26f * scale;
            var buttonHeight = 44f * scale;
            var editRect = new Rect(new Vector2(origin.X, y), new Vector2(origin.X + width, y + buttonHeight));
            if (ui.PillButton(editRect, Loc.T(L.Velvet.EditProfile), true))
            {
                router.Push(VelvetRoute.EditProfile);
            }

            y += buttonHeight + 12f * scale;
            var settingsRect = new Rect(new Vector2(origin.X, y), new Vector2(origin.X + width, y + buttonHeight));
            if (ui.GhostButton(settingsRect, Loc.T(L.Velvet.Settings)))
            {
                router.Push(VelvetRoute.Settings);
            }

            y += buttonHeight;
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, y - origin.Y + 40f * scale));
        }
    }

    private void DrawSettings(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Velvet.Settings), back);
        var me = store.Me;
        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 6f * scale));
            ui.SectionLabel(Loc.T(L.Velvet.DiscoverableLabel));
            ui.HelpText(Loc.T(L.Velvet.AppearHelp));
            ImGui.Dummy(new Vector2(0f, 6f * scale));
            if (me is not null)
            {
                var discoverable = me.Discoverable;
                ui.ToggleRow(Loc.T(L.Velvet.DiscoverableLabel), ref discoverable);
                if (discoverable != me.Discoverable && !editBusy)
                {
                    editBusy = true;
                    store.UpdateProfile(
                        new UpdateVelvetProfileRequest(null, null, null, null, null, null, null, discoverable),
                        _ => editBusy = false);
                }
            }

            ImGui.Dummy(new Vector2(0f, 30f * scale));
        }
    }

    private void DrawEditProfile(Rect area)
    {
        var me = store.Me;
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Velvet.EditProfile), back);
        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        if (me is null)
        {
            store.EnsureMe();
            Typography.DrawCentered(body.Center, Loc.T(L.Common.Loading), AppPalettes.Velvet.MutedInk);
            return;
        }

        if (editOutcome == 1)
        {
            editOutcome = 0;
            router.Pop();
            return;
        }

        if (editOutcome == 2)
        {
            editOutcome = 0;
        }

        if (editLoadedFor != me.UserId)
        {
            editLoadedFor = me.UserId;
            editDisplayName = me.DisplayName;
            editHandle = me.Handle;
            editIntro = me.Intro;
            editPronouns = me.Pronouns;
            editVibe = me.Dynamic;
            editTags = VelvetTags.Join(me.Tags);
            editLimits = VelvetTags.Join(me.Limits);
            editLookingFor = me.LookingFor;
            editRelationship = me.RelationshipStatus;
            editDiscoverable = me.Discoverable;
        }

        if (ui.HeaderAction(area, editBusy ? Loc.T(L.Velvet.Saving) : Loc.T(L.Velvet.Save), !editBusy))
        {
            SaveProfile();
        }

        using (AppSurface.Begin(body))
        {
            DrawAvatarEditor(me);
            ImGui.Dummy(new Vector2(0f, 18f * scale));
            ui.SectionLabel(Loc.T(L.Velvet.IdentityHeader));
            ui.Field(Loc.T(L.Velvet.DisplayNameLabel), "##vName", ref editDisplayName, ShortFieldMax, false);
            ImGui.Dummy(new Vector2(0f, 10f * scale));
            ui.Field(Loc.T(L.Velvet.HandleLabel), "##vHandle", ref editHandle, HandleMax, false);
            ImGui.Dummy(new Vector2(0f, 18f * scale));
            ui.SectionLabel(Loc.T(L.Velvet.AboutHeader));
            ui.Field(Loc.T(L.Velvet.IntroLabel), "##vIntro", ref editIntro, IntroMax, true);
            ImGui.Dummy(new Vector2(0f, 10f * scale));
            ui.Field(Loc.T(L.Velvet.PronounsLabel), "##vPronouns", ref editPronouns, ShortFieldMax, false);
            ImGui.Dummy(new Vector2(0f, 16f * scale));
            ui.SectionLabel(Loc.T(L.Velvet.DynamicLabel));
            ui.Field(Loc.T(L.Velvet.DynamicLabel), "##vVibe", ref editVibe, ShortFieldMax, false);
            DrawSuggestionRow(VibeSuggestions, ref editVibe, false);
            ImGui.Dummy(new Vector2(0f, 8f * scale));
            ui.Field(Loc.T(L.Velvet.TagsLabel), "##vTags", ref editTags, TagsMax, false);
            DrawSuggestionRow(TagSuggestions, ref editTags, true);
            ImGui.Dummy(new Vector2(0f, 16f * scale));
            ui.SectionLabel(Loc.T(L.Velvet.WantHeader));
            DrawChipPicker(Loc.T(L.Velvet.LookingForLabel), VelvetLookingFor.All, ref editLookingFor, true);
            ImGui.Dummy(new Vector2(0f, 10f * scale));
            DrawChipPicker(Loc.T(L.Velvet.RelationshipLabel), VelvetRelationship.All, ref editRelationship, false);
            ImGui.Dummy(new Vector2(0f, 12f * scale));
            ui.Field(Loc.T(L.Velvet.LimitsLabel), "##vLimits", ref editLimits, TagsMax, false);
            ImGui.Dummy(new Vector2(0f, 30f * scale));
        }
    }

    private void SaveProfile()
    {
        if (editBusy)
        {
            return;
        }

        editBusy = true;
        var request = new UpdateVelvetProfileRequest(editIntro.Trim(), editPronouns.Trim(), editVibe.Trim(),
            VelvetTags.Parse(editTags), VelvetTags.Parse(editLimits), editLookingFor, editRelationship,
            editDiscoverable);
        store.UpdateIdentity(editDisplayName.Trim(), editHandle.Trim(), identityOk =>
        {
            if (!identityOk)
            {
                editBusy = false;
                editOutcome = 2;
                return;
            }

            store.UpdateProfile(request, ok =>
            {
                editBusy = false;
                editOutcome = ok ? 1 : 2;
            });
        });
    }

    private void DrawAvatarEditor(VelvetProfileDto me)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var centerX = origin.X + width * 0.5f;
        var radius = 62f * scale;
        var center = new Vector2(centerX, origin.Y + 16f * scale + radius);
        drawList.AddCircleFilled(center, radius + 3f * scale, ImGui.GetColorU32(theme.AppBackground), 72);
        AvatarView.Draw(drawList, center, radius, Accent, MonogramFor(me), 2.1f, AvatarFor(me), 72);
        var badge = new Vector2(center.X + radius * 0.72f, center.Y + radius * 0.72f);
        drawList.AddCircleFilled(badge, 15f * scale, ImGui.GetColorU32(Accent), 24);
        drawList.AddCircle(badge, 15f * scale, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.85f)), 24, 1.5f * scale);
        AppSkin.Icon(badge, FontAwesomeIcon.Camera.ToIconString(), new Vector4(1f, 1f, 1f, 1f), 0.78f);
        if (UiInteract.HoverClick(center - new Vector2(radius, radius), center + new Vector2(radius, radius)))
        {
            OpenAvatarPicker();
        }

        var y = center.Y + radius + 14f * scale;
        var buttonWidth = 170f * scale;
        var buttonHeight = 34f * scale;
        var buttonRect = new Rect(new Vector2(centerX - buttonWidth * 0.5f, y),
            new Vector2(centerX + buttonWidth * 0.5f, y + buttonHeight));
        if (ui.GhostButton(buttonRect, Loc.T(L.Velvet.ChangePhoto)))
        {
            OpenAvatarPicker();
        }

        y += buttonHeight;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, y - origin.Y + 4f * scale));
    }

    private void OpenAvatarPicker()
    {
        avatar.Open();
        router.Push(VelvetRoute.Avatar);
    }

    private void DrawAvatar(Rect area)
    {
        if (avatar.Draw(area, ui, new PhoneContext(area, theme, navigation)))
        {
            router.Pop();
        }
    }

    private void DrawChipPicker(string label, int[] values, ref int selected, bool skipFirst)
    {
        var scale = ImGuiHelpers.GlobalScale;
        using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Velvet.MutedInk))
        {
            ImGui.TextUnformatted(label);
        }

        var origin = ImGui.GetCursorScreenPos();
        var cursorX = origin.X;
        var cursorY = origin.Y + 2f * scale;
        var maxX = origin.X + ImGui.GetContentRegionAvail().X;
        var chipHeight = 30f * scale;
        for (var index = skipFirst ? 1 : 0; index < values.Length; index++)
        {
            var value = values[index];
            var text = label == Loc.T(L.Velvet.RelationshipLabel)
                ? VelvetRelationship.Label(value)
                : VelvetLookingFor.Label(value);
            var chipWidth = Typography.Measure(text, 0.85f, FontWeight.Medium).X + 22f * scale;
            if (cursorX + chipWidth > maxX)
            {
                cursorX = origin.X;
                cursorY += chipHeight + 6f * scale;
            }

            var rect = new Rect(new Vector2(cursorX, cursorY), new Vector2(cursorX + chipWidth, cursorY + chipHeight));
            if (ui.Chip(rect, text, selected == value))
            {
                selected = value;
            }

            cursorX += chipWidth + 6f * scale;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, cursorY - origin.Y + chipHeight + 4f * scale));
    }

    private void DrawSuggestionRow(string[] suggestions, ref string field, bool commaSeparated)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.Dummy(new Vector2(0f, 4f * scale));
        ui.HelpText(Loc.T(L.Velvet.Suggestions));
        var origin = ImGui.GetCursorScreenPos();
        DrawSuggestionChips(origin, ImGui.GetContentRegionAvail().X, suggestions, ref field, commaSeparated);
    }

    private void DrawSuggestionChips(Vector2 origin, float maxWidth, string[] suggestions, ref string field,
        bool commaSeparated = true)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var cursorX = origin.X;
        var cursorY = origin.Y + 2f * scale;
        var chipHeight = 28f * scale;
        for (var index = 0; index < suggestions.Length; index++)
        {
            var tag = suggestions[index];
            var chipWidth = Typography.Measure(tag, 0.8f).X + 20f * scale;
            if (cursorX + chipWidth > origin.X + maxWidth)
            {
                cursorX = origin.X;
                cursorY += chipHeight + 6f * scale;
            }

            var rect = new Rect(new Vector2(cursorX, cursorY), new Vector2(cursorX + chipWidth, cursorY + chipHeight));
            var present = field.Contains(tag, StringComparison.OrdinalIgnoreCase);
            if (ui.Chip(rect, tag, present) && !present)
            {
                field = AppendToken(field, tag, commaSeparated);
            }

            cursorX += chipWidth + 6f * scale;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(maxWidth, cursorY - origin.Y + chipHeight + 4f * scale));
    }

    private static string AppendToken(string field, string token, bool commaSeparated)
    {
        var trimmed = field.Trim();
        if (!commaSeparated)
        {
            return token;
        }

        if (trimmed.Length == 0)
        {
            return token;
        }

        return trimmed.TrimEnd(',') + ", " + token;
    }

    private void DrawInlineField(string id, ref string value, int maxLength, bool multiline, Rect rect, string hint)
    {
        var scale = ImGuiHelpers.GlobalScale;
        Squircle.Fill(ImGui.GetWindowDrawList(), rect.Min, new Vector2(rect.Max.X, rect.Max.Y), 9f * scale,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));
        ImGui.SetCursorScreenPos(new Vector2(rect.Min.X + 12f * scale,
            rect.Min.Y + (multiline ? 8f * scale : rect.Height * 0.5f - ImGui.GetFrameHeight() * 0.5f)));
        ImGui.SetNextItemWidth(rect.Width - 24f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Velvet.TitleInk))
        {
            if (multiline)
            {
                ImGui.InputTextMultiline(id, ref value, maxLength,
                    new Vector2(rect.Width - 24f * scale, rect.Height - 16f * scale), ImGuiInputTextFlags.None);
            }
            else
            {
                ImGui.InputTextWithHint(id, hint, ref value, maxLength, ImGuiInputTextFlags.None);
            }
        }
    }

    private void DrawTagChips(string[] tags)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var cursorX = origin.X;
        var cursorY = origin.Y + 2f * scale;
        var maxX = origin.X + ImGui.GetContentRegionAvail().X;
        var chipHeight = 24f * scale;
        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < tags.Length; index++)
        {
            var label = tags[index];
            var width = Typography.Measure(label, 0.8f).X + 18f * scale;
            if (cursorX + width > maxX)
            {
                cursorX = origin.X;
                cursorY += chipHeight + 6f * scale;
            }

            var chipMin = new Vector2(cursorX, cursorY);
            var chipMax = new Vector2(cursorX + width, cursorY + chipHeight);
            Squircle.Fill(drawList, chipMin, chipMax, chipHeight * 0.5f,
                ImGui.GetColorU32(Palette.WithAlpha(Accent, 0.18f)));
            Typography.DrawCentered(new Vector2((chipMin.X + chipMax.X) * 0.5f, (chipMin.Y + chipMax.Y) * 0.5f), label,
                new Vector4(0.99f, 0.80f, 0.88f, 1f), 0.8f, FontWeight.Medium);
            cursorX += width + 6f * scale;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, cursorY - origin.Y + chipHeight + 4f * scale));
    }

    private void DrawTagsLine(Vector2 position, string[] tags)
    {
        if (tags.Length == 0)
        {
            return;
        }

        var text = "#" + string.Join(" #", tags);
        Typography.Draw(position, UiText.Truncate(text, 40), Palette.Mix(Accent, theme.TextStrong, 0.3f), 0.78f,
            FontWeight.Medium);
    }
}
