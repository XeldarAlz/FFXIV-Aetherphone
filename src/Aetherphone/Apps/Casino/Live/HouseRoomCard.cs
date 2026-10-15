using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Live;

internal static class HouseRoomCard
{
    private const float PadX = 14f;
    private const float PadY = 12f;
    private const float Radius = 16f;
    private const float LineGap = 3f;
    private const float ColumnGap = 12f;
    private const float StrokeAlpha = 0.06f;
    private const float EmptySeatAlpha = 0.16f;
    private const float DotsGap = 7f;
    private const float RimAlpha = 0.45f;
    private const float DimAlpha = 0.45f;

    public static float Height(in HouseRoomRow row, float width, float scale) =>
        PadY * 2f * scale + MathF.Max(Button.RegularHeight * scale,
            TextHeight(row, TextWidth(row, width, scale), scale));

    public static bool Draw(ImDrawListPtr drawList, AppSkin skin, in HouseRoomRow row, Vector2 origin, float width,
        string actionId, bool enabled, bool overlay, float scale)
    {
        var height = Height(row, width, scale);
        var max = new Vector2(origin.X + width, origin.Y + height);
        var radius = Radius * scale;
        Squircle.Fill(drawList, origin, max, radius, ImGui.GetColorU32(StageText.CapsuleFill));
        Squircle.Stroke(drawList, origin, max, radius,
            ImGui.GetColorU32(skin.TitleInk with { W = StrokeAlpha }), MathF.Max(1f, scale));
        if (row.Rim)
        {
            Squircle.Stroke(drawList, origin, max, radius,
                ImGui.GetColorU32(CasinoColors.Money with { W = RimAlpha }), MathF.Max(1f, scale));
        }

        var centerY = origin.Y + height * 0.5f;
        var padX = PadX * scale;
        var actionWidth = ActionWidth(row);
        var actionHeight = Button.RegularHeight * scale;
        var action = new Rect(new Vector2(max.X - padX - actionWidth, centerY - actionHeight * 0.5f),
            new Vector2(max.X - padX, centerY + actionHeight * 0.5f));
        var left = origin.X + padX;
        var textWidth = TextWidth(row, width, scale);
        var top = centerY - TextHeight(row, textWidth, scale) * 0.5f;
        var fade = row.Dim ? DimAlpha : 1f;
        top = PitText.Draw(drawList, row.Name, TextStyles.Headline,
            CasinoColors.Money with { W = CasinoColors.Money.W * fade }, left, top, textWidth, false);
        top = PitText.Draw(drawList, row.Detail, TextStyles.FootnoteEmphasized, skin.TitleInk, left,
            top + LineGap * scale, textWidth, false);
        top = PitText.Draw(drawList, row.Note, TextStyles.Footnote, skin.BodyInk, left, top, textWidth, false);
        CasinoArt.SeatDots(drawList, new Vector2(left, top + DotsGap * scale + DotsHeight(scale) * 0.5f), row.Seated,
            row.MaxSeats, skin.Accent with { W = skin.Accent.W * fade },
            skin.TitleInk with { W = EmptySeatAlpha * fade }, scale);
        return Button.Draw(drawList, action, row.Action, skin.Ink, row.Seats ? ButtonStyle.Prominent : ButtonStyle.Gray,
            enabled: enabled, overlay: overlay, id: actionId);
    }

    private static float ActionWidth(in HouseRoomRow row) => Button.WidthFor(row.Action, ButtonSize.Regular);

    private static float TextWidth(in HouseRoomRow row, float width, float scale) =>
        MathF.Max(1f, width - PadX * 2f * scale - ActionWidth(row) - ColumnGap * scale);

    private static float DotsHeight(float scale) => CasinoArt.SeatDotRadius * 2f * scale;

    private static float TextHeight(in HouseRoomRow row, float textWidth, float scale) =>
        PitText.Height(row.Name, TextStyles.Headline, textWidth) + LineGap * scale
        + PitText.Height(row.Detail, TextStyles.FootnoteEmphasized, textWidth)
        + PitText.Height(row.Note, TextStyles.Footnote, textWidth) + DotsGap * scale + DotsHeight(scale);
}
