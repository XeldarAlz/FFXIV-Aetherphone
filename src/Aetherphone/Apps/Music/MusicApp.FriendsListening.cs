using Aetherphone.Apps.Music.Components;
using Aetherphone.Apps.Music.Jam;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Core.Songs;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const string FriendsContextPrefix = "friends.";
    private const float FriendAvatarRadius = 15f;
    private const float FriendAvatarInset = 8f;
    private const float FriendAvatarRing = 2f;
    private const float FriendMonogramScale = 0.62f;
    private const float FriendJamPillHeight = 28f;
    private const float FriendJamGlyphScale = 0.62f;
    private const float FriendJamHoverLighten = 0.08f;
    private const float FriendLiveDot = 3f;
    private const int FriendSegments = 24;
    private const float ListeningPromptTile = 40f;
    private const float ListeningPromptGlyphScale = 1.05f;
    private const float ListeningPromptButtonHeight = 34f;
    private const float ListeningPromptGlowCoverage = 0.55f;
    private const float ListeningPromptGlowStrength = 0.18f;

    private readonly ListeningPresence listening;
    private readonly ShelfRail friendsRail = new();
    private ListeningFriendDto[] friendsShown = Array.Empty<ListeningFriendDto>();
    private JamTextCache[] friendAgeText = Array.Empty<JamTextCache>();
    private int friendsVersion = -1;

    private void DrawListeningPrompt(float scale)
    {
        if (configuration.ListeningPromptShown || !session.IsSignedIn)
        {
            return;
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        var pad = Metrics.Space.Lg * scale;
        var tile = ListeningPromptTile * scale;
        var width = ScrollLayout.StableContentWidth() - MusicUi.Inset * scale * 2f;
        var textWidth = MathF.Max(1f, width - pad * 3f - tile);
        var titleLines = Typography.WrapText(Loc.T(L.Music.Friends.PromptTitle), TextStyles.Headline, textWidth);
        var bodyLines = Typography.WrapText(Loc.T(L.Music.Friends.PromptBody), TextStyles.Footnote, textWidth);
        var titleLine = Typography.LineHeight(TextStyles.Headline);
        var bodyLine = Typography.LineHeight(TextStyles.Footnote);
        var textHeight = titleLines.Length * titleLine + Metrics.Space.Xs * scale + bodyLines.Length * bodyLine;
        var buttonHeight = ListeningPromptButtonHeight * scale;
        var card = BeginJamBlock(pad + MathF.Max(tile, textHeight) + Metrics.Space.Lg * scale + buttonHeight + pad);
        var drawList = ImGui.GetWindowDrawList();
        var rounding = Metrics.Radius.Card * scale;
        Material.ThemedGlass(drawList, card.Min, card.Max, rounding, scale, theme);
        Material.TopGlow(drawList, card.Min, card.Max, rounding, ui.Accent, ListeningPromptGlowCoverage,
            ListeningPromptGlowStrength);
        var left = card.Min.X + pad;
        var top = card.Min.Y + pad;
        var tileMin = new Vector2(left, top);
        IconTile.FillShaded(drawList, tileMin, tileMin + new Vector2(tile, tile), tile * Metrics.Radius.TileFactor,
            IconTile.Surface(ui.Accent));
        AppSkin.Icon(drawList, tileMin + new Vector2(tile * 0.5f, tile * 0.5f), IconGlyph.Of(FontAwesomeIcon.Headphones),
            AccentRing.Ink, ListeningPromptGlyphScale);
        var textLeft = left + tile + pad;
        var lineTop = top;
        for (var lineIndex = 0; lineIndex < titleLines.Length; lineIndex++)
        {
            Typography.Draw(drawList, new Vector2(textLeft, lineTop), titleLines[lineIndex], ui.TitleInk,
                TextStyles.Headline);
            lineTop += titleLine;
        }

        lineTop += Metrics.Space.Xs * scale;
        for (var lineIndex = 0; lineIndex < bodyLines.Length; lineIndex++)
        {
            Typography.Draw(drawList, new Vector2(textLeft, lineTop), bodyLines[lineIndex], ui.MutedInk,
                TextStyles.Footnote);
            lineTop += bodyLine;
        }

        var buttonTop = card.Max.Y - pad - buttonHeight;
        var half = (card.Width - pad * 2f - Metrics.Space.Sm * scale) * 0.5f;
        var notNow = new Rect(new Vector2(left, buttonTop), new Vector2(left + half, buttonTop + buttonHeight));
        var share = new Rect(new Vector2(card.Max.X - pad - half, buttonTop),
            new Vector2(card.Max.X - pad, buttonTop + buttonHeight));
        var declined = ui.GhostButton(notNow, Loc.T(L.Music.Friends.NotNow));
        var accepted = ui.PillButton(share, Loc.T(L.Music.Friends.Share), true, "music.listening.share");
        EndJamBlock();
        if (!declined && !accepted)
        {
            return;
        }

        configuration.ListeningPromptShown = true;
        configuration.ShareListeningActivity = accepted;
        configuration.Save();
    }

    private void DrawFriendsListening(float scale)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        listening.Touch();
        EnsureFriendsListening();
        if (friendsShown.Length == 0)
        {
            return;
        }

        var side = ArtworkTile.Side(ArtworkTile.Standard);
        friendsRail.Begin(ui, Loc.T(L.Music.Friends.ShelfTitle), false, friendsShown.Length, side,
            side + FriendCaptionHeight(scale));
        var drawList = ImGui.GetWindowDrawList();
        var nowUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (var index = 0; index < friendsShown.Length; index++)
        {
            if (!friendsRail.Tile(index, out var tile))
            {
                continue;
            }

            var friend = friendsShown[index];
            var track = friend.Track!;
            var jamCode = JoinableJamCode(friend);
            var jamPill = FriendJamPill(tile.Min, side, scale);
            var jamHovered = jamCode.Length > 0 && friendsRail.Hover(jamPill);
            var hovered = !jamHovered && friendsRail.Hover(tile);
            ArtworkTile.Draw(drawList, images, tile.Min, side, track.ThumbnailUrl ?? string.Empty, track.VideoId);
            ArtworkTile.DrawPressed(drawList, tile.Min, side, hovered);
            DrawFriendAvatar(drawList, tile.Min, friend, scale);
            if (jamCode.Length > 0)
            {
                DrawFriendJamPill(drawList, jamPill, jamHovered, scale);
            }

            DrawFriendCaption(drawList, tile.Min, side, friend, index, nowUnixMilliseconds, scale);
            if (jamHovered && friendsRail.Tapped(jamPill, jamHovered))
            {
                jamLauncher.RequestLobby(jamCode);
                continue;
            }

            if (friendsRail.Tapped(tile, hovered))
            {
                PlayFriendSong(friend);
            }
        }

        friendsRail.End();
    }

    private void EnsureFriendsListening()
    {
        var version = listening.Version;
        if (version == friendsVersion)
        {
            return;
        }

        friendsVersion = version;
        friendsShown = listening.Friends;
        if (friendAgeText.Length != friendsShown.Length)
        {
            friendAgeText = new JamTextCache[friendsShown.Length];
        }
    }

    private string JoinableJamCode(ListeningFriendDto friend)
    {
        var code = friend.JamCode;
        if (string.IsNullOrEmpty(code))
        {
            return string.Empty;
        }

        return jam.InJam && string.Equals(jam.Code, code, StringComparison.Ordinal) ? string.Empty : code;
    }

    private static float FriendCaptionHeight(float scale) =>
        ArtworkTile.CaptionGap * scale + Typography.LineHeight(TextStyles.FootnoteEmphasized)
        + Typography.LineHeight(TextStyles.Footnote) * 2f;

    private static Rect FriendJamPill(Vector2 artMin, float side, float scale)
    {
        var inset = FriendAvatarInset * scale;
        var height = FriendJamPillHeight * scale;
        var bottom = artMin.Y + side - inset;
        return new Rect(new Vector2(artMin.X + inset, bottom - height), new Vector2(artMin.X + side - inset, bottom));
    }

    private void DrawFriendAvatar(ImDrawListPtr drawList, Vector2 artMin, ListeningFriendDto friend, float scale)
    {
        var radius = FriendAvatarRadius * scale;
        var inset = FriendAvatarInset * scale;
        var center = new Vector2(artMin.X + inset + radius, artMin.Y + inset + radius);
        drawList.AddCircleFilled(center, radius + FriendAvatarRing * scale,
            ImGui.GetColorU32(ui.Palette.BackdropBottom), FriendSegments);
        var name = SocialIdentity.Name(friend.DisplayName ?? string.Empty, friend.Handle ?? string.Empty);
        AvatarView.DrawRemote(drawList, center, radius, theme, name, string.Empty, friend.AvatarUrl, images, lodestone,
            FriendMonogramScale, FriendSegments);
    }

    private void DrawFriendJamPill(ImDrawListPtr drawList, Rect pill, bool hovered, float scale)
    {
        var fill = hovered ? Palette.Lighten(ui.Accent, FriendJamHoverLighten) : ui.Accent;
        Squircle.Fill(drawList, pill.Min, pill.Max, pill.Height * 0.5f, ImGui.GetColorU32(fill));
        var glyphCenter = new Vector2(pill.Min.X + pill.Height * 0.5f + Metrics.Space.Xs * scale, pill.Center.Y);
        DrawJamGlyph(drawList, glyphCenter, AccentRing.Ink, FriendJamGlyphScale);
        var textLeft = glyphCenter.X + pill.Height * 0.5f;
        var textWidth = MathF.Max(1f, pill.Max.X - Metrics.Space.Sm * scale - textLeft);
        var label = Typography.FitText(Loc.T(L.Music.Friends.JoinJam), textWidth, TextStyles.FootnoteEmphasized);
        var textHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, pill.Center.Y - textHeight * 0.5f), label, AccentRing.Ink,
            TextStyles.FootnoteEmphasized);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    private void DrawFriendCaption(ImDrawListPtr drawList, Vector2 artMin, float side, ListeningFriendDto friend,
        int index, long nowUnixMilliseconds, float scale)
    {
        var track = friend.Track!;
        var title = string.IsNullOrEmpty(track.Title) ? track.VideoId : track.Title;
        var name = SocialIdentity.Name(friend.DisplayName ?? string.Empty, friend.Handle ?? string.Empty);
        ArtworkTile.DrawCaption(drawList, ui, artMin, side, title, name);
        var top = artMin.Y + side + ArtworkTile.CaptionGap * scale + Typography.LineHeight(TextStyles.FootnoteEmphasized)
            + Typography.LineHeight(TextStyles.Footnote);
        var status = ListeningCadence.Status(friend.Paused, friend.UpdatedAtUnixMs, nowUnixMilliseconds);
        var left = artMin.X;
        var ink = ui.MutedInk;
        if (status == ListeningStatus.Now)
        {
            var dot = FriendLiveDot * scale;
            var lineHeight = Typography.LineHeight(TextStyles.Footnote);
            drawList.AddCircleFilled(new Vector2(left + dot, top + lineHeight * 0.5f), dot,
                ImGui.GetColorU32(ui.Accent), FriendSegments);
            left += dot * 2f + Metrics.Space.Xs * scale;
            ink = ui.Accent;
        }

        var label = FriendStatusLabel(status, friend.UpdatedAtUnixMs, nowUnixMilliseconds, index);
        var fitted = Typography.FitText(label, MathF.Max(1f, artMin.X + side - left), TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(left, top), fitted, ink, TextStyles.Footnote);
    }

    private string FriendStatusLabel(ListeningStatus status, long updatedAtUnixMilliseconds,
        long nowUnixMilliseconds, int index)
    {
        switch (status)
        {
            case ListeningStatus.Now:
                return Loc.T(L.Music.Friends.ListeningNow);
            case ListeningStatus.Paused:
                return Loc.T(L.Music.Friends.Paused);
            default:
                var minutes = ListeningCadence.MinutesAgo(updatedAtUnixMilliseconds, nowUnixMilliseconds);
                return friendAgeText[index].Format(Loc.T(L.Music.Friends.MinutesAgo), minutes);
        }
    }

    private void PlayFriendSong(ListeningFriendDto friend)
    {
        var track = friend.Track!;
        var duration = track.DurationSeconds is { } seconds ? (int)Math.Max(0d, seconds) : 0;
        var song = new Song(track.VideoId, track.Title ?? string.Empty, track.Author ?? string.Empty,
            track.ThumbnailUrl ?? string.Empty, duration);
        playback.PlaySongs(new[] { song }, 0, FriendsContextPrefix + friend.UserId,
            Loc.T(L.Music.Friends.ShelfTitle));
    }
}
