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
using Aetherphone.Core.Social;
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
            var meta = SocialIdentity.ProfileMeta(me.Handle, SocialRegion.EffectiveCode(configuration, gameData));
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
                y += DrawCenteredChips(centerX, y, contentWidth, SplitTokens(me.Dynamic), me.Tags) + 6f * scale;
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

            ImGui.Dummy(new Vector2(0f, 24f * scale));
            ui.SectionLabel(Loc.T(L.Velvet.SafetyLabel));
            ImGui.Dummy(new Vector2(0f, 6f * scale));
            if (DrawNavRow(Loc.T(L.Velvet.BlockedUsers)))
            {
                router.Push(VelvetRoute.Blocked);
            }

            ImGui.Dummy(new Vector2(0f, 30f * scale));
        }
    }

    private bool DrawNavRow(string label)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 40f * scale;
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(origin, new Vector2(origin.X + width, origin.Y + height));
        if (hovered)
        {
            Squircle.Fill(drawList, origin, new Vector2(origin.X + width, origin.Y + height), 12f * scale,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.05f)));
        }

        Typography.Draw(new Vector2(origin.X + 4f * scale, origin.Y + height * 0.5f - 8f * scale), label,
            theme.TextStrong, 0.95f);
        AppSkin.Icon(new Vector2(origin.X + width - 12f * scale, origin.Y + height * 0.5f),
            FontAwesomeIcon.ChevronRight.ToIconString(), AppPalettes.Velvet.MutedInk, 0.72f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        return UiInteract.HoverClick(origin, new Vector2(origin.X + width, origin.Y + height));
    }

    private void DrawBlocked(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Velvet.BlockedUsers), back);
        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        if (!store.BlockedLoaded && !store.LoadingBlocked)
        {
            store.RefreshBlocked();
        }

        var list = store.Blocked;
        using (AppSurface.Begin(body))
        {
            if (list.Length == 0)
            {
                var message = store.LoadingBlocked ? Loc.T(L.Common.Loading) : Loc.T(L.Velvet.BlockedEmpty);
                Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 60f * scale), message,
                    AppPalettes.Velvet.MutedInk);
            }
            else
            {
                ImGui.Dummy(new Vector2(0f, 6f * scale));
                for (var index = 0; index < list.Length; index++)
                {
                    DrawBlockedRow(list[index]);
                }

                ImGui.Dummy(new Vector2(0f, 16f * scale));
            }
        }
    }

    private void DrawBlockedRow(UserDto user)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rowHeight = 60f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var radius = 20f * scale;
        var avatarCenter = new Vector2(origin.X + radius, origin.Y + rowHeight * 0.5f);
        AvatarView.Draw(ImGui.GetWindowDrawList(), avatarCenter, radius, Accent, Monogram(user.DisplayName, user.Handle),
            0.95f, lodestone.Remote(user.Id, ToUri(user.AvatarUrl)), 32);
        var textLeft = origin.X + radius * 2f + 12f * scale;
        var displayName = string.IsNullOrEmpty(user.DisplayName) ? user.Handle : user.DisplayName;
        Typography.Draw(new Vector2(textLeft, origin.Y + 11f * scale), displayName, theme.TextStrong, 1f,
            FontWeight.SemiBold);
        if (user.Handle.Length > 0)
        {
            Typography.Draw(new Vector2(textLeft, origin.Y + 31f * scale), $"@{user.Handle}", AppPalettes.Velvet.MutedInk,
                0.82f);
        }

        var label = Loc.T(L.Velvet.Unblock);
        var buttonHeight = 30f * scale;
        var buttonWidth = MathF.Max(92f * scale, Typography.Measure(label, 0.9f, FontWeight.SemiBold).X + 24f * scale);
        var buttonMin = new Vector2(origin.X + width - buttonWidth, origin.Y + rowHeight * 0.5f - buttonHeight * 0.5f);
        var buttonRect = new Rect(buttonMin, new Vector2(buttonMin.X + buttonWidth, buttonMin.Y + buttonHeight));
        if (ui.PillButton(buttonRect, label, false))
        {
            store.Unblock(user.Id);
        }

        if (UiInteract.HoverClick(origin, new Vector2(buttonMin.X - 6f * scale, origin.Y + rowHeight)))
        {
            OpenProfile(user.Id);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
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
            editVibeAdd = string.Empty;
            editTags = VelvetTags.Join(me.Tags);
            editTagsAdd = string.Empty;
            editLimits = VelvetTags.Join(me.Limits);
            editLimitsAdd = string.Empty;
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
            ImGui.Dummy(new Vector2(0f, 18f * scale));
            DrawTokenCard(FontAwesomeIcon.Fire, Loc.T(L.Velvet.DynamicLabel), Loc.T(L.Velvet.VibeCardHelp),
                ref editVibe, ref editVibeAdd, VibeSuggestions, Loc.T(L.Velvet.AddVibeHint), "##vVibeAdd", VibeMax, false);
            ImGui.Dummy(new Vector2(0f, 12f * scale));
            DrawTokenCard(FontAwesomeIcon.Hashtag, Loc.T(L.Velvet.TagsHeading), Loc.T(L.Velvet.TagsCardHelp),
                ref editTags, ref editTagsAdd, TagSuggestions, Loc.T(L.Velvet.AddTagHint), "##vTagsAdd", TagsMax, false);
            ImGui.Dummy(new Vector2(0f, 12f * scale));
            DrawChoiceCard(FontAwesomeIcon.Compass, Loc.T(L.Velvet.LookingForLabel), Loc.T(L.Velvet.LookingForHelp),
                VelvetLookingFor.All, ref editLookingFor, true, false);
            ImGui.Dummy(new Vector2(0f, 12f * scale));
            DrawChoiceCard(FontAwesomeIcon.Heart, Loc.T(L.Velvet.RelationshipLabel), Loc.T(L.Velvet.RelationshipHelp),
                VelvetRelationship.All, ref editRelationship, false, true);
            ImGui.Dummy(new Vector2(0f, 12f * scale));
            DrawTokenCard(FontAwesomeIcon.ShieldAlt, Loc.T(L.Velvet.LimitsLabel), Loc.T(L.Velvet.LimitsCardHelp),
                ref editLimits, ref editLimitsAdd, LimitSuggestions, Loc.T(L.Velvet.AddLimitHint), "##vLimitsAdd", TagsMax, true);
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

    private void DrawTokenCard(FontAwesomeIcon icon, string title, string help, ref string field, ref string addBuffer,
        string[] suggestions, string addHint, string addId, int maxTotal, bool safety)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = 14f * scale;
        var innerX = origin.X + pad;
        var innerWidth = width - pad * 2f;
        var accent = safety ? theme.Danger : Accent;
        var tokens = SplitTokens(field);
        var hasPicks = tokens.Length > 0;

        var headerH = 22f * scale;
        var afterHeader = 8f * scale;
        var helpH = Typography.MeasureWrapped(help, innerWidth, 0.82f);
        var afterHelp = 12f * scale;
        var labelH = 15f * scale;
        var afterLabel = 6f * scale;
        var pillsH = hasPicks ? MeasurePills(tokens, innerWidth) : 0f;
        var afterPicks = 12f * scale;
        var addH = 34f * scale;
        var afterAdd = 12f * scale;
        var suggH = MeasureChips(suggestions, innerWidth, 0.85f, 13f * scale, 30f * scale);

        var contentH = pad + headerH + afterHeader + helpH + afterHelp +
                       (hasPicks ? labelH + afterLabel + pillsH + afterPicks : 0f) +
                       addH + afterAdd + labelH + afterLabel + suggH + pad;
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + contentH), 18f * scale);

        var y = origin.Y + pad;
        DrawCardHeader(drawList, new Vector2(innerX, y), icon, title, accent);
        y += headerH + afterHeader;
        DrawWrappedHelp(innerX, y, innerWidth, help);
        y += helpH + afterHelp;
        if (hasPicks)
        {
            DrawMiniLabel(new Vector2(innerX, y), Loc.T(L.Velvet.SelectedLabel));
            y += labelH + afterLabel;
            var removed = DrawPills(new Vector2(innerX, y), innerWidth, tokens, accent);
            if (removed >= 0)
            {
                field = RemoveTokenAt(field, removed);
            }

            y += pillsH + afterPicks;
        }

        DrawAddInput(new Vector2(innerX, y), innerWidth, addId, addHint, ref addBuffer, ref field, maxTotal);
        y += addH + afterAdd;
        DrawMiniLabel(new Vector2(innerX, y), Loc.T(L.Velvet.SuggestionsLabel));
        y += labelH + afterLabel;
        DrawSuggestions(new Vector2(innerX, y), innerWidth, suggestions, ref field, maxTotal);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, contentH));
    }

    private void DrawChoiceCard(FontAwesomeIcon icon, string title, string help, int[] values, ref int selected,
        bool skipFirst, bool relationship)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = 14f * scale;
        var innerX = origin.X + pad;
        var innerWidth = width - pad * 2f;
        var start = skipFirst ? 1 : 0;

        var headerH = 22f * scale;
        var afterHeader = 8f * scale;
        var helpH = Typography.MeasureWrapped(help, innerWidth, 0.82f);
        var afterHelp = 12f * scale;
        var chipsH = MeasureChoice(values, start, relationship, innerWidth);

        var contentH = pad + headerH + afterHeader + helpH + afterHelp + chipsH + pad;
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + contentH), 18f * scale);

        var y = origin.Y + pad;
        DrawCardHeader(drawList, new Vector2(innerX, y), icon, title, Accent);
        y += headerH + afterHeader;
        DrawWrappedHelp(innerX, y, innerWidth, help);
        y += helpH + afterHelp;
        var picked = DrawChoice(new Vector2(innerX, y), innerWidth, values, start, relationship, selected);
        if (picked >= 0)
        {
            selected = picked;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, contentH));
    }

    private void DrawCardHeader(ImDrawListPtr drawList, Vector2 pos, FontAwesomeIcon icon, string title, Vector4 accent)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var tileSize = 22f * scale;
        var tileMin = pos;
        var tileMax = new Vector2(pos.X + tileSize, pos.Y + tileSize);
        Squircle.Fill(drawList, tileMin, tileMax, 7f * scale, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.20f)));
        AppSkin.Icon((tileMin + tileMax) * 0.5f, icon.ToIconString(), accent, 0.72f);
        var titleSize = Typography.Measure(title, 0.98f, FontWeight.SemiBold);
        Typography.Draw(new Vector2(tileMax.X + 10f * scale, pos.Y + tileSize * 0.5f - titleSize.Y * 0.5f), title,
            AppPalettes.Velvet.TitleInk, 0.98f, FontWeight.SemiBold);
    }

    private void DrawWrappedHelp(float x, float y, float wrapWidth, string text)
    {
        ImGui.SetCursorScreenPos(new Vector2(x, y));
        ImGui.PushTextWrapPos(x + wrapWidth - ImGui.GetWindowPos().X);
        using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Velvet.MutedInk))
        using (Plugin.Fonts.Push(0.82f))
        {
            ImGui.TextWrapped(text);
        }

        ImGui.PopTextWrapPos();
    }

    private static void DrawMiniLabel(Vector2 pos, string text)
    {
        Typography.Draw(pos, text.ToUpperInvariant(), AppPalettes.Velvet.HeaderInk, 0.72f, FontWeight.SemiBold);
    }

    private int DrawPills(Vector2 origin, float maxWidth, string[] tokens, Vector4 accent)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var pillHeight = 28f * scale;
        Span<float> widths = stackalloc float[tokens.Length];
        for (var index = 0; index < tokens.Length; index++)
        {
            widths[index] = PillWidth(tokens[index], scale);
        }

        Span<Vector2> positions = stackalloc Vector2[tokens.Length];
        FlowLayout(widths, maxWidth, pillHeight, 8f * scale, 8f * scale, positions);
        var removed = -1;
        for (var index = 0; index < tokens.Length; index++)
        {
            var min = origin + positions[index];
            var max = new Vector2(min.X + widths[index], min.Y + pillHeight);
            var hovered = ImGui.IsMouseHoveringRect(min, max);
            Squircle.Fill(drawList, min, max, pillHeight * 0.5f,
                ImGui.GetColorU32(Palette.WithAlpha(accent, hovered ? 0.42f : 0.30f)));
            var textSize = Typography.Measure(tokens[index], 0.82f, FontWeight.Medium);
            Typography.Draw(new Vector2(min.X + 12f * scale, min.Y + pillHeight * 0.5f - textSize.Y * 0.5f),
                tokens[index], new Vector4(1f, 0.96f, 0.98f, 1f), 0.82f, FontWeight.Medium);
            AppSkin.Icon(new Vector2(max.X - 16f * scale, min.Y + pillHeight * 0.5f),
                FontAwesomeIcon.Times.ToIconString(), new Vector4(1f, 0.92f, 0.95f, hovered ? 1f : 0.72f), 0.58f);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                removed = index;
            }
        }

        return removed;
    }

    private void DrawSuggestions(Vector2 origin, float maxWidth, string[] labels, ref string field, int maxTotal)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var chipHeight = 30f * scale;
        var padX = 13f * scale;
        Span<float> widths = stackalloc float[labels.Length];
        for (var index = 0; index < labels.Length; index++)
        {
            widths[index] = Typography.Measure(labels[index], 0.85f, FontWeight.Medium).X + padX * 2f;
        }

        Span<Vector2> positions = stackalloc Vector2[labels.Length];
        FlowLayout(widths, maxWidth, chipHeight, 8f * scale, 8f * scale, positions);
        var toggled = -1;
        for (var index = 0; index < labels.Length; index++)
        {
            var min = origin + positions[index];
            var max = new Vector2(min.X + widths[index], min.Y + chipHeight);
            if (ui.Chip(new Rect(min, max), labels[index], HasToken(field, labels[index])))
            {
                toggled = index;
            }
        }

        if (toggled >= 0)
        {
            field = ToggleToken(field, labels[toggled], maxTotal);
        }
    }

    private void DrawAddInput(Vector2 origin, float width, string id, string hint, ref string buffer, ref string field,
        int maxTotal)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var height = 34f * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        Squircle.Fill(drawList, origin, max, 9f * scale, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.06f)));
        Squircle.Stroke(drawList, origin, max, 9f * scale, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)), 1f);
        AppSkin.Icon(new Vector2(origin.X + 16f * scale, origin.Y + height * 0.5f), FontAwesomeIcon.Plus.ToIconString(),
            AppPalettes.Velvet.MutedInk, 0.66f);
        ImGui.SetCursorScreenPos(new Vector2(origin.X + 30f * scale,
            origin.Y + height * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(width - 42f * scale);
        if (pendingTokenFocus == id)
        {
            ImGui.SetKeyboardFocusHere();
            pendingTokenFocus = null;
        }

        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Velvet.TitleInk))
        {
            if (ImGui.InputTextWithHint(id, hint, ref buffer, 40, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                CommitTokens(ref field, ref buffer, maxTotal);
                pendingTokenFocus = id;
            }
        }
    }

    private int DrawChoice(Vector2 origin, float maxWidth, int[] values, int start, bool relationship, int selected)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var chipHeight = 34f * scale;
        var count = values.Length - start;
        Span<float> widths = stackalloc float[count];
        for (var index = 0; index < count; index++)
        {
            widths[index] = ChoiceWidth(values[start + index], relationship, scale);
        }

        Span<Vector2> positions = stackalloc Vector2[count];
        FlowLayout(widths, maxWidth, chipHeight, 8f * scale, 8f * scale, positions);
        var picked = -1;
        for (var index = 0; index < count; index++)
        {
            var value = values[start + index];
            var label = relationship ? VelvetRelationship.Label(value) : VelvetLookingFor.Label(value);
            var glyph = relationship ? RelationshipIcon(value) : LookingForIcon(value);
            var active = selected == value;
            var min = origin + positions[index];
            var max = new Vector2(min.X + widths[index], min.Y + chipHeight);
            var hovered = ImGui.IsMouseHoveringRect(min, max);
            var fill = active
                ? Palette.WithAlpha(Accent, 0.28f)
                : new Vector4(1f, 1f, 1f, hovered ? 0.14f : 0.08f);
            Squircle.Fill(drawList, min, max, chipHeight * 0.5f, ImGui.GetColorU32(fill));
            if (active)
            {
                Squircle.Stroke(drawList, min, max, chipHeight * 0.5f, ImGui.GetColorU32(Accent), 1.4f);
            }

            var ink = active ? new Vector4(0.99f, 0.85f, 0.91f, 1f) : AppPalettes.Velvet.BodyInk;
            var iconInk = active ? new Vector4(0.99f, 0.85f, 0.91f, 1f) : AppPalettes.Velvet.MutedInk;
            AppSkin.Icon(new Vector2(min.X + 18f * scale, min.Y + chipHeight * 0.5f), glyph.ToIconString(), iconInk, 0.72f);
            var labelSize = Typography.Measure(label, 0.85f, FontWeight.Medium);
            Typography.Draw(new Vector2(min.X + 32f * scale, min.Y + chipHeight * 0.5f - labelSize.Y * 0.5f), label, ink,
                0.85f, FontWeight.Medium);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                picked = value;
            }
        }

        return picked;
    }

    private static float MeasurePills(string[] tokens, float maxWidth)
    {
        var scale = ImGuiHelpers.GlobalScale;
        Span<float> widths = stackalloc float[tokens.Length];
        for (var index = 0; index < tokens.Length; index++)
        {
            widths[index] = PillWidth(tokens[index], scale);
        }

        Span<Vector2> positions = stackalloc Vector2[tokens.Length];
        return FlowLayout(widths, maxWidth, 28f * scale, 8f * scale, 8f * scale, positions);
    }

    private static float MeasureChips(string[] labels, float maxWidth, float fontScale, float padX, float chipHeight)
    {
        var scale = ImGuiHelpers.GlobalScale;
        Span<float> widths = stackalloc float[labels.Length];
        for (var index = 0; index < labels.Length; index++)
        {
            widths[index] = Typography.Measure(labels[index], fontScale, FontWeight.Medium).X + padX * 2f;
        }

        Span<Vector2> positions = stackalloc Vector2[labels.Length];
        return FlowLayout(widths, maxWidth, chipHeight, 8f * scale, 8f * scale, positions);
    }

    private static float MeasureChoice(int[] values, int start, bool relationship, float maxWidth)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var count = values.Length - start;
        Span<float> widths = stackalloc float[count];
        for (var index = 0; index < count; index++)
        {
            widths[index] = ChoiceWidth(values[start + index], relationship, scale);
        }

        Span<Vector2> positions = stackalloc Vector2[count];
        return FlowLayout(widths, maxWidth, 34f * scale, 8f * scale, 8f * scale, positions);
    }

    private static float ChoiceWidth(int value, bool relationship, float scale)
    {
        var label = relationship ? VelvetRelationship.Label(value) : VelvetLookingFor.Label(value);
        return 32f * scale + Typography.Measure(label, 0.85f, FontWeight.Medium).X + 14f * scale;
    }

    private static float PillWidth(string token, float scale) =>
        12f * scale + Typography.Measure(token, 0.82f, FontWeight.Medium).X + 6f * scale + 12f * scale + 10f * scale;

    private static float FlowLayout(ReadOnlySpan<float> widths, float maxWidth, float chipHeight, float rowGap,
        float chipGap, Span<Vector2> positions)
    {
        if (widths.Length == 0)
        {
            return 0f;
        }

        var x = 0f;
        var y = 0f;
        for (var index = 0; index < widths.Length; index++)
        {
            if (index > 0)
            {
                if (x + chipGap + widths[index] <= maxWidth)
                {
                    x += chipGap;
                }
                else
                {
                    x = 0f;
                    y += chipHeight + rowGap;
                }
            }

            positions[index] = new Vector2(x, y);
            x += widths[index];
        }

        return y + chipHeight;
    }

    private static FontAwesomeIcon LookingForIcon(int value) =>
        value switch
        {
            VelvetLookingFor.Erp => FontAwesomeIcon.Comments,
            VelvetLookingFor.Gpose => FontAwesomeIcon.Camera,
            VelvetLookingFor.Relationship => FontAwesomeIcon.Heart,
            VelvetLookingFor.Collab => FontAwesomeIcon.Feather,
            VelvetLookingFor.Friends => FontAwesomeIcon.Users,
            VelvetLookingFor.Sharing => FontAwesomeIcon.Image,
            VelvetLookingFor.Wandering => FontAwesomeIcon.Moon,
            _ => FontAwesomeIcon.Compass,
        };

    private static FontAwesomeIcon RelationshipIcon(int value) =>
        value switch
        {
            VelvetRelationship.Single => FontAwesomeIcon.User,
            VelvetRelationship.Taken => FontAwesomeIcon.Heart,
            VelvetRelationship.Open => FontAwesomeIcon.LockOpen,
            VelvetRelationship.Complicated => FontAwesomeIcon.HandHoldingHeart,
            _ => FontAwesomeIcon.Lock,
        };

    private static string[] SplitTokens(string raw) =>
        raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool HasToken(string raw, string token)
    {
        var tokens = SplitTokens(raw);
        for (var index = 0; index < tokens.Length; index++)
        {
            if (string.Equals(tokens[index], token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string AddToken(string raw, string token, int maxTotal)
    {
        token = token.Trim();
        if (token.Length == 0 || HasToken(raw, token))
        {
            return raw;
        }

        var trimmed = raw.Trim().TrimEnd(',').TrimEnd();
        var result = trimmed.Length == 0 ? token : trimmed + ", " + token;
        return result.Length > maxTotal ? raw : result;
    }

    private static string ToggleToken(string raw, string token, int maxTotal) =>
        HasToken(raw, token) ? RemoveTokenByValue(raw, token) : AddToken(raw, token, maxTotal);

    private static string RemoveTokenAt(string raw, int removeIndex)
    {
        var tokens = SplitTokens(raw);
        if (removeIndex < 0 || removeIndex >= tokens.Length)
        {
            return raw;
        }

        var kept = new List<string>(tokens.Length);
        for (var index = 0; index < tokens.Length; index++)
        {
            if (index != removeIndex)
            {
                kept.Add(tokens[index]);
            }
        }

        return string.Join(", ", kept);
    }

    private static string RemoveTokenByValue(string raw, string token)
    {
        var tokens = SplitTokens(raw);
        var kept = new List<string>(tokens.Length);
        for (var index = 0; index < tokens.Length; index++)
        {
            if (!string.Equals(tokens[index], token, StringComparison.OrdinalIgnoreCase))
            {
                kept.Add(tokens[index]);
            }
        }

        return string.Join(", ", kept);
    }

    private static void CommitTokens(ref string field, ref string buffer, int maxTotal)
    {
        var parts = buffer.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var index = 0; index < parts.Length; index++)
        {
            field = AddToken(field, parts[index].ToLowerInvariant(), maxTotal);
        }

        buffer = string.Empty;
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
                var fieldSize = new Vector2(rect.Width - 24f * scale, rect.Height - 16f * scale);
                var wrapWidth = fieldSize.X - ImGui.GetStyle().FramePadding.X * 2f - 4f * scale;
                SoftWrapField.Multiline(id, ref value, maxLength, fieldSize, wrapWidth);
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
