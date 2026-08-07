using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Muster;

internal sealed partial class MusterApp
{
    private const float DataCenterRowHeight = 52f;

    private void DrawDataCenters(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Muster.DataCenterSection), back);
        var scale = UiScale.Current;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        var all = MusterDataCenters.All;
        var pinned = configuration.MusterDataCenterId;
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
            ui.HelpText(Loc.T(L.Muster.DataCenterHint));
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            var homeName = MusterDataCenters.Name(store.CurrentDataCenterId);
            if (DrawDataCenterRow(Loc.T(L.Muster.MyDataCenter), homeName, pinned == 0, true, scale))
            {
                PinDataCenter(0);
            }

            var region = 0;
            for (var index = 0; index < all.Length; index++)
            {
                var dataCenter = all[index];
                if (dataCenter.RegionBit != region)
                {
                    region = dataCenter.RegionBit;
                    ui.SectionHeading(Loc.T(MusterCategories.RegionLabel(region)), 10f);
                }

                if (DrawDataCenterRow(dataCenter.Name, string.Empty, pinned == dataCenter.Id, false, scale))
                {
                    PinDataCenter(dataCenter.Id);
                }
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private bool DrawDataCenterRow(string label, string detail, bool selected, bool home, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = DataCenterRowHeight * scale;
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var rounding = Metrics.Radius.Card * scale;
        var hovered = UiInteract.Hover(card.Min, card.Max);
        ui.Card(drawList, card.Min, card.Max, rounding, elevated: true);
        if (selected)
        {
            Squircle.Fill(drawList, card.Min, card.Max, rounding,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.16f)));
            Squircle.Stroke(drawList, card.Min, card.Max, rounding,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.55f)), 1.4f * scale);
        }

        var iconCenter = new Vector2(card.Min.X + 24f * scale, card.Center.Y);
        AppSkin.Icon(drawList, iconCenter,
            home ? FontAwesomeIcon.Home.ToIconString() : FontAwesomeIcon.Server.ToIconString(),
            selected ? ui.Accent : Palette.WithAlpha(AppPalettes.Muster.MutedInk, 0.9f), 0.7f);
        var textLeft = card.Min.X + 44f * scale;
        var hasDetail = detail.Length > 0;
        var labelTop = hasDetail ? card.Min.Y + 9f * scale : card.Center.Y - 10f * scale;
        Typography.Draw(drawList, new Vector2(textLeft, labelTop), label, AppPalettes.Muster.TitleInk,
            TextStyles.Headline);
        if (hasDetail)
        {
            Typography.Draw(drawList, new Vector2(textLeft, card.Min.Y + 29f * scale), detail,
                AppPalettes.Muster.MutedInk, TextStyles.Subheadline);
        }

        if (selected)
        {
            DrawSelectedTick(drawList, new Vector2(card.Max.X - 22f * scale, card.Center.Y), scale);
        }

        if (hovered)
        {
            UiInteract.HoverHighlight(drawList, card.Min, card.Max, rounding);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var clicked = UiInteract.Click(card.Min, card.Max, hovered);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
        return clicked;
    }

    private void DrawSelectedTick(ImDrawListPtr drawList, Vector2 center, float scale)
    {
        var color = ImGui.GetColorU32(ui.Accent);
        var thickness = 2f * scale;
        drawList.AddLine(center + new Vector2(-5f * scale, 0f), center + new Vector2(-1.5f * scale, 4f * scale),
            color, thickness);
        drawList.AddLine(center + new Vector2(-1.5f * scale, 4f * scale), center + new Vector2(5.5f * scale,
            -4.5f * scale), color, thickness);
    }

    private void PinDataCenter(int dataCenterId)
    {
        if (configuration.MusterDataCenterId != dataCenterId)
        {
            configuration.MusterDataCenterId = dataCenterId;
            configuration.MusterScope = MusterScopes.MyDataCenter;
            configuration.Save();
            store.RefreshDirectory();
        }

        router.Pop();
    }
}
