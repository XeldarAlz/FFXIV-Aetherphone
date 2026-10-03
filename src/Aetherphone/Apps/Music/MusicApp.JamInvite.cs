using Aetherphone.Apps.Music.Components;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const ImGuiWindowFlags JamSheetFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                   ImGuiWindowFlags.NoBackground;

    private const float JamSheetHeaderHeight = 40f;
    private const float JamSheetSearchHeight = 36f;
    private const float JamSheetMutedAlpha = 0.6f;
    private const float JamSheetHoverAlpha = 0.07f;
    private const float JamInvitePillHeight = 28f;
    private const float JamInvitePillPadX = 14f;
    private const float JamInviteCheckScale = 0.62f;

    private readonly Sheet jamInviteSheet = new();
    private readonly HashSet<string> jamInvited = new(StringComparer.Ordinal);
    private ContactDto[] jamInviteRows = Array.Empty<ContactDto>();
    private string[] jamInviteNames = Array.Empty<string>();
    private string[] jamInviteHandles = Array.Empty<string>();
    private string jamInviteQuery = string.Empty;
    private string jamInviteAppliedQuery = string.Empty;
    private string jamInvitedCode = string.Empty;
    private int jamInviteContactsVersion = -1;
    private int jamInviteMutualCount;

    private void OpenJamInviteSheet()
    {
        if (!jam.InJam)
        {
            return;
        }

        if (!string.Equals(jamInvitedCode, jam.Code, StringComparison.Ordinal))
        {
            jamInvitedCode = jam.Code;
            jamInvited.Clear();
        }

        contacts.Refresh();
        jamInviteQuery = string.Empty;
        jamInviteContactsVersion = -1;
        jamInviteSheet.Open();
    }

    private void DrawJamInviteSheet(Rect screen)
    {
        if (!jamInviteSheet.CapturesPointer)
        {
            return;
        }

        if (!jam.InJam && jamInviteSheet.IsOpen)
        {
            jamInviteSheet.Close();
        }

        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##musicJamInvite", screen.Size, false, JamSheetFlags))
        {
            var veil = SheetMetrics.VeilFor(WallpaperBackdrop.FlatAvailable);
            var frame = jamInviteSheet.Begin(ImGui.GetWindowDrawList(), screen, ui.Theme,
                SheetDetents.Standard(screen.Height), veil);
            if (!frame.Visible)
            {
                return;
            }

            DrawJamInviteContent(in frame);
            jamInviteSheet.End(in frame);
        }
    }

    private void DrawJamInviteContent(in SheetFrame frame)
    {
        var scale = UiScale.Current;
        var content = frame.Content;
        var ink = Palette.WithAlpha(frame.Ink, frame.Ink.W * frame.Opacity);
        var muted = Palette.WithAlpha(ink, ink.W * JamSheetMutedAlpha);
        var headerHeight = JamSheetHeaderHeight * scale;
        Typography.DrawCentered(frame.DrawList, new Vector2(content.Center.X, content.Min.Y + headerHeight * 0.5f),
            Loc.T(L.Music.Jam.InviteSheetTitle), ink, TextStyles.Headline);
        var inset = MusicUi.Inset * scale;
        var searchTop = content.Min.Y + headerHeight;
        var search = new Rect(new Vector2(content.Min.X + inset, searchTop),
            new Vector2(content.Max.X - inset, searchTop + JamSheetSearchHeight * scale));
        SearchField.Draw(search, "##musicJamInviteSearch", Loc.T(L.Music.Jam.InviteSearch), ref jamInviteQuery,
            ui.Palette);
        var footnoteTop = search.Max.Y + Metrics.Space.Sm * scale;
        var footnote = Loc.T(L.Music.Jam.InviteFootnote);
        var footnoteWidth = content.Width - inset * 2f;
        var footnoteHeight = Typography.MeasureWrappedBlock(footnote, TextStyles.Footnote, footnoteWidth).Y;
        Typography.DrawWrappedCentered(frame.DrawList,
            new Vector2(content.Center.X, footnoteTop + footnoteHeight * 0.5f), footnote, muted, TextStyles.Footnote,
            footnoteWidth);
        var rows = new Rect(new Vector2(content.Min.X, footnoteTop + footnoteHeight + Metrics.Space.Sm * scale),
            new Vector2(content.Max.X, content.Max.Y - Metrics.Size.HomeIndicatorInset * scale));
        EnsureJamInviteRows();
        if (jamInviteRows.Length == 0)
        {
            var empty = jamInviteMutualCount == 0;
            EmptyState.Draw(rows, ui, empty ? FontAwesomeIcon.UserFriends : FontAwesomeIcon.Search,
                Loc.T(empty ? L.Music.Jam.InviteEmptyTitle : L.Music.NoResults),
                empty ? Loc.T(L.Music.Jam.InviteEmptyBody) : string.Empty);
            return;
        }

        using (AppSurface.BeginEdgeToEdge(rows))
        {
            var drawList = ImGui.GetWindowDrawList();
            for (var index = 0; index < jamInviteRows.Length; index++)
            {
                DrawJamInviteRow(drawList, index, ink, muted, frame.Interactive, scale);
            }
        }
    }

    private void EnsureJamInviteRows()
    {
        if (jamInviteContactsVersion == contacts.Version
            && string.Equals(jamInviteAppliedQuery, jamInviteQuery, StringComparison.Ordinal))
        {
            return;
        }

        jamInviteContactsVersion = contacts.Version;
        jamInviteAppliedQuery = jamInviteQuery;
        var query = jamInviteQuery.AsSpan().Trim();
        var all = contacts.Contacts;
        var matches = new List<ContactDto>(all.Length);
        var mutual = 0;
        for (var index = 0; index < all.Length; index++)
        {
            var contact = all[index];
            if (!contact.IsMutual)
            {
                continue;
            }

            mutual++;
            if (query.Length > 0 && !ContactBook.DisplayLabel(contact).AsSpan().Contains(query,
                    StringComparison.OrdinalIgnoreCase) && !contact.Handle.AsSpan().Contains(query,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            matches.Add(contact);
        }

        jamInviteMutualCount = mutual;
        jamInviteRows = matches.ToArray();
        jamInviteNames = new string[jamInviteRows.Length];
        jamInviteHandles = new string[jamInviteRows.Length];
        for (var index = 0; index < jamInviteRows.Length; index++)
        {
            var contact = jamInviteRows[index];
            jamInviteNames[index] = ContactBook.DisplayLabel(contact);
            jamInviteHandles[index] = contact.Handle.Length > 0
                ? string.Concat(JamHandlePrefix, contact.Handle)
                : string.Empty;
        }
    }

    private void DrawJamInviteRow(ImDrawListPtr drawList, int index, Vector4 ink, Vector4 muted, bool interactive,
        float scale)
    {
        var height = JamRowHeight * scale;
        var width = ScrollLayout.StableContentWidth();
        if (!ImGui.IsRectVisible(new Vector2(width, height)))
        {
            ImGui.Dummy(new Vector2(width, height));
            return;
        }

        var contact = jamInviteRows[index];
        var inJam = IsJamMember(contact.UserId);
        var invited = jamInvited.Contains(contact.UserId);
        var invitable = interactive && !inJam && !invited;
        var hover = Palette.WithAlpha(ink, JamSheetHoverAlpha);
        var cell = FeedCell.Begin(drawList, height, hover, invitable);
        var row = cell.Bounds;
        var inset = MusicUi.Inset * scale;
        var avatarRadius = JamRowAvatarRadius * scale;
        var avatarCenter = new Vector2(row.Min.X + inset + avatarRadius, row.Center.Y);
        var name = jamInviteNames[index];
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, name, string.Empty, contact.AvatarUrl,
            images, lodestone, JamRowMonogramScale, JamRowSegments);
        var label = Loc.T(inJam ? L.Music.Jam.InJam : invited ? L.Music.Jam.Invited : L.Music.Jam.InviteAction);
        var pillWidth = Typography.Measure(label, TextStyles.FootnoteEmphasized).X + JamInvitePillPadX * 2f * scale
            + (invited ? JamInvitePillHeight * 0.5f * scale : 0f);
        var pillHeight = JamInvitePillHeight * scale;
        var pill = new Rect(new Vector2(row.Max.X - inset - pillWidth, row.Center.Y - pillHeight * 0.5f),
            new Vector2(row.Max.X - inset, row.Center.Y + pillHeight * 0.5f));
        DrawJamInvitePill(drawList, pill, label, inJam, invited, muted, scale);
        var textLeft = avatarCenter.X + avatarRadius + Metrics.Space.Md * scale;
        var textWidth = MathF.Max(1f, pill.Min.X - Metrics.Space.Md * scale - textLeft);
        var handle = jamInviteHandles[index];
        var titleHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        var handleHeight = handle.Length > 0 ? Typography.LineHeight(TextStyles.Footnote) : 0f;
        var top = row.Center.Y - (titleHeight + handleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top), Typography.FitText(name, textWidth,
            TextStyles.BodyEmphasized), ink, TextStyles.BodyEmphasized);
        if (handle.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(textLeft, top + titleHeight),
                Typography.FitText(handle, textWidth, TextStyles.Footnote), muted, TextStyles.Footnote);
        }

        FeedCell.End(drawList, cell, hover);
        if (!cell.Tapped || !invitable)
        {
            return;
        }

        jam.Invite(contact.UserId);
        jamInvited.Add(contact.UserId);
        ShellToast.Show(string.Format(Loc.Culture, Loc.T(L.Music.Jam.InviteSent), name));
    }

    private void DrawJamInvitePill(ImDrawListPtr drawList, Rect pill, string label, bool inJam, bool invited,
        Vector4 muted, float scale)
    {
        var radius = pill.Height * 0.5f;
        if (!inJam && !invited)
        {
            Squircle.Fill(drawList, pill.Min, pill.Max, radius, ImGui.GetColorU32(ui.Accent));
            Typography.DrawCentered(drawList, pill.Center, label, AccentRing.Ink, TextStyles.FootnoteEmphasized);
            return;
        }

        Squircle.Stroke(drawList, pill.Min, pill.Max, radius, ImGui.GetColorU32(muted), Metrics.Stroke.Hairline * scale);
        if (!invited)
        {
            Typography.DrawCentered(drawList, pill.Center, label, muted, TextStyles.FootnoteEmphasized);
            return;
        }

        var checkCenter = new Vector2(pill.Min.X + JamInvitePillPadX * scale, pill.Center.Y);
        AppSkin.Icon(drawList, checkCenter, IconGlyph.Of(FontAwesomeIcon.Check), ui.Accent, JamInviteCheckScale);
        Typography.DrawCentered(drawList, new Vector2(pill.Center.X + radius * 0.5f, pill.Center.Y), label, muted,
            TextStyles.FootnoteEmphasized);
    }
}
