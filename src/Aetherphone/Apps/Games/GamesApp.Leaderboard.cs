using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Hub;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
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
    private readonly record struct BoardRow(string RowId, int Rank, string Name, string Subtitle, string? AvatarUrl,
        int Badges, string Value, bool Mine);

    private enum SelfBarMode : byte
    {
        None,
        Rank,
        Join,
    }

    private const string LeaderboardNavId = "games.leaderboard.nav";
    private const string LeaderboardScopeId = "games.leaderboard.scope";
    private const string LeaderboardPeriodMenuId = "games.leaderboard.period";
    private const string LeaderboardRowPrefix = "games.leaderboard.row.";
    private const string LeaderboardMeRowId = "games.leaderboard.me";
    private const string LeaderboardJoinId = "games.leaderboard.join";
    private const string LeaderboardBarJoinId = "games.leaderboard.bar.join";
    private const string LeaderboardPlayId = "games.leaderboard.play";
    private const string LeaderboardSelfLayerId = "games.leaderboard.self";
    private const string LeaderboardSettingsAppId = "settings";
    private const string MetaSeparator = " · ";
    private const float LeaderboardRowHeight = 56f;
    private const float LeaderboardAvatarRadius = 16f;
    private const float LeaderboardRankWidth = 30f;
    private const float LeaderboardTextGap = Metrics.Space.Md;
    private const float LeaderboardValueReserve = 0.28f;
    private const float LeaderboardStripHeight = 30f;
    private const float LeaderboardIdentityIcon = 44f;
    private const float LeaderboardBlockHeight = 300f;
    private const float LeaderboardHighlightAlpha = 0.12f;
    private const float LeaderboardWashInset = 2f;
    private const float LeaderboardWashRadius = Metrics.Radius.Md;
    private const float LeaderboardMonogramScale = 0.95f;
    private const float SelfBarHeight = 60f;
    private const float SelfBarInset = 12f;
    private const float SelfBarReserve = 84f;
    private const float SelfBarFadeFloor = 0.01f;
    private const float SelfBarShowThreshold = 0.5f;
    private const int LeaderboardAvatarSegments = 32;
    private const int LeaderboardSkeletonRows = 5;

    private static readonly LocString[] LeaderboardScopeNames = { L.Stage.Global, L.Stage.Friends };
    private static readonly LocString[] LeaderboardSpanNames = { L.Stage.AllTime, L.Stage.ThisWeek };
    private static readonly LeaderboardSpan[] LeaderboardPeriodSpans = { LeaderboardSpan.Week, LeaderboardSpan.All };

    private readonly PullToRefresh leaderboardRefresh = new();
    private readonly Action refreshLeaderboard;
    private readonly string[] leaderboardScopeLabels = new string[LeaderboardScopeNames.Length];
    private readonly string[] leaderboardSpanLabels = new string[LeaderboardSpanNames.Length];
    private readonly DropdownMenu leaderboardPeriodMenu = new();
    private readonly DropdownMenu.Item[] leaderboardPeriodItems = new DropdownMenu.Item[LeaderboardPeriodSpans.Length];
    private readonly NavBarButton[] leaderboardButtons = new NavBarButton[1];
    private readonly ChipRail leaderboardModeRail = new();
    private readonly PodiumCard leaderboardPodium = new();
    private IMiniGame? leaderboardGame;
    private string[] leaderboardModeStatIds = Array.Empty<string>();
    private ScoreKind[] leaderboardModeKinds = Array.Empty<ScoreKind>();
    private LocString[] leaderboardModeNames = Array.Empty<LocString>();
    private string[] leaderboardModeLabels = Array.Empty<string>();
    private bool[] leaderboardModeActive = Array.Empty<bool>();
    private LeaderboardKey leaderboardKey;
    private LeaderboardScope leaderboardScope;
    private LeaderboardSpan leaderboardSpan = LeaderboardSpan.Week;
    private int leaderboardMode;
    private string leaderboardBackTitle = string.Empty;
    private Vector4 leaderboardAccent;
    private LanguageInfo? leaderboardLanguage;
    private GameLeaderboardDto? labeledLeaderboard;
    private string[] leaderboardValues = Array.Empty<string>();
    private string[] leaderboardHandles = Array.Empty<string>();
    private string[] leaderboardRowIds = Array.Empty<string>();
    private string leaderboardMeValue = string.Empty;
    private int leaderboardMeIndex = -1;
    private string leaderboardIdentityLine = string.Empty;
    private IMiniGame? leaderboardIdentityGame;
    private LeaderboardSpan leaderboardIdentitySpan;
    private LanguageInfo? leaderboardIdentityLanguage;
    private Spring leaderboardSelfAlpha;
    private SelfBarMode leaderboardSelfMode;
    private bool leaderboardSelfVisible;

    private void OpenLeaderboard(IMiniGame game, string statId, string backTitle)
    {
        leaderboardGame = game;
        leaderboardAccent = game.Accent;
        leaderboardBackTitle = backTitle;
        BuildLeaderboardModes(game.Spec);
        leaderboardMode = Math.Max(0, Array.IndexOf(leaderboardModeStatIds, statId));
        leaderboardSpan = LeaderboardSpan.Week;
        leaderboardLanguage = null;
        labeledLeaderboard = null;
        leaderboardPodium.Reset();
        leaderboardModeRail.Reset();
        leaderboardPeriodMenu.Close();
        leaderboardSelfAlpha.SnapTo(0f);
        leaderboard.EnsureMyRanksFresh();
        router.Push(GamesRoute.LeaderboardOf(game.Id, statId));
    }

    private void RefreshLeaderboardNow()
    {
        leaderboard.RefreshNow(leaderboardKey);
    }

    private string CurrentLeaderboardStatId() =>
        leaderboardModeStatIds.Length > 0 ? leaderboardModeStatIds[leaderboardMode] : string.Empty;

    private ScoreKind CurrentLeaderboardKind() =>
        leaderboardModeKinds.Length > 0 ? leaderboardModeKinds[leaderboardMode] : ScoreKind.Score;

    private void BuildLeaderboardModes(in GameSpec spec)
    {
        BuildLeaderboardModeIds(spec);
        leaderboardModeKinds = new ScoreKind[leaderboardModeStatIds.Length];
        for (var index = 0; index < leaderboardModeKinds.Length; index++)
        {
            leaderboardModeKinds[index] = LeaderboardKind(spec, leaderboardModeStatIds[index]);
        }
    }

    private static ScoreKind LeaderboardKind(in GameSpec spec, string statId)
    {
        var modes = Math.Max(1, spec.Modes.Length);
        for (var mode = 0; mode < modes; mode++)
        {
            var modeBoard = ScoreStatIds.LeaderboardId(spec.StatIdFor(mode), spec.Id, spec.KindFor(mode));
            if (string.Equals(modeBoard, statId, StringComparison.Ordinal))
            {
                return spec.KindFor(mode);
            }
        }

        return ScoreStatIds.KindOf(statId);
    }

    private void BuildLeaderboardModeIds(in GameSpec spec)
    {
        if (spec.HasModes)
        {
            BuildLeaderboardModesFromSpec(spec);
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
            leaderboardModeActive = leaderboardModeNames.Length == 0
                ? Array.Empty<bool>()
                : new bool[leaderboardModeNames.Length];
        }
    }

    private void BuildLeaderboardModesFromSpec(in GameSpec spec)
    {
        var ids = new string[spec.Modes.Length];
        var names = new LocString[spec.Modes.Length];
        var count = 0;
        for (var index = 0; index < spec.Modes.Length; index++)
        {
            var id = spec.UnrankedFor(index)
                ? string.Empty
                : ScoreStatIds.LeaderboardId(spec.StatIdFor(index), spec.Id, spec.KindFor(index));
            if (id.Length == 0 || Array.IndexOf(ids, id, 0, count) >= 0)
            {
                continue;
            }

            ids[count] = id;
            names[count] = spec.Modes[index];
            count++;
        }

        if (count <= 1)
        {
            leaderboardModeStatIds = new[] { count == 0 ? spec.Id : ids[0] };
            leaderboardModeNames = Array.Empty<LocString>();
            return;
        }

        if (count < ids.Length)
        {
            Array.Resize(ref ids, count);
            Array.Resize(ref names, count);
        }

        leaderboardModeStatIds = ids;
        leaderboardModeNames = names;
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

        leaderboardButtons[0] = new NavBarButton(PhoneIcons.Calendar, Loc.T(L.GamesHub.Period));
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
        leaderboardMeIndex = -1;
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            leaderboardValues[index] = PodiumCard.Value(kind, entry.Value);
            leaderboardHandles[index] = HandlePrefix + entry.Handle;
            leaderboardRowIds[index] = LeaderboardRowPrefix + entry.UserId;
            if (me.Length > 0 && string.Equals(entry.UserId, me, StringComparison.Ordinal))
            {
                leaderboardMeIndex = index;
            }
        }

        leaderboardMeValue = data.Me is { Rank: > 0 } mine ? PodiumCard.Value(kind, mine.Value) : string.Empty;
    }

    private string LeaderboardIdentityLine(IMiniGame game)
    {
        if (ReferenceEquals(leaderboardIdentityGame, game) && leaderboardIdentitySpan == leaderboardSpan
            && ReferenceEquals(leaderboardIdentityLanguage, Loc.Current))
        {
            return leaderboardIdentityLine;
        }

        leaderboardIdentityGame = game;
        leaderboardIdentitySpan = leaderboardSpan;
        leaderboardIdentityLanguage = Loc.Current;
        leaderboardIdentityLine = string.Concat(Loc.T(GameGenres.Label(game.Genre)), MetaSeparator,
            Loc.T(LeaderboardSpanNames[(int)leaderboardSpan]));
        return leaderboardIdentityLine;
    }

    private static bool Visible(ImDrawListPtr drawList, float top, float bottom) =>
        bottom >= drawList.GetClipRectMin().Y && top <= drawList.GetClipRectMax().Y;

    private void DrawLeaderboard(in PhoneContext context)
    {
        leaderboardPeriodMenu.Gate();
        var scale = UiScale.Current;
        var signedIn = leaderboard.IsSignedIn;
        SyncLeaderboardLabels();
        var bar = SelfBarRect(context.Content, scale);
        leaderboardSelfMode = SelfBarMode.None;
        leaderboardSelfVisible = false;
        var navBar = AppHeader.BeginLargeTitle(context);
        using (AppSurface.ReserveBottom(signedIn ? SelfBarReserve * scale : 0f))
        using (var surface = AppSurface.Begin(navBar.Body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            if (!signedIn)
            {
                FinishPage(origin, width, DrawLeaderboardSignedOut(origin.X, origin.Y, width, scale), scale);
            }
            else
            {
                leaderboardKey = new LeaderboardKey(CurrentLeaderboardStatId(), leaderboardScope, leaderboardSpan);
                leaderboard.EnsureFresh(leaderboardKey);
                var board = leaderboard.Board(leaderboardKey);
                leaderboardRefresh.Draw(navBar.Body, surface.Pull, surface.Dragging, board.Loading, ui.MutedInk,
                    refreshLeaderboard);
                var y = DrawLeaderboardIdentity(drawList, origin.X, origin.Y, width, scale) + Metrics.Space.Md * scale;
                y = DrawLeaderboardControls(origin.X, y, width, scale);
                if (ShowsConsentCompact)
                {
                    var cardTop = y;
                    y = DrawConsentCompact(origin.X, y, width, scale, LeaderboardJoinId);
                    leaderboardSelfMode = SelfBarMode.Join;
                    leaderboardSelfVisible = SelfInView(drawList, cardTop, y, bar.Min.Y);
                    y += Metrics.Space.Md * scale;
                }

                y = DrawLeaderboardBody(drawList, board, origin.X, y, width, bar.Min.Y, scale);
                FinishPage(origin, width, y, scale);
            }
        }

        var title = leaderboardGame?.Title ?? Loc.T(L.Stage.Leaderboard);
        var buttons = signedIn ? leaderboardButtons : ReadOnlySpan<NavBarButton>.Empty;
        var pressed = AppHeader.EndLargeTitle(in navBar, context, LeaderboardNavId, title, NavBarStyle.From(ui),
            buttons, leaderboardBackTitle, back);
        if (pressed == 0)
        {
            leaderboardPeriodMenu.Toggle(LeaderboardPeriodMenuId, AppHeader.LargeTitleButtonRect(navBar, 0, 1));
        }

        DrawLeaderboardSelfBar(bar, scale);
        DrawLeaderboardPeriodMenu(context.Content);
    }

    private void DrawLeaderboardPeriodMenu(Rect screen)
    {
        for (var index = 0; index < LeaderboardPeriodSpans.Length; index++)
        {
            var span = LeaderboardPeriodSpans[index];
            leaderboardPeriodItems[index] = new DropdownMenu.Item(leaderboardSpanLabels[(int)span],
                Selected: span == leaderboardSpan);
        }

        var picked = leaderboardPeriodMenu.Draw(screen, theme, leaderboardPeriodItems);
        if (picked < 0)
        {
            return;
        }

        leaderboardPeriodMenu.Close();
        var chosen = LeaderboardPeriodSpans[picked];
        if (chosen == leaderboardSpan)
        {
            return;
        }

        leaderboardSpan = chosen;
        labeledLeaderboard = null;
        leaderboardPodium.Reset();
    }

    private float DrawLeaderboardSignedOut(float left, float top, float width, float scale)
    {
        var body = new Rect(new Vector2(left, top), new Vector2(left + width, top + LeaderboardBlockHeight * scale));
        var action = navigation.IsAvailable(LeaderboardSettingsAppId) ? Loc.T(L.GamesHub.OpenSettings) : string.Empty;
        if (EmptyState.Draw(body, ui, FontAwesomeIcon.Trophy, Loc.T(L.Stage.SignInToRank),
                Loc.T(L.Leaderboard.SignInHint), action))
        {
            navigation.Open(LeaderboardSettingsAppId);
        }

        return body.Max.Y;
    }

    private float DrawLeaderboardIdentity(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var game = leaderboardGame;
        if (game is null)
        {
            return top;
        }

        var icon = LeaderboardIdentityIcon * scale;
        var iconRect = new Rect(new Vector2(left, top), new Vector2(left + icon, top + icon));
        GameIconArt.Draw(drawList, game.Id, game.Accent, iconRect.Min, iconRect.Max, null, true);
        var playLabel = Loc.T(L.Games.Play);
        var showPlay = currentGame is null;
        var buttonWidth = showPlay ? Button.WidthFor(playLabel, ButtonSize.Small) : 0f;
        var textLeft = iconRect.Max.X + Metrics.Space.Md * scale;
        var textWidth = MathF.Max(1f,
            left + width - textLeft - (showPlay ? buttonWidth + Metrics.Space.Md * scale : 0f));
        var lineHeight = Typography.LineHeight(TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(textLeft, iconRect.Center.Y - lineHeight * 0.5f),
            Typography.FitText(LeaderboardIdentityLine(game), textWidth, TextStyles.Footnote), ui.MutedInk,
            TextStyles.Footnote);
        if (!showPlay)
        {
            return iconRect.Max.Y;
        }

        var buttonHeight = Button.SmallHeight * scale;
        var button = new Rect(new Vector2(left + width - buttonWidth, iconRect.Center.Y - buttonHeight * 0.5f),
            new Vector2(left + width, iconRect.Center.Y + buttonHeight * 0.5f));
        if (Button.Draw(drawList, button, playLabel, ui.Ink.WithAccent(game.Accent), ButtonStyle.Tinted,
                id: LeaderboardPlayId))
        {
            OpenGame(game);
        }

        return iconRect.Max.Y;
    }

    private float DrawLeaderboardControls(float left, float top, float width, float scale)
    {
        var stripHeight = LeaderboardStripHeight * scale;
        var scopeRow = new Rect(new Vector2(left, top), new Vector2(left + width, top + stripHeight));
        var scope = (LeaderboardScope)SegmentStrip.Draw(LeaderboardScopeId, scopeRow, leaderboardScopeLabels,
            (int)leaderboardScope, ui.Palette);
        if (scope != leaderboardScope)
        {
            leaderboardScope = scope;
            labeledLeaderboard = null;
            leaderboardPodium.Reset();
        }

        var y = scopeRow.Max.Y + Metrics.Space.Md * scale;
        if (leaderboardModeLabels.Length <= 1)
        {
            return y;
        }

        for (var index = 0; index < leaderboardModeActive.Length; index++)
        {
            leaderboardModeActive[index] = index == leaderboardMode;
        }

        var modeRow = new Rect(new Vector2(left, y), new Vector2(left + width, y + ChipRail.RowHeight * scale));
        var tapped = leaderboardModeRail.Draw(modeRow, ui, leaderboardModeLabels, leaderboardModeActive);
        if (tapped >= 0 && tapped != leaderboardMode)
        {
            leaderboardMode = tapped;
            labeledLeaderboard = null;
            leaderboardPodium.Reset();
        }

        return modeRow.Max.Y + Metrics.Space.Md * scale;
    }

    private float DrawLeaderboardBody(ImDrawListPtr drawList, LeaderboardBoard board, float left, float top,
        float width, float barTop, float scale)
    {
        var data = board.Data;
        if (data is null)
        {
            return board.Failed
                ? DrawLeaderboardFailure(left, top, width, scale)
                : DrawLeaderboardLoading(drawList, left, top, width, scale);
        }

        var entries = data.Entries ?? Array.Empty<GameLeaderboardEntryDto>();
        var kind = CurrentLeaderboardKind();
        LabelLeaderboard(data, entries, kind);
        if (entries.Length == 0)
        {
            return DrawLeaderboardEmpty(left, top, width, scale);
        }

        var me = leaderboard.AccountId;
        var user = leaderboard.CurrentUser;
        if (leaderboardSelfMode == SelfBarMode.None && user is not null
            && (leaderboardMeIndex >= 0 || data.Me is { Rank: > 0 }))
        {
            leaderboardSelfMode = SelfBarMode.Rank;
        }

        leaderboardPodium.Sync(data, kind, me);
        var podium = new Rect(new Vector2(left, top),
            new Vector2(left + width, top + PodiumCard.Height(scale, false)));
        if (Visible(drawList, podium.Min.Y, podium.Max.Y))
        {
            leaderboardPodium.Draw(drawList, ui, podium, false, leaderboardAccent, images, lodestone, scale);
        }

        if (leaderboardMeIndex is >= 0 and < PodiumCard.Places)
        {
            leaderboardSelfVisible = SelfInView(drawList, podium.Min.Y, podium.Max.Y, barTop);
        }

        if (entries.Length <= PodiumCard.Places)
        {
            return podium.Max.Y;
        }

        ImGui.SetCursorScreenPos(new Vector2(left, podium.Max.Y + Metrics.Space.Md * scale));
        var card = GroupCard.Begin(ui, entries.Length - PodiumCard.Places, LeaderboardRowHeight);
        card.SeparatorInset = LeaderboardRankWidth + LeaderboardAvatarRadius * 2f + LeaderboardTextGap * 2f;
        for (var index = PodiumCard.Places; index < entries.Length; index++)
        {
            var row = card.NextRow();
            var mine = index == leaderboardMeIndex;
            if (mine)
            {
                leaderboardSelfVisible = SelfInView(drawList, row.Min.Y, row.Max.Y, barTop);
            }

            if (!Visible(drawList, row.Min.Y, row.Max.Y))
            {
                continue;
            }

            var entry = entries[index];
            DrawBoardRow(drawList, row, new BoardRow(leaderboardRowIds[index], entry.Rank,
                SocialIdentity.Name(entry.DisplayName, entry.Handle), leaderboardHandles[index], entry.AvatarUrl,
                entry.Badges, leaderboardValues[index], mine), mine, leaderboardAccent, scale);
        }

        card.End();
        return card.Bounds.Max.Y;
    }

    private static bool SelfInView(ImDrawListPtr drawList, float top, float bottom, float barTop)
    {
        var center = (top + bottom) * 0.5f;
        return center >= drawList.GetClipRectMin().Y && center <= MathF.Min(drawList.GetClipRectMax().Y, barTop);
    }

    private void DrawBoardRow(ImDrawListPtr drawList, Rect row, in BoardRow data, bool wash, Vector4 accent,
        float scale)
    {
        var accentInk = ui.Ink.WithAccent(accent).AccentInk;
        if (wash)
        {
            var padding = (Metrics.Space.Lg - LeaderboardWashInset * 2f) * scale;
            var inset = LeaderboardWashInset * scale;
            Squircle.Fill(drawList, new Vector2(row.Min.X - padding, row.Min.Y + inset),
                new Vector2(row.Max.X + padding, row.Max.Y - inset), LeaderboardWashRadius * scale,
                ImGui.GetColorU32(accent with { W = LeaderboardHighlightAlpha }));
        }

        var rankWidth = LeaderboardRankWidth * scale;
        var rankLabel = Typography.FitText(GameNumber.Label(data.Rank), rankWidth, TextStyles.Headline);
        var rankSize = Typography.Measure(rankLabel, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(row.Min.X, row.Center.Y - rankSize.Y * 0.5f), rankLabel,
            data.Mine ? accentInk : ui.MutedInk, TextStyles.Headline);
        var radius = LeaderboardAvatarRadius * scale;
        var avatarCenter = new Vector2(row.Min.X + rankWidth + LeaderboardTextGap * scale + radius, row.Center.Y);
        AvatarView.DrawRemote(drawList, avatarCenter, radius, ui.Theme, data.Name, string.Empty, data.AvatarUrl,
            images, lodestone, LeaderboardMonogramScale, LeaderboardAvatarSegments);
        var valueWidth = row.Width * LeaderboardValueReserve;
        var textLeft = avatarCenter.X + radius + LeaderboardTextGap * scale;
        var textWidth = MathF.Max(1f, row.Max.X - valueWidth - Metrics.Space.Sm * scale - textLeft);
        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        var subtitleHeight = Typography.LineHeight(TextStyles.Footnote);
        var textTop = row.Center.Y - (nameHeight + subtitleHeight) * 0.5f;
        UserName.DrawAuto(drawList, data.RowId, data.Name, data.Badges, textLeft, textTop, textWidth,
            TextStyles.Headline, ui.TitleInk, ui.Theme);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + nameHeight),
            Typography.FitText(data.Subtitle, textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        var fittedValue = Typography.FitText(data.Value, valueWidth, TextStyles.Headline);
        var valueSize = Typography.Measure(fittedValue, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(row.Max.X - valueSize.X, row.Center.Y - valueSize.Y * 0.5f), fittedValue,
            ui.TitleInk, TextStyles.Headline);
    }

    private Rect SelfBarRect(Rect area, float scale)
    {
        var inset = SelfBarInset * scale;
        var bottom = MathF.Min(area.Max.Y, screenRect.Max.Y - (Metrics.Size.HomeIndicatorInset + SelfBarInset) * scale);
        return new Rect(new Vector2(area.Min.X + inset, bottom - SelfBarHeight * scale),
            new Vector2(area.Max.X - inset, bottom));
    }

    private void DrawLeaderboardSelfBar(Rect bar, float scale)
    {
        var show = leaderboardSelfMode != SelfBarMode.None && !leaderboardSelfVisible;
        var alpha = Math.Clamp(leaderboardSelfAlpha.Step(show ? 1f : 0f, Motion.Appear, frameSeconds), 0f, 1f);
        if (alpha < SelfBarFadeFloor || leaderboardSelfMode == SelfBarMode.None)
        {
            return;
        }

        using var layer = ScreenLayer.Begin(LeaderboardSelfLayerId, bar, false);
        UiInteract.HoverOverlay(bar);
        var drawList = ImGui.GetWindowDrawList();
        var firstVertex = drawList.VtxBuffer.Size;
        var radius = HubMetrics.CardRadius * scale;
        Material.ThemedGlass(drawList, bar.Min, bar.Max, radius, scale, ui.BackdropColor, TabBar.GlassOpacity);
        var pad = Metrics.Space.Lg * scale;
        var content = new Rect(new Vector2(bar.Min.X + pad, bar.Min.Y), new Vector2(bar.Max.X - pad, bar.Max.Y));
        if (leaderboardSelfMode == SelfBarMode.Join)
        {
            DrawSelfBarJoin(drawList, content, alpha > SelfBarShowThreshold && show, scale);
        }
        else
        {
            DrawSelfBarRank(drawList, content, scale);
        }

        LayerCompositor.Fade(drawList, firstVertex, alpha);
    }

    private void DrawSelfBarRank(ImDrawListPtr drawList, Rect content, float scale)
    {
        var user = leaderboard.CurrentUser;
        var data = leaderboard.Board(leaderboardKey).Data;
        if (user is null || data is null)
        {
            return;
        }

        var entries = data.Entries ?? Array.Empty<GameLeaderboardEntryDto>();
        var listed = leaderboardMeIndex >= 0 && leaderboardMeIndex < entries.Length;
        var rank = listed ? entries[leaderboardMeIndex].Rank : data.Me?.Rank ?? 0;
        if (rank <= 0)
        {
            return;
        }

        var value = listed ? leaderboardValues[leaderboardMeIndex] : leaderboardMeValue;
        DrawBoardRow(drawList, content, new BoardRow(LeaderboardMeRowId, rank,
            SocialIdentity.Name(user.DisplayName, user.Handle), Loc.T(L.Leaderboard.You), user.AvatarUrl, user.Badges,
            value, true), false, leaderboardAccent, scale);
    }

    private void DrawSelfBarJoin(ImDrawListPtr drawList, Rect content, bool interactive, float scale)
    {
        var failure = ParticipationFailureText();
        var message = failure.Length > 0 ? failure : Loc.T(L.Leaderboard.NotOnBoards);
        var label = Loc.T(L.Leaderboard.JoinShort);
        var buttonWidth = Button.WidthFor(label, ButtonSize.Small);
        var buttonHeight = Button.SmallHeight * scale;
        var textWidth = MathF.Max(1f, content.Width - buttonWidth - Metrics.Space.Md * scale);
        var lineHeight = Typography.LineHeight(TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(content.Min.X, content.Center.Y - lineHeight * 0.5f),
            Typography.FitText(message, textWidth, TextStyles.Subheadline),
            failure.Length > 0 ? ui.Theme.Danger : ui.TitleInk, TextStyles.Subheadline);
        var button = new Rect(new Vector2(content.Max.X - buttonWidth, content.Center.Y - buttonHeight * 0.5f),
            new Vector2(content.Max.X, content.Center.Y + buttonHeight * 0.5f));
        if (Button.Draw(drawList, button, label, ui.Ink, ButtonStyle.Tinted,
                enabled: interactive && !leaderboard.SavingParticipation, overlay: true, id: LeaderboardBarJoinId))
        {
            leaderboard.SetParticipation(true);
        }
    }

    private float DrawLeaderboardLoading(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var podium = new Rect(new Vector2(left, top),
            new Vector2(left + width, top + PodiumCard.Height(scale, false)));
        PodiumCard.DrawPlaceholder(drawList, ui, podium, false, true, scale);
        var rowsTop = podium.Max.Y + Metrics.Space.Md * scale;
        var rows = new Rect(new Vector2(left, rowsTop),
            new Vector2(left + width, rowsTop + LeaderboardSkeletonRows * LeaderboardRowHeight * scale));
        Skeleton.Rows(drawList, rows, LeaderboardRowHeight, 0f, scale);
        return rows.Max.Y;
    }

    private float DrawLeaderboardFailure(float left, float top, float width, float scale)
    {
        var body = new Rect(new Vector2(left, top), new Vector2(left + width, top + LeaderboardBlockHeight * scale));
        if (EmptyState.Draw(body, ui, FontAwesomeIcon.ExclamationTriangle, Loc.T(L.Common.LoadFailed),
                Loc.T(L.Common.LoadFailedHint), Loc.T(L.Common.Retry)))
        {
            RefreshLeaderboardNow();
        }

        return body.Max.Y;
    }

    private float DrawLeaderboardEmpty(float left, float top, float width, float scale)
    {
        var body = new Rect(new Vector2(left, top), new Vector2(left + width, top + LeaderboardBlockHeight * scale));
        var game = leaderboardGame;
        var action = currentGame is null && game is not null ? Loc.T(L.Games.Play) : string.Empty;
        if (EmptyState.Draw(body, ui, FontAwesomeIcon.Trophy, Loc.T(L.Leaderboard.EmptyTitle),
                Loc.T(L.Leaderboard.EmptyHint), action) && game is not null)
        {
            OpenGame(game);
        }

        return body.Max.Y;
    }
}
