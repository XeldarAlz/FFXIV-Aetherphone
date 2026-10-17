using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const float StatusMaxUnits = 40f;
    private const float StatusMinUnits = 24f;

    private void DrawEntry(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        if (store.Week is not { MyEntry: { } entry } week)
        {
            Go(PrismScreen.Cover);
            return;
        }

        var top = DrawHeader(drawList, content, string.Empty, IssueLine(week.Issue), true, scale);
        var gutter = PrismArt.Gutter(scale);
        var width = content.Width - gutter * 2f;
        var editable = week.Phase == PrismPhases.Entering;
        var strips = editable ? 2 : 0;
        var mono = PrismArt.MonoPixels();
        var status = PrismType.Upper(Loc.T(StatusName(entry.Status)));
        var pixels = PrismType.Fit(status, EditorialFace.Headline, width, StatusMaxUnits * scale,
            StatusMinUnits * scale, 2, out var lines);
        var hint = entry.Status == PrismPhases.StatusRejected
            ? entry.RejectReason
            : entry.Status == PrismPhases.StatusLive ? string.Empty : Loc.T(L.Prism.StatusReviewHint);
        var hintLines = hint.Length > 0 ? PrismType.Wrap(PrismType.Upper(hint), EditorialFace.Mono, mono, width) : [];
        var textHeight = lines.Length * PrismType.LineHeight(EditorialFace.Headline, pixels) +
                         hintLines.Length * PrismType.LineHeight(EditorialFace.Mono, mono) + Metrics.Space.Sm * scale;
        var stripsTop = strips > 0 ? StripRect(content, scale, strips - 1).Min.Y : content.Max.Y - gutter;
        var textTop = stripsTop - Metrics.Space.Lg * scale - textHeight;
        var photo = new Rect(new Vector2(content.Min.X + gutter, top),
            new Vector2(content.Max.X - gutter, textTop - Metrics.Space.Lg * scale));
        DrawLook(drawList, photo, store.Busy ? string.Empty : entry.ImageUrl, true);
        var left = content.Min.X + gutter;
        textTop += PrismType.DrawLines(drawList, new Vector2(left, textTop), lines, PrismArt.White,
            EditorialFace.Headline, pixels) + Metrics.Space.Sm * scale;
        PrismType.DrawLines(drawList, new Vector2(left, textTop), hintLines, PrismArt.Muted, EditorialFace.Mono, mono);
        if (!editable)
        {
            return;
        }

        if (PrismArt.Strip(drawList, StripRect(content, scale, 1), PrismType.Upper(Loc.T(L.Common.ChangePhoto)), "→",
                true, !store.Busy))
        {
            StartPick();
        }

        if (PrismArt.Strip(drawList, StripRect(content, scale), PrismType.Upper(Loc.T(L.Prism.Withdraw)),
                string.Empty, false, !store.Busy))
        {
            confirm.Ask(new ConfirmRequest
            {
                Title = Loc.T(L.Prism.WithdrawTitle),
                Message = Loc.T(L.Prism.WithdrawBody),
                ConfirmLabel = Loc.T(L.Prism.Withdraw),
                CancelLabel = Loc.T(L.Common.Cancel),
                Sheet = true,
                Confirm = withdraw,
            });
        }
    }
}
