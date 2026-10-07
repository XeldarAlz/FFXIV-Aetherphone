using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Hub;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games;

internal sealed partial class GamesApp
{
    private const string ConsentJoinId = "games.consent.join";
    private const string ConsentNotNowId = "games.consent.notNow";
    private const string ConsentPreviewRowId = "games.consent.preview";
    private const string HandlePrefix = "@";
    private const int ConsentSampleRank = 12;
    private const int ConsentSampleScore = 12480;
    private const int ConsentNeighbourScoreAbove = 12960;
    private const int ConsentNeighbourScoreBelow = 11730;
    private const float ConsentPadding = Metrics.Space.Lg;
    private const float ConsentHeroIconSize = 72f;
    private const float ConsentGlyphFraction = 0.5f;
    private const float ConsentRowHeight = 56f;
    private const float ConsentGhostAlpha = 0.45f;
    private const float ConsentGhostBarHeight = 10f;
    private const float ConsentGhostNameFraction = 0.42f;
    private const float ConsentGhostHandleFraction = 0.26f;
    private const float ConsentGhostValueWidth = 46f;
    private const float ConsentInfoGlyphBox = 32f;
    private const float ConsentInfoGlyphSize = 20f;
    private const float ConsentTopGap = Metrics.Space.Xxl;
    private const float ConsentBlockGap = Metrics.Space.Xl;
    private const float ConsentLineGap = Metrics.Space.Sm;
    private const float ConsentButtonGap = Metrics.Space.Sm;
    private const float ConsentBottomInset = Metrics.Space.Lg;
    private const float CompactMinHeight = 56f;
    private const float CompactIconSize = 28f;
    private const float CompactPadding = Metrics.Space.Md;

    private readonly FailureSlot participationFailure = new();
    private UserDto? handleUser;
    private string userHandle = string.Empty;

    private bool ShowsConsent()
    {
        if (!leaderboard.IsSignedIn || leaderboard.CurrentUser is null || leaderboard.OptedIn)
        {
            return false;
        }

        if (router.IsTransitioning || router.Current.Screen != GamesScreen.Root || currentGame is not null)
        {
            return false;
        }

        return leaderboard.NeedsConsent;
    }

    private bool ShowsConsentCompact => leaderboard.OptedOut;

