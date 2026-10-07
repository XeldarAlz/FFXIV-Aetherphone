using Aetherphone.Apps.Games.Hub;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Framework;

internal enum IntroAction : byte
{
    None,
    Play,
    Leaderboard,
    Levels,
}

internal sealed class StageIntro
{
    private const float IdleDim = 0.45f;
    private const float EntranceSpeed = 1.6f;
    private const float PlayHeight = 52f;
    private const float PillGap = 8f;
    private const float TagPadX = 12f;
    private const float StripHeight = 30f;
    private const float MinTitleFit = 0.6f;
    private const float LiftDistance = 14f;
    private const float IconSize = 72f;
    private const int StackSlots = 7;
    private const string ModeStripId = "stage.mode";
    private const string SeatStripId = "stage.seats";
    private static readonly TextStyle TagStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle LevelStyle = TextStyles.Title3;
    private static readonly TextStyle CaptionStyle = TextStyles.Footnote;

    private string[] modeLabels = Array.Empty<string>();
    private string[] seatLabels = Array.Empty<string>();
    private LanguageInfo? modeLanguage;
    private RankText rankText = new();
    private LabelSlot levelLabel;
    private LabelPairSlot starsLabel;
    private float entrance;
    private int slotCount = StackSlots;

    public void Begin(in GameSpec spec)
    {
        entrance = 0f;
        modeLanguage = null;
        if (modeLabels.Length != spec.Modes.Length)
        {
            modeLabels = spec.Modes.Length == 0 ? Array.Empty<string>() : new string[spec.Modes.Length];
        }

        var seats = spec.HotSeat ? spec.Seats : 0;
        if (seatLabels.Length == seats)
        {
            return;
        }

        seatLabels = seats == 0 ? Array.Empty<string>() : new string[seats];
        for (var seat = 0; seat < seats; seat++)
        {
            seatLabels[seat] = GameNumber.Label(seat + 1);
        }
    }

