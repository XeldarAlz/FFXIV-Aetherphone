using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Games;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Race;

internal sealed class RaceFieldList
{
    private const float Pad = 10f;
    private const float SilkSize = 40f;
    private const float SilkFrame = 2f;
    private const float StarSize = 12f;
    private const float FormGap = 8f;
    private const float OddsShare = 0.36f;
    private const float SlideShare = 0.25f;
    private const float PickedStroke = 1.8f;

    private static readonly Vector4 CardFill = new(0.03f, 0.04f, 0.08f, 0.66f);
    private static readonly Vector4 CardHover = new(0.06f, 0.07f, 0.12f, 0.8f);
    private static readonly Vector4 StarEmpty = new(0.56f, 0.54f, 0.61f, 0.45f);

    private float entrance;
    private long entranceRound = -1;

    public void Reset()
    {
        entrance = 0f;
        entranceRound = -1;
    }

    public int Draw(AppSkin ui, Rect rect, CasinoRaceRunnerDto[] runners, RaceTicketBuilder builder, RaceTexts texts,
        ReadOnlySpan<long> stakes, bool selectable, long roundIndex, float deltaSeconds, float scale)
    {
        if (rect.Width <= 1f || rect.Height <= 1f)
        {
            return -1;
        }

        if (roundIndex != entranceRound)
        {
            entranceRound = roundIndex;
            entrance = 0f;
        }

        entrance = GameJuice.Advance(entrance, deltaSeconds);
        var tapped = -1;
        ImGui.SetCursorScreenPos(rect.Min);
        using (ScrollLayout.PushScrollbarInk(CasinoColors.InkBody))
        using (ImRaii.Child("##raceField", rect.Size, false, ImGuiWindowFlags.NoBackground))
        {
            var drawList = ImGui.GetWindowDrawList();
            var width = ScrollLayout.NativeScrollContentWidth();
            var cardHeight = RaceOpenLayout.CardHeight * scale;
            var gap = RaceOpenLayout.CardGap * scale;
            for (var slot = 0; slot < RaceRules.FieldSize && slot < runners.Length; slot++)
            {
                var origin = ImGui.GetCursorScreenPos();
                var share = GameJuice.Stagger(entrance, slot, RaceRules.FieldSize);
                var slide = (1f - GameJuice.PopIn(share)) * width * SlideShare;
                var card = new Rect(new Vector2(origin.X + slide, origin.Y),
                    new Vector2(origin.X + width + slide, origin.Y + cardHeight));
                var stake = slot < stakes.Length ? stakes[slot] : 0;
                if (DrawCard(drawList, ui, card, runners, slot, builder.PickOf(slot), builder.Pair, stake, texts,
                        selectable && share >= 1f, MathF.Min(1f, share * 1.5f), scale))
                {
                    tapped = slot;
                }

                ImGui.Dummy(new Vector2(width, slot < RaceRules.FieldSize - 1 ? cardHeight + gap : cardHeight));
            }
        }

        return tapped;
    }

    public static int Stars(int rating) => Math.Clamp((rating + 1) / 2, 1, GameStatsStore.MaxStars);