    private void DrawConsent(Rect area, float scale)
    {
        var user = leaderboard.CurrentUser;
        if (user is null)
        {
            return;
        }

        var buttonHeight = Button.LargeHeight * scale;
        var gap = ConsentButtonGap * scale;
        var side = AppSurface.SidePadding * scale;
        var failure = ParticipationFailureText();
        var stackWidth = MathF.Max(1f, area.Width - side * 2f);
        var failureHeight = failure.Length > 0
            ? Typography.MeasureWrappedBlock(failure, TextStyles.Footnote, stackWidth).Y + gap
            : 0f;
        var stackHeight = buttonHeight * 2f + gap + failureHeight;
        var stackTop = area.Max.Y - ConsentBottomInset * scale - stackHeight;
        var body = new Rect(area.Min, new Vector2(area.Max.X, stackTop - Metrics.Space.Md * scale));
        using (AppSurface.Begin(body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var bottom = DrawConsentBody(drawList, user, origin.X, origin.Y, width, scale);
            FinishPage(origin, width, bottom, scale);
        }

        DrawConsentButtons(new Vector2(area.Min.X + side, stackTop), stackWidth, failure, scale);
    }

    private float DrawConsentBody(ImDrawListPtr drawList, UserDto user, float left, float top, float width,
        float scale)
    {
        var center = left + width * 0.5f;
        var y = top + ConsentTopGap * scale;
        var hero = ConsentHeroIconSize * scale;
        DrawGlyphTile(drawList, new Vector2(center - hero * 0.5f, y), hero, PhoneIcons.Crown, scale);
        y += hero + ConsentBlockGap * scale;
        y += Typography.DrawWrappedCentered(new Vector2(center, y), Loc.T(L.Leaderboard.ConsentTitle), ui.TitleInk,
            TextStyles.Title1, width);
        y += ConsentLineGap * scale;
        y += Typography.DrawWrappedCentered(new Vector2(center, y), Loc.T(L.Leaderboard.ConsentBody), ui.BodyInk,
            TextStyles.Body, width);
        y += ConsentBlockGap * scale;
        Typography.Draw(drawList, new Vector2(left, y),
            Typography.FitText(Loc.T(L.Leaderboard.ConsentPreview), width, TextStyles.FootnoteEmphasized),
            ui.MutedInk, TextStyles.FootnoteEmphasized);
        y += Typography.LineHeight(TextStyles.FootnoteEmphasized) + Metrics.Space.Xs * scale;
        y = DrawConsentPreview(drawList, user, left, y, width, scale) + ConsentBlockGap * scale;
        y = DrawConsentInfo(drawList, PhoneIcons.Eye, Loc.T(L.Leaderboard.ConsentShown), left, y, width, scale)
            + ConsentLineGap * scale * 2f;
        y = DrawConsentInfo(drawList, PhoneIcons.DeviceMobile, Loc.T(L.Leaderboard.ConsentOff), left, y, width,
            scale) + ConsentLineGap * scale * 2f;
        y = DrawConsentInfo(drawList, PhoneIcons.Settings, Loc.T(L.Leaderboard.ConsentSettings), left, y, width,
            scale);
        return y + ConsentBlockGap * scale;
    }

    private float DrawConsentPreview(ImDrawListPtr drawList, UserDto user, float left, float top, float width,
        float scale)
    {
        var rowHeight = ConsentRowHeight * scale;
        var pad = ConsentPadding * scale;
        var edge = Metrics.Space.Xs * scale;
        var max = new Vector2(left + width, top + rowHeight * 3f + edge * 2f);
        ui.Card(drawList, new Vector2(left, top), max, HubMetrics.CardRadius * scale);
        var rowLeft = left + pad;
        var rowRight = max.X - pad;
        var y = top + edge;
        DrawConsentGhostRow(drawList, new Rect(new Vector2(rowLeft, y), new Vector2(rowRight, y + rowHeight)),
            ConsentSampleRank - 1, ConsentNeighbourScoreAbove, scale);
        y += rowHeight;
        DrawBoardRow(drawList, new Rect(new Vector2(rowLeft, y), new Vector2(rowRight, y + rowHeight)),
            new BoardRow(ConsentPreviewRowId, ConsentSampleRank, SocialIdentity.Name(user.DisplayName, user.Handle),
                UserHandle(user), user.AvatarUrl, user.Badges, GameNumber.Label(ConsentSampleScore), true), true,
            ui.Accent, scale);
        y += rowHeight;
        DrawConsentGhostRow(drawList, new Rect(new Vector2(rowLeft, y), new Vector2(rowRight, y + rowHeight)),
            ConsentSampleRank + 1, ConsentNeighbourScoreBelow, scale);
        return max.Y;
    }

    private void DrawConsentGhostRow(ImDrawListPtr drawList, Rect row, int rank, int score, float scale)
    {
        var ghost = ui.MutedInk with { W = ui.MutedInk.W * ConsentGhostAlpha };
        var rankWidth = LeaderboardRankWidth * scale;
        var rankLabel = GameNumber.Label(rank);
        var rankSize = Typography.Measure(rankLabel, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(row.Min.X, row.Center.Y - rankSize.Y * 0.5f), rankLabel, ghost,
            TextStyles.Headline);
        var radius = LeaderboardAvatarRadius * scale;
        var avatarCenter = new Vector2(row.Min.X + rankWidth + LeaderboardTextGap * scale + radius, row.Center.Y);
        var fill = ImGui.GetColorU32(Surfaces.Fill(ui.TitleInk, FillLevel.Tertiary));
        drawList.AddCircleFilled(avatarCenter, radius, fill);
        var textLeft = avatarCenter.X + radius + LeaderboardTextGap * scale;
        var valueWidth = ConsentGhostValueWidth * scale;
        var textWidth = MathF.Max(1f, row.Max.X - valueWidth - Metrics.Space.Md * scale - textLeft);
        var bar = ConsentGhostBarHeight * scale;
        var nameTop = row.Center.Y - bar - Metrics.Space.Xxs * scale;
        Squircle.Fill(drawList, new Vector2(textLeft, nameTop),
            new Vector2(textLeft + textWidth * ConsentGhostNameFraction, nameTop + bar), bar * 0.5f, fill);
        var handleTop = row.Center.Y + Metrics.Space.Xxs * scale;
        Squircle.Fill(drawList, new Vector2(textLeft, handleTop),
            new Vector2(textLeft + textWidth * ConsentGhostHandleFraction, handleTop + bar), bar * 0.5f, fill);
        var value = GameNumber.Label(score);
        var valueSize = Typography.Measure(value, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(row.Max.X - valueSize.X, row.Center.Y - valueSize.Y * 0.5f), value,
            ghost, TextStyles.Headline);
    }

    private float DrawConsentInfo(ImDrawListPtr drawList, string glyph, string text, float left, float top,
        float width, float scale)
    {
        var box = ConsentInfoGlyphBox * scale;
        var textLeft = left + box + Metrics.Space.Md * scale;
        var textWidth = MathF.Max(1f, left + width - textLeft);
        var block = Typography.MeasureWrappedBlock(text, TextStyles.Subheadline, textWidth);
        var height = MathF.Max(box, block.Y);
        var firstLine = MathF.Min(block.Y, Typography.LineHeight(TextStyles.Subheadline));
        PhoneIcon.Draw(drawList, new Vector2(left + box * 0.5f, top + box * 0.5f), glyph, ui.Accent,
            ConsentInfoGlyphSize * scale);
        var textTop = top + MathF.Max(0f, (box - firstLine) * 0.5f);
        Typography.DrawWrappedLeft(new Vector2(textLeft, textTop), text, ui.BodyInk, TextStyles.Subheadline,
            textWidth);
        return top + MathF.Max(height, textTop - top + block.Y);
    }

    private void DrawConsentButtons(Vector2 topLeft, float width, string failure, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var y = topLeft.Y;
        if (failure.Length > 0)
        {
            y += Typography.DrawWrappedLeft(new Vector2(topLeft.X, y), failure, ui.Theme.Danger, TextStyles.Footnote,
                width) + ConsentButtonGap * scale;
        }

        var saving = leaderboard.SavingParticipation;
        var buttonHeight = Button.LargeHeight * scale;
        var join = new Rect(new Vector2(topLeft.X, y), new Vector2(topLeft.X + width, y + buttonHeight));
        y += buttonHeight + ConsentButtonGap * scale;
        var notNow = new Rect(new Vector2(topLeft.X, y), new Vector2(topLeft.X + width, y + buttonHeight));
        if (Button.Draw(drawList, join, Loc.T(L.Leaderboard.Join), ui.Ink, enabled: !saving, id: ConsentJoinId))
        {
            leaderboard.SetParticipation(true);
        }

        if (Button.Draw(drawList, notNow, Loc.T(L.Leaderboard.NotNow), ui.Ink, ButtonStyle.Gray, enabled: !saving,
                id: ConsentNotNowId))
        {
            leaderboard.DeclineConsent();
        }
    }

    private float DrawConsentCompact(float left, float top, float width, float scale, string joinId)
    {
        var failure = ParticipationFailureText();
        var message = failure.Length > 0 ? failure : Loc.T(L.Leaderboard.NotOnBoards);
        var bottom = DrawCompactCard(ImGui.GetWindowDrawList(), left, top, width, scale, PhoneIcons.Crown, message,
            failure.Length > 0 ? ui.Theme.Danger : ui.TitleInk, Loc.T(L.Leaderboard.JoinShort), joinId,
            !leaderboard.SavingParticipation, out var join);
        if (join)
        {
            leaderboard.SetParticipation(true);
        }

        return bottom;
    }

    private float DrawCompactCard(ImDrawListPtr drawList, float left, float top, float width, float scale,
        string glyph, string message, Vector4 messageInk, string buttonLabel, string buttonId, bool buttonEnabled,
        out bool clicked)
    {
        clicked = false;
        var pad = ConsentPadding * scale;
        var icon = CompactIconSize * scale;
        var hasButton = buttonLabel.Length > 0;
        var buttonWidth = hasButton ? Button.WidthFor(buttonLabel, ButtonSize.Small) : 0f;
        var textLeft = left + pad + icon + Metrics.Space.Md * scale;
        var textRight = left + width - pad - (hasButton ? buttonWidth + Metrics.Space.Md * scale : 0f);
        var textWidth = MathF.Max(1f, textRight - textLeft);
        var block = Typography.MeasureWrappedBlock(message, TextStyles.Subheadline, textWidth);
        var height = MathF.Max(CompactMinHeight * scale, block.Y + CompactPadding * 2f * scale);
        var max = new Vector2(left + width, top + height);
        ui.Card(drawList, new Vector2(left, top), max, HubMetrics.CardRadius * scale);
        var centerY = top + height * 0.5f;
        DrawGlyphTile(drawList, new Vector2(left + pad, centerY - icon * 0.5f), icon, glyph, scale);
        Typography.DrawWrappedLeft(new Vector2(textLeft, centerY - block.Y * 0.5f), message, messageInk,
            TextStyles.Subheadline, textWidth);
        if (!hasButton)
        {
            return max.Y;
        }

        var buttonHeight = Button.SmallHeight * scale;
        var button = new Rect(new Vector2(max.X - pad - buttonWidth, centerY - buttonHeight * 0.5f),
            new Vector2(max.X - pad, centerY + buttonHeight * 0.5f));
        clicked = Button.Draw(drawList, button, buttonLabel, ui.Ink, ButtonStyle.Tinted, enabled: buttonEnabled,
            id: buttonId);
        return max.Y;
    }

    private void DrawGlyphTile(ImDrawListPtr drawList, Vector2 min, float size, string glyph, float scale)
    {
        var max = min + new Vector2(size, size);
        IconTile.FillShaded(drawList, min, max, GameIconArt.Radius(size), IconTile.Surface(ui.Accent));
        PhoneIcon.Draw(drawList, (min + max) * 0.5f, glyph, AccentRing.Ink, size * ConsentGlyphFraction);
    }

    private string UserHandle(UserDto user)
    {
        if (ReferenceEquals(handleUser, user))
        {
            return userHandle;
        }

        handleUser = user;
        userHandle = HandlePrefix + user.Handle;
        return userHandle;
    }

    private string ParticipationFailureText()
    {
        participationFailure.Set(leaderboard.ParticipationFailure);
        return participationFailure.Failed ? participationFailure.Text() : string.Empty;
    }
}
