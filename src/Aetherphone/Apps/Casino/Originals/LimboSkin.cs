using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Originals;

internal sealed class LimboSkin : IOriginalsSkin
{
    private const float ArcStart = MathF.PI * 0.8f;
    private const float ArcSweep = MathF.PI * 1.4f;
    private const float ArcWidth = 12f;
    private const float MaxDialRadius = 150f;
    private const int ArcSegments = 64;
    private const float FieldShare = 0.58f;
    private const float FieldGap = 6f;
    private const float MinScaleTop = 1000f;
    private const float ScaleHeadroom = 4f;
    private const int StopSparkles = 16;

    private readonly LimboClimbPlayback playback = new();
    private readonly OriginalsField targetField = new("##originalsLimboTarget");

    private OriginalsLabel chanceLabel;
    private int target = OriginalsRules.LimboDefaultTarget;
    private bool noticePending;
    private bool stopCued;
    private LocString notice;
    private Vector2 dialCenter;
    private float idleTime;

    public string GameId => CasinoGames.Limbo;

    public LocString Title => L.Originals.GameLimbo;

    public CasinoSign Sign => CasinoSign.Limbo;

    public LocString Action => L.Originals.PlayFor;

    public LocString Hint => L.Originals.LimboHint;

    public bool Knob => true;

    public bool AutoAvailable => true;

    public bool Live => false;

    public bool Busy => playback.Climbing;

    public bool CanCashOut => false;

    public long CashOutValue => 0;

    public LocString LiveSecondary => default;

    public Vector2 Focus => dialCenter;

    public Backdrop IdleBackdrop => Backdrop.Strip;

    public void Enter()
    {
        noticePending = false;
        targetField.Cancel();
    }

    public void Reset()
    {
        playback.Clear();
        noticePending = false;
    }

    public void Snap()
    {
        playback.Snap();
    }

    public void Resume(CasinoOriginalsOpenDto open)
    {
    }

    public void Consume(CasinoOriginalsStore originals, bool instant)
    {
        var round = originals.TakeLimbo();
        if (round is not null)
        {
            if (!round.Granted)
            {
                Raise(OriginalsControls.ReasonNotice(round.Reason));
            }
            else if (playback.Begin(round, instant))
            {
                stopCued = false;
            }
            else
            {
                Raise(L.Casino.ReasonGeneric);
            }
        }

        if (originals.TakeFailure(OriginalsGame.Limbo))
        {
            Raise(L.Casino.ReasonUnreachable);
        }
    }

    public void Advance(float deltaSeconds)
    {
        playback.Advance(deltaSeconds);
        if (playback.TakeTick())
        {
            CasinoSfx.Play(UiSound.ReelTick);
        }
    }

    public int FillLadder(Span<LadderStep> steps, out int focus)
    {
        focus = 0;
        if (steps.Length < 2)
        {
            return 0;
        }

        steps[0] = new LadderStep(Loc.T(L.Originals.Target), OriginalsText.MultiplierHundredths(target),
            LadderState.Current);
        steps[1] = new LadderStep(Loc.T(L.Originals.WinChance),
            OriginalsText.Percent(OriginalsRules.LimboChanceBasisPoints(target)), LadderState.Upcoming);
        return 2;
    }

    public void DrawWorld(ImDrawListPtr drawList, in OriginalsFrame frame, AppSkin ui)
    {
        var scale = UiScale.Current;
        var world = frame.World;
        var radius = MathF.Min(MathF.Min(world.Width * 0.5f, world.Height * 0.55f) - ArcWidth * scale,
            MaxDialRadius * scale);
        radius = MathF.Max(radius, 1f);
        dialCenter = new Vector2(world.Center.X, world.Center.Y + radius * 0.12f);
        var top = ScaleTop();
        DrawArc(drawList, dialCenter, radius, 1f, Palette.WithAlpha(ui.TitleInk, 0.10f), scale);
        var shown = playback.HasResult ? playback.Display : OriginalsRules.LimboMinResult;
        var settledWin = playback.HasResult && !playback.Climbing && playback.Won;
        var settledLoss = playback.HasResult && !playback.Climbing && !playback.Won;
        var fillInk = settledWin ? OriginalsArt.Gem : settledLoss ? CasinoColors.InkMuted : CasinoColors.LightB;
        DrawArc(drawList, dialCenter, radius, Fraction(shown, top), fillInk, scale);
        DrawTargetNotch(drawList, dialCenter, radius, Fraction(target, top), scale);
        var number = OriginalsText.MultiplierHundredths(shown);
        var numberInk = settledWin ? OriginalsArt.Gem : settledLoss ? CasinoColors.Loss : CasinoColors.InkTitle;
        Typography.DrawCentered(drawList, dialCenter, Typography.FitText(number, radius * 1.6f, TextStyles.LargeTitle),
            numberInk, TextStyles.LargeTitle);
        var caption = Loc.T(L.Originals.Target);
        var captionTop = dialCenter.Y + Typography.LineHeight(TextStyles.LargeTitle) * 0.6f;
        Typography.DrawCentered(drawList, new Vector2(dialCenter.X, captionTop + Typography.LineHeight(TextStyles.Footnote)
            * 0.5f), Typography.FitText(caption, radius, TextStyles.Footnote), CasinoColors.InkBody, TextStyles.Footnote);
        Typography.DrawCentered(drawList,
            new Vector2(dialCenter.X, captionTop + Typography.LineHeight(TextStyles.Footnote)
                + Typography.LineHeight(TextStyles.FootnoteEmphasized) * 0.5f),
            OriginalsText.MultiplierHundredths(target), CasinoColors.LightA, TextStyles.FootnoteEmphasized);
        CueStop(frame, scale);
    }

