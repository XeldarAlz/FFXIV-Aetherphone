using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games;

internal sealed partial class GamesApp
{
    private const string LeaderboardNavId = "games.leaderboard.nav";
    private const string LeaderboardScopeId = "games.leaderboard.scope";
    private const string LeaderboardSpanId = "games.leaderboard.span";
    private const string LeaderboardModeId = "games.leaderboard.mode";
    private const string LeaderboardRowPrefix = "games.leaderboard.row.";
    private const string LeaderboardMeRowId = "games.leaderboard.me";
    private const string LeaderboardSettingsAppId = "settings";
    private const string LeaderboardHandlePrefix = "@";
    private const float LeaderboardRowHeight = 60f;
    private const float LeaderboardAvatarRadius = 18f;
    private const float LeaderboardRankWidth = 34f;
    private const float LeaderboardTextGap = 12f;
    private const float LeaderboardValueReserve = 0.28f;
    private const float LeaderboardStripHeight = 30f;
    private const float LeaderboardStripGap = 8f;
    private const float LeaderboardBlockHeight = 300f;
    private const float LeaderboardSpinnerRadius = 9f;
    private const float LeaderboardHighlightAlpha = 0.12f;
    private const float LeaderboardMonogramScale = 0.95f;
    private const int LeaderboardAvatarSegments = 32;

    private static readonly LocString[] LeaderboardScopeNames = { L.Stage.Global, L.Stage.Friends };
    private static readonly LocString[] LeaderboardSpanNames = { L.Stage.AllTime, L.Stage.ThisWeek };

    private readonly PullToRefresh leaderboardRefresh = new();
    private readonly Action refreshLeaderboard;
    private readonly string[] leaderboardScopeLabels = new string[LeaderboardScopeNames.Length];
    private readonly string[] leaderboardSpanLabels = new string[LeaderboardSpanNames.Length];
    private IMiniGame? leaderboardGame;
    private string[] leaderboardModeStatIds = Array.Empty<string>();
    private LocString[] leaderboardModeNames = Array.Empty<LocString>();
    private string[] leaderboardModeLabels = Array.Empty<string>();
    private LeaderboardKey leaderboardKey;
    private LeaderboardScope leaderboardScope;
    private LeaderboardSpan leaderboardSpan;
    private int leaderboardMode;
    private string leaderboardBackTitle = string.Empty;
    private Vector4 leaderboardAccent;
    private LanguageInfo? leaderboardLanguage;
    private GameLeaderboardDto? labeledLeaderboard;
    private string[] leaderboardValues = Array.Empty<string>();
    private string[] leaderboardHandles = Array.Empty<string>();
    private string[] leaderboardRowIds = Array.Empty<string>();
    private string leaderboardMeValue = string.Empty;
    private bool leaderboardMeListed;

    private void OpenLeaderboard(IMiniGame game, string statId, string backTitle)
    {
        leaderboardGame = game;
        leaderboardAccent = game.Accent;
        leaderboardBackTitle = backTitle;
        BuildLeaderboardModes(game.Spec);
        leaderboardMode = Math.Max(0, Array.IndexOf(leaderboardModeStatIds, statId));
        leaderboardLanguage = null;
        labeledLeaderboard = null;
        leaderboard.EnsureMyRanksFresh();
        router.Push(GamesRoute.LeaderboardOf(game.Id, statId));
    }

    private void RefreshLeaderboardNow()
    {
        leaderboard.RefreshNow(leaderboardKey);
    }

    private string CurrentLeaderboardStatId() =>
        leaderboardModeStatIds.Length > 0 ? leaderboardModeStatIds[leaderboardMode] : string.Empty;

    private void BuildLeaderboardModes(in GameSpec spec)
    {
        if (spec.HasModes)
        {
            leaderboardModeStatIds = new string[spec.Modes.Length];
            leaderboardModeNames = spec.Modes;
            for (var index = 0; index < spec.Modes.Length; index++)
            {
                leaderboardModeStatIds[index] = spec.StatIdFor(index);
            }
        }
        else
        {
            var count = ScoreStatIds.CountFor(spec.Id);
            if (count <= 1)
            {
                leaderboardModeStatIds = new[] { spec.Id };
                leaderboardModeNames = Array.Empty<LocString>();
            }
            else
            {
                leaderboardModeStatIds = new string[count];
                leaderboardModeNames = new LocString[count];
                var slot = 0;
                var catalog = ScoreStatIds.Catalog;
                for (var index = 0; index < catalog.Length; index++)
                {
                    if (ScoreStatIds.BelongsTo(catalog[index].Id, spec.Id))
                    {
                        leaderboardModeStatIds[slot++] = catalog[index].Id;
                    }
                }

                var baseKind = ScoreStatIds.KindOf(spec.Id);
                var hasRuleset = HasRulesetSibling(spec.Id);
                for (var index = 0; index < count; index++)
                {
                    leaderboardModeNames[index] = LeaderboardModeName(leaderboardModeStatIds[index], baseKind, hasRuleset);
                }
            }
        }

        if (leaderboardModeLabels.Length != leaderboardModeNames.Length)
        {
            leaderboardModeLabels = leaderboardModeNames.Length == 0
                ? Array.Empty<string>()
                : new string[leaderboardModeNames.Length];
        }
    }

