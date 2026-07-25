using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

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

    private int settingsWho;
    private bool settingsLoaded;

    private void DrawSettings(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        if (VHeader.Push(area, Loc.T(L.Velvet.Settings), theme))
        {
            router.Pop();
            return;
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            Gap(10f);
            VSectionHeader.Overline(Loc.T(L.Velvet.DiscoveryHeader));
            var me = store.Me;
            if (me is not null)
            {
                Gap(2f);
                var discoverable = me.Discoverable;
                ui.ToggleRow(Loc.T(L.Velvet.DiscoverableLabel), ref discoverable);
                if (discoverable != me.Discoverable && !editBusy)
                {
                    editBusy = true;
                    store.UpdateProfile(new UpdateVelvetProfileRequest(null, null, null, null, null, null, null,
                        discoverable), _ => editBusy = false);
                }

                Gap(6f);
                ui.HelpText(Loc.T(L.Velvet.DiscoverableHelp));

                Gap(14f);
                var showLalafell = configuration.VelvetShowLalafell;
                ui.ToggleRow(Loc.T(L.Velvet.ShowLalafellLabel), ref showLalafell);
                if (showLalafell != configuration.VelvetShowLalafell)
                {
                    if (showLalafell)
                    {
                        AskShowLalafell();
                    }
                    else
                    {
                        SetShowLalafell(false);
                    }
                }

                Gap(6f);
                ui.HelpText(Loc.T(L.Velvet.ShowLalafellHelp));

                Gap(22f);
                VSectionHeader.Overline(Loc.T(L.Velvet.WhoCanMessage));
                Gap(10f);
                if (!settingsLoaded)
                {
                    settingsWho = me.WhoCanMessage;
                    settingsLoaded = true;
                }

                var whoRect = Reserve(34f);
                var who = VSegmented.Draw("velvetWho", whoRect,
                    new[] { Loc.T(L.Velvet.WhoEveryone), Loc.T(L.Velvet.WhoFriends), Loc.T(L.Velvet.WhoNoOne) },
                    settingsWho, scale);
                if (who >= 0 && who != settingsWho)
                {
                    settingsWho = who;
                    store.UpdateProfile(
                        new UpdateVelvetProfileRequest(null, null, null, null, null, null, null, null, who),
                        _ => { });
                }

                Gap(10f);
                ui.HelpText(Loc.T(L.Velvet.WhoHelp));
            }

            Gap(18f);
            VSectionHeader.Overline(Loc.T(L.Velvet.SafetyHeader));
            var blockedRow = new VRowModel
            {
                Title = Loc.T(L.Velvet.Blocked),
                Leading = VRowLeading.IconTile,
                TileIcon = FontAwesomeIcon.ShieldAlt,
                TileTint = VelvetTheme.Gold,
                Chevron = true,
                Height = 52f,
            };
            if (VRow.Draw(in blockedRow, ui, theme, images, lodestone) == VRowHit.Body)
            {
                router.Push(VelvetView.Blocked);
            }

            Gap(40f);
        }
    }

    private void AskShowLalafell()
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Velvet.ShowLalafellConfirmTitle),
            Message = Loc.T(L.Velvet.ShowLalafellConfirmMessage),
            ConfirmLabel = Loc.T(L.Velvet.ShowLalafellConfirmAction),
            CancelLabel = Loc.T(L.Velvet.DeleteCancel),
            Danger = false,
            Confirm = () => SetShowLalafell(true),
        });
    }

    private void SetShowLalafell(bool value)
    {
        configuration.VelvetShowLalafell = value;
        configuration.Save();
        ApplyDiscoverFilters();
    }

    private void DrawBlocked(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        if (VHeader.Push(area, Loc.T(L.Velvet.Blocked), theme))
        {
            router.Pop();
            return;
        }

        if (!store.BlockedLoaded && !store.LoadingBlocked)
        {
            store.RefreshBlocked();
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            var blocked = store.Blocked;
            if (blocked.Length == 0)
            {
                Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 80f * scale), Loc.T(L.Velvet.BlockedNone),
                    VelvetTheme.MutedInk, TextStyles.Callout);
                return;
            }

            Gap(8f);
            for (var index = 0; index < blocked.Length; index++)
            {
                var user = blocked[index];
                var model = new VRowModel
                {
                    Title = DisplayNameOf(user.DisplayName, user.Handle),
                    Subtitle = SocialIdentity.ProfileMeta(user.Handle, RegionOf(user.World)),
                    Height = 60f,
                    Leading = VRowLeading.Avatar,
                    AvatarRadius = 20f,
                    Name = DisplayNameOf(user.DisplayName, user.Handle),
                    World = user.World,
                    AvatarUrl = user.AvatarUrl,
                    Pill = Loc.T(L.Velvet.Unblock),
                    PillFilled = false,
                    PillEnabled = true,
                };
                var hit = VRow.Draw(in model, ui, theme, images, lodestone);
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
}