    public void DrawKnob(ImDrawListPtr drawList, Rect rect, AppSkin ui, bool changeable)
    {
        var scale = UiScale.Current;
        var fieldRect = new Rect(rect.Min, new Vector2(rect.Min.X + rect.Width * FieldShare, rect.Max.Y));
        if (targetField.Draw(drawList, fieldRect, Loc.T(L.Originals.Target),
                OriginalsText.MultiplierHundredths(target), target / 100m, ui, changeable, out var typed))
        {
            target = OriginalsRules.ClampLimboTarget((int)Math.Round(typed * 100m));
            CasinoSfx.Play(UiSound.ChipSlide);
        }

        var chanceRect = new Rect(new Vector2(fieldRect.Max.X + FieldGap * scale, rect.Min.Y), rect.Max);
        var chance = chanceLabel.Get(L.Originals.Chance,
            OriginalsText.Percent(OriginalsRules.LimboChanceBasisPoints(target)));
        Squircle.Fill(drawList, chanceRect.Min, chanceRect.Max, chanceRect.Height * 0.5f,
            ImGui.GetColorU32(Surfaces.Fill(ui.TitleInk, FillLevel.Quaternary)));
        Typography.DrawCentered(drawList, chanceRect.Center,
            Typography.FitText(chance, chanceRect.Width - chanceRect.Height * 0.5f, TextStyles.FootnoteEmphasized),
            CasinoColors.LightB, TextStyles.FootnoteEmphasized);
    }

    public bool Play(CasinoOriginalsStore originals, long stake, bool auto)
    {
        if (!OriginalsRules.IsLimboTarget(target))
        {
            Raise(L.Casino.ReasonStakeRange);
            return false;
        }

        playback.Snap();
        originals.PlayLimbo(stake, target);
        CasinoSfx.Play(UiSound.ChipSlide);
        return true;
    }

    public void CashOut(CasinoOriginalsStore originals)
    {
    }

    public void Secondary(CasinoOriginalsStore originals)
    {
    }

    public void Step(CasinoOriginalsStore originals, bool auto)
    {
    }

    public bool TakeSettled(out OriginalsOutcome outcome) => playback.TakeSettled(out outcome);

    public bool TakeNotice(out LocString message)
    {
        message = notice;
        if (!noticePending)
        {
            return false;
        }

        noticePending = false;
        return true;
    }

    public void DrawIdle(ImDrawListPtr drawList, Rect rect, float deltaSeconds)
    {
        var scale = UiScale.Current;
        idleTime += deltaSeconds;
        var radius = MathF.Min(rect.Width, rect.Height) * 0.4f;
        var center = rect.Center + new Vector2(0f, radius * 0.15f);
        var cycle = idleTime % 2.4f / 2.4f;
        DrawArc(drawList, center, radius, 1f, CasinoColors.InkMuted with { W = 0.25f }, scale);
        DrawArc(drawList, center, radius, MathF.Min(1f, cycle * 1.4f), CasinoColors.LightB, scale);
        DrawTargetNotch(drawList, center, radius, 0.55f, scale);
    }

    private void CueStop(in OriginalsFrame frame, float scale)
    {
        if (stopCued || !playback.HasResult || playback.Climbing)
        {
            return;
        }

        stopCued = true;
        if (playback.Won)
        {
            frame.Stage.Particles.Emit(CasinoLights.Sparkle(scale), dialCenter, StopSparkles);
        }
    }

    private float ScaleTop()
    {
        var top = MathF.Max(MinScaleTop, target * ScaleHeadroom);
        if (playback.HasResult)
        {
            top = MathF.Max(top, playback.Result * 1.2f);
        }

        return MathF.Min(top, OriginalsRules.LimboMaxTarget);
    }

    private static float Fraction(int hundredths, float top)
    {
        var span = MathF.Log(MathF.Max(top, OriginalsRules.LimboMinResult + 1f) / OriginalsRules.LimboMinResult);
        var value = MathF.Log(MathF.Max(hundredths, OriginalsRules.LimboMinResult) /
                              (float)OriginalsRules.LimboMinResult);
        return Math.Clamp(value / span, 0f, 1f);
    }

    private static void DrawArc(ImDrawListPtr drawList, Vector2 center, float radius, float fraction, Vector4 ink,
        float scale)
    {
        if (fraction <= 0f)
        {
            return;
        }

        var segments = Math.Max(2, (int)(ArcSegments * fraction));
        drawList.PathClear();
        drawList.PathArcTo(center, radius, ArcStart, ArcStart + ArcSweep * fraction, segments);
        drawList.PathStroke(ImGui.GetColorU32(ink), ImDrawFlags.None, ArcWidth * scale);
    }

    private static void DrawTargetNotch(ImDrawListPtr drawList, Vector2 center, float radius, float fraction,
        float scale)
    {
        var angle = ArcStart + ArcSweep * fraction;
        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var inner = center + direction * (radius - ArcWidth * scale);
        var outer = center + direction * (radius + ArcWidth * scale);
        drawList.AddLine(inner, outer, ImGui.GetColorU32(CasinoColors.LightA), 3f * scale);
        drawList.AddCircleFilled(outer, 4f * scale, ImGui.GetColorU32(CasinoColors.LightA), 12);
    }

    private void Raise(LocString message)
    {
        notice = message;
        noticePending = true;
    }
}
