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
    private const string ReasonMarquee = "casino.machines.reason";
    private const float StripRoundShare = 0.3f;
    private const float MutedValueAlpha = 0.7f;

    private const float HeroExtentShare = 0.42f;
    private const float BannerTextShare = 0.84f;
    private const float SignHeightShare = 0.62f;
    private const float JackpotRadius = 12f;
    private const float JackpotRimAlpha = 0.55f;
    private const float FutureRungAlpha = 0.75f;
    private const float MeterInset = 4f;

    private static readonly TextStyle EyebrowStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle CaptionStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle JackpotEyebrowStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle JackpotAmountStyle = TextStyles.Title3;
    private static readonly Vector4 StripFill = new(0f, 0f, 0f, 0.55f);
    private static readonly Vector4 JackpotFill = new(0f, 0f, 0f, 0.35f);
    private static readonly Vector4 Divider = new(1f, 1f, 1f, 0.07f);
    private static readonly Vector4 FutureRungFill = new(1f, 1f, 1f, 0.08f);
    private static readonly Vector4 ActiveRungInk = new(0.02f, 0.05f, 0.08f, 1f);

    private static readonly Vector4 GlassTop = new(0.10f, 0.06f, 0.16f, 0.92f);
    private static readonly Vector4 GlassBottom = new(0.03f, 0.02f, 0.06f, 0.92f);
    private static readonly Vector4 CardRed = new(0.92f, 0.20f, 0.28f, 1f);
    private static readonly Vector4 CardBlack = new(0.10f, 0.10f, 0.14f, 1f);
    private static readonly Vector4 CardPaper = new(0.97f, 0.95f, 0.90f, 1f);
    private static readonly Vector4 Veil = new(0.01f, 0.0f, 0.03f, 0.78f);
    private static readonly CasinoSlotsCoinDto NoCoin = new();

    private AmountSlot buyLabel;
    private AmountSlot meterHit;
    private AmountSlot gambleStake;
    private LabelPairSlot featureCounter;
    private LabelSlot respinCounter;
    private LabelSlot introSpins;
    private LabelSlot retrigger;
    private LabelSlot featureTotalLabel;

    private void DrawGlass(ImDrawListPtr drawList, in CasinoStageFrame frame, float scale)
    {
        var radius = GlassRadius * scale;
        var neon = MachineArt.NeonOf(machineId);
        Squircle.FillVerticalGradient(drawList, glass.Min, glass.Max, radius, ImGui.GetColorU32(GlassTop),
            ImGui.GetColorU32(GlassBottom));
        Squircle.Stroke(drawList, glass.Min, glass.Max, radius, ImGui.GetColorU32(neon with { W = 0.4f }), 1.5f * scale);
        var eyebrow = Loc.T(L.Machines.FloorJackpot);
        var amount = NumberText.Compact(store.Jackpot);
        var eyebrowSize = Typography.Measure(eyebrow, JackpotEyebrowStyle);
        var amountSize = CurrencyGlyph.MeasureAmount(amount, JackpotAmountStyle);
        var ladder = machineIndex == 1 && !(playback.HasRound && playback.InFeature);
        var layout = MachineGlassLayout.Compute(glass,
            new Vector2(MathF.Max(eyebrowSize.X, amountSize.X), eyebrowSize.Y + amountSize.Y),
            ladder ? Typography.LineHeight(CaptionStyle) : 0f, scale);
        MachineArt.Hero(drawList, layout.Hero.Center, layout.Hero.Height * HeroExtentShare, machineId, frame.Phase);
        var sign = MachineArt.SignOf(machineId);
        var signHeight = CasinoSigns.HeightToFit(sign, layout.Sign.Width, layout.Sign.Height * SignHeightShare);
        var flicker = 0.85f + 0.15f * Pulse.Wave(Pulse.Breath);
        CasinoSigns.Draw(drawList, sign, layout.Sign.Center, signHeight, neon, flicker);
        DrawFloorJackpot(drawList, layout.Jackpot, eyebrow, amount, scale);
        drawList.AddLine(new Vector2(layout.Top.Min.X, layout.Top.Max.Y), layout.Top.Max,
            ImGui.GetColorU32(Divider), MathF.Max(1f, scale));
        switch (machineId)
        {
            case SlotsRules.CascadeId:
                DrawCascadeRow(drawList, layout, scale);
                break;
            case SlotsRules.MoogleId:
                DrawMeterRow(drawList, layout.Content, scale);
                break;
            default:
                DrawBirdRow(drawList, layout.Content, frame.Phase, scale);
                break;
        }
    }

    private static void DrawFloorJackpot(ImDrawListPtr drawList, Rect capsule, string eyebrow, string amount,
        float scale)
    {
        var radius = MathF.Min(capsule.Height * 0.5f, JackpotRadius * scale);
        Squircle.Fill(drawList, capsule.Min, capsule.Max, radius, ImGui.GetColorU32(JackpotFill));
        Squircle.Stroke(drawList, capsule.Min, capsule.Max, radius,
            ImGui.GetColorU32(CasinoColors.Money with { W = JackpotRimAlpha }), MathF.Max(1f, scale));
        var width = MathF.Max(1f, capsule.Width - MachineGlassLayout.JackpotPadX * 2f * scale);
        var room = MathF.Max(1f, capsule.Height - MachineGlassLayout.JackpotPadY * 2f * scale);
        var eyebrowStyle = MachineText.Fit(eyebrow, width, JackpotEyebrowStyle);
        var eyebrowSize = Typography.Measure(eyebrow, eyebrowStyle);
        var amountStyle = JackpotAmountStyle;
        var amountSize = CurrencyGlyph.MeasureAmount(amount, amountStyle);
        var shrink = MathF.Min(width / MathF.Max(1f, amountSize.X),
            (room - eyebrowSize.Y) / MathF.Max(1f, amountSize.Y));
        if (shrink < 1f)
        {
            amountStyle = amountStyle with { Scale = amountStyle.Scale * MathF.Max(MachineText.LabelShrink, shrink) };
            amountSize = CurrencyGlyph.MeasureAmount(amount, amountStyle);
        }

        var top = capsule.Center.Y - (eyebrowSize.Y + amountSize.Y) * 0.5f;
        Typography.Draw(drawList, new Vector2(capsule.Center.X - eyebrowSize.X * 0.5f, top), eyebrow,
            CasinoColors.MoneyHighlight, eyebrowStyle);
        CurrencyGlyph.DrawAmount(drawList, new Vector2(capsule.Center.X - amountSize.X * 0.5f, top + eyebrowSize.Y),
            amount, CurrencyKind.Chips, CasinoColors.Money, amountStyle);
    }

    private void DrawBirdRow(ImDrawListPtr drawList, Rect row, float phase, float scale)
    {
        var expander = playback.HasRound && playback.InFeature ? playback.Expander : -1;
        if (expander < 0 || playback.Beat == MachineBeat.Intro)
        {
            var text = Loc.T(L.Machines.LinesInfo);
            Typography.DrawCentered(drawList, row.Center, text, StageInks.Strong,
                MachineText.Fit(text, row.Width, TextStyles.Headline));
            return;
        }

        var symbolSize = row.Height * 0.45f;
        var gap = Metrics.Space.Sm * scale;
        var label = Loc.T(L.Machines.Expanding);
        var style = MachineText.Fit(label, MathF.Max(1f, row.Width - gap - symbolSize * 2f), TextStyles.Headline);
        var labelSize = Typography.Measure(label, style);
        var total = labelSize.X + gap + symbolSize * 2f;
        var left = row.Center.X - total * 0.5f;
        Typography.Draw(drawList, new Vector2(left, row.Center.Y - labelSize.Y * 0.5f), label, CasinoColors.Money,
            style);
        MachineSymbols.Draw(drawList, machineId, expander, new Vector2(left + labelSize.X + gap + symbolSize,
            row.Center.Y), symbolSize, 1f, MachineSymbols.ShimmerFrame(0, phase));
    }

    private void DrawCascadeRow(ImDrawListPtr drawList, in MachineGlassLayout layout, float scale)
    {
        var row = layout.Content;
        if (playback.HasRound && playback.InFeature)
        {
            var total = Math.Max(1, playback.FeatureTotal);
            var text = featureTotalLabel.Get(L.Machines.FeatureTotal, total);
            Typography.DrawCentered(drawList, row.Center, text, CasinoColors.Money,
                MachineText.Fit(text, row.Width, TextStyles.Title3));
            return;
        }

        var caption = Loc.T(L.Machines.TumbleLadder);
        var captionStyle = MachineText.Fit(caption, layout.Caption.Width, CaptionStyle);
        var captionSize = Typography.Measure(caption, captionStyle);
        Typography.Draw(drawList, new Vector2(layout.Caption.Center.X - captionSize.X * 0.5f,
            layout.Caption.Center.Y - captionSize.Y * 0.5f), caption, CasinoColors.InkBody, captionStyle);
        var ladder = CrystalCascadeRules.TumbleLadder;
        var multiplier = playback.HasRound && playback.Active ? playback.Current.Multiplier : 1;
        var active = MachineGlassLayout.ActiveRung(ladder, multiplier);
        for (var rung = 0; rung < ladder.Length; rung++)
        {
            var pill = MachineGlassLayout.Pill(row, rung, ladder.Length, scale);
            var radius = pill.Height * 0.5f;
            var label = CasinoMultiples.Label(ladder[rung] * 100);
            var ink = StageInks.Strong with { W = FutureRungAlpha };
            if (rung == active)
            {
                Squircle.Stroke(drawList, pill.Min, pill.Max, radius,
                    ImGui.GetColorU32(CasinoColors.LightB with { W = 0.35f }), 3f * scale);
                Squircle.Fill(drawList, pill.Min, pill.Max, radius, ImGui.GetColorU32(CasinoColors.LightB));
                ink = ActiveRungInk;
            }
            else if (rung < active)
            {
                Squircle.Fill(drawList, pill.Min, pill.Max, radius,
                    ImGui.GetColorU32(CasinoColors.LightB with { W = 0.28f }));
                ink = StageInks.Strong;
            }
            else
            {
                Squircle.Fill(drawList, pill.Min, pill.Max, radius, ImGui.GetColorU32(FutureRungFill));
            }

            Typography.DrawCentered(drawList, pill.Center, label, ink,
                MachineText.Fit(label, pill.Width - pill.Height * 0.5f, TextStyles.SubheadlineEmphasized));
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

            var labelText = Loc.T(label);
            var labelStyle = MachineText.Fit(labelText, column - MeterInset * scale, TextStyles.FootnoteEmphasized);
            var labelHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
            Typography.DrawCentered(drawList, new Vector2(cell.Center.X, cell.Min.Y + labelHeight * 0.5f), labelText,
                tier == 3 ? CasinoColors.LightA : CasinoColors.MoneyHighlight, labelStyle);
            var amountRoom = cell.Height - labelHeight - (fill < 0f ? 0f : MeterBarHeight * scale);
            var amount = NumberText.Compact(value);
            var amountStyle = MachineText.Fit(amount, column - MeterInset * scale, FitHeight(TextStyles.Title3, amountRoom));
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
            var note = Loc.T(L.Machines.CapNote);
            var style = MachineText.Fit(note, window.Width - pad * 4f, TextStyles.FootnoteEmphasized);
            var size = Typography.Measure(note, style);
            var center = new Vector2(window.Center.X, window.Max.Y - pad - size.Y * 0.5f);
            var half = new Vector2(size.X * 0.5f + pad, size.Y * 0.5f + pad * 0.5f);
            Squircle.Fill(drawList, center - half, center + half, half.Y, ImGui.GetColorU32(Veil));
            Typography.DrawCentered(drawList, center, note, StageInks.Strong, style);
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
        var titleStyle = MachineText.Fit(title, inner.Width, FitHeight(TextStyles.Title1, inner.Height * IntroTitleShare),
            MachineText.DisplayShrink);
        var caption = Loc.T(L.Machines.Expanding);
        var captionStyle = MachineText.Fit(caption, inner.Width,
            FitHeight(TextStyles.Title3, inner.Height * IntroCaptionShare));
        var titleHeight = Typography.LineHeight(titleStyle);
        var captionHeight = bird ? Typography.LineHeight(captionStyle) : 0f;
        Typography.DrawCentered(drawList, new Vector2(inner.Center.X, inner.Min.Y + titleHeight * 0.5f), title,
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

        var textWidth = window.Width * BannerTextShare;
        titleStyle = MachineText.Fit(title, textWidth, titleStyle, MachineText.DisplayShrink);
        var titleSize = Typography.Measure(title, titleStyle);
        if (subtitle.Length > 0)
        {
            subtitleStyle = MachineText.Fit(subtitle, textWidth, subtitleStyle, MachineText.DisplayShrink);
        }

        var subtitleHeight = subtitle.Length > 0 ? Typography.LineHeight(subtitleStyle) : 0f;
        var width = MathF.Min(window.Width - pad, MathF.Max(titleSize.X, subtitle.Length > 0
            ? Typography.Measure(subtitle, subtitleStyle).X : 0f) + pad * 2f);
        var height = titleSize.Y + subtitleHeight + pad * 1.5f;
        var center = window.Center;
        var min = center - new Vector2(width, height) * 0.5f;
        var max = center + new Vector2(width, height) * 0.5f;
        Squircle.Fill(drawList, min, max, 16f * scale, ImGui.GetColorU32(Veil));
        Squircle.Stroke(drawList, min, max, 16f * scale, ImGui.GetColorU32(accent with { W = 0.8f }), 2f * scale);
        CasinoLights.BulbChase(drawList, new Rect(min, max), 16f * scale, scale, frame.Phase, CasinoLights.BulbPitch,
            CasinoColors.Money, accent, 0.9f);
        Typography.DrawCentered(drawList, new Vector2(center.X, min.Y + pad * 0.75f + titleSize.Y * 0.5f), title,
            accent, titleStyle);
        if (subtitle.Length > 0)
        {
            Typography.DrawCentered(drawList,
                new Vector2(center.X, min.Y + pad * 0.75f + titleSize.Y + subtitleHeight * 0.5f), subtitle,
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

    private void DrawStrip(ImDrawListPtr drawList, float scale)
    {
        Squircle.Fill(drawList, strip.Min, strip.Max, strip.Height * StripRoundShare, ImGui.GetColorU32(StripFill));
        var footer = MachineFooterLayout.Compute(strip, scale);
        var eyebrowHeight = Typography.LineHeight(EyebrowStyle);
        var valueStyle = FitHeight(TextStyles.Title2, footer.Right.Height - eyebrowHeight);
        if (inlineReason.Length > 0)
        {
            DrawReason(drawList, footer.Left);
        }
        else
        {
            DrawStripLeft(drawList, footer.Left, valueStyle);
        }

        var shown = playback.HasRound ? rollup.Shown : 0;
        var lossLike = playback.HasRound && settled && playback.TotalWin + playback.Jackpot <= playback.Cost;
        var ink = shown <= 0 || lossLike ? StageInks.Strong with { W = MutedValueAlpha } : CasinoColors.Money;
        DrawFooterColumn(drawList, footer.Right, Loc.T(L.Machines.Win), NumberText.Group(shown), true, ink, valueStyle,
            true);
    }

    private void DrawReason(ImDrawListPtr drawList, Rect area)
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
    }

    private void DrawStripLeft(ImDrawListPtr drawList, Rect area, in TextStyle valueStyle)
    {
        if (playback.HasRound && playback.InHold)
        {
            DrawFooterColumn(drawList, area, Loc.T(L.Machines.RespinsLabel),
                GameNumber.Label(Math.Max(0, playback.Current.SpinsLeft)), false, StageInks.Strong, valueStyle, false);
            return;
        }

        if (playback.HasRound && playback.InFeature)
        {
            var played = playback.FeaturePlayed;
            var total = Math.Max(played, played + playback.SpinsLeftShown());
            var games = string.Equals(machineId, SlotsRules.MoogleId, StringComparison.Ordinal);
            DrawFooterColumn(drawList, area, Loc.T(games ? L.Machines.FreeGamesLabel : L.Machines.FreeSpinsLabel),
                featureCounter.Get(L.Machines.CounterValue, played, total), false, StageInks.Strong, valueStyle, false);
            return;
        }

        DrawFooterColumn(drawList, area, Loc.T(L.Machines.BetLabel),
            NumberText.Compact(SlotsRules.CostOf(Mode, Composer.Amount)), true, StageInks.Strong, valueStyle, false);
    }

    private static void DrawFooterColumn(ImDrawListPtr drawList, Rect column, string eyebrow, string value,
        bool chips, Vector4 valueInk, in TextStyle valueStyle, bool alignRight)
    {
        var eyebrowStyle = MachineText.Fit(eyebrow, column.Width, EyebrowStyle);
        var eyebrowHeight = Typography.LineHeight(EyebrowStyle);
        var valueHeight = Typography.LineHeight(valueStyle);
        var top = MachineFooterLayout.BlockTop(column, eyebrowHeight, valueHeight);
        var eyebrowSize = Typography.Measure(eyebrow, eyebrowStyle);
        var eyebrowLeft = alignRight ? column.Max.X - eyebrowSize.X : column.Min.X;
        Typography.Draw(drawList, new Vector2(eyebrowLeft, top + (eyebrowHeight - eyebrowSize.Y) * 0.5f), eyebrow,
            CasinoColors.InkBody, eyebrowStyle);
        var size = chips ? CurrencyGlyph.MeasureAmount(value, valueStyle) : Typography.Measure(value, valueStyle);
        var style = valueStyle;
        if (size.X > column.Width && size.X > 0f)
        {
            style = valueStyle with { Scale = valueStyle.Scale * column.Width / size.X };
            size = chips ? CurrencyGlyph.MeasureAmount(value, style) : Typography.Measure(value, style);
        }

        var origin = new Vector2(alignRight ? column.Max.X - size.X : column.Min.X, top + eyebrowHeight + valueHeight
            - size.Y);
        if (chips)
        {
            CurrencyGlyph.DrawAmount(drawList, origin, value, CurrencyKind.Chips, valueInk, style);
            return;
        }

        Typography.Draw(drawList, origin, value, valueInk, style);
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
        var heading = Loc.T(L.Machines.GambleHeading);
        var headingStyle = MachineText.Fit(heading, inner.Width, TextStyles.Title3);
        var headingHeight = Typography.LineHeight(TextStyles.Title3);
        Typography.DrawCentered(drawList, new Vector2(inner.Center.X, inner.Min.Y + headingHeight * 0.5f), heading,
            StageInks.Strong, headingStyle);
        var amount = gambleStake.Get(L.Machines.GambleAmount, gamble.Amount);
        var amountStyle = MachineText.Fit(amount, inner.Width, TextStyles.Title1, MachineText.DisplayShrink);
        var amountHeight = Typography.LineHeight(TextStyles.Title1);
        var amountCenterY = inner.Min.Y + headingHeight + amountHeight * 0.5f;
        var lost = gamble.Phase == GamblePhase.Closing && !gamble.Won;
        Typography.DrawCentered(drawList, new Vector2(inner.Center.X, amountCenterY), amount,
            lost ? StageInks.Strong with { W = 0.6f } : CasinoColors.Money, amountStyle);
        var ladderTop = amountCenterY + amountHeight * 0.5f + Metrics.Space.Xs * scale;
        var ladderHeight = Typography.LineHeight(TextStyles.Footnote) + 8f * scale;
        DrawGambleLadder(drawList, new Rect(new Vector2(inner.Min.X, ladderTop),
            new Vector2(inner.Max.X, ladderTop + ladderHeight)), scale);
        var buttons = MachineGambleLayout.Compute(inner, Button.LargeHeight * scale, scale);
        var cardTop = ladderTop + ladderHeight + Metrics.Space.Xs * scale;
        var cardArea = new Rect(new Vector2(inner.Min.X, cardTop),
            new Vector2(inner.Max.X, MathF.Max(cardTop, buttons.Red.Min.Y - Metrics.Space.Xs * scale)));
        if (cardArea.Height >= CardMinHeight * scale)
        {
            DrawCard(drawList, cardArea, scale);
        }

        var choosing = gamble.Phase == GamblePhase.Choosing && !play.RoundInFlight;
        if (Button.Draw(drawList, buttons.Red, Loc.T(L.Machines.Red), ui.Ink, ButtonStyle.Prominent, ButtonRole.Destructive,
                enabled: choosing, id: "casino.machines.red") && gamble.Pick(play, SlotsRules.GambleRed))
        {
            CasinoSfx.Play(UiSound.ChipSlide);
        }

        if (Button.Draw(drawList, buttons.Black, Loc.T(L.Machines.Black), ui.Ink, ButtonStyle.Prominent, enabled: choosing,
                id: "casino.machines.black") && gamble.Pick(play, SlotsRules.GambleBlack))
        {
            CasinoSfx.Play(UiSound.ChipSlide);
        }

        if (Button.Draw(drawList, buttons.Collect, Loc.T(L.Machines.Collect), ui.Ink, ButtonStyle.Gray, enabled: choosing,
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
            var text = NumberText.Compact(gamble.RungAmount(rung + 1));
            Typography.DrawCentered(drawList, (min + max) * 0.5f, text,
                done ? new Vector4(0.08f, 0.05f, 0.02f, 1f) : StageInks.Strong,
                MachineText.Fit(text, max.X - min.X - 4f * scale, TextStyles.FootnoteEmphasized));
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
        var text = Loc.T(verdict);
        var style = MachineText.Fit(text, area.Width - NotePad * 2f * scale, TextStyles.Headline);
        var verdictY = MathF.Min(max.Y + Typography.LineHeight(TextStyles.Headline) * 0.6f,
            area.Max.Y - Typography.LineHeight(TextStyles.Headline) * 0.5f);
        var textSize = Typography.Measure(text, style);
        var plate = new Vector2(textSize.X * 0.5f + NotePad * scale, textSize.Y * 0.5f);
        var plateCenter = new Vector2(center.X, verdictY);
        Squircle.Fill(drawList, plateCenter - plate, plateCenter + plate, plate.Y, ImGui.GetColorU32(Veil));
        Typography.DrawCentered(drawList, plateCenter, text, gamble.Won ? CasinoColors.Money : StageInks.Strong,
            style);
    }
}
