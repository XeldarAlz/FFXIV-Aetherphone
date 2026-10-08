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

internal sealed class KenoSkin : IOriginalsSkin
{
    public const int Columns = 8;
    public const int Rows = 5;

    private const float TileGap = 5f;
    private const float BoardPad = 8f;
    private const int HitSparkles = 8;

    private readonly KenoDrawPlayback playback = new();
    private readonly bool[] picks = new bool[OriginalsRules.KenoTiles];
    private readonly int[] chosen = new int[OriginalsRules.KenoMaxPicks];
    private readonly LabelSlot[] hitCaptions = new LabelSlot[OriginalsRules.KenoMaxPicks + 1];
    private readonly string[] riskLabels = new string[OriginalsRules.KenoRiskCount];

    private OriginalsPicker picker = OriginalsPicker.FromSeed(GameSeed.Fresh());
    private LabelSlot pickedLabel;
    private OriginalsLabel hitsLabel;
    private int pickCount;
    private int risk = OriginalsRules.KenoClassic;
    private bool noticePending;
    private LocString notice;
    private Rect grid;
    private float idleTime;

    public string GameId => CasinoGames.Keno;

    public LocString Title => L.Originals.GameKeno;

    public CasinoSign Sign => CasinoSign.Keno;

    public LocString Action => L.Originals.DrawFor;

    public LocString Hint => pickCount == 0 && !playback.HasDraw ? L.Originals.KenoPickFirst : default;

    public bool Knob => true;

    public bool AutoAvailable => true;

    public bool Live => false;

    public bool Busy => playback.Drawing;

    public bool CanCashOut => false;

    public long CashOutValue => 0;

    public LocString LiveSecondary => default;

    public Vector2 Focus => grid.Center;

    public Backdrop IdleBackdrop => Backdrop.Strip;

    public void Enter()
    {
        noticePending = false;
        picker = OriginalsPicker.FromSeed(GameSeed.Fresh());
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
        var round = originals.TakeKeno();
        if (round is not null)
        {
            if (!round.Granted)
            {
                Raise(OriginalsControls.ReasonNotice(round.Reason));
            }
            else if (!playback.Begin(round, instant))
            {
                Raise(L.Casino.ReasonGeneric);
            }
        }

        if (originals.TakeFailure(OriginalsGame.Keno))
        {
            Raise(L.Casino.ReasonUnreachable);
        }
    }

    public void Advance(float deltaSeconds)
    {
        playback.Advance(deltaSeconds);
    }

    public int FillLadder(Span<LadderStep> steps, out int focus)
    {
        var picked = playback.HasDraw ? playback.Picks : pickCount;
        var table = playback.HasDraw ? playback.Risk : risk;
        focus = 0;
        if (picked <= 0)
        {
            return 0;
        }

        var count = Math.Min(steps.Length, picked + 1);
        var hits = playback.HitsShown;
        for (var hit = 0; hit < count; hit++)
        {
            var pay = OriginalsRules.KenoPayHundredths(table, picked, hit);
            var state = !playback.HasDraw ? LadderState.Upcoming
                : hit != hits ? LadderState.Upcoming
                : pay > 100 ? LadderState.Hit
                : LadderState.Current;
            steps[hit] = new LadderStep(hitCaptions[hit].Get(L.Originals.HitsCaption, hit),
                OriginalsText.MultiplierHundredths(pay), state);
        }

        focus = playback.HasDraw ? Math.Min(hits, count - 1) : count - 1;
        return count;
    }

    public void DrawWorld(ImDrawListPtr drawList, in OriginalsFrame frame, AppSkin ui)
    {
        var scale = UiScale.Current;
        var world = frame.World;
        var footer = Button.SmallHeight * scale;
        var gap = Metrics.Space.Sm * scale;
        var boardArea = new Rect(world.Min, new Vector2(world.Max.X, world.Max.Y - footer - gap));
        var cell = MathF.Min((boardArea.Width - BoardPad * 2f * scale - TileGap * scale * (Columns - 1)) / Columns,
            (boardArea.Height - BoardPad * 2f * scale - TileGap * scale * (Rows - 1)) / Rows);
        cell = MathF.Max(cell, 1f);
        var boardWidth = cell * Columns + TileGap * scale * (Columns - 1) + BoardPad * 2f * scale;
        var boardHeight = cell * Rows + TileGap * scale * (Rows - 1) + BoardPad * 2f * scale;
        var boardRect = new Rect(new Vector2(boardArea.Center.X - boardWidth * 0.5f, boardArea.Min.Y),
            new Vector2(boardArea.Center.X + boardWidth * 0.5f, boardArea.Min.Y + boardHeight));
        OriginalsArt.Panel(drawList, boardRect, scale);
        grid = boardRect.Inset(BoardPad * scale);
        PlayReveals(frame, cell, scale);
        var editable = frame.Interactive && !playback.Drawing && !frame.Originals.InFlight;
        DrawTiles(drawList, ui, cell, editable, scale);
        DrawFooter(drawList, ui, new Rect(new Vector2(world.Min.X, boardRect.Max.Y + gap),
            new Vector2(world.Max.X, boardRect.Max.Y + gap + footer)), editable, scale);
    }

