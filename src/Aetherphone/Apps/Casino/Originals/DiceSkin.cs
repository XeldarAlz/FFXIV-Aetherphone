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

internal sealed class DiceSkin : IOriginalsSkin
{
    private const float TrackHeight = 14f;
    private const float ThumbRadius = 11f;
    private const float MarkerWidth = 64f;
    private const float MarkerHeight = 30f;
    private const float TickLength = 6f;
    private const float FieldGap = 6f;
    private const float SegmentShare = 0.4f;
    private const int TickCount = 5;
    private const int DragStep = 100;
    private const float IdleSweepRate = 0.35f;

    private static readonly int[] TickValues = { 0, 25, 50, 75, 100 };

    private readonly DiceRollPlayback playback = new();
    private readonly OriginalsField chanceField = new("##originalsDiceChance");
    private readonly OriginalsField multiplierField = new("##originalsDiceMultiplier");
    private readonly string[] sideLabels = new string[2];

    private int target = OriginalsRules.DiceDefaultTarget;
    private bool over = true;
    private bool dragging;
    private bool noticePending;
    private LocString notice;
    private Rect track;
    private float idleTime;

    public string GameId => CasinoGames.Dice;

    public LocString Title => L.Originals.GameDice;

    public CasinoSign Sign => CasinoSign.Dice;

    public LocString Action => L.Originals.RollFor;

    public LocString Hint => L.Originals.DiceHint;

    public bool Knob => true;

    public bool AutoAvailable => true;

    public bool Live => false;

    public bool Busy => playback.Rolling;

    public bool CanCashOut => false;

    public long CashOutValue => 0;

    public LocString LiveSecondary => default;

    public Vector2 Focus => new(MarkerX(playback.Marker), track.Min.Y);

    public Backdrop IdleBackdrop => Backdrop.Strip;

    private int Chance => OriginalsRules.DiceChance(target, over);

    public void Enter()
    {
        noticePending = false;
        chanceField.Cancel();
        multiplierField.Cancel();
    }

    public void Reset()
    {
        playback.Clear();
        noticePending = false;
        dragging = false;
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
        var roll = originals.TakeDice();
        if (roll is not null)
        {
            if (!roll.Granted)
            {
                Raise(OriginalsControls.ReasonNotice(roll.Reason));
            }
            else if (!playback.Begin(roll, instant))
            {
                Raise(L.Casino.ReasonGeneric);
            }
        }

        if (originals.TakeFailure(OriginalsGame.Dice))
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
        if (steps.Length < 3)
        {
            return 0;
        }

        var chance = Chance;
        steps[0] = new LadderStep(Loc.T(L.Originals.Multiplier),
            OriginalsText.Multiplier(OriginalsRules.DiceMultiplierTenThousandths(chance)), LadderState.Current);
        steps[1] = new LadderStep(Loc.T(L.Originals.WinChance), OriginalsText.Percent(chance), LadderState.Upcoming);
        steps[2] = new LadderStep(Loc.T(over ? L.Originals.RollOver : L.Originals.RollUnder),
            OriginalsText.Decimal(target), LadderState.Upcoming);
        focus = 1;
        return 3;
    }

    public void DrawWorld(ImDrawListPtr drawList, in OriginalsFrame frame, AppSkin ui)
    {
        var scale = UiScale.Current;
        var world = frame.World;
        var resultHeight = Typography.LineHeight(TextStyles.LargeTitle);
        var center = world.Center.Y + resultHeight * 0.35f;
        var inset = ThumbRadius * 2f * scale;
        track = new Rect(new Vector2(world.Min.X + inset, center - TrackHeight * scale * 0.5f),
            new Vector2(world.Max.X - inset, center + TrackHeight * scale * 0.5f));
        DrawResult(drawList, new Vector2(world.Center.X, world.Min.Y + (center - world.Min.Y) * 0.4f), ui);
        DrawTrack(drawList, ui, scale);
        DrawTicks(drawList, ui, scale);
        HandleDrag(frame.Interactive && !playback.Rolling && !frame.Originals.InFlight, scale);
        DrawThumb(drawList, scale);
        if (playback.HasRoll)
        {
            DrawMarker(drawList, scale);
        }
    }

