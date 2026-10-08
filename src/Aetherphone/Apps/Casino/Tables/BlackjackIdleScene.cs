using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Tables;

internal static class BlackjackIdleScene
{
    private const float CardWidthShare = 0.13f;
    private const float SpotRowFraction = 0.74f;
    private const float DealerRowFraction = 0.34f;
    private const float PuckFraction = 0.12f;
    private const float SpotSpreadShare = 0.30f;
    private const float LayerOffset = 0.42f;

    public static void Draw(ImDrawListPtr drawList, Rect rect, BlackjackIdleScript script, float deltaSeconds,
        float scale)
    {
        script.Advance(deltaSeconds);
        drawList.PushClipRect(rect.Min, rect.Max, true);
        FeltTable.Draw(drawList, rect, scale);
        var cardWidth = MathF.Max(12f * scale, rect.Width * CardWidthShare);
        var radius = MathF.Max(8f * scale, cardWidth * 0.45f);
        var puck = new Vector2(rect.Center.X, rect.Min.Y + rect.Height * PuckFraction + radius * 0.3f);
        BlackjackDealer.DrawPuck(drawList, puck, radius, 0f, scale);
        var shoe = new Vector2(rect.Max.X - cardWidth, rect.Min.Y + cardWidth);
        BlackjackTableArt.DrawShoe(drawList, shoe, scale);
        var spotY = rect.Min.Y + rect.Height * SpotRowFraction;
        var ring = ImGui.GetColorU32(CasinoColors.Money with { W = 0.45f });
        for (var spot = 0; spot < BlackjackIdleScript.Spots; spot++)
        {
            drawList.AddCircle(SpotCenter(rect, spot, spotY), cardWidth * 0.75f, ring, 32, MathF.Max(1f, scale));
        }

        var fade = 1f - script.Sweep;
        var rounding = PlayingCards.RoundingFor(cardWidth);
        var leadTravel = 2f;
        var lead = shoe;
        for (var index = 0; index < BlackjackIdleScript.CardsPerRound; index++)
        {
            var travel = script.TravelOf(index);
            if (travel <= 0f)
            {
                continue;
            }

            var target = TargetOf(rect, index, spotY, cardWidth);
            var center = BlackjackDealChoreography.Position(shoe, target, travel, scale);
            if (travel < 1f && travel < leadTravel)
            {
                leadTravel = travel;
                lead = center;
            }

            var cardRect = BlackjackDealChoreography.CardRect(center, cardWidth, travel);
            var card = script.CardAt(index);
            if (fade < 1f)
            {
                var shift = new Vector2(0f, -script.Sweep * cardWidth);
                cardRect = new Rect(cardRect.Min + shift, cardRect.Max + shift);
            }

            if (fade <= 0.02f)
            {
                continue;
            }

            if (BlackjackDealChoreography.FaceUp(travel) && script.HoleShown(index) && PlayingCards.IsCard(card))
            {
                PlayingCards.DrawFace(drawList, cardRect, card, rounding, scale, true);
            }
            else
            {
                PlayingCards.DrawBack(drawList, cardRect, rounding, scale, true);
            }
        }

        BlackjackDealer.DrawGlove(drawList, leadTravel <= 1f ? lead : shoe + new Vector2(-cardWidth, cardWidth * 0.4f),
            shoe, scale);
        drawList.PopClipRect();
    }

    private static Vector2 SpotCenter(Rect rect, int spot, float spotY)
    {
        var spread = rect.Width * SpotSpreadShare;
        var x = rect.Center.X + (spot - (BlackjackIdleScript.Spots - 1) * 0.5f) * spread;
        var lift = MathF.Abs(spot - (BlackjackIdleScript.Spots - 1) * 0.5f) * rect.Height * 0.06f;
        return new Vector2(x, spotY - lift);
    }

    private static Vector2 TargetOf(Rect rect, int index, float spotY, float cardWidth)
    {
        var layer = BlackjackIdleScript.LayerOf(index);
        var offset = new Vector2(layer * cardWidth * LayerOffset, 0f);
        if (BlackjackIdleScript.IsDealerCard(index))
        {
            return new Vector2(rect.Center.X - cardWidth * 0.2f, rect.Min.Y + rect.Height * DealerRowFraction)
                + offset;
        }

        return SpotCenter(rect, BlackjackIdleScript.TargetOf(index), spotY) + offset
            - new Vector2(cardWidth * 0.2f, 0f);
    }
}
