using Aetherphone.Apps.Coin;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp
{
    private const float HistoryRowHeight = 62f;
    private const float HistoryTile = 36f;
    private const float FactRowHeight = 46f;
    private const float ReferencePad = 16f;
    private const float ReferenceGap = 10f;
    private const float StepBadge = 28f;
    private const float StepPad = 16f;
    private const float ActionHeight = Button.LargeHeight;
    private const int FairnessRecentRoundLimit = 8;

    private readonly struct HistoryRowText
    {
        public readonly string GameId;
        public readonly string Clock;
        public readonly string Outcome;
        public readonly string Stake;
        public readonly sbyte Sign;
        public readonly bool Settled;

        public HistoryRowText(string gameId, string clock, string outcome, string stake, sbyte sign, bool settled)
        {
            GameId = gameId;
            Clock = clock;
            Outcome = outcome;
            Stake = stake;
            Sign = sign;
            Settled = settled;
        }
    }

    private static readonly Func<long, long, bool> SameDay = TimeText.SameLocalDay;

    private readonly List<HistoryRowText> historyRows = new();
    private readonly List<HistoryDay> historyDays = new();
    private readonly List<string> historyDayLabels = new();
    private readonly List<string> historyDayNets = new();
    private CasinoRoundHistoryDto[]? historySource;
    private bool historySourceHasMore;
    private LanguageInfo? historyLanguage;
    private int historyTimeFormat = -1;
    private DateTime historyDay;
    private string detailRoundId = string.Empty;
    private string detailPlayed = string.Empty;
    private string detailSettled = string.Empty;
    private CasinoRoundHistoryDto? detailSource;
    private LanguageInfo? detailLanguage;
    private int detailTimeFormat = -1;

    private void SyncHistoryText()
    {
        var rounds = history.Rounds;
        var today = DateTime.Now.Date;
        if (ReferenceEquals(rounds, historySource) && historySourceHasMore == history.HasMore
            && ReferenceEquals(historyLanguage, Loc.Current) && historyTimeFormat == TimeText.FormatVersion
            && historyDay == today)
        {
            return;
        }

        historySource = rounds;
        historySourceHasMore = history.HasMore;
        historyLanguage = Loc.Current;
        historyTimeFormat = TimeText.FormatVersion;
        historyDay = today;
        historyRows.Clear();
        for (var index = 0; index < rounds.Length; index++)
        {
            historyRows.Add(RowTextOf(rounds[index]));
        }

        CasinoHistoryDays.Group(rounds, history.HasMore, SameDay, historyDays);
        historyDayLabels.Clear();
        historyDayNets.Clear();
        for (var dayIndex = 0; dayIndex < historyDays.Count; dayIndex++)
        {
            var day = historyDays[dayIndex];
            historyDayLabels.Add(TimeText.DayLabel(rounds[day.Start].CreatedAtUnix));
            historyDayNets.Add(day.Complete && day.Settled ? CasinoTextCache.SignedText(day.Net) : string.Empty);
        }
    }

    private static HistoryRowText RowTextOf(CasinoRoundHistoryDto round)
    {
        var gameId = ClientGameId(round.GameKind);
        var clock = TimeText.Clock(round.CreatedAtUnix);
        var stake = Loc.T(L.Casino.HistoryStakeLine, NumberText.Group(round.Stake));
        if (round.State == CasinoRoundStates.Open)
        {
            return new HistoryRowText(gameId, clock, Loc.T(L.Casino.StateOpen), stake, 0, false);
        }

        if (round.State == CasinoRoundStates.Voided)
        {
            return new HistoryRowText(gameId, clock, Loc.T(L.Casino.StateVoided), stake, 0, false);
        }

        var net = round.Payout - round.Stake;
        return new HistoryRowText(gameId, clock, CasinoTextCache.SignedText(net), stake, (sbyte)Math.Sign(net), true);
    }

    private void DrawHistory(Rect body)
    {
        var scale = UiScale.Current;
        using (ImRaii.PushId("casino.history"))
        using (AppSurface.Begin(body))
        {
            history.EnsureFresh();
            if (history.TakeLoadFailure())
            {
                historyLoadFailed = true;
            }

            var drawList = ImGui.GetWindowDrawList();
            var rounds = history.Rounds;
            if (rounds.Length == 0)
            {
                DrawHistoryEmpty(drawList, body, scale);
                return;
            }

            SyncHistoryText();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = origin.Y;
            var rowHeight = HistoryRowHeight * scale;
            for (var dayIndex = 0; dayIndex < historyDays.Count; dayIndex++)
            {
                var day = historyDays[dayIndex];
                var headerTop = dayIndex == 0 ? cursorY : cursorY + CoinArt.SectionGap * scale;
                cursorY = headerTop + DrawDayHeader(drawList, new Vector2(origin.X, headerTop), width, dayIndex,
                    scale) + CoinArt.HeaderGap * scale;
                var min = new Vector2(origin.X, cursorY);
                var max = new Vector2(origin.X + width, cursorY + rowHeight * (day.End - day.Start));
                ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale);
                for (var entryIndex = day.Start; entryIndex < day.End; entryIndex++)
                {
                    using (ImRaii.PushId(entryIndex))
                    {
                        DrawHistoryRow(drawList, RowAt(min, max.X, rowHeight, entryIndex - day.Start), entryIndex,
                            entryIndex > day.Start, scale);
                    }
                }

                cursorY = max.Y;
            }

            CoinArt.Reserve(origin, width, cursorY + Metrics.Space.Lg * scale);
            if (history.HasMore && !history.Loading && InfiniteScroll.ReachedBottom())
            {
                history.LoadMore();
            }

            if (history.Loading)
            {
                InfiniteScroll.DrawLoadingRow(body.Center.X, ui.MutedInk);
            }

            ImGui.Dummy(new Vector2(0f, CoinArt.BottomPad * scale));
        }
    }

    private void DrawHistoryEmpty(ImDrawListPtr drawList, Rect body, float scale)
    {
        if (history.Loading || (!historyLoadFailed && !history.Loaded))
        {
            LoadingPulse.Draw(body.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
            return;
        }

        if (historyLoadFailed)
        {
            if (CoinArt.StateScreen(drawList, ui, body, FontAwesomeIcon.CloudShowersHeavy,
                    Loc.T(L.Casino.HistoryEmptyTitle), Loc.T(CasinoReasons.MessageFor(CasinoReasons.Unreachable)),
                    Loc.T(L.Common.Retry), ImGui.GetID("retry"), scale))
            {
                historyLoadFailed = false;
                history.Invalidate();
            }

            return;
        }

        CoinArt.StateScreen(drawList, ui, body, FontAwesomeIcon.Receipt, Loc.T(L.Casino.HistoryEmptyTitle),
            Loc.T(L.Casino.HistoryEmptyHint), string.Empty, 0, scale);
    }

    private float DrawDayHeader(ImDrawListPtr drawList, Vector2 origin, float width, int dayIndex, float scale)
    {
        var net = historyDayNets[dayIndex];
        var reserve = 0f;
        var height = CoinArt.SectionHeaderHeight * scale;
        if (net.Length > 0)
        {
            var netSize = CurrencyGlyph.MeasureAmount(net, TextStyles.Headline);
            reserve = netSize.X + CoinArt.ValueGap * scale;
            var sign = historyDays[dayIndex].Net;
            var ink = sign > 0 ? CoinArt.GainInk : sign < 0 ? ui.BodyInk : ui.MutedInk;
            CurrencyGlyph.DrawAmount(drawList, new Vector2(origin.X + width - netSize.X,
                origin.Y + (height - netSize.Y) * 0.5f), net, CurrencyKind.Chips, ink, TextStyles.Headline);
        }

        return CardSectionHeader.Draw(drawList, origin, width, historyDayLabels[dayIndex], ui.TitleInk, reserve);
    }

    private void DrawHistoryRow(ImDrawListPtr drawList, Rect row, int roundIndex, bool hairline, float scale)
    {
        var text = historyRows[roundIndex];
        var pad = Metrics.Space.Lg * scale;
        var tile = HistoryTile * scale;
        if (hairline)
        {
            CoinArt.Hairline(drawList, ui, row.Min.X + pad + tile + CoinArt.TextGap * scale, row.Max.X, row.Min.Y);
        }

        var hovered = CoinArt.RowInteraction(drawList, ui, row, scale);
        var tileCenter = new Vector2(row.Min.X + pad + tile * 0.5f, row.Center.Y);
        CasinoArt.GameTile(drawList, text.GameId, tileCenter, tile);

        var outcomeStyle = text.Settled ? TextStyles.Headline : TextStyles.Subheadline;
        var outcomeInk = text.Sign > 0 ? CoinArt.GainInk : text.Settled ? ui.TitleInk : ui.MutedInk;
        var outcomeSize = Typography.Measure(text.Outcome, outcomeStyle);
        var stakeSize = Typography.Measure(text.Stake, TextStyles.Footnote);
        var right = row.Max.X - pad;
        var blockHeight = outcomeSize.Y + stakeSize.Y;
        var blockTop = row.Center.Y - blockHeight * 0.5f;
        Typography.Draw(drawList, new Vector2(right - outcomeSize.X, blockTop), text.Outcome, outcomeInk,
            outcomeStyle);
        Typography.Draw(drawList, new Vector2(right - stakeSize.X, blockTop + outcomeSize.Y), text.Stake, ui.MutedInk,
            TextStyles.Footnote);

        var textLeft = tileCenter.X + tile * 0.5f + CoinArt.TextGap * scale;
        var textRight = right - MathF.Max(outcomeSize.X, stakeSize.X) - CoinArt.ValueGap * scale;
        CoinArt.Labels(drawList, textLeft, textRight, row.Center.Y, Loc.T(GameName(text.GameId)), text.Clock,
            ui.TitleInk, ui.MutedInk, scale);
        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            router.Push(new CasinoRoute(CasinoScreen.RoundDetail, text.GameId, history.Rounds[roundIndex].RoundId));
        }
    }

    private void DrawFairness(Rect body)
    {
        var scale = UiScale.Current;
        using (ImRaii.PushId("casino.fairness"))
        using (AppSurface.Begin(body))
        {
            history.EnsureFresh();
            SyncHistoryText();
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = origin.Y;
            cursorY += Typography.DrawWrappedLeft(new Vector2(origin.X, cursorY), Loc.T(L.Casino.FairnessIntro),
                ui.BodyInk, TextStyles.Subheadline, width) + CardGap * scale;
            var stepTop = cursorY;
            var stepsMax = DrawFairnessSteps(drawList, new Vector2(origin.X, stepTop), width, scale);
            cursorY = stepsMax + CardGap * scale;
            cursorY += Typography.DrawWrappedLeft(new Vector2(origin.X + Metrics.Space.Lg * scale, cursorY),
                Loc.T(L.Casino.FairnessChainNote), ui.BodyInk, TextStyles.Footnote,
                width - Metrics.Space.Lg * 2f * scale);

            var listTop = SectionTitle(drawList, new Vector2(origin.X, cursorY), width,
                Loc.T(L.Casino.FairnessRecentHeading), scale);
            cursorY = DrawFairnessRounds(drawList, new Vector2(origin.X, listTop), width, scale);
            var handsTop = SectionTitle(drawList, new Vector2(origin.X, cursorY), width,
                Loc.T(L.Blackjack.TableHandsHeading), scale);
            cursorY = tableHands.Draw(drawList, ui, new Vector2(origin.X, handsTop), width, scale);
            CoinArt.Reserve(origin, width, cursorY + CoinArt.BottomPad * scale);
        }
    }

    private float DrawFairnessSteps(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var pad = StepPad * scale;
        var badge = StepBadge * scale;
        var textLeft = origin.X + pad + badge + CoinArt.TextGap * scale;
        var textWidth = MathF.Max(1f, origin.X + width - pad - textLeft);
        var lockHeight = StepHeight(L.Casino.FairnessLockTitle, L.Casino.FairnessLockBody, textWidth, badge);
        var revealHeight = StepHeight(L.Casino.FairnessRevealTitle, L.Casino.FairnessRevealBody, textWidth, badge);
        var replayHeight = StepHeight(L.Casino.FairnessReplayTitle, L.Casino.FairnessReplayBody, textWidth, badge);
        var total = pad * 4f + lockHeight + revealHeight + replayHeight;
        var max = new Vector2(origin.X + width, origin.Y + total);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var top = origin.Y + pad;
        top = DrawFairnessStep(drawList, origin.X + pad, top, textLeft, textWidth, 1, L.Casino.FairnessLockTitle,
            L.Casino.FairnessLockBody, lockHeight, scale) + pad;
        top = DrawFairnessStep(drawList, origin.X + pad, top, textLeft, textWidth, 2, L.Casino.FairnessRevealTitle,
            L.Casino.FairnessRevealBody, revealHeight, scale) + pad;
        DrawFairnessStep(drawList, origin.X + pad, top, textLeft, textWidth, 3, L.Casino.FairnessReplayTitle,
            L.Casino.FairnessReplayBody, replayHeight, scale);
        return max.Y;
    }

    private static float StepHeight(LocString title, LocString body, float textWidth, float badge)
    {
        var titleHeight = Typography.MeasureWrappedBlock(Loc.T(title), TextStyles.Headline, textWidth).Y;
        var bodyHeight = Typography.MeasureWrappedBlock(Loc.T(body), TextStyles.Subheadline, textWidth).Y;
        return MathF.Max(badge, titleHeight + bodyHeight);
    }

    private float DrawFairnessStep(ImDrawListPtr drawList, float badgeLeft, float top, float textLeft,
        float textWidth, int number, LocString title, LocString body, float height, float scale)
    {
        var badge = StepBadge * scale;
        var badgeCenter = new Vector2(badgeLeft + badge * 0.5f, top + badge * 0.5f);
        drawList.AddCircleFilled(badgeCenter, badge * 0.5f, ImGui.GetColorU32(ui.Accent), 28);
        Typography.DrawCentered(drawList, badgeCenter, Games.Framework.GameNumber.Label(number), CasinoArt.White,
            TextStyles.SubheadlineEmphasized);
        var titleHeight = Typography.DrawWrappedLeft(new Vector2(textLeft, top), Loc.T(title), ui.TitleInk,
            TextStyles.Headline, textWidth);
        Typography.DrawWrappedLeft(new Vector2(textLeft, top + titleHeight), Loc.T(body), ui.MutedInk,
            TextStyles.Subheadline, textWidth);
        return top + height;
    }

    private float DrawFairnessRounds(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var rounds = history.Rounds;
        var count = 0;
        for (var index = 0; index < rounds.Length && count < FairnessRecentRoundLimit; index++)
        {
            if (rounds[index].State != CasinoRoundStates.Open)
            {
                count++;
            }
        }

        if (count == 0)
        {
            return CoinArt.DrawPanel(ui, origin, width, FontAwesomeIcon.ShieldAlt, AccentRing.Green,
                Loc.T(L.Casino.FairnessRecentHeading), Loc.T(L.Casino.FairnessNoRounds), scale);
        }

        var rowHeight = HistoryRowHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + rowHeight * count);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var drawn = 0;
        for (var index = 0; index < rounds.Length && drawn < count; index++)
        {
            if (rounds[index].State == CasinoRoundStates.Open)
            {
                continue;
            }

            using (ImRaii.PushId(index))
            {
                DrawHistoryRow(drawList, RowAt(origin, max.X, rowHeight, drawn), index, drawn > 0, scale);
            }

            drawn++;
        }

        return max.Y;
    }

    private void DrawRoundDetail(Rect body, string roundId)
    {
        var scale = UiScale.Current;
        using (ImRaii.PushId("casino.round"))
        using (AppSurface.Begin(body))
        {
            if (history.TakeVerifyFailure())
            {
                UiFeedback.Play(UiSound.Blocked);
                confirm.Alert(null, Loc.T(CasinoReasons.MessageFor(CasinoReasons.Unreachable)), Loc.T(L.Common.Close));
            }

            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var round = FindHistoryRound(roundId);
            SyncDetailText(roundId, round);
            var hasVerified = history.TryGetVerified(roundId, out var verifiedRound);
            var cursorY = origin.Y;
            if (hasVerified)
            {
                cursorY = DrawVerdict(new Vector2(origin.X, cursorY), width, verifiedRound.Verdict, scale) +
                          CardGap * scale;
            }

            if (round is not null)
            {
                cursorY = DrawRoundFacts(drawList, new Vector2(origin.X, cursorY), width, round, scale) +
                          CardGap * scale;
            }

            cursorY = DrawRoundReference(drawList, new Vector2(origin.X, cursorY), width, roundId, round,
                hasVerified ? verifiedRound : null, scale) + CoinArt.SectionGap * scale;

            var actionHeight = ActionHeight * scale;
            var verifyRect = new Rect(new Vector2(origin.X, cursorY), new Vector2(origin.X + width, cursorY + actionHeight));
            var canVerify = !history.Verifying
                && (!hasVerified || verifiedRound.Verdict == CasinoRoundVerdict.Unrevealed);
            if (Button.Draw(drawList, verifyRect, Loc.T(L.Casino.VerifyAction), ui.Ink, ButtonStyle.Prominent,
                    enabled: canVerify, id: "casino.history.verify"))
            {
                history.RequestVerify(roundId);
            }

            cursorY = verifyRect.Max.Y;
            if (hasVerified)
            {
                cursorY += CardGap * scale;
                var copyRect = new Rect(new Vector2(origin.X, cursorY),
                    new Vector2(origin.X + width, cursorY + actionHeight));
                if (Button.Draw(drawList, copyRect, Loc.T(L.Casino.CopyDetails), ui.Ink, ButtonStyle.Tinted,
                        id: "casino.history.copy"))
                {
                    ImGui.SetClipboardText(BuildRoundDetailsBlob(verifiedRound));
                    ShellToast.Show();
                }

                cursorY = copyRect.Max.Y;
            }

            CoinArt.Reserve(origin, width, cursorY + CoinArt.BottomPad * scale);
        }
    }

    private void SyncDetailText(string roundId, CasinoRoundHistoryDto? round)
    {
        if (string.Equals(roundId, detailRoundId, StringComparison.Ordinal) && ReferenceEquals(round, detailSource)
            && ReferenceEquals(detailLanguage, Loc.Current) && detailTimeFormat == TimeText.FormatVersion)
        {
            return;
        }

        detailRoundId = roundId;
        detailSource = round;
        detailLanguage = Loc.Current;
        detailTimeFormat = TimeText.FormatVersion;
        detailPlayed = round is not null && round.CreatedAtUnix > 0
            ? TimeText.DayLabel(round.CreatedAtUnix) + " " + TimeText.Clock(round.CreatedAtUnix)
            : string.Empty;
        detailSettled = round?.SettledAtUnix is long settledAtUnix
            ? TimeText.DayLabel(settledAtUnix) + " " + TimeText.Clock(settledAtUnix)
            : string.Empty;
    }

    private CasinoRoundHistoryDto? FindHistoryRound(string roundId)
    {
        var rounds = history.Rounds;
        for (var roundIndex = 0; roundIndex < rounds.Length; roundIndex++)
        {
            if (string.Equals(rounds[roundIndex].RoundId, roundId, StringComparison.Ordinal))
            {
                return rounds[roundIndex];
            }
        }

        return null;
    }

    private float DrawVerdict(Vector2 origin, float width, CasinoRoundVerdict verdict, float scale)
    {
        var title = verdict switch
        {
            CasinoRoundVerdict.Match => Loc.T(L.Casino.VerdictMatchTitle),
            CasinoRoundVerdict.Mismatch => Loc.T(L.Casino.VerdictMismatchTitle),
            _ => Loc.T(L.Casino.VerdictUnrevealedTitle),
        };
        var hint = verdict switch
        {
            CasinoRoundVerdict.Match => Loc.T(L.Casino.VerdictMatchHint),
            CasinoRoundVerdict.Mismatch => Loc.T(L.Casino.VerdictMismatchHint),
            _ => Loc.T(L.Casino.VerdictUnrevealedHint),
        };
        var icon = verdict switch
        {
            CasinoRoundVerdict.Match => FontAwesomeIcon.CheckCircle,
            CasinoRoundVerdict.Mismatch => FontAwesomeIcon.ExclamationTriangle,
            _ => FontAwesomeIcon.Hourglass,
        };
        var tint = verdict switch
        {
            CasinoRoundVerdict.Match => AccentRing.Green,
            CasinoRoundVerdict.Mismatch => AccentRing.Red,
            _ => AccentRing.Slate,
        };
        return CoinArt.DrawPanel(ui, origin, width, icon, tint, title, hint, scale);
    }

    private float DrawRoundFacts(ImDrawListPtr drawList, Vector2 origin, float width, CasinoRoundHistoryDto round,
        float scale)
    {
        var settled = detailSettled.Length > 0;
        var rows = settled ? 5 : 4;
        var rowHeight = FactRowHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + rowHeight * rows);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        DrawFactRow(drawList, RowAt(origin, max.X, rowHeight, 0), Loc.T(L.Casino.RoundGame),
            Loc.T(GameName(ClientGameId(round.GameKind))), ui.TitleInk, false, CurrencyKind.Chips, false, scale);
        DrawFactRow(drawList, RowAt(origin, max.X, rowHeight, 1), Loc.T(L.Casino.RoundState), StateText(round.State),
            ui.TitleInk, true, CurrencyKind.Chips, false, scale);
        DrawFactRow(drawList, RowAt(origin, max.X, rowHeight, 2), Loc.T(L.Casino.RoundStake),
            NumberText.Group(round.Stake), ui.TitleInk, true, CurrencyKind.Chips, true, scale);
        var payoutInk = round.State == CasinoRoundStates.Settled && round.Payout > round.Stake
            ? CoinArt.GainInk
            : ui.TitleInk;
        DrawFactRow(drawList, RowAt(origin, max.X, rowHeight, 3), Loc.T(L.Casino.RoundPayout),
            NumberText.Group(round.Payout), payoutInk, true, CurrencyKind.Chips, true, scale);
        if (settled)
        {
            DrawFactRow(drawList, RowAt(origin, max.X, rowHeight, 4), Loc.T(L.Casino.RoundSettledAt), detailSettled,
                ui.TitleInk, true, CurrencyKind.Chips, false, scale);
        }

        return max.Y;
    }

    private void DrawFactRow(ImDrawListPtr drawList, Rect row, string label, string value, Vector4 valueInk,
        bool hairline, CurrencyKind kind, bool withGlyph, float scale)
    {
        var pad = Metrics.Space.Lg * scale;
        if (hairline)
        {
            CoinArt.Hairline(drawList, ui, row.Min.X + pad, row.Max.X, row.Min.Y);
        }

        var lineHeight = Typography.LineHeight(TextStyles.Body);
        var labelWidth = Typography.Measure(label, TextStyles.Body).X;
        Typography.Draw(drawList, new Vector2(row.Min.X + pad, row.Center.Y - lineHeight * 0.5f), label, ui.BodyInk,
            TextStyles.Body);
        var available = MathF.Max(1f, row.Width - pad * 2f - labelWidth - CoinArt.ValueGap * scale);
        if (withGlyph)
        {
            var fitted = Typography.FitText(value, MathF.Max(1f, available - CurrencyGlyph.Reserve(lineHeight)),
                TextStyles.BodyEmphasized);
            var size = CurrencyGlyph.MeasureAmount(fitted, TextStyles.BodyEmphasized);
            CurrencyGlyph.DrawAmount(drawList, new Vector2(row.Max.X - pad - size.X, row.Center.Y - size.Y * 0.5f),
                fitted, kind, valueInk, TextStyles.BodyEmphasized);
            return;
        }

        var text = Typography.FitText(value, available, TextStyles.BodyEmphasized);
        var textWidth = Typography.Measure(text, TextStyles.BodyEmphasized).X;
        Typography.Draw(drawList, new Vector2(row.Max.X - pad - textWidth, row.Center.Y - lineHeight * 0.5f), text,
            valueInk, TextStyles.BodyEmphasized);
    }

    private string StateText(int state) => state switch
    {
        CasinoRoundStates.Open => Loc.T(L.Casino.StateOpen),
        CasinoRoundStates.Voided => Loc.T(L.Casino.StateVoided),
        _ => Loc.T(L.Casino.StateSettled),
    };

    private float DrawRoundReference(ImDrawListPtr drawList, Vector2 origin, float width, string roundId,
        CasinoRoundHistoryDto? round, VerifiedCasinoRound? verifiedRound, float scale)
    {
        var commit = verifiedRound?.Round.SeedCommitHash ?? round?.SeedCommitHash ?? string.Empty;
        var pad = ReferencePad * scale;
        var gap = ReferenceGap * scale;
        var inner = width - pad * 2f;
        var caption = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var height = pad * 2f + caption + Typography.MeasureWrappedBlock(roundId, TextStyles.Footnote, inner).Y;
        if (commit.Length > 0)
        {
            height += gap + caption + Typography.MeasureWrappedBlock(commit, TextStyles.Footnote, inner).Y;
        }

        if (detailPlayed.Length > 0)
        {
            height += gap + caption + Typography.LineHeight(TextStyles.Footnote);
        }

        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var left = origin.X + pad;
        var top = origin.Y + pad;
        top = DrawReferenceField(drawList, left, top, inner, Loc.T(L.Casino.RoundIdLabel), roundId);
        if (commit.Length > 0)
        {
            top = DrawReferenceField(drawList, left, top + gap, inner, Loc.T(L.Casino.RoundCommit), commit);
        }

        if (detailPlayed.Length > 0)
        {
            DrawReferenceField(drawList, left, top + gap, inner, Loc.T(L.Casino.RoundPlayed), detailPlayed);
        }

        return max.Y;
    }

    private float DrawReferenceField(ImDrawListPtr drawList, float left, float top, float width, string label,
        string value)
    {
        Typography.Draw(drawList, new Vector2(left, top), Loc.Upper(label), ui.BodyInk, TextStyles.FootnoteEmphasized);
        top += Typography.LineHeight(TextStyles.FootnoteEmphasized);
        return top + Typography.DrawWrappedLeft(new Vector2(left, top), value, ui.BodyInk, TextStyles.Footnote, width);
    }

    internal static string BuildRoundDetailsBlob(VerifiedCasinoRound verifiedRound)
    {
        var round = verifiedRound.Round;
        var verdict = verifiedRound.Verdict switch
        {
            CasinoRoundVerdict.Match => "match",
            CasinoRoundVerdict.Mismatch => "mismatch",
            _ => "unrevealed",
        };
        var builder = new System.Text.StringBuilder(256 + round.DrawLog.Length);
        builder.Append("roundId=").Append(round.RoundId).Append('\n');
        builder.Append("gameKind=").Append(round.GameKind).Append('\n');
        builder.Append("state=").Append(round.State).Append('\n');
        builder.Append("stake=").Append(round.Stake).Append('\n');
        builder.Append("payout=").Append(round.Payout).Append('\n');
        builder.Append("seedCommitHash=").Append(round.SeedCommitHash).Append('\n');
        builder.Append("seedRevealed=").Append(round.SeedRevealed).Append('\n');
        builder.Append("nextSeedHash=").Append(round.NextSeedHash).Append('\n');
        builder.Append("streamBinding=").Append(round.StreamBinding).Append('\n');
        builder.Append("drawLog=").Append(round.DrawLog).Append('\n');
        builder.Append("verdict=").Append(verdict);
        return builder.ToString();
    }
}
