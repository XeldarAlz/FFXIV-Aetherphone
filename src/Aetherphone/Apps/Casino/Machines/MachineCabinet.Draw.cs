using Aetherphone.Apps.Casino.Cabinets;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Machines;

internal sealed partial class MachineCabinet
{
    private const float GlassRadius = 14f;
    private const float MeterBarHeight = 5f;
    private const float CardWidth = 92f;
    private const float CardHeight = 128f;
    private const string SkipId = "casino.machines.skip";
    private const string GambleId = "casino.machines.gamble";

    private static readonly Vector4 GlassTop = new(0.10f, 0.06f, 0.16f, 0.92f);
    private static readonly Vector4 GlassBottom = new(0.03f, 0.02f, 0.06f, 0.92f);
    private static readonly Vector4 CardRed = new(0.92f, 0.20f, 0.28f, 1f);
    private static readonly Vector4 CardBlack = new(0.10f, 0.10f, 0.14f, 1f);
    private static readonly Vector4 CardPaper = new(0.97f, 0.95f, 0.90f, 1f);
    private static readonly Vector4 Veil = new(0.01f, 0.0f, 0.03f, 0.78f);
    private static readonly CasinoSlotsCoinDto NoCoin = new();

    private AmountSlot betLabel;
    private AmountSlot anteLabel;
    private AmountSlot buyLabel;
    private AmountSlot meterHit;
    private AmountSlot gambleStake;
    private LabelPairSlot featureCounter;
    private LabelSlot respinCounter;
    private LabelSlot introSpins;
    private LabelSlot retrigger;
    private LabelSlot featureTotalLabel;

    private void DrawGlass(ImDrawListPtr drawList, AppSkin ui, in CasinoStageFrame frame, float scale)
    {
        var radius = GlassRadius * scale;
        Squircle.FillVerticalGradient(drawList, glass.Min, glass.Max, radius, ImGui.GetColorU32(GlassTop),
            ImGui.GetColorU32(GlassBottom));
        Squircle.Stroke(drawList, glass.Min, glass.Max, radius,
            ImGui.GetColorU32(MachineArt.NeonOf(machineId) with { W = 0.4f }), 1.5f * scale);
        var half = glass.Height * 0.5f;
        var topRow = new Rect(glass.Min, new Vector2(glass.Max.X, glass.Min.Y + half));
        var bottomRow = new Rect(new Vector2(glass.Min.X, glass.Min.Y + half), glass.Max);
        var heroExtent = half * 0.42f;
        MachineArt.Hero(drawList, new Vector2(topRow.Min.X + half * 0.62f, topRow.Center.Y), heroExtent, machineId,
            frame.Phase);
        var jackpotWidth = topRow.Width * 0.3f;
        var signWidth = topRow.Width - jackpotWidth - half * 1.3f;
        var signCenter = new Vector2(topRow.Min.X + half * 1.24f + signWidth * 0.5f, topRow.Center.Y);
        var sign = MachineArt.SignOf(machineId);
        var signHeight = CasinoSigns.HeightToFit(sign, signWidth * 0.95f, half * 0.5f);
        var flicker = 0.85f + 0.15f * Pulse.Wave(Pulse.Breath);
        CasinoSigns.Draw(drawList, sign, signCenter, signHeight, MachineArt.NeonOf(machineId), flicker);
        DrawFloorJackpot(drawList, new Rect(new Vector2(topRow.Max.X - jackpotWidth, topRow.Min.Y), topRow.Max),
            scale);
        var row = bottomRow.Inset(6f * scale);
        switch (machineId)
        {
            case SlotsRules.CascadeId:
                DrawCascadeRow(drawList, row, scale);
                break;
            case SlotsRules.MoogleId:
                DrawMeterRow(drawList, row, scale);
                break;
            default:
                DrawBirdRow(drawList, row, frame.Phase, scale);
                break;
        }
    }

