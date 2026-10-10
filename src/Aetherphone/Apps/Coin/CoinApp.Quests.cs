using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Coin;

internal sealed partial class CoinApp
{
    private const float QuestPad = 12f;
    private const float QuestLineGap = 6f;
    private const float QuestHintGap = 2f;
    private const float QuestBarHeight = 4f;
    private const float QuestCheckSize = 18f;
    private const float QuestFooterGap = 8f;
    private const float QuestReasonShare = 0.7f;
    private const float QuestFloatLift = 6f;

    private CachedText[] questTitles = Array.Empty<CachedText>();
    private CachedText[] questHints = Array.Empty<CachedText>();
    private CachedText[] questProgress = Array.Empty<CachedText>();
    private Spring[] questFill = Array.Empty<Spring>();
    private Vector2[] questAnchors = Array.Empty<Vector2>();
    private CachedText questResetText;
    private Vector2 questCardAnchor;

    private void PrimeQuests()
    {
        for (var index = 0; index < questFill.Length; index++)
        {
            questFill[index].SnapTo(0f);
        }
    }

    private void ConsumeQuestClaim()
    {
        while (quests.TakeClaimResult() is { } claim)
        {
            var award = claim.Award;
            var anchor = QuestAnchor(claim.QuestId);
            if (award is { Granted: true, Amount: > 0 })
            {
                UiFeedback.Play(UiSound.Payout);
                floats.Spawn(Loc.T(L.Coin.CheckInReward, NumberText.Group(award.Amount)), anchor);
                continue;
            }

            if (string.Equals(award.Reason, CoinQuests.FrozenReason, StringComparison.Ordinal))
            {
                confirm.Alert(Loc.T(L.Coin.FrozenAlertTitle), Loc.T(L.Coin.FrozenAlertBody), Loc.T(L.Common.Close));
                continue;
            }

            floats.Spawn(Loc.T(CoinQuests.ReasonFor(award.Reason)), anchor, true);
        }
    }

    private Vector2 QuestAnchor(string questId)
    {
        var board = quests.Board;
        if (board is null)
        {
            return questCardAnchor;
        }

        var items = board.Quests;
        for (var index = 0; index < items.Length && index < questAnchors.Length; index++)
        {
            if (string.Equals(items[index].Id, questId, StringComparison.Ordinal))
            {
                return questAnchors[index];
            }
        }

        return questCardAnchor;
    }

    private float DrawQuests(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        quests.EnsureCurrent();
        var board = quests.Board;
        if (board is null || board.Quests.Length == 0)
        {
            return origin.Y;
        }

        var items = board.Quests;
        EnsureQuestSlots(items.Length);
        var headerTop = origin.Y + CoinArt.SectionGap * scale;
        var cursorY = headerTop + CoinArt.SectionHeader(drawList, new Vector2(origin.X, headerTop), width,
            Loc.T(L.Coin.QuestsTitle), ui.TitleInk, 0f, scale);
        var total = 0f;
        for (var index = 0; index < items.Length; index++)
        {
            total += QuestRowHeight(items[index], index, width, scale);
        }

        var min = new Vector2(origin.X, cursorY);
        var max = new Vector2(origin.X + width, cursorY + total);
        questCardAnchor = new Vector2(origin.X + width * 0.5f, min.Y);
        var footHeight = Typography.LineHeight(TextStyles.Footnote);
        var bottom = max.Y + QuestFooterGap * scale + footHeight;
        if (!ImGui.IsRectVisible(min, new Vector2(max.X, bottom)))
        {
            return bottom;
        }

        CoinArt.Card(drawList, ui, min, max, scale);
        var rowTop = min.Y;
        for (var index = 0; index < items.Length; index++)
        {
            var height = QuestRowHeight(items[index], index, width, scale);
            var row = new Rect(new Vector2(min.X, rowTop), new Vector2(max.X, rowTop + height));
            if (index > 0)
            {
                DrawRowHairline(drawList, row, scale);
            }

            DrawQuestRow(drawList, row, items[index], index, scale);
            rowTop += height;
        }

        DrawQuestFooter(drawList, new Vector2(origin.X + Metrics.Space.Lg * scale, max.Y + QuestFooterGap * scale),
            max.X - Metrics.Space.Lg * scale, board.ResetsAtUnix, scale);
        return bottom;
    }

    private void EnsureQuestSlots(int count)
    {
        if (questTitles.Length >= count)
        {
            return;
        }

        questTitles = new CachedText[count];
        questHints = new CachedText[count];
        questProgress = new CachedText[count];
        questFill = new Spring[count];
        questAnchors = new Vector2[count];
    }

