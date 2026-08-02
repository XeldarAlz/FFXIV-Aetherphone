using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.AetherStream;

internal sealed partial class AetherStreamApp
{
    private const float JoinDebounceSeconds = 0.20f;
    private const float JoinRowHeight = 52f;
    private const float NearbyRefreshIntervalSeconds = 5f;

    private string joinQuery = string.Empty;
    private string joinApplied = string.Empty;
    private float joinDebounce;
    private UserDto[] joinResults = Array.Empty<UserDto>();
    private bool joinSearching;
    // Distinguishes "the request came back with zero matches" from "the request itself failed"
    // (bad status, timeout, exception) - AccountClient.SearchAsync returns null either way, which
    // used to render identically to a genuine empty search. See HttpService.SendForJsonAsync's
    // new non-2xx log line for the server-side half of this same gap.
    private bool joinSearchFailed;
    private float nearbyRefreshTimer = NearbyRefreshIntervalSeconds; // due immediately on first draw

    // Two ways onto someone's screen: search-by-person (mutual-contact/block checks happen
    // entirely server-side, via AccountClient.SearchAsync - the general /users/search endpoint,
    // not the mention-suggest one used for @-mentions elsewhere, since that DTO has no World
    // field and results here need to show it, mirroring SocialFeedStore.Search's own use of the
    // same endpoint), or "Nearby" - zone-scoped discovery (stream.nearby) with no contact
    // relationship required at all, refreshed on a timer while this screen is open.
    private void DrawJoinScreen(PhoneContext context, Rect area, float scale)
    {
        var accentedTheme = AccentedTheme(context.Theme);
        var accentedContext = new PhoneContext(context.Content, accentedTheme, context.Navigation);
        AppHeader.Draw(accentedContext, Loc.T(L.AetherStream.JoinStream), () => router.Pop());

        TickNearbyRefresh();

        var margin = Metrics.Space.Lg * scale;
        var top = area.Min.Y + AppHeader.Height * scale + Metrics.Space.Sm * scale;
        var content = new Rect(new Vector2(area.Min.X + margin, top), new Vector2(area.Max.X - margin, area.Max.Y));

        var fieldRect = new Rect(content.Min, new Vector2(content.Max.X, content.Min.Y + 36f * scale));
        SearchField.Draw(fieldRect, "##aetherstreamJoinSearch", Loc.T(L.AetherStream.JoinSearchHint), ref joinQuery,
            accentedTheme);
        TickJoinSearch();

        var listTop = fieldRect.Max.Y + 10f * scale;
        var nearby = watchAlong.Nearby;
        if (joinQuery.Trim().Length == 0 && nearby.Count > 0)
        {
            Typography.Draw(new Vector2(content.Min.X, listTop), Loc.T(L.AetherStream.JoinNearbyHeader),
                accentedTheme.TextMuted, TextStyles.Caption1);
            listTop += 20f * scale;
        }

        var listRect = new Rect(new Vector2(content.Min.X, listTop), content.Max);
        using (AppSurface.Begin(listRect))
        {
            var rowHeight = JoinRowHeight * scale;
            var cursorY = listRect.Min.Y;

            // Nearby streams only show while the search box is empty - once the user starts
            // typing a name, that's an explicit "I know who I want" search, not zone browsing.
            if (joinQuery.Trim().Length == 0)
            {
                for (var index = 0; index < nearby.Count; index++)
                {
                    var rowRect = new Rect(new Vector2(listRect.Min.X, cursorY),
                        new Vector2(listRect.Max.X, cursorY + rowHeight));
                    DrawNearbyResultRow(rowRect, nearby[index], accentedTheme, scale);
                    cursorY = rowRect.Max.Y;
                }
            }

            if (joinResults.Length == 0 && (joinQuery.Trim().Length > 0 || nearby.Count == 0))
            {
                var message = joinSearching ? Loc.T(L.Social.MentionSearching)
                    : joinSearchFailed ? Loc.T(L.AetherStream.JoinSearchFailed)
                    : Loc.T(L.PhotoTag.NoPeople);
                Typography.DrawCentered(new Vector2(listRect.Center.X, cursorY + 40f * scale), message,
                    accentedTheme.TextMuted, TextStyles.Subheadline.Scale);
            }

            for (var index = 0; index < joinResults.Length; index++)
            {
                var row = joinResults[index];
                var rowRect = new Rect(new Vector2(listRect.Min.X, cursorY), new Vector2(listRect.Max.X, cursorY + rowHeight));
                DrawJoinResultRow(rowRect, row, accentedTheme, scale);
                cursorY = rowRect.Max.Y;
            }

            ImGui.SetCursorScreenPos(listRect.Min);
            ImGui.Dummy(new Vector2(listRect.Width, cursorY - listRect.Min.Y + Metrics.Space.Lg * scale));
        }
    }

