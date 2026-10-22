using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Glamour;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const float GearIconUnits = 32f;

    private GlamGearDto[] wornOutfit = Array.Empty<GlamGearDto>();
    private PrismScreen outfitReturn = PrismScreen.Cover;

    private static bool HasOutfit(GlamGearDto[]? outfit) => outfit is { Length: > 0 };

    private void OpenOutfit()
    {
        wornOutfit = glamour.Capture();
        outfitReturn = screen;
        Go(PrismScreen.Outfit);
    }

    private void OpenGlam(PrismPlacingDto placing)
    {
        var outfit = placing.Outfit ?? Array.Empty<GlamGearDto>();
        OpenGlam(new GlamDto(
            Id: placing.GlamId,
            Author: new GlamAuthorDto(DisplayName: placing.DisplayName, Handle: placing.Handle),
            Title: store.Results?.Title ?? string.Empty,
            Photos: new[] { new GlamPhotoDto(placing.ImageUrl, placing.Width, placing.Height) },
            Gear: outfit,
            Vanilla: GlamourService.IsVanilla(outfit),
            WeekId: store.Results?.WeekId ?? string.Empty,
            Placing: placing.Rank));
    }

    private void TapForOutfit(Vector2 min, Vector2 max, PrismPlacingDto placing)
    {
        if (!HasOutfit(placing.Outfit) && placing.GlamId.Length == 0)
        {
            return;
        }

        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(min, max, hovered))
        {
            OpenGlam(placing);
        }
    }

    private void DrawOutfit(ImDrawListPtr drawList, Rect content, float scale)
    {
        var top = DrawHeader(drawList, content, string.Empty, string.Empty, true, scale);
        var entry = store.Week is { Phase: PrismPhases.Entering, MyEntry: { } mine } ? mine : null;
        var shared = entry is null ? configuration.PrismShareOutfit : HasOutfit(entry.Outfit);
        var strips = shared && entry is not null ? 2 : 1;
        var body = new Rect(new Vector2(content.Min.X, top),
            new Vector2(content.Max.X, StripRect(content, scale, strips - 1).Min.Y - Metrics.Space.Lg * scale));
        using (BeginBody(body))
        {
            var list = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var gutter = PrismArt.Gutter(scale);
            var width = ScrollLayout.StableContentWidth() - gutter * 2f;
            var left = origin.X + gutter;
            var cursor = DrawSectionTitle(list, left, origin.Y, width, Loc.T(L.Prism.OutfitTitle), scale);
            cursor += Typography.DrawWrappedLeft(new Vector2(left, cursor), Loc.T(L.Prism.OutfitBody), PrismArt.Muted,
                TextStyles.Subheadline, width) + Metrics.Space.Xl * scale;
            cursor = DrawStillTryingOn(list, left, cursor, width, scale);
            if (RecaptureDue())
            {
                wornOutfit = glamour.Capture();
            }

            if (wornOutfit.Length == 0)
            {
                PrismType.Draw(list, new Vector2(left, cursor), Loc.T(L.Prism.NothingEquipped), PrismArt.Muted,
                    PrismFace.Label, PrismArt.LabelPixels());
                cursor += Metrics.Size.TapTarget * scale;
            }

            cursor = DrawGearList(list, left, cursor, width, wornOutfit, false, scale);
            ImGui.Dummy(new Vector2(width, cursor - origin.Y));
        }

        var canShare = wornOutfit.Length > 0 && !store.Busy && recaptureAt <= 0d;
        if (!shared)
        {
            if (PrismArt.Strip(drawList, StripRect(content, scale), Loc.T(L.Prism.ShareOutfit), Arrow, true, canShare))
            {
                ShareOutfit(true, wornOutfit);
            }

            return;
        }

        if (entry is not null &&
            PrismArt.Strip(drawList, StripRect(content, scale, 1), Loc.T(L.Prism.UpdateOutfit), Arrow, true, canShare))
        {
            ShareOutfit(true, wornOutfit);
        }

        if (PrismArt.Strip(drawList, StripRect(content, scale), Loc.T(L.Prism.StopSharingOutfit), string.Empty, false,
                !store.Busy))
        {
            ShareOutfit(false, null);
        }
    }

    private void ShareOutfit(bool share, GlamGearDto[]? outfit)
    {
        configuration.PrismShareOutfit = share;
        configuration.Save();
        if (store.Week is { Phase: PrismPhases.Entering, MyEntry: not null })
        {
            store.ShareOutfit(outfit);
        }

        Go(outfitReturn, -1);
    }
}