    private static bool HasRulesetSibling(string gameId)
    {
        var catalog = ScoreStatIds.Catalog;
        for (var index = 0; index < catalog.Length; index++)
        {
            if (!ScoreStatIds.BelongsTo(catalog[index].Id, gameId))
            {
                continue;
            }

            var suffix = ScoreStatIds.SuffixOf(catalog[index].Id);
            if (suffix.SequenceEqual("modern".AsSpan()) || suffix.SequenceEqual("blitz".AsSpan()))
            {
                return true;
            }
        }

        return false;
    }

    private static LocString LeaderboardModeName(string statId, ScoreKind baseKind, bool hasRuleset)
    {
        var suffix = ScoreStatIds.SuffixOf(statId);
        if (suffix.Length == 0)
        {
            return baseKind == ScoreKind.Time ? L.Games.Time : hasRuleset ? L.Games.Classic : L.Games.Score;
        }

        if (suffix.SequenceEqual("easy".AsSpan()))
        {
            return L.Games.Easy;
        }

        if (suffix.SequenceEqual("medium".AsSpan()))
        {
            return L.Games.Medium;
        }

        if (suffix.SequenceEqual("hard".AsSpan()))
        {
            return L.Games.Hard;
        }

        if (suffix.SequenceEqual("modern".AsSpan()))
        {
            return L.Games.Modern;
        }

        if (suffix.SequenceEqual("blitz".AsSpan()))
        {
            return L.Games.Blitz;
        }

        if (suffix.SequenceEqual("height".AsSpan()))
        {
            return L.Updraft.Height;
        }

        if (suffix.SequenceEqual("attempts".AsSpan()))
        {
            return L.Games.Attempts;
        }

        return L.Games.Score;
    }

    private static string LeaderboardValue(ScoreKind kind, int value) =>
        kind == ScoreKind.Time ? TimeText.MinutesSeconds(value) : GameNumber.Label(value);

    private void SyncLeaderboardLabels()
    {
        if (ReferenceEquals(leaderboardLanguage, Loc.Current))
        {
            return;
        }

        leaderboardLanguage = Loc.Current;
        for (var index = 0; index < leaderboardScopeLabels.Length; index++)
        {
            leaderboardScopeLabels[index] = Loc.T(LeaderboardScopeNames[index]);
        }

        for (var index = 0; index < leaderboardSpanLabels.Length; index++)
        {
            leaderboardSpanLabels[index] = Loc.T(LeaderboardSpanNames[index]);
        }

        for (var index = 0; index < leaderboardModeLabels.Length; index++)
        {
            leaderboardModeLabels[index] = Loc.T(leaderboardModeNames[index]);
        }

        labeledLeaderboard = null;
    }

    private void LabelLeaderboard(GameLeaderboardDto data, GameLeaderboardEntryDto[] entries, ScoreKind kind)
    {
        if (ReferenceEquals(labeledLeaderboard, data))
        {
            return;
        }

        labeledLeaderboard = data;
        if (leaderboardValues.Length < entries.Length)
        {
            leaderboardValues = new string[entries.Length];
            leaderboardHandles = new string[entries.Length];
            leaderboardRowIds = new string[entries.Length];
        }

        var me = leaderboard.AccountId;
        leaderboardMeListed = false;
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            leaderboardValues[index] = LeaderboardValue(kind, entry.Value);
            leaderboardHandles[index] = LeaderboardHandlePrefix + entry.Handle;
            leaderboardRowIds[index] = LeaderboardRowPrefix + entry.UserId;
            if (string.Equals(entry.UserId, me, StringComparison.Ordinal))
            {
                leaderboardMeListed = true;
            }
        }

