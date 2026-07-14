using System.Numerics;
using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
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
            Typography.DrawCentered(area.Center, "Loading", VelvetTheme.MutedInk, TextStyles.Callout);
            return;
        }

        DrawProfileBody(area, me);
    }

    private int settingsWho;
    private bool settingsLoaded;

    private void DrawSettings(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        if (VHeader.Push(area, "Settings", theme))
        {
            router.Pop();
            return;
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            Gap(10f);
            VSectionHeader.Overline("Discovery");
            var me = store.Me;
            if (me is not null)
            {
                Gap(2f);
                var discoverable = me.Discoverable;
                ui.ToggleRow("Appear in Discover", ref discoverable);
                if (discoverable != me.Discoverable && !editBusy)
                {
                    editBusy = true;
                    store.UpdateProfile(new UpdateVelvetProfileRequest(null, null, null, null, null, null, null,
                        discoverable), _ => editBusy = false);
                }

                Gap(6f);
                ui.HelpText("When on, your profile can be found by others in Discover.");

                Gap(22f);
                VSectionHeader.Overline("Who can message you");
                Gap(10f);
                if (!settingsLoaded)
                {
                    settingsWho = me.WhoCanMessage;
                    settingsLoaded = true;
                }

                var whoRect = Reserve(34f);
                var who = VSegmented.Draw("velvetWho", whoRect, new[] { "Everyone", "Friends", "No one" }, settingsWho,
                    scale);
                if (who >= 0 && who != settingsWho)
                {
                    settingsWho = who;
                    store.UpdateProfile(
                        new UpdateVelvetProfileRequest(null, null, null, null, null, null, null, null, who),
                        _ => { });
                }

                Gap(10f);
                ui.HelpText("Choose who can send you a one line intro. Friends means friends of friends.");
            }

            Gap(18f);
            VSectionHeader.Overline("Safety");
            var blockedRow = new VRowModel
            {
                Title = "Blocked",
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

    private void DrawBlocked(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        if (VHeader.Push(area, "Blocked", theme))
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
                Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 80f * scale), "No one blocked.",
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
                    Pill = "Unblock",
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
