using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games;

internal sealed partial class GamesApp
{
    private const string ConsentJoinId = "games.consent.join";
    private const string ConsentNotNowId = "games.consent.notNow";
    private const string ConsentPreviewRowId = "games.consent.preview";
    private const int ConsentSampleRank = 12;
    private const int ConsentSampleScore = 12480;

    private readonly FailureSlot participationFailure = new();
    private bool consentRequested;
    private UserDto? consentHandleUser;
    private string consentHandle = string.Empty;
    private float consentLeft;
    private float consentWidth;

    private bool ShowsConsent()
    {
        if (!leaderboard.IsSignedIn || leaderboard.CurrentUser is null || leaderboard.OptedIn)
        {
            consentRequested = false;
            return false;
        }

        if (router.IsTransitioning || router.Current.Screen != GamesScreen.Root || currentGame is not null)
        {
            return false;
        }

        return consentRequested || leaderboard.NeedsConsent;
    }

    private void RequestConsent()
    {
        leaderboard.ClearParticipationFailure();
        consentRequested = true;
    }

    private void DrawConsent(Rect area, float scale)
    {
        var user = leaderboard.CurrentUser;
        if (user is null)
        {
            return;
        }

        var buttonHeight = Button.LargeHeight * scale;
        var notNowTop = area.Max.Y - Metrics.Space.Sm * scale - buttonHeight;
        var joinTop = notNowTop - Metrics.Space.Sm * scale - buttonHeight;
        var body = new Rect(area.Min, new Vector2(area.Max.X, joinTop - Metrics.Space.Md * scale));
        using (AppSurface.Begin(body))
        {
            DrawConsentCopy(user, scale);
        }

        var saving = leaderboard.SavingParticipation;
        var right = consentLeft + consentWidth;
        var join = new Rect(new Vector2(consentLeft, joinTop), new Vector2(right, joinTop + buttonHeight));
        if (Button.Draw(join, Loc.T(L.Leaderboard.Join), ui.Ink, enabled: !saving, id: ConsentJoinId))
        {
            leaderboard.SetParticipation(true);
        }

        var notNow = new Rect(new Vector2(consentLeft, notNowTop), new Vector2(right, notNowTop + buttonHeight));
        if (!Button.Draw(notNow, Loc.T(L.Leaderboard.NotNow), ui.Ink, ButtonStyle.Plain, enabled: !saving,
                id: ConsentNotNowId))
        {
            return;
        }

        leaderboard.DeclineConsent();
        consentRequested = false;
    }

    private void DrawConsentCopy(UserDto user, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        consentLeft = origin.X;
        consentWidth = width;
        var left = origin.X;
        var y = origin.Y + Metrics.Space.Sm * scale;
        y = DrawConsentLine(Loc.T(L.Leaderboard.ConsentTitle), ui.TitleInk, TextStyles.Title1, left, y, width)
            + Metrics.Space.Md * scale;
        y = DrawConsentLine(Loc.T(L.Leaderboard.ConsentBody), ui.TitleInk, TextStyles.Body, left, y, width)
            + Metrics.Space.Md * scale;
        y = DrawConsentLine(Loc.T(L.Leaderboard.ConsentShown), ui.BodyInk, TextStyles.Subheadline, left, y, width)
            + Metrics.Space.Lg * scale;
        GamesHubArt.Section(drawList, ui, left, y, width, Loc.T(L.Leaderboard.ConsentPreview), string.Empty,
            string.Empty);
        y += GamesHubArt.SectionHeight * scale;
        ImGui.SetCursorScreenPos(new Vector2(left, y));
        var card = GroupCard.Begin(ui, 1, LeaderboardRowHeight);
        DrawLeaderboardRow(drawList, card.NextRow(), ConsentPreviewRowId, ConsentSampleRank,
            SocialIdentity.Name(user.DisplayName, user.Handle), ConsentHandle(user), user.AvatarUrl, user.Badges,
            GameNumber.Label(ConsentSampleScore), true, ui.Accent, scale);
        card.End();
        y = card.Bounds.Max.Y + Metrics.Space.Lg * scale;
        y = DrawConsentLine(Loc.T(L.Leaderboard.ConsentOff), ui.BodyInk, TextStyles.Subheadline, left, y, width)
            + Metrics.Space.Md * scale;
        y = DrawConsentLine(Loc.T(L.Leaderboard.ConsentSettings), ui.MutedInk, TextStyles.Footnote, left, y,
            width);
        var failure = ParticipationFailureText();
        if (failure.Length > 0)
        {
            y = DrawConsentLine(failure, ui.Theme.Danger, TextStyles.Footnote, left, y + Metrics.Space.Md * scale,
                width);
        }

        FinishPage(origin, width, y, scale);
    }

    private string ConsentHandle(UserDto user)
    {
        if (ReferenceEquals(consentHandleUser, user))
        {
            return consentHandle;
        }

        consentHandleUser = user;
        consentHandle = LeaderboardHandlePrefix + user.Handle;
        return consentHandle;
    }

    private string ParticipationFailureText()
    {
        participationFailure.Set(leaderboard.ParticipationFailure);
        return participationFailure.Failed ? participationFailure.Text() : string.Empty;
    }

    private static float DrawConsentLine(string text, Vector4 ink, in TextStyle style, float left, float top,
        float width) =>
        top + Typography.DrawWrappedLeft(new Vector2(left, top), text, ink, style, width);
}
