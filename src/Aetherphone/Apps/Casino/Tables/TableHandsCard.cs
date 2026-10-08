using Aetherphone.Apps.Coin;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Tables;

internal sealed class TableHandsCard
{
    private const float RowHeight = 62f;
    private const float Pad = 16f;
    private const float VerifyWidth = 96f;

    private readonly CasinoHistoryStore history;
    private readonly CasinoTextCache text = new();

    public TableHandsCard(CasinoHistoryStore history)
    {
        this.history = history;
    }

    public float Draw(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, float scale)
    {
        var hands = history.TableHands;
        var top = origin.Y;
        top += Typography.DrawWrappedLeft(new Vector2(origin.X, top), Loc.T(L.Blackjack.TableHandsHint), ui.BodyInk,
            TextStyles.Footnote, width) + Metrics.Space.Sm * scale;
        if (hands.Length == 0)
        {
            return CoinArt.DrawPanel(ui, new Vector2(origin.X, top), width, Dalamud.Interface.FontAwesomeIcon.Couch,
                AccentRing.Green, Loc.T(L.Blackjack.TableHandsHeading), Loc.T(L.Blackjack.TableHandsEmpty), scale);
        }

        var rowHeight = RowHeight * scale;
        var min = new Vector2(origin.X, top);
        var max = new Vector2(origin.X + width, top + rowHeight * hands.Length);
        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale);
        for (var index = 0; index < hands.Length; index++)
        {
            var rowTop = top + index * rowHeight;
            using (ImRaii.PushId(index))
            {
                DrawRow(drawList, ui, hands[index], new Rect(new Vector2(min.X, rowTop),
                    new Vector2(max.X, rowTop + rowHeight)), index > 0, scale);
            }
        }

        return max.Y;
    }

    private void DrawRow(ImDrawListPtr drawList, AppSkin ui, in CasinoTableHand hand, Rect row, bool hairline,
        float scale)
    {
        var pad = Pad * scale;
        if (hairline)
        {
            CoinArt.Hairline(drawList, ui, row.Min.X + pad, row.Max.X, row.Min.Y);
        }

        var buttonWidth = VerifyWidth * scale;
        var buttonHeight = Button.SmallHeight * scale;
        var button = new Rect(new Vector2(row.Max.X - pad - buttonWidth, row.Center.Y - buttonHeight * 0.5f),
            new Vector2(row.Max.X - pad, row.Center.Y + buttonHeight * 0.5f));
        var textWidth = MathF.Max(1f, button.Min.X - row.Min.X - pad * 2f);
        var titleHeight = Typography.LineHeight(TextStyles.Subheadline);
        var lineTop = row.Center.Y - (titleHeight + Typography.LineHeight(TextStyles.Footnote)) * 0.5f;
        Typography.Draw(drawList, new Vector2(row.Min.X + pad, lineTop),
            Typography.FitText(hand.TableName, textWidth, TextStyles.Subheadline), ui.TitleInk, TextStyles.Subheadline);
        var detail = text.Number(L.Blackjack.TableHandLine, hand.HandIndex);
        Typography.Draw(drawList, new Vector2(row.Min.X + pad, lineTop + titleHeight),
            Typography.FitText(detail, textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        if (history.TryGetVerified(hand.Key, out var verified) && verified.Verdict != CasinoRoundVerdict.Unrevealed)
        {
            var match = verified.Verdict == CasinoRoundVerdict.Match;
            var label = Loc.T(match ? L.Blackjack.VerdictMatch : L.Blackjack.VerdictMismatch);
            var fitted = Typography.FitText(label, buttonWidth, TextStyles.FootnoteEmphasized);
            var size = Typography.Measure(fitted, TextStyles.FootnoteEmphasized);
            Typography.Draw(drawList, new Vector2(button.Max.X - size.X, row.Center.Y - size.Y * 0.5f), fitted,
                match ? AccentRing.Green : AccentRing.Red, TextStyles.FootnoteEmphasized);
            return;
        }

        var pending = history.TryGetVerified(hand.Key, out _);
        var action = Loc.T(pending ? L.Blackjack.VerdictPending : L.Blackjack.VerifyHand);
        if (Button.Draw(drawList, button, action, ui.Ink, ButtonStyle.Tinted, enabled: !history.Verifying,
                id: "casino.tablehand.verify"))
        {
            history.RequestTableVerify(hand);
        }
    }
}
