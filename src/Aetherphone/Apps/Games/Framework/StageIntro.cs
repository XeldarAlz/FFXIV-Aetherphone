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
}

internal sealed class StageIntro
{
    private const float IdleDim = 0.45f;
    private const float EntranceSpeed = 1.6f;
    private const float PlayWidth = 220f;
    private const float PlayHeight = 52f;
    private const float PillHeight = 28f;
    private const float PillPadX = 12f;
    private const float PillGap = 8f;
    private const float IconSize = 11f;
    private const float IconGap = 5f;
    private const float StripWidth = 280f;
    private const float StripHeight = 30f;
    private const float HookMaxWidth = 300f;
    private const float MinTitleFit = 0.6f;
    private const float LiftDistance = 14f;
    private const int StackSlots = 7;
    private const string ModeStripId = "stage.mode";
    private static readonly TextStyle PillStyle = TextStyles.FootnoteEmphasized;

    private string[] modeLabels = Array.Empty<string>();
    private LanguageInfo? modeLanguage;
    private RankText rankText = new();
    private float entrance;

    public void Begin(in GameSpec spec)
    {
        entrance = 0f;
        modeLanguage = null;
        if (modeLabels.Length != spec.Modes.Length)
        {
            modeLabels = spec.Modes.Length == 0 ? Array.Empty<string>() : new string[spec.Modes.Length];
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
        var dailyHeight = session.Daily ? PillHeight * scale : 0f;
        var pillHeight = PillHeight * scale;
        var stripHeight = spec.HasModes ? StripHeight * scale : 0f;
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

        top += gapXl;
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
        var leaderboardPhase = Phase(slot);
        if (leaderboardPhase > 0.6f &&
            TextButton.Draw(new Vector2(centerX, top + leaderboardHeight * 0.5f), Loc.T(L.Stage.Leaderboard), muted, scale))
        {
            action = IntroAction.Leaderboard;
        }

        if (action == IntroAction.None && entrance >= 0.5f && GameInput.Pressed(ImGuiKey.Space, ImGuiKey.Enter))
        {
            action = IntroAction.Play;
        }

        return action;
    }

    private float Phase(int slot) => Easing.EaseOutCubic(GameJuice.Stagger(entrance, slot, StackSlots));

    private static float Lift(float phase, float scale) => (1f - phase) * LiftDistance * scale;

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

        var bestValue = BestLabel(session);
        var hasBest = bestValue.Length > 0;
        var rankLine = rankText.IntroLine;
        var iconSpan = (IconSize + IconGap) * scale;
        var pad = PillPadX * scale;
        var bestWidth = hasBest ? pad * 2f + iconSpan + Typography.Measure(bestValue, PillStyle).X : 0f;
        var rankWidth = pad * 2f + Typography.Measure(rankLine, PillStyle).X;
        var gap = hasBest ? PillGap * scale : 0f;
        var total = bestWidth + gap + rankWidth;
        var height = PillHeight * scale;
        var left = center.X - total * 0.5f;
        if (hasBest)
        {
            var rect = new Rect(new Vector2(left, center.Y - height * 0.5f), new Vector2(left + bestWidth, center.Y + height * 0.5f));
            Material.Frosted(drawList, rect.Min, rect.Max, height * 0.5f, scale, 0.9f * phase);
            ProgressRing.CenterIcon(drawList, new Vector2(rect.Min.X + pad + IconSize * scale * 0.5f, center.Y),
                FontAwesomeIcon.Trophy, accent with { W = phase }, IconSize * scale);
            Typography.Draw(drawList,
                new Vector2(rect.Min.X + pad + iconSpan, center.Y - Typography.LineHeight(PillStyle) * 0.5f), bestValue,
                theme.TextStrong with { W = phase }, PillStyle);
            left += bestWidth + gap;
        }

        var rankRect = new Rect(new Vector2(left, center.Y - height * 0.5f), new Vector2(left + rankWidth, center.Y + height * 0.5f));
        Material.Frosted(drawList, rankRect.Min, rankRect.Max, height * 0.5f, scale, 0.9f * phase);
        Typography.Draw(drawList, new Vector2(rankRect.Min.X + pad, center.Y - Typography.LineHeight(PillStyle) * 0.5f),
            rankLine, theme.TextMuted with { W = phase }, PillStyle);
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

        var half = new Vector2(Typography.Measure(text, PillStyle).X * 0.5f + PillPadX * scale, PillHeight * scale * 0.5f);
        Squircle.Fill(drawList, center - half, center + half, half.Y, ImGui.GetColorU32(accent with { W = 0.26f * phase }));
        Squircle.Stroke(drawList, center - half, center + half, half.Y, ImGui.GetColorU32(accent with { W = 0.5f * phase }),
            1f * scale);
        Typography.DrawCentered(drawList, center, text, GamePalette.Lighten(accent, 0.45f) with { W = phase }, PillStyle);
    }
}
