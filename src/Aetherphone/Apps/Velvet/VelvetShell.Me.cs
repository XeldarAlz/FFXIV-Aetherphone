using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private void DrawMe(Rect area)
    {
        var me = store.Me;
        if (me is null)
        {
            store.EnsureMe();
            Typography.DrawCentered(area.Center, Loc.T(L.Common.Loading), VelvetTheme.MutedInk, TextStyles.Callout);
            return;
        }

        DrawProfileBody(area, me);
    }

    private const float SettingsRowHeight = 52f;
    private const float SettingsSegmentHeight = 34f;
    private const float SettingsCardPadY = 6f;
    private const float SettingsSegmentPadY = 10f;
    private const float SettingsTextInset = VCard.Pad;
    private const float SettingsHelpGap = 8f;
    private const float SettingsSectionGap = 18f;
    private const float SettingsTopGap = 6f;
    private const float SettingsBottomGap = 40f;

    private int settingsWho;
    private bool settingsLoaded;

    private void DrawSettings(Rect area)
    {
        var scale = UiScale.Current;
        if (VHeader.Push(area, Loc.T(L.Velvet.Settings)))
        {
            router.Pop();
            return;
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var width = ScrollLayout.StableContentWidth();
            var me = store.Me;
            Gap(SettingsTopGap);
            if (me is not null)
            {
                VSectionHeader.Overline(Loc.T(L.Velvet.DiscoveryHeader), string.Empty, SettingsTextInset * scale);
                DrawDiscoveryCard(drawList, width, me, scale);
                Gap(SettingsHelpGap);
                DrawSettingsFootnote(Loc.T(L.Velvet.DiscoverableHelp), scale);

                Gap(SettingsSectionGap);
                VSectionHeader.Overline(Loc.T(L.Velvet.WhoCanMessage), string.Empty, SettingsTextInset * scale);
                DrawWhoCard(drawList, width, me, scale);
                Gap(SettingsHelpGap);
                DrawSettingsFootnote(Loc.T(L.Velvet.WhoHelp), scale);
                Gap(SettingsSectionGap);
            }

            VSectionHeader.Overline(Loc.T(L.Velvet.SafetyHeader), string.Empty, SettingsTextInset * scale);
            DrawSafetyCard(drawList, width, scale);
            Gap(SettingsBottomGap);
        }
    }

    private void DrawDiscoveryCard(ImDrawListPtr drawList, float width, VelvetProfileDto me, float scale)
    {
        var rowHeight = SettingsRowHeight * scale;
        var card = VCard.Begin(drawList, width, rowHeight, scale, SettingsCardPadY);
        var row = new Rect(card.ContentOrigin,
            new Vector2(card.ContentOrigin.X + card.ContentWidth, card.ContentOrigin.Y + rowHeight));
        var labelWidth = card.ContentWidth - (VToggle.TrackWidth + Metrics.Space.Md) * scale;
        VCard.RowLabel(drawList, card.ContentOrigin, rowHeight, PhoneIcons.Compass, VelvetTheme.Rose,
            Loc.T(L.Velvet.DiscoverableLabel), labelWidth, scale);
        var discoverable = VToggle.Draw(drawList, "velvetDiscoverable", row, me.Discoverable, scale);
        VCard.End(card);
        if (discoverable == me.Discoverable || editBusy)
        {
            return;
        }

        editBusy = true;
        store.UpdateProfile(
            new UpdateVelvetProfileRequest(null, null, null, null, null, null, null, discoverable),
            _ => editBusy = false);
    }

    private void DrawWhoCard(ImDrawListPtr drawList, float width, VelvetProfileDto me, float scale)
    {
        if (!settingsLoaded)
        {
            settingsWho = me.WhoCanMessage;
            settingsLoaded = true;
        }

        var segmentHeight = SettingsSegmentHeight * scale;
        var card = VCard.Begin(drawList, width, segmentHeight, scale, SettingsSegmentPadY);
        var segment = new Rect(card.ContentOrigin,
            new Vector2(card.ContentOrigin.X + card.ContentWidth, card.ContentOrigin.Y + segmentHeight));
        FillWhoLabels();
        var who = VSegmented.Draw("velvetWho", segment, whoLabels, settingsWho, scale);
        VCard.End(card);
        if (who < 0 || who == settingsWho)
        {
            return;
        }

        settingsWho = who;
        store.UpdateProfile(new UpdateVelvetProfileRequest(null, null, null, null, null, null, null, null, who),
            _ => { });
    }

    private void DrawSafetyCard(ImDrawListPtr drawList, float width, float scale)
    {
        var rowHeight = SettingsRowHeight * scale;
        var card = VCard.Begin(drawList, width, rowHeight * 2f, scale, SettingsCardPadY);
        var openNotInterested = DrawSettingsLinkRow(drawList, card.ContentOrigin, card.ContentWidth, rowHeight,
            PhoneIcons.EyeOff, VelvetTheme.Gold, Loc.T(L.Velvet.NotInterested), true, scale);
        var blockedOrigin = new Vector2(card.ContentOrigin.X, card.ContentOrigin.Y + rowHeight);
        var openBlocked = DrawSettingsLinkRow(drawList, blockedOrigin, card.ContentWidth, rowHeight, PhoneIcons.Shield,
            VelvetTheme.Gold, Loc.T(L.Velvet.Blocked), false, scale);
        VCard.End(card);
        if (openNotInterested)
        {
            router.Push(VelvetView.NotInterested);
        }
        else if (openBlocked)
        {
            router.Push(VelvetView.Blocked);
        }
    }

    private static bool DrawSettingsLinkRow(ImDrawListPtr drawList, Vector2 origin, float width, float rowHeight,
        string glyph, Vector4 tone, string label, bool separator, float scale)
    {
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + rowHeight);
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            Squircle.Fill(drawList, min, max, Metrics.Radius.Sm * scale, VelvetTheme.HoverWash.Packed());
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var chevron = VIcon.Row * scale;
        VCard.RowLabel(drawList, origin, rowHeight, glyph, tone, label,
            width - chevron - Metrics.Space.Sm * scale, scale);
        PhoneIcon.Draw(drawList, new Vector2(max.X - chevron * 0.5f, origin.Y + rowHeight * 0.5f),
            PhoneIcons.ChevronRight, VelvetTheme.Faint, chevron);
        if (separator)
        {
            FeedCell.Hairline(drawList, VCard.RowTextLeft(origin.X, scale), max.X, max.Y, VelvetTheme.Hairline);
        }

        return UiInteract.Click(min, max, hovered);
    }

    private static void DrawSettingsFootnote(string text, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var inset = SettingsTextInset * scale;
        var height = Typography.DrawWrappedLeft(new Vector2(origin.X + inset, origin.Y), text, VelvetTheme.MutedInk,
            TextStyles.Footnote, MathF.Max(1f, width - inset * 2f));
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawBlocked(Rect area)
    {
        var scale = UiScale.Current;
        if (VHeader.Push(area, Loc.T(L.Velvet.Blocked)))
        {
            router.Pop();
            return;
        }

        if (!store.BlockedLoaded && !store.LoadingBlocked)
        {
            store.RefreshBlocked();
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        using (AppSurface.BeginEdgeToEdge(body))
        {
            var blocked = store.Blocked;
            if (blocked.Length == 0)
            {
                DrawEmpty(body, Loc.T(L.Velvet.BlockedNone), string.Empty);
                return;
            }

            Gap(8f);
            for (var index = 0; index < blocked.Length; index++)
            {
                var user = blocked[index];
                var model = new VRowModel
                {
                    Title = DisplayNameOf(user.DisplayName, user.Handle),
                    Subtitle = SocialIdentity.ProfileMeta(user.Handle, RegionCodeOf(user)),
                    Height = 60f,
                    Leading = VRowLeading.Avatar,
                    AvatarRadius = 20f,
                    Name = DisplayNameOf(user.DisplayName, user.Handle),
                    World = string.Empty,
                    AvatarUrl = user.AvatarUrl,
                    FrameId = user.FrameId,
                    RoleBadges = user.Badges,
                    RoleBadgeIds = user.ProfileBadges,
                    UserId = user.Id,
                    Pill = Loc.T(L.Velvet.Unblock),
                    PillFilled = false,
                    PillEnabled = true,
                };
                var hit = VRow.Cell(in model, ui, theme, images, lodestone);
                if (hit == VRowHit.Pill)
                {
                    store.Unblock(user.Id);
                }
                else if (hit == VRowHit.Body)
                {
                    OpenProfile(user.Id);
                }
            }

            Gap(40f);
        }
    }

    private void DrawNotInterested(Rect area)
    {
        var scale = UiScale.Current;
        if (VHeader.Push(area, Loc.T(L.Velvet.NotInterested)))
        {
            router.Pop();
            return;
        }

        if (!store.NotInterestedLoaded && !store.LoadingNotInterested)
        {
            store.RefreshNotInterested();
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        using (AppSurface.BeginEdgeToEdge(body))
        {
            var notInterested = store.NotInterested;
            if (notInterested.Length == 0)
            {
                DrawEmpty(body, Loc.T(L.Velvet.NotInterestedNone), string.Empty);
                return;
            }

            Gap(8f);
            for (var index = 0; index < notInterested.Length; index++)
            {
                var user = notInterested[index];
                var model = new VRowModel
                {
                    Title = DisplayNameOf(user.DisplayName, user.Handle),
                    Subtitle = SocialIdentity.ProfileMeta(user.Handle, RegionCodeOf(user)),
                    Height = 60f,
                    Leading = VRowLeading.Avatar,
                    AvatarRadius = 20f,
                    Name = DisplayNameOf(user.DisplayName, user.Handle),
                    World = string.Empty,
                    AvatarUrl = user.AvatarUrl,
                    FrameId = user.FrameId,
                    RoleBadges = user.Badges,
                    RoleBadgeIds = user.BadgeIds,
                    UserId = user.UserId,
                    Pill = Loc.T(L.Velvet.NotInterestedRemove),
                    PillFilled = false,
                    PillEnabled = true,
                };
                var hit = VRow.Cell(in model, ui, theme, images, lodestone);
                if (hit == VRowHit.Pill)
                {
                    store.RemoveFromNotInterested(user.UserId);
                }
                else if (hit == VRowHit.Body)
                {
                    OpenProfile(user.UserId);
                }
            }

            Gap(40f);
        }
    }
}