    private void DrawNearbyResultRow(Rect rect, NearbyStream row, PhoneTheme theme, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, 10f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.06f)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var avatarRadius = 18f * scale;
        var avatarCenter = new Vector2(rect.Min.X + 8f * scale + avatarRadius, rect.Center.Y);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, row.Name, row.World, null, remoteImages,
            lodestone, 0.8f, 28);

        var textLeft = avatarCenter.X + avatarRadius + 12f * scale;
        Typography.Draw(new Vector2(textLeft, rect.Center.Y - 16f * scale), row.DisplayName, theme.TextStrong,
            TextStyles.Body);
        Typography.Draw(new Vector2(textLeft, rect.Center.Y + 2f * scale), $"{row.Name}  ·  {row.World}",
            theme.TextMuted, TextStyles.Caption1);

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            watchAlong.Join(row.HostId);
            router.Pop();
        }
    }

    private void TickNearbyRefresh()
    {
        nearbyRefreshTimer += ImGui.GetIO().DeltaTime;
        if (nearbyRefreshTimer < NearbyRefreshIntervalSeconds)
        {
            return;
        }

        nearbyRefreshTimer = 0f;
        watchAlong.RequestNearbyStreams();
    }

    private void DrawJoinResultRow(Rect rect, UserDto row, PhoneTheme theme, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, 10f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.06f)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var avatarRadius = 18f * scale;
        var avatarCenter = new Vector2(rect.Min.X + 8f * scale + avatarRadius, rect.Center.Y);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, row.Name, row.World, row.AvatarUrl,
            remoteImages, lodestone, 0.8f, 28);

        var textLeft = avatarCenter.X + avatarRadius + 12f * scale;
        Typography.Draw(new Vector2(textLeft, rect.Center.Y - 16f * scale), row.DisplayName, theme.TextStrong,
            TextStyles.Body);
        Typography.Draw(new Vector2(textLeft, rect.Center.Y + 2f * scale), $"{row.Name}  ·  {row.World}",
            theme.TextMuted, TextStyles.Caption1);

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            watchAlong.Join(row.Id);
            router.Pop();
        }
    }

    private void TickJoinSearch()
    {
        var trimmed = joinQuery.Trim();
        if (trimmed.Length == 0)
        {
            joinApplied = string.Empty;
            joinResults = Array.Empty<UserDto>();
            joinSearching = false;
            joinSearchFailed = false;
            return;
        }

        if (string.Equals(trimmed, joinApplied, StringComparison.Ordinal))
        {
            return;
        }

        joinDebounce += ImGui.GetIO().DeltaTime;
        if (joinDebounce < JoinDebounceSeconds)
        {
            return;
        }

        joinDebounce = 0f;
        joinApplied = trimmed;
        joinSearching = true;
        joinSearchFailed = false;
        joinWork.Run("join search", async token =>
        {
            var result = await joinAccount.SearchAsync(trimmed, token).ConfigureAwait(false);
            // A null result means the request itself failed (see AccountClient.SearchAsync /
            // HttpService.SendForJsonAsync) - clear any stale prior results instead of leaving
            // them on screen, and flag it separately from a genuine zero-match search.
            joinResults = result?.Users ?? Array.Empty<UserDto>();
            joinSearchFailed = result is null;
        }, () => joinSearching = false);
    }
}
