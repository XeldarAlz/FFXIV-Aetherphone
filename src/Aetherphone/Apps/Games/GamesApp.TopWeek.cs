using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Hub;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games;

internal sealed partial class GamesApp
{
    private const string TopWeekSeeAllId = "games.topWeek.seeAll";
    private const string TopWeekActionId = "games.topWeek.action";
    private const float TopWeekChevron = 13f;
    private const float TopWeekHoverMix = 0.25f;
    private const float TopWeekSkeletonWidth = 0.55f;
    private const float TopWeekSkeletonHeight = 10f;

    private readonly PodiumCard topWeekPodium = new();
    private string topWeekStatId = string.Empty;
    private ScoreKind topWeekKind;
    private int topWeekGame = -1;
    private int topWeekVersion = -1;
    private CachedText topWeekRankText;

    private float DrawTopThisWeek(float left, float top, float width, float scale)
    {
        SyncTopWeekStat();
        if (topWeekStatId.Length == 0 || leaderboard.Unavailable)
        {
            return top;
        }

        var key = new LeaderboardKey(topWeekStatId, LeaderboardScope.Global, LeaderboardSpan.Week);
        var signedIn = leaderboard.IsSignedIn;
        if (signedIn)
        {
            leaderboard.EnsureFresh(key);
        }

        var cardTop = top + (GamesHubArt.SectionHeight + HubMetrics.HeaderGap) * scale;
        var card = new Rect(new Vector2(left, cardTop),
            new Vector2(left + width, cardTop + PodiumCard.Height(scale, true)));
        var bottom = card.Max.Y + HubMetrics.SectionGap * scale;
        var drawList = ImGui.GetWindowDrawList();
        if (!Visible(drawList, top, card.Max.Y))
        {
            return bottom;
        }

        var daily = games[topWeekGame];
        if (GamesHubArt.Section(drawList, ui, left, top, width, Loc.T(L.GamesHub.TopThisWeek),
                Loc.T(L.GamesHub.SeeAll), TopWeekSeeAllId))
        {
            OpenLeaderboard(daily, topWeekStatId, DisplayName);
        }

        if (!signedIn)
        {
            var footer = PodiumCard.DrawPlaceholder(drawList, ui, card, true, false, scale);
            var settings = navigation.IsAvailable(SettingsAppId);
            if (DrawTopWeekRow(drawList, footer, Loc.T(L.Stage.SignInToRank), ui.TitleInk, string.Empty, settings,
                    scale))
            {
                navigation.Open(SettingsAppId);
            }

            return bottom;
        }

        var board = leaderboard.Board(key);
        var data = board.Data;
        if (data is null)
        {
            var footer = PodiumCard.DrawPlaceholder(drawList, ui, card, true, !board.Failed, scale);
            if (!board.Failed)
            {
                var bar = TopWeekSkeletonHeight * scale;
                var barTop = footer.Center.Y - bar * 0.5f;
                Skeleton.Bar(drawList, new Vector2(footer.Min.X, barTop),
                    new Vector2(footer.Min.X + footer.Width * TopWeekSkeletonWidth, barTop + bar), bar * 0.5f);
                return bottom;
            }

            if (DrawTopWeekRow(drawList, footer, Loc.T(L.Common.LoadFailed), ui.MutedInk, Loc.T(L.Common.Retry),
                    true, scale))
            {
                leaderboard.RefreshNow(key);
            }

            return bottom;
        }

        var entries = data.Entries ?? Array.Empty<GameLeaderboardEntryDto>();
        if (entries.Length == 0)
        {
            var footer = PodiumCard.DrawPlaceholder(drawList, ui, card, true, false, scale);
            if (DrawTopWeekRow(drawList, footer, Loc.T(L.GamesHub.BeFirst), ui.TitleInk, Loc.T(L.Games.Play), true,
                    scale))
            {
                OpenGame(daily);
            }

            return bottom;
        }

        topWeekPodium.Sync(data, topWeekKind, leaderboard.AccountId);
        var podiumFooter = topWeekPodium.Draw(drawList, ui, card, true, daily.Accent, images, lodestone, scale);
        DrawTopWeekFooter(drawList, podiumFooter, data, daily, scale);
        return bottom;
    }

