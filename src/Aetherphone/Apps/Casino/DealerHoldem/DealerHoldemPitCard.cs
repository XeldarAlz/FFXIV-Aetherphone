using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.DealerHoldem;

internal sealed class DealerHoldemPitCard
{
    public const float Height = 84f;
    public const float Gap = 12f;

    private const float PuckRadius = 20f;
    private const float PadX = 16f;
    private const float PadY = 14f;
    private const float TextGap = 14f;
    private const float ChevronReserve = 24f;
    private const float BulbAlpha = 0.55f;

    private float phase;

    public static float TextWidth(float width, float scale) =>
        MathF.Max(1f, width - PadX * scale - PuckRadius * 2f * scale - TextGap * scale - ChevronReserve * scale);

    public static float HeightFor(float titleHeight, float hintHeight, float scale) =>
        MathF.Max(Height * scale, PadY * 2f * scale + titleHeight + hintHeight);

    public bool Draw(AppSkin ui, float scale)
    {
        phase += MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var textWidth = TextWidth(width, scale);
        var title = Loc.T(L.DealerHoldem.PlayAlone);
        var hint = Loc.T(L.DealerHoldem.PlayAloneHint);
        var titleBlock = Typography.MeasureWrappedBlock(title, TextStyles.Headline, textWidth);
        var hintBlock = Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, textWidth);
        var height = HeightFor(titleBlock.Y, hintBlock.Y, scale);
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var rounding = Metrics.Radius.Grouped * scale;
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        Squircle.FillVerticalGradient(drawList, rect.Min, rect.Max, rounding,
            ImGui.GetColorU32(CasinoColors.FeltTop), ImGui.GetColorU32(CasinoColors.FeltBottom));
        CasinoLights.BulbChase(drawList, rect, rounding, scale, phase, CasinoLights.BulbPitch, CasinoColors.Money,
            CasinoColors.LightA, BulbAlpha);
        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var radius = PuckRadius * scale;
        var puck = new Vector2(rect.Min.X + PadX * scale + radius, rect.Center.Y);
        BlackjackDealer.DrawPuck(drawList, puck, radius, phase, scale);
        var textLeft = puck.X + radius + TextGap * scale;
        var top = rect.Center.Y - (titleBlock.Y + hintBlock.Y) * 0.5f;
        Typography.DrawWrappedLeft(new Vector2(textLeft, top), title, CasinoColors.InkTitle, TextStyles.Headline,
            textWidth);
        Typography.DrawWrappedLeft(new Vector2(textLeft, top + titleBlock.Y), hint, CasinoColors.InkBody,
            TextStyles.Footnote, textWidth);
        CasinoArt.Chevron(drawList, new Vector2(rect.Max.X - ChevronReserve * scale * 0.6f, rect.Center.Y),
            CasinoColors.InkTitle);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Gap * scale));
        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }
}