    public IntroAction Draw(ImDrawListPtr drawList, in GameContext context, Vector4 accent, float scale)
    {
        var session = context.Session;
        var spec = session.Spec;
        var theme = context.Theme;
        var full = context.Full;
        var ink = StageInks.StrongOn(context.Backdrop.Ink);
        var muted = StageInks.MutedOn(context.Backdrop.Ink);
        entrance = GameJuice.Advance(entrance, context.RawDeltaSeconds, EntranceSpeed);
        Material.Veil(drawList, full.Min, full.Max, IdleDim);
        rankText.Refresh(session.Rank);
        SyncModeLabels(spec);

        var columns = spec.Landscape && full.IsLandscape();
        var textColumn = IntroLayout.TextColumnOf(full, context.Safe, columns, scale);
        var title = Loc.T(spec.Title);
        var titleScale = Typography.FitScale(title, textColumn.Width, TextStyles.LargeTitle.Scale,
            TextStyles.LargeTitle.Scale * MinTitleFit, TextStyles.LargeTitle.Weight);
        var titleHeight = Typography.Measure(title, titleScale, TextStyles.LargeTitle.Weight).Y;
        var hookWidth = IntroLayout.HookWidth(textColumn, scale);
        var hook = spec.Hook.HasValue ? Loc.T(spec.Hook.Value) : string.Empty;
        var hookHeight = hook.Length > 0 ? Typography.MeasureWrappedBlock(hook, TextStyles.Subheadline, hookWidth).Y : 0f;
        var showModes = session.ShowsModes;
        var blocks = new IntroBlocks(session.Daily ? StagePill.Height * scale : 0f, titleHeight, hookHeight,
            StagePill.Height * scale, showModes ? StripHeight * scale : 0f,
            spec.HotSeat ? Typography.LineHeight(CaptionStyle) : 0f, spec.HotSeat ? StripHeight * scale : 0f,
            session.HasLevels ? Typography.LineHeight(LevelStyle) : 0f, PlayHeight * scale,
            Button.Height(ButtonSize.Small) * scale, IconSize * scale);
        var layout = IntroLayout.Compute(full, context.Safe, columns, blocks, scale);
        var showIcon = layout.Icon.Height > 0f;
        slotCount = StackSlots + (showIcon ? 1 : 0) + (spec.HotSeat ? 1 : 0) + (session.HasLevels ? 1 : 0);
        var slot = 0;
        var action = IntroAction.None;
        if (showIcon)
        {
            DrawIcon(drawList, spec.Id, layout.Icon, accent, Phase(slot++), scale);
        }

        if (session.Daily)
        {
            var phase = Phase(slot++);
            DrawTag(drawList, Lifted(layout.Daily.Center, phase, scale), Loc.T(L.Stage.TodaysBoard), accent, phase,
                scale);
        }

        var titlePhase = Phase(slot++);
        Typography.DrawCentered(drawList, Lifted(layout.Title.Center, titlePhase, scale), title,
            ink with { W = ink.W * titlePhase }, titleScale, TextStyles.LargeTitle.Weight);
        if (hook.Length > 0)
        {
            var hookPhase = Phase(slot++);
            Typography.DrawWrappedCentered(drawList, Lifted(layout.Hook.Center, hookPhase, scale), hook,
                muted with { W = muted.W * hookPhase }, TextStyles.Subheadline, hookWidth);
        }

        var pillPhase = Phase(slot++);
        DrawRecordPills(drawList, session, Lifted(layout.Pills.Center, pillPhase, scale), accent, pillPhase, scale);
        if (showModes)
        {
            var stripPhase = Phase(slot++);
            if (stripPhase > 0.5f)
            {
                var selected = StageStrip(ModeStripId, layout.Modes, modeLabels, session.Mode, theme);
                if (selected != session.Mode)
                {
                    session.SelectMode(selected);
                }
            }
        }

        if (spec.HotSeat)
        {
            var seatsPhase = Phase(slot++);
            Typography.DrawCentered(drawList, Lifted(layout.SeatsCaption.Center, seatsPhase, scale),
                Loc.T(L.Stage.Players), muted with { W = muted.W * seatsPhase }, CaptionStyle);
            if (seatsPhase > 0.5f)
            {
                var selected = StageStrip(SeatStripId, layout.Seats, seatLabels, session.Seats - 1, theme);
                if (selected != session.Seats - 1)
                {
                    session.SelectSeats(selected + 1);
                }
            }
        }

        if (session.HasLevels)
        {
            var levelPhase = Phase(slot++);
            Typography.DrawCentered(drawList, Lifted(layout.Level.Center, levelPhase, scale),
                levelLabel.Get(L.Stage.LevelNumber, session.Level), ink with { W = ink.W * levelPhase }, LevelStyle);
        }

        var playPhase = Phase(slot++);
        if (playPhase > 0f)
        {
            var pop = 0.85f + 0.15f * GameJuice.PopIn(playPhase);
            var size = layout.Play.Size * pop;
            if (GameHud.Button(Lifted(layout.Play.Center, playPhase, scale), size, Loc.T(L.Games.Play), accent, theme) &&
                playPhase > 0.6f)
            {
                action = IntroAction.Play;
            }
        }

        var linksPhase = Phase(slot);
        if (linksPhase > 0.6f)
        {
            var linksAction = DrawLinks(layout.Links.Center, session.HasLevels, session.RankedMode, muted, scale);
            if (linksAction != IntroAction.None)
            {
                action = linksAction;
            }
        }

        if (action == IntroAction.None && entrance >= 0.5f && GameInput.Pressed(ImGuiKey.Space, ImGuiKey.Enter))
        {
            action = IntroAction.Play;
        }

        return action;
    }

    private float Phase(int slot) => Easing.EaseOutCubic(GameJuice.Stagger(entrance, slot, slotCount));

    private static Vector2 Lifted(Vector2 center, float phase, float scale) =>
        new(center.X, center.Y + (1f - phase) * LiftDistance * scale);

    private static void DrawIcon(ImDrawListPtr drawList, string gameId, Rect rect, Vector4 accent, float phase,
        float scale)
    {
        if (phase <= 0f)
        {
            return;
        }

        var center = Lifted(rect.Center, phase, scale);
        var half = rect.Size * 0.5f;
        var firstVertex = drawList.VtxBuffer.Size;
        GameIconArt.Draw(drawList, gameId, accent, center - half, center + half, IconAppearance.Default, true);
        LayerCompositor.Fade(drawList, firstVertex, phase);
    }

    private static int StageStrip(string id, Rect row, string[] labels, int selected, PhoneTheme theme) =>
        SegmentStrip.Draw(id, row, labels, selected, StageInks.Track, theme.Accent, StageInks.Muted, StageInks.Strong);