    public void DrawKnob(ImDrawListPtr drawList, Rect rect, AppSkin ui, bool changeable)
    {
        riskLabels[OriginalsRules.KenoClassic] = Loc.T(L.Originals.RiskClassic);
        riskLabels[OriginalsRules.KenoLow] = Loc.T(L.Originals.RiskLow);
        riskLabels[OriginalsRules.KenoMedium] = Loc.T(L.Originals.RiskMedium);
        riskLabels[OriginalsRules.KenoHigh] = Loc.T(L.Originals.RiskHigh);
        var picked = SegmentStrip.Draw("casino.originals.keno.risk", rect, riskLabels, risk,
            Surfaces.Fill(ui.TitleInk, FillLevel.Tertiary), ui.Accent, ui.MutedInk, CasinoColors.InkTitle);
        if (!changeable || picked == risk)
        {
            return;
        }

        risk = picked;
        playback.Clear();
        CasinoSfx.Play(UiSound.ChipSlide);
    }

    public bool Play(CasinoOriginalsStore originals, long stake, bool auto)
    {
        if (pickCount == 0)
        {
            Raise(L.Originals.KenoPickFirst);
            return false;
        }

        playback.Snap();
        var count = 0;
        for (var tile = 0; tile < picks.Length && count < chosen.Length; tile++)
        {
            if (picks[tile])
            {
                chosen[count] = tile;
                count++;
            }
        }

        originals.DrawKeno(stake, risk, chosen.AsSpan(0, count));
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
        var cell = MathF.Min((rect.Width - TileGap * scale * (Columns - 1)) / Columns,
            (rect.Height - TileGap * scale * (Rows - 1)) / Rows);
        var width = cell * Columns + TileGap * scale * (Columns - 1);
        var height = cell * Rows + TileGap * scale * (Rows - 1);
        var area = new Rect(rect.Center - new Vector2(width, height) * 0.5f, rect.Center + new Vector2(width, height) * 0.5f);
        var beat = (int)(idleTime * 3f);
        for (var tile = 0; tile < OriginalsRules.KenoTiles; tile++)
        {
            var tileRect = TileRect(area, tile, cell, scale);
            var lit = (tile * 13 + beat) % 9 == 0;
            Squircle.Fill(drawList, tileRect.Min, tileRect.Max, cell * 0.22f,
                ImGui.GetColorU32(lit ? CasinoColors.Money : OriginalsArt.TileTop));
        }
    }

    private void PlayReveals(in OriginalsFrame frame, float cell, float scale)
    {
        while (playback.TakeReveal(out var hit, out var hits))
        {
            if (!hit)
            {
                CasinoSfx.Play(UiSound.PegTick);
                continue;
            }

            CasinoSfx.Pitched(UiSound.TileSafe, hits);
            var tile = NewestHit();
            if (tile >= 0)
            {
                frame.Stage.Particles.Emit(CasinoLights.Sparkle(scale), TileRect(grid, tile, cell, scale).Center,
                    HitSparkles);
            }
        }
    }

    private int NewestHit()
    {
        for (var tile = 0; tile < OriginalsRules.KenoTiles; tile++)
        {
            if (playback.IsHit(tile) && playback.Pop(tile) < 1f)
            {
                return tile;
            }
        }

        return -1;
    }