        leaderboardMeValue = data.Me is { Rank: > 0 } mine ? LeaderboardValue(kind, mine.Value) : string.Empty;
    }

    private void DrawLeaderboard(in PhoneContext context)
    {
        var navBar = AppHeader.BeginLargeTitle(context);
        using (var surface = AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            SyncLeaderboardLabels();
            if (!leaderboard.IsSignedIn)
            {
                var block = new Rect(origin, new Vector2(origin.X + width, origin.Y + LeaderboardBlockHeight * scale));
                if (GamesHubArt.StateScreen(drawList, ui, block, FontAwesomeIcon.Trophy, Loc.T(L.Stage.SignInToRank),
                        Loc.T(L.Leaderboard.SignInHint),
                        navigation.IsAvailable(LeaderboardSettingsAppId) ? Loc.T(L.GamesHub.OpenSettings) : string.Empty,
                        "games.leaderboard.signin"))
                {
                    navigation.Open(LeaderboardSettingsAppId);
                }

                FinishPage(origin, width, block.Max.Y, scale);
            }
            else
            {
                leaderboardKey = new LeaderboardKey(CurrentLeaderboardStatId(), leaderboardScope, leaderboardSpan);
                leaderboard.EnsureFresh(leaderboardKey);
                var board = leaderboard.Board(leaderboardKey);
                leaderboardRefresh.Draw(navBar.Body, surface.Pull, surface.Dragging, board.Loading, ui.MutedInk,
                    refreshLeaderboard);
                var y = DrawLeaderboardStrips(origin.X, origin.Y, width, scale);
                y = DrawLeaderboardBody(drawList, board, origin.X, y, width, scale);
                FinishPage(origin, width, y, scale);
            }
        }

        var title = leaderboardGame?.Title ?? Loc.T(L.Stage.Leaderboard);
        AppHeader.EndLargeTitle(in navBar, context, LeaderboardNavId, title, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty, leaderboardBackTitle, back);
    }

    private float DrawLeaderboardStrips(float left, float top, float width, float scale)
    {
        var y = top;
        var stripHeight = LeaderboardStripHeight * scale;
        var gap = LeaderboardStripGap * scale;
        if (leaderboardModeLabels.Length > 1)
        {
            var modeRow = new Rect(new Vector2(left, y), new Vector2(left + width, y + stripHeight));
            var selectedMode = SegmentStrip.Draw(LeaderboardModeId, modeRow, leaderboardModeLabels, leaderboardMode,
                ui.Palette);
            if (selectedMode != leaderboardMode)
            {
                leaderboardMode = selectedMode;
                labeledLeaderboard = null;
            }

            y += stripHeight + gap;
        }

        var half = (width - gap) * 0.5f;
        var scopeRow = new Rect(new Vector2(left, y), new Vector2(left + half, y + stripHeight));
        var spanRow = new Rect(new Vector2(left + half + gap, y), new Vector2(left + width, y + stripHeight));
        var scope = (LeaderboardScope)SegmentStrip.Draw(LeaderboardScopeId, scopeRow, leaderboardScopeLabels,
            (int)leaderboardScope, ui.Palette);
        var span = (LeaderboardSpan)SegmentStrip.Draw(LeaderboardSpanId, spanRow, leaderboardSpanLabels,
            (int)leaderboardSpan, ui.Palette);
        if (scope != leaderboardScope || span != leaderboardSpan)
        {
            leaderboardScope = scope;
            leaderboardSpan = span;
            labeledLeaderboard = null;
        }

        return y + stripHeight + GamesHubArt.SectionGap * scale;
    }

    private float DrawLeaderboardBody(ImDrawListPtr drawList, LeaderboardBoard board, float left, float top,
        float width, float scale)
    {
        var data = board.Data;
        if (data is null)
        {
            return board.Failed
                ? DrawLeaderboardFailure(drawList, left, top, width, scale)
                : DrawLeaderboardLoading(left, top, width, scale);
        }

        var entries = data.Entries ?? Array.Empty<GameLeaderboardEntryDto>();
        var kind = ScoreStatIds.KindOf(CurrentLeaderboardStatId());
        LabelLeaderboard(data, entries, kind);
        if (entries.Length == 0)
        {
            var block = new Rect(new Vector2(left, top), new Vector2(left + width, top + LeaderboardBlockHeight * scale));
            var action = currentGame is null && leaderboardGame is not null ? Loc.T(L.Games.Play) : string.Empty;
            if (GamesHubArt.StateScreen(drawList, ui, block, FontAwesomeIcon.Trophy, Loc.T(L.Leaderboard.EmptyTitle),
                    Loc.T(L.Leaderboard.EmptyHint), action, "games.leaderboard.play"))
            {
                OpenGame(leaderboardGame!);
            }

            return block.Max.Y;
        }

        ImGui.SetCursorScreenPos(new Vector2(left, top));
        var card = GroupCard.Begin(ui, entries.Length, LeaderboardRowHeight);
        card.SeparatorInset = LeaderboardRankWidth + LeaderboardAvatarRadius * 2f + LeaderboardTextGap;
        var clipMin = drawList.GetClipRectMin();
        var clipMax = drawList.GetClipRectMax();
        var me = leaderboard.AccountId;
        for (var index = 0; index < entries.Length; index++)
        {
            var row = card.NextRow();
            if (row.Max.Y < clipMin.Y || row.Min.Y > clipMax.Y)
            {
                continue;
            }

            var entry = entries[index];
            DrawLeaderboardRow(drawList, row, leaderboardRowIds[index], entry.Rank,
                SocialIdentity.Name(entry.DisplayName, entry.Handle), leaderboardHandles[index], entry.AvatarUrl,
                entry.Badges, leaderboardValues[index], string.Equals(entry.UserId, me, StringComparison.Ordinal),
                scale);
        }

        card.End();
        var bottom = card.Bounds.Max.Y;
        var mine = data.Me;
        var user = leaderboard.CurrentUser;
        if (leaderboardMeListed || mine is null || mine.Rank <= 0 || user is null)
        {
            return bottom;
        }

        var pinnedTop = bottom + Metrics.Space.Md * scale;
        ImGui.SetCursorScreenPos(new Vector2(left, pinnedTop));
        var pinned = GroupCard.Begin(ui, 1, LeaderboardRowHeight);
        DrawLeaderboardRow(drawList, pinned.NextRow(), LeaderboardMeRowId, mine.Rank,
            SocialIdentity.Name(user.DisplayName, user.Handle), Loc.T(L.Leaderboard.You), user.AvatarUrl, user.Badges,
            leaderboardMeValue, true, scale);
        pinned.End();
        return pinned.Bounds.Max.Y;
    }

    private void DrawLeaderboardRow(ImDrawListPtr drawList, Rect row, string rowId, int rank, string name,
        string subtitle, string? avatarUrl, int badges, string value, bool highlighted, float scale)
    {
        var padding = Metrics.Space.Lg * scale;
        if (highlighted)
        {
            drawList.AddRectFilled(new Vector2(row.Min.X - padding, row.Min.Y), new Vector2(row.Max.X + padding, row.Max.Y),
                ImGui.GetColorU32(leaderboardAccent with { W = LeaderboardHighlightAlpha }));
        }

        var rankWidth = LeaderboardRankWidth * scale;
        var rankLabel = Typography.FitText(GameNumber.Label(rank), rankWidth, TextStyles.Headline);
        var rankSize = Typography.Measure(rankLabel, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(row.Min.X, row.Center.Y - rankSize.Y * 0.5f), rankLabel,
            highlighted ? leaderboardAccent : ui.MutedInk, TextStyles.Headline);
        var radius = LeaderboardAvatarRadius * scale;
        var avatarCenter = new Vector2(row.Min.X + rankWidth + radius, row.Center.Y);
        AvatarView.DrawRemote(drawList, avatarCenter, radius, ui.Theme, name, string.Empty, avatarUrl, images,
            lodestone, LeaderboardMonogramScale, LeaderboardAvatarSegments);
        var valueWidth = row.Width * LeaderboardValueReserve;
        var textLeft = avatarCenter.X + radius + LeaderboardTextGap * scale;
        var textWidth = MathF.Max(1f, row.Max.X - valueWidth - Metrics.Space.Sm * scale - textLeft);
        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        var subtitleHeight = Typography.LineHeight(TextStyles.Footnote);
        var textTop = row.Center.Y - (nameHeight + subtitleHeight) * 0.5f;
        UserName.DrawAuto(drawList, rowId, name, badges, textLeft, textTop, textWidth, TextStyles.Headline,
            ui.TitleInk, ui.Theme);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + nameHeight),
            Typography.FitText(subtitle, textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        var fittedValue = Typography.FitText(value, valueWidth, TextStyles.Headline);
        var valueSize = Typography.Measure(fittedValue, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(row.Max.X - valueSize.X, row.Center.Y - valueSize.Y * 0.5f), fittedValue,
            ui.TitleInk, TextStyles.Headline);
    }

    private float DrawLeaderboardLoading(float left, float top, float width, float scale)
    {
        var block = new Rect(new Vector2(left, top), new Vector2(left + width, top + LeaderboardBlockHeight * scale));
        LoadingPulse.Spinner(block.Center, LeaderboardSpinnerRadius * scale, ui.Accent);
        return block.Max.Y;
    }

    private float DrawLeaderboardFailure(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var bottom = GamesHubArt.Notice(drawList, ui, left, top, width, scale, Loc.T(L.Common.LoadFailed),
            FontAwesomeIcon.ExclamationTriangle, ui.MutedInk);
        var label = Loc.T(L.Common.Retry);
        var height = Button.RegularHeight * scale;
        var pillWidth = GamesHubArt.ButtonWidth(label, height);
        var rect = new Rect(new Vector2(left + (width - pillWidth) * 0.5f, bottom + Metrics.Space.Md * scale),
            new Vector2(left + (width + pillWidth) * 0.5f, bottom + Metrics.Space.Md * scale + height));
        if (Button.Draw(drawList, rect, label, ui.Ink, id: "games.leaderboard.retry"))
        {
            RefreshLeaderboardNow();
        }

        return rect.Max.Y;
    }
}
