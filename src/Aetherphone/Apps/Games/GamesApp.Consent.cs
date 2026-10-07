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
    private const float ConsentPadding = Metrics.Space.Lg;
    private const float ConsentIconSize = 40f;
    private const float ConsentGlyphFraction = 0.5f;
    private const float ConsentPreviewHeight = 56f;
    private const float ConsentPreviewRadius = 14f;
    private const float ConsentPreviewInset = Metrics.Space.Md;
    private const float ConsentBlockGap = Metrics.Space.Lg;
    private const float ConsentLineGap = Metrics.Space.Xs;
    private const float ConsentButtonGap = Metrics.Space.Sm;
    private const float CompactMinHeight = 56f;
    private const float CompactIconSize = 28f;
    private const float CompactPadding = Metrics.Space.Md;

    private readonly FailureSlot participationFailure = new();
    private bool consentRequested;
    private UserDto? handleUser;
    private string userHandle = string.Empty;

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

    private bool ShowsConsentCompact => leaderboard.OptedOut;

    private void DrawConsent(Rect area, float scale)
    {
        var user = leaderboard.CurrentUser;
        if (user is null)
        {
            return;
        }

        using (AppSurface.Begin(area))
        {
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var height = ConsentCardHeight(width, scale);
            var slack = area.Max.Y - origin.Y - height;
            var top = origin.Y + MathF.Max(Metrics.Space.Xl * scale, slack * 0.5f);
            DrawConsentCard(drawList, user, origin.X, top, width, height, scale);
            FinishPage(origin, width, top + height, scale);
        }
    }

    private float ConsentCardHeight(float width, float scale)
    {
        var inner = MathF.Max(1f, width - ConsentPadding * 2f * scale);
        var height = (ConsentPadding * 2f + ConsentIconSize + ConsentPreviewHeight + Button.RegularHeight
                      + ConsentBlockGap * 4f + ConsentLineGap * 4f) * scale
                     + Typography.MeasureWrappedBlock(Loc.T(L.Leaderboard.ConsentTitle), TextStyles.Headline, inner).Y
                     + Typography.MeasureWrappedBlock(Loc.T(L.Leaderboard.ConsentBody), TextStyles.Footnote, inner).Y
                     + Typography.MeasureWrappedBlock(Loc.T(L.Leaderboard.ConsentShown), TextStyles.Footnote, inner).Y
                     + Typography.LineHeight(TextStyles.FootnoteEmphasized)
                     + Typography.MeasureWrappedBlock(Loc.T(L.Leaderboard.ConsentOff), TextStyles.Footnote, inner).Y
                     + Typography.MeasureWrappedBlock(Loc.T(L.Leaderboard.ConsentSettings), TextStyles.Footnote,
                         inner).Y;
        var failure = ParticipationFailureText();
        if (failure.Length > 0)
        {
            height += ConsentLineGap * scale + Typography.MeasureWrappedBlock(failure, TextStyles.Footnote, inner).Y;
        }

        return height;
    }

    private void DrawConsentCard(ImDrawListPtr drawList, UserDto user, float left, float top, float width,
        float height, float scale)
    {
        ui.Card(drawList, new Vector2(left, top), new Vector2(left + width, top + height),
            HubMetrics.CardRadius * scale);
        var pad = ConsentPadding * scale;
        var inner = MathF.Max(1f, width - pad * 2f);
        var x = left + pad;
        var y = top + pad;
        DrawGlyphTile(drawList, new Vector2(x, y), ConsentIconSize * scale, PhoneIcons.Crown, scale);
        y += (ConsentIconSize + ConsentBlockGap) * scale;
        y = ConsentLine(Loc.T(L.Leaderboard.ConsentTitle), ui.TitleInk, TextStyles.Headline, x, y, inner)
            + ConsentLineGap * scale;
        y = ConsentLine(Loc.T(L.Leaderboard.ConsentBody), ui.MutedInk, TextStyles.Footnote, x, y, inner)
            + ConsentLineGap * scale;
        y = ConsentLine(Loc.T(L.Leaderboard.ConsentShown), ui.BodyInk, TextStyles.Footnote, x, y, inner)
            + ConsentBlockGap * scale;
        Typography.Draw(drawList, new Vector2(x, y),
            Typography.FitText(Loc.T(L.Leaderboard.ConsentPreview), inner, TextStyles.FootnoteEmphasized),
            ui.MutedInk, TextStyles.FootnoteEmphasized);
        y += Typography.LineHeight(TextStyles.FootnoteEmphasized) + ConsentLineGap * scale;
        var preview = new Rect(new Vector2(x, y), new Vector2(x + inner, y + ConsentPreviewHeight * scale));
        Squircle.Fill(drawList, preview.Min, preview.Max, ConsentPreviewRadius * scale,
            ImGui.GetColorU32(Surfaces.Fill(ui.TitleInk, FillLevel.Tertiary)));
        var rowInset = ConsentPreviewInset * scale;
        var row = new Rect(new Vector2(preview.Min.X + rowInset, preview.Min.Y),
            new Vector2(preview.Max.X - rowInset, preview.Max.Y));
        DrawBoardRow(drawList, row, new BoardRow(ConsentPreviewRowId, ConsentSampleRank,
            SocialIdentity.Name(user.DisplayName, user.Handle), UserHandle(user), user.AvatarUrl, user.Badges,
            GameNumber.Label(ConsentSampleScore), true), false, ui.Accent, scale);
        y = preview.Max.Y + ConsentBlockGap * scale;
        y = ConsentLine(Loc.T(L.Leaderboard.ConsentOff), ui.BodyInk, TextStyles.Footnote, x, y, inner)
            + ConsentLineGap * scale;
        y = ConsentLine(Loc.T(L.Leaderboard.ConsentSettings), ui.MutedInk, TextStyles.Footnote, x, y, inner);
        var failure = ParticipationFailureText();
        if (failure.Length > 0)
        {
            y = ConsentLine(failure, ui.Theme.Danger, TextStyles.Footnote, x, y + ConsentLineGap * scale, inner);
        }

        y += ConsentBlockGap * scale;
        var saving = leaderboard.SavingParticipation;
        var gap = ConsentButtonGap * scale;
        var buttonWidth = (inner - gap) * 0.5f;
        var buttonHeight = Button.RegularHeight * scale;
        var notNow = new Rect(new Vector2(x, y), new Vector2(x + buttonWidth, y + buttonHeight));
        var join = new Rect(new Vector2(notNow.Max.X + gap, y), new Vector2(x + inner, y + buttonHeight));
        if (Button.Draw(drawList, join, Loc.T(L.Leaderboard.Join), ui.Ink, enabled: !saving, id: ConsentJoinId))
        {
            leaderboard.SetParticipation(true);
        }

        if (!Button.Draw(drawList, notNow, Loc.T(L.Leaderboard.NotNow), ui.Ink, ButtonStyle.Gray, enabled: !saving,
                id: ConsentNotNowId))
        {
            return;
        }

        leaderboard.DeclineConsent();
        consentRequested = false;
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

    private static float ConsentLine(string text, Vector4 ink, in TextStyle style, float left, float top,
        float width) =>
        top + Typography.DrawWrappedLeft(new Vector2(left, top), text, ink, style, width);
}