    public void DrawKnob(ImDrawListPtr drawList, Rect rect, AppSkin ui, bool changeable)
    {
        var scale = UiScale.Current;
        var gap = FieldGap * scale;
        var segmentWidth = rect.Width * SegmentShare;
        var segment = new Rect(rect.Min, new Vector2(rect.Min.X + segmentWidth, rect.Max.Y));
        sideLabels[0] = Loc.T(L.Originals.RollOver);
        sideLabels[1] = Loc.T(L.Originals.RollUnder);
        var picked = SegmentStrip.Draw("casino.originals.dice.side", segment, sideLabels, over ? 0 : 1,
            Surfaces.Fill(ui.TitleInk, FillLevel.Tertiary), ui.Accent, ui.MutedInk, CasinoColors.InkTitle);
        if (changeable && picked != (over ? 0 : 1))
        {
            SetSide(picked == 0);
        }

        var fieldWidth = (rect.Width - segmentWidth - gap * 2f) * 0.5f;
        var chanceRect = new Rect(new Vector2(segment.Max.X + gap, rect.Min.Y),
            new Vector2(segment.Max.X + gap + fieldWidth, rect.Max.Y));
        var multiplierRect = new Rect(new Vector2(chanceRect.Max.X + gap, rect.Min.Y), rect.Max);
        var chance = Chance;
        if (chanceField.Draw(drawList, chanceRect, string.Empty, OriginalsText.Percent(chance), chance / 100m, ui,
                changeable, out var typedChance))
        {
            SetChance((int)Math.Round(typedChance * 100m));
        }

        var multiplier = OriginalsRules.DiceMultiplierTenThousandths(chance);
        if (multiplierField.Draw(drawList, multiplierRect, string.Empty, OriginalsText.Multiplier(multiplier),
                multiplier / 10_000m, ui, changeable, out var typedMultiplier))
        {
            SetChance(OriginalsRules.DiceChanceForMultiplier((long)Math.Round(typedMultiplier * 100m)));
        }
    }

    public bool Play(CasinoOriginalsStore originals, long stake, bool auto)
    {
        if (!OriginalsRules.IsDiceTarget(target, over))
        {
            Raise(L.Casino.ReasonStakeRange);
            return false;
        }

        playback.Snap();
        originals.RollDice(stake, target, over);
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
        var bar = TrackHeight * scale;
        var min = new Vector2(rect.Min.X + bar, rect.Center.Y - bar * 0.5f);
        var max = new Vector2(rect.Max.X - bar, rect.Center.Y + bar * 0.5f);
        var split = min.X + (max.X - min.X) * 0.505f;
        Squircle.Fill(drawList, min, new Vector2(split, max.Y), bar * 0.5f,
            ImGui.GetColorU32(CasinoColors.InkMuted with { W = 0.45f }));
        Squircle.Fill(drawList, new Vector2(split, min.Y), max, bar * 0.5f, ImGui.GetColorU32(CasinoColors.LightB));
        var sweep = 0.5f + 0.5f * MathF.Sin(idleTime * IdleSweepRate * MathF.PI * 2f);
        var x = min.X + (max.X - min.X) * sweep;
        DrawTag(drawList, new Vector2(x, min.Y - MarkerHeight * scale * 0.7f), CasinoColors.Money, scale);
    }

    private void SetSide(bool rollOver)
    {
        var chance = Chance;
        over = rollOver;
        target = OriginalsRules.DiceTargetFor(chance, over);
        CasinoSfx.Play(UiSound.ChipSlide);
    }

    private void SetChance(int chance)
    {
        target = OriginalsRules.DiceTargetFor(OriginalsRules.ClampDiceChance(chance), over);
    }

    private void DrawResult(ImDrawListPtr drawList, Vector2 center, AppSkin ui)
    {
        if (!playback.HasRoll)
        {
            Typography.DrawCentered(drawList, center, OriginalsText.Decimal(target), ui.MutedInk, TextStyles.LargeTitle);
            return;
        }

        if (playback.Rolling)
        {
            var whole = (int)(playback.Marker / 100f) * 100;
            Typography.DrawCentered(drawList, center, OriginalsText.Decimal(whole), CasinoColors.InkBody,
                TextStyles.LargeTitle);
            return;
        }

        Typography.DrawCentered(drawList, center, OriginalsText.Decimal(playback.Roll),
            playback.Won ? CasinoColors.Money : CasinoColors.Loss, TextStyles.LargeTitle);
    }

    private void DrawTrack(ImDrawListPtr drawList, AppSkin ui, float scale)
    {
        var radius = track.Height * 0.5f;
        var split = MarkerX(target);
        var lose = Palette.WithAlpha(CasinoColors.InkMuted, 0.4f);
        var win = Palette.Mix(CasinoColors.LightB, CasinoColors.Money, playback.HasRoll && !playback.Rolling
            && playback.Won ? 0.6f : 0f);
        Squircle.Fill(drawList, track.Min, track.Max, radius, ImGui.GetColorU32(lose));
        var winMin = over ? new Vector2(split, track.Min.Y) : track.Min;
        var winMax = over ? track.Max : new Vector2(split, track.Max.Y);
        if (winMax.X - winMin.X > 1f)
        {
            Squircle.Fill(drawList, winMin, winMax, MathF.Min(radius, (winMax.X - winMin.X) * 0.5f),
                ImGui.GetColorU32(win));
            drawList.AddRectFilled(new Vector2(winMin.X, track.Min.Y - 10f * scale),
                new Vector2(winMax.X, track.Max.Y + 10f * scale), ImGui.GetColorU32(win with { W = 0.06f }));
        }

        Squircle.Stroke(drawList, track.Min, track.Max, radius, ImGui.GetColorU32(ui.TitleInk with { W = 0.12f }),
            MathF.Max(1f, scale));
    }