    private static IntroAction DrawLinks(Vector2 center, bool levels, bool leaderboardShown, Vector4 muted,
        float scale)
    {
        var levelsLabel = Loc.T(L.Stage.Levels);
        var leaderboard = Loc.T(L.Stage.Leaderboard);
        if (!levels || !leaderboardShown)
        {
            if (levels)
            {
                return TextButton.Draw(center, levelsLabel, muted, scale) ? IntroAction.Levels : IntroAction.None;
            }

            return leaderboardShown && TextButton.Draw(center, leaderboard, muted, scale)
                ? IntroAction.Leaderboard
                : IntroAction.None;
        }

        var levelsWidth = TextButton.Width(levelsLabel, scale);
        var leaderboardWidth = TextButton.Width(leaderboard, scale);
        var gap = Metrics.Space.Sm * scale;
        var left = center.X - (levelsWidth + gap + leaderboardWidth) * 0.5f;
        if (TextButton.Draw(new Vector2(left + levelsWidth * 0.5f, center.Y), levelsLabel, muted, scale))
        {
            return IntroAction.Levels;
        }

        var leaderboardCenter = new Vector2(left + levelsWidth + gap + leaderboardWidth * 0.5f, center.Y);
        return TextButton.Draw(leaderboardCenter, leaderboard, muted, scale) ? IntroAction.Leaderboard : IntroAction.None;
    }

    private void SyncModeLabels(in GameSpec spec)
    {
        if (ReferenceEquals(modeLanguage, Loc.Current) || modeLabels.Length != spec.Modes.Length)
        {
            return;
        }

        modeLanguage = Loc.Current;
        for (var index = 0; index < modeLabels.Length; index++)
        {
            modeLabels[index] = Loc.T(spec.Modes[index]);
        }
    }

    private void DrawRecordPills(ImDrawListPtr drawList, GameSession session, Vector2 center, Vector4 accent,
        float phase, float scale)
    {
        if (phase <= 0f)
        {
            return;
        }

        var stars = session.HasLevels;
        var recordValue = !session.RankedMode
            ? string.Empty
            : stars
                ? starsLabel.Get(L.Stage.StarsOf, session.TotalStars, session.LevelCount * GameStatsStore.MaxStars)
                : BestLabel(session);
        var hasRecord = recordValue.Length > 0;
        var rankLine = session.HotSeat || !session.RankedMode ? Loc.T(L.Stage.NotRanked) : rankText.IntroLine;
        var recordWidth = hasRecord ? StagePill.Width(recordValue, true, scale) : 0f;
        var rankWidth = StagePill.Width(rankLine, false, scale);
        var gap = hasRecord ? PillGap * scale : 0f;
        var left = center.X - (recordWidth + gap + rankWidth) * 0.5f;
        if (hasRecord)
        {
            var record = StagePill.Around(new Vector2(left + recordWidth * 0.5f, center.Y), recordWidth, scale);
            StagePill.Draw(drawList, record, stars ? FontAwesomeIcon.Star : FontAwesomeIcon.Trophy,
                stars ? GamePalette.Star : accent, recordValue, StageInks.Strong, phase, scale);
            left += recordWidth + gap;
        }

        var rank = StagePill.Around(new Vector2(left + rankWidth * 0.5f, center.Y), rankWidth, scale);
        StagePill.Draw(drawList, rank, rankLine, StageInks.Muted, phase, scale);
    }

    private static string BestLabel(GameSession session) =>
        session.Best <= 0 ? string.Empty : StageHud.ValueLabel(session.Best, session.Kind);

    private static void DrawTag(ImDrawListPtr drawList, Vector2 center, string text, Vector4 accent, float phase,
        float scale)
    {
        if (phase <= 0f)
        {
            return;
        }

        var half = new Vector2(Typography.Measure(text, TagStyle).X * 0.5f + TagPadX * scale,
            StagePill.Height * scale * 0.5f);
        Squircle.Fill(drawList, center - half, center + half, half.Y, ImGui.GetColorU32(accent with { W = 0.26f * phase }));
        Squircle.Stroke(drawList, center - half, center + half, half.Y, ImGui.GetColorU32(accent with { W = 0.5f * phase }),
            1f * scale);
        Typography.DrawCentered(drawList, center, text, GamePalette.Lighten(accent, 0.45f) with { W = phase }, TagStyle);
    }
}