    private float QuestRowHeight(CoinQuestDto quest, int index, float width, float scale)
    {
        var textWidth = QuestTextWidth(quest, width, scale);
        var content = MathF.Max(CoinArt.RowTile * scale, QuestBlockHeight(quest, index, textWidth, scale));
        return MathF.Max(content + QuestPad * scale * 2f, CoinArt.RowHeight * scale);
    }

    private float QuestBlockHeight(CoinQuestDto quest, int index, float textWidth, float scale)
    {
        var titleHeight = Typography.MeasureWrappedBlock(QuestTitle(quest, index), TextStyles.BodyEmphasized,
            textWidth).Y;
        var hint = QuestHint(quest, index);
        var hintHeight = hint.Length == 0
            ? 0f
            : Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, textWidth).Y + QuestHintGap * scale;
        return titleHeight + hintHeight + QuestLineGap * scale + Typography.LineHeight(TextStyles.Footnote);
    }

    private float QuestTextWidth(CoinQuestDto quest, float width, float scale)
    {
        var trailing = QuestTrailingWidth(quest, scale);
        var reserve = trailing > 0f ? trailing + CoinArt.ValueGap * scale : 0f;
        return MathF.Max(1f, width - Metrics.Space.Lg * scale * 2f - (CoinArt.RowTile + CoinArt.TextGap) * scale -
                             reserve);
    }

    private static float QuestTrailingWidth(CoinQuestDto quest, float scale)
    {
        if (quest.Claimed)
        {
            return QuestCheckSize * scale;
        }

        return quest.Amount > 0
            ? CoinArt.PriceWidth(NumberText.Group(quest.Amount), TextStyles.SubheadlineEmphasized)
            : 0f;
    }

    private void DrawQuestRow(ImDrawListPtr drawList, Rect row, CoinQuestDto quest, int index, float scale)
    {
        var pad = Metrics.Space.Lg * scale;
        var appId = quest.App.Length == 0 ? Id : quest.App;
        var openable = !string.Equals(appId, Id, StringComparison.Ordinal) && navigation.IsAvailable(appId);
        var hovered = openable && CoinArt.RowInteraction(drawList, ui, row, scale);

        var tile = CoinArt.RowTile * scale;
        var tileCenter = new Vector2(row.Min.X + pad + tile * 0.5f, row.Center.Y);
        IconTile.DrawApp(drawList, appId, tileCenter, tile, IconTile.Surface(AppAccents.For(appId)));

        var textLeft = tileCenter.X + tile * 0.5f + CoinArt.TextGap * scale;
        var textWidth = QuestTextWidth(quest, row.Width, scale);
        var textRight = textLeft + textWidth;
        var title = QuestTitle(quest, index);
        var titleHeight = Typography.MeasureWrappedBlock(title, TextStyles.BodyEmphasized, textWidth).Y;
        var hint = QuestHint(quest, index);
        var hintHeight = hint.Length == 0 ? 0f : Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, textWidth).Y;
        var lineHeight = Typography.LineHeight(TextStyles.Footnote);
        var blockHeight = QuestBlockHeight(quest, index, textWidth, scale);
        var top = row.Center.Y - blockHeight * 0.5f;
        Typography.DrawWrappedLeft(new Vector2(textLeft, top), title, ui.TitleInk, TextStyles.BodyEmphasized,
            textWidth);

        var cursor = top + titleHeight;
        if (hint.Length > 0)
        {
            cursor += QuestHintGap * scale;
            Typography.DrawWrappedLeft(new Vector2(textLeft, cursor), hint, ui.MutedInk, TextStyles.Footnote,
                textWidth);
            cursor += hintHeight;
        }

        var lineTop = cursor + QuestLineGap * scale;
        var progressStyle = TextStyles.FootnoteEmphasized;
        var progress = QuestLineText(quest, index, textWidth, progressStyle, out var progressInk);
        var progressWidth = WidgetText.TabularWidth(progress, progressStyle);
        WidgetText.Tabular(drawList, new Vector2(textRight - progressWidth, lineTop), progress, progressInk,
            progressStyle);
        var barRight = textRight - progressWidth - CoinArt.ValueGap * scale;
        if (barRight > textLeft)
        {
            var barHeight = QuestBarHeight * scale;
            var barTop = lineTop + (lineHeight - barHeight) * 0.5f;
            var fill = Math.Clamp(Step(ref questFill[index], CoinQuests.Fraction(quest), Motion.Sheet), 0f, 1f);
            CoinArt.Bar(drawList, new Vector2(textLeft, barTop), new Vector2(barRight, barTop + barHeight), fill,
                Palette.WithAlpha(ui.TitleInk, EarnBarTrackAlpha), ui.Accent);
        }

        var trailingWidth = QuestTrailingWidth(quest, scale);
        var trailingRight = row.Max.X - pad;
        var trailing = new Rect(new Vector2(trailingRight - trailingWidth, row.Center.Y - lineHeight * 0.5f),
            new Vector2(trailingRight, row.Center.Y + lineHeight * 0.5f));
        questAnchors[index] = new Vector2(trailing.Center.X, trailing.Min.Y - QuestFloatLift * scale);
        DrawQuestTrailing(drawList, quest, trailing);
        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            UiFeedback.Play(UiSound.Tap);
            navigation.Open(appId);
        }
    }

    private string QuestLineText(CoinQuestDto quest, int index, float textWidth, TextStyle style, out Vector4 ink)
    {
        if (quest.Claimed)
        {
            ink = ui.Accent;
            return Loc.T(L.Coin.QuestPaid);
        }

        var reason = quests.ReasonFor(quest.Id);
        if (reason.Length > 0)
        {
            ink = ui.MutedInk;
            return Typography.FitText(Loc.T(CoinQuests.ReasonFor(reason)), textWidth * QuestReasonShare, style);
        }

        ink = CoinQuests.IsComplete(quest) ? ui.Accent : ui.MutedInk;
        return QuestProgressText(quest, index);
    }

    private void DrawQuestTrailing(ImDrawListPtr drawList, CoinQuestDto quest, Rect trailing)
    {
        if (quest.Claimed)
        {
            ProgressRing.CenterIcon(drawList, trailing.Center, FontAwesomeIcon.CheckCircle, ui.Accent,
                trailing.Width);
            return;
        }

        if (quest.Amount <= 0)
        {
            return;
        }

        var style = TextStyles.SubheadlineEmphasized;
        var lineHeight = Typography.LineHeight(style);
        var amount = NumberText.Group(quest.Amount);
        CoinArt.Price(drawList, new Vector2(trailing.Max.X - CoinArt.PriceWidth(amount, style),
            trailing.Center.Y - lineHeight * 0.5f), amount, ui.MutedInk, style);
    }

    private void DrawQuestFooter(ImDrawListPtr drawList, Vector2 topLeft, float right, long resetsAtUnix,
        float scale)
    {
        if (resetsAtUnix <= 0)
        {
            return;
        }

        var lineHeight = Typography.LineHeight(TextStyles.Footnote);
        var glyph = ResetGlyph * scale;
        ProgressRing.CenterIcon(drawList, new Vector2(topLeft.X + glyph * 0.5f, topLeft.Y + lineHeight * 0.5f),
            FontAwesomeIcon.HourglassHalf, ui.MutedInk, glyph);
        var textLeft = topLeft.X + glyph + Metrics.Space.Xs * scale;
        var text = questResetText.IsCurrent(resetsAtUnix)
            ? questResetText.Value
            : questResetText.Store(resetsAtUnix, Loc.T(L.Coin.QuestsReset, TimeText.Clock(resetsAtUnix)));
        Typography.Draw(drawList, new Vector2(textLeft, topLeft.Y),
            Typography.FitText(text, MathF.Max(1f, right - textLeft), TextStyles.Footnote), ui.MutedInk,
            TextStyles.Footnote);
    }

    private string QuestTitle(CoinQuestDto quest, int index)
    {
        ref var cache = ref questTitles[index];
        var key = ((long)quest.Id.GetHashCode() << 32) | (uint)quest.Target;
        return cache.IsCurrent(key) ? cache.Value : cache.Store(key, CoinQuests.Title(quest));
    }

    private string QuestHint(CoinQuestDto quest, int index)
    {
        ref var cache = ref questHints[index];
        var key = ((long)quest.Id.GetHashCode() << 32) | (uint)Loc.Current.GetHashCode();
        return cache.IsCurrent(key) ? cache.Value : cache.Store(key, CoinQuests.Hint(quest));
    }

    private string QuestProgressText(CoinQuestDto quest, int index)
    {
        ref var cache = ref questProgress[index];
        var key = CoinQuests.TextKey(quest) ^ ((long)quest.Id.GetHashCode() << 16);
        return cache.IsCurrent(key) ? cache.Value : cache.Store(key, CoinQuests.ProgressText(quest));
    }
}
