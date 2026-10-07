using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Framework.Cards;

internal static class PileDraw
{
    public const int DiscardVisible = 4;
    private const float UnderAlpha = 0.85f;

    public static void Deck(ImDrawListPtr drawList, Vector2 center, float width, int count, Vector4 accent,
        float scale, float alpha = 1f)
    {
        if (count <= 0)
        {
            CardFace.DrawSlot(drawList, new CardPose(center, width), scale, alpha);
            return;
        }

        var layers = PileLayout.Layers(count);
        for (var layer = 0; layer < layers; layer++)
        {
            var shade = layer == layers - 1 ? 1f : UnderAlpha;
            CardFace.DrawBack(drawList, new CardPose(center + PileLayout.LayerOffset(layer, scale), width, 0f, false),
                accent, scale, alpha * shade);
        }
    }

    public static void Discard(ImDrawListPtr drawList, Vector2 center, float width, ReadOnlySpan<int> cards,
        ReadOnlySpan<CardDesign> designs, Vector4 accent, float scale, float alpha = 1f, int visible = DiscardVisible)
    {
        if (cards.Length == 0)
        {
            CardFace.DrawSlot(drawList, new CardPose(center, width), scale, alpha);
            return;
        }

        for (var index = Math.Max(0, cards.Length - visible); index < cards.Length; index++)
        {
            var card = cards[index];
            if (card < 0 || card >= designs.Length)
            {
                continue;
            }

            CardFace.Draw(drawList, PileLayout.Scatter(center, width, index), designs[card], accent, scale, alpha);
        }
    }
}
