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
    private const float PlayWidth = 220f;
    private const float PlayHeight = 52f;
    private const float PillGap = 8f;
    private const float TagPadX = 12f;
    private const float StripWidth = 280f;
    private const float StripHeight = 30f;
    private const float HookMaxWidth = 300f;
    private const float MinTitleFit = 0.6f;
    private const float LiftDistance = 14f;
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
        var safe = context.Safe;
        var ink = context.Backdrop.Ink == StageInk.Dark ? GamePalette.InkDark : theme.TextStrong;
        var muted = context.Backdrop.Ink == StageInk.Dark ? GamePalette.InkDark with { W = 0.62f } : theme.TextMuted;
        entrance = GameJuice.Advance(entrance, context.RawDeltaSeconds, EntranceSpeed);
        Material.Veil(drawList, full.Min, full.Max, IdleDim);
        rankText.Refresh(session.Rank);
        SyncModeLabels(spec);

        var title = Loc.T(spec.Title);
        var titleScale = Typography.FitScale(title, safe.Width, TextStyles.LargeTitle.Scale,
            TextStyles.LargeTitle.Scale * MinTitleFit, TextStyles.LargeTitle.Weight);
        var titleHeight = Typography.Measure(title, titleScale, TextStyles.LargeTitle.Weight).Y;
        var hookWidth = MathF.Min(safe.Width, HookMaxWidth * scale);
        var hook = spec.Hook.HasValue ? Loc.T(spec.Hook.Value) : string.Empty;
        var hookHeight = hook.Length > 0 ? Typography.MeasureWrappedBlock(hook, TextStyles.Subheadline, hookWidth).Y : 0f;
        var dailyHeight = session.Daily ? StagePill.Height * scale : 0f;
        var pillHeight = StagePill.Height * scale;
        var stripHeight = spec.HasModes ? StripHeight * scale : 0f;
        var seatsHeight = StripHeight * scale;
        var captionHeight = Typography.LineHeight(CaptionStyle);
        var levelHeight = session.HasLevels ? Typography.LineHeight(LevelStyle) : 0f;
        var playHeight = PlayHeight * scale;
        var leaderboardHeight = Button.Height(ButtonSize.Small) * scale;
        var gapSm = Metrics.Space.Sm * scale;
        var gapMd = Metrics.Space.Md * scale;
        var gapLg = Metrics.Space.Lg * scale;
        var gapXl = Metrics.Space.Xl * scale;
        var stackHeight = titleHeight + gapMd + pillHeight + gapXl + playHeight + gapMd + leaderboardHeight;
        if (session.Daily)
        {
            stackHeight += dailyHeight + gapSm;
        }

        if (hook.Length > 0)
        {
            stackHeight += gapSm + hookHeight;
        }

        if (spec.HasModes)
        {
            stackHeight += gapLg + stripHeight;
        }

        slotCount = StackSlots;
        if (spec.HotSeat)
        {
            stackHeight += gapLg + captionHeight + Metrics.Space.Xs * scale + seatsHeight;
            slotCount++;
        }

        if (session.HasLevels)
        {
            stackHeight += levelHeight + gapMd;
            slotCount++;
        }

        var top = MathF.Max(safe.Min.Y, full.Center.Y - stackHeight * 0.5f);
        var centerX = full.Center.X;
        var slot = 0;
        var action = IntroAction.None;
        if (session.Daily)
        {
            var phase = Phase(slot++);
            DrawTag(drawList, new Vector2(centerX, top + dailyHeight * 0.5f + Lift(phase, scale)),
                Loc.T(L.Stage.TodaysBoard), accent, phase, scale);
            top += dailyHeight + gapSm;
        }

        var titlePhase = Phase(slot++);
        Typography.DrawCentered(drawList, new Vector2(centerX, top + titleHeight * 0.5f + Lift(titlePhase, scale)),
            title, ink with { W = ink.W * titlePhase }, titleScale, TextStyles.LargeTitle.Weight);
        top += titleHeight;
        if (hook.Length > 0)
        {
            top += gapSm;
            var hookPhase = Phase(slot++);
            Typography.DrawWrappedCentered(drawList, new Vector2(centerX, top + hookHeight * 0.5f + Lift(hookPhase, scale)),
                hook, muted with { W = muted.W * hookPhase }, TextStyles.Subheadline, hookWidth);
            top += hookHeight;
        }

        top += gapMd;
        var pillPhase = Phase(slot++);
        DrawRecordPills(drawList, session, new Vector2(centerX, top + pillHeight * 0.5f + Lift(pillPhase, scale)), accent,
            theme, pillPhase, scale);
        top += pillHeight;
        if (spec.HasModes)
        {
            top += gapLg;
            var stripPhase = Phase(slot++);
            if (stripPhase > 0.5f)
            {
                var stripWidth = MathF.Min(safe.Width, StripWidth * scale);
                var row = new Rect(new Vector2(centerX - stripWidth * 0.5f, top), new Vector2(centerX + stripWidth * 0.5f, top + stripHeight));
                var selected = SegmentStrip.Draw(ModeStripId, row, modeLabels, session.Mode, theme);
                if (selected != session.Mode)
                {
                    session.SelectMode(selected);
                }
            }

            top += stripHeight;
        }

        if (spec.HotSeat)
        {
            top += gapLg;
            var seatsPhase = Phase(slot++);
            Typography.DrawCentered(drawList, new Vector2(centerX, top + captionHeight * 0.5f + Lift(seatsPhase, scale)),
                Loc.T(L.Stage.Players), muted with { W = muted.W * seatsPhase }, CaptionStyle);
            top += captionHeight + Metrics.Space.Xs * scale;
            if (seatsPhase > 0.5f)
            {
                var stripWidth = MathF.Min(safe.Width, StripWidth * scale);
                var row = new Rect(new Vector2(centerX - stripWidth * 0.5f, top),
                    new Vector2(centerX + stripWidth * 0.5f, top + seatsHeight));
                var selected = SegmentStrip.Draw(SeatStripId, row, seatLabels, session.Seats - 1, theme);
                if (selected != session.Seats - 1)
                {
                    session.SelectSeats(selected + 1);
                }
            }

            top += seatsHeight;
        }

        top += gapXl;
        if (session.HasLevels)
        {
            var levelPhase = Phase(slot++);
            Typography.DrawCentered(drawList,
                new Vector2(centerX, top + levelHeight * 0.5f + Lift(levelPhase, scale)),
                levelLabel.Get(L.Stage.LevelNumber, session.Level), ink with { W = ink.W * levelPhase }, LevelStyle);
            top += levelHeight + gapMd;
        }

        var playPhase = Phase(slot++);
        if (playPhase > 0f)
        {
            var pop = 0.85f + 0.15f * GameJuice.PopIn(playPhase);
            var size = new Vector2(PlayWidth * scale, playHeight) * pop;
            if (GameHud.Button(new Vector2(centerX, top + playHeight * 0.5f + Lift(playPhase, scale)), size,
                    Loc.T(L.Games.Play), accent, theme) && playPhase > 0.6f)
            {
                action = IntroAction.Play;
            }
        }

        top += playHeight + gapMd;
        var linksPhase = Phase(slot);
        if (linksPhase > 0.6f)
        {
            var linksAction = DrawLinks(new Vector2(centerX, top + leaderboardHeight * 0.5f), session.HasLevels, muted,
                scale);
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

    private static float Lift(float phase, float scale) => (1f - phase) * LiftDistance * scale;

    private static IntroAction DrawLinks(Vector2 center, bool levels, Vector4 muted, float scale)
    {
        var leaderboard = Loc.T(L.Stage.Leaderboard);
        if (!levels)
        {
            return TextButton.Draw(center, leaderboard, muted, scale) ? IntroAction.Leaderboard : IntroAction.None;
        }

        var levelsLabel = Loc.T(L.Stage.Levels);
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
        PhoneTheme theme, float phase, float scale)
    {
        if (phase <= 0f)
        {
            return;
        }

        var stars = session.HasLevels;
        var recordValue = stars
            ? starsLabel.Get(L.Stage.StarsOf, session.TotalStars, session.LevelCount * GameStatsStore.MaxStars)
            : BestLabel(session);
        var hasRecord = recordValue.Length > 0;
        var rankLine = session.HotSeat ? Loc.T(L.Stage.NotRanked) : rankText.IntroLine;
        var recordWidth = hasRecord ? StagePill.Width(recordValue, true, scale) : 0f;
        var rankWidth = StagePill.Width(rankLine, false, scale);
        var gap = hasRecord ? PillGap * scale : 0f;
        var left = center.X - (recordWidth + gap + rankWidth) * 0.5f;
        if (hasRecord)
        {
            var record = StagePill.Around(new Vector2(left + recordWidth * 0.5f, center.Y), recordWidth, scale);
            StagePill.Draw(drawList, record, stars ? FontAwesomeIcon.Star : FontAwesomeIcon.Trophy,
                stars ? GamePalette.Star : accent, recordValue, theme.TextStrong, phase, scale);
            left += recordWidth + gap;
        }

        var rank = StagePill.Around(new Vector2(left + rankWidth * 0.5f, center.Y), rankWidth, scale);
        StagePill.Draw(drawList, rank, rankLine, theme.TextMuted, phase, scale);
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