    private void DrawFloorJackpot(ImDrawListPtr drawList, Rect area, float scale)
    {
        var inset = 8f * scale;
        var width = area.Width - inset;
        var eyebrow = Typography.FitText(Loc.T(L.Machines.FloorJackpot), width, TextStyles.Footnote);
        var eyebrowHeight = Typography.LineHeight(TextStyles.Footnote);
        var amount = NumberText.Compact(store.Jackpot);
        var amountStyle = TextStyles.Title3;
        var amountSize = CurrencyGlyph.MeasureAmount(amount, amountStyle);
        if (amountSize.X > width || eyebrowHeight + amountSize.Y > area.Height)
        {
            amountStyle = TextStyles.Headline;
            amountSize = CurrencyGlyph.MeasureAmount(amount, amountStyle);
        }

        var top = area.Center.Y - (eyebrowHeight + amountSize.Y) * 0.5f;
        var eyebrowSize = Typography.Measure(eyebrow, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(area.Max.X - inset - eyebrowSize.X, top), eyebrow,
            CasinoColors.MoneyHighlight, TextStyles.Footnote);
        CurrencyGlyph.DrawAmount(drawList, new Vector2(area.Max.X - inset - amountSize.X, top + eyebrowHeight), amount,
            CurrencyKind.Chips, CasinoColors.Money, amountStyle);
    }

    private void DrawBirdRow(ImDrawListPtr drawList, Rect row, float phase, float scale)
    {
        var expander = playback.HasRound && playback.InFeature ? playback.Expander : -1;
        if (expander < 0 || playback.Beat == MachineBeat.Intro)
        {
            var text = Typography.FitText(Loc.T(L.Machines.LinesInfo), row.Width, TextStyles.Headline);
            Typography.DrawCentered(drawList, row.Center, text, StageInks.Strong, TextStyles.Headline);
            return;
        }

        var label = Typography.FitText(Loc.T(L.Machines.Expanding), row.Width * 0.6f, TextStyles.Headline);
        var labelSize = Typography.Measure(label, TextStyles.Headline);
        var symbolSize = row.Height * 0.45f;
        var total = labelSize.X + Metrics.Space.Sm * scale + symbolSize * 2f;
        var left = row.Center.X - total * 0.5f;
        Typography.Draw(drawList, new Vector2(left, row.Center.Y - labelSize.Y * 0.5f), label, CasinoColors.Money,
            TextStyles.Headline);
        MachineSymbols.Draw(drawList, machineId, expander,
            new Vector2(left + labelSize.X + Metrics.Space.Sm * scale + symbolSize, row.Center.Y), symbolSize, 1f,
            MachineSymbols.ShimmerFrame(0, phase));
    }

    private void DrawCascadeRow(ImDrawListPtr drawList, Rect row, float scale)
    {
        if (playback.HasRound && playback.InFeature)
        {
            var total = Math.Max(1, playback.FeatureTotal);
            var text = featureTotalLabel.Get(L.Machines.FeatureTotal, total);
            var fitted = Typography.FitText(text, row.Width, TextStyles.Title3);
            Typography.DrawCentered(drawList, row.Center, fitted, CasinoColors.Money, TextStyles.Title3);
            return;
        }

        var ladder = CrystalCascadeRules.TumbleLadder;
        var current = playback.HasRound && playback.Active ? playback.Current.Multiplier : 0;
        var pip = row.Width / ladder.Length;
        for (var rung = 0; rung < ladder.Length; rung++)
        {
            var min = new Vector2(row.Min.X + rung * pip + 3f * scale, row.Min.Y);
            var max = new Vector2(min.X + pip - 6f * scale, row.Max.Y);
            var lit = current >= ladder[rung] && current > 0;
            Squircle.Fill(drawList, min, max, (max.Y - min.Y) * 0.5f,
                ImGui.GetColorU32(lit ? CasinoColors.LightB with { W = 0.85f } : new Vector4(1f, 1f, 1f, 0.08f)));
            var label = CasinoMultiples.Label(ladder[rung] * 100);
            Typography.DrawCentered(drawList, (min + max) * 0.5f,
                Typography.FitText(label, max.X - min.X, TextStyles.Headline),
                lit ? new Vector4(0.02f, 0.05f, 0.08f, 1f) : StageInks.Strong, TextStyles.Headline);
        }
    }