    private void DrawTicks(ImDrawListPtr drawList, AppSkin ui, float scale)
    {
        var top = track.Max.Y + 4f * scale;
        for (var tick = 0; tick < TickCount; tick++)
        {
            var value = TickValues[tick];
            var x = MarkerX(value * 100f);
            drawList.AddLine(new Vector2(x, top), new Vector2(x, top + TickLength * scale),
                ImGui.GetColorU32(ui.MutedInk), MathF.Max(1f, scale));
            var label = GameNumber.Label(value);
            var size = Typography.Measure(label, TextStyles.Caption1);
            Typography.Draw(drawList, new Vector2(x - size.X * 0.5f, top + TickLength * scale + 2f * scale), label,
                ui.MutedInk, TextStyles.Caption1);
        }
    }

    private void HandleDrag(bool enabled, float scale)
    {
        if (!enabled)
        {
            dragging = false;
            return;
        }

        var reach = ThumbRadius * scale;
        var hitArea = new Rect(new Vector2(track.Min.X - reach, track.Min.Y - reach * 1.5f),
            new Vector2(track.Max.X + reach, track.Max.Y + reach * 1.5f));
        var hovered = PressSurface.Claim("casino.originals.dice.track", hitArea, out var activated);
        if (activated)
        {
            dragging = true;
        }

        if (dragging && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            dragging = false;
        }

        if (hovered || dragging)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);
        }

        if (!dragging)
        {
            return;
        }

        var at = Math.Clamp((ImGui.GetMousePos().X - track.Min.X) / MathF.Max(1f, track.Width), 0f, 1f);
        var raw = (int)MathF.Round(at * OriginalsRules.DiceScale / DragStep) * DragStep;
        var chance = OriginalsRules.ClampDiceChance(OriginalsRules.DiceChance(raw, over));
        var next = OriginalsRules.DiceTargetFor(chance, over);
        if (next == target)
        {
            return;
        }

        target = next;
        CasinoSfx.Play(UiSound.PegTick);
    }

    private void DrawThumb(ImDrawListPtr drawList, float scale)
    {
        var center = new Vector2(MarkerX(target), track.Center.Y);
        var radius = ThumbRadius * scale;
        drawList.AddCircleFilled(center, radius * 1.5f, ImGui.GetColorU32(CasinoColors.LightB with { W = 0.18f }), 24);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(CasinoColors.InkTitle), 24);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(CasinoColors.LightB), 24, 2f * scale);
    }

    private void DrawMarker(ImDrawListPtr drawList, float scale)
    {
        var x = MarkerX(playback.Marker);
        var ink = playback.Rolling ? CasinoColors.LightB : playback.Won ? CasinoColors.Money : CasinoColors.InkMuted;
        var center = new Vector2(x, track.Min.Y - MarkerHeight * scale * 0.85f);
        DrawTag(drawList, center, ink, scale);
        var text = playback.Rolling
            ? OriginalsText.Decimal((int)(playback.Marker / 100f) * 100)
            : OriginalsText.Decimal(playback.Roll);
        var fitted = Typography.FitText(text, MarkerWidth * scale * 0.9f, TextStyles.FootnoteEmphasized);
        Typography.DrawCentered(drawList, center, fitted, new Vector4(0.05f, 0.04f, 0.10f, 1f),
            TextStyles.FootnoteEmphasized);
    }

    private static void DrawTag(ImDrawListPtr drawList, Vector2 center, Vector4 ink, float scale)
    {
        var half = new Vector2(MarkerWidth * 0.5f, MarkerHeight * 0.5f) * scale;
        var min = center - half;
        var max = center + half;
        Squircle.Fill(drawList, min, max, half.Y * 0.6f, ImGui.GetColorU32(ink));
        var tip = new Vector2(center.X, max.Y + 6f * scale);
        drawList.AddTriangleFilled(new Vector2(center.X - 6f * scale, max.Y - 1f), new Vector2(center.X + 6f * scale,
            max.Y - 1f), tip, ImGui.GetColorU32(ink));
    }

    private float MarkerX(float hundredths) =>
        track.Min.X + track.Width * Math.Clamp(hundredths / OriginalsRules.DiceScale, 0f, 1f);

    private void Raise(LocString message)
    {
        notice = message;
        noticePending = true;
    }
}
