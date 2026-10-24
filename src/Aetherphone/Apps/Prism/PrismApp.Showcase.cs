using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const int ShowcaseSize = 8;
    private const float ShowcaseCardShare = 0.4f;
    private const float ShowcaseAdvanceSeconds = 3.5f;
    private const string ShowcaseRailId = "##prism.showcase";

    private readonly GlamDto[] showcase = new GlamDto[ShowcaseSize];
    private readonly StridePager showcasePager = new(loop: true);
    private int showcaseCount;
    private bool showcasePicked;

    private void ResetShowcase()
    {
        showcasePicked = false;
        showcaseCount = 0;
        showcasePager.Reset();
    }

    private float DrawShowcase(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        PickShowcase();
        DropFailedShowcase();
        if (showcaseCount == 0)
        {
            return top;
        }

        var inset = Metrics.Space.Lg * scale;
        var cardWidth = width * ShowcaseCardShare;
        var height = cardWidth / GlamStore.PhotoRatio;
        var stride = cardWidth + Metrics.Space.Sm * scale;
        var row = new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        showcasePager.Step(delta, showcaseCount);
        showcasePager.Drive(ShowcaseRailId, row, stride, showcaseCount, delta, scale);
        if (!ImGui.IsRectVisible(row.Min, row.Max))
        {
            return row.Max.Y;
        }

        showcasePager.AutoAdvance(row, showcaseCount, delta, ShowcaseAdvanceSeconds);
        var interactive = !showcasePager.Dragging;
        var value = showcasePager.Value;
        var looping = showcaseCount > 1;
        var first = looping ? (int)MathF.Floor(value) - 1 : 0;
        var span = looping ? (int)MathF.Ceiling(width / stride) + 2 : 0;
        drawList.PushClipRect(row.Min, row.Max, true);
        for (var slot = first; slot <= first + span; slot++)
        {
            var cardLeft = left + inset + (slot - value) * stride;
            if (cardLeft + cardWidth < row.Min.X || cardLeft > row.Max.X)
            {
                continue;
            }

            var glam = showcase[StridePager.CardIndex(slot, showcaseCount)];
            var card = new Rect(new Vector2(cardLeft, top), new Vector2(cardLeft + cardWidth, top + height));
            if (DrawShowcaseCard(drawList, card, glam, interactive, scale))
            {
                OpenGlam(glam);
            }
        }

        drawList.PopClipRect();
        return row.Max.Y;
    }

    private bool DrawShowcaseCard(ImDrawListPtr drawList, Rect card, GlamDto glam, bool interactive, float scale)
    {
        DrawLook(drawList, card, Cover(glam), false);
        var handle = AuthorLine(glam.Author);
        if (handle.Length > 0)
        {
            var line = Typography.LineHeight(PrismType.Caption);
            var inset = Metrics.Space.Sm * scale;
            PrismArt.Scrim(drawList, card, line + inset, scale);
            Typography.Draw(drawList, new Vector2(card.Min.X + inset, card.Max.Y - inset - line),
                Typography.FitText(handle, MathF.Max(1f, card.Width - inset * 2f), PrismType.Caption),
                PrismArt.PhotoFore, PrismType.Caption);
        }

        return interactive && UiInteract.Click(card.Min, card.Max, HoverWash(drawList, card));
    }

    private void PickShowcase()
    {
        if (showcasePicked || ShowcasePool() is not { Length: > 0 } pool)
        {
            return;
        }

        var eligible = 0;
        for (var index = 0; index < pool.Length; index++)
        {
            if (Cover(pool[index]).Length > 0)
            {
                eligible++;
            }
        }

        var wanted = Math.Min(ShowcaseSize, eligible);
        var count = 0;
        var remaining = eligible;
        for (var index = 0; index < pool.Length && count < wanted; index++)
        {
            if (Cover(pool[index]).Length == 0)
            {
                continue;
            }

            if (Random.Shared.Next(remaining) < wanted - count)
            {
                showcase[count] = pool[index];
                count++;
            }

            remaining--;
        }

        for (var index = count - 1; index > 0; index--)
        {
            var swap = Random.Shared.Next(index + 1);
            (showcase[index], showcase[swap]) = (showcase[swap], showcase[index]);
        }

        showcaseCount = count;
        showcasePicked = count > 0;
    }

    private GlamDto[]? ShowcasePool()
    {
        var popular = glams.Lane(GlamScope.Showcase);
        if (popular.Items.Length > 0)
        {
            return popular.Items;
        }

        return popular.Loading || popular.NeverLoaded ? null : glams.Lane(GlamScope.New).Items;
    }

    private void DropFailedShowcase()
    {
        var kept = 0;
        for (var index = 0; index < showcaseCount; index++)
        {
            if (images.Failed(Cover(showcase[index])))
            {
                continue;
            }

            showcase[kept] = showcase[index];
            kept++;
        }

        showcaseCount = kept;
    }
}
