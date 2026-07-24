using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Muster;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Muster;

internal sealed partial class MusterApp
{
    private const float StatusCardHeight = 92f;
    private const float AttendeeRowHeight = 36f;
    private const float ManageActionHeight = 46f;

    private void DrawManage(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Muster.YourMuster), back);
        if (store.Mine is not { } mine)
        {
            router.Pop(false);
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        var nowUnix = NowUnix();
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
            DrawManageStatus(mine, nowUnix, scale);
            DrawWrappedDescription(mine.Description, scale);
            DrawLocationBlock(mine, "manage", scale);
            DrawAttendees(mine, scale);
            DrawManageActions(mine, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void DrawManageStatus(MusterDto mine, long nowUnix, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = StatusCardHeight * scale;
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + height), Metrics.Radius.Card * scale,
            elevated: true);
        var tileSide = 40f * scale;
        var tileCenter = new Vector2(origin.X + 14f * scale + tileSide * 0.5f, origin.Y + 14f * scale
            + tileSide * 0.5f);
        IconTile.Draw(tileCenter, tileSide, IconTile.Surface(ui.Accent), MusterCategories.Icon(mine.Category));
        var textLeft = tileCenter.X + tileSide * 0.5f + 12f * scale;
        Typography.Draw(drawList, new Vector2(textLeft, origin.Y + 14f * scale),
            Loc.T(MusterCategories.Label(mine.Category)), AppPalettes.Muster.TitleInk, TextStyles.Headline);
        var live = mine.StartsAtUnix <= nowUnix;
        var status = live
            ? $"{Loc.T(L.Common.Live)} · {Loc.T(L.Muster.EndsIn, MusterText.Span(mine.EndsAtUnix - nowUnix))}"
            : $"{Loc.T(L.Muster.StartsAt, TimeText.Clock(mine.StartsAtUnix))} · {Loc.T(L.Muster.RunsFor, MusterText.Span(mine.EndsAtUnix - mine.StartsAtUnix))}";
        Typography.Draw(drawList, new Vector2(textLeft, origin.Y + 37f * scale), status,
            live ? MusterCard.LiveGreen : ui.Accent, TextStyles.FootnoteEmphasized);
        var capacity = mine.MaxAttendees > 0
            ? Loc.T(L.Muster.CapacityLine, mine.RsvpCount, mine.MaxAttendees)
            : Loc.T(L.Muster.GoingCount, mine.RsvpCount);
        var listed = mine.IsPublic ? Loc.T(L.Muster.ListedPublicly) : Loc.T(L.Muster.ListedPrivately);
        var meta = $"{capacity} · {listed}";
        var fitted = Typography.FitText(meta, width - (textLeft - origin.X) - 14f * scale, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(textLeft, origin.Y + 60f * scale), fitted,
            AppPalettes.Muster.MutedInk, TextStyles.Footnote);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Md * scale));
    }

    private void DrawAttendees(MusterDto mine, float scale)
    {
        ui.SectionHeading(Loc.T(L.Muster.AttendeesSection));
        var attendees = store.MineAttendees;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        if (attendees.Length == 0)
        {
            Typography.Draw(new Vector2(origin.X, origin.Y), Loc.T(L.Muster.NoAttendees),
                AppPalettes.Muster.MutedInk, TextStyles.Subheadline);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, 26f * scale + Metrics.Space.Md * scale));
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var rowHeight = AttendeeRowHeight * scale;
        for (var index = 0; index < attendees.Length; index++)
        {
            var attendee = attendees[index];
            var rowTop = origin.Y + index * rowHeight;
            var avatarRadius = 13f * scale;
            var avatarCenter = new Vector2(origin.X + avatarRadius, rowTop + rowHeight * 0.5f);
            MusterCard.DrawAvatarCircle(drawList, avatarCenter, avatarRadius, attendee.AvatarUrl,
                attendee.DisplayName, images, ui.Accent);
            var nameLeft = avatarCenter.X + avatarRadius + 10f * scale;
            var nameSize = Typography.Measure(attendee.DisplayName, TextStyles.BodyEmphasized);
            Typography.Draw(drawList, new Vector2(nameLeft, rowTop + (rowHeight - nameSize.Y) * 0.5f),
                attendee.DisplayName, AppPalettes.Muster.TitleInk, TextStyles.BodyEmphasized);
            var handle = MusterText.HandleAt(attendee.UserId, attendee.Handle);
            if (handle.Length > 0)
            {
                var handleSize = Typography.Measure(handle, TextStyles.Footnote);
                Typography.Draw(drawList, new Vector2(nameLeft + nameSize.X + 8f * scale,
                    rowTop + (rowHeight - handleSize.Y) * 0.5f), handle, AppPalettes.Muster.MutedInk,
                    TextStyles.Footnote);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, attendees.Length * rowHeight + Metrics.Space.Md * scale));
    }

    private void DrawManageActions(MusterDto mine, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = ManageActionHeight * scale;
        var copyRect = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var copyLabel = JustCopied("invite") ? Loc.T(L.Muster.Copied) : Loc.T(L.Muster.CopyInvite);
        if (ui.PillButton(copyRect, copyLabel, false))
        {
            Copy("invite", MusterShare.Compose(mine.Id));
        }

        var endTop = copyRect.Max.Y + Metrics.Space.Md * scale;
        var endRect = new Rect(new Vector2(origin.X, endTop), new Vector2(origin.X + width, endTop + height));
        if (ui.DangerGhostButton(endRect, Loc.T(L.Muster.EndMuster)))
        {
            AskEndMuster();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, endRect.Max.Y - origin.Y));
    }

    private void AskEndMuster()
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Muster.EndMuster),
            Message = Loc.T(L.Muster.EndConfirm),
            ConfirmLabel = Loc.T(L.Muster.EndMuster),
            CancelLabel = Loc.T(L.Common.Cancel),
            BusyLabel = Loc.T(L.Muster.Ending),
            FailedMessage = Loc.T(L.Muster.EndFailed),
            Danger = true,
            ConfirmAsync = done => store.EndMine(done),
        });
    }
}
