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
    private const float CardMinHeight = 36f;
    private const float IntroPad = 10f;
    private const float IntroTitleShare = 0.3f;
    private const float IntroCaptionShare = 0.2f;
    private const float IntroTileSpan = 1.3f;
    private const float NotePad = 8f;
    private const string SkipId = "casino.machines.skip";
    private const string GambleId = "casino.machines.gamble";
    private const string ReasonMarquee = "casino.machines.reason";

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

        var shrink = MathF.Min(width / MathF.Max(1f, amountSize.X),
            (area.Height - eyebrowHeight) / MathF.Max(1f, amountSize.Y));
        if (shrink < 1f)
        {
            amountStyle = amountStyle with { Scale = amountStyle.Scale * MathF.Max(0.1f, shrink) };
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
            var amountRoom = cell.Height - labelHeight - (fill < 0f ? 0f : MeterBarHeight * scale);
            var amountStyle = FitHeight(TextStyles.Title3, amountRoom);
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
            var pad = NotePad * scale;
            var note = Typography.FitText(Loc.T(L.Machines.CapNote), window.Width - pad * 4f, TextStyles.Footnote);
            var size = Typography.Measure(note, TextStyles.Footnote);
            var center = new Vector2(window.Center.X, window.Max.Y - pad - size.Y * 0.5f);
            var half = new Vector2(size.X * 0.5f + pad, size.Y * 0.5f + pad * 0.5f);
            Squircle.Fill(drawList, center - half, center + half, half.Y, ImGui.GetColorU32(Veil));
            Typography.DrawCentered(drawList, center, note, StageInks.Strong, TextStyles.Footnote);
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
        var pad = IntroPad * scale;
        var inner = window.Inset(pad);
        var bird = string.Equals(machineId, SlotsRules.BirdId, StringComparison.Ordinal);
        var titleStyle = FitHeight(TextStyles.Title1, inner.Height * IntroTitleShare);
        var captionStyle = FitHeight(TextStyles.Title3, inner.Height * IntroCaptionShare);
        var titleHeight = Typography.LineHeight(titleStyle);
        var captionHeight = bird ? Typography.LineHeight(captionStyle) : 0f;
        var titleText = Typography.FitText(title, inner.Width, titleStyle);
        Typography.DrawCentered(drawList, new Vector2(inner.Center.X, inner.Min.Y + titleHeight * 0.5f), titleText,
            CasinoColors.Money, titleStyle);
        var gap = Metrics.Space.Xs * scale;
        var middleTop = inner.Min.Y + titleHeight + gap;
        var middleBottom = MathF.Max(middleTop, inner.Max.Y - captionHeight - (bird ? gap : 0f));
        var middle = new Rect(new Vector2(inner.Min.X, middleTop), new Vector2(inner.Max.X, middleBottom));
        if (!bird)
        {
            MachineArt.Hero(drawList, middle.Center, MathF.Min(middle.Width, middle.Height) * 0.4f, machineId,
                frame.Phase);
            return;
        }

        var progress = playback.BeatProgress / 0.8f;
        var symbol = playback.PickerSymbol(progress);
        var center = middle.Center;
        var extent = MathF.Min(middle.Width, middle.Height) * 0.5f / IntroTileSpan;
        var settledPick = progress >= 1f;
        var tile = extent * IntroTileSpan;
        Squircle.FillVerticalGradient(drawList, center - new Vector2(tile, tile), center + new Vector2(tile, tile),
            extent * 0.3f, ImGui.GetColorU32(new Vector4(0.5f, 0.25f, 0.04f, 0.9f)),
            ImGui.GetColorU32(new Vector4(0.2f, 0.08f, 0f, 0.9f)));
        MachineSymbols.Draw(drawList, machineId, symbol, center, extent * (settledPick ? 1.1f : 1f), 1f, settledPick);
        var caption = Typography.FitText(Loc.T(L.Machines.Expanding), inner.Width, captionStyle);
        Typography.DrawCentered(drawList, new Vector2(inner.Center.X, inner.Max.Y - captionHeight * 0.5f), caption,
            StageInks.Strong, captionStyle);
    }

    private void Banner(ImDrawListPtr drawList, string title, string subtitle, Vector4 accent,
        in CasinoStageFrame frame, float scale)
    {
        var pad = 16f * scale;
        var titleStyle = TextStyles.Title1;
        var subtitleStyle = TextStyles.Title2;
        var lines = Typography.LineHeight(titleStyle) + (subtitle.Length > 0 ? Typography.LineHeight(subtitleStyle) : 0f);
        var room = window.Height - pad * 2.5f;
        if (lines > room && lines > 0f)
        {
            var shrink = MathF.Max(0.1f, room) / lines;
            titleStyle = titleStyle with { Scale = titleStyle.Scale * shrink };
            subtitleStyle = subtitleStyle with { Scale = subtitleStyle.Scale * shrink };
        }

        var titleText = Typography.FitText(title, window.Width * 0.84f, titleStyle);
        var titleSize = Typography.Measure(titleText, titleStyle);
        var subtitleText = subtitle.Length > 0
            ? Typography.FitText(subtitle, window.Width * 0.84f, subtitleStyle)
            : string.Empty;
        var subtitleHeight = subtitleText.Length > 0 ? Typography.LineHeight(subtitleStyle) : 0f;
        var width = MathF.Min(window.Width - pad, MathF.Max(titleSize.X, subtitleText.Length > 0
            ? Typography.Measure(subtitleText, subtitleStyle).X : 0f) + pad * 2f);
        var height = titleSize.Y + subtitleHeight + pad * 1.5f;
        var center = window.Center;
        var min = center - new Vector2(width, height) * 0.5f;
        var max = center + new Vector2(width, height) * 0.5f;
        Squircle.Fill(drawList, min, max, 16f * scale, ImGui.GetColorU32(Veil));
        Squircle.Stroke(drawList, min, max, 16f * scale, ImGui.GetColorU32(accent with { W = 0.8f }), 2f * scale);
        CasinoLights.BulbChase(drawList, new Rect(min, max), 16f * scale, scale, frame.Phase, CasinoLights.BulbPitch,
            CasinoColors.Money, accent, 0.9f);
        Typography.DrawCentered(drawList, new Vector2(center.X, min.Y + pad * 0.75f + titleSize.Y * 0.5f), titleText,
            accent, titleStyle);
        if (subtitleText.Length > 0)
        {
            Typography.DrawCentered(drawList,
                new Vector2(center.X, min.Y + pad * 0.75f + titleSize.Y + subtitleHeight * 0.5f), subtitleText,
                StageInks.Strong, subtitleStyle);
        }
    }

    private static TextStyle FitHeight(in TextStyle style, float height)
    {
        var line = Typography.LineHeight(style);
        if (line <= height || line <= 0f)
        {
            return style;
        }

        return style with { Scale = style.Scale * MathF.Max(0.1f, height) / line };
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
            var block = Typography.MeasureWrappedBlock(reason, TextStyles.Footnote, area.Width);
            if (block.Y <= area.Height)
            {
                Typography.DrawWrappedCentered(drawList, reason, TextStyles.Footnote, StageInks.Strong,
                    new Vector2(area.Center.X, area.Center.Y - block.Y * 0.5f), area.Width);
                return;
            }

            var lineHeight = Typography.LineHeight(TextStyles.Footnote);
            Marquee.DrawCentered(drawList, new MarqueeId(ReasonMarquee, 0), reason, area.Center.X,
                area.Center.Y - lineHeight * 0.5f, area.Width, TextStyles.Footnote, StageInks.Strong,
                UiInteract.Hover(area.Min, area.Max));
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

        style = FitHeight(style, area.Height);
        var fitted = Typography.FitText(text, area.Width, style);
        Typography.DrawCentered(drawList, area.Center, fitted, StageInks.Strong, style);
    }

    private void DrawWinAmount(ImDrawListPtr drawList, Rect area)
    {
        var shown = playback.HasRound ? rollup.Shown : 0;
        var labelStyle = TextStyles.Footnote;
        var label = Typography.FitText(Loc.T(L.Machines.Win), area.Width, labelStyle);
        var labelHeight = Typography.LineHeight(labelStyle);
        var amountStyle = FitHeight(TextStyles.Title1, area.Height - labelHeight);
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
        var cardTop = ladderTop + ladderHeight + Metrics.Space.Xs * scale;
        var cardArea = new Rect(new Vector2(inner.Min.X, cardTop),
            new Vector2(inner.Max.X, MathF.Max(cardTop, buttonsTop - Metrics.Space.Xs * scale)));
        if (cardArea.Height >= CardMinHeight * scale)
        {
            DrawCard(drawList, cardArea, scale);
        }
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
        var verdictY = MathF.Min(max.Y + Typography.LineHeight(TextStyles.Headline) * 0.6f,
            area.Max.Y - Typography.LineHeight(TextStyles.Headline) * 0.5f);
        var textSize = Typography.Measure(text, TextStyles.Headline);
        var plate = new Vector2(textSize.X * 0.5f + NotePad * scale, textSize.Y * 0.5f);
        var plateCenter = new Vector2(center.X, verdictY);
        Squircle.Fill(drawList, plateCenter - plate, plateCenter + plate, plate.Y, ImGui.GetColorU32(Veil));
        Typography.DrawCentered(drawList, plateCenter, text, gamble.Won ? CasinoColors.Money : StageInks.Strong,
            TextStyles.Headline);
    }

    private void DrawKnob(AppSkin ui, Rect rect, CasinoSittingDto sitting, bool changeable, float scale)
    {
        if (rect.Width <= 0f)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var bonusRow = MachineBonusRow.Compute(rect, Metrics.Space.Sm * scale);
        var turboRect = bonusRow.Turbo;
        var turboHover = UiInteract.Hover(turboRect.Min, turboRect.Max);
        ChipRail.PaintChip(drawList, turboRect, Loc.T(L.Machines.Turbo), turbo, turboHover, ui.Ink);
        if (UiInteract.Click(turboRect.Min, turboRect.Max, turboHover))
        {
            turbo = !turbo;
            UiFeedback.Play(turbo ? UiSound.ToggleOn : UiSound.ToggleOff);
        }

        if (machineIndex != 1)
        {
            var infoRect = bonusRow.Info;
            var info = Typography.FitText(Loc.T(machineIndex == 2 ? L.Machines.MoogleKnobInfo : L.Machines.LinesInfo),
                infoRect.Width, TextStyles.Footnote);
            var size = Typography.Measure(info, TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(infoRect.Min.X, infoRect.Center.Y - size.Y * 0.5f), info,
                StageInks.Strong, TextStyles.Footnote);
            return;
        }

        var bet = Composer.Amount;
        var anteRect = bonusRow.Ante;
        var anteHover = changeable && UiInteract.Hover(anteRect.Min, anteRect.Max);
        ChipRail.PaintChip(drawList, anteRect, anteLabel.Get(L.Machines.AnteCost,
            SlotsRules.CostOf(SlotsRules.AnteMode, bet)), ante, anteHover, ui.Ink);
        if (UiInteract.Click(anteRect.Min, anteRect.Max, anteHover))
        {
            ante = !ante;
            UiFeedback.Play(ante ? UiSound.ToggleOn : UiSound.ToggleOff);
        }

        var buyRect = bonusRow.Buy;
        var cost = SlotsRules.CostOf(SlotsRules.BuyMode, bet);
        var canBuy = changeable && cost <= sitting.Stack && !Composer.Auto.Running;
        if (Button.Draw(drawList, buyRect, buyLabel.Get(L.Machines.BuyBonus, cost), ui.Ink, ButtonStyle.Tinted,
                enabled: canBuy, id: "casino.machines.buy"))
        {
            AskBuy(bet);
        }
    }
}
