using Aetherphone.Apps.Music.Components;
using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Jam;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float JamRowHeight = 58f;
    private const float JamRowAvatarRadius = 18f;
    private const float JamRowMonogramScale = 0.7f;
    private const int JamRowSegments = 28;
    private const float JamCrownBox = 14f;
    private const float JamCrownOffsetX = 0.72f;
    private const float JamCrownOffsetY = 0.82f;
    private const float JamCrownBackingShare = 0.62f;
    private const float JamDecisionRadius = 15f;
    private const float JamDecisionGlyphScale = 0.62f;
    private const float JamDecisionFillAlpha = 0.18f;
    private const float JamDotsBox = 18f;
    private const float JamTagPadX = 6f;
    private const float JamTagFillAlpha = 0.16f;
    private const int JamMemberActionCount = 2;
    private const int JamActionMakeHost = 0;
    private const int JamActionRemove = 1;

    private readonly ActionSheet jamMemberSheet = new();
    private readonly ActionSheet.Item[] jamMemberItems = new ActionSheet.Item[JamMemberActionCount];
    private readonly int[] jamMemberCodes = new int[JamMemberActionCount];
    private string[] jamRequestHandles = Array.Empty<string>();
    private string[] jamRequestNames = Array.Empty<string>();
    private int jamRequestsVersion = -1;
    private string jamActionUserId = string.Empty;
    private string jamActionName = string.Empty;

    private void DrawJamRequests(float scale)
    {
        EnsureJamRequests();
        SectionHeader.Draw(ui, Loc.T(L.Music.Jam.RequestsHeader), false);
        var requests = jam.JoinRequests;
        for (var index = 0; index < requests.Length && index < jamRequestNames.Length; index++)
        {
            DrawJamRequestRow(requests[index], jamRequestNames[index], jamRequestHandles[index], scale);
        }
    }

    private void EnsureJamRequests()
    {
        if (jam.JoinRequestsVersion == jamRequestsVersion)
        {
            return;
        }

        jamRequestsVersion = jam.JoinRequestsVersion;
        var requests = jam.JoinRequests;
        var names = new string[requests.Length];
        var handles = new string[requests.Length];
        for (var index = 0; index < requests.Length; index++)
        {
            var request = requests[index];
            handles[index] = request.Handle.Length > 0 ? string.Concat(JamHandlePrefix, request.Handle) : string.Empty;
            names[index] = request.DisplayName.Length > 0 ? request.DisplayName : handles[index];
        }

        jamRequestNames = names;
        jamRequestHandles = handles;
    }

    private void DrawJamRequestRow(JamJoinRequest request, string name, string handle, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, JamRowHeight * scale, ui.HoverWash, false);
        var row = cell.Bounds;
        var avatarRadius = JamRowAvatarRadius * scale;
        var avatarCenter = new Vector2(row.Min.X + MusicUi.Inset * scale + avatarRadius, row.Center.Y);
        var shownName = name.Length > 0 ? name : Loc.T(L.Music.Jam.SomeoneName);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, shownName, string.Empty, request.AvatarUrl,
            images, lodestone, JamRowMonogramScale, JamRowSegments);
        var decisionRadius = JamDecisionRadius * scale;
        var denyCenter = new Vector2(row.Max.X - MusicUi.Inset * scale - decisionRadius, row.Center.Y);
        var approveCenter = new Vector2(denyCenter.X - decisionRadius * 2f - Metrics.Space.Sm * scale, row.Center.Y);
        var textLeft = avatarCenter.X + avatarRadius + Metrics.Space.Md * scale;
        var textRight = approveCenter.X - decisionRadius - Metrics.Space.Md * scale;
        DrawJamRowText(drawList, textLeft, textRight, row.Center.Y, shownName, handle);
        var positive = AccentRing.Green;
        if (ui.IconButton(approveCenter, decisionRadius, IconGlyph.Of(FontAwesomeIcon.Check), positive,
                Palette.WithAlpha(positive, JamDecisionFillAlpha), JamDecisionGlyphScale, Loc.T(L.Music.Jam.Approve)))
        {
            jam.Approve(request.UserId);
        }

        var danger = theme.Danger;
        if (ui.IconButton(denyCenter, decisionRadius, IconGlyph.Of(FontAwesomeIcon.Times), danger,
                Palette.WithAlpha(danger, JamDecisionFillAlpha), JamDecisionGlyphScale, Loc.T(L.Music.Jam.Deny)))
        {
            jam.Deny(request.UserId);
        }

        FeedCell.End(drawList, cell, ui.Hairline);
    }

    private void DrawJamMembers(float scale)
    {
        SectionHeader.Draw(ui, Loc.T(L.Music.Jam.MembersHeader), false);
        var hosting = jam.IsHost;
        for (var index = 0; index < jamMembers.Length; index++)
        {
            DrawJamMemberRow(index, hosting, scale);
        }
    }

    private void DrawJamMemberRow(int index, bool hosting, float scale)
    {
        var member = jamMembers[index];
        var isMe = index == jamMeIndex;
        var manageable = hosting && !isMe && !member.IsHost;
        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, JamRowHeight * scale, ui.HoverWash, manageable);
        var row = cell.Bounds;
        var avatarRadius = JamRowAvatarRadius * scale;
        var avatarCenter = new Vector2(row.Min.X + MusicUi.Inset * scale + avatarRadius, row.Center.Y);
        var name = JamMemberName(index);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, name, string.Empty, member.AvatarUrl,
            images, lodestone, JamRowMonogramScale, JamRowSegments);
        var trailing = row.Max.X - MusicUi.Inset * scale;
        if (manageable)
        {
            PhoneIcon.Draw(drawList, new Vector2(trailing - JamDotsBox * 0.5f * scale, row.Center.Y), PhoneIcons.Dots,
                ui.MutedInk, JamDotsBox * scale);
            trailing -= (JamDotsBox + Metrics.Space.Sm) * scale;
        }

        if (isMe)
        {
            trailing -= DrawJamTag(drawList, trailing, row.Center.Y, Loc.T(L.Music.Jam.You), scale)
                + Metrics.Space.Sm * scale;
        }

        var textLeft = avatarCenter.X + avatarRadius + Metrics.Space.Md * scale;
        if (member.IsHost)
        {
            var crownCenter = new Vector2(avatarCenter.X + avatarRadius * JamCrownOffsetX,
                avatarCenter.Y - avatarRadius * JamCrownOffsetY);
            drawList.AddCircleFilled(crownCenter, JamCrownBox * JamCrownBackingShare * scale,
                ImGui.GetColorU32(ui.Palette.BackdropBottom), JamRowSegments);
            PhoneIcon.Draw(drawList, crownCenter, PhoneIcons.Crown, AccentRing.Gold, JamCrownBox * scale);
        }

        var subtitle = member.IsHost ? Loc.T(L.Music.Jam.HostBadge) : jamMemberHandles[index];
        DrawJamRowText(drawList, textLeft, trailing, row.Center.Y, name, subtitle, member.IsHost);
        FeedCell.End(drawList, cell, ui.Hairline);
        if (cell.Tapped)
        {
            OpenJamMemberActions(member.UserId, name);
        }
    }

    private void DrawJamRowText(ImDrawListPtr drawList, float left, float right, float centerY, string title,
        string subtitle, bool accentSubtitle = false)
    {
        var width = MathF.Max(1f, right - left);
        var titleHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        var subtitleHeight = subtitle.Length > 0 ? Typography.LineHeight(TextStyles.Footnote) : 0f;
        var top = centerY - (titleHeight + subtitleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(title, width, TextStyles.BodyEmphasized),
            ui.TitleInk, TextStyles.BodyEmphasized);
        if (subtitle.Length == 0)
        {
            return;
        }

        Typography.Draw(drawList, new Vector2(left, top + titleHeight),
            Typography.FitText(subtitle, width, TextStyles.Footnote), accentSubtitle ? ui.Accent : ui.MutedInk,
            TextStyles.Footnote);
    }

    private float DrawJamTag(ImDrawListPtr drawList, float right, float centerY, string label, float scale)
    {
        var size = Typography.Measure(label, TextStyles.Caption2);
        var padX = JamTagPadX * scale;
        var height = size.Y + Metrics.Space.Xxs * scale;
        var min = new Vector2(right - size.X - padX * 2f, centerY - height * 0.5f);
        var max = new Vector2(right, centerY + height * 0.5f);
        Squircle.Fill(drawList, min, max, height * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, JamTagFillAlpha)));
        Typography.DrawCentered(drawList, (min + max) * 0.5f, label, ui.Accent, TextStyles.Caption2);
        return max.X - min.X;
    }

    private void OpenJamMemberActions(string userId, string name)
    {
        if (!jam.IsHost)
        {
            return;
        }

        jamActionUserId = userId;
        jamActionName = name;
        jamMemberSheet.Open();
    }

    private void DrawJamMemberSheet(Rect screen)
    {
        if (!jamMemberSheet.CapturesPointer)
        {
            return;
        }

        jamMemberItems[0] = new ActionSheet.Item(Loc.T(L.Music.Jam.MakeHost), IconGlyph.Of(FontAwesomeIcon.Crown));
        jamMemberCodes[0] = JamActionMakeHost;
        jamMemberItems[1] = new ActionSheet.Item(Loc.T(L.Music.Jam.Remove), IconGlyph.Of(FontAwesomeIcon.UserSlash),
            true);
        jamMemberCodes[1] = JamActionRemove;
        var picked = jamMemberSheet.Draw(screen, ActionSheetStyle.From(ui), jamMemberItems,
            Loc.T(L.Common.Cancel), false, jamActionName);
        if (picked < 0)
        {
            return;
        }

        RunJamMemberAction(jamMemberCodes[picked]);
    }

    private void RunJamMemberAction(int action)
    {
        var userId = jamActionUserId;
        var name = jamActionName;
        jamActionUserId = string.Empty;
        if (!jam.IsHost || userId.Length == 0 || !IsJamMember(userId))
        {
            return;
        }

        if (action == JamActionRemove)
        {
            jam.Kick(userId);
            return;
        }

        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Music.Jam.MakeHost),
            Message = string.Format(Loc.Culture, Loc.T(L.Music.Jam.MakeHostBody), name),
            ConfirmLabel = Loc.T(L.Music.Jam.MakeHost),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = false,
            Sheet = true,
            Confirm = () => jam.Transfer(userId),
        });
    }
}