    private void DrawMeterRow(ImDrawListPtr drawList, Rect row, float scale)
    {
        var column = row.Width / 4f;
        var bet = Composer.Amount;
        for (var tier = 0; tier < 4; tier++)
        {
            var min = new Vector2(row.Min.X + tier * column, row.Min.Y);
            var cell = new Rect(min, new Vector2(min.X + column, row.Max.Y));
            var label = tier switch
            {
                0 => L.Machines.Mini,
                1 => L.Machines.Minor,
                2 => L.Machines.Major,
                _ => L.Machines.Grand,
            };
            long value;
            var fill = -1f;
            if (tier < 2)
            {
                var meter = meters[tier];
                var reset = SlotsRules.ChipsFor(bet, tier == 0 ? MoogleMoneyRules.MiniResetUnits
                    : MoogleMoneyRules.MinorResetUnits);
                var ceiling = SlotsRules.ChipsFor(bet, tier == 0 ? MoogleMoneyRules.MiniCeilingUnits
                    : MoogleMoneyRules.MinorCeilingUnits);
                value = meter?.Value ?? reset;
                fill = ceiling > reset ? Math.Clamp((float)(value - reset) / (ceiling - reset), 0f, 1f) : 0f;
            }
            else
            {
                value = SlotsRules.ChipsFor(bet, tier == 2 ? MoogleMoneyRules.MajorUnits : MoogleMoneyRules.GrandUnits);
            }

            var labelText = Typography.FitText(Loc.T(label), column - 4f * scale, TextStyles.Footnote);
            var labelHeight = Typography.LineHeight(TextStyles.Footnote);
            Typography.DrawCentered(drawList, new Vector2(cell.Center.X, cell.Min.Y + labelHeight * 0.5f), labelText,
                tier == 3 ? CasinoColors.LightA : CasinoColors.MoneyHighlight, TextStyles.Footnote);
            var amountStyle = labelHeight + Typography.LineHeight(TextStyles.Title3) + MeterBarHeight * scale
                <= cell.Height ? TextStyles.Title3 : TextStyles.Headline;
            var amount = Typography.FitText(NumberText.Compact(value), column - 4f * scale, amountStyle);
            Typography.DrawCentered(drawList,
                new Vector2(cell.Center.X, cell.Min.Y + labelHeight + Typography.LineHeight(amountStyle) * 0.5f),
                amount, CasinoColors.Money, amountStyle);
            if (fill < 0f)
            {
                continue;
            }

            var barMin = new Vector2(cell.Min.X + 6f * scale, cell.Max.Y - MeterBarHeight * scale);
            var barMax = new Vector2(cell.Max.X - 6f * scale, cell.Max.Y);
            Squircle.Fill(drawList, barMin, barMax, MeterBarHeight * 0.5f * scale,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.14f)));
            Squircle.Fill(drawList, barMin, new Vector2(barMin.X + (barMax.X - barMin.X) * MathF.Max(0.04f, fill),
                barMax.Y), MeterBarHeight * 0.5f * scale, ImGui.GetColorU32(CasinoColors.Money));
        }
    }

    private void DrawBanners(ImDrawListPtr drawList, in CasinoStageFrame frame, float scale)
    {
        if (!playback.HasRound)
        {
            return;
        }

        var beat = playback.Beat;
        var step = playback.Current;
        switch (beat)
        {
            case MachineBeat.Intro:
                DrawIntro(drawList, frame, scale);
                return;
            case MachineBeat.Buy:
                Banner(drawList, Loc.T(L.Machines.BuyIntro), introSpins.Get(L.Machines.IntroFreeSpins,
                    Math.Max(step.SpinsAdded, CrystalCascadeRules.FreeSpins)), MachineArt.NeonOf(machineId), frame,
                    scale);
                return;
            case MachineBeat.Hold:
                Banner(drawList, Loc.T(L.Machines.IntroHold), respinCounter.Get(L.Machines.Respins, step.SpinsLeft),
                    CasinoColors.Money, frame, scale);
                return;
            case MachineBeat.Meter:
                var coin = step.Coins is { Length: > 0 } coins ? coins[0] : NoCoin;
                Banner(drawList, MachineReels.CoinLabel(coin), meterHit.Get(L.Machines.MeterHit, coin.Value),
                    CasinoColors.Money, frame, scale);
                return;
            case MachineBeat.Outro:
                Banner(drawList, Loc.T(L.Machines.FeatureWin), NumberText.Group(playback.Committed),
                    CasinoColors.Money, frame, scale);
                return;
            case MachineBeat.Present when step.SpinsAdded > 0 && playback.InFeature
                && playback.StepIndex > 0 && !string.Equals(step.Kind, SlotsRules.StepBase, StringComparison.Ordinal):
                Banner(drawList, retrigger.Get(L.Machines.Retrigger, step.SpinsAdded), string.Empty,
                    MachineArt.NeonOf(machineId), frame, scale);
                return;
            case MachineBeat.Present when MachineRoundPlayback.HasWinLine(step, SlotsRules.LineGrand):
                Banner(drawList, Loc.T(L.Machines.Grand), NumberText.Group(step.Pay), CasinoColors.LightA, frame,
                    scale);
                return;
        }

        if (playback.Finished && playback.CapApplied)
        {
            var note = Typography.FitText(Loc.T(L.Machines.CapNote), window.Width, TextStyles.Footnote);
            Typography.DrawCentered(drawList, new Vector2(window.Center.X, window.Max.Y - 14f * scale), note,
                StageInks.Strong, TextStyles.Footnote);
        }
    }

    private void DrawIntro(ImDrawListPtr drawList, in CasinoStageFrame frame, float scale)
    {
        drawList.AddRectFilled(window.Min, window.Max, ImGui.GetColorU32(Veil), 12f * scale);
        CasinoLights.BulbChase(drawList, window, 12f * scale, scale, frame.Phase * 2f, CasinoLights.BulbPitch,
            CasinoColors.Money, MachineArt.NeonOf(machineId), 1f);
        var next = playback.Current;
        var spins = Math.Max(next.SpinsLeft + 1, 1);
        var title = string.Equals(next.Kind, SlotsRules.StepGame, StringComparison.Ordinal)
            ? introSpins.Get(L.Machines.IntroFreeGames, spins)
            : introSpins.Get(L.Machines.IntroFreeSpins, spins);
        var titleText = Typography.FitText(title, window.Width * 0.9f, TextStyles.Title1);
        var titleCenter = new Vector2(window.Center.X, window.Min.Y + window.Height * 0.2f);
        Typography.DrawCentered(drawList, titleCenter, titleText, CasinoColors.Money, TextStyles.Title1);
        if (!string.Equals(machineId, SlotsRules.BirdId, StringComparison.Ordinal))
        {
            MachineArt.Hero(drawList, window.Center + new Vector2(0f, window.Height * 0.1f), window.Height * 0.2f,
                machineId, frame.Phase);
            return;
        }

        var progress = playback.BeatProgress / 0.8f;
        var symbol = playback.PickerSymbol(progress);
        var center = window.Center + new Vector2(0f, window.Height * 0.08f);
        var extent = MathF.Min(window.Width, window.Height) * 0.22f;
        var settledPick = progress >= 1f;
        Squircle.FillVerticalGradient(drawList, center - new Vector2(extent * 1.3f, extent * 1.3f),
            center + new Vector2(extent * 1.3f, extent * 1.3f), extent * 0.3f,
            ImGui.GetColorU32(new Vector4(0.5f, 0.25f, 0.04f, 0.9f)), ImGui.GetColorU32(new Vector4(0.2f, 0.08f, 0f, 0.9f)));
        MachineSymbols.Draw(drawList, machineId, symbol, center, extent * (settledPick ? 1.1f : 1f), 1f, settledPick);
        var caption = Typography.FitText(Loc.T(L.Machines.Expanding), window.Width * 0.9f, TextStyles.Title3);
        Typography.DrawCentered(drawList, new Vector2(window.Center.X, center.Y + extent * 1.3f +
            Typography.LineHeight(TextStyles.Title3)), caption, StageInks.Strong, TextStyles.Title3);
    }

    private void Banner(ImDrawListPtr drawList, string title, string subtitle, Vector4 accent,
        in CasinoStageFrame frame, float scale)
    {
        var titleText = Typography.FitText(title, window.Width * 0.84f, TextStyles.Title1);
        var titleSize = Typography.Measure(titleText, TextStyles.Title1);
        var subtitleText = subtitle.Length > 0
            ? Typography.FitText(subtitle, window.Width * 0.84f, TextStyles.Title2)
            : string.Empty;
        var subtitleHeight = subtitleText.Length > 0 ? Typography.LineHeight(TextStyles.Title2) : 0f;
        var pad = 16f * scale;
        var width = MathF.Min(window.Width - pad, MathF.Max(titleSize.X, subtitleText.Length > 0
            ? Typography.Measure(subtitleText, TextStyles.Title2).X : 0f) + pad * 2f);
        var height = titleSize.Y + subtitleHeight + pad * 1.5f;
        var center = window.Center;
        var min = center - new Vector2(width, height) * 0.5f;
        var max = center + new Vector2(width, height) * 0.5f;
        Squircle.Fill(drawList, min, max, 16f * scale, ImGui.GetColorU32(Veil));
        Squircle.Stroke(drawList, min, max, 16f * scale, ImGui.GetColorU32(accent with { W = 0.8f }), 2f * scale);
        CasinoLights.BulbChase(drawList, new Rect(min, max), 16f * scale, scale, frame.Phase, CasinoLights.BulbPitch,
            CasinoColors.Money, accent, 0.9f);
        Typography.DrawCentered(drawList, new Vector2(center.X, min.Y + pad * 0.75f + titleSize.Y * 0.5f), titleText,
            accent, TextStyles.Title1);
        if (subtitleText.Length > 0)
        {
            Typography.DrawCentered(drawList,
                new Vector2(center.X, min.Y + pad * 0.75f + titleSize.Y + subtitleHeight * 0.5f), subtitleText,
                StageInks.Strong, TextStyles.Title2);
        }
    }

    private void DrawStrip(ImDrawListPtr drawList, AppSkin ui, in CasinoStageFrame frame, float scale)
    {
        var radius = strip.Height * 0.3f;
        Squircle.Fill(drawList, strip.Min, strip.Max, radius, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.55f)));
        var leftWidth = strip.Width * 0.46f;
        var left = new Rect(strip.Min, new Vector2(strip.Min.X + leftWidth, strip.Max.Y)).Inset(8f * scale);
        var right = new Rect(new Vector2(strip.Min.X + leftWidth, strip.Min.Y), strip.Max).Inset(8f * scale);
        DrawStripLeft(drawList, ui, frame, left, scale);
        DrawWinAmount(drawList, right);
    }

    private void DrawStripLeft(ImDrawListPtr drawList, AppSkin ui, in CasinoStageFrame frame, Rect area, float scale)
    {
        if (inlineReason.Length > 0)
        {
            var reason = CasinoReasons.Text(inlineReason, store.Ceiling.MaxBet);
            Typography.DrawWrappedCentered(drawList, reason, TextStyles.Footnote, StageInks.Strong,
                new Vector2(area.Center.X, area.Min.Y), area.Width);
            return;
        }

        var buttonHeight = MathF.Min(area.Height, Button.LargeHeight * scale);
        var buttonRect = new Rect(new Vector2(area.Min.X, area.Center.Y - buttonHeight * 0.5f),
            new Vector2(area.Max.X, area.Center.Y + buttonHeight * 0.5f));
        if (playback.Active && playback.InFeature && !frame.Blocked)
        {
            if (Button.Draw(drawList, buttonRect, Loc.T(L.Machines.Skip), ui.Ink, ButtonStyle.Gray, id: SkipId))
            {
                playback.Skip();
            }

            return;
        }

        if (gamble.Offered && !Composer.Auto.Running && !frame.Blocked)
        {
            if (Button.Draw(drawList, buttonRect, Loc.T(L.Machines.Gamble), ui.Ink, ButtonStyle.Tinted, id: GambleId))
            {
                gamble.Choose();
                CasinoSfx.Play(UiSound.CardSnap);
            }

            return;
        }

        string text;
        var style = TextStyles.Title3;
        if (playback.HasRound && playback.InHold)
        {
            text = respinCounter.Get(L.Machines.Respins, Math.Max(0, playback.Current.SpinsLeft));
            style = TextStyles.Title2;
        }
        else if (playback.HasRound && playback.InFeature)
        {
            var played = playback.FeaturePlayed;
            var total = played + playback.SpinsLeftShown();
            var games = string.Equals(machineId, SlotsRules.MoogleId, StringComparison.Ordinal);
            text = featureCounter.Get(games ? L.Machines.FreeGamesCounter : L.Machines.FreeSpinsCounter, played,
                Math.Max(played, total));
        }
        else
        {
            text = betLabel.Get(L.Machines.CostLine, SlotsRules.CostOf(Mode, Composer.Amount));
        }

        var fitted = Typography.FitText(text, area.Width, style);
        Typography.DrawCentered(drawList, area.Center, fitted, StageInks.Strong, style);
    }

    private void DrawWinAmount(ImDrawListPtr drawList, Rect area)
    {
        var shown = playback.HasRound ? rollup.Shown : 0;
        var labelStyle = TextStyles.Footnote;
        var amountStyle = TextStyles.Title1;
        var label = Typography.FitText(Loc.T(L.Machines.Win), area.Width, labelStyle);
        var labelHeight = Typography.LineHeight(labelStyle);
        var amount = NumberText.Group(shown);
        var lossLike = playback.HasRound && settled && playback.TotalWin + playback.Jackpot <= playback.Cost;
        var ink = shown <= 0 || lossLike ? StageInks.Strong with { W = 0.7f } : CasinoColors.Money;
        var amountHeight = Typography.LineHeight(amountStyle);
        var top = area.Center.Y - (labelHeight + amountHeight) * 0.5f;
        var labelSize = Typography.Measure(label, labelStyle);
        Typography.Draw(drawList, new Vector2(area.Max.X - labelSize.X, top), label, CasinoColors.MoneyHighlight,
            labelStyle);
        var size = CurrencyGlyph.MeasureAmount(amount, amountStyle);
        var scaleFit = size.X > area.Width ? area.Width / size.X : 1f;
        var style = scaleFit < 1f ? amountStyle with { Scale = amountStyle.Scale * scaleFit } : amountStyle;
        var fittedSize = CurrencyGlyph.MeasureAmount(amount, style);
        CurrencyGlyph.DrawAmount(drawList, new Vector2(area.Max.X - fittedSize.X, top + labelHeight), amount,
            CurrencyKind.Chips, ink, style);
    }

    private void DrawGamble(ImDrawListPtr drawList, AppSkin ui, in CasinoStageFrame frame, float scale)
    {
        if (!gamble.Open)
        {
            return;
        }

        drawList.AddRectFilled(window.Min, window.Max, ImGui.GetColorU32(Veil), 12f * scale);
        var pad = 10f * scale;
        var inner = window.Inset(pad);
        var heading = Typography.FitText(Loc.T(L.Machines.GambleHeading), inner.Width, TextStyles.Title3);
        var headingHeight = Typography.LineHeight(TextStyles.Title3);
        Typography.DrawCentered(drawList, new Vector2(inner.Center.X, inner.Min.Y + headingHeight * 0.5f), heading,
            StageInks.Strong, TextStyles.Title3);
        var amount = gambleStake.Get(L.Machines.GambleAmount, gamble.Amount);
        var amountText = Typography.FitText(amount, inner.Width, TextStyles.Title1);
        var amountHeight = Typography.LineHeight(TextStyles.Title1);
        var amountCenterY = inner.Min.Y + headingHeight + amountHeight * 0.5f;
        var lost = gamble.Phase == GamblePhase.Closing && !gamble.Won;
        Typography.DrawCentered(drawList, new Vector2(inner.Center.X, amountCenterY), amountText,
            lost ? StageInks.Strong with { W = 0.6f } : CasinoColors.Money, TextStyles.Title1);
        var ladderTop = amountCenterY + amountHeight * 0.5f + Metrics.Space.Xs * scale;
        var ladderHeight = Typography.LineHeight(TextStyles.Footnote) + 8f * scale;
        DrawGambleLadder(drawList, new Rect(new Vector2(inner.Min.X, ladderTop),
            new Vector2(inner.Max.X, ladderTop + ladderHeight)), scale);
        var buttonsHeight = Button.LargeHeight * scale;
        var buttonsTop = inner.Max.Y - buttonsHeight;
        var cardArea = new Rect(new Vector2(inner.Min.X, ladderTop + ladderHeight + Metrics.Space.Xs * scale),
            new Vector2(inner.Max.X, buttonsTop - Metrics.Space.Xs * scale));
        DrawCard(drawList, cardArea, scale);
        var choosing = gamble.Phase == GamblePhase.Choosing && !play.RoundInFlight && !frame.Blocked;
        var gap = Metrics.Space.Sm * scale;
        var third = (inner.Width - gap * 2f) / 3f;
        var red = new Rect(new Vector2(inner.Min.X, buttonsTop), new Vector2(inner.Min.X + third, inner.Max.Y));
        var black = new Rect(new Vector2(red.Max.X + gap, buttonsTop), new Vector2(red.Max.X + gap + third, inner.Max.Y));
        var collect = new Rect(new Vector2(black.Max.X + gap, buttonsTop), inner.Max);
        if (Button.Draw(drawList, red, Loc.T(L.Machines.Red), ui.Ink, ButtonStyle.Prominent, ButtonRole.Destructive,
                enabled: choosing, id: "casino.machines.red") && gamble.Pick(play, SlotsRules.GambleRed))
        {
            CasinoSfx.Play(UiSound.ChipSlide);
        }

        if (Button.Draw(drawList, black, Loc.T(L.Machines.Black), ui.Ink, ButtonStyle.Prominent, enabled: choosing,
                id: "casino.machines.black") && gamble.Pick(play, SlotsRules.GambleBlack))
        {
            CasinoSfx.Play(UiSound.ChipSlide);
        }

        if (Button.Draw(drawList, collect, Loc.T(L.Machines.Collect), ui.Ink, ButtonStyle.Gray, enabled: choosing,
                id: "casino.machines.collect"))
        {
            gamble.Collect();
        }
    }

    private void DrawGambleLadder(ImDrawListPtr drawList, Rect row, float scale)
    {
        var rungs = SlotsRules.GambleMaxSteps;
        var width = row.Width / rungs;
        var reached = gamble.Phase == GamblePhase.Closing && !gamble.Won ? -1 : gamble.Step;
        for (var rung = 0; rung < rungs; rung++)
        {
            var min = new Vector2(row.Min.X + rung * width + 2f * scale, row.Min.Y);
            var max = new Vector2(min.X + width - 4f * scale, row.Max.Y);
            var done = rung < reached;
            Squircle.Fill(drawList, min, max, (max.Y - min.Y) * 0.5f,
                ImGui.GetColorU32(done ? CasinoColors.Money with { W = 0.85f } : new Vector4(1f, 1f, 1f, 0.08f)));
            var value = gamble.RungAmount(rung + 1);
            var text = Typography.FitText(NumberText.Compact(value), max.X - min.X, TextStyles.Footnote);
            Typography.DrawCentered(drawList, (min + max) * 0.5f, text,
                done ? new Vector4(0.08f, 0.05f, 0.02f, 1f) : StageInks.Strong, TextStyles.Footnote);
        }
    }

    private void DrawCard(ImDrawListPtr drawList, Rect area, float scale)
    {
        var height = MathF.Min(area.Height, CardHeight * scale);
        var width = MathF.Min(height * CardWidth / CardHeight, area.Width);
        var reveal = gamble.RevealProgress;
        var flip = MathF.Abs(MathF.Cos(reveal * MathF.PI));
        var shown = gamble.CardShown && gamble.Card >= 0;
        var halfWidth = width * 0.5f * MathF.Max(0.05f, flip);
        var center = area.Center;
        var min = new Vector2(center.X - halfWidth, center.Y - height * 0.5f);
        var max = new Vector2(center.X + halfWidth, center.Y + height * 0.5f);
        var rounding = 8f * scale;
        if (!shown)
        {
            Squircle.FillVerticalGradient(drawList, min, max, rounding, ImGui.GetColorU32(CasinoColors.LightA),
                ImGui.GetColorU32(new Vector4(0.35f, 0.05f, 0.25f, 1f)));
            Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(CasinoColors.MoneyHighlight), 2f * scale);
            MachineSymbols.DrawBird(drawList, center, MathF.Min(halfWidth, height * 0.5f) * 0.55f, 0.9f);
            return;
        }

        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(CardPaper));
        var redCard = gamble.Card == SlotsRules.GambleRed;
        var suitInk = redCard ? CardRed : CardBlack;
        var suitSymbol = redCard ? 5 : 4;
        SlotsSymbolArt.Draw(drawList, suitSymbol, center, MathF.Min(halfWidth, height * 0.5f) * 0.6f, 1f);
        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(suitInk), 2f * scale);
        var verdict = gamble.Won ? L.Machines.GambleWon : L.Machines.GambleLost;
        var text = Typography.FitText(Loc.T(verdict), area.Width, TextStyles.Headline);
        Typography.DrawCentered(drawList, new Vector2(center.X, max.Y + Typography.LineHeight(TextStyles.Headline) * 0.6f),
            text, gamble.Won ? CasinoColors.Money : StageInks.Strong, TextStyles.Headline);
    }

    private void DrawKnob(AppSkin ui, Rect rect, CasinoSittingDto sitting, bool changeable, float scale)
    {
        if (rect.Width <= 0f)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var gap = Metrics.Space.Sm * scale;
        var turboWidth = rect.Width * 0.28f;
        var turboRect = new Rect(rect.Min, new Vector2(rect.Min.X + turboWidth, rect.Max.Y));
        var turboHover = UiInteract.Hover(turboRect.Min, turboRect.Max);
        ChipRail.PaintChip(drawList, turboRect, Loc.T(L.Machines.Turbo), turbo, turboHover, ui.Ink);
        if (UiInteract.Click(turboRect.Min, turboRect.Max, turboHover))
        {
            turbo = !turbo;
            UiFeedback.Play(turbo ? UiSound.ToggleOn : UiSound.ToggleOff);
        }

        var restLeft = turboRect.Max.X + gap;
        if (machineIndex != 1)
        {
            var info = Typography.FitText(Loc.T(machineIndex == 2 ? L.Machines.MoogleKnobInfo : L.Machines.LinesInfo),
                rect.Max.X - restLeft, TextStyles.Footnote);
            var size = Typography.Measure(info, TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(restLeft, rect.Center.Y - size.Y * 0.5f), info, StageInks.Strong,
                TextStyles.Footnote);
            return;
        }

        var bet = Composer.Amount;
        var half = (rect.Max.X - restLeft - gap) * 0.5f;
        var anteRect = new Rect(new Vector2(restLeft, rect.Min.Y), new Vector2(restLeft + half, rect.Max.Y));
        var anteHover = changeable && UiInteract.Hover(anteRect.Min, anteRect.Max);
        ChipRail.PaintChip(drawList, anteRect, anteLabel.Get(L.Machines.AnteCost,
            SlotsRules.CostOf(SlotsRules.AnteMode, bet)), ante, anteHover, ui.Ink);
        if (UiInteract.Click(anteRect.Min, anteRect.Max, anteHover))
        {
            ante = !ante;
            UiFeedback.Play(ante ? UiSound.ToggleOn : UiSound.ToggleOff);
        }

        var buyRect = new Rect(new Vector2(anteRect.Max.X + gap, rect.Min.Y), rect.Max);
        var cost = SlotsRules.CostOf(SlotsRules.BuyMode, bet);
        var canBuy = changeable && cost <= sitting.Stack && !Composer.Auto.Running;
        if (Button.Draw(drawList, buyRect, buyLabel.Get(L.Machines.BuyBonus, cost), ui.Ink, ButtonStyle.Tinted,
                enabled: canBuy, id: "casino.machines.buy"))
        {
            AskBuy(bet);
        }
    }
}