    private static bool DrawCard(ImDrawListPtr drawList, AppSkin ui, Rect card, CasinoRaceRunnerDto[] runners,
        int slot, int pick, bool pair, long stake, RaceTexts texts, bool selectable, float alpha, float scale)
    {
        var runner = runners[slot];
        var radius = Metrics.Radius.Card * scale;
        var hovered = selectable && UiInteract.Hover(card.Min, card.Max);
        var picked = pick >= 0;
        var fill = picked ? Palette.Mix(CardFill, ui.Palette.Accent, 0.26f) : hovered ? CardHover : CardFill;
        Squircle.Fill(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(fill with { W = fill.W * alpha }));
        if (picked)
        {
            Squircle.Stroke(drawList, card.Min, card.Max, radius,
                ImGui.GetColorU32(ui.Palette.Accent with { W = alpha }), PickedStroke * scale);
        }
        else if (stake > 0)
        {
            Squircle.Stroke(drawList, card.Min, card.Max, radius,
                ImGui.GetColorU32(CasinoColors.Money with { W = 0.45f * alpha }), MathF.Max(1f, scale));
        }

        var pad = Pad * scale;
        var silk = MathF.Min(SilkSize * scale, card.Height - pad * 2f);
        var silkMin = new Vector2(card.Min.X + pad, card.Center.Y - silk * 0.5f);
        DrawSilkBadge(drawList, silkMin, silk, runner, slot, alpha, scale);
        if (picked && pair)
        {
            DrawPickBadge(drawList, ui, new Vector2(silkMin.X + silk, silkMin.Y), texts.Place(pick), alpha, scale);
        }

        var oddsText = CasinoMultiples.Label(runner.OddsHundredths);
        var oddsStyle = TextStyles.Title2;
        var stakeStyle = TextStyles.FootnoteEmphasized;
        var stakeText = stake > 0 ? RaceAmounts.Text(stake) : string.Empty;
        var oddsWidth = MathF.Min(card.Width * OddsShare,
            MathF.Max(Typography.Measure(oddsText, oddsStyle).X,
                stake > 0 ? RaceAmounts.Measure(stakeText, stakeStyle).X : 0f));
        var right = card.Max.X - pad;
        var oddsHeight = Typography.LineHeight(oddsStyle);
        var stakeHeight = stake > 0 ? Typography.LineHeight(stakeStyle) : 0f;
        var oddsTop = card.Center.Y - (oddsHeight + stakeHeight) * 0.5f;
        var fittedOdds = Typography.FitText(oddsText, oddsWidth, oddsStyle);
        var oddsSize = Typography.Measure(fittedOdds, oddsStyle);
        Typography.Draw(drawList, new Vector2(right - oddsSize.X, oddsTop), fittedOdds,
            CasinoColors.Money with { W = alpha }, oddsStyle);
        if (stake > 0)
        {
            RaceAmounts.DrawRight(drawList, right, oddsTop + oddsHeight + stakeHeight * 0.5f, stakeText,
                CasinoColors.MoneyHighlight with { W = alpha }, stakeStyle, alpha);
        }

        var textLeft = silkMin.X + silk + pad;
        var textWidth = MathF.Max(1f, right - oddsWidth - pad - textLeft);
        var nameStyle = TextStyles.Headline;
        var formStyle = TextStyles.Footnote;
        var nameHeight = Typography.LineHeight(nameStyle);
        var formHeight = Typography.LineHeight(formStyle);
        var blockTop = card.Center.Y - (nameHeight + formHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, blockTop), Typography.FitText(runner.Name, textWidth, nameStyle),
            CasinoColors.InkTitle with { W = alpha }, nameStyle);
        var star = StarSize * scale;
        var starsWidth = StarRow.Width(star);
        var formCenterY = blockTop + nameHeight + formHeight * 0.5f;
        if (starsWidth <= textWidth)
        {
            StarRow.Draw(drawList, new Vector2(textLeft + starsWidth * 0.5f, formCenterY), star, Stars(runner.Rating),
                StarEmpty, alpha);
        }

        var formLeft = textLeft + starsWidth + FormGap * scale;
        var formWidth = textLeft + textWidth - formLeft;
        if (formWidth > star)
        {
            Typography.Draw(drawList, new Vector2(formLeft, formCenterY - formHeight * 0.5f),
                Typography.FitText(texts.Form(runners, slot), formWidth, formStyle),
                CasinoColors.InkBody with { W = alpha }, formStyle);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return selectable && UiInteract.Click(card.Min, card.Max, hovered);
    }

    public static void DrawSilkBadge(ImDrawListPtr drawList, Vector2 min, float size, CasinoRaceRunnerDto runner,
        int slot, float alpha, float scale)
    {
        var max = min + new Vector2(size, size);
        var frame = SilkFrame * scale;
        Squircle.Fill(drawList, min - new Vector2(frame, frame), max + new Vector2(frame, frame), size * 0.3f,
            ImGui.GetColorU32(RaceBirdArt.PlumageOf(runner.Colour) with { W = alpha }));
        RaceBirdArt.DrawSilk(drawList, min, max, slot, runner.Silk, alpha);
        var center = (min + max) * 0.5f;
        var number = GameNumber.Label(slot + 1);
        var cloth = RaceBirdArt.ClothOf(slot);
        var ink = RaceBirdArt.InkOn(cloth);
        var style = size >= 34f * scale ? TextStyles.Headline : TextStyles.FootnoteEmphasized;
        Typography.DrawCentered(drawList, center + new Vector2(0f, MathF.Max(1f, scale)), number,
            RaceBirdArt.InkOn(ink) with { W = alpha * 0.6f }, style);
        Typography.DrawCentered(drawList, center, number, ink with { W = alpha }, style);
    }

    private static void DrawPickBadge(ImDrawListPtr drawList, AppSkin ui, Vector2 anchor, string label, float alpha,
        float scale)
    {
        var style = TextStyles.FootnoteEmphasized;
        var size = Typography.Measure(label, style);
        var height = size.Y + 2f * scale;
        var min = new Vector2(anchor.X - size.X * 0.5f - 4f * scale, anchor.Y - height * 0.5f);
        var max = new Vector2(anchor.X + size.X * 0.5f + 4f * scale, anchor.Y + height * 0.5f);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(ui.Palette.Accent with { W = alpha }));
        Typography.DrawCentered(drawList, (min + max) * 0.5f, label, new Vector4(1f, 1f, 1f, alpha), style);
    }
}