    private void DrawTiles(ImDrawListPtr drawList, AppSkin ui, float cell, bool editable, float scale)
    {
        var rounding = cell * 0.22f;
        for (var tile = 0; tile < OriginalsRules.KenoTiles; tile++)
        {
            var rect = TileRect(grid, tile, cell, scale);
            var picked = playback.HasDraw ? playback.WasPicked(tile) : picks[tile];
            var shown = playback.IsShown(tile);
            var hit = playback.IsHit(tile);
            var pop = playback.Pop(tile);
            var hovered = editable && UiInteract.Hover(rect.Min, rect.Max);
            var grow = shown ? 1f + 0.18f * MathF.Sin(MathF.PI * pop) : 1f;
            var half = rect.Size * 0.5f * grow;
            var face = new Rect(rect.Center - half, rect.Center + half);
            var fill = hit ? CasinoColors.Money
                : picked ? Palette.Mix(OriginalsArt.TileTop, CasinoColors.LightA, 0.55f)
                : hovered ? Palette.Mix(OriginalsArt.TileTop, CasinoColors.LightB, 0.18f)
                : OriginalsArt.TileTop;
            Squircle.FillVerticalGradient(drawList, face.Min, face.Max, rounding, ImGui.GetColorU32(fill),
                ImGui.GetColorU32(Palette.Mix(fill, OriginalsArt.TileBottom, 0.35f)));
            if (shown && !hit)
            {
                Squircle.Stroke(drawList, face.Min, face.Max, rounding,
                    ImGui.GetColorU32(CasinoColors.LightB with { W = 0.75f }), 1.6f * scale);
            }

            var ink = hit ? new Vector4(0.12f, 0.08f, 0.02f, 1f) : picked ? CasinoColors.InkTitle : ui.BodyInk;
            Typography.DrawCentered(drawList, face.Center, GameNumber.Label(tile + 1), ink, TextStyles.FootnoteEmphasized);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(rect.Min, rect.Max, hovered))
            {
                Toggle(tile);
            }
        }
    }

    private void DrawFooter(ImDrawListPtr drawList, AppSkin ui, Rect row, bool editable, float scale)
    {
        var gap = Metrics.Space.Sm * scale;
        var autoLabel = Loc.T(L.Originals.AutoPick);
        var clearLabel = Loc.T(L.Originals.Clear);
        var autoWidth = Button.WidthFor(autoLabel, ButtonSize.Small);
        var clearWidth = Button.WidthFor(clearLabel, ButtonSize.Small);
        var autoRect = new Rect(row.Min, new Vector2(row.Min.X + autoWidth, row.Max.Y));
        var clearRect = new Rect(new Vector2(autoRect.Max.X + gap, row.Min.Y),
            new Vector2(autoRect.Max.X + gap + clearWidth, row.Max.Y));
        if (Button.Draw(drawList, autoRect, autoLabel, ui.Ink, ButtonStyle.Tinted, enabled: editable))
        {
            AutoPick();
        }

        if (Button.Draw(drawList, clearRect, clearLabel, ui.Ink, ButtonStyle.Gray, enabled: editable && pickCount > 0))
        {
            Array.Clear(picks);
            pickCount = 0;
            playback.Clear();
            CasinoSfx.Play(UiSound.ChipSlide);
        }

        var status = playback.HasDraw && !playback.Drawing
            ? hitsLabel.Get(L.Originals.KenoHits, GameNumber.Label(playback.HitsShown), GameNumber.Label(playback.Picks))
            : pickedLabel.Get(L.Originals.KenoPicked, pickCount);
        var available = MathF.Max(1f, row.Max.X - clearRect.Max.X - gap);
        var fitted = Typography.FitText(status, available, TextStyles.Footnote);
        var size = Typography.Measure(fitted, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(row.Max.X - size.X, row.Center.Y - size.Y * 0.5f), fitted,
            playback.HasDraw && playback.HitsShown > 0 ? CasinoColors.Money : ui.MutedInk, TextStyles.Footnote);
    }

    private void Toggle(int tile)
    {
        if (playback.HasDraw)
        {
            playback.Clear();
        }

        if (picks[tile])
        {
            picks[tile] = false;
            pickCount--;
            CasinoSfx.Play(UiSound.ChipSlide);
            return;
        }

        if (pickCount >= OriginalsRules.KenoMaxPicks)
        {
            return;
        }

        picks[tile] = true;
        pickCount++;
        CasinoSfx.Play(UiSound.Daub);
    }

    private void AutoPick()
    {
        var wanted = pickCount > 0 ? pickCount : OriginalsRules.KenoMaxPicks;
        Array.Clear(picks);
        playback.Clear();
        Span<int> drawn = stackalloc int[OriginalsRules.KenoMaxPicks];
        var count = picker.PickDistinct(drawn[..wanted], OriginalsRules.KenoTiles);
        for (var index = 0; index < count; index++)
        {
            picks[drawn[index]] = true;
        }

        pickCount = count;
        CasinoSfx.Play(UiSound.Daub);
    }

    private void Raise(LocString message)
    {
        notice = message;
        noticePending = true;
    }

    private static Rect TileRect(Rect area, int tile, float cell, float scale)
    {
        var column = tile % Columns;
        var row = tile / Columns;
        var gap = TileGap * scale;
        var min = new Vector2(area.Min.X + column * (cell + gap), area.Min.Y + row * (cell + gap));
        return new Rect(min, min + new Vector2(cell, cell));
    }
}