    private void DrawTopWeekFooter(ImDrawListPtr drawList, Rect footer, GameLeaderboardDto data, IMiniGame daily,
        float scale)
    {
        if (ShowsConsentCompact)
        {
            var failure = ParticipationFailureText();
            if (DrawTopWeekRow(drawList, footer, failure.Length > 0 ? failure : Loc.T(L.Leaderboard.NotOnBoards),
                    failure.Length > 0 ? ui.Theme.Danger : ui.TitleInk, Loc.T(L.Leaderboard.JoinShort),
                    !leaderboard.SavingParticipation, scale))
            {
                leaderboard.SetParticipation(true);
            }

            return;
        }

        var rank = data.Me?.Rank ?? 0;
        var label = rank > 0
            ? topWeekRankText.IsCurrent(rank)
                ? topWeekRankText.Value
                : topWeekRankText.Store(rank, Loc.T(L.GamesHub.YouAreRank, GameNumber.Label(rank)))
            : Loc.T(L.Stage.NotRanked);
        if (DrawTopWeekRow(drawList, footer, label, rank > 0 ? ui.TitleInk : ui.MutedInk, string.Empty, true, scale))
        {
            OpenLeaderboard(daily, topWeekStatId, DisplayName);
        }
    }

    private bool DrawTopWeekRow(ImDrawListPtr drawList, Rect footer, string text, Vector4 ink, string buttonLabel,
        bool enabled, float scale)
    {
        var hasButton = buttonLabel.Length > 0;
        var trailing = hasButton ? Button.WidthFor(buttonLabel, ButtonSize.Small) : TopWeekChevron * scale;
        var textWidth = MathF.Max(1f, footer.Width - trailing - Metrics.Space.Md * scale);
        var lineHeight = Typography.LineHeight(TextStyles.Subheadline);
        var centerY = footer.Center.Y;
        Typography.Draw(drawList, new Vector2(footer.Min.X, centerY - lineHeight * 0.5f),
            Typography.FitText(text, textWidth, TextStyles.Subheadline), ink, TextStyles.Subheadline);
        if (hasButton)
        {
            var buttonHeight = Button.SmallHeight * scale;
            var button = new Rect(new Vector2(footer.Max.X - trailing, centerY - buttonHeight * 0.5f),
                new Vector2(footer.Max.X, centerY + buttonHeight * 0.5f));
            return Button.Draw(drawList, button, buttonLabel, ui.Ink, ButtonStyle.Tinted, enabled: enabled,
                id: TopWeekActionId);
        }

        var hovered = enabled && UiInteract.Hover(footer.Min, footer.Max);
        var chevronInk = hovered ? Palette.Mix(ui.MutedInk, ui.TitleInk, TopWeekHoverMix) : ui.MutedInk;
        PhoneIcon.Draw(drawList, new Vector2(footer.Max.X - trailing * 0.5f, centerY), PhoneIcons.ChevronRight,
            chevronInk, trailing);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return enabled && UiInteract.Click(footer.Min, footer.Max, hovered);
    }

    private void SyncTopWeekStat()
    {
        if (topWeekGame == featuredIndex && topWeekVersion == library.Version)
        {
            return;
        }

        topWeekGame = featuredIndex;
        topWeekVersion = library.Version;
        var spec = games[featuredIndex].Spec;
        var mode = TopWeekMode(spec, stats.LastMode(spec.Id));
        topWeekKind = spec.KindFor(mode);
        topWeekStatId = mode < 0
            ? string.Empty
            : ScoreStatIds.LeaderboardId(spec.StatIdFor(mode), spec.Id, topWeekKind);
        topWeekPodium.Reset();
    }

    internal static int TopWeekMode(in GameSpec spec, int lastMode)
    {
        var modes = Math.Max(1, spec.Modes.Length);
        var preferred = Math.Clamp(lastMode, 0, modes - 1);
        if (!spec.UnrankedFor(preferred))
        {
            return preferred;
        }

        for (var mode = 0; mode < modes; mode++)
        {
            if (!spec.UnrankedFor(mode))
            {
                return mode;
            }
        }

        return -1;
    }
}
